using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsBuddy.Tools.Personal;

public sealed class CompanionPetCanvas : FrameworkElement
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(string), typeof(CompanionPetCanvas),
            new PropertyMetadata("ditto", (d, _) => ((CompanionPetCanvas)d).InvalidateVisual()));

    private readonly DispatcherTimer _timer;

    public CompanionPetCanvas()
    {
        Width = 180;
        Height = 150;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Width, Height);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var scale = Math.Min(w / 180.0, h / 150.0);
        var t = Environment.TickCount / 1000.0;
        var bob = Math.Sin(t * 2.6) * 2.4;

        d.PushTransform(new TranslateTransform((w - 180 * scale) / 2, (h - 150 * scale) / 2));
        d.PushTransform(new ScaleTransform(scale, scale));
        d.PushTransform(new TranslateTransform(0, bob));

        switch ((Kind ?? "ditto").ToLowerInvariant())
        {
            case "cat": DrawCat(d, t); break;
            case "husky": DrawHusky(d, t); break;
            case "pikachu": DrawPikachu(d, t); break;
            case "doraemon": DrawDoraemon(d, t); break;
            case "dots": DrawDots(d, t); break;
            case "grok": DrawGrok(d, t); break;
            case "robot": DrawRobot(d, t); break;
            default: DrawDitto(d, t); break;
        }

        d.Pop();
        d.Pop();
        d.Pop();
    }

    private static Brush B(byte r, byte g, byte b, byte a = 255) => new SolidColorBrush(Color.FromArgb(a, r, g, b));
    private static readonly Pen Ink = new(B(38, 30, 48), 2.0);

    private static void Sparkle(DrawingContext d, double x, double y, double s, Color c)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(x, y - s), true, true);
            g.LineTo(new Point(x + s * 0.28, y - s * 0.28), true, true);
            g.LineTo(new Point(x + s, y), true, true);
            g.LineTo(new Point(x + s * 0.28, y + s * 0.28), true, true);
            g.LineTo(new Point(x, y + s), true, true);
            g.LineTo(new Point(x - s * 0.28, y + s * 0.28), true, true);
            g.LineTo(new Point(x - s, y), true, true);
            g.LineTo(new Point(x - s * 0.28, y - s * 0.28), true, true);
        }
        d.DrawGeometry(new SolidColorBrush(c), null, geo);
    }

    private static void Face(DrawingContext d, double cx, double cy, double eyeDx, double eyeR, byte blush)
    {
        d.DrawEllipse(Brushes.White, null, new Point(cx - eyeDx, cy), eyeR, eyeR * 1.12);
        d.DrawEllipse(Brushes.White, null, new Point(cx + eyeDx, cy), eyeR, eyeR * 1.12);
        d.DrawEllipse(B(32, 28, 42), null, new Point(cx - eyeDx, cy + 1), eyeR * 0.52, eyeR * 0.62);
        d.DrawEllipse(B(32, 28, 42), null, new Point(cx + eyeDx, cy + 1), eyeR * 0.52, eyeR * 0.62);
        d.DrawEllipse(Brushes.White, null, new Point(cx - eyeDx - eyeR * 0.22, cy - eyeR * 0.34), eyeR * 0.22, eyeR * 0.24);
        d.DrawEllipse(Brushes.White, null, new Point(cx + eyeDx - eyeR * 0.22, cy - eyeR * 0.34), eyeR * 0.22, eyeR * 0.24);
        d.DrawEllipse(B(255, 130, 150, blush), null, new Point(cx - eyeDx - eyeR * 1.5, cy + eyeR * 1.2), eyeR * 0.7, eyeR * 0.42);
        d.DrawEllipse(B(255, 130, 150, blush), null, new Point(cx + eyeDx + eyeR * 1.5, cy + eyeR * 1.2), eyeR * 0.7, eyeR * 0.42);
        d.DrawGeometry(null, new Pen(B(70, 50, 60), 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            Curved(new Point(cx - 4, cy + eyeR * 1.35), new Point(cx, cy + eyeR * 1.75), new Point(cx + 4, cy + eyeR * 1.35)));
    }

    private static Geometry Curved(Point a, Point mid, Point b)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(a, false, false);
            g.QuadraticBezierTo(mid, b, true, true);
        }
        geo.Freeze();
        return geo;
    }

    private static void DrawCat(DrawingContext d, double t)
    {
        d.DrawEllipse(B(0, 0, 0, 45), null, new Point(90, 132), 46, 7);
        Triangle(d, new Point(53, 62), new Point(50, 20), new Point(82, 51), B(250, 205, 175));
        Triangle(d, new Point(127, 62), new Point(130, 20), new Point(98, 51), B(250, 205, 175));
        Triangle(d, new Point(57, 56), new Point(55, 32), new Point(75, 50), B(255, 165, 175));
        Triangle(d, new Point(123, 56), new Point(125, 32), new Point(105, 50), B(255, 165, 175));
        d.DrawEllipse(B(255, 214, 178), null, new Point(90, 88), 45, 42);
        d.DrawEllipse(B(255, 232, 205), null, new Point(90, 96), 34, 30);
        Face(d, 90, 84, 16, 8, 150);
        d.DrawGeometry(null, new Pen(B(150, 100, 90), 1.2), Curved(new Point(62, 94), new Point(40, 88), new Point(30, 96)));
        d.DrawGeometry(null, new Pen(B(150, 100, 90), 1.2), Curved(new Point(118, 94), new Point(140, 88), new Point(150, 96)));
        Sparkle(d, 38, 36, 5, Color.FromArgb(200, 255, 220, 120));
        Sparkle(d, 146, 46, 4, Color.FromArgb(180, 255, 200, 220));
    }

    private static void DrawHusky(DrawingContext d, double t)
    {
        d.DrawEllipse(B(0, 0, 0, 45), null, new Point(90, 132), 46, 7);
        d.DrawEllipse(B(120, 180, 235), null, new Point(90, 88), 44, 41);
        d.DrawEllipse(B(245, 248, 252), null, new Point(90, 98), 32, 29);
        Triangle(d, new Point(54, 66), new Point(40, 30), new Point(80, 54), B(95, 130, 165));
        Triangle(d, new Point(126, 66), new Point(140, 30), new Point(100, 54), B(95, 130, 165));
        d.DrawEllipse(B(70, 175, 235), null, new Point(76, 80), 6, 7);
        d.DrawEllipse(B(70, 175, 235), null, new Point(104, 80), 6, 7);
        d.DrawEllipse(Brushes.White, null, new Point(74, 77), 2.2, 2.4);
        d.DrawEllipse(Brushes.White, null, new Point(102, 77), 2.2, 2.4);
        d.DrawEllipse(B(250, 160, 170, 130), null, new Point(64, 94), 6, 4);
        d.DrawEllipse(B(250, 160, 170, 130), null, new Point(116, 94), 6, 4);
        d.DrawEllipse(Brushes.White, null, new Point(90, 100), 11, 8);
        d.DrawEllipse(B(35, 35, 45), null, new Point(90, 96), 4, 3);
        d.DrawGeometry(null, new Pen(B(70, 50, 60), 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            Curved(new Point(84, 104), new Point(90, 110), new Point(96, 104)));
        Sparkle(d, 40, 40, 5, Color.FromArgb(190, 200, 235, 255));
        Sparkle(d, 144, 52, 4, Color.FromArgb(170, 255, 220, 180));
    }

    private static void DrawDitto(DrawingContext d, double t)
    {
        var squash = 1.0 + Math.Sin(t * 3.0) * 0.06;
        d.DrawEllipse(B(0, 0, 0, 45), null, new Point(90, 132), 44, 7);
        d.DrawEllipse(B(170, 138, 230), null, new Point(90, 88), 47 * squash, 41 / squash);
        d.DrawEllipse(B(200, 172, 245, 180), null, new Point(78, 70), 16, 10);
        Face(d, 90, 84, 16, 8, 110);
        d.DrawEllipse(B(210, 185, 250), null, new Point(90, 118), 10, 5);
        Sparkle(d, 40, 42, 5, Color.FromArgb(200, 220, 180, 255));
        Sparkle(d, 142, 38, 4, Color.FromArgb(180, 255, 200, 240));
    }

    private static void DrawPikachu(DrawingContext d, double t)
    {
        Triangle(d, new Point(56, 60), new Point(48, 16), new Point(82, 50), B(255, 218, 74));
        Triangle(d, new Point(124, 60), new Point(132, 16), new Point(98, 50), B(255, 218, 74));
        Triangle(d, new Point(51, 24), new Point(47, 8), new Point(64, 22), B(50, 42, 40));
        Triangle(d, new Point(129, 24), new Point(133, 8), new Point(116, 22), B(50, 42, 40));
        d.DrawEllipse(B(0, 0, 0, 45), null, new Point(90, 132), 44, 7);
        d.DrawEllipse(B(255, 218, 74), null, new Point(90, 88), 43, 40);
        Face(d, 90, 82, 15, 7, 170);
        d.DrawEllipse(B(235, 130, 90), null, new Point(66, 98), 6, 4);
        d.DrawEllipse(B(235, 130, 90), null, new Point(114, 98), 6, 4);
        var pen = new Pen(B(170, 115, 45), 8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        d.DrawLine(pen, new Point(128, 104), new Point(158, 82));
        d.DrawLine(pen, new Point(158, 82), new Point(148, 82));
        d.DrawLine(pen, new Point(152, 100), new Point(170, 110));
        Sparkle(d, 36, 44, 5, Color.FromArgb(210, 255, 240, 150));
        Sparkle(d, 146, 34, 4, Color.FromArgb(190, 255, 210, 200));
    }

    private static void DrawDoraemon(DrawingContext d, double t)
    {
        d.DrawEllipse(B(0, 0, 0, 45), null, new Point(90, 132), 46, 7);
        d.DrawEllipse(B(80, 175, 235), null, new Point(90, 86), 45, 43);
        d.DrawEllipse(B(250, 252, 255), null, new Point(90, 98), 34, 30);
        d.DrawEllipse(Brushes.White, null, new Point(76, 74), 12, 15);
        d.DrawEllipse(Brushes.White, null, new Point(104, 74), 12, 15);
        d.DrawEllipse(B(30, 30, 38), null, new Point(79, 78), 4, 6);
        d.DrawEllipse(B(30, 30, 38), null, new Point(101, 78), 4, 6);
        d.DrawEllipse(Brushes.White, null, new Point(77.6, 75.4), 1.6, 1.8);
        d.DrawEllipse(Brushes.White, null, new Point(99.6, 75.4), 1.6, 1.8);
        d.DrawEllipse(B(250, 160, 170, 140), null, new Point(65, 93), 6, 4);
        d.DrawEllipse(B(250, 160, 170, 140), null, new Point(115, 93), 6, 4);
        d.DrawEllipse(B(235, 65, 75), null, new Point(90, 90), 5, 5);
        d.DrawGeometry(null, Ink, Curved(new Point(90, 95), new Point(90, 103), new Point(90, 107)));
        d.DrawEllipse(B(200, 205, 215), null, new Point(90, 120), 17, 10);
        d.DrawGeometry(null, new Pen(B(120, 125, 135), 1.6), Curved(new Point(78, 120), new Point(90, 127), new Point(102, 120)));
        Sparkle(d, 38, 40, 5, Color.FromArgb(200, 160, 220, 255));
        Sparkle(d, 144, 44, 4, Color.FromArgb(180, 255, 210, 220));
    }

    private static void DrawDots(DrawingContext d, double t)
    {
        var colors = new[] { Color.FromRgb(16, 163, 127), Color.FromRgb(70, 130, 220), Color.FromRgb(245, 175, 70), Color.FromRgb(235, 95, 125) };
        for (var i = 0; i < 4; i++)
        {
            var r = 12 + Math.Sin(t * 3.0 + i * 0.85) * 3.5;
            var x = 90 + Math.Cos(i * Math.PI / 2 + t * 0.4) * 18;
            var y = 86 + Math.Sin(i * Math.PI / 2 + t * 0.4) * 14;
            d.DrawEllipse(new SolidColorBrush(colors[i]), null, new Point(x, y), r, r);
            d.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), null, new Point(x - r * 0.3, y - r * 0.35), r * 0.35, r * 0.28);
        }
        Sparkle(d, 42, 40, 4, Color.FromArgb(190, 200, 255, 220));
        Sparkle(d, 142, 42, 4, Color.FromArgb(190, 255, 220, 200));
    }

    private static void DrawGrok(DrawingContext d, double t)
    {
        d.DrawEllipse(B(0, 0, 0, 60), null, new Point(90, 90), 46, 46);
        d.DrawEllipse(B(24, 24, 28), null, new Point(90, 86), 42, 42);
        var pen = new Pen(B(235, 235, 245), 3.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        d.DrawGeometry(null, pen, Curved(new Point(62, 68), new Point(90, 48), new Point(118, 72)));
        d.DrawEllipse(Brushes.White, null, new Point(76, 76), 4.5, 4.5);
        d.DrawEllipse(Brushes.White, null, new Point(104, 76), 4.5, 4.5);
        d.DrawEllipse(B(30, 30, 34), null, new Point(77, 77), 2, 2.4);
        d.DrawEllipse(B(30, 30, 34), null, new Point(105, 77), 2, 2.4);
        d.DrawGeometry(null, new Pen(B(220, 220, 230), 1.8), Curved(new Point(80, 100), new Point(90, 108), new Point(100, 100)));
        Sparkle(d, 40, 40, 5, Color.FromArgb(190, 220, 230, 255));
        Sparkle(d, 144, 46, 4, Color.FromArgb(180, 255, 240, 210));
    }

    private static void DrawRobot(DrawingContext d, double t)
    {
        d.DrawEllipse(B(0, 0, 0, 45), null, new Point(90, 132), 40, 7);
        d.DrawRoundedRectangle(B(236, 239, 246), Ink, new Rect(52, 52, 76, 68), 22, 22);
        d.DrawRoundedRectangle(B(248, 249, 252), null, new Rect(58, 58, 64, 54), 18, 18);
        Face(d, 90, 84, 15, 8, 120);
        d.DrawRoundedRectangle(B(180, 190, 205), null, new Rect(80, 40, 20, 8), 4, 4);
        Sparkle(d, 38, 42, 5, Color.FromArgb(200, 190, 230, 255));
        Sparkle(d, 144, 40, 4, Color.FromArgb(180, 220, 255, 220));
    }

    private static void Triangle(DrawingContext d, Point a, Point b, Point c, Brush fill)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(a, true, true);
            g.LineTo(b, true, true);
            g.LineTo(c, true, true);
        }
        d.DrawGeometry(fill, Ink, geo);
    }
}
