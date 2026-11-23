using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.AICoding;
using WindowsBuddy.Tools.Analytics;
using WindowsBuddy.Tools.Calendar;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.Tools.Clock;
using WindowsBuddy.Tools.ScreenTime;
using WindowsBuddy.Tools.Timer;
using WindowsBuddy.Tools.Weather;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsWindow : Window
{
	private sealed class CachedPage
	{
		public UIElement View;

		public PageContext Ctx;

		public bool Dirty;
	}

	private struct MonitorPoint
	{
		public int X;

		public int Y;
	}

	internal delegate int DwmSetter(IntPtr hwnd, int attr, ref int value, int size);

	private sealed class NavRow
	{
		public SClickable Root;

		public bool Selected;
	}

	private const int GWL_STYLE = -16;

	private const int WS_MAXIMIZEBOX = 65536;

	private const int WM_SYSCHAR = 262;

	private const int WM_SYSCOMMAND = 274;

	private const int SC_KEYMENU = 61696;

	private const int WM_NCLBUTTONDBLCLK = 163;

	private const int SC_MAXIMIZE = 61488;

	private static SettingsWindow _current;

	private readonly Grid _root;
	private Grid _contentGrid;
	private Grid _sidebarGrid;

	private StackPanel _navStack;

	private readonly Dictionary<string, NavRow> _navRows = new Dictionary<string, NavRow>();

	private TextBlock _brandStatus;

	private Border _trialCard;

	private STextBox _search;

	private readonly ContentControl _pageHost = new ContentControl
	{
		Focusable = false,
		MaxWidth = 616.0,
		HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
		HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch
	};

	private readonly Dictionary<string, CachedPage> _pages = new Dictionary<string, CachedPage>();

	private readonly Grid _pageGrid = new Grid();

	private UIElement _currentView;

	private CachedPage _currentCached;

	private PageContext _searchCtx;

	private DispatcherTimer _bgTimer;

	private bool _closed;

	private EventHandler _staleHandler;

	private EventHandler<string> _preferencesChanged;

	private bool _stateRefreshQueued;

	private readonly HashSet<string> _observedStores = new HashSet<string>();

	private readonly List<Action> _stateUnhooks = new List<Action>();

	private readonly HashSet<string> _pendingStores = new HashSet<string>();

	private EventHandler<NotchSettingsChangedEventArgs> _notchStaleHandler;

	private readonly BuddyScrollViewer _scroll;

	private string _page;

	private bool _showingSearch;

	private EventHandler _toolsUpdated;

	private EventHandler _licenseChanged;

	private bool _suppressToolsRefresh;

	private List<SettingsSearch.Hit> _hits = new List<SettingsSearch.Hit>();

	private StackPanel _resultsStack;

	public static bool IsOpen => _current != null;

	public static SettingsWindow Current => _current;

	internal List<FrameworkElement> NavRowElements => ((IEnumerable<NavRow>)_navRows.Values).Select((Func<NavRow, FrameworkElement>)((NavRow r) => r.Root)).ToList();

	internal FrameworkElement PageHost => (_currentView as FrameworkElement) ?? _pageHost;

	public string CurrentPage => _page;

	public static void ShowOrActivate(string page = null)
	{
		try
		{
			if (_current == null)
			{
				_current = new SettingsWindow(page);
				_current.Show();
			}
			else
			{
				if (_current.WindowState == WindowState.Minimized)
				{
					_current.WindowState = WindowState.Normal;
				}
				if (!string.IsNullOrEmpty(page))
				{
					_current.Navigate(page);
				}
			}
			_current.Activate();
			_current.BringToFront();
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Could not open Settings: " + ex.Message, ex);
			try
			{
				if (_current != null && !_current.IsVisible)
				{
					_current = null;
				}
			}
			catch
			{
				_current = null;
			}
		}
	}

	private SettingsWindow(string page)
	{
		ThemeTokens.Initialize();
		SettingsPages.PrepareNotchCode();
		base.Title = "Ayn Settings";
		base.Width = 960.0;
		base.MinWidth = 560.0;
		base.MaxWidth = double.PositiveInfinity;
		base.MinHeight = 360.0;
		base.MaxHeight = double.PositiveInfinity;
		base.Height = 720.0;
		base.AllowsTransparency = true;
		base.Background = System.Windows.Media.Brushes.Transparent;
		base.Foreground = SettingsTheme.TextPrimary;
		base.FontFamily = SettingsTheme.Font;
		base.FontSize = 15.0;
		base.ShowInTaskbar = true;
		base.ResizeMode = ResizeMode.CanMinimize;
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		SettingsTheme.PrepareRoot(this);
		try
		{
			base.Icon = LoadIcon();
		}
		catch
		{
		}
		WindowChrome.SetWindowChrome(this, new WindowChrome
		{
			CaptionHeight = 44.0,
			ResizeBorderThickness = new Thickness(6.0),
			GlassFrameThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
			CornerRadius = new CornerRadius(0.0),
			UseAeroCaptionButtons = false
		});
		ApplySavedBounds();
		_root = new Grid();
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(248.0)
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_sidebarGrid = BuildSidebar() as Grid;
		_root.Children.Add(_sidebarGrid);
		_contentGrid = new Grid
		{
			Background = SettingsTheme.ContentBg
		};
		Grid grid = _contentGrid;
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(44.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.Children.Add(BuildTopBar());
		_scroll = new BuddyScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Content = new Border
			{
				Padding = new Thickness(24.0, 24.0, 24.0, 40.0),
				Child = _pageHost
			}
		};
		Grid.SetRow(_scroll, 1);
		grid.Children.Add(_scroll);
		Grid.SetColumn(grid, 1);
		_root.Children.Add(grid);
		base.Content = _root;
		WindowsBuddy.Tools.Personal.GlassShell.Apply(this, _root);
		ApplyGlobalTransparency();
		ThemeTokens.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyGlobalTransparency));
		_toolsUpdated = delegate
		{
			if (base.Dispatcher.CheckAccess())
			{
				OnToolsUpdated();
			}
			else
			{
				base.Dispatcher.BeginInvoke(new Action(OnToolsUpdated));
			}
		};
		ToolRegistry.Instance.ToolsUpdated += _toolsUpdated;
		_licenseChanged = delegate
		{
			base.Dispatcher.BeginInvoke(new Action(OnLicenseChanged));
		};
		LicenseStub.Changed += _licenseChanged;
		base.KeyDown += OnWindowKeyDown;
		base.PreviewKeyDown += OnWindowPreviewKeyDown;
		base.LostKeyboardFocus += delegate
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)delegate
			{
				if (!_closed && _currentCached != null && _currentCached.Dirty && !(Keyboard.FocusedElement is System.Windows.Controls.TextBox) && Mouse.Captured == null && SSettingBinding.StructureChanged(_currentView))
				{
					ReloadPage();
				}
			});
		};
		base.Activated += delegate
		{
			RefreshBrand();
		};
		base.LocationChanged += delegate
		{
			if (base.WindowState == WindowState.Normal)
			{
				base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FitToWorkArea));
			}
		};
		base.StateChanged += delegate
		{
			ApplyMaximizedPadding();
		};
		base.SourceInitialized += delegate
		{
			FitToWorkArea();
			ApplyDwmStyle();
			try
			{
				HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WndProc);
			}
			catch
			{
			}
		};
		PersistenceHelper.RegisterExitPreparation("preferences-window", delegate
		{
			if (_current != null && !_current._closed)
			{
				_current.SaveState();
			}
		});
		bool closeApproved = false;
		bool closePending = false;
		base.Closing += async delegate(object s, CancelEventArgs e)
		{
			if (!closeApproved && !AppPreferences.Instance.IsSealed)
			{
				e.Cancel = true;
				if (!closePending)
				{
					closePending = true;
					bool enabled = base.IsEnabled;
					base.IsEnabled = false;
					try
					{
						PersistenceHelper.PrepareExit();
						Rect bounds = ((base.WindowState == WindowState.Normal) ? new Rect(base.Left, base.Top, base.ActualWidth, base.ActualHeight) : base.RestoreBounds);
						string savedPage = ((_page != null && !_page.StartsWith("search:")) ? _page : null);
						bool saved = await Task.Run(delegate
						{
							if (!bounds.IsEmpty && !double.IsNaN(bounds.Height))
							{
								AppPreferences.Instance.SaveWindowState(bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized: false, savedPage);
							}
							AppPreferences.Instance.VerifyAsync(CancellationToken.None).GetAwaiter().GetResult();
							return PersistenceHelper.FlushForExit();
						});
						await base.Dispatcher.InvokeAsync(delegate
						{
							if (saved && !_closed)
							{
								closeApproved = true;
								Close();
							}
						});
					}
					catch (Exception ex)
					{
						TransactionLogger.Error("Settings close persistence failed", ex);
					}
					finally
					{
						base.Dispatcher.BeginInvoke((Action)delegate
						{
							closePending = false;
							if (!_closed)
							{
								base.IsEnabled = enabled;
							}
						});
					}
				}
			}
		};
		base.Closed += delegate
		{
			Teardown();
		};
		_pageHost.Content = _pageGrid;
		_staleHandler = delegate(object s, EventArgs e)
		{
			QueueStateRefresh(s);
		};
		_notchStaleHandler = delegate(object s, NotchSettingsChangedEventArgs e)
		{
			_staleHandler(s, EventArgs.Empty);
		};
		NotchSettings.Instance.Changed += _notchStaleHandler;
		ClockSettings.Instance.SettingsChanged += _staleHandler;
		_preferencesChanged = delegate(object s, string key)
		{
			if (key != "WindowState")
			{
				QueueStateRefresh(s);
			}
		};
		AppPreferences.Instance.Changed += _preferencesChanged;
		Navigate((!string.IsNullOrEmpty(page)) ? page : AppPreferences.Instance.WindowState.LastPage);
	}

	private static BitmapSource LoadIcon()
	{
		using Stream stream = typeof(SettingsWindow).Assembly.GetManifestResourceStream("Ayn.Brand.mark-128.png");
		if (stream == null)
		{
			return null;
		}
		BitmapFrame bitmapFrame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
		bitmapFrame.Freeze();
		return bitmapFrame;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(MonitorPoint point, uint flags);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);

	[DllImport("user32.dll", EntryPoint = "GetWindowLong")]
	private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
	private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

	internal static int SetDarkTitleBar(IntPtr hwnd, DwmSetter set)
	{
		int value = 1;
		if (set(hwnd, 20, ref value, 4) == 0)
		{
			return 20;
		}
		value = 1;
		if (set(hwnd, 19, ref value, 4) != 0)
		{
			return 0;
		}
		return 19;
	}

	public void ApplyGlobalTransparency()
	{
		var cfg = WindowsBuddy.Extras.AynExtrasService.Instance.Config;
		var alpha = (byte)Math.Round(255.0 * (1.0 - cfg.SettingsTransparency / 100.0));
		if (_contentGrid != null)
			_contentGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 11, 11, 12));
		if (_sidebarGrid != null)
			_sidebarGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 18, 18, 20));
	}

	private void ApplyDwmStyle()
	{
		try
		{
			IntPtr handle = new WindowInteropHelper(this).Handle;
			int windowLong = GetWindowLong32(handle, -16);
			SetWindowLong32(handle, -16, windowLong & -65537);
			SetDarkTitleBar(handle, DwmSetWindowAttribute);
			int value = 2;
			DwmSetWindowAttribute(handle, 33, ref value, 4);
			bool glass = string.Equals(NotchConfig.Instance.AppTheme, "Glass", StringComparison.OrdinalIgnoreCase);
			int backdrop = glass ? 2 : 0;
			DwmSetWindowAttribute(handle, 38, ref backdrop, 4);
			int value2 = glass ? 14205408 : 2236962;
			DwmSetWindowAttribute(handle, 34, ref value2, 4);
		}
		catch
		{
		}
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg == 736)
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FitToWorkArea));
		}
		if (msg == 163 || (msg == 274 && (wParam.ToInt64() & 0xFFF0) == 61488))
		{
			handled = true;
			return IntPtr.Zero;
		}
		if ((msg == 262 || (msg == 274 && (wParam.ToInt64() & 0xFFF0) == 61696)) && Keyboard.FocusedElement is SHotkeyRecorder { IsRecording: not false })
		{
			handled = true;
		}
		return IntPtr.Zero;
	}

	private void BringToFront()
	{
		try
		{
			SetForegroundWindow(new WindowInteropHelper(this).Handle);
		}
		catch
		{
		}
	}

	private void ApplyMaximizedPadding()
	{
		_root.Margin = ((base.WindowState == WindowState.Maximized) ? new Thickness(6.0) : new Thickness(0.0));
	}

	private void ApplySavedBounds()
	{
		base.Width = 960.0;
		base.MinWidth = 560.0;
		base.MaxWidth = double.PositiveInfinity;
		AppPreferencesModel windowState = AppPreferences.Instance.WindowState;
		double val = ((windowState.WindowHeight >= 360.0 && !double.IsInfinity(windowState.WindowHeight) && !double.IsNaN(windowState.WindowHeight)) ? windowState.WindowHeight : 720.0);
		base.Height = Math.Min(val, SystemParameters.WorkArea.Height);
		base.Width = Math.Min((windowState.WindowWidth >= 560.0 && windowState.WindowWidth <= 1600.0) ? windowState.WindowWidth : 960.0, SystemParameters.WorkArea.Width);
		base.MinWidth = Math.Min(560.0, base.Width);
		base.MinHeight = Math.Min(360.0, base.Height);
		if (windowState.HasWindowBounds && !double.IsNaN(windowState.WindowLeft) && !double.IsNaN(windowState.WindowTop) && windowState.WindowLeft + 80.0 > SystemParameters.VirtualScreenLeft && windowState.WindowTop >= SystemParameters.VirtualScreenTop - 4.0 && windowState.WindowLeft + 80.0 < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth && windowState.WindowTop + 40.0 < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
		{
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			base.Left = windowState.WindowLeft;
			base.Top = windowState.WindowTop;
		}
		else
		{
			try
			{
				System.Drawing.Point position = System.Windows.Forms.Cursor.Position;
				Screen screen = Screen.FromPoint(position) ?? Screen.PrimaryScreen;
				Rectangle workingArea = screen.WorkingArea;
				PresentationSource presentationSource = PresentationSource.FromVisual(this);
				double num = presentationSource?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
				double num2 = presentationSource?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
				if (presentationSource == null)
				{
					MonitorPoint point = default(MonitorPoint);
					point.X = position.X;
					point.Y = position.Y;
					if (GetDpiForMonitor(MonitorFromPoint(point, 2u), 0, out var x, out var y) == 0)
					{
						num = (double)x / 96.0;
						num2 = (double)y / 96.0;
					}
				}
				if (num <= 0.0)
				{
					num = 1.0;
				}
				if (num2 <= 0.0)
				{
					num2 = 1.0;
				}
				double num3 = (double)workingArea.Left / num;
				double num4 = (double)workingArea.Top / num2;
				double num5 = (double)workingArea.Width / num;
				double num6 = (double)workingArea.Height / num2;
				base.MinWidth = Math.Min(base.MinWidth, num5);
				base.MinHeight = Math.Min(base.MinHeight, num6);
				base.Width = Math.Min(base.Width, num5);
				base.Height = Math.Min(base.Height, num6);
				base.Left = num3 + (num5 - base.Width) / 2.0;
				base.Top = num4 + (num6 - base.Height) / 2.0;
				base.WindowStartupLocation = WindowStartupLocation.Manual;
			}
			catch
			{
				base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}
		}
		base.WindowState = WindowState.Normal;
	}

	private void FitToWorkArea()
	{
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		if (presentationSource != null && presentationSource.CompositionTarget != null)
		{
			Rectangle workingArea = Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
			Matrix transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
			double val = (double)workingArea.Width / transformToDevice.M11;
			double val2 = (double)workingArea.Height / transformToDevice.M22;
			base.MinWidth = Math.Min(560.0, val);
			base.MinHeight = Math.Min(360.0, val2);
			base.Width = Math.Min(base.Width, val);
			base.Height = Math.Min(base.Height, val2);
			base.Left = Math.Max((double)workingArea.Left / transformToDevice.M11, Math.Min(base.Left, (double)workingArea.Right / transformToDevice.M11 - base.Width));
			base.Top = Math.Max((double)workingArea.Top / transformToDevice.M22, Math.Min(base.Top, (double)workingArea.Bottom / transformToDevice.M22 - base.Height));
		}
	}

	private void SaveState()
	{
		try
		{
			Rect rect = ((base.WindowState == WindowState.Normal) ? new Rect(base.Left, base.Top, base.ActualWidth, base.ActualHeight) : base.RestoreBounds);
			if (!rect.IsEmpty && !double.IsNaN(rect.Height))
			{
				string lastPage = ((_page != null && !_page.StartsWith("search:")) ? _page : null);
				AppPreferences.Instance.SaveWindowState(rect.Left, rect.Top, rect.Width, rect.Height, maximized: false, lastPage);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Settings window state not saved: " + ex.Message);
		}
	}

	private void Teardown()
	{
		_closed = true;
		if (_bgTimer != null)
		{
			_bgTimer.Stop();
			_bgTimer = null;
		}
		NotchSettings.Instance.Changed -= _notchStaleHandler;
		ClockSettings.Instance.SettingsChanged -= _staleHandler;
		AppPreferences.Instance.Changed -= _preferencesChanged;
		foreach (Action stateUnhook in _stateUnhooks)
		{
			stateUnhook();
		}
		_stateUnhooks.Clear();
		_pendingStores.Clear();
		foreach (CachedPage item in _pages.Values.ToList())
		{
			try
			{
				item.Ctx.RunCleanup();
			}
			catch
			{
			}
		}
		_pages.Clear();
		if (_searchCtx != null)
		{
			_searchCtx.RunCleanup();
			_searchCtx = null;
		}
		_currentView = null;
		_currentCached = null;
		_pageGrid.Children.Clear();
		ToolRegistry.Instance.ToolsUpdated -= _toolsUpdated;
		LicenseStub.Changed -= _licenseChanged;
		_pageHost.Content = null;
		if (_current == this)
		{
			_current = null;
		}
	}

	private UIElement BuildSidebar()
	{
		Grid grid = new Grid();
		grid.Background = SettingsTheme.SidebarBg;
		Grid grid2 = grid;
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(16.0, 20.0, 16.0, 16.0);
		Grid grid4 = grid3;
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement frameworkElement = BrandAssets.CreateLogo(40.0);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid4.Children.Add(frameworkElement);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		TextBlock textBlock = SettingsTheme.Text("Ayn", 15.0, SettingsTheme.TextPrimary, AynType.Semibold);
		textBlock.TextWrapping = TextWrapping.NoWrap;
		stackPanel2.Children.Add(textBlock);
		_brandStatus = SettingsTheme.Text("", 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		_brandStatus.TextWrapping = TextWrapping.NoWrap;
		_brandStatus.TextTrimming = TextTrimming.CharacterEllipsis;
		_brandStatus.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel2.Children.Add(_brandStatus);
		Grid.SetColumn(stackPanel2, 1);
		grid4.Children.Add(stackPanel2);
		grid2.Children.Add(grid4);
		_search = new STextBox("", "Search", 0.0, "Search settings", "search", "Ctrl K");
		_search.Height = 36.0;
		_search.Background = ThemeTokens.Surface2;
		_search.BorderThickness = new Thickness(0.0);
		_search.CornerRadius = new CornerRadius(8.0);
		_search.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
		_search.Margin = new Thickness(16.0, 0.0, 16.0, 16.0);
		_search.Box.TextChanged += delegate
		{
			OnSearchChanged();
		};
		_search.Box.PreviewKeyDown += OnSearchKey;
		WindowChrome.SetIsHitTestVisibleInChrome(_search, hitTestVisible: true);
		Grid.SetRow(_search, 1);
		grid2.Children.Add(_search);
		_navStack = new StackPanel
		{
			Margin = new Thickness(16.0, 0.0, 16.0, 8.0)
		};
		KeyboardNavigation.SetDirectionalNavigation(_navStack, KeyboardNavigationMode.Contained);
		KeyboardNavigation.SetTabNavigation(_navStack, KeyboardNavigationMode.Once);
		BuddyScrollViewer buddyScrollViewer = new BuddyScrollViewer();
		buddyScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		buddyScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		buddyScrollViewer.Content = _navStack;
		BuddyScrollViewer element = buddyScrollViewer;
		Grid.SetRow(element, 2);
		grid2.Children.Add(element);
		BuildNav();
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		StackPanel stackPanel4 = stackPanel3;
		_trialCard = new Border
		{
			Background = SettingsTheme.GroupBg,
			BorderBrush = SettingsTheme.Hairline,
			BorderThickness = new Thickness(0.0),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(12.0),
			Margin = new Thickness(16.0, 0.0, 16.0, 0.0)
		};
		stackPanel4.Children.Add(_trialCard);
		stackPanel4.Children.Add(BuildFooterLinks());
		Grid.SetRow(stackPanel4, 3);
		grid2.Children.Add(stackPanel4);
		RefreshBrand();
		Border border = new Border();
		border.Background = SettingsTheme.SidebarBg;
		border.Child = grid2;
		return border;
	}

	private FrameworkElement BuildFooterLinks()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 12.0, 0.0, 4.0);
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement element = CreateFooterLink("Help", delegate
		{
			AppLinks.Open("https://github.com/salahuddinselim/Ayn");
		});
		TextBlock element2 = CreateFooterDot();
		FrameworkElement element3 = CreateFooterLink("Feedback", delegate
		{
			AppLinks.Open("https://github.com/salahuddinselim/Ayn");
		});
		TextBlock element4 = CreateFooterDot();
		string text = "v" + SettingsPages.ShortVersion();
		FrameworkElement element5 = CreateFooterLink(text, delegate
		{
			Navigate("about");
		});
		stackPanel2.Children.Add(element);
		stackPanel2.Children.Add(element2);
		stackPanel2.Children.Add(element3);
		stackPanel2.Children.Add(element4);
		stackPanel2.Children.Add(element5);
		return stackPanel2;
	}

	private static FrameworkElement CreateFooterLink(string text, Action onClick)
	{
		SButton sButton = new SButton(text, SButtonKind.Ghost);
		sButton.Height = 32.0;
		sButton.Padding = new Thickness(4.0, 0.0, 4.0, 0.0);
		sButton.LabelSize = 12.0;
		SButton sButton2 = sButton;
		sButton2.Click += delegate
		{
			if (onClick != null)
			{
				onClick();
			}
		};
		return sButton2;
	}

	private static TextBlock CreateFooterDot()
	{
		TextBlock textBlock = SettingsTheme.Text(" · ", 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.IsHitTestVisible = false;
		return textBlock;
	}

	private void OnLicenseChanged()
	{
		RefreshBrand();
		InvalidatePage("license");
	}

	private void RefreshBrand()
	{
		if (_brandStatus == null)
		{
			return;
		}
		LicenseStatus licenseStatus;
		try
		{
			licenseStatus = LicenseStub.Status;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("License state unavailable: " + ex.Message);
			licenseStatus = LicenseStatus.NotActivated;
		}
		licenseStatus = LicenseStatus.Active;
		string text;
		switch (licenseStatus)
		{
		case LicenseStatus.Active:
			text = "Ayn · SabrWare";
			break;
		case LicenseStatus.Ayn:
		{
			int trialDaysLeft = LicenseStub.AynDaysLeft;
			text = ((trialDaysLeft > 1) ? $"Ayn · {trialDaysLeft} days left" : ((trialDaysLeft == 1) ? "Ayn · 1 day left" : "Ayn"));
			break;
		}
		default:
			text = (SafeAynStarted() ? "Ayn" : ((LicenseStub.Access == AccessState.Offline) ? "Connect to the internet to start your trial" : ((LicenseStub.Access == AccessState.Checking) ? "Checking your trial…" : "Not activated")));
			break;
		}
		_brandStatus.Text = text;
		if (_trialCard == null)
		{
			return;
		}
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		if (licenseStatus == LicenseStatus.Active)
		{
			TextBlock element = SettingsTheme.Text("Ayn", 13.5, SettingsTheme.TextPrimary, AynType.Medium);
			stackPanel2.Children.Add(element);
			TextBlock textBlock = SettingsTheme.Text("Ayn · SabrWare", 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock);
			grid.Children.Add(stackPanel2);
			_trialCard.Child = grid;
			return;
		}
		TextBlock element2 = SettingsTheme.Text("Ayn", 13.5, SettingsTheme.TextPrimary, AynType.Medium);
		stackPanel2.Children.Add(element2);
		int trialDaysLeft2 = LicenseStub.AynDaysLeft;
		string text2 = ((trialDaysLeft2 > 1) ? $"{trialDaysLeft2} days left" : ((trialDaysLeft2 == 1) ? "1 day left" : "Ayn"));
		TextBlock textBlock2 = SettingsTheme.Text(text2, 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock2);
		grid.Children.Add(stackPanel2);
		SClickable sClickable = new SClickable(8.0, "Upgrade");
		sClickable.VisualRole = SVisualRole.Primary;
		sClickable.Height = 32.0;
		sClickable.CornerRadius = new CornerRadius(6.0);
		sClickable.Background = SettingsTheme.PrimaryButtonBg;
		sClickable.Cursor = System.Windows.Input.Cursors.Hand;
		sClickable.Padding = new Thickness(12.0, 0.0, 12.0, 0.0);
		sClickable.VerticalAlignment = VerticalAlignment.Center;
		SClickable sClickable2 = sClickable;
		TextBlock textBlock3 = SettingsTheme.Text("Upgrade", 13.5, SettingsTheme.PrimaryButtonText, AynType.Medium);
		textBlock3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock3.VerticalAlignment = VerticalAlignment.Center;
		sClickable2.Child = textBlock3;
		sClickable2.Click += delegate
		{
			AppLinks.Open("https://github.com/salahuddinselim/Ayn");
		};
		Grid.SetColumn(sClickable2, 1);
		grid.Children.Add(sClickable2);
		_trialCard.Child = grid;
	}

	private static string SafeStatusLine()
	{
		try
		{
			return LicenseStub.StatusLine;
		}
		catch
		{
			return "Ayn";
		}
	}

	private static bool SafeAynStarted()
	{
		try
		{
			return LicenseStub.AynStarted;
		}
		catch
		{
			return false;
		}
	}

	private void BuildNav()
	{
		_navStack.Children.Clear();
		_navRows.Clear();
		int num = -1;
		PageInfo[] all = PageInfo.All;
		foreach (PageInfo pageInfo in all)
		{
			if (num >= 0 && pageInfo.Group != num)
			{
				_navStack.Children.Add(new Border
				{
					Height = 1.0,
					Background = SettingsTheme.SidebarEdge,
					Margin = new Thickness(8.0, 4.0, 8.0, 8.0),
					IsHitTestVisible = false
				});
			}
			num = pageInfo.Group;
			AddNavRow(pageInfo.Key, pageInfo.Title, pageInfo.Tile(24.0));
		}
		UpdateNavSelection();
	}

	private void AddNavRow(string key, string title, FrameworkElement tile)
	{
		SClickable sClickable = new SClickable(8.0, title);
		sClickable.VisualRole = SVisualRole.Navigation;
		sClickable.Height = 36.0;
		sClickable.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
		sClickable.Background = System.Windows.Media.Brushes.Transparent;
		SClickable sClickable2 = sClickable;
		Grid grid = new Grid();
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(tile);
		TextBlock textBlock = SettingsTheme.Text(title, 15.0, SettingsTheme.TextPrimary, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.NoWrap;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		textBlock.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		grid.Children.Add(stackPanel2);
		sClickable2.Child = grid;
		sClickable2.Tag = key;
		sClickable2.Click += delegate
		{
			ClearSearch(restorePage: false);
			Navigate(key);
		};
		_navRows[key] = new NavRow
		{
			Root = sClickable2
		};
		_navStack.Children.Add(sClickable2);
	}

	private static string NavKeyFor(string page)
	{
		if (page == null)
		{
			return null;
		}
		if (!page.StartsWith("module:"))
		{
			return page;
		}
		return "modules";
	}

	private void UpdateNavSelection()
	{
		string text = (_showingSearch ? null : NavKeyFor(_page));
		foreach (KeyValuePair<string, NavRow> navRow in _navRows)
		{
			bool flag = navRow.Key == text;
			navRow.Value.Selected = flag;
			navRow.Value.Root.IsSelected = flag;
			AutomationProperties.SetItemStatus(navRow.Value.Root, flag ? "Selected" : "");
		}
	}

	internal void SuppressToolsRefresh(bool on)
	{
		_suppressToolsRefresh = on;
	}

	private void OnToolsUpdated()
	{
		MarkOthersStale();
		if (!_suppressToolsRefresh && !_showingSearch && (_page == "modules" || (_page != null && _page.StartsWith("module:"))))
		{
			ReloadPage();
		}
	}

	private UIElement BuildTopBar()
	{
		Grid grid = new Grid();
		grid.Background = System.Windows.Media.Brushes.Transparent;
		Grid grid2 = grid;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(CaptionButton("min", "Minimize", delegate
		{
			base.WindowState = WindowState.Minimized;
		}, isClose: false));
		base.StateChanged += delegate
		{
			if (base.WindowState == WindowState.Maximized)
			{
				base.WindowState = WindowState.Normal;
			}
		};
		stackPanel2.Children.Add(CaptionButton("close", "Close", base.Close, isClose: true));
		grid2.Children.Add(stackPanel2);
		return grid2;
	}

	private Border CaptionButton(string glyph, string name, Action onClick, bool isClose)
	{
		SCaptionButton sCaptionButton = new SCaptionButton(glyph, name, isClose);
		sCaptionButton.Click += delegate
		{
			onClick();
		};
		WindowChrome.SetIsHitTestVisibleInChrome(sCaptionButton, hitTestVisible: true);
		return sCaptionButton;
	}

	private static bool IsKnownPage(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		try
		{
			if (key.StartsWith("module:"))
			{
				return ToolRegistry.Instance.GetTool(key.Substring(7)) != null;
			}
			return PageInfo.Get(key) != null;
		}
		catch
		{
			return false;
		}
	}

	public void Navigate(string key)
	{
		key = PageInfo.Normalize(key);
		if (!IsKnownPage(key))
		{
			key = "general";
		}
		if (_showingSearch)
		{
			ClearSearch(restorePage: false);
		}
		_page = key;
		_showingSearch = false;
		ShowPage();
	}

	internal void ReloadPage()
	{
		if (!_closed && !_showingSearch && _page != null)
		{
			double offset = _scroll.VerticalOffset;
			if (_pages.TryGetValue(_page, out var value))
			{
				value.Dirty = true;
			}
			ShowPage();
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
			{
				_scroll.ScrollToVerticalOffset(offset);
			});
		}
	}

	private void ShowPage()
	{
		if (_searchCtx != null)
		{
			_searchCtx.RunCleanup();
			_searchCtx = null;
		}
		CachedPage cachedPage = Obtain(_page);
		Present(cachedPage.View, cachedPage);
		ScheduleBackgroundBuilds();
	}

	private CachedPage Obtain(string key)
	{
		if (_pages.TryGetValue(key, out var value) && !value.Dirty)
		{
			return value;
		}
		if (value != null)
		{
			Drop(key, value);
		}
		PageContext pageContext = new PageContext(this);
		UIElement uIElement;
		try
		{
			uIElement = SettingsPages.Build(key, pageContext);
			ObservePageStores(key);
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Settings page '" + key + "' failed: " + ex.Message, ex);
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(SUI.PageHeader("Something went wrong", "This page couldn't be shown. Details are in the Ayn log."));
			uIElement = stackPanel;
		}
		uIElement.Visibility = Visibility.Collapsed;
		_pageGrid.Children.Add(uIElement);
		CachedPage cachedPage = new CachedPage();
		cachedPage.View = uIElement;
		cachedPage.Ctx = pageContext;
		value = cachedPage;
		_pages[key] = value;
		pageContext.Hide();
		return value;
	}

	private new void Drop(string key, CachedPage p)
	{
		_pages.Remove(key);
		try
		{
			p.Ctx.RunCleanup();
		}
		catch
		{
		}
		_pageGrid.Children.Remove(p.View);
		if (_currentCached == p)
		{
			_currentCached = null;
			_currentView = null;
		}
	}

	private void Present(UIElement view, CachedPage cached)
	{
		UIElement currentView = _currentView;
		CachedPage currentCached = _currentCached;
		view.Visibility = Visibility.Visible;
		if (currentView != null && currentView != view)
		{
			currentView.Visibility = Visibility.Collapsed;
		}
		_currentView = view;
		_currentCached = cached;
		if (currentCached != null && currentCached != cached)
		{
			currentCached.Ctx.Hide();
		}
		cached?.Ctx.Show();
		SSettingBinding.RefreshTree(view);
		if (currentView != null && currentView != view && currentCached == null)
		{
			_pageGrid.Children.Remove(currentView);
		}
		_scroll.ScrollToTop();
		UpdateNavSelection();
	}

	internal void InvalidatePage(string key)
	{
		if (!_closed && _pages.TryGetValue(key, out var value))
		{
			if (!_showingSearch && _page == key && value == _currentCached)
			{
				ReloadPage();
				return;
			}
			value.Dirty = true;
			ScheduleBackgroundBuilds();
		}
	}

	private void MarkOthersStale()
	{
		if (_closed)
		{
			return;
		}
		foreach (CachedPage value in _pages.Values)
		{
			if (value != _currentCached)
			{
				value.Dirty = true;
			}
		}
		ScheduleBackgroundBuilds();
	}

	private void QueueStateRefresh(object source)
	{
		if (_closed)
		{
			return;
		}
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				QueueStateRefresh(source);
			});
			return;
		}
		_pendingStores.Add((source == null) ? "" : source.GetType().Name);
		if (_stateRefreshQueued)
		{
			return;
		}
		_stateRefreshQueued = true;
		base.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)delegate
		{
			_stateRefreshQueued = false;
			if (!_closed)
			{
				HashSet<string> hashSet = new HashSet<string>();
				using (HashSet<string>.Enumerator enumerator = _pendingStores.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						object obj;
						switch (enumerator.Current)
						{
						default:
							obj = null;
							break;
						case "AnalyticsService":
							obj = new string[1] { "module:analytics" };
							break;
						case "AICodingService":
							obj = new string[1] { "module:aicoding" };
							break;
						case "ScreenTimeService":
							obj = new string[2] { "privacy", "module:screentime" };
							break;
						case "CalendarService":
							obj = new string[1] { "module:calendar" };
							break;
						case "TimerService":
							obj = new string[1] { "module:timer" };
							break;
						case "ClockSettings":
							obj = new string[3] { "general", "notch", "module:clock" };
							break;
						case "ClipboardService":
						case "AppPreferences":
							obj = new string[3] { "general", "privacy", "module:clipboard" };
							break;
						case "WeatherService":
							obj = new string[2] { "general", "module:weather" };
							break;
						}
						string[] array = (string[])obj;
						if (array == null)
						{
							foreach (string key in _pages.Keys)
							{
								hashSet.Add(key);
							}
						}
						else
						{
							string[] array2 = array;
							foreach (string item in array2)
							{
								hashSet.Add(item);
							}
						}
					}
				}
				_pendingStores.Clear();
				foreach (string item2 in hashSet)
				{
					if (_pages.TryGetValue(item2, out var value) && value != _currentCached)
					{
						value.Dirty = true;
					}
				}
				ScheduleBackgroundBuilds();
				bool flag = SSettingBinding.StructureChanged(_currentView);
				if (flag && Keyboard.FocusedElement is System.Windows.Controls.TextBox)
				{
					if (_currentCached != null)
					{
						_currentCached.Dirty = true;
					}
					SSettingBinding.RefreshTree(_currentView);
				}
				else if (flag && Mouse.Captured == null)
				{
					ReloadPage();
				}
				else
				{
					SSettingBinding.RefreshTree(_currentView);
				}
			}
		});
	}

	private void ObservePageStores(string key)
	{
		if ((key == "general" || key == "module:weather") && _observedStores.Add("weather"))
		{
			WeatherService service = WeatherService.Instance;
			service.WeatherUpdated += _staleHandler;
			_stateUnhooks.Add(delegate
			{
				service.WeatherUpdated -= _staleHandler;
			});
		}
		if ((key == "privacy" || key == "module:clipboard") && _observedStores.Add("clipboard"))
		{
			ClipboardService service2 = ClipboardService.Instance;
			service2.MonitoringStateChanged += _staleHandler;
			_stateUnhooks.Add(delegate
			{
				service2.MonitoringStateChanged -= _staleHandler;
			});
		}
		if (key == "module:timer" && _observedStores.Add("timer"))
		{
			TimerService service3 = TimerService.Instance;
			service3.SettingsChanged += _staleHandler;
			service3.HydrationChanged += _staleHandler;
			_stateUnhooks.Add(delegate
			{
				service3.SettingsChanged -= _staleHandler;
				service3.HydrationChanged -= _staleHandler;
			});
		}
		if (key == "module:calendar" && _observedStores.Add("calendar"))
		{
			CalendarService service4 = CalendarService.Instance;
			service4.Changed += _staleHandler;
			_stateUnhooks.Add(delegate
			{
				service4.Changed -= _staleHandler;
			});
		}
		if (key == "module:screentime" && _observedStores.Add("screentime"))
		{
			ScreenTimeService service5 = ScreenTimeService.Instance;
			service5.UsageUpdated += _staleHandler;
			_stateUnhooks.Add(delegate
			{
				service5.UsageUpdated -= _staleHandler;
			});
		}
		if (key == "module:aicoding" && _observedStores.Add("aicoding"))
		{
			AICodingService service6 = AICodingService.Instance;
			service6.Updated += _staleHandler;
			_stateUnhooks.Add(delegate
			{
				service6.Updated -= _staleHandler;
			});
		}
		if (key == "module:analytics" && _observedStores.Add("analytics"))
		{
			AnalyticsService service7 = AnalyticsService.Instance;
			Action changed = delegate
			{
				QueueStateRefresh(service7);
			};
			service7.SettingsChanged += changed;
			_stateUnhooks.Add(delegate
			{
				service7.SettingsChanged -= changed;
			});
		}
	}

	private void ScheduleBackgroundBuilds()
	{
		if (_closed)
		{
			return;
		}
		if (_bgTimer == null)
		{
			_bgTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
			{
				Interval = TimeSpan.FromMilliseconds(450.0)
			};
			_bgTimer.Tick += delegate
			{
				BuildNextInBackground();
			};
		}
		_bgTimer.Stop();
		_bgTimer.Start();
	}

	private void BuildNextInBackground()
	{
		if (_closed || Mouse.LeftButton == MouseButtonState.Pressed)
		{
			return;
		}
		string text = null;
		foreach (KeyValuePair<string, CachedPage> page in _pages)
		{
			if (page.Value.Dirty && page.Value != _currentCached)
			{
				text = page.Key;
				break;
			}
		}
		if (text == null)
		{
			PageInfo[] all = PageInfo.All;
			foreach (PageInfo pageInfo in all)
			{
				if (!_pages.ContainsKey(pageInfo.Key))
				{
					text = pageInfo.Key;
					break;
				}
			}
		}
		if (text == null)
		{
			_bgTimer.Stop();
			return;
		}
		try
		{
			Obtain(text);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Settings background build of '" + text + "' failed: " + ex.Message);
		}
	}

	internal void HighlightRow(string settingId)
	{
		base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
		{
			FrameworkElement frameworkElement = FindRow(_currentView, settingId);
			if (frameworkElement != null)
			{
				frameworkElement.BringIntoView();
				Border flash = new Border
				{
					Background = new SolidColorBrush(((SolidColorBrush)SettingsTheme.Highlight).Color),
					IsHitTestVisible = false
				};
				System.Windows.Controls.Panel host = frameworkElement as System.Windows.Controls.Panel;
				if (host != null)
				{
					Grid.SetColumnSpan(flash, Math.Max(1, (!(host is Grid)) ? 1 : ((Grid)host).ColumnDefinitions.Count));
					host.Children.Insert(0, flash);
					TranslateTransform move = new TranslateTransform(-4.0, 0.0);
					flash.RenderTransform = move;
					SMotion.To(move, TranslateTransform.XProperty, 0.0, flash.IsVisible);
					DispatcherTimer removal = new DispatcherTimer
					{
						Interval = TimeSpan.FromMilliseconds(650.0)
					};
					EventHandler remove = null;
					remove = delegate
					{
						removal.Stop();
						removal.Tick -= remove;
						SMotion.Finish(move);
						host.Children.Remove(flash);
					};
					removal.Tick += remove;
					flash.IsVisibleChanged += delegate
					{
						if (!flash.IsVisible)
						{
							remove(null, EventArgs.Empty);
						}
					};
					removal.Start();
				}
				FindFocusable(frameworkElement)?.Focus();
			}
		});
	}

	private static FrameworkElement FindRow(DependencyObject root, string tag)
	{
		if (root == null)
		{
			return null;
		}
		if (root is FrameworkElement frameworkElement && object.Equals(frameworkElement.Tag, tag))
		{
			return frameworkElement;
		}
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			FrameworkElement frameworkElement2 = FindRow(child as DependencyObject, tag);
			if (frameworkElement2 != null)
			{
				return frameworkElement2;
			}
		}
		return null;
	}

	private static UIElement FindFocusable(DependencyObject root)
	{
		if (root is UIElement { Focusable: not false, IsEnabled: not false, IsVisible: not false } uIElement)
		{
			return uIElement;
		}
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is UIElement { Focusable: not false, IsEnabled: not false, IsVisible: not false } uIElement2)
			{
				return uIElement2;
			}
			if (child is DependencyObject root2)
			{
				UIElement uIElement3 = FindFocusable(root2);
				if (uIElement3 != null)
				{
					return uIElement3;
				}
			}
		}
		return null;
	}

	internal IEnumerable<KeyValuePair<string, UIElement>> SearchPages()
	{
		try
		{
			PageInfo[] all = PageInfo.All;
			foreach (PageInfo p in all)
			{
				yield return new KeyValuePair<string, UIElement>(p.Key, Obtain(p.Key).View);
			}
		}
		finally
		{
		}
		foreach (IBuddyTool tool in ModuleCatalog.DockOrder())
		{
			string key = "module:" + tool.Id;
			yield return new KeyValuePair<string, UIElement>(key, Obtain(key).View);
		}
	}

	private void OnSearchChanged()
	{
		string text = _search.Text.Trim();
		if (text.Length == 0)
		{
			if (_showingSearch)
			{
				_showingSearch = false;
				ShowPage();
			}
			return;
		}
		_showingSearch = true;
		if (_searchCtx != null)
		{
			_searchCtx.RunCleanup();
		}
		_searchCtx = new PageContext(this);
		try
		{
			_hits = SettingsSearch.Find(text);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Settings search failed: " + ex.Message);
			_hits = new List<SettingsSearch.Hit>();
		}
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(SUI.PageHeader("Search", (_hits.Count == 0) ? $"No settings match “{text}”" : string.Format("{0} result{1} for “{2}”", _hits.Count, (_hits.Count == 1) ? "" : "s", text)));
		_resultsStack = null;
		if (_hits.Count > 0)
		{
			Border border = SUI.Group();
			border.Margin = new Thickness(0.0, 32.0, 0.0, 0.0);
			_resultsStack = SUI.GroupStack(border);
			KeyboardNavigation.SetDirectionalNavigation(_resultsStack, KeyboardNavigationMode.Contained);
			foreach (SettingsSearch.Hit item in _hits.Take(60))
			{
				SettingsSearch.Hit h = item;
				SClickable sClickable = new SClickable(0.0, h.Title + ", " + h.Where);
				sClickable.MinHeight = 52.0;
				SClickable sClickable2 = sClickable;
				sClickable2.VisualRole = SVisualRole.Navigation;
				Grid grid = new Grid();
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				FrameworkElement frameworkElement = SettingsSearch.TileFor(h.Page, 28.0);
				frameworkElement.Margin = new Thickness(16.0, 0.0, 0.0, 0.0);
				grid.Children.Add(frameworkElement);
				StackPanel stackPanel2 = new StackPanel();
				stackPanel2.VerticalAlignment = VerticalAlignment.Center;
				stackPanel2.Margin = new Thickness(12.0, 8.0, 12.0, 8.0);
				StackPanel stackPanel3 = stackPanel2;
				stackPanel3.Children.Add(SettingsTheme.Text(h.Title, 15.0, SettingsTheme.TextPrimary, FontWeights.Normal));
				TextBlock textBlock = SettingsTheme.Text(h.Where, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
				textBlock.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
				stackPanel3.Children.Add(textBlock);
				Grid.SetColumn(stackPanel3, 1);
				grid.Children.Add(stackPanel3);
				FrameworkElement frameworkElement2 = Glyphs.Create("chevron_right", 12.0, SettingsTheme.TextSecondary);
				frameworkElement2.Margin = new Thickness(0.0, 0.0, 16.0, 0.0);
				frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
				Grid.SetColumn(frameworkElement2, 2);
				grid.Children.Add(frameworkElement2);
				sClickable2.Child = grid;
				sClickable2.Click += delegate
				{
					OpenHit(h);
				};
				SUI.AddToGroup(_resultsStack, sClickable2);
			}
			stackPanel.Children.Add(border);
		}
		_pageGrid.Children.Add(stackPanel);
		Present(stackPanel, null);
	}

	private void OpenHit(SettingsSearch.Hit h)
	{
		ClearSearch(restorePage: false);
		Navigate(h.Page);
		if (!string.IsNullOrEmpty(h.SettingId))
		{
			HighlightRow(h.SettingId);
		}
	}

	private void OnSearchKey(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.Return && _hits.Count > 0 && _showingSearch)
		{
			OpenHit(_hits[0]);
			e.Handled = true;
		}
		else if (e.Key == Key.Down && _showingSearch && _resultsStack != null)
		{
			SClickable sClickable = _resultsStack.Children.OfType<SClickable>().FirstOrDefault();
			if (sClickable != null)
			{
				sClickable.Focus();
				e.Handled = true;
			}
		}
		else if (e.Key == Key.Escape && _search.Text.Length > 0)
		{
			ClearSearch(restorePage: true);
			e.Handled = true;
		}
	}

	private void ClearSearch(bool restorePage)
	{
		if (_search != null && _search.Text.Length != 0)
		{
			bool showingSearch = _showingSearch;
			_showingSearch = false;
			_search.Text = "";
			if (showingSearch && restorePage && _page != null)
			{
				ShowPage();
			}
		}
	}

	private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		if (Keyboard.Modifiers == ModifierKeys.Control && (key == Key.K || key == Key.F) && !(Keyboard.FocusedElement is SHotkeyRecorder { IsRecording: not false }))
		{
			_search.Box.Focus();
			_search.Box.SelectAll();
			e.Handled = true;
		}
	}

	private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.Escape && !e.Handled)
		{
			e.Handled = true;
			Close();
		}
	}
}
