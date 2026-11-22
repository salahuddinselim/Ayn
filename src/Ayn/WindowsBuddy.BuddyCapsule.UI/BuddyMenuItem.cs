using System;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddyMenuItem
{
	public string Label;

	public string Icon;

	public bool IsChecked;

	public Action Invoke;

	public BuddyMenuItem(string label, string icon, Action invoke)
	{
		Label = label;
		Icon = icon;
		Invoke = invoke;
	}
}
