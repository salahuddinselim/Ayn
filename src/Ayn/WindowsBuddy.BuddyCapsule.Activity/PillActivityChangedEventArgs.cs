using System;

namespace WindowsBuddy.BuddyCapsule.Activity;

public class PillActivityChangedEventArgs : EventArgs
{
	public PillActivitySnapshot OldActivity { get; private set; }

	public PillActivitySnapshot NewActivity { get; private set; }

	public bool IsUpdateOnly { get; private set; }

	public ActivityTransitionType TransitionType { get; private set; }

	public PillActivityChangedEventArgs(PillActivitySnapshot oldActivity, PillActivitySnapshot newActivity, bool isUpdateOnly, ActivityTransitionType transition)
	{
		OldActivity = oldActivity;
		NewActivity = newActivity;
		IsUpdateOnly = isUpdateOnly;
		TransitionType = transition;
	}
}
