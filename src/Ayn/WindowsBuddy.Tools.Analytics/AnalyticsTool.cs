using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Analytics;

public class AnalyticsTool : IBuddyTool, IHeaderStatusLead, IEscapeAware
{
	private enum Mode
	{
		Dashboard,
		Picker,
		Form,
		Manage
	}

	public sealed class VisitorsChart : FrameworkElement
	{
		private List<SeriesPoint> _points = new List<SeriesPoint>();

		private int _hover = -1;

		private static readonly Pen Line = MakePen(BuddyTheme.Text100, 1.6, null);

		private static readonly Pen Guide = MakePen(BuddyTheme.Text30, 1.0, new DashStyle(new double[2] { 2.0, 3.0 }, 0.0));

		private static readonly Brush Area = MakeArea();

		public int HoverIndex => _hover;

		public event Action<int, Point> HoverChanged;

		public VisitorsChart()
		{
			base.ClipToBounds = false;
			base.Cursor = Cursors.Arrow;
			base.SnapsToDevicePixels = true;
		}

		private static Pen MakePen(Brush b, double w, DashStyle dash)
		{
			Pen pen = new Pen(b, w);
			pen.LineJoin = PenLineJoin.Round;
			pen.StartLineCap = PenLineCap.Round;
			pen.EndLineCap = PenLineCap.Round;
			Pen pen2 = pen;
			if (dash != null)
			{
				pen2.DashStyle = dash;
			}
			ThemeTokens.FreezeSafe(pen2);
			return pen2;
		}

		private static Brush MakeArea()
		{
			return ThemeTokens.AccentSoft;
		}

		public void SetPoints(List<SeriesPoint> pts, AnalyticsRange range)
		{
			_points = pts ?? new List<SeriesPoint>();
			_hover = -1;
			InvalidateVisual();
			this.HoverChanged?.Invoke(-1, default(Point));
		}

		public Point[] Layout()
		{
			int count = _points.Count;
			Point[] array = new Point[count];
			if (count == 0)
			{
				return array;
			}
			double num = Math.Max(1.0, base.ActualWidth);
			double num2 = Math.Max(1.0, base.ActualHeight);
			long num3 = Math.Max(1L, _points.Max((SeriesPoint p) => p.Value));
			double num4 = (double)num3 * 1.06;
			for (int i = 0; i < count; i++)
			{
				double x = ((count == 1) ? (num - 5.0) : (3.0 + (double)i * (num - 3.0 - 5.0) / (double)(count - 1)));
				double y = 8.0 + (num2 - 8.0) * (1.0 - (double)_points[i].Value / num4);
				ref Point reference = ref array[i];
				reference = new Point(x, y);
			}
			return array;
		}

