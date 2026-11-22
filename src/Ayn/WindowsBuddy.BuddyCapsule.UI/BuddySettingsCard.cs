using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddySettingsCard : Border
{
	public static readonly Brush CardBgBrush = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(17, 17, 19)));

	public static readonly Brush CardBorderSolidBrush = ThemeTokens.Border;

	public BuddySettingsCard()
		: this(new Thickness(20.0))
	{
	}

	public BuddySettingsCard(double padding)
		: this(new Thickness(padding))
	{
	}

	public BuddySettingsCard(Thickness padding)
	{
		base.Background = CardBgBrush;
		base.BorderBrush = CardBorderSolidBrush;
		base.BorderThickness = new Thickness(1.0);
		base.CornerRadius = new CornerRadius(14.0);
		base.Padding = padding;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
	}
}
