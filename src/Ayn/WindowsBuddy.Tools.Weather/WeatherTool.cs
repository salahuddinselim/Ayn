using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.Tools.Weather;

public class WeatherTool : IBuddyTool, IEscapeAware
{
	private sealed class Segment : Border
	{
		private readonly TextBlock _text;

		private bool _active;

		public Segment(string label, Action click)
		{
			Segment segment = this;
			base.Width = 34.0;
			base.Height = 24.0;
			base.CornerRadius = new CornerRadius(base.Height / 2.0);
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			_text = BuddyTheme.Text(label, 13.0, BuddyTheme.Text50, FontWeights.Medium);
			_text.HorizontalAlignment = HorizontalAlignment.Center;
			_text.VerticalAlignment = VerticalAlignment.Center;
			Child = _text;
			MouseEventHandler value = delegate
			{
				Paint();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Paint();
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				if (!segment._active)
				{
					click();
				}
			};
		}

		public void SetActive(bool on)
		{
			_active = on;
			Paint();
		}

		private void Paint()
		{
			base.Background = (_active ? BuddyTheme.ControlActive : Brushes.Transparent);
			_text.Foreground = (_active ? BuddyTheme.Text100 : (base.IsMouseOver ? BuddyTheme.Text92 : BuddyTheme.Text50));
			_text.FontWeight = (_active ? AynType.Medium : AynType.Regular);
		}
	}

	private sealed class Pill : Border
	{
		private readonly StackPanel _sp;

		private readonly Action _click;

		public Pill(string glyph, string label, Action click)
		{
			_click = click;
			base.Height = 28.0;
			base.CornerRadius = new CornerRadius(14.0);
			base.Background = BuddyTheme.Control;
			base.Padding = new Thickness(10.0, 0.0, 12.0, 0.0);
			base.Cursor = Cursors.Hand;
			base.VerticalAlignment = VerticalAlignment.Center;
			_sp = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			Child = _sp;
			SetLabel(label, glyph);
			MouseEventHandler value = delegate
			{
				base.Background = BuddyTheme.ControlHover;
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				base.Background = BuddyTheme.Control;
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				_click();
			};
		}

		public void SetLabel(string label, string glyph)
		{
			_sp.Children.Clear();
			base.Padding = new Thickness((glyph != null) ? 10 : 12, 0.0, 12.0, 0.0);
			if (glyph != null)
			{
				FrameworkElement frameworkElement = WeatherGlyphs.Symbol(glyph, 14.0, BuddyTheme.Text92);
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				frameworkElement.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
				_sp.Children.Add(frameworkElement);
			}
			TextBlock textBlock = BuddyTheme.Text(label, 13.0, BuddyTheme.Text92, FontWeights.Medium);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			_sp.Children.Add(textBlock);
		}
	}

	private sealed class DayRow : Grid
	{
		private readonly TextBlock _day;

		private readonly TextBlock _lo;

		private readonly TextBlock _hi;

		private readonly ContentControl _icon;

		private readonly RangeBar _bar;

		private string _iconKey;

