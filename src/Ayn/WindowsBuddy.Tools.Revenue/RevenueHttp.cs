using System;
using WindowsBuddy.Tools.Analytics;

namespace WindowsBuddy.Tools.Revenue;

public static class RevenueHttp
{
	public static Func<HttpCall, HttpReply> Transport = AnalyticsHttp.DefaultTransport;

	public static HttpReply Send(HttpCall call)
	{
		HttpReply httpReply = Transport(call);
		if (httpReply == null || httpReply.Status == 0)
		{
			throw new RevenueException(RevenueErrorKind.Offline, "You're offline", "Check your internet connection. Your last numbers stay on screen until Ayn can refresh.");
		}
		return httpReply;
	}

	internal static TimeSpan? RetryAfter(HttpReply r)
	{
		if (r.Headers.TryGetValue("Retry-After", out var value) && int.TryParse(value, out var result))
		{
			return TimeSpan.FromSeconds(Math.Max(1, result));
		}
		return null;
	}
}
