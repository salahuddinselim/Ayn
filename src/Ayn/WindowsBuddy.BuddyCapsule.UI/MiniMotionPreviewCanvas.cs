using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public class MiniMotionPreviewCanvas : FrameworkElement
{
	private static readonly Pen BaseTrackPen;

	private static readonly SolidColorBrush[] WhiteBrushes;

	private static readonly Pen[] WhitePens;

	public string StyleKey { get; set; }

	public double Time { get; set; }

	public bool IsSelected { get; set; }

	static MiniMotionPreviewCanvas()
	{
		WhiteBrushes = new SolidColorBrush[16];
		WhitePens = new Pen[16];
		BaseTrackPen = new Pen(new SolidColorBrush(Color.FromArgb(45, byte.MaxValue, byte.MaxValue, byte.MaxValue)), 1.0);
		BaseTrackPen.Freeze();
		for (int i = 0; i < 16; i++)
		{
			byte a = (byte)(i * 17);
			SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb(a, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			solidColorBrush.Freeze();
			WhiteBrushes[i] = solidColorBrush;
			Pen pen = new Pen(solidColorBrush, 1.4);
			pen.StartLineCap = PenLineCap.Round;
			pen.EndLineCap = PenLineCap.Round;
			pen.Freeze();
			WhitePens[i] = pen;
		}
	}

	private static SolidColorBrush GetWhiteBrush(double alpha01)
	{
		int num = Math.Max(0, Math.Min(15, (int)(alpha01 * 15.0)));
		return WhiteBrushes[num];
	}

	private static Pen GetWhitePen(double alpha01)
	{
		int num = Math.Max(0, Math.Min(15, (int)(alpha01 * 15.0)));
		return WhitePens[num];
	}

	public MiniMotionPreviewCanvas(string styleKey)
	{
		StyleKey = styleKey;
		base.IsHitTestVisible = false;
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		double num = ((base.ActualWidth > 0.0) ? base.ActualWidth : 36.0);
		double num2 = ((base.ActualHeight > 0.0) ? base.ActualHeight : 13.0);
		double time = Time;
		switch (StyleKey)
		{
		case "Soft Sweep":
		{
			dc.DrawLine(BaseTrackPen, new Point(3.0, num2 / 2.0), new Point(num - 3.0, num2 / 2.0));
			double num27 = time * 0.7 % 1.0;
			double num28 = 3.0 + num27 * (num - 6.0);
			double num29 = 10.0;
			for (int l = 0; l < 4; l++)
			{
				double num30 = (double)l / 4.0;
				double num31 = (double)(l + 1) / 4.0;
				double num32 = Math.Max(3.0, num28 - num29 * (1.0 - num30));
				double num33 = Math.Max(3.0, num28 - num29 * (1.0 - num31));
				if (num33 > num32)
				{
					dc.DrawLine(GetWhitePen(Math.Pow(num31, 1.8)), new Point(num32, num2 / 2.0), new Point(num33, num2 / 2.0));
				}
			}
			break;
		}
		case "Sharp Sweep":
		{
			dc.DrawLine(BaseTrackPen, new Point(3.0, num2 / 2.0), new Point(num - 3.0, num2 / 2.0));
			double num16 = time * 0.95 % 1.0;
			double num17 = 3.0 + num16 * (num - 6.0);
			double num18 = 6.0;
			for (int j = 0; j < 3; j++)
			{
				double num19 = (double)j / 3.0;
				double num20 = (double)(j + 1) / 3.0;
				double num21 = Math.Max(3.0, num17 - num18 * (1.0 - num19));
				double num22 = Math.Max(3.0, num17 - num18 * (1.0 - num20));
				if (num22 > num21)
				{
					dc.DrawLine(GetWhitePen(Math.Pow(num20, 1.5)), new Point(num21, num2 / 2.0), new Point(num22, num2 / 2.0));
				}
			}
			break;
		}
		case "Dotted Trail":
		{
			int num23 = 5;
			double num24 = (num - 8.0) / (double)(num23 - 1);
			double num25 = time * 2.2 % (double)(num23 + 1);
			for (int k = 0; k < num23; k++)
			{
				double x = 4.0 + (double)k * num24;
				double num26 = Math.Abs((double)k - num25);
				double alpha2 = Math.Max(0.12, 1.0 - num26 / 1.5);
				dc.DrawEllipse(GetWhiteBrush(alpha2), null, new Point(x, num2 / 2.0), 1.6, 1.6);
			}
			break;
		}
		case "Dual Sweep":
		{
			dc.DrawLine(BaseTrackPen, new Point(3.0, num2 / 2.0), new Point(num - 3.0, num2 / 2.0));
			double num5 = time * 0.75 % 1.0;
			double num6 = 1.0 - num5;
			double num7 = 3.0 + num5 * (num - 6.0);
			double num8 = 3.0 + num6 * (num - 6.0);
			double num9 = 7.0;
			for (int i = 0; i < 3; i++)
			{
				double num10 = (double)i / 3.0;
				double num11 = (double)(i + 1) / 3.0;
				double num12 = Math.Max(3.0, num7 - num9 * (1.0 - num10));
				double num13 = Math.Max(3.0, num7 - num9 * (1.0 - num11));
				if (num13 > num12)
				{
					dc.DrawLine(GetWhitePen(Math.Pow(num11, 1.6)), new Point(num12, num2 / 2.0), new Point(num13, num2 / 2.0));
				}
				double num14 = Math.Min(num - 3.0, num8 + num9 * (1.0 - num10));
				double num15 = Math.Min(num - 3.0, num8 + num9 * (1.0 - num11));
				if (num14 > num15)
				{
					dc.DrawLine(GetWhitePen(Math.Pow(num11, 1.6)), new Point(num14, num2 / 2.0), new Point(num15, num2 / 2.0));
				}
			}
			break;
		}
		case "Pulse":
		{
			double num4 = 0.5 + 0.5 * Math.Sin(time * 3.6);
			dc.DrawRoundedRectangle(rectangle: new Rect(4.0, 2.0, num - 8.0, num2 - 4.0), brush: null, pen: GetWhitePen(0.15 + 0.75 * num4), radiusX: 3.0, radiusY: 3.0);
			break;
		}
		case "Breathing":
		{
			double num3 = 0.5 + 0.5 * Math.Sin(time * 1.5);
			dc.DrawRoundedRectangle(rectangle: new Rect(4.0, 2.0, num - 8.0, num2 - 4.0), brush: null, pen: GetWhitePen(0.1 + 0.55 * num3), radiusX: 3.0, radiusY: 3.0);
			break;
		}
		default:
		{
			Rect rectangle = new Rect(4.0, 2.0, num - 8.0, num2 - 4.0);
			double alpha = 0.4 + 0.12 * Math.Sin(time * 1.8);
			dc.DrawRoundedRectangle(null, GetWhitePen(alpha), rectangle, 3.0, 3.0);
			break;
		}
		}
	}
}
