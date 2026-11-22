using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public class BuddyToolDefinition
{
	public string Id { get; set; }

	public string DisplayName { get; set; }

	public string IconKey { get; set; }

	public ToolCategory Category { get; set; }

	public bool IsPrimary { get; set; }

	public bool IsAvailable { get; set; }

	public BuddyToolDefinition(string id, string displayName, string iconKey, ToolCategory category, bool isPrimary = false, bool isAvailable = true)
	{
		Id = id;
		DisplayName = displayName;
		IconKey = iconKey;
		Category = category;
		IsPrimary = isPrimary;
		IsAvailable = isAvailable;
	}
}
