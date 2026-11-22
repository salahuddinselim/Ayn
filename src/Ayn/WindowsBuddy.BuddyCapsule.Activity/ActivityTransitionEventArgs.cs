using System;

namespace WindowsBuddy.BuddyCapsule.Activity;

public class ActivityTransitionEventArgs : EventArgs
{
	public NotchActivity OldActivity { get; private set; }

	public NotchActivity NewActivity { get; private set; }

	public ActivityTransitionType TransitionType { get; private set; }

	public ActivityTransitionEventArgs(NotchActivity oldActivity, NotchActivity newActivity, ActivityTransitionType transition)
	{
		OldActivity = oldActivity;
		NewActivity = newActivity;
		TransitionType = transition;
	}
}
