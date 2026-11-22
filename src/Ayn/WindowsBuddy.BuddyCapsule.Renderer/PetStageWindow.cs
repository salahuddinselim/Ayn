using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class PetStageWindow : Window
{
	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private IntPtr handle;

	private readonly GardenView view;

	private int left;

	private int top;

	private int wide;

	private int high;

	private bool placed;

	private bool placementQueued;

	private bool placementRequested;

	private Point pendingTopLeft;

	private Point pendingBottomRight;

	public Action<Point, bool> PointerInput;

	public bool Suspended;

	private bool hiddenOnPurpose;

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hwnd, int index);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hwnd);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hwnd, int command);

	public void SetNativeHidden(bool hidden)
	{
		if (hidden)
		{
			placementRequested = false;
			if (handle != IntPtr.Zero && (!hiddenOnPurpose || IsWindowVisible(handle)))
			{
				ShowWindow(handle, 0);
				hiddenOnPurpose = true;
			}
		}
	}

	public PetStageWindow(Window owner, GardenView view)
	{
		PetStageWindow petStageWindow = this;
		this.view = view;
		base.WindowStyle = WindowStyle.None;
		base.ResizeMode = ResizeMode.NoResize;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.Focusable = false;
		base.Content = view;
		base.Width = view.Width;
		base.Height = view.Height;
		EventHandler value = delegate
		{
			petStageWindow.handle = new WindowInteropHelper(petStageWindow).Handle;
			SetWindowLong(petStageWindow.handle, -20, (GetWindowLong(petStageWindow.handle, -20) | 0x8000000 | 0x80) & -33);
			HwndSource source = HwndSource.FromHwnd(petStageWindow.handle);
			if (source != null)
			{
				source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
				source.AddHook(delegate(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
				{
					switch (msg)
					{
					case 132:
					{
						if (petStageWindow.Suspended)
						{
							handled = true;
							return new IntPtr(-1);
						}
						Point hitPoint = petStageWindow.PointFromScreen(new Point((short)(long)l, (short)((long)l >> 16)));
						handled = true;
						return new IntPtr((view.StageShape != null && view.StageShape.FillContains(hitPoint)) ? 1 : (-1));
					}
					case 33:
						handled = true;
						return new IntPtr(3);
					case 512:
					case 514:
					{
						Action<Point, bool> pointerInput = petStageWindow.PointerInput;
						if (pointerInput != null)
						{
							Point point = new Point((short)(long)l, (short)((long)l >> 16));
							pointerInput(source.CompositionTarget.TransformFromDevice.Transform(point), msg == 514);
						}
						handled = true;
						break;
					}
					}
					return IntPtr.Zero;
				});
			}
		};
		base.SourceInitialized += value;
	}

	public void Place(Point topLeft, Point bottomRight)
	{
		pendingTopLeft = topLeft;
		pendingBottomRight = bottomRight;
		placementRequested = true;
		if (placementQueued)
		{
			return;
		}
		placementQueued = true;
		base.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			placementQueued = false;
			if (placementRequested && !Suspended && base.IsVisible)
			{
				placementRequested = false;
				PlaceCore(pendingTopLeft, pendingBottomRight);
			}
		});
	}

	private void PlaceCore(Point topLeft, Point bottomRight)
	{
		if (handle == IntPtr.Zero)
		{
			return;
		}
		int num = (int)Math.Round(topLeft.X);
		int num2 = (int)Math.Round(topLeft.Y);
		int num3 = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
		int num4 = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));
		NativeRect rect;
		bool flag = IsWindowVisible(handle) && GetWindowRect(handle, out rect) && Math.Abs(rect.Left - num) <= 1 && Math.Abs(rect.Top - num2) <= 1 && Math.Abs(rect.Right - rect.Left - num3) <= 1 && Math.Abs(rect.Bottom - rect.Top - num4) <= 1;
		if (!placed || left != num || top != num2 || wide != num3 || high != num4 || !flag)
		{
			if (placed && !flag && !hiddenOnPurpose)
			{
				TransactionLogger.Info("Pet stage restored: hidden or displaced native surface");
			}
			hiddenOnPurpose = false;
			SetWindowPos(handle, new IntPtr(-1), num, num2, num3, num4, 592u);
			if (!double.IsNaN(view.Width) && base.Width != view.Width)
			{
				base.Width = view.Width;
			}
			if (!double.IsNaN(view.Height) && base.Height != view.Height)
			{
				base.Height = view.Height;
			}
			if (!placed || wide != num3 || high != num4)
			{
				view.InvalidateMeasure();
				view.Measure(new Size(view.Width, view.Height));
				view.Arrange(new Rect(0.0, 0.0, view.Width, view.Height));
			}
			view.InvalidateStageClip();
			placed = true;
			left = num;
			top = num2;
			wide = num3;
			high = num4;
		}
	}
}
