using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.ScreenTime;

public class ScreenTimeTool : IBuddyTool, IEscapeAware
{
	private sealed class LimitRing : FrameworkElement
	{
		private static readonly Pen TrackPen = Frozen(new Pen(BuddyTheme.Track, 2.5));

		private static readonly Pen ArcPen = Frozen(new Pen(BuddyTheme.Text100, 2.5)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		});

		private double _f = -1.0;

		public LimitRing()
		{
			base.IsHitTestVisible = false;
		}

		public void Set(double f)
		{
			f = Math.Max(0.0, Math.Min(1.0, f));
			if (!(Math.Abs(f - _f) < 0.002))
			{
				_f = f;
				InvalidateVisual();
			}
		}

		protected override void OnRender(DrawingContext dc)
		{
			double num = Math.Min(base.ActualWidth, base.ActualHeight);
			double num2 = num / 2.0;
			double num3 = (num - 2.5) / 2.0;
			Point center = new Point(num2, num2);
			dc.DrawEllipse(null, TrackPen, center, num3, num3);
			if (_f >= 0.999)
			{
				dc.DrawEllipse(null, ArcPen, center, num3, num3);
			}
			else if (!(_f <= 0.002))
			{
				double num4 = _f * 2.0 * Math.PI;
				PathFigure pathFigure = new PathFigure();
				pathFigure.StartPoint = new Point(num2, num2 - num3);
				pathFigure.IsClosed = false;
				PathFigure pathFigure2 = pathFigure;
				pathFigure2.Segments.Add(new ArcSegment(new Point(num2 + num3 * Math.Sin(num4), num2 - num3 * Math.Cos(num4)), new Size(num3, num3), 0.0, _f > 0.5, SweepDirection.Clockwise, isStroked: true));
				PathGeometry pathGeometry = new PathGeometry();
				pathGeometry.Figures.Add(pathFigure2);
				pathGeometry.Freeze();
				dc.DrawGeometry(null, ArcPen, pathGeometry);
			}
		}

