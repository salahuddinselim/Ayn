using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar;

public sealed class IcsEvent
{
	public string Uid;

	public string Summary;

	public string Description;

	public string Location;

	public string Status;

	public IcsTime Start;

	public IcsTime End;

	public TimeSpan? Duration;

	public IcsRule Rule;

	public List<IcsTime> ExDates = new List<IcsTime>();

	public List<IcsTime> RDates = new List<IcsTime>();

	public IcsTime RecurrenceId;

	public bool AllDay
	{
		get
		{
			if (Start != null)
			{
				return Start.IsDate;
			}
			return false;
		}
	}

	public bool Cancelled => string.Equals(Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);

	public TimeSpan Length
	{
		get
		{
			if (End != null && Start != null)
			{
				TimeSpan timeSpan = (AllDay ? (End.Value.Date - Start.Value.Date) : (End.ToLocal() - Start.ToLocal()));
				if (timeSpan > TimeSpan.Zero)
				{
					return timeSpan;
				}
				if (!AllDay)
				{
					return TimeSpan.Zero;
				}
				return TimeSpan.FromDays(1.0);
			}
			if (Duration.HasValue && Duration.Value > TimeSpan.Zero)
			{
				return Duration.Value;
			}
			if (!AllDay)
			{
				return TimeSpan.Zero;
			}
			return TimeSpan.FromDays(1.0);
		}
	}
}
