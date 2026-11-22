using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Core;

internal sealed class NativeSurfaceRegion
{
	private struct Bounds
	{
		public int L;

		public int T;

		public int R;

		public int B;
	}

	private Int32Rect _last = new Int32Rect(-1, -1, 0, 0);

	private long _checkedAt;

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateRectRgn(int l, int t, int r, int b);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr region);

	[DllImport("gdi32.dll")]
	private static extern int GetRgnBox(IntPtr region, out Bounds bounds);

	[DllImport("user32.dll")]
	private static extern int GetWindowRgn(IntPtr window, IntPtr region);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr window, int command);

	internal static bool Matches(IntPtr window, Int32Rect wanted)
	{
		IntPtr intPtr = CreateRectRgn(0, 0, 0, 0);
		if (intPtr == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			int windowRgn = GetWindowRgn(window, intPtr);
			if (windowRgn == 0)
			{
				return false;
			}
			if (wanted.Width == 0 || wanted.Height == 0)
			{
				return windowRgn == 1;
			}
			Bounds bounds;
			return windowRgn == 2 && GetRgnBox(intPtr, out bounds) != 0 && bounds.L == wanted.X && bounds.T == wanted.Y && bounds.R == wanted.X + wanted.Width && bounds.B == wanted.Y + wanted.Height;
		}
		finally
		{
			DeleteObject(intPtr);
		}
	}

	internal bool Apply(IntPtr window, Rect drawing, bool force = false)
	{
		Int32Rect int32Rect = ((drawing.IsEmpty || drawing.Width <= 0.0 || drawing.Height <= 0.0) ? default(Int32Rect) : new Int32Rect((int)Math.Floor(drawing.Left), (int)Math.Floor(drawing.Top), (int)Math.Ceiling(drawing.Right) - (int)Math.Floor(drawing.Left), (int)Math.Ceiling(drawing.Bottom) - (int)Math.Floor(drawing.Top)));
		long elapsedMilliseconds = _clock.ElapsedMilliseconds;
		if (!force && int32Rect.Equals(_last) && (elapsedMilliseconds - _checkedAt < 400 || Matches(window, int32Rect)))
		{
			if (elapsedMilliseconds - _checkedAt >= 400)
			{
				_checkedAt = elapsedMilliseconds;
			}
			return true;
		}
		IntPtr intPtr = CreateRectRgn(int32Rect.X, int32Rect.Y, int32Rect.X + int32Rect.Width, int32Rect.Y + int32Rect.Height);
		if (intPtr == IntPtr.Zero || SetWindowRgn(window, intPtr, redraw: false) == 0)
		{
			int lastWin32Error = Marshal.GetLastWin32Error();
			if (intPtr != IntPtr.Zero)
			{
				DeleteObject(intPtr);
			}
			_last = new Int32Rect(-1, -1, 0, 0);
			ShowWindow(window, 0);
			TransactionLogger.Warn("Native overlay region failed, Win32=" + lastWin32Error);
			return false;
		}
		_last = int32Rect;
		_checkedAt = elapsedMilliseconds;
		return true;
	}
}
