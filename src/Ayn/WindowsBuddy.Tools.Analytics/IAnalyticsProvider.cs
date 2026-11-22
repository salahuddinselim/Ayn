namespace WindowsBuddy.Tools.Analytics;

public interface IAnalyticsProvider
{
	string Id { get; }

	string Name { get; }

	int RealtimeWindowMinutes { get; }

	string DocsUrl { get; }

	TestResult Test(AnalyticsSite site, string secret);

	AnalyticsSnapshot Fetch(AnalyticsSite site, string secret, AnalyticsRange range);

	int Realtime(AnalyticsSite site, string secret);
}
