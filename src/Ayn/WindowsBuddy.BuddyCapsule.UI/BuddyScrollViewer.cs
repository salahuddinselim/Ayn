using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyScrollViewer : ScrollViewer
{
	public BuddyScrollViewer()
	{
		OverlayScroll.Install();
		RoutedEventHandler value = delegate
		{
			OverlayScroll.Attach(this);
		};
		base.Loaded += value;
		base.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		base.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		base.Focusable = false;
		base.PanningMode = PanningMode.VerticalOnly;
		base.ClipToBounds = true;
	}
}
