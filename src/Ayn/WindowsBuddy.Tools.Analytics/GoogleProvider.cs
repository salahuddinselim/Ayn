using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WindowsBuddy.Tools.Analytics;

public sealed class GoogleProvider : IAnalyticsProvider
{
	public const string DataApi = "https://analyticsdata.googleapis.com/v1beta/properties/";

	public string Id => "ga4";

	public string Name => "Google Analytics";

	public int RealtimeWindowMinutes => 30;

	public string DocsUrl => "https://support.google.com/analytics/answer/9304153";

	public static string PropertyId(string s)
	{
		s = (s ?? "").Trim();
		if (s.StartsWith("properties/", StringComparison.OrdinalIgnoreCase))
		{
			s = s.Substring(11);
		}
		return s;
	}

	private object Post(AnalyticsSite site, string secret, string method, object body)
	{
		string text = GoogleAuth.AccessToken(site, secret);
		HttpCall httpCall = new HttpCall();
		httpCall.Method = "POST";
		httpCall.Url = "https://analyticsdata.googleapis.com/v1beta/properties/" + Uri.EscapeDataString(PropertyId(site.SiteId)) + ":" + method;
		httpCall.Body = J.Write(body);
		httpCall.ContentType = "application/json";
		HttpCall httpCall2 = httpCall;
		httpCall2.Headers["Authorization"] = "Bearer " + text;
		HttpReply httpReply = AnalyticsHttp.Send(httpCall2);
		object obj = J.Parse(httpReply.Body);
		if (httpReply.Status >= 200 && httpReply.Status < 300)
		{
			return obj;
		}
		if (httpReply.Status == 401)
		{
			GoogleAuth.Forget(site);
		}
		throw MapError(httpReply.Status, obj, site);
	}

	internal static AnalyticsException MapError(int status, object json, AnalyticsSite site)
	{
		string text = J.Str(J.Get(json, "error", "message")) ?? "";
		string text2 = J.Str(J.Get(json, "error", "status")) ?? "";
		switch (status)
		{
		case 401:
			return new AnalyticsException(AnalyticsErrorKind.WrongKey, "Google sign-in expired — sign in again");
		case 403:
			if (text.IndexOf("has not been used", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return new AnalyticsException(AnalyticsErrorKind.NoAccess, "Enable the “Google Analytics Data API” in your Google Cloud project");
			}
			break;
		}
		if (status == 403 || text2 == "PERMISSION_DENIED")
		{
			return new AnalyticsException(AnalyticsErrorKind.NoAccess, (site.GoogleAuth == "service_account" && !string.IsNullOrEmpty(site.ServiceAccountEmail)) ? ("No access — add " + site.ServiceAccountEmail + " as a Viewer of property " + PropertyId(site.SiteId)) : ("Your Google account has no access to property " + PropertyId(site.SiteId)));
		}
		if (status == 404 || (status == 400 && text.IndexOf("propert", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			return new AnalyticsException(AnalyticsErrorKind.WrongSite, "No GA4 property " + PropertyId(site.SiteId) + " — use the numeric Property ID (Admin → Property details)");
		}
		if (status == 429)
		{
			return new AnalyticsException(AnalyticsErrorKind.RateLimited, "Google Analytics quota reached — trying again later", TimeSpan.FromMinutes(5.0));
		}
		if (status >= 500)
		{
			return new AnalyticsException(AnalyticsErrorKind.Server, "Google Analytics is having trouble (" + status + ")");
		}
		return new AnalyticsException(AnalyticsErrorKind.BadResponse, string.IsNullOrEmpty(text) ? ("Google rejected the request (" + status + ")") : text);
	}

	public int Realtime(AnalyticsSite site, string secret)
	{
		object o = Post(site, secret, "runRealtimeReport", new Dictionary<string, object> { 
		{
			"metrics",
			new Dictionary<string, object>[1] { M("activeUsers") }
		} });
		object o2 = J.Arr(J.Get(o, "rows")).FirstOrDefault();
		double? num = J.Num(J.Get(J.Arr(J.Get(o2, "metricValues")).FirstOrDefault(), "value"));
		if (!num.HasValue)
		{
			return 0;
		}
		return (int)num.Value;
	}

	public TestResult Test(AnalyticsSite site, string secret)
	{
		object o = Post(site, secret, "runReport", new Dictionary<string, object>
		{
			{
				"dateRanges",
				new Dictionary<string, object>[1] { DR("today", "today", null) }
			},
			{
				"metrics",
				new Dictionary<string, object>[1] { M("activeUsers") }
			}
		});
		TestResult testResult = new TestResult();
		testResult.Ok = true;
		testResult.TimeZone = J.Str(J.Get(o, "metadata", "timeZone"));
		TestResult testResult2 = testResult;
		try
		{
			testResult2.Online = Realtime(site, secret);
		}
		catch
		{
		}
		testResult2.Message = (testResult2.Online.HasValue ? ("Connected · " + testResult2.Online.Value + " online now") : "Connected");
		return testResult2;
	}

	private static Dictionary<string, object> M(string name)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("name", name);
		return dictionary;
	}

	private static Dictionary<string, object> D(string name)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("name", name);
		return dictionary;
	}

	private static Dictionary<string, object> DR(string s, string e, string name)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("startDate", s);
		dictionary.Add("endDate", e);
		Dictionary<string, object> dictionary2 = dictionary;
		if (name != null)
		{
			dictionary2["name"] = name;
		}
		return dictionary2;
	}

