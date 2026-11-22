using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class AgreementCheckBoxPeer : FrameworkElementAutomationPeer, IToggleProvider
{
	public ToggleState ToggleState
	{
		get
		{
			if (!((SCheckBox)base.Owner).IsChecked)
			{
				return ToggleState.Off;
			}
			return ToggleState.On;
		}
	}

	public AgreementCheckBoxPeer(SCheckBox owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "AgreementCheckBox";
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
		((SCheckBox)base.Owner).Toggle();
	}
}
