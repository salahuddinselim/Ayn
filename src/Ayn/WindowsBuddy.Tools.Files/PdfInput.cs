using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Files;

internal sealed class PdfInput : IDisposable
{
	public sealed class PdfException : Exception
	{
		public PdfException(string m)
			: base(m)
		{
		}
	}

	private static readonly Type TStorageFile;

	private static readonly Type TPdfDocument;

	private static readonly Type TRenderOptions;

	private static readonly Type TMemStream;

	private static readonly MethodInfo AsTaskOp;

	private static readonly MethodInfo AsTaskAction;

	private static readonly MethodInfo AsStream;

	public static readonly bool Available;

	private object _doc;

	private static double _dipScale;

	public int PageCount { get; private set; }

	static PdfInput()
	{
		_dipScale = 1.0;
		try
		{
			TStorageFile = WinRt("Windows.Storage.StorageFile", "Windows.Storage");
			TPdfDocument = WinRt("Windows.Data.Pdf.PdfDocument", "Windows.Data");
			TRenderOptions = WinRt("Windows.Data.Pdf.PdfPageRenderOptions", "Windows.Data");
			TMemStream = WinRt("Windows.Storage.Streams.InMemoryRandomAccessStream", "Windows.Storage");
			Assembly assembly = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			MethodInfo[] methods = assembly.GetType("System.WindowsRuntimeSystemExtensions").GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (!(methodInfo.Name != "AsTask") && methodInfo.GetParameters().Length == 1)
				{
					string name = methodInfo.GetParameters()[0].ParameterType.Name;
					if (methodInfo.IsGenericMethod && name.StartsWith("IAsyncOperation`"))
					{
						AsTaskOp = methodInfo;
					}
					else if (!methodInfo.IsGenericMethod && name == "IAsyncAction")
					{
						AsTaskAction = methodInfo;
					}
				}
			}
			MethodInfo[] methods2 = assembly.GetType("System.IO.WindowsRuntimeStreamExtensions").GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo2 in methods2)
			{
				if (methodInfo2.Name == "AsStream" && methodInfo2.GetParameters().Length == 1 && methodInfo2.GetParameters()[0].ParameterType.Name == "IRandomAccessStream")
				{
					AsStream = methodInfo2;
				}
			}
			Available = TStorageFile != null && TPdfDocument != null && TRenderOptions != null && TMemStream != null && AsTaskOp != null && AsTaskAction != null && AsStream != null;
		}
		catch (Exception ex)
		{
			Available = false;
			TransactionLogger.Warn("Converter: Windows.Data.Pdf unavailable: " + ex.Message);
		}
	}

	private static Type WinRt(string name, string contract)
	{
		return Type.GetType(name + ", " + contract + ", ContentType=WindowsRuntime") ?? Type.GetType(name + ", Windows, ContentType=WindowsRuntime");
	}

	private PdfInput(object doc)
	{
		_doc = doc;
		PageCount = Convert.ToInt32(Get(doc, "PageCount"));
	}

	public static PdfInput Open(string path)
	{
		ConvertEngine.CheckInputSize(path);
		if (!Available)
		{
			throw new PdfException("PDF input not supported");
		}
		object obj;
		try
		{
			obj = Await(TStorageFile.GetMethod("GetFileFromPathAsync").Invoke(null, new object[1] { path }), TStorageFile);
		}
		catch (Exception ex)
		{
			throw Unwrap(ex);
		}
		object obj2;
		try
		{
			MethodInfo methodInfo = TPdfDocument.GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo m) => m.Name == "LoadFromFileAsync" && m.GetParameters().Length == 1);
			obj2 = Await(methodInfo.Invoke(null, new object[1] { obj }), TPdfDocument);
		}
		catch (Exception ex2)
		{
			Exception ex3 = Unwrap(ex2);
			if (ex3.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				throw new PdfException("PDF is password-protected");
			}
			throw new PdfException("Can't open this PDF");
		}
		if (obj2 == null)
		{
			throw new PdfException("Can't open this PDF");
		}
		PdfInput pdfInput = new PdfInput(obj2);
		if (pdfInput.PageCount < 1 || pdfInput.PageCount > 200)
		{
			pdfInput.Dispose();
			throw new PdfException("PDF must contain 1 to 200 pages");
		}
		return pdfInput;
	}

	public void PageSizeAt(int index, int longEdge, out int w, out int h)
	{
		object obj = Call(_doc, "GetPage", (uint)index);
		try
		{
			PageSize(obj, longEdge, out w, out h);
		}
		finally
		{
			if (obj is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}
	}

	private static void PageSize(object page, int longEdge, out int w, out int h)
	{
		object obj = Get(page, "Size");
		double num = Convert.ToDouble(obj.GetType().GetProperty("Width").GetValue(obj, null));
		double num2 = Convert.ToDouble(obj.GetType().GetProperty("Height").GetValue(obj, null));
		if (double.IsNaN(num) || double.IsInfinity(num) || double.IsNaN(num2) || double.IsInfinity(num2) || num <= 0.0 || num2 <= 0.0 || num * num2 * Math.Pow(1.5625, 2.0) > 120000000.0)
		{
			throw new PdfException("PDF page exceeds the 120 megapixel limit");
		}
		double num3 = 1.5625;
		if (longEdge > 0)
		{
			num3 = Math.Min(num3, (double)longEdge / Math.Max(1.0, Math.Max(num, num2)));
		}
		w = Math.Max(1, (int)Math.Round(num * num3));
		h = Math.Max(1, (int)Math.Round(num2 * num3));
	}

	public byte[] Render(int index, int longEdge)
	{
		object obj = Call(_doc, "GetPage", (uint)index);
		try
		{
			PageSize(obj, longEdge, out var w, out var h);
			int num = 0;
			byte[] array;
			while (true)
			{
				double dipScale = _dipScale;
				array = RenderAt(obj, Math.Max(1, (int)Math.Round((double)w / dipScale)), Math.Max(1, (int)Math.Round((double)h / dipScale)));
				BitmapFrame bitmapFrame = BitmapDecoder.Create(new MemoryStream(array), BitmapCreateOptions.None, BitmapCacheOption.None).Frames[0];
				if (Math.Abs(bitmapFrame.PixelWidth - w) <= 2 || num > 0)
				{
					break;
				}
				_dipScale = dipScale * (double)bitmapFrame.PixelWidth / (double)w;
				num++;
			}
			return array;
		}
		catch (Exception ex)
		{
			Exception ex2 = Unwrap(ex);
			if (ex2 is PdfException)
			{
				throw ex2;
			}
			throw new PdfException("Can't render PDF page");
		}
		finally
		{
			if (obj is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}
	}

	private static byte[] RenderAt(object page, int w, int h)
	{
		ConvertEngine.CheckPixels(w, h);
		object obj = Activator.CreateInstance(TRenderOptions);
		TRenderOptions.GetProperty("DestinationWidth").SetValue(obj, (uint)w, null);
		TRenderOptions.GetProperty("DestinationHeight").SetValue(obj, (uint)h, null);
		object obj2 = Activator.CreateInstance(TMemStream);
		MethodInfo methodInfo = FindMethod(page, "RenderToStreamAsync", 2);
		object obj3 = methodInfo.Invoke(page, new object[2] { obj2, obj });
		Task task = (Task)AsTaskAction.Invoke(null, new object[1] { obj3 });
		if (!task.Wait(30000))
		{
			throw new PdfException("PDF page timed out");
		}
		using Stream stream = (Stream)AsStream.Invoke(null, new object[1] { obj2 });
		using MemoryStream memoryStream = new MemoryStream();
		stream.Position = 0L;
		stream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	public void Dispose()
	{
		object obj = Interlocked.Exchange(ref _doc, null);
		if (obj != null)
		{
			if (obj is IDisposable disposable)
			{
				disposable.Dispose();
			}
			else if (Marshal.IsComObject(obj))
			{
				Marshal.FinalReleaseComObject(obj);
			}
		}
	}

	private static object Await(object op, Type result)
	{
		Task task = (Task)AsTaskOp.MakeGenericMethod(result).Invoke(null, new object[1] { op });
		if (!task.Wait(30000))
		{
			throw new PdfException("PDF timed out");
		}
		return task.GetType().GetProperty("Result").GetValue(task, null);
	}

	private static Exception Unwrap(Exception ex)
	{
		while ((ex is TargetInvocationException || ex is AggregateException) && ex.InnerException != null)
		{
			ex = ex.InnerException;
		}
		return ex;
	}

	private static MethodInfo FindMethod(object o, string name, int args)
	{
		Type type = o.GetType();
		MethodInfo methodInfo = type.GetMethods().FirstOrDefault((MethodInfo x) => x.Name == name && x.GetParameters().Length == args);
		if (methodInfo != null)
		{
			return methodInfo;
		}
		Type[] interfaces = type.GetInterfaces();
		foreach (Type type2 in interfaces)
		{
			methodInfo = type2.GetMethods().FirstOrDefault((MethodInfo x) => x.Name == name && x.GetParameters().Length == args);
			if (methodInfo != null)
			{
				return methodInfo;
			}
		}
		throw new MissingMethodException(type.Name, name);
	}

	private static object Call(object o, string name, params object[] args)
	{
		return FindMethod(o, name, args.Length).Invoke(o, args);
	}

	private static object Get(object o, string name)
	{
		Type type = o.GetType();
		PropertyInfo property = type.GetProperty(name);
		if (property == null)
		{
			Type[] interfaces = type.GetInterfaces();
			foreach (Type type2 in interfaces)
			{
				property = type2.GetProperty(name);
				if (property != null)
				{
					break;
				}
			}
		}
		if (property == null)
		{
			throw new MissingMemberException(type.Name, name);
		}
		return property.GetValue(o, null);
	}
}
