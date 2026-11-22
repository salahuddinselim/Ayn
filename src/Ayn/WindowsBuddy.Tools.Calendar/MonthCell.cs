using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class MonthCell : Grid
{
	internal const double DotSize = 5.0;

	internal const double CircleSize = 26.0;

	private readonly CalendarTool _tool;

	private readonly Border _fill;

	private readonly Ellipse _circle;

	private readonly TextBlock _num;

	private readonly StackPanel _dots;

	private DateTime _day;

	private DateTime _lastClick;

	private bool _selected;

	private bool _today;

	private readonly List<string> _kinds = new List<string>(3);

	public DateTime Day => _day;

	public IList<string> DotKinds => _kinds;

	public TextBlock Number => _num;

	public bool IsTodayCircle => _circle.Visibility == Visibility.Visible;

	public Brush FillBrush => _fill.Background;

	public MonthCell(CalendarTool tool)
	{
		_tool = tool;
		base.Background = Brushes.Transparent;
		base.Cursor = Cursors.Hand;
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		_fill = new Border
		{
			CornerRadius = new CornerRadius(10.0),
			Margin = new Thickness(2.0, 1.0, 2.0, 1.0),
			IsHitTestVisible = false
		};
		base.Children.Add(_fill);
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			IsHitTestVisible = false
		};
		Grid grid = new Grid
		{
			Width = 26.0,
			Height = 26.0,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_circle = new Ellipse
		{
			Fill = ThemeTokens.Accent,
			Visibility = Visibility.Collapsed
		};
		grid.Children.Add(_circle);
		_num = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, AynType.Regular);
		_num.HorizontalAlignment = HorizontalAlignment.Center;
		_num.VerticalAlignment = VerticalAlignment.Center;
		TextOptions.SetTextFormattingMode(_num, TextFormattingMode.Display);
		Typography.SetNumeralAlignment(_num, FontNumeralAlignment.Tabular);
		grid.Children.Add(_num);
		stackPanel.Children.Add(grid);
		_dots = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Height = 5.0,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		for (int i = 0; i < 3; i++)
		{
			_dots.Children.Add(new Ellipse
			{
				Width = 5.0,
				Height = 5.0,
				Margin = new Thickness((i != 0) ? 3 : 0, 0.0, 0.0, 0.0),
				Visibility = Visibility.Collapsed
			});
		}
		stackPanel.Children.Add(_dots);
		base.Children.Add(stackPanel);
		CalendarTool.AttachClick(this, delegate
		{
			bool flag = (DateTime.UtcNow - _lastClick).TotalMilliseconds < 450.0 && _tool != null;
			_lastClick = DateTime.UtcNow;
			if (flag)
			{
				_tool.OpenEditorNew(_day, reminder: false);
			}
			else
			{
				_tool.SelectDay(_day);
			}
		}, null);
		base.MouseEnter += delegate
		{
			Paint(hover: true);
		};
		base.MouseLeave += delegate
		{
			Paint(hover: false);
		};
	}

	public void Bind(DateTime day, bool inMonth, bool today, bool selected, List<CalOccurrence> items, List<UserReminder> reminders)
	{
		_day = day;
		_today = today;
		_selected = selected;
		_num.Text = day.Day.ToString(Cal.Inv);
		_kinds.Clear();
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		foreach (CalOccurrence item in items)
		{
			if (item.Kind == OccKind.Holiday)
			{
				flag = true;
			}
			else if (item.Kind == OccKind.Observance)
			{
				flag2 = true;
			}
			else
			{
				flag3 = true;
			}
		}
		if (flag)
		{
			_kinds.Add("holiday");
		}
		if (flag2)
		{
			_kinds.Add("observance");
		}
		if (flag3)
		{
			_kinds.Add("event");
		}
		if (reminders != null && reminders.Count > 0)
		{
			_kinds.Add("reminder");
		}
		if (_kinds.Count > 3)
		{
			_kinds.RemoveRange(3, _kinds.Count - 3);
		}
		for (int i = 0; i < 3; i++)
		{
			Ellipse ellipse = (Ellipse)_dots.Children[i];
			ellipse.Visibility = ((i >= _kinds.Count) ? Visibility.Collapsed : Visibility.Visible);
			if (i < _kinds.Count)
			{
				ellipse.Fill = DotInk(_kinds[i]);
			}
		}
		base.Opacity = (inMonth ? 1.0 : 0.3);
		base.ToolTip = Tip(day, items, reminders);
		Paint(base.IsMouseOver);
	}

	internal static Brush DotInk(string kind)
	{
		return kind switch
		{
			"holiday" => CalendarTool.HolidayInk, 
			"observance" => CalendarTool.ObservanceInk, 
			"event" => CalendarTool.EventInk, 
			_ => CalendarTool.ReminderInk, 
		};
	}

	private static object Tip(DateTime day, List<CalOccurrence> items, List<UserReminder> reminders)
	{
		List<string> list = new List<string>();
		foreach (CalOccurrence item in items)
		{
			list.Add((item.AllDay ? "" : (Cal.Time(item.Start) + "  ")) + item.Title);
		}
		if (reminders != null)
		{
			foreach (UserReminder reminder in reminders)
			{
				list.Add((reminder.DueTime.HasValue ? (Cal.Time(reminder.DueDate + reminder.DueTime.Value) + "  ") : "") + reminder.Title);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		string text = string.Join("\n", list.Take(8)) + ((list.Count > 8) ? ("\n+" + (list.Count - 8) + " more") : "");
		return BuddyTooltip.Create(text);
	}

	private void Paint(bool hover)
	{
		_circle.Visibility = ((!_today) ? Visibility.Collapsed : Visibility.Visible);
		_fill.Background = (_selected ? ThemeTokens.AccentSoft : (hover ? CalendarTool.HoverFill : Brushes.Transparent));
		_num.Foreground = (_today ? ThemeTokens.AccentFg : BuddyTheme.Text92);
		_num.FontWeight = ((_today || _selected) ? AynType.Semibold : AynType.Regular);
	}
}
