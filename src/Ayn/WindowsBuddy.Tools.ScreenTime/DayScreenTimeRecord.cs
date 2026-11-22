using System;
using System.Collections.Generic;
using System.Globalization;

namespace WindowsBuddy.Tools.ScreenTime;

public class DayScreenTimeRecord
{
	public string DateString { get; set; }

	public int TotalSeconds { get; set; }

	public Dictionary<string, int> AppSeconds { get; set; }

	public int[] HourlySeconds { get; set; }

	public DayScreenTimeRecord()
	{
		DateString = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		TotalSeconds = 0;
		AppSeconds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		HourlySeconds = new int[24];
	}
}
