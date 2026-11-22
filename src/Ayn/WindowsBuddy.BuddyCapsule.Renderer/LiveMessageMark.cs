using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class LiveMessageMark : FrameworkElement
{
	public static readonly DependencyProperty EyeOpenProperty = DependencyProperty.Register("EyeOpen", typeof(double), typeof(LiveMessageMark), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

	private readonly bool _wink;

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

	public LiveMessageMark(double size, bool wink)
	{
		base.Width = (base.Height = size);
		_wink = wink;
		base.IsHitTestVisible = false;
		AutomationProperties.SetName(this, "Ayn");
		RoutedEventHandler value = delegate
		{
			if (LiveMessageVisuals.Motion)
			{
				DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames
				{
					FillBehavior = FillBehavior.Stop
				};
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350.0))));
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(440.0))));
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(560.0))));
				BeginAnimation(EyeOpenProperty, doubleAnimationUsingKeyFrames);
			}
		};
		base.Loaded += value;
	}

	protected override void OnRender(DrawingContext dc)
	{
		double num = base.ActualWidth / 32.0;
		dc.PushTransform(new ScaleTransform(num, num));
		dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.0, 0.0, 32.0, 32.0), 8.0, 8.0);
		dc.DrawRoundedRectangle(Brushes.Black, null, new Rect(5.0, -5.0, 22.0, 24.0), 5.0, 5.0);
		double num2 = (_wink ? 1.0 : EyeOpen);
		double eyeOpen = EyeOpen;
		dc.DrawRoundedRectangle(Brushes.White, null, new Rect(10.0, 12.0 - 3.0 * num2, 3.0, Math.Max(0.8, 6.0 * num2)), 1.5, 1.5);
		dc.DrawRoundedRectangle(Brushes.White, null, new Rect(19.0, 12.0 - 3.0 * eyeOpen, 3.0, Math.Max(0.8, 6.0 * eyeOpen)), 1.5, 1.5);
		dc.Pop();
	}
}
