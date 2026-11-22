using System;

namespace WindowsBuddy.Tools.Analytics;

public class AnalyticsSite
{
	public string Key { get; set; }

	public string Provider { get; set; }

	public string SiteId { get; set; }

	public string DisplayName { get; set; }

	public string ServerUrl { get; set; }

	public string GoogleAuth { get; set; }

	public string ServiceAccountEmail { get; set; }

	public string TimeZone { get; set; }

	public string Currency { get; set; }

	public AnalyticsSite()
	{
		Key = Guid.NewGuid().ToString("N").Substring(0, 12);
	}
}
