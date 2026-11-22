using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal static class PetArt
{
	public static readonly Brush White = Paint("#F4F1EA");

	public static readonly Brush Ink = Paint("#101114");

	public static readonly Brush Cyan = Paint("#69C9E8");

	public static readonly Brush Yellow = Paint("#FFD54F");

	public static readonly Brush Red = Paint("#EF6F6C");

	public static readonly Brush Green = Paint("#7BCB7F");

	public static readonly Brush Pink = Paint("#F48FB1");

	public static readonly Brush Wood = Paint("#D09A62");

	public static readonly Brush Orange = Paint("#FFA552");

	public static readonly Brush Board = Paint("#2E4A3E");

	public static double SW = 2.6;

	private static readonly Dictionary<Brush, Dictionary<double, Pen>> pens = new Dictionary<Brush, Dictionary<double, Pen>>();

	private static Brush Paint(string c)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public static Pen Pen(Brush b, double w)
	{
		w = Math.Round(w * 20.0) / 20.0;
		if (!pens.TryGetValue(b, out var value))
		{
			value = new Dictionary<double, Pen>();
			pens.Add(b, value);
		}
		if (!value.TryGetValue(w, out var value2))
		{
			Pen pen = new Pen(b, w);
			pen.StartLineCap = PenLineCap.Round;
			pen.EndLineCap = PenLineCap.Round;
			pen.LineJoin = PenLineJoin.Round;
			value2 = pen;
			value2.Freeze();
			value.Add(w, value2);
		}
		return value2;
	}

	public static void Line(DrawingContext d, Brush b, double x, double y, double xx, double yy, double w = 0.0)
	{
		d.DrawLine(Pen(b, (w <= 0.0) ? SW : w), new Point(x, y), new Point(xx, yy));
	}

	public static void Rect(DrawingContext d, Brush b, double x, double y, double w, double h, double r = 0.0, Brush stroke = null)
	{
		d.DrawRoundedRectangle(b, (stroke == null) ? null : Pen(stroke, SW * 0.8), new Rect(x, y, Math.Max(0.01, w), Math.Max(0.01, h)), r, r);
	}

	public static void Dot(DrawingContext d, Brush b, double x, double y, double r, Brush stroke = null)
	{
		d.DrawEllipse(b, (stroke == null) ? null : Pen(stroke, SW * 0.8), new Point(x, y), r, r);
	}

	public static void Oval(DrawingContext d, Brush b, double x, double y, double rx, double ry, Brush stroke = null)
	{
		d.DrawEllipse(b, (stroke == null) ? null : Pen(stroke, SW * 0.8), new Point(x, y), rx, ry);
	}

	public static void Ring(DrawingContext d, Brush b, double x, double y, double r, double w = 0.0)
	{
		d.DrawEllipse(null, Pen(b, (w <= 0.0) ? SW : w), new Point(x, y), r, r);
	}

	public static void Poly(DrawingContext d, Brush fill, Brush stroke, params double[] xy)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(new Point(xy[0], xy[1]), fill != null, isClosed: true);
			for (int i = 2; i < xy.Length; i += 2)
			{
				streamGeometryContext.LineTo(new Point(xy[i], xy[i + 1]), isStroked: true, isSmoothJoin: true);
			}
		}
		streamGeometry.Freeze();
		d.DrawGeometry(fill, (stroke == null) ? null : Pen(stroke, SW * 0.8), streamGeometry);
	}

	public static void Polyline(DrawingContext d, Brush stroke, double w, params double[] xy)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(new Point(xy[0], xy[1]), isFilled: false, isClosed: false);
			for (int i = 2; i < xy.Length; i += 2)
			{
				streamGeometryContext.LineTo(new Point(xy[i], xy[i + 1]), isStroked: true, isSmoothJoin: true);
			}
		}
		streamGeometry.Freeze();
		d.DrawGeometry(null, Pen(stroke, (w <= 0.0) ? SW : w), streamGeometry);
	}

	public static void Curve(DrawingContext d, Brush stroke, double w, double x0, double y0, double cx, double cy, double x1, double y1)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(new Point(x0, y0), isFilled: false, isClosed: false);
			streamGeometryContext.QuadraticBezierTo(new Point(cx, cy), new Point(x1, y1), isStroked: true, isSmoothJoin: true);
		}
		streamGeometry.Freeze();
		d.DrawGeometry(null, Pen(stroke, (w <= 0.0) ? SW : w), streamGeometry);
	}

	public static void Star(DrawingContext d, Brush b, double x, double y, double r, int points = 5, double inner = 0.45)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			for (int i = 0; i < points * 2; i++)
			{
				double num = -Math.PI / 2.0 + (double)i * Math.PI / (double)points;
				double num2 = ((i % 2 == 0) ? r : (r * inner));
				Point point = new Point(x + Math.Cos(num) * num2, y + Math.Sin(num) * num2);
				if (i == 0)
				{
					streamGeometryContext.BeginFigure(point, isFilled: true, isClosed: true);
				}
				else
				{
					streamGeometryContext.LineTo(point, isStroked: true, isSmoothJoin: true);
				}
			}
		}
		streamGeometry.Freeze();
		d.DrawGeometry(b, null, streamGeometry);
	}

	public static void Zee(DrawingContext d, double x, double y, double size)
	{
		Polyline(d, White, Math.Max(SW, size * 0.28), x, y, x + size, y, x, y + size, x + size, y + size);
	}

	public static void Note(DrawingContext d, Brush b, double x, double y, double size)
	{
		Oval(d, b, x, y, size * 0.42, size * 0.32);
		Line(d, b, x + size * 0.36, y - size * 0.05, x + size * 0.36, y - size, SW * 0.9);
		Line(d, b, x + size * 0.36, y - size, x + size * 0.8, y - size * 0.7, SW * 0.9);
	}

	public static void Drop(DrawingContext d, Brush b, double x, double y, double size)
	{
		Poly(d, b, null, x, y - size, x + size * 0.55, y + size * 0.15, x, y + size * 0.6, x - size * 0.55, y + size * 0.15);
		Dot(d, b, x, y + size * 0.15, size * 0.55);
	}
}
