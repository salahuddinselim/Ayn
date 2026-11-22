using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsColorAxisPeer : FrameworkElementAutomationPeer, IRangeValueProvider
{
	public bool IsReadOnly => !base.Owner.IsEnabled;

	public double Minimum => 0.0;

	public double Maximum => 360.0;

	public double SmallChange => 1.0;

	public double LargeChange => 10.0;

	public double Value => ((SettingsColorAxis)base.Owner).Read();

	internal SettingsColorAxisPeer(SettingsColorAxis owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsHue";
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
		((SettingsColorAxis)base.Owner).Write(value);
	}
}
