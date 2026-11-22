using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.Tools.Weather;

public static class WeatherGlyphs
{
	public enum Kind
	{
		Clear,
		PartlyCloudy,
		Cloudy,
		Fog,
		Drizzle,
		Rain,
		Thunder,
		Snow
	}

	private sealed class Pen24
	{
		public readonly double K;

		public readonly Pen Pen;

		public readonly GeometryGroup Strokes = new GeometryGroup();

		public readonly GeometryGroup Fills = new GeometryGroup();

		public Pen24(double size, double stroke)
		{
			K = size / 24.0;
			Pen = new Pen(Brushes.Black, stroke)
			{
				StartLineCap = PenLineCap.Round,
				EndLineCap = PenLineCap.Round,
				LineJoin = PenLineJoin.Round
			};
		}

		public Point P(double x, double y)
		{
			return new Point(x * K, y * K);
		}

		public void Line(double x1, double y1, double x2, double y2)
		{
			Strokes.Children.Add(new LineGeometry(P(x1, y1), P(x2, y2)));
		}

		public void Circle(double cx, double cy, double r)
		{
			Strokes.Children.Add(new EllipseGeometry(P(cx, cy), r * K, r * K));
		}

		public void Dot(double cx, double cy, double r)
		{
			double num = Math.Max(r * K, Pen.Thickness * 0.85);
			Fills.Children.Add(new EllipseGeometry(P(cx, cy), num, num));
		}

		public Geometry Scaled(string d)
		{
			return Geometry.Parse(Number.Replace(d, (Match m) => (double.Parse(m.Value, CultureInfo.InvariantCulture) * K).ToString("0.###", CultureInfo.InvariantCulture)));
		}

		public void Path(string d)
		{
			Strokes.Children.Add(Scaled(d));
		}

		public Geometry Outline(Geometry g)
		{
			return g.GetWidenedPathGeometry(Pen);
		}
	}

	private static readonly Dictionary<string, Geometry> Cache = new Dictionary<string, Geometry>();

	private static readonly Regex Number = new Regex("-?\\d+(\\.\\d+)?", RegexOptions.Compiled);

	public static Kind FromCode(int code)
	{
		switch (code)
		{
		case 0:
			return Kind.Clear;
		case 1:
		case 2:
			return Kind.PartlyCloudy;
		case 45:
		case 48:
			return Kind.Fog;
		case 51:
		case 52:
		case 53:
		case 54:
		case 55:
		case 56:
		case 57:
			return Kind.Drizzle;
		default:
			if ((code >= 61 && code <= 67) || (code >= 80 && code <= 82))
			{
				return Kind.Rain;
			}
			if (code < 71 || code > 77)
			{
				switch (code)
				{
				case 85:
				case 86:
					break;
				case 95:
				case 96:
				case 97:
				case 98:
				case 99:
					return Kind.Thunder;
				default:
					return Kind.Cloudy;
				}
			}
			return Kind.Snow;
		}
	}

	public static FrameworkElement Create(int code, bool day, double size, Brush ink, double stroke = -1.0)
	{
		Kind kind = FromCode(code);
		bool flag = !day && (kind == Kind.Clear || kind == Kind.PartlyCloudy);
		if (stroke <= 0.0)
		{
			stroke = ((size >= 40.0) ? 1.5 : ((size >= 24.0) ? 1.4 : 1.25));
		}
		string key = string.Concat(kind, flag ? "n" : "d", "|", size.ToString(CultureInfo.InvariantCulture), "|", stroke.ToString(CultureInfo.InvariantCulture));
		if (!Cache.TryGetValue(key, out var value))
		{
			value = Build(kind, flag, size, stroke);
			Cache[key] = value;
		}
		return Wrap(value, size, ink);
	}

	public static FrameworkElement Symbol(string name, double size, Brush ink)
	{
		double stroke = ((size >= 30.0) ? 1.5 : ((size >= 14.0) ? 1.3 : 1.2));
		string key = "sym:" + name + "|" + size.ToString(CultureInfo.InvariantCulture);
		if (!Cache.TryGetValue(key, out var value))
		{
			value = BuildSymbol(name, size, stroke);
			Cache[key] = value;
		}
		return Wrap(value, size, ink);
	}

