using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.FileShelf;

public static class ShelfDataImport
{
	private sealed class ComStreamReader : Stream
	{
		private readonly IStream _s;

		private readonly IntPtr _read = Marshal.AllocHGlobal(4);

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public override long Position
		{
			get
			{
				throw new NotSupportedException();
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public ComStreamReader(IStream s)
		{
			_s = s;
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			byte[] array = ((offset == 0) ? buffer : new byte[count]);
			Marshal.WriteInt32(_read, 0);
			_s.Read(array, count, _read);
			int num = Marshal.ReadInt32(_read);
			if (offset != 0 && num > 0)
			{
				Buffer.BlockCopy(array, 0, buffer, offset, num);
			}
			return num;
		}

		protected override void Dispose(bool disposing)
		{
			Marshal.FreeHGlobal(_read);
			base.Dispose(disposing);
		}
	}

	private const string Descriptor = "FileGroupDescriptorW";

	private const string Contents = "FileContents";

	private const int FD_ATTRIBUTES = 4;

	private const int FILE_ATTRIBUTE_DIRECTORY = 16;

	private const int DescriptorSize = 592;

	public static bool CanImport(System.Windows.IDataObject d)
	{
		if (d == null)
		{
			return false;
		}
		try
		{
			return d.GetDataPresent(DataFormats.UnicodeText) || d.GetDataPresent(DataFormats.FileDrop) || d.GetDataPresent("FileGroupDescriptorW") || d.GetDataPresent("PNG") || d.GetDataPresent(DataFormats.Bitmap);
		}
		catch
		{
			return false;
		}
	}

	public static int Import(System.Windows.IDataObject d, FileShelfService svc, string shelfId)
	{
		if (Application.Current != null && Application.Current.Dispatcher.CheckAccess())
		{
			throw new InvalidOperationException("Use ImportAsync on the dispatcher.");
		}
		return ImportAsync(d, svc, shelfId).GetAwaiter().GetResult();
	}

	public static async Task<int> ImportAsync(System.Windows.IDataObject d, FileShelfService svc, string shelfId)
	{
		if (d == null)
		{
			return 0;
		}
		if (string.IsNullOrEmpty(shelfId))
		{
			shelfId = svc.ActiveShelf.Id;
		}
		try
		{
			if (d.GetDataPresent("Ayn.ClipboardDrag") && d.GetData("Ayn.ClipboardDrag") as string == "image" && d.GetData(DataFormats.FileDrop) is string[] imagePaths && imagePaths.Length > 0)
			{
				await svc.CopyFileAsync(shelfId, imagePaths[0]);
				return 1;
			}
			if (d.GetDataPresent(DataFormats.FileDrop) && d.GetData(DataFormats.FileDrop) is string[] array && array.Length > 0)
			{
				return svc.AddFilesTo(shelfId, array, owned: false);
			}
			if (d.GetDataPresent("FileGroupDescriptorW"))
			{
				return await ReadVirtualFiles(d, svc, shelfId);
			}
			if (d.GetDataPresent(DataFormats.UnicodeText))
			{
				string text = d.GetData(DataFormats.UnicodeText) as string;
				using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(text ?? "")))
				{
					await svc.ImportStreamAsync(shelfId, "Clipboard text " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".txt", stream);
				}
				return 1;
			}
			BitmapSource image = ReadImage(d);
			if (image != null)
			{
				return (await svc.ImportImageAsync(shelfId, image) != null) ? 1 : 0;
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: import failed: " + ex.Message);
		}
		return 0;
	}

