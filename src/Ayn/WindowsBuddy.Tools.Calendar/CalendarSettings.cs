using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar;

public sealed class CalendarSettings
{
	public string Country { get; set; }

	public bool Holidays { get; set; }

	public bool Observances { get; set; }

	public string View { get; set; }

	public List<CalendarSubscription> Subscriptions { get; set; }

	public CalendarSettings()
	{
		Holidays = true;
		Observances = true;
		View = "agenda";
		Subscriptions = new List<CalendarSubscription>();
	}
}
