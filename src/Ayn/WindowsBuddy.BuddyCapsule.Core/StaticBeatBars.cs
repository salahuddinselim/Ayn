using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Core;

internal sealed class StaticBeatBars : FrameworkElement
{
	public enum Pattern
	{
		Rest,
		Still
	}

	private readonly int _n;

	private readonly double _barW;

	private readonly bool _mini;

	private readonly Color _color;

	private Pattern _pattern;

	public Pattern Current => _pattern;

	public StaticBeatBars(int n, double barWidth, bool mini, Color color)
	{
		_n = n;
		_barW = barWidth;
		_mini = mini;
		_color = color;
		base.IsHitTestVisible = false;
		base.SnapsToDevicePixels = false;
	}

	public void SetPattern(Pattern p)
	{
		if (p != _pattern)
		{
			_pattern = p;
			InvalidateVisual();
		}
	}

	private double Level(int i)
	{
		if (_pattern == Pattern.Rest)
		{
			if (!_mini)
			{
				return 0.0;
			}
			return 0.12;
		}
		if (_mini)
		{
			return (new double[4] { 0.55, 0.9, 0.4, 0.7 })[i % 4];
		}
		double num = (double)(_n - 1) / 2.0;
		double num2 = Math.Abs((double)i - num) / num;
		return Math.Max(0.0, 0.55 * Math.Exp((0.0 - num2) * num2 * 4.5) + 0.12 * (0.5 + 0.5 * Math.Sin((double)i * 1.9)) * (1.0 - num2));
	}

	protected override void OnRender(DrawingContext dc)
	{
		double actualWidth = base.ActualWidth;
		double actualHeight = base.ActualHeight;
		double barW = _barW;
		if (actualWidth <= barW || actualHeight <= 0.0 || _n <= 0)
		{
			return;
		}
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext g = streamGeometry.Open())
		{
			double num = (actualWidth - barW) / (double)Math.Max(1, _n - 1);
			double num2 = Math.Max(0.0, actualHeight - barW);
			for (int i = 0; i < _n; i++)
			{
				double x = (double)i * num;
				double num3 = barW + Level(i) * num2;
				double y = (_mini ? ((actualHeight - num3) / 2.0) : (actualHeight - num3));
				AddRounded(g, new Rect(x, y, barW, num3), barW / 2.0);
			}
		}
		streamGeometry.Freeze();
		Brush brush;
		if (_mini)
		{
			brush = new SolidColorBrush(_color);
		}
		else
		{
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.StartPoint = new Point(0.0, 0.0);
			linearGradientBrush.EndPoint = new Point(1.0, 0.0);
			LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(115, _color.R, _color.G, _color.B), 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(_color, 0.5));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(115, _color.R, _color.G, _color.B), 1.0));
			brush = linearGradientBrush2;
		}
		dc.DrawGeometry(brush, null, streamGeometry);
	}

	private static void AddRounded(StreamGeometryContext g, Rect r, double rad)
	{
		rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2.0);
		g.BeginFigure(new Point(r.Left + rad, r.Top), isFilled: true, isClosed: true);
		g.LineTo(new Point(r.Right - rad, r.Top), isStroked: false, isSmoothJoin: false);
		g.ArcTo(new Point(r.Right, r.Top + rad), new Size(rad, rad), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: false, isSmoothJoin: false);
		g.LineTo(new Point(r.Right, r.Bottom - rad), isStroked: false, isSmoothJoin: false);
		g.ArcTo(new Point(r.Right - rad, r.Bottom), new Size(rad, rad), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: false, isSmoothJoin: false);
		g.LineTo(new Point(r.Left + rad, r.Bottom), isStroked: false, isSmoothJoin: false);
		g.ArcTo(new Point(r.Left, r.Bottom - rad), new Size(rad, rad), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: false, isSmoothJoin: false);
		g.LineTo(new Point(r.Left, r.Top + rad), isStroked: false, isSmoothJoin: false);
		g.ArcTo(new Point(r.Left + rad, r.Top), new Size(rad, rad), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: false, isSmoothJoin: false);
	}
}
