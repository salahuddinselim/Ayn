using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.Tools.AICoding;

internal static class AgentMarks
{
	public static FrameworkElement Create(AICodingToolType agent, double size)
	{
		Canvas canvas = new Canvas();
		canvas.Width = 24.0;
		canvas.Height = 24.0;
		canvas.IsHitTestVisible = false;
		Canvas canvas2 = canvas;
		switch (agent)
		{
		case AICodingToolType.ClaudeCode:
		{
			Brush stroke2 = Solid(217, 119, 87);
			double[] array = new double[12]
			{
				9.6, 7.4, 9.2, 8.0, 9.8, 7.2, 9.0, 8.4, 9.6, 7.6,
				9.3, 8.1
			};
			for (int j = 0; j < 12; j++)
			{
				double num = (double)j * Math.PI / 6.0 + 0.12;
				canvas2.Children.Add(new Line
				{
					X1 = 12.0 + Math.Cos(num) * 1.6,
					Y1 = 12.0 + Math.Sin(num) * 1.6,
					X2 = 12.0 + Math.Cos(num) * array[j],
					Y2 = 12.0 + Math.Sin(num) * array[j],
					Stroke = stroke2,
					StrokeThickness = 2.1,
					StrokeStartLineCap = PenLineCap.Round,
					StrokeEndLineCap = PenLineCap.Round
				});
			}
			break;
		}
		case AICodingToolType.Codex:
		{
			Brush stroke = Solid(242, 242, 247);
			for (int i = 0; i < 6; i++)
			{
				Ellipse ellipse = new Ellipse();
				ellipse.Width = 7.4;
				ellipse.Height = 13.6;
				ellipse.Stroke = stroke;
				ellipse.StrokeThickness = 1.5;
				Ellipse ellipse2 = ellipse;
				Canvas.SetLeft(ellipse2, 8.3);
				Canvas.SetTop(ellipse2, 0.8000000000000007);
				ellipse2.RenderTransformOrigin = new Point(0.5, 0.8235294117647058);
				ellipse2.RenderTransform = new RotateTransform(i * 60);
				canvas2.Children.Add(ellipse2);
			}
			break;
		}
		case AICodingToolType.OpenCode:
		{
			Brush fill = Solid(255, 156, 64);
			canvas2.Children.Add(new Ellipse
			{
				Width = 15.0,
				Height = 15.0,
				Fill = fill
			});
			canvas2.Children.Add(new Ellipse
			{
				Width = 6.0,
				Height = 6.0,
				Fill = Solid(28, 28, 30)
			});
			Canvas.SetLeft(canvas2.Children[0], 4.5);
			Canvas.SetTop(canvas2.Children[0], 4.5);
			Canvas.SetLeft(canvas2.Children[1], 9.0);
			Canvas.SetTop(canvas2.Children[1], 9.0);
			break;
		}
		}
		Viewbox viewbox = new Viewbox();
		viewbox.Width = size;
		viewbox.Height = size;
		viewbox.Child = canvas2;
		viewbox.IsHitTestVisible = false;
		return viewbox;
	}

	private static Brush Solid(byte r, byte g, byte b)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}
}
