using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WindowsBuddy.Tools.Analytics;

public sealed class DataFastProvider : IAnalyticsProvider
{
	public const string BaseUrl = "https://datafa.st/api/v1";

	public string Id => "datafast";

	public string Name => "DataFast";

	public int RealtimeWindowMinutes => 10;

	public string DocsUrl => "https://datafa.st/docs/api-introduction";

	public static string CheckKey(string key)
	{
		key = (key ?? "").Trim();
		if (key.StartsWith("dft_", StringComparison.Ordinal))
		{
			return "That's an account token — use the website API key (starts with df_)";
		}
		if (!key.StartsWith("df_", StringComparison.Ordinal))
		{
			return "DataFast website keys start with df_";
		}
		return null;
	}

	private object Get(string key, string path, Dictionary<string, string> query)
	{
		StringBuilder stringBuilder = new StringBuilder("https://datafa.st/api/v1" + path);
		if (query != null && query.Count > 0)
		{
			stringBuilder.Append("?").Append(string.Join("&", query.Select((KeyValuePair<string, string> kv) => kv.Key + "=" + Uri.EscapeDataString(kv.Value))));
		}
		HttpCall httpCall = new HttpCall();
		httpCall.Method = "GET";
		httpCall.Url = stringBuilder.ToString();
		HttpCall httpCall2 = httpCall;
		httpCall2.Headers["Authorization"] = "Bearer " + key;
		HttpReply httpReply = AnalyticsHttp.Send(httpCall2);
		object obj = J.Parse(httpReply.Body);
		if (httpReply.Status >= 200 && httpReply.Status < 300)
		{
			return obj;
		}
		string text = J.Str(J.Get(obj, "error", "message")) ?? "";
		switch (httpReply.Status)
		{
		case 401:
			throw new AnalyticsException(AnalyticsErrorKind.WrongKey, "That API key isn't valid — copy the website key (df_…) from DataFast");
		case 403:
			throw new AnalyticsException(AnalyticsErrorKind.NoAccess, "This key can't read analytics for the site");
		case 404:
			throw new AnalyticsException(AnalyticsErrorKind.WrongSite, "DataFast couldn't find this website");
		default:
			if (httpReply.Status >= 500)
			{
				throw new AnalyticsException(AnalyticsErrorKind.Server, "DataFast is having trouble (" + httpReply.Status + ")");
			}
			throw new AnalyticsException(AnalyticsErrorKind.BadResponse, string.IsNullOrEmpty(text) ? ("DataFast rejected the request (" + httpReply.Status + ")") : text);
		}
	}

	public int Realtime(AnalyticsSite site, string key)
	{
		object o = Get(key, "/analytics/realtime", new Dictionary<string, string> { { "fields", "visitors" } });
		double? num = J.Num(J.Get(J.Arr(J.Get(o, "data")).FirstOrDefault(), "visitors"));
		if (!num.HasValue)
		{
			throw new AnalyticsException(AnalyticsErrorKind.BadResponse, "Unexpected realtime response");
		}
		return (int)num.Value;
	}

	public TestResult Test(AnalyticsSite site, string key)
	{
		string text = CheckKey(key);
		if (text != null)
		{
			throw new AnalyticsException(AnalyticsErrorKind.WrongKey, text);
		}
		TestResult testResult = new TestResult();
		testResult.Ok = true;
		TestResult testResult2 = testResult;
		testResult2.Online = Realtime(site, key);
		string value = SiteClock.Day(SiteClock.Now(site.TimeZone).DateTime);
		object o = Get(key, "/analytics/timeseries", new Dictionary<string, string>
		{
			{ "fields", "visitors" },
			{ "interval", "day" },
			{ "startAt", value },
			{ "endAt", value }
		});
		testResult2.TimeZone = J.Str(J.Get(o, "timezone"));
		object o2 = Get(key, "/analytics/pages", new Dictionary<string, string> { { "limit", "5" } });
		List<string> list = (from p in J.Arr(J.Get(o2, "data"))
			select Norm(J.Str(J.Get(p, "hostname"))) into h
			where !string.IsNullOrEmpty(h)
			select h).Distinct().ToList();
		if (list.Count > 0 && !string.IsNullOrWhiteSpace(site.SiteId) && !list.Contains(Norm(site.SiteId)))
		{
			throw new AnalyticsException(AnalyticsErrorKind.WrongSite, "This key belongs to " + list[0] + ", not " + site.SiteId);
		}
		testResult2.Message = "Connected · " + testResult2.Online.Value + " online now";
		return testResult2;
	}

