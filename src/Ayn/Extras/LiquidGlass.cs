using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.Liquid;

public static class LiquidGlass
{
    private static readonly DropShadowEffect Shadow = CreateShadow();
    private static readonly DropShadowEffect SmallShadow = CreateShadow(10, 4, 0.16);

    public static void ApplyTo(Border card, bool nested = true)
    {
        card.Background = ThemeTokens.GlassFill;
        card.BorderBrush = ThemeTokens.GlassEdge;
        card.BorderThickness = new Thickness(1);
        card.CornerRadius = new CornerRadius(20);
        card.Effect = Shadow;
        if (!nested || card.Child is not FrameworkElement child) return;

        var inner = new Border
        {
            BorderBrush = ThemeTokens.GlassSheen,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            Child = child
        };
        card.Child = inner;
    }

    public static void ApplyPopup(Border border)
    {
        border.Background = ThemeTokens.GlassFill;
        border.BorderBrush = ThemeTokens.GlassEdge;
        border.BorderThickness = new Thickness(1);
        border.CornerRadius = new CornerRadius(22);
        border.Effect = Shadow;
    }

    public static DropShadowEffect GetShadow() => Shadow;

    public static DropShadowEffect GetSmallShadow() => SmallShadow;

    private static DropShadowEffect CreateShadow(double blur = 18, double depth = 6, double opacity = 0.22)
    {
        var effect = new DropShadowEffect
        {
            BlurRadius = blur,
            ShadowDepth = depth,
            Direction = 270,
            Color = Color.FromArgb(120, 0, 0, 0),
            Opacity = opacity,
            RenderingBias = RenderingBias.Performance
        };
        effect.Freeze();
        return effect;
    }
}
