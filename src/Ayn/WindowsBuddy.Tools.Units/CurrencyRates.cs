using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Units;

internal static class CurrencyRates
{
	public class RatesCache
	{
		public long FetchedUtcTicks { get; set; }

		public Dictionary<string, decimal> Rates { get; set; }
	}

	private const string Endpoint = "https://open.er-api.com/v6/latest/USD";

	private static readonly TimeSpan MaxAge = TimeSpan.FromHours(12.0);

	private static readonly object _lock = new object();

	private static Dictionary<string, decimal> _rates;

	private static DateTime _fetchedUtc;

	private static bool _loaded;

	private static bool _fetching;

	private static string CachePath => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "units", "rates_usd.json");

	public static bool HasRates
	{
		get
		{
			LoadCache();
			lock (_lock)
			{
				return _rates != null && _rates.Count > 0;
			}
		}
	}

	public static bool IsFetching
	{
		get
		{
			lock (_lock)
			{
				return _fetching;
			}
		}
	}

	public static DateTime FetchedLocal
	{
		get
		{
			lock (_lock)
			{
				return _fetchedUtc.ToLocalTime();
			}
		}
	}

	public static IEnumerable<string> Codes
	{
		get
		{
			LoadCache();
			lock (_lock)
			{
				return (_rates == null) ? new List<string>() : _rates.Keys.OrderBy((string k) => k, StringComparer.Ordinal).ToList();
			}
		}
	}

	public static event Action Loaded;

	public static bool TryRate(string code, out decimal rate)
	{
		LoadCache();
		lock (_lock)
		{
			rate = 0m;
			return _rates != null && _rates.TryGetValue(code, out rate);
		}
	}

	public static void EnsureFresh()
	{
		LoadCache();
		lock (_lock)
		{
			if (_fetching || (_rates != null && DateTime.UtcNow - _fetchedUtc < MaxAge))
			{
				return;
			}
			_fetching = true;
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			bool flag = false;
			try
			{
				HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("https://open.er-api.com/v6/latest/USD");
				httpWebRequest.Timeout = 5000;
				httpWebRequest.ReadWriteTimeout = 5000;
				httpWebRequest.UserAgent = "Ayn";
				string input;
				using (WebResponse webResponse = httpWebRequest.GetResponse())
				{
					using StreamReader streamReader = new StreamReader(webResponse.GetResponseStream());
					input = streamReader.ReadToEnd();
				}
				Dictionary<string, object> dictionary = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(input);
				if (dictionary != null && dictionary.TryGetValue("result", out var value) && "success".Equals(value as string) && dictionary.TryGetValue("rates", out var value2) && value2 is Dictionary<string, object>)
				{
					Dictionary<string, decimal> dictionary2 = new Dictionary<string, decimal>(StringComparer.Ordinal);
					foreach (KeyValuePair<string, object> item in (Dictionary<string, object>)value2)
					{
						try
						{
							dictionary2[item.Key] = Convert.ToDecimal(item.Value, CultureInfo.InvariantCulture);
						}
						catch
						{
						}
					}
					if (dictionary2.Count > 0)
					{
						lock (_lock)
						{
							_rates = dictionary2;
							_fetchedUtc = DateTime.UtcNow;
						}
						SaveCache();
						flag = true;
					}
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[Units] Rates fetch failed: " + ex.Message);
			}
			finally
			{
				lock (_lock)
				{
					_fetching = false;
				}
			}
			Action loaded = CurrencyRates.Loaded;
			if (loaded != null)
			{
				try
				{
					loaded();
				}
				catch
				{
				}
			}
		});
	}

	private static void LoadCache()
	{
		lock (_lock)
		{
			if (_loaded)
			{
				return;
			}
			_loaded = true;
			try
			{
				string text = PersistenceHelper.SafeReadAllText(CachePath);
				if (!string.IsNullOrEmpty(text))
				{
					RatesCache ratesCache = new JavaScriptSerializer().Deserialize<RatesCache>(text);
					if (ratesCache != null && ratesCache.Rates != null && ratesCache.Rates.Count != 0)
					{
						_rates = new Dictionary<string, decimal>(ratesCache.Rates, StringComparer.Ordinal);
						_fetchedUtc = new DateTime(ratesCache.FetchedUtcTicks, DateTimeKind.Utc);
					}
				}
			}
			catch
			{
			}
		}
	}

	private static void SaveCache()
	{
		try
		{
			RatesCache obj;
			lock (_lock)
			{
				RatesCache ratesCache = new RatesCache();
				ratesCache.FetchedUtcTicks = _fetchedUtc.Ticks;
				ratesCache.Rates = new Dictionary<string, decimal>(_rates);
				obj = ratesCache;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
			PersistenceHelper.WriteCacheText(CachePath, new JavaScriptSerializer().Serialize(obj));
		}
		catch
		{
		}
	}
}