	public static void Plan(AnalyticsRange range, out string cs, out string ce, out string ps, out string pe)
	{
		int num;
		switch (range)
		{
		case AnalyticsRange.Today:
			cs = (ce = "today");
			ps = (pe = "yesterday");
			return;
		default:
			num = 30;
			break;
		case AnalyticsRange.Week:
			num = 7;
			break;
		}
		int num2 = num;
		cs = num2 + "daysAgo";
		ce = "yesterday";
		ps = 2 * num2 + "daysAgo";
		pe = num2 + 1 + "daysAgo";
	}

	public AnalyticsSnapshot Fetch(AnalyticsSite site, string secret, AnalyticsRange range)
	{
		AnalyticsSnapshot analyticsSnapshot = new AnalyticsSnapshot();
		analyticsSnapshot.SiteKey = site.Key;
		analyticsSnapshot.Range = range;
		analyticsSnapshot.FetchedUtc = DateTime.UtcNow;
		AnalyticsSnapshot analyticsSnapshot2 = analyticsSnapshot;
		Plan(range, out var cs, out var ce, out var ps, out var pe);
		if (string.IsNullOrEmpty(site.TimeZone))
		{
			site.TimeZone = Test(site, secret).TimeZone;
		}
		DateTimeOffset dateTimeOffset = SiteClock.Now(site.TimeZone);
		DateTime date = dateTimeOffset.DateTime.Date;
		int num = range switch
		{
			AnalyticsRange.Week => 7, 
			AnalyticsRange.Today => 1, 
			_ => 30, 
		};
		DateTime dateTime = ((range == AnalyticsRange.Today) ? date : date.AddDays(-num));
		DateTime dateTime2 = ((range == AnalyticsRange.Today) ? date : date.AddDays(-1.0));
		analyticsSnapshot2.RangeStart = SiteClock.Day(dateTime);
		analyticsSnapshot2.RangeEnd = SiteClock.Day(dateTime2);
		analyticsSnapshot2.PreviousStart = SiteClock.Day(dateTime.AddDays(-num));
		analyticsSnapshot2.PreviousEnd = SiteClock.Day(dateTime.AddDays(-1.0));
		analyticsSnapshot2.TimeZone = site.TimeZone;
		Dictionary<string, object>[] current = new Dictionary<string, object>[1] { DR(cs, ce, "current") };
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("dateRanges", new Dictionary<string, object>[2]
		{
			DR(cs, ce, "current"),
			DR(ps, pe, "previous")
		});
		dictionary.Add("metrics", new Dictionary<string, object>[5]
		{
			M("activeUsers"),
			M("screenPageViews"),
			M("bounceRate"),
			M("averageSessionDuration"),
			M("totalRevenue")
		});
		Dictionary<string, object> dictionary2 = dictionary;
		if (range == AnalyticsRange.Today)
		{
			string[] value = (from h in Enumerable.Range(0, dateTimeOffset.DateTime.Hour + 1)
				select h.ToString("00")).ToArray();
			dictionary2["dimensionFilter"] = new Dictionary<string, object> { 
			{
				"filter",
				new Dictionary<string, object>
				{
					{ "fieldName", "hour" },
					{
						"inListFilter",
						new Dictionary<string, object> { { "values", value } }
					}
				}
			} };
		}
		bool flag = range == AnalyticsRange.Today;
		Dictionary<string, object> dictionary3 = new Dictionary<string, object>();
		dictionary3.Add("dateRanges", current);
		dictionary3.Add("dimensions", new Dictionary<string, object>[1] { D(flag ? "dateHour" : "date") });
		dictionary3.Add("metrics", new Dictionary<string, object>[1] { M("activeUsers") });
		dictionary3.Add("limit", 1000);
		Dictionary<string, object> dictionary4 = dictionary3;
		Func<string, Dictionary<string, object>> func = (string dim) => new Dictionary<string, object>
		{
			{ "dateRanges", current },
			{
				"dimensions",
				new Dictionary<string, object>[1] { D(dim) }
			},
			{
				"metrics",
				new Dictionary<string, object>[1] { M("activeUsers") }
			},
			{
				"orderBys",
				new Dictionary<string, object>[1]
				{
					new Dictionary<string, object>
					{
						{
							"metric",
							new Dictionary<string, object> { { "metricName", "activeUsers" } }
						},
						{ "desc", true }
					}
				}
			},
			{ "limit", 7 }
		};
		object o = Post(site, secret, "batchRunReports", new Dictionary<string, object> { 
		{
			"requests",
			new object[5]
			{
				dictionary2,
				dictionary4,
				func("pagePath"),
				func("sessionSource"),
				func("country")
			}
		} });
		object[] array = J.Arr(J.Get(o, "reports"));
		if (array.Length < 5)
		{
			throw new AnalyticsException(AnalyticsErrorKind.BadResponse, "Unexpected Google Analytics response");
		}
		ParseTotals(array[0], analyticsSnapshot2);
		string text = J.Str(J.Get(array[0], "metadata", "timeZone"));
		if (!string.IsNullOrEmpty(text))
		{
			site.TimeZone = text;
			analyticsSnapshot2.TimeZone = text;
		}
		analyticsSnapshot2.Series = ParseSeries(array[1], flag, dateTime, flag ? new DateTime(date.Year, date.Month, date.Day, dateTimeOffset.DateTime.Hour, 0, 0) : dateTime2);
		analyticsSnapshot2.Pages = ParseRows(array[2]);
		analyticsSnapshot2.Sources = ParseRows(array[3]);
		analyticsSnapshot2.Countries = ParseRows(array[4]);
		return analyticsSnapshot2;
	}

