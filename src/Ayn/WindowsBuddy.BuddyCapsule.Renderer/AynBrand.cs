using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class AynBrand
{
    public static DrawingGroup CreateMark(double size, bool light = false)
    {
        var group = new DrawingGroup();
        using (var ctx = group.Open())
        {
            var bg = light ? new SolidColorBrush(Color.FromRgb(245, 245, 247)) : new SolidColorBrush(Color.FromRgb(14, 14, 16));
            var ink = light ? new SolidColorBrush(Color.FromRgb(20, 20, 24)) : Brushes.White;

            ctx.DrawRoundedRectangle(bg, null, new Rect(0, 0, size, size), size * 0.28, size * 0.28);

            var eyeW = size * 0.20;
            var eyeH = size * 0.34;
            var gap = size * 0.07;
            var cy = size * 0.44;
            var left = size / 2 - gap - eyeW;
            var right = size / 2 + gap;

            ctx.DrawRoundedRectangle(ink, null,
                new Rect(left, cy - eyeH / 2, eyeW, eyeH), eyeW / 2, eyeW / 2);
            ctx.DrawRoundedRectangle(ink, null,
                new Rect(right, cy - eyeH / 2, eyeW, eyeH), eyeW / 2, eyeW / 2);

            if (size >= 48)
            {
                var word = new FormattedText("العين", CultureInfo.InvariantCulture, FlowDirection.RightToLeft,
                    new Typeface("Segoe UI"), size * 0.19, ink)
                {
                    TextAlignment = TextAlignment.Center
                };
                ctx.DrawText(word, new Point((size - word.Width) / 2, size * 0.70));

                var pupil = light ? new SolidColorBrush(Color.FromRgb(245, 245, 247)) : new SolidColorBrush(Color.FromRgb(14, 14, 16));
                var pw = eyeW * 0.34;
                var ph = eyeH * 0.42;
                ctx.DrawRoundedRectangle(pupil, null,
                    new Rect(left + eyeW / 2 - pw / 2, cy - ph / 2, pw, ph), pw / 2, pw / 2);
                ctx.DrawRoundedRectangle(pupil, null,
                    new Rect(right + eyeW / 2 - pw / 2, cy - ph / 2, pw, ph), pw / 2, pw / 2);
            }
        }
        group.Freeze();
        return group;
    }

    public static FrameworkElement CreateLogo(double size, bool light = false)
    {
        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Source = new DrawingImage(CreateMark(size, light))
        };
        return image;
    }

    public static string Subtitle => "Ayn  ·  العين  ·  by SabrWare";
}
