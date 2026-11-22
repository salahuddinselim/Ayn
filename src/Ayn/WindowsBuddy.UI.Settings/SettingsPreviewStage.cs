using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.UI.Settings;

internal static class SettingsPreviewStage
{
	internal static readonly SolidColorBrush Background;

	internal static readonly SolidColorBrush AppSurface;

	static SettingsPreviewStage()
	{
		Background = new SolidColorBrush();
		AppSurface = new SolidColorBrush();
		Refresh();
		ThemeTokens.Subscribe(Background, delegate
		{
			Refresh();
		});
	}

	private static void Refresh()
	{
		string id = ThemeTokens.Current.Id;
		Background.Color = SettingsTheme.Hex(id switch
		{
			"Vivid" => "#141B28", 
			"EmberDark" => "#211C18", 
			"Silver" => "#E7E9ED", 
			_ => "#151517", 
		});
		AppSurface.Color = SettingsTheme.Hex(id switch
		{
			"Vivid" => "#222F44", 
			"EmberDark" => "#30251E", 
			"Silver" => "#F7F8FA", 
			_ => "#232326", 
		});
	}
}