	public static void ParseTotals(object report, AnalyticsSnapshot snap)
	{
		List<string> list = (from h in J.Arr(J.Get(report, "metricHeaders"))
			select J.Str(J.Get(h, "name"))).ToList();
		Dictionary<string, double> dictionary = new Dictionary<string, double>();
		Dictionary<string, double> dictionary2 = new Dictionary<string, double>();
		object[] array = J.Arr(J.Get(report, "rows"));
		foreach (object o in array)
		{
			string text = J.Str(J.Get(J.Arr(J.Get(o, "dimensionValues")).FirstOrDefault(), "value")) ?? "current";
			Dictionary<string, double> dictionary3 = ((text == "previous" || text == "date_range_1") ? dictionary2 : dictionary);
			object[] array2 = J.Arr(J.Get(o, "metricValues"));
			for (int j = 0; j < list.Count && j < array2.Length; j++)
			{
				double? num = J.Num(J.Get(array2[j], "value"));
				if (num.HasValue)
				{
					dictionary3[list[j]] = num.Value;
				}
			}
		}
		double value;
		Func<Dictionary<string, double>, string, double> func = (Dictionary<string, double> d, string k) => (!d.TryGetValue(k, out value)) ? 0.0 : value;
		snap.Visitors = (long)func(dictionary, "activeUsers");
		snap.PreviousVisitors = (long)func(dictionary2, "activeUsers");
		snap.Pageviews = (long)func(dictionary, "screenPageViews");
		snap.BounceRate = (dictionary.ContainsKey("bounceRate") ? new double?(dictionary["bounceRate"] * 100.0) : null);
		snap.AvgDurationSeconds = (dictionary.ContainsKey("averageSessionDuration") ? new double?(dictionary["averageSessionDuration"]) : null);
		double num2 = func(dictionary, "totalRevenue");
		double num3 = func(dictionary2, "totalRevenue");
		snap.Revenue = ((num2 > 0.0 || num3 > 0.0) ? new double?(num2) : null);
		snap.Currency = J.Str(J.Get(report, "metadata", "currencyCode"));
		if (dictionary.Count == 0 && list.Count > 0)
		{
			snap.BounceRate = null;
			snap.AvgDurationSeconds = null;
		}
	}

	public static List<SeriesPoint> ParseSeries(object report, bool hourly, DateTime first, DateTime last)
	{
		Dictionary<DateTime, long> dictionary = new Dictionary<DateTime, long>();
		object[] array = J.Arr(J.Get(report, "rows"));
		foreach (object o in array)
		{
			string s = J.Str(J.Get(J.Arr(J.Get(o, "dimensionValues")).FirstOrDefault(), "value"));
			if (DateTime.TryParseExact(s, hourly ? "yyyyMMddHH" : "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				double? num = J.Num(J.Get(J.Arr(J.Get(o, "metricValues")).FirstOrDefault(), "value"));
				dictionary[result] = (num.HasValue ? ((long)num.Value) : 0);
			}
		}
		return AnalyticsProviders.Fill(dictionary, first, last, hourly);
	}

	public static List<RankedRow> ParseRows(object report)
	{
		List<RankedRow> list = new List<RankedRow>();
		object[] array = J.Arr(J.Get(report, "rows"));
		foreach (object o in array)
		{
			string text = J.Str(J.Get(J.Arr(J.Get(o, "dimensionValues")).FirstOrDefault(), "value"));
			double? num = J.Num(J.Get(J.Arr(J.Get(o, "metricValues")).FirstOrDefault(), "value"));
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
