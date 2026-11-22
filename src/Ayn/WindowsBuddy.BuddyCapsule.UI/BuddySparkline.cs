using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddySparkline : FrameworkElement
{
	private static readonly Pen LinePen = CreatePen();

	private readonly int _slots;

	private readonly double _fixedMax;

	private readonly double _autoFloor;

	private readonly double _heightUse;

	private double[] _values = new double[0];

	private Geometry _line;

	private Geometry _area;

	private Size _builtFor;

	private static readonly Brush AreaFade = CreateFade();

	public BuddySparkline(int slots, double fixedMax, double autoFloor, double heightUse)
	{
		_slots = Math.Max(2, slots);
		_fixedMax = fixedMax;
		_autoFloor = autoFloor;
		_heightUse = Math.Max(0.1, Math.Min(1.0, heightUse));
		base.IsHitTestVisible = false;
		base.SnapsToDevicePixels = true;
	}

	public void SetValues(double[] values)
	{
		_values = values ?? new double[0];
		_line = null;
		InvalidateVisual();
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		_line = null;
	}

	protected override void OnRender(DrawingContext dc)
	{
		double actualWidth = base.ActualWidth;
		double actualHeight = base.ActualHeight;
		if (!(actualWidth < 2.0) && !(actualHeight < 2.0))
		{
			if (_line == null || _builtFor.Width != actualWidth || _builtFor.Height != actualHeight)
			{
				Build(actualWidth, actualHeight);
			}
			if (_area != null)
			{
				dc.PushOpacityMask(AreaFade);
				dc.DrawGeometry(BuddyTheme.SparkFill, null, _area);
				dc.Pop();
			}
			if (_line != null)
			{
				dc.DrawGeometry(null, LinePen, _line);
			}
		}
	}

	private static Brush CreateFade()
	{
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(Color.FromArgb(byte.MaxValue, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), new Point(0.0, 0.0), new Point(0.0, 1.0));
		linearGradientBrush.Freeze();
		return linearGradientBrush;
	}

	private void Build(double w, double h)
	{
		_builtFor = new Size(w, h);
		double[] values = _values;
		int num = Math.Min(values.Length, _slots);
		int num2 = values.Length - num;
		double num3 = _fixedMax;
		if (num3 <= 0.0)
		{
			num3 = _autoFloor;
			for (int i = 0; i < num; i++)
			{
				if (values[num2 + i] > num3)
				{
					num3 = values[num2 + i];
				}
			}
			num3 *= 1.1;
		}
		double num4 = 0.75;
		double num5 = h - num4;
		double num6 = (h - 1.5) * _heightUse;
		double num7 = w / (double)(_slots - 1);
		Point[] array = new Point[Math.Max(num, 2)];
		switch (num)
		{
		case 0:
		{
			ref Point reference2 = ref array[0];
			reference2 = new Point(0.0, num5);
			ref Point reference3 = ref array[1];
			reference3 = new Point(w, num5);
			break;
		}
		case 1:
		{
			double y = Y(values[num2], num3, num5, num6);
			ref Point reference4 = ref array[0];
			reference4 = new Point(w - num7, y);
			ref Point reference5 = ref array[1];
			reference5 = new Point(w, y);
			break;
		}
		default:
		{
			for (int j = 0; j < num; j++)
			{
				double x = w - (double)(num - 1 - j) * num7;
				ref Point reference = ref array[j];
				reference = new Point(x, Y(values[num2 + j], num3, num5, num6));
			}
			break;
		}
		}
		double top = num5 - num6;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext ctx = streamGeometry.Open())
		{
			Trace(ctx, array, top, num5, closeArea: false, h);
		}
		streamGeometry.Freeze();
		StreamGeometry streamGeometry2 = new StreamGeometry();
		using (StreamGeometryContext ctx2 = streamGeometry2.Open())
		{
			Trace(ctx2, array, top, num5, closeArea: true, h);
		}
		streamGeometry2.Freeze();
		_line = streamGeometry;
		_area = streamGeometry2;
	}

	private static double Y(double value, double max, double baseline, double usable)
	{
		if (double.IsNaN(value) || value < 0.0)
		{
			value = 0.0;
		}
		double num = ((max > 0.0) ? Math.Min(1.0, value / max) : 0.0);
		return baseline - num * usable;
	}

	private static void Trace(StreamGeometryContext ctx, Point[] p, double top, double baseline, bool closeArea, double bottom)
	{
		ctx.BeginFigure(p[0], closeArea, closeArea);
		for (int i = 0; i < p.Length - 1; i++)
		{
			Point point = p[Math.Max(0, i - 1)];
			Point point2 = p[i];
			Point point3 = p[i + 1];
			Point point4 = p[Math.Min(p.Length - 1, i + 2)];
			Point point5 = new Point(point2.X + (point3.X - point.X) / 6.0, Clamp(point2.Y + (point3.Y - point.Y) / 6.0, top, baseline));
			Point point6 = new Point(point3.X - (point4.X - point2.X) / 6.0, Clamp(point3.Y - (point4.Y - point2.Y) / 6.0, top, baseline));
			ctx.BezierTo(point5, point6, point3, isStroked: true, isSmoothJoin: true);
		}
		if (closeArea)
		{
			ctx.LineTo(new Point(p[p.Length - 1].X, bottom), isStroked: false, isSmoothJoin: false);
			ctx.LineTo(new Point(p[0].X, bottom), isStroked: false, isSmoothJoin: false);
		}
	}

	private static double Clamp(double v, double lo, double hi)
	{
		if (!(v < lo))
		{
			if (!(v > hi))
			{
				return v;
			}
			return hi;
		}
		return lo;
	}

	private static Pen CreatePen()
	{
		Pen pen = new Pen(BuddyTheme.SparkLine, 1.5);
		pen.LineJoin = PenLineJoin.Round;
		pen.StartLineCap = PenLineCap.Round;
		pen.EndLineCap = PenLineCap.Round;
		return pen;
	}
}
