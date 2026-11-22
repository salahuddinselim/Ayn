using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WindowsBuddy.Tools.Analytics;

namespace WindowsBuddy.Tools.Revenue;

public sealed class StripeProvider : IRevenueProvider
{
	public const string Api = "https://api.stripe.com/v1";

	public const string KeysUrl = "https://dashboard.stripe.com/apikeys";

	public const int MaxPages = 20;

	public string Id => "stripe";

	public string Name => "Stripe";

	public string CheckKey(string key)
	{
		key = (key ?? "").Trim();
		if (key.Length == 0)
		{
			return "Paste your restricted key";
		}
		if (key.StartsWith("sk_live_", StringComparison.Ordinal) || key.StartsWith("sk_test_", StringComparison.Ordinal))
		{
			return "That's a secret key with full access to your Stripe account. Create a restricted key instead (it starts with rk_) — step 2 above.";
		}
		if (key.StartsWith("pk_", StringComparison.Ordinal))
		{
			return "That's a publishable key. Ayn needs a restricted key (it starts with rk_).";
		}
		if (key.StartsWith("whsec_", StringComparison.Ordinal))
		{
			return "That's a webhook signing secret. Ayn needs a restricted key (it starts with rk_).";
		}
		if (key.StartsWith("dft_", StringComparison.Ordinal) || key.StartsWith("df_", StringComparison.Ordinal))
		{
			return "That looks like a DataFast key. Paste the Stripe restricted key (it starts with rk_).";
		}
		if (key.IndexOfAny(new char[4] { ' ', '\t', '\r', '\n' }) >= 0)
		{
			return "Paste only the key — it has no spaces.";
		}
		if (!key.StartsWith("rk_live_", StringComparison.Ordinal) && !key.StartsWith("rk_test_", StringComparison.Ordinal))
		{
			return "Stripe restricted keys start with rk_live_ (or rk_test_ in test mode).";
		}
		return null;
	}

	private static object Get(string key, string pathAndQuery)
	{
		HttpCall httpCall = new HttpCall();
		httpCall.Method = "GET";
		httpCall.Url = "https://api.stripe.com/v1" + pathAndQuery;
		HttpCall httpCall2 = httpCall;
		httpCall2.Headers["Authorization"] = "Bearer " + key;
		HttpReply httpReply = RevenueHttp.Send(httpCall2);
		object obj = J.Parse(httpReply.Body);
		if (httpReply.Status >= 200 && httpReply.Status < 300)
		{
			if (obj == null)
			{
				throw new RevenueException(RevenueErrorKind.BadResponse, "Stripe sent something Ayn didn't understand", "Try Refresh in a minute.");
			}
			return obj;
		}
		throw MapError(httpReply.Status, obj, RevenueHttp.RetryAfter(httpReply));
	}

