using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsInvokePeer : FrameworkElementAutomationPeer, IInvokeProvider
{
	public SettingsInvokePeer(SClickable owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsButton";
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.Button;
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
