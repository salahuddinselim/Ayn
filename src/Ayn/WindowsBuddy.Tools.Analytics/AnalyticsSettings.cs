using System.Collections.Generic;

namespace WindowsBuddy.Tools.Analytics;

public class AnalyticsSettings
{
	public int Version { get; set; }

	public List<AnalyticsSite> Sites { get; set; }

	public string ActiveSiteKey { get; set; }

	public AnalyticsRange Range { get; set; }

	public Breakdown Breakdown { get; set; }

	public bool ShowLiveOnNotch { get; set; }

	public string GoogleClientId { get; set; }

	public string GoogleClientSecret { get; set; }

	public AnalyticsSettings()
	{
		Version = 2;
		Sites = new List<AnalyticsSite>();
		Range = AnalyticsRange.Week;
		Breakdown = Breakdown.Pages;
	}
}