	private static BitmapSource ReadImage(System.Windows.IDataObject d)
	{
		try
		{
			if (d.GetDataPresent("PNG") && d.GetData("PNG") is MemoryStream memoryStream)
			{
				memoryStream.Position = 0L;
				BitmapDecoder bitmapDecoder = BitmapDecoder.Create(memoryStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
				if (bitmapDecoder.Frames.Count > 0)
				{
					return bitmapDecoder.Frames[0];
				}
			}
		}
		catch
		{
		}
		try
		{
			if (d.GetDataPresent(DataFormats.Bitmap))
			{
				return d.GetData(DataFormats.Bitmap) as BitmapSource;
			}
		}
		catch
		{
		}
		return null;
	}

	[DllImport("ole32.dll")]
	private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

	[DllImport("kernel32.dll")]
	private static extern IntPtr GlobalLock(IntPtr h);

	[DllImport("kernel32.dll")]
	private static extern bool GlobalUnlock(IntPtr h);

	[DllImport("kernel32.dll")]
	private static extern UIntPtr GlobalSize(IntPtr h);

	private static async Task<int> ReadVirtualFiles(System.Windows.IDataObject d, FileShelfService svc, string shelfId)
	{
		int stored = 0;
		if (!(d.GetData("FileGroupDescriptorW") is MemoryStream ms))
		{
			return stored;
		}
		byte[] buf = ms.ToArray();
		if (buf.Length < 4)
		{
			return stored;
		}
		int count = BitConverter.ToInt32(buf, 0);
		System.Runtime.InteropServices.ComTypes.IDataObject com = d as System.Runtime.InteropServices.ComTypes.IDataObject;
		for (int i = 0; i < count && 4 + (i + 1) * 592 <= buf.Length; i++)
		{
			if (!svc.Operations.AdmissionOpen)
			{
				TransactionLogger.Warn("Shelf: remaining virtual batch items were not admitted during shutdown.");
				break;
			}
			int o = 4 + i * 592;
			int flags = BitConverter.ToInt32(buf, o);
			int attrs = BitConverter.ToInt32(buf, o + 36);
			if (((uint)flags & 4u) != 0 && ((uint)attrs & 0x10u) != 0)
			{
				continue;
			}
			string name = Encoding.Unicode.GetString(buf, o + 72, 520);
			int nul = name.IndexOf('\0');
			if (nul >= 0)
			{
				name = name.Substring(0, nul);
			}
			int slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
			if (slash >= 0)
			{
				name = name.Substring(slash + 1);
			}
			string path = null;
			bool nativeAcquired = false;
			if (com != null)
			{
				path = await ReadContent(com, i, name, svc, shelfId, delegate
				{
					nativeAcquired = true;
				});
			}
			if (!nativeAcquired && path == null && i == 0 && d.GetData("FileContents") is Stream s)
			{
				path = await svc.ImportStreamAsync(shelfId, name, s);
			}
			if (path != null)
			{
				stored++;
			}
		}
		return stored;
	}

	private static async Task<string> ReadContent(System.Runtime.InteropServices.ComTypes.IDataObject com, int index, string name, FileShelfService svc, string shelfId, Action acquired)
	{
		FORMATETC fmt = new FORMATETC
		{
			cfFormat = (short)DataFormats.GetDataFormat("FileContents").Id,
			dwAspect = DVASPECT.DVASPECT_CONTENT,
			lindex = index,
			ptd = IntPtr.Zero,
			tymed = (TYMED.TYMED_HGLOBAL | TYMED.TYMED_ISTREAM)
		};
		STGMEDIUM medium;
		try
		{
			com.GetData(ref fmt, out medium);
		}
		catch
		{
			return null;
		}
		try
		{
			if (medium.tymed == TYMED.TYMED_ISTREAM && medium.unionmember != IntPtr.Zero)
			{
				IStream stream = (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
				try
				{
					acquired();
					using ComStreamReader managed = new ComStreamReader(stream);
					return await svc.ImportStreamAsync(shelfId, name, managed);
				}
				finally
				{
					Marshal.ReleaseComObject(stream);
				}
			}
			if (medium.tymed == TYMED.TYMED_HGLOBAL && medium.unionmember != IntPtr.Zero)
			{
				IntPtr p = GlobalLock(medium.unionmember);
				if (p == IntPtr.Zero)
				{
					return null;
				}
				try
				{
					long size = (long)GlobalSize(medium.unionmember).ToUInt64();
					byte[] bytes = new byte[size];
					Marshal.Copy(p, bytes, 0, (int)size);
					acquired();
					using MemoryStream mem = new MemoryStream(bytes);
					return await svc.ImportStreamAsync(shelfId, name, mem);
				}
				finally
				{
					GlobalUnlock(medium.unionmember);
				}
			}
			return null;
		}
		finally
		{
			try
			{
				ReleaseStgMedium(ref medium);
			}
			catch
			{
			}
		}
	}
}
