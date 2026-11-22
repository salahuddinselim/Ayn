namespace WindowsBuddy.BuddyCapsule.Behavior;

public class AppVisibilityRule
{
	public string ExecutablePath { get; set; }

	public string ProcessName { get; set; }

	public PerAppVisibility Rule { get; set; }

	public AppVisibilityRule()
	{
		Rule = PerAppVisibility.FollowDefault;
	}

	public AppVisibilityRule(string processName, string executablePath, PerAppVisibility rule)
	{
		ProcessName = processName ?? string.Empty;
		ExecutablePath = executablePath ?? string.Empty;
		Rule = rule;
	}
}
