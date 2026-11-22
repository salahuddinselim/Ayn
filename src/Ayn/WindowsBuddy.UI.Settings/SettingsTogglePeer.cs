using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsTogglePeer : FrameworkElementAutomationPeer, IToggleProvider
{
	public ToggleState ToggleState
	{
		get
		{
			if (!((SToggle)base.Owner).IsOn)
			{
				return ToggleState.Off;
			}
			return ToggleState.On;
		}
	}

	public SettingsTogglePeer(SToggle owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsToggle";
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.CheckBox;
	}

	public override object GetPattern(PatternInterface pattern)
	{
		if (pattern != PatternInterface.Toggle)
		{
			return base.GetPattern(pattern);
		}
		return this;
	}

	public void Toggle()
	{
		if (!base.Owner.IsEnabled)
		{
			throw new ElementNotEnabledException();
		}
		((SToggle)base.Owner).Flip();
	}
}
