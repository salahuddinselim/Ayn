using System.Collections.Generic;

namespace WindowsBuddy.Tools;

public class ToolConfigModel
{
	public bool CapsuleEnabled { get; set; }

	public string PrimaryCollapsedToolId { get; set; }

	public string HoverSensitivity { get; set; }

	public bool AnimationsEnabled { get; set; }

	public string Position { get; set; }

	public string PinnedDisplay { get; set; }

	public bool StartWithWindows { get; set; }

	public string OpenOn { get; set; }

	public int HoverDelayMs { get; set; }

	public int DockVersion { get; set; }

	public List<ToolItemConfig> Tools { get; set; }

	public ToolConfigModel()
	{
		CapsuleEnabled = true;
		PrimaryCollapsedToolId = "time";
		HoverSensitivity = "normal";
		AnimationsEnabled = true;
		Position = "center";
		PinnedDisplay = "primary";
		StartWithWindows = false;
		OpenOn = "hover";
		HoverDelayMs = -1;
		Tools = new List<ToolItemConfig>();
	}
}
