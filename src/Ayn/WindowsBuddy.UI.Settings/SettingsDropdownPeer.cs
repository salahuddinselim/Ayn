using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsDropdownPeer : FrameworkElementAutomationPeer, IExpandCollapseProvider
{
	public ExpandCollapseState ExpandCollapseState
	{
		get
		{
			if (!((SDropdown)base.Owner).IsExpanded)
			{
				return ExpandCollapseState.Collapsed;
			}
			return ExpandCollapseState.Expanded;
		}
	}

	internal SettingsDropdownPeer(SDropdown owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsDropdown";
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.ComboBox;
	}

	public override object GetPattern(PatternInterface pattern)
	{
		if (pattern != PatternInterface.ExpandCollapse)
		{
			return base.GetPattern(pattern);
		}
		return this;
	}

	public void Expand()
	{
		if (!base.Owner.IsEnabled)
		{
			throw new ElementNotEnabledException();
		}
		((SDropdown)base.Owner).SetExpanded(value: true);
	}

	public void Collapse()
	{
		if (!base.Owner.IsEnabled)
		{
			throw new ElementNotEnabledException();
		}
		((SDropdown)base.Owner).SetExpanded(value: false);
	}
}
