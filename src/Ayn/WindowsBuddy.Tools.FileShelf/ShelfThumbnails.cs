using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.FileShelf;

public static class ShelfThumbnails
{
	public sealed class Result
	{
		public BitmapSource Image;

		public bool IsThumbnail;
	}

	private sealed class Request
	{
		public string Key;

		public string Path;

		public string Ext;

		public bool WantThumbnail;

		public bool Missing;

		public bool IsDirectory;

		public int Pixels;

		public Func<bool> StillWanted;

		public Action<Result> Done;

		public Dispatcher Dispatcher;

		public FileShelfService Owner;

		public FileShelfService.CacheLease Lease;
	}

	private sealed class Worker
	{
		private readonly string _name;

		private readonly LinkedList<Request> _queue = new LinkedList<Request>();

		private Thread _thread;

		public Worker(string name)
		{
			_name = name;
		}

		public void Enqueue(Request r)
		{
			List<Request> list = null;
			lock (_queue)
			{
				if (_queue.Count >= 64)
				{
					LinkedListNode<Request> linkedListNode = _queue.Last;
					while (linkedListNode != null)
					{
						LinkedListNode<Request> previous = linkedListNode.Previous;
						Request value = linkedListNode.Value;
						if (!value.Lease.IsActive || (value.StillWanted != null && !value.StillWanted()))
						{
							_queue.Remove(linkedListNode);
							if (list == null)
							{
								list = new List<Request>();
							}
							list.Add(value);
						}
						linkedListNode = previous;
					}
				}
				_queue.AddFirst(r);
				if (_thread == null)
				{
					_thread = new Thread(Run)
					{
						IsBackground = true,
						Name = _name,
						Priority = ThreadPriority.BelowNormal
					};
					_thread.SetApartmentState(ApartmentState.STA);
					_thread.Start();
				}
				Monitor.Pulse(_queue);
			}
			if (list == null)
			{
				return;
			}
			foreach (Request item in list)
			{
				item.Owner.FinishCacheOperation(item.Lease, success: false);
			}
		}

		private void Run()
		{
			while (true)
			{
				Request value;
				lock (_queue)
				{
					while (_queue.Count == 0)
					{
						Monitor.Wait(_queue);
					}
					value = _queue.First.Value;
					_queue.RemoveFirst();
				}
				bool flag = false;
				try
				{
					if (!value.Lease.IsActive || (value.StillWanted != null && !value.StillWanted()))
					{
						continue;
					}
					Result res = Lookup(value.Key) ?? FitImage(Build(value), value.Pixels);
					if (res == null)
					{
						continue;
					}
					Store(value.Key, res);
					if (!value.Lease.IsActive || (value.StillWanted != null && !value.StillWanted()))
					{
						continue;
					}
					Request req = value;
					req.Dispatcher.BeginInvoke((Action)delegate
					{
						try
						{
							if (req.Lease.IsActive && (req.StillWanted == null || req.StillWanted()))
							{
								req.Done(res);
							}
						}
						finally
						{
							req.Owner.FinishCacheOperation(req.Lease, success: true);
						}
					}, DispatcherPriority.Background);
					flag = true;
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Shelf thumbnail failed: " + ex.Message);
				}
				finally
				{
					if (!flag)
					{
						value.Owner.FinishCacheOperation(value.Lease, success: false);
					}
				}
			}
		}
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	private interface IShellItemImageFactory
	{
		[PreserveSig]
		int GetImage([In][MarshalAs(UnmanagedType.Struct)] NativeSize size, [In] int flags, out IntPtr phbm);
	}

	private struct NativeSize
	{
		public int cx;

		public int cy;
	}

	private struct BITMAP
	{
		public int bmType;

		public int bmWidth;

		public int bmHeight;

		public int bmWidthBytes;

		public ushort bmPlanes;

		public ushort bmBitsPixel;

		public IntPtr bmBits;
	}

	private struct BITMAPINFOHEADER
	{
		public uint biSize;

		public int biWidth;

		public int biHeight;

		public ushort biPlanes;

