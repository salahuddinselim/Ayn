using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class VectorIcons
{
	private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public static double GetOpticalStroke(double size)
	{
		if (size <= 14.0)
		{
			return 1.2;
		}
		if (size <= 18.0)
		{
			return 1.25;
		}
		if (size <= 24.0)
		{
			return 1.4;
		}
		return 1.25 * (size / 16.0);
	}

	private static Path CreatePath(string data, Brush stroke, double thickness, Brush fill = null)
	{
		Geometry geometry = Geometry.Parse(data);
		if (geometry.CanFreeze)
		{
			geometry.Freeze();
		}
		Path path = new Path();
		path.Data = geometry;
		path.Stroke = stroke;
		path.StrokeThickness = thickness;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.Fill = ((fill != null) ? fill : Brushes.Transparent);
		return path;
	}

	public static Canvas CreateLogoMark(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Path path = new Path();
		path.Fill = fill;
		path.Data = Geometry.Parse(string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5} L {6},{7} Z M {8},{9} L {10},{11} L {12},{13} L {14},{15} Z M {16},{17} L {18},{19} L {20},{21} L {22},{23} Z M {24},{25} L {26},{27} L {28},{29} L {30},{31} Z", 2.0 * num, 8.0 * num, 8.0 * num, 2.0 * num, 7.0 * num, 8.0 * num, 2.0 * num, 8.0 * num, 8.0 * num, 2.0 * num, 14.0 * num, 8.0 * num, 8.0 * num, 7.0 * num, 8.0 * num, 2.0 * num, 14.0 * num, 8.0 * num, 8.0 * num, 14.0 * num, 9.0 * num, 8.0 * num, 14.0 * num, 8.0 * num, 8.0 * num, 14.0 * num, 2.0 * num, 8.0 * num, 8.0 * num, 9.0 * num, 8.0 * num, 14.0 * num));
		Path element = path;
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateHomeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(8.0 * num, 2.0 * num);
		pathFigure.IsClosed = true;
		pathFigure.IsFilled = true;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new LineSegment(new Point(14.4 * num, 7.2 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(12.8 * num, 7.2 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(12.8 * num, 14.0 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(9.6 * num, 14.0 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(9.6 * num, 9.6 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(6.4 * num, 9.6 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(6.4 * num, 14.0 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(3.2 * num, 14.0 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(3.2 * num, 7.2 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(1.6 * num, 7.2 * num), isStroked: true));
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		Path path = new Path();
		path.Data = pathGeometry;
		path.Fill = stroke;
		path.Stroke = stroke;
		path.StrokeThickness = 0.5 * num;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		Path element = path;
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateSettingsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text60Brush;
		double num2 = 8.0 * num;
		double num3 = 8.0 * num;
		double num4 = 5.5 * num;
		double num5 = 4.0 * num;
		double num6 = 1.8 * num;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < 8; i++)
		{
			double num7 = (double)i * 45.0;
			double num8 = (num7 - 6.0) * Math.PI / 180.0;
			double num9 = (num7 + 6.0) * Math.PI / 180.0;
			double num10 = (num7 + 11.5) * Math.PI / 180.0;
			double num11 = (num7 + 33.5) * Math.PI / 180.0;
			double num12 = num2 + num4 * Math.Cos(num8);
			double num13 = num3 + num4 * Math.Sin(num8);
			double num14 = num2 + num4 * Math.Cos(num9);
			double num15 = num3 + num4 * Math.Sin(num9);
			double num16 = num2 + num5 * Math.Cos(num10);
			double num17 = num3 + num5 * Math.Sin(num10);
			double num18 = num2 + num5 * Math.Cos(num11);
			double num19 = num3 + num5 * Math.Sin(num11);
			if (i == 0)
			{
				stringBuilder.Append(string.Format(Inv, "M {0:0.##},{1:0.##} ", num12, num13));
			}
			else
			{
				stringBuilder.Append(string.Format(Inv, "L {0:0.##},{1:0.##} ", num12, num13));
			}
			stringBuilder.Append(string.Format(Inv, "L {0:0.##},{1:0.##} L {2:0.##},{3:0.##} L {4:0.##},{5:0.##} ", num14, num15, num16, num17, num18, num19));
		}
		stringBuilder.Append("Z ");
		stringBuilder.Append(string.Format(Inv, "M {0:0.##},{1:0.##} A {2:0.##},{2:0.##} 0 1 0 {0:0.##},{3:0.##} A {2:0.##},{2:0.##} 0 1 0 {0:0.##},{1:0.##} Z", num2, num3 - num6, num6, num3 + num6));
		Path path = CreatePath(stringBuilder.ToString(), stroke2, thickness);
		if (path.Data != null && path.Data.CanFreeze)
		{
			path.Data.Freeze();
		}
		canvas2.Children.Add(path);
		return canvas2;
	}

	public static Canvas CreateCloseIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} M {2},{1} L {0},{3}", 3.8 * num, 3.8 * num, 12.2 * num, 12.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateMinimizeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1}", 3.0 * num, 8.0 * num, 13.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCollapseIcon(double size, Brush stroke)
	{
		return CreateChevronUp(size, stroke);
	}

	public static Canvas CreatePinIcon(double size, Brush stroke, bool isPinned = false)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Brush fill = (isPinned ? (stroke ?? BuddyTokens.SignatureOrangeBrush) : Brushes.Transparent);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5} L {6},{7} Z", 5.0 * num, 3.0 * num, 11.0 * num, 3.0 * num, 10.0 * num, 8.0 * num, 6.0 * num, 8.0 * num);
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {3},{4}", 4.0 * num, 8.5 * num, 12.0 * num, 8.0 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke, fill));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateSlidersIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {4},{1}", 2.5 * num, 5.5 * num, 5.2 * num, 8.2 * num, 13.5 * num);
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2}", 6.7 * num, 3.2 * num, 7.8 * num);
		string data3 = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {4},{1}", 2.5 * num, 10.5 * num, 7.8 * num, 10.8 * num, 13.5 * num);
		string data4 = string.Format(Inv, "M {0},{1} L {0},{2}", 9.3 * num, 8.2 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data4, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreatePulseIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {5},{6} L {7},{8} L {9},{1} L {10},{1}", 1.6 * num, 8.0 * num, 4.4 * num, 6.0 * num, 3.8 * num, 8.0 * num, 12.8 * num, 10.0 * num, 5.0 * num, 11.6 * num, 14.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateLocationPinIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {5},{1} C {5},{6} {0},{7} {0},{7} C {0},{7} {2},{6} {2},{1} Z", 8.0 * num, 14.0 * num, 3.4 * num, 3.6 * num, 12.6 * num, 12.6 * num, 9.0 * num, 14.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 3.2 * num;
		ellipse.Height = 3.2 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Margin = new Thickness(6.4 * num, 4.4 * num, 0.0, 0.0);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateGridMenuIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		double num2 = 4.2 * num;
		double num3 = 1.0 * num;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = num2;
		rectangle.Height = num2;
		rectangle.RadiusX = num3;
		rectangle.RadiusY = num3;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(2.4 * num, 2.4 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = num2;
		rectangle2.Height = num2;
		rectangle2.RadiusX = num3;
		rectangle2.RadiusY = num3;
		rectangle2.Stroke = stroke;
		rectangle2.StrokeThickness = opticalStroke;
		rectangle2.Margin = new Thickness(9.4 * num, 2.4 * num, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		Rectangle rectangle3 = new Rectangle();
		rectangle3.Width = num2;
		rectangle3.Height = num2;
		rectangle3.RadiusX = num3;
		rectangle3.RadiusY = num3;
		rectangle3.Stroke = stroke;
		rectangle3.StrokeThickness = opticalStroke;
		rectangle3.Margin = new Thickness(2.4 * num, 9.4 * num, 0.0, 0.0);
		Rectangle element3 = rectangle3;
		Rectangle rectangle4 = new Rectangle();
		rectangle4.Width = num2;
		rectangle4.Height = num2;
		rectangle4.RadiusX = num3;
		rectangle4.RadiusY = num3;
		rectangle4.Stroke = stroke;
		rectangle4.StrokeThickness = opticalStroke;
		rectangle4.Margin = new Thickness(9.4 * num, 9.4 * num, 0.0, 0.0);
		Rectangle element4 = rectangle4;
		canvas2.Children.Add(element);
		canvas2.Children.Add(element2);
		canvas2.Children.Add(element3);
		canvas2.Children.Add(element4);
		return canvas2;
	}

	public static Canvas CreateVisibilityEyeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {5},{1} C {4},{6} {2},{6} {0},{1} Z", 1.8 * num, 8.0 * num, 4.5 * num, 3.5 * num, 11.5 * num, 14.2 * num, 12.5 * num);
		string data2 = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 0 {3},{1} A {2},{2} 0 1 0 {0},{1} Z", 5.8 * num, 8.0 * num, 2.2 * num, 10.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke, stroke));
		return canvas2;
	}

	public static Canvas CreateChevronUp(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 3.6 * num, 10.2 * num, 8.0 * num, 5.8 * num, 12.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateChevronDown(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 3.6 * num, 5.8 * num, 8.0 * num, 10.2 * num, 12.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateBackIcon(double size, Brush stroke)
	{
		return CreateChevronLeft(size, stroke);
	}

	public static Canvas CreateChevronLeft(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4}", 10.2 * num, 3.6 * num, 5.8 * num, 8.0 * num, 12.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateForwardIcon(double size, Brush stroke)
	{
		return CreateChevronRight(size, stroke);
	}

	public static Canvas CreateChevronRight(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4}", 5.8 * num, 3.6 * num, 10.2 * num, 8.0 * num, 12.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateBatteryIcon(double size, Brush stroke)
	{
		return CreateBatteryIcon(size, stroke, Brushes.Transparent, 100.0, isCharging: false);
	}

	public static Canvas CreateBatteryIcon(double size, Brush stroke, Brush fill, double percent, bool isCharging)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 11.2 * num;
		rectangle.Height = 6.6 * num;
		rectangle.RadiusX = 1.6 * num;
		rectangle.RadiusY = 1.6 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(1.6 * num, 4.7 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 1.2 * num;
		rectangle2.Height = 3.0 * num;
		rectangle2.RadiusX = 0.6 * num;
		rectangle2.RadiusY = 0.6 * num;
		rectangle2.Fill = stroke;
		rectangle2.Margin = new Thickness(13.3 * num, 6.5 * num, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element2);
		double num2 = 8.6 * num;
		double width = Math.Max(1.0 * num, num2 * (Math.Max(0.0, Math.Min(100.0, percent)) / 100.0));
		Rectangle rectangle3 = new Rectangle();
		rectangle3.Width = width;
		rectangle3.Height = 4.2 * num;
		rectangle3.RadiusX = 0.8 * num;
		rectangle3.RadiusY = 0.8 * num;
		rectangle3.Fill = ((fill != null && fill != Brushes.Transparent) ? fill : stroke);
		rectangle3.Margin = new Thickness(2.8 * num, 5.9 * num, 0.0, 0.0);
		Rectangle element3 = rectangle3;
		canvas2.Children.Add(element3);
		if (isCharging)
		{
			Path path = new Path();
			path.Fill = Brushes.White;
			path.Data = Geometry.Parse(string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3} L {5},{6} L {7},{8} L {9},{8} Z", 7.6 * num, 3.8 * num, 5.2 * num, 8.2 * num, 7.4 * num, 8.2 * num, 6.4 * num, 12.2 * num, 9.8 * num, 7.8 * num, 7.6 * num, 7.8 * num));
			Path element4 = path;
			canvas2.Children.Add(element4);
		}
		return canvas2;
	}

	public static Canvas CreateHeaderBatteryIcon(double width, double height, Brush stroke, Brush fill, double percent, bool isCharging)
	{
		Canvas canvas = new Canvas();
		canvas.Width = width;
		canvas.Height = height;
		Canvas canvas2 = canvas;
		double strokeThickness = 1.0;
		double num = width - 2.8;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = num;
		rectangle.Height = height;
		rectangle.RadiusX = 2.4;
		rectangle.RadiusY = 2.4;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = strokeThickness;
		rectangle.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 1.6;
		rectangle2.Height = 3.8;
		rectangle2.RadiusX = 0.8;
		rectangle2.RadiusY = 0.8;
		rectangle2.Fill = stroke;
		rectangle2.Margin = new Thickness(num + 0.6, (height - 3.8) / 2.0, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element2);
		double num2 = 1.6;
		double num3 = num - num2 * 2.0;
		double width2 = Math.Max(1.0, num3 * (Math.Max(0.0, Math.Min(100.0, percent)) / 100.0));
		Rectangle rectangle3 = new Rectangle();
		rectangle3.Width = width2;
		rectangle3.Height = Math.Max(1.0, height - num2 * 2.0);
		rectangle3.RadiusX = 1.0;
		rectangle3.RadiusY = 1.0;
		rectangle3.Fill = ((fill != null && fill != Brushes.Transparent) ? fill : stroke);
		rectangle3.Margin = new Thickness(num2, num2, 0.0, 0.0);
		Rectangle element3 = rectangle3;
		canvas2.Children.Add(element3);
		if (isCharging)
		{
			Path path = new Path();
			path.Fill = Brushes.White;
			path.Data = Geometry.Parse("M 7,0.5 L 4,4.5 L 6.5,4.5 L 5,8.5 L 9,4 L 6.5,4 Z");
			Path element4 = path;
			canvas2.Children.Add(element4);
		}
		return canvas2;
	}

	public static Canvas CreateWiFiIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 2.0 * num;
		ellipse.Height = 2.0 * num;
		ellipse.Fill = stroke;
		ellipse.Margin = new Thickness(7.0 * num, 12.2 * num, 0.0, 0.0);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} A {2},{3} 0 0,1 {4},{1}", 5.6 * num, 10.2 * num, 3.4 * num, 3.4 * num, 10.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} A {2},{3} 0 0,1 {4},{1}", 3.6 * num, 7.6 * num, 6.2 * num, 6.2 * num, 12.4 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} A {2},{3} 0 0,1 {4},{1}", 1.6 * num, 5.0 * num, 9.0 * num, 9.0 * num, 14.4 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateVolumeIcon(double size, Brush stroke)
	{
		return CreateVolumeIcon(size, stroke, isMuted: false, 50);
	}

	public static Canvas CreateVolumeIcon(double size, Brush stroke, bool isMuted, int volumePercent)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		canvas.ClipToBounds = false;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {3},{5} L {2},{6} L {0},{6} Z", 2.2 * num, 5.8 * num, 4.8 * num, 5.8 * num, 8.2 * num, 2.8 * num, 8.2 * num, 13.2 * num, 4.8 * num, 10.2 * num, 2.2 * num, 10.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke, stroke));
		if (isMuted || volumePercent == 0)
		{
			string data2 = string.Format(Inv, "M {0},{1} L {2},{3} M {2},{1} L {0},{3}", 10.4 * num, 6.2 * num, 14.2 * num, 10.2 * num);
			canvas2.Children.Add(CreatePath(data2, BuddyTokens.DangerRedBrush, opticalStroke));
		}
		else
		{
			string data3 = string.Format(Inv, "M {0},{1} A {2},{3} 0 0,1 {0},{4}", 10.4 * num, 6.0 * num, 3.6 * num, 3.6 * num, 10.0 * num);
			canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
			if (volumePercent > 35)
			{
				string data4 = string.Format(Inv, "M {0},{1} A {2},{3} 0 0,1 {0},{4}", 12.6 * num, 4.0 * num, 6.2 * num, 6.2 * num, 12.0 * num);
				canvas2.Children.Add(CreatePath(data4, stroke, opticalStroke));
			}
		}
		return canvas2;
	}

	public static Canvas CreateClockIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 12.6 * num;
		ellipse.Height = 12.6 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Margin = new Thickness(1.7 * num);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2}", 8.0 * num, 4.0 * num, 8.0 * num, 11.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateTimerIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 11.6 * num;
		ellipse.Height = 11.6 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Margin = new Thickness(2.2 * num, 3.2 * num, 0.0, 0.0);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {3},{4} M {5},{6} L {7},{8}", 6.5 * num, 1.2 * num, 9.5 * num, 8.0 * num, 3.2 * num, 12.2 * num, 3.2 * num, 13.4 * num, 4.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3}", 8.0 * num, 9.0 * num, 10.8 * num, 6.4 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateHourglassIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {2},{3} M {4},{1} C {4},{5} {6},{7} {7},{7} C {8},{7} {10},{5} {10},{1} M {4},{3} C {4},{9} {6},{7} {7},{7} C {8},{7} {10},{9} {10},{3}", 3.0 * num, 2.5 * num, 13.0 * num, 13.5 * num, 4.0 * num, 6.0 * num, 7.0 * num, 8.0 * num, 9.0 * num, 10.0 * num, 12.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateDropletIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {0},{1} {2},{3} {2},{4} A {5},{5} 0 0,0 {6},{4} C {6},{3} {0},{1} {0},{1} Z", 8.0 * num, 2.0 * num, 3.5 * num, 8.0 * num, 11.5 * num, 4.5 * num, 12.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateBellIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {0},{2} {3},{4} {3},{5} L {6},{7} L {8},{7} L {9},{5} C {9},{4} {10},{2} {10},{1} C {10},{11} {0},{11} {0},{1} Z", 5.0 * num, 7.5 * num, 5.0 * num, 6.0 * num, 3.5 * num, 10.5 * num, 2.5 * num, 12.5 * num, 13.5 * num, 12.5 * num, 11.0 * num, 3.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} C {0},{2} {3},{2} {3},{1}", 6.5 * num, 12.5 * num, 14.5 * num, 9.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateClipboardIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {4},{1} L {4},{5} L {0},{5} Z", 3.2 * num, 3.6 * num, 5.4 * num, 10.6 * num, 12.8 * num, 14.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2} L {3},{1} Z", 5.4 * num, 4.4 * num, 2.0 * num, 10.6 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {4},{3}", 5.4 * num, 7.4 * num, 10.6 * num, 10.2 * num, 9.2 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke * 0.9));
		return canvas2;
	}

	public static Canvas CreateNotesIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {3},{5} L {0},{5} Z M {2},{1} L {2},{4} L {3},{4}", 3.2 * num, 2.0 * num, 9.8 * num, 12.8 * num, 5.0 * num, 14.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {4},{3}", 5.4 * num, 7.8 * num, 10.6 * num, 10.4 * num, 9.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 0.9));
		return canvas2;
	}

	public static Canvas CreateScratchpadIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 3.2 * num, 3.4 * num, 12.8 * num, 14.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2} M {4},{1} L {4},{2}", 5.2 * num, 1.8 * num, 4.4 * num, 8.0 * num, 10.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {4},{3}", 5.2 * num, 7.2 * num, 10.8 * num, 10.0 * num, 8.8 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke * 0.9));
		return canvas2;
	}

	public static Canvas CreateFilesIcon(double size, Brush stroke)
	{
		return CreateFileShelfIcon(size, stroke);
	}

	public static Canvas CreateFileShelfIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {5},{4} L {5},{6} L {0},{6} Z", 2.2 * num, 4.6 * num, 5.8 * num, 7.4 * num, 6.6 * num, 13.8 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1}", 2.2 * num, 7.8 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 0.9));
		return canvas2;
	}

	public static Canvas CreateCalendarIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z M {0},{4} L {2},{4}", 2.4 * num, 3.6 * num, 13.6 * num, 14.0 * num, 6.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2}", 5.2 * num, 1.8 * num, 4.4 * num, 10.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {4},{1} M {0},{5} L {2},{5} M {3},{5} L {4},{5}", 5.0 * num, 9.2 * num, 6.2 * num, 9.8 * num, 11.0 * num, 11.6 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke * 1.2));
		return canvas2;
	}

	public static Canvas CreateWeatherIcon(double size, Brush stroke)
	{
		return CreateWeatherPartlyCloudyLine(size, stroke);
	}

	public static Canvas CreateSoundsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} L {3},{5} M {6},{7} L {6},{8} M {9},{10} L {9},{11} M {12},{13} L {12},{14}", 2.8 * num, 6.4 * num, 9.6 * num, 5.4 * num, 4.4 * num, 11.6 * num, 8.0 * num, 2.4 * num, 13.6 * num, 10.6 * num, 4.4 * num, 11.6 * num, 13.2 * num, 6.4 * num, 9.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke * 1.2));
		return canvas2;
	}

	public static Canvas CreateMessageIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {4},{3} L {5},{6} L {5},{3} L {0},{3} Z", 2.4 * num, 3.2 * num, 13.6 * num, 11.2 * num, 7.6 * num, 4.4 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {4},{1}", 5.0 * num, 7.2 * num, 7.0 * num, 9.0 * num, 11.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateLinksIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} A {4},{4} 0 0,0 {5},{6} L {7},{8} A {4},{4} 0 0,0 {9},{10}", 6.8 * num, 9.2 * num, 5.2 * num, 10.8 * num, 2.4 * num, 1.8 * num, 7.4 * num, 3.4 * num, 5.8 * num, 6.8 * num, 5.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} A {4},{4} 0 0,0 {5},{6} L {7},{8} A {4},{4} 0 0,0 {9},{10}", 9.2 * num, 6.8 * num, 10.8 * num, 5.2 * num, 2.4 * num, 14.2 * num, 8.6 * num, 12.6 * num, 10.2 * num, 9.2 * num, 10.2 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{3}", 6.4 * num, 9.6 * num, 9.6 * num, 6.4 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateUnitsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 2.2 * num, 5.4 * num, 13.8 * num, 10.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{4} M {5},{1} L {5},{2} M {6},{1} L {6},{4} M {7},{1} L {7},{2}", 4.2 * num, 5.4 * num, 8.2 * num, 6.4 * num, 7.2 * num, 8.6 * num, 10.8 * num, 12.6 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateStatsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {3},{4} M {5},{1} L {5},{6} M {7},{1} L {7},{8}", 2.2 * num, 13.8 * num, 13.8 * num, 4.2 * num, 9.2 * num, 8.0 * num, 6.2 * num, 11.8 * num, 3.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke * 1.1));
		return canvas2;
	}

	public static Canvas CreateScreenTimeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {2},{3} M {4},{1} L {5},{6} L {4},{3} M {7},{1} L {8},{6} L {7},{3}", 3.4 * num, 2.2 * num, 12.6 * num, 13.8 * num, 4.2 * num, 7.6 * num, 8.0 * num, 11.8 * num, 8.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2}", 8.0 * num, 8.4 * num, 11.4 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateAnalyticsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2} M {4},{5} L {6},{7} L {8},{9} L {10},{11}", 2.4 * num, 2.4 * num, 13.6 * num, 13.6 * num, 3.2 * num, 11.2 * num, 6.6 * num, 8.2 * num, 9.6 * num, 9.8 * num, 13.2 * num, 4.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateHistogramIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 2.6 * num;
		rectangle.Height = 7.5 * num;
		rectangle.RadiusX = 1.3 * num;
		rectangle.RadiusY = 1.3 * num;
		rectangle.Fill = stroke;
		rectangle.Margin = new Thickness(2.4 * num, 6.0 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 2.6 * num;
		rectangle2.Height = 11.5 * num;
		rectangle2.RadiusX = 1.3 * num;
		rectangle2.RadiusY = 1.3 * num;
		rectangle2.Fill = stroke;
		rectangle2.Margin = new Thickness(6.7 * num, 2.0 * num, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element2);
		Rectangle rectangle3 = new Rectangle();
		rectangle3.Width = 2.6 * num;
		rectangle3.Height = 5.5 * num;
		rectangle3.RadiusX = 1.3 * num;
		rectangle3.RadiusY = 1.3 * num;
		rectangle3.Fill = stroke;
		rectangle3.Margin = new Thickness(11.0 * num, 8.0 * num, 0.0, 0.0);
		Rectangle element3 = rectangle3;
		canvas2.Children.Add(element3);
		return canvas2;
	}

	public static Canvas CreateStatsBarsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 2.6 * num;
		rectangle.Height = 4.8 * num;
		rectangle.RadiusX = 1.3 * num;
		rectangle.RadiusY = 1.3 * num;
		rectangle.Fill = stroke;
		rectangle.Margin = new Thickness(2.4 * num, 8.7 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 2.6 * num;
		rectangle2.Height = 8.5 * num;
		rectangle2.RadiusX = 1.3 * num;
		rectangle2.RadiusY = 1.3 * num;
		rectangle2.Fill = stroke;
		rectangle2.Margin = new Thickness(6.7 * num, 5.0 * num, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element2);
		Rectangle rectangle3 = new Rectangle();
		rectangle3.Width = 2.6 * num;
		rectangle3.Height = 12.0 * num;
		rectangle3.RadiusX = 1.3 * num;
		rectangle3.RadiusY = 1.3 * num;
		rectangle3.Fill = stroke;
		rectangle3.Margin = new Thickness(11.0 * num, 1.5 * num, 0.0, 0.0);
		Rectangle element3 = rectangle3;
		canvas2.Children.Add(element3);
		return canvas2;
	}

	public static Canvas CreateRevenueIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z M {0},{4} L {2},{4}", 2.2 * num, 4.8 * num, 13.8 * num, 12.2 * num, 7.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} M {4},{3} L {2},{3} L {2},{5}", 8.4 * num, 13.8 * num, 13.8 * num, 8.4 * num, 11.0 * num, 11.2 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateClaudeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} L {5},{4} M {6},{7} L {8},{9} M {8},{7} L {6},{9}", 8.0 * num, 2.0 * num, 14.0 * num, 2.0 * num, 8.0 * num, 14.0 * num, 3.8 * num, 3.8 * num, 12.2 * num, 12.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke * 1.15));
		return canvas2;
	}

	public static Canvas CreateEmojiIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 0 {0},{3} A {2},{2} 0 1 0 {0},{1} Z", 8.0 * num, 1.8 * num, 6.2 * num, 14.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2}", 5.8 * num, 5.8 * num, 7.0 * num, 10.2 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 1.2));
		string data3 = string.Format(Inv, "M {0},{1} A {2},{3} 0 0 0 {4},{1}", 5.4 * num, 9.5 * num, 2.8 * num, 2.0 * num, 10.6 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateStyleContourIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} A {3},{3} 0 0 1 {2},{4} L {0},{4} A {3},{3} 0 0 1 {0},{1} Z", 4.5 * num, 5.2 * num, 11.5 * num, 2.8 * num, 10.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1}", 7.2 * num, 8.0 * num, 8.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 1.3));
		return canvas2;
	}

	public static Canvas CreatePlusCloseIcon(double size, Brush stroke, out RotateTransform rotateTransform)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {1},{0} L {1},{2}", 4.2 * num, 8.0 * num, 11.8 * num);
		Path path = CreatePath(data, stroke, opticalStroke);
		rotateTransform = new RotateTransform(0.0, 8.0 * num, 8.0 * num);
		path.RenderTransform = rotateTransform;
		canvas2.Children.Add(path);
		return canvas2;
	}

	public static Canvas CreateSearchIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 8.6 * num;
		ellipse.Height = 8.6 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Margin = new Thickness(2.2 * num, 2.2 * num, 0.0, 0.0);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{3}", 9.4 * num, 9.4 * num, 13.8 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke * 1.25));
		return canvas2;
	}

	public static Canvas CreateRefreshIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,0 {3},{4}", 13.4 * num, 7.2 * num, 5.4 * num, 12.8 * num, 11.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5}", 10.8 * num, 3.2 * num, 13.6 * num, 7.2 * num, 9.4 * num, 7.4 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateAddIcon(double size, Brush stroke)
	{
		return CreatePlusIcon(size, stroke);
	}

	public static Canvas CreatePlusIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {1},{0} L {2},{0}", 8.0 * num, 3.2 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateTrashIcon(double size, Brush stroke)
	{
		return CreateDeleteIcon(size, stroke);
	}

	public static Canvas CreateDeleteIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {3},{4} L {5},{4} L {5},{1}", 2.4 * num, 4.4 * num, 13.6 * num, 6.2 * num, 2.4 * num, 9.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3} L {5},{1}", 3.8 * num, 4.4 * num, 4.6 * num, 13.8 * num, 11.4 * num, 12.2 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2}", 6.6 * num, 7.2 * num, 11.2 * num, 9.4 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke * 0.9));
		return canvas2;
	}

	public static Canvas CreateTextLinesIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {4},{3} M {0},{5} L {6},{5}", 3.0 * num, 4.5 * num, 13.0 * num, 8.0 * num, 10.0 * num, 11.5 * num, 12.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCodeBracketsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4} M {5},{1} L {6},{3} L {5},{4} M {7},{8} L {9},{10}", 5.6 * num, 4.2 * num, 2.4 * num, 8.0 * num, 11.8 * num, 10.4 * num, 13.6 * num, 9.4 * num, 3.4 * num, 6.6 * num, 12.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCopyIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 5.0 * num, 2.2 * num, 13.8 * num, 11.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 2.2 * num, 5.0 * num, 11.0 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateEditIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5} L {6},{7} Z M {8},{9} L {10},{11}", 11.4 * num, 2.4 * num, 13.6 * num, 4.6 * num, 5.4 * num, 12.8 * num, 2.4 * num, 13.6 * num, 9.6 * num, 4.2 * num, 11.8 * num, 6.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateClearIcon(double size, Brush stroke)
	{
		return CreateTrashIcon(size, stroke);
	}

	public static Canvas CreateFolderIcon(double size, Brush stroke)
	{
		return CreateFileShelfIcon(size, stroke);
	}

	public static Canvas CreateSaveIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {3},{5} L {0},{5} Z", 2.5 * num, 2.5 * num, 11.5 * num, 13.5 * num, 4.5 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2} L {3},{1} Z", 5.0 * num, 2.5 * num, 6.5 * num, 10.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 0.9));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 4.5 * num, 9.5 * num, 11.5 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke * 0.9));
		return canvas2;
	}

	public static Canvas CreateCopyPathIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 5.2 * num, 2.4 * num, 13.6 * num, 10.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 2.4 * num, 5.2 * num, 10.8 * num, 13.6 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateExternalLaunchIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {4},{3} L {4},{5}", 7.5 * num, 3.5 * num, 3.5 * num, 12.5 * num, 12.5 * num, 8.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} M {4},{3} L {2},{3} L {2},{5}", 7.0 * num, 9.0 * num, 12.5 * num, 3.5 * num, 9.0 * num, 7.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCheckIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5}", 3.2 * num, 8.4 * num, 6.6 * num, 12.0 * num, 13.2 * num, 4.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke * 1.15));
		return canvas2;
	}

	public static Canvas CreateWarningIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3} Z M {0},{5} L {0},{6} M {0},{7} L {0},{8}", 8.0 * num, 2.2 * num, 14.0 * num, 13.8 * num, 2.0 * num, 6.0 * num, 9.2 * num, 11.2 * num, 11.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateLockIcon(double size, Brush stroke)
	{
		return CreateLockStateIcon(size, stroke, isUnlocked: false);
	}

	public static Canvas CreateUnlockIcon(double size, Brush stroke)
	{
		return CreateLockStateIcon(size, stroke, isUnlocked: true);
	}

	private static Canvas CreateLockStateIcon(double size, Brush stroke, bool isUnlocked)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z", 3.4 * num, 7.6 * num, 12.6 * num, 14.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = (isUnlocked ? string.Format(Inv, "M {0},{1} L {0},{2} A {3},{3} 0 0,1 {4},{2} L {4},{5}", 5.4 * num, 7.6 * num, 4.8 * num, 2.6 * num, 10.6 * num, 2.6 * num) : string.Format(Inv, "M {0},{1} L {0},{2} A {3},{3} 0 0,1 {4},{2} L {4},{1}", 5.4 * num, 7.6 * num, 4.8 * num, 2.6 * num, 10.6 * num));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {0},{2}", 8.0 * num, 9.8 * num, 11.8 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreatePlayMediaIcon(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		string source = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4} Z", 5.4 * num, 3.4 * num, 12.8 * num, 8.0 * num, 12.6 * num);
		Path path = new Path();
		path.Fill = fill;
		path.Data = Geometry.Parse(source);
		Path element = path;
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreatePauseMediaIcon(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 2.6 * num;
		rectangle.Height = 9.8 * num;
		rectangle.Fill = fill;
		rectangle.RadiusX = 0.8 * num;
		rectangle.RadiusY = 0.8 * num;
		rectangle.Margin = new Thickness(4.4 * num, 3.1 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 2.6 * num;
		rectangle2.Height = 9.8 * num;
		rectangle2.Fill = fill;
		rectangle2.RadiusX = 0.8 * num;
		rectangle2.RadiusY = 0.8 * num;
		rectangle2.Margin = new Thickness(9.0 * num, 3.1 * num, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element);
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateStopIcon(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 9.2 * num;
		rectangle.Height = 9.2 * num;
		rectangle.Fill = fill;
		rectangle.RadiusX = 1.2 * num;
		rectangle.RadiusY = 1.2 * num;
		rectangle.Margin = new Thickness(3.4 * num);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateSkipPrevIcon(double size, Brush stroke, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.6 * num;
		rectangle.Height = 9.6 * num;
		rectangle.Fill = fill;
		rectangle.RadiusX = 0.8 * num;
		rectangle.RadiusY = 0.8 * num;
		rectangle.Margin = new Thickness(3.0 * num, 3.2 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Path path = new Path();
		path.Fill = fill;
		path.Data = Geometry.Parse(string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4} Z", 12.8 * num, 3.4 * num, 5.4 * num, 8.0 * num, 12.6 * num));
		Path element2 = path;
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateSkipNextIcon(double size, Brush stroke, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Path path = new Path();
		path.Fill = fill;
		path.Data = Geometry.Parse(string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4} Z", 3.2 * num, 3.4 * num, 10.6 * num, 8.0 * num, 12.6 * num));
		Path element = path;
		canvas2.Children.Add(element);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.6 * num;
		rectangle.Height = 9.6 * num;
		rectangle.Fill = fill;
		rectangle.RadiusX = 0.8 * num;
		rectangle.RadiusY = 0.8 * num;
		rectangle.Margin = new Thickness(11.4 * num, 3.2 * num, 0.0, 0.0);
		Rectangle element2 = rectangle;
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateMusicIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{4} L {3},{5} M {0},{6} L {3},{7}", 5.0 * num, 11.2 * num, 3.4 * num, 13.0 * num, 2.0 * num, 9.6 * num, 5.6 * num, 4.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 3.6 * num;
		ellipse.Height = 2.8 * num;
		ellipse.Fill = stroke;
		ellipse.Margin = new Thickness(2.4 * num, 10.4 * num, 0.0, 0.0);
		Ellipse element = ellipse;
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 3.6 * num;
		ellipse2.Height = 2.8 * num;
		ellipse2.Fill = stroke;
		ellipse2.Margin = new Thickness(10.4 * num, 8.8 * num, 0.0, 0.0);
		Ellipse element2 = ellipse2;
		canvas2.Children.Add(element);
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateCpuChipIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 9.6 * num;
		rectangle.Height = 9.6 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(3.2 * num);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 4.0 * num;
		rectangle2.Height = 4.0 * num;
		rectangle2.RadiusX = 0.8 * num;
		rectangle2.RadiusY = 0.8 * num;
		rectangle2.Stroke = stroke;
		rectangle2.StrokeThickness = opticalStroke * 0.9;
		rectangle2.Margin = new Thickness(6.0 * num);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element2);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2} M {4},{1} L {4},{2} M {0},{5} L {0},{6} M {3},{5} L {3},{6} M {4},{5} L {4},{6} M {1},{0} L {2},{0} M {1},{3} L {2},{3} M {1},{4} L {2},{4} M {5},{0} L {6},{0} M {5},{3} L {6},{3} M {5},{4} L {6},{4}", 5.6 * num, 1.2 * num, 3.0 * num, 8.0 * num, 10.4 * num, 13.0 * num, 14.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateMemoryRamIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 13.0 * num;
		rectangle.Height = 7.6 * num;
		rectangle.RadiusX = 1.2 * num;
		rectangle.RadiusY = 1.2 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(1.5 * num, 4.2 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z M {4},{1} L {5},{1} L {5},{3} L {4},{3} Z M {6},{1} L {7},{1} L {7},{3} L {6},{3} Z", 3.2 * num, 5.8 * num, 5.2 * num, 8.6 * num, 7.0 * num, 9.0 * num, 10.8 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke * 0.9));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1}", 6.0 * num, 11.8 * num, 7.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateDiskDriveIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 12.6 * num;
		rectangle.Height = 10.0 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(1.7 * num, 3.0 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 6.0 * num;
		ellipse.Height = 6.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke * 0.9;
		ellipse.Margin = new Thickness(3.5 * num, 5.0 * num, 0.0, 0.0);
		Ellipse element2 = ellipse;
		canvas2.Children.Add(element2);
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 1.6 * num;
		ellipse2.Height = 1.6 * num;
		ellipse2.Fill = stroke;
		ellipse2.Margin = new Thickness(11.0 * num, 5.0 * num, 0.0, 0.0);
		Ellipse element3 = ellipse2;
		canvas2.Children.Add(element3);
		return canvas2;
	}

	public static Canvas CreateNetworkInOutIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} L {0},{2} L {5},{4}", 5.2 * num, 3.0 * num, 13.0 * num, 2.6 * num, 10.4 * num, 7.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} L {0},{1} L {5},{4}", 10.8 * num, 3.0 * num, 13.0 * num, 8.2 * num, 5.6 * num, 13.4 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateSystemPulseIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 13.0 * num;
		rectangle.Height = 9.0 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(1.5 * num, 2.5 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{2} L {4},{2}", 8.0 * num, 11.5 * num, 13.5 * num, 5.5 * num, 10.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateFileGenericIcon(double size, Brush stroke)
	{
		return CreateNotesIcon(size, stroke);
	}

	public static Canvas CreateFileIcon(double size, Brush stroke)
	{
		return CreateFileGenericIcon(size, stroke);
	}

	public static Canvas CreateNineDotLauncherIcon(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 8.0;
		double num2 = size / 3.0;
		double num3 = num2 / 2.0;
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				Ellipse ellipse = new Ellipse();
				ellipse.Width = num;
				ellipse.Height = num;
				ellipse.Fill = fill;
				Ellipse element = ellipse;
				Canvas.SetLeft(element, (double)j * num2 + num3 - num / 2.0);
				Canvas.SetTop(element, (double)i * num2 + num3 - num / 2.0);
				canvas2.Children.Add(element);
			}
		}
		return canvas2;
	}

	public static Canvas CreateMoreHorizontalIcon(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.5 * num;
		double top = 8.0 * num - num2;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 2.0 * num2;
		ellipse.Height = 2.0 * num2;
		ellipse.Fill = fill;
		ellipse.Margin = new Thickness(3.5 * num - num2, top, 0.0, 0.0);
		Ellipse element = ellipse;
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 2.0 * num2;
		ellipse2.Height = 2.0 * num2;
		ellipse2.Fill = fill;
		ellipse2.Margin = new Thickness(8.0 * num - num2, top, 0.0, 0.0);
		Ellipse element2 = ellipse2;
		Ellipse ellipse3 = new Ellipse();
		ellipse3.Width = 2.0 * num2;
		ellipse3.Height = 2.0 * num2;
		ellipse3.Fill = fill;
		ellipse3.Margin = new Thickness(12.5 * num - num2, top, 0.0, 0.0);
		Ellipse element3 = ellipse3;
		canvas2.Children.Add(element);
		canvas2.Children.Add(element2);
		canvas2.Children.Add(element3);
		return canvas2;
	}

	public static Canvas CreateWeatherCloudIcon(double size, Brush stroke)
	{
		return CreateWeatherIcon(size, stroke);
	}

	public static Canvas CreateFocusIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 12.0 * num;
		ellipse.Height = 12.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Margin = new Thickness(2.0 * num);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 4.0 * num;
		ellipse2.Height = 4.0 * num;
		ellipse2.Fill = stroke;
		ellipse2.Margin = new Thickness(6.0 * num);
		Ellipse element2 = ellipse2;
		canvas2.Children.Add(element2);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} M {4},{5} L {6},{7} M {8},{9} L {10},{11} M {12},{13} L {14},{15}", 8.0 * num, 1.0 * num, 8.0 * num, 3.0 * num, 8.0 * num, 13.0 * num, 8.0 * num, 15.0 * num, 1.0 * num, 8.0 * num, 3.0 * num, 8.0 * num, 13.0 * num, 8.0 * num, 15.0 * num, 8.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateFocusRingGauge(double size, Brush accent)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 13.0 * num;
		ellipse.Height = 13.0 * num;
		ellipse.Stroke = new SolidColorBrush(Color.FromArgb(32, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		ellipse.StrokeThickness = 2.2 * num;
		ellipse.Margin = new Thickness(1.5 * num);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,1 {3},{4}", 8.0 * num, 1.5 * num, 6.5 * num, 14.5 * num, 8.0 * num);
		Path element2 = CreatePath(data, accent, 2.2 * num);
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateFocusReticleIcon(double size, Brush stroke)
	{
		return CreateFocusIcon(size, stroke);
	}

	public static Canvas CreatePaletteIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double strokeThickness = 1.4 * num;
		PathGeometry pathGeometry = new PathGeometry();
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(8.0 * num, 2.0 * num);
		pathFigure.IsClosed = true;
		pathFigure.IsFilled = false;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new BezierSegment(new Point(3.5 * num, 2.0 * num), new Point(1.5 * num, 5.5 * num), new Point(1.5 * num, 9.5 * num), isStroked: true));
		pathFigure2.Segments.Add(new BezierSegment(new Point(1.5 * num, 13.5 * num), new Point(5.5 * num, 14.5 * num), new Point(8.0 * num, 14.5 * num), isStroked: true));
		pathFigure2.Segments.Add(new BezierSegment(new Point(9.5 * num, 14.5 * num), new Point(11.0 * num, 13.0 * num), new Point(12.5 * num, 13.0 * num), isStroked: true));
		pathFigure2.Segments.Add(new BezierSegment(new Point(13.8 * num, 13.0 * num), new Point(14.5 * num, 14.0 * num), new Point(14.5 * num, 12.5 * num), isStroked: true));
		pathFigure2.Segments.Add(new BezierSegment(new Point(14.5 * num, 8.0 * num), new Point(12.5 * num, 2.0 * num), new Point(8.0 * num, 2.0 * num), isStroked: true));
		pathGeometry.Figures.Add(pathFigure2);
		Path path = new Path();
		path.Data = pathGeometry;
		path.Stroke = stroke;
		path.StrokeThickness = strokeThickness;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		path.StrokeLineJoin = PenLineJoin.Round;
		Path element = path;
		canvas2.Children.Add(element);
		double[][] array = new double[3][]
		{
			new double[2]
			{
				5.2 * num,
				6.0 * num
			},
			new double[2]
			{
				8.0 * num,
				5.0 * num
			},
			new double[2]
			{
				10.8 * num,
				6.5 * num
			}
		};
		for (int i = 0; i < array.Length; i++)
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = 1.8 * num;
			ellipse.Height = 1.8 * num;
			ellipse.Fill = stroke;
			Ellipse element2 = ellipse;
			Canvas.SetLeft(element2, array[i][0] - 0.9 * num);
			Canvas.SetTop(element2, array[i][1] - 0.9 * num);
			canvas2.Children.Add(element2);
		}
		return canvas2;
	}

	public static Canvas CreateGlobeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 12.6 * num;
		ellipse.Height = 12.6 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Margin = new Thickness(1.7 * num);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} A {5},{6} 0 0,0 {3},{7}", 1.7 * num, 8.0 * num, 14.3 * num, 8.0 * num, 1.7 * num, 4.2 * num, 6.3 * num, 14.3 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateImageIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 12.0 * num;
		rectangle.Height = 10.0 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(2.0 * num, 3.0 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 2.2 * num;
		ellipse.Height = 2.2 * num;
		ellipse.Fill = stroke;
		ellipse.Margin = new Thickness(4.5 * num, 5.0 * num, 0.0, 0.0);
		Ellipse element2 = ellipse;
		canvas2.Children.Add(element2);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5} L {6},{7} L {8},{9}", 3.0 * num, 11.0 * num, 6.5 * num, 7.5 * num, 9.5 * num, 10.0 * num, 11.5 * num, 8.0 * num, 13.0 * num, 11.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateArrowsSwapIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {2},{1} L {3},{5}", 3.0 * num, 5.5 * num, 13.0 * num, 10.5 * num, 3.0 * num, 8.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {2},{1} L {3},{5}", 13.0 * num, 10.5 * num, 3.0 * num, 5.5 * num, 8.0 * num, 13.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateExternalIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {4},{3} L {4},{5}", 7.0 * num, 3.0 * num, 3.0 * num, 13.0 * num, 13.0 * num, 9.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} M {4},{3} L {2},{3} L {2},{5}", 7.0 * num, 9.0 * num, 13.0 * num, 3.0 * num, 9.5 * num, 6.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateWaterDropIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {0},{1} {2},{3} {2},{4} A {5},{5} 0 0,0 {6},{4} C {6},{3} {0},{1} {0},{1} Z", 8.0 * num, 2.4 * num, 3.4 * num, 8.2 * num, 10.4 * num, 4.6 * num, 12.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCoffeeCupIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} A {5},{5} 0 0,1 {6},{7} L {8},{7} A {5},{5} 0 0,1 {9},{4} Z", 3.2 * num, 4.2 * num, 11.2 * num, 10.8 * num, 10.8 * num, 2.6 * num, 8.2 * num, 12.8 * num, 6.2 * num, 3.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} C {2},{1} {2},{3} {0},{3}", 11.2 * num, 5.8 * num, 14.2 * num, 9.4 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{1}", 2.4 * num, 14.0 * num, 13.6 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateLapFlagIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2}", 3.2 * num, 2.2 * num, 14.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {2},{5} L {0},{5} Z", 3.2 * num, 2.8 * num, 12.8 * num, 10.2 * num, 6.2 * num, 9.6 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCycleRepeatIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 0,1 {3},{4}", 3.6 * num, 8.0 * num, 5.0 * num, 13.0 * num, 7.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3}", 11.2 * num, 4.4 * num, 13.2 * num, 7.2 * num, 10.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} A {2},{2} 0 0,1 {3},{4}", 12.4 * num, 8.0 * num, 5.0 * num, 3.0 * num, 9.0 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		string data4 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3}", 4.8 * num, 11.6 * num, 2.8 * num, 8.8 * num, 5.2 * num);
		canvas2.Children.Add(CreatePath(data4, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateHeadphonesIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{3} 0 0,1 {4},{1}", 3.4 * num, 9.2 * num, 4.6 * num, 4.8 * num, 12.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 2.4 * num;
		rectangle.Height = 4.8 * num;
		rectangle.RadiusX = 1.2 * num;
		rectangle.RadiusY = 1.2 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Fill = stroke;
		rectangle.Margin = new Thickness(2.0 * num, 8.0 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 2.4 * num;
		rectangle2.Height = 4.8 * num;
		rectangle2.RadiusX = 1.2 * num;
		rectangle2.RadiusY = 1.2 * num;
		rectangle2.Stroke = stroke;
		rectangle2.StrokeThickness = opticalStroke;
		rectangle2.Fill = stroke;
		rectangle2.Margin = new Thickness(11.6 * num, 8.0 * num, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateEarbudsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 3.8 * num;
		ellipse.Height = 4.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Fill = stroke;
		ellipse.Margin = new Thickness(2.4 * num, 3.2 * num, 0.0, 0.0);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2}", 5.6 * num, 7.0 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 3.8 * num;
		ellipse2.Height = 4.0 * num;
		ellipse2.Stroke = stroke;
		ellipse2.StrokeThickness = opticalStroke;
		ellipse2.Fill = stroke;
		ellipse2.Margin = new Thickness(9.8 * num, 3.2 * num, 0.0, 0.0);
		Ellipse element2 = ellipse2;
		canvas2.Children.Add(element2);
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2}", 10.4 * num, 7.0 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateBluetoothIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} L {5},{6} L {0},{2} L {5},{7} L {3},{8}", 8.0 * num, 2.2 * num, 13.8 * num, 4.8 * num, 5.2 * num, 11.4 * num, 9.6 * num, 6.4 * num, 10.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateKeyboardIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 12.4 * num;
		rectangle.Height = 8.0 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(1.8 * num, 4.0 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{1} L {4},{1} M {5},{6} L {7},{6}", 4.2 * num, 6.8 * num, 6.2 * num, 7.6 * num, 11.8 * num, 5.6 * num, 9.4 * num, 10.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateMouseIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 8.0 * num;
		rectangle.Height = 12.0 * num;
		rectangle.RadiusX = 4.0 * num;
		rectangle.RadiusY = 4.0 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(4.0 * num, 2.0 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2}", 8.0 * num, 3.8 * num, 6.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateControllerIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {5},{1} C {6},{7} {8},{9} {10},{11} C {12},{13} {14},{13} {15},{11} C {16},{9} {17},{7} {0},{1} Z", 4.2 * num, 4.8 * num, 6.0 * num, 4.2 * num, 10.0 * num, 4.2 * num, 11.8 * num, 4.8 * num, 14.2 * num, 5.8 * num, 14.8 * num, 11.2 * num, 13.0 * num, 12.8 * num, 11.6 * num, 13.8 * num, 9.8 * num, 10.2 * num, 8.0 * num, 10.2 * num, 6.2 * num, 10.2 * num, 4.4 * num, 13.8 * num, 3.0 * num, 12.8 * num, 1.2 * num, 11.2 * num, 1.8 * num, 5.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {3},{5} M {6},{1} L {7},{1}", 4.2 * num, 7.8 * num, 6.2 * num, 5.2 * num, 6.8 * num, 8.8 * num, 10.0 * num, 11.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCheckmarkIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5}", 3.2 * num, 8.6 * num, 6.4 * num, 11.8 * num, 12.8 * num, 4.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateSparkleIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} Q {0},{2} {3},{2} Q {0},{2} {0},{4} Q {0},{2} {5},{2} Q {0},{2} {0},{1} Z", 8.0 * num, 2.4 * num, 8.0 * num, 13.6 * num, 13.6 * num, 2.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCheckCircleIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 13.0 * num;
		ellipse.Height = 13.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = opticalStroke;
		ellipse.Margin = new Thickness(1.5 * num);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5}", 4.8 * num, 8.2 * num, 7.2 * num, 10.6 * num, 11.2 * num, 5.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static FrameworkElement CreateVolumeSliderPill(double width, double height, int percent, Brush trackBrush, Brush fillBrush)
	{
		Grid grid = new Grid();
		grid.Width = width;
		grid.Height = height;
		grid.VerticalAlignment = VerticalAlignment.Center;
		Grid grid2 = grid;
		double uniformRadius = height / 2.0;
		Border border = new Border();
		border.Background = trackBrush;
		border.CornerRadius = new CornerRadius(uniformRadius);
		border.HorizontalAlignment = HorizontalAlignment.Stretch;
		border.VerticalAlignment = VerticalAlignment.Stretch;
		Border element = border;
		grid2.Children.Add(element);
		double num = Math.Max(0, Math.Min(100, percent));
		double width2 = Math.Max(height, width * (num / 100.0));
		Border border2 = new Border();
		border2.Background = fillBrush;
		border2.CornerRadius = new CornerRadius(uniformRadius);
		border2.Width = width2;
		border2.HorizontalAlignment = HorizontalAlignment.Left;
		border2.VerticalAlignment = VerticalAlignment.Stretch;
		Border element2 = border2;
		grid2.Children.Add(element2);
		return grid2;
	}

	public static Canvas CreateLightningIcon(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3} L {5},{6} L {7},{8} L {9},{8} Z", 8.5 * num, 1.5 * num, 4.5 * num, 9.0 * num, 8.0 * num, 9.0 * num, 7.5 * num, 14.5 * num, 11.5 * num, 7.0 * num, 8.0 * num, 7.0 * num);
		canvas2.Children.Add(CreatePath(data, fill, 0.5, fill));
		return canvas2;
	}

	public static Canvas CreateSunWeatherIcon(double size, Brush color)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 24.0;
		double thickness = Math.Max(1.4, 1.8 * num);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 10.0 * num;
		ellipse.Height = 10.0 * num;
		ellipse.Fill = color;
		ellipse.Margin = new Thickness(7.0 * num);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {0},{3} L {0},{4} M {1},{0} L {2},{0} M {3},{0} L {4},{0} M {5},{5} L {6},{6} M {7},{7} L {8},{8} M {5},{7} L {6},{8} M {7},{5} L {8},{6}", 12.0 * num, 2.0 * num, 4.5 * num, 19.5 * num, 22.0 * num, 4.93 * num, 6.7 * num, 17.3 * num, 19.07 * num);
		canvas2.Children.Add(CreatePath(data, color, thickness));
		return canvas2;
	}

	public static Canvas CreateRainWeatherIcon(double size, Brush color)
	{
		return CreateWeatherRainLine(size, color);
	}

	public static Canvas CreateCloudSunWeatherIcon(double size, Brush sunColor, Brush cloudColor)
	{
		return CreateWeatherPartlyCloudyLine(size, cloudColor);
	}

	public static Canvas CreateSystemStatusIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 13.2 * num;
		rectangle.Height = 8.8 * num;
		rectangle.RadiusX = 1.4 * num;
		rectangle.RadiusY = 1.4 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(1.4 * num, 1.8 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{2} L {4},{2}", 8.0 * num, 10.6 * num, 13.5 * num, 5.0 * num, 11.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {5},{6} L {7},{8} L {9},{1} L {10},{1}", 3.0 * num, 6.2 * num, 5.0 * num, 6.4 * num, 4.0 * num, 7.8 * num, 8.4 * num, 9.2 * num, 5.0 * num, 10.5 * num, 13.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 0.95));
		return canvas2;
	}

	public static Canvas CreateBatteryGlyph(double width, double height, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = width;
		canvas.Height = height;
		Canvas canvas2 = canvas;
		double num = height / 10.0;
		double strokeThickness = 1.2;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = width - 3.0 * num;
		rectangle.Height = height - 1.0 * num;
		rectangle.RadiusX = 2.2 * num;
		rectangle.RadiusY = 2.2 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = strokeThickness;
		rectangle.Margin = new Thickness(0.5 * num, 0.5 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 1.5 * num;
		rectangle2.Height = 4.0 * num;
		rectangle2.RadiusX = 0.8 * num;
		rectangle2.RadiusY = 0.8 * num;
		rectangle2.Fill = stroke;
		rectangle2.Margin = new Thickness(width - 2.0 * num, (height - 4.0 * num) / 2.0, 0.0, 0.0);
		Rectangle element2 = rectangle2;
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreatePlugIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2}", 5.4 * num, 1.6 * num, 5.0 * num, 10.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 8.8 * num;
		rectangle.Height = 5.2 * num;
		rectangle.RadiusX = 1.4 * num;
		rectangle.RadiusY = 1.4 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(3.6 * num, 5.0 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2}", 8.0 * num, 10.2 * num, 14.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateHeartIcon(double size, Brush stroke, Brush fill = null)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		string source = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {10},{12} C {10},{13} {12},{14} {0},{15} C {16},{14} {18},{13} {18},{12} C {18},{11} {20},{9} {22},{7} C {24},{5} {26},{3} {0},{1} Z", 8.0 * num, 13.8 * num, 4.8 * num, 11.0 * num, 1.8 * num, 8.2 * num, 1.8 * num, 5.2 * num, 1.8 * num, 3.0 * num, 3.4 * num, 1.6 * num, 5.6 * num, 1.6 * num, 6.8 * num, 1.6 * num, 7.6 * num, 2.3 * num, 8.4 * num, 2.3 * num, 9.2 * num, 1.6 * num, 10.4 * num, 1.6 * num, 12.6 * num, 1.6 * num, 14.2 * num, 3.0 * num, 14.2 * num, 5.2 * num, 14.2 * num, 8.2 * num, 11.2 * num, 11.0 * num);
		Brush brush = ((fill != null) ? fill : stroke);
		Path path = new Path();
		path.Data = Geometry.Parse(source);
		path.Fill = brush;
		path.Stroke = brush;
		path.StrokeThickness = 0.5 * num;
		Path element = path;
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateThermometerIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 0,1 {3},{1} L {3},{4} A {5},{5} 0 1,1 {0},{4} Z", 6.4 * num, 3.2 * num, 1.6 * num, 9.6 * num, 9.4 * num, 2.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 2.8 * num;
		ellipse.Height = 2.8 * num;
		ellipse.Fill = stroke;
		ellipse.Margin = new Thickness(6.6 * num, 10.2 * num, 0.0, 0.0);
		Ellipse element = ellipse;
		canvas2.Children.Add(element);
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2}", 8.0 * num, 6.0 * num, 10.4 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 1.1));
		return canvas2;
	}

	public static Canvas CreateRouterIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2}", 4.8 * num, 3.0 * num, 8.8 * num, 11.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 13.0 * num;
		rectangle.Height = 4.6 * num;
		rectangle.RadiusX = 1.2 * num;
		rectangle.RadiusY = 1.2 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = opticalStroke;
		rectangle.Margin = new Thickness(1.5 * num, 8.8 * num, 0.0, 0.0);
		Rectangle element = rectangle;
		canvas2.Children.Add(element);
		for (int i = 0; i < 3; i++)
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = 1.0 * num;
			ellipse.Height = 1.0 * num;
			ellipse.Fill = stroke;
			ellipse.Margin = new Thickness((3.6 + (double)i * 2.4) * num, 10.6 * num, 0.0, 0.0);
			Ellipse element2 = ellipse;
			canvas2.Children.Add(element2);
		}
		return canvas2;
	}

	public static Canvas CreateSignalBarsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = GetOpticalStroke(size) * 1.1;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} L {3},{2} M {5},{6} L {5},{2} M {7},{8} L {7},{2}", 2.8 * num, 12.0 * num, 14.0 * num, 6.2 * num, 9.5 * num, 9.8 * num, 6.5 * num, 13.2 * num, 3.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateLeafIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {0},{1} {2},{3} {4},{5} C {6},{7} {8},{9} {8},{9} C {8},{9} {10},{11} {10},{12} C {10},{13} {14},{1} {0},{1} Z", 3.0 * num, 13.0 * num, 3.5 * num, 6.5 * num, 8.5 * num, 3.0 * num, 12.5 * num, 3.0 * num, 13.5 * num, 7.5 * num, 13.5 * num, 11.5 * num, 8.5 * num, 13.0 * num, 4.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7}", 3.0 * num, 13.0 * num, 6.0 * num, 10.0 * num, 8.5 * num, 7.5 * num, 11.0 * num, 5.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 0.8));
		return canvas2;
	}

	public static Canvas CreatePencilIcon(double size, Brush stroke)
	{
		return CreateEditIcon(size, stroke ?? BuddyTokens.Text60Brush);
	}

	public static Canvas CreateHomeOutlined(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 2.2 * num, 7.2 * num, 8.0 * num, 2.6 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2} L {3},{4} L {5},{4} L {5},{2} L {6},{2} L {6},{1}", 3.8 * num, 6.6 * num, 13.5 * num, 6.5 * num, 9.5 * num, 9.5 * num, 12.2 * num);
		canvas2.Children.Add(CreatePath(data2, stroke2, thickness));
		return canvas2;
	}

	public static Canvas CreateBatteryOutlined(double size, Brush stroke, double fillPercent = 1.0, bool isCharging = false)
	{
		double width = size * 1.35;
		double height = size * 0.75;
		Canvas canvas = new Canvas();
		canvas.Width = width;
		canvas.Height = height;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double strokeThickness = 1.5 * num;
		Brush brush = stroke ?? BuddyTokens.Text100Brush;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 16.0 * num;
		rectangle.Height = 9.0 * num;
		rectangle.RadiusX = 2.5 * num;
		rectangle.RadiusY = 2.5 * num;
		rectangle.Stroke = brush;
		rectangle.StrokeThickness = strokeThickness;
		rectangle.Fill = Brushes.Transparent;
		Rectangle element = rectangle;
		Canvas.SetLeft(element, 0.5 * num);
		Canvas.SetTop(element, 1.5 * num);
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 1.5 * num;
		rectangle2.Height = 4.0 * num;
		rectangle2.RadiusX = 0.8 * num;
		rectangle2.RadiusY = 0.8 * num;
		rectangle2.Fill = brush;
		Rectangle element2 = rectangle2;
		Canvas.SetLeft(element2, 17.0 * num);
		Canvas.SetTop(element2, 4.0 * num);
		canvas2.Children.Add(element2);
		double num2 = Math.Max(0.0, Math.Min(1.0, fillPercent));
		double num3 = 12.0 * num;
		double width2 = Math.Max(1.0 * num, num3 * num2);
		Brush fill = ((num2 <= 0.15) ? BuddyTokens.AlertBrush : brush);
		Rectangle rectangle3 = new Rectangle();
		rectangle3.Width = width2;
		rectangle3.Height = 5.0 * num;
		rectangle3.RadiusX = 1.2 * num;
		rectangle3.RadiusY = 1.2 * num;
		rectangle3.Fill = fill;
		Rectangle element3 = rectangle3;
		Canvas.SetLeft(element3, 2.5 * num);
		Canvas.SetTop(element3, 3.5 * num);
		canvas2.Children.Add(element3);
		if (isCharging)
		{
			string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3} L {5},{6} L {4},{7} L {2},{7} Z", 9.0 * num, 1.5 * num, 6.5 * num, 6.0 * num, 8.5 * num, 6.0 * num, 7.0 * num, 10.5 * num, 10.5 * num, 5.0 * num, 8.5 * num, 5.0 * num);
			canvas2.Children.Add(CreatePath(data, BuddyTokens.CanvasBrush, 0.5, BuddyTokens.Text100Brush));
		}
		return canvas2;
	}

	public static Canvas CreatePlayOutlined(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Brush brush = stroke ?? BuddyTokens.Text100Brush;
		PathFigure pathFigure = new PathFigure();
		pathFigure.StartPoint = new Point(5.5 * num, 3.5 * num);
		pathFigure.IsClosed = true;
		pathFigure.IsFilled = true;
		PathFigure pathFigure2 = pathFigure;
		pathFigure2.Segments.Add(new LineSegment(new Point(12.5 * num, 8.0 * num), isStroked: true));
		pathFigure2.Segments.Add(new LineSegment(new Point(5.5 * num, 12.5 * num), isStroked: true));
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		canvas2.Children.Add(new Path
		{
			Data = pathGeometry,
			Fill = brush,
			Stroke = brush,
			StrokeThickness = 1.5 * num,
			StrokeLineJoin = PenLineJoin.Round
		});
		return canvas2;
	}

	public static Canvas CreatePauseOutlined(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{1} L {3},{2}", 5.5 * num, 3.5 * num, 12.5 * num, 10.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		return canvas2;
	}

	public static Canvas CreateCopyOutlined(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} C {3},{1} {4},{5} {4},{6} L {4},{7} C {4},{8} {3},{9} {2},{9} L {0},{9} C {10},{9} {11},{8} {11},{7} L {11},{6} C {11},{5} {10},{1} {0},{1} Z", 5.5 * num, 2.5 * num, 11.5 * num, 12.5 * num, 13.0 * num, 3.0 * num, 3.5 * num, 10.0 * num, 10.5 * num, 11.0 * num, 4.5 * num, 4.0 * num);
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} C {3},{1} {4},{5} {4},{6} L {4},{7} C {4},{8} {3},{9} {2},{9} L {0},{9} C {10},{9} {11},{8} {11},{7} L {11},{6} C {11},{5} {10},{1} {0},{1} Z", 3.5 * num, 5.5 * num, 9.5 * num, 10.5 * num, 11.0 * num, 6.0 * num, 6.5 * num, 13.0 * num, 13.5 * num, 14.0 * num, 2.5 * num, 2.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		canvas2.Children.Add(CreatePath(data2, stroke2, thickness));
		return canvas2;
	}

	public static Canvas CreateCheckOutlined(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5}", 3.5 * num, 8.5 * num, 6.5 * num, 11.8 * num, 12.8 * num, 4.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		return canvas2;
	}

	public static Canvas CreatePlusOutlined(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {3},{5}", 3.0 * num, 8.0 * num, 13.0 * num, 8.0 * num, 3.0 * num, 13.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		return canvas2;
	}

	public static Canvas CreateMoreDots(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Brush fill2 = fill ?? BuddyTokens.Text60Brush;
		double num2 = 1.25 * num;
		for (int i = 0; i < 3; i++)
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = num2 * 2.0;
			ellipse.Height = num2 * 2.0;
			ellipse.Fill = fill2;
			Ellipse element = ellipse;
			Canvas.SetLeft(element, (4.0 + (double)i * 4.0 - num2) * num);
			Canvas.SetTop(element, (8.0 - num2) * num);
			canvas2.Children.Add(element);
		}
		return canvas2;
	}

	private static string GetCanonicalWeatherCloud(double s, double dy = 0.0)
	{
		return string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {12},{13} C {14},{15} {16},{17} {18},{19} C {20},{21} {22},{23} {0},{1} Z", 4.5 * s, (12.2 + dy) * s, 2.4 * s, (12.2 + dy) * s, 1.8 * s, (9.6 + dy) * s, 3.4 * s, (8.5 + dy) * s, 3.4 * s, (6.0 + dy) * s, 6.2 * s, (4.5 + dy) * s, 8.8 * s, (6.0 + dy) * s, 10.8 * s, (5.0 + dy) * s, 13.8 * s, (6.5 + dy) * s, 13.8 * s, (9.2 + dy) * s, 15.2 * s, (10.8 + dy) * s, 14.5 * s, (12.2 + dy) * s);
	}

	public static Canvas CreateWeatherClearLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 6.5 * num;
		ellipse.Height = 6.5 * num;
		ellipse.Stroke = stroke2;
		ellipse.StrokeThickness = num2;
		ellipse.Fill = Brushes.Transparent;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 4.75 * num);
		Canvas.SetTop(element, 4.75 * num);
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {0},{3} L {0},{4} M {1},{0} L {2},{0} M {3},{0} L {4},{0} M {5},{5} L {6},{6} M {7},{7} L {8},{8} M {7},{5} L {8},{6} M {5},{7} L {6},{8}", 8.0 * num, 1.4 * num, 3.2 * num, 12.8 * num, 14.6 * num, 3.3 * num, 4.6 * num, 12.7 * num, 11.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, num2));
		return canvas2;
	}

	public static Canvas CreateWeatherMoonLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {0},{1} Z", 9.5 * num, 2.0 * num, 14.5 * num, 4.0 * num, 14.5 * num, 12.0 * num, 9.5 * num, 14.0 * num, 12.5 * num, 11.0 * num, 12.5 * num, 5.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness, Brushes.Transparent));
		return canvas2;
	}

	public static Canvas CreateWeatherPartlyCloudyLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 0 1 {3},{4} M {5},{6} L {7},{8} M {9},{10} L {11},{12} M {13},{14} L {15},{16}", 8.5 * num, 4.0 * num, 3.2 * num, 13.5 * num, 6.2 * num, 11.0 * num, 2.0 * num, 11.4 * num, 1.0 * num, 13.2 * num, 3.4 * num, 14.2 * num, 2.6 * num, 14.4 * num, 5.5 * num, 15.4 * num, 6.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		string canonicalWeatherCloud = GetCanonicalWeatherCloud(num, 0.5);
		canvas2.Children.Add(CreatePath(canonicalWeatherCloud, stroke2, thickness, Brushes.Transparent));
		return canvas2;
	}

	public static Canvas CreateWeatherPartlyCloudyNightLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {0},{1}", 9.5 * num, 2.2 * num, 13.5 * num, 3.5 * num, 14.0 * num, 8.0 * num, 11.0 * num, 9.5 * num, 12.5 * num, 7.0 * num, 12.0 * num, 4.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		string canonicalWeatherCloud = GetCanonicalWeatherCloud(num, 0.5);
		canvas2.Children.Add(CreatePath(canonicalWeatherCloud, stroke2, thickness, Brushes.Transparent));
		return canvas2;
	}

	public static Canvas CreateWeatherCloudyLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string canonicalWeatherCloud = GetCanonicalWeatherCloud(num);
		canvas2.Children.Add(CreatePath(canonicalWeatherCloud, stroke2, thickness, Brushes.Transparent));
		return canvas2;
	}

	public static Canvas CreateWeatherRainLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string canonicalWeatherCloud = GetCanonicalWeatherCloud(num, -2.0);
		canvas2.Children.Add(CreatePath(canonicalWeatherCloud, stroke2, thickness, Brushes.Transparent));
		string data = string.Format(Inv, "M {0},{1} L {2},{3} M {4},{1} L {5},{3} M {6},{1} L {7},{3}", 5.0 * num, 11.8 * num, 3.8 * num, 14.5 * num, 8.5 * num, 11.8 * num, 7.3 * num, 14.5 * num, 12.0 * num, 11.8 * num, 10.8 * num, 14.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		return canvas2;
	}

	public static Canvas CreateWeatherStormLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string canonicalWeatherCloud = GetCanonicalWeatherCloud(num, -2.0);
		canvas2.Children.Add(CreatePath(canonicalWeatherCloud, stroke2, thickness, Brushes.Transparent));
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3} L {5},{6}", 8.5 * num, 9.2 * num, 6.2 * num, 12.0 * num, 8.4 * num, 12.0 * num, 7.0 * num, 15.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		return canvas2;
	}

	public static Canvas CreateWeatherSnowLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush brush = stroke ?? BuddyTokens.Text100Brush;
		string canonicalWeatherCloud = GetCanonicalWeatherCloud(num, -2.0);
		canvas2.Children.Add(CreatePath(canonicalWeatherCloud, brush, thickness, Brushes.Transparent));
		double num2 = 1.0 * num;
		double[] array = new double[3]
		{
			5.0 * num,
			8.5 * num,
			12.0 * num
		};
		double num3 = 13.2 * num;
		for (int i = 0; i < 3; i++)
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = num2 * 2.0;
			ellipse.Height = num2 * 2.0;
			ellipse.Fill = brush;
			Ellipse element = ellipse;
			Canvas.SetLeft(element, array[i] - num2);
			Canvas.SetTop(element, num3 - num2);
			canvas2.Children.Add(element);
		}
		return canvas2;
	}

	public static Canvas CreateWeatherFogLine(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		Brush stroke2 = stroke ?? BuddyTokens.Text100Brush;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {5},{4} M {0},{6} L {2},{6}", 3.0 * num, 5.5 * num, 13.0 * num, 4.5 * num, 8.5 * num, 11.5 * num, 3.0 * num, 11.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke2, thickness));
		return canvas2;
	}

	public static Border CreatePaperDocTile(double size = 24.0)
	{
		Border border = new Border();
		border.Width = size;
		border.Height = size;
		border.CornerRadius = new CornerRadius(5.0);
		border.Background = new SolidColorBrush(Color.FromRgb(233, 233, 236));
		border.SnapsToDevicePixels = true;
		border.UseLayoutRounding = true;
		Border border2 = border;
		if (border2.Background.CanFreeze)
		{
			border2.Background.Freeze();
		}
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 24.0;
		double strokeThickness = 1.6 * num;
		Brush brush = new SolidColorBrush(Color.FromRgb(158, 158, 167));
		if (brush.CanFreeze)
		{
			brush.Freeze();
		}
		string source = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {4},{3} M {0},{5} L {6},{5}", 6.0 * num, 7.5 * num, 18.0 * num, 12.0 * num, 15.0 * num, 16.5 * num, 12.0 * num);
		Path path = new Path();
		path.Stroke = brush;
		path.StrokeThickness = strokeThickness;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		path.Data = Geometry.Parse(source);
		Path element = path;
		canvas2.Children.Add(element);
		border2.Child = canvas2;
		return border2;
	}

	public static Canvas CreateEdgeLightWaveIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {12},{13}", 1.5 * num, 8.0 * num, 3.5 * num, 3.5 * num, 5.5 * num, 3.5 * num, 8.0 * num, 8.0 * num, 10.5 * num, 12.5 * num, 12.5 * num, 12.5 * num, 14.5 * num, 8.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreatePowerLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} A {5},{5} 0 1 0 {6},{4}", 8.0 * num, 2.0 * num, 7.5 * num, 4.8 * num, 4.5 * num, 5.6 * num, 11.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateDirectionArrowsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {2},{1} L {3},{5} M {2},{6} L {0},{6} M {7},{8} L {0},{6} L {7},{9}", 2.5 * num, 5.5 * num, 13.5 * num, 10.5 * num, 2.5 * num, 8.5 * num, 10.5 * num, 5.5 * num, 7.5 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateClockwiseIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.3 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 1 {3},{4} M {5},{6} L {3},{4} L {7},{8}", 3.2 * num, 8.0 * num, 4.8 * num, 8.0 * num, 3.2 * num, 5.6 * num, 1.2 * num, 6.0 * num, 5.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateCounterClockwiseIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.3 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 0 {3},{4} M {5},{6} L {3},{4} L {7},{8}", 12.8 * num, 8.0 * num, 4.8 * num, 8.0 * num, 3.2 * num, 10.4 * num, 1.2 * num, 10.0 * num, 5.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreatePingPongIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {2},{1} L {3},{5} M {2},{6} L {0},{6} M {7},{8} L {0},{6} L {7},{9}", 2.5 * num, 6.0 * num, 13.5 * num, 11.0 * num, 3.5 * num, 8.5 * num, 10.0 * num, 5.0 * num, 7.5 * num, 12.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateSpeedometerIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 1 {3},{1} M {4},{5} L {6},{7}", 3.5 * num, 12.5 * num, 5.8 * num, 12.5 * num, 8.0 * num, 9.0 * num, 11.5 * num, 5.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateSunBrightnessIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {0},{3} L {0},{4} M {1},{0} L {2},{0} M {3},{0} L {4},{0} M {5},{5} L {6},{6} M {7},{7} L {8},{8} M {5},{7} L {6},{8} M {7},{5} L {8},{6}", 8.0 * num, 1.5 * num, 3.5 * num, 12.5 * num, 14.5 * num, 3.4 * num, 4.8 * num, 12.6 * num, 11.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 6.0 * num;
		ellipse.Height = 6.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = num2;
		ellipse.Fill = Brushes.Transparent;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 5.0 * num);
		Canvas.SetTop(element, 5.0 * num);
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateLineWidthIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		canvas2.Children.Add(CreatePath(string.Format(Inv, "M {0},{1} L {2},{1}", 2.5 * num, 4.0 * num, 13.5 * num), stroke, 1.2 * num));
		canvas2.Children.Add(CreatePath(string.Format(Inv, "M {0},{1} L {2},{1}", 2.5 * num, 8.0 * num, 13.5 * num), stroke, 2.0 * num));
		canvas2.Children.Add(CreatePath(string.Format(Inv, "M {0},{1} L {2},{1}", 2.5 * num, 12.0 * num, 13.5 * num), stroke, 2.8 * num));
		return canvas2;
	}

	public static Canvas CreateTrailLengthIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double[] array = new double[4]
		{
			3.0 * num,
			6.3 * num,
			9.7 * num,
			13.0 * num
		};
		for (int i = 0; i < array.Length; i++)
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = 2.4 * num;
			ellipse.Height = 2.4 * num;
			ellipse.Fill = stroke;
			Ellipse element = ellipse;
			Canvas.SetLeft(element, array[i] - 1.2 * num);
			Canvas.SetTop(element, 8.0 * num - 1.2 * num);
			canvas2.Children.Add(element);
		}
		return canvas2;
	}

	public static Canvas CreateGlowSoftnessIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 0 {0},{3}", 8.0 * num, 4.5 * num, 3.5 * num, 11.5 * num);
		Path path = CreatePath(data, stroke, thickness);
		path.StrokeDashArray = new DoubleCollection { 1.0, 2.0 };
		canvas2.Children.Add(path);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 3.0 * num;
		ellipse.Height = 3.0 * num;
		ellipse.Fill = stroke;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 6.5 * num);
		Canvas.SetTop(element, 6.5 * num);
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateAnimationModeIcon(double size, Brush stroke)
	{
		return CreatePowerLineIcon(size, stroke);
	}

	public static Canvas CreateSharpSweepIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {5},{6} L {7},{1} L {8},{1}", 2.0 * num, 9.5 * num, 5.5 * num, 7.5 * num, 4.0 * num, 10.5 * num, 12.0 * num, 12.5 * num, 14.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateDottedTrailIcon(double size, Brush stroke)
	{
		return CreateTrailLengthIcon(size, stroke);
	}

	public static Canvas CreateDualSweepIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.3 * num;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {12},{13}", 2.0 * num, 6.0 * num, 4.0 * num, 3.5 * num, 6.0 * num, 3.5 * num, 8.0 * num, 6.0 * num, 10.0 * num, 8.5 * num, 12.0 * num, 8.5 * num, 14.0 * num, 6.0 * num);
		string data2 = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {12},{13}", 2.0 * num, 10.5 * num, 4.0 * num, 8.0 * num, 6.0 * num, 8.0 * num, 8.0 * num, 10.5 * num, 10.0 * num, 13.0 * num, 12.0 * num, 13.0 * num, 14.0 * num, 10.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		canvas2.Children.Add(CreatePath(data2, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateTargetPulseIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double strokeThickness = 1.5 * num;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 11.0 * num;
		ellipse.Height = 11.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = strokeThickness;
		ellipse.Fill = Brushes.Transparent;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 2.5 * num);
		Canvas.SetTop(element, 2.5 * num);
		canvas2.Children.Add(element);
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 4.0 * num;
		ellipse2.Height = 4.0 * num;
		ellipse2.Fill = stroke;
		Ellipse element2 = ellipse2;
		Canvas.SetLeft(element2, 6.0 * num);
		Canvas.SetTop(element2, 6.0 * num);
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateBreathingArcIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.5 * num;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {5},{1}", 2.5 * num, 11.5 * num, 6.0 * num, 4.5 * num, 10.0 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateInfinityIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.4 * num;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {4},{1} C {4},{5} {2},{5} {0},{1} C {6},{5} {7},{5} {7},{1} C {7},{3} {6},{3} {0},{1}", 8.0 * num, 8.0 * num, 5.5 * num, 5.0 * num, 3.0 * num, 11.0 * num, 10.5 * num, 13.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateClockLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 12.5 * num;
		ellipse.Height = 12.5 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = num2;
		ellipse.Fill = Brushes.Transparent;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 1.75 * num);
		Canvas.SetTop(element, 1.75 * num);
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2}", 8.0 * num, 4.5 * num, 8.0 * num, 11.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2));
		return canvas2;
	}

	public static Canvas CreateVolumeLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {3},{5} L {2},{6} L {0},{6} Z", 2.5 * num, 6.0 * num, 5.0 * num, 8.5 * num, 3.2 * num, 12.8 * num, 10.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		string data2 = string.Format(Inv, "M {0},{1} A {2},{2} 0 0 1 {0},{3} M {4},{5} A {6},{6} 0 0 1 {4},{7}", 10.5 * num, 5.5 * num, 4.0 * num, 10.5 * num, 12.5 * num, 3.5 * num, 6.5 * num, 12.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateWifiLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 0 1 {3},{1} M {4},{5} A {6},{6} 0 0 1 {7},{5} M {8},{9} A {10},{10} 0 0 1 {11},{9}", 2.0 * num, 5.0 * num, 8.5 * num, 14.0 * num, 4.0 * num, 7.8 * num, 5.8 * num, 12.0 * num, 6.2 * num, 10.5 * num, 3.0 * num, 9.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 2.0 * num;
		ellipse.Height = 2.0 * num;
		ellipse.Fill = stroke;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 7.0 * num);
		Canvas.SetTop(element, 12.8 * num);
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateBatteryLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 11.5 * num;
		rectangle.Height = 6.5 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = num2;
		rectangle.Fill = Brushes.Transparent;
		Rectangle element = rectangle;
		Canvas.SetLeft(element, 1.8 * num);
		Canvas.SetTop(element, 4.75 * num);
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {0},{2}", 14.2 * num, 6.8 * num, 9.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2 * 1.1));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1}", 4.2 * num, 8.0 * num, 9.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, num2 * 1.2));
		return canvas2;
	}

	public static Canvas CreateAppLauncherLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z M {4},{1} L {5},{1} L {5},{3} L {4},{3} Z M {0},{4} L {2},{4} L {2},{5} L {0},{5} Z M {4},{4} L {5},{4} L {5},{5} L {4},{5} Z", 2.5 * num, 2.5 * num, 6.5 * num, 6.5 * num, 9.5 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateBluetoothLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {0},{1} L {3},{4} L {0},{5} L {3},{6} L {0},{2} M {7},{4} L {0},{5} M {7},{6} L {0},{1}", 8.0 * num, 2.2 * num, 13.8 * num, 12.0 * num, 5.0 * num, 8.0 * num, 11.0 * num, 4.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateDoNotDisturbLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 12.5 * num;
		ellipse.Height = 12.5 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = num2;
		ellipse.Fill = Brushes.Transparent;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 1.75 * num);
		Canvas.SetTop(element, 1.75 * num);
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{1}", 4.5 * num, 8.0 * num, 11.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2 * 1.1));
		return canvas2;
	}

	public static Canvas CreateNowPlayingLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{4} L {3},{5} M {0},{6} L {3},{7}", 5.5 * num, 12.0 * num, 5.5 * num, 4.2 * num, 12.5 * num, 2.5 * num, 12.5 * num, 10.2 * num, 5.5 * num, 6.2 * num, 12.5 * num, 4.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 3.6 * num;
		ellipse.Height = 2.8 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = num2;
		ellipse.Fill = Brushes.Transparent;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 2.2 * num);
		Canvas.SetTop(element, 10.2 * num);
		canvas2.Children.Add(element);
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 3.6 * num;
		ellipse2.Height = 2.8 * num;
		ellipse2.Stroke = stroke;
		ellipse2.StrokeThickness = num2;
		ellipse2.Fill = Brushes.Transparent;
		Ellipse element2 = ellipse2;
		Canvas.SetLeft(element2, 9.2 * num);
		Canvas.SetTop(element2, 8.4 * num);
		canvas2.Children.Add(element2);
		return canvas2;
	}

	public static Canvas CreateNotificationBadgeLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 0 1 {3},{1} L {3},{4} L {5},{6} L {7},{6} L {8},{4} L {8},{1} M {9},{6} A {10},{10} 0 0 1 {11},{6}", 6.0 * num, 4.5 * num, 2.0 * num, 10.0 * num, 8.5 * num, 12.2 * num, 11.2 * num, 3.8 * num, 6.0 * num, 6.8 * num, 1.4 * num, 9.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 3.0 * num;
		ellipse.Height = 3.0 * num;
		ellipse.Fill = stroke;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 11.2 * num);
		Canvas.SetTop(element, 1.8 * num);
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateClipboardQuickAccessLineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 10.5 * num;
		rectangle.Height = 12.0 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = num2;
		rectangle.Fill = Brushes.Transparent;
		Rectangle element = rectangle;
		Canvas.SetLeft(element, 2.75 * num);
		Canvas.SetTop(element, 2.5 * num);
		canvas2.Children.Add(element);
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 5.0 * num;
		rectangle2.Height = 2.4 * num;
		rectangle2.RadiusX = 1.0 * num;
		rectangle2.RadiusY = 1.0 * num;
		rectangle2.Stroke = stroke;
		rectangle2.StrokeThickness = num2;
		rectangle2.Fill = Brushes.Transparent;
		Rectangle element2 = rectangle2;
		Canvas.SetLeft(element2, 5.5 * num);
		Canvas.SetTop(element2, 1.2 * num);
		canvas2.Children.Add(element2);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {4},{3}", 5.2 * num, 6.8 * num, 10.8 * num, 9.6 * num, 9.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2));
		return canvas2;
	}

	public static Canvas CreateCornerRadiusIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} A {3},{3} 0 0 0 {2},{0} L {1},{0}", 2.5 * num, 13.5 * num, 7.5 * num, 5.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3}", 7.5 * num, 7.5 * num, 12.5 * num, 12.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, num2 * 0.9));
		return canvas2;
	}

	public static Canvas CreateBorderOutlineIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double strokeThickness = 1.35 * num;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 12.0 * num;
		rectangle.Height = 9.0 * num;
		rectangle.RadiusX = 2.5 * num;
		rectangle.RadiusY = 2.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = strokeThickness;
		rectangle.Fill = Brushes.Transparent;
		Rectangle element = rectangle;
		Canvas.SetLeft(element, 2.0 * num);
		Canvas.SetTop(element, 3.5 * num);
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateWallpaperIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 12.5 * num;
		rectangle.Height = 9.5 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = num2;
		rectangle.Fill = Brushes.Transparent;
		Rectangle element = rectangle;
		Canvas.SetLeft(element, 1.75 * num);
		Canvas.SetTop(element, 3.25 * num);
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1} M {5},{6} L {7},{8} L {9},{6}", 3.5 * num, 10.5 * num, 6.0 * num, 6.8 * num, 8.5 * num, 7.5 * num, 10.5 * num, 10.0 * num, 7.8 * num, 12.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2 * 0.95));
		return canvas2;
	}

	public static Canvas CreateAnimationSpeedIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{3} L {5},{6} L {7},{8} L {9},{8} Z", 8.5 * num, 2.0 * num, 4.5 * num, 9.0 * num, 7.5 * num, 6.5 * num, 14.5 * num, 11.5 * num, 7.5 * num, 8.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateAutoHideIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1} M {2},{5} L {2},{3}", 3.5 * num, 8.0 * num, 8.0 * num, 12.5 * num, 12.5 * num, 3.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateBatterySaverIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 11.5 * num;
		rectangle.Height = 6.5 * num;
		rectangle.RadiusX = 1.5 * num;
		rectangle.RadiusY = 1.5 * num;
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = num2;
		rectangle.Fill = Brushes.Transparent;
		Rectangle element = rectangle;
		Canvas.SetLeft(element, 1.8 * num);
		Canvas.SetTop(element, 4.75 * num);
		canvas2.Children.Add(element);
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {5},{1} C {4},{6} {2},{6} {0},{1}", 4.5 * num, 8.0 * num, 6.0 * num, 6.0 * num, 9.0 * num, 10.5 * num, 10.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2 * 0.95));
		return canvas2;
	}

	public static Canvas CreateFocusAssistIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 0 {3},{4} A {5},{5} 0 0 1 {0},{1}", 8.0 * num, 2.5 * num, 5.5 * num, 13.5 * num, 8.0 * num, 5.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateGamingMonitorIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double num2 = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {5},{1} C {6},{7} {8},{9} {10},{9} C {11},{9} {12},{7} {0},{1} Z", 3.0 * num, 6.0 * num, 5.0 * num, 4.0 * num, 11.0 * num, 13.0 * num, 14.5 * num, 11.0 * num, 12.5 * num, 13.0 * num, 10.5 * num, 8.5 * num, 3.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, num2));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{1} M {3},{4} L {3},{5}", 4.8 * num, 8.0 * num, 7.2 * num, 6.0 * num, 6.8 * num, 9.2 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, num2 * 0.9));
		return canvas2;
	}

	public static Canvas CreatePresentationIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {2},{3} L {0},{3} Z M {4},{3} L {4},{5} M {6},{7} L {4},{5} L {8},{7}", 2.5 * num, 2.5 * num, 13.5 * num, 9.5 * num, 8.0 * num, 13.5 * num, 5.0 * num, 15.0 * num, 11.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateHoverRevealIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} M {3},{4} L {5},{6} M {7},{4} L {8},{6} M {9},{10} L {11},{12} L {13},{14} L {15},{12} Z", 8.0 * num, 1.5 * num, 4.5 * num, 4.0 * num, 2.5 * num, 6.0 * num, 4.5 * num, 12.0 * num, 10.0 * num, 8.0 * num, 6.0 * num, 12.5 * num, 10.5 * num, 8.0 * num, 14.5 * num, 3.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateExceptionAppsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{1} C {2},{3} {4},{5} {6},{7} C {8},{5} {0},{3} {0},{1} Z", 3.0 * num, 3.5 * num, 13.0 * num, 8.5 * num, 12.5 * num, 11.5 * num, 8.0 * num, 14.5 * num, 3.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateRocketLaunchIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} L {8},{9} C {10},{11} {12},{13} {0},{1} Z", 13.5 * num, 2.5 * num, 11.0 * num, 5.0 * num, 8.5 * num, 6.5 * num, 6.5 * num, 9.5 * num, 6.5 * num, 6.5 * num, 9.5 * num, 4.5 * num, 11.0 * num, 2.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5}", 4.5 * num, 11.5 * num, 2.5 * num, 13.5 * num, 6.5 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreatePerformanceSpeedIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 1 {3},{1} M {4},{5} L {6},{7}", 3.0 * num, 11.5 * num, 6.0 * num, 13.0 * num, 8.0 * num, 9.5 * num, 11.0 * num, 6.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateResetDefaultsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1 1 {3},{4} M {5},{6} L {3},{4} L {7},{8}", 3.5 * num, 8.0 * num, 4.8 * num, 8.0 * num, 3.2 * num, 5.6 * num, 1.2 * num, 6.0 * num, 5.6 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateExportJsonIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2} L {3},{1} M {4},{5} L {4},{6} M {7},{8} L {4},{5} L {9},{8}", 2.5 * num, 9.5 * num, 13.5 * num, 13.5 * num, 8.0 * num, 2.0 * num, 9.5 * num, 5.5 * num, 4.5 * num, 10.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateImportJsonIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2} L {3},{1} M {4},{5} L {4},{6} M {7},{8} L {4},{6} L {9},{8}", 2.5 * num, 9.5 * num, 13.5 * num, 13.5 * num, 8.0 * num, 2.0 * num, 9.5 * num, 5.5 * num, 7.0 * num, 10.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreatePlayIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4} Z", 4.5 * num, 3.5 * num, 12.5 * num, 8.0 * num, 4.5 * num, 12.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateGamepadIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = 1.35 * num;
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{3} {5},{1} C {6},{7} {8},{9} {10},{11} C {12},{13} {14},{13} {15},{11} C {17},{9} {19},{7} {0},{1} Z", 3.5 * num, 5.0 * num, 5.5 * num, 4.0 * num, 10.5 * num, 4.0 * num, 12.5 * num, 5.0 * num, 14.5 * num, 7.5 * num, 14.0 * num, 11.5 * num, 12.0 * num, 12.0 * num, 10.5 * num, 11.0 * num, 9.5 * num, 8.0 * num, 8.0 * num, 8.5 * num, 6.5 * num, 8.0 * num, 5.5 * num, 11.0 * num, 4.0 * num, 12.0 * num, 2.0 * num, 11.5 * num, 1.5 * num, 7.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateLayersIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4} L {5},{3} Z", 8.0 * num, 2.0 * num, 14.0 * num, 5.0 * num, 8.0 * num, 2.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 2.2 * num, 8.0 * num, 8.0 * num, 11.0 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 2.2 * num, 11.0 * num, 8.0 * num, 14.0 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCloudUploadIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 24.0;
		double thickness = GetOpticalStroke(size) * 1.05;
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,1 {3},{4} H {5} A {6},{6} 0 0,1 {7},{8}", 4.0 * num, 14.9 * num, 7.0 * num, 15.7 * num, 8.0 * num, 17.5 * num, 4.5 * num, 19.5 * num, 16.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2}", 12.0 * num, 20.0 * num, 12.0 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, thickness));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 8.5 * num, 15.5 * num, 12.0 * num, 12.0 * num, 15.5 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateChainLinkIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 24.0;
		double thickness = GetOpticalStroke(size) * 1.1;
		string data = string.Format(Inv, "M {0},{1} H {2} A {3},{3} 0 0,1 {2},{4} H {0}", 9.0 * num, 17.0 * num, 7.0 * num, 5.0 * num, 7.0 * num);
		string data2 = string.Format(Inv, "M {0},{1} H {2} A {3},{3} 0 0,1 {2},{4} H {0}", 15.0 * num, 7.0 * num, 17.0 * num, 5.0 * num, 17.0 * num);
		string data3 = string.Format(Inv, "M {0},{1} H {2}", 8.0 * num, 12.0 * num, 16.0 * num);
		Path element = CreatePath(data, stroke, thickness);
		Path element2 = CreatePath(data2, stroke, thickness);
		Path element3 = CreatePath(data3, stroke, thickness);
		canvas2.Children.Add(element);
		canvas2.Children.Add(element2);
		canvas2.Children.Add(element3);
		canvas2.RenderTransformOrigin = new Point(0.5, 0.5);
		canvas2.RenderTransform = new RotateTransform(-45.0);
		return canvas2;
	}

	public static Canvas CreateDocumentPdfIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {3},{5} L {0},{5} Z", 3.2 * num, 2.2 * num, 9.6 * num, 12.8 * num, 5.4 * num, 13.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} L {0},{2} L {3},{2}", 9.6 * num, 2.2 * num, 5.4 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke * 0.9));
		string data3 = string.Format(Inv, "M {0},{1} L {2},{1} M {0},{3} L {4},{3}", 5.4 * num, 8.2 * num, 10.6 * num, 10.8 * num, 9.2 * num);
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke * 0.9));
		return canvas2;
	}

	public static Canvas CreateHeicBadgeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{1} L {3},{4} L {5},{4} L {6},{1} L {7},{1} L {7},{8} L {0},{8} Z", 2.5 * num, 5.5 * num, 5.0 * num, 6.0 * num, 3.5 * num, 10.0 * num, 11.0 * num, 13.5 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		string data2 = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,0 {3},{1} A {2},{2} 0 1,0 {0},{1}", 5.8 * num, 9.2 * num, 2.2 * num, 10.2 * num);
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateMinusIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double thickness = GetOpticalStroke(size) * 1.25;
		string data = string.Format(Inv, "M {0},{1} L {2},{1}", 3.5 * num, 8.0 * num, 12.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, thickness));
		return canvas2;
	}

	public static Canvas CreateMoreDotsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,0 {3},{1} A {2},{2} 0 1,0 {0},{1} M {4},{1} A {2},{2} 0 1,0 {5},{1} A {2},{2} 0 1,0 {4},{1} M {6},{1} A {2},{2} 0 1,0 {7},{1} A {2},{2} 0 1,0 {6},{1}", 3.2 * num, 8.0 * num, 1.1 * num, 5.4 * num, 6.9 * num, 9.1 * num, 10.6 * num, 12.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke, stroke));
		return canvas2;
	}

	public static Canvas CreateAcrobatEmblemIcon(double size, Brush fill)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		Path path = new Path();
		path.Fill = fill;
		path.Stretch = Stretch.Uniform;
		path.Width = size;
		path.Height = size;
		path.Data = Geometry.Parse("M 19.38,15.75 C 18.52,14.07 16.54,12.72 14.15,12.02 C 15.08,9.75 15.22,7.38 14.34,6.23 C 13.56,5.21 12.28,4.72 11.08,5 C 9.55,5.36 8.35,7.03 7.91,9.45 C 5.88,11.04 4.09,13.23 3.32,15.08 C 2.5,17.06 2.76,18.5 3.96,19 C 4.67,19.29 5.58,19.31 6.64,19.06 C 9.17,18.45 11.96,16.59 14.02,14.47 C 16.03,15.42 18.23,16.89 18.89,17.78 C 19.49,18.6 19.86,18.48 20.08,18.21 C 20.44,17.77 20.25,16.73 19.38,15.75 Z M 12.35,6.72 C 12.74,7.23 12.63,8.76 11.95,10.66 C 11.23,8.96 11.66,7.44 12.18,6.86 C 12.26,6.77 12.31,6.73 12.35,6.72 Z M 4.88,17.77 C 4.54,17.58 4.56,16.89 4.96,15.82 C 5.52,14.32 6.88,12.59 8.44,11.24 C 7.55,14.19 6.22,16.75 5.24,17.65 C 5.1,17.77 4.97,17.81 4.88,17.77 Z M 13.06,13.48 C 11.45,15.06 9.38,16.48 7.47,17.09 C 8.6,15.15 10.02,12.72 11.41,10.42 C 12.08,11.53 12.65,12.54 13.06,13.48 Z");
		Path element = path;
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateUsersIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,0 {3},{1} A {2},{2} 0 1,0 {0},{1}", 3.8 * num, 4.5 * num, 2.2 * num, 8.2 * num);
		string data2 = string.Format(Inv, "M {0},{1} C {0},{2} {3},{4} {5},{4} C {6},{4} {7},{2} {7},{1}", 2.2 * num, 13.5 * num, 10.0 * num, 3.5 * num, 9.0 * num, 6.0 * num, 8.5 * num, 9.8 * num);
		string data3 = string.Format(Inv, "M {0},{1} A {2},{2} 0 0,1 {3},{4}", 10.0 * num, 3.2 * num, 1.8 * num, 11.5 * num, 6.2 * num);
		string data4 = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {4},{6}", 9.5 * num, 9.5 * num, 11.5 * num, 9.5 * num, 13.8 * num, 11.0 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data4, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateDocumentPageviewsIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} H {2} L {3},{4} V {5} H {0} Z", 3.5 * num, 2.2 * num, 9.6 * num, 12.8 * num, 5.4 * num, 13.8 * num);
		string data2 = string.Format(Inv, "M {0},{1} V {2} H {3}", 9.6 * num, 2.2 * num, 5.4 * num, 12.8 * num);
		string data3 = string.Format(Inv, "M {0},{1} H {2}", 5.5 * num, 8.2 * num, 10.5 * num);
		string data4 = string.Format(Inv, "M {0},{1} H {2}", 5.5 * num, 10.8 * num, 8.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data4, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateBullseyeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,0 {3},{1} A {2},{2} 0 1,0 {0},{1}", 2.5 * num, 8.0 * num, 5.5 * num, 13.5 * num);
		string data2 = string.Format(Inv, "M {0},{1} A {2},{2} 0 1,0 {3},{1} A {2},{2} 0 1,0 {0},{1}", 5.6 * num, 8.0 * num, 2.4 * num, 10.4 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCreditCardIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} H {2} C {3},{1} {3},{1} {3},{4} V {5} C {3},{6} {3},{6} {2},{6} H {0} C {7},{6} {7},{6} {7},{5} V {4} C {7},{1} {7},{1} {0},{1} Z", 2.5 * num, 3.8 * num, 13.5 * num, 14.8 * num, 5.0 * num, 11.0 * num, 12.2 * num, 1.2 * num);
		string data2 = string.Format(Inv, "M {0},{1} H {2}", 1.2 * num, 6.8 * num, 14.8 * num);
		string data3 = string.Format(Inv, "M {0},{1} H {2}", 3.5 * num, 9.8 * num, 6.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateTrendingUpIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5} L {6},{7}", 2.5 * num, 11.8 * num, 6.2 * num, 8.0 * num, 9.5 * num, 11.2 * num, 13.5 * num, 4.5 * num);
		string data2 = string.Format(Inv, "M {0},{1} H {2} V {3}", 10.0 * num, 4.5 * num, 13.5 * num, 8.0 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateTrendingDownIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{5} L {6},{7}", 2.5 * num, 4.5 * num, 6.2 * num, 8.2 * num, 9.5 * num, 5.0 * num, 13.5 * num, 11.8 * num);
		string data2 = string.Format(Inv, "M {0},{1} H {2} V {3}", 10.0 * num, 11.8 * num, 13.5 * num, 8.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCodeIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4}", 5.8 * num, 4.2 * num, 2.2 * num, 8.0 * num, 11.8 * num);
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3}", 10.2 * num, 3.2 * num, 5.8 * num, 12.8 * num);
		string data3 = string.Format(Inv, "M {0},{1} L {2},{3} L {0},{4}", 10.2 * num, 4.2 * num, 13.8 * num, 8.0 * num, 11.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateCoinStackIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} A {2},{3} 0 1 0 {4},{1} A {2},{3} 0 1 0 {0},{1} Z", 8.0 * num, 2.8 * num, 5.5 * num, 2.0 * num, 8.001 * num);
		string data2 = string.Format(Inv, "M {0},{1} A {2},{3} 0 0 0 {4},{1}", 2.5 * num, 6.8 * num, 5.5 * num, 2.0 * num, 13.5 * num);
		string data3 = string.Format(Inv, "M {0},{1} V {2} M {3},{1} V {2}", 2.5 * num, 4.8 * num, 6.8 * num, 13.5 * num);
		string data4 = string.Format(Inv, "M {0},{1} A {2},{3} 0 0 0 {4},{1}", 2.5 * num, 10.8 * num, 5.5 * num, 2.0 * num, 13.5 * num);
		string data5 = string.Format(Inv, "M {0},{1} V {2} M {3},{1} V {2}", 2.5 * num, 8.8 * num, 10.8 * num, 13.5 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data4, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data5, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateFlameIcon(double size, Brush stroke, Brush fill = null)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} C {2},{3} {4},{5} {6},{7} C {8},{9} {10},{11} {12},{13} C {14},{15} {16},{17} {18},{19} C {20},{21} {22},{23} {24},{25} C {26},{27} {28},{29} {30},{31} C {32},{33} {34},{35} {0},{1} Z", 8.0 * num, 2.0 * num, 7.0 * num, 4.5 * num, 5.0 * num, 6.5 * num, 4.0 * num, 9.0 * num, 3.0 * num, 11.5 * num, 4.5 * num, 14.0 * num, 8.0 * num, 14.5 * num, 11.5 * num, 14.0 * num, 13.0 * num, 11.5 * num, 12.0 * num, 9.0 * num, 11.5 * num, 7.5 * num, 10.5 * num, 6.5 * num, 10.0 * num, 5.0 * num, 9.5 * num, 7.0 * num, 8.5 * num, 8.0 * num, 7.5 * num, 8.5 * num, 8.0 * num, 6.0 * num, 8.5 * num, 4.0 * num);
		Path element = CreatePath(data, stroke, opticalStroke, fill);
		canvas2.Children.Add(element);
		return canvas2;
	}

	public static Canvas CreateMessageSquareIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} H {2} C {3},{1} {3},{1} {3},{4} V {5} C {3},{6} {3},{6} {2},{6} H {7} L {8},{9} L {10},{6} H {0} C {11},{6} {11},{6} {11},{5} V {4} C {11},{1} {11},{1} {0},{1} Z", 3.8 * num, 3.2 * num, 12.2 * num, 13.8 * num, 4.8 * num, 9.8 * num, 11.4 * num, 6.8 * num, 4.0 * num, 13.8 * num, 4.8 * num, 2.2 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateWrenchIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} A {4},{4} 0 0 1 {5},{6} L {7},{8} L {9},{10} L {11},{12} A {4},{4} 0 0 1 {13},{14} L {15},{16} C {17},{18} {19},{20} {0},{1} Z", 14.2 * num, 3.8 * num, 12.2 * num, 5.8 * num, 1.8 * num, 11.0 * num, 5.4 * num, 6.8 * num, 9.6 * num, 6.4 * num, 10.0 * num, 3.8 * num, 12.6 * num, 1.5 * num, 3.0 * num, 14.8 * num, 1.2 * num, 13.0 * num, 10.0 * num, 4.2 * num, 12.0 * num, 1.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateWarningTriangleIcon(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} L {2},{3} C {4},{5} {6},{5} {7},{3} L {8},{1} C {9},{10} {11},{10} {0},{1} Z", 7.2 * num, 3.2 * num, 2.3 * num, 12.0 * num, 1.9 * num, 12.8 * num, 2.5 * num, 13.8 * num, 3.5 * num, 12.5 * num, 13.5 * num, 14.1 * num, 12.8 * num, 13.7 * num, 12.0 * num, 8.8 * num);
		string data2 = string.Format(Inv, "M {0},{1} V {2}", 8.0 * num, 6.2 * num, 9.2 * num);
		string data3 = string.Format(Inv, "M {0},{1} V {2}", 8.0 * num, 11.2 * num, 11.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data3, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateArrowUp(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} V {2}", 8.0 * num, 13.5 * num, 3.2 * num);
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 4.2 * num, 7.0 * num, 8.0 * num, 3.2 * num, 11.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}

	public static Canvas CreateArrowDown(double size, Brush stroke)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		double opticalStroke = GetOpticalStroke(size);
		string data = string.Format(Inv, "M {0},{1} V {2}", 8.0 * num, 2.5 * num, 12.8 * num);
		string data2 = string.Format(Inv, "M {0},{1} L {2},{3} L {4},{1}", 4.2 * num, 9.0 * num, 8.0 * num, 12.8 * num, 11.8 * num);
		canvas2.Children.Add(CreatePath(data, stroke, opticalStroke));
		canvas2.Children.Add(CreatePath(data2, stroke, opticalStroke));
		return canvas2;
	}
}
