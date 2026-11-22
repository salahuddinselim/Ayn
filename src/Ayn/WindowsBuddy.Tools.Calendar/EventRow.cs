using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class EventRow : Grid
{
	private readonly CalendarTool _tool;

	private readonly TextBlock _time;

	private readonly TextBlock _title;

	private readonly Rectangle _bar;

	private readonly ColumnDefinition _timeCol;

	private CalendarTool.DayItem _item;

	public CalendarTool.DayItem Item => _item;

	public EventRow(CalendarTool tool)
	{
		_tool = tool;
		base.Background = Brushes.Transparent;
		base.Cursor = Cursors.Hand;
		_timeCol = new ColumnDefinition
		{
			Width = new GridLength(80.0)
		};
		base.ColumnDefinitions.Add(_timeCol);
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(3.0)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(12.0)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_time = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		_time.VerticalAlignment = VerticalAlignment.Center;
		_time.TextTrimming = TextTrimming.CharacterEllipsis;
		Typography.SetNumeralAlignment(_time, FontNumeralAlignment.Tabular);
		base.Children.Add(_time);
		_bar = new Rectangle
		{
			Width = 3.0,
			Height = 14.0,
			RadiusX = 1.5,
			RadiusY = 1.5,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(_bar, 1);
		base.Children.Add(_bar);
		_title = BuddyTheme.Text("", 13.5, BuddyTheme.Text92, FontWeights.Normal);
		_title.VerticalAlignment = VerticalAlignment.Center;
		_title.TextTrimming = TextTrimming.CharacterEllipsis;
		Grid.SetColumn(_title, 3);
		base.Children.Add(_title);
		Action click = delegate
		{
			if (_item != null)
			{
				_tool.OpenOccurrence(_item.Occ);
			}
		};
		CalendarTool.AttachClick(this, click, delegate
		{
			if (_item != null)
			{
				_tool.OccurrenceMenu(_item.Occ, this);
			}
		});
		base.MouseEnter += delegate
		{
			_title.Foreground = BuddyTheme.Text100;
		};
		base.MouseLeave += delegate
		{
			_title.Foreground = BuddyTheme.Text92;
		};
	}

	public void Bind(CalendarTool.DayItem item, DateTime now)
	{
		_item = item;
		CalOccurrence occ = item.Occ;
		_timeCol.Width = new GridLength(item.Compact ? 66 : 80);
		string text = (occ.AllDay ? "All day" : ((occ.Start.Date < item.Day) ? ((occ.End.Date > item.Day) ? "All day" : ("until " + Cal.Time(occ.End))) : ((!occ.IsNow(now)) ? Cal.Time(occ.Start) : "Now")));
		_time.Text = text;
		_time.Foreground = (occ.IsNow(now) ? BuddyTheme.Text100 : BuddyTheme.Text50);
		_bar.Fill = CalendarTool.InkOf(occ);
		_title.Text = occ.Title;
		_title.TextWrapping = ((!item.Compact) ? TextWrapping.NoWrap : TextWrapping.Wrap);
		_title.TextTrimming = ((!item.Compact) ? TextTrimming.CharacterEllipsis : TextTrimming.None);
		_title.Foreground = (base.IsMouseOver ? BuddyTheme.Text100 : BuddyTheme.Text92);
		base.Opacity = (occ.IsPast(now) ? 0.42 : 1.0);
		base.ToolTip = ((occ.Title.Length > 40) ? BuddyTooltip.Create(occ.Title) : null);
	}
}