		protected override void OnRender(DrawingContext dc)
		{
			dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight));
			Point[] array = Layout();
			if (array.Length != 0)
			{
				double actualHeight = base.ActualHeight;
				if (array.Length > 1)
				{
					Geometry geometry = Smooth(array, closeToBottom: false, actualHeight);
					Geometry geometry2 = Smooth(array, closeToBottom: true, actualHeight);
					dc.DrawGeometry(Area, null, geometry2);
					dc.DrawGeometry(null, Line, geometry);
				}
				Point center = array[array.Length - 1];
				dc.DrawEllipse(BuddyTheme.Text100, null, center, 3.6, 3.6);
				if (_hover >= 0 && _hover < array.Length)
				{
					Point center2 = array[_hover];
					dc.DrawLine(Guide, new Point(Math.Round(center2.X) + 0.5, center2.Y + 4.0), new Point(Math.Round(center2.X) + 0.5, actualHeight));
					dc.DrawEllipse(BuddyTheme.Text100, null, center2, 3.6, 3.6);
				}
			}
		}

		public static Geometry Smooth(Point[] p, bool closeToBottom, double bottom)
		{
			int num = p.Length;
			double[] array = new double[num];
			double[] array2 = new double[num - 1];
			for (int i = 0; i < num - 1; i++)
			{
				array2[i] = (p[i + 1].Y - p[i].Y) / Math.Max(1E-06, p[i + 1].X - p[i].X);
			}
			array[0] = array2[0];
			array[num - 1] = array2[num - 2];
			for (int j = 1; j < num - 1; j++)
			{
				array[j] = ((array2[j - 1] * array2[j] <= 0.0) ? 0.0 : ((array2[j - 1] + array2[j]) / 2.0));
			}
			for (int k = 0; k < num - 1; k++)
			{
				if (Math.Abs(array2[k]) < 1E-09)
				{
					array[k] = 0.0;
					array[k + 1] = 0.0;
					continue;
				}
				double num2 = array[k] / array2[k];
				double num3 = array[k + 1] / array2[k];
				double num4 = num2 * num2 + num3 * num3;
				if (num4 > 9.0)
				{
					double num5 = 3.0 / Math.Sqrt(num4);
					array[k] = num5 * num2 * array2[k];
					array[k + 1] = num5 * num3 * array2[k];
				}
			}
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(closeToBottom ? new Point(p[0].X, bottom) : p[0], closeToBottom, closeToBottom);
				if (closeToBottom)
				{
					streamGeometryContext.LineTo(p[0], isStroked: false, isSmoothJoin: false);
				}
				for (int l = 0; l < num - 1; l++)
				{
					double num6 = (p[l + 1].X - p[l].X) / 3.0;
					streamGeometryContext.BezierTo(new Point(p[l].X + num6, p[l].Y + array[l] * num6), new Point(p[l + 1].X - num6, p[l + 1].Y - array[l + 1] * num6), p[l + 1], isStroked: true, isSmoothJoin: true);
				}
				if (closeToBottom)
				{
					streamGeometryContext.LineTo(new Point(p[num - 1].X, bottom), isStroked: false, isSmoothJoin: false);
				}
			}
			streamGeometry.Freeze();
			return streamGeometry;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			Point[] array = Layout();
			if (array.Length == 0)
			{
				return;
			}
			double x = e.GetPosition(this).X;
			int num = 0;
			for (int i = 1; i < array.Length; i++)
			{
				if (Math.Abs(array[i].X - x) < Math.Abs(array[num].X - x))
				{
					num = i;
				}
			}
			SetHover(num, array[num]);
		}

		protected override void OnMouseLeave(MouseEventArgs e)
		{
			SetHover(-1, default(Point));
		}

		public void SetHover(int i, Point at)
		{
			if (i != _hover)
			{
				_hover = i;
				InvalidateVisual();
				this.HoverChanged?.Invoke(i, at);
			}
		}
	}

	private const double RightWidth = 244.0;

	private const double HeroSize = 28.0;

	private const string NotchActivityId = "analytics_live";

	public const string PlausibleKeysUrl = "https://plausible.io/settings/api-keys";

	public const string DataFastUrl = "https://datafa.st/dashboard";

	private static readonly Brush OnlineDot = Frozen(Color.FromRgb(48, 209, 88));

	private readonly AnalyticsService _svc;

	private Grid _root;

	private Mode _mode;

	private Border _siteSwitch;

	private TextBlock _siteText;

	private StackPanel _headerActions;

	private StackPanel _online;

	private TextBlock _onlineText;

	private Grid _dashboard;

	private TextBlock _updated;

	private TextBlock _hero;

	private TextBlock _changeValue;

	private TextBlock _changeLabel;

	private TextBlock _message;

	private ContentControl _changeArrow;

	private StackPanel _change;

	private VisitorsChart _chart;

	private Canvas _tipLayer;

	private Border _tip;

	private TextBlock _tipText;

	private Grid _axis;

	private Grid _stats;

	private StackPanel _rows;

	private TextBlock _rowsEmpty;

	private AnalyticsSnapshot _snap;

	private Exception _lastError;

	private bool _loading;

	private int? _onlineCount;

	private DispatcherTimer _dataTimer;

	private DispatcherTimer _realtimeTimer;

	private DispatcherTimer _notchTimer;

	private bool _notchPublished;

	private BuddySlidingSegmented _rangeSeg;

	private TextBlock _updatedLabel;

	private Border _refresh;

	private StackPanel _loadingRow;

	private ConnectorKit.Notice _dashNotice;

	private BuddySlidingSegmented _breakdownSeg;

	private string _formProvider;

	internal static readonly Color PlausibleBrand = Color.FromRgb(88, 80, 236);

	internal static readonly Color DataFastBrand = Color.FromRgb(242, 107, 58);

	internal static readonly Color GoogleBrand = Color.FromRgb(249, 171, 0);

	private ConnectorKit.SoftField _fDomain;

	private ConnectorKit.SoftField _fKey;

	private ConnectorKit.SoftField _fServer;

	private ConnectorKit.SoftField _fProperty;

	private ConnectorKit.SoftField _fClientId;

	private ConnectorKit.SoftField _fClientSecret;

	private ConnectorKit.Notice _fNotice;

	private ConnectorKit.BusyButton _fConnect;

	private TextBlock _fSaFile;

	private string _gaMode = "oauth";

	private string _gaRefresh;

	private string _gaJson;

	private string _gaPropertyName;

	private List<GaProperty> _gaProperties;

	private CancellationTokenSource _signInCancel;

	private int _connectGeneration;

	public string Id => "analytics";

	public string DisplayName => "Analytics";

	public ToolCategory Category => ToolCategory.Utility;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => false;

	public bool RequiresNetwork => true;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	private static Brush Track => BuddyTint.Ink(18);

	private TimeSpan StaleAfter
	{
		get
		{
			if (_svc.Settings.Range != 0)
			{
				return TimeSpan.FromMinutes(10.0);
			}
			return TimeSpan.FromMinutes(2.0);
		}
	}

	internal Grid StatsGrid => _stats;

	internal ConnectorKit.Notice DashNotice => _dashNotice;

	internal TextBlock UpdatedText => _updated;

	internal TextBlock HeroText => _hero;

	internal TextBlock MessageText => _message;

	internal ConnectorKit.SoftField FormDomain => _fDomain;

	internal ConnectorKit.SoftField FormKey => _fKey;

	internal ConnectorKit.SoftField FormServer => _fServer;

	internal ConnectorKit.Notice FormNotice => _fNotice;

	internal ConnectorKit.BusyButton FormConnect => _fConnect;

	internal string ModeName => _mode.ToString();

	internal BuddySlidingSegmented RangeControl => _rangeSeg;

	private static Brush Frozen(Color c)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(c);
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public AnalyticsTool()
	{
		IsEnabled = true;
		OrderIndex = 24;
		State = ToolState.Idle;
		_svc = AnalyticsService.Instance;
		if (Application.Current != null)
		{
			UpdateNotchTimer();
		}
	}

	public void Initialize()
	{
		if (!IsInitialized)
		{
			IsInitialized = true;
			_dataTimer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(60.0)
			};
			_dataTimer.Tick += delegate
			{
				UpdateAgo();
				RefreshIfStale(force: false);
			};
			_realtimeTimer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(45.0)
			};
			_realtimeTimer.Tick += delegate
			{
				RefreshOnline();
			};
		}
	}

	public void Shutdown()
	{
		StopTimers();
		if (_notchTimer != null)
		{
			_notchTimer.Stop();
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		if (!IsInitialized)
		{
			Initialize();
		}
		if (_root != null)
		{
			if (_svc.VisibleSites.Count == 0 && _mode == Mode.Dashboard)
			{
				_mode = Mode.Picker;
			}
			Render(animate: false);
			StartTimers();
		}
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		StopTimers();
	}

	private void StartTimers()
	{
		if (_svc.ActiveSite != null && _dataTimer != null)
		{
			ShowCached();
			RefreshIfStale(force: false);
			RefreshOnline();
			_dataTimer.Start();
			_realtimeTimer.Start();
		}
	}

	private void StopTimers()
	{
		if (_dataTimer != null)
		{
			_dataTimer.Stop();
		}
		if (_realtimeTimer != null)
		{
			_realtimeTimer.Stop();
		}
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateAnalyticsIcon(size, stroke);
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return null;
	}

	public bool HandleEscape()
	{
		if (_mode != 0 && _svc.VisibleSites.Count > 0)
		{
			_mode = Mode.Dashboard;
			Render();
			StartTimers();
			return true;
		}
		return false;
	}

	public UIElement GetHeaderStatusLead()
	{
		if (_siteSwitch != null)
		{
			return _siteSwitch;
		}
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		_siteText = BuddyTheme.Text("", 15.0, BuddyTheme.Text50, FontWeights.Medium);
		_siteText.FontFamily = BuddyTheme.DisplayFont;
		_siteText.MaxWidth = 210.0;
		_siteText.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(_siteText);
		FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 14.0, BuddyTheme.Text50, filled: false);
		frameworkElement.Margin = new Thickness(8.0, 2.0, 0.0, 0.0);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(frameworkElement);
		_siteSwitch = new Border
		{
			Child = stackPanel2,
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Padding = new Thickness(0.0, 4.0, 0.0, 4.0)
		};
		_siteSwitch.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_siteSwitch.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (!BuddyDropdown.JustClosed(_siteSwitch))
			{
				ShowSiteMenu();
			}
		};
		UpdateHeader();
		return _siteSwitch;
	}

	public UIElement GetHeaderActions()
	{
		if (_headerActions != null)
		{
			return _headerActions;
		}
		_headerActions = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		_online = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 14.0, 0.0),
			Background = Brushes.Transparent
		};
		_online.Children.Add(new Ellipse
		{
			Width = 7.0,
			Height = 7.0,
			Fill = OnlineDot,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 1.0, 7.0, 0.0)
		});
		_onlineText = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		Typography.SetNumeralAlignment(_onlineText, FontNumeralAlignment.Tabular);
		_onlineText.VerticalAlignment = VerticalAlignment.Center;
		_online.Children.Add(_onlineText);
		_headerActions.Children.Add(_online);
		_rangeSeg = new BuddySlidingSegmented(new string[3] { "Today", "7d", "30d" }, RangeIndex(_svc.Settings.Range), 28.0, 11.5, 52.0);
		_rangeSeg.SelectionChanged += delegate(int i)
		{
			SetRange(i switch
			{
				1 => AnalyticsRange.Week, 
				0 => AnalyticsRange.Today, 
				_ => AnalyticsRange.Month, 
			});
		};
		ConnectorKit.AutomationName(_rangeSeg, "Period");
		_headerActions.Children.Add(_rangeSeg);
		UpdateHeader();
		return _headerActions;
	}

	private static int RangeIndex(AnalyticsRange r)
	{
		return r switch
		{
			AnalyticsRange.Week => 1, 
			AnalyticsRange.Today => 0, 
			_ => 2, 
		};
	}

	private void UpdateHeader()
	{
		AnalyticsSite activeSite = _svc.ActiveSite;
		bool flag = _mode == Mode.Dashboard && activeSite != null;
		if (_siteSwitch != null)
		{
			_siteSwitch.Visibility = ((activeSite == null) ? Visibility.Collapsed : Visibility.Visible);
			if (activeSite != null)
			{
				_siteText.Text = activeSite.DisplayName ?? activeSite.SiteId;
			}
		}
		if (_headerActions != null)
		{
			_headerActions.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			if (_rangeSeg != null && _rangeSeg.Selected != RangeIndex(_svc.Settings.Range))
			{
				_rangeSeg.Selected = RangeIndex(_svc.Settings.Range);
			}
			IAnalyticsProvider analyticsProvider = ((activeSite != null) ? AnalyticsProviders.For(activeSite.Provider) : null);
			bool flag2 = flag && _onlineCount.HasValue && analyticsProvider != null;
			_online.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			if (flag2)
			{
				_onlineText.Text = _onlineCount.Value.ToString("N0", CultureInfo.CurrentCulture) + " online";
				_online.ToolTip = BuddyTooltip.Create($"Visitors in the last {analyticsProvider.RealtimeWindowMinutes} minutes · {analyticsProvider.Name}");
			}
		}
	}

	private void ShowSiteMenu()
	{
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		foreach (AnalyticsSite visibleSite in _svc.VisibleSites)
		{
			AnalyticsSite site = visibleSite;
			list.Add(new BuddyMenuItem(site.DisplayName ?? site.SiteId, null, delegate
			{
				SwitchSite(site.Key);
			})
			{
				IsChecked = (_svc.ActiveSite == site)
			});
		}
		list.Add(new BuddyMenuItem("Add site", "add", delegate
		{
			_mode = Mode.Picker;
			StopTimers();
			Render();
		}));
		list.Add(new BuddyMenuItem("Manage", "settings", delegate
		{
			_mode = Mode.Manage;
			StopTimers();
			Render();
		}));
		BuddyDropdown.Show(_siteSwitch, list, null, 220.0);
	}

	private void SwitchSite(string key)
	{
		if (!(_svc.Settings.ActiveSiteKey == key) || _mode != 0 || _snap == null || !(_snap.SiteKey == key))
		{
			_svc.Settings.ActiveSiteKey = key;
			_svc.Save();
			_mode = Mode.Dashboard;
			_onlineCount = null;
			_lastError = null;
			Render();
			StartTimers();
		}
	}

	private void SetRange(AnalyticsRange r)
	{
		if (_svc.Settings.Range != r)
		{
			_svc.Settings.Range = r;
			_svc.Save();
			_lastError = null;
			UpdateHeader();
			ShowCached();
			RefreshIfStale(force: false);
		}
	}

	public UIElement CreateView()
	{
		_root = BuddyTheme.Crisp(new Grid
		{
			Background = BuddyTheme.Canvas,
			Tag = "NoOuterScroll",
			ClipToBounds = true
		});
		if (_svc.VisibleSites.Count == 0)
		{
			_mode = Mode.Picker;
		}
		Render(animate: false);
		if (State == ToolState.Active)
		{
			StartTimers();
		}
		return _root;
	}

	private void Render(bool animate = true)
	{
		if (_root == null)
		{
			return;
		}
		_root.Children.Clear();
		switch (_mode)
		{
		case Mode.Picker:
			_root.Children.Add(BuildPicker(animate));
			break;
		case Mode.Form:
			_root.Children.Add(BuildForm(animate));
			break;
		case Mode.Manage:
			_root.Children.Add(BuildManage());
			break;
		default:
			if (_dashboard == null)
			{
				_dashboard = BuildDashboard();
			}
			_root.Children.Add(_dashboard);
			ShowCached();
			break;
		}
		UpdateHeader();
	}

	private Grid BuildDashboard()
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(18.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(272.0)
		});
		grid.Children.Add(BuildLeft());
		Border element = ConnectorKit.Card(BuildRight(), new Thickness(14.0, 12.0, 14.0, 10.0));
		Grid.SetColumn(element, 2);
		grid.Children.Add(element);
		return grid;
	}

	private FrameworkElement BuildLeft()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(16.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid grid2 = new Grid();
		BuddyLabel buddyLabel = BuddyTheme.LabelText("VISITORS");
		buddyLabel.HorizontalAlignment = HorizontalAlignment.Left;
		buddyLabel.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(buddyLabel);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		_updated = ConnectorKit.Text("", 11.5, ThemeTokens.Text3, AynType.Medium);
		_updated.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(_updated);
		_refresh = ConnectorKit.Link("Refresh", delegate
		{
			_lastError = null;
			RefreshIfStale(force: true);
			RefreshOnline();
		});
		_refresh.Margin = new Thickness(8.0, -4.0, -2.0, -4.0);
		((TextBlock)_refresh.Child).FontSize = 11.5;
		ConnectorKit.AutomationName(_refresh, "Refresh");
		stackPanel2.Children.Add(_refresh);
		grid2.Children.Add(stackPanel2);
		_updatedLabel = _updated;
		grid.Children.Add(grid2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		StackPanel stackPanel4 = stackPanel3;
		_hero = BuddyTheme.Text("", 28.0, BuddyTheme.Text100, AynType.Semibold);
		_hero.FontFamily = BuddyTheme.DisplayFont;
		_hero.LineHeight = 30.0;
		_hero.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		_hero.TextTrimming = TextTrimming.None;
		Typography.SetNumeralAlignment(_hero, FontNumeralAlignment.Tabular);
		stackPanel4.Children.Add(_hero);
		_change = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(18.0, 6.0, 0.0, 0.0)
		};
		_changeArrow = new ContentControl
		{
			Width = 15.0,
			Height = 15.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Focusable = false
		};
		_change.Children.Add(_changeArrow);
		_changeValue = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, AynType.Medium);
		Typography.SetNumeralAlignment(_changeValue, FontNumeralAlignment.Tabular);
		_changeValue.VerticalAlignment = VerticalAlignment.Center;
		_change.Children.Add(_changeValue);
		_changeLabel = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, AynType.Regular);
		_changeLabel.VerticalAlignment = VerticalAlignment.Center;
		_changeLabel.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
		_change.Children.Add(_changeLabel);
		stackPanel4.Children.Add(_change);
		Grid.SetRow(stackPanel4, 1);
		grid.Children.Add(stackPanel4);
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		Grid grid4 = grid3;
		_chart = new VisitorsChart();
		_chart.HoverChanged += OnChartHover;
		grid4.Children.Add(_chart);
		_tipLayer = new Canvas
		{
			IsHitTestVisible = false,
			ClipToBounds = false
		};
		_tipText = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
		Typography.SetNumeralAlignment(_tipText, FontNumeralAlignment.Tabular);
		_tip = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(9.0, 4.0, 9.0, 4.0),
			Child = _tipText,
			Visibility = Visibility.Collapsed
		};
		Action<Border> action = delegate(Border b)
		{
			b.Background = (ThemeTokens.IsLight ? ThemeTokens.Surface : ThemeTokens.Surface3);
			b.Effect = (ThemeTokens.IsLight ? ThemeTokens.CardElevation : null);
		};
		action(_tip);
		ThemeTokens.Subscribe(_tip, action);
		_tipLayer.Children.Add(_tip);
		grid4.Children.Add(_tipLayer);
		_loadingRow = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		_loadingRow.Children.Add(new ConnectorKit.Spinner(14.0, ThemeTokens.Text2)
		{
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		TextBlock textBlock = ConnectorKit.Text("Loading your stats…", 13.0, ThemeTokens.Text2, AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		_loadingRow.Children.Add(textBlock);
		grid4.Children.Add(_loadingRow);
		_message = ConnectorKit.Wrap("", 13.0, ThemeTokens.Text2, AynType.Regular);
		_message.HorizontalAlignment = HorizontalAlignment.Center;
		_message.VerticalAlignment = VerticalAlignment.Center;
		_message.TextAlignment = TextAlignment.Center;
		_message.MaxWidth = 340.0;
		grid4.Children.Add(_message);
		_dashNotice = new ConnectorKit.Notice
		{
			VerticalAlignment = VerticalAlignment.Center,
			MaxWidth = 420.0
		};
		grid4.Children.Add(_dashNotice);
		Grid.SetRow(grid4, 2);
		grid.Children.Add(grid4);
		_axis = new Grid
		{
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		Grid.SetRow(_axis, 3);
		grid.Children.Add(_axis);
		_stats = new Grid
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		Grid.SetRow(_stats, 4);
		grid.Children.Add(_stats);
		return grid;
	}

	private FrameworkElement BuildRight()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Breakdown breakdown = _svc.Settings.Breakdown;
		_breakdownSeg = new BuddySlidingSegmented(new string[3] { "Pages", "Sources", "Countries" }, breakdown switch
		{
			Breakdown.Sources => 1, 
			Breakdown.Pages => 0, 
			_ => 2, 
		}, 26.0, 11.5, 70.0);
		_breakdownSeg.SelectionChanged += delegate(int i)
		{
			SetBreakdown(i switch
			{
				1 => Breakdown.Sources, 
				0 => Breakdown.Pages, 
				_ => Breakdown.Countries, 
			});
		};
		ConnectorKit.AutomationName(_breakdownSeg, "Breakdown");
		grid.Children.Add(_breakdownSeg);
		TextBlock textBlock = ConnectorKit.Text("Visitors", 11.5, ThemeTokens.Text3, AynType.Medium);
		textBlock.HorizontalAlignment = HorizontalAlignment.Right;
		textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 2.0);
		Grid.SetRow(textBlock, 1);
		grid.Children.Add(textBlock);
		Grid grid2 = new Grid();
		_rows = new StackPanel();
		grid2.Children.Add(_rows);
		_rowsEmpty = ConnectorKit.Wrap("", 13.0, ThemeTokens.Text2, AynType.Regular);
		_rowsEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_rowsEmpty.VerticalAlignment = VerticalAlignment.Center;
		_rowsEmpty.TextAlignment = TextAlignment.Center;
		grid2.Children.Add(_rowsEmpty);
		Grid.SetRow(grid2, 2);
		grid.Children.Add(grid2);
		return grid;
	}

	private void SetBreakdown(Breakdown b)
	{
		_svc.Settings.Breakdown = b;
		_svc.Save();
		RenderRows();
	}

	private void ShowCached()
	{
		AnalyticsSite activeSite = _svc.ActiveSite;
		if (activeSite != null && _dashboard != null)
		{
			AnalyticsSnapshot snap = _svc.Cached(activeSite.Key, _svc.Settings.Range);
			_snap = snap;
			RenderSnapshot();
		}
	}

	private void RefreshIfStale(bool force)
	{
		AnalyticsSite site = _svc.ActiveSite;
		if (site != null && _mode == Mode.Dashboard && State == ToolState.Active && _root != null)
		{
			AnalyticsRange range = _svc.Settings.Range;
			if ((_snap == null || _snap.Range != range || _snap.SiteKey != site.Key || DateTime.UtcNow - _snap.FetchedUtc > StaleAfter || force) && _svc.Fetch(site, range, force, _root.Dispatcher, delegate(AnalyticsSnapshot snap, Exception err)
			{
				_loading = false;
				if (_svc.ActiveSite == site && _svc.Settings.Range == range)
				{
					if (snap != null)
					{
						_snap = snap;
						_lastError = null;
					}
					else
					{
						_lastError = err;
					}
					RenderSnapshot();
				}
			}))
			{
				_loading = true;
				RenderSnapshot();
			}
		}
	}

	private void RefreshOnline()
	{
		AnalyticsSite site = _svc.ActiveSite;
		if (site == null || _root == null)
		{
			return;
		}
		_svc.Realtime(site, _root.Dispatcher, delegate(int? n)
		{
			if (_svc.ActiveSite == site)
			{
				_onlineCount = n;
				UpdateHeader();
				PublishNotch(site, n);
			}
		});
	}

	private void UpdateAgo()
	{
		if (_updated == null)
		{
			return;
		}
		if (_snap == null)
		{
			_updated.Text = (_loading ? "" : "");
			_updated.ToolTip = null;
			return;
		}
		string text = ConnectorKit.Ago(_snap.FetchedUtc);
		AnalyticsException ex = _lastError as AnalyticsException;
		if (_lastError == null)
		{
			_updated.Text = (_loading ? "Refreshing…" : ("Updated " + text));
			_updated.Foreground = ThemeTokens.Text3;
			_updated.ToolTip = null;
		}
		else
		{
			_updated.Text = ((ex != null && ex.Kind == AnalyticsErrorKind.Offline) ? "Offline · " : "Couldn't refresh · ") + "showing " + text;
			_updated.Foreground = ThemeTokens.Text2;
			Explain(_lastError, (_svc.ActiveSite != null) ? _svc.ActiveSite.Provider : null, out var title, out var advice, out var _);
			_updated.ToolTip = BuddyTooltip.Create(title + ". " + advice);
		}
	}

	public static string Ago(TimeSpan t)
	{
		if (t.TotalMinutes < 1.0)
		{
			return "just now";
		}
		if (t.TotalMinutes < 60.0)
		{
			return (int)t.TotalMinutes + "m ago";
		}
		if (t.TotalHours < 24.0)
		{
			return (int)t.TotalHours + "h ago";
		}
		return (int)t.TotalDays + "d ago";
	}

	private static bool NeedsReconnect(Exception ex)
	{
		if (ex is AnalyticsException ex2)
		{
			if (ex2.Kind != AnalyticsErrorKind.WrongKey && ex2.Kind != AnalyticsErrorKind.NoAccess && ex2.Kind != AnalyticsErrorKind.WrongSite && ex2.Kind != AnalyticsErrorKind.NotConfigured)
			{
				return ex2.Kind == AnalyticsErrorKind.PlanNotSupported;
			}
			return true;
		}
		return false;
	}

	private void ShowDashNotice()
	{
		AnalyticsSite site = _svc.ActiveSite;
		if (_lastError == null || site == null || (_snap != null && !NeedsReconnect(_lastError)))
		{
			_dashNotice.Hide();
			return;
		}
		Explain(_lastError, site.Provider, out var title, out var advice, out var _);
		if (NeedsReconnect(_lastError))
		{
			AnalyticsException ex = (AnalyticsException)_lastError;
			if (ex.Kind == AnalyticsErrorKind.WrongKey)
			{
				title = "Your key stopped working";
				advice = "It may have been deleted or changed. Connect " + site.DisplayName + " again with a new key.";
			}
			_dashNotice.Show(ConnectorKit.NoticeKind.Error, title, advice, "Reconnect", delegate
			{
				Reconnect(site);
			});
		}
		else
		{
			_dashNotice.Show(a2(_lastError) ? ConnectorKit.NoticeKind.Offline : ConnectorKit.NoticeKind.Error, title, advice, "Try again", delegate
			{
				_lastError = null;
				RefreshIfStale(force: true);
			});
		}
	}

	private static bool a2(Exception ex)
	{
		if (ex is AnalyticsException ex2)
		{
			return ex2.Kind == AnalyticsErrorKind.Offline;
		}
		return false;
	}

	private void Reconnect(AnalyticsSite site)
	{
		if (!(site.Provider == "ga4") || AnalyticsFeatures.GoogleAnalytics)
		{
			try
			{
				_svc.Disconnect(site.Key);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Analytics reconnect cleanup failed: " + ex.GetType().Name);
			}
			_snap = null;
			_lastError = null;
			_formProvider = site.Provider;
			_mode = Mode.Form;
			Render();
			if (_fDomain != null)
			{
				_fDomain.Value = site.SiteId;
			}
			if (_fServer != null && !string.IsNullOrEmpty(site.ServerUrl))
			{
				_fServer.Value = site.ServerUrl;
			}
			if (_fKey != null)
			{
				_fKey.FocusInput();
			}
		}
	}

	private void RenderSnapshot()
	{
		if (_dashboard == null)
		{
			return;
		}
		AnalyticsSnapshot snap = _snap;
		AnalyticsRange range = _svc.Settings.Range;
		UpdateAgo();
		ShowDashNotice();
		_loadingRow.Visibility = ((snap != null || !_loading || _lastError != null) ? Visibility.Collapsed : Visibility.Visible);
		if (snap == null)
		{
			_hero.Text = ((_loading || _lastError != null) ? "—" : "");
			_hero.Foreground = ThemeTokens.Text3;
			_change.Visibility = Visibility.Collapsed;
			_chart.SetPoints(null, range);
			_axis.Children.Clear();
			BuildStats(null);
			_message.Visibility = Visibility.Collapsed;
			RenderRows();
			return;
		}
		_hero.Foreground = ThemeTokens.Text;
		_hero.Text = (snap.Visitors ?? 0).ToString("N0", CultureInfo.CurrentCulture);
		_hero.ToolTip = BuddyTooltip.Create(string.Format("Unique visitors · {0}{1}", (snap.RangeStart == snap.RangeEnd) ? snap.RangeStart : (snap.RangeStart + " – " + snap.RangeEnd), string.IsNullOrEmpty(snap.TimeZone) ? "" : (" · " + snap.TimeZone)));
		double? changePercent = snap.ChangePercent;
		_change.Visibility = ((!changePercent.HasValue) ? Visibility.Collapsed : Visibility.Visible);
		if (changePercent.HasValue)
		{
			int num = (int)Math.Round(Math.Abs(changePercent.Value));
			_changeArrow.Content = ((num == 0) ? null : FluentIcons.Create((changePercent.Value >= 0.0) ? "arrow_up" : "arrow_down", 15.0, BuddyTheme.Text92, filled: false));
			_changeArrow.Visibility = ((num == 0) ? Visibility.Collapsed : Visibility.Visible);
			_changeValue.Text = num + "%";
			_changeLabel.Text = range switch
			{
				AnalyticsRange.Week => "vs last week", 
				AnalyticsRange.Today => "vs yesterday", 
				_ => "vs last 30 days", 
			};
			_change.ToolTip = BuddyTooltip.Create(string.Format(CultureInfo.CurrentCulture, "{0:N0} visitors {1} {2}", snap.PreviousVisitors, (range == AnalyticsRange.Today) ? "yesterday until this time," : "in the previous period,", (snap.PreviousStart == snap.PreviousEnd) ? snap.PreviousStart : (snap.PreviousStart + " – " + snap.PreviousEnd)));
		}
		bool flag = (snap.Visitors ?? 0) == 0 && (snap.Series == null || snap.Series.All((SeriesPoint p) => p.Value == 0));
		_message.Text = (flag ? "No visitors in this period yet. If that's unexpected, check that the tracking script is on your site." : "");
		_message.Visibility = ((!flag || _dashNotice.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible);
		bool flag2 = _dashNotice.Visibility == Visibility.Visible;
		_chart.SetPoints((flag || flag2) ? null : snap.Series, range);
		BuildAxis((flag || flag2) ? null : snap.Series, range);
		BuildStats(snap);
		RenderRows();
	}

	private void BuildAxis(List<SeriesPoint> pts, AnalyticsRange range)
	{
		_axis.Children.Clear();
		if (pts != null && pts.Count != 0)
		{
			object obj;
			if (pts.Count != 1)
			{
				obj = ((pts.Count == 2) ? new int[2] { 0, 1 } : new int[3]
				{
					0,
					(pts.Count - 1) / 2,
					pts.Count - 1
				});
			}
			else
			{
				int[] array = new int[1];
				obj = array;
			}
			int[] array2 = (int[])obj;
			for (int i = 0; i < array2.Length; i++)
			{
				TextBlock textBlock = BuddyTheme.Text(AxisLabel(pts[array2[i]].Time, range), 11.5, BuddyTheme.Text50, FontWeights.Medium);
				textBlock.HorizontalAlignment = ((i != 0) ? ((i != array2.Length - 1) ? HorizontalAlignment.Center : HorizontalAlignment.Right) : HorizontalAlignment.Left);
				_axis.Children.Add(textBlock);
			}
		}
	}

	public static string AxisLabel(DateTime t, AnalyticsRange range)
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		switch (range)
		{
		case AnalyticsRange.Today:
			if (!currentCulture.DateTimeFormat.ShortTimePattern.Contains("t"))
			{
				return t.ToString("HH:00", currentCulture);
			}
			return t.ToString("h tt", currentCulture);
		case AnalyticsRange.Week:
			return t.ToString("ddd", currentCulture);
		default:
			if (!currentCulture.DateTimeFormat.MonthDayPattern.StartsWith("M"))
			{
				return t.ToString("d MMM", currentCulture);
			}
			return t.ToString("MMM d", currentCulture);
		}
	}

	private void BuildStats(AnalyticsSnapshot s)
	{
		_stats.Children.Clear();
		_stats.ColumnDefinitions.Clear();
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		if (s == null)
		{
			if (!_loading)
			{
				return;
			}
			string[] array = new string[3] { "Pageviews", "Bounce rate", "Avg. time" };
			foreach (string key in array)
			{
				list.Add(new KeyValuePair<string, string>(key, null));
			}
		}
		else
		{
			if (s.Pageviews.HasValue)
			{
				list.Add(new KeyValuePair<string, string>("Pageviews", Compact(s.Pageviews.Value)));
			}
			if (s.BounceRate.HasValue)
			{
				list.Add(new KeyValuePair<string, string>("Bounce rate", Math.Round(s.BounceRate.Value).ToString("0", CultureInfo.CurrentCulture) + "%"));
			}
			if (s.AvgDurationSeconds.HasValue)
			{
				list.Add(new KeyValuePair<string, string>("Avg. time", Duration(s.AvgDurationSeconds.Value)));
			}
			if (s.Revenue.HasValue)
			{
				list.Add(new KeyValuePair<string, string>("Revenue", Money(s.Revenue.Value, s.Currency)));
			}
		}
		for (int j = 0; j < list.Count; j++)
		{
			if (j > 0)
			{
				_stats.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(8.0)
				});
			}
			_stats.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			ConnectorKit.StatTile statTile = new ConnectorKit.StatTile(list[j].Key, 17.0);
			statTile.Padding = new Thickness(12.0, 8.0, 12.0, 8.0);
			ConnectorKit.StatTile statTile2 = statTile;
			if (list[j].Value == null)
			{
				statTile2.SetLoading();
			}
			else
			{
				statTile2.Set(list[j].Value);
			}
			Grid.SetColumn(statTile2, _stats.ColumnDefinitions.Count - 1);
			_stats.Children.Add(statTile2);
		}
	}

	internal void SetSnapshotForQa(AnalyticsSnapshot s, Exception err, bool loading)
	{
		_snap = s;
		_lastError = err;
		_loading = loading;
		RenderSnapshot();
	}

	private void RenderRows()
	{
		if (_rows == null)
		{
			return;
		}
		Breakdown breakdown = _svc.Settings.Breakdown;
		int num = breakdown switch
		{
			Breakdown.Sources => 1, 
			Breakdown.Pages => 0, 
			_ => 2, 
		};
		if (_breakdownSeg != null && _breakdownSeg.Selected != num)
		{
			_breakdownSeg.Selected = num;
		}
		_rows.Children.Clear();
		List<RankedRow> list = ((_snap != null) ? _snap.Rows(breakdown) : null);
		_rowsEmpty.Text = ((_snap == null) ? "" : "Nothing here for this period yet");
		_rowsEmpty.Visibility = ((list != null && list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		if (list != null)
		{
			long max = ((list.Count > 0) ? Math.Max(1L, list.Max((RankedRow r) => r.Visitors)) : 1);
			for (int i = 0; i < list.Count; i++)
			{
				_rows.Children.Add(BuildRow(list[i], max, i == 0, breakdown));
			}
		}
	}

	private FrameworkElement BuildRow(RankedRow r, long max, bool first, Breakdown b)
	{
		Grid grid = new Grid();
		grid.Height = 25.0;
		grid.Background = Brushes.Transparent;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(60.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(46.0)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		if (b == Breakdown.Sources)
		{
			string text = SourceIcon(r.Label);
			ContentControl contentControl = new ContentControl();
			contentControl.Width = 12.0;
			contentControl.Height = 12.0;
			contentControl.Margin = new Thickness(0.0, 0.0, 7.0, 0.0);
			contentControl.Focusable = false;
			contentControl.VerticalAlignment = VerticalAlignment.Center;
			ContentControl contentControl2 = contentControl;
			if (text != null)
			{
				contentControl2.Content = FluentIcons.Create(text, 12.0, BuddyTheme.Text50, filled: false);
			}
			stackPanel2.Children.Add(contentControl2);
		}
		TextBlock textBlock = BuddyTheme.Text(r.Label, 13.0, BuddyTheme.Text92, FontWeights.Normal);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.MaxWidth = 128.0 - (double)((b == Breakdown.Sources) ? 19 : 0);
		textBlock.ToolTip = BuddyTooltip.Create(r.Label);
		stackPanel2.Children.Add(textBlock);
		grid2.Children.Add(stackPanel2);
		Grid grid3 = new Grid();
		grid3.Height = 6.0;
		grid3.VerticalAlignment = VerticalAlignment.Center;
		Grid grid4 = grid3;
		grid4.Children.Add(new Border
		{
			CornerRadius = new CornerRadius(3.0),
			Background = Track
		});
		Border border = new Border();
		border.CornerRadius = new CornerRadius(3.0);
		border.Background = (first ? BuddyTheme.Text92 : BuddyTheme.Text50);
		border.HorizontalAlignment = HorizontalAlignment.Left;
		Border border2 = border;
		double num = (double)r.Visitors / (double)max;
		border2.Width = ((r.Visitors <= 0) ? 0.0 : Math.Max(4.0, 60.0 * num));
		grid4.Children.Add(border2);
		Grid.SetColumn(grid4, 1);
		grid2.Children.Add(grid4);
		TextBlock textBlock2 = BuddyTheme.Text(r.Visitors.ToString("N0", CultureInfo.CurrentCulture), 13.0, BuddyTheme.Text92, FontWeights.Medium);
		Typography.SetNumeralAlignment(textBlock2, FontNumeralAlignment.Tabular);
		textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(textBlock2, 2);
		grid2.Children.Add(textBlock2);
		return grid2;
	}

	public static string SourceIcon(string source)
	{
		string s = (source ?? "").ToLowerInvariant();
		Func<string[], bool> func = (string[] keys) => keys.Any((string k) => s.Contains(k));
		if (s.Contains("direct"))
		{
			return "link";
		}
		if (func(new string[8] { "chatgpt", "openai", "perplexity", "claude", "gemini", "copilot", "you.com", "phind" }))
		{
			return "bot";
		}
		if (func(new string[11]
		{
			"google", "bing", "duckduckgo", "yahoo", "ecosia", "brave", "baidu", "yandex", "startpage", "qwant",
			"kagi"
		}))
		{
			return "search";
		}
		if (func(new string[5] { "github", "gitlab", "stackoverflow", "stack overflow", "dev.to" }))
		{
			return "code";
		}
		if (func(new string[4] { "youtube", "vimeo", "twitch", "tiktok" }))
		{
			return "play";
		}
		if (func(new string[4] { "mail", "newsletter", "substack", "outlook" }))
		{
			return "mail";
		}
		if (func(new string[19]
		{
			"twitter", "x.com", "t.co", "facebook", "linkedin", "reddit", "instagram", "threads", "bluesky", "bsky",
			"mastodon", "hacker news", "news.ycombinator", "product hunt", "producthunt", "pinterest", "discord", "telegram", "whatsapp"
		}))
		{
			return "globe";
		}
		return null;
	}

	public static string Compact(long n)
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		if (n < 1000)
		{
			return n.ToString("N0", currentCulture);
		}
		if (n < 999950)
		{
			return Round((double)n / 1000.0) + "k";
		}
		return Round((double)n / 1000000.0) + "M";
	}

	private static string Round(double v)
	{
		if (!(v < 100.0))
		{
			return Math.Round(v, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.CurrentCulture);
		}
		return Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.CurrentCulture);
	}

	public static string Duration(double seconds)
	{
		int num = (int)Math.Round(seconds);
		if (num < 60)
		{
			return num + "s";
		}
		if (num < 3600)
		{
			return num / 60 + "m " + num % 60 + "s";
		}
		return num / 3600 + "h " + num % 3600 / 60 + "m";
	}

	public static string Money(double v, string currency)
	{
		string text = (currency ?? "").ToUpperInvariant() switch
		{
			"USD" => "$", 
			"EUR" => "€", 
			"GBP" => "£", 
			"INR" => "₹", 
			"JPY" => "¥", 
			"CNY" => "¥", 
			"AUD" => "A$", 
			"CAD" => "C$", 
			"" => "", 
			_ => currency.ToUpperInvariant() + " ", 
		};
		string text2 = ((v >= 100000.0) ? Compact((long)Math.Round(v)) : v.ToString((v >= 1000.0) ? "N0" : "N2", CultureInfo.CurrentCulture));
		return text + text2;
	}

	private void OnChartHover(int index, Point at)
	{
		List<SeriesPoint> list = ((_snap != null) ? _snap.Series : null);
		if (index < 0 || list == null || index >= list.Count)
		{
			_tip.Visibility = Visibility.Collapsed;
			return;
		}
		SeriesPoint seriesPoint = list[index];
		AnalyticsRange range = _svc.Settings.Range;
		_tipText.Text = TipLabel(seriesPoint.Time, range) + " · " + seriesPoint.Value.ToString("N0", CultureInfo.CurrentCulture);
		_tip.Visibility = Visibility.Visible;
		_tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		double width = _tip.DesiredSize.Width;
		double height = _tip.DesiredSize.Height;
		double length = Math.Max(0.0, Math.Min(_chart.ActualWidth - width, at.X - width / 2.0));
		double length2 = Math.Max(-6.0, at.Y - height - 10.0);
		Canvas.SetLeft(_tip, length);
		Canvas.SetTop(_tip, length2);
	}

	public static string TipLabel(DateTime t, AnalyticsRange range)
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		if (range == AnalyticsRange.Week)
		{
			return t.ToString("ddd", currentCulture);
		}
		return AxisLabel(t, range);
	}

	internal static List<ConnectorKit.ProviderOption> ProviderOptions()
	{
		List<ConnectorKit.ProviderOption> list = new List<ConnectorKit.ProviderOption>();
		list.Add(new ConnectorKit.ProviderOption
		{
			Id = "plausible",
			Name = "Plausible",
			Letters = "P",
			Brand = PlausibleBrand,
			WhatYouSee = "Visitors, top pages, sources and live users",
			Sample = () => SampleBody("example.com", "2,847", new double[7] { 18.0, 22.0, 19.0, 27.0, 31.0, 26.0, 35.0 }, "/", "1,204", "/pricing", "512", "12")
		});
		list.Add(new ConnectorKit.ProviderOption
		{
			Id = "datafast",
			Name = "DataFast",
			Letters = "DF",
			Brand = DataFastBrand,
			WhatYouSee = "Visitors, revenue, top pages and live users",
			Sample = () => SampleBody("example.com", "1,392", new double[7] { 9.0, 12.0, 11.0, 15.0, 13.0, 18.0, 21.0 }, "/", "688", "/blog", "241", "7")
		});
		List<ConnectorKit.ProviderOption> list2 = list;
		if (AnalyticsFeatures.GoogleAnalytics)
		{
			list2.Add(new ConnectorKit.ProviderOption
			{
				Id = "ga4",
				Name = "Google Analytics",
				Letters = "G",
				Brand = GoogleBrand,
				WhatYouSee = "Active users, pages, sources and live users",
				Sample = () => SampleBody("GA4 property", "3,105", new double[7] { 20.0, 24.0, 22.0, 30.0, 28.0, 33.0, 36.0 }, "/", "1,410", "/docs", "603", "18")
			});
		}
		return list2;
	}

	private static FrameworkElement SampleBody(string site, string visitors, double[] line, string p1, string v1, string p2, string v2, string live)
	{
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid();
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(ConnectorKit.Text(site + " · last 7 days", 11.5, ThemeTokens.Text2, AynType.Regular));
		TextBlock textBlock = ConnectorKit.Text(visitors, 24.0, ThemeTokens.Text, AynType.Semibold);
		textBlock.FontFamily = BuddyTheme.DisplayFont;
		textBlock.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		grid.Children.Add(stackPanel2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel3.VerticalAlignment = VerticalAlignment.Top;
		StackPanel stackPanel4 = stackPanel3;
		stackPanel4.Children.Add(new Ellipse
		{
			Width = 6.0,
			Height = 6.0,
			Fill = OnlineDot,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		});
		stackPanel4.Children.Add(ConnectorKit.Text(live + " online", 11.5, ThemeTokens.Text2, AynType.Medium));
		grid.Children.Add(stackPanel4);
		stackPanel.Children.Add(grid);
		FrameworkElement frameworkElement = ConnectorKit.SampleLine(line, 286.0, 40.0);
		frameworkElement.Margin = new Thickness(0.0, 8.0, 0.0, 8.0);
		stackPanel.Children.Add(frameworkElement);
		stackPanel.Children.Add(ConnectorKit.SampleRow(p1, v1));
		stackPanel.Children.Add(ConnectorKit.SampleRow(p2, v2));
		return stackPanel;
	}

	private FrameworkElement BuildPicker(bool animate = true)
	{
		Action cancel = null;
		if (_svc.VisibleSites.Count > 0)
		{
			cancel = delegate
			{
				_mode = Mode.Dashboard;
				Render();
				StartTimers();
			};
		}
		FrameworkElement frameworkElement = ConnectorKit.Picker("Connect your site", "See your website's visitors right in the notch. Read-only — your key is encrypted on this PC and only sent to the provider you pick.", ProviderOptions(), delegate(string id)
		{
			_formProvider = id;
			_mode = Mode.Form;
			Render();
		}, cancel);
		if (animate)
		{
			ConnectorKit.Reveal(frameworkElement, 6.0);
		}
		return frameworkElement;
	}

	private FrameworkElement BuildForm(bool animate = true)
	{
		ConnectorKit.ProviderOption providerOption = ProviderOptions().FirstOrDefault((ConnectorKit.ProviderOption o) => o.Id == _formProvider);
		if (providerOption == null)
		{
			_mode = Mode.Picker;
			return BuildPicker(animate);
		}
		_fDomain = (_fKey = (_fServer = (_fProperty = (_fClientId = (_fClientSecret = null)))));
		_fNotice = new ConnectorKit.Notice
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		_fConnect = new ConnectorKit.BusyButton("Connect");
		_fConnect.Click += Connect;
		ConnectorKit.AutomationName(_fConnect, "Connect " + providerOption.Name);
		FrameworkElement leftExtra = null;
		StackPanel stackPanel = new StackPanel();
		FrameworkElement steps;
		if (_formProvider == "plausible")
		{
			steps = ConnectorKit.Steps(new ConnectorKit.Step("Open your Plausible account settings", "Open Plausible", "https://plausible.io/settings/api-keys"), new ConnectorKit.Step("Choose API Keys → New API Key → Stats API, then copy it"), new ConnectorKit.Step("Paste it here with your site's domain"));
			_fDomain = new ConnectorKit.SoftField("Site domain", "example.com", "Exactly as it appears in Plausible");
			_fKey = new ConnectorKit.SoftField("Stats API key", "Paste your Stats API key", null, secret: true, paste: true);
			_fServer = new ConnectorKit.SoftField("Server address", "https://plausible.example.com", "Only for self-hosted Plausible");
			leftExtra = ConnectorKit.Disclosure("Self-hosted Plausible", _fServer);
		}
		else if (_formProvider == "datafast")
		{
			steps = ConnectorKit.Steps(new ConnectorKit.Step("Open DataFast and go to your website's settings", "Open DataFast", "https://datafa.st/dashboard"), new ConnectorKit.Step("Under API, create a website API key (it starts with df_)"), new ConnectorKit.Step("Paste it here with your site's domain"));
			_fDomain = new ConnectorKit.SoftField("Site domain", "example.com", "The website this key belongs to");
			_fKey = new ConnectorKit.SoftField("Website API key", "df_…", null, secret: true, paste: true);
		}
		else
		{
			steps = BuildGoogleSteps(stackPanel);
		}
		if (_fDomain != null)
		{
			stackPanel.Children.Add(_fDomain);
			_fKey.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
			stackPanel.Children.Add(_fKey);
		}
		ConnectorKit.SoftField[] array = new ConnectorKit.SoftField[6] { _fDomain, _fKey, _fServer, _fProperty, _fClientId, _fClientSecret };
		foreach (ConnectorKit.SoftField softField in array)
		{
			if (softField != null)
			{
				softField.Submitted += Connect;
				softField.Changed += delegate
				{
					_fNotice.Hide();
				};
			}
		}
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		Grid grid2 = grid;
		_fConnect.HorizontalAlignment = HorizontalAlignment.Right;
		grid2.Children.Add(_fConnect);
		stackPanel.Children.Add(grid2);
		Grid grid3 = ConnectorKit.ConnectLayout(providerOption, "Read-only. Your key is encrypted on this PC and only sent to " + providerOption.Name + ".", steps, leftExtra, stackPanel, _fNotice, delegate
		{
			CancelSignIn();
			_connectGeneration++;
			_mode = Mode.Picker;
			Render();
		});
		if (animate)
		{
			ConnectorKit.Reveal(grid3, 6.0);
		}
		if (_fDomain != null)
		{
			_fDomain.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
			{
				if (_fDomain != null && _mode == Mode.Form)
				{
					_fDomain.FocusInput();
				}
			});
		}
		return grid3;
	}

	private FrameworkElement BuildGoogleSteps(StackPanel right)
	{
		BuddySlidingSegmented buddySlidingSegmented = new BuddySlidingSegmented(new string[2] { "Google sign-in", "Service account" }, (!(_gaMode == "oauth")) ? 1 : 0);
		buddySlidingSegmented.SelectionChanged += delegate(int i)
		{
			CancelSignIn();
			_gaMode = ((i == 0) ? "oauth" : "service_account");
			Render();
		};
		buddySlidingSegmented.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		right.Children.Add(buddySlidingSegmented);
		_fProperty = new ConnectorKit.SoftField("GA4 property ID", "123456789", "Admin → Property details");
		if (_gaMode == "oauth")
		{
			if (_gaRefresh == null)
			{
				_fClientId = new ConnectorKit.SoftField("OAuth client ID (Desktop app)", "….apps.googleusercontent.com");
				_fClientId.Value = _svc.Settings.GoogleClientId ?? "";
				_fClientSecret = new ConnectorKit.SoftField("Client secret", "Paste the client secret", null, secret: true, paste: true);
				right.Children.Add(_fClientId);
				_fClientSecret.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
				right.Children.Add(_fClientSecret);
				Border border = ConnectorKit.SoftButton("Sign in with Google", SignIn);
				border.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
				border.HorizontalAlignment = HorizontalAlignment.Left;
				right.Children.Add(border);
			}
			else
			{
				if (_gaProperties != null && _gaProperties.Count > 0 && string.IsNullOrEmpty(_fProperty.Value))
				{
					_fProperty.Value = _gaProperties[0].Id;
					_gaPropertyName = _gaProperties[0].Name;
				}
				right.Children.Add(_fProperty);
				if (_gaProperties != null && _gaProperties.Count > 1)
				{
					right.Children.Add(ConnectorKit.Link("Choose another property", ShowPropertyMenu));
				}
			}
			return ConnectorKit.Steps(new ConnectorKit.Step("Create a Desktop OAuth client in Google Cloud", "Open Google Cloud", "https://console.cloud.google.com/apis/credentials"), new ConnectorKit.Step("Paste its client ID and secret, then sign in"), new ConnectorKit.Step("Pick the GA4 property to show"));
		}
		Border border2 = ConnectorKit.SoftButton("Choose key file…", ChooseServiceAccount);
		border2.HorizontalAlignment = HorizontalAlignment.Left;
		right.Children.Add(border2);
		_fSaFile = ConnectorKit.Text((_gaJson != null) ? SaEmail(_gaJson) : "No file chosen", 11.5, ThemeTokens.Text2, AynType.Regular);
		_fSaFile.Margin = new Thickness(2.0, 6.0, 0.0, 10.0);
		right.Children.Add(_fSaFile);
		right.Children.Add(_fProperty);
		return ConnectorKit.Steps(new ConnectorKit.Step("Create a service account key (JSON) in Google Cloud", "Open Google Cloud", "https://console.cloud.google.com/iam-admin/serviceaccounts"), new ConnectorKit.Step("Add its email as a Viewer in GA4 property access"), new ConnectorKit.Step("Choose the key file and enter the property ID"));
	}

	private static string SaEmail(string json)
	{
		try
		{
			return ServiceAccount.Parse(json).ClientEmail;
		}
		catch
		{
			return "Invalid key file";
		}
	}

	private void ShowPropertyMenu()
	{
		if (_gaProperties == null || _fProperty == null)
		{
			return;
		}
		List<BuddyMenuItem> items = (from p in _gaProperties.Take(12)
			select new BuddyMenuItem(p.Name + "  ·  " + p.Id, null, delegate
			{
				_fProperty.Value = p.Id;
				_gaPropertyName = p.Name;
			})
			{
				IsChecked = (_fProperty.Value == p.Id)
			}).ToList();
		BuddyDropdown.Show(_fProperty, items, null, 260.0);
	}

	private void ChooseServiceAccount()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "Choose the service-account key";
		openFileDialog.Filter = "Key file (*.json)|*.json";
		OpenFileDialog openFileDialog2 = openFileDialog;
		Window window = Window.GetWindow(_root);
		if (((window != null) ? openFileDialog2.ShowDialog(window) : openFileDialog2.ShowDialog()) != true)
		{
			return;
		}
		try
		{
			string text = File.ReadAllText(openFileDialog2.FileName);
			ServiceAccount serviceAccount = ServiceAccount.Parse(text);
			_gaJson = text;
			_fSaFile.Text = serviceAccount.ClientEmail;
		}
		catch (Exception ex)
		{
			ShowConnectError(ex);
		}
	}

	private void SignIn()
	{
		if (_fClientId != null)
		{
			_svc.Settings.GoogleClientId = _fClientId.Value;
			_svc.Save();
			_svc.SetGoogleClientSecret(_fClientSecret.Value);
		}
		CancelSignIn();
		_signInCancel = new CancellationTokenSource();
		CancellationToken token = _signInCancel.Token;
		_fNotice.Show(ConnectorKit.NoticeKind.Info, "Waiting for Google", "Finish signing in in your browser, then come back here.");
		Dispatcher ui = _root.Dispatcher;
		Task.Factory.StartNew(delegate
		{
			string refresh = null;
			List<GaProperty> props = null;
			Exception err = null;
			Exception listErr = null;
			try
			{
				refresh = GoogleAuth.SignIn(token);
				try
				{
					props = GoogleAuth.ListProperties(refresh);
				}
				catch (Exception ex)
				{
					listErr = ex;
				}
			}
			catch (Exception ex2)
			{
				err = ex2;
			}
			ui.BeginInvoke((Action)delegate
			{
				if (!token.IsCancellationRequested && _mode == Mode.Form)
				{
					if (err != null)
					{
						ShowConnectError(err);
					}
					else
					{
						_gaRefresh = refresh;
						_gaProperties = props;
						Render();
						if (listErr != null)
						{
							ShowConnectError(listErr);
						}
					}
				}
			});
		});
	}

	private void CancelSignIn()
	{
		if (_signInCancel != null)
		{
			_signInCancel.Cancel();
			_signInCancel = null;
		}
	}

	public static string CheckPlausibleKey(string key)
	{
		key = (key ?? "").Trim();
		if (key.Length == 0)
		{
			return "Paste your Stats API key";
		}
		if (key.StartsWith("df_", StringComparison.Ordinal) || key.StartsWith("dft_", StringComparison.Ordinal))
		{
			return "That's a DataFast key. Paste the Plausible Stats API key.";
		}
		if (key.StartsWith("rk_", StringComparison.Ordinal) || key.StartsWith("sk_", StringComparison.Ordinal) || key.StartsWith("pk_", StringComparison.Ordinal))
		{
			return "That's a Stripe key. Paste the Plausible Stats API key.";
		}
		if (key.Contains("://") || key.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
		{
			return "That looks like a web address. Paste the API key from Plausible instead.";
		}
		if (key.IndexOfAny(new char[4] { ' ', '\t', '\r', '\n' }) >= 0)
		{
			return "Paste only the key — it has no spaces.";
		}
		if (key.Length < 20)
		{
			return "That's too short for a Plausible API key. Copy the whole key.";
		}
		return null;
	}

	public static string NormalizeDomain(string text, out string problem)
	{
		problem = null;
		string text2 = (text ?? "").Trim().ToLowerInvariant();
		if (text2.StartsWith("https://"))
		{
			text2 = text2.Substring(8);
		}
		else if (text2.StartsWith("http://"))
		{
			text2 = text2.Substring(7);
		}
		text2 = text2.TrimEnd('/');
		if (text2.Length == 0)
		{
			problem = "Enter your site's domain, like example.com";
			return null;
		}
		if (text2.IndexOfAny(new char[5] { ' ', '/', '?', '#', '@' }) >= 0)
		{
			problem = "Enter just the domain, like example.com (no page path)";
			return null;
		}
		if (text2.IndexOf('.') < 0)
		{
			problem = "Enter the full domain, like example.com";
			return null;
		}
		return text2;
	}

	private AnalyticsSite DraftSite(out string secret)
	{
		secret = null;
		AnalyticsSite analyticsSite = new AnalyticsSite();
		analyticsSite.Provider = _formProvider;
		AnalyticsSite analyticsSite2 = analyticsSite;
		if (_formProvider == "ga4")
		{
			analyticsSite2.GoogleAuth = _gaMode;
			analyticsSite2.SiteId = GoogleProvider.PropertyId((_fProperty != null) ? _fProperty.Value : "");
			if (_gaMode == "oauth")
			{
				secret = _gaRefresh;
			}
			else
			{
				secret = _gaJson;
				if (secret != null)
				{
					analyticsSite2.ServiceAccountEmail = SaEmail(secret);
				}
			}
			if (secret == null)
			{
				_fNotice.Show(ConnectorKit.NoticeKind.Error, (_gaMode == "oauth") ? "Sign in with Google first" : "Choose the key file first", null);
				return null;
			}
			if (analyticsSite2.SiteId.Length == 0 || !analyticsSite2.SiteId.All(char.IsDigit))
			{
				if (_fProperty != null)
				{
					_fProperty.SetError("The property ID is a number (Admin → Property details)");
				}
				return null;
			}
			analyticsSite2.DisplayName = _gaPropertyName ?? ("GA4 " + analyticsSite2.SiteId);
			return analyticsSite2;
		}
		string problem;
		string text = NormalizeDomain(_fDomain.Value, out problem);
		string value = _fKey.Value;
		string text2 = ((_formProvider == "datafast") ? DataFastProvider.CheckKey(value) : CheckPlausibleKey(value));
		if (_formProvider == "datafast" && value.Length == 0)
		{
			text2 = "Paste your website API key";
		}
		if (_formProvider == "datafast" && value.StartsWith("rk_", StringComparison.Ordinal))
		{
			text2 = "That's a Stripe key. Paste the DataFast website key (it starts with df_).";
		}
		if (problem != null)
		{
			_fDomain.SetError(problem);
		}
		if (text2 != null)
		{
			_fKey.SetError(text2);
		}
		if (problem != null || text2 != null)
		{
			((problem != null) ? _fDomain : _fKey).FocusInput();
			return null;
		}
		analyticsSite2.SiteId = text;
		analyticsSite2.DisplayName = text;
		secret = value;
		if (_fServer != null && !string.IsNullOrWhiteSpace(_fServer.Value))
		{
			string text3 = _fServer.Value.Trim().TrimEnd('/');
			if (!Uri.TryCreate(text3.Contains("://") ? text3 : ("https://" + text3), UriKind.Absolute, out var result) || (result.Scheme != "https" && result.Scheme != "http"))
			{
				_fServer.SetError("Enter the address of your Plausible server, like https://plausible.example.com");
				return null;
			}
			if (!string.Equals(text3, "https://plausible.io", StringComparison.OrdinalIgnoreCase))
			{
				analyticsSite2.ServerUrl = text3;
			}
		}
		return analyticsSite2;
	}

	private void Connect()
	{
		if (_fConnect == null || _fConnect.IsBusy)
		{
			return;
		}
		_fNotice.Hide();
		ConnectorKit.SoftField[] array = new ConnectorKit.SoftField[6] { _fDomain, _fKey, _fServer, _fProperty, _fClientId, _fClientSecret };
		foreach (ConnectorKit.SoftField softField in array)
		{
			if (softField != null && softField.HasError)
			{
				softField.SetError(null);
			}
		}
		string secret;
		AnalyticsSite site = DraftSite(out secret);
		if (site == null)
		{
			return;
		}
		IAnalyticsProvider provider = AnalyticsProviders.For(site.Provider);
		_fConnect.SetBusy(busy: true, "Connecting…");
		int gen = ++_connectGeneration;
		Dispatcher ui = _root.Dispatcher;
		Task.Factory.StartNew(delegate
		{
			TestResult res = null;
			Exception err = null;
			try
			{
				res = provider.Test(site, secret);
			}
			catch (Exception ex)
			{
				err = ex;
			}
			finally
			{
				GoogleAuth.Forget(site);
			}
			ui.BeginInvoke((Action)delegate
			{
				FinishConnect(gen, site, secret, res, err);
			});
		});
	}

	private void FinishConnect(int gen, AnalyticsSite site, string secret, TestResult res, Exception err)
	{
		if (gen != _connectGeneration || _mode != Mode.Form || _fConnect == null)
		{
			return;
		}
		_fConnect.SetBusy(busy: false);
		if (err != null)
		{
			ShowConnectError(err);
			return;
		}
		site.TimeZone = res.TimeZone ?? site.TimeZone;
		try
		{
			_svc.AddSite(site, secret);
		}
		catch (IOException)
		{
			_fNotice.Show(ConnectorKit.NoticeKind.Error, "Couldn't save the key on this PC", "Please try again. If it keeps failing, restart Ayn.");
			return;
		}
		_gaRefresh = null;
		_gaJson = null;
		_gaProperties = null;
		_gaPropertyName = null;
		_onlineCount = res.Online;
		_snap = null;
		_lastError = null;
		_mode = Mode.Dashboard;
		Render();
		ConnectorKit.Reveal(_dashboard, 12.0);
		StartTimers();
	}

	public static void Explain(Exception ex, string providerId, out string title, out string advice, out string actionText)
	{
		IAnalyticsProvider analyticsProvider = AnalyticsProviders.For(providerId);
		string text = ((analyticsProvider != null) ? analyticsProvider.Name : "The provider");
		AnalyticsException ex2 = ex as AnalyticsException;
		actionText = null;
		switch (ex2?.Kind ?? AnalyticsErrorKind.None)
		{
		case AnalyticsErrorKind.WrongKey:
			title = "That key didn't work";
			advice = ((providerId == "datafast") ? "Copy the whole website key (df_…) again. If it was deleted, create a new one in DataFast." : ((providerId == "ga4") ? ex2.Message : "Copy the whole Stats API key again. Plausible shows a key only once — if you lost it, create a new one."));
			actionText = "Open " + text;
			break;
		case AnalyticsErrorKind.WrongSite:
			title = "Site not found";
			advice = (ex2.Message.StartsWith("This key belongs to", StringComparison.Ordinal) ? (ex2.Message + ". Check the domain.") : ("Use the domain exactly as it appears in your " + text + " dashboard, like example.com."));
			break;
		case AnalyticsErrorKind.NoAccess:
			title = "This key can't see that site";
			advice = ((providerId == "plausible") ? "Create the key while the team that owns the site is selected in Plausible." : "Use the key that belongs to this website.");
			actionText = "Open " + text;
			break;
		case AnalyticsErrorKind.Offline:
			title = "You're offline";
			advice = "Check your internet connection and try again.";
			actionText = "Try again";
			break;
		case AnalyticsErrorKind.RateLimited:
			title = "Too many requests";
			advice = text + " limits how often a key can be used. Wait a minute and try again.";
			break;
		case AnalyticsErrorKind.PlanNotSupported:
			title = "Your plan doesn't include the Stats API";
			advice = "API access needs a Plausible plan that includes it. Check your subscription in Plausible.";
			actionText = "Open " + text;
			break;
		case AnalyticsErrorKind.Server:
			title = text + " is having trouble";
			advice = "It's not your key. Try again in a few minutes.";
			actionText = "Try again";
			break;
		case AnalyticsErrorKind.NotConfigured:
			title = ex2.Message;
			advice = "Connect the site again with its key.";
			break;
		default:
			title = "Couldn't connect";
			advice = ((ex2 != null) ? ex2.Message : "Something unexpected happened. Try again.");
			actionText = "Try again";
			break;
		}
	}

	private void ShowConnectError(Exception ex)
	{
		if (_fNotice == null)
		{
			return;
		}
		Explain(ex, _formProvider, out var title, out var advice, out var actionText);
		Action action = null;
		if (actionText == "Try again")
		{
			action = Connect;
		}
		else if (actionText != null && actionText.StartsWith("Open ", StringComparison.Ordinal))
		{
			action = delegate
			{
				ConnectorKit.Browser((_formProvider == "datafast") ? "https://datafa.st/dashboard" : ((_formProvider == "ga4") ? "https://console.cloud.google.com/apis/credentials" : "https://plausible.io/settings/api-keys"));
			};
		}
		_fNotice.Show(ConnectorKit.NoticeKind.Error, title, advice, (action != null) ? actionText : null, action);
		AnalyticsException ex2 = ex as AnalyticsException;
		if (ex2 != null && ex2.Kind == AnalyticsErrorKind.WrongKey && _fKey != null)
		{
			_fKey.SetError("Check this key");
		}
		if (ex2 != null && ex2.Kind == AnalyticsErrorKind.WrongSite && _fDomain != null)
		{
			_fDomain.SetError("Check this domain");
		}
	}

	internal void OpenForm(string provider)
	{
		_formProvider = provider;
		_mode = Mode.Form;
		Render();
	}

	internal void OpenPicker()
	{
		_mode = Mode.Picker;
		Render();
	}

	internal void OpenManage()
	{
		_mode = Mode.Manage;
		StopTimers();
		Render();
	}

	private FrameworkElement BuildManage()
	{
		Grid grid = new Grid();
		grid.MaxWidth = 560.0;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		Grid grid2 = grid;
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
		grid3.Margin = new Thickness(0.0, 2.0, 0.0, 10.0);
		Grid grid4 = grid3;
		grid4.Children.Add(ConnectorKit.Text("Your sites", 13.5, ThemeTokens.Text, AynType.Semibold));
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		StackPanel stackPanel2 = stackPanel;
		Border border = ConnectorKit.SoftButton("Add site", delegate
		{
			_mode = Mode.Picker;
			Render();
		}, 28.0, "add");
		border.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		stackPanel2.Children.Add(border);
		ConnectorKit.BusyButton busyButton = new ConnectorKit.BusyButton("Done", 28.0);
		busyButton.Click += delegate
		{
			if (_svc.VisibleSites.Count == 0)
			{
				_mode = Mode.Picker;
				Render();
			}
			else
			{
				_mode = Mode.Dashboard;
				Render();
				StartTimers();
			}
		};
		stackPanel2.Children.Add(busyButton);
		grid4.Children.Add(stackPanel2);
		grid2.Children.Add(grid4);
		StackPanel stackPanel3 = new StackPanel();
		foreach (AnalyticsSite visibleSite in _svc.VisibleSites)
		{
			stackPanel3.Children.Add(ManageRow(visibleSite));
		}
		if (_svc.VisibleSites.Count == 0)
		{
			stackPanel3.Children.Add(ConnectorKit.Text("No sites connected yet", 13.0, ThemeTokens.Text2, AynType.Regular));
		}
		BuddyScrollViewer buddyScrollViewer = new BuddyScrollViewer();
		buddyScrollViewer.Content = stackPanel3;
		BuddyScrollViewer element = buddyScrollViewer;
		Grid.SetRow(element, 1);
		grid2.Children.Add(element);
		Grid grid5 = new Grid();
		grid5.Margin = new Thickness(0.0, 10.0, 0.0, 2.0);
		Grid grid6 = grid5;
		grid6.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid6.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel4 = new StackPanel();
		stackPanel4.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel5 = stackPanel4;
		stackPanel5.Children.Add(ConnectorKit.Text("Show live visitors on the notch", 13.0, ThemeTokens.Text, AynType.Medium));
		TextBlock textBlock = ConnectorKit.Text("Checks the active site every minute, even with this tab closed", 11.5, ThemeTokens.Text2, AynType.Regular);
		textBlock.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel5.Children.Add(textBlock);
		grid6.Children.Add(stackPanel5);
		BuddyToggle buddyToggle = new BuddyToggle();
		buddyToggle.IsChecked = _svc.Settings.ShowLiveOnNotch;
		buddyToggle.VerticalAlignment = VerticalAlignment.Center;
		BuddyToggle buddyToggle2 = buddyToggle;
		buddyToggle2.Toggled += delegate(object s, bool on)
		{
			_svc.Settings.ShowLiveOnNotch = on;
			_svc.Save();
			UpdateNotchTimer();
		};
		Grid.SetColumn(buddyToggle2, 1);
		grid6.Children.Add(buddyToggle2);
		Border element2 = ConnectorKit.Card(grid6, new Thickness(14.0, 8.0, 12.0, 8.0));
		Grid.SetRow(element2, 2);
		grid2.Children.Add(element2);
		return grid2;
	}

	private FrameworkElement ManageRow(AnalyticsSite site)
	{
		Grid grid = new Grid();
		grid.MinHeight = 52.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(42.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		ConnectorKit.ProviderOption providerOption = ProviderOptions().FirstOrDefault((ConnectorKit.ProviderOption o) => o.Id == site.Provider);
		Border border = ConnectorKit.Monogram((providerOption != null) ? providerOption.Letters : "?", providerOption?.Brand ?? Colors.Gray, 30.0);
		border.HorizontalAlignment = HorizontalAlignment.Left;
		grid2.Children.Add(border);
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(ConnectorKit.Text(site.DisplayName ?? site.SiteId, 13.0, ThemeTokens.Text, AynType.Medium));
		IAnalyticsProvider analyticsProvider = AnalyticsProviders.For(site.Provider);
		string text = ((site.Provider == "ga4") ? (((site.GoogleAuth == "service_account") ? "service account" : "Google sign-in") + " · property " + site.SiteId) : (string.IsNullOrEmpty(site.ServerUrl) ? "" : site.ServerUrl));
		TextBlock textBlock = ConnectorKit.Text(((analyticsProvider != null) ? analyticsProvider.Name : site.Provider) + ((text.Length > 0) ? (" · " + text) : "") + (string.IsNullOrEmpty(site.TimeZone) ? "" : (" · " + site.TimeZone)), 11.5, ThemeTokens.Text2, AynType.Regular);
		textBlock.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		Grid.SetColumn(stackPanel2, 1);
		grid2.Children.Add(stackPanel2);
		ConnectorKit.ConfirmAction element = new ConnectorKit.ConfirmAction("Disconnect", "Remove key?", "Disconnect", delegate
		{
			try
			{
				_svc.Disconnect(site.Key);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Analytics disconnect failed: " + ex.GetType().Name);
				return;
			}
			if (_svc.VisibleSites.Count == 0)
			{
				_snap = null;
				ClearNotch();
			}
			_onlineCount = null;
			Render();
		});
		Grid.SetColumn(element, 2);
		grid2.Children.Add(element);
		Border border2 = new Border();
		border2.Child = grid2;
		border2.Padding = new Thickness(10.0, 4.0, 10.0, 4.0);
		border2.Margin = new Thickness(-10.0, 0.0, -10.0, 4.0);
		border2.CornerRadius = new CornerRadius(14.0);
		border2.Background = Brushes.Transparent;
		return border2;
	}

	private void UpdateNotchTimer()
	{
		if (_svc.Settings.ShowLiveOnNotch && _svc.ActiveSite != null)
		{
			if (_notchTimer == null)
			{
				_notchTimer = new DispatcherTimer(DispatcherPriority.Background)
				{
					Interval = TimeSpan.FromSeconds(60.0)
				};
				_notchTimer.Tick += delegate
				{
					if (State != ToolState.Active && _svc.Settings.ShowLiveOnNotch)
					{
						AnalyticsSite site = _svc.ActiveSite;
						Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;
						_svc.Realtime(site, currentDispatcher, delegate(int? n)
						{
							PublishNotch(site, n);
						});
					}
				};
			}
			_notchTimer.Start();
			AnalyticsSite now = _svc.ActiveSite;
			_svc.Realtime(now, Dispatcher.CurrentDispatcher, delegate(int? n)
			{
				PublishNotch(now, n);
			});
		}
		else
		{
			if (_notchTimer != null)
			{
				_notchTimer.Stop();
			}
			ClearNotch();
		}
	}

	private void PublishNotch(AnalyticsSite site, int? n)
	{
		if (!_svc.Settings.ShowLiveOnNotch || site == null)
		{
			return;
		}
		if (!n.HasValue)
		{
			ClearNotch();
			return;
		}
		try
		{
			DynamicActivityService.Instance.PublishActivity(new NotchActivity
			{
				Id = "analytics_live",
				Type = ActivityType.Custom,
				Priority = ActivityPriority.P4_Ambient,
				Mode = ActivityMode.Persistent,
				Title = n.Value.ToString("N0", CultureInfo.CurrentCulture) + " online",
				Subtitle = (site.DisplayName ?? site.SiteId),
				NavigateToToolId = Id,
				IconFactory = (double size, Brush brush) => new Ellipse
				{
					Width = Math.Max(6.0, size * 0.45),
					Height = Math.Max(6.0, size * 0.45),
					Fill = OnlineDot
				}
			});
			_notchPublished = true;
		}
		catch
		{
		}
	}

	private void ClearNotch()
	{
		if (!_notchPublished)
		{
			return;
		}
		_notchPublished = false;
		try
		{
			DynamicActivityService.Instance.ClearPersistentActivity(ActivityType.Custom, 0L);
		}
		catch
		{
		}
	}
}
