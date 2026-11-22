using System.Windows.Automation.Peers;
using System.Windows.Input;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsSegment : SClickable
{
	internal readonly SSegmented Group;

	internal readonly string Key;

	internal SettingsSegment(SSegmented group, string key, string label)
		: base(6.0, label)
	{
		Group = group;
		Key = key;
		base.VisualRole = SVisualRole.Segment;
		base.Focusable = false;
		KeyboardNavigation.SetIsTabStop(this, isTabStop: false);
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsSegmentPeer(this);
	}
}
