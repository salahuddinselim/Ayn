using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Tools.Drop;

namespace WindowsBuddy.Tools.Clipboard;

public static class ClipboardDrag
{
	private struct NativePoint
	{
		public int X;

		public int Y;
	}

	[Guid("00000121-0000-0000-C000-000000000046")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComVisible(true)]
	public interface IDropSource
	{
		[PreserveSig]
		int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape, uint keys);

		[PreserveSig]
		int GiveFeedback(uint effect);
	}

	[ClassInterface(ClassInterfaceType.None)]
	[ComVisible(true)]
	private sealed class DragSource : IDropSource
	{
		private readonly Window preview;

		public DragSource(Window window)
		{
			preview = window;
		}

		public int QueryContinueDrag(bool escape, uint keys)
		{
			if (!escape)
			{
				if ((keys & (true ? 1u : 0u)) != 0)
				{
					return 0;
				}
				return 262400;
			}
			return 262401;
		}

		public int GiveFeedback(uint effect)
		{
			if (GetCursorPos(out var point))
			{
				System.Windows.Point point2 = PresentationSource.FromVisual(preview)?.CompositionTarget.TransformFromDevice.Transform(new System.Windows.Point(point.X, point.Y)) ?? new System.Windows.Point(point.X, point.Y);
				preview.Left = point2.X + 18.0;
				preview.Top = point2.Y + 18.0;
			}
			return 262402;
		}
	}

	[ComVisible(true)]
	[ClassInterface(ClassInterfaceType.None)]
	public sealed class DragData : System.Runtime.InteropServices.ComTypes.IDataObject
	{
		public readonly DataObject Managed;

		public readonly string VirtualFile;

		private readonly System.Runtime.InteropServices.ComTypes.IDataObject inner;

		private readonly short contents = (short)DataFormats.GetDataFormat("FileContents").Id;

		public DragData(DataObject data, string file)
		{
			Managed = data;
			VirtualFile = file;
			inner = data;
		}

		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		private static extern int SHCreateStreamOnFileEx(string path, uint mode, uint attributes, bool create, IntPtr template, out IStream stream);

		public void GetData(ref FORMATETC format, out STGMEDIUM medium)
		{
			if (format.cfFormat == contents && VirtualFile != null)
			{
				if (format.lindex != 0 && format.lindex != -1)
				{
					throw new COMException("Invalid file index", -2147221400);
				}
				if ((format.tymed & TYMED.TYMED_ISTREAM) == 0)
				{
					throw new COMException("Stream required", -2147221399);
				}
				Marshal.ThrowExceptionForHR(SHCreateStreamOnFileEx(VirtualFile, 64u, 0u, create: false, IntPtr.Zero, out var stream));
				medium = new STGMEDIUM
				{
					tymed = TYMED.TYMED_ISTREAM,
					unionmember = Marshal.GetComInterfaceForObject(stream, typeof(IStream))
				};
				Marshal.ReleaseComObject(stream);
			}
			else
			{
				inner.GetData(ref format, out medium);
			}
		}

		public int QueryGetData(ref FORMATETC format)
		{
			if (format.cfFormat == contents && VirtualFile != null)
			{
				if ((format.lindex != 0 && format.lindex != -1) || (format.tymed & TYMED.TYMED_ISTREAM) == 0)
				{
					return -2147221399;
				}
				return 0;
			}
			return inner.QueryGetData(ref format);
		}

		public void GetDataHere(ref FORMATETC f, ref STGMEDIUM m)
		{
			inner.GetDataHere(ref f, ref m);
		}

		public int GetCanonicalFormatEtc(ref FORMATETC a, out FORMATETC b)
		{
			return inner.GetCanonicalFormatEtc(ref a, out b);
		}

		public void SetData(ref FORMATETC f, ref STGMEDIUM m, bool release)
		{
			inner.SetData(ref f, ref m, release);
		}

		public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
		{
			return new FormatEnumerator(inner.EnumFormatEtc(direction), contents, VirtualFile != null);
		}

		public int DAdvise(ref FORMATETC f, ADVF flags, IAdviseSink sink, out int connection)
		{
			return inner.DAdvise(ref f, flags, sink, out connection);
		}

		public void DUnadvise(int connection)
		{
			inner.DUnadvise(connection);
		}

		public int EnumDAdvise(out IEnumSTATDATA data)
		{
			return inner.EnumDAdvise(out data);
		}
	}

	private sealed class FormatEnumerator : IEnumFORMATETC
	{
		private readonly IEnumFORMATETC inner;

		private readonly short contents;

		private readonly bool file;

		public FormatEnumerator(IEnumFORMATETC source, short format, bool hasFile)
		{
			inner = source;
			contents = format;
			file = hasFile;
		}

		public int Next(int count, FORMATETC[] formats, int[] fetched)
		{
			int result = inner.Next(count, formats, fetched);
			for (int i = 0; i < ((fetched == null) ? count : fetched[0]); i++)
			{
				if (file && formats[i].cfFormat == contents)
				{
					formats[i].tymed = TYMED.TYMED_ISTREAM;
					formats[i].lindex = 0;
				}
			}
			return result;
		}

		public int Skip(int count)
		{
			return inner.Skip(count);
		}

		public int Reset()
		{
			return inner.Reset();
		}

		public void Clone(out IEnumFORMATETC clone)
		{
			inner.Clone(out var newEnum);
			clone = new FormatEnumerator(newEnum, contents, file);
		}
	}

	public const string Marker = "Ayn.ClipboardDrag";

	private static readonly List<string> temporary = new List<string>();

	private static DispatcherTimer cleanup;

	public static bool Active { get; private set; }

	public static string TempRoot => Path.Combine(Path.GetTempPath(), "Ayn", "ClipboardDrag");

	public static bool PastThreshold(System.Windows.Point start, System.Windows.Point current)
	{
		return (current - start).Length >= 6.0;
	}

	public static void Cleanup(DateTime now)
	{
		if (!Directory.Exists(TempRoot))
		{
			return;
		}
		string[] files = Directory.GetFiles(TempRoot, "ayn-drag-*.png");
		foreach (string path in files)
		{
			try
			{
				if (now - File.GetLastWriteTimeUtc(path) >= TimeSpan.FromMinutes(10.0))
				{
					File.Delete(path);
				}
			}
			catch
			{
			}
		}
	}

	public static DragData CreateData(ClipboardItem item)
	{
		DataObject dataObject = new DataObject();
		string text = null;
		dataObject.SetData("Ayn.ClipboardDrag", (item.Kind == ClipKind.Image) ? "image" : "text");
		if (item.Kind == ClipKind.Files)
		{
			string[] array = (item.FilePaths ?? new List<string>()).Where((string p) => File.Exists(p) || Directory.Exists(p)).ToArray();
			if (array.Length == 0)
			{
				throw new IOException("Source files unavailable");
			}
			dataObject.SetData(DataFormats.FileDrop, array);
			dataObject.SetData("Ayn.ClipboardDrag", "files");
		}
		else if (item.Kind == ClipKind.Image)
		{
			Directory.CreateDirectory(TempRoot);
			Cleanup(DateTime.UtcNow);
			text = Path.Combine(TempRoot, "ayn-drag-" + Guid.NewGuid().ToString("N") + ".png");
			File.Copy(item.ImagePath, text, overwrite: false);
			File.SetLastWriteTimeUtc(text, DateTime.UtcNow);
			temporary.Add(text);
			if (cleanup == null)
			{
				DispatcherTimer dispatcherTimer = new DispatcherTimer();
				dispatcherTimer.Interval = TimeSpan.FromSeconds(30.0);
				cleanup = dispatcherTimer;
				cleanup.Tick += delegate
				{
					Cleanup(DateTime.UtcNow);
					temporary.RemoveAll((string p) => !File.Exists(p));
					if (temporary.Count == 0)
					{
						cleanup.Stop();
					}
				};
			}
			cleanup.Start();
			byte[] buffer = File.ReadAllBytes(text);
			dataObject.SetData("PNG", new MemoryStream(buffer), autoConvert: false);
			BitmapImage bitmapImage = new BitmapImage();
			using (MemoryStream streamSource = new MemoryStream(buffer))
			{
				bitmapImage.BeginInit();
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.StreamSource = streamSource;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
			}
			dataObject.SetData(DataFormats.Bitmap, bitmapImage);
			using (MemoryStream stream = new MemoryStream(buffer))
			{
				using Bitmap bitmap = new Bitmap(stream);
				using MemoryStream memoryStream = new MemoryStream();
				bitmap.Save(memoryStream, ImageFormat.Bmp);
				byte[] array2 = memoryStream.ToArray();
				dataObject.SetData(DataFormats.Dib, new MemoryStream(array2, 14, array2.Length - 14), autoConvert: false);
			}
			dataObject.SetData(DataFormats.FileDrop, new string[1] { text });
			dataObject.SetData("FileGroupDescriptorW", new MemoryStream(Descriptor(text)), autoConvert: false);
			dataObject.SetData("FileContents", new MemoryStream(new byte[0]), autoConvert: false);
		}
		else
		{
			dataObject.SetData(DataFormats.UnicodeText, item.Text ?? "");
			if (!string.IsNullOrEmpty(item.Html))
			{
				dataObject.SetData(DataFormats.Html, item.Html, autoConvert: false);
			}
			if (!string.IsNullOrEmpty(item.Rtf))
			{
				dataObject.SetData(DataFormats.Rtf, item.Rtf, autoConvert: false);
			}
			if (item.Kind == ClipKind.Link)
			{
				dataObject.SetData("UniformResourceLocatorW", new MemoryStream(Encoding.Unicode.GetBytes((item.Text ?? "") + "\0")), autoConvert: false);
			}
		}
		dataObject.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)), autoConvert: false);
		return new DragData(dataObject, text);
	}

	public static byte[] Descriptor(string file)
	{
		byte[] array = new byte[596];
		using MemoryStream memoryStream = new MemoryStream(array);
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(1);
		binaryWriter.Write(-2147483580);
		memoryStream.Position = 40L;
		binaryWriter.Write(128);
		long length = new FileInfo(file).Length;
		memoryStream.Position = 68L;
		binaryWriter.Write((uint)(length >> 32));
		binaryWriter.Write((uint)length);
		memoryStream.Position = 76L;
		binaryWriter.Write(Encoding.Unicode.GetBytes(Path.GetFileName(file)));
		return array;
	}

	public static bool CanDrop(System.Windows.IDataObject data)
	{
		if (data != null)
		{
			if (!data.GetDataPresent(DataFormats.FileDrop))
			{
				return data.GetDataPresent(DataFormats.UnicodeText);
			}
			return true;
		}
		return false;
	}

	public static void ToDrop(System.Windows.IDataObject data)
	{
		if (data.GetDataPresent(DataFormats.FileDrop))
		{
			DropTool.SendFiles((string[])data.GetData(DataFormats.FileDrop));
		}
		else if (data.GetDataPresent(DataFormats.UnicodeText))
		{
			DropTool.SendText(data.GetData(DataFormats.UnicodeText) as string);
		}
	}

	public static void Start(FrameworkElement row, ClipboardItem item)
	{
		if (Active || item == null)
		{
			return;
		}
		Window preview = null;
		Transform renderTransform = row.RenderTransform;
		double opacity = row.Opacity;
		try
		{
			DragData dragData = CreateData(item);
			Active = true;
			TranslateTransform translateTransform = (TranslateTransform)(row.RenderTransform = new TranslateTransform());
			row.Opacity = 0.7;
			translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-3.0, TimeSpan.FromMilliseconds(120.0)));
			Border border = new Border();
			border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 36));
			border.CornerRadius = new CornerRadius(12.0);
			border.Padding = new Thickness(14.0);
			border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 90));
			border.BorderThickness = new Thickness(1.0);
			border.MaxWidth = 260.0;
			Border border2 = border;
			if (item.Kind == ClipKind.Image)
			{
				BitmapImage bitmapImage = new BitmapImage();
				using (FileStream streamSource = File.OpenRead(dragData.VirtualFile))
				{
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.StreamSource = streamSource;
					bitmapImage.EndInit();
					bitmapImage.Freeze();
				}
				border2.Child = new System.Windows.Controls.Image
				{
					Source = bitmapImage,
					Width = 160.0,
					Height = 100.0,
					Stretch = Stretch.Uniform
				};
			}
			else
			{
				border2.Child = new TextBlock
				{
					Text = ((item.Kind == ClipKind.Files) ? Path.GetFileName(item.FilePaths[0]) : (item.Text ?? "").Substring(0, Math.Min(150, (item.Text ?? "").Length))),
					Foreground = System.Windows.Media.Brushes.White,
					FontSize = 13.0,
					TextWrapping = TextWrapping.Wrap,
					MaxHeight = 90.0
				};
			}
			preview = new Window
			{
				Content = border2,
				SizeToContent = SizeToContent.WidthAndHeight,
				WindowStyle = WindowStyle.None,
				AllowsTransparency = true,
				Background = System.Windows.Media.Brushes.Transparent,
				Opacity = 0.7,
				ShowInTaskbar = false,
				ShowActivated = false,
				Topmost = true,
				IsHitTestVisible = false
			};
			preview.SourceInitialized += delegate
			{
				IntPtr handle = new WindowInteropHelper(preview).Handle;
				SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x8000000 | 0x20 | 0x80);
			};
			preview.Show();
			DragSource source = new DragSource(preview);
			int effect;
			int num = DoDragDrop(dragData, source, 1, out effect);
			if (num < 0)
			{
				Marshal.ThrowExceptionForHR(num);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Clipboard drag failed: " + ex.GetType().Name);
		}
		finally
		{
			if (preview != null)
			{
				preview.Close();
			}
			row.RenderTransform = renderTransform;
			row.Opacity = opacity;
			Active = false;
			if (CapsuleWindow.Instance != null)
			{
				CapsuleWindow.Instance.Machine.HoldReleased("clipboard drag finished");
			}
		}
	}

	[DllImport("ole32.dll")]
	private static extern int DoDragDrop([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject data, [MarshalAs(UnmanagedType.Interface)] IDropSource source, int effects, out int effect);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr window, int index);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr window, int index, int value);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out NativePoint point);
}
