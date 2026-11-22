using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WindowsBuddy.Tools.Analytics;

public sealed class PlausibleProvider : IAnalyticsProvider
{
	public string Id => "plausible";

	public string Name => "Plausible";

	public int RealtimeWindowMinutes => 5;

	public string DocsUrl => "https://plausible.io/docs/stats-api";

	private static string Base(AnalyticsSite s)
	{
		string text = (string.IsNullOrWhiteSpace(s.ServerUrl) ? "https://plausible.io" : s.ServerUrl.Trim().TrimEnd('/'));
		if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			text = "https://" + text;
		}
		return text;
	}

	public static string NamedRange(AnalyticsRange r)
	{
		return r switch
		{
			AnalyticsRange.Week => "7d", 
			AnalyticsRange.Today => "day", 
			_ => "30d", 
		};
	}

	private object Query(AnalyticsSite site, string key, Dictionary<string, object> q)
	{
		q["site_id"] = site.SiteId;
		HttpCall httpCall = new HttpCall();
		httpCall.Method = "POST";
		httpCall.Url = Base(site) + "/api/v2/query";
		httpCall.Body = J.Write(q);
		httpCall.ContentType = "application/json";
		HttpCall httpCall2 = httpCall;
		httpCall2.Headers["Authorization"] = "Bearer " + key;
		return Check(AnalyticsHttp.Send(httpCall2), site);
	}

	internal static object Check(HttpReply r, AnalyticsSite site)
	{
		object obj = J.Parse(r.Body);
		if (r.Status >= 200 && r.Status < 300)
		{
			return obj;
		}
		string text = J.Str(J.Get(obj, "error")) ?? "";
		switch (r.Status)
		{
		case 401:
			throw new AnalyticsException(AnalyticsErrorKind.WrongKey, "That API key isn't valid — create a Stats API key in Plausible");
		case 402:
			throw new AnalyticsException(AnalyticsErrorKind.PlanNotSupported, "Your Plausible plan doesn't include the Stats API");
		case 403:
			throw new AnalyticsException(AnalyticsErrorKind.NoAccess, "This key has no access to " + site.SiteId);
		case 404:
			throw new AnalyticsException(AnalyticsErrorKind.WrongSite, "No site “" + site.SiteId + "” — use the domain exactly as it appears in Plausible");
		default:
			if (r.Status == 400 && text.IndexOf("site", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				throw new AnalyticsException(AnalyticsErrorKind.WrongSite, "No site “" + site.SiteId + "” for this key — use the domain exactly as it appears in Plausible");
			}
			if (r.Status >= 500)
			{
				throw new AnalyticsException(AnalyticsErrorKind.Server, "Plausible is having trouble (" + r.Status + ")");
			}
			throw new AnalyticsException(AnalyticsErrorKind.BadResponse, string.IsNullOrEmpty(text) ? ("Plausible rejected the request (" + r.Status + ")") : text);
		}
	}

	public int Realtime(AnalyticsSite site, string key)
	{
		HttpCall httpCall = new HttpCall();
		httpCall.Method = "GET";
		httpCall.Url = Base(site) + "/api/v1/stats/realtime/visitors?site_id=" + Uri.EscapeDataString(site.SiteId ?? "");
		HttpCall httpCall2 = httpCall;
		httpCall2.Headers["Authorization"] = "Bearer " + key;
		HttpReply httpReply = AnalyticsHttp.Send(httpCall2);
		if (httpReply.Status < 200 || httpReply.Status >= 300)
		{
			Check(httpReply, site);
		}
		if (!int.TryParse((httpReply.Body ?? "").Trim(), out var result))
		{
			throw new AnalyticsException(AnalyticsErrorKind.BadResponse, "Unexpected realtime response");
		}
		return Math.Max(0, result);
	}

	public TestResult Test(AnalyticsSite site, string key)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("metrics", new string[1] { "visitors" });
		dictionary.Add("date_range", "day");
		Dictionary<string, object> q = dictionary;
		Query(site, key, q);
		TestResult testResult = new TestResult();
		testResult.Ok = true;
		TestResult testResult2 = testResult;
		try
		{
			testResult2.Online = Realtime(site, key);
		}
		catch
		{
		}
		testResult2.Message = (testResult2.Online.HasValue ? ("Connected · " + testResult2.Online.Value + " online now") : "Connected");
		return testResult2;
	}

	public AnalyticsSnapshot Fetch(AnalyticsSite site, string key, AnalyticsRange range)
	{
		AnalyticsSnapshot analyticsSnapshot = new AnalyticsSnapshot();
		analyticsSnapshot.SiteKey = site.Key;
		analyticsSnapshot.Range = range;
		analyticsSnapshot.FetchedUtc = DateTime.UtcNow;
		AnalyticsSnapshot analyticsSnapshot2 = analyticsSnapshot;
		string text = NamedRange(range);
		object json = Query(site, key, new Dictionary<string, object>
		{
			{
				"metrics",
				new string[4] { "visitors", "pageviews", "bounce_rate", "visit_duration" }
			},
			{ "date_range", text }
		});
		ParseTotals(json, analyticsSnapshot2);
		ResolvedRange(json, range, out var start, out var end, out var offset);
		analyticsSnapshot2.RangeStart = SiteClock.Day(start);
		analyticsSnapshot2.RangeEnd = SiteClock.Day(end);
		analyticsSnapshot2.TimeZone = "UTC" + SiteClock.Iso(DateTime.MinValue, offset).Substring(19);
		DateTime value = DateTime.SpecifyKind(SiteClock.UtcNow() + offset, DateTimeKind.Unspecified);
		object value2;
		if (range == AnalyticsRange.Today)
		{
			DateTime dateTime = start.AddDays(-1.0);
			value2 = new string[2]
			{
				SiteClock.Iso(dateTime, offset),
				SiteClock.Iso(dateTime + value.TimeOfDay, offset)
			};
			string previousStart = (analyticsSnapshot2.PreviousEnd = SiteClock.Day(dateTime));
			analyticsSnapshot2.PreviousStart = previousStart;
		}
		else
		{
			int num = (int)(end - start).TotalDays + 1;
			DateTime d = start.AddDays(-num);
			DateTime d2 = start.AddDays(-1.0);
			value2 = new string[2]
			{
				SiteClock.Day(d),
				SiteClock.Day(d2)
			};
			analyticsSnapshot2.PreviousStart = SiteClock.Day(d);
			analyticsSnapshot2.PreviousEnd = SiteClock.Day(d2);
		}
		object o = Query(site, key, new Dictionary<string, object>
		{
			{
				"metrics",
				new string[1] { "visitors" }
			},
			{ "date_range", value2 }
		});
		object o2 = J.Arr(J.Get(o, "results")).FirstOrDefault();
		double? num2 = J.Num(J.Arr(J.Get(o2, "metrics")).FirstOrDefault());
		analyticsSnapshot2.PreviousVisitors = (num2.HasValue ? ((long)num2.Value) : 0);
		bool flag = range == AnalyticsRange.Today;
		object json2 = Query(site, key, new Dictionary<string, object>
		{
			{
				"metrics",
				new string[1] { "visitors" }
			},
			{ "date_range", text },
			{
				"dimensions",
				new string[1] { flag ? "time:hour" : "time:day" }
			},
			{
				"include",
				new Dictionary<string, object> { { "time_labels", true } }
			}
		});
		analyticsSnapshot2.Series = ParseSeries(json2, flag, flag ? new DateTime?(value) : null);
		analyticsSnapshot2.Pages = Breakdown(site, key, text, "event:page");
		analyticsSnapshot2.Sources = Breakdown(site, key, text, "visit:source");
		analyticsSnapshot2.Countries = Breakdown(site, key, text, "visit:country_name");
		try
		{
			object json3 = Query(site, key, new Dictionary<string, object>
			{
				{
					"metrics",
					new string[1] { "total_revenue" }
				},
				{ "date_range", text },
				{
					"dimensions",
					new string[1] { "event:goal" }
				}
			});
			analyticsSnapshot2.Revenue = ParseRevenue(json3, out var currency);
			analyticsSnapshot2.Currency = currency;
		}
		catch (AnalyticsException ex)
		{
			if (ex.Kind == AnalyticsErrorKind.Offline || ex.Kind == AnalyticsErrorKind.RateLimited)
			{
				throw;
			}
			analyticsSnapshot2.Revenue = null;
		}
		return analyticsSnapshot2;
	}

	private List<RankedRow> Breakdown(AnalyticsSite site, string key, string named, string dimension)
	{
		object json = Query(site, key, new Dictionary<string, object>
		{
			{
				"metrics",
				new string[1] { "visitors" }
			},
			{ "date_range", named },
			{
				"dimensions",
				new string[1] { dimension }
			},
			{
				"order_by",
				new object[1] { new string[2] { "visitors", "desc" } }
			},
			{
				"pagination",
				new Dictionary<string, object>
				{
					{ "limit", 7 },
					{ "offset", 0 }
				}
			}
		});
		return ParseBreakdown(json);
	}

	public static void ParseTotals(object json, AnalyticsSnapshot snap)
	{
		object[] j = J.Arr(J.Get(J.Arr(J.Get(json, "results")).FirstOrDefault(), "metrics"));
		Func<int, double?> func = (int i) => (i >= j.Length) ? null : J.Num(j[i]);
		snap.Visitors = (func(0).HasValue ? new long?((long)func(0).Value) : new long?(0L));
		snap.Pageviews = (func(1).HasValue ? new long?((long)func(1).Value) : null);
		snap.BounceRate = func(2);
		snap.AvgDurationSeconds = func(3);
	}

	public static void ResolvedRange(object json, AnalyticsRange range, out DateTime start, out DateTime end, out TimeSpan offset)
	{
		object[] array = J.Arr(J.Get(json, "query", "date_range"));
		if (array.Length == 2 && DateTimeOffset.TryParse(J.Str(array[0]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) && DateTimeOffset.TryParse(J.Str(array[1]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
		{
			start = result.DateTime.Date;
			end = result2.DateTime.Date;
			offset = result.Offset;
		}
		else
		{
			DateTimeOffset dateTimeOffset = SiteClock.Now(null);
			offset = dateTimeOffset.Offset;
			end = dateTimeOffset.DateTime.Date;
			start = ((range == AnalyticsRange.Today) ? end : end.AddDays((range == AnalyticsRange.Week) ? (-6) : (-29)));
		}
	}

	public static List<SeriesPoint> ParseSeries(object json, bool hourly, DateTime? cutoff)
	{
		Dictionary<DateTime, long> dictionary = new Dictionary<DateTime, long>();
		object[] array = J.Arr(J.Get(json, "results"));
		foreach (object o in array)
		{
			if (TryBucket(J.Str(J.Arr(J.Get(o, "dimensions")).FirstOrDefault()), out var t))
			{
				double? num = J.Num(J.Arr(J.Get(o, "metrics")).FirstOrDefault());
				dictionary[t] = (num.HasValue ? ((long)num.Value) : 0);
			}
		}
		List<string> list = J.Arr(J.Get(json, "meta", "time_labels")).Select(J.Str).ToList();
		List<DateTime> list2 = new List<DateTime>();
		foreach (string item in list)
		{
			if (TryBucket(item, out var t2))
			{
				list2.Add(t2);
			}
		}
		if (list2.Count == 0)
		{
			list2 = dictionary.Keys.OrderBy((DateTime x) => x).ToList();
		}
		List<SeriesPoint> list3 = new List<SeriesPoint>();
		foreach (DateTime item2 in list2.OrderBy((DateTime x) => x))
		{
			if (!cutoff.HasValue || !(item2 > cutoff.Value))
			{
				dictionary.TryGetValue(item2, out var value);
				list3.Add(new SeriesPoint
				{
					Time = item2,
					Value = value
				});
				continue;
			}
			break;
		}
		return list3;
	}

	private static bool TryBucket(string s, out DateTime t)
	{
		return DateTime.TryParseExact(s, new string[3] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out t);
	}

	public static List<RankedRow> ParseBreakdown(object json)
	{
		List<RankedRow> list = new List<RankedRow>();
		object[] array = J.Arr(J.Get(json, "results"));
		foreach (object o in array)
		{
			string text = J.Str(J.Arr(J.Get(o, "dimensions")).FirstOrDefault());
			double? num = J.Num(J.Arr(J.Get(o, "metrics")).FirstOrDefault());
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

	public static double? ParseRevenue(object json, out string currency)
	{
		currency = null;
		double num = 0.0;
		bool flag = false;
		object[] array = J.Arr(J.Get(json, "results"));
		foreach (object o in array)
		{
			object obj = J.Arr(J.Get(o, "metrics")).FirstOrDefault();
			if (obj == null)
			{
				continue;
			}
			double? num2 = J.Num(J.Get(obj, "value")) ?? J.Num(obj);
			if (num2.HasValue)
			{
				string text = J.Str(J.Get(obj, "currency"));
				if (currency == null)
				{
					currency = text;
				}
				else if (text != null && text != currency)
				{
					continue;
				}
				num += num2.Value;
				flag = true;
			}
		}
		if (!flag)
		{
			return null;
		}
		return num;
	}
}
