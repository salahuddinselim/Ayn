using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WindowsBuddy.UI.Settings;

internal static class SMotion
{
	internal const double DurationMs = 180.0;

	internal static ScaleTransform AddScale(FrameworkElement element)
	{
		ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
		TransformGroup transformGroup = new TransformGroup();
		if (element.RenderTransform != null && !element.RenderTransform.Value.IsIdentity)
		{
			transformGroup.Children.Add(element.RenderTransform);
		}
		transformGroup.Children.Add(scaleTransform);
		element.RenderTransform = transformGroup;
		element.RenderTransformOrigin = new Point(0.5, 0.5);
		return scaleTransform;
	}

	internal static void To(Transform target, DependencyProperty property, double value, bool animate)
	{
		if (target != null)
		{
			if (!animate)
			{
				target.BeginAnimation(property, null);
				target.SetValue(property, value);
			}
			else if (!(Math.Abs((double)target.GetAnimationBaseValue(property) - value) < 1E-05))
			{
				double fromValue = (double)target.GetValue(property);
				target.BeginAnimation(property, null);
				target.SetValue(property, value);
				target.BeginAnimation(property, new DoubleAnimation(fromValue, value, TimeSpan.FromMilliseconds(180.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					},
					FillBehavior = FillBehavior.Stop
				});
			}
		}
	}

	internal static void Scale(ScaleTransform target, double value, bool animate)
	{
		To(target, ScaleTransform.ScaleXProperty, value, animate);
		To(target, ScaleTransform.ScaleYProperty, value, animate);
	}

	internal static void Finish(Transform target)
	{
		if (target != null)
		{
			if (target is ScaleTransform scaleTransform)
			{
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			}
			if (target is TranslateTransform translateTransform)
			{
				translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
				translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			}
		}
	}
}
