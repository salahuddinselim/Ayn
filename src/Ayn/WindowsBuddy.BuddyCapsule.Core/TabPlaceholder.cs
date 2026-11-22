using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.BuddyCapsule.Core;

internal sealed class TabPlaceholder : Border
{
	public TabPlaceholder()
	{
		base.Width = 728.0;
		base.Height = 242.0;
		base.Background = Brushes.Transparent;
		base.Tag = "NoOuterScroll";
		base.IsHitTestVisible = false;
		TextBlock textBlock = BuddyTheme.SecondaryText("Loading…");
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Child = textBlock;
	}
}
