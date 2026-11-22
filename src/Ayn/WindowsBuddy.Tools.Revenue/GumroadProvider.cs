using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WindowsBuddy.Tools.Analytics;

namespace WindowsBuddy.Tools.Revenue;

public sealed class GumroadProvider : IRevenueProvider
{
	public const string Api = "https://api.gumroad.com/v2";

	public const string AppsUrl = "https://app.gumroad.com/settings/advanced";

	public const int MaxPages = 100;

	public string Id => "gumroad";

	public string Name => "Gumroad";

	public string CheckKey(string key)
	{
		key = (key ?? "").Trim();
		if (key.Length == 0)
		{
			return "Paste your Gumroad access token";
		}
		if (key.StartsWith("rk_", StringComparison.Ordinal) || key.StartsWith("sk_", StringComparison.Ordinal) || key.StartsWith("pk_", StringComparison.Ordinal))
		{
			return "That's a Stripe key. Paste the Gumroad access token from step 2.";
		}
		if (key.StartsWith("df_", StringComparison.Ordinal) || key.StartsWith("dft_", StringComparison.Ordinal))
		{
			return "That looks like a DataFast key. Paste the Gumroad access token from step 2.";
		}
		if (key.IndexOfAny(new char[4] { ' ', '\t', '\r', '\n' }) >= 0)
		{
			return "Paste only the access token — it has no spaces.";
		}
		if (key.Length < 20)
		{
			return "That's too short for a Gumroad access token. Copy the whole token from Gumroad.";
		}
		return null;
	}

	private static object Get(string key, string pathAndQuery)
	{
		HttpCall httpCall = new HttpCall();
		httpCall.Method = "GET";
		httpCall.Url = "https://api.gumroad.com/v2" + pathAndQuery;
		HttpCall httpCall2 = httpCall;
		httpCall2.Headers["Authorization"] = "Bearer " + key;
		HttpReply httpReply = RevenueHttp.Send(httpCall2);
		object obj = J.Parse(httpReply.Body);
		bool flag = httpReply.Status >= 200 && httpReply.Status < 300 && (!(J.Get(obj, "success") is bool) || (bool)J.Get(obj, "success"));
		if (flag && obj != null)
		{
			return obj;
		}
		if (flag)
		{
			throw new RevenueException(RevenueErrorKind.BadResponse, "Gumroad sent something Ayn didn't understand", "Try Refresh in a minute.");
		}
		throw MapError(httpReply.Status, obj, RevenueHttp.RetryAfter(httpReply));
	}

	public static RevenueException MapError(int status, object json, TimeSpan? retry)
	{
		if (status == 401)
		{
			return new RevenueException(RevenueErrorKind.Revoked, "Gumroad doesn't accept this token", "It may be mistyped or revoked. Generate a new access token in Gumroad and paste it here.");
		}
		if (status == 403)
		{
			return new RevenueException(RevenueErrorKind.NoPermission, "This token can't read your sales", "Generate the token from your own Gumroad application (step 2) so it includes sales access.");
		}
		if (status == 429)
		{
			return new RevenueException(RevenueErrorKind.RateLimited, "Gumroad asked Ayn to slow down", "Ayn will try again in a minute.", retry ?? TimeSpan.FromSeconds(60.0));
		}
		if (status >= 500)
		{
			return new RevenueException(RevenueErrorKind.Server, "Gumroad is having trouble right now", "Your last numbers stay on screen. Try Refresh in a few minutes.", TimeSpan.FromMinutes(2.0));
		}
		string text = J.Str(J.Get(json, "message"));
		return new RevenueException(RevenueErrorKind.BadResponse, "Gumroad couldn't answer (" + status + ")", string.IsNullOrEmpty(text) ? "Try Refresh in a minute." : text);
	}

	public RevenueTestResult Test(string key)
	{
		string text = CheckKey(key);
		if (text != null)
		{
			throw new RevenueException(RevenueErrorKind.WrongKeyType, text, null);
		}
		object o = Get(key, "/user");
		string text2 = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		Get(key, "/sales?after=" + text2);
		string text3 = J.Str(J.Get(o, "user", "name"));
		RevenueTestResult revenueTestResult = new RevenueTestResult();
		revenueTestResult.AccountLabel = (string.IsNullOrWhiteSpace(text3) ? null : text3.Trim());
		return revenueTestResult;
	}

