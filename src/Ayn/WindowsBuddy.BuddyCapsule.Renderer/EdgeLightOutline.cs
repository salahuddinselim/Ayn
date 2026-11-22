using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class EdgeLightOutline
{
	public Point[] Points;

	public Point[] Fill;

	public bool Closed;

	public double FadeTop;

	public double Top;

	public double Width;

	public double Height;

	public double Length;

	public static EdgeLightOutline For(string style, double width, double height, double cornerRadius)
	{
		if (width < 4.0 || height < 4.0)
		{
			return null;
		}
		bool flag = string.Equals(style, "Island", StringComparison.OrdinalIgnoreCase);
		EdgeLightOutline edgeLightOutline = new EdgeLightOutline();
		edgeLightOutline.Width = width;
		edgeLightOutline.Height = height;
		edgeLightOutline.Closed = flag;
		EdgeLightOutline edgeLightOutline2 = edgeLightOutline;
		edgeLightOutline2.Fill = Flatten(NotchGeometryBuilder.BuildGeometry(style, width, height, cornerRadius));
		Point[] array = Flatten(flag ? NotchGeometryBuilder.BuildGeometry(style, width, height, cornerRadius) : NotchGeometryBuilder.BuildSilhouetteGeometry(style, width, height, cornerRadius));
		if (array.Length < 2)
		{
			return null;
		}
		if (flag)
		{
			int num = 0;
			double num2 = double.MaxValue;
			double num3 = double.MaxValue;
			Point[] array2 = array;
			foreach (Point point in array2)
			{
				num3 = Math.Min(num3, point.Y);
			}
			for (int j = 0; j < array.Length; j++)
			{
				double num4 = Math.Abs(array[j].X - width / 2.0) + Math.Abs(array[j].Y - num3) * 4.0;
				if (num4 < num2)
				{
					num2 = num4;
					num = j;
				}
			}
			List<Point> list = new List<Point>(array.Length + 1);
			int num5 = array.Length;
			if (array[0] == array[num5 - 1])
			{
				num5--;
			}
			for (int k = 0; k < num5; k++)
			{
				list.Add(array[(num + k) % num5]);
			}
			list.Add(list[0]);
			array = list.ToArray();
			edgeLightOutline2.Top = num3;
			edgeLightOutline2.FadeTop = 0.0;
		}
		else
		{
			edgeLightOutline2.Top = 0.0;
			edgeLightOutline2.FadeTop = Math.Max(4.0, Math.Min(9.0, height * 0.3));
		}
		edgeLightOutline2.Points = array;
		double num6 = 0.0;
		for (int l = 1; l < array.Length; l++)
		{
			num6 += (array[l] - array[l - 1]).Length;
		}
		edgeLightOutline2.Length = num6;
		return edgeLightOutline2;
	}

	public static Point[] Flatten(PathGeometry g)
	{
		PathGeometry pathGeometry = g.Clone();
		foreach (PathFigure figure in pathGeometry.Figures)
		{
			figure.IsFilled = true;
		}
		PathGeometry flattenedPathGeometry = pathGeometry.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);
		List<Point> list = new List<Point>(160);
		if (flattenedPathGeometry.Figures.Count == 0)
		{
			return list.ToArray();
		}
		PathFigure pathFigure = flattenedPathGeometry.Figures[0];
		list.Add(pathFigure.StartPoint);
		foreach (PathSegment segment in pathFigure.Segments)
		{
			if (segment is LineSegment lineSegment)
			{
				list.Add(lineSegment.Point);
			}
			else
			{
				if (!(segment is PolyLineSegment polyLineSegment))
				{
					continue;
				}
				foreach (Point point in polyLineSegment.Points)
				{
					list.Add(point);
				}
			}
		}
		if (pathFigure.IsClosed && list.Count > 1 && list[0] != list[list.Count - 1])
		{
			list.Add(list[0]);
		}
		return list.ToArray();
	}

	public bool SameShape(EdgeLightOutline other)
	{
		if (other != null && Closed == other.Closed && Math.Abs(Width - other.Width) < 0.01 && Math.Abs(Height - other.Height) < 0.01 && Points.Length == other.Points.Length)
		{
			return Math.Abs(Length - other.Length) < 0.01;
		}
		return false;
	}
}
