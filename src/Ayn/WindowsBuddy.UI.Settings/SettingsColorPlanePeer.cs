using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsColorPlanePeer : FrameworkElementAutomationPeer, IValueProvider
{
	public bool IsReadOnly => !base.Owner.IsEnabled;

	public string Value => ((SettingsColorPlane)base.Owner).Read();

	internal SettingsColorPlanePeer(SettingsColorPlane owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsSaturationBrightness";
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.Custom;
	}

	public override object GetPattern(PatternInterface pattern)
	{
		if (pattern != PatternInterface.Value)
		{
			return base.GetPattern(pattern);
		}
		return this;
	}

	public void SetValue(string value)
	{
		if (!base.Owner.IsEnabled)
		{
			throw new ElementNotEnabledException();
		}
		((SettingsColorPlane)base.Owner).Write(value);
	}
}
