using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.Extras;

namespace WindowsBuddy.Tools.Personal;

public static class GlassShell
{
    private static readonly List<System.WeakReference<Window>> Windows = [];

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
            IsHitTestVisible = false
        };
        System.Windows.Controls.Panel.SetZIndex(overlay, 90);
        root.Children.Add(overlay);
        Windows.Add(new System.WeakReference<Window>(window));
        RefreshAll();
    }

    public static void RefreshAll()
    {
        var cfg = AynExtrasService.Instance.Config;
        foreach (var weak in Windows.ToArray())
        {
            if (!weak.TryGetTarget(out var window) || window.Content is not Panel root) continue;
            foreach (var child in root.Children)
            {
                if (child is not Border overlay || overlay.Tag as string != "ayn-glass-overlay") continue;
                overlay.Background = new SolidColorBrush(Color.FromArgb((byte)(cfg.GlassOuterOpacity * 255), 255, 255, 255));
                overlay.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(cfg.GlassBorderOpacity * 255), 255, 255, 255));
                overlay.BorderThickness = new Thickness(1);
            }
        }
    }
}
