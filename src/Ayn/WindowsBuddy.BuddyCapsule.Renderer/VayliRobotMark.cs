using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class AynRobotMark : FrameworkElement
{
	private static readonly Brush shell = Paint(244, 241, 234);

	private static readonly Brush shade = Paint(204, 200, 193);

	private static readonly Brush ink = Paint(16, 17, 20);

	private static readonly Geometry visor = Frozen("M 8.5,3.5 L 18.5,3.5 Q 21,3.5 21,6 L 21,9.5 Q 21,13 17.5,13 L 9.5,13 Q 6,13 6,9.5 L 6,6 Q 6,3.5 8.5,3.5 Z");

	private static readonly Random random = new Random();

	public static readonly DependencyProperty EyeOpenProperty = DependencyProperty.Register("EyeOpen", typeof(double), typeof(AynRobotMark), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

	private readonly DispatcherTimer blink = new DispatcherTimer(DispatcherPriority.Background);

	public double EyeOpen
	{
		get
		{
			return (double)GetValue(EyeOpenProperty);
		}
		set
		{
			SetValue(EyeOpenProperty, value);
		}
	}

	public AynRobotMark(double size)
	{
		base.Width = (base.Height = size);
		base.IsHitTestVisible = false;
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		AutomationProperties.SetName(this, "Ayn");
		DispatcherTimer dispatcherTimer = blink;
		EventHandler value = delegate
		{
			Blink();
		};
		dispatcherTimer.Tick += value;
		base.Loaded += delegate
		{
			Schedule();
		};
		base.Unloaded += delegate
		{
			blink.Stop();
			BeginAnimation(EyeOpenProperty, null);
		};
	}

	private static Brush Paint(byte r, byte g, byte b)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static Geometry Frozen(string path)
	{
		Geometry geometry = Geometry.Parse(path);
		geometry.Freeze();
		return geometry;
	}

	private void Schedule()
	{
		blink.Stop();
		blink.Interval = TimeSpan.FromMilliseconds(3200 + random.Next(3800));
		blink.Start();
	}

	private void Blink()
	{
		if (!base.IsLoaded)
		{
			blink.Stop();
			return;
		}
		if (CapsuleMotion.AnimationsEnabled)
		{
			DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
			doubleAnimationUsingKeyFrames.FillBehavior = FillBehavior.Stop;
			DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = doubleAnimationUsingKeyFrames;
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80.0))));
			doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200.0))));
			BeginAnimation(EyeOpenProperty, doubleAnimationUsingKeyFrames2);
		}
		Schedule();
	}

	protected override void OnRender(DrawingContext dc)
	{
		double num = Math.Min(base.ActualWidth, base.ActualHeight);
		if (!(num <= 0.0))
		{
			double num2 = num * 0.92 / 23.0;
			double offsetX = (base.ActualWidth - 23.0 * num2) / 2.0 - 2.0 * num2;
			double offsetY = (base.ActualHeight - 18.0 * num2) / 2.0 - 1.0 * num2;
			dc.PushTransform(new MatrixTransform(num2, 0.0, 0.0, num2, offsetX, offsetY));
			dc.DrawRoundedRectangle(shade, null, new Rect(2.0, 2.0, 23.0, 17.0), 5.0, 5.0);
			dc.DrawRoundedRectangle(shell, null, new Rect(2.0, 1.0, 23.0, 17.0), 5.0, 5.0);
			dc.DrawGeometry(ink, null, visor);
			double num3 = Math.Max(0.14, Math.Min(1.0, EyeOpen));
			double num4 = 4.6 * num3;
			dc.DrawRoundedRectangle(Brushes.White, null, new Rect(9.5, 8.25 - num4 / 2.0, 2.6, num4), 1.3, Math.Min(1.5, num4 / 2.0));
			dc.DrawRoundedRectangle(Brushes.White, null, new Rect(14.9, 8.25 - num4 / 2.0, 2.6, num4), 1.3, Math.Min(1.5, num4 / 2.0));
			dc.Pop();
		}
	}
}
