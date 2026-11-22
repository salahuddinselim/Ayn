using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyHero : TextBlock
{
	public BuddyHero()
		: this(string.Empty)
	{
	}

	public BuddyHero(string text)
	{
		base.Text = text;
		base.FontFamily = BuddyTokens.FontDisplay;
		base.FontSize = 28.0;
		base.FontWeight = AynType.Semibold;
		base.Foreground = BuddyTokens.Text100Brush;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		Typography.SetNumeralAlignment(this, FontNumeralAlignment.Tabular);
	}
}
