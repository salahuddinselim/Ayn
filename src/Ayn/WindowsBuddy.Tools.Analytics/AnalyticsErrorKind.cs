namespace WindowsBuddy.Tools.Analytics;

public enum AnalyticsErrorKind
{
	None,
	WrongKey,
	WrongSite,
	NoAccess,
	Offline,
	RateLimited,
	PlanNotSupported,
	Server,
	BadResponse,
	NotConfigured
}
