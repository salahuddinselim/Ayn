using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class CountryRow : Grid
{
	private readonly TextBlock _name;

	private readonly FrameworkElement _check;

	private readonly Border _fill;

	private string _code;

	public CountryRow(CalendarSettingsPanel panel)
	{
		CountryRow countryRow = this;
		base.Background = Brushes.Transparent;
		base.Cursor = Cursors.Hand;
		_fill = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			IsHitTestVisible = false,
			Margin = new Thickness(-8.0, 1.0, 0.0, 1.0)
		};
		base.Children.Add(_fill);
		_name = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, FontWeights.Normal);
		_name.VerticalAlignment = VerticalAlignment.Center;
		base.Children.Add(_name);
		_check = FluentIcons.Create("checkmark", 14.0, BuddyTheme.Text100, filled: false);
		_check.HorizontalAlignment = HorizontalAlignment.Right;
		_check.VerticalAlignment = VerticalAlignment.Center;
		_check.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		base.Children.Add(_check);
		Action click = delegate
		{
			if (countryRow._code != null)
			{
				panel.Pick(countryRow._code);
			}
		};
		CalendarTool.AttachClick(this, click, null);
		base.MouseEnter += delegate
		{
			_fill.Background = CalendarTool.HoverFill;
		};
		base.MouseLeave += delegate
		{
			_fill.Background = Brushes.Transparent;
		};
	}

	public void Bind(string[] c, bool selected)
	{
		_code = c[0];
		_name.Text = c[1];
		_check.Visibility = ((!selected) ? Visibility.Collapsed : Visibility.Visible);
	}
}
