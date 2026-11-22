using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.UI.Settings;

internal static class Keycap
{
	public static Border Create(string text, bool dim)
	{
		TextBlock textBlock = SettingsTheme.Text(text, 13.5, dim ? SettingsTheme.TextSecondary : SettingsTheme.TextPrimary, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		Border border = new Border();
		border.Height = 24.0;
		border.MinWidth = 24.0;
		border.Padding = new Thickness(8.0, 0.0, 8.0, 0.0);
		border.CornerRadius = new CornerRadius(5.0);
		border.Background = SettingsTheme.ControlBg;
		border.Child = textBlock;
		border.VerticalAlignment = VerticalAlignment.Center;
		return border;
	}
}
