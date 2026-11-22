using System;

namespace WindowsBuddy.Tools.Analytics;

public class AnalyticsException : Exception
{
	public AnalyticsErrorKind Kind { get; private set; }

	public TimeSpan? RetryAfter { get; private set; }

	public AnalyticsException(AnalyticsErrorKind kind, string message, TimeSpan? retryAfter = null)
		: base(message)
	{
		Kind = kind;
		RetryAfter = retryAfter;
	}
}
