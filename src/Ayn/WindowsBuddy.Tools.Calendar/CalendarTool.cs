using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.Tools.Calendar;

public class CalendarTool : IBuddyTool, IHeaderStatusLead, IEscapeAware
{
	internal sealed class DayItem
	{
		public CalOccurrence Occ;

		public DateTime Day;

		public bool Compact;

		public DayItem(CalOccurrence o, DateTime day)
		{
			Occ = o;
			Day = day;
		}
	}

	internal const double Focal = 20.0;

	internal const double Title = 13.5;

	internal const double Body = 13.0;

	internal const double Meta = 11.5;

	internal const double Label = 11.0;

	internal const int RowHeader = 0;

	internal const int RowEvent = 1;

	internal const int RowEmpty = 2;

	internal const int RowReminder = 3;

	internal const int RowCompletedHeader = 4;

	internal static readonly Brush Green = ThemeTokens.Success;

	internal static readonly Brush Blue = ThemeTokens.Info;

	internal static readonly Brush Amber = ThemeTokens.Warning;

	internal static readonly Brush Grey = ThemeTokens.IconMuted;

	internal static readonly Brush Rose = ThemeTokens.Danger;

	internal static readonly Brush RoseSoft = ThemeTokens.Danger;

	internal static readonly Brush OverdueInk = ThemeTokens.Warning;

	internal static readonly Brush Scrim = ThemeTokens.Overlay;

	private bool _monthView;

	private DateTime _selected = DateTime.Today;

	private DateTime _viewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

	private bool _active;

	private DateTime _lastToday = DateTime.Today;

	private DispatcherTimer _minute;

	private EventHandler _changed;

	private bool _completedOpen;

	private readonly HashSet<string> _ticking = new HashSet<string>();

	private Grid _root;

	private Grid _agenda;

	private Grid _month;

	private StackPanel _lead;

	private StackPanel _actions;

	private Run _monthRun;

	private Run _yearRun;

	private TextBlock _headerTitle;

	private FrameworkElement _monthNav;

	private FrameworkElement _todayLink;

	private BuddySlidingSegmented _viewToggle;

	private readonly DayColumn[] _week = new DayColumn[7];

	private Grid _focal;

	private BuddyLabel _focalLabel;

	private Rectangle _focalBar;

	private TextBlock _focalTitle;

	private TextBlock _focalSub;

	private TextBlock _focalEmpty;

	private CalOccurrence _focalOcc;

	private CalList _agendaList;

	private TextBlock _remCount;

	private CalList _remList;

	private Grid _remAdd;

	private TextBlock _remAddHint;

	private TextBox _remAddBox;

	private readonly MonthCell[] _cells = new MonthCell[42];

	private Grid _monthGrid;

	private readonly TextBlock[] _weekLetters = new TextBlock[7];

	private DayPanel _dayPanel;

	private double _wheelAcc;

	private Grid _overlay;

	private Canvas _popLayer;

	private Border _popover;

	private CalendarSettingsPanel _settings;

	private BuddyUndoToast _toast;

	private CalendarEditor _editor;

	private DateTime _agendaKey = DateTime.MinValue;

	public static readonly Duration SheetDuration = new Duration(TimeSpan.FromMilliseconds(160.0));

	private static readonly Brush ScrimDark = FrozenBrush(196, 0, 0, 0);

	private static readonly Brush ScrimLight = FrozenBrush(71, 0, 0, 0);

	private static readonly DropShadowEffect SheetShadowDark = SheetShadow(0.55, 28.0);

	private static readonly DropShadowEffect SheetShadowLight = SheetShadow(0.16, 24.0);

	public string Id => "calendar";

	public string DisplayName => "Calendar";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	internal static Brush HolidayInk => ThemeTokens.Danger;

	internal static Brush HolidaySoft => ThemeTokens.DangerSoft;

	internal static Brush ObservanceInk => BuddyTint.Ink(112);

	internal static Brush ObservanceSoft => BuddyTint.Ink(18);

	internal static Brush EventInk => ThemeTokens.Accent;

	internal static Brush ReminderInk => ThemeTokens.Text2;

	internal static Brush HoverFill => BuddyTint.Ink(15);

	internal static Brush QuietFill => BuddyTint.Ink(18);

	internal static Brush QuietFillHover => BuddyTint.Ink(30);

	private CalendarService Svc => CalendarService.Instance;

	internal static bool CapsuleMotionOn => CapsuleMotion.AnimationsEnabled;

	internal Canvas PopLayer => _popLayer;

	internal Grid OverlayHost => _overlay;

	internal CalendarEditor Editor => _editor;

	internal CalendarSettingsPanel SettingsPanel => _settings;

	internal DayPanel DayDetail => _dayPanel;

	internal BuddySlidingSegmented ViewToggle => _viewToggle;

	internal MonthCell[] Cells => _cells;

	internal BuddyUndoToast Toast => _toast;

	internal static Brush ColorOf(CalColor c)
	{
		return c switch
		{
			CalColor.Blue => Blue, 
			CalColor.Amber => Amber, 
			CalColor.Grey => Grey, 
			_ => Green, 
		};
	}

	internal static Brush InkOf(CalOccurrence o)
	{
		if (o.Kind == OccKind.Holiday)
		{
			return HolidayInk;
		}
		if (o.Kind == OccKind.Observance)
		{
			return ObservanceInk;
		}
		return ColorOf(o.Color);
	}

	public CalendarTool()
	{
		IsEnabled = false;
		OrderIndex = 15;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		Svc.Start();
		_monthView = Svc.Settings.View == "month";
		_changed = delegate
		{
			if (_active)
			{
				Refresh();
			}
		};
		Svc.Changed += _changed;
		ClockSettings.Instance.SettingsChanged += _changed;
		Application current = Application.Current;
		if (current != null)
		{
			current.Exit += delegate
			{
				Svc.Stop();
			};
		}
	}

	public void Shutdown()
	{
		if (_changed != null)
		{
			Svc.Changed -= _changed;
			ClockSettings.Instance.SettingsChanged -= _changed;
		}
		_changed = null;
		StopMinute();
		Svc.Stop();
		IsInitialized = false;
	}

	public void OnActivated()
	{
		_active = true;
		if (_root == null)
		{
			return;
		}
		if (_lastToday != DateTime.Today)
		{
			if (_selected == _lastToday)
			{
				_selected = DateTime.Today;
			}
			_lastToday = DateTime.Today;
		}
		Svc.RefreshFeeds(force: false);
		Refresh();
		StartMinute();
	}

	public void OnDeactivated()
	{
		_active = false;
		StopMinute();
		CloseOverlays(animate: false);
		BuddyDropdown.Close();
	}

	public UIElement GetHeaderActions()
	{
		EnsureHeader();
		return _actions;
	}

	public UIElement GetHeaderStatusLead()
	{
		EnsureHeader();
		return _lead;
	}

	public bool HandleEscape()
	{
		if (BuddyDropdown.IsOpen)
		{
			BuddyDropdown.Close();
			return true;
		}
		if (_editor != null && _editor.HandleEscape())
		{
			return true;
		}
		if (_overlay != null && _overlay.Visibility == Visibility.Visible)
		{
			CloseOverlays();
			return true;
		}
		if (_settings != null && _settings.Visibility == Visibility.Visible)
		{
			if (_settings.HandleEscape())
			{
				return true;
			}
			CloseSettings();
			return true;
		}
		if (_remAddBox != null && _remAddBox.Visibility == Visibility.Visible)
		{
			EndReminderAdd(commit: false);
			return true;
		}
		return false;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		try
		{
			DateTime now = DateTime.Now;
			int num = Svc.OverdueCount(now);
			if (num > 0)
			{
				return new CollapsedBadgeData(num + " overdue", "#E8890C");
			}
			int num2 = Svc.TodayCount(now);
			if (num2 > 0)
			{
				return new CollapsedBadgeData("Today: " + num2, "#30A46C");
			}
		}
		catch
		{
		}
		return null;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateCalendarIcon(size, stroke);
	}

