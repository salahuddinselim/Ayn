using System.Collections.Generic;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsSegmentsPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
	public bool CanSelectMultiple => false;

	public bool IsSelectionRequired => true;

	internal SettingsSegmentsPeer(SSegmented owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsSegments";
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.List;
	}

	public override object GetPattern(PatternInterface pattern)
	{
		if (pattern != PatternInterface.Selection)
		{
			return base.GetPattern(pattern);
		}
		return this;
	}

	protected override List<AutomationPeer> GetChildrenCore()
	{
		List<AutomationPeer> list = new List<AutomationPeer>();
		foreach (Border segment in ((SSegmented)base.Owner).Segments)
		{
			list.Add(UIElementAutomationPeer.CreatePeerForElement(segment));
		}
		return list;
	}

	public IRawElementProviderSimple[] GetSelection()
	{
		foreach (SettingsSegment segment in ((SSegmented)base.Owner).Segments)
		{
			if (segment.Key == ((SSegmented)base.Owner).Selected)
			{
				return new IRawElementProviderSimple[1] { ProviderFromPeer(UIElementAutomationPeer.CreatePeerForElement(segment)) };
			}
		}
		return new IRawElementProviderSimple[0];
	}
}
