using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class PetLaneSolver
{
	public static Rect Solve(Geometry shape, IList<Rect> occupied, double gap)
	{
		Rect bounds = shape.Bounds;
		if (bounds.IsEmpty || bounds.Width < 10.0 || bounds.Height < 12.0)
		{
			return Rect.Empty;
		}
		bounds.Inflate(-2.0, -2.0);
		List<double> list = new List<double>();
		list.Add(bounds.Top);
		list.Add(bounds.Bottom);
		List<double> list2 = list;
		foreach (Rect item in occupied)
		{
			double num = item.Top - gap;
			double num2 = item.Bottom + gap;
			if (num > bounds.Top && num < bounds.Bottom)
			{
				list2.Add(num);
			}
			if (num2 > bounds.Top && num2 < bounds.Bottom)
			{
				list2.Add(num2);
			}
		}
		list2 = (from v in list2.Distinct()
			orderby v
			select v).ToList();
		Rect best = Rect.Empty;
		double num3 = Math.Min(18.0, bounds.Height);
		for (int i = 0; i < list2.Count - 1; i++)
		{
			for (int j = i + 1; j < list2.Count; j++)
			{
				double num4 = list2[i];
				double num5 = list2[j] - num4;
				if (num5 < num3)
				{
					continue;
				}
				double num6 = bounds.Left;
				double num7 = bounds.Left + bounds.Width / 2.0;
				for (int k = 0; k < 12; k++)
				{
					double num8 = (num6 + num7) / 2.0;
					RectangleGeometry geometry = new RectangleGeometry(new Rect(num8, num4, Math.Max(0.01, bounds.Right - num8 - (num8 - bounds.Left)), num5));
					if (shape.FillContainsWithDetail(geometry) == IntersectionDetail.FullyContains)
					{
						num7 = num8;
					}
					else
					{
						num6 = num8;
					}
				}
				double num9 = num7 + 1.0;
				double num10 = bounds.Right - (num7 - bounds.Left) - 1.0;
				double num11 = num9;
				foreach (Rect item2 in occupied.OrderBy((Rect r) => r.Left))
				{
					Rect rect = item2;
					rect.Inflate(gap, gap);
					if (!(rect.Bottom <= num4) && !(rect.Top >= num4 + num5))
					{
						Consider(ref best, num11, Math.Min(num10, rect.Left), num4, num5);
						num11 = Math.Max(num11, rect.Right);
					}
				}
				Consider(ref best, num11, num10, num4, num5);
			}
		}
		return best;
	}

	private static void Consider(ref Rect best, double left, double right, double top, double height)
	{
		if (!(right - left < 10.0) && (best.IsEmpty || right - left > best.Width + 0.5 || (Math.Abs(right - left - best.Width) <= 0.5 && height > best.Height)))
		{
			best = new Rect(left, top, right - left, height);
		}
	}
}
