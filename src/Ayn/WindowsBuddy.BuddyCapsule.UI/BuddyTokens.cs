using System;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyTokens
{
	public const double Space4 = 4.0;

	public const double Space8 = 8.0;

	public const double Space12 = 12.0;

	public const double Space16 = 16.0;

	public const double Space20 = 20.0;

	public const double Space24 = 24.0;

	public const double Space32 = 32.0;

	public const double RadiusCard = 16.0;

	public const double RadiusControl = 12.0;

	public const double RadiusPill = 999.0;

	public const double RadiusWindowBottom = 28.0;

	public const double RadiusCapsule = 28.0;

	public const double RadiusSurface = 16.0;

	public const double HeightHeader = 44.0;

	public const double HeightDock = 60.0;

	public const double HeightControlSmall = 24.0;

	public const double HeightControlDefault = 32.0;

	public const double HeightControlLarge = 40.0;

	public const double HeightListRow = 40.0;

	public const double CollapsedWidth = 216.0;

	public const double CollapsedHeight = 32.0;

	public const double ExpandedWidth = 752.0;

	public const double ExpandedHeight = 352.0;

	public const double FixedCapsuleWidth = 752.0;

	public const double FixedCapsuleHeight = 352.0;

	public const double ExpandedCornerRadius = 28.0;

	public const double FixedViewportHeight = 242.0;

	public const double ShadowMarginX = 24.0;

	public const double ShadowMarginY = 36.0;

	public const double StableEnvelopeWidth = 800.0;

	public const double StableEnvelopeHeight = 388.0;

	public static readonly Color CanvasColor = Color.FromRgb(0, 0, 0);

	public static readonly Color CardColor = Color.FromRgb(12, 12, 14);

	public static readonly Color CardHoverColor = Color.FromRgb(16, 16, 19);

	public static readonly Color RowColor = Color.FromArgb(10, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color HairlineColor = Color.FromArgb(20, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color HairlineHoverColor = Color.FromArgb(41, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color Text100Color = Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color Text80Color = Color.FromArgb(204, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color Text60Color = Color.FromArgb(153, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color Text38Color = Color.FromArgb(97, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color AlertColor = Color.FromRgb(byte.MaxValue, 69, 58);

	public static readonly Color LiveColor = Color.FromRgb(52, 199, 89);

	public static readonly Color MeterTrackColor = Color.FromArgb(26, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Brush CanvasBrush = ThemeTokens.Bg;

	public static readonly Brush CardBrush = ThemeTokens.Surface;

	public static readonly Brush CardHoverBrush = ThemeTokens.Surface2;

	public static readonly Brush RowBrush = ThemeTokens.Surface2;

	public static readonly Brush RowHoverBrush = ThemeTokens.Surface3;

	public static readonly Brush HairlineBrush = ThemeTokens.Border;

	public static readonly Brush HairlineHoverBrush = ThemeTokens.BorderStrong;

	public static readonly Brush Text100Brush = ThemeTokens.Text;

	public static readonly Brush Text80Brush = ThemeTokens.Text;

	public static readonly Brush Text60Brush = ThemeTokens.Text2;

	public static readonly Brush Text38Brush = ThemeTokens.Text3;

	public static readonly Brush AlertBrush = ThemeTokens.Danger;

	public static readonly Brush LiveBrush = ThemeTokens.Success;

	public static readonly Brush MeterTrackBrush = ThemeTokens.Track;

	public static readonly Brush CardBorderBrush = ThemeTokens.CardBorder;

	public static readonly Brush CardBorderHoverBrush = ThemeTokens.BorderStrong;

	public static readonly Brush CardSheenBrush = Brushes.Transparent;

	public static readonly Brush EmptyHourDotBrush = ThemeTokens.Bar;

	public static readonly Brush BarPastBrush = ThemeTokens.Bar;

	public static readonly Color ToolSurfaceColor = CardColor;

	public static readonly Color FloatingSurfaceColor = CardColor;

	public static readonly Color ElevatedSurfaceColor = CardHoverColor;

	public static readonly Color ButtonSecondarySurfaceColor = CardColor;

	public static readonly Color SegmentedSurfaceColor = CardColor;

	public static readonly Color IconButtonHoverColor = CardHoverColor;

	public static readonly Color ToggleTrackInactiveColor = Color.FromRgb(42, 42, 45);

	public static readonly Color ToggleTrackActiveColor = Color.FromRgb(90, 90, 94);

	public static readonly Color InteractiveSurfaceColor = CardHoverColor;

	public static readonly Color HoverSurfaceColor = HairlineHoverColor;

	public static readonly Color PressedSurfaceColor = Color.FromArgb(50, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color ObsidianBaseColor = CanvasColor;

	public static readonly Color BorderSubtleColor = HairlineColor;

	public static readonly Color BorderDefaultColor = HairlineColor;

	public static readonly Color BorderStrongColor = HairlineHoverColor;

	public static readonly Color BorderFocusColor = Color.FromArgb(178, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Color HairlineLightColor = HairlineColor;

	public static readonly Color FocusRingColor = BorderFocusColor;

	public static readonly Color TextDisplayColor = Text100Color;

	public static readonly Color TextPrimaryColor = Text100Color;

	public static readonly Color TextSecondaryColor = Text60Color;

	public static readonly Color TextTertiaryColor = Text38Color;

	public static readonly Color TextMutedColor = Text38Color;

	public static readonly Color TextDisabledColor = Text38Color;

	public static readonly Brush ToolSurfaceBrush = CardBrush;

	public static readonly Brush FloatingSurfaceBrush = CardBrush;

	public static readonly Brush ElevatedSurfaceBrush = CardHoverBrush;

	public static readonly Brush ButtonSecondarySurfaceBrush = CardBrush;

	public static readonly Brush SegmentedSurfaceBrush = CardBrush;

	public static readonly Brush IconButtonHoverBrush = CardHoverBrush;

	public static readonly Brush ToggleTrackInactiveBrush = ThemeTokens.Surface3;

	public static readonly Brush ToggleTrackActiveBrush = ThemeTokens.Accent;

	public static readonly Brush InteractiveSurfaceBrush = CardHoverBrush;

	public static readonly Brush HoverSurfaceBrush = CardHoverBrush;

	public static readonly Brush PressedSurfaceBrush = ThemeTokens.Surface3;

	public static readonly Brush ListRowHoverBrush = RowBrush;

	public static readonly Brush ObsidianBaseBrush = CanvasBrush;

	public static readonly Brush SurfacePrimaryBrush = CardBrush;

	public static readonly Brush SurfaceElevatedBrush = CardHoverBrush;

	public static readonly Brush SurfaceSecondaryBrush = CardHoverBrush;

	public static readonly Brush SurfaceActiveBrush = PressedSurfaceBrush;

	public static readonly Brush BorderSubtleBrush = HairlineBrush;

	public static readonly Brush BorderDefaultBrush = HairlineBrush;

	public static readonly Brush BorderStrongBrush = HairlineHoverBrush;

	public static readonly Brush BorderFocusBrush = ThemeTokens.Focus;

	public static readonly Brush HairlineLightBrush = HairlineBrush;

	public static readonly Brush FocusRingBrush = BorderFocusBrush;

	public static readonly Brush TextDisplayBrush = Text100Brush;

	public static readonly Brush TextPrimaryBrush = Text100Brush;

	public static readonly Brush TextSecondaryBrush = Text60Brush;

	public static readonly Brush TextTertiaryBrush = Text38Brush;

	public static readonly Brush TextMutedBrush = Text38Brush;

	public static readonly Brush TextDisabledBrush = Text38Brush;

	public static readonly Color AccentCyanColor = Text100Color;

	public static readonly Color AccentCyanDimColor = HairlineColor;

	public static readonly Brush AccentCyanBrush = Text100Brush;

	public static readonly Brush AccentCyanDimBrush = HairlineBrush;

	public static readonly Brush DangerRedBrush = AlertBrush;

	public static readonly Brush DestructiveRedBrush = AlertBrush;

	public static readonly Brush AccentCoralBrush = AlertBrush;

	public static readonly Color DangerRedColor = AlertColor;

	public static readonly Color AccentCoralColor = AlertColor;

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Color SuccessGreenColor = Color.FromRgb(52, 199, 89);

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Color SuccessGreenDimColor = Color.FromArgb(28, 52, 199, 89);

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Color WarningAmberColor = Color.FromRgb(byte.MaxValue, 159, 10);

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Color BuddyOrangeColor = Color.FromRgb(242, 106, 33);

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Color BuddyOrangeHighlightColor = Color.FromRgb(byte.MaxValue, 138, 61);

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Color BuddyOrangeDeepColor = Color.FromRgb(217, 79, 11);

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Brush SuccessGreenBrush = ThemeTokens.Success;

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Brush SuccessGreenDimBrush = ThemeTokens.SuccessSoft;

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Brush WarningAmberBrush = ThemeTokens.Warning;

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Brush BuddyOrangeBrush = ThemeTokens.Accent;

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Brush BuddyOrangeHighlightBrush = ThemeTokens.Accent;

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Brush BuddyOrangeDeepBrush = ThemeTokens.Accent;

	[Obsolete("Removed in monochrome redesign. Preserved only for legacy tabs.")]
	public static readonly Brush SignatureOrangeBrush = BuddyOrangeBrush;

	public static FontFamily FontDisplay => AynType.Family(TypeSurface.Capsule, 28.0);

	public static FontFamily FontText => AynType.Family(TypeSurface.Capsule, 13.0);

	public static Brush FreezeBrush(Brush b)
	{
		return ThemeTokens.FreezeSafe(b);
	}
}