		public ushort biBitCount;

		public uint biCompression;

		public uint biSizeImage;

		public int biXPelsPerMeter;

		public int biYPelsPerMeter;

		public uint biClrUsed;

		public uint biClrImportant;
	}

	private struct DIBSECTION
	{
		public BITMAP dsBm;

		public BITMAPINFOHEADER dsBmih;

		public uint dsBitfield0;

		public uint dsBitfield1;

		public uint dsBitfield2;

		public IntPtr dshSection;

		public uint dsOffset;
	}

	private const int MaxEntries = 600;

	private const int SIIGBF_BIGGERSIZEOK = 1;

	private const int SIIGBF_ICONONLY = 4;

	private const int SIIGBF_THUMBNAILONLY = 8;

	private const int SIIGBF_CROPTOSQUARE = 32;

	private static readonly object CacheLock = new object();

	private static readonly Dictionary<string, LinkedListNode<KeyValuePair<string, Result>>> Cache = new Dictionary<string, LinkedListNode<KeyValuePair<string, Result>>>(StringComparer.OrdinalIgnoreCase);

	private static readonly LinkedList<KeyValuePair<string, Result>> Lru = new LinkedList<KeyValuePair<string, Result>>();

	private static readonly Worker Local = new Worker("Shelf thumbnails");

	private static readonly Worker Network = new Worker("Shelf thumbnails (network)");

	private static readonly HashSet<string> ImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"png", "jpg", "jpeg", "jfif", "gif", "bmp", "webp", "tif", "tiff", "heic",
		"heif", "avif", "ico", "dng", "cr2", "nef", "arw", "svg", "psd"
	};

