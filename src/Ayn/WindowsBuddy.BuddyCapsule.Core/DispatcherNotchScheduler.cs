using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace WindowsBuddy.BuddyCapsule.Core;

public sealed class DispatcherNotchScheduler : INotchScheduler
{
	private readonly Dispatcher _dispatcher;

	private readonly DispatcherTimer[] _timers;

	private readonly Action[] _callbacks;

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	public long NowMs => _clock.ElapsedMilliseconds;

	public DispatcherNotchScheduler(Dispatcher dispatcher)
	{
		_dispatcher = dispatcher;
		int length = Enum.GetValues(typeof(NotchTimer)).Length;
		_timers = new DispatcherTimer[length];
		_callbacks = new Action[length];
		for (int i = 0; i < length; i++)
		{
			int idx = i;
			DispatcherPriority priority = ((i == 3 || i == 4) ? DispatcherPriority.Background : DispatcherPriority.Normal);
			DispatcherTimer dispatcherTimer = new DispatcherTimer(priority, dispatcher);
			dispatcherTimer.Tick += delegate
			{
				_timers[idx].Stop();
				Action action = _callbacks[idx];
				if (action == null)
				{
					return;
				}
				try
				{
					action();
				}
				catch (Exception ex)
				{
					NotchLog.Error(string.Concat("Notch timer ", (NotchTimer)idx, " failed"), ex);
				}
			};
			_timers[i] = dispatcherTimer;
		}
	}

	public void Schedule(NotchTimer timer, int delayMs, Action callback)
	{
		_callbacks[(int)timer] = callback;
		DispatcherTimer dispatcherTimer = _timers[(int)timer];
		dispatcherTimer.Stop();
		dispatcherTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, delayMs));
		dispatcherTimer.Start();
	}

	public void Cancel(NotchTimer timer)
	{
		_timers[(int)timer].Stop();
		_callbacks[(int)timer] = null;
	}

	public void StopAll()
	{
		for (int i = 0; i < _timers.Length; i++)
		{
			_timers[i].Stop();
			_callbacks[i] = null;
		}
	}
}
