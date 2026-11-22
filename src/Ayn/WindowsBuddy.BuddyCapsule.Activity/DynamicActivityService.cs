using System;
using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.Activity;

public sealed class DynamicActivityService
{
	private static readonly Lazy<DynamicActivityService> _instanceAdapter = new Lazy<DynamicActivityService>(() => new DynamicActivityService());

	public static DynamicActivityService Instance => _instanceAdapter.Value;

	public NotchActivity CurrentActivity => PillActivityArbiter.Instance.CurrentActivity?.ToLegacyNotchActivity();

	public NotchActivity PersistentActivity
	{
		get
		{
			PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
			if (currentActivity != null && currentActivity.Tier == ActivityTier.Tier2_Session)
			{
				return currentActivity.ToLegacyNotchActivity();
			}
			return null;
		}
	}

	public bool HasActiveTemporaryActivity => PillActivityArbiter.Instance.HasActiveTransient;

	public long CurrentGeneration => PillActivityArbiter.Instance.CurrentSequenceNumber;

	public event Action<NotchActivity> Published;

	public event EventHandler<ActivityTransitionEventArgs> ActivityChanged;

	public event EventHandler<TimerUpdateEventArgs> TimerUpdated;

	private DynamicActivityService()
	{
		PillActivityArbiter instance = PillActivityArbiter.Instance;
		EventHandler<PillActivityChangedEventArgs> value = delegate(object s, PillActivityChangedEventArgs e)
		{
			EventHandler<ActivityTransitionEventArgs> activityChanged = this.ActivityChanged;
			if (activityChanged != null)
			{
				try
				{
					NotchActivity oldActivity = ((e.OldActivity != null) ? e.OldActivity.ToLegacyNotchActivity() : null);
					NotchActivity newActivity = ((e.NewActivity != null) ? e.NewActivity.ToLegacyNotchActivity() : null);
					activityChanged(this, new ActivityTransitionEventArgs(oldActivity, newActivity, e.TransitionType));
				}
				catch
				{
				}
			}
		};
		instance.ActivityChanged += value;
		PillActivityArbiter.Instance.TimerUpdated += delegate(object s, TimerUpdateEventArgs e)
		{
			EventHandler<TimerUpdateEventArgs> timerUpdated = this.TimerUpdated;
			if (timerUpdated != null)
			{
				try
				{
					timerUpdated(this, e);
				}
				catch
				{
				}
			}
		};
	}

	public void PublishActivity(NotchActivity activity)
	{
		if (activity == null)
		{
			return;
		}
		this.Published?.Invoke(activity);
		PillActivitySnapshot pillActivitySnapshot = activity.ToSnapshot();
		if (pillActivitySnapshot.Tier == ActivityTier.Tier0_Critical)
		{
			PillActivityArbiter.Instance.SetCriticalAlert(pillActivitySnapshot);
		}
		else if (pillActivitySnapshot.Type == ActivityType.CompanionMessage)
		{
			if (activity.Mode == ActivityMode.Persistent)
			{
				PillActivityArbiter.Instance.PublishMessageSession(pillActivitySnapshot);
				return;
			}
			NotchActivity persistentActivity = PersistentActivity;
			if (persistentActivity == null || persistentActivity.Type != ActivityType.Media || !persistentActivity.IsPlaying)
			{
				PillActivityArbiter.Instance.PublishMessageTransient(pillActivitySnapshot, activity.Duration);
			}
		}
		else if (pillActivitySnapshot.Type == ActivityType.Media)
		{
			PillActivityArbiter.Instance.PublishMedia(pillActivitySnapshot);
		}
		else if (pillActivitySnapshot.Type == ActivityType.TimerRunning)
		{
			PillActivityArbiter.Instance.PublishTimer(pillActivitySnapshot);
		}
		else if (pillActivitySnapshot.Type == ActivityType.Custom && activity.Mode == ActivityMode.Persistent)
		{
			PillActivityArbiter.Instance.PublishCustomSession(pillActivitySnapshot);
		}
		else if (pillActivitySnapshot.Tier == ActivityTier.Tier1_Transient)
		{
			PillActivityArbiter.Instance.PublishTransient(pillActivitySnapshot, activity.Duration);
		}
		else
		{
			PillActivityArbiter.Instance.PublishTransient(pillActivitySnapshot, activity.Duration);
		}
	}

	public void UpdateTimerInPlace(long generation, string display, bool isPaused)
	{
		PillActivityArbiter.Instance.UpdateTimerTick(display, isPaused);
	}

	public void ClearPersistentActivity(ActivityType type, long generation = 0L)
	{
		switch (type)
		{
		case ActivityType.Media:
			PillActivityArbiter.Instance.ClearMedia();
			break;
		case ActivityType.TimerRunning:
			PillActivityArbiter.Instance.ClearTimer();
			break;
		case ActivityType.CompanionMessage:
			PillActivityArbiter.Instance.ClearMessage();
			break;
		case ActivityType.Custom:
			PillActivityArbiter.Instance.ClearCustom();
			break;
		}
	}

	public void DismissCurrentTemporaryActivity()
	{
		PillActivityArbiter.Instance.DismissCurrentTransient();
	}

	public void SetRestingClock(string timeText, string dateText, string batteryText)
	{
		PillActivityArbiter.Instance.UpdateClock(timeText, dateText, batteryText, isAnalog: false);
	}

	public void PublishClockActivity(NotchActivity activity)
	{
		if (activity != null)
		{
			bool isAnalog = string.Equals(activity.Payload as string, "analog", StringComparison.OrdinalIgnoreCase);
			PillActivityArbiter.Instance.UpdateClock(activity.Title, activity.Subtitle, activity.ValueText, isAnalog);
		}
	}

	public void ClearClockActivity()
	{
		PillActivityArbiter.Instance.UpdateClock(DateTime.Now.ToString("h:mm tt"), DateTime.Now.ToString("dddd · MMM d"), "", isAnalog: false);
	}

	public void ResetToRestingClock()
	{
		PillActivityArbiter.Instance.ResetToRestingClock();
	}

	public IReadOnlyList<ActivityTimelineEntry> GetTimeline()
	{
		return PillActivityArbiter.Instance.GetTimeline();
	}
}