	private static string Norm(string host)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return null;
		}
		host = host.Trim().ToLowerInvariant();
		if (host.StartsWith("https://"))
		{
			host = host.Substring(8);
		}
		if (host.StartsWith("http://"))
		{
			host = host.Substring(7);
		}
		host = host.TrimEnd('/');
		if (!host.StartsWith("www."))
		{
			return host;
		}
		return host.Substring(4);
	}

	public static void Plan(AnalyticsRange range, DateTime today, out DateTime start, out DateTime end, out DateTime prevStart, out DateTime prevEnd)
	{
		end = today;
		int num = range switch
		{
			AnalyticsRange.Week => 7, 
			AnalyticsRange.Today => 1, 
			_ => 30, 
		};
		start = today.AddDays(-(num - 1));
		prevEnd = start.AddDays(-1.0);
		prevStart = prevEnd.AddDays(-(num - 1));
	}

	public AnalyticsSnapshot Fetch(AnalyticsSite site, string key, AnalyticsRange range)
	{
		AnalyticsSnapshot analyticsSnapshot = new AnalyticsSnapshot();
		analyticsSnapshot.SiteKey = site.Key;
		analyticsSnapshot.Range = range;
		analyticsSnapshot.FetchedUtc = DateTime.UtcNow;
		AnalyticsSnapshot analyticsSnapshot2 = analyticsSnapshot;
		DateTimeOffset now = SiteClock.Now(site.TimeZone);
		Plan(range, now.DateTime.Date, out var start, out var end, out var prevStart, out var prevEnd);
		analyticsSnapshot2.RangeStart = SiteClock.Day(start);
		analyticsSnapshot2.RangeEnd = SiteClock.Day(end);
		analyticsSnapshot2.PreviousStart = SiteClock.Day(prevStart);
		analyticsSnapshot2.PreviousEnd = SiteClock.Day(prevEnd);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary.Add("startAt", analyticsSnapshot2.RangeStart);
		dictionary.Add("endAt", analyticsSnapshot2.RangeEnd);
		Dictionary<string, string> dictionary2 = dictionary;
		bool flag = range == AnalyticsRange.Today;
		object obj = Get(key, "/analytics/timeseries", new Dictionary<string, string>(dictionary2)
		{
			{ "fields", "visitors" },
			{
				"interval",
				flag ? "hour" : "day"
			},
			{ "limit", "1000" }
		});
		string text = J.Str(J.Get(obj, "timezone"));
		if (!string.IsNullOrEmpty(text) && text != site.TimeZone)
		{
			site.TimeZone = text;
			now = SiteClock.Now(text);
			if (now.DateTime.Date != end)
			{
				return Fetch(site, key, range);
			}
		}
		analyticsSnapshot2.TimeZone = site.TimeZone;
		analyticsSnapshot2.Series = ParseSeries(obj, flag, start, end, now, SiteClock.Offset(site.TimeZone, SiteClock.UtcNow()) ?? now.Offset);
		object json = Get(key, "/analytics/overview", dictionary2);
		ParseOverview(json, analyticsSnapshot2);
		Dictionary<string, string> query;
		if (flag)
		{
			TimeSpan offset = now.Offset;
			DateTime dateTime = start.AddDays(-1.0);
			Dictionary<string, string> dictionary3 = new Dictionary<string, string>();
			dictionary3.Add("startAt", SiteClock.Iso(dateTime, offset));
			dictionary3.Add("endAt", SiteClock.Iso(dateTime + now.DateTime.TimeOfDay, offset));
			query = dictionary3;
		}
		else
		{
			Dictionary<string, string> dictionary4 = new Dictionary<string, string>();
			dictionary4.Add("startAt", analyticsSnapshot2.PreviousStart);
			dictionary4.Add("endAt", analyticsSnapshot2.PreviousEnd);
			query = dictionary4;
		}
		AnalyticsSnapshot analyticsSnapshot3 = new AnalyticsSnapshot();
		ParseOverview(Get(key, "/analytics/overview", query), analyticsSnapshot3);
		analyticsSnapshot2.PreviousVisitors = analyticsSnapshot3.Visitors ?? 0;
		if (!((analyticsSnapshot2.Revenue ?? 0.0) > 0.0) && !((analyticsSnapshot3.Revenue ?? 0.0) > 0.0))
		{
			analyticsSnapshot2.Revenue = null;
		}
		Dictionary<string, string> dictionary5 = new Dictionary<string, string>(dictionary2);
		dictionary5.Add("limit", "7");
		Dictionary<string, string> query2 = dictionary5;
		analyticsSnapshot2.Pages = ParseRows(Get(key, "/analytics/pages", query2), "path");
		analyticsSnapshot2.Sources = ParseRows(Get(key, "/analytics/referrers", query2), "referrer");
		analyticsSnapshot2.Countries = ParseRows(Get(key, "/analytics/countries", query2), "country");
		return analyticsSnapshot2;
	}

	public static void ParseOverview(object json, AnalyticsSnapshot snap)
	{
		object o = J.Arr(J.Get(json, "data")).FirstOrDefault() ?? J.Get(json, "data");
		double? num = J.Num(J.Get(o, "visitors"));
		snap.Visitors = (num.HasValue ? ((long)num.Value) : 0);
		double? num2 = J.Num(J.Get(o, "pageviews"));
		snap.Pageviews = (num2.HasValue ? new long?((long)num2.Value) : null);
		snap.BounceRate = J.Num(J.Get(o, "bounce_rate"));
		snap.AvgDurationSeconds = J.Num(J.Get(o, "avg_session_duration"));
		snap.Revenue = J.Num(J.Get(o, "revenue"));
		snap.Currency = J.Str(J.Get(o, "currency"));
	}

	public static List<SeriesPoint> ParseSeries(object json, bool hourly, DateTime start, DateTime end, DateTimeOffset now, TimeSpan offset)
	{
		List<KeyValuePair<DateTimeOffset, long>> list = new List<KeyValuePair<DateTimeOffset, long>>();
		object[] array = J.Arr(J.Get(json, "data"));
		foreach (object o in array)
		{
			if (DateTimeOffset.TryParse(J.Str(J.Get(o, "timestamp")), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
			{
				double? num = J.Num(J.Get(o, "visitors"));
				list.Add(new KeyValuePair<DateTimeOffset, long>(result, num.HasValue ? ((long)num.Value) : 0));
			}
		}
		bool flag = list.All((KeyValuePair<DateTimeOffset, long> p) => p.Key.DateTime.Date >= start && p.Key.DateTime.Date <= end);
		Dictionary<DateTime, long> dictionary = new Dictionary<DateTime, long>();
		foreach (KeyValuePair<DateTimeOffset, long> item in list)
		{
			DateTime dateTime = (flag ? item.Key.DateTime : (item.Key.UtcDateTime + offset));
			DateTime key = (hourly ? new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0) : dateTime.Date);
			dictionary.TryGetValue(key, out var value);
			dictionary[key] = value + item.Value;
		}
		DateTime last = (hourly ? new DateTime(end.Year, end.Month, end.Day, Math.Min(23, now.DateTime.Hour), 0, 0) : end);
		if (hourly && now.DateTime.Date > end)
		{
			last = end.AddHours(23.0);
		}
		return AnalyticsProviders.Fill(dictionary, start, last, hourly);
	}

	public static List<RankedRow> ParseRows(object json, string labelField)
	{
		List<RankedRow> list = new List<RankedRow>();
		object[] array = J.Arr(J.Get(json, "data"));
		foreach (object o in array)
		{
			string text = J.Str(J.Get(o, labelField));
			double? num = J.Num(J.Get(o, "visitors"));
			if (!string.IsNullOrEmpty(text) && num.HasValue)
			{
				list.Add(new RankedRow
				{
					Label = text,
					Visitors = (long)num.Value
				});
			}
		}
		return list.OrderByDescending((RankedRow r) => r.Visitors).Take(7).ToList();
	}
}
