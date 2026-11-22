using System;

namespace WindowsBuddy.Tools.Clock;

public class TimeZoneOption
{
	public string Id { get; set; }

	public string DisplayName { get; set; }

	public string CitySuggestion { get; set; }

	public TimeSpan BaseUtcOffset { get; set; }
}
