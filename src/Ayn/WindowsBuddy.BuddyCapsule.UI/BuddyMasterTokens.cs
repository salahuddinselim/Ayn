using System;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

[Obsolete("Use BuddyTokens instead. BuddyMasterTokens has been merged into BuddyTokens.")]
public static class BuddyMasterTokens
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

	public static readonly Brush SurfaceCanvas = BuddyTokens.CanvasBrush;

	public static readonly Brush SurfaceCard = BuddyTokens.CardBrush;

	public static readonly Brush SurfaceFloating = BuddyTokens.CardBrush;

	public static readonly Brush SurfaceElevated = BuddyTokens.CardHoverBrush;

	public static readonly Brush SurfaceSecondary = BuddyTokens.CardBrush;

	public static readonly Brush Hover = BuddyTokens.HairlineHoverBrush;

	public static readonly Brush Pressed = BuddyTokens.PressedSurfaceBrush;

	public static readonly Brush Hairline = BuddyTokens.HairlineBrush;

	public static readonly Brush Focus = BuddyTokens.FocusRingBrush;

	public static readonly Brush TextPrimary = BuddyTokens.Text100Brush;

	public static readonly Brush TextSecondary = BuddyTokens.Text60Brush;

	public static readonly Brush TextMuted = BuddyTokens.Text38Brush;
}
