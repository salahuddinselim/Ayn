using System;
using System.Windows.Automation.Peers;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsColorPlane : SettingsColorSurface
{
	internal readonly Func<string> Read;

	internal readonly Action<string> Write;

	internal SettingsColorPlane(Func<string> read, Action<string> write)
	{
		Read = read;
		Write = write;
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsColorPlanePeer(this);
	}
}
