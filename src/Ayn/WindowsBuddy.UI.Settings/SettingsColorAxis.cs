using System;
using System.Windows.Automation.Peers;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsColorAxis : SettingsColorSurface
{
	internal readonly Func<double> Read;

	internal readonly Action<double> Write;

	internal SettingsColorAxis(Func<double> read, Action<double> write)
	{
		Read = read;
		Write = write;
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsColorAxisPeer(this);
	}
}
