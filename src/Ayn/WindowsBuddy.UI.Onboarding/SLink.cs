using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class SLink : SClickable
{
	private readonly TextBlock _text;

	public string Text => _text.Text;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new AgreementLinkPeer(this);
	}

	public SLink(string text, string helpText)
		: base(4.0, text)
	{
		base.VisualRole = SVisualRole.Bare;
		base.MinWidth = 0.0;
		base.MinHeight = 28.0;
		base.Padding = new Thickness(4.0, 2.0, 4.0, 2.0);
		_text = SettingsTheme.Text(text, 13.5, SettingsTheme.TextSecondary, AynType.Medium);
		_text.TextWrapping = TextWrapping.NoWrap;
		_text.TextDecorations = TextDecorations.Underline;
		Child = _text;
		if (!string.IsNullOrEmpty(helpText))
		{
			AutomationProperties.SetHelpText(this, helpText);
			base.ToolTip = helpText;
		}
	}

	protected override void OnVisual()
	{
		base.OnVisual();
		if (_text != null)
		{
			_text.Foreground = ((base.IsEnabled && (base.IsMouseOver || base.IsKeyboardFocused)) ? SettingsTheme.TextPrimary : SettingsTheme.TextSecondary);
		}
	}
}