	private void StartMinute()
	{
		if (_minute == null)
		{
			_minute = new DispatcherTimer(DispatcherPriority.Background);
			_minute.Tick += delegate
			{
				_minute.Stop();
				OnMinute();
				StartMinute();
			};
		}
		DateTime now = DateTime.Now;
		_minute.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50.0);
		_minute.Start();
	}

	private void StopMinute()
	{
		if (_minute != null)
		{
			_minute.Stop();
		}
	}

	private void OnMinute()
	{
		if (!_active)
		{
			return;
		}
		if (_lastToday != DateTime.Today)
		{
			if (_selected == _lastToday)
			{
				_selected = DateTime.Today;
			}
			_lastToday = DateTime.Today;
		}
		if (_editor == null)
		{
			Refresh();
		}
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			return _root;
		}
		EnsureHeader();
		_root = BuddyTheme.Crisp(new Grid
		{
			Background = BuddyTheme.Canvas,
			Tag = "NoOuterScroll",
			ClipToBounds = true,
			Focusable = false
		});
		_agenda = BuildAgenda();
		_month = BuildMonth();
		_root.Children.Add(_agenda);
		_root.Children.Add(_month);
		_settings = new CalendarSettingsPanel(this)
		{
			Visibility = Visibility.Collapsed
		};
		_root.Children.Add(_settings);
		_overlay = new Grid
		{
			Visibility = Visibility.Collapsed
		};
		ApplyScrim(_overlay);
		ThemeTokens.Subscribe(_overlay, ApplyScrim);
		_overlay.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (e.OriginalSource == _overlay || e.OriginalSource == _popLayer)
			{
				e.Handled = true;
				if (_editor == null || !_editor.ClosePickers())
				{
					CloseOverlays();
				}
			}
		};
		_root.Children.Add(_overlay);
		_toast = new BuddyUndoToast();
		_root.Children.Add(_toast);
		_root.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && _toast.IsPending && !(Keyboard.FocusedElement is TextBox))
			{
				_toast.Undo();
				e.Handled = true;
			}
		};
		ApplyView();
		if (_active)
		{
			Refresh();
		}
		return _root;
	}

	private void EnsureHeader()
	{
		if (_lead == null)
		{
			_lead = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			_headerTitle = new TextBlock
			{
				FontFamily = BuddyTheme.DisplayFont,
				FontSize = 15.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			_monthRun = new Run
			{
				FontWeight = AynType.Semibold,
				Foreground = BuddyTheme.Text100
			};
			_yearRun = new Run
			{
				FontWeight = AynType.Regular,
				Foreground = BuddyTheme.Text50
			};
			_headerTitle.Inlines.Add(_monthRun);
			_headerTitle.Inlines.Add(new Run(" "));
			_headerTitle.Inlines.Add(_yearRun);
			BuddyTheme.ApplyTextMode(_headerTitle, 15.0);
			TextBlock textBlock = new TextBlock();
			textBlock.FontFamily = BuddyTheme.DisplayFont;
			textBlock.FontSize = 15.0;
			textBlock.FontWeight = AynType.Semibold;
			textBlock.Text = "September 2026";
			TextBlock textBlock2 = textBlock;
			textBlock2.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			_headerTitle.MinWidth = Math.Ceiling(textBlock2.DesiredSize.Width) + 2.0;
			_lead.Children.Add(_headerTitle);
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = Orientation.Horizontal;
			stackPanel.VerticalAlignment = VerticalAlignment.Center;
			stackPanel.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
			StackPanel stackPanel2 = stackPanel;
			stackPanel2.Children.Add(QuietIcon(-90.0, "Previous", delegate
			{
				Step(-1);
			}));
			stackPanel2.Children.Add(QuietIcon(90.0, "Next", delegate
			{
				Step(1);
			}));
			_monthNav = stackPanel2;
			_lead.Children.Add(stackPanel2);
			Border border = SoftPill("Today", GoToday, 24.0);
			border.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
			border.ToolTip = BuddyTooltip.Create("Go to today");
			_todayLink = border;
			_lead.Children.Add(border);
			_actions = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			_viewToggle = new BuddySlidingSegmented(new string[2] { "Agenda", "Month" }, _monthView ? 1 : 0);
			_viewToggle.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
			_viewToggle.SelectionChanged += delegate(int i)
			{
				SetView(i == 1);
			};
			_actions.Children.Add(_viewToggle);
			Border border2 = BuddyKit.CircleButton("add", 28.0, "Add event or reminder", OpenQuickAdd);
			ContentControl obj = (ContentControl)border2.Child;
			double width = (((ContentControl)border2.Child).Height = 13.0);
			obj.Width = width;
			((ContentControl)border2.Child).Content = FluentIcons.Create("add", 13.0, BuddyTheme.Text92, filled: false);
			border2.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
			_actions.Children.Add(border2);
			Border border3 = BuddyKit.CircleButton("more_horizontal", 28.0, "Calendars & holidays", ToggleSettings);
			ContentControl obj2 = (ContentControl)border3.Child;
			double width2 = (((ContentControl)border3.Child).Height = 13.0);
			obj2.Width = width2;
			((ContentControl)border3.Child).Content = FluentIcons.Create("more_horizontal", 13.0, BuddyTheme.Text92, filled: false);
			_actions.Children.Add(border3);
		}
	}

	private Grid BuildAgenda()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(52.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Grid grid2 = new Grid();
		grid2.Background = Brushes.Transparent;
		grid2.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		Grid grid3 = grid2;
		for (int i = 0; i < 7; i++)
		{
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		for (int j = 0; j < 7; j++)
		{
			DayColumn dayColumn = new DayColumn(this);
			_week[j] = dayColumn;
			Grid.SetColumn(dayColumn, j);
			grid3.Children.Add(dayColumn);
		}
		grid3.MouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			e.Handled = true;
			StepWeek((e.Delta <= 0) ? 1 : (-1));
		};
		grid.Children.Add(grid3);
		Grid grid4 = new Grid();
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(14.0)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(260.0)
		});
		Grid.SetRow(grid4, 1);
		grid.Children.Add(grid4);
		Grid grid5 = new Grid();
		grid5.Margin = new Thickness(6.0, 2.0, 4.0, 0.0);
		Grid grid6 = grid5;
		grid6.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid6.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid6.Children.Add(BuildFocal());
		_agendaList = new CalList(CreateAgendaRow, BindAgendaRow);
		Grid.SetRow(_agendaList, 1);
		grid6.Children.Add(_agendaList);
		grid4.Children.Add(grid6);
		Border element = SoftPanel(BuildReminders());
		Grid.SetColumn(element, 2);
		grid4.Children.Add(element);
		return grid;
	}

	internal static Border SoftPanel(UIElement child)
	{
		Border border = new Border();
		border.Background = BuddyTheme.CardFill;
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(14.0, 12.0, 12.0, 10.0);
		border.Child = child;
		Border border2 = border;
		ApplyPanelShadow(border2);
		ThemeTokens.Subscribe(border2, ApplyPanelShadow);
		return border2;
	}

	private static void ApplyPanelShadow(Border b)
	{
		b.Effect = (ThemeTokens.IsLight ? ThemeTokens.CardElevation : null);
	}

	private FrameworkElement BuildFocal()
	{
		_focal = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand
		};
		_focal.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_focal.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_focalLabel = BuddyTheme.LabelText("Up next");
		_focalLabel.Margin = new Thickness(0.0, 0.0, 0.0, 7.0);
		_focal.Children.Add(_focalLabel);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(3.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(12.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_focalBar = new Rectangle
		{
			Width = 3.0,
			RadiusX = 1.5,
			RadiusY = 1.5,
			Fill = Amber,
			Margin = new Thickness(0.0, 2.0, 0.0, 2.0)
		};
		grid.Children.Add(_focalBar);
		StackPanel stackPanel = new StackPanel();
		_focalTitle = BuddyTheme.Text("", 20.0, BuddyTheme.Text100, AynType.Semibold);
		_focalTitle.FontFamily = BuddyTheme.DisplayFont;
		_focalTitle.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel.Children.Add(_focalTitle);
		_focalSub = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		_focalSub.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		_focalSub.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel.Children.Add(_focalSub);
		Grid.SetColumn(stackPanel, 2);
		grid.Children.Add(stackPanel);
		Grid.SetRow(grid, 1);
		_focal.Children.Add(grid);
		_focalEmpty = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		_focalEmpty.Visibility = Visibility.Collapsed;
		Grid.SetRow(_focalEmpty, 1);
		_focal.Children.Add(_focalEmpty);
		AttachClick(_focal, delegate
		{
			if (_focalOcc != null)
			{
				OpenOccurrence(_focalOcc);
			}
		}, delegate
		{
			if (_focalOcc != null)
			{
				OccurrenceMenu(_focalOcc, _focal);
			}
		});
		_focal.MouseEnter += delegate
		{
			if (_focalOcc != null)
			{
				_focalSub.Foreground = BuddyTheme.Text92;
			}
		};
		_focal.MouseLeave += delegate
		{
			_focalSub.Foreground = BuddyTheme.Text50;
		};
		return _focal;
	}

	private FrameworkElement BuildReminders()
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
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		Grid grid3 = grid2;
		grid3.Children.Add(BuddyTheme.LabelText("Reminders"));
		_remCount = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, AynType.Medium);
		_remCount.HorizontalAlignment = HorizontalAlignment.Right;
		_remCount.VerticalAlignment = VerticalAlignment.Center;
		grid3.Children.Add(_remCount);
		grid.Children.Add(grid3);
		_remList = new CalList(CreateReminderRow, BindReminderRow);
		Grid.SetRow(_remList, 1);
		grid.Children.Add(_remList);
		_remAdd = new Grid
		{
			Height = 30.0,
			Background = Brushes.Transparent,
			Cursor = Cursors.IBeam
		};
		_remAdd.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(30.0)
		});
		_remAdd.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement frameworkElement = FluentIcons.Create("add", 16.0, BuddyTheme.Text50, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Left;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		_remAdd.Children.Add(frameworkElement);
		_remAddHint = BuddyTheme.Text("Add a reminder", 13.5, BuddyTheme.Text50, FontWeights.Normal);
		_remAddHint.VerticalAlignment = VerticalAlignment.Center;
		_remAddHint.IsHitTestVisible = false;
		Grid.SetColumn(_remAddHint, 1);
		_remAdd.Children.Add(_remAddHint);
		_remAddBox = CleanBox(13.5);
		_remAddBox.Visibility = Visibility.Collapsed;
		Grid.SetColumn(_remAddBox, 1);
		_remAdd.Children.Add(_remAddBox);
		_remAddBox.TextChanged += delegate
		{
			_remAddHint.Visibility = ((_remAddBox.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		};
		_remAddBox.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				EndReminderAdd(commit: true);
			}
			else if (e.Key == Key.Escape)
			{
				e.Handled = true;
				EndReminderAdd(commit: false);
			}
		};
		_remAddBox.LostKeyboardFocus += delegate
		{
			if (_remAddBox.Text.Trim().Length == 0)
			{
				EndReminderAdd(commit: false);
			}
		};
		_remAdd.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			BeginReminderAdd();
		};
		_remAdd.MouseEnter += delegate
		{
			if (_remAddBox.Visibility != 0)
			{
				_remAddHint.Foreground = BuddyTheme.Text92;
			}
		};
		_remAdd.MouseLeave += delegate
		{
			_remAddHint.Foreground = BuddyTheme.Text50;
		};
		Grid.SetRow(_remAdd, 2);
		grid.Children.Add(_remAdd);
		return grid;
	}

	private void BeginReminderAdd()
	{
		_remAddHint.Text = "e.g. Pay rent on 5th 9am";
		_remAddHint.Foreground = BuddyTheme.Text30;
		_remAddBox.Visibility = Visibility.Visible;
		_remAddBox.Focus();
		Keyboard.Focus(_remAddBox);
	}

	private void EndReminderAdd(bool commit)
	{
		string text = _remAddBox.Text.Trim();
		if (commit && text.Length > 0)
		{
			QuickAddResult quickAddResult = QuickAdd.Parse(text, DateTime.Now);
			UserReminder userReminder = new UserReminder();
			userReminder.Title = CapitalizeFirst(string.IsNullOrEmpty(quickAddResult.Title) ? text : quickAddResult.Title);
			userReminder.Date = Cal.D((quickAddResult.HasDate || quickAddResult.Start.HasValue) ? quickAddResult.Date : DateTime.Today);
			userReminder.Time = (quickAddResult.Start.HasValue ? Cal.T(quickAddResult.Start.Value) : null);
			userReminder.Repeat = quickAddResult.Repeat;
			UserReminder r = userReminder;
			Svc.AddReminder(r);
			_remAddBox.Text = "";
			_remAddBox.Focus();
		}
		else
		{
			_remAddBox.Text = "";
			_remAddBox.Visibility = Visibility.Collapsed;
			_remAddHint.Text = "Add a reminder";
			_remAddHint.Foreground = BuddyTheme.Text50;
			_remAddHint.Visibility = Visibility.Visible;
		}
	}

	internal static string CapitalizeFirst(string s)
	{
		s = (s ?? "").Trim();
		if (s.Length == 0)
		{
			return s;
		}
		return CultureInfo.CurrentCulture.TextInfo.ToUpper(s.Substring(0, 1)) + s.Substring(1);
	}

	private Grid BuildMonth()
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(14.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(250.0)
		});
		Grid grid2 = new Grid();
		grid2.Background = Brushes.Transparent;
		Grid grid3 = grid2;
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(18.0)
		});
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Grid grid4 = new Grid();
		for (int i = 0; i < 7; i++)
		{
			grid4.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			TextBlock textBlock = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, AynType.Medium);
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Top;
			Grid.SetColumn(textBlock, i);
			grid4.Children.Add(textBlock);
			_weekLetters[i] = textBlock;
		}
		grid3.Children.Add(grid4);
		_monthGrid = new Grid();
		for (int j = 0; j < 7; j++)
		{
			_monthGrid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		for (int k = 0; k < 6; k++)
		{
			_monthGrid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
		}
		for (int l = 0; l < 42; l++)
		{
			MonthCell monthCell = new MonthCell(this);
			Grid.SetRow(monthCell, l / 7);
			Grid.SetColumn(monthCell, l % 7);
			_monthGrid.Children.Add(monthCell);
			_cells[l] = monthCell;
		}
		Grid.SetRow(_monthGrid, 1);
		grid3.Children.Add(_monthGrid);
		grid3.MouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			e.Handled = true;
			_wheelAcc += e.Delta;
			if (Math.Abs(_wheelAcc) >= 100.0)
			{
				StepMonth((!(_wheelAcc > 0.0)) ? 1 : (-1));
				_wheelAcc = 0.0;
			}
		};
		grid.Children.Add(grid3);
		_dayPanel = new DayPanel(this);
		Grid.SetColumn(_dayPanel, 2);
		grid.Children.Add(_dayPanel);
		return grid;
	}

	private void SetView(bool month)
	{
		if (_monthView != month)
		{
			_monthView = month;
			Svc.SetView(month ? "month" : "agenda");
			if (month)
			{
				_viewMonth = new DateTime(_selected.Year, _selected.Month, 1);
			}
			ApplyView();
			Refresh();
		}
	}

	private void ApplyView()
	{
		if (_agenda != null)
		{
			_agenda.Visibility = (_monthView ? Visibility.Collapsed : Visibility.Visible);
			_month.Visibility = ((!_monthView) ? Visibility.Collapsed : Visibility.Visible);
			_viewToggle.Selected = (_monthView ? 1 : 0);
		}
	}

	internal void SelectDay(DateTime day)
	{
		_selected = day.Date;
		if (_monthView && (day.Year != _viewMonth.Year || day.Month != _viewMonth.Month))
		{
			_viewMonth = new DateTime(day.Year, day.Month, 1);
		}
		Refresh();
	}

	private void Step(int dir)
	{
		if (_monthView)
		{
			StepMonth(dir);
		}
		else
		{
			StepWeek(dir);
		}
	}

	private void StepWeek(int dir)
	{
		SelectDay(_selected.AddDays(7 * dir));
	}

	private void StepMonth(int dir)
	{
		_viewMonth = _viewMonth.AddMonths(dir);
		DateTime today = DateTime.Today;
		_selected = ((_viewMonth.Year == today.Year && _viewMonth.Month == today.Month) ? today : _viewMonth);
		Refresh();
	}

	internal void GoToday()
	{
		_selected = DateTime.Today;
		_viewMonth = new DateTime(_selected.Year, _selected.Month, 1);
		Refresh();
	}

	internal void Refresh()
	{
		if (_root != null)
		{
			DateTime today = DateTime.Today;
			DateTime dateTime = (_monthView ? _viewMonth : _selected);
			bool flag = ((!_monthView) ? (_selected == today) : (_viewMonth.Year == today.Year && _viewMonth.Month == today.Month && _selected == today));
			_todayLink.Opacity = (flag ? 0.55 : 1.0);
			_monthRun.Text = dateTime.ToString("MMMM", Cal.Inv);
			_yearRun.Text = dateTime.ToString("yyyy", Cal.Inv);
			if (_monthView)
			{
				RenderMonth();
			}
			else
			{
				RenderAgenda();
			}
			if (Svc.ExpansionError != null)
			{
				_monthRun.Text = Svc.ExpansionError;
				_yearRun.Text = "";
			}
			if (_settings != null && _settings.Visibility == Visibility.Visible)
			{
				_settings.Refresh();
			}
		}
	}

	internal static DateTime WeekStart(DateTime d)
	{
		DayOfWeek firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
		int num = (d.DayOfWeek - firstDayOfWeek + 7) % 7;
		return d.Date.AddDays(-num);
	}

	private void RenderAgenda()
	{
		DateTime now = DateTime.Now;
		DateTime date = now.Date;
		DateTime dateTime = WeekStart(_selected);
		for (int i = 0; i < 7; i++)
		{
			DateTime dateTime2 = dateTime.AddDays(i);
			_week[i].Bind(dateTime2, dateTime2 == date, dateTime2 == _selected, dateTime2 < date, Svc.Day(dateTime2), Svc.RemindersOn(dateTime2).Count > 0);
		}
		_focalOcc = ((_selected < date) ? null : Svc.UpNext(now, (_selected == date) ? now : _selected));
		if (_focalOcc != null && _selected != date && _focalOcc.Start.Date != _selected)
		{
			_focalOcc = null;
		}
		if (_focalOcc != null)
		{
			CalOccurrence focalOcc = _focalOcc;
			_focalLabel.Text = ((_selected == date) ? "Up next" : "First up");
			_focalLabel.Visibility = Visibility.Visible;
			_focalBar.Fill = InkOf(focalOcc);
			_focalTitle.Text = focalOcc.Title;
			string text = (focalOcc.IsNow(now) ? ("Now · until " + Cal.Time(focalOcc.End)) : (((focalOcc.Start.Date == date) ? "" : (Cal.DayName(focalOcc.Start.Date, date) + " · ")) + Cal.Range(focalOcc.Start, focalOcc.End) + ((focalOcc.Start.Date == date) ? ("  ·  " + Cal.Until(now, focalOcc.Start)) : "")));
			if (!string.IsNullOrEmpty(focalOcc.Location))
			{
				text = text + "  ·  " + focalOcc.Location;
			}
			_focalSub.Text = text;
			Rectangle focalBar = _focalBar;
			TextBlock focalTitle = _focalTitle;
			Visibility visibility2 = (_focalSub.Visibility = Visibility.Visible);
			Visibility visibility4 = (focalTitle.Visibility = visibility2);
			focalBar.Visibility = visibility4;
			_focalEmpty.Visibility = Visibility.Collapsed;
			_focal.Cursor = Cursors.Hand;
			_focal.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		}
		else
		{
			_focalLabel.Visibility = Visibility.Collapsed;
			Rectangle focalBar2 = _focalBar;
			TextBlock focalTitle2 = _focalTitle;
			Visibility visibility6 = (_focalSub.Visibility = Visibility.Collapsed);
			Visibility visibility8 = (focalTitle2.Visibility = visibility6);
			focalBar2.Visibility = visibility8;
			_focalEmpty.Visibility = Visibility.Collapsed;
			_focal.Cursor = Cursors.Arrow;
			_focal.Margin = new Thickness(0.0);
		}
		List<CalList.Entry> list = new List<CalList.Entry>();
		string text2 = ((_focalOcc != null) ? (_focalOcc.SourceId + _focalOcc.Key) : null);
		int num = 0;
		for (int j = 0; j < 45; j++)
		{
			if (num >= 20)
			{
				break;
			}
			DateTime dateTime3 = _selected.AddDays(j);
			List<CalOccurrence> list2 = Svc.Day(dateTime3);
			int num2 = 0;
			foreach (CalOccurrence item in list2)
			{
				if (item.SourceId + item.Key != text2)
				{
					num2++;
				}
			}
			bool flag = _focalOcc != null && _focalOcc.Covers(dateTime3) && j == 0;
			if (num2 == 0 && (j > 0 || flag))
			{
				continue;
			}
			list.Add(new CalList.Entry
			{
				Kind = 0,
				Height = ((list.Count == 0) ? 18 : 32),
				Data = SectionLabel(dateTime3, date)
			});
			if (num2 == 0)
			{
				list.Add(new CalList.Entry
				{
					Kind = 2,
					Height = 24.0,
					Data = EmptyLine(dateTime3, date)
				});
			}
			foreach (CalOccurrence item2 in list2)
			{
				if (!(item2.SourceId + item2.Key == text2))
				{
					list.Add(new CalList.Entry
					{
						Kind = 1,
						Height = 26.0,
						Data = new DayItem(item2, dateTime3)
					});
				}
			}
			num++;
		}
		if (list.Count == 0)
		{
			list.Add(new CalList.Entry
			{
				Kind = 2,
				Height = 24.0,
				Data = EmptyLine(_selected, date)
			});
		}
		_agendaList.SetEntries(list, _agendaKey != _selected);
		_agendaKey = _selected;
		RenderReminders(now);
	}

	private static string SectionLabel(DateTime d, DateTime today)
	{
		if (d == today)
		{
			return "Today";
		}
		if (d == today.AddDays(1.0))
		{
			return "Tomorrow · " + d.ToString("ddd d MMM", Cal.Inv);
		}
		if (d == today.AddDays(-1.0))
		{
			return "Yesterday · " + d.ToString("ddd d MMM", Cal.Inv);
		}
		return d.ToString((d.Year == today.Year) ? "ddd d MMM" : "ddd d MMM yyyy", Cal.Inv);
	}

	private static string EmptyLine(DateTime d, DateTime today)
	{
		if (d == today)
		{
			return "Nothing planned today";
		}
		if (d < today)
		{
			return "Nothing happened this day";
		}
		return "Nothing planned for " + ((d == today.AddDays(1.0)) ? "tomorrow" : d.ToString("ddd d MMM", Cal.Inv));
	}

	private void RenderReminders(DateTime now)
	{
		List<UserReminder> list = Svc.ActiveReminders(now);
		List<UserReminder> list2 = Svc.CompletedReminders();
		_remCount.Text = ((list.Count > 0) ? list.Count.ToString(Cal.Inv) : "");
		List<CalList.Entry> list3 = new List<CalList.Entry>();
		foreach (UserReminder item in list)
		{
			list3.Add(new CalList.Entry
			{
				Kind = 3,
				Height = 32.0,
				Data = item
			});
		}
		if (list.Count == 0)
		{
			list3.Add(new CalList.Entry
			{
				Kind = 2,
				Height = 28.0,
				Data = ((list2.Count > 0) ? "All done" : "No reminders yet")
			});
		}
		if (list2.Count > 0)
		{
			list3.Add(new CalList.Entry
			{
				Kind = 4,
				Height = 30.0,
				Data = list2.Count
			});
			if (_completedOpen)
			{
				foreach (UserReminder item2 in list2)
				{
					list3.Add(new CalList.Entry
					{
						Kind = 3,
						Height = 32.0,
						Data = item2
					});
				}
			}
		}
		_remList.SetEntries(list3, resetOffset: false);
	}

	private void RenderMonth()
	{
		DateTime now = DateTime.Now;
		RenderMonthCells();
		List<CalOccurrence> items = Svc.Day(_selected);
		_dayPanel.Render(_selected, items, Svc.RemindersOn(_selected), Svc.UpcomingAfter(_selected, 3), now);
	}

	private void RenderMonthCells()
	{
		DateTime today = DateTime.Today;
		DateTime viewMonth = _viewMonth;
		DateTime dateTime = WeekStart(viewMonth);
		int num = (int)Math.Ceiling(((viewMonth - dateTime).TotalDays + (double)DateTime.DaysInMonth(viewMonth.Year, viewMonth.Month)) / 7.0);
		for (int i = 0; i < 6; i++)
		{
			_monthGrid.RowDefinitions[i].Height = ((i < num) ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0));
		}
		for (int j = 0; j < 7; j++)
		{
			_weekLetters[j].Text = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(dateTime.AddDays(j).DayOfWeek).Substring(0, 1);
		}
		for (int k = 0; k < 42; k++)
		{
			MonthCell monthCell = _cells[k];
			if (k >= num * 7)
			{
				monthCell.Visibility = Visibility.Collapsed;
				continue;
			}
			monthCell.Visibility = Visibility.Visible;
			DateTime dateTime2 = dateTime.AddDays(k);
			monthCell.Bind(dateTime2, dateTime2.Month == viewMonth.Month, dateTime2 == today, dateTime2 == _selected, Svc.Day(dateTime2), Svc.RemindersOn(dateTime2));
		}
	}

	private FrameworkElement CreateAgendaRow(int kind)
	{
		switch (kind)
		{
		case 0:
		{
			BuddyLabel buddyLabel = BuddyTheme.LabelText("");
			buddyLabel.VerticalAlignment = VerticalAlignment.Bottom;
			buddyLabel.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
			Grid grid3 = new Grid();
			grid3.Background = Brushes.Transparent;
			Grid grid4 = grid3;
			grid4.Children.Add(buddyLabel);
			return grid4;
		}
		case 2:
		{
			TextBlock textBlock = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, FontWeights.Normal);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			Grid grid = new Grid();
			grid.Background = Brushes.Transparent;
			Grid grid2 = grid;
			grid2.Children.Add(textBlock);
			return grid2;
		}
		default:
			return new EventRow(this);
		}
	}

	private void BindAgendaRow(FrameworkElement el, CalList.Entry e)
	{
		switch (e.Kind)
		{
		case 0:
			((BuddyLabel)((Grid)el).Children[0]).Text = (string)e.Data;
			break;
		case 2:
			((TextBlock)((Grid)el).Children[0]).Text = (string)e.Data;
			break;
		default:
			((EventRow)el).Bind((DayItem)e.Data, DateTime.Now);
			break;
		}
	}

	private FrameworkElement CreateReminderRow(int kind)
	{
		return kind switch
		{
			2 => CreateAgendaRow(2), 
			4 => new CompletedHeaderRow(this), 
			_ => new ReminderRow(this), 
		};
	}

	private void BindReminderRow(FrameworkElement el, CalList.Entry e)
	{
		switch (e.Kind)
		{
		case 2:
			((TextBlock)((Grid)el).Children[0]).Text = (string)e.Data;
			break;
		case 4:
			((CompletedHeaderRow)el).Bind((int)e.Data, _completedOpen);
			break;
		default:
		{
			UserReminder userReminder = (UserReminder)e.Data;
			((ReminderRow)el).Bind(userReminder, DateTime.Now, _ticking.Contains(userReminder.Id));
			break;
		}
		}
	}

	internal void ToggleCompleted()
	{
		_completedOpen = !_completedOpen;
		RenderReminders(DateTime.Now);
	}

	internal void ClearCompleted()
	{
		List<UserReminder> removed = Svc.ClearCompleted();
		if (removed.Count != 0)
		{
			_completedOpen = false;
			_toast.Show((removed.Count == 1) ? "Cleared 1 reminder" : ("Cleared " + removed.Count + " reminders"), delegate
			{
				Svc.RestoreReminders(removed);
			}, delegate
			{
			});
		}
	}

	internal void TickReminder(UserReminder r, ReminderRow row)
	{
		if (r.Completed)
		{
			Svc.ReopenReminder(r.Id);
		}
		else if (!_ticking.Contains(r.Id))
		{
			_ticking.Add(r.Id);
			row?.PlayTick();
			CompleteAfterTick(r);
		}
	}

	internal void TickReminder(UserReminder r)
	{
		if (r.Completed)
		{
			Svc.ReopenReminder(r.Id);
		}
		else if (!_ticking.Contains(r.Id))
		{
			_ticking.Add(r.Id);
			CompleteAfterTick(r);
		}
	}

	private void CompleteAfterTick(UserReminder r)
	{
		string id = r.Id;
		string oldDate = r.Date;
		DispatcherTimer t = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds((!CapsuleMotionOn) ? 1 : 650)
		};
		t.Tick += delegate
		{
			t.Stop();
			_ticking.Remove(id);
			if (Svc.CompleteReminder(id))
			{
				UserReminder userReminder = Svc.FindReminder(id);
				string text = ((userReminder != null) ? Cal.DayName(userReminder.DueDate, DateTime.Today) : "");
				_toast.Show("Done · next " + text.ToLowerInvariant(), delegate
				{
					UserReminder userReminder2 = Svc.FindReminder(id);
					if (userReminder2 != null)
					{
						UserReminder userReminder3 = userReminder2.Clone();
						userReminder3.Date = oldDate;
						Svc.UpdateReminder(userReminder3);
					}
				}, delegate
				{
				});
			}
		};
		t.Start();
	}

	internal bool IsTicking(string id)
	{
		return _ticking.Contains(id);
	}

	internal void OpenOccurrence(CalOccurrence o)
	{
		if (o.Event != null)
		{
			OpenEditor(o.Event, null, o.Start.Date);
		}
		else
		{
			OpenDetails(o);
		}
	}

	internal void OccurrenceMenu(CalOccurrence o, FrameworkElement anchor)
	{
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		if (o.Event != null)
		{
			UserEvent ev = o.Event;
			list.Add(new BuddyMenuItem("Edit", "note_edit", delegate
			{
				OpenEditor(ev, null, o.Start.Date);
			}));
			list.Add(new BuddyMenuItem("Duplicate", "copy", delegate
			{
				Svc.DuplicateEvent(ev.Id);
			}));
			if (ev.Repeat != 0)
			{
				DateTime day = o.Start.Date;
				list.Add(new BuddyMenuItem("Skip this one", "subtract", delegate
				{
					Svc.SkipOccurrence(ev.Id, day);
					_toast.Show("Skipped " + day.ToString("ddd d MMM", Cal.Inv), delegate
					{
						Svc.UnskipOccurrence(ev.Id, day);
					}, delegate
					{
					});
				}));
			}
			list.Add(new BuddyMenuItem((ev.Repeat != 0) ? "Delete series" : "Delete", "delete", delegate
			{
				DeleteEvent(ev.Id);
			}));
		}
		else
		{
			list.Add(new BuddyMenuItem("Details", "open", delegate
			{
				OpenDetails(o);
			}));
			list.Add(new BuddyMenuItem("Copy to my calendar", "copy", delegate
			{
				Svc.CopyToLocal(o);
			}));
		}
		BuddyDropdown.Show(anchor, list);
	}

	internal void ReminderMenu(UserReminder r, FrameworkElement anchor)
	{
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		list.Add(new BuddyMenuItem("Edit", "note_edit", delegate
		{
			OpenEditor(null, r, r.DueDate);
		}));
		list.Add(new BuddyMenuItem(r.Completed ? "Mark not done" : "Mark done", "checkmark", delegate
		{
			if (r.Completed)
			{
				Svc.ReopenReminder(r.Id);
			}
			else
			{
				Svc.CompleteReminder(r.Id);
			}
		}));
		list.Add(new BuddyMenuItem("Duplicate", "copy", delegate
		{
			UserReminder userReminder = r.Clone();
			userReminder.Id = Guid.NewGuid().ToString("N");
			userReminder.Completed = false;
			Svc.AddReminder(userReminder);
		}));
		list.Add(new BuddyMenuItem("Delete", "delete", delegate
		{
			DeleteReminder(r.Id);
		}));
		List<BuddyMenuItem> items = list;
		BuddyDropdown.Show(anchor, items);
	}

	internal void DeleteEvent(string id)
	{
		UserEvent removed = Svc.DeleteEvent(id);
		if (removed != null)
		{
			CloseOverlays();
			_toast.Show("Deleted “" + Short(removed.Title) + "”", delegate
			{
				Svc.RestoreEvent(removed);
			}, delegate
			{
			});
		}
	}

	internal void DeleteReminder(string id)
	{
		UserReminder removed = Svc.DeleteReminder(id);
		if (removed != null)
		{
			CloseOverlays();
			_toast.Show("Deleted “" + Short(removed.Title) + "”", delegate
			{
				Svc.RestoreReminder(removed);
			}, delegate
			{
			});
		}
	}

	private static string Short(string s)
	{
		s = s ?? "";
		if (s.Length <= 28)
		{
			return s;
		}
		return s.Substring(0, 27) + "…";
	}

	private void ShowPopover(Border card, HorizontalAlignment h, VerticalAlignment v, Thickness margin)
	{
		BuddyDropdown.Close();
		if (_settings.Visibility == Visibility.Visible)
		{
			CloseSettings();
		}
		_overlay.BeginAnimation(UIElement.OpacityProperty, null);
		_overlay.Opacity = 1.0;
		_overlay.Children.Clear();
		card.HorizontalAlignment = h;
		card.VerticalAlignment = v;
		card.Margin = margin;
		_overlay.Children.Add(card);
		_popLayer = new Canvas
		{
			Background = null,
			ClipToBounds = false
		};
		_overlay.Children.Add(_popLayer);
		_popover = card;
		_overlay.Visibility = Visibility.Visible;
		ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
		card.RenderTransformOrigin = new Point(0.5, 0.5);
		card.RenderTransform = scaleTransform;
		if (CapsuleMotionOn)
		{
			CubicEase cubicEase = new CubicEase();
			cubicEase.EasingMode = EasingMode.EaseOut;
			CubicEase easingFunction = cubicEase;
			card.Opacity = 0.0;
			card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, SheetDuration)
			{
				EasingFunction = easingFunction
			});
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.98, 1.0, SheetDuration)
			{
				EasingFunction = easingFunction
			});
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.98, 1.0, SheetDuration)
			{
				EasingFunction = easingFunction
			});
		}
		else
		{
			card.Opacity = 1.0;
		}
	}

	private static Brush FrozenBrush(byte a, byte r, byte g, byte b)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static void ApplyScrim(Grid g)
	{
		g.Background = (ThemeTokens.IsLight ? ScrimLight : ScrimDark);
	}

	internal void CloseOverlays()
	{
		CloseOverlays(animate: true);
	}

	internal void CloseOverlays(bool animate)
	{
		if (_overlay == null)
		{
			return;
		}
		Border popover = _popover;
		_popover = null;
		_editor = null;
		if (_overlay.Visibility != 0)
		{
			return;
		}
		if (animate && CapsuleMotionOn && popover != null)
		{
			CubicEase cubicEase = new CubicEase();
			cubicEase.EasingMode = EasingMode.EaseIn;
			CubicEase easingFunction = cubicEase;
			DoubleAnimation doubleAnimation = new DoubleAnimation(popover.Opacity, 0.0, SheetDuration);
			doubleAnimation.EasingFunction = easingFunction;
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			if (popover.RenderTransform is ScaleTransform { IsFrozen: false } scaleTransform)
			{
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 0.98, SheetDuration)
				{
					EasingFunction = easingFunction
				});
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 0.98, SheetDuration)
				{
					EasingFunction = easingFunction
				});
			}
			popover.IsHitTestVisible = false;
			doubleAnimation2.Completed += delegate
			{
				if (_popover == null)
				{
					HideOverlay();
				}
			};
			popover.BeginAnimation(UIElement.OpacityProperty, doubleAnimation2);
			_overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.0, SheetDuration)
			{
				EasingFunction = easingFunction
			});
		}
		else
		{
			HideOverlay();
		}
	}

	private void HideOverlay()
	{
		_overlay.BeginAnimation(UIElement.OpacityProperty, null);
		_overlay.Opacity = 1.0;
		_overlay.Visibility = Visibility.Collapsed;
		_overlay.Children.Clear();
		_popLayer = null;
		if (_active)
		{
			Refresh();
		}
	}

	internal static Border Card(double width)
	{
		Border border = new Border();
		border.Width = width;
		border.CornerRadius = new CornerRadius(18.0);
		border.Padding = new Thickness(18.0, 16.0, 18.0, 14.0);
		Border border2 = border;
		ApplySheetLook(border2);
		ThemeTokens.Subscribe(border2, ApplySheetLook);
		return border2;
	}

	private static DropShadowEffect SheetShadow(double opacity, double blur)
	{
		DropShadowEffect dropShadowEffect = new DropShadowEffect();
		dropShadowEffect.Color = Colors.Black;
		dropShadowEffect.Direction = 270.0;
		dropShadowEffect.ShadowDepth = 6.0;
		dropShadowEffect.BlurRadius = blur;
		dropShadowEffect.Opacity = opacity;
		dropShadowEffect.RenderingBias = RenderingBias.Performance;
		DropShadowEffect dropShadowEffect2 = dropShadowEffect;
		dropShadowEffect2.Freeze();
		return dropShadowEffect2;
	}

	private static void ApplySheetLook(Border b)
	{
		bool isLight = ThemeTokens.IsLight;
		b.Background = (isLight ? ThemeTokens.Surface : ThemeTokens.Surface2);
		b.Effect = (isLight ? SheetShadowLight : SheetShadowDark);
	}

	private void OpenQuickAdd()
	{
		if (_root != null)
		{
			if (_popover != null && _popover.Tag as string == "editor")
			{
				CloseOverlays();
			}
			else
			{
				OpenEditorNew(_selected, reminder: false);
			}
		}
	}

	private static QuickAddResult ParseQuick(string text, out bool reminder)
	{
		Match match = Regex.Match(text, "^\\s*remind\\s+me\\s+(?:to\\s+)?", RegexOptions.IgnoreCase);
		reminder = match.Success;
		return QuickAdd.Parse(reminder ? text.Substring(match.Length) : text, DateTime.Now);
	}

	internal UserEvent AddQuick(string text)
	{
		bool reminder;
		QuickAddResult quickAddResult = ParseQuick(text, out reminder);
		if (reminder)
		{
			Svc.AddReminder(new UserReminder
			{
				Title = CapitalizeFirst(string.IsNullOrEmpty(quickAddResult.Title) ? "Reminder" : quickAddResult.Title),
				Date = Cal.D(quickAddResult.Date),
				Time = (quickAddResult.Start.HasValue ? Cal.T(quickAddResult.Start.Value) : null),
				Repeat = quickAddResult.Repeat
			});
			return null;
		}
		UserEvent userEvent = FromQuick(quickAddResult);
		Svc.AddEvent(userEvent);
		if (!_monthView || quickAddResult.Date.Month == _viewMonth.Month)
		{
			SelectDay(quickAddResult.Date);
		}
		return userEvent;
	}

	private UserEvent FromQuick(QuickAddResult q)
	{
		UserEvent userEvent = new UserEvent();
		userEvent.Title = CapitalizeFirst(string.IsNullOrEmpty(q.Title) ? "New event" : q.Title);
		userEvent.Date = Cal.D(q.Date);
		userEvent.EndDate = Cal.D(q.Date);
		userEvent.Repeat = q.Repeat;
		UserEvent userEvent2 = userEvent;
		if (q.Start.HasValue)
		{
			TimeSpan timeSpan = q.End ?? (q.Start.Value + TimeSpan.FromHours(1.0));
			if (timeSpan >= TimeSpan.FromDays(1.0))
			{
				timeSpan -= TimeSpan.FromDays(1.0);
				userEvent2.EndDate = Cal.D(q.Date.AddDays(1.0));
			}
			userEvent2.Start = Cal.T(q.Start.Value);
			userEvent2.End = Cal.T(timeSpan);
			userEvent2.Alert = CalAlert.AtTime;
		}
		return userEvent2;
	}

	internal void OpenEditorNew(DateTime day, bool reminder)
	{
		DateTime now = DateTime.Now;
		TimeSpan timeSpan = ((day == now.Date) ? TimeSpan.FromMinutes(Math.Min(1380.0, Math.Ceiling((now.TimeOfDay.TotalMinutes + 1.0) / 30.0) * 30.0)) : TimeSpan.FromHours(9.0));
		if (reminder)
		{
			OpenEditor(null, new UserReminder
			{
				Date = Cal.D(day),
				Time = Cal.T(timeSpan)
			}, day, isNew: true);
			return;
		}
		TimeSpan timeSpan2 = timeSpan + TimeSpan.FromHours(1.0);
		UserEvent userEvent = new UserEvent();
		userEvent.Date = Cal.D(day);
		userEvent.EndDate = Cal.D((timeSpan2 >= TimeSpan.FromDays(1.0)) ? day.AddDays(1.0) : day);
		userEvent.Start = Cal.T(timeSpan);
		userEvent.End = Cal.T((timeSpan2 >= TimeSpan.FromDays(1.0)) ? (timeSpan2 - TimeSpan.FromDays(1.0)) : timeSpan2);
		userEvent.Alert = CalAlert.AtTime;
		UserEvent ev = userEvent;
		OpenEditor(ev, null, day, isNew: true);
	}

	internal void OpenEditor(UserEvent ev, UserReminder rem, DateTime day, bool isNew = false)
	{
		if (_root != null)
		{
			Border border = Card(700.0);
			border.Tag = "editor";
			ShowPopover(border, HorizontalAlignment.Center, VerticalAlignment.Center, new Thickness(0.0));
			_editor = new CalendarEditor(this, ev?.Clone(), rem?.Clone(), isNew);
			border.Child = _editor;
			_editor.FocusTitle();
		}
	}

	internal void EditorSaved(UserEvent ev, UserReminder rem, bool isNew)
	{
		if (ev != null)
		{
			if (isNew || Svc.FindEvent(ev.Id) == null)
			{
				Svc.AddEvent(ev);
			}
			else
			{
				Svc.UpdateEvent(ev);
			}
			CloseOverlays();
			DateTime startDate = ev.StartDate;
			if (_monthView && (startDate.Month != _viewMonth.Month || startDate.Year != _viewMonth.Year))
			{
				_viewMonth = new DateTime(startDate.Year, startDate.Month, 1);
			}
			SelectDay(startDate);
		}
		else if (rem != null)
		{
			if (isNew || Svc.FindReminder(rem.Id) == null)
			{
				Svc.AddReminder(rem);
			}
			else
			{
				Svc.UpdateReminder(rem);
			}
			CloseOverlays();
			if (_monthView)
			{
				SelectDay(rem.DueDate);
			}
		}
	}

	internal void OpenDetails(CalOccurrence o)
	{
		Border border = Card(380.0);
		border.Tag = "details";
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(4.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(12.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.Children.Add(new Rectangle
		{
			Width = 4.0,
			RadiusX = 2.0,
			RadiusY = 2.0,
			Fill = InkOf(o)
		});
		StackPanel stackPanel2 = new StackPanel();
		TextBlock textBlock = BuddyTheme.Text(o.Title, 17.0, BuddyTheme.Text100, AynType.Semibold);
		textBlock.FontFamily = BuddyTheme.DisplayFont;
		textBlock.TextWrapping = TextWrapping.Wrap;
		stackPanel2.Children.Add(textBlock);
		DateTime today = DateTime.Today;
		string text;
		if (o.AllDay)
		{
			int num = Math.Max(1, (int)Math.Round((o.End.Date - o.Start.Date).TotalDays));
			text = Cal.DayName(o.Start.Date, today) + ((num > 1) ? (" – " + Cal.DayName(o.End.Date.AddDays(-1.0), today)) : "") + " · All day";
		}
		else
		{
			text = Cal.DayName(o.Start.Date, today) + " · " + Cal.Range(o.Start, o.End);
		}
		TextBlock textBlock2 = BuddyTheme.Text(text, 13.0, BuddyTheme.Text92, FontWeights.Normal);
		textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock2);
		string text2 = (o.IsHoliday ? (o.SourceName + " · " + HolidayCountries.NameOf(Svc.Settings.Country)) : ("From " + o.SourceName + " · read-only"));
		TextBlock textBlock3 = BuddyTheme.Text(text2, 11.5, BuddyTheme.Text50, FontWeights.Normal);
		textBlock3.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		textBlock3.TextWrapping = TextWrapping.Wrap;
		stackPanel2.Children.Add(textBlock3);
		if (!string.IsNullOrEmpty(o.Location))
		{
			TextBlock textBlock4 = BuddyTheme.Text(o.Location, 11.5, BuddyTheme.Text50, FontWeights.Normal);
			textBlock4.TextWrapping = TextWrapping.Wrap;
			textBlock4.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock4);
		}
		Grid.SetColumn(stackPanel2, 2);
		grid.Children.Add(stackPanel2);
		stackPanel.Children.Add(grid);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel3.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		StackPanel stackPanel4 = stackPanel3;
		Border border2 = SoftPill("Copy to my calendar", delegate
		{
			Svc.CopyToLocal(o);
			CloseOverlays();
		});
		border2.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		stackPanel4.Children.Add(border2);
		stackPanel4.Children.Add(Pill("Close", CloseOverlays));
		stackPanel.Children.Add(stackPanel4);
		border.Child = stackPanel;
		ShowPopover(border, HorizontalAlignment.Center, VerticalAlignment.Center, new Thickness(0.0));
	}

	private void ToggleSettings()
	{
		if (_root != null)
		{
			if (_settings.Visibility == Visibility.Visible)
			{
				CloseSettings();
				return;
			}
			CloseOverlays(animate: false);
			BuddyDropdown.Close();
			_settings.Refresh();
			_settings.Visibility = Visibility.Visible;
			_settings.Opacity = 0.0;
			BuddyTheme.Animate(_settings, UIElement.OpacityProperty, 1.0, BuddyTheme.EntryFade);
		}
	}

	internal void OpenSettings()
	{
		if (_settings != null && _settings.Visibility != 0)
		{
			ToggleSettings();
		}
	}

	internal void CloseSettings()
	{
		if (_settings != null)
		{
			_settings.Visibility = Visibility.Collapsed;
			_settings.Reset();
		}
	}

	internal static void AttachClick(FrameworkElement el, Action click, Action context)
	{
		bool pressed = false;
		el.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			pressed = true;
			e.Handled = true;
		};
		el.MouseLeave += delegate
		{
			pressed = false;
		};
		el.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (pressed)
			{
				pressed = false;
				e.Handled = true;
				if (click != null)
				{
					click();
				}
			}
		};
		if (context == null)
		{
			return;
		}
		el.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			context();
		};
		el.StylusSystemGesture += delegate(object s, StylusSystemGestureEventArgs e)
		{
			if (e.SystemGesture == SystemGesture.RightTap || e.SystemGesture == SystemGesture.HoldEnter)
			{
				pressed = false;
				context();
			}
		};
	}

	internal static void MakeKeyboardButton(Border b, Action click, Brush rest, Brush focused)
	{
		b.Focusable = true;
		b.FocusVisualStyle = null;
		KeyboardNavigation.SetIsTabStop(b, isTabStop: true);
		b.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				if (click != null)
				{
					click();
				}
			}
		};
		b.GotKeyboardFocus += delegate
		{
			b.Background = focused;
		};
		b.LostKeyboardFocus += delegate
		{
			b.Background = rest;
		};
	}

	internal static FrameworkElement Clickable(TextBlock t, Action click, string tip)
	{
		t.VerticalAlignment = VerticalAlignment.Center;
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Padding = new Thickness(2.0, 6.0, 2.0, 6.0);
		border.Child = t;
		Border border2 = border;
		AttachClick(border2, click, null);
		return border2;
	}

	internal static Border TextLink(string text, Action click)
	{
		TextBlock t = BuddyTheme.Text(text, 13.0, BuddyTheme.Text92, FontWeights.Medium);
		t.VerticalAlignment = VerticalAlignment.Center;
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Padding = new Thickness(2.0, 6.0, 2.0, 6.0);
		border.Child = t;
		border.VerticalAlignment = VerticalAlignment.Center;
		Border border2 = border;
		border2.MouseEnter += delegate
		{
			t.Foreground = BuddyTheme.Text100;
		};
		border2.MouseLeave += delegate
		{
			t.Foreground = BuddyTheme.Text92;
		};
		AttachClick(border2, click, null);
		return border2;
	}

	internal static Border Pill(string text, Action click)
	{
		TextBlock textBlock = BuddyTheme.Text(text, 13.0, BuddyTheme.PrimaryInk, AynType.Medium);
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Border b = new Border
		{
			Height = 30.0,
			MinWidth = 68.0,
			Padding = new Thickness(16.0, 0.0, 16.0, 0.0),
			CornerRadius = new CornerRadius(15.0),
			Background = BuddyTheme.PrimaryFill,
			Cursor = Cursors.Hand,
			Child = textBlock
		};
		b.MouseEnter += delegate
		{
			if (b.IsEnabled)
			{
				b.Opacity = 0.88;
			}
		};
		b.MouseLeave += delegate
		{
			if (b.IsEnabled)
			{
				b.Opacity = 1.0;
			}
		};
		AttachClick(b, delegate
		{
			if (b.IsEnabled && click != null)
			{
				click();
			}
		}, null);
		b.Focusable = true;
		b.FocusVisualStyle = null;
		b.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if ((e.Key == Key.Return || e.Key == Key.Space) && b.IsEnabled)
			{
				e.Handled = true;
				if (click != null)
				{
					click();
				}
			}
		};
		return b;
	}

	internal static Border SoftPill(string text, Action click, double height = 28.0)
	{
		TextBlock textBlock = BuddyTheme.Text(text, (height <= 24.0) ? 11.5 : 13.0, BuddyTheme.Text92, AynType.Medium);
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Border b = new Border
		{
			Height = height,
			Padding = new Thickness((height <= 24.0) ? 10 : 14, 0.0, (height <= 24.0) ? 10 : 14, 0.0),
			CornerRadius = new CornerRadius(height / 2.0),
			Background = QuietFill,
			Cursor = Cursors.Hand,
			Child = textBlock,
			VerticalAlignment = VerticalAlignment.Center
		};
		b.MouseEnter += delegate
		{
			b.Background = QuietFillHover;
		};
		b.MouseLeave += delegate
		{
			if (!b.IsKeyboardFocused)
			{
				b.Background = QuietFill;
			}
		};
		AttachClick(b, click, null);
		MakeKeyboardButton(b, click, QuietFill, QuietFillHover);
		return b;
	}

	internal static Border QuietIcon(double angle, string tip, Action click)
	{
		FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 12.0, BuddyTheme.Text92, filled: false);
		frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
		frameworkElement.RenderTransform = new RotateTransform(0.0 - angle);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		Border b = new Border
		{
			Width = 28.0,
			Height = 28.0,
			CornerRadius = new CornerRadius(14.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = frameworkElement,
			VerticalAlignment = VerticalAlignment.Center,
			ToolTip = BuddyTooltip.Create(tip)
		};
		b.MouseEnter += delegate
		{
			b.Background = HoverFill;
		};
		b.MouseLeave += delegate
		{
			b.Background = Brushes.Transparent;
		};
		AttachClick(b, click, null);
		MakeKeyboardButton(b, click, Brushes.Transparent, HoverFill);
		return b;
	}

	internal static FrameworkElement Chevron(double angle, string tip, Action click, double glyph)
	{
		FrameworkElement g = FluentIcons.Create("chevron_down", glyph, BuddyTheme.Text92, filled: false);
		g.RenderTransformOrigin = new Point(0.5, 0.5);
		g.RenderTransform = new RotateTransform(0.0 - angle);
		g.HorizontalAlignment = HorizontalAlignment.Center;
		g.VerticalAlignment = VerticalAlignment.Center;
		g.Opacity = 0.66;
		Border border = new Border();
		border.Width = glyph + 12.0;
		border.Height = glyph + 12.0;
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Child = g;
		border.VerticalAlignment = VerticalAlignment.Center;
		border.ToolTip = BuddyTooltip.Create(tip);
		Border border2 = border;
		border2.MouseEnter += delegate
		{
			g.Opacity = 1.0;
		};
		border2.MouseLeave += delegate
		{
			g.Opacity = 0.66;
		};
		AttachClick(border2, click, null);
		return border2;
	}

	internal static TextBox CleanBox(double size)
	{
		TextBox textBox = new TextBox();
		textBox.FontSize = size;
		textBox.FontFamily = BuddyTheme.Font;
		textBox.Foreground = BuddyTheme.Text100;
		textBox.Background = Brushes.Transparent;
		textBox.BorderThickness = new Thickness(0.0);
		textBox.CaretBrush = BuddyTheme.Text100;
		textBox.SelectionBrush = ThemeTokens.Accent;
		textBox.VerticalAlignment = VerticalAlignment.Center;
		textBox.VerticalContentAlignment = VerticalAlignment.Center;
		textBox.Padding = new Thickness(0.0);
		textBox.FocusVisualStyle = null;
		TextBox textBox2 = textBox;
		ControlTemplate controlTemplate = new ControlTemplate(typeof(TextBox));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ScrollViewer));
		frameworkElementFactory2.Name = "PART_ContentHost";
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		textBox2.Template = controlTemplate;
		BuddyTheme.ApplyTextMode(textBox2, size);
		return textBox2;
	}

	internal static void FocusLater(UIElement el)
	{
		el.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			el.Focus();
			Keyboard.Focus(el);
		});
	}

	internal static FrameworkElement Bell(double size, Brush ink)
	{
		FrameworkElement frameworkElement = NotchGlyphs.Create("bell", size, ink);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		return frameworkElement;
	}
}
