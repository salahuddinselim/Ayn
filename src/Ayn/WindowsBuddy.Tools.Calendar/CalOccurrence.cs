using System;

namespace WindowsBuddy.Tools.Calendar;

public sealed class CalOccurrence
{
	public OccKind Kind;

	public string SourceId;

	public string SourceName;

	public string Id;

	public string Title;

	public string Location;

	public DateTime Start;

	public DateTime End;

	public bool AllDay;

	public CalColor Color;

	public UserEvent Event;

	public string Key => (Id ?? "") + "|" + Start.Ticks;

	public bool ReadOnly => Event == null;

	public bool IsHoliday
	{
		get
		{
			if (Kind != OccKind.Holiday)
			{
				return Kind == OccKind.Observance;
			}
			return true;
		}
	}

	public bool MultiDay => (End.Date - Start.Date).TotalDays > (double)(AllDay ? 1 : ((End.TimeOfDay == TimeSpan.Zero) ? 1 : 0));

	public bool Covers(DateTime day)
	{
		DateTime date = day.Date;
		DateTime dateTime = date.AddDays(1.0);
		if (End <= Start)
		{
			if (Start >= date)
			{
				return Start < dateTime;
			}
			return false;
		}
		if (Start < dateTime)
		{
			return End > date;
		}
		return false;
	}

	public bool IsPast(DateTime now)
	{
		if (!(End > Start))
		{
			return Start < now;
		}
		return End <= now;
	}

	public bool IsNow(DateTime now)
	{
		if (!AllDay && Start <= now)
		{
			return End > now;
		}
		return false;
	}
}
