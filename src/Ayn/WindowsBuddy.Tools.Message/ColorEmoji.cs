using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Message;

public static class ColorEmoji
{
	private struct ColorF
	{
		public float R;

		public float G;

		public float B;

		public float A;
	}

	private struct RectF
	{
		public float L;

		public float T;

		public float R;

		public float B;
	}

	private struct RectI
	{
		public int L;

		public int T;

		public int R;

		public int B;
	}

	private struct RtProps
	{
		public int Type;

		public int Format;

		public int Alpha;

		public float DpiX;

		public float DpiY;

		public int Usage;

		public int MinLevel;
	}

	private struct BitmapInfoHeader
	{
		public int Size;

		public int Width;

		public int Height;

		public short Planes;

		public short BitCount;

		public int Compression;

		public int SizeImage;

		public int XPels;

		public int YPels;

		public int ClrUsed;

		public int ClrImportant;
	}

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int CreateDcRtFn(IntPtr self, ref RtProps props, out IntPtr rt);

	[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	private delegate int CreateTextFormatFn(IntPtr self, string family, IntPtr collection, int weight, int style, int stretch, float size, string locale, out IntPtr format);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int IntArgFn(IntPtr self, int value);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int CreateBrushFn(IntPtr self, ref ColorF color, IntPtr props, out IntPtr brush);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate void SetModeFn(IntPtr self, int mode);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int BindDcFn(IntPtr self, IntPtr hdc, ref RectI rect);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate void VoidFn(IntPtr self);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate void ClearFn(IntPtr self, ref ColorF color);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int EndDrawFn(IntPtr self, IntPtr tag1, IntPtr tag2);

	[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	private delegate void DrawTextFn(IntPtr self, string text, int length, IntPtr format, ref RectF rect, IntPtr brush, int options, int measuring);

	private const int Px = 72;

	private static readonly Dictionary<string, BitmapSource> _cache = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);

	private static bool _failed;

	private static IntPtr _d2d;

	private static IntPtr _dwrite;

	private static IntPtr _format;

	private static IntPtr _rt;

	private static IntPtr _brush;

	private static IntPtr _dc;

	private static IntPtr _dib;

	private static IntPtr _bits;

	private static IntPtr _oldBmp;

	private static DrawTextFn _drawText;

	private static BindDcFn _bindDc;

	private static VoidFn _beginDraw;

	private static ClearFn _clear;

	private static EndDrawFn _endDraw;

	private static readonly object _cacheLock = new object();

	private static readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);

	private static BlockingCollection<string> _jobs;

	private static int _revision;

	public static int Revision => Interlocked.CompareExchange(ref _revision, 0, 0);

	public static event EventHandler Rendered;

	public static BitmapSource Get(string emoji)
	{
		if (string.IsNullOrEmpty(emoji))
		{
			return null;
		}
		lock (_cacheLock)
		{
			if (_cache.TryGetValue(emoji, out var value))
			{
				return value;
			}
			if (_failed || !_pending.Add(emoji))
			{
				return null;
			}
			if (_jobs == null)
			{
				_jobs = new BlockingCollection<string>(256);
				Thread thread = new Thread(RenderJobs);
				thread.IsBackground = true;
				thread.Name = "Ayn emoji renderer";
				Thread thread2 = thread;
				thread2.SetApartmentState(ApartmentState.STA);
				thread2.Start();
			}
			if (!_jobs.TryAdd(emoji))
			{
				_pending.Remove(emoji);
			}
		}
		return null;
	}

	private static void RenderJobs()
	{
		try
		{
			using (new MessageTiming("emoji initialization"))
			{
				Init();
			}
			foreach (string item in _jobs.GetConsumingEnumerable())
			{
				BitmapSource value = null;
				try
				{
					using (new MessageTiming("emoji rendering"))
					{
						value = Render(item);
					}
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Colour emoji glyph unavailable: " + ex.GetType().Name);
				}
				lock (_cacheLock)
				{
					if (_cache.Count >= 512)
					{
						_cache.Clear();
					}
					_cache[item] = value;
					_pending.Remove(item);
					Interlocked.Increment(ref _revision);
				}
				EventHandler rendered = ColorEmoji.Rendered;
				if (rendered == null)
				{
					continue;
				}
				Delegate[] invocationList = rendered.GetInvocationList();
				for (int i = 0; i < invocationList.Length; i++)
				{
					EventHandler eventHandler = (EventHandler)invocationList[i];
					try
					{
						eventHandler(null, EventArgs.Empty);
					}
					catch
					{
					}
				}
			}
		}
		catch (Exception ex2)
		{
			lock (_cacheLock)
			{
				_failed = true;
				_pending.Clear();
			}
			TransactionLogger.Warn("Colour emoji renderer unavailable: " + ex2.GetType().Name);
		}
	}

	[DllImport("d2d1.dll")]
	private static extern int D2D1CreateFactory(int type, ref Guid riid, IntPtr options, out IntPtr factory);

	[DllImport("dwrite.dll")]
	private static extern int DWriteCreateFactory(int type, ref Guid riid, out IntPtr factory);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

	[DllImport("gdi32.dll")]
	private static extern bool GdiFlush();

