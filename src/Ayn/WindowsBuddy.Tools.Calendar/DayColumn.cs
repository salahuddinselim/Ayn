using System;
using System.Collections.Generic;
using System.Globalization;
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

internal sealed class DayColumn : Grid
{
	private readonly CalendarTool _tool;

	private readonly TextBlock _letter;

	private readonly TextBlock _num;

	private readonly Ellipse _circle;

	private readonly Border _fill;

	private readonly StackPanel _dots;

	private DateTime _day;

	private bool _today;

	private bool _selected;

	private bool _past;

	public DateTime Day => _day;

	public DayColumn(CalendarTool tool)
	{
		_tool = tool;
		base.Background = Brushes.Transparent;
		base.Cursor = Cursors.Hand;
		_fill = new Border
		{
			CornerRadius = new CornerRadius(12.0),
			Margin = new Thickness(4.0, 0.0, 4.0, 0.0),
			IsHitTestVisible = false
		};
		base.Children.Add(_fill);
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		_letter = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, AynType.Medium);
		_letter.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.Children.Add(_letter);
		Grid grid = new Grid
		{
			Width = 24.0,
			Height = 24.0,
			Margin = new Thickness(0.0, 1.0, 0.0, 0.0)
		};
		_circle = new Ellipse
		{
			Fill = ThemeTokens.Accent,
			Visibility = Visibility.Collapsed
		};
		grid.Children.Add(_circle);
		_num = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
		_num.HorizontalAlignment = HorizontalAlignment.Center;
		_num.VerticalAlignment = VerticalAlignment.Center;
		Typography.SetNumeralAlignment(_num, FontNumeralAlignment.Tabular);
		grid.Children.Add(_num);
		stackPanel.Children.Add(grid);
		_dots = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Height = 5.0,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
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
			_tool.SelectDay(_day);
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

	public void Bind(DateTime day, bool today, bool selected, bool past, List<CalOccurrence> items, bool hasReminder)
	{
		_day = day;
		_today = today;
		_selected = selected;
		_past = past;
		_letter.Text = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek).Substring(0, 3);
		_num.Text = day.Day.ToString(Cal.Inv);
		List<string> list = new List<string>(4);
		if (items.Any((CalOccurrence o) => o.Kind == OccKind.Holiday))
		{
			list.Add("holiday");
		}
		if (items.Any((CalOccurrence o) => o.Kind == OccKind.Observance))
		{
			list.Add("observance");
		}
		if (items.Any((CalOccurrence o) => !o.IsHoliday))
		{
			list.Add("event");
		}
		if (hasReminder)
		{
			list.Add("reminder");
		}
		for (int i = 0; i < 3; i++)
		{
			Ellipse ellipse = (Ellipse)_dots.Children[i];
			ellipse.Visibility = ((i >= list.Count) ? Visibility.Collapsed : Visibility.Visible);
			if (i < list.Count)
			{
				ellipse.Fill = MonthCell.DotInk(list[i]);
			}
		}
		_dots.Opacity = (past ? 0.6 : 1.0);
		Paint(base.IsMouseOver);
	}

	private void Paint(bool hover)
	{
		_circle.Visibility = ((!_today) ? Visibility.Collapsed : Visibility.Visible);
		_fill.Background = (_selected ? ThemeTokens.AccentSoft : (hover ? CalendarTool.HoverFill : Brushes.Transparent));
		_num.Foreground = (_today ? ThemeTokens.AccentFg : ((_past && !_selected) ? BuddyTheme.Text50 : BuddyTheme.Text92));
		_num.FontWeight = ((_today || _selected) ? AynType.Semibold : AynType.Regular);
		_letter.Foreground = ((_today || _selected) ? BuddyTheme.Text100 : BuddyTheme.Text50);
	}
}
