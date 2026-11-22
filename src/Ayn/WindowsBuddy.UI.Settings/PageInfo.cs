using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.UI.Settings;

internal sealed class PageInfo
{
	public string Key;

	public string Title;

	public string Subtitle;

	public string Glyph;

	public string Keywords;

	public Color Top;

	public Color Bottom;

	public int Group;

	public static readonly PageInfo[] All = new PageInfo[13]
	{
		new PageInfo(0, "general", "General", "Startup, shortcut, time and units", "settings_f", "#6E6E76", "#4A4A52", "startup shortcut time units language"),
		new PageInfo(0, "notch", "Notch", "Shape, behavior and live preview", "notch_f", "#48484E", "#2A2A2E", "shape style behavior visibility"),
		new PageInfo(0, "appearance", "Appearance", "Theme, surface and notch effects", "contrast_f", "#5E5CE6", null, "theme dark light silver vivid ember pure black colour color surface blur shadow outline tint wallpaper appearance"),
		new PageInfo(0, "modules", "Modules", "Choose what lives in your notch. Drag to reorder.", "grid_f", "#0A84FF", null, "dock tabs modules"),
		new PageInfo(1, "live", "Live Activities", "Which events may expand the notch", "bell_f", "#FF453A", null, "events alerts"),
		new PageInfo(1, "privacy", "Privacy", "What Ayn keeps on this PC", "hand_f", "#32ADE6", null, "privacy data clear export"),
		new PageInfo(1, "breaks", "Breaks & Meals", "Rest nudges and meal reminders", "bell_f", "#FF9F0A", null, "break rest meal lunch dinner"),
		new PageInfo(1, "salat", "Salat Times", "Prayer times and notifications", "notch_f", "#30D158", null, "salat prayer namaz time"),
		new PageInfo(1, "guard", "Network Guard", "Block adult, betting and ads on your Wi-Fi", "grid_f", "#FF453A", null, "dns wifi block porn betting ads guard"),
		new PageInfo(1, "todo", "To-Do List", "Your tasks, stored on this PC", "contrast_f", "#FF9F0A", null, "todo task tasks list reminder"),
		new PageInfo(1, "wifi", "Wi-Fi QR", "Share your Wi-Fi password with a QR code", "info_f", "#0A84FF", null, "wifi qr share password hotspot"),
		new PageInfo(2, "license", "License & Updates", "Your plan, this PC's activation and updates", "key_f", "#30D158", null, "license key activate pro purchase updates version"),
		new PageInfo(2, "about", "About", "Ayn, the notch for Windows", "info_f", "#0A84FF", null, "about version support")
	};

	public Color Hue => Color.FromRgb((byte)((Top.R + Bottom.R) / 2), (byte)((Top.G + Bottom.G) / 2), (byte)((Top.B + Bottom.B) / 2));

	private PageInfo(int group, string key, string title, string subtitle, string glyph, string top, string bottom, string keywords)
	{
		Group = group;
		Key = key;
		Title = title;
		Subtitle = subtitle;
		Glyph = glyph;
		Keywords = keywords ?? "";
		Color color = SettingsTheme.Hex(top);
		Top = ((bottom == null) ? IconTile.Shift(color, 0.1) : color);
		Bottom = ((bottom == null) ? IconTile.Shift(color, -0.1) : SettingsTheme.Hex(bottom));
	}

	public FrameworkElement Tile(double size)
	{
		return IconTile.Create(Top, Bottom, Glyph, size, Brushes.White);
	}

	public static string Normalize(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return key;
		}
		switch (key.ToLowerInvariant())
		{
		case "updates":
		case "update":
			return "license";
		case "privacy & data":
		case "privacydata":
		case "data":
			return "privacy";
		case "live activities":
		case "liveactivities":
			return "live";
		case "extras":
			return "breaks";
		case "module:converter":
			return "module:units";
		case "module:file_converter":
		case "module:fileconverter":
			return "module:files";
		default:
			return key;
		}
	}

	public static PageInfo Get(string key)
	{
		key = Normalize(key);
		PageInfo[] all = All;
		foreach (PageInfo pageInfo in all)
		{
			if (pageInfo.Key == key)
			{
				return pageInfo;
			}
		}
		return null;
	}
}
