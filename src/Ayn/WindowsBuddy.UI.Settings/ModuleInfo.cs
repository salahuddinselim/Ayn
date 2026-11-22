using System.Windows.Media;

namespace WindowsBuddy.UI.Settings;

internal sealed class ModuleInfo
{
	public string Id;

	public string Name;

	public string Description;

	public Color Hue;

	public string Glyph;

	public string Keywords;

	public string Category;

	public ModuleInfo(string id, string name, string description, string hue, string glyph, string keywords, string category = "Productivity")
	{
		Id = id;
		Name = name;
		Description = description;
		Hue = SettingsTheme.Hex(hue);
		Glyph = glyph;
		Keywords = keywords ?? "";
		Category = category ?? "Productivity";
	}
}