		private static Pen Frozen(Pen p)
		{
			return ThemeTokens.FreezeSafe(p);
		}
	}

	private sealed class DayColumn : Grid
	{
		private readonly TextBlock _value;

		private readonly TextBlock _day;

		private readonly Border _bar;

		private readonly Ellipse _dot;

		private readonly StackPanel _tip;

		private readonly TextBlock _tipDate;

		private readonly TextBlock _tipTotal;

		private readonly TextBlock _tipTop;

		private bool _today;

		private bool _empty;

		public DayColumn()
		{
			base.Background = Brushes.Transparent;
			base.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			base.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			base.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			_value = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
			_value.HorizontalAlignment = HorizontalAlignment.Center;
			base.Children.Add(_value);
			Grid grid = new Grid
			{
				Margin = new Thickness(0.0, 4.0, 0.0, 6.0)
			};
			_bar = new Border
			{
				Width = 22.0,
				CornerRadius = new CornerRadius(5.0),
				VerticalAlignment = VerticalAlignment.Bottom,
				HorizontalAlignment = HorizontalAlignment.Center,
				SnapsToDevicePixels = true
			};
			grid.Children.Add(_bar);
			_dot = new Ellipse
			{
				Width = 4.0,
				Height = 4.0,
				Fill = BuddyTheme.Text100,
				VerticalAlignment = VerticalAlignment.Bottom,
				HorizontalAlignment = HorizontalAlignment.Center,
				Visibility = Visibility.Collapsed
			};
			grid.Children.Add(_dot);
			Grid.SetRow(grid, 1);
			base.Children.Add(grid);
			_day = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
			_day.HorizontalAlignment = HorizontalAlignment.Center;
			Grid.SetRow(_day, 2);
			base.Children.Add(_day);
			_tip = new StackPanel();
			_tipDate = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
			_tipTotal = Txt("", 13.0, BuddyTheme.Text100, AynType.Medium);
			_tipTotal.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			_tipTop = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
			_tipTop.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			_tip.Children.Add(_tipDate);
			_tip.Children.Add(_tipTotal);
			_tip.Children.Add(_tipTop);
			ToolTip toolTip = BuddyTooltip.Create("");
			toolTip.Content = _tip;
			toolTip.Padding = new Thickness(10.0, 7.0, 10.0, 7.0);
			base.ToolTip = toolTip;
			MouseEventHandler value = delegate
			{
				Paint();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Paint();
			};
		}

		public void Set(DayScreenTimeRecord rec, bool today, int max, int limit)
		{
			_today = today;
			int num = ScreenTimeService.DayTotal(rec);
			_empty = num <= 0;
			DateTime result;
			bool flag = DateTime.TryParseExact(rec.DateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
			SetText(_value, _empty ? "—" : ScreenTimeService.FormatDuration(num));
			SetText(_day, flag ? result.ToString("ddd", CultureInfo.CurrentCulture) : "");
			_bar.Height = (_empty ? 3.0 : Math.Max(4.0, Math.Round(50.0 * (double)num / (double)Math.Max(1, max))));
			bool flag2 = limit > 0 && num > limit;
			_dot.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			_dot.Margin = new Thickness(0.0, 0.0, 0.0, _bar.Height + 4.0);
			SetText(_tipDate, flag ? ((today ? "Today, " : "") + result.ToString("ddd, d MMM", CultureInfo.CurrentCulture)) : rec.DateString);
			string text = (_empty ? "No screen time recorded" : ScreenTimeService.FormatDuration(num));
			if (flag2)
			{
				text = text + "  ·  " + ScreenTimeService.FormatDuration(num - limit) + " over limit";
			}
			SetText(_tipTotal, text);
			KeyValuePair<string, int>? keyValuePair = ScreenTimeService.DayTopApp(rec);
			_tipTop.Visibility = ((!keyValuePair.HasValue) ? Visibility.Collapsed : Visibility.Visible);
			if (keyValuePair.HasValue)
			{
				SetText(_tipTop, "Top: " + keyValuePair.Value.Key + "  ·  " + ScreenTimeService.FormatDuration(keyValuePair.Value.Value));
			}
			Paint();
		}

		private void Paint()
		{
			bool isMouseOver = base.IsMouseOver;
			_bar.Background = ((_today && !_empty) ? BuddyTheme.Text100 : (_empty ? BuddyTheme.Track : (isMouseOver ? BuddyTheme.DockSeparator : BuddyTheme.ControlActive)));
			_value.Foreground = ((_today || isMouseOver) ? BuddyTheme.Text100 : (_empty ? BuddyTheme.Text30 : BuddyTheme.Text50));
			_value.FontWeight = (_today ? AynType.Medium : AynType.Regular);
			_day.Foreground = ((_today || isMouseOver) ? BuddyTheme.Text100 : BuddyTheme.Text50);
			_day.FontWeight = (_today ? AynType.Medium : AynType.Regular);
		}

		private static void SetText(TextBlock tb, string s)
		{
			if (tb.Text != s)
			{
				tb.Text = s;
			}
		}
	}

	private sealed class MiniSwitch : Border
	{
		private readonly Ellipse _knob;

		private bool _on;

		public bool On
		{
			get
			{
				return _on;
			}
			set
			{
				_on = value;
				Paint();
			}
		}

		public MiniSwitch()
		{
			base.Width = 30.0;
			base.Height = 18.0;
			base.CornerRadius = new CornerRadius(9.0);
			base.Padding = new Thickness(2.0);
			_knob = new Ellipse
			{
				Width = 14.0,
				Height = 14.0
			};
			Child = _knob;
			Paint();
		}

		public void Toggle()
		{
			On = !_on;
		}

		private void Paint()
		{
			base.Background = (_on ? ThemeTokens.Accent : ThemeTokens.Surface3);
			_knob.Fill = (_on ? ThemeTokens.AccentFg : ThemeTokens.IconMuted);
			_knob.HorizontalAlignment = (_on ? HorizontalAlignment.Right : HorizontalAlignment.Left);
		}
	}

	private sealed class AppList : Grid
	{
		private const double RowH = 40.0;

		private const double Stride = 42.0;

		private const double Fade = 22.0;

		private readonly Canvas _canvas = new Canvas
		{
			ClipToBounds = false
		};

		private readonly Border _thumb;

		private readonly Dictionary<int, AppRow> _active = new Dictionary<int, AppRow>();

		private readonly Stack<AppRow> _pool = new Stack<AppRow>();

		private List<KeyValuePair<string, int>> _items = new List<KeyValuePair<string, int>>();

		private int _total;

		private int _topSeconds;

		private double _offset;

		private double _target;

		private bool _animating;

		private bool _dragging;

		private Point _dragStart;

		private double _dragOrigin;

		private readonly DispatcherTimer _thumbHide;

		private int _maskSig = -1;

		private double ContentHeight
		{
			get
			{
				if (_items.Count != 0)
				{
					return (double)_items.Count * 42.0 - 2.0;
				}
				return 0.0;
			}
		}

		private double MaxOffset => Math.Max(0.0, ContentHeight - base.ActualHeight);

		public AppList()
		{
			base.ClipToBounds = true;
			base.Background = Brushes.Transparent;
			base.Children.Add(_canvas);
			_thumb = new Border
			{
				Width = 3.0,
				CornerRadius = new CornerRadius(1.5),
				Background = ThemeTokens.Text2,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0.0, 0.0, 1.0, 0.0),
				Opacity = 0.0,
				IsHitTestVisible = false,
				RenderTransform = new TranslateTransform()
			};
			base.Children.Add(_thumb);
			_thumbHide = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(700.0)
			};
			DispatcherTimer thumbHide = _thumbHide;
			EventHandler value = delegate
			{
				_thumbHide.Stop();
				BuddyTheme.Animate(_thumb, UIElement.OpacityProperty, 0.0, BuddyTheme.CrossFade);
			};
			thumbHide.Tick += value;
			base.SizeChanged += delegate
			{
				_target = Clamp(_target);
				_offset = Clamp(_offset);
				Layout();
			};
			base.MouseWheel += delegate(object s, MouseWheelEventArgs e)
			{
				if (!(MaxOffset <= 0.0))
				{
					e.Handled = true;
					ScrollTo(_target - (double)e.Delta * 0.4);
				}
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				_dragStart = e.GetPosition(this);
				_dragOrigin = _offset;
				_dragging = false;
				CaptureMouse();
			};
			base.MouseMove += delegate(object s, MouseEventArgs e)
			{
				if (base.IsMouseCaptured)
				{
					double num = e.GetPosition(this).Y - _dragStart.Y;
					if (_dragging || !(Math.Abs(num) < 3.0))
					{
						_dragging = true;
						Jump(_dragOrigin - num);
					}
				}
			};
			base.MouseLeftButtonUp += delegate
			{
				if (base.IsMouseCaptured)
				{
					ReleaseMouseCapture();
				}
				_dragging = false;
			};
			base.IsManipulationEnabled = true;
			base.ManipulationStarting += delegate(object s, ManipulationStartingEventArgs e)
			{
				e.ManipulationContainer = this;
				e.Mode = ManipulationModes.TranslateY;
				e.Handled = true;
			};
			base.ManipulationDelta += delegate(object s, ManipulationDeltaEventArgs e)
			{
				Jump(_offset - e.DeltaManipulation.Translation.Y);
				e.Handled = true;
			};
			base.ManipulationInertiaStarting += delegate(object s, ManipulationInertiaStartingEventArgs e)
			{
				e.TranslationBehavior.DesiredDeceleration = 0.003;
				e.Handled = true;
			};
		}

		private double Clamp(double v)
		{
			return Math.Max(0.0, Math.Min(MaxOffset, v));
		}

		public void SetItems(List<KeyValuePair<string, int>> items, int total)
		{
			_items = items;
			_total = Math.Max(1, total);
			_topSeconds = ((items.Count <= 0) ? 1 : Math.Max(1, items[0].Value));
			_target = Clamp(_target);
			_offset = Clamp(_offset);
			foreach (KeyValuePair<int, AppRow> item in _active.ToList())
			{
				if (item.Key >= _items.Count)
				{
					Release(item.Key);
				}
				else
				{
					item.Value.Bind(_items[item.Key], item.Key == 0, _total, _topSeconds);
				}
			}
			Layout();
		}

		private void ScrollTo(double y)
		{
			_target = Clamp(y);
			ShowThumb();
			if (!CapsuleMotion.AnimationsEnabled)
			{
				_offset = _target;
				Layout();
			}
			else if (!_animating)
			{
				_animating = true;
				CompositionTarget.Rendering += OnFrame;
			}
		}

		private void Jump(double y)
		{
			_offset = (_target = Clamp(y));
			ShowThumb();
			Layout();
		}

		private void OnFrame(object sender, EventArgs e)
		{
			double num = _target - _offset;
			if (Math.Abs(num) < 0.5 || !base.IsVisible)
			{
				_offset = _target;
				_animating = false;
				CompositionTarget.Rendering -= OnFrame;
			}
			else
			{
				_offset += num * 0.22;
			}
			Layout();
		}

		private void ShowThumb()
		{
			if (!(MaxOffset <= 0.0))
			{
				_thumb.BeginAnimation(UIElement.OpacityProperty, null);
				_thumb.Opacity = 1.0;
				_thumbHide.Stop();
				_thumbHide.Start();
			}
		}

		private void Layout()
		{
			double actualHeight = base.ActualHeight;
			double actualWidth = base.ActualWidth;
			if (actualHeight <= 0.0 || actualWidth <= 0.0)
			{
				return;
			}
			int num = Math.Max(0, (int)Math.Floor(_offset / 42.0));
			int num2 = Math.Min(_items.Count - 1, (int)Math.Floor((_offset + actualHeight) / 42.0));
			foreach (int item in _active.Keys.ToList())
			{
				if (item < num || item > num2)
				{
					Release(item);
				}
			}
			for (int i = num; i <= num2; i++)
			{
				if (!_active.TryGetValue(i, out var value))
				{
					value = ((_pool.Count > 0) ? _pool.Pop() : NewRow());
					value.Visibility = Visibility.Visible;
					value.Bind(_items[i], i == 0, _total, _topSeconds);
					_active[i] = value;
				}
				value.Width = actualWidth;
				((TranslateTransform)value.RenderTransform).Y = Math.Round((double)i * 42.0 - _offset);
			}
			double contentHeight = ContentHeight;
			if (contentHeight > actualHeight)
			{
				double num3 = Math.Max(24.0, actualHeight * actualHeight / contentHeight);
				_thumb.Height = num3;
				((TranslateTransform)_thumb.RenderTransform).Y = (actualHeight - num3) * (_offset / MaxOffset);
			}
			else
			{
				_thumb.Opacity = 0.0;
			}
			bool flag = _offset > 1.0;
			bool flag2 = _offset < MaxOffset - 1.0;
			int num4 = (flag ? 1 : 0) | (flag2 ? 2 : 0) | ((int)actualHeight << 2);
			if (num4 != _maskSig)
			{
				_maskSig = num4;
				if (!flag && !flag2)
				{
					_canvas.OpacityMask = null;
					return;
				}
				double num5 = Math.Min(0.3, 22.0 / actualHeight);
				LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
				linearGradientBrush.MappingMode = BrushMappingMode.Absolute;
				linearGradientBrush.StartPoint = new Point(0.0, 0.0);
				linearGradientBrush.EndPoint = new Point(0.0, actualHeight);
				LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
				linearGradientBrush2.GradientStops.Add(new GradientStop(flag ? Colors.Transparent : Colors.Black, 0.0));
				linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag ? num5 : 0.0));
				linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag2 ? (1.0 - num5) : 1.0));
				linearGradientBrush2.GradientStops.Add(new GradientStop(flag2 ? Colors.Transparent : Colors.Black, 1.0));
				linearGradientBrush2.Freeze();
				_canvas.OpacityMask = linearGradientBrush2;
			}
		}

		private AppRow NewRow()
		{
			AppRow appRow = new AppRow();
			appRow.Height = 40.0;
			appRow.RenderTransform = new TranslateTransform();
			AppRow appRow2 = appRow;
			_canvas.Children.Add(appRow2);
			return appRow2;
		}

		private void Release(int index)
		{
			if (_active.TryGetValue(index, out var value))
			{
				_active.Remove(index);
				value.Visibility = Visibility.Collapsed;
				value.Unbind();
				_pool.Push(value);
			}
		}
	}

	private sealed class AppRow : Border
	{
		private readonly Image _icon;

		private readonly TextBlock _name;

		private readonly TextBlock _time;

		private readonly TextBlock _pct;

		private readonly Border _fill;

		private readonly Grid _barHost;

		private string _app;

		private double _fraction;

		public AppRow()
		{
			base.CornerRadius = new CornerRadius(10.0);
			base.Padding = new Thickness(6.0, 0.0, 8.0, 0.0);
			base.Background = Brushes.Transparent;
			BuddyTheme.Crisp(this);
			Grid grid = new Grid
			{
				ColumnDefinitions = 
				{
					new ColumnDefinition
					{
						Width = new GridLength(26.0)
					},
					new ColumnDefinition
					{
						Width = new GridLength(10.0)
					},
					new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					},
					new ColumnDefinition
					{
						Width = new GridLength(12.0)
					},
					new ColumnDefinition
					{
						Width = GridLength.Auto,
						SharedSizeGroup = null
					}
				}
			};
			_icon = new Image
			{
				Width = 26.0,
				Height = 26.0,
				VerticalAlignment = VerticalAlignment.Center,
				Stretch = Stretch.Uniform
			};
			RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
			grid.Children.Add(_icon);
			StackPanel stackPanel = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			_name = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
			stackPanel.Children.Add(_name);
			_barHost = new Grid
			{
				Height = 3.0,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
			};
			_barHost.Children.Add(new Border
			{
				CornerRadius = new CornerRadius(1.5),
				Background = BuddyTheme.Track
			});
			_fill = new Border
			{
				CornerRadius = new CornerRadius(1.5),
				Background = BuddyTheme.Mark,
				HorizontalAlignment = HorizontalAlignment.Left
			};
			_barHost.Children.Add(_fill);
			Grid barHost = _barHost;
			SizeChangedEventHandler value = delegate
			{
				SizeFill();
			};
			barHost.SizeChanged += value;
			stackPanel.Children.Add(_barHost);
			Grid.SetColumn(stackPanel, 2);
			grid.Children.Add(stackPanel);
			StackPanel stackPanel2 = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center,
				MinWidth = 40.0
			};
			_time = Txt("", 13.0, BuddyTheme.Text100, AynType.Medium);
			_time.HorizontalAlignment = HorizontalAlignment.Right;
			stackPanel2.Children.Add(_time);
			_pct = Txt("", 10.0, BuddyTheme.Text50, FontWeights.Medium);
			_pct.HorizontalAlignment = HorizontalAlignment.Right;
			_pct.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
			stackPanel2.Children.Add(_pct);
			Grid.SetColumn(stackPanel2, 4);
			grid.Children.Add(stackPanel2);
			Child = grid;
		}

		public void Bind(KeyValuePair<string, int> item, bool top, int total, int topSeconds)
		{
			string app = item.Key;
			if (app != _app)
			{
				_app = app;
				_name.Text = app;
				base.ToolTip = BuddyTooltip.Create(app);
				_icon.Source = AppIconExtractor.Get(app, delegate(ImageSource img)
				{
					if (_app == app)
					{
						_icon.Source = img;
					}
				});
			}
			string text = ScreenTimeService.FormatDuration(item.Value);
			if (_time.Text != text)
			{
				_time.Text = text;
			}
			double num = 100.0 * (double)item.Value / (double)Math.Max(1, total);
			string text2 = ((num >= 1.0) ? (Math.Round(num).ToString(CultureInfo.CurrentCulture) + "%") : "<1%");
			if (_pct.Text != text2)
			{
				_pct.Text = text2;
			}
			base.Background = (top ? BuddyTheme.Control : Brushes.Transparent);
			_fill.Background = (top ? BuddyTheme.Text100 : BuddyTheme.Text92);
			_fraction = Math.Max(0.0, Math.Min(1.0, (double)item.Value / (double)Math.Max(1, topSeconds)));
			SizeFill();
		}

		public void Unbind()
		{
			_app = null;
			_icon.Source = null;
		}

		private void SizeFill()
		{
			double actualWidth = _barHost.ActualWidth;
			if (!(actualWidth <= 0.0))
			{
				_fill.Width = Math.Max(3.0, Math.Round(actualWidth * _fraction));
			}
		}
	}

	private const double LeftW = 494.0;

	private const double TodayH = 92.0;

	private const double Value = 24.0;

	private const double RingD = 36.0;

	private const double RingStroke = 2.5;

	private const double RingGap = 6.0;

	private const double CellPad = 1.0;

	private const double BarW = 22.0;

	private const double BarMaxH = 50.0;

	private Grid _root;

	private EventHandler _usageHandler;

	private TextBlock _tTotal;

	private TextBlock _tTotalLabel;

	private TextBlock _tApps;

	private TextBlock _tAppsLabel;

	private TextBlock _tLimitValue;

	private TextBlock _tLimitLabel;

	private LimitRing _tRing;

	private FrameworkElement _tLimitBlock;

	private StackPanel _tLimitStack;

	private StackPanel _tLimitText;

	private string _tLimitSig;

	private TextBlock _wAvg;

	private readonly DayColumn[] _days = new DayColumn[7];

	private AppList _list;

	private TextBlock _listEmpty;

	private FrameworkElement _refreshGlyph;

	private Popup _popup;

	private TextBlock _pValue;

	private readonly List<Tuple<Border, int>> _pChips = new List<Tuple<Border, int>>();

	private MiniSwitch _pNotch;

	private MiniSwitch _pNotify;

	private MiniSwitch _pPause;

	private int _pLimit;

	private static readonly Geometry PencilGeo = MakePencil();

	public string Id => "screentime";

	public string DisplayName => "Screen Time";

	public ToolCategory Category => ToolCategory.System;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public ScreenTimeTool()
	{
		IsEnabled = true;
		OrderIndex = 23;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_usageHandler = delegate
		{
			if (State == ToolState.Active)
			{
				SafeRefreshUI();
			}
		};
		ScreenTimeService.Instance.UsageUpdated += _usageHandler;
	}

	public void Shutdown()
	{
		if (_usageHandler != null)
		{
			ScreenTimeService.Instance.UsageUpdated -= _usageHandler;
		}
		if (_popup != null)
		{
			_popup.IsOpen = false;
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		SafeRefreshUI();
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		if (_popup != null)
		{
			_popup.IsOpen = false;
		}
	}

	public bool HandleEscape()
	{
		if (_popup != null && _popup.IsOpen)
		{
			_popup.IsOpen = false;
			return true;
		}
		return false;
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return new CollapsedBadgeData(ScreenTimeService.FormatDuration(ScreenTimeService.Instance.TodayTotalSeconds), "#FFFFFF");
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateScreenTimeIcon(size, stroke);
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			SafeRefreshUI();
			return _root;
		}
		Grid grid = new Grid();
		grid.Width = 728.0;
		grid.Height = 242.0;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		grid.Background = Brushes.Transparent;
		grid.Tag = "NoOuterScroll";
		Grid grid2 = BuddyTheme.Crisp(grid);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(494.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Grid grid3 = new Grid();
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(92.0)
		});
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(10.0)
		});
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement element = BuildToday();
		grid3.Children.Add(element);
		FrameworkElement element2 = BuildWeek();
		Grid.SetRow(element2, 2);
		grid3.Children.Add(element2);
		grid2.Children.Add(grid3);
		FrameworkElement element3 = BuildByApp();
		Grid.SetColumn(element3, 2);
		grid2.Children.Add(element3);
		BuildPopup();
		_root = grid2;
		SafeRefreshUI();
		return grid2;
	}

	private static Border Card(UIElement child)
	{
		Border border = new Border();
		border.Background = BuddyTheme.CardFill;
		border.BorderBrush = BuddyTheme.Hairline;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(13.0, 13.0, 13.0, 11.0);
		border.Child = child;
		Border border2 = BuddyTheme.Crisp(border);
		RenderOptions.SetClearTypeHint(border2, ClearTypeHint.Enabled);
		return ThemeTokens.Card(border2);
	}

	private static TextBlock Txt(string text, double size, Brush ink, FontWeight weight)
	{
		TextBlock textBlock = BuddyTheme.Text(text, size, ink, weight);
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		return textBlock;
	}

	private static TextBlock ValueText(string text)
	{
		TextBlock textBlock = Txt(text, 24.0, BuddyTheme.Text100, AynType.Semibold);
		textBlock.FontFamily = BuddyTheme.DisplayFont;
		textBlock.LineHeight = 26.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		return textBlock;
	}

	private static TextBlock LabelLine(string text)
	{
		TextBlock textBlock = Txt(text, 11.5, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
		return textBlock;
	}

	private static FrameworkElement VRule()
	{
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Height = 36.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.VerticalAlignment = VerticalAlignment.Center;
		rectangle.IsHitTestVisible = false;
		return rectangle;
	}

	private FrameworkElement BuildToday()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.Children.Add(BuddyTheme.CardHeader("data_bar_vertical", "Today"));
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		Grid grid3 = grid2;
		double[] array = new double[5] { 1.0, 0.0, 1.0, 0.0, 1.0 };
		foreach (double num in array)
		{
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = ((num > 0.0) ? new GridLength(1.0, GridUnitType.Star) : new GridLength(1.0))
			});
		}
		Grid.SetRow(grid3, 1);
		grid.Children.Add(grid3);
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		_tTotal = ValueText("0m");
		_tTotal.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel2.Children.Add(_tTotal);
		_tTotalLabel = LabelLine("Screen time");
		_tTotalLabel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel2.Children.Add(_tTotalLabel);
		grid3.Children.Add(stackPanel2);
		Place(grid3, VRule(), 1);
		_tLimitStack = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_tRing = new LimitRing
		{
			Width = 36.0,
			Height = 36.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		_tLimitStack.Children.Add(_tRing);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		_tLimitValue = ValueText("");
		_tLimitValue.TextTrimming = TextTrimming.None;
		stackPanel4.Children.Add(_tLimitValue);
		_tLimitLabel = LabelLine("");
		_tLimitLabel.TextTrimming = TextTrimming.None;
		stackPanel4.Children.Add(_tLimitLabel);
		_tLimitText = stackPanel4;
		_tLimitStack.Children.Add(stackPanel4);
		Border hit = new Border
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = _tLimitStack,
			Padding = new Thickness(1.0, 0.0, 1.0, 0.0)
		};
		hit.ToolTip = BuddyTooltip.Create("Change daily limit");
		hit.MouseEnter += delegate
		{
			_tLimitLabel.Foreground = BuddyTheme.Text92;
		};
		hit.MouseLeave += delegate
		{
			_tLimitLabel.Foreground = BuddyTheme.Text50;
		};
		hit.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		hit.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			OpenPopup(hit);
		};
		hit.SizeChanged += delegate(object s, SizeChangedEventArgs e)
		{
			if (e.WidthChanged)
			{
				RenderLimit(ScreenTimeService.Instance);
			}
		};
		_tLimitBlock = hit;
		Place(grid3, hit, 2);
		Place(grid3, VRule(), 3);
		StackPanel stackPanel5 = new StackPanel();
		stackPanel5.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel5.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel6 = stackPanel5;
		_tApps = ValueText("0");
		_tApps.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel6.Children.Add(_tApps);
		_tAppsLabel = LabelLine("Apps used");
		_tAppsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel6.Children.Add(_tAppsLabel);
		Place(grid3, stackPanel6, 4);
		return Card(grid);
	}

	private static void Place(Grid g, UIElement e, int col)
	{
		Grid.SetColumn(e, col);
		g.Children.Add(e);
	}

	private FrameworkElement BuildWeek()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.Children.Add(BuddyTheme.CardHeader("calendar_ltr", "Last 7 days"));
		_wAvg = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_wAvg.HorizontalAlignment = HorizontalAlignment.Right;
		_wAvg.VerticalAlignment = VerticalAlignment.Center;
		_wAvg.Margin = new Thickness(0.0, -2.0, 0.0, -2.0);
		grid.Children.Add(_wAvg);
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		Grid grid3 = grid2;
		for (int i = 0; i < 7; i++)
		{
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			_days[i] = new DayColumn();
			Place(grid3, _days[i], i);
		}
		Grid.SetRow(grid3, 1);
		grid.Children.Add(grid3);
		return Card(grid);
	}

	private FrameworkElement BuildByApp()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Grid grid2 = new Grid();
		grid2.Height = 22.0;
		Grid grid3 = grid2;
		FrameworkElement frameworkElement = BuddyTheme.CardHeader("data_histogram", "By app");
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid3.Children.Add(frameworkElement);
		grid3.Children.Add(BuildRefreshButton());
		grid.Children.Add(grid3);
		Grid grid4 = new Grid();
		grid4.Margin = new Thickness(-6.0, 6.0, -6.0, 0.0);
		Grid grid5 = grid4;
		_list = new AppList();
		grid5.Children.Add(_list);
		_listEmpty = Txt("No app usage yet today", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_listEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_listEmpty.VerticalAlignment = VerticalAlignment.Center;
		_listEmpty.Visibility = Visibility.Collapsed;
		grid5.Children.Add(_listEmpty);
		Grid.SetRow(grid5, 1);
		grid.Children.Add(grid5);
		return Card(grid);
	}

	private FrameworkElement BuildRefreshButton()
	{
		FrameworkElement frameworkElement = FluentIcons.Create("arrow_reset", 13.0, BuddyTheme.Text50, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		RotateTransform rot = new RotateTransform(0.0);
		frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
		frameworkElement.RenderTransform = rot;
		_refreshGlyph = frameworkElement;
		Border b = new Border
		{
			Width = 24.0,
			Height = 24.0,
			CornerRadius = new CornerRadius(12.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Child = frameworkElement,
			ToolTip = BuddyTooltip.Create("Refresh")
		};
		b.MouseEnter += delegate
		{
			b.Background = BuddyTheme.Control;
		};
		b.MouseLeave += delegate
		{
			b.Background = Brushes.Transparent;
		};
		b.MouseLeftButtonDown += delegate(object x, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		b.MouseLeftButtonUp += delegate(object x, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (CapsuleMotion.AnimationsEnabled)
			{
				rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0.0, 360.0, new Duration(TimeSpan.FromMilliseconds(420.0)))
				{
					EasingFunction = BuddyTheme.EaseOut
				});
			}
			ScreenTimeService.Instance.Refresh();
			SafeRefreshUI();
		};
		return b;
	}

	public void SafeRefreshUI()
	{
		if (_root == null)
		{
			return;
		}
		if (!_root.Dispatcher.CheckAccess())
		{
			_root.Dispatcher.BeginInvoke(new Action(SafeRefreshUI));
			return;
		}
		try
		{
			Refresh();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("ScreenTime refresh: " + ex.Message);
		}
	}

	private void Refresh()
	{
		ScreenTimeService instance = ScreenTimeService.Instance;
		int todayTotalSeconds = instance.TodayTotalSeconds;
		Set(_tTotal, ScreenTimeService.FormatDuration(todayTotalSeconds));
		Set(_tTotalLabel, instance.IsPaused ? "Screen time · paused" : "Screen time");
		int appsUsedToday = instance.AppsUsedToday;
		Set(_tApps, appsUsedToday.ToString(CultureInfo.CurrentCulture));
		Set(_tAppsLabel, (appsUsedToday == 1) ? "App used" : "Apps used");
		RenderLimit(instance);
		List<DayScreenTimeRecord> rolling7Days = instance.GetRolling7Days();
		int num = 3600;
		int num2 = 0;
		int num3 = 0;
		foreach (DayScreenTimeRecord item in rolling7Days)
		{
			int num4 = ScreenTimeService.DayTotal(item);
			num = Math.Max(num, num4);
			if (num4 > 0)
			{
				num2 += num4;
				num3++;
			}
		}
		Set(_wAvg, (num3 > 0) ? ("avg " + ScreenTimeService.FormatDuration(num2 / num3)) : "");
		for (int i = 0; i < 7 && i < rolling7Days.Count; i++)
		{
			_days[i].Set(rolling7Days[i], i == rolling7Days.Count - 1, num, instance.HasLimit ? instance.DailyLimitSeconds : 0);
		}
		List<KeyValuePair<string, int>> topAppsToday = instance.GetTopAppsToday(0);
		_list.SetItems(topAppsToday, todayTotalSeconds);
		_listEmpty.Text = ((instance.IsPaused && topAppsToday.Count == 0) ? "Tracking is paused" : "No app usage yet today");
		_listEmpty.Visibility = ((topAppsToday.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void RenderLimit(ScreenTimeService svc)
	{
		double f = 0.0;
		bool flag = svc.HasLimit;
		string text;
		string text2;
		string text3;
		if (!svc.HasLimit)
		{
			text = "Set limit";
			text2 = null;
			text3 = "No daily limit";
		}
		else
		{
			int limitRemainingSeconds = svc.LimitRemainingSeconds;
			text3 = svc.LimitFormatted + " limit";
			if (limitRemainingSeconds > 0)
			{
				text = ScreenTimeService.FormatDuration(limitRemainingSeconds);
				text2 = "left";
				f = (double)limitRemainingSeconds / (double)svc.DailyLimitSeconds;
			}
			else if (-limitRemainingSeconds < 60)
			{
				text = "Limit";
				text2 = "reached";
				f = 1.0;
			}
			else
			{
				text = "+" + ScreenTimeService.FormatDuration(-limitRemainingSeconds);
				text2 = "over";
				f = 1.0;
			}
		}
		if (flag)
		{
			_tRing.Set(f);
		}
		double num = _tLimitBlock.ActualWidth - 2.0;
		if (num <= 0.0)
		{
			num = 140.0;
		}
		double num2 = 42.0;
		double num3 = Measure(text, 24.0, AynType.Semibold, BuddyTheme.DisplayFont);
		double num4 = ((text2 != null) ? Measure(" " + text2, 11.5, FontWeights.Medium, BuddyTheme.Font) : 0.0);
		Func<string, double> func = (string t) => Measure(t, 11.5, FontWeights.Medium, BuddyTheme.Font) + 16.0;
		string text4 = ((text2 == null) ? text3 : ("of " + text3));
		object obj;
		string text5;
		switch (text2)
		{
		default:
			obj = "reached · ";
			goto IL_0191;
		case "over":
			obj = "over ";
			goto IL_0191;
		case "left":
			obj = "left of ";
			goto IL_0191;
		case null:
			{
				text5 = text3;
				break;
			}
			IL_0191:
			text5 = (string)obj + text3;
			break;
		}
		string text6 = text5;
		bool flag2;
		if (Math.Max(num3 + num4, func(text4)) <= num - (flag ? num2 : 0.0))
		{
			flag2 = true;
		}
		else if (Math.Max(num3, func(text6)) <= num - (flag ? num2 : 0.0))
		{
			flag2 = false;
		}
		else
		{
			flag = false;
			flag2 = Math.Max(num3 + num4, func(text4)) <= num;
		}
		string text7 = text + "|" + text2 + "|" + text3 + "|" + flag + "|" + flag2;
		if (!(text7 == _tLimitSig))
		{
			_tLimitSig = text7;
			_tRing.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			HorizontalAlignment horizontalAlignment = ((!flag) ? HorizontalAlignment.Center : HorizontalAlignment.Left);
			TextBlock tLimitValue = _tLimitValue;
			HorizontalAlignment horizontalAlignment3 = (_tLimitLabel.HorizontalAlignment = horizontalAlignment);
			tLimitValue.HorizontalAlignment = horizontalAlignment3;
			TextBlock tLimitValue2 = _tLimitValue;
			TextAlignment textAlignment2 = (_tLimitLabel.TextAlignment = ((!flag) ? TextAlignment.Center : TextAlignment.Left));
			tLimitValue2.TextAlignment = textAlignment2;
			_tLimitValue.Inlines.Clear();
			_tLimitValue.Inlines.Add(new Run(text));
			if (text2 != null && flag2)
			{
				_tLimitValue.Inlines.Add(Small(" " + text2));
			}
			_tLimitLabel.Inlines.Clear();
			_tLimitLabel.Inlines.Add(new Run(flag2 ? text4 : text6));
			_tLimitLabel.Inlines.Add(new InlineUIContainer(PencilGlyph())
			{
				BaselineAlignment = BaselineAlignment.Center
			});
		}
	}

	private double Measure(string text, double size, FontWeight weight, FontFamily family)
	{
		char[] array = text.ToCharArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (char.IsDigit(array[i]))
			{
				array[i] = '4';
			}
		}
		FormattedText formattedText = new FormattedText(new string(array), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal), size, Brushes.White);
		return Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace) + 1.0;
	}

	private static Geometry MakePencil()
	{
		Geometry geometry = Geometry.Parse("M2.5,13.5 L3.2,10.4 L10.9,2.7 C11.6,2 12.7,2 13.4,2.7 C14.1,3.4 14.1,4.5 13.4,5.2 L5.7,12.9 Z M9.6,4 L12.1,6.5");
		geometry.Freeze();
		return geometry;
	}

	private static FrameworkElement PencilGlyph()
	{
		Path path = new Path();
		path.Data = PencilGeo;
		path.Width = 11.0;
		path.Height = 11.0;
		path.Stretch = Stretch.Uniform;
		path.Stroke = BuddyTheme.Text50;
		path.StrokeThickness = 1.0;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		path.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
		path.IsHitTestVisible = false;
		return path;
	}

	private static Run Small(string text)
	{
		Run run = new Run(text);
		run.FontSize = 11.5;
		run.FontWeight = FontWeights.Medium;
		run.FontFamily = BuddyTheme.Font;
		run.Foreground = BuddyTheme.Text50;
		return run;
	}

	private static void Set(TextBlock tb, string s)
	{
		if (tb.Text != s)
		{
			tb.Text = s;
		}
	}

	private void BuildPopup()
	{
		_popup = new Popup
		{
			AllowsTransparency = true,
			StaysOpen = false,
			Placement = PlacementMode.Bottom,
			VerticalOffset = 6.0,
			PopupAnimation = PopupAnimation.Fade
		};
		StackPanel stackPanel = new StackPanel();
		Border border = new Border();
		border.Width = 252.0;
		border.Background = BuddyTheme.Control;
		border.BorderBrush = BuddyTheme.ControlActive;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(14.0);
		border.Padding = new Thickness(14.0, 12.0, 14.0, 12.0);
		border.Child = stackPanel;
		Border border2 = BuddyTheme.Crisp(border);
		border2.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				_popup.IsOpen = false;
			}
		};
		stackPanel.Children.Add(BuddyTheme.LabelText("Daily limit"));
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.Children.Add(Circle("subtract", "15 minutes less", delegate
		{
			Step(-1);
		}));
		_pValue = ValueText("");
		_pValue.HorizontalAlignment = HorizontalAlignment.Center;
		_pValue.VerticalAlignment = VerticalAlignment.Center;
		Place(grid2, _pValue, 1);
		Place(grid2, Circle("add", "15 minutes more", delegate
		{
			Step(1);
		}), 2);
		stackPanel.Children.Add(grid2);
		UniformGrid uniformGrid = new UniformGrid();
		uniformGrid.Rows = 1;
		uniformGrid.Columns = 5;
		uniformGrid.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		UniformGrid uniformGrid2 = uniformGrid;
		int[] array = new int[5] { 1, 2, 4, 6, 8 };
		foreach (int num in array)
		{
			int sec = num * 3600;
			TextBlock textBlock = Txt(num + "h", 13.0, BuddyTheme.Text92, FontWeights.Medium);
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			Border border3 = new Border();
			border3.Height = 26.0;
			border3.Margin = new Thickness(2.0, 0.0, 2.0, 0.0);
			border3.CornerRadius = new CornerRadius(13.0);
			border3.Background = BuddyTheme.ControlActive;
			border3.Cursor = Cursors.Hand;
			border3.Child = textBlock;
			Border border4 = border3;
			border4.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			border4.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				_pLimit = sec;
				SyncPopup();
			};
			uniformGrid2.Children.Add(border4);
			_pChips.Add(Tuple.Create(border4, sec));
		}
		uniformGrid2.Margin = new Thickness(-2.0, 12.0, -2.0, 0.0);
		stackPanel.Children.Add(uniformGrid2);
		stackPanel.Children.Add(new Rectangle
		{
			Height = 1.0,
			Fill = BuddyTheme.ControlActive,
			Margin = new Thickness(0.0, 12.0, 0.0, 4.0)
		});
		_pNotch = new MiniSwitch();
		_pNotify = new MiniSwitch();
		_pPause = new MiniSwitch();
		stackPanel.Children.Add(SwitchRow("Notch alert at the limit", _pNotch));
		stackPanel.Children.Add(SwitchRow("Notification at the limit", _pNotify));
		stackPanel.Children.Add(SwitchRow("Pause tracking", _pPause));
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		Grid grid4 = grid3;
		TextBlock remove = Txt("Remove limit", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		remove.VerticalAlignment = VerticalAlignment.Center;
		remove.Cursor = Cursors.Hand;
		remove.MouseEnter += delegate
		{
			remove.Foreground = BuddyTheme.Text100;
		};
		remove.MouseLeave += delegate
		{
			remove.Foreground = BuddyTheme.Text50;
		};
		remove.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			_pLimit = 0;
			Save();
		};
		grid4.Children.Add(remove);
		TextBlock textBlock2 = Txt("Save", 13.0, BuddyTheme.PrimaryInk, AynType.Medium);
		textBlock2.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		Border save = new Border
		{
			Height = 28.0,
			Padding = new Thickness(16.0, 0.0, 16.0, 0.0),
			CornerRadius = new CornerRadius(14.0),
			Background = BuddyTheme.PrimaryFill,
			Cursor = Cursors.Hand,
			HorizontalAlignment = HorizontalAlignment.Right,
			Child = textBlock2
		};
		save.MouseEnter += delegate
		{
			save.Opacity = 0.9;
		};
		save.MouseLeave += delegate
		{
			save.Opacity = 1.0;
		};
		save.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		save.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			Save();
		};
		grid4.Children.Add(save);
		stackPanel.Children.Add(grid4);
		_popup.Child = border2;
	}

	private static FrameworkElement SwitchRow(string label, MiniSwitch sw)
	{
		Grid grid = new Grid();
		grid.Height = 28.0;
		grid.Background = Brushes.Transparent;
		grid.Cursor = Cursors.Hand;
		Grid grid2 = grid;
		TextBlock textBlock = Txt(label, 13.0, BuddyTheme.Text92, FontWeights.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(textBlock);
		sw.HorizontalAlignment = HorizontalAlignment.Right;
		sw.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(sw);
		grid2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			sw.Toggle();
		};
		return grid2;
	}

	private static FrameworkElement Circle(string icon, string tip, Action click)
	{
		FrameworkElement frameworkElement = FluentIcons.Create(icon, 13.0, BuddyTheme.Text92, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		Border b = new Border
		{
			Width = 28.0,
			Height = 28.0,
			CornerRadius = new CornerRadius(14.0),
			Background = BuddyTheme.ControlActive,
			Cursor = Cursors.Hand,
			Child = frameworkElement,
			ToolTip = BuddyTooltip.Create(tip)
		};
		b.MouseEnter += delegate
		{
			b.Background = BuddyTheme.DockSeparator;
		};
		b.MouseLeave += delegate
		{
			b.Background = BuddyTheme.ControlActive;
		};
		b.MouseLeftButtonDown += delegate(object x, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		b.MouseLeftButtonUp += delegate(object x, MouseButtonEventArgs e)
		{
			e.Handled = true;
			click();
		};
		return b;
	}

	private void OpenPopup(UIElement target)
	{
		ScreenTimeService instance = ScreenTimeService.Instance;
		_pLimit = (instance.HasLimit ? instance.DailyLimitSeconds : 0);
		_pNotch.On = instance.ShowNotchAlertOnLimit;
		_pNotify.On = instance.NotifyOnLimitReached;
		_pPause.On = instance.IsPaused;
		SyncPopup();
		_popup.PlacementTarget = target;
		_popup.IsOpen = true;
	}

	private void Step(int dir)
	{
		if (_pLimit <= 0)
		{
			if (dir > 0)
			{
				_pLimit = 3600;
			}
		}
		else
		{
			_pLimit = Math.Max(900, Math.Min(57600, (_pLimit / 900 + dir) * 900));
		}
		SyncPopup();
	}

	private void SyncPopup()
	{
		_pValue.Text = ((_pLimit > 0) ? ScreenTimeService.FormatDuration(_pLimit) : "No limit");
		foreach (Tuple<Border, int> pChip in _pChips)
		{
			bool flag = pChip.Item2 == _pLimit;
			pChip.Item1.Background = (flag ? BuddyTheme.SelectedFill : BuddyTheme.ControlActive);
			pChip.Item1.BorderThickness = new Thickness(1.0);
			pChip.Item1.BorderBrush = (flag ? BuddyTheme.SelectedEdge : Brushes.Transparent);
			TextBlock textBlock = (TextBlock)pChip.Item1.Child;
			textBlock.Foreground = (flag ? BuddyTheme.SelectedInk : BuddyTheme.Text92);
			textBlock.FontWeight = (flag ? AynType.Medium : AynType.Regular);
		}
	}

	private void Save()
	{
		ScreenTimeService instance = ScreenTimeService.Instance;
		instance.SetDailyLimit(_pLimit, _pNotify.On, _pNotch.On);
		instance.IsPaused = _pPause.On;
		_popup.IsOpen = false;
		SafeRefreshUI();
	}
}