	private static FrameworkElement Wrap(Geometry geo, double size, Brush ink)
	{
		Path path = new Path();
		path.Data = geo;
		path.Fill = ink;
		path.Width = size;
		path.Height = size;
		path.Stretch = Stretch.None;
		path.IsHitTestVisible = false;
		Path path2 = path;
		RenderOptions.SetEdgeMode(path2, EdgeMode.Unspecified);
		return path2;
	}

	private static Geometry CloudArea(Pen24 g, double ox, double oy, double s)
	{
		Func<double, double, double, Geometry> func = (double cx, double cy, double r) => new EllipseGeometry(g.P(ox + cx * s, oy + cy * s), r * s * g.K, r * s * g.K);
		Geometry geometry = func(5.0, 7.0, 4.0);
		geometry = Geometry.Combine(geometry, func(10.0, 5.6, 5.4), GeometryCombineMode.Union, null);
		geometry = Geometry.Combine(geometry, func(14.5, 8.0, 3.0), GeometryCombineMode.Union, null);
		return Geometry.Combine(geometry, new RectangleGeometry(new Rect(g.P(ox + 5.0 * s, oy + 7.0 * s), g.P(ox + 14.5 * s, oy + 11.0 * s))), GeometryCombineMode.Union, null);
	}

	private static void Sun(Pen24 g, double cx, double cy, double r, double r0, double r1)
	{
		g.Circle(cx, cy, r);
		for (int i = 0; i < 8; i++)
		{
			double num = (double)i * Math.PI / 4.0;
			g.Line(cx + Math.Cos(num) * r0, cy + Math.Sin(num) * r0, cx + Math.Cos(num) * r1, cy + Math.Sin(num) * r1);
		}
	}

	private static Geometry MoonArea(Pen24 g, double cx, double cy, double r)
	{
		EllipseGeometry geometry = new EllipseGeometry(g.P(cx, cy), r * g.K, r * g.K);
		EllipseGeometry geometry2 = new EllipseGeometry(g.P(cx + r * 0.62, cy - r * 0.55), r * 0.82 * g.K, r * 0.82 * g.K);
		return Geometry.Combine(geometry, geometry2, GeometryCombineMode.Exclude, null);
	}

	private static Geometry Build(Kind kind, bool night, double size, double stroke)
	{
		Pen24 pen = new Pen24(size, stroke);
		GeometryGroup geometryGroup = new GeometryGroup();
		geometryGroup.FillRule = FillRule.Nonzero;
		GeometryGroup geometryGroup2 = geometryGroup;
		switch (kind)
		{
		case Kind.Clear:
			if (night)
			{
				geometryGroup2.Children.Add(pen.Outline(MoonArea(pen, 11.2, 12.8, 7.6)));
				break;
			}
			Sun(pen, 12.0, 12.0, 4.4, 7.4, 9.8);
			geometryGroup2.Children.Add(pen.Outline(pen.Strokes));
			break;
		case Kind.PartlyCloudy:
		{
			Geometry geometry = CloudArea(pen, 5.6, 9.6, 0.98);
			Geometry value = pen.Outline(geometry);
			Geometry geometry2;
			if (night)
			{
				geometry2 = pen.Outline(MoonArea(pen, 9.0, 9.0, 5.2));
			}
			else
			{
				Sun(pen, 8.8, 8.8, 3.2, 5.4, 7.2);
				geometry2 = pen.Outline(pen.Strokes);
			}
			PathGeometry geometry3 = Geometry.Combine(geometry, geometry.GetWidenedPathGeometry(new Pen(Brushes.Black, stroke * 3.0)), GeometryCombineMode.Union, null);
			geometryGroup2.Children.Add(Geometry.Combine(geometry2, geometry3, GeometryCombineMode.Exclude, null));
			geometryGroup2.Children.Add(value);
			break;
		}
		case Kind.Cloudy:
			geometryGroup2.Children.Add(pen.Outline(CloudArea(pen, 0.9, 5.6, 1.2)));
			break;
		default:
		{
			Geometry g = CloudArea(pen, 1.8, 2.2, 1.1);
			geometryGroup2.Children.Add(pen.Outline(g));
			switch (kind)
			{
			case Kind.Fog:
				pen.Line(4.5, 17.6, 19.5, 17.6);
				pen.Line(7.5, 20.9, 16.5, 20.9);
				break;
			case Kind.Drizzle:
				pen.Line(8.2, 17.0, 7.7, 18.4);
				pen.Line(12.2, 17.0, 11.7, 18.4);
				pen.Line(16.2, 17.0, 15.7, 18.4);
				pen.Line(10.2, 20.0, 9.7, 21.4);
				pen.Line(14.2, 20.0, 13.7, 21.4);
				break;
			case Kind.Rain:
				pen.Line(8.4, 16.8, 7.0, 21.2);
				pen.Line(12.4, 16.8, 11.0, 21.2);
				pen.Line(16.4, 16.8, 15.0, 21.2);
				break;
			case Kind.Thunder:
				pen.Path("M13.2,15.6 L10.6,19.2 L13.6,19.2 L11.0,22.8");
				break;
			case Kind.Snow:
				pen.Dot(8.0, 17.4, 0.95);
				pen.Dot(12.0, 17.4, 0.95);
				pen.Dot(16.0, 17.4, 0.95);
				pen.Dot(10.0, 20.8, 0.95);
				pen.Dot(14.0, 20.8, 0.95);
				break;
			}
			if (pen.Strokes.Children.Count > 0)
			{
				geometryGroup2.Children.Add(pen.Outline(pen.Strokes));
			}
			break;
		}
		}
		if (pen.Fills.Children.Count > 0)
		{
			geometryGroup2.Children.Add(pen.Fills);
		}
		geometryGroup2.Freeze();
		return geometryGroup2;
	}

