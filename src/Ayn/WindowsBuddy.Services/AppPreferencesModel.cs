namespace WindowsBuddy.Services;

public class AppPreferencesModel
{
	public string HomeWidgetOrder { get; set; }

	public string HomeWidgetSlots { get; set; }

	public string HiddenHomeWidgets { get; set; }

	public string UpdateNoticeDay { get; set; }

	public int HotkeyModifiers { get; set; }

	public int HotkeyKey { get; set; }

	public string Language { get; set; }

	public string Theme { get; set; }

	public string ColorStyle { get; set; }

	public string Accent { get; set; }

	public string DropSaveFolder { get; set; }

	public bool DropFirewallExplained { get; set; }

	public bool DropRequirePin { get; set; }

	public string DropFirewallExplainedPath { get; set; }

	public int ClipboardHistoryLimit { get; set; }

	public int ClipboardRetentionDays { get; set; }

	public bool ShowWebsiteIconsInLinks { get; set; }

	public bool ClipboardRecording { get; set; }

	public bool AutomaticWeeklyBackup { get; set; }

	public bool BackupIncludeClipboardImages { get; set; }

	public bool BackupIncludeShelfFiles { get; set; }

	public double WindowLeft { get; set; }

	public double WindowTop { get; set; }

	public double WindowWidth { get; set; }

	public double WindowHeight { get; set; }

	public bool WindowMaximized { get; set; }

	public bool HasWindowBounds { get; set; }

	public string LastPage { get; set; }

	public string EulaAcceptedVersion { get; set; }

	public string EulaAcceptedUtc { get; set; }

	public string EulaAcceptedAppVersion { get; set; }

	internal AppPreferencesModel Copy()
	{
		return (AppPreferencesModel)MemberwiseClone();
	}

	public AppPreferencesModel()
	{
		Language = "en";
		Theme = "Dark";
		ColorStyle = "Mono";
		Accent = "#F59E0B";
		ClipboardHistoryLimit = 500;
		ClipboardRetentionDays = 30;
		ClipboardRecording = true;
		ShowWebsiteIconsInLinks = true;
		DropRequirePin = true;
		WindowWidth = 960.0;
		WindowHeight = 720.0;
		HasWindowBounds = false;
		LastPage = "general";
	}
}
