using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Analytics;

public static class AnalyticsProviders
{
	public static readonly PlausibleProvider Plausible = new PlausibleProvider();

	public static readonly DataFastProvider DataFast = new DataFastProvider();

	public static readonly GoogleProvider Google = new GoogleProvider();

	public static IAnalyticsProvider For(string id)
	{
		return id switch
		{
			"plausible" => Plausible, 
			"datafast" => DataFast, 
			"ga4" => Google, 
			_ => null, 
		};
	}

	public static string Describe(Exception ex)
	{
		if (ex is AnalyticsException ex2)
		{
			return ex2.Message;
		}
		return "Something went wrong (" + ex.GetType().Name + ")";
	}

	internal static List<SeriesPoint> Fill(Dictionary<DateTime, long> values, DateTime first, DateTime last, bool hourly)
	{
		List<SeriesPoint> list = new List<SeriesPoint>();
		TimeSpan timeSpan = (hourly ? TimeSpan.FromHours(1.0) : TimeSpan.FromDays(1.0));
		for (DateTime dateTime = first; dateTime <= last; dateTime += timeSpan)
		{
			values.TryGetValue(dateTime, out var value);
			list.Add(new SeriesPoint
			{
				Time = dateTime,
				Value = value
			});
		}
		return list;
	}
}
