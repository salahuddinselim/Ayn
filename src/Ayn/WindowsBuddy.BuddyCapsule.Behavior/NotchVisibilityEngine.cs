using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Core;

namespace WindowsBuddy.BuddyCapsule.Behavior;

public sealed class NotchVisibilityEngine : IDisposable
{
	private delegate void WinEventCallback(IntPtr hook, uint evt, IntPtr hwnd, int objectId, int childId, uint thread, uint time);

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MONITORINFO
	{
		public int cbSize;

		public RECT rcMonitor;

		public RECT rcWork;

		public int dwFlags;
	}

	private sealed class WindowIdentity
	{
		public uint Pid;

		public string Name;

		public string Path;

		public DateTime ReadUtc;
	}

	private const int GWL_STYLE = -16;

	private const int GWL_EXSTYLE = -20;

	private const int WS_CAPTION = 12582912;

	private const int WS_EX_TOOLWINDOW = 128;

	private const uint MONITOR_DEFAULTTONEAREST = 2u;

	private const uint GW_HWNDNEXT = 2u;

	private const int DWMWA_CLOAKED = 14;

	private const int NullForegroundDebounceMs = 250;

	public const int HideAnimationDurationMs = 160;

	public const int ShowAnimationDurationMs = 200;

	private static readonly object _syncLock = new object();

	private static NotchVisibilityEngine _instance;

	private NotchPresence _presence;

	private bool _published;

	private readonly StringBuilder _classBuf = new StringBuilder(256);

	private bool _hovering;

	private bool _sessionLocked;

	private DateTime _nullForegroundSinceUtc = DateTime.MinValue;

	private readonly DispatcherTimer _nullForegroundTimer;

	private readonly DispatcherTimer _pollTimer;

	private readonly DispatcherTimer _eventTimer;

	private readonly Dispatcher _dispatcher;

	private readonly EventHandler<NotchSettingsChangedEventArgs> _settingsHandler;

	private readonly WinEventCallback _winEventCallback;

	private IntPtr _foregroundHook;

	private IntPtr _windowHook;

	private int _eventQueued;

	private bool _disposed;

	private volatile bool _observing;

	private bool _forceNotify;

	private bool _revealActive;

	private static readonly object _identityLock = new object();

	private static readonly Dictionary<IntPtr, WindowIdentity> _identities = new Dictionary<IntPtr, WindowIdentity>();

