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

    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var t = Environment.TickCount / 1000.0;
        var bob = Math.Sin(t * 2.2) * 3.0;
        d.PushTransform(new TranslateTransform(0, bob));

        switch ((Kind ?? "ditto").ToLowerInvariant())
        {
            case "cat": DrawCat(d); break;
            case "husky": DrawHusky(d); break;
            case "pikachu": DrawPikachu(d); break;
            case "doraemon": DrawDoraemon(d); break;
            case "dots": DrawDots(d, t); break;
            case "grok": DrawGrok(d, t); break;
            case "robot": DrawRobot(d, t); break;
            default: DrawDitto(d, t); break;
        }
        d.Pop();
    }

    private static Brush B(byte r, byte g, byte b, byte a = 255) => new SolidColorBrush(Color.FromArgb(a, r, g, b));

    private static readonly Pen ink = new Pen(B(24, 24, 28), 1.5);

    private void DrawCat(DrawingContext d)
    {
        var body = B(238, 238, 244);
        var ink = B(28, 28, 32);
        d.DrawEllipse(B(0, 0, 0, 40), null, new Point(90, 134), 42, 6);
        d.DrawEllipse(body, null, new Point(90, 88), 38, 36);
        Triangle(d, new Point(58, 62), new Point(62, 24), new Point(86, 52), body);
        Triangle(d, new Point(122, 62), new Point(118, 24), new Point(94, 52), body);
        d.DrawEllipse(Brushes.White, null, new Point(76, 82), 7, 9);
        d.DrawEllipse(Brushes.White, null, new Point(104, 82), 7, 9);
        d.DrawEllipse(ink, null, new Point(77, 84), 3, 5);
        d.DrawEllipse(ink, null, new Point(103, 84), 3, 5);
        Triangle(d, new Point(90, 94), new Point(84, 94), new Point(90, 99), B(240, 130, 150));
        var pen = new Pen(ink, 1.2);
        d.DrawLine(pen, new Point(58, 96), new Point(30, 90));
        d.DrawLine(pen, new Point(58, 100), new Point(32, 104));
        d.DrawLine(pen, new Point(122, 96), new Point(150, 90));
        d.DrawLine(pen, new Point(122, 100), new Point(148, 104));
    }

    private void DrawHusky(DrawingContext d)
    {
        var fur = B(232, 236, 242);
        var dark = B(74, 88, 104);
        d.DrawEllipse(B(0, 0, 0, 40), null, new Point(90, 134), 44, 6);
        d.DrawEllipse(dark, null, new Point(90, 86), 40, 38);
        d.DrawEllipse(fur, null, new Point(90, 94), 30, 28);
        Triangle(d, new Point(56, 66), new Point(42, 30), new Point(78, 56), dark);
        Triangle(d, new Point(124, 66), new Point(138, 30), new Point(102, 56), dark);
        d.DrawEllipse(B(60, 170, 230), null, new Point(76, 82), 5, 6);
        d.DrawEllipse(B(60, 170, 230), null, new Point(104, 82), 5, 6);
        d.DrawEllipse(Brushes.White, null, new Point(90, 100), 10, 7);
        d.DrawEllipse(B(24, 24, 28), null, new Point(90, 97), 4, 3);
    }

    private void DrawDitto(DrawingContext d, double t)
    {
        var squash = 1.0 + Math.Sin(t * 3.2) * 0.05;
        var jelly = B(154, 122, 214, 240);
        d.DrawEllipse(B(0, 0, 0, 40), null, new Point(90, 134), 40, 6);
        d.DrawEllipse(jelly, null, new Point(90, 90), 44 * squash, 38 / squash);
        d.DrawEllipse(B(255, 255, 255, 180), null, new Point(76, 74), 7, 9);
        d.DrawEllipse(B(255, 255, 255, 180), null, new Point(104, 74), 7, 9);
        d.DrawEllipse(B(30, 24, 46), null, new Point(77, 76), 3, 4);
        d.DrawEllipse(B(30, 24, 46), null, new Point(103, 76), 3, 4);
        d.DrawEllipse(B(255, 255, 255, 90), null, new Point(74, 68), 13, 7);
    }

    private void DrawPikachu(DrawingContext d)
    {
        var yellow = B(255, 214, 62);
        var ink = B(30, 30, 30);
        Triangle(d, new Point(58, 62), new Point(50, 18), new Point(82, 52), yellow);
        Triangle(d, new Point(122, 62), new Point(130, 18), new Point(98, 52), yellow);
        Triangle(d, new Point(52, 26), new Point(50, 12), new Point(64, 24), ink);
        Triangle(d, new Point(128, 26), new Point(130, 12), new Point(116, 24), ink);
        d.DrawEllipse(yellow, null, new Point(90, 90), 40, 38);
        d.DrawEllipse(ink, null, new Point(76, 82), 4, 5);
        d.DrawEllipse(ink, null, new Point(104, 82), 4, 5);
        d.DrawEllipse(B(236, 110, 90), null, new Point(66, 98), 6, 4);
        d.DrawEllipse(B(236, 110, 90), null, new Point(114, 98), 6, 4);
        var pen = new Pen(B(160, 110, 40), 8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        d.DrawLine(pen, new Point(128, 104), new Point(156, 84));
        d.DrawLine(pen, new Point(156, 84), new Point(146, 84));
        d.DrawLine(pen, new Point(150, 100), new Point(168, 108));
    }

    private void DrawDoraemon(DrawingContext d)
    {
        var blue = B(64, 164, 224);
        d.DrawEllipse(blue, null, new Point(90, 88), 44, 42);
        d.DrawEllipse(Brushes.White, null, new Point(90, 100), 32, 28);
        d.DrawEllipse(Brushes.White, null, new Point(76, 74), 11, 14);
        d.DrawEllipse(Brushes.White, null, new Point(104, 74), 11, 14);
        d.DrawEllipse(B(24, 24, 28), null, new Point(79, 78), 3, 5);
        d.DrawEllipse(B(24, 24, 28), null, new Point(101, 78), 3, 5);
        d.DrawEllipse(B(230, 54, 60), null, new Point(90, 90), 5, 5);
        var pen = new Pen(B(24, 24, 28), 1.5);
        d.DrawLine(pen, new Point(90, 95), new Point(90, 104));
        Arc(d, ink2(), new Point(74, 104), new Point(106, 104), 8);
        d.DrawEllipse(B(200, 200, 210), null, new Point(90, 120), 16, 9);
    }

    private static Pen ink2(double width = 1.5) => new Pen(B(24, 24, 28), width);

    private void DrawDots(DrawingContext d, double t)
    {
        var colors = new[] { B(16, 163, 127), B(70, 130, 220), B(240, 170, 60), B(230, 90, 110) };
        for (var i = 0; i < 4; i++)
        {
            var scale = 8 + Math.Sin(t * 3.0 + i * 0.9) * 2.5;
            d.DrawEllipse(colors[i], null, new Point(90, 90), scale, scale);
        }
    }

    private void DrawGrok(DrawingContext d, double t)
    {
        var black = B(18, 18, 20);
        d.DrawEllipse(black, null, new Point(90, 90), 40, 40);
        var pen = new Pen(B(235, 235, 240), 3.2);
        Arc(d, pen, new Point(62, 70), new Point(118, 110), 26);
        d.DrawEllipse(B(255, 255, 255), null, new Point(78, 76), 4, 4);
    }

    private void DrawRobot(DrawingContext d, double t)
    {
        var shell = B(236, 238, 244);
        d.DrawRoundedRectangle(shell, ink2(2), new Rect(54, 56, 72, 64), 20, 20);
        d.DrawEllipse(Brushes.White, null, new Point(76, 84), 7, 9);
        d.DrawEllipse(Brushes.White, null, new Point(104, 84), 7, 9);
        d.DrawEllipse(B(20, 20, 24), null, new Point(77 + Math.Sin(t * 2) * 2, 86), 3, 4);
        d.DrawEllipse(B(20, 20, 24), null, new Point(103 + Math.Sin(t * 2) * 2, 86), 3, 4);
    }

    private static void Arc(DrawingContext d, Pen pen, Point a, Point b, double radius)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(a, false, false);
            ctx.ArcTo(b, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
        }
        geo.Freeze();
        d.DrawGeometry(null, pen, geo);
    }

    private static void Triangle(DrawingContext d, Point a, Point b, Point c, Brush fill)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(a, true, true);
            ctx.LineTo(b, true, true);
            ctx.LineTo(c, true, true);
        }
        geo.Freeze();
        d.DrawGeometry(fill, null, geo);
    }
}
