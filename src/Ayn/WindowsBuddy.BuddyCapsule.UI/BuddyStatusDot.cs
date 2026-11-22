using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyStatusDot
{
	public static Ellipse Create(Brush fill, double size = 5.0)
	{
		Ellipse ellipse = new Ellipse();
		ellipse.Width = size;
		ellipse.Height = size;
		ellipse.Fill = fill;
		ellipse.VerticalAlignment = VerticalAlignment.Center;
		ellipse.HorizontalAlignment = HorizontalAlignment.Center;
		return ellipse;
	}

	public static StackPanel CreateWithLabel(string label, Brush fill, double dotSize = 5.0, double fontSize = 10.0)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(Create(fill, dotSize));
		stackPanel2.Children.Add(new TextBlock
		{
			Text = label,
			FontSize = fontSize,
			FontWeight = AynType.Medium,
			Foreground = fill,
			FontFamily = BuddyTypography.FontText,
			Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		return stackPanel2;
	}
}
