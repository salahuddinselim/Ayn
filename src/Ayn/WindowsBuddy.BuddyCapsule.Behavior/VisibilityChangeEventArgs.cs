using System;
using WindowsBuddy.BuddyCapsule.Core;

namespace WindowsBuddy.BuddyCapsule.Behavior;

public class VisibilityChangeEventArgs : EventArgs
{
	public NotchPresence Presence { get; private set; }

	public bool ShouldBeVisible => Presence != NotchPresence.None;

	public bool IsTransient { get; private set; }

	public string Reason { get; private set; }

	public VisibilityChangeEventArgs(NotchPresence presence, bool isTransient, string reason)
	{
		Presence = presence;
		IsTransient = isTransient;
		Reason = reason;
	}
}
