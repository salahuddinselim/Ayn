using System;
using System.Windows;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class NotchGeometryBuilder
{
	public const double IslandGap = 5.0;

	public const double ContentPadding = 12.0;

	public const double MiniShoulderCap = 0.15;

	public static PathGeometry BuildGeometry(string style, double width, double height, double customCornerRadius = -1.0)
	{
		return BuildSilhouetteCore(style, width, height, customCornerRadius, isClosed: true);
	}

	public static PathGeometry BuildSilhouetteGeometry(string style, double width, double height, double customCornerRadius = -1.0)
	{
		return BuildSilhouetteCore(style, width, height, customCornerRadius, isClosed: false);
	}

	private static PathGeometry BuildSilhouetteCore(string style, double width, double height, double customCornerRadius, bool isClosed)
	{
		switch (style)
		{
		case "Circular":
			return BuildCircularGeometry(width, height, isClosed);
		case "Compact":
			return BuildCompactGeometry(width, height, (customCornerRadius >= 0.0) ? customCornerRadius : 14.0, isClosed);
		case "Wide":
			return BuildWideGeometry(width, height, (customCornerRadius >= 0.0) ? customCornerRadius : 18.0, isClosed);
		case "Minimal":
		case "SquareMinimal":
			return BuildSquareMinimalGeometry(width, height, (customCornerRadius >= 0.0) ? customCornerRadius : 5.0, isClosed);
		case "Dynamic":
			return BuildDynamicGeometry(width, height, (customCornerRadius >= 0.0) ? customCornerRadius : 16.0, isClosed);
		case "Line":
			return BuildLineGeometry(width, height, (customCornerRadius >= 0.0) ? Math.Min(customCornerRadius, height / 2.0) : 9.0, isClosed);
		case "Island":
			return BuildIslandGeometry(width, height);
		default:
			return BuildShoulderedGeometry(width, height, (customCornerRadius >= 0.0) ? customCornerRadius : 16.0, isClosed);
		}
	}

	public static double BodyLeftAt(string style, double width, double height, double customCornerRadius, double y)
	{
		double num;
		double val;
		switch (style)
		{
		case "Island":
		{
			double num3 = Math.Min(5.0, Math.Max(0.0, height - 8.0));
			double num4 = (height - num3) / 2.0;
			double num5 = Math.Abs(y - (num3 + num4));
			if (!(num5 >= num4))
			{
				return num4 - Math.Sqrt(num4 * num4 - num5 * num5);
			}
			return num4;
		}
		case "Line":
		{
			num = 0.0;
			double num2 = 0.0;
			val = ((customCornerRadius >= 0.0) ? Math.Min(customCornerRadius, height / 2.0) : 9.0);
			val = Math.Max(0.0, Math.Min(val, Math.Min(width / 4.0, height / 4.0)));
			break;
		}
		case "Wide":
		{
			num = Math.Min(16.0, width * 0.09);
			double num2 = Math.Min(12.0, height * 0.38);
			val = ((customCornerRadius >= 0.0) ? customCornerRadius : 18.0);
			val = Math.Max(0.0, Math.Min(val, Math.Min((width - 2.0 * num) / 2.0, height - num2)));
			break;
		}
		case "Dynamic":
		{
			num = Math.Min(8.0, width * 0.05);
			double num2 = Math.Min(6.0, height * 0.2);
			val = ((customCornerRadius >= 0.0) ? customCornerRadius : 16.0);
			val = Math.Max(0.0, Math.Min(val, Math.Min((width - 2.0 * num) / 2.0, height - num2)));
			break;
		}
		default:
		{
			num = Math.Min(12.0, width * 0.08);
			double num2 = Math.Min(10.0, height * 0.35);
			switch (style)
			{
			case "Circular":
				val = double.MaxValue;
				break;
			case "Minimal":
			case "SquareMinimal":
				val = ((customCornerRadius >= 0.0) ? customCornerRadius : 5.0);
				val = Math.Min(val, Math.Min(width / 4.0, height / 4.0));
				break;
			case "Compact":
				val = ((customCornerRadius >= 0.0) ? customCornerRadius : 14.0);
				break;
			default:
				val = ((customCornerRadius >= 0.0) ? customCornerRadius : 16.0);
				break;
			}
			val = Math.Max(0.0, Math.Min(val, Math.Min((width - 2.0 * num) / 2.0, height - num2)));
			break;
		}
		}
		double num6 = height - val;
		if (val <= 0.001 || y <= num6)
		{
			return num;
		}
		double num7 = Math.Min(val, y - num6);
		return num + val - Math.Sqrt(val * val - num7 * num7);
	}

	public static double ContentInset(string style, double width, double height, double customCornerRadius, double yTop, double yBottom)
	{
		return Math.Max(BodyLeftAt(style, width, height, customCornerRadius, yTop), BodyLeftAt(style, width, height, customCornerRadius, yBottom)) + 12.0;
	}

	private static PathGeometry BuildIslandGeometry(double width, double height)
	{
		double num = Math.Min(5.0, Math.Max(0.0, height - 8.0));
		double num2 = height - num;
		double num3 = num2 / 2.0;
		PathGeometry pathGeometry = new PathGeometry();
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(num3, num);
		pathFigure.IsClosed = true;
		pathFigure.IsFilled = true;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new LineSegment(new Point(width - num3, num), isStroked: true));
		pathFigure2.Segments.Add(new ArcSegment(new Point(width - num3, height), new Size(num3, num3), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(num3, height), isStroked: true));
		pathFigure2.Segments.Add(new ArcSegment(new Point(num3, num), new Size(num3, num3), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true));
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		return pathGeometry;
	}

	private static PathGeometry BuildWideGeometry(double width, double height, double rBottom, bool isClosed)
	{
		double num = Math.Min(16.0, width * 0.09);
		double num2 = Math.Min(12.0, height * 0.38);
		rBottom = Math.Max(0.0, Math.Min(rBottom, Math.Min((width - 2.0 * num) / 2.0, height - num2)));
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(0.0, 0.0);
		pathFigure.IsClosed = isClosed;
		pathFigure.IsFilled = isClosed;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new BezierSegment(new Point(num * 0.4, 0.0), new Point(num, num2 * 0.4), new Point(num, num2), isStroked: true));
		if (rBottom > 0.001)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(num, height - rBottom), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(num + rBottom, height), new Size(rBottom, rBottom), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width - num - rBottom, height), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(width - num, height - rBottom), new Size(rBottom, rBottom), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		}
		else
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(num, height), isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width - num, height), isStroked: true));
		}
		pathFigure2.Segments.Add(new LineSegment(new Point(width - num, num2), isStroked: true));
		pathFigure2.Segments.Add(new BezierSegment(new Point(width - num, num2 * 0.4), new Point(width - num * 0.4, 0.0), new Point(width, 0.0), isStroked: true));
		if (isClosed)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, 0.0), isStroked: true));
		}
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		return pathGeometry;
	}

	private static PathGeometry BuildCompactGeometry(double width, double height, double rBottom, bool isClosed)
	{
		return BuildShoulderedGeometry(width, height, rBottom, isClosed);
	}

	private static PathGeometry BuildCircularGeometry(double width, double height, bool isClosed)
	{
		return BuildShoulderedGeometry(width, height, double.MaxValue, isClosed);
	}

	private static PathGeometry BuildSquareMinimalGeometry(double width, double height, double r, bool isClosed)
	{
		return BuildShoulderedGeometry(width, height, Math.Min(r, Math.Min(width / 4.0, height / 4.0)), isClosed);
	}

	private static PathGeometry BuildLineGeometry(double width, double height, double r, bool isClosed)
	{
		r = Math.Max(0.0, Math.Min(r, Math.Min(width / 4.0, height / 4.0)));
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(0.0, 0.0);
		pathFigure.IsClosed = isClosed;
		pathFigure.IsFilled = isClosed;
		PathFigure pathFigure2 = pathFigure;
		if (r > 0.001)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, height - r), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(r, height), new Size(r, r), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width - r, height), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(width, height - r), new Size(r, r), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		}
		else
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, height), isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width, height), isStroked: true));
		}
		pathFigure2.Segments.Add(new LineSegment(new Point(width, 0.0), isStroked: true));
		if (isClosed)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, 0.0), isStroked: true));
		}
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		return pathGeometry;
	}

	private static PathGeometry BuildShoulderedGeometry(double width, double height, double rBottom, bool isClosed)
	{
		return BuildShoulderedCore(width, height, rBottom, isClosed, 12.0, 10.0, 0.08);
	}

	public static PathGeometry BuildMiniGeometry(double width, double height, double scale, double rBottom)
	{
		MiniShoulders(width, height, scale, out var wingW, out var wingH);
		return BuildShoulderedCore(width, height, MiniBottomRadius(width, height, wingW, wingH, rBottom), isClosed: true, wingW, wingH, 1.0);
	}

	public static void MiniShoulders(double width, double height, double scale, out double wingW, out double wingH)
	{
		wingW = Math.Min(12.0 * scale, width * 0.15);
		wingH = Math.Min(10.0 * scale, height * 0.35);
	}

	public static double MiniBottomRadius(double width, double height, double wingW, double wingH, double rBottom)
	{
		return Math.Max(0.0, Math.Min(rBottom, Math.Min((width - 2.0 * wingW) / 2.0, height - wingH)));
	}

	private static PathGeometry BuildShoulderedCore(double width, double height, double rBottom, bool isClosed, double maxWingW, double maxWingH, double widthCap)
	{
		double num = Math.Min(maxWingW, width * widthCap);
		double num2 = Math.Min(maxWingH, height * 0.35);
		rBottom = Math.Max(0.0, Math.Min(rBottom, Math.Min((width - 2.0 * num) / 2.0, height - num2)));
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(0.0, 0.0);
		pathFigure.IsClosed = isClosed;
		pathFigure.IsFilled = isClosed;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new BezierSegment(new Point(num * 0.45, 0.0), new Point(num, num2 * 0.45), new Point(num, num2), isStroked: true));
		if (rBottom > 0.001)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(num, height - rBottom), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(num + rBottom, height), new Size(rBottom, rBottom), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width - num - rBottom, height), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(width - num, height - rBottom), new Size(rBottom, rBottom), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		}
		else
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(num, height), isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width - num, height), isStroked: true));
		}
		pathFigure2.Segments.Add(new LineSegment(new Point(width - num, num2), isStroked: true));
		pathFigure2.Segments.Add(new BezierSegment(new Point(width - num, num2 * 0.45), new Point(width - num * 0.45, 0.0), new Point(width, 0.0), isStroked: true));
		if (isClosed)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, 0.0), isStroked: true));
		}
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		return pathGeometry;
	}

	private static PathGeometry BuildDynamicGeometry(double width, double height, double rBottom, bool isClosed)
	{
		double num = Math.Min(8.0, width * 0.05);
		double num2 = Math.Min(6.0, height * 0.2);
		rBottom = Math.Max(0.0, Math.Min(rBottom, Math.Min((width - 2.0 * num) / 2.0, height - num2)));
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(0.0, 0.0);
		pathFigure.IsClosed = isClosed;
		pathFigure.IsFilled = isClosed;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new BezierSegment(new Point(num * 0.5, 0.0), new Point(num, num2 * 0.5), new Point(num, num2), isStroked: true));
		if (rBottom > 0.001)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(num, height - rBottom), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(num + rBottom, height), new Size(rBottom, rBottom), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width - num - rBottom, height), isStroked: true));
			pathFigure2.Segments.Add(new ArcSegment(new Point(width - num, height - rBottom), new Size(rBottom, rBottom), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		}
		else
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(num, height), isStroked: true));
			pathFigure2.Segments.Add(new LineSegment(new Point(width - num, height), isStroked: true));
		}
		pathFigure2.Segments.Add(new LineSegment(new Point(width - num, num2), isStroked: true));
		pathFigure2.Segments.Add(new BezierSegment(new Point(width - num, num2 * 0.5), new Point(width - num * 0.5, 0.0), new Point(width, 0.0), isStroked: true));
		if (isClosed)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, 0.0), isStroked: true));
		}
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		return pathGeometry;
	}

	public static PathGeometry BuildExpandedGeometry(double width, double height, double cornerRadius = 28.0)
	{
		return BuildExpandedCore(width, height, cornerRadius, isClosed: true);
	}

	public static PathGeometry BuildExpandedSilhouetteGeometry(double width, double height, double cornerRadius = 28.0)
	{
		return BuildExpandedCore(width, height, cornerRadius, isClosed: false);
	}

	private static PathGeometry BuildExpandedCore(double width, double height, double cornerRadius, bool isClosed)
	{
		cornerRadius = Math.Min(cornerRadius, Math.Min(width / 2.0, height / 2.0));
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(0.0, 0.0);
		pathFigure.IsClosed = isClosed;
		pathFigure.IsFilled = isClosed;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new LineSegment(new Point(0.0, height - cornerRadius), isStroked: true));
		pathFigure2.Segments.Add(new ArcSegment(new Point(cornerRadius, height), new Size(cornerRadius, cornerRadius), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(width - cornerRadius, height), isStroked: true));
		pathFigure2.Segments.Add(new ArcSegment(new Point(width, height - cornerRadius), new Size(cornerRadius, cornerRadius), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(width, 0.0), isStroked: true));
		if (isClosed)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, 0.0), isStroked: true));
		}
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		return pathGeometry;
	}

	public static PathGeometry BuildMorphedGeometry(double width, double height, double progress, double cornerRadius = 28.0)
	{
		return BuildMorphedCore(width, height, progress, cornerRadius, isClosed: true);
	}

	public static PathGeometry BuildMorphedSilhouetteGeometry(double width, double height, double progress, double cornerRadius = 28.0)
	{
		return BuildMorphedCore(width, height, progress, cornerRadius, isClosed: false);
	}

	private static PathGeometry BuildMorphedCore(double width, double height, double progress, double cornerRadius, bool isClosed)
	{
		progress = Math.Max(0.0, Math.Min(1.0, progress));
		if (progress >= 0.999)
		{
			return BuildExpandedCore(width, height, cornerRadius, isClosed);
		}
		if (progress <= 0.001)
		{
			return BuildSilhouetteCore(NotchConfig.Instance.Style, width, height, cornerRadius, isClosed);
		}
		double num = Math.Pow(1.0 - progress, 1.5);
		double num2 = Math.Min(12.0 * num, width * 0.08);
		double num3 = Math.Min(10.0 * num, height * 0.35);
		double num4 = 16.0;
		double val = num4 + (cornerRadius - num4) * progress;
		val = Math.Min(val, Math.Min((width - 2.0 * num2) / 2.0, height - num3));
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(0.0, 0.0);
		pathFigure.IsClosed = isClosed;
		pathFigure.IsFilled = isClosed;
		PathFigure pathFigure2 = pathFigure;
		if (num2 > 0.1 && num3 > 0.1)
		{
			pathFigure2.Segments.Add(new BezierSegment(new Point(num2 * 0.45, 0.0), new Point(num2, num3 * 0.45), new Point(num2, num3), isStroked: true));
		}
		else
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, 0.0), isStroked: true));
		}
		pathFigure2.Segments.Add(new LineSegment(new Point(num2, height - val), isStroked: true));
		pathFigure2.Segments.Add(new ArcSegment(new Point(num2 + val, height), new Size(val, val), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(width - num2 - val, height), isStroked: true));
		pathFigure2.Segments.Add(new ArcSegment(new Point(width - num2, height - val), new Size(val, val), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(width - num2, num3), isStroked: true));
		if (num2 > 0.1 && num3 > 0.1)
		{
			pathFigure2.Segments.Add(new BezierSegment(new Point(width - num2, num3 * 0.45), new Point(width - num2 * 0.45, 0.0), new Point(width, 0.0), isStroked: true));
		}
		else
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(width, 0.0), isStroked: true));
		}
		if (isClosed)
		{
			pathFigure2.Segments.Add(new LineSegment(new Point(0.0, 0.0), isStroked: true));
		}
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		return pathGeometry;
	}
}