	public RevenueSnapshot Fetch(string key, RevenueRange range, DateTime nowLocal)
	{
		RevenueMath.Plan(range, nowLocal, out var start, out var end);
		RevenueSnapshot revenueSnapshot = new RevenueSnapshot();
		revenueSnapshot.Provider = Id;
		revenueSnapshot.Range = range;
		revenueSnapshot.FetchedUtc = DateTime.UtcNow;
		revenueSnapshot.RangeStart = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		revenueSnapshot.RangeEnd = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		RevenueSnapshot revenueSnapshot2 = revenueSnapshot;
		string text = "/sales?after=" + start.AddDays(-1.0).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&before=" + end.AddDays(2.0).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		List<object> list = new List<object>();
		string text2 = null;
		int num = 0;
		while (true)
		{
			if (num >= 100)
			{
				revenueSnapshot2.Partial = true;
				break;
			}
			object o = Get(key, text + ((text2 != null) ? ("&page_key=" + Uri.EscapeDataString(text2)) : ""));
			list.AddRange(J.Arr(J.Get(o, "sales")));
			text2 = J.Str(J.Get(o, "next_page_key"));
			if (string.IsNullOrEmpty(text2))
			{
				break;
			}
			num++;
		}
		Aggregate(list, revenueSnapshot2, start, end, range == RevenueRange.Today, nowLocal);
		return revenueSnapshot2;
	}

	public static void Aggregate(IEnumerable<object> rows, RevenueSnapshot snap, DateTime start, DateTime end, bool hourly, DateTime nowLocal)
	{
		Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, long> dictionary2 = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, long> dictionary3 = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, int> dictionary4 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, Dictionary<DateTime, long>> dictionary5 = new Dictionary<string, Dictionary<DateTime, long>>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet = new HashSet<string>();
		foreach (object row in rows)
		{
			string text = J.Str(J.Get(row, "id"));
			if ((text != null && !hashSet.Add(text)) || !DateTimeOffset.TryParse(J.Str(J.Get(row, "created_at")), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
			{
				continue;
			}
			DateTime dateTime = result.UtcDateTime.ToLocalTime();
			if (!(dateTime.Date < start) && !(dateTime.Date > end))
			{
				string text2 = (J.Str(J.Get(row, "currency")) ?? "usd").ToUpperInvariant();
				long num = (long)(J.Num(J.Get(row, "price")) ?? 0.0);
				long num2 = (long)(J.Num(J.Get(row, "gumroad_fee")) ?? 0.0);
				bool flag = J.Get(row, "refunded") is bool && (bool)J.Get(row, "refunded");
				Add(dictionary, text2, num);
				dictionary4.TryGetValue(text2, out var value);
				dictionary4[text2] = value + 1;
				if (flag)
				{
					Add(dictionary3, text2, num);
				}
				else
				{
					Add(dictionary2, text2, num - num2);
				}
				DateTime key = (hourly ? new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0) : dateTime.Date);
				if (!dictionary5.TryGetValue(text2, out var value2))
				{
					value2 = (dictionary5[text2] = new Dictionary<DateTime, long>());
				}
				value2.TryGetValue(key, out var value3);
				value2[key] = value3 + num;
			}
		}
		string primary = (from kv in dictionary
			orderby kv.Value descending
			select kv.Key).FirstOrDefault() ?? "USD";
		snap.Currency = primary;
		snap.Gross = (dictionary.TryGetValue(primary, out var value4) ? value4 : 0);
		snap.Net = (dictionary2.TryGetValue(primary, out var value5) ? value5 : 0);
		snap.Refunds = (dictionary3.TryGetValue(primary, out var value6) ? value6 : 0);
		snap.Sales = (dictionary4.TryGetValue(primary, out var value7) ? value7 : 0);
		snap.OtherCurrencies = (from k in dictionary.Keys
			where !string.Equals(k, primary, StringComparison.OrdinalIgnoreCase)
			orderby k
			select k).ToList();
		snap.Available = null;
		snap.Pending = null;
		snap.Series = RevenueMath.Series(dictionary5.TryGetValue(primary, out var value8) ? value8 : new Dictionary<DateTime, long>(), start, end, hourly, nowLocal);
	}

	private static void Add(Dictionary<string, long> d, string k, long v)
	{
		d.TryGetValue(k, out var value);
		d[k] = value + v;
	}
}
