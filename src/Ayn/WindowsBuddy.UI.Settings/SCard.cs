using System.Windows;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Liquid;
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
		base.Background = ThemeTokens.GlassFill;
		base.BorderBrush = ThemeTokens.GlassEdge;
		base.BorderThickness = new Thickness(1.0);
		base.CornerRadius = new CornerRadius(20.0);
		base.Effect = LiquidGlass.GetShadow();
		base.SnapsToDevicePixels = true;
		Child = new Border
		{
			BorderBrush = ThemeTokens.GlassSheen,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(18.0),
			Background = Brushes.Transparent,
			IsHitTestVisible = true,
			Child = Stack
		};
	}
}
