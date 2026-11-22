using System.Windows.Automation.Peers;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class TrackedTextPeer : FrameworkElementAutomationPeer
{
	public TrackedTextPeer(TrackedText owner)
		: base(owner)
	{
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.Text;
	}

	protected override string GetClassNameCore()
	{
		return "TrackedText";
	}

	protected override string GetNameCore()
	{
		string nameCore = base.GetNameCore();
		if (!string.IsNullOrEmpty(nameCore))
		{
			return nameCore;
		}
		return ((TrackedText)base.Owner).Text;
	}

	protected override bool IsControlElementCore()
	{
		return true;
	}

	protected override bool IsContentElementCore()
	{
		return true;
	}
}
