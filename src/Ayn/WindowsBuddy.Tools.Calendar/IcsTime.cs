using System;

namespace WindowsBuddy.Tools.Calendar;

public sealed class IcsTime
{
	public DateTime Value;

	public bool IsDate;

	public bool IsUtc;

	public TimeZoneInfo Zone;

	public IcsTime WithValue(DateTime v)
	{
		IcsTime icsTime = new IcsTime();
		icsTime.Value = v;
		icsTime.IsDate = IsDate;
		icsTime.IsUtc = IsUtc;
		icsTime.Zone = Zone;
		return icsTime;
	}

	public DateTime ToLocal()
	{
		return Ics.WallToLocal(Value, this);
	}

	public long Key()
	{
		if (IsDate)
		{
			return Value.Date.Ticks;
		}
		return ToLocal().Ticks;
	}
}
