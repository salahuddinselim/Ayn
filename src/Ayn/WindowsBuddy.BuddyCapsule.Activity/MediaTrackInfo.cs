using System;

namespace WindowsBuddy.BuddyCapsule.Activity;

public sealed class MediaTrackInfo
{
	public string Artist;

	public double PositionSeconds;

	public double DurationSeconds;

	public DateTime UpdatedUtc;

	public bool Playing;

	public double RemainingSeconds(DateTime nowUtc)
	{
		if (DurationSeconds <= 1.0)
		{
			return -1.0;
		}
		double val = PositionSeconds + (Playing ? Math.Max(0.0, (nowUtc - UpdatedUtc).TotalSeconds) : 0.0);
		return Math.Max(0.0, DurationSeconds - Math.Min(DurationSeconds, val));
	}

	public double Progress(DateTime nowUtc)
	{
		double num = RemainingSeconds(nowUtc);
		if (!(num < 0.0))
		{
			return 1.0 - num / DurationSeconds;
		}
		return -1.0;
	}
}
