using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Extras;

namespace WindowsBuddy.Tools.Personal;

public static class GlassShell
{
    private static readonly List<System.WeakReference<Window>> Windows = [];
    private static readonly SolidColorBrush Fill = new(Colors.Transparent);
    private static readonly SolidColorBrush Edge = new(Colors.Transparent);

    public static void Apply(Window window, Panel root)
    {
        if (window == null || root == null) return;
        const string tag = "ayn-glass-overlay";
        foreach (var child in root.Children)
            if (child is FrameworkElement fe && fe.Tag as string == tag) return;

        var overlay = new Border
        {
            Tag = tag,
            Margin = new Thickness(6),
            CornerRadius = new CornerRadius(36),
            IsHitTestVisible = false,
            Background = Fill,
            BorderBrush = Edge,
            BorderThickness = new Thickness(1)
        };
        System.Windows.Controls.Panel.SetZIndex(overlay, 90);
        root.Children.Add(overlay);
        Windows.Add(new System.WeakReference<Window>(window));
        RefreshAll();
    }

    public static void RefreshAll()
    {
        var cfg = AynExtrasService.Instance.Config;
        var source = ThemeTokens.Target(ThemeTokens.GlassFill);
        var edge = ThemeTokens.Target(ThemeTokens.GlassEdge);
        Fill.Color = Color.FromArgb(
            (byte)(source.A * cfg.GlassOuterOpacity / 0.08), source.R, source.G, source.B);
        Edge.Color = Color.FromArgb(
            (byte)Math.Min(255, edge.A * cfg.GlassBorderOpacity / 0.2), edge.R, edge.G, edge.B);

        for (var i = Windows.Count - 1; i >= 0; i--)
            if (!Windows[i].TryGetTarget(out _)) Windows.RemoveAt(i);
    }
}