	private static readonly HashSet<string> VideoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"mp4", "mov", "m4v", "avi", "mkv", "webm", "wmv", "mpg", "mpeg", "3gp",
		"mts", "m2ts"
	};

	private static readonly HashSet<string> OwnIconExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "exe", "lnk", "ico", "url", "appref-ms", "msc", "cpl", "scr", "msi" };

	private static Func<string, int, BitmapSource> _provider;

	public static bool IsVideo(string ext)
	{
		if (ext != null)
		{
			return VideoExts.Contains(ext);
		}
		return false;
	}

	internal static bool HasPreview(string ext)
	{
		if (ext != null)
		{
			if (!ImageExts.Contains(ext))
			{
				return VideoExts.Contains(ext);
			}
			return true;
		}
		return false;
	}

	public static Result GetCached(ParkedFile f, int pixels)
	{
		return Lookup("t|" + f.FullPath + "|" + f.LastWriteTicks + "|" + f.FileSizeBytes + "|" + pixels) ?? Lookup("i|" + f.FullPath + "|" + f.LastWriteTicks + "|" + pixels) ?? Lookup("e|" + (f.IsDirectory ? "<dir>" : f.Extension) + "|" + pixels);
	}

	public static Result Get(ParkedFile f, int pixels, Func<bool> stillWanted, Action<Result> done, Dispatcher dispatcher, FileShelfService owner = null, bool deferPreview = false)
	{
		owner = owner ?? FileShelfService.Instance;
		if (!owner.Operations.AdmissionOpen)
		{
			return null;
		}
		string text = f.Extension ?? string.Empty;
		bool flag = f.IsMissing || (!f.IsProbed && f.IsNetworkPath);
		bool flag2 = !flag && !f.IsDirectory && (ImageExts.Contains(text) || VideoExts.Contains(text));
		string key = (flag2 ? ("t|" + f.FullPath + "|" + f.LastWriteTicks + "|" + f.FileSizeBytes + "|" + pixels) : (flag ? ("e|" + (f.IsDirectory ? "<dir>" : text) + "|" + pixels) : ((!f.IsDirectory && !OwnIconExts.Contains(text)) ? ("e|" + text + "|" + pixels) : ("i|" + f.FullPath + "|" + f.LastWriteTicks + "|" + pixels))));
		Result result = Lookup(key);
		if (result != null)
		{
			return result;
		}
		if (flag2 && deferPreview)
		{
			flag2 = false;
			flag = true;
			key = "e|" + text + "|" + pixels;
			result = Lookup(key);
			if (result != null)
			{
				return result;
			}
		}
		FileShelfService.CacheLease cacheLease = owner.BeginCacheOperation();
		if (cacheLease == null)
		{
			return null;
		}
		Request request = new Request();
		request.Key = key;
		request.Path = f.FullPath;
		request.Ext = text;
		request.WantThumbnail = flag2;
		request.Missing = flag;
		request.IsDirectory = f.IsDirectory;
		request.Pixels = pixels;
		request.StillWanted = stillWanted;
		request.Done = done;
		request.Dispatcher = dispatcher;
		request.Owner = owner;
		request.Lease = cacheLease;
		Request r = request;
		((f.IsNetworkPath && !flag) ? Network : Local).Enqueue(r);
		return null;
	}

	private static Result Lookup(string key)
	{
		lock (CacheLock)
		{
			if (!Cache.TryGetValue(key, out var value))
			{
				return null;
			}
			Lru.Remove(value);
			Lru.AddLast(value);
			return value.Value.Value;
		}
	}

	private static void Store(string key, Result r)
	{
		lock (CacheLock)
		{
			if (!Cache.ContainsKey(key))
			{
				Cache[key] = Lru.AddLast(new KeyValuePair<string, Result>(key, r));
				while (Cache.Count > 600 && Lru.First != null)
				{
					Cache.Remove(Lru.First.Value.Key);
					Lru.RemoveFirst();
				}
			}
		}
	}

	private static Result FitImage(Result result, int pixels)
	{
		if (result == null || result.Image == null)
		{
			return result;
		}
		BitmapSource image = result.Image;
		if (image.PixelWidth <= pixels && image.PixelHeight <= pixels)
		{
			return result;
		}
		double num = Math.Min((double)pixels / (double)image.PixelWidth, (double)pixels / (double)image.PixelHeight);
		TransformedBitmap source = new TransformedBitmap(image, new ScaleTransform(num, num));
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0.0);
		int pixelWidth = formatConvertedBitmap.PixelWidth;
		int pixelHeight = formatConvertedBitmap.PixelHeight;
		int num2 = pixelWidth * 4;
		byte[] pixels2 = new byte[num2 * pixelHeight];
		formatConvertedBitmap.CopyPixels(pixels2, num2, 0);
		BitmapSource bitmapSource = BitmapSource.Create(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Pbgra32, null, pixels2, num2);
		bitmapSource.Freeze();
		Result result2 = new Result();
		result2.Image = bitmapSource;
		result2.IsThumbnail = result.IsThumbnail;
		return result2;
	}

	private static Result Build(Request r)
	{
		if (_provider != null)
		{
			BitmapSource bitmapSource = _provider(r.Path, r.Pixels);
			if (bitmapSource != null)
			{
				Result result = new Result();
				result.Image = bitmapSource;
				result.IsThumbnail = true;
				return result;
			}
			return null;
		}
		if (!r.Lease.IsActive)
		{
			return null;
		}
		BitmapSource bitmapSource2 = null;
		if (!r.Missing)
		{
			if (r.WantThumbnail)
			{
				bitmapSource2 = ShellImage(r.Path, r.Pixels, 40);
				if (bitmapSource2 != null)
				{
					Result result2 = new Result();
					result2.Image = bitmapSource2;
					result2.IsThumbnail = true;
					return result2;
				}
				if (!OwnIconExts.Contains(r.Ext))
				{
					string key = "e|" + r.Ext + "|" + r.Pixels;
					Result result3 = Lookup(key);
					if (result3 != null)
					{
						return result3;
					}
					if (!r.Lease.IsActive)
					{
						return null;
					}
					BitmapSource bitmapSource3 = TypeIcon(r.Ext, dir: false, r.Pixels);
					if (bitmapSource3 != null)
					{
						Result result4 = new Result();
						result4.Image = bitmapSource3;
						result4.IsThumbnail = false;
						result3 = result4;
						Store(key, result3);
						return Lookup(key) ?? result3;
					}
				}
			}
			bitmapSource2 = ShellImage(r.Path, r.Pixels, 4);
		}
		if (bitmapSource2 == null && r.Lease.IsActive)
		{
			bitmapSource2 = TypeIcon(r.Ext, r.IsDirectory, r.Pixels);
		}
		if (bitmapSource2 != null)
		{
			Result result5 = new Result();
			result5.Image = bitmapSource2;
			result5.IsThumbnail = false;
			return result5;
		}
		return null;
	}

	private static BitmapSource TypeIcon(string ext, bool dir, int pixels)
	{
		try
		{
			string text = Path.Combine(AppPaths.DataDir, "cache", "shelf-icons");
			Directory.CreateDirectory(text);
			if (dir)
			{
				return ShellImage(text, pixels, 4);
			}
			string text2 = new string((ext ?? string.Empty).Where((char c) => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
			string path = Path.Combine(text, (text2.Length > 0) ? ("file." + text2) : "file");
			if (!File.Exists(path))
			{
				PersistenceHelper.WriteCacheBytes(path, new byte[0]);
			}
			return ShellImage(path, pixels, 4);
		}
		catch
		{
			return null;
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

	[DllImport("gdi32.dll")]
	private static extern int GetObject(IntPtr h, int size, ref DIBSECTION ds);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr h);

	private static BitmapSource ShellImage(string path, int pixels, int flags)
	{
		IShellItemImageFactory ppv = null;
		IntPtr phbm = IntPtr.Zero;
		try
		{
			if (SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out ppv) != 0 || ppv == null)
			{
				return null;
			}
			if (ppv.GetImage(new NativeSize
			{
				cx = pixels,
				cy = pixels
			}, flags, out phbm) != 0 || phbm == IntPtr.Zero)
			{
				return null;
			}
			return FromHBitmap(phbm);
		}
		catch
		{
			return null;
		}
		finally
		{
			if (phbm != IntPtr.Zero)
			{
				DeleteObject(phbm);
			}
			if (ppv != null)
			{
				try
				{
					Marshal.ReleaseComObject(ppv);
				}
				catch
				{
				}
			}
		}
	}

	private static BitmapSource FromHBitmap(IntPtr hbm)
	{
		DIBSECTION ds = default(DIBSECTION);
		if (GetObject(hbm, Marshal.SizeOf(typeof(DIBSECTION)), ref ds) == 0 || ds.dsBm.bmBits == IntPtr.Zero || ds.dsBm.bmBitsPixel != 32)
		{
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bitmapSource.Freeze();
			return bitmapSource;
		}
		int bmWidth = ds.dsBm.bmWidth;
		int num = Math.Abs(ds.dsBm.bmHeight);
		int bmWidthBytes = ds.dsBm.bmWidthBytes;
		byte[] array = new byte[bmWidthBytes * num];
		Marshal.Copy(ds.dsBm.bmBits, array, 0, array.Length);
		if (ds.dsBmih.biHeight > 0)
		{
			byte[] array2 = new byte[bmWidthBytes];
			for (int i = 0; i < num / 2; i++)
			{
				int num2 = i * bmWidthBytes;
				int num3 = (num - 1 - i) * bmWidthBytes;
				Buffer.BlockCopy(array, num2, array2, 0, bmWidthBytes);
				Buffer.BlockCopy(array, num3, array, num2, bmWidthBytes);
				Buffer.BlockCopy(array2, 0, array, num3, bmWidthBytes);
			}
		}
		bool flag = false;
		for (int j = 3; j < array.Length; j += 4)
		{
			if (array[j] != 0)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			for (int k = 3; k < array.Length; k += 4)
			{
				array[k] = byte.MaxValue;
			}
		}
		BitmapSource bitmapSource2 = BitmapSource.Create(bmWidth, num, 96.0, 96.0, PixelFormats.Pbgra32, null, array, bmWidthBytes);
		bitmapSource2.Freeze();
		return bitmapSource2;
	}
}
