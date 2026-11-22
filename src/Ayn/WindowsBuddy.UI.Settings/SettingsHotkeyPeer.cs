using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsHotkeyPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
	internal SettingsHotkeyPeer(SHotkeyRecorder owner)
		: base(owner)
	{
	}

	protected override string GetClassNameCore()
	{
		return "SettingsHotkeyRecorder";
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
		((SHotkeyRecorder)base.Owner).ToggleRecording();
	}
}
