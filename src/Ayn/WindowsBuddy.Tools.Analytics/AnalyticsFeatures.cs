namespace WindowsBuddy.Tools.Analytics;

public static class AnalyticsFeatures
{
	public static bool GoogleAnalytics;

	public static bool IsAvailable(string provider)
	{
		if (!(provider != "ga4"))
		{
			return GoogleAnalytics;
		}
		return true;
	}
}
