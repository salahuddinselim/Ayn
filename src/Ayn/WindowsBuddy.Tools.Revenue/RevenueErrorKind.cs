namespace WindowsBuddy.Tools.Revenue;

public enum RevenueErrorKind
{
	None,
	WrongKey,
	WrongKeyType,
	Revoked,
	NoPermission,
	Offline,
	RateLimited,
	Server,
	BadResponse,
	NotConfigured
}
