using System;

namespace WindowsBuddy.BuddyCapsule.Core;

public sealed class NotchTransition
{
	public DateTime Time;

	public NotchPhase From;

	public NotchPhase To;

	public string Trigger;

	public override string ToString()
	{
		return $"{Time:HH:mm:ss.fff} {From} -> {To} ({Trigger})";
	}
}
