using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class MiniMonth : StackPanel
{
	private readonly Action<DateTime> _pick;

	private readonly DateTime _selected;

	private DateTime _month;

	private readonly TextBlock _title;

	private readonly UniformGrid _grid = new UniformGrid
	{
		Columns = 7,
		Rows = 6
	};

	public DateTime Month => _month;

	public UniformGrid Days => _grid;

	public MiniMonth(DateTime selected, Action<DateTime> pick)
	{
		_selected = selected.Date;
		_pick = pick;
		_month = new DateTime(selected.Year, selected.Month, 1);
		Grid grid = new Grid
		{
			Height = 28.0,
			Margin = new Thickness(2.0, 0.0, 2.0, 2.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Action click = delegate
		{
			_month = _month.AddMonths(-1);
			Fill();
		};
		Border element = CalendarTool.QuietIcon(-90.0, "Previous month", click);
		grid.Children.Add(element);
		_title = BuddyTheme.Text("", 13.0, BuddyTheme.Text100, AynType.Semibold);
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		_title.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_title, 1);
		grid.Children.Add(_title);
		Border element2 = CalendarTool.QuietIcon(90.0, "Next month", delegate
		{
			_month = _month.AddMonths(1);
			Fill();
		});
		Grid.SetColumn(element2, 2);
		grid.Children.Add(element2);
		base.Children.Add(grid);
		UniformGrid uniformGrid = new UniformGrid
		{
			Columns = 7,
			Height = 16.0
		};
		DateTime dateTime = CalendarTool.WeekStart(_month);
		for (int i = 0; i < 7; i++)
		{
			TextBlock textBlock = BuddyTheme.Text(CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(dateTime.AddDays(i).DayOfWeek).Substring(0, 1), 10.0, BuddyTheme.Text50, AynType.Medium);
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			uniformGrid.Children.Add(textBlock);
		}
		base.Children.Add(uniformGrid);
		_grid.Height = 168.0;
		base.Children.Add(_grid);
		Fill();
	}

	private void Fill()
	{
		_title.Text = _month.ToString("MMMM yyyy", Cal.Inv);
		_grid.Children.Clear();
		DateTime dateTime = CalendarTool.WeekStart(_month);
		DateTime today = DateTime.Today;
		for (int i = 0; i < 42; i++)
		{
			DateTime dateTime2 = dateTime.AddDays(i);
			bool sel = dateTime2 == _selected;
			bool flag = dateTime2 == today;
			bool flag2 = dateTime2.Month == _month.Month;
			Border circle = new Border
			{
				Width = 26.0,
				Height = 26.0,
				CornerRadius = new CornerRadius(13.0),
				Background = (sel ? ThemeTokens.Accent : Brushes.Transparent),
				Cursor = Cursors.Hand,
				Tag = dateTime2
			};
			TextBlock textBlock = BuddyTheme.Text(dateTime2.Day.ToString(Cal.Inv), 11.5, sel ? ThemeTokens.AccentFg : (flag ? ThemeTokens.Accent : BuddyTheme.Text92), (sel || flag) ? AynType.Semibold : AynType.Regular);
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
			circle.Child = textBlock;
			circle.Opacity = (flag2 ? 1.0 : 0.3);
			circle.MouseEnter += delegate
			{
				if (!sel)
				{
					circle.Background = CalendarTool.HoverFill;
				}
			};
			circle.MouseLeave += delegate
			{
				if (!sel)
				{
					circle.Background = Brushes.Transparent;
				}
			};
			DateTime day = dateTime2;
			CalendarTool.AttachClick(circle, delegate
			{
				_pick(day);
			}, null);
			_grid.Children.Add(circle);
		}
	}
}
