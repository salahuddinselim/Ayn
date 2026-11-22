using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddySurface
{
	public static Border Card(UIElement child = null, double padding = 12.0)
	{
		Border border = new Border();
		border.Background = BuddyTokens.ToolSurfaceBrush;
		border.CornerRadius = new CornerRadius(16.0);
		border.BorderThickness = new Thickness(0.0);
		border.Padding = new Thickness(padding);
		border.Child = child;
		return border;
	}

	public static Border FloatingRow(UIElement child = null, double height = 44.0)
	{
		Border border = new Border();
		border.Background = BuddyTokens.FloatingSurfaceBrush;
		border.CornerRadius = new CornerRadius(12.0);
		border.BorderThickness = new Thickness(0.0);
		border.Height = height;
		border.Child = child;
		return border;
	}

	public static Border Popover(UIElement child = null, double padding = 8.0)
	{
		Border border = new Border();
		border.Background = BuddyTokens.ElevatedSurfaceBrush;
		border.CornerRadius = new CornerRadius(16.0);
		border.BorderBrush = BuddyTokens.BorderSubtleBrush;
		border.BorderThickness = new Thickness(1.0);
		border.Padding = new Thickness(padding);
		border.Child = child;
		return border;
	}
}
