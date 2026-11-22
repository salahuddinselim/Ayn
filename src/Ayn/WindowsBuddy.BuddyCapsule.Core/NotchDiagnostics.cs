using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class NotchDiagnostics
{
	private const int WM_HOTKEY = 786;

	private const int HotkeyId = 23058;

	private const uint MOD_ALT = 1u;

	private const uint MOD_CONTROL = 2u;

	private const uint MOD_SHIFT = 4u;

	private const uint MOD_NOREPEAT = 16384u;

	private const uint VK_D = 68u;

	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_TRANSPARENT = 32;

	private const int WS_EX_TOOLWINDOW = 128;

	private const int WS_EX_NOACTIVATE = 134217728;

	private static HwndSource _hotkeySource;

	private static CapsuleWindow _notch;

	private static Window _overlay;

	private static TextBlock _text;

	private static DispatcherTimer _refresh;

	public static bool IsOpen
	{
		get
		{
			if (_overlay != null)
			{
				return _overlay.IsVisible;
			}
			return false;
		}
	}

	public static string LastSnapshotPath { get; private set; }

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

	public static void Initialize(CapsuleWindow notch)
	{
		_notch = notch;
		if (_hotkeySource != null)
		{
			return;
		}
		try
		{
			HwndSourceParameters parameters = new HwndSourceParameters("AynNotchDiagnostics");
			parameters.ParentWindow = new IntPtr(-3);
			parameters.Width = 0;
			parameters.Height = 0;
			parameters.WindowStyle = 0;
			_hotkeySource = new HwndSource(parameters);
			_hotkeySource.AddHook(delegate(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
			{
				if (msg == 786 && w.ToInt32() == 23058)
				{
					handled = true;
					Toggle();
				}
				return IntPtr.Zero;
			});
			if (!RegisterHotKey(_hotkeySource.Handle, 23058, 16391u, 68u))
			{
				NotchLog.Warn("Diagnostics shortcut Ctrl+Alt+Shift+D is taken by another app (" + Marshal.GetLastWin32Error() + ").");
			}
		}
		catch (Exception ex)
		{
			NotchLog.Error("Diagnostics shortcut", ex);
		}
	}

	public static void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	public static void Open()
	{
		if (_notch != null)
		{
			if (_overlay == null)
			{
				Build();
			}
			Place();
			_overlay.Show();
			Update();
			_refresh.Start();
			SaveSnapshot();
		}
	}

	public static void Close()
	{
		if (_refresh != null)
		{
			_refresh.Stop();
		}
		if (_overlay != null)
		{
			_overlay.Hide();
		}
		if (_notch != null)
		{
			_notch.UpdateDiagnosticsHitArea(show: false);
		}
	}

	public static void NotifyChanged()
	{
		if (IsOpen)
		{
			Update();
		}
	}

	private static void Build()
	{
		TextBlock textBlock = new TextBlock();
		textBlock.FontFamily = new FontFamily("Cascadia Mono, Consolas");
		textBlock.FontSize = 11.0;
		textBlock.Foreground = Brushes.White;
		textBlock.TextWrapping = TextWrapping.NoWrap;
		_text = textBlock;
		TextBlock textBlock2 = new TextBlock();
		textBlock2.Text = "VAYLI NOTCH DIAGNOSTICS  ·  Ctrl+Alt+Shift+D to close";
		textBlock2.FontSize = 10.0;
		textBlock2.FontWeight = AynType.Medium;
		textBlock2.Foreground = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 64, 129));
		textBlock2.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		TextBlock element = textBlock2;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(element);
		stackPanel.Children.Add(_text);
		Window window = new Window();
		window.Title = "Ayn notch diagnostics";
		window.WindowStyle = WindowStyle.None;
		window.AllowsTransparency = true;
		window.Background = Brushes.Transparent;
		window.Topmost = true;
		window.ShowInTaskbar = false;
		window.ShowActivated = false;
		window.SizeToContent = SizeToContent.WidthAndHeight;
		window.ResizeMode = ResizeMode.NoResize;
		window.Content = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(225, 16, 16, 18)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(120, byte.MaxValue, 64, 129)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(12.0, 10.0, 12.0, 10.0),
			Child = stackPanel
		};
		_overlay = window;
		_overlay.SourceInitialized += delegate
		{
			IntPtr handle = new WindowInteropHelper(_overlay).Handle;
			SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x20 | 0x80 | 0x8000000);
			NotchVisibilityEngine.Instance.DiagnosticsWindowHandle = handle;
		};
		DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background);
		dispatcherTimer.Interval = TimeSpan.FromMilliseconds(150.0);
		_refresh = dispatcherTimer;
		_refresh.Tick += delegate
		{
			Update();
		};
	}

	private static void Place()
	{
		Point point = _notch.PointToScreen(new Point(0.0, 0.0));
		double num = PresentationSource.FromVisual(_notch)?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
		_overlay.Left = point.X / num + 16.0;
		_overlay.Top = point.Y / num + 388.0 + 8.0;
	}

	private static void Update()
	{
		if (_notch == null || _text == null)
		{
			return;
		}
		try
		{
			_notch.UpdateDiagnosticsHitArea(show: true);
			_text.Text = Snapshot(12);
		}
		catch (Exception ex)
		{
			_text.Text = "diagnostics failed: " + ex.Message;
		}
	}

	public static string Snapshot(int transitions)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(_notch.DescribeDiagnostics());
		if (_notch.Machine != null)
		{
			stringBuilder.Append("\nLast transitions (newest first)\n");
			foreach (NotchTransition item in _notch.Machine.RecentTransitions(transitions))
			{
				stringBuilder.Append("  ").Append(item).Append('\n');
			}
		}
		stringBuilder.Append("\nLog: ").Append(LogWriter.PathFor("notch.log"));
		return stringBuilder.ToString();
	}

	private static void SaveSnapshot()
	{
		try
		{
			string text = LogWriter.PathFor("notch-diagnostics.txt");
			if (text != null)
			{
				File.WriteAllText(text, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + Snapshot(40));
				LastSnapshotPath = text;
			}
		}
		catch (Exception ex)
		{
			NotchLog.Error("Saving the diagnostics snapshot", ex);
		}
	}
}
