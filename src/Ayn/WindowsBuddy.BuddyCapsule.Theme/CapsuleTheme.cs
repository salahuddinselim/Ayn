using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.BuddyCapsule.Theme;

public class CapsuleTheme
{
	private static CapsuleTheme _current;

	public static readonly CapsuleTheme Obsidian;

	public static CapsuleTheme Current
	{
		get
		{
			if (_current == null)
			{
				_current = Obsidian;
			}
			return _current;
		}
		set
		{
			if (value != null)
			{
				_current = value;
			}
		}
	}

	public string Name { get; set; }

	public Brush Background { get; set; }

	public Brush Foreground { get; set; }

	public Brush SecondaryForeground { get; set; }

	public Brush TertiaryForeground { get; set; }

	public Brush Border { get; set; }

	public Brush ActivePill { get; set; }

	public Brush HoverBackground { get; set; }

	public Brush SubduedSurface { get; set; }

	public Brush ChargingAccent { get; set; }

	public Brush ErrorAccent { get; set; }

	public Brush Accent { get; set; }

	public CornerRadius OuterCornerRadius { get; set; }

	public CornerRadius CardCornerRadius { get; set; }

	public CornerRadius CollapsedCornerRadius { get; set; }

	public CornerRadius InnerElementRadius { get; set; }

	public DropShadowEffect Shadow { get; set; }

	public FontFamily TypographyFontFamily => AynType.Family(TypeSurface.Notch, 12.5);

	public double BaseFontSize { get; set; }

	public Brush IconStroke { get; set; }

	public Thickness ContentPadding { get; set; }

	public Brush SemanticAmber { get; set; }

	public Brush SemanticBlue { get; set; }

	public Brush AccentCyan { get; set; }

	public Brush AccentCyanGlow { get; set; }

	public Brush Divider { get; set; }

	public Brush DockItemHover { get; set; }

	public Brush InputBackground { get; set; }

	public Brush InputBorder { get; set; }

	public Brush CardBackground { get; set; }

	public Brush ActiveDockItemBrush { get; set; }

	public Brush ActiveDockItemBorder { get; set; }

	static CapsuleTheme()
	{
		Brush black = NotchInk.Black;
		Brush primary = NotchInk.Primary;
		Brush secondary = NotchInk.Secondary;
		Brush tertiary = NotchInk.Tertiary;
		Brush hairline = NotchInk.Hairline;
		Brush primary2 = NotchInk.Primary;
		Brush hairlineStrong = NotchInk.HairlineStrong;
		Brush surface = NotchInk.Surface;
		Brush surface2 = NotchInk.Surface;
		Brush primary3 = NotchInk.Primary;
		SolidColorBrush transparent = Brushes.Transparent;
		Brush primary4 = NotchInk.Primary;
		Brush danger = NotchInk.Danger;
		Brush secondary2 = NotchInk.Secondary;
		Brush primary5 = NotchInk.Primary;
		Brush hairlineStrong2 = NotchInk.HairlineStrong;
		Brush hairline2 = NotchInk.Hairline;
		Brush surface3 = NotchInk.Surface2;
		Brush surface4 = NotchInk.Surface;
		Brush hairline3 = NotchInk.Hairline;
		Brush primary6 = NotchInk.Primary;
		Brush secondary3 = NotchInk.Secondary;
		DropShadowEffect dropShadowEffect = new DropShadowEffect
		{
			Color = Colors.Black,
			BlurRadius = 14.0,
			ShadowDepth = 3.0,
			Direction = 270.0,
			Opacity = 0.35
		};
		dropShadowEffect.Freeze();
		Obsidian = new CapsuleTheme
		{
			Name = "Obsidian",
			Background = black,
			Foreground = primary,
			SecondaryForeground = secondary,
			TertiaryForeground = tertiary,
			Border = hairline,
			ActivePill = primary2,
			HoverBackground = hairlineStrong,
			SubduedSurface = surface,
			CardBackground = surface2,
			ActiveDockItemBrush = primary3,
			ActiveDockItemBorder = transparent,
			ChargingAccent = primary4,
			ErrorAccent = danger,
			SemanticAmber = secondary2,
			SemanticBlue = primary5,
			AccentCyan = primary5,
			AccentCyanGlow = hairlineStrong2,
			Divider = hairline2,
			DockItemHover = surface3,
			InputBackground = surface4,
			InputBorder = hairline3,
			Accent = primary6,
			OuterCornerRadius = new CornerRadius(0.0, 0.0, 28.0, 28.0),
			CardCornerRadius = new CornerRadius(16.0),
			CollapsedCornerRadius = new CornerRadius(0.0, 0.0, 16.0, 16.0),
			InnerElementRadius = new CornerRadius(12.0),
			Shadow = dropShadowEffect,
			BaseFontSize = 11.5,
			IconStroke = secondary3,
			ContentPadding = new Thickness(16.0, 0.0, 16.0, 12.0)
		};
		_current = Obsidian;
	}
}