	public static NotchVisibilityEngine Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_syncLock)
				{
					if (_instance == null)
					{
						_instance = new NotchVisibilityEngine();
					}
				}
			}
			return _instance;
		}
	}

	public NotchPresence CurrentPresence => _presence;

	public string LastReason { get; private set; }

	public PresenceInputs LastInputs { get; private set; }

	public bool IsHoveringNotch
	{
		get
		{
			return HoverProbe?.Invoke() ?? _hovering;
		}
		set
		{
			_hovering = value;
		}
	}

	public Func<bool> HoverProbe { get; set; }

	public bool IsCapsuleExpanded { get; set; }

	public Func<IntPtr> NotchMonitorProvider { get; set; }

	public IntPtr NotchWindowHandle { get; set; }

	public IntPtr DiagnosticsWindowHandle { get; set; }

	public bool SessionLocked
	{
		get
		{
			return _sessionLocked;
		}
		set
		{
			if (_sessionLocked != value)
			{
				_sessionLocked = value;
				ForceReevaluate(value ? "session locked" : "session unlocked");
			}
		}
	}

	public bool IsTransientActive => _revealActive;

	public bool IsTransientEventActive => IsTransientActive;

	public event EventHandler<VisibilityChangeEventArgs> VisibilityChanged;

	public event EventHandler EnvironmentChanged;

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

	[DllImport("user32.dll")]
	private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventCallback callback, uint process, uint thread, uint flags);

	[DllImport("user32.dll")]
	private static extern bool UnhookWinEvent(IntPtr hook);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

	[DllImport("user32.dll")]
	private static extern IntPtr GetTopWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsIconic(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsZoomed(IntPtr hWnd);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

	private NotchVisibilityEngine()
	{
		_dispatcher = Dispatcher.CurrentDispatcher;
		_eventTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
		{
			Interval = TimeSpan.FromMilliseconds(50.0)
		};
		DispatcherTimer eventTimer = _eventTimer;
		EventHandler value = delegate
		{
			_eventTimer.Stop();
			Interlocked.Exchange(ref _eventQueued, 0);
			if (!_disposed)
			{
				EvaluateVisibility("window event");
			}
		};
		eventTimer.Tick += value;
		_winEventCallback = OnWindowEvent;
		_foregroundHook = SetWinEventHook(3u, 3u, IntPtr.Zero, _winEventCallback, 0u, 0u, 0u);
		_windowHook = SetWinEventHook(32769u, 32779u, IntPtr.Zero, _winEventCallback, 0u, 0u, 0u);
		_nullForegroundTimer = new DispatcherTimer(DispatcherPriority.Normal)
		{
			Interval = TimeSpan.FromMilliseconds(260.0)
		};
		_nullForegroundTimer.Tick += delegate
		{
			_nullForegroundTimer.Stop();
			EvaluateVisibility("null foreground settled");
		};
		_pollTimer = new DispatcherTimer(DispatcherPriority.Background);
		_pollTimer.Tick += delegate
		{
			EvaluateVisibility("monitor poll");
		};
		UpdatePollInterval();
		_settingsHandler = delegate(object s, NotchSettingsChangedEventArgs e)
		{
			if (e.Area != 0)
			{
				UpdatePollInterval();
				ForceReevaluate("settings changed: " + e.Area);
			}
		};
		NotchSettings.Instance.Changed += _settingsHandler;
	}

	private void OnWindowEvent(IntPtr hook, uint evt, IntPtr hwnd, int objectId, int childId, uint thread, uint time)
	{
		if (_disposed || !_observing || hwnd == IntPtr.Zero || hwnd == NotchWindowHandle || hwnd == DiagnosticsWindowHandle || (evt != 3 && (objectId != 0 || childId != 0 || (evt != 32769 && evt != 32770 && evt != 32771 && evt != 32772 && evt != 32779))))
		{
			return;
		}
		if (evt == 32769)
		{
			lock (_identityLock)
			{
				_identities.Remove(hwnd);
			}
		}
		if ((evt != 3 && evt != 32769 && GetAncestor(hwnd, 2u) != hwnd) || Interlocked.Exchange(ref _eventQueued, 1) == 1)
		{
			return;
		}
		try
		{
			_dispatcher.BeginInvoke((Action)delegate
			{
				if (_disposed || !NotchSettings.Instance.NotchEnabled || NotchSettings.Instance.VisibilityMode == NotchVisibilityMode.Hidden)
				{
					Interlocked.Exchange(ref _eventQueued, 0);
				}
				else
				{
					_eventTimer.Start();
				}
			});
		}
		catch
		{
			Interlocked.Exchange(ref _eventQueued, 0);
		}
	}

	private void UpdatePollInterval()
	{
		NotchVisibilityMode visibilityMode = NotchSettings.Instance.VisibilityMode;
		_observing = visibilityMode != NotchVisibilityMode.Hidden && NotchSettings.Instance.NotchEnabled;
		if (!_observing)
		{
			_pollTimer.Stop();
			_eventTimer.Stop();
			Interlocked.Exchange(ref _eventQueued, 0);
			return;
		}
		_pollTimer.Interval = TimeSpan.FromSeconds((_foregroundHook == IntPtr.Zero || _windowHook == IntPtr.Zero) ? 1 : 5);
		if (!_pollTimer.IsEnabled)
		{
			_pollTimer.Start();
		}
	}

	public static NotchPresence ComputePresence(PresenceInputs i, out string reason)
	{
		if (!i.Enabled)
		{
			reason = "notch turned off";
			return NotchPresence.None;
		}
		if (i.SessionLocked)
		{
			reason = "session locked";
			return NotchPresence.None;
		}
		if (i.FullscreenOnMonitor && i.HideInFullscreen)
		{
			reason = "fullscreen app on the notch's display";
			return NotchPresence.None;
		}
		NotchVisibilityMode notchVisibilityMode = ((i.Mode == NotchVisibilityMode.Desktop) ? NotchVisibilityMode.Smart : i.Mode);
		if (notchVisibilityMode == NotchVisibilityMode.Hidden)
		{
			reason = "Hidden mode";
			return NotchPresence.None;
		}
		if (i.AppRule == PerAppVisibility.AlwaysHide)
		{
			reason = "app rule: always hide";
			return NotchPresence.None;
		}
		if (i.AppRule == PerAppVisibility.AlwaysShow)
		{
			reason = "app rule: always show";
			return NotchPresence.Full;
		}
		if (i.AppRule == PerAppVisibility.EventsOnly)
		{
			reason = (i.TransientActive ? "app rule: events only (event showing)" : "app rule: events only");
			if (!i.TransientActive)
			{
				return NotchPresence.None;
			}
			return NotchPresence.Full;
		}
		if (notchVisibilityMode == NotchVisibilityMode.AlwaysVisible)
		{
			reason = "Always mode";
			return NotchPresence.Full;
		}
		if (notchVisibilityMode != NotchVisibilityMode.Minimal && (notchVisibilityMode != NotchVisibilityMode.Smart || i.ForegroundIsDesktop || !i.MaximizedOnMonitor))
		{
			reason = "Smart mode: desktop / no maximized window";
			return NotchPresence.Full;
		}
		if (i.TransientActive && i.ShowEventsOverApps)
		{
			reason = "brief event over the minimized notch";
			return NotchPresence.Full;
		}
		reason = ((notchVisibilityMode == NotchVisibilityMode.Minimal) ? "Minimal mode" : "Smart mode: maximized window on the notch's display");
		return NotchPresence.Line;
	}

	public void EvaluateVisibility(string trigger)
	{
		IntPtr foregroundWindow = GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (_nullForegroundSinceUtc == DateTime.MinValue)
			{
				_nullForegroundSinceUtc = utcNow;
			}
			if ((utcNow - _nullForegroundSinceUtc).TotalMilliseconds < 250.0)
			{
				if (!_nullForegroundTimer.IsEnabled)
				{
					_nullForegroundTimer.Start();
				}
				return;
			}
		}
		else
		{
			_nullForegroundSinceUtc = DateTime.MinValue;
			_nullForegroundTimer.Stop();
		}
		PresenceInputs presenceInputs = BuildInputs(foregroundWindow);
		bool flag = LastInputs.FullscreenOnMonitor != presenceInputs.FullscreenOnMonitor || LastInputs.SessionLocked != presenceInputs.SessionLocked;
		LastInputs = presenceInputs;
		if (flag)
		{
			this.EnvironmentChanged?.Invoke(this, EventArgs.Empty);
		}
		string reason;
		NotchPresence presence = ComputePresence(presenceInputs, out reason);
		Publish(presence, reason + " [" + trigger + "]");
	}

	private PresenceInputs BuildInputs(IntPtr fg)
	{
		NotchSettings instance = NotchSettings.Instance;
		NotchBehaviorConfig instance2 = NotchBehaviorConfig.Instance;
		PresenceInputs presenceInputs = default(PresenceInputs);
		presenceInputs.Enabled = instance.NotchEnabled;
		presenceInputs.SessionLocked = _sessionLocked || IsLockScreenOrSecureDesktop(fg);
		presenceInputs.HideInFullscreen = instance2.HideInFullscreen;
		presenceInputs.Mode = instance.VisibilityMode;
		presenceInputs.ForegroundIsDesktop = fg == IntPtr.Zero || IsWallpaperWindow(fg);
		presenceInputs.AppRule = PerAppVisibility.FollowDefault;
		presenceInputs.TransientActive = IsTransientActive;
		presenceInputs.ShowEventsOverApps = instance2.ShowBriefEventsWhenHidden;
		PresenceInputs result = presenceInputs;
		if (result.Enabled && !result.SessionLocked && result.Mode != NotchVisibilityMode.Hidden)
		{
			ScanNotchMonitor(out var fullscreen, out var maximized);
			result.FullscreenOnMonitor = fullscreen;
			result.MaximizedOnMonitor = maximized;
			if (instance2.AppRules.Count > 0 && fg != IntPtr.Zero && !IsDesktopWindow(fg))
			{
				GetWindowProcessIdentity(fg, out var processName, out var executablePath);
				result.AppRule = instance2.GetAppRule(processName, executablePath);
			}
		}
		return result;
	}

	public bool WouldShowEvent(out string reason)
	{
		PresenceInputs i = BuildInputs(GetForegroundWindow());
		i.TransientActive = true;
		return ComputePresence(i, out reason) == NotchPresence.Full;
	}

	private void Publish(NotchPresence presence, string reason)
	{
		if (_published && presence == _presence && !_forceNotify)
		{
			return;
		}
		_forceNotify = false;
		_published = true;
		_presence = presence;
		LastReason = reason;
		EventHandler<VisibilityChangeEventArgs> visibilityChanged = this.VisibilityChanged;
		if (visibilityChanged != null)
		{
			try
			{
				visibilityChanged(this, new VisibilityChangeEventArgs(presence, IsTransientActive, reason));
			}
			catch (Exception ex)
			{
				NotchLog.Error("VisibilityChanged handler", ex);
			}
		}
	}

	public void ForceReevaluate(string reason)
	{
		_forceNotify = true;
		_nullForegroundSinceUtc = DateTime.MinValue.AddTicks(1L);
		try
		{
			EvaluateVisibility(reason);
		}
		finally
		{
			_forceNotify = false;
		}
	}

	public void SetRevealActive(bool active, string reason)
	{
		bool revealActive = _revealActive;
		_revealActive = active;
		if (revealActive != active)
		{
			EvaluateVisibility(active ? ("event: " + reason) : "event cleared");
		}
	}

	public void ClearTransientEvent()
	{
		PillActivityArbiter.Instance.EndReveal("cleared");
		if (_revealActive)
		{
			SetRevealActive(active: false, "cleared");
		}
	}

	public double GetTransientRemainingSeconds()
	{
		return PillActivityArbiter.Instance.RevealRemainingSeconds;
	}

	internal static bool IsPassiveOverlay(int extendedStyle)
	{
		if ((extendedStyle & 0x20) == 0)
		{
			return (extendedStyle & 0x8000080) == 134217856;
		}
		return true;
	}

	public static bool IsRectFullscreen(int winLeft, int winTop, int winRight, int winBottom, int monLeft, int monTop, int monRight, int monBottom, int windowStyle)
	{
		if (winLeft > monLeft || winTop > monTop || winRight < monRight || winBottom < monBottom)
		{
			return false;
		}
		return (windowStyle & 0xC00000) != 12582912;
	}

	public void ScanNotchMonitor(out bool fullscreen, out bool maximized)
	{
		fullscreen = false;
		maximized = false;
		try
		{
			IntPtr intPtr = ((NotchMonitorProvider != null) ? NotchMonitorProvider() : IntPtr.Zero);
			if (intPtr == IntPtr.Zero)
			{
				return;
			}
			MONITORINFO mONITORINFO = default(MONITORINFO);
			mONITORINFO.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
			MONITORINFO lpmi = mONITORINFO;
			if (!GetMonitorInfo(intPtr, ref lpmi))
			{
				return;
			}
			RECT rcMonitor = lpmi.rcMonitor;
			RECT rcWork = lpmi.rcWork;
			long num = (long)(rcMonitor.Right - rcMonitor.Left) * (long)(rcMonitor.Bottom - rcMonitor.Top);
			if (num <= 0)
			{
				return;
			}
			bool flag = false;
			int num2 = 0;
			IntPtr intPtr2 = GetTopWindow(IntPtr.Zero);
			while (intPtr2 != IntPtr.Zero && num2 < 1024)
			{
				if (!(intPtr2 == NotchWindowHandle) && !(intPtr2 == DiagnosticsWindowHandle) && IsWindowVisible(intPtr2) && !IsIconic(intPtr2) && !IsPassiveOverlay(GetWindowLong(intPtr2, -20)) && (DwmGetWindowAttribute(intPtr2, 14, out var pvAttribute, 4) != 0 || pvAttribute == 0) && GetWindowRect(intPtr2, out var lpRect))
				{
					int num3 = Math.Min(lpRect.Right, rcMonitor.Right) - Math.Max(lpRect.Left, rcMonitor.Left);
					int num4 = Math.Min(lpRect.Bottom, rcMonitor.Bottom) - Math.Max(lpRect.Top, rcMonitor.Top);
					if (num3 > 0 && num4 > 0)
					{
						if (IsWallpaperWindow(intPtr2))
						{
							break;
						}
						bool flag2 = lpRect.Left <= rcMonitor.Left && lpRect.Top <= rcMonitor.Top && lpRect.Right >= rcMonitor.Right && lpRect.Bottom >= rcMonitor.Bottom;
						int windowLong = GetWindowLong(intPtr2, -16);
						if (!flag)
						{
							if (flag2)
							{
								fullscreen = (windowLong & 0xC00000) != 12582912;
								flag = true;
							}
							else if ((long)num3 * (long)num4 >= num / 2)
							{
								flag = true;
							}
						}
						if (!maximized)
						{
							int windowLong2 = GetWindowLong(intPtr2, -20);
							if ((windowLong2 & 0x80) == 0 && !IsShellWindow(intPtr2) && MonitorFromWindow(intPtr2, 2u) == intPtr)
							{
								bool flag3 = lpRect.Left <= rcWork.Left && lpRect.Top <= rcWork.Top && lpRect.Right >= rcWork.Right && lpRect.Bottom >= rcWork.Bottom;
								if (IsZoomed(intPtr2) || flag3)
								{
									maximized = true;
								}
							}
						}
						if (flag && maximized)
						{
							break;
						}
					}
				}
				intPtr2 = GetWindow(intPtr2, 2u);
				num2++;
			}
		}
		catch
		{
		}
	}

	public bool IsFullscreenOnNotchMonitor()
	{
		ScanNotchMonitor(out var fullscreen, out var _);
		return fullscreen;
	}

	private string ClassOf(IntPtr hWnd)
	{
		_classBuf.Length = 0;
		GetClassName(hWnd, _classBuf, _classBuf.Capacity);
		return _classBuf.ToString();
	}

	private bool IsWallpaperWindow(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return true;
		}
		string text = ClassOf(hWnd);
		if (!(text == "Progman"))
		{
			return text == "WorkerW";
		}
		return true;
	}

	private bool IsShellWindow(IntPtr hWnd)
	{
		string text = ClassOf(hWnd);
		switch (text)
		{
		default:
			return text == "WorkerW";
		case "Shell_TrayWnd":
		case "Shell_SecondaryTrayWnd":
		case "Windows.UI.Core.CoreWindow":
		case "XamlExplorerHostIslandWindow":
		case "TopLevelWindowForOverflowXamlIsland":
		case "Progman":
			return true;
		}
	}

	public static bool IsDesktopWindow(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return true;
		}
		StringBuilder stringBuilder = new StringBuilder(256);
		GetClassName(hWnd, stringBuilder, stringBuilder.Capacity);
		string text = stringBuilder.ToString();
		switch (text)
		{
		default:
			return text == "TopLevelWindowForOverflowXamlIsland";
		case "Progman":
		case "WorkerW":
		case "Shell_TrayWnd":
		case "Shell_SecondaryTrayWnd":
		case "Windows.UI.Core.CoreWindow":
		case "XamlExplorerHostIslandWindow":
			return true;
		}
	}

	private bool IsLockScreenOrSecureDesktop(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		string text = ClassOf(hWnd);
		if (!(text == "LockScreenHostWindow") && !(text == "LogonUI"))
		{
			return text == "SecurityHealthHostWindow";
		}
		return true;
	}

	public static void GetWindowProcessIdentity(IntPtr hWnd, out string processName, out string executablePath)
	{
		GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
		lock (_identityLock)
		{
			if (_identities.TryGetValue(hWnd, out var value) && value.Pid == lpdwProcessId && (DateTime.UtcNow - value.ReadUtc).TotalSeconds < 30.0)
			{
				processName = value.Name;
				executablePath = value.Path;
				return;
			}
		}
		ReadWindowProcessIdentity(hWnd, out processName, out executablePath);
		lock (_identityLock)
		{
			if (_identities.Count >= 128)
			{
				_identities.Clear();
			}
			_identities[hWnd] = new WindowIdentity
			{
				Pid = lpdwProcessId,
				Name = processName,
				Path = executablePath,
				ReadUtc = DateTime.UtcNow
			};
		}
	}

	private static void ReadWindowProcessIdentity(IntPtr hWnd, out string processName, out string executablePath)
	{
		processName = string.Empty;
		executablePath = string.Empty;
		if (hWnd == IntPtr.Zero)
		{
			return;
		}
		try
		{
			GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
			if (lpdwProcessId == 0)
			{
				return;
			}
			using Process process = Process.GetProcessById((int)lpdwProcessId);
			processName = process.ProcessName;
			try
			{
				executablePath = process.MainModule.FileName;
			}
			catch
			{
				executablePath = process.ProcessName + ".exe";
			}
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_observing = false;
		if (_foregroundHook != IntPtr.Zero)
		{
			UnhookWinEvent(_foregroundHook);
		}
		if (_windowHook != IntPtr.Zero)
		{
			UnhookWinEvent(_windowHook);
		}
		_foregroundHook = (_windowHook = IntPtr.Zero);
		_eventTimer.Stop();
		NotchSettings.Instance.Changed -= _settingsHandler;
		_pollTimer.Stop();
		_nullForegroundTimer.Stop();
	}
}
