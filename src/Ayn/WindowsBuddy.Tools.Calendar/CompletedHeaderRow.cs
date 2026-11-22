using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class CompletedHeaderRow : Grid
{
	private readonly TextBlock _label;

	private readonly FrameworkElement _chev;

	private readonly Border _clear;

	public CompletedHeaderRow(CalendarTool tool)
	{
		base.Background = Brushes.Transparent;
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand
		};
		_chev = FluentIcons.Create("chevron_down", 12.0, BuddyTheme.Text50, filled: false);
		_chev.VerticalAlignment = VerticalAlignment.Center;
		_chev.RenderTransformOrigin = new Point(0.5, 0.5);
		_chev.Margin = new Thickness(2.0, 0.0, 14.0, 0.0);
		stackPanel.Children.Add(_chev);
		_label = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		_label.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Children.Add(_label);
		CalendarTool.AttachClick(stackPanel, tool.ToggleCompleted, null);
		base.Children.Add(stackPanel);
		_clear = CalendarTool.TextLink("Clear", tool.ClearCompleted);
		Grid.SetColumn(_clear, 1);
		base.Children.Add(_clear);
	}

	public void Bind(int count, bool open)
	{
		_label.Text = "Completed (" + count + ")";
		_chev.RenderTransform = new RotateTransform((!open) ? (-90) : 0);
		_clear.Visibility = ((!open) ? Visibility.Collapsed : Visibility.Visible);
	}
}
