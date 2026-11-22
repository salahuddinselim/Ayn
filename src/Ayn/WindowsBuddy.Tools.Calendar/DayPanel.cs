using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class DayPanel : Border
{
	private readonly CalendarTool _tool;

	private readonly TextBlock _weekday;

	private readonly TextBlock _date;

	private readonly StackPanel _body;

	private readonly BuddyScrollViewer _scroll;

	private DateTime _day = DateTime.MinValue;

	public string WeekdayText => _weekday.Text;

	public string DateText => _date.Text;

	public StackPanel Body => _body;

	public List<string> Sections { get; private set; }

	public DayPanel(CalendarTool tool)
	{
		_tool = tool;
		base.Background = BuddyTheme.CardFill;
		base.CornerRadius = new CornerRadius(16.0);
		base.Padding = new Thickness(14.0, 12.0, 6.0, 6.0);
		ApplyShadow(this);
		ThemeTokens.Subscribe(this, ApplyShadow);
		Grid grid = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = new GridLength(1.0, GridUnitType.Star)
				}
			}
		};
		Grid grid2 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 8.0, 8.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel();
		_weekday = BuddyTheme.Text("", 20.0, BuddyTheme.Text100, AynType.Semibold);
		_weekday.FontFamily = BuddyTheme.DisplayFont;
		_weekday.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel.Children.Add(_weekday);
		_date = BuddyTheme.Text("", 11.5, BuddyTheme.Text50, AynType.Regular);
		_date.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
		stackPanel.Children.Add(_date);
		grid2.Children.Add(stackPanel);
		Border border = AddButton();
		border.VerticalAlignment = VerticalAlignment.Top;
		border.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		Grid.SetColumn(border, 1);
		grid2.Children.Add(border);
		grid.Children.Add(grid2);
		_scroll = new BuddyScrollViewer();
		_body = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 8.0, 6.0)
		};
		_scroll.Content = _body;
		Grid.SetRow(_scroll, 1);
		grid.Children.Add(_scroll);
		Child = grid;
	}

	private static void ApplyShadow(DayPanel p)
	{
		p.Effect = (ThemeTokens.IsLight ? ThemeTokens.CardElevation : null);
	}

	private Border AddButton()
	{
		FrameworkElement frameworkElement = FluentIcons.Create("add", 14.0, BuddyTheme.Text92, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		Border b = new Border
		{
			Width = 26.0,
			Height = 26.0,
			CornerRadius = new CornerRadius(13.0),
			Background = CalendarTool.QuietFill,
			Cursor = Cursors.Hand,
			Child = frameworkElement,
			ToolTip = BuddyTooltip.Create("Add on this day")
		};
		Action click = delegate
		{
			_tool.OpenEditorNew(_day, reminder: false);
		};
		b.MouseEnter += delegate
		{
			b.Background = CalendarTool.QuietFillHover;
		};
		b.MouseLeave += delegate
		{
			b.Background = CalendarTool.QuietFill;
		};
		CalendarTool.AttachClick(b, click, null);
		CalendarTool.MakeKeyboardButton(b, click, CalendarTool.QuietFill, CalendarTool.QuietFillHover);
		return b;
	}

	public void Render(DateTime day, List<CalOccurrence> items, List<UserReminder> reminders, List<CalendarService.UpcomingItem> next, DateTime now)
	{
		bool flag = day != _day;
		_day = day;
		_weekday.Text = day.ToString("dddd", Cal.Inv);
		_date.Text = day.ToString((day.Year == now.Year) ? "d MMMM" : "d MMMM yyyy", Cal.Inv);
		_body.Children.Clear();
		Sections = new List<string>();
		List<CalOccurrence> list = items.Where((CalOccurrence o) => o.IsHoliday).ToList();
		List<CalOccurrence> list2 = items.Where((CalOccurrence o) => !o.IsHoliday).ToList();
		if (list.Count > 0)
		{
			AddHeading("Holidays", first: true);
			WrapPanel wrapPanel = new WrapPanel();
			wrapPanel.Margin = new Thickness(0.0, 0.0, 0.0, 2.0);
			WrapPanel wrapPanel2 = wrapPanel;
			foreach (CalOccurrence item in list)
			{
				wrapPanel2.Children.Add(HolidayPill(item));
			}
			_body.Children.Add(wrapPanel2);
		}
		if (list2.Count > 0)
		{
			AddHeading("Events", list.Count == 0);
			foreach (CalOccurrence item2 in list2)
			{
				_body.Children.Add(EventLine(item2, day, now));
			}
		}
		if (reminders.Count > 0)
		{
			AddHeading("Reminders", list.Count == 0 && list2.Count == 0);
			foreach (UserReminder reminder in reminders)
			{
				_body.Children.Add(ReminderLine(reminder, now));
			}
		}
		if (list.Count == 0 && list2.Count == 0 && reminders.Count == 0)
		{
			Sections.Add("Empty");
			StackPanel stackPanel = new StackPanel();
			stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
			stackPanel.Margin = new Thickness(0.0, 4.0, 0.0, 4.0);
			StackPanel stackPanel2 = stackPanel;
			TextBlock element = BuddyTheme.Text("Nothing planned", 13.0, BuddyTheme.Text50, AynType.Regular);
			stackPanel2.Children.Add(element);
			Border border = CalendarTool.SoftPill("Add event", delegate
			{
				_tool.OpenEditorNew(_day, reminder: false);
			});
			border.HorizontalAlignment = HorizontalAlignment.Left;
			border.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
			stackPanel2.Children.Add(border);
			_body.Children.Add(stackPanel2);
		}
		if (next != null && next.Count > 0)
		{
			AddHeading("Up next", first: false);
			foreach (CalendarService.UpcomingItem item3 in next)
			{
				_body.Children.Add(UpNextLine(item3, now));
			}
		}
		if (flag)
		{
			_scroll.ScrollToTop();
		}
	}

	private void AddHeading(string text, bool first)
	{
		Sections.Add(text);
		BuddyLabel buddyLabel = BuddyTheme.LabelText(text);
		buddyLabel.Margin = new Thickness(0.0, (!first) ? 10 : 0, 0.0, 4.0);
		_body.Children.Add(buddyLabel);
	}

	private FrameworkElement HolidayPill(CalOccurrence o)
	{
		bool flag = o.Kind == OccKind.Holiday;
		TextBlock textBlock = BuddyTheme.Text(o.Title, 11.5, flag ? CalendarTool.HolidayInk : BuddyTheme.Text92, AynType.Medium);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		Border b = new Border
		{
			Background = (flag ? CalendarTool.HolidaySoft : CalendarTool.ObservanceSoft),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(9.0, 4.0, 9.0, 5.0),
			Margin = new Thickness(0.0, 0.0, 6.0, 6.0),
			Child = textBlock,
			Cursor = Cursors.Hand,
			MaxWidth = 222.0,
			Tag = o,
			ToolTip = BuddyTooltip.Create(flag ? "Public holiday" : "Festival / observance")
		};
		CalendarTool.AttachClick(b, delegate
		{
			_tool.OpenDetails(o);
		}, delegate
		{
			_tool.OccurrenceMenu(o, b);
		});
		return b;
	}

	private FrameworkElement EventLine(CalOccurrence o, DateTime day, DateTime now)
	{
		Grid g = new Grid
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Margin = new Thickness(-6.0, 0.0, -6.0, 2.0),
			Tag = o
		};
		Border fill = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			IsHitTestVisible = false
		};
		Grid.SetColumnSpan(fill, 4);
		g.Children.Add(fill);
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(66.0)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(3.0)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(9.0)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		string text = (o.AllDay ? "All day" : ((!(o.Start.Date < day)) ? Cal.Time(o.Start) : ((o.End.Date > day) ? "All day" : ("until " + Cal.Time(o.End)))));
		TextBlock textBlock = BuddyTheme.Text(text, 11.5, o.IsNow(now) ? BuddyTheme.Text100 : BuddyTheme.Text50, AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Top;
		textBlock.Margin = new Thickness(6.0, 6.0, 0.0, 0.0);
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		g.Children.Add(textBlock);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 3.0;
		rectangle.RadiusX = 1.5;
		rectangle.RadiusY = 1.5;
		rectangle.Fill = CalendarTool.InkOf(o);
		rectangle.Margin = new Thickness(0.0, 6.0, 0.0, 6.0);
		rectangle.MinHeight = 14.0;
		Rectangle element = rectangle;
		Grid.SetColumn(element, 1);
		g.Children.Add(element);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(0.0, 5.0, 6.0, 5.0);
		StackPanel stackPanel2 = stackPanel;
		TextBlock textBlock2 = BuddyTheme.Text(o.Title, 13.0, BuddyTheme.Text92, AynType.Medium);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.MaxHeight = 36.0;
		textBlock2.TextTrimming = TextTrimming.WordEllipsis;
		stackPanel2.Children.Add(textBlock2);
		string text2 = (o.ReadOnly ? (o.SourceName + (string.IsNullOrEmpty(o.Location) ? "" : (" · " + o.Location))) : o.Location);
		if (!string.IsNullOrEmpty(text2))
		{
			TextBlock textBlock3 = BuddyTheme.Text(text2, 11.0, BuddyTheme.Text50, AynType.Regular);
			textBlock3.TextTrimming = TextTrimming.CharacterEllipsis;
			textBlock3.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock3);
		}
		Grid.SetColumn(stackPanel2, 3);
		g.Children.Add(stackPanel2);
		g.Opacity = (o.IsPast(now) ? 0.5 : 1.0);
		g.ToolTip = ((o.Title.Length > 36) ? BuddyTooltip.Create(o.Title) : null);
		g.MouseEnter += delegate
		{
			fill.Background = CalendarTool.HoverFill;
		};
		g.MouseLeave += delegate
		{
			fill.Background = Brushes.Transparent;
		};
		CalendarTool.AttachClick(g, delegate
		{
			_tool.OpenOccurrence(o);
		}, delegate
		{
			_tool.OccurrenceMenu(o, g);
		});
		return g;
	}

	private FrameworkElement ReminderLine(UserReminder r, DateTime now)
	{
		Grid g = new Grid
		{
			Background = Brushes.Transparent,
			Margin = new Thickness(-6.0, 0.0, -6.0, 2.0),
			Tag = r
		};
		Border fill = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			IsHitTestVisible = false
		};
		Grid.SetColumnSpan(fill, 4);
		g.Children.Add(fill);
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(24.0)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(66.0)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(30.0)
		});
		bool flag = r.IsOverdue(now);
		FrameworkElement frameworkElement = CalendarTool.Bell(13.0, flag ? CalendarTool.OverdueInk : BuddyTheme.Text50);
		frameworkElement.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Left;
		g.Children.Add(frameworkElement);
		string text = (r.DueTime.HasValue ? Cal.Time(r.DueDate + r.DueTime.Value) : "Anytime");
		TextBlock textBlock = BuddyTheme.Text(text, 11.5, flag ? CalendarTool.OverdueInk : BuddyTheme.Text50, AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		Grid.SetColumn(textBlock, 1);
		g.Children.Add(textBlock);
		bool flag2 = _tool.IsTicking(r.Id);
		TextBlock textBlock2 = BuddyTheme.Text(r.Title, 13.0, flag2 ? BuddyTheme.Text50 : BuddyTheme.Text92, AynType.Medium);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.MaxHeight = 36.0;
		textBlock2.TextTrimming = TextTrimming.WordEllipsis;
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		textBlock2.Margin = new Thickness(0.0, 6.0, 4.0, 6.0);
		if (flag2)
		{
			textBlock2.TextDecorations = TextDecorations.Strikethrough;
		}
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Child = textBlock2;
		Border border2 = border;
		CalendarTool.AttachClick(border2, delegate
		{
			_tool.OpenEditor(null, r, r.DueDate);
		}, delegate
		{
			_tool.ReminderMenu(r, g);
		});
		Grid.SetColumn(border2, 2);
		g.Children.Add(border2);
		g.Children.Add(CheckBox(r, textBlock2, flag2));
		g.MouseEnter += delegate
		{
			fill.Background = CalendarTool.HoverFill;
		};
		g.MouseLeave += delegate
		{
			fill.Background = Brushes.Transparent;
		};
		return g;
	}

	private FrameworkElement CheckBox(UserReminder r, TextBlock title, bool done)
	{
		Grid grid = new Grid();
		grid.Width = 16.0;
		grid.Height = 16.0;
		grid.IsHitTestVisible = false;
		Grid grid2 = grid;
		Rectangle ring = new Rectangle
		{
			RadiusX = 5.0,
			RadiusY = 5.0,
			Stroke = BuddyTheme.Text30,
			StrokeThickness = 1.4,
			Visibility = (done ? Visibility.Collapsed : Visibility.Visible)
		};
		Rectangle fill = new Rectangle
		{
			RadiusX = 5.0,
			RadiusY = 5.0,
			Fill = ThemeTokens.Accent,
			Opacity = (done ? 1 : 0)
		};
		Path check = new Path
		{
			Data = Geometry.Parse("M 4.2,8.4 L 6.9,11 L 11.8,5.6"),
			Stroke = ThemeTokens.AccentFg,
			StrokeThickness = 1.7,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			Opacity = (done ? 1 : 0)
		};
		grid2.Children.Add(ring);
		grid2.Children.Add(fill);
		grid2.Children.Add(check);
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Child = grid2;
		border.Width = 30.0;
		border.Height = 30.0;
		border.HorizontalAlignment = HorizontalAlignment.Center;
		border.ToolTip = BuddyTooltip.Create("Mark done");
		Border border2 = border;
		Action click = delegate
		{
			if (!_tool.IsTicking(r.Id))
			{
				ring.Visibility = Visibility.Collapsed;
				fill.Opacity = 1.0;
				check.Opacity = 1.0;
				title.TextDecorations = TextDecorations.Strikethrough;
				title.Foreground = BuddyTheme.Text50;
				_tool.TickReminder(r);
			}
		};
		border2.MouseEnter += delegate
		{
			ring.Stroke = BuddyTheme.Text92;
		};
		border2.MouseLeave += delegate
		{
			ring.Stroke = BuddyTheme.Text30;
		};
		CalendarTool.AttachClick(border2, click, null);
		CalendarTool.MakeKeyboardButton(border2, click, Brushes.Transparent, CalendarTool.HoverFill);
		Grid.SetColumn(border2, 3);
		return border2;
	}

	private FrameworkElement UpNextLine(CalendarService.UpcomingItem item, DateTime now)
	{
		Grid grid = new Grid();
		grid.Background = Brushes.Transparent;
		grid.Cursor = Cursors.Hand;
		grid.Margin = new Thickness(-6.0, 0.0, -6.0, 0.0);
		grid.Height = 28.0;
		Grid grid2 = grid;
		Border fill = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			IsHitTestVisible = false
		};
		Grid.SetColumnSpan(fill, 4);
		grid2.Children.Add(fill);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(66.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(12.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		DateTime d = item.At.Date;
		string text = ((d == now.Date.AddDays(1.0)) ? "Tomorrow" : d.ToString((d.Month == now.Month && d.Year == now.Year) ? "ddd d" : "d MMM", Cal.Inv));
		TextBlock textBlock = BuddyTheme.Text(text, 11.5, BuddyTheme.Text50, AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		grid2.Children.Add(textBlock);
		Brush fill2 = ((item.Occ == null) ? MonthCell.DotInk("reminder") : (item.Occ.IsHoliday ? MonthCell.DotInk("holiday") : MonthCell.DotInk("event")));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 5.0;
		ellipse.Height = 5.0;
		ellipse.Fill = fill2;
		ellipse.VerticalAlignment = VerticalAlignment.Center;
		ellipse.HorizontalAlignment = HorizontalAlignment.Left;
		Ellipse element = ellipse;
		Grid.SetColumn(element, 1);
		grid2.Children.Add(element);
		TextBlock textBlock2 = BuddyTheme.Text(item.Title, 13.0, BuddyTheme.Text92, AynType.Regular);
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		textBlock2.TextTrimming = TextTrimming.CharacterEllipsis;
		Grid.SetColumn(textBlock2, 2);
		grid2.Children.Add(textBlock2);
		if (!item.AllDay)
		{
			TextBlock textBlock3 = BuddyTheme.Text(Cal.Time(item.At), 11.0, BuddyTheme.Text30, AynType.Regular);
			textBlock3.VerticalAlignment = VerticalAlignment.Center;
			textBlock3.Margin = new Thickness(8.0, 0.0, 6.0, 0.0);
			Grid.SetColumn(textBlock3, 3);
			grid2.Children.Add(textBlock3);
		}
		grid2.ToolTip = BuddyTooltip.Create(item.Title);
		grid2.MouseEnter += delegate
		{
			fill.Background = CalendarTool.HoverFill;
		};
		grid2.MouseLeave += delegate
		{
			fill.Background = Brushes.Transparent;
		};
		CalendarTool.AttachClick(grid2, delegate
		{
			_tool.SelectDay(d);
		}, null);
		return grid2;
	}
}
