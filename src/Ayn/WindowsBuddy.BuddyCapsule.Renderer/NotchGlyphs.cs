using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class NotchGlyphs
{
	private const double Grid = 24.0;

	private const double Stroke = 2.0;

	private const string Cone = "M3.4 9.2 H6.2 L10.55 5.45 Q11.75 4.5 11.75 6 V18 Q11.75 19.5 10.55 18.55 L6.2 14.8 H3.4 Q2.25 14.8 2.25 13.65 V10.35 Q2.25 9.2 3.4 9.2 Z";

	private const string Wave1 = "M15.1 9.2 A4 4 0 0 1 15.1 14.8";

	private const string Wave2 = "M17.6 6.75 A7.4 7.4 0 0 1 17.6 17.25";

	private const string Wave3 = "M20.1 4.3 A10.9 10.9 0 0 1 20.1 19.7";

	private const string DropPath = "M12 3 C10.2 5.4 5.75 10.6 5.75 14.6 A6.25 6.25 0 0 0 18.25 14.6 C18.25 10.6 13.8 5.4 12 3 Z";

	private const string WifiArcs = "M9.2 16.1 A4 4 0 0 1 14.8 16.1 M6.35 13.25 A8 8 0 0 1 17.65 13.25 M3.5 10.4 A12 12 0 0 1 20.5 10.4";

	public static readonly Color Secondary = Color.FromRgb(142, 142, 147);

	public static readonly Color Green = Color.FromRgb(48, 209, 88);

	public static readonly Color Red = Color.FromRgb(byte.MaxValue, 69, 58);

	public static readonly Color WaterBlue = Color.FromRgb(58, 184, 217);

	public static readonly Brush SecondaryBrush = Freeze(new SolidColorBrush(Secondary));

	public static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Green));

	public static readonly Brush RedBrush = Freeze(new SolidColorBrush(Red));

	public static readonly Brush WaterBlueBrush = Freeze(new SolidColorBrush(WaterBlue));

	private static readonly Dictionary<string, string[]> Data = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		{
			"bell",
			new string[2] { "M6.5 16.5 V11 A5.5 5.5 0 0 1 17.5 11 V16.5 L19 18.5 H5 Z M10 20.6 A2.1 2.1 0 0 0 14 20.6 M12 3.6 V5.4", null }
		},
		{
			"speaker",
			new string[2] { null, "M3.4 9.2 H6.2 L10.55 5.45 Q11.75 4.5 11.75 6 V18 Q11.75 19.5 10.55 18.55 L6.2 14.8 H3.4 Q2.25 14.8 2.25 13.65 V10.35 Q2.25 9.2 3.4 9.2 Z" }
		},
		{
			"speaker.wave.1",
			new string[2] { "M15.1 9.2 A4 4 0 0 1 15.1 14.8", "M3.4 9.2 H6.2 L10.55 5.45 Q11.75 4.5 11.75 6 V18 Q11.75 19.5 10.55 18.55 L6.2 14.8 H3.4 Q2.25 14.8 2.25 13.65 V10.35 Q2.25 9.2 3.4 9.2 Z" }
		},
		{
			"speaker.wave.2",
			new string[2] { "M15.1 9.2 A4 4 0 0 1 15.1 14.8 M17.6 6.75 A7.4 7.4 0 0 1 17.6 17.25", "M3.4 9.2 H6.2 L10.55 5.45 Q11.75 4.5 11.75 6 V18 Q11.75 19.5 10.55 18.55 L6.2 14.8 H3.4 Q2.25 14.8 2.25 13.65 V10.35 Q2.25 9.2 3.4 9.2 Z" }
		},
		{
			"speaker.wave.3",
			new string[2] { "M15.1 9.2 A4 4 0 0 1 15.1 14.8 M17.6 6.75 A7.4 7.4 0 0 1 17.6 17.25 M20.1 4.3 A10.9 10.9 0 0 1 20.1 19.7", "M3.4 9.2 H6.2 L10.55 5.45 Q11.75 4.5 11.75 6 V18 Q11.75 19.5 10.55 18.55 L6.2 14.8 H3.4 Q2.25 14.8 2.25 13.65 V10.35 Q2.25 9.2 3.4 9.2 Z" }
		},
		{
			"speaker.slash",
			new string[2] { "M15 9.5 L20.5 15 M20.5 9.5 L15 15", "M3.4 9.2 H6.2 L10.55 5.45 Q11.75 4.5 11.75 6 V18 Q11.75 19.5 10.55 18.55 L6.2 14.8 H3.4 Q2.25 14.8 2.25 13.65 V10.35 Q2.25 9.2 3.4 9.2 Z" }
		},
		{
			"clipboard.check",
			new string[2] { "M8 4.5 H7 Q4.5 4.5 4.5 7 V19 Q4.5 21.5 7 21.5 H17 Q19.5 21.5 19.5 19 V7 Q19.5 4.5 17 4.5 H16 M9.25 2.75 H14.75 Q16 2.75 16 4 V5 Q16 6.25 14.75 6.25 H9.25 Q8 6.25 8 5 V4 Q8 2.75 9.25 2.75 Z M8.5 13.75 L11 16.25 L15.75 11.25", null }
		},
		{
			"timer",
			new string[2] { "M12 6 A7.75 7.75 0 1 1 11.99 6 Z M9.75 2.75 H14.25 M12 2.75 V6 M12 13.75 L15 10.75 M18.25 7 L19.5 5.75", null }
		},
		{
			"bolt",
			new string[2] { null, "M13.4 2.4 L5.2 13.7 H11.2 L10.4 21.6 L18.8 10.1 H12.6 Z" }
		},
		{
			"drop",
			new string[2] { "M12 3 C10.2 5.4 5.75 10.6 5.75 14.6 A6.25 6.25 0 0 0 18.25 14.6 C18.25 10.6 13.8 5.4 12 3 Z", null }
		},
		{
			"drop.fill",
			new string[2] { null, "M12 3 C10.2 5.4 5.75 10.6 5.75 14.6 A6.25 6.25 0 0 0 18.25 14.6 C18.25 10.6 13.8 5.4 12 3 Z" }
		},
		{
			"music.note",
			new string[2] { "M15.5 16.5 V4 L20 5.5 V8.5 L15.5 7", "M15.5 16.75 A3.4 2.8 0 1 1 8.7 16.75 A3.4 2.8 0 1 1 15.5 16.75 Z" }
		},
		{
			"bluetooth",
			new string[2] { "M7 7.5 L17 16.25 L12 20.75 V3.25 L17 7.75 L7 16.5", null }
		},
		{
			"wifi",
			new string[2] { "M9.2 16.1 A4 4 0 0 1 14.8 16.1 M6.35 13.25 A8 8 0 0 1 17.65 13.25 M3.5 10.4 A12 12 0 0 1 20.5 10.4", "M13.3 19 A1.3 1.3 0 1 1 10.7 19 A1.3 1.3 0 1 1 13.3 19 Z" }
		},
		{
			"ethernet",
			new string[2] { "M5 3.5 H19 V13.5 H15 V17 H9 V13.5 H5 Z M8 3.5 V7 M12 3.5 V7 M16 3.5 V7 M12 17 V21", null }
		},
		{
			"wifi.slash",
			new string[2] { "M9.2 16.1 A4 4 0 0 1 14.8 16.1 M6.35 13.25 A8 8 0 0 1 17.65 13.25 M3.5 10.4 A12 12 0 0 1 20.5 10.4 M4 3.5 L20 20.5", "M13.3 19 A1.3 1.3 0 1 1 10.7 19 A1.3 1.3 0 1 1 13.3 19 Z" }
		},
		{
			"headphones",
			new string[2] { "M4.25 15.5 V12.5 A7.75 7.75 0 0 1 19.75 12.5 V15.5 M4.25 14.5 H6.25 Q7.5 14.5 7.5 15.75 V18.75 Q7.5 20 6.25 20 H5.5 Q4.25 20 4.25 18.75 Z M19.75 14.5 H17.75 Q16.5 14.5 16.5 15.75 V18.75 Q16.5 20 17.75 20 H18.5 Q19.75 20 19.75 18.75 Z", null }
		},
		{
			"calendar",
			new string[2] { "M6.5 5 H17.5 Q19.75 5 19.75 7.25 V18.25 Q19.75 20.5 17.5 20.5 H6.5 Q4.25 20.5 4.25 18.25 V7.25 Q4.25 5 6.5 5 Z M4.25 9.75 H19.75 M8.25 3 V6.5 M15.75 3 V6.5", null }
		},
		{
			"hourglass",
			new string[2] { "M6.5 3.25 H17.5 M6.5 20.75 H17.5 M7.5 3.25 V6.5 Q7.5 9.75 12 12 Q16.5 9.75 16.5 6.5 V3.25 M7.5 20.75 V17.5 Q7.5 14.25 12 12 Q16.5 14.25 16.5 17.5 V20.75", null }
		},
		{
			"exclamation.triangle",
			new string[2] { "M10.6 4.6 Q12 2.3 13.4 4.6 L20.6 17.4 Q21.9 19.75 19.25 19.75 H4.75 Q2.1 19.75 3.4 17.4 Z M12 9.25 V13.5", "M13.15 16.4 A1.15 1.15 0 1 1 10.85 16.4 A1.15 1.15 0 1 1 13.15 16.4 Z" }
		},
		{
			"checkmark.circle",
			new string[2] { "M12 3 A9 9 0 1 1 11.99 3 Z M8.25 12.25 L10.75 14.75 L15.75 9.5", null }
		},
		{
			"message",
			new string[2] { "M6.5 4.75 H17.5 Q20 4.75 20 7.25 V14.25 Q20 16.75 17.5 16.75 H11 L7 20 V16.75 H6.5 Q4 16.75 4 14.25 V7.25 Q4 4.75 6.5 4.75 Z", null }
		}
	};

	private static readonly Dictionary<string, Geometry> Cache = new Dictionary<string, Geometry>(StringComparer.Ordinal);

	private static Brush Freeze(Brush b)
	{
		b.Freeze();
		return b;
	}

	private static Geometry Parse(string d)
	{
		if (d == null)
		{
			return null;
		}
		lock (Cache)
		{
			if (Cache.TryGetValue(d, out var value))
			{
				return value;
			}
			value = Geometry.Parse(d);
			value.Freeze();
			Cache[d] = value;
			return value;
		}
	}

	public static bool Has(string name)
	{
		if (name != null)
		{
			return Data.ContainsKey(name);
		}
		return false;
	}

	public static FrameworkElement Create(string name, double size, Brush brush)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		canvas.IsHitTestVisible = false;
		canvas.SnapsToDevicePixels = false;
		Canvas canvas2 = canvas;
		if (name == null || !Data.TryGetValue(name, out var value))
		{
			return canvas2;
		}
		ScaleTransform scaleTransform = new ScaleTransform(size / 24.0, size / 24.0);
		scaleTransform.Freeze();
		Geometry geometry = Parse(value[0]);
		Geometry geometry2 = Parse(value[1]);
		if (geometry2 != null)
		{
			canvas2.Children.Add(new Path
			{
				Data = geometry2,
				Fill = brush,
				RenderTransform = scaleTransform
			});
		}
		if (geometry != null)
		{
			canvas2.Children.Add(new Path
			{
				Data = geometry,
				Stroke = brush,
				StrokeThickness = 2.0,
				RenderTransform = scaleTransform,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round,
				StrokeLineJoin = PenLineJoin.Round
			});
		}
		return canvas2;
	}

	public static string SpeakerFor(int percent, bool muted)
	{
		if (muted)
		{
			return "speaker.slash";
		}
		if (percent <= 0)
		{
			return "speaker";
		}
		if (percent < 34)
		{
			return "speaker.wave.1";
		}
		if (percent < 67)
		{
			return "speaker.wave.2";
		}
		return "speaker.wave.3";
	}

	public static FrameworkElement CreateBattery(double height, int percent, bool charging, Brush normal, bool charged = false, bool plugged = false)
	{
		percent = Math.Max(0, Math.Min(100, percent));
		double num = height * 2.0;
		double num2 = num - 2.5;
		Brush fill = ((charging || charged) ? new SolidColorBrush(Color.FromRgb(48, 209, 88)) : ((percent <= 20 && !plugged) ? RedBrush : normal));
		Canvas canvas = new Canvas();
		canvas.Width = num;
		canvas.Height = height;
		canvas.IsHitTestVisible = false;
		canvas.SnapsToDevicePixels = true;
		canvas.UseLayoutRounding = true;
		Canvas canvas2 = canvas;
		canvas2.Children.Add(new Rectangle
		{
			Width = num2,
			Height = height,
			RadiusX = height * 0.25,
			RadiusY = height * 0.25,
			Fill = normal,
			Opacity = 0.18
		});
		double num3 = num2 * (charged ? 1.0 : ((double)percent / 100.0));
		if (num3 > 0.0)
		{
			Rectangle rectangle = new Rectangle();
			rectangle.Width = num2;
			rectangle.Height = height;
			rectangle.RadiusX = height * 0.25;
			rectangle.RadiusY = height * 0.25;
			rectangle.Fill = fill;
			rectangle.Clip = new RectangleGeometry(new Rect(0.0, 0.0, num3, height));
			Rectangle element = rectangle;
			canvas2.Children.Add(element);
		}
		Rectangle rectangle2 = new Rectangle();
		rectangle2.Width = 1.5;
		rectangle2.Height = height * 0.38;
		rectangle2.RadiusX = 0.7;
		rectangle2.RadiusY = 0.7;
		rectangle2.Fill = fill;
		rectangle2.Opacity = 0.65;
		Rectangle element2 = rectangle2;
		Canvas.SetLeft(element2, num2 + 1.0);
		Canvas.SetTop(element2, height * 0.31);
		canvas2.Children.Add(element2);
		if (charging)
		{
			Path path = new Path();
			path.Data = Geometry.Parse("M 5.4,0 L 1,5.3 L 4.1,5.3 L 3.1,9 L 8,3.5 L 4.8,3.5 Z");
			path.Fill = Brushes.White;
			path.Stretch = Stretch.Fill;
			path.Width = height * 0.62;
			path.Height = height * 0.84;
			Path element3 = path;
			Canvas.SetLeft(element3, (num2 - height * 0.62) / 2.0);
			Canvas.SetTop(element3, height * 0.08);
			canvas2.Children.Add(element3);
		}
		return canvas2;
	}

	public static FrameworkElement CreateFillingDrop(double size, Brush brush, bool animate)
	{
		Grid grid = new Grid();
		grid.Width = size;
		grid.Height = size;
		grid.IsHitTestVisible = false;
		Grid grid2 = grid;
		grid2.Children.Add(Create("drop", size, brush));
		FrameworkElement frameworkElement = Create("drop.fill", size, brush);
		RectangleGeometry rectangleGeometry = (RectangleGeometry)(frameworkElement.Clip = new RectangleGeometry(new Rect(0.0, animate ? size : 0.0, size, size)));
		grid2.Children.Add(frameworkElement);
		if (animate)
		{
			RectAnimationUsingKeyFrames rectAnimationUsingKeyFrames = new RectAnimationUsingKeyFrames();
			rectAnimationUsingKeyFrames.Duration = TimeSpan.FromSeconds(1.2);
			rectAnimationUsingKeyFrames.RepeatBehavior = new RepeatBehavior(3.0);
			rectAnimationUsingKeyFrames.FillBehavior = FillBehavior.HoldEnd;
			RectAnimationUsingKeyFrames rectAnimationUsingKeyFrames2 = rectAnimationUsingKeyFrames;
			rectAnimationUsingKeyFrames2.KeyFrames.Add(new DiscreteRectKeyFrame(new Rect(0.0, size, size, size), KeyTime.FromPercent(0.0)));
			rectAnimationUsingKeyFrames2.KeyFrames.Add(new EasingRectKeyFrame(new Rect(0.0, size * 0.12, size, size), KeyTime.FromPercent(0.75))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
			rectAnimationUsingKeyFrames2.KeyFrames.Add(new DiscreteRectKeyFrame(new Rect(0.0, 0.0, size, size), KeyTime.FromPercent(1.0)));
			rectangleGeometry.BeginAnimation(RectangleGeometry.RectProperty, rectAnimationUsingKeyFrames2);
		}
		return grid2;
	}

	public static void Pulse(FrameworkElement element, int times, bool animate)
	{
		if (animate && element != null)
		{
			ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
			element.RenderTransformOrigin = new Point(0.5, 0.5);
			element.RenderTransform = scaleTransform;
			DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
			doubleAnimationUsingKeyFrames.Duration = TimeSpan.FromSeconds(0.9);
			doubleAnimationUsingKeyFrames.RepeatBehavior = new RepeatBehavior(times);
			doubleAnimationUsingKeyFrames.FillBehavior = FillBehavior.Stop;
			DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = doubleAnimationUsingKeyFrames;
			SineEase sineEase = new SineEase();
			sineEase.EasingMode = EasingMode.EaseInOut;
			SineEase easingFunction = sineEase;
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)));
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(1.14, KeyTime.FromPercent(0.4))
			{
				EasingFunction = easingFunction
			});
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0))
			{
				EasingFunction = easingFunction
			});
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimationUsingKeyFrames2);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimationUsingKeyFrames2);
		}
	}
}