	private static T Slot<T>(IntPtr com, int index) where T : class
	{
		IntPtr ptr = Marshal.ReadIntPtr(com);
		return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(ptr, index * IntPtr.Size), typeof(T)) as T;
	}

	private static void Check(int hr, string what)
	{
		if (hr < 0)
		{
			throw new InvalidOperationException(what + " failed (0x" + hr.ToString("X8") + ")");
		}
	}

	private static void Init()
	{
		Guid riid = new Guid("06152247-6f50-465a-9245-118bfd3b6007");
		Guid riid2 = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
		Check(D2D1CreateFactory(0, ref riid, IntPtr.Zero, out _d2d), "D2D1CreateFactory");
		Check(DWriteCreateFactory(0, ref riid2, out _dwrite), "DWriteCreateFactory");
		Check(Slot<CreateTextFormatFn>(_dwrite, 15)(_dwrite, "Segoe UI Emoji", IntPtr.Zero, 400, 0, 5, 51.840004f, "en-us", out _format), "CreateTextFormat");
		Slot<IntArgFn>(_format, 3)(_format, 2);
		Slot<IntArgFn>(_format, 4)(_format, 2);
		Slot<IntArgFn>(_format, 5)(_format, 1);
		RtProps rtProps = default(RtProps);
		rtProps.Type = 0;
		rtProps.Format = 87;
		rtProps.Alpha = 1;
		rtProps.DpiX = 96f;
		rtProps.DpiY = 96f;
		RtProps props = rtProps;
		Check(Slot<CreateDcRtFn>(_d2d, 16)(_d2d, ref props, out _rt), "CreateDCRenderTarget");
		ColorF colorF = default(ColorF);
		colorF.R = 0.95f;
		colorF.G = 0.95f;
		colorF.B = 0.97f;
		colorF.A = 1f;
		ColorF color = colorF;
		Check(Slot<CreateBrushFn>(_rt, 8)(_rt, ref color, IntPtr.Zero, out _brush), "CreateSolidColorBrush");
		Slot<SetModeFn>(_rt, 34)(_rt, 2);
		_dc = CreateCompatibleDC(IntPtr.Zero);
		BitmapInfoHeader bitmapInfoHeader = default(BitmapInfoHeader);
		bitmapInfoHeader.Size = 40;
		bitmapInfoHeader.Width = 72;
		bitmapInfoHeader.Height = -72;
		bitmapInfoHeader.Planes = 1;
		bitmapInfoHeader.BitCount = 32;
		BitmapInfoHeader bmi = bitmapInfoHeader;
		_dib = CreateDIBSection(_dc, ref bmi, 0u, out _bits, IntPtr.Zero, 0u);
		if (_dc == IntPtr.Zero || _dib == IntPtr.Zero)
		{
			throw new InvalidOperationException("DIB section");
		}
		_oldBmp = SelectObject(_dc, _dib);
		_drawText = Slot<DrawTextFn>(_rt, 27);
		_clear = Slot<ClearFn>(_rt, 47);
		_beginDraw = Slot<VoidFn>(_rt, 48);
		_endDraw = Slot<EndDrawFn>(_rt, 49);
		_bindDc = Slot<BindDcFn>(_rt, 57);
	}

	private static BitmapSource Render(string emoji)
	{
		RectI rectI = default(RectI);
		rectI.R = 72;
		rectI.B = 72;
		RectI rect = rectI;
		Check(_bindDc(_rt, _dc, ref rect), "BindDC");
		_beginDraw(_rt);
		ColorF color = default(ColorF);
		_clear(_rt, ref color);
		RectF rectF = default(RectF);
		rectF.R = 72f;
		rectF.B = 72f;
		RectF rect2 = rectF;
		_drawText(_rt, emoji, emoji.Length, _format, ref rect2, _brush, 4, 0);
		Check(_endDraw(_rt, IntPtr.Zero, IntPtr.Zero), "EndDraw");
		GdiFlush();
		byte[] array = new byte[20736];
		Marshal.Copy(_bits, array, 0, array.Length);
		int num = 72;
		int num2 = 72;
		int num3 = -1;
		int num4 = -1;
		for (int i = 0; i < 72; i++)
		{
			for (int j = 0; j < 72; j++)
			{
				if (array[(i * 72 + j) * 4 + 3] > 8)
				{
					if (j < num)
					{
						num = j;
					}
					if (j > num3)
					{
						num3 = j;
					}
					if (i < num2)
					{
						num2 = i;
					}
					if (i > num4)
					{
						num4 = i;
					}
				}
			}
		}
		if (num3 < 0)
		{
			throw new InvalidOperationException("no glyph drawn");
		}
		int num5 = num3 - num + 1;
		int num6 = num4 - num2 + 1;
		int num7 = Math.Max(num5, num6);
		byte[] array2 = new byte[num7 * num7 * 4];
		int num8 = (num7 - num5) / 2;
		int num9 = (num7 - num6) / 2;
		for (int k = 0; k < num6; k++)
		{
			Buffer.BlockCopy(array, ((num2 + k) * 72 + num) * 4, array2, ((num9 + k) * num7 + num8) * 4, num5 * 4);
		}
		BitmapSource bitmapSource = BitmapSource.Create(num7, num7, 96.0, 96.0, PixelFormats.Pbgra32, null, array2, num7 * 4);
		bitmapSource.Freeze();
		return bitmapSource;
	}

	public static Image CreateImage(string emoji, double fontSize)
	{
		BitmapSource bitmapSource = Get(emoji);
		if (bitmapSource == null)
		{
			return null;
		}
		double num = Math.Round(fontSize * 1.12, 1);
		Image image = new Image();
		image.Source = bitmapSource;
		image.Width = num;
		image.Height = num;
		image.Stretch = Stretch.Uniform;
		image.IsHitTestVisible = false;
		Image image2 = image;
		RenderOptions.SetBitmapScalingMode(image2, BitmapScalingMode.HighQuality);
		return image2;
	}
}