	public static RevenueException MapError(int status, object json, TimeSpan? retry)
	{
		string text = J.Str(J.Get(json, "error", "message")) ?? "";
		string text2 = J.Str(J.Get(json, "error", "code")) ?? "";
		if (status == 401)
		{
			if (text.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0 || text2 == "api_key_expired")
			{
				return new RevenueException(RevenueErrorKind.Revoked, "This key has expired or was deleted in Stripe", "Create a new restricted key in Stripe and paste it here.");
			}
			return new RevenueException(RevenueErrorKind.WrongKey, "Stripe doesn't recognise this key", "Copy the whole restricted key again (it starts with rk_) and paste it here.");
		}
		if (status == 403 || (status == 400 && text.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			return new RevenueException(RevenueErrorKind.NoPermission, "This key can't read your balance", "In Stripe, edit the key and set Balance to Read, then try again.");
		}
		if (status == 429)
		{
			return new RevenueException(RevenueErrorKind.RateLimited, "Stripe asked Ayn to slow down", "Ayn will try again in a minute.", retry ?? TimeSpan.FromSeconds(60.0));
		}
		if (status >= 500)
		{
			return new RevenueException(RevenueErrorKind.Server, "Stripe is having trouble right now", "Your last numbers stay on screen. Try Refresh in a few minutes.", TimeSpan.FromMinutes(2.0));
		}
		return new RevenueException(RevenueErrorKind.BadResponse, "Stripe couldn't answer (" + status + ")", "Try Refresh in a minute.");
	}

	public RevenueTestResult Test(string key)
	{
		string text = CheckKey(key);
		if (text != null)
		{
			throw new RevenueException(RevenueErrorKind.WrongKeyType, text, null);
		}
		object o = Get(key, "/balance");
		Get(key, "/balance_transactions?limit=1");
		bool flag = ((J.Get(o, "livemode") is bool) ? ((bool)J.Get(o, "livemode")) : key.StartsWith("rk_live_", StringComparison.Ordinal));
		RevenueTestResult revenueTestResult = new RevenueTestResult();
		revenueTestResult.TestMode = !flag;
		revenueTestResult.AccountLabel = (flag ? "Live" : "Test mode");
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
		object balance = Get(key, "/balance");
		long num = ToUnix(start);
		long num2 = ToUnix(end.AddDays(1.0));
		List<object> list = new List<object>();
		string text = null;
		int num3 = 0;
		while (true)
		{
			if (num3 >= 20)
			{
				revenueSnapshot2.Partial = true;
				break;
			}
			string pathAndQuery = "/balance_transactions?limit=100&created[gte]=" + num + "&created[lt]=" + num2 + ((text != null) ? ("&starting_after=" + Uri.EscapeDataString(text)) : "");
			object o = Get(key, pathAndQuery);
			object[] array = J.Arr(J.Get(o, "data"));
			list.AddRange(array);
			if (!(J.Get(o, "has_more") is bool) || !(bool)J.Get(o, "has_more") || array.Length == 0)
			{
				break;
			}
			text = J.Str(J.Get(array[array.Length - 1], "id"));
			if (string.IsNullOrEmpty(text))
			{
				break;
			}
			num3++;
		}
		Aggregate(list, balance, revenueSnapshot2, start, end, range == RevenueRange.Today, nowLocal);
		return revenueSnapshot2;
	}

	private static long ToUnix(DateTime local)
	{
		DateTime dateTime = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
		return (long)(dateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
	}

	public static void Aggregate(IEnumerable<object> rows, object balance, RevenueSnapshot snap, DateTime start, DateTime end, bool hourly, DateTime nowLocal)
	{
		Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, long> dictionary2 = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, long> dictionary3 = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, int> dictionary4 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, Dictionary<DateTime, long>> dictionary5 = new Dictionary<string, Dictionary<DateTime, long>>(StringComparer.OrdinalIgnoreCase);
		foreach (object row in rows)
		{
			string text = J.Str(J.Get(row, "type")) ?? "";
			string text2 = (J.Str(J.Get(row, "currency")) ?? "").ToUpperInvariant();
			if (text2.Length != 3)
			{
				continue;
			}
			long num = (long)(J.Num(J.Get(row, "amount")) ?? 0.0);
			long v = (long)(J.Num(J.Get(row, "net")) ?? 0.0);
			bool flag = text == "charge" || text == "payment";
			bool flag2 = text == "refund" || text == "payment_refund";
			if (!flag && !flag2)
			{
				continue;
			}
			Add(dictionary2, text2, v);
			if (flag)
			{
				Add(dictionary, text2, num);
				dictionary4.TryGetValue(text2, out var value);
				dictionary4[text2] = value + 1;
				double? num2 = J.Num(J.Get(row, "created"));
				if (num2.HasValue)
				{
					DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(num2.Value).ToLocalTime();
					DateTime key = (hourly ? new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0) : dateTime.Date);
					if (!dictionary5.TryGetValue(text2, out var value2))
					{
						value2 = (dictionary5[text2] = new Dictionary<DateTime, long>());
					}
					value2.TryGetValue(key, out var value3);
					value2[key] = value3 + num;
				}
			}
			else
			{
				Add(dictionary3, text2, -num);
			}
		}
		string primary = (from kv in dictionary
			orderby kv.Value descending
			select kv.Key).FirstOrDefault();
		object[] array = J.Arr(J.Get(balance, "available"));
		object[] entries = J.Arr(J.Get(balance, "pending"));
		if (primary == null)
		{
			primary = array.Select((object a) => (J.Str(J.Get(a, "currency")) ?? "").ToUpperInvariant()).FirstOrDefault((string c) => c.Length == 3);
		}
		if (primary == null)
		{
			primary = "USD";
		}
		snap.Currency = primary;
		snap.Gross = (dictionary.TryGetValue(primary, out var value4) ? value4 : 0);
		snap.Net = (dictionary2.TryGetValue(primary, out var value5) ? value5 : 0);
		snap.Refunds = (dictionary3.TryGetValue(primary, out var value6) ? value6 : 0);
		snap.Sales = (dictionary4.TryGetValue(primary, out var value7) ? value7 : 0);
		snap.OtherCurrencies = (from c in (from c in dictionary.Keys.Concat(dictionary3.Keys)
				where !string.Equals(c, primary, StringComparison.OrdinalIgnoreCase)
				select c).Distinct()
			orderby c
			select c).ToList();
		snap.Available = BalanceIn(array, primary);
		snap.Pending = BalanceIn(entries, primary);
		snap.Series = RevenueMath.Series(dictionary5.TryGetValue(primary, out var value8) ? value8 : new Dictionary<DateTime, long>(), start, end, hourly, nowLocal);
	}

	private static long? BalanceIn(object[] entries, string currency)
	{
		long num = 0L;
		bool flag = false;
		foreach (object o in entries)
		{
			if (string.Equals(J.Str(J.Get(o, "currency")), currency, StringComparison.OrdinalIgnoreCase))
			{
				double? num2 = J.Num(J.Get(o, "amount"));
				if (num2.HasValue)
				{
					num += (long)num2.Value;
					flag = true;
				}
			}
		}
		if (!flag)
		{
			if (entries.Length <= 0)
			{
				return null;
			}
			return 0L;
		}
		return num;
	}

	private static void Add(Dictionary<string, long> d, string k, long v)
	{
		d.TryGetValue(k, out var value);
		d[k] = value + v;
	}
}
