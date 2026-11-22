using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsSliderPeer : FrameworkElementAutomationPeer, IRangeValueProvider
{
	private SSlider Slider => (SSlider)base.Owner;

	public bool IsReadOnly => !base.Owner.IsEnabled;

	public double Minimum => Slider.Minimum;

	public double Maximum => Slider.Maximum;

	public double SmallChange => Slider.Step;

	public double LargeChange => Slider.Step * 5.0;

	public double Value => Slider.Value;

	public SettingsSliderPeer(SSlider owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsSlider";
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
		if (!base.Owner.IsEnabled)
		{
			throw new ElementNotEnabledException();
		}
		if (double.IsNaN(value) || value < Minimum || value > Maximum)
		{
			throw new ArgumentOutOfRangeException("value");
		}
		Slider.SetUserValue(value);
	}
}
