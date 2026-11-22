using System;

namespace WindowsBuddy.Tools.Revenue;

public class RevenueException : Exception
{
	public RevenueErrorKind Kind { get; private set; }

	public string Advice { get; private set; }

	public TimeSpan? RetryAfter { get; private set; }

	public RevenueException(RevenueErrorKind kind, string message, string advice, TimeSpan? retryAfter = null)
		: base(message)
	{
		Kind = kind;
		Advice = advice;
		RetryAfter = retryAfter;
	}
}
