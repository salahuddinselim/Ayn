using System;

namespace WindowsBuddy.BuddyCapsule.Activity;

public class ActivityTimelineEntry
{
	public DateTime Timestamp { get; set; }

	public string EventType { get; set; }

	public string ActivityId { get; set; }

	public ActivityPriority Priority { get; set; }

	public string Details { get; set; }

	public ActivityTimelineEntry(string eventType, string activityId, ActivityPriority priority, string details)
	{
		Timestamp = DateTime.Now;
		EventType = eventType;
		ActivityId = activityId ?? "";
		Priority = priority;
		Details = details ?? "";
	}

	public override string ToString()
	{
		return $"{Timestamp:HH:mm:ss.fff} [{EventType}] {ActivityId} (P{(int)Priority}) {Details}";
	}
}
