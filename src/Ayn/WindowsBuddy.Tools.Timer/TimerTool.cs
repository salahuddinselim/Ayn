using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Timer;

public class TimerTool : IBuddyTool, IEscapeAware
{
	private enum View
	{
		Pomodoro,
		Countdown,
		Stopwatch,
		Water
	}

	private sealed class ListRow : Border
	{
		private readonly TextBlock _name;

		private readonly TextBlock _value;

		private readonly TextBlock _sub;

		private readonly Ellipse _dot;

		private bool _selected;

		public event Action Click;

		public ListRow(string icon, string name)
		{
			base.Height = 54.0;
			base.CornerRadius = new CornerRadius(14.0);
			base.Padding = new Thickness(10.0, 0.0, 10.0, 0.0);
			base.Margin = new Thickness(0.0, 2.0, 0.0, 2.0);
			base.Background = Brushes.Transparent;
			base.Focusable = true;
			base.FocusVisualStyle = null;
			base.Cursor = Cursors.Hand;
			AutomationProperties.SetName(this, name);
			Grid grid = new Grid
			{
				ColumnDefinitions = 
				{
					new ColumnDefinition
					{
						Width = new GridLength(24.0)
					},
					new ColumnDefinition()
				}
			};
			Child = grid;
			FrameworkElement frameworkElement = OutlineIcon(icon);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			grid.Children.Add(frameworkElement);
			StackPanel stackPanel = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(stackPanel, 1);
			grid.Children.Add(stackPanel);
			Grid grid2 = new Grid();
			_name = Txt(name, 12.0, ThemeTokens.Text);
			grid2.Children.Add(_name);
			stackPanel.Children.Add(grid2);
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
			};
			_dot = new Ellipse
			{
				Width = 4.0,
				Height = 4.0,
				Fill = ThemeTokens.Accent,
				Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel2.Children.Add(_dot);
			_value = Txt("", 11.0, ThemeTokens.Text2);
			stackPanel2.Children.Add(_value);
			_sub = Txt("", 10.0, ThemeTokens.Text3);
			_sub.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
			_sub.MaxWidth = 72.0;
			_sub.TextTrimming = TextTrimming.CharacterEllipsis;
			stackPanel2.Children.Add(_sub);
			stackPanel.Children.Add(stackPanel2);
			MouseEventHandler value = delegate
			{
				Paint();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Paint();
			};
			base.GotKeyboardFocus += delegate
			{
				Paint();
			};
			base.LostKeyboardFocus += delegate
			{
				Paint();
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				if (this.Click != null)
				{
					this.Click();
				}
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return)
				{
					e.Handled = true;
					if (this.Click != null)
					{
						this.Click();
					}
				}
			};
		}

		public void SetSelected(bool selected)
		{
			_selected = selected;
			Paint();
		}

		private void Paint()
		{
			base.Background = (_selected ? ThemeTokens.Selection : ((base.IsMouseOver || base.IsKeyboardFocused) ? ThemeTokens.Surface2 : Brushes.Transparent));
		}

