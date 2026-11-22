using System.Collections.Generic;

namespace WindowsBuddy.Core;

public class ThemePackage
{
	public string id { get; set; }

	public string name { get; set; }

	public string version { get; set; }

	public string author { get; set; }

	public string description { get; set; }

	public ThemeMetadata metadata { get; set; }

	public ThemeColors colors { get; set; }

	public ThemeTypography typography { get; set; }

	public ThemeWallpaper wallpaper { get; set; }

	public Dictionary<string, string> cursors { get; set; }

	public ThemeTaskbar taskbar { get; set; }

	public ThemeSystemStatus systemStatus { get; set; }

	public string RootDirectory { get; set; }

	public string ResolvedWallpaperPath { get; set; }

	public Dictionary<string, string> ResolvedCursorPaths { get; set; }

	public ThemePackage()
	{
		cursors = new Dictionary<string, string>();
		ResolvedCursorPaths = new Dictionary<string, string>();
	}
}