		public DayRow(bool rule)
		{
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(40.0)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(26.0)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(28.0)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(30.0)
			});
			base.Background = Brushes.Transparent;
			if (rule)
			{
				Rectangle element = new Rectangle
				{
					Height = 1.0,
					Fill = BuddyTheme.Hairline,
					VerticalAlignment = VerticalAlignment.Top,
					IsHitTestVisible = false
				};
				Grid.SetColumnSpan(element, 5);
				base.Children.Add(element);
			}
			_day = Txt("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
			_day.VerticalAlignment = VerticalAlignment.Center;
			base.Children.Add(_day);
			_icon = new ContentControl
			{
				Width = 16.0,
				Height = 16.0,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false,
				Focusable = false
			};
			Grid.SetColumn(_icon, 1);
			base.Children.Add(_icon);
			_lo = Txt("", 13.0, BuddyTheme.Text50, FontWeights.Medium);
			_lo.HorizontalAlignment = HorizontalAlignment.Right;
			_lo.VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(_lo, 2);
			base.Children.Add(_lo);
			_bar = new RangeBar
			{
				Height = 8.0,
				Margin = new Thickness(8.0, 0.0, 8.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(_bar, 3);
			base.Children.Add(_bar);
			_hi = Txt("", 13.0, BuddyTheme.Text92, AynType.Medium);
			_hi.HorizontalAlignment = HorizontalAlignment.Right;
			_hi.VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(_hi, 4);
			base.Children.Add(_hi);
		}

		public void Set(string day, int code, int lo, int hi, double min, double max, double? now, string condition)
		{
			Set(_day, day);
			string text = code.ToString(CultureInfo.InvariantCulture);
			if (text != _iconKey)
			{
				_iconKey = text;
				_icon.Content = WeatherGlyphs.Create(code, day: true, 16.0, BuddyTheme.Text92);
			}
			Set(_lo, lo + "°");
			Set(_hi, hi + "°");
			_bar.Set(lo, hi, min, max, now);
			base.ToolTip = BuddyTooltip.Create(condition);
		}

		private static void Set(TextBlock tb, string s)
		{
			if (tb.Text != s)
			{
				tb.Text = s;
			}
		}
	}

	private sealed class RangeBar : FrameworkElement
	{
		private const double H = 4.0;

		private double _lo;

		private double _hi;

		private double _min;

		private double _max;

		private double? _now;

		private static readonly Pen NowPen = MakePen();

		public RangeBar()
		{
			base.IsHitTestVisible = false;
			base.SnapsToDevicePixels = true;
		}

		public void Set(double lo, double hi, double min, double max, double? now)
		{
			_lo = lo;
			_hi = hi;
			_min = min;
			_max = max;
			_now = now;
			InvalidateVisual();
		}

		protected override void OnRender(DrawingContext dc)
		{
			double actualWidth = base.ActualWidth;
			double num = base.ActualHeight / 2.0;
			if (!(actualWidth <= 4.0))
			{
				dc.DrawRoundedRectangle(BuddyTheme.Track, null, new Rect(0.0, num - 2.0, actualWidth, 4.0), 2.0, 2.0);
				double num2 = Math.Max(1.0, _max - _min);
				double num3 = (_lo - _min) / num2 * actualWidth;
				double num4 = (_hi - _min) / num2 * actualWidth;
				if (num4 - num3 < 4.0)
				{
					double num5 = (num3 + num4) / 2.0;
					num3 = Math.Max(0.0, num5 - 2.0);
					num4 = Math.Min(actualWidth, num3 + 4.0);
				}
				dc.DrawRoundedRectangle(BuddyTheme.Text100, null, new Rect(num3, num - 2.0, num4 - num3, 4.0), 2.0, 2.0);
				if (_now.HasValue)
				{
					double x = Math.Max(2.0, Math.Min(actualWidth - 2.0, (_now.Value - _min) / num2 * actualWidth));
					dc.DrawEllipse(BuddyTheme.Text100, NowPen, new Point(x, num), 3.5, 3.5);
				}
			}
		}

		private static Pen MakePen()
		{
			return ThemeTokens.FreezeSafe(new Pen(BuddyTheme.CardFill, 1.5));
		}
	}

	private sealed class HourlyCurve : FrameworkElement
	{
		private readonly int _columns;

		private readonly List<double> _values = new List<double>();

		private static readonly Pen LinePen = Freeze(new Pen(BuddyTheme.Text92, 1.5)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round,
			LineJoin = PenLineJoin.Round
		});

		private static readonly Pen GuidePen = Freeze(new Pen(ThemeTokens.BorderStrong, 1.0)
		{
			DashStyle = new DashStyle(new double[2] { 1.0, 3.0 }, 0.0)
		});

		private static readonly Brush FadeFill = ThemeTokens.AccentSoft;

		public HourlyCurve(int columns)
		{
			_columns = columns;
			base.IsHitTestVisible = false;
		}

		public void SetValues(List<double> values)
		{
			bool flag = values.Count == _values.Count;
			int num = 0;
			while (flag && num < values.Count)
			{
				flag = values[num] == _values[num];
				num++;
			}
			if (!flag)
			{
				_values.Clear();
				_values.AddRange(values);
				InvalidateVisual();
			}
		}

		protected override void OnRender(DrawingContext dc)
		{
			int count = _values.Count;
			double actualWidth = base.ActualWidth;
			double actualHeight = base.ActualHeight;
			if (count < 2 || actualWidth <= 0.0 || actualHeight <= 12.0)
			{
				return;
			}
			double num = double.MaxValue;
			double num2 = double.MinValue;
			foreach (double value in _values)
			{
				double val = value;
				num = Math.Min(num, val);
				num2 = Math.Max(num2, val);
			}
			double num3 = Math.Max(4.0, num2 - num);
			double num4 = (num + num2) / 2.0;
			double num5 = 6.0;
			double num6 = actualHeight - 10.0;
			Point[] array = new Point[count];
			for (int i = 0; i < count; i++)
			{
				double a = ((double)i + 0.5) * actualWidth / (double)_columns;
				double y = num5 + (1.0 - (_values[i] - (num4 - num3 / 2.0)) / num3) * (num6 - num5);
				ref Point reference = ref array[i];
				reference = new Point(Math.Round(a) + 0.5, y);
			}
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(array[0], isFilled: false, isClosed: false);
				Spline(streamGeometryContext, array);
			}
			streamGeometry.Freeze();
			StreamGeometry streamGeometry2 = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
			{
				streamGeometryContext2.BeginFigure(new Point(array[0].X, actualHeight), isFilled: true, isClosed: true);
				streamGeometryContext2.LineTo(array[0], isStroked: false, isSmoothJoin: false);
				Spline(streamGeometryContext2, array);
				streamGeometryContext2.LineTo(new Point(array[count - 1].X, actualHeight), isStroked: false, isSmoothJoin: false);
			}
			streamGeometry2.Freeze();
			for (int j = 0; j < count; j++)
			{
				dc.DrawLine(GuidePen, new Point(array[j].X, 0.0), new Point(array[j].X, array[j].Y - 4.0));
			}
			dc.DrawGeometry(FadeFill, null, streamGeometry2);
			dc.DrawGeometry(null, LinePen, streamGeometry);
			for (int k = 0; k < count; k++)
			{
				dc.DrawEllipse((k == 0) ? BuddyTheme.Text100 : BuddyTheme.Text92, null, array[k], (k == 0) ? 3.0 : 2.5, (k == 0) ? 3.0 : 2.5);
			}
		}

		private static void Spline(StreamGeometryContext ctx, Point[] p)
		{
			for (int i = 0; i < p.Length - 1; i++)
			{
				Point point = p[Math.Max(0, i - 1)];
				Point point2 = p[i];
				Point point3 = p[i + 1];
				Point point4 = p[Math.Min(p.Length - 1, i + 2)];
				Point point5 = new Point(point2.X + (point3.X - point.X) / 6.0, point2.Y + (point3.Y - point.Y) / 6.0);
				Point point6 = new Point(point3.X - (point4.X - point2.X) / 6.0, point3.Y - (point4.Y - point2.Y) / 6.0);
				ctx.BezierTo(point5, point6, point3, isStroked: true, isSmoothJoin: true);
			}
		}

