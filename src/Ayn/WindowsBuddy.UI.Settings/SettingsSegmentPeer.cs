using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsSegmentPeer : FrameworkElementAutomationPeer, ISelectionItemProvider
{
	private SettingsSegment Segment => (SettingsSegment)base.Owner;

	public bool IsSelected => Segment.Group.Selected == Segment.Key;

	public IRawElementProviderSimple SelectionContainer => ProviderFromPeer(UIElementAutomationPeer.CreatePeerForElement(Segment.Group));

	internal SettingsSegmentPeer(SettingsSegment owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsSegment";
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.ListItem;
	}

	public override object GetPattern(PatternInterface pattern)
	{
		if (pattern != PatternInterface.SelectionItem)
		{
			return base.GetPattern(pattern);
		}
		return this;
	}

	public void Select()
	{
		if (!base.Owner.IsEnabled)
		{
			throw new ElementNotEnabledException();
		}
		Segment.Group.Focus();
		Segment.Group.Select(Segment.Key, raise: true);
	}

	public void AddToSelection()
	{
		Select();
	}

	public void RemoveFromSelection()
	{
		throw new InvalidOperationException("A segmented control requires one selection.");
	}
}
