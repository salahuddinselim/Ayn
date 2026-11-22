namespace WindowsBuddy.Taskbar;

public class TaskbarPropertyCapability
{
	public string PropertyKey { get; private set; }

	public string DisplayName { get; private set; }

	public CapabilityTier Tier { get; private set; }

	public string Description { get; private set; }

	public bool IsSupported { get; private set; }

	public TaskbarPropertyCapability(string propertyKey, string displayName, CapabilityTier tier, string description, bool isSupported)
	{
		PropertyKey = propertyKey;
		DisplayName = displayName;
		Tier = tier;
		Description = description;
		IsSupported = isSupported;
	}

	public string GetTierBadgeText()
	{
		return Tier switch
		{
			CapabilityTier.RealNative => "Native Windows", 
			CapabilityTier.Partial => "System Accent", 
			CapabilityTier.PreviewOnly => "Studio Preview", 
			_ => "Unsupported", 
		};
	}
}