		private static T Freeze<T>(T f) where T : Freezable
		{
			return ThemeTokens.FreezeSafe(f);
		}
	}

	private sealed class PickRow : Border
	{
		private readonly TextBlock _name;

		private readonly TextBlock _detail;

		private readonly ContentControl _check;

		private readonly Action _pick;

		private Border _remove;

		private bool _highlighted;

		private bool _current;

		public PickRow(string glyph, string name, string detail, Action pick)
		{
			_pick = pick;
			base.Height = 34.0;
			base.CornerRadius = new CornerRadius(10.0);
			base.Background = Brushes.Transparent;
			base.Padding = new Thickness(10.0, 0.0, 8.0, 0.0);
			base.Cursor = Cursors.Hand;
			Grid grid = new Grid
			{
				ColumnDefinitions = 
				{
					new ColumnDefinition
					{
						Width = GridLength.Auto
					},
					new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					},
					new ColumnDefinition
					{
						Width = GridLength.Auto
					}
				}
			};
			if (glyph != null)
			{
				FrameworkElement frameworkElement = WeatherGlyphs.Symbol(glyph, 14.0, BuddyTheme.Text92);
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				frameworkElement.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
				grid.Children.Add(frameworkElement);
			}
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			_name = BuddyTheme.Text(name, 13.0, BuddyTheme.Text92, FontWeights.Medium);
			stackPanel.Children.Add(_name);
			_detail = BuddyTheme.Text(detail ?? "", 11.5, BuddyTheme.Text50, FontWeights.Medium);
			_detail.Margin = new Thickness(8.0, 1.0, 0.0, 0.0);
			_detail.VerticalAlignment = VerticalAlignment.Center;
			stackPanel.Children.Add(_detail);
			Grid.SetColumn(stackPanel, 1);
			grid.Children.Add(stackPanel);
			FrameworkElement frameworkElement2 = WeatherGlyphs.Symbol("check", 11.0, BuddyTheme.PrimaryInk);
			frameworkElement2.HorizontalAlignment = HorizontalAlignment.Center;
			frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
			_check = new ContentControl
			{
				Width = 18.0,
				Height = 18.0,
				VerticalAlignment = VerticalAlignment.Center,
				Visibility = Visibility.Collapsed,
				IsHitTestVisible = false,
				Focusable = false,
				Content = new Border
				{
					Width = 18.0,
					Height = 18.0,
					CornerRadius = new CornerRadius(9.0),
					Background = BuddyTheme.PrimaryFill,
					Child = frameworkElement2
				}
			};
			AutomationProperties.SetName(_check, "Selected");
			Grid.SetColumn(_check, 2);
			grid.Children.Add(_check);
			Child = grid;
			base.MouseEnter += delegate
			{
				Paint();
			};
			base.MouseLeave += delegate
			{
				Paint();
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				Invoke();
			};
		}

		public void Invoke()
		{
			if (_pick != null)
			{
				_pick();
			}
		}

		public void SetText(string name, string detail)
		{
			if (_name.Text != name)
			{
				_name.Text = name;
			}
			if (_detail.Text != detail)
			{
				_detail.Text = detail ?? "";
			}
		}

		public void SetCurrent(bool on)
		{
			_current = on;
			_check.Visibility = ((!on) ? Visibility.Collapsed : Visibility.Visible);
			Paint();
		}

		public void SetHighlighted(bool on)
		{
			_highlighted = on;
			Paint();
		}

		public void EnableRemove(Action remove)
		{
			Grid grid = (Grid)Child;
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			_remove = new Border
			{
				Width = 20.0,
				Height = 20.0,
				CornerRadius = new CornerRadius(10.0),
				Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
				Background = Brushes.Transparent,
				VerticalAlignment = VerticalAlignment.Center,
				Visibility = Visibility.Hidden,
				ToolTip = BuddyTooltip.Create("Remove from recent")
			};
			FrameworkElement frameworkElement = WeatherGlyphs.Symbol("dismiss", 10.0, BuddyTheme.Text50);
			frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			_remove.Child = frameworkElement;
			_remove.MouseEnter += delegate
			{
				_remove.Background = BuddyTheme.ControlActive;
			};
			_remove.MouseLeave += delegate
			{
				_remove.Background = Brushes.Transparent;
			};
			_remove.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			_remove.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				remove();
			};
			Grid.SetColumn(_remove, 3);
			grid.Children.Add(_remove);
		}

		private void Paint()
		{
			bool flag = base.IsMouseOver || _highlighted;
			base.Background = (flag ? BuddyTheme.Row : (_current ? BuddyTheme.ControlActive : Brushes.Transparent));
			_name.Foreground = ((flag || _current) ? BuddyTheme.Text100 : BuddyTheme.Text92);
			_name.FontWeight = (_current ? FontWeights.SemiBold : FontWeights.Medium);
			if (_remove != null)
			{
				_remove.Visibility = ((!base.IsMouseOver) ? Visibility.Hidden : Visibility.Visible);
			}
		}
	}

	private const double CurrentW = 228.0;

	private const double HourlyW = 268.0;

	private const double DailyW = 212.0;

	private const double CurIcon = 52.0;

	private const double CurTemp = 28.0;

	private const int Hours = 6;

	private const int Days = 5;

	private const double SearchW = 468.0;

	private const int MaxSuggestions = 4;

	private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10.0);

	private Grid _root;

	private Grid _cards;

	private Grid _picker;

	private FrameworkElement _stateView;

	private Border _locationCard;

	private TextBlock _locationDetail;

	private TextBlock _locationTitle;

	private PickRow _locationSettings;

	private TextBlock _stateTitle;

	private TextBlock _stateBody;

	private TextBlock _stateAction;

	private ContentControl _stateIcon;

	private FrameworkElement _headerActions;

	private Segment _unitC;

	private Segment _unitF;

	private Pill _cityPill;

	private bool _pickerOpen;

	private EventHandler _weatherUpdatedHandler;

	private EventHandler _clockFormatHandler;

	private ContentControl _cIcon;

	private TextBlock _cTemp;

	private TextBlock _cCond;

	private TextBlock _cHiLo;

	private TextBlock _cSun;

	private TextBlock _cStatus;

	private string _cIconKey;

	private readonly TextBlock[] _hTime = new TextBlock[6];

	private readonly TextBlock[] _hTemp = new TextBlock[6];

	private readonly ContentControl[] _hIcon = new ContentControl[6];

	private readonly string[] _hIconKey = new string[6];

	private Grid _hColumns;

	private HourlyCurve _hCurve;

	private TextBlock _hEmpty;

	private TextBlock _sHum;

	private TextBlock _sWind;

	private TextBlock _sUv;

	private readonly DayRow[] _dRows = new DayRow[5];

	private TextBlock _dEmpty;

	private TextBox _search;

	private TextBlock _searchHint;

	private FrameworkElement _searchClear;

	private StackPanel _results;

	private StackPanel _recent;

	private TextBlock _recentEmpty;

	private PickRow _hereRow;

	private readonly List<PickRow> _pickRows = new List<PickRow>();

	private int _highlight = -1;

	private DispatcherTimer _debounce;

	private int _searchGen;

	private bool _locating;

	private bool _autoOpened;

	public string Id => "weather";

	public string DisplayName => "Weather";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => true;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public WeatherTool()
	{
		IsEnabled = true;
		OrderIndex = 10;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_weatherUpdatedHandler = delegate
		{
			OnWeatherUpdated();
		};
		_clockFormatHandler = delegate
		{
			Dispatcher dispatcher = ((Application.Current != null) ? Application.Current.Dispatcher : null);
			if (dispatcher != null)
			{
				dispatcher.BeginInvoke(new Action(OnWeatherUpdated));
			}
			else
			{
				OnWeatherUpdated();
			}
		};
		WeatherService.Instance.WeatherUpdated += _weatherUpdatedHandler;
		ClockSettings.Instance.SettingsChanged += _clockFormatHandler;
	}

	public void Shutdown()
	{
		if (_debounce != null)
		{
			_debounce.Stop();
		}
		if (_weatherUpdatedHandler != null)
		{
			WeatherService.Instance.WeatherUpdated -= _weatherUpdatedHandler;
		}
		ClockSettings.Instance.SettingsChanged -= _clockFormatHandler;
		try
		{
			WeatherService.Instance.Shutdown();
		}
		catch
		{
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		WeatherService instance = WeatherService.Instance;
		WeatherData current = instance.Current;
		bool flag = current == null || current.LastUpdatedTicks == 0 || DateTime.UtcNow - new DateTime(current.LastUpdatedTicks, DateTimeKind.Utc) > StaleAfter;
		if (instance.IsConfigured && flag)
		{
			instance.Refresh();
		}
		Render();
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		if (_pickerOpen && WeatherService.Instance.IsConfigured)
		{
			ShowPicker(open: false);
		}
	}

	public bool HandleEscape()
	{
		if (_pickerOpen && WeatherService.Instance.IsConfigured)
		{
			ShowPicker(open: false);
			return true;
		}
		return false;
	}

	public UIElement GetHeaderActions()
	{
		if (_headerActions == null)
		{
			_headerActions = BuildHeaderActions();
		}
		SyncHeaderActions();
		return _headerActions;
	}

	public static string HeaderSubtitle()
	{
		WeatherData current = WeatherService.Instance.Current;
		if (current == null || !current.IsConfigured || string.IsNullOrEmpty(current.City))
		{
			return "Choose a city";
		}
		string text = current.City.Replace(" · Approximate", "");
		int num = text.IndexOf(", ", StringComparison.Ordinal);
		string text2 = ((num > 0) ? text.Substring(0, num) : text);
		if (text2.Length > 24)
		{
			text2 = text2.Substring(0, 23).TrimEnd() + "…";
		}
		if (!(text2 == text))
		{
			return text2 + "\t" + text;
		}
		return text2;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		WeatherService instance = WeatherService.Instance;
		if (!instance.IsConfigured || instance.Current == null || !instance.Current.HasData)
		{
			return null;
		}
		return new CollapsedBadgeData(instance.Temp(instance.Current.Temperature) + instance.UnitSymbol, "#F4F4F5");
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateWeatherIcon(size, stroke);
	}

	private void OnWeatherUpdated()
	{
		Render();
		if (State == ToolState.Active)
		{
			ToolRouter.Instance.UpdateHeaderSubtitle(HeaderSubtitle());
		}
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			Render();
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
		_cards = new Grid();
		_cards.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(228.0)
		});
		_cards.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		_cards.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(268.0)
		});
		_cards.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		_cards.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Place(_cards, BuildCurrent(), 0);
		Place(_cards, BuildHourly(), 2);
		Place(_cards, BuildDaily(), 4);
		grid2.Children.Add(_cards);
		_stateView = BuildStateView();
		grid2.Children.Add(_stateView);
		_picker = BuildPicker();
		_picker.Visibility = Visibility.Collapsed;
		grid2.Children.Add(_picker);
		_root = grid2;
		Render();
		return grid2;
	}

	private static void Place(Grid g, UIElement e, int col)
	{
		Grid.SetColumn(e, col);
		g.Children.Add(e);
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

	private static FrameworkElement Hairline(Thickness margin)
	{
		Rectangle rectangle = new Rectangle();
		rectangle.Height = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.Margin = margin;
		rectangle.IsHitTestVisible = false;
		rectangle.SnapsToDevicePixels = true;
		return rectangle;
	}

	private FrameworkElement BuildHeaderActions()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		Border border = new Border();
		border.Height = 28.0;
		border.CornerRadius = new CornerRadius(14.0);
		border.Background = BuddyTheme.Control;
		border.Padding = new Thickness(2.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		Border border2 = border;
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		StackPanel stackPanel4 = stackPanel3;
		_unitC = new Segment("°C", delegate
		{
			WeatherService.Instance.UseFahrenheit = false;
		});
		_unitF = new Segment("°F", delegate
		{
			WeatherService.Instance.UseFahrenheit = true;
		});
		stackPanel4.Children.Add(_unitC);
		stackPanel4.Children.Add(_unitF);
		border2.Child = stackPanel4;
		border2.ToolTip = BuddyTooltip.Create("Temperature unit");
		stackPanel2.Children.Add(border2);
		_cityPill = new Pill("pin", "Change City", delegate
		{
			ShowPicker(!_pickerOpen);
		})
		{
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		stackPanel2.Children.Add(_cityPill);
		return stackPanel2;
	}

	private void SyncHeaderActions()
	{
		if (_unitC != null)
		{
			bool useFahrenheit = WeatherService.Instance.UseFahrenheit;
			_unitC.SetActive(!useFahrenheit);
			_unitF.SetActive(useFahrenheit);
			bool flag = _pickerOpen && WeatherService.Instance.IsConfigured;
			_cityPill.SetLabel(flag ? "Cancel" : "Change City", flag ? null : "pin");
			_cityPill.Visibility = ((_pickerOpen && !WeatherService.Instance.IsConfigured) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private FrameworkElement BuildCurrent()
	{
		Grid grid = new Grid();
		for (int i = 0; i < 5; i++)
		{
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
		}
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_cIcon = new ContentControl
		{
			Width = 52.0,
			Height = 52.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			IsHitTestVisible = false,
			Focusable = false
		};
		Grid.SetRow(_cIcon, 0);
		grid.Children.Add(_cIcon);
		_cTemp = Txt("—", 28.0, BuddyTheme.Text100, AynType.Semibold);
		_cTemp.LineHeight = 28.0;
		_cTemp.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		_cTemp.TextTrimming = TextTrimming.None;
		_cTemp.Margin = new Thickness(-2.0, 8.0, 0.0, 0.0);
		Grid.SetRow(_cTemp, 1);
		grid.Children.Add(_cTemp);
		_cCond = Txt("", 13.5, BuddyTheme.Text92, FontWeights.Medium);
		_cCond.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		Grid.SetRow(_cCond, 2);
		grid.Children.Add(_cCond);
		_cHiLo = Txt("", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		_cHiLo.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		Grid.SetRow(_cHiLo, 3);
		grid.Children.Add(_cHiLo);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Hairline(new Thickness(0.0, 0.0, 0.0, 8.0)));
		_cSun = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		stackPanel.Children.Add(_cSun);
		_cStatus = Txt("", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_cStatus.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		_cStatus.Cursor = Cursors.Hand;
		_cStatus.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			WeatherService.Instance.Refresh();
		};
		stackPanel.Children.Add(_cStatus);
		Grid.SetRow(stackPanel, 6);
		grid.Children.Add(stackPanel);
		return Card(grid);
	}

	private FrameworkElement BuildHourly()
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
			Height = GridLength.Auto
		});
		grid.Children.Add(BuddyTheme.LabelText("Hourly"));
		_hColumns = new Grid
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		for (int i = 0; i < 6; i++)
		{
			_hColumns.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			StackPanel stackPanel = new StackPanel();
			stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
			StackPanel stackPanel2 = stackPanel;
			_hTime[i] = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
			_hTime[i].HorizontalAlignment = HorizontalAlignment.Center;
			stackPanel2.Children.Add(_hTime[i]);
			_hIcon[i] = new ContentControl
			{
				Width = 18.0,
				Height = 18.0,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
				HorizontalAlignment = HorizontalAlignment.Center,
				IsHitTestVisible = false,
				Focusable = false
			};
			stackPanel2.Children.Add(_hIcon[i]);
			_hTemp[i] = Txt("", 13.0, BuddyTheme.Text92, AynType.Medium);
			_hTemp[i].HorizontalAlignment = HorizontalAlignment.Center;
			_hTemp[i].Margin = new Thickness(2.0, 5.0, 0.0, 0.0);
			stackPanel2.Children.Add(_hTemp[i]);
			Grid.SetColumn(stackPanel2, i);
			_hColumns.Children.Add(stackPanel2);
		}
		Grid.SetRow(_hColumns, 1);
		grid.Children.Add(_hColumns);
		_hCurve = new HourlyCurve(6)
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
		};
		Grid.SetRow(_hCurve, 2);
		grid.Children.Add(_hCurve);
		_hEmpty = Txt("Hourly forecast unavailable", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_hEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_hEmpty.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetRow(_hEmpty, 1);
		Grid.SetRowSpan(_hEmpty, 2);
		grid.Children.Add(_hEmpty);
		Grid grid2 = new Grid();
		GridLength[] array = new GridLength[9]
		{
			GridLength.Auto,
			Star(),
			new GridLength(1.0),
			Star(),
			GridLength.Auto,
			Star(),
			new GridLength(1.0),
			Star(),
			GridLength.Auto
		};
		GridLength[] array2 = array;
		foreach (GridLength width in array2)
		{
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = width
			});
		}
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Children.Add(Hairline(new Thickness(0.0, 0.0, 0.0, 10.0)));
		stackPanel3.Children.Add(grid2);
		Place(grid2, Stat("humidity", "Humidity", out _sHum), 0);
		Place(grid2, VRule(), 2);
		Place(grid2, Stat("wind", "Wind", out _sWind), 4);
		Place(grid2, VRule(), 6);
		Place(grid2, Stat("uv", "UV index", out _sUv), 8);
		Grid.SetRow(stackPanel3, 3);
		grid.Children.Add(stackPanel3);
		return Card(grid);
	}

	private static GridLength Star()
	{
		return new GridLength(1.0, GridUnitType.Star);
	}

	private static FrameworkElement VRule()
	{
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Height = 26.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.VerticalAlignment = VerticalAlignment.Center;
		rectangle.IsHitTestVisible = false;
		return rectangle;
	}

	private static FrameworkElement Stat(string glyph, string label, out TextBlock value)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement frameworkElement = WeatherGlyphs.Symbol(glyph, 16.0, BuddyTheme.Text50);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 7.0, 0.0);
		stackPanel2.Children.Add(frameworkElement);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		BuddyLabel buddyLabel = BuddyTheme.LabelText(label);
		buddyLabel.FontSize = 10.0;
		stackPanel4.Children.Add(buddyLabel);
		value = Txt("—", 13.0, BuddyTheme.Text92, AynType.Medium);
		value.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel4.Children.Add(value);
		stackPanel2.Children.Add(stackPanel4);
		return stackPanel2;
	}

	private FrameworkElement BuildDaily()
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
		grid.Children.Add(BuddyTheme.LabelText("5-Day"));
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		Grid grid3 = grid2;
		for (int i = 0; i < 5; i++)
		{
			grid3.RowDefinitions.Add(new RowDefinition
			{
				Height = Star()
			});
			_dRows[i] = new DayRow(i > 0);
			Grid.SetRow(_dRows[i], i);
			grid3.Children.Add(_dRows[i]);
		}
		Grid.SetRow(grid3, 1);
		grid.Children.Add(grid3);
		_dEmpty = Txt("Forecast unavailable", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_dEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_dEmpty.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetRow(_dEmpty, 1);
		grid.Children.Add(_dEmpty);
		return Card(grid);
	}

	private FrameworkElement BuildStateView()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		_stateIcon = new ContentControl
		{
			Width = 40.0,
			Height = 40.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			IsHitTestVisible = false,
			Focusable = false
		};
		stackPanel2.Children.Add(_stateIcon);
		_stateTitle = Txt("", 13.5, BuddyTheme.Text92, AynType.Medium);
		_stateTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_stateTitle.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		stackPanel2.Children.Add(_stateTitle);
		_stateBody = Txt("", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		_stateBody.HorizontalAlignment = HorizontalAlignment.Center;
		_stateBody.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel2.Children.Add(_stateBody);
		_stateAction = Txt("Try again", 13.0, BuddyTheme.Text100, AynType.Medium);
		_stateAction.HorizontalAlignment = HorizontalAlignment.Center;
		_stateAction.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		_stateAction.Cursor = Cursors.Hand;
		_stateAction.MouseEnter += delegate
		{
			_stateAction.TextDecorations = TextDecorations.Underline;
		};
		_stateAction.MouseLeave += delegate
		{
			_stateAction.TextDecorations = null;
		};
		_stateAction.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			WeatherService.Instance.Refresh();
		};
		stackPanel2.Children.Add(_stateAction);
		return Card(stackPanel2);
	}

	private void Render()
	{
		SyncHeaderActions();
		if (_root == null)
		{
			return;
		}
		WeatherService instance = WeatherService.Instance;
		WeatherData current = instance.Current;
		if ((!instance.IsConfigured || instance.LocationNeedsAttention) && !_pickerOpen)
		{
			_autoOpened = true;
			ShowPicker(open: true);
		}
		if (_pickerOpen)
		{
			RenderPicker();
			return;
		}
		bool flag = current?.HasData ?? false;
		_cards.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_stateView.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		if (!flag)
		{
			RenderState(instance);
			return;
		}
		RenderCurrent(instance, current);
		RenderHourly(instance, current);
		RenderDaily(instance, current);
	}

	private void RenderState(WeatherService svc)
	{
		WeatherFetchState fetchState = svc.FetchState;
		bool flag = fetchState == WeatherFetchState.Loading || fetchState == WeatherFetchState.Idle;
		string text = fetchState switch
		{
			WeatherFetchState.Error => "alert", 
			WeatherFetchState.Offline => "offline", 
			_ => "cloudy", 
		};
		_stateIcon.Content = ((text == "cloudy") ? WeatherGlyphs.Create(3, day: true, 40.0, BuddyTheme.Text30) : WeatherGlyphs.Symbol(text, 40.0, BuddyTheme.Text30));
		_stateTitle.Text = (flag ? "Loading weather…" : ((fetchState == WeatherFetchState.Offline) ? "You're offline" : "Couldn't load weather"));
		string text2 = ((svc.Current != null) ? svc.Current.StatusMessage : null);
		_stateBody.Text = (flag ? "Fetching the forecast from Open-Meteo" : ((fetchState == WeatherFetchState.Offline) ? "The forecast will load when you're back online" : (string.IsNullOrEmpty(text2) ? "Open-Meteo didn't respond" : text2)));
		_stateAction.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
	}

	private void RenderCurrent(WeatherService svc, WeatherData d)
	{
		bool flag = svc.IsDayNow();
		string text = d.WeatherCode + (flag ? "d" : "n");
		if (text != _cIconKey)
		{
			_cIconKey = text;
			_cIcon.Content = WeatherGlyphs.Create(d.WeatherCode, flag, 52.0, BuddyTheme.Text100, 1.5);
		}
		Set(_cTemp, svc.Temp(d.Temperature) + "°");
		Set(_cCond, string.IsNullOrEmpty(d.Condition) ? WeatherService.WeatherCodeToString(d.WeatherCode) : d.Condition);
		List<DailyForecast> list = svc.UpcomingDays(1);
		_cHiLo.Inlines.Clear();
		if (list.Count > 0)
		{
			_cHiLo.Inlines.Add(new Run("H " + svc.Temp(list[0].MaxTemp) + "°   L " + svc.Temp(list[0].MinTemp) + "°")
			{
				Foreground = BuddyTheme.Text92
			});
			_cHiLo.Inlines.Add(new Run("  ·  "));
		}
		_cHiLo.Inlines.Add(new Run("Feels like " + svc.Temp(d.FeelsLike) + "°"));
		DateTime? dateTime = ((list.Count > 0) ? WeatherService.ParseLocal(list[0].Sunrise) : null);
		DateTime? dateTime2 = ((list.Count > 0) ? WeatherService.ParseLocal(list[0].Sunset) : null);
		string text2 = (dateTime.HasValue ? ("Sunrise " + WeatherService.Clock(dateTime.Value)) : "") + ((dateTime.HasValue && dateTime2.HasValue) ? "  ·  " : "") + (dateTime2.HasValue ? ("Sunset " + WeatherService.Clock(dateTime2.Value)) : "");
		Set(_cSun, text2);
		_cSun.Visibility = ((text2.Length <= 0) ? Visibility.Collapsed : Visibility.Visible);
		string text3 = (string.IsNullOrEmpty(d.LastUpdatedFormatted) ? "" : ("Updated " + d.LastUpdatedFormatted));
		_cStatus.Inlines.Clear();
		switch (svc.FetchState)
		{
		case WeatherFetchState.Loading:
			_cStatus.Inlines.Add(new Run("Updating…"));
			_cStatus.ToolTip = null;
			break;
		case WeatherFetchState.Offline:
			_cStatus.Inlines.Add(new Run("Offline")
			{
				Foreground = BuddyTheme.Text92,
				FontWeight = AynType.Medium
			});
			if (text3.Length > 0)
			{
				_cStatus.Inlines.Add(new Run("  ·  " + text3));
			}
			_cStatus.ToolTip = BuddyTooltip.Create("Showing the last saved forecast. Click to retry.");
			break;
		case WeatherFetchState.Error:
			_cStatus.Inlines.Add(new Run("Couldn't update")
			{
				Foreground = BuddyTheme.Text92,
				FontWeight = AynType.Medium
			});
			if (text3.Length > 0)
			{
				_cStatus.Inlines.Add(new Run("  ·  " + text3));
			}
			_cStatus.ToolTip = BuddyTooltip.Create("Open-Meteo didn't respond. Click to retry.");
			break;
		default:
			_cStatus.Inlines.Add(new Run(text3));
			_cStatus.ToolTip = BuddyTooltip.Create("Open-Meteo · click to refresh");
			break;
		}
	}

	private void RenderHourly(WeatherService svc, WeatherData d)
	{
		List<HourlyForecast> list = svc.UpcomingHours(6);
		bool flag = list.Count >= 2;
		_hColumns.Visibility = ((!flag) ? Visibility.Hidden : Visibility.Visible);
		_hCurve.Visibility = ((!flag) ? Visibility.Hidden : Visibility.Visible);
		_hEmpty.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		List<double> list2 = new List<double>();
		for (int i = 0; i < 6; i++)
		{
			bool flag2 = i < list.Count;
			TextBlock obj = _hTime[i];
			ContentControl obj2 = _hIcon[i];
			Visibility visibility2 = (_hTemp[i].Visibility = ((!flag2) ? Visibility.Hidden : Visibility.Visible));
			Visibility visibility4 = (obj2.Visibility = visibility2);
			obj.Visibility = visibility4;
			if (flag2)
			{
				HourlyForecast hourlyForecast = list[i];
				DateTime? dateTime = WeatherService.ParseLocal(hourlyForecast.Time);
				Set(_hTime[i], (i == 0) ? "Now" : (dateTime.HasValue ? WeatherService.HourLabel(dateTime.Value) : ""));
				_hTime[i].Foreground = ((i == 0) ? BuddyTheme.Text92 : BuddyTheme.Text50);
				int num = ((i == 0) ? d.WeatherCode : hourlyForecast.WeatherCode);
				bool flag3 = ((i == 0) ? svc.IsDayNow() : hourlyForecast.IsDay);
				string text = num + (flag3 ? "d" : "n");
				if (text != _hIconKey[i])
				{
					_hIconKey[i] = text;
					_hIcon[i].Content = WeatherGlyphs.Create(num, flag3, 18.0, BuddyTheme.Text92);
				}
				double celsius = ((i == 0) ? d.Temperature : hourlyForecast.Temp);
				Set(_hTemp[i], svc.Temp(celsius) + "°");
				list2.Add(svc.Temp(celsius));
			}
		}
		_hCurve.SetValues(list2);
		Set(_sHum, d.Humidity + "%");
		Set(_sWind, svc.Wind(d.WindSpeed));
		int num2 = (int)Math.Round(d.UvIndex);
		Set(_sUv, num2 + " " + WeatherService.UvLevel(d.UvIndex));
	}

	private void RenderDaily(WeatherService svc, WeatherData d)
	{
		List<DailyForecast> list = svc.UpcomingDays(5);
		_dEmpty.Visibility = ((list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		if (list.Count == 0)
		{
			DayRow[] dRows = _dRows;
			foreach (DayRow dayRow in dRows)
			{
				dayRow.Visibility = Visibility.Hidden;
			}
			return;
		}
		double num = double.MaxValue;
		double num2 = double.MinValue;
		foreach (DailyForecast item in list)
		{
			num = Math.Min(num, svc.Temp(item.MinTemp));
			num2 = Math.Max(num2, svc.Temp(item.MaxTemp));
		}
		for (int j = 0; j < 5; j++)
		{
			if (j >= list.Count)
			{
				_dRows[j].Visibility = Visibility.Hidden;
				continue;
			}
			DailyForecast dailyForecast = list[j];
			_dRows[j].Visibility = Visibility.Visible;
			double? now = ((j == 0 && svc.DayLabel(dailyForecast) == "Today") ? new double?(svc.Temp(d.Temperature)) : null);
			_dRows[j].Set(svc.DayLabel(dailyForecast), dailyForecast.WeatherCode, svc.Temp(dailyForecast.MinTemp), svc.Temp(dailyForecast.MaxTemp), num, num2, now, dailyForecast.Condition ?? WeatherService.WeatherCodeToString(dailyForecast.WeatherCode));
		}
	}

	private static void Set(TextBlock tb, string s)
	{
		if (tb.Text != s)
		{
			tb.Text = s;
		}
	}

	private Grid BuildPicker()
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(468.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = Star()
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(BuildSearchField());
		StackPanel stackPanel2 = new StackPanel();
		_locationTitle = Txt("Couldn't find your exact town. Search for it once.", 13.5, BuddyTheme.Text100, FontWeights.SemiBold);
		_locationTitle.TextWrapping = TextWrapping.Wrap;
		stackPanel2.Children.Add(_locationTitle);
		_locationDetail = Txt("Type your town above. Ayn remembers it.", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		_locationDetail.TextWrapping = TextWrapping.Wrap;
		_locationDetail.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel2.Children.Add(_locationDetail);
		_locationSettings = new PickRow("locate", "Open location settings", "", delegate
		{
			Process.Start(new ProcessStartInfo("ms-settings:privacy-location")
			{
				UseShellExecute = true
			});
		})
		{
			Margin = new Thickness(-10.0, 6.0, -8.0, 0.0)
		};
		stackPanel2.Children.Add(_locationSettings);
		_locationCard = new Border
		{
			Child = stackPanel2,
			Background = BuddyTheme.Row,
			BorderBrush = BuddyTheme.Hairline,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(14.0, 12.0, 14.0, 10.0),
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		stackPanel.Children.Add(_locationCard);
		_results = new StackPanel
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		_hereRow = new PickRow("locate", "Use current location", "Windows location", delegate
		{
			UseHere();
		});
		_results.Children.Add(_hereRow);
		stackPanel.Children.Add(_results);
		Place(grid, Card(new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		}), 0);
		Grid grid2 = new Grid();
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = Star()
		});
		grid2.Children.Add(BuddyTheme.LabelText("Recent"));
		_recent = new StackPanel
		{
			Margin = new Thickness(-6.0, 8.0, -6.0, 0.0)
		};
		Grid.SetRow(_recent, 1);
		grid2.Children.Add(_recent);
		_recentEmpty = Txt("Cities you pick appear here", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_recentEmpty.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		Grid.SetRow(_recentEmpty, 1);
		grid2.Children.Add(_recentEmpty);
		Place(grid, Card(grid2), 2);
		return grid;
	}

	private FrameworkElement BuildSearchField()
	{
		Border border = new Border();
		border.Height = 34.0;
		border.CornerRadius = new CornerRadius(10.0);
		border.Background = BuddyTheme.Row;
		border.BorderBrush = BuddyTheme.Hairline;
		border.BorderThickness = new Thickness(1.0);
		border.Padding = new Thickness(10.0, 0.0, 6.0, 0.0);
		Border border2 = border;
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = Star()
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		FrameworkElement frameworkElement = WeatherGlyphs.Symbol("search", 14.0, BuddyTheme.Text50);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		grid.Children.Add(frameworkElement);
		_search = new TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			FontFamily = BuddyTheme.Font,
			FontSize = 13.0,
			FontWeight = FontWeights.Medium,
			VerticalAlignment = VerticalAlignment.Center,
			VerticalContentAlignment = VerticalAlignment.Center,
			MaxLength = 80,
			FocusVisualStyle = null
		};
		BuddyTheme.ApplyTextMode(_search, 13.0);
		_search.TextChanged += delegate
		{
			OnQueryChanged();
		};
		_search.PreviewKeyDown += OnSearchKey;
		Grid.SetColumn(_search, 1);
		grid.Children.Add(_search);
		_searchHint = Txt("Search for a city", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_searchHint.VerticalAlignment = VerticalAlignment.Center;
		_searchHint.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		_searchHint.IsHitTestVisible = false;
		Grid.SetColumn(_searchHint, 1);
		grid.Children.Add(_searchHint);
		Border clear = new Border
		{
			Width = 22.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(11.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Visibility = Visibility.Collapsed,
			VerticalAlignment = VerticalAlignment.Center
		};
		clear.Child = WeatherGlyphs.Symbol("dismiss", 12.0, BuddyTheme.Text50);
		((FrameworkElement)clear.Child).HorizontalAlignment = HorizontalAlignment.Center;
		((FrameworkElement)clear.Child).VerticalAlignment = VerticalAlignment.Center;
		clear.MouseEnter += delegate
		{
			clear.Background = BuddyTheme.ControlActive;
		};
		clear.MouseLeave += delegate
		{
			clear.Background = Brushes.Transparent;
		};
		clear.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			_search.Clear();
			_search.Focus();
		};
		clear.ToolTip = BuddyTooltip.Create("Clear");
		_searchClear = clear;
		Grid.SetColumn(clear, 2);
		grid.Children.Add(clear);
		border2.Child = grid;
		border2.MouseLeftButtonDown += delegate
		{
			_search.Focus();
			Keyboard.Focus(_search);
		};
		return border2;
	}

	private void ShowPicker(bool open)
	{
		if (_root == null)
		{
			_pickerOpen = open;
		}
		else
		{
			if (!open && !WeatherService.Instance.IsConfigured)
			{
				return;
			}
			_pickerOpen = open;
			_picker.Visibility = ((!open) ? Visibility.Collapsed : Visibility.Visible);
			_cards.Visibility = (open ? Visibility.Collapsed : _cards.Visibility);
			_stateView.Visibility = (open ? Visibility.Collapsed : _stateView.Visibility);
			if (open)
			{
				_locating = false;
				_search.Text = string.Empty;
				ClearSuggestions(null);
				Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
				{
					_search.Focus();
					Keyboard.Focus(_search);
				});
			}
			else
			{
				_autoOpened = false;
				if (_debounce != null)
				{
					_debounce.Stop();
				}
				_searchGen++;
			}
			Render();
		}
	}

	private void RenderPicker()
	{
		_cards.Visibility = Visibility.Collapsed;
		_stateView.Visibility = Visibility.Collapsed;
		_picker.Visibility = Visibility.Visible;
		WeatherService svc = WeatherService.Instance;
		WeatherData current = svc.Current;
		_locationCard.Visibility = ((!svc.LocationNeedsAttention) ? Visibility.Collapsed : Visibility.Visible);
		bool locationIsCoarse = svc.LocationIsCoarse;
		_locationTitle.Text = (locationIsCoarse ? "Couldn't find your exact town. Search for it once." : "Turn on location for exact weather");
		_locationDetail.Text = (locationIsCoarse ? "Windows only knows your area roughly on this connection. Type your town above; Ayn remembers it." : "Enable Windows location, or type your town above.");
		_locationSettings.Visibility = (locationIsCoarse ? Visibility.Collapsed : Visibility.Visible);
		if (svc.IsConfigured && !svc.IsLocating && !svc.LocationNeedsAttention && (_locating || (_autoOpened && string.IsNullOrEmpty(_search.Text))))
		{
			_locating = false;
			ShowPicker(open: false);
			return;
		}
		string detail = ((!_locating) ? ((svc.FollowLocation && svc.IsConfigured && !svc.LocationNeedsAttention) ? ("In use · " + current.City) : "Windows GPS / Wi-Fi location") : ((svc.FetchState != WeatherFetchState.Error) ? "Locating…" : (string.IsNullOrEmpty(current.StatusMessage) ? "Location unavailable" : current.StatusMessage)));
		_hereRow.SetText("Use current location", detail);
		_hereRow.SetCurrent(svc.FollowLocation && svc.IsConfigured);
		_recent.Children.Clear();
		foreach (RecentCity recentCity in svc.RecentCities)
		{
			RecentCity c = recentCity;
			bool current3 = svc.IsConfigured && !svc.FollowLocation && current != null && Math.Abs(current.Latitude - c.Latitude) < 0.01 && Math.Abs(current.Longitude - c.Longitude) < 0.01;
			int num = (c.Name ?? "").IndexOf(", ", StringComparison.Ordinal);
			string name = ((num > 0) ? c.Name.Substring(0, num) : c.Name);
			string detail2 = ((num > 0) ? c.Name.Substring(num + 2) : c.Detail);
			PickRow pickRow = new PickRow(null, name, detail2, delegate
			{
				svc.SetRecentCity(c);
				ShowPicker(open: false);
			});
			pickRow.ToolTip = c.Name;
			pickRow.SetCurrent(current3);
			pickRow.EnableRemove(delegate
			{
				svc.RemoveRecentCity(c);
			});
			_recent.Children.Add(pickRow);
		}
		_recent.Visibility = Visibility.Visible;
		_recentEmpty.Visibility = ((_recent.Children.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		SyncHighlight();
	}

	private void UseHere()
	{
		WeatherService instance = WeatherService.Instance;
		_locating = true;
		instance.UseCurrentLocation();
		RenderPicker();
	}

	private void OnQueryChanged()
	{
		string text = _search.Text ?? string.Empty;
		_searchHint.Visibility = ((text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		_searchClear.Visibility = ((text.Length == 0) ? Visibility.Collapsed : Visibility.Visible);
		if (_debounce == null)
		{
			_debounce = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(250.0)
			};
			_debounce.Tick += delegate
			{
				_debounce.Stop();
				RunSearch();
			};
		}
		_debounce.Stop();
		_searchGen++;
		if (text.Trim().Length < 2)
		{
			ClearSuggestions((text.Trim().Length == 1) ? "Keep typing…" : null);
			return;
		}
		ClearSuggestions("Searching…");
		_debounce.Start();
	}

	private void RunSearch()
	{
		string q = (_search.Text ?? string.Empty).Trim();
		int gen = _searchGen;
		WeatherService.Instance.SearchCitiesAsync(q, delegate(List<GeocodedCity> cities, string err)
		{
			if (gen == _searchGen && _pickerOpen)
			{
				if (!string.IsNullOrEmpty(err))
				{
					ClearSuggestions((err == "Offline") ? "You're offline · recent cities still work" : "Search unavailable right now");
				}
				else if (cities == null || cities.Count == 0)
				{
					ClearSuggestions("No matches for “" + q + "”");
				}
				else
				{
					ClearSuggestions(null);
					for (int i = 0; i < cities.Count && i < 4; i++)
					{
						GeocodedCity c = cities[i];
						bool flag = cities.Count((GeocodedCity x) => WeatherService.Fold(x.Name) == WeatherService.Fold(c.Name) && x.Admin1 == c.Admin1) > 1;
						string text = (string.IsNullOrEmpty(c.Admin1) ? c.Country : ((string.Equals(c.CountryCode, WeatherService.HomeCountryCode(), StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(c.Country)) ? c.Admin1 : (c.Admin1 + ", " + c.Country)));
						string detail = ((flag && !string.IsNullOrEmpty(c.Admin2)) ? (text + " · " + c.Admin2) : text);
						PickRow pickRow = new PickRow(null, c.Name, detail, delegate
						{
							WeatherService.Instance.SetCity(c);
							ShowPicker(open: false);
						});
						_pickRows.Add(pickRow);
						_results.Children.Add(pickRow);
					}
					_highlight = 1;
					SyncHighlight();
				}
			}
		});
	}

	private void ClearSuggestions(string status)
	{
		while (_results.Children.Count > 1)
		{
			_results.Children.RemoveAt(_results.Children.Count - 1);
		}
		_pickRows.Clear();
		_pickRows.Add(_hereRow);
		_highlight = -1;
		if (!string.IsNullOrEmpty(status))
		{
			TextBlock textBlock = Txt(status, 13.0, BuddyTheme.Text50, FontWeights.Medium);
			textBlock.Margin = new Thickness(10.0, 10.0, 0.0, 0.0);
			_results.Children.Add(textBlock);
		}
		SyncHighlight();
	}

	private void OnSearchKey(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Down || e.Key == Key.Up)
		{
			e.Handled = true;
			int count = _pickRows.Count;
			if (count != 0)
			{
				_highlight = ((e.Key == Key.Down) ? Math.Min(count - 1, _highlight + 1) : Math.Max(0, _highlight - 1));
				SyncHighlight();
			}
		}
		else if (e.Key == Key.Return)
		{
			e.Handled = true;
			if (_highlight >= 0 && _highlight < _pickRows.Count)
			{
				_pickRows[_highlight].Invoke();
			}
		}
	}

	private void SyncHighlight()
	{
		for (int i = 0; i < _pickRows.Count; i++)
		{
			_pickRows[i].SetHighlighted(i == _highlight);
		}
	}
}
