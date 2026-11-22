using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using WindowsBuddy.Services.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class MediaSeekSurface : Grid
{
	private sealed class SeekPeer : FrameworkElementAutomationPeer, IRangeValueProvider
	{
		private readonly MediaSeekSurface _bar;

		public bool IsReadOnly => !_bar.IsEnabled;

		public double Minimum => 0.0;

		public double Maximum => 100.0;

		public double SmallChange
		{
			get
			{
				MediaViewState mediaViewState = _bar.StateProvider();
				if (mediaViewState == null || !(mediaViewState.Timeline.DurationSeconds > 0.0))
				{
					return 1.0;
				}
				return 500.0 / mediaViewState.Timeline.DurationSeconds;
			}
		}

		public double LargeChange => 10.0;

		public double Value
		{
			get
			{
				MediaViewState mediaViewState = _bar.StateProvider();
				if (mediaViewState != null)
				{
					return Math.Max(0.0, mediaViewState.Timeline.Progress(DateTime.UtcNow)) * 100.0;
				}
				return 0.0;
			}
		}

		public SeekPeer(MediaSeekSurface bar)
			: base(bar)
		{
			_bar = bar;
		}

		protected override string GetClassNameCore()
		{
			return "MediaSeekSurface";
		}

		protected override AutomationControlType GetAutomationControlTypeCore()
		{
			return AutomationControlType.Slider;
		}

		public override object GetPattern(PatternInterface pattern)
		{
			if (pattern != PatternInterface.RangeValue)
			{
				return base.GetPattern(pattern);
			}
			return this;
		}

		public void SetValue(double value)
		{
			if (!_bar.IsEnabled)
			{
				throw new ElementNotEnabledException();
			}
			if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value > 100.0)
			{
				throw new ArgumentOutOfRangeException("value");
			}
			_bar.SeekFraction(value / 100.0);
		}
	}

	internal Func<MediaViewState> StateProvider;

	internal Action<double> SeekFraction;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SeekPeer(this);
	}
}