		public void SetStatus(string value, bool running, string sub)
		{
			SetText(_value, value);
			_dot.Visibility = ((!running) ? Visibility.Collapsed : Visibility.Visible);
			_sub.Text = (string.IsNullOrWhiteSpace(sub) ? "" : sub);
			_sub.Visibility = (string.IsNullOrWhiteSpace(sub) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private sealed class RoundButton : Border
	{
		private readonly Action _click;

		private readonly bool _primary;

		private readonly bool _startPause;

		private readonly ContentControl _glyph;

		private string _name;

		private string _tip;

		private string _disabledTip;

		private bool _enabled = true;

		public bool Enabled
		{
			get
			{
				return _enabled;
			}
			set
			{
				_enabled = value;
				base.Opacity = (value ? 1.0 : 0.4);
				base.Cursor = (value ? Cursors.Hand : Cursors.Arrow);
				Tip();
			}
		}

		public string DisabledTip
		{
			get
			{
				return _disabledTip;
			}
			set
			{
				_disabledTip = value;
				Tip();
			}
		}

		public RoundButton(string icon, bool primary, string tip, Action click)
		{
			_click = click;
			_primary = primary;
			_tip = tip;
			_startPause = tip == "Start or pause";
			base.Width = (base.Height = (primary ? 46 : 32));
			base.CornerRadius = new CornerRadius(base.Width / 2.0);
			base.BorderThickness = new Thickness(0.0);
			base.Focusable = true;
			base.FocusVisualStyle = null;
			base.Cursor = Cursors.Hand;
			base.Background = (primary ? ThemeTokens.AccentStrong : ThemeTokens.Selection);
			_glyph = new ContentControl
			{
				IsHitTestVisible = false,
				Focusable = false,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			Child = _glyph;
			SetGlyph(icon);
			base.MouseEnter += delegate
			{
				if (_enabled)
				{
					base.Opacity = 0.85;
				}
			};
			base.MouseLeave += delegate
			{
				base.Opacity = (_enabled ? 1.0 : 0.4);
			};
			base.GotKeyboardFocus += delegate
			{
				base.Opacity = 0.75;
			};
			base.LostKeyboardFocus += delegate
			{
				base.Opacity = (_enabled ? 1.0 : 0.4);
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				if (_enabled)
				{
					_click();
				}
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (_enabled && (e.Key == Key.Space || e.Key == Key.Return))
				{
					e.Handled = true;
					_click();
				}
			};
		}

		private void Tip()
		{
			string text = ((!_enabled && !string.IsNullOrEmpty(_disabledTip)) ? _disabledTip : _tip);
			base.ToolTip = BuddyTooltip.Create(text);
			AutomationProperties.SetName(this, text);
		}

		public void SetGlyph(string name)
		{
			if (!(name == _name))
			{
				_name = name;
				if (_startPause)
				{
					_tip = ((name == "pause") ? "Pause" : "Start");
				}
				_glyph.Content = FluentIcons.Create(name, _primary ? 20 : 15, _primary ? ThemeTokens.AccentFg : ThemeTokens.Text, filled: false);
				Tip();
			}
		}
	}

	private sealed class Ring : FrameworkElement
	{
		private double _fraction = -1.0;

		public Ring(double size)
		{
			base.Width = (base.Height = size);
			base.IsHitTestVisible = false;
		}

		public void Set(double value)
		{
			value = Math.Max(0.0, Math.Min(1.0, value));
			if (!(Math.Abs(value - _fraction) < 1E-08))
			{
				_fraction = value;
				InvalidateVisual();
			}
		}

		protected override void OnRender(DrawingContext dc)
		{
			double num = base.Width / 2.0;
			double num2 = num - 4.0;
			Pen pen = new Pen(ThemeTokens.Text, 6.0);
			dc.PushOpacity(0.12);
			dc.DrawEllipse(null, pen, new Point(num, num), num2, num2);
			dc.Pop();
			Pen pen2 = new Pen(ThemeTokens.Accent, 6.0);
			pen2.StartLineCap = PenLineCap.Round;
			pen2.EndLineCap = PenLineCap.Round;
			Pen pen3 = pen2;
			if (_fraction >= 0.99999)
			{
				dc.DrawEllipse(null, pen3, new Point(num, num), num2, num2);
			}
			else if (_fraction > 0.0001)
			{
				double num3 = _fraction * 2.0 * Math.PI;
				PathFigure pathFigure = new PathFigure();
				pathFigure.StartPoint = new Point(num, num - num2);
				PathFigure pathFigure2 = pathFigure;
				pathFigure2.Segments.Add(new ArcSegment(new Point(num + num2 * Math.Sin(num3), num - num2 * Math.Cos(num3)), new Size(num2, num2), 0.0, _fraction > 0.5, SweepDirection.Clockwise, isStroked: true));
				dc.DrawGeometry(null, pen3, new PathGeometry(new PathFigure[1] { pathFigure2 }));
			}
		}
	}

	private sealed class Glass : FrameworkElement
	{
		private bool _filled;

		public Glass()
		{
			base.Width = 22.0;
			base.Height = 28.0;
			base.IsHitTestVisible = false;
		}

		public void Set(bool filled)
		{
			if (filled != _filled)
			{
				_filled = filled;
				InvalidateVisual();
			}
		}

		protected override void OnRender(DrawingContext dc)
		{
			Geometry geometry = Geometry.Parse("M3,3 L19,3 L17,25 L5,25 Z");
			if (_filled)
			{
				dc.PushClip(geometry);
				dc.DrawRectangle(ThemeTokens.Accent, null, new Rect(0.0, 9.0, 22.0, 19.0));
				dc.Pop();
			}
			dc.DrawGeometry(null, new Pen(_filled ? ThemeTokens.Accent : ThemeTokens.Text3, 1.5)
			{
				LineJoin = PenLineJoin.Round
			}, geometry);
		}
	}

	private static View? _remembered;

	private static readonly int[] Presets = new int[6] { 5, 10, 15, 25, 45, 60 };

	private Grid _root;

	private View _view;

	private readonly Dictionary<View, ListRow> _rows = new Dictionary<View, ListRow>();

	private readonly Dictionary<View, FrameworkElement> _details = new Dictionary<View, FrameworkElement>();

	private readonly Dictionary<int, BuddyChip> _presets = new Dictionary<int, BuddyChip>();

	private EventHandler _tickHandler;

	private bool _attached;

	private bool _frameHooked;

	private bool _dragging;

	private bool _editingTime;

	private double _dragAngle;

	private double _dragSeconds;

	private int _wheelRemainder;

	private int _viewGeneration;

	private bool _wheelSeconds;

	private Ring _pRing;

	private Ring _cRing;

	private Ring _wRing;

	private TextBlock _pTime;

	private TextBlock _pPhase;

	private TextBlock _pSession;

	private TextBlock _pNext;

	private TextBlock _pDone;

	private TextBlock _cTime;

	private TextBlock _cCaption;

	private TextBlock _sTime;

	private TextBlock _wCount;

	private TextBlock _wNext;

	private TextBlock _wReminderText;

	private TextBlock _wCaption;

	private Run _sMain;

	private Run _sFrac;

	private StackPanel _pDots;

	private StackPanel _sLaps;

	private StackPanel _wHistory;

	private WrapPanel _customPresets;

	private FrameworkElement _cChipRow;

	private FrameworkElement _cRunningChips;

	private FrameworkElement _cEditor;

	private BuddyStepper _pFocus;

	private BuddyStepper _pBreak;

	private BuddyStepper _pLong;

	private BuddyStepper _pCycles;

	private BuddyStepper _wInterval;

	private BuddyStepper _wGoal;

	private BuddyStepper _cHours;

	private BuddyStepper _cMinutes;

	private BuddyStepper _cSeconds;

	private BuddyToggle _pAuto;

	private BuddyToggle _wSwitch;

	private RoundButton _pReset;

	private RoundButton _pPlay;

	private RoundButton _pSkip;

	private RoundButton _cReset;

	private RoundButton _cPlay;

	private RoundButton _cAdd;

	private RoundButton _sLap;

	private RoundButton _sPlay;

	private RoundButton _sReset;

	private RoundButton _wMinus;

	private RoundButton _wPlus;

	private TextBox _countLabelBox;

	private TextBlock _labelHint;

	private readonly List<Glass> _glasses = new List<Glass>();

	private string _presetKey = "";

	private string _waterKey = "";

	private int _sLapCount = -1;

	private TextBlock _fastMark;

	private TextBlock _slowMark;

	private TimeSpan _fast = TimeSpan.MaxValue;

	private TimeSpan _slow = TimeSpan.MinValue;

	public string Id => "timer";

	public string DisplayName => "Timers";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public TimerTool()
	{
		IsEnabled = true;
		OrderIndex = 7;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (!IsInitialized)
		{
			IsInitialized = true;
			_tickHandler = delegate
			{
				RefreshAll();
			};
		}
	}

	public void Shutdown()
	{
		Detach();
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		Attach();
		RefreshAll();
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		if (_dragging)
		{
			Mouse.Capture(null);
		}
		_dragging = false;
		CommitLabel();
		Detach();
		TimerService.Instance.SaveState();
	}

	private void Attach()
	{
		if (!_attached && _tickHandler != null)
		{
			_attached = true;
			TimerService instance = TimerService.Instance;
			instance.Tick += _tickHandler;
			instance.SettingsChanged += _tickHandler;
			instance.HydrationChanged += _tickHandler;
			instance.TimerStarted += _tickHandler;
			instance.TimerPaused += _tickHandler;
			instance.TimerResumed += _tickHandler;
			instance.TimerCancelled += _tickHandler;
			instance.TimerCompleted += _tickHandler;
			if (!_frameHooked)
			{
				CompositionTarget.Rendering += OnFrame;
				_frameHooked = true;
			}
			CapsuleMotion.AnimationsPolicyChanged += MotionPolicyChanged;
			SystemParameters.StaticPropertyChanged += MotionPolicyChanged;
		}
	}

	private void Detach()
	{
		if (_attached)
		{
			_attached = false;
			TimerService instance = TimerService.Instance;
			instance.Tick -= _tickHandler;
			instance.SettingsChanged -= _tickHandler;
			instance.HydrationChanged -= _tickHandler;
			instance.TimerStarted -= _tickHandler;
			instance.TimerPaused -= _tickHandler;
			instance.TimerResumed -= _tickHandler;
			instance.TimerCancelled -= _tickHandler;
			instance.TimerCompleted -= _tickHandler;
			if (_frameHooked)
			{
				CompositionTarget.Rendering -= OnFrame;
				_frameHooked = false;
			}
			CapsuleMotion.AnimationsPolicyChanged -= MotionPolicyChanged;
			SystemParameters.StaticPropertyChanged -= MotionPolicyChanged;
		}
	}

	private void MotionPolicyChanged(object sender, EventArgs e)
	{
		if (_root == null || CapsuleMotion.AnimationsEnabled)
		{
			return;
		}
		if (!_root.Dispatcher.CheckAccess())
		{
			_root.Dispatcher.BeginInvoke((Action)delegate
			{
				MotionPolicyChanged(sender, e);
			});
			return;
		}
		_viewGeneration++;
		foreach (FrameworkElement value in _details.Values)
		{
			value.BeginAnimation(UIElement.OpacityProperty, null);
			value.Opacity = 1.0;
		}
		_cTime.RenderTransform = null;
	}

	public bool HandleEscape()
	{
		if (!_editingTime)
		{
			return false;
		}
		_editingTime = false;
		RefreshAll();
		return true;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateTimerIcon(size, stroke);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		TimerService instance = TimerService.Instance;
		if (!instance.PomoIsRunning)
		{
			if (!instance.CountIsRunning)
			{
				return null;
			}
			return new CollapsedBadgeData(Clock(instance.CountRemainingSeconds), "#FFFFFF");
		}
		return new CollapsedBadgeData(Clock(instance.PomoRemainingSeconds), "#FFFFFF");
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			RefreshAll();
			return _root;
		}
		_root = BuddyTheme.Crisp(new Grid
		{
			Width = 728.0,
			Height = 242.0,
			Background = Brushes.Transparent,
			Tag = "NoOuterScroll",
			Focusable = true,
			FocusVisualStyle = null
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(184.0)
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(24.0)
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		string[] array = new string[4] { "Pomodoro", "Countdown", "Stopwatch", "Water" };
		string[] array2 = new string[4] { "timer", "hourglass", "stopwatch", "drop" };
		for (int i = 0; i < 4; i++)
		{
			View v = (View)i;
			ListRow listRow = new ListRow(array2[i], array[i]);
			listRow.Click += delegate
			{
				Select(v, animate: true);
			};
			_rows[v] = listRow;
			stackPanel2.Children.Add(listRow);
		}
		_root.Children.Add(stackPanel2);
		Grid grid = new Grid();
		Grid.SetColumn(grid, 2);
		_root.Children.Add(grid);
		_details[View.Pomodoro] = BuildPomodoro();
		_details[View.Countdown] = BuildCountdown();
		_details[View.Stopwatch] = BuildStopwatch();
		_details[View.Water] = BuildWater();
		foreach (FrameworkElement value in _details.Values)
		{
			value.Visibility = Visibility.Collapsed;
			grid.Children.Add(value);
		}
		_root.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (!(e.OriginalSource is TextBox) && Keyboard.Modifiers == ModifierKeys.None && !e.IsRepeat)
			{
				if (e.Key == Key.Space)
				{
					e.Handled = true;
					ToggleSelected();
				}
				else if (e.Key == Key.R)
				{
					e.Handled = true;
					ResetSelected();
				}
			}
		};
		Select(_remembered ?? DefaultView(), animate: false);
		return _root;
	}

	private static View DefaultView()
	{
		TimerService instance = TimerService.Instance;
		if (!instance.CountIsRunning)
		{
			if (!instance.StopwatchIsRunning)
			{
				return View.Pomodoro;
			}
			return View.Stopwatch;
		}
		return View.Countdown;
	}

	private void Select(View v, bool animate)
	{
		CommitLabel();
		_view = v;
		_remembered = v;
		if (_dragging)
		{
			Mouse.Capture(null);
		}
		_dragging = false;
		int generation = ++_viewGeneration;
		foreach (KeyValuePair<View, ListRow> row in _rows)
		{
			row.Value.SetSelected(row.Key == v);
		}
		foreach (KeyValuePair<View, FrameworkElement> detail in _details)
		{
			detail.Value.BeginAnimation(UIElement.OpacityProperty, null);
			detail.Value.Opacity = 1.0;
			detail.Value.Visibility = Visibility.Hidden;
		}
		FrameworkElement incoming = _details[v];
		incoming.Measure(new Size(520.0, 242.0));
		incoming.Arrange(new Rect(0.0, 0.0, 520.0, 242.0));
		foreach (KeyValuePair<View, FrameworkElement> detail2 in _details)
		{
			detail2.Value.Visibility = ((detail2.Key != v) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (animate && CapsuleMotion.AnimationsEnabled)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(160.0));
			doubleAnimation.EasingFunction = CapsuleMotion.EaseOutCubic;
			doubleAnimation.FillBehavior = FillBehavior.Stop;
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			doubleAnimation2.Completed += delegate
			{
				if (generation == _viewGeneration)
				{
					incoming.BeginAnimation(UIElement.OpacityProperty, null);
					incoming.Opacity = 1.0;
				}
			};
			incoming.BeginAnimation(UIElement.OpacityProperty, doubleAnimation2);
		}
		RefreshAll();
		OnFrame(null, EventArgs.Empty);
	}

	private static Grid DetailGrid()
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(184.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		return grid;
	}

	private static StackPanel Controls(Grid g)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.Width = 310.0;
		StackPanel stackPanel2 = stackPanel;
		Grid.SetColumn(stackPanel2, 1);
		g.Children.Add(stackPanel2);
		return stackPanel2;
	}

	private static TextBlock Txt(string text, double size, Brush ink)
	{
		TextBlock textBlock = BuddyTheme.Text(text, size, ink, AynType.Medium);
		textBlock.TextTrimming = TextTrimming.None;
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		return textBlock;
	}

	private static TextBlock Center(string text, double size, Brush ink)
	{
		TextBlock textBlock = Txt(text, size, ink);
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		return textBlock;
	}

	private static Grid Hero(out Ring ring, out TextBlock value, out TextBlock caption, string name)
	{
		Grid grid = new Grid();
		grid.Width = 174.0;
		grid.Height = 174.0;
		grid.Background = Brushes.Transparent;
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		Grid grid2 = grid;
		ring = new Ring(168.0);
		ring.HorizontalAlignment = HorizontalAlignment.Center;
		ring.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(ring);
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.IsHitTestVisible = false;
		StackPanel stackPanel2 = stackPanel;
		value = Center("25:00", 34.0, ThemeTokens.Text);
		value.FontFamily = BuddyTheme.DisplayFont;
		value.FontWeight = AynType.Semibold;
		stackPanel2.Children.Add(value);
		caption = Center(name, 11.0, ThemeTokens.Text2);
		caption.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
		stackPanel2.Children.Add(caption);
		grid2.Children.Add(stackPanel2);
		return grid2;
	}

	private static StackPanel Buttons(params RoundButton[] buttons)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		foreach (RoundButton roundButton in buttons)
		{
			roundButton.VerticalAlignment = VerticalAlignment.Center;
			roundButton.Margin = new Thickness(7.0, 0.0, 7.0, 0.0);
			stackPanel2.Children.Add(roundButton);
		}
		return stackPanel2;
	}

	private static BuddyChip Chip(string name, Action action)
	{
		BuddyChip buddyChip = new BuddyChip(name);
		buddyChip.Borderless = true;
		buddyChip.Height = 26.0;
		buddyChip.Padding = new Thickness(9.0, 2.0, 9.0, 2.0);
		buddyChip.Margin = new Thickness(0.0, 0.0, 5.0, 0.0);
		BuddyChip buddyChip2 = buddyChip;
		AutomationProperties.SetName(buddyChip2, name);
		buddyChip2.Click += delegate(object s, RoutedEventArgs e)
		{
			e.Handled = true;
			action();
		};
		return buddyChip2;
	}

	private static Grid Pair(FrameworkElement a, FrameworkElement b)
	{
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		a.HorizontalAlignment = HorizontalAlignment.Left;
		b.HorizontalAlignment = HorizontalAlignment.Right;
		grid2.Children.Add(a);
		Grid.SetColumn(b, 1);
		grid2.Children.Add(b);
		return grid2;
	}

	private FrameworkElement BuildPomodoro()
	{
		Grid grid = DetailGrid();
		grid.Children.Add(Hero(out _pRing, out _pTime, out _pPhase, "Focus"));
		StackPanel stackPanel = Controls(grid);
		TimerService t = TimerService.Instance;
		_pSession = Center("Focus 1 of 4", 13.0, ThemeTokens.Text);
		stackPanel.Children.Add(_pSession);
		_pDots = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(_pDots);
		_pNext = Center("Break 5:00 next", 11.0, ThemeTokens.Text2);
		_pNext.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		stackPanel.Children.Add(_pNext);
		_pReset = new RoundButton("arrow_reset", primary: false, "Reset", delegate
		{
			t.ResetPomodoro();
		})
		{
			DisabledTip = "Nothing to reset"
		};
		_pPlay = new RoundButton("play", primary: true, "Start or pause", TogglePomodoro);
		_pSkip = new RoundButton("next", primary: false, "Skip phase", delegate
		{
			t.SkipPomodoro();
			RefreshAll();
		});
		stackPanel.Children.Add(Buttons(_pReset, _pPlay, _pSkip));
		_pFocus = new BuddyStepper("Focus", 1, 120, "m", delegate(int v)
		{
			SetPomo(v, -1, -1, -1);
		});
		_pBreak = new BuddyStepper("Break", () => 1, () => Math.Min(30, t.PomoLongBreakSeconds / 60), "m", delegate(int v)
		{
			SetPomo(-1, v, -1, -1);
		});
		_pLong = new BuddyStepper("Long", () => Math.Max(5, t.PomoBreakSeconds / 60), () => 60, "m", delegate(int v)
		{
			SetPomo(-1, -1, v, -1);
		});
		_pCycles = new BuddyStepper("Cycles", 1, 10, "", delegate(int v)
		{
			SetPomo(-1, -1, -1, v);
		});
		stackPanel.Children.Add(Pair(_pFocus, _pBreak));
		stackPanel.Children.Add(Pair(_pLong, _pCycles));
		_pAuto = new BuddyToggle();
		_pAuto.Toggled += delegate
		{
			t.SetAutoStartNext(_pAuto.IsChecked);
		};
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		stackPanel2.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel2.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		StackPanel stackPanel3 = stackPanel2;
		TextBlock textBlock = Txt("Auto-start next", 11.0, ThemeTokens.Text2);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		stackPanel3.Children.Add(textBlock);
		stackPanel3.Children.Add(_pAuto);
		stackPanel.Children.Add(stackPanel3);
		_pDone = Center("", 11.0, ThemeTokens.Text3);
		_pDone.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
		stackPanel.Children.Add(_pDone);
		return grid;
	}

	private static void SetPomo(int f, int b, int l, int n)
	{
		TimerService instance = TimerService.Instance;
		instance.SetPomodoroSettings((f > 0) ? f : (instance.PomoWorkSeconds / 60), (b > 0) ? b : (instance.PomoBreakSeconds / 60), (l > 0) ? l : (instance.PomoLongBreakSeconds / 60), (n > 0) ? n : instance.PomoTargetCycles);
	}

	private void TogglePomodoro()
	{
		TimerService instance = TimerService.Instance;
		if (!instance.PomoIsRunning)
		{
			instance.StartPomodoro();
		}
		else if (instance.PomoIsPaused)
		{
			instance.ResumePomodoro();
		}
		else
		{
			instance.PausePomodoro();
		}
		RefreshAll();
	}

	private FrameworkElement BuildCountdown()
	{
		Grid grid = DetailGrid();
		Grid hero = Hero(out _cRing, out _cTime, out _cCaption, "Countdown");
		grid.Children.Add(hero);
		hero.ToolTip = BuddyTooltip.Create("Drag the ring to set time · wheel for minutes · Shift+wheel for seconds · click digits to edit");
		hero.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			Point position = e.GetPosition(hero);
			double length = (position - new Point(87.0, 87.0)).Length;
			if (length < 58.0)
			{
				_editingTime = !_editingTime;
				RefreshAll();
				e.Handled = true;
			}
			else if (!TimerService.Instance.CountIsRunning)
			{
				BeginRingDrag(position);
				hero.CaptureMouse();
				e.Handled = true;
			}
		};
		hero.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (_dragging)
			{
				MoveRingDrag(e.GetPosition(hero));
				e.Handled = true;
			}
		};
		hero.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_dragging)
			{
				_dragging = false;
				hero.ReleaseMouseCapture();
				e.Handled = true;
			}
		};
		hero.LostMouseCapture += delegate
		{
			_dragging = false;
		};
		hero.MouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			e.Handled = true;
			WheelCountdown(e.Delta, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
		};
		StackPanel stackPanel = Controls(grid);
		Grid grid2 = new Grid();
		grid2.Height = 26.0;
		grid2.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		Grid grid3 = grid2;
		_countLabelBox = new TextBox
		{
			Text = TimerService.Instance.CountLabel,
			MaxLength = 40,
			Background = Brushes.Transparent,
			Foreground = ThemeTokens.Text,
			CaretBrush = ThemeTokens.Text,
			BorderThickness = new Thickness(0.0),
			FontFamily = BuddyTheme.Font,
			FontSize = 13.0,
			VerticalContentAlignment = VerticalAlignment.Center,
			HorizontalContentAlignment = HorizontalAlignment.Center,
			Padding = new Thickness(4.0, 0.0, 4.0, 0.0)
		};
		AutomationProperties.SetName(_countLabelBox, "Countdown label, optional");
		grid3.Children.Add(_countLabelBox);
		_labelHint = Center("Add a label · optional", 11.0, ThemeTokens.Text3);
		_labelHint.VerticalAlignment = VerticalAlignment.Center;
		_labelHint.IsHitTestVisible = false;
		grid3.Children.Add(_labelHint);
		_countLabelBox.TextChanged += delegate
		{
			_labelHint.Visibility = ((!string.IsNullOrWhiteSpace(_countLabelBox.Text)) ? Visibility.Collapsed : Visibility.Visible);
		};
		_countLabelBox.LostKeyboardFocus += delegate
		{
			CommitLabel();
		};
		_countLabelBox.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				CommitLabel();
				_root.Focus();
				e.Handled = true;
			}
		};
		stackPanel.Children.Add(grid3);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		stackPanel2.HorizontalAlignment = HorizontalAlignment.Center;
		StackPanel stackPanel3 = stackPanel2;
		int[] presets = Presets;
		foreach (int num in presets)
		{
			int i = num;
			BuddyChip buddyChip = Chip(num + "m", delegate
			{
				SetCountdownSeconds(i * 60);
			});
			buddyChip.Padding = new Thickness(8.0, 2.0, 8.0, 2.0);
			_presets[num] = buddyChip;
			stackPanel3.Children.Add(buddyChip);
		}
		_cChipRow = stackPanel3;
		stackPanel.Children.Add(stackPanel3);
		StackPanel stackPanel4 = new StackPanel();
		stackPanel4.Orientation = Orientation.Horizontal;
		stackPanel4.HorizontalAlignment = HorizontalAlignment.Center;
		StackPanel stackPanel5 = stackPanel4;
		stackPanel5.Children.Add(Chip("+1 min", delegate
		{
			AddTime(60);
		}));
		stackPanel5.Children.Add(Chip("+5 min", delegate
		{
			AddTime(300);
		}));
		_cRunningChips = stackPanel5;
		stackPanel.Children.Add(stackPanel5);
		_customPresets = new WrapPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ScrollViewer scrollViewer = new ScrollViewer();
		scrollViewer.Content = _customPresets;
		scrollViewer.MaxHeight = 56.0;
		scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		scrollViewer.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		ScrollViewer element = scrollViewer;
		stackPanel.Children.Add(element);
		_cReset = new RoundButton("arrow_reset", primary: false, "Reset", delegate
		{
			TimerService.Instance.ResetCountdown();
		})
		{
			DisabledTip = "Nothing to reset"
		};
		_cPlay = new RoundButton("play", primary: true, "Start or pause", ToggleCountdown);
		_cAdd = new RoundButton("add", primary: false, "Add one minute", delegate
		{
			AddTime(60);
		});
		stackPanel.Children.Add(Buttons(_cReset, _cPlay, _cAdd));
		_cHours = new BuddyStepper("", 0, 12, "h", delegate
		{
			ApplyTimeParts();
		});
		_cMinutes = new BuddyStepper("", 0, 59, "m", delegate
		{
			ApplyTimeParts();
		});
		_cSeconds = new BuddyStepper("", 0, 59, "s", delegate
		{
			ApplyTimeParts();
		});
		StackPanel stackPanel6 = new StackPanel();
		stackPanel6.Orientation = Orientation.Horizontal;
		stackPanel6.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel6.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		StackPanel stackPanel7 = stackPanel6;
		BuddyStepper[] array = new BuddyStepper[3] { _cHours, _cMinutes, _cSeconds };
		foreach (BuddyStepper buddyStepper in array)
		{
			buddyStepper.Width = 94.0;
			buddyStepper.Margin = new Thickness(3.0, 0.0, 3.0, 0.0);
			stackPanel7.Children.Add(buddyStepper);
		}
		_cEditor = stackPanel7;
		stackPanel.Children.Add(stackPanel7);
		TextBlock textBlock = Center("Drag ring · scroll digits · tap to edit", 11.0, ThemeTokens.Text3);
		textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock);
		return grid;
	}

	private void CommitLabel()
	{
		if (_countLabelBox != null && _countLabelBox.Text != TimerService.Instance.CountLabel)
		{
			TimerService.Instance.SetCountdownLabel(_countLabelBox.Text);
		}
	}

	private void ToggleCountdown()
	{
		CommitLabel();
		TimerService instance = TimerService.Instance;
		if (!instance.CountIsRunning)
		{
			instance.StartCountdown();
		}
		else if (instance.CountIsPaused)
		{
			instance.ResumeCountdown();
		}
		else
		{
			instance.PauseCountdown();
		}
		RefreshAll();
	}

	private void SetCountdownSeconds(int seconds)
	{
		if (!TimerService.Instance.CountIsRunning)
		{
			TimerService.Instance.SetCountdown(Math.Max(1, Math.Min(43200, seconds)));
			RefreshAll();
			TickSettle();
		}
	}

	private void ApplyTimeParts()
	{
		SetCountdownSeconds(_cHours.Value * 3600 + _cMinutes.Value * 60 + _cSeconds.Value);
	}

	private void AddTime(int seconds)
	{
		TimerService.Instance.AddCountdownSeconds(seconds);
		RefreshAll();
	}

	private void WheelCountdown(int delta, bool shift)
	{
		if (!TimerService.Instance.CountIsRunning)
		{
			if (_wheelSeconds != shift)
			{
				_wheelRemainder = 0;
				_wheelSeconds = shift;
			}
			_wheelRemainder += delta;
			int num = _wheelRemainder / 120;
			_wheelRemainder %= 120;
			if (num != 0)
			{
				SetCountdownSeconds(TimerService.Instance.CountTotalSeconds + num * (shift ? 1 : 60));
			}
		}
	}

	private static double Angle(Point p)
	{
		return Math.Atan2(p.X - 87.0, 87.0 - p.Y);
	}

	private void BeginRingDrag(Point p)
	{
		if (!TimerService.Instance.CountIsRunning)
		{
			_dragging = true;
			_dragAngle = Angle(p);
			_dragSeconds = TimerService.Instance.CountTotalSeconds;
		}
	}

	private void MoveRingDrag(Point p)
	{
		if (_dragging)
		{
			double num = Angle(p);
			double num2 = num - _dragAngle;
			if (num2 > Math.PI)
			{
				num2 -= Math.PI * 2.0;
			}
			if (num2 < -Math.PI)
			{
				num2 += Math.PI * 2.0;
			}
			_dragAngle = num;
			_dragSeconds = Math.Max(60.0, Math.Min(43200.0, _dragSeconds + num2 * 3600.0 / (Math.PI * 2.0)));
			int num3 = ((_dragSeconds > 3600.0) ? 300 : 60);
			SetCountdownSeconds((int)Math.Round(_dragSeconds / (double)num3, MidpointRounding.AwayFromZero) * num3);
		}
	}

	private void TickSettle()
	{
		if (CapsuleMotion.AnimationsEnabled)
		{
			ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
			_cTime.RenderTransformOrigin = new Point(0.5, 0.5);
			_cTime.RenderTransform = scaleTransform;
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.985, 1.0, TimeSpan.FromMilliseconds(160.0));
			doubleAnimation.EasingFunction = CapsuleMotion.EaseOutCubic;
			doubleAnimation.FillBehavior = FillBehavior.Stop;
			DoubleAnimation animation = doubleAnimation;
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}
	}

	private void RebuildPresets()
	{
		TimerService t = TimerService.Instance;
		string text = string.Join(",", t.CountCustomPresets);
		if (text == _presetKey && _customPresets.Children.Count > 0)
		{
			return;
		}
		_presetKey = text;
		_customPresets.Children.Clear();
		foreach (int countCustomPreset in t.CountCustomPresets)
		{
			int i = countCustomPreset;
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = Orientation.Horizontal;
			stackPanel.Margin = new Thickness(0.0, 0.0, 5.0, 4.0);
			StackPanel stackPanel2 = stackPanel;
			BuddyChip buddyChip = Chip(Clock(i), delegate
			{
				SetCountdownSeconds(i);
			});
			buddyChip.Margin = new Thickness(0.0);
			stackPanel2.Children.Add(buddyChip);
			BuddyChip buddyChip2 = Chip("×", delegate
			{
				t.RemoveCountdownPreset(i);
			});
			buddyChip2.ToolTip = BuddyTooltip.Create("Remove " + Clock(i) + " preset");
			AutomationProperties.SetName(buddyChip2, "Remove " + Clock(i) + " preset");
			stackPanel2.Children.Add(buddyChip2);
			_customPresets.Children.Add(stackPanel2);
		}
		BuddyChip buddyChip3 = Chip("+ Save current", delegate
		{
			t.SaveCountdownPreset(t.CountTotalSeconds);
		});
		buddyChip3.IsEnabled = t.CountCustomPresets.Count < 12;
		_customPresets.Children.Add(buddyChip3);
	}

	private FrameworkElement BuildStopwatch()
	{
		Grid grid = DetailGrid();
		_sTime = new TextBlock
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_sMain = new Run("00:00:00")
		{
			FontFamily = BuddyTheme.DisplayFont,
			FontSize = 29.0,
			FontWeight = AynType.Semibold,
			Foreground = ThemeTokens.Text
		};
		_sFrac = new Run(".00")
		{
			FontFamily = BuddyTheme.DisplayFont,
			FontSize = 20.0,
			Foreground = ThemeTokens.Text2
		};
		_sTime.Inlines.Add(_sMain);
		_sTime.Inlines.Add(_sFrac);
		Typography.SetNumeralAlignment(_sTime, FontNumeralAlignment.Tabular);
		grid.Children.Add(_sTime);
		StackPanel stackPanel = Controls(grid);
		TimerService t = TimerService.Instance;
		_sLap = new RoundButton("flag", primary: false, "Lap", delegate
		{
			t.AddStopwatchLap();
			RefreshAll();
		})
		{
			DisabledTip = "Start the stopwatch to record laps"
		};
		_sPlay = new RoundButton("play", primary: true, "Start or pause", delegate
		{
			if (t.StopwatchIsRunning)
			{
				t.PauseStopwatch();
			}
			else
			{
				t.StartStopwatch();
			}
			RefreshAll();
		});
		_sReset = new RoundButton("arrow_reset", primary: false, "Reset", delegate
		{
			t.ResetStopwatch();
			RefreshAll();
		})
		{
			DisabledTip = "Nothing to reset"
		};
		stackPanel.Children.Add(Buttons(_sLap, _sPlay, _sReset));
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(0.0, 12.0, 0.0, 5.0);
		Grid grid3 = grid2;
		grid3.Children.Add(Txt("Laps", 12.0, ThemeTokens.Text2));
		BuddyChip buddyChip = Chip("Copy laps", CopyLaps);
		buddyChip.HorizontalAlignment = HorizontalAlignment.Right;
		buddyChip.Height = 22.0;
		grid3.Children.Add(buddyChip);
		stackPanel.Children.Add(grid3);
		Grid grid4 = new Grid();
		double[] array = new double[4] { 30.0, 92.0, 100.0, 70.0 };
		string[] array2 = new string[4] { "LAP", "SPLIT", "TOTAL", "" };
		for (int i = 0; i < 4; i++)
		{
			grid4.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(array[i])
			});
			TextBlock element = Txt(array2[i], 9.0, ThemeTokens.Text3);
			Grid.SetColumn(element, i);
			grid4.Children.Add(element);
		}
		stackPanel.Children.Add(grid4);
		_sLaps = new StackPanel();
		ScrollViewer scrollViewer = new ScrollViewer();
		scrollViewer.Height = 100.0;
		scrollViewer.Content = _sLaps;
		scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		scrollViewer.CanContentScroll = false;
		ScrollViewer element2 = scrollViewer;
		stackPanel.Children.Add(element2);
		return grid;
	}

	private string LapsText()
	{
		List<TimeSpan> stopwatchLaps = TimerService.Instance.StopwatchLaps;
		StringBuilder stringBuilder = new StringBuilder("Lap\tSplit\tTotal");
		for (int num = stopwatchLaps.Count - 1; num >= 0; num--)
		{
			TimeSpan t = stopwatchLaps[num] - ((num + 1 < stopwatchLaps.Count) ? stopwatchLaps[num + 1] : TimeSpan.Zero);
			stringBuilder.Append("\n").Append(stopwatchLaps.Count - num).Append('\t')
				.Append(Stopwatch(t))
				.Append('\t')
				.Append(Stopwatch(stopwatchLaps[num]));
		}
		return stringBuilder.ToString();
	}

	private void CopyLaps()
	{
		try
		{
			System.Windows.Clipboard.SetText(LapsText());
		}
		catch (COMException)
		{
		}
	}

	private void RebuildLaps()
	{
		List<TimeSpan> stopwatchLaps = TimerService.Instance.StopwatchLaps;
		if (stopwatchLaps.Count == _sLapCount)
		{
			return;
		}
		if (stopwatchLaps.Count != _sLapCount + 1 || _sLapCount < 1)
		{
			_sLaps.Children.Clear();
			_fast = TimeSpan.MaxValue;
			_slow = TimeSpan.MinValue;
			_fastMark = (_slowMark = null);
			for (int num = stopwatchLaps.Count - 1; num >= 0; num--)
			{
				AddLapRow(stopwatchLaps.Count - num, stopwatchLaps[num] - ((num + 1 < stopwatchLaps.Count) ? stopwatchLaps[num + 1] : TimeSpan.Zero), stopwatchLaps[num]);
			}
		}
		else
		{
			AddLapRow(stopwatchLaps.Count, stopwatchLaps[0] - ((stopwatchLaps.Count > 1) ? stopwatchLaps[1] : TimeSpan.Zero), stopwatchLaps[0]);
		}
		_sLapCount = stopwatchLaps.Count;
		if (stopwatchLaps.Count == 0)
		{
			_sLaps.Children.Add(Center("Start, then tap Lap to mark a split", 11.0, ThemeTokens.Text3));
		}
	}

	private void AddLapRow(int n, TimeSpan split, TimeSpan total)
	{
		if (_sLaps.Children.Count == 1 && !(_sLaps.Children[0] is Grid))
		{
			_sLaps.Children.Clear();
		}
		Grid grid = new Grid();
		grid.Height = 25.0;
		Grid grid2 = grid;
		double[] array = new double[4] { 30.0, 92.0, 100.0, 70.0 };
		double[] array2 = array;
		foreach (double pixels in array2)
		{
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(pixels)
			});
		}
		string[] array3 = new string[4]
		{
			n.ToString(CultureInfo.InvariantCulture),
			Stopwatch(split),
			Stopwatch(total),
			""
		};
		TextBlock textBlock = null;
		for (int j = 0; j < 4; j++)
		{
			TextBlock textBlock2 = Txt(array3[j], (j == 3) ? 10 : 11, (j == 0 || j == 3) ? ThemeTokens.Text3 : ThemeTokens.Text);
			textBlock2.VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(textBlock2, j);
			grid2.Children.Add(textBlock2);
			if (j == 3)
			{
				textBlock = textBlock2;
			}
		}
		if (split <= _fast)
		{
			if (_fastMark != null)
			{
				_fastMark.Text = "";
			}
			_fast = split;
			_fastMark = textBlock;
		}
		if (split >= _slow)
		{
			if (_slowMark != null)
			{
				_slowMark.Text = "";
			}
			_slow = split;
			_slowMark = textBlock;
		}
		if (n > 1)
		{
			_fastMark.Text = "Fastest";
			_fastMark.Foreground = ThemeTokens.Accent;
			if (_slowMark != _fastMark)
			{
				_slowMark.Text = "Slowest";
			}
		}
		_sLaps.Children.Insert(0, grid2);
	}

	private FrameworkElement BuildWater()
	{
		Grid grid = DetailGrid();
		grid.Children.Add(Hero(out _wRing, out _wCount, out _wCaption, "glasses today"));
		StackPanel stackPanel = Controls(grid);
		TimerService t = TimerService.Instance;
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		stackPanel2.HorizontalAlignment = HorizontalAlignment.Center;
		StackPanel stackPanel3 = stackPanel2;
		for (int i = 0; i < 8; i++)
		{
			int count = i + 1;
			Glass glass = new Glass();
			_glasses.Add(glass);
			BuddyChip buddyChip = Chip("", delegate
			{
				SetWaterGlass(count);
			});
			buddyChip.Content = glass;
			buddyChip.Width = 32.0;
			buddyChip.Height = 38.0;
			buddyChip.Padding = new Thickness(2.0);
			buddyChip.Margin = new Thickness(2.0, 0.0, 2.0, 0.0);
			buddyChip.ToolTip = BuddyTooltip.Create("Log " + count + " glasses");
			AutomationProperties.SetName(buddyChip, "Log " + count + " glasses");
			stackPanel3.Children.Add(buddyChip);
		}
		stackPanel.Children.Add(stackPanel3);
		_wMinus = new RoundButton("subtract", primary: false, "Remove a glass", delegate
		{
			t.RemoveHydrationGlass();
		})
		{
			DisabledTip = "No glasses to remove"
		};
		_wPlus = new RoundButton("add", primary: true, "Add a glass", delegate
		{
			t.AddHydrationGlass();
		});
		stackPanel.Children.Add(Buttons(_wMinus, _wPlus));
		_wInterval = new BuddyStepper("Every", 5, 180, "m", delegate(int v)
		{
			t.SetHydration(t.HydrationEnabled, v);
		})
		{
			StepSize = 5
		};
		_wGoal = new BuddyStepper("Goal", 1, 30, "", delegate(int v)
		{
			t.SetHydrationTarget(v);
		});
		stackPanel.Children.Add(Pair(_wInterval, _wGoal));
		StackPanel stackPanel4 = new StackPanel();
		stackPanel4.Orientation = Orientation.Horizontal;
		stackPanel4.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel4.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		StackPanel stackPanel5 = stackPanel4;
		_wReminderText = Txt("Reminders", 11.0, ThemeTokens.Text2);
		_wReminderText.VerticalAlignment = VerticalAlignment.Center;
		_wReminderText.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		stackPanel5.Children.Add(_wReminderText);
		_wSwitch = new BuddyToggle();
		_wSwitch.Toggled += delegate
		{
			t.SetHydration(_wSwitch.IsChecked, t.HydrationIntervalMinutes);
		};
		stackPanel5.Children.Add(_wSwitch);
		stackPanel.Children.Add(stackPanel5);
		_wNext = Center("", 11.0, ThemeTokens.Text3);
		_wNext.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel.Children.Add(_wNext);
		_wHistory = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(_wHistory);
		return grid;
	}

	private void SetWaterGlass(int count)
	{
		TimerService instance = TimerService.Instance;
		if (instance.HydrationGlassesConsumed == count)
		{
			count--;
		}
		while (instance.HydrationGlassesConsumed < count)
		{
			instance.AddHydrationGlass();
		}
		while (instance.HydrationGlassesConsumed > count)
		{
			instance.RemoveHydrationGlass();
		}
		RefreshAll();
	}

	private void RefreshHistory()
	{
		TimerService instance = TimerService.Instance;
		string text = DateTime.Today.ToString("yyyy-MM-dd") + "/" + instance.HydrationGlassesConsumed + "/" + instance.HydrationDailyTarget + "/" + string.Join(",", instance.HydrationHistory.Select((KeyValuePair<string, int> k) => k.Key + ":" + k.Value));
		if (text == _waterKey)
		{
			return;
		}
		_waterKey = text;
		_wHistory.Children.Clear();
		for (int i = 0; i < 7; i++)
		{
			DateTime dateTime = DateTime.Today.AddDays(i - 6);
			int value = 0;
			if (i == 6)
			{
				value = instance.HydrationGlassesConsumed;
			}
			else
			{
				instance.HydrationHistory.TryGetValue(dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out value);
			}
			StackPanel stackPanel = new StackPanel();
			stackPanel.Width = 30.0;
			stackPanel.Margin = new Thickness(4.0, 0.0, 4.0, 0.0);
			stackPanel.ToolTip = BuddyTooltip.Create(dateTime.ToString("ddd, d MMM") + " · " + value + " glasses");
			StackPanel stackPanel2 = stackPanel;
			Grid grid = new Grid();
			grid.Height = 24.0;
			grid.Width = 18.0;
			Grid grid2 = grid;
			grid2.Children.Add(new Border
			{
				Background = ThemeTokens.Selection,
				CornerRadius = new CornerRadius(3.0)
			});
			grid2.Children.Add(new Border
			{
				Height = Math.Max(2.0, 24.0 * Math.Min(1.0, (double)value / (double)Math.Max(1, instance.HydrationDailyTarget))),
				VerticalAlignment = VerticalAlignment.Bottom,
				Background = ((i == 6) ? ThemeTokens.Accent : ThemeTokens.Text3),
				CornerRadius = new CornerRadius(3.0)
			});
			stackPanel2.Children.Add(grid2);
			TextBlock textBlock = Center(dateTime.ToString("ddd").Substring(0, 1), 9.0, ThemeTokens.Text3);
			textBlock.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock);
			_wHistory.Children.Add(stackPanel2);
		}
	}

	private void ToggleSelected()
	{
		if (_view == View.Pomodoro)
		{
			TogglePomodoro();
		}
		else if (_view == View.Countdown)
		{
			ToggleCountdown();
		}
		else if (_view == View.Stopwatch)
		{
			if (TimerService.Instance.StopwatchIsRunning)
			{
				TimerService.Instance.PauseStopwatch();
			}
			else
			{
				TimerService.Instance.StartStopwatch();
			}
			RefreshAll();
		}
		else
		{
			TimerService.Instance.SetHydration(!TimerService.Instance.HydrationEnabled, TimerService.Instance.HydrationIntervalMinutes);
		}
	}

	private void ResetSelected()
	{
		TimerService instance = TimerService.Instance;
		if (_view == View.Pomodoro)
		{
			instance.ResetPomodoro();
		}
		else if (_view == View.Countdown)
		{
			instance.ResetCountdown();
		}
		else if (_view == View.Stopwatch)
		{
			instance.ResetStopwatch();
		}
		else
		{
			instance.ResetHydrationGlasses();
		}
		RefreshAll();
	}

	private void RefreshAll()
	{
		if (_root == null || _pTime == null || _wNext == null)
		{
			return;
		}
		TimerService instance = TimerService.Instance;
		bool flag = instance.PomoIsRunning && !instance.PomoIsPaused;
		bool flag2 = instance.CountIsRunning && !instance.CountIsPaused;
		_rows[View.Pomodoro].SetStatus(instance.PomoIsPaused ? "Paused" : Clock(instance.PomoRemainingSeconds), flag, Phase(instance) + " · " + instance.PomoCurrentCycle + " of " + instance.PomoTargetCycles);
		_rows[View.Countdown].SetStatus(instance.CountIsPaused ? "Paused" : Clock(instance.CountRemainingSeconds), flag2, instance.CountLabel);
		_rows[View.Stopwatch].SetStatus((!instance.StopwatchIsRunning && instance.StopwatchElapsed > TimeSpan.Zero) ? "Paused" : Stopwatch(instance.StopwatchElapsed), instance.StopwatchIsRunning, "");
		_rows[View.Water].SetStatus(instance.HydrationGlassesConsumed + " / " + instance.HydrationDailyTarget, running: false, "");
		_pPhase.Text = (instance.PomoIsPaused ? "Paused" : Phase(instance));
		_pSession.Text = Phase(instance) + " " + instance.PomoCurrentCycle + " of " + instance.PomoTargetCycles;
		_pNext.Text = (instance.PomoIsWork ? (((instance.PomoCurrentCycle >= instance.PomoTargetCycles) ? ("Long break " + Clock(instance.PomoLongBreakSeconds)) : ("Break " + Clock(instance.PomoBreakSeconds))) + " next") : ("Focus " + Clock(instance.PomoWorkSeconds) + " next"));
		while (_pDots.Children.Count < instance.PomoTargetCycles)
		{
			_pDots.Children.Add(new Ellipse
			{
				Width = 6.0,
				Height = 6.0,
				Margin = new Thickness(4.0, 0.0, 4.0, 0.0)
			});
		}
		while (_pDots.Children.Count > instance.PomoTargetCycles)
		{
			_pDots.Children.RemoveAt(_pDots.Children.Count - 1);
		}
		for (int i = 0; i < _pDots.Children.Count; i++)
		{
			((Ellipse)_pDots.Children[i]).Fill = ((i < instance.PomoCurrentCycle) ? ThemeTokens.Accent : ThemeTokens.Selection);
		}
		_pPlay.SetGlyph(flag ? "pause" : "play");
		_pReset.Enabled = instance.PomoIsRunning || !instance.PomoIsWork || instance.PomoRemainingSeconds != instance.PomoWorkSeconds;
		_pFocus.Value = instance.PomoWorkSeconds / 60;
		_pBreak.Value = instance.PomoBreakSeconds / 60;
		_pLong.Value = instance.PomoLongBreakSeconds / 60;
		_pCycles.Value = instance.PomoTargetCycles;
		BuddyStepper[] array = new BuddyStepper[4] { _pFocus, _pBreak, _pLong, _pCycles };
		foreach (BuddyStepper buddyStepper in array)
		{
			buddyStepper.IsEditable = !instance.PomoIsRunning;
		}
		_pAuto.SetCheckedSilently(instance.PomoAutoStartNext);
		_pDone.Text = instance.PomoSessionsToday + " completed today";
		_cPlay.SetGlyph(flag2 ? "pause" : "play");
		_cReset.Enabled = instance.CountIsRunning || instance.CountRemainingSeconds != instance.CountTotalSeconds;
		_cCaption.Text = (instance.CountIsPaused ? "Paused" : ((!instance.CountIsRunning && instance.CountRemainingSeconds == 0) ? "Time's up" : "Countdown"));
		_labelHint.Visibility = ((!string.IsNullOrWhiteSpace(_countLabelBox.Text)) ? Visibility.Collapsed : Visibility.Visible);
		foreach (KeyValuePair<int, BuddyChip> preset in _presets)
		{
			preset.Value.IsActive = instance.CountTotalSeconds == preset.Key * 60;
		}
		_cChipRow.Visibility = (instance.CountIsRunning ? Visibility.Collapsed : Visibility.Visible);
		_cRunningChips.Visibility = ((!instance.CountIsRunning) ? Visibility.Collapsed : Visibility.Visible);
		_cHours.Value = instance.CountTotalSeconds / 3600;
		_cMinutes.Value = instance.CountTotalSeconds / 60 % 60;
		_cSeconds.Value = instance.CountTotalSeconds % 60;
		BuddyStepper[] array2 = new BuddyStepper[3] { _cHours, _cMinutes, _cSeconds };
		foreach (BuddyStepper buddyStepper2 in array2)
		{
			buddyStepper2.IsEditable = !instance.CountIsRunning;
		}
		_cEditor.Visibility = ((!_editingTime || instance.CountIsRunning) ? Visibility.Collapsed : Visibility.Visible);
		RebuildPresets();
		_sPlay.SetGlyph(instance.StopwatchIsRunning ? "pause" : "play");
		_sLap.Enabled = instance.StopwatchIsRunning;
		_sReset.Enabled = instance.StopwatchIsRunning || instance.StopwatchElapsed > TimeSpan.Zero || instance.StopwatchLaps.Count > 0;
		RebuildLaps();
		_wCount.Text = instance.HydrationGlassesConsumed + " / " + instance.HydrationDailyTarget;
		_wCaption.Text = ((instance.HydrationGlassesConsumed >= instance.HydrationDailyTarget) ? "Daily goal met" : "glasses today");
		_wRing.Set((double)instance.HydrationGlassesConsumed / (double)Math.Max(1, instance.HydrationDailyTarget));
		for (int l = 0; l < 8; l++)
		{
			_glasses[l].Set(l < instance.HydrationGlassesConsumed);
		}
		_wMinus.Enabled = instance.HydrationGlassesConsumed > 0;
		_wInterval.Value = instance.HydrationIntervalMinutes;
		_wGoal.Value = instance.HydrationDailyTarget;
		_wSwitch.SetCheckedSilently(instance.HydrationEnabled);
		_wReminderText.Text = (instance.HydrationEnabled ? "Reminders" : "Reminders off");
		_wNext.Text = (instance.HydrationEnabled ? ("Next in " + Math.Max(1, (int)Math.Ceiling((double)instance.HydrationRemainingSeconds / 60.0)) + " min") : "");
		_wNext.Visibility = ((!instance.HydrationEnabled) ? Visibility.Collapsed : Visibility.Visible);
		RefreshHistory();
		OnFrame(null, EventArgs.Empty);
	}

	private void OnFrame(object sender, EventArgs e)
	{
		if (_root == null || _pRing == null)
		{
			return;
		}
		TimerService instance = TimerService.Instance;
		if (_view == View.Pomodoro)
		{
			_pRing.Set(instance.PomoIsRunning ? (1.0 - instance.PomoRemainingExact / (double)Math.Max(1, instance.PomoPhaseTotalSeconds)) : 0.0);
			SetText(_pTime, Clock((int)Math.Ceiling(instance.PomoRemainingExact - 0.0001)));
		}
		else if (_view == View.Countdown)
		{
			_cRing.Set(instance.CountRemainingExact / (double)Math.Max(1, instance.CountTotalSeconds));
			SetText(_cTime, Clock((int)Math.Ceiling(instance.CountRemainingExact - 0.0001)));
		}
		else if (_view == View.Stopwatch)
		{
			string text = Stopwatch(instance.StopwatchElapsed);
			int num = text.IndexOf('.');
			if (_sMain.Text != text.Substring(0, num))
			{
				_sMain.Text = text.Substring(0, num);
			}
			if (_sFrac.Text != text.Substring(num))
			{
				_sFrac.Text = text.Substring(num);
			}
		}
	}

	private static void SetText(TextBlock t, string s)
	{
		if (t.Text != s)
		{
			t.Text = s;
		}
	}

	private static string Phase(TimerService t)
	{
		if (!t.PomoIsWork)
		{
			if (!t.PomoIsLongBreak)
			{
				return "Break";
			}
			return "Long break";
		}
		return "Focus";
	}

	private static string Clock(int seconds)
	{
		seconds = Math.Max(0, seconds);
		if (seconds < 3600)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", seconds / 60, seconds % 60);
		}
		return string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}", seconds / 3600, seconds / 60 % 60, seconds % 60);
	}

	private static string Stopwatch(TimeSpan t)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}.{3:D2}", (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds / 10);
	}

	private static FrameworkElement OutlineIcon(string name)
	{
		string source = name switch
		{
			"stopwatch" => "M7,1 L13,1 M10,1 L10,4 M15,4 L17,2 M10,4 A7,7 0 1 1 9.99,4 M10,7 L10,11 L13,13", 
			"drop" => "M10,2 C10,2 4,9 4,13 C4,20 16,20 16,13 C16,9 10,2 10,2 Z", 
			"hourglass" => "M5,2 L15,2 M5,18 L15,18 M6,2 C6,6 7,8 10,10 C13,12 14,14 14,18 M14,2 C14,6 13,8 10,10 C7,12 6,14 6,18", 
			_ => "M7,1 L13,1 M10,1 L10,4 M10,4 A7,7 0 1 1 9.99,4 M10,7 L10,11", 
		};
		Path path = new Path();
		path.Data = Geometry.Parse(source);
		path.Stroke = ThemeTokens.Text2;
		path.StrokeThickness = 1.4;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.Width = 18.0;
		path.Height = 18.0;
		path.Stretch = Stretch.Uniform;
		path.IsHitTestVisible = false;
		return path;
	}
}
