using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Clock;

public class ClockSettingsModel
{
	public int SchemaVersion { get; set; }

	public ClockFormat Format { get; set; }

	public bool FollowSystemFormat { get; set; }

	public ClockDisplayMode DisplayMode { get; set; }

	public bool ShowSeconds { get; set; }

	public bool ShowDate { get; set; }

	public ClockDateFormat DateFormat { get; set; }

	public bool AnalogShowSecondHand { get; set; }

	public bool AnalogShowNumerals { get; set; }

	public bool AnalogShowMinuteTicks { get; set; }

	public bool ShowInNotch { get; set; }

	public List<WorldClockCity> WorldClocks { get; set; }

	public ClockSettingsModel()
	{
		SchemaVersion = 2;
		Format = ClockFormat.TwelveHour;
		FollowSystemFormat = true;
		DisplayMode = ClockDisplayMode.Digital;
		ShowSeconds = false;
		ShowDate = true;
		DateFormat = ClockDateFormat.Full;
		AnalogShowSecondHand = true;
		AnalogShowNumerals = true;
		AnalogShowMinuteTicks = true;
		ShowInNotch = false;
		WorldClocks = new List<WorldClockCity>
		{
			new WorldClockCity("local", "Local", TimeZoneInfo.Local.Id, isLocal: true),
			new WorldClockCity("nyc", "New York", "Eastern Standard Time"),
			new WorldClockCity("lon", "London", "GMT Standard Time"),
			new WorldClockCity("tyo", "Tokyo", "Tokyo Standard Time")
		};
	}
}
