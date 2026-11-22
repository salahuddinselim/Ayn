using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowsBuddy.UI.Settings;

internal sealed class SCard : Border
{
	public readonly StackPanel Stack = new StackPanel();

	public SCard()
	{
		base.MaxWidth = 616.0;
		base.HorizontalAlignment = HorizontalAlignment.Stretch;
		base.Background = Brushes.Transparent;
		base.BorderThickness = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(0.0);
		base.SnapsToDevicePixels = true;
		Child = Stack;
	}
}
