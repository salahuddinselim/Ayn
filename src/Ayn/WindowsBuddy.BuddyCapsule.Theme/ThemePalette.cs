using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Theme;

public sealed class ThemePalette
{
	public string Id;

	public string DisplayName;

	public bool IsLight;

	public bool UseIconTiles;

	public Color Bg;

	public Color Surface;

	public Color Surface2;

	public Color Surface3;

	public Color Overlay;

	public Color Border;

	public Color BorderStrong;

	public Color Text;

	public Color Text2;

	public Color Text3;

	public Color TextDisabled;

	public Color Icon;

	public Color IconMuted;

	public Color Accent;

	public Color AccentFg;

	public Color AccentSoft;

	public Color Track;

	public Color Bar;

	public Color Chart1;

	public Color Chart2;

	public Color Chart3;

	public Color Chart4;

	public Color CScreen;

	public Color CSystem;

	public Color CWeather;

	public Color CFocus;

	public Color CClipboard;

	public Color CNotes;

	public Color Success;

	public Color Warning;

	public Color Danger;

	public Color Info;

	public Color SuccessSoft;

	public Color WarningSoft;

	public Color DangerSoft;

	public Color InfoSoft;

	public Color Focus;

	public Color AccentStrong;

	public Color CardBorder;

	public bool CardShadow;

	public Color Inverse;

	public Color DockPill;

	public Color Rule;

	public Color PanelRule;

	public Color Selection;

	public Color NavSelectedFill;

	public Color NavHoverFill;

	public Color CardHoverFill;

	public Color CardPressedFill;

	public Color ControlFill;

	public Color ControlFillHover;

	public Color SegmentThumb;

	public Color Ring1;

	public Color Ring2;

	public Color Ring3;

	public Color PlayInk;

	public Color SunInk;

	public Color TileGlyph;

	public Color TileGlyphOnLight;

	public ThemePalette Clone()
	{
		return (ThemePalette)MemberwiseClone();
	}

	internal ThemePalette Finish()
	{
		Inverse = (IsLight ? Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue) : Color.FromRgb(0, 0, 0));
		DockPill = (IsLight ? Color.FromRgb(0, 0, 0) : Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue));
		if (Rule.A == 0)
		{
			Rule = Border;
		}
		PanelRule = (IsLight ? Colors.Transparent : BorderStrong);
		Selection = (IsLight ? Color.FromArgb(15, 0, 0, 0) : Color.FromArgb(31, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		NavSelectedFill = (IsLight ? Color.FromRgb(226, 226, 230) : AccentSoft);
		NavHoverFill = (IsLight ? Color.FromRgb(234, 234, 237) : Surface3);
		CardHoverFill = (IsLight ? Color.FromRgb(247, 247, 249) : Surface3);
		CardPressedFill = (IsLight ? Color.FromRgb(240, 240, 243) : Surface3);
		ControlFill = (IsLight ? Color.FromArgb(15, 0, 0, 0) : Surface2);
		ControlFillHover = (IsLight ? Color.FromArgb(23, 0, 0, 0) : Surface3);
		SegmentThumb = (IsLight ? Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue) : Color.FromRgb(58, 58, 60));
		CardBorder = (IsLight ? Colors.Transparent : Border);
		CardShadow = IsLight;
		if (Id == "EmberDark")
		{
			AccentStrong = Color.FromRgb(194, 65, 12);
		}
		else if (Id == "Vivid")
		{
			AccentStrong = Color.FromRgb(29, 100, 216);
		}
		else
		{
			AccentStrong = Accent;
		}
		if (UseIconTiles)
		{
			Ring1 = Chart1;
			Ring2 = Chart2;
			Ring3 = Chart3;
			PlayInk = CFocus;
			SunInk = CNotes;
		}
		else
		{
			Ring1 = (Ring2 = (Ring3 = Accent));
			PlayInk = Text;
			SunInk = ((Id == "EmberDark") ? Accent : Text);
		}
		TileGlyph = Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue);
		TileGlyphOnLight = Color.FromRgb(29, 29, 31);
		return this;
	}
}
