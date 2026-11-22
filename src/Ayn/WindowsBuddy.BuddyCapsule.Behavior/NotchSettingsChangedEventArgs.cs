using System;

namespace WindowsBuddy.BuddyCapsule.Behavior;

public sealed class NotchSettingsChangedEventArgs : EventArgs
{
	public NotchSettingsArea Area { get; private set; }

	public NotchSettingsChangedEventArgs(NotchSettingsArea area)
	{
		Area = area;
	}
}
