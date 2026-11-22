using System.Windows;
using System.Windows.Controls;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class WidgetCard : BuddyThemeCard
{
	public const double ValueTop = 18.0;

	public const double SecondaryTop = 52.0;

	public const double EmptyTop = 26.0;

	public readonly Grid Body;

	public WidgetCard(string icon, string label, string widget)
	{
		base.BorderThickness = new Thickness(0.0);
		base.Padding = new Thickness(14.0, 14.0, 14.0, 12.0);
		Body = new Grid
		{
			Height = 90.0
		};
		Body.Children.Add(BuddyTheme.WidgetHeader(icon, label, widget));
		Child = Body;
	}

	public static TextBlock Secondary(string text)
	{
		TextBlock textBlock = BuddyTheme.Text(text, 13.0, ThemeTokens.Text, FontWeights.Normal);
		textBlock.Opacity = 0.6;
		return textBlock;
	}

	public static TextBlock Value(string text)
	{
		TextBlock textBlock = BuddyTheme.HeroText(text);
		textBlock.FontFamily = BuddyTheme.DisplayFont;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static void ValueStyle(TextBlock t, bool empty)
	{
		t.FontSize = (empty ? 15.0 : 28.0);
		t.FontWeight = (empty ? FontWeights.Medium : FontWeights.SemiBold);
		t.Opacity = (empty ? 0.85 : 1.0);
		t.LineHeight = (empty ? 20.0 : 28.0);
		t.Margin = new Thickness(0.0, empty ? 26.0 : 18.0, 0.0, 0.0);
	}

	public static TextBlock EmptyTitle(string text)
	{
		TextBlock textBlock = BuddyTheme.Text(text, 15.0, ThemeTokens.Text, FontWeights.Medium);
		textBlock.Opacity = 0.85;
		return textBlock;
	}

	public static TextBlock EmptyHint(string text)
	{
		TextBlock textBlock = BuddyTheme.Text(text, 13.0, ThemeTokens.Text, FontWeights.Normal);
		textBlock.Opacity = 0.55;
		return textBlock;
	}
}
