using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar;

public sealed class CalendarData
{
	public int Version { get; set; }

	public List<UserEvent> Events { get; set; }

	public List<UserReminder> Reminders { get; set; }

	public CalendarSettings Settings { get; set; }

	public List<string> Fired { get; set; }

	public CalendarData()
	{
		Version = 3;
		Events = new List<UserEvent>();
		Reminders = new List<UserReminder>();
		Settings = new CalendarSettings();
		Fired = new List<string>();
	}
}
