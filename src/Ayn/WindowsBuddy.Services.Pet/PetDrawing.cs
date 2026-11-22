using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace WindowsBuddy.Services.Pet;

public sealed class PetDrawing
{
	public string Name;

	public readonly List<Point[]> Strokes = new List<Point[]>();

	public Rect Bounds
	{
		get
		{
			double num = double.MaxValue;
			double num2 = double.MaxValue;
			double num3 = double.MinValue;
			double num4 = double.MinValue;
			foreach (Point[] stroke in Strokes)
			{
				Point[] array = stroke;
				for (int i = 0; i < array.Length; i++)
				{
					Point point = array[i];
					num = Math.Min(num, point.X);
					num2 = Math.Min(num2, point.Y);
					num3 = Math.Max(num3, point.X);
					num4 = Math.Max(num4, point.Y);
				}
			}
			return new Rect(num, num2, num3 - num, num4 - num2);
		}
	}

	public int PointCount => Strokes.Sum((Point[] s) => s.Length);

	public PetDrawing(string name)
	{
		Name = name;
	}

	public PetDrawing Line(params double[] xy)
	{
		Point[] array = new Point[xy.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			ref Point reference = ref array[i];
			reference = new Point(xy[i * 2], xy[i * 2 + 1]);
		}
		Strokes.Add(array);
		return this;
	}

	public PetDrawing Arc(double cx, double cy, double rx, double ry, double from, double to, int steps = 14)
	{
		Point[] array = new Point[steps + 1];
		for (int i = 0; i <= steps; i++)
		{
			double num = (from + (to - from) * (double)i / (double)steps) * Math.PI / 180.0;
			ref Point reference = ref array[i];
			reference = new Point(cx + Math.Cos(num) * rx, cy + Math.Sin(num) * ry);
		}
		Strokes.Add(array);
		return this;
	}

	public PetDrawing Circle(double cx, double cy, double r)
	{
		return Arc(cx, cy, r, r, 0.0, 360.0, 18);
	}
}
