using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class CalendarEditor : Grid
{
	internal static readonly string[] RepeatLabels = new string[6] { "None", "Daily", "Weekdays", "Weekly", "Monthly", "Yearly" };

	internal static readonly CalRepeat[] RepeatValues = new CalRepeat[6]
	{
		CalRepeat.None,
		CalRepeat.Daily,
		CalRepeat.Weekdays,
		CalRepeat.Weekly,
		CalRepeat.Monthly,
		CalRepeat.Yearly
	};

	internal static readonly string[] AlertLabels = new string[6] { "None", "At time", "5 min before", "10 min before", "30 min before", "1 day before" };

	private readonly CalendarTool _tool;

	private readonly bool _isNew;

	private bool _reminder;

	private UserEvent _ev;

	private UserReminder _rem;

	private DateTime _date;

	private DateTime _endDate;

	private TimeSpan? _start;

	private TimeSpan? _end;

	private bool _allDay;

	private CalRepeat _repeat;

	private CalAlert _alert;

	private CalColor _color;

	private TextBox _title;

	private TextBlock _titleHint;

	private TextBlock _hint;

	private TextBlock _allDayLabel;

	private Border _saveButton;

	private Border _dateButton;

	private Border _untilButton;

	private Border _repeatButton;

	private Border _alertButton;

	private TextBlock _dateText;

	private TextBlock _untilText;

	private TextBlock _repeatText;

	private TextBlock _alertText;

	private TimeField _startField;

	private TimeField _endField;

	private FrameworkElement _timeDash;

	private FrameworkElement _allDayRow;

	private FrameworkElement _alertRow;

	private FrameworkElement _quickRow;

	private FrameworkElement _timeRow;

	private readonly List<Border> _dateChips = new List<Border>();

	private BuddySlidingSegmented _kind;

	private Toggle _allDayToggle;

	private StackPanel _swatches;

	private Border _picker;

	private string _pickerKind;

	internal bool PickerOpen => _picker != null;

	internal string PickerKind
	{
		get
		{
			if (_picker == null)
			{
				return null;
			}
			return _pickerKind;
		}
	}

	internal Border PickerElement => _picker;

	internal TextBox TitleBox => _title;

	internal TimeField StartField => _startField;

	internal TimeField EndField => _endField;

	internal BuddySlidingSegmented KindToggle => _kind;

	internal Border SaveButton => _saveButton;

	internal IList<Border> DateChips => _dateChips;

	internal Border DateButton => _dateButton;

	internal Border RepeatButton => _repeatButton;

	internal bool IsReminder => _reminder;

	internal DateTime Date => _date;

	internal TimeSpan? Start => _start;

	internal TimeSpan? End => _end;

	internal CalRepeat Repeat => _repeat;

	internal string Hint => _hint.Text;

	internal bool HintIsWarning => _hint.Foreground == CalendarTool.OverdueInk;

	internal CalendarEditor(CalendarTool tool, UserEvent ev, UserReminder rem, bool isNew)
	{
		_tool = tool;
		_isNew = isNew;
		_reminder = rem != null;
		_ev = ev ?? new UserEvent();
		_rem = rem ?? new UserReminder();
		if (_reminder)
		{
			_date = _rem.DueDate;
			_start = _rem.DueTime;
			_repeat = _rem.Repeat;
			_end = (_start.HasValue ? new TimeSpan?(_start.Value + TimeSpan.FromHours(1.0)) : null);
			_endDate = _date;
			_alert = CalAlert.AtTime;
			_color = CalColor.Green;
		}
		else
		{
			_date = _ev.StartDate;
			_endDate = _ev.LastDate;
			_allDay = _ev.AllDay;
			_start = (_allDay ? null : Cal.ParseTime(_ev.Start));
			_end = (_allDay ? null : Cal.ParseTime(_ev.End));
			_repeat = _ev.Repeat;
			_alert = _ev.Alert;
			_color = _ev.Color;
		}
		KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
		KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.Cycle);
		Build();
		Sync();
	}

	private void Build()
	{
		base.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		base.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		base.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		base.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid grid = new Grid();
		grid.Height = 30.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		if (_isNew)
		{
			_kind = new BuddySlidingSegmented(new string[2] { "Event", "Reminder" }, _reminder ? 1 : 0, 28.0, 11.5, 76.0);
			_kind.SelectionChanged += delegate(int i)
			{
				SetKind(i == 1);
			};
			KeyboardNavigation.SetTabIndex(_kind, 2);
			grid2.Children.Add(_kind);
		}
		else
		{
			BuddyLabel buddyLabel = BuddyTheme.LabelText(_reminder ? "Edit reminder" : "Edit event");
			buddyLabel.VerticalAlignment = VerticalAlignment.Center;
			grid2.Children.Add(buddyLabel);
		}
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		if (!_isNew)
		{
			Border border = CalendarTool.TextLink("Delete", delegate
			{
				if (_reminder)
				{
					_tool.DeleteReminder(_rem.Id);
				}
				else
				{
					_tool.DeleteEvent(_ev.Id);
				}
			});
			((TextBlock)border.Child).Foreground = ThemeTokens.Danger;
			border.Margin = new Thickness(0.0, 0.0, 14.0, 0.0);
			stackPanel2.Children.Add(border);
		}
		Border border2 = CalendarTool.SoftPill("Cancel", delegate
		{
			_tool.CloseOverlays();
		}, 30.0);
		border2.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		KeyboardNavigation.SetTabIndex(border2, 60);
		stackPanel2.Children.Add(border2);
		_saveButton = CalendarTool.Pill(_isNew ? "Add" : "Save", Save);
		KeyboardNavigation.SetTabIndex(_saveButton, 61);
		stackPanel2.Children.Add(_saveButton);
		Grid.SetColumn(stackPanel2, 1);
		grid2.Children.Add(stackPanel2);
		base.Children.Add(grid2);
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(0.0, 10.0, 0.0, 10.0);
		Grid grid4 = grid3;
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Grid grid5 = new Grid();
		_titleHint = BuddyTheme.Text("Add a title", 20.0, BuddyTheme.Text30, AynType.Medium);
		_titleHint.FontFamily = BuddyTheme.DisplayFont;
		_titleHint.IsHitTestVisible = false;
		_titleHint.VerticalAlignment = VerticalAlignment.Center;
		grid5.Children.Add(_titleHint);
		_title = CalendarTool.CleanBox(20.0);
		_title.FontFamily = BuddyTheme.DisplayFont;
		_title.FontWeight = AynType.Medium;
		_title.MaxLength = 200;
		_title.Text = (_reminder ? (_rem.Title ?? "") : (_ev.Title ?? ""));
		_title.TextChanged += delegate
		{
			_titleHint.Visibility = ((_title.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
			UpdateSaveState();
		};
		_titleHint.Visibility = ((_title.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		KeyboardNavigation.SetTabIndex(_title, 1);
		grid5.Children.Add(_title);
		grid4.Children.Add(grid5);
		_swatches = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(12.0, 0.0, 0.0, 0.0)
		};
		CalColor[] array = new CalColor[4]
		{
			CalColor.Green,
			CalColor.Blue,
			CalColor.Amber,
			CalColor.Grey
		};
		foreach (CalColor calColor in array)
		{
			CalColor col = calColor;
			Grid grid6 = new Grid();
			grid6.Width = 20.0;
			grid6.Height = 20.0;
			grid6.Background = Brushes.Transparent;
			grid6.Cursor = Cursors.Hand;
			grid6.Tag = calColor;
			grid6.ToolTip = BuddyTooltip.Create((calColor == CalColor.Grey) ? "Grey" : calColor.ToString());
			Grid grid7 = grid6;
			grid7.Children.Add(new Ellipse
			{
				Width = 12.0,
				Height = 12.0,
				Fill = CalendarTool.ColorOf(calColor)
			});
			CalendarTool.AttachClick(grid7, delegate
			{
				_color = col;
				Sync();
			}, null);
			_swatches.Children.Add(grid7);
		}
		Grid.SetColumn(_swatches, 1);
		grid4.Children.Add(_swatches);
		Grid.SetRow(grid4, 1);
		base.Children.Add(grid4);
		Grid grid8 = new Grid();
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(20.0)
		});
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(226.0)
		});
		Grid.SetRow(grid8, 2);
		base.Children.Add(grid8);
		StackPanel stackPanel3 = new StackPanel();
		Grid grid9 = Line("calendar_ltr");
		StackPanel stackPanel4 = (StackPanel)grid9.Children[1];
		string[] array2 = new string[3] { "Today", "Tomorrow", "Next week" };
		for (int k = 0; k < array2.Length; k++)
		{
			int choice2 = k;
			Border border3 = Chip(array2[k], delegate
			{
				ApplyQuickDate(choice2);
			}, 28.0);
			border3.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
			KeyboardNavigation.SetTabIndex(border3, 10 + k);
			_dateChips.Add(border3);
			stackPanel4.Children.Add(border3);
		}
		_dateButton = PickerButton(out _dateText, delegate
		{
			OpenDatePicker(_dateButton, until: false);
		}, "Pick a date");
		KeyboardNavigation.SetTabIndex(_dateButton, 13);
		stackPanel4.Children.Add(_dateButton);
		stackPanel3.Children.Add(grid9);
		Grid grid10 = Line("clock");
		grid10.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		StackPanel stackPanel5 = (StackPanel)grid10.Children[1];
		_startField = new TimeField(this, isEnd: false);
		_startField.Committed += OnStartCommitted;
		KeyboardNavigation.SetTabIndex(_startField.Box, 20);
		stackPanel5.Children.Add(_startField);
		TextBlock textBlock = BuddyTheme.Text("–", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		_timeDash = textBlock;
		stackPanel5.Children.Add(textBlock);
		_endField = new TimeField(this, isEnd: true);
		_endField.Committed += delegate(TimeSpan? v)
		{
			_end = v;
			Sync();
		};
		KeyboardNavigation.SetTabIndex(_endField.Box, 21);
		stackPanel5.Children.Add(_endField);
		_allDayLabel = BuddyTheme.Text("All day", 13.0, BuddyTheme.Text92, AynType.Medium);
		_allDayLabel.VerticalAlignment = VerticalAlignment.Center;
		_allDayLabel.Margin = new Thickness(2.0, 0.0, 10.0, 0.0);
		stackPanel5.Children.Add(_allDayLabel);
		_untilButton = PickerButton(out _untilText, delegate
		{
			OpenDatePicker(_untilButton, until: true);
		}, "Last day");
		KeyboardNavigation.SetTabIndex(_untilButton, 22);
		stackPanel5.Children.Add(_untilButton);
		_timeRow = grid10;
		stackPanel3.Children.Add(grid10);
		StackPanel stackPanel6 = new StackPanel();
		stackPanel6.Orientation = Orientation.Horizontal;
		stackPanel6.Margin = new Thickness(30.0, 8.0, 0.0, 0.0);
		StackPanel stackPanel7 = stackPanel6;
		string[] array3 = new string[4] { "In 15 min", "In 1 hour", "Evening 6 PM", "Tomorrow 9 AM" };
		for (int l = 0; l < array3.Length; l++)
		{
			int choice = l;
			Border border4 = Chip(array3[l], delegate
			{
				ApplyQuickTime(choice);
			}, 24.0);
			border4.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
			KeyboardNavigation.SetTabIndex(border4, 30 + l);
			stackPanel7.Children.Add(border4);
		}
		_quickRow = stackPanel7;
		stackPanel3.Children.Add(stackPanel7);
		grid8.Children.Add(stackPanel3);
		StackPanel stackPanel8 = new StackPanel();
		_allDayToggle = new Toggle(_allDay, delegate(bool on)
		{
			SetAllDay(on);
		});
		KeyboardNavigation.SetTabIndex(_allDayToggle, 40);
		_allDayRow = OptionRow("All day", _allDayToggle);
		stackPanel8.Children.Add(_allDayRow);
		_repeatButton = PickerButton(out _repeatText, delegate
		{
			OpenOptions(_repeatButton, "repeat");
		}, "Repeat");
		KeyboardNavigation.SetTabIndex(_repeatButton, 41);
		FrameworkElement element = OptionRow("Repeat", _repeatButton);
		stackPanel8.Children.Add(element);
		_alertButton = PickerButton(out _alertText, delegate
		{
			OpenOptions(_alertButton, "alert");
		}, "Alert");
		KeyboardNavigation.SetTabIndex(_alertButton, 42);
		_alertRow = OptionRow("Alert", _alertButton);
		stackPanel8.Children.Add(_alertRow);
		Grid.SetColumn(stackPanel8, 2);
		grid8.Children.Add(stackPanel8);
		_hint = BuddyTheme.Text("", 11.5, BuddyTheme.Text50, FontWeights.Normal);
		_hint.Margin = new Thickness(30.0, 8.0, 0.0, 0.0);
		_hint.TextTrimming = TextTrimming.CharacterEllipsis;
		_hint.MinHeight = 16.0;
		Grid.SetRow(_hint, 3);
		base.Children.Add(_hint);
		UpdateSaveState();
		base.PreviewKeyDown += OnKey;
	}

	private void OnKey(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			e.Handled = true;
			if (!ClosePickers())
			{
				_tool.CloseOverlays();
			}
		}
		else if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
		{
			FrameworkElement frameworkElement = Keyboard.FocusedElement as FrameworkElement;
			if (!(frameworkElement is Border) || frameworkElement == _saveButton)
			{
				e.Handled = true;
				Save();
			}
		}
	}

	internal bool HandleEscape()
	{
		return ClosePickers();
	}

	private static Grid Line(string icon)
	{
		Grid grid = new Grid();
		grid.MinHeight = 30.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(30.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement frameworkElement = FluentIcons.Create(icon, 16.0, BuddyTheme.Text50, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Left;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(frameworkElement);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel element = stackPanel;
		Grid.SetColumn(element, 1);
		grid2.Children.Add(element);
		return grid2;
	}

	private static FrameworkElement OptionRow(string label, FrameworkElement control)
	{
		Grid grid = new Grid();
		grid.Height = 30.0;
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		Grid grid2 = grid;
		TextBlock textBlock = BuddyTheme.Text(label, 13.0, BuddyTheme.Text50, AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(textBlock);
		control.HorizontalAlignment = HorizontalAlignment.Right;
		control.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(control);
		return grid2;
	}

	private static Border Chip(string text, Action click, double height)
	{
		return CalendarTool.SoftPill(text, click, height);
	}

	private static void SetChipOn(Border chip, bool on)
	{
		chip.Tag = on;
		chip.Background = (on ? ThemeTokens.AccentSoft : CalendarTool.QuietFill);
		((TextBlock)chip.Child).FontWeight = (on ? AynType.Semibold : AynType.Medium);
	}

	private static Border PickerButton(out TextBlock text, Action click, string tip)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		text = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, AynType.Medium);
		text.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(text);
		FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 10.0, BuddyTheme.Text50, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(6.0, 1.0, 0.0, 0.0);
		stackPanel2.Children.Add(frameworkElement);
		Border b = new Border
		{
			Height = 28.0,
			Padding = new Thickness(11.0, 0.0, 10.0, 0.0),
			CornerRadius = new CornerRadius(14.0),
			Background = CalendarTool.QuietFill,
			Cursor = Cursors.Hand,
			Child = stackPanel2,
			ToolTip = BuddyTooltip.Create(tip)
		};
		b.MouseEnter += delegate
		{
			b.Background = CalendarTool.QuietFillHover;
		};
		b.MouseLeave += delegate
		{
			if (!b.IsKeyboardFocused)
			{
				b.Background = CalendarTool.QuietFill;
			}
		};
		CalendarTool.AttachClick(b, click, null);
		CalendarTool.MakeKeyboardButton(b, click, CalendarTool.QuietFill, CalendarTool.QuietFillHover);
		return b;
	}

	private void UpdateSaveState()
	{
		if (_saveButton != null)
		{
			bool flag = !string.IsNullOrWhiteSpace(_title.Text);
			_saveButton.IsEnabled = flag;
			_saveButton.Opacity = (flag ? 1.0 : 0.38);
			_saveButton.Cursor = (flag ? Cursors.Hand : Cursors.Arrow);
		}
	}

	public void FocusTitle()
	{
		CalendarTool.FocusLater(_title);
	}

	private void SetKind(bool reminder)
	{
		ClosePickers();
		_reminder = reminder;
		if (reminder)
		{
			_allDay = false;
		}
		else if (!_start.HasValue)
		{
			_start = DefaultStart(_date);
			_end = _start.Value + TimeSpan.FromHours(1.0);
		}
		Sync();
	}

	private void SetAllDay(bool on)
	{
		_allDay = on;
		if (on)
		{
			_endDate = _date;
		}
		else if (!_start.HasValue)
		{
			_start = DefaultStart(_date);
			_end = _start.Value + TimeSpan.FromHours(1.0);
		}
		Sync();
	}

	private static TimeSpan DefaultStart(DateTime day)
	{
		DateTime now = DateTime.Now;
		if (day != now.Date)
		{
			return TimeSpan.FromHours(9.0);
		}
		return TimeSpan.FromMinutes(Math.Min(1380.0, Math.Ceiling((now.TimeOfDay.TotalMinutes + 1.0) / 30.0) * 30.0));
	}

	private void OnStartCommitted(TimeSpan? t)
	{
		if (!t.HasValue)
		{
			_start = null;
			Sync();
			return;
		}
		TimeSpan timeSpan = ((_start.HasValue && _end.HasValue) ? (_end.Value - _start.Value) : TimeSpan.FromHours(1.0));
		if (timeSpan <= TimeSpan.Zero)
		{
			timeSpan += TimeSpan.FromDays(1.0);
		}
		if (timeSpan >= TimeSpan.FromDays(1.0))
		{
			timeSpan = TimeSpan.FromHours(1.0);
		}
		_start = t.Value;
		TimeSpan timeSpan2 = t.Value + timeSpan;
		_end = ((timeSpan2 >= TimeSpan.FromDays(1.0)) ? (timeSpan2 - TimeSpan.FromDays(1.0)) : timeSpan2);
		Sync();
	}

	internal void ApplyQuickDate(int choice)
	{
		MoveDate(choice switch
		{
			1 => DateTime.Today.AddDays(1.0), 
			0 => DateTime.Today, 
			_ => DateTime.Today.AddDays(7.0), 
		});
		Sync();
	}

	private void MoveDate(DateTime d)
	{
		TimeSpan timeSpan = _endDate - _date;
		_date = d.Date;
		_endDate = _date + ((timeSpan > TimeSpan.Zero) ? timeSpan : TimeSpan.Zero);
	}

	internal void ApplyQuickTime(int choice)
	{
		DateTime now = DateTime.Now;
		now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
		DateTime dateTime = choice switch
		{
			0 => now.AddMinutes(15.0), 
			1 => now.AddHours(1.0), 
			2 => now.Date.AddHours(18.0), 
			_ => now.Date.AddDays(1.0).AddHours(9.0), 
		};
		TimeSpan timeSpan = ((_start.HasValue && _end.HasValue && _end.Value > _start.Value) ? (_end.Value - _start.Value) : TimeSpan.FromHours(1.0));
		_allDay = false;
		MoveDate(dateTime.Date);
		_start = dateTime.TimeOfDay;
		TimeSpan timeSpan2 = dateTime.TimeOfDay + timeSpan;
		_end = ((timeSpan2 >= TimeSpan.FromDays(1.0)) ? (timeSpan2 - TimeSpan.FromDays(1.0)) : timeSpan2);
		Sync();
	}

	private void Sync()
	{
		DateTime today = DateTime.Today;
		_dateText.Text = FormatDate(_date);
		_untilText.Text = ((_endDate > _date) ? ("until " + FormatDate(_endDate)) : "Add end date");
		for (int i = 0; i < _dateChips.Count; i++)
		{
			SetChipOn(_dateChips[i], _date == i switch
			{
				1 => today.AddDays(1.0), 
				0 => today, 
				_ => today.AddDays(7.0), 
			});
		}
		bool flag = _reminder || !_allDay;
		_startField.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_startField.AllowNone = _reminder;
		_startField.Value = _start;
		_endField.Value = _end ?? (_start.HasValue ? (_start.Value + TimeSpan.FromHours(1.0)) : TimeSpan.FromHours(10.0));
		_endField.Visibility = ((_reminder || _allDay) ? Visibility.Collapsed : Visibility.Visible);
		_timeDash.Visibility = _endField.Visibility;
		_allDayLabel.Visibility = ((_reminder || !_allDay) ? Visibility.Collapsed : Visibility.Visible);
		_untilButton.Visibility = _allDayLabel.Visibility;
		_allDayRow.Visibility = (_reminder ? Visibility.Collapsed : Visibility.Visible);
		_alertRow.Visibility = (_reminder ? Visibility.Collapsed : Visibility.Visible);
		_swatches.Visibility = (_reminder ? Visibility.Collapsed : Visibility.Visible);
		_allDayToggle.IsOn = _allDay;
		_repeatText.Text = RepeatLabels[(Array.IndexOf(RepeatValues, _repeat) >= 0) ? Array.IndexOf(RepeatValues, _repeat) : 0];
		_alertText.Text = AlertLabels[Math.Max(0, Math.Min(AlertLabels.Length - 1, (int)_alert))];
		foreach (Grid child in _swatches.Children)
		{
			bool flag2 = (CalColor)child.Tag == _color;
			Ellipse ellipse = (Ellipse)child.Children[0];
			double width = (ellipse.Height = (flag2 ? 16 : 11));
			ellipse.Width = width;
			ellipse.Opacity = (flag2 ? 1.0 : 0.55);
		}
		RefreshHint();
	}

	private void RefreshHint()
	{
		if (_startField.Error != null)
		{
			SetHint(_startField.Error, warn: true);
			return;
		}
		if (_endField.Error != null && _endField.Visibility == Visibility.Visible)
		{
			SetHint(_endField.Error, warn: true);
			return;
		}
		DateTime now = DateTime.Now;
		string text = PastWarning(now);
		if (text != null)
		{
			SetHint(text, warn: true);
			return;
		}
		string text2 = "";
		if ((_reminder || _alert != 0) && !CalendarService.NotchAlertsOn)
		{
			text2 = "Notch alerts for Calendar are off — turn on Calendar & Reminders in notch settings";
		}
		else if (!_reminder && !_allDay && _start.HasValue && _end.HasValue && _end.Value < _start.Value)
		{
			text2 = "Ends the next day at " + Cal.Time(DateTime.Today + _end.Value);
		}
		else if (!_reminder && _allDay && _endDate > _date)
		{
			text2 = (int)(_endDate - _date).TotalDays + 1 + " days";
		}
		else if (_repeat == CalRepeat.Monthly && _date.Day > 28)
		{
			text2 = "Repeats on day " + _date.Day + " (or the month's last day)";
		}
		else if (_repeat == CalRepeat.Weekdays)
		{
			text2 = "Repeats Monday to Friday";
		}
		SetHint(text2, warn: false);
	}

	internal string PastWarning(DateTime now)
	{
		bool hasValue;
		if (!_reminder)
		{
			if (_allDay)
			{
				goto IL_0081;
			}
			hasValue = _start.HasValue;
		}
		else
		{
			hasValue = _start.HasValue;
		}
		if (hasValue && _date + _start.Value < now && _date >= now.Date)
		{
			if (!(_date == now.Date))
			{
				return "This time is in the past";
			}
			return "This time has already passed today";
		}
		goto IL_0081;
		IL_0081:
		if (_date < now.Date)
		{
			return "This date is in the past";
		}
		return null;
	}

	private void SetHint(string text, bool warn)
	{
		_hint.Text = text;
		_hint.Foreground = (warn ? CalendarTool.OverdueInk : BuddyTheme.Text50);
	}

	private static string FormatDate(DateTime d)
	{
		return d.ToString((d.Year == DateTime.Today.Year) ? "ddd, d MMM" : "ddd, d MMM yyyy", Cal.Inv);
	}

	internal bool ClosePickers()
	{
		if (_picker == null)
		{
			return false;
		}
		_tool.PopLayer?.Children.Remove(_picker);
		_picker = null;
		_pickerKind = null;
		return true;
	}

	private Border PickerCard(double width)
	{
		Border border = new Border();
		border.Width = width;
		border.CornerRadius = new CornerRadius(14.0);
		border.Padding = new Thickness(6.0);
		Border border2 = border;
		bool isLight = ThemeTokens.IsLight;
		border2.Background = (isLight ? ThemeTokens.Surface : ThemeTokens.Surface3);
		border2.Effect = new DropShadowEffect
		{
			Color = Colors.Black,
			Direction = 270.0,
			ShadowDepth = 4.0,
			BlurRadius = 20.0,
			Opacity = (isLight ? 0.18 : 0.6)
		};
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		return border2;
	}

	private void Place(Border card, FrameworkElement anchor, string kind, double height)
	{
		ClosePickers();
		Canvas popLayer = _tool.PopLayer;
		if (popLayer != null)
		{
			popLayer.UpdateLayout();
			double num = ((popLayer.ActualWidth > 0.0) ? popLayer.ActualWidth : 728.0);
			double num2 = ((popLayer.ActualHeight > 0.0) ? popLayer.ActualHeight : 242.0);
			Point point;
			try
			{
				point = anchor.TranslatePoint(new Point(0.0, 0.0), popLayer);
			}
			catch
			{
				point = new Point(num / 2.0, num2 / 2.0);
			}
			double width = card.Width;
			double val;
			double val2;
			if (point.Y + anchor.ActualHeight + 6.0 + height <= num2 - 6.0)
			{
				val = point.X;
				val2 = point.Y + anchor.ActualHeight + 6.0;
			}
			else if (point.X + anchor.ActualWidth + 8.0 + width <= num - 6.0)
			{
				val = point.X + anchor.ActualWidth + 8.0;
				val2 = point.Y + anchor.ActualHeight / 2.0 - height / 2.0;
			}
			else
			{
				val = point.X - 8.0 - width;
				val2 = point.Y + anchor.ActualHeight / 2.0 - height / 2.0;
			}
			val = Math.Max(6.0, Math.Min(num - width - 6.0, val));
			val2 = Math.Max(6.0, Math.Min(num2 - height - 6.0, val2));
			Canvas.SetLeft(card, Math.Round(val));
			Canvas.SetTop(card, Math.Round(val2));
			card.Height = height;
			popLayer.Children.Add(card);
			_picker = card;
			_pickerKind = kind;
			if (CalendarTool.CapsuleMotionOn)
			{
				card.Opacity = 0.0;
				card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(120.0))));
			}
		}
	}

	internal void OpenTimePicker(TimeField field)
	{
		if (_picker != null && _pickerKind == (field.IsEnd ? "end" : "start"))
		{
			ClosePickers();
			return;
		}
		Border border = PickerCard(150.0);
		StackPanel stackPanel = new StackPanel();
		BuddyScrollViewer buddyScrollViewer = new BuddyScrollViewer();
		buddyScrollViewer.Content = stackPanel;
		BuddyScrollViewer buddyScrollViewer2 = (BuddyScrollViewer)(border.Child = buddyScrollViewer);
		DateTime now = DateTime.Now;
		TimeSpan timeSpan = TimeSpan.FromMinutes(Math.Floor(now.TimeOfDay.TotalMinutes / 15.0) * 15.0);
		TimeSpan? value = field.Value;
		FrameworkElement frameworkElement = null;
		if (field.AllowNone)
		{
			FrameworkElement frameworkElement2 = TimeRow("No time", !value.HasValue, isNow: false, delegate
			{
				field.SetFromPicker(null);
				ClosePickers();
			});
			stackPanel.Children.Add(frameworkElement2);
			if (!value.HasValue)
			{
				frameworkElement = frameworkElement2;
			}
		}
		bool flag = _date == now.Date;
		for (int i = 0; i < 1440; i += 15)
		{
			TimeSpan t = TimeSpan.FromMinutes(i);
			bool flag2 = value.HasValue && Math.Abs((value.Value - t).TotalMinutes) < 7.5;
			bool flag3 = flag && t == timeSpan;
			FrameworkElement frameworkElement3 = TimeRow(Cal.Time(DateTime.Today + t), flag2, flag3, delegate
			{
				field.SetFromPicker(t);
				ClosePickers();
			});
			frameworkElement3.Tag = t;
			stackPanel.Children.Add(frameworkElement3);
			if (flag2 || (frameworkElement == null && !value.HasValue && flag3))
			{
				frameworkElement = frameworkElement3;
			}
			if (frameworkElement == null && value.HasValue && t > value.Value)
			{
				frameworkElement = frameworkElement3;
			}
		}
		Place(border, field, field.IsEnd ? "end" : "start", 212.0);
		double num = 28.0;
		double num2 = ((frameworkElement != null) ? ((double)stackPanel.Children.IndexOf(frameworkElement)) : (timeSpan.TotalMinutes / 15.0));
		buddyScrollViewer2.ScrollToVerticalOffset(Math.Max(0.0, num2 * num - 100.0 + num / 2.0));
	}

	private FrameworkElement TimeRow(string text, bool selected, bool isNow, Action pick)
	{
		Grid grid = new Grid();
		grid.Height = 28.0;
		grid.Background = (selected ? ThemeTokens.AccentSoft : Brushes.Transparent);
		grid.Cursor = Cursors.Hand;
		Grid grid2 = grid;
		Border border = new Border();
		border.CornerRadius = new CornerRadius(8.0);
		border.IsHitTestVisible = false;
		Border element = border;
		grid2.Children.Add(element);
		TextBlock textBlock = BuddyTheme.Text(text, 13.0, BuddyTheme.Text92, selected ? AynType.Semibold : AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		grid2.Children.Add(textBlock);
		if (isNow)
		{
			TextBlock textBlock2 = BuddyTheme.Text("Now", 10.0, ThemeTokens.Accent, AynType.Semibold);
			textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
			textBlock2.VerticalAlignment = VerticalAlignment.Center;
			textBlock2.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
			grid2.Children.Add(textBlock2);
			if (!selected)
			{
				textBlock.Foreground = ThemeTokens.Accent;
			}
		}
		Border host = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			Child = grid2,
			Background = Brushes.Transparent
		};
		grid2.Background = Brushes.Transparent;
		host.Background = (selected ? ThemeTokens.AccentSoft : Brushes.Transparent);
		host.MouseEnter += delegate
		{
			if (!selected)
			{
				host.Background = CalendarTool.HoverFill;
			}
		};
		host.MouseLeave += delegate
		{
			if (!selected)
			{
				host.Background = Brushes.Transparent;
			}
		};
		host.Tag = selected;
		CalendarTool.AttachClick(host, pick, null);
		return host;
	}

	internal void OpenDatePicker(FrameworkElement anchor, bool until)
	{
		string text = (until ? "until" : "date");
		if (_picker != null && _pickerKind == text)
		{
			ClosePickers();
			return;
		}
		Border border = PickerCard(236.0);
		MiniMonth child = new MiniMonth(until ? _endDate : _date, delegate(DateTime d)
		{
			if (until)
			{
				_endDate = ((d < _date) ? _date : d);
			}
			else
			{
				MoveDate(d);
			}
			ClosePickers();
			Sync();
		});
		border.Child = child;
		Place(border, anchor, text, 232.0);
	}

	internal void OpenOptions(FrameworkElement anchor, string kind)
	{
		if (_picker != null && _pickerKind == kind)
		{
			ClosePickers();
			return;
		}
		string[] array = ((kind == "repeat") ? RepeatLabels : AlertLabels);
		int num = ((kind == "repeat") ? Math.Max(0, Array.IndexOf(RepeatValues, _repeat)) : ((int)_alert));
		Border border = PickerCard(170.0);
		StackPanel stackPanel = new StackPanel();
		for (int i = 0; i < array.Length; i++)
		{
			int idx = i;
			stackPanel.Children.Add(TimeRow(array[i], i == num, isNow: false, delegate
			{
				if (kind == "repeat")
				{
					_repeat = RepeatValues[idx];
				}
				else
				{
					_alert = (CalAlert)idx;
				}
				ClosePickers();
				Sync();
			}));
		}
		border.Child = stackPanel;
		Place(border, anchor, kind, array.Length * 28 + 12);
	}

	private void Save()
	{
		if (!_startField.CommitPending() || (_endField.Visibility == Visibility.Visible && !_endField.CommitPending()))
		{
			RefreshHint();
			return;
		}
		string text = CalendarTool.CapitalizeFirst(_title.Text);
		if (text.Length == 0)
		{
			SetHint("Add a title", warn: true);
			_title.Focus();
			return;
		}
		ClosePickers();
		if (_reminder)
		{
			UserReminder userReminder = _rem;
			if (_isNew && string.IsNullOrEmpty(userReminder.Id))
			{
				userReminder = new UserReminder();
			}
			userReminder.Title = text;
			userReminder.Date = Cal.D(_date);
			userReminder.Time = (_start.HasValue ? Cal.T(_start.Value) : null);
			userReminder.Repeat = _repeat;
			_tool.EditorSaved(null, userReminder, _isNew);
			return;
		}
		UserEvent ev = _ev;
		ev.Title = text;
		ev.Date = Cal.D(_date);
		ev.Repeat = _repeat;
		ev.Alert = _alert;
		ev.Color = _color;
		if (_allDay)
		{
			string start = (ev.End = null);
			ev.Start = start;
			ev.EndDate = Cal.D((_endDate < _date) ? _date : _endDate);
		}
		else
		{
			TimeSpan timeSpan = _start ?? TimeSpan.FromHours(9.0);
			TimeSpan timeSpan2 = _end ?? (timeSpan + TimeSpan.FromHours(1.0));
			ev.Start = Cal.T(timeSpan);
			ev.End = Cal.T(timeSpan2);
			ev.EndDate = Cal.D((timeSpan2 < timeSpan) ? _date.AddDays(1.0) : _date);
		}
		_tool.EditorSaved(ev, null, _isNew);
	}

	internal void SetDate(DateTime d)
	{
		_date = d;
		if (_endDate < d)
		{
			_endDate = d;
		}
		Sync();
	}

	internal void SetTimes(TimeSpan s, TimeSpan e)
	{
		_start = s;
		_end = e;
		_allDay = false;
		Sync();
	}

	internal void SetAllDayForQa(bool on, DateTime until)
	{
		_allDay = on;
		_endDate = until;
		Sync();
	}

	internal void SetOptions(CalRepeat r, CalAlert a, CalColor c)
	{
		_repeat = r;
		_alert = a;
		_color = c;
		Sync();
	}

	internal void InvokeSave()
	{
		Save();
	}

	internal void FieldChanged()
	{
		RefreshHint();
	}
}
