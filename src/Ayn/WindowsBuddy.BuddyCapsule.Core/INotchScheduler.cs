using System;

namespace WindowsBuddy.BuddyCapsule.Core;

public interface INotchScheduler
{
	long NowMs { get; }

	void Schedule(NotchTimer timer, int delayMs, Action callback);

	void Cancel(NotchTimer timer);
}
