using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class AgreementLinkPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
	public AgreementLinkPeer(SLink owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "AgreementLink";
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.Hyperlink;
	}

	public override object GetPattern(PatternInterface pattern)
	{
		if (pattern != 0)
		{
			return base.GetPattern(pattern);
		}
		return this;
	}

	public void Invoke()
	{
		if (!base.Owner.IsEnabled)
		{
			throw new ElementNotEnabledException();
		}
		base.Owner.Dispatcher.BeginInvoke(new Action(((SClickable)base.Owner).RaiseClick));
	}
}
