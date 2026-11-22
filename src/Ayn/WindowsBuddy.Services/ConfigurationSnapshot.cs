using System;
using System.Collections.Generic;

namespace WindowsBuddy.Services;

public class ConfigurationSnapshot
{
	public string OriginalWallpaperPath { get; set; }

	public string OriginalWallpaperStyle { get; set; }

	public string OriginalTileWallpaper { get; set; }

	public string OriginalCursorSchemeName { get; set; }

	public int OriginalCursorSchemeSource { get; set; }

	public Dictionary<string, string> OriginalCursorScheme { get; set; }

	public int OriginalNativeTaskbarState { get; set; }

	public string AppliedThemeId { get; set; }

	public DateTime CreatedAt { get; set; }
}
