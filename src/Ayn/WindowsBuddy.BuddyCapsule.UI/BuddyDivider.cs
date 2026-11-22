using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyDivider : Border
{
	public BuddyDivider(double topBottomMargin = 8.0)
	{
		base.Height = 1.0;
		base.Background = BuddyTokens.HairlineBrush;
		base.Margin = new Thickness(0.0, topBottomMargin, 0.0, topBottomMargin);
		base.HorizontalAlignment = HorizontalAlignment.Stretch;
	}
}
