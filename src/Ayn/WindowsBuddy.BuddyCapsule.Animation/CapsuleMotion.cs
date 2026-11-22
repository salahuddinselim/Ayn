using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace WindowsBuddy.BuddyCapsule.Animation;

public static class CapsuleMotion
{
	private sealed class SpringEase : EasingFunctionBase
	{
		private const double K = 8.0;

		private const double V0 = 4.0;

		private static readonly double End = 1.0 - 5.0 * Math.Exp(-8.0);

		public SpringEase()
		{
			base.EasingMode = EasingMode.EaseIn;
		}

		protected override double EaseInCore(double t)
		{
			if (t <= 0.0)
			{
				return 0.0;
			}
			if (t >= 1.0)
			{
				return 1.0;
			}
			return (1.0 - (1.0 + 4.0 * t) * Math.Exp(-8.0 * t)) / End;
		}

		protected override Freezable CreateInstanceCore()
		{
			return new SpringEase();
		}
	}

	public static readonly IEasingFunction EaseOutCubic;

	public static readonly IEasingFunction EaseInCubic;

	public static readonly IEasingFunction EaseInOutQuad;

	public static readonly IEasingFunction Spring;

	private static bool? _qaAnimationsOverride;

	public static bool AnimationsEnabled => _qaAnimationsOverride ?? SystemParameters.ClientAreaAnimation;

	public static event EventHandler AnimationsPolicyChanged;

	static CapsuleMotion()
	{
		CubicEase cubicEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		cubicEase.Freeze();
		EaseOutCubic = cubicEase;
		CubicEase cubicEase2 = new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		};
		cubicEase2.Freeze();
		EaseInCubic = cubicEase2;
		QuadraticEase quadraticEase = new QuadraticEase
		{
			EasingMode = EasingMode.EaseInOut
		};
		quadraticEase.Freeze();
		EaseInOutQuad = quadraticEase;
		SpringEase springEase = new SpringEase();
		springEase.Freeze();
		Spring = springEase;
	}

	public static void SetQaAnimationsOverride(bool? enabled)
	{
		_qaAnimationsOverride = enabled;
		CapsuleMotion.AnimationsPolicyChanged?.Invoke(null, EventArgs.Empty);
	}

	public static void AnimateDouble(IAnimatable element, DependencyProperty property, double toValue, double durationSeconds, IEasingFunction easing = null, Action onCompleted = null, TimeSpan? beginTime = null)
	{
		if (element == null)
		{
			return;
		}
		if (!AnimationsEnabled || durationSeconds <= 0.0)
		{
			element.BeginAnimation(property, null);
			if (element is DependencyObject dependencyObject)
			{
				dependencyObject.SetValue(property, toValue);
			}
			if (onCompleted != null)
			{
				onCompleted();
			}
			return;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation();
		doubleAnimation.To = toValue;
		doubleAnimation.Duration = TimeSpan.FromSeconds(durationSeconds);
		doubleAnimation.EasingFunction = easing ?? EaseOutCubic;
		DoubleAnimation doubleAnimation2 = doubleAnimation;
		if (beginTime.HasValue)
		{
			doubleAnimation2.BeginTime = beginTime.Value;
		}
		if (onCompleted != null)
		{
			doubleAnimation2.Completed += delegate
			{
				try
				{
					onCompleted();
				}
				catch
				{
				}
			};
		}
		element.BeginAnimation(property, doubleAnimation2, HandoffBehavior.SnapshotAndReplace);
	}
}
