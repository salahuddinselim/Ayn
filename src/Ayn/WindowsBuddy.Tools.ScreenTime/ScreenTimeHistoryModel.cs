using System.Collections.Generic;

namespace WindowsBuddy.Tools.ScreenTime;

public class ScreenTimeHistoryModel
{
	public bool IsTrackingPaused { get; set; }

	public int DailyLimitSeconds { get; set; }

	public bool NotifyOnLimitReached { get; set; }

	public bool ShowNotchAlertOnLimit { get; set; }

	public string LastLimitAlertDate { get; set; }

	public List<DayScreenTimeRecord> Days { get; set; }

	public Dictionary<string, string> ExePaths { get; set; }

	public ScreenTimeHistoryModel()
	{
		IsTrackingPaused = false;
		DailyLimitSeconds = 25200;
		NotifyOnLimitReached = true;
		ShowNotchAlertOnLimit = true;
		LastLimitAlertDate = string.Empty;
		Days = new List<DayScreenTimeRecord>();
	}
}