	private static Geometry BuildSymbol(string name, double size, double stroke)
	{
		Pen24 pen = new Pen24(size, stroke);
		GeometryGroup geometryGroup = new GeometryGroup();
		geometryGroup.FillRule = FillRule.Nonzero;
		GeometryGroup geometryGroup2 = geometryGroup;
		switch (name)
		{
		case "humidity":
			pen.Path("M12,3.2 C12,3.2 5.8,10.2 5.8,14.6 C5.8,18.1 8.6,20.8 12,20.8 C15.4,20.8 18.2,18.1 18.2,14.6 C18.2,10.2 12,3.2 12,3.2 Z");
			break;
		case "wind":
			pen.Path("M3,9.5 L13.6,9.5 C15.3,9.5 16.6,8.3 16.6,6.8 C16.6,5.3 15.4,4.2 14,4.2 C12.9,4.2 12,4.9 11.7,5.9");
			pen.Path("M3,13.5 L18.2,13.5 C19.9,13.5 21.2,14.8 21.2,16.5 C21.2,18.2 19.9,19.4 18.3,19.4 C17,19.4 16,18.6 15.6,17.6");
			pen.Line(3.0, 17.5, 10.5, 17.5);
			break;
		case "uv":
			Sun(pen, 12.0, 12.0, 4.0, 7.0, 9.5);
			break;
		case "pin":
			pen.Path("M12,21 C12,21 5.6,14.8 5.6,9.8 C5.6,6.2 8.5,3.4 12,3.4 C15.5,3.4 18.4,6.2 18.4,9.8 C18.4,14.8 12,21 12,21 Z");
			pen.Circle(12.0, 9.8, 2.4);
			break;
		case "locate":
			pen.Path("M20,4 L4,11 L11.2,12.8 L13,20 Z");
			break;
		case "search":
			pen.Circle(10.5, 10.5, 6.3);
			pen.Line(15.2, 15.2, 20.0, 20.0);
			break;
		case "dismiss":
			pen.Line(6.0, 6.0, 18.0, 18.0);
			pen.Line(18.0, 6.0, 6.0, 18.0);
			break;
		case "check":
			pen.Path("M4.5,12.5 L9.5,17.5 L19.5,6.5");
			break;
		case "offline":
			geometryGroup2.Children.Add(pen.Outline(CloudArea(pen, 0.9, 5.6, 1.2)));
			pen.Line(3.5, 3.5, 20.5, 20.5);
			break;
		case "alert":
			pen.Circle(12.0, 12.0, 9.0);
			pen.Line(12.0, 7.5, 12.0, 13.0);
			pen.Dot(12.0, 16.4, 1.1);
			break;
		}
		if (pen.Strokes.Children.Count > 0)
		{
			geometryGroup2.Children.Add(pen.Outline(pen.Strokes));
		}
		if (pen.Fills.Children.Count > 0)
		{
			geometryGroup2.Children.Add(pen.Fills);
		}
		geometryGroup2.Freeze();
		return geometryGroup2;
	}
}
