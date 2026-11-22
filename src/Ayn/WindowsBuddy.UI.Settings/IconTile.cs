using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.UI.Settings;

internal static class IconTile
{
	private static readonly Dictionary<double, Geometry> _squircles = new Dictionary<double, Geometry>();

	public static Geometry Squircle(double size)
	{
		if (_squircles.TryGetValue(size, out var value))
		{
			return value;
		}
		double num = size / 2.0;
		PathFigure pathFigure = new PathFigure();
		pathFigure.IsClosed = true;
		pathFigure.IsFilled = true;
		PathFigure pathFigure2 = pathFigure;
		PointCollection pointCollection = new PointCollection();
		for (int i = 0; i < 96; i++)
		{
			double num2 = Math.PI * 2.0 * (double)i / 96.0;
			double value2 = Math.Cos(num2);
			double value3 = Math.Sin(num2);
			double x = num + num * (double)Math.Sign(value2) * Math.Pow(Math.Abs(value2), 0.4);
			double y = num + num * (double)Math.Sign(value3) * Math.Pow(Math.Abs(value3), 0.4);
			if (i == 0)
			{
				pathFigure2.StartPoint = new Point(x, y);
			}
			else
			{
				pointCollection.Add(new Point(x, y));
			}
		}
		pathFigure2.Segments.Add(new PolyLineSegment(pointCollection, isStroked: true));
		PathGeometry pathGeometry = new PathGeometry();
		pathGeometry.Figures.Add(pathFigure2);
		pathGeometry.Freeze();
		_squircles[size] = pathGeometry;
		return pathGeometry;
	}

	public static FrameworkElement Create(Color hue, string glyph, double size)
	{
		return Create(hue, glyph, size, Brushes.White);
	}

	public static FrameworkElement Create(Color hue, string glyph, double size, Brush glyphBrush)
	{
		return Create(Shift(hue, 0.14), Shift(hue, -0.08), glyph, size, glyphBrush);
	}

	public static FrameworkElement Create(Color top, Color bottom, string glyph, double size, Brush glyphBrush)
	{
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(top, bottom, new Point(0.5, 0.0), new Point(0.5, 1.0));
		linearGradientBrush.Freeze();
		Geometry data = Squircle(size);
		Grid grid = new Grid();
		grid.Width = size;
		grid.Height = size;
		grid.SnapsToDevicePixels = true;
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.IsHitTestVisible = false;
		Grid grid2 = grid;
		grid2.Children.Add(new Path
		{
			Data = data,
			Fill = linearGradientBrush
		});
		FrameworkElement frameworkElement = Glyphs.Create(glyph, size * (2.0 / 3.0), glyphBrush);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		double num = ((glyph == "key_f") ? (-0.5) : ((glyph == "hand_f") ? 0.25 : 0.0));
		double num2 = ((glyph == "bell_f") ? (-0.25) : ((glyph == "notch_f") ? (-0.25) : 0.0));
		frameworkElement.RenderTransform = new TranslateTransform(num * size / 24.0, num2 * size / 24.0);
		grid2.Children.Add(frameworkElement);
		return grid2;
	}

	public static Color Shift(Color c, double amount)
	{
		Func<byte, byte> func = (byte v) => (!(amount >= 0.0)) ? ((byte)Math.Round((double)(int)v * (1.0 + amount))) : ((byte)Math.Round((double)(int)v + (double)(255 - v) * amount));
		return Color.FromRgb(func(c.R), func(c.G), func(c.B));
	}
}
