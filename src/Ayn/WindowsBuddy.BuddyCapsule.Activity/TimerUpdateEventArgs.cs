using System;

namespace WindowsBuddy.BuddyCapsule.Activity;

public class TimerUpdateEventArgs : EventArgs
{
	public long Generation { get; private set; }

	public string DisplayText { get; private set; }

	public bool IsPaused { get; private set; }

	public TimerUpdateEventArgs(long generation, string displayText, bool isPaused)
	{
		Generation = generation;
		DisplayText = displayText;
		IsPaused = isPaused;
	}
}
