using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Analytics;

public sealed class AnalyticsService
{
	public const string GoogleClientSecretKey = "googleoauthclient";

	private static AnalyticsService _instance;

	public static Func<string> LegacyTokenReader = () => CredentialStore.LoadToken("WindowsBuddy_Analytics_Token");

	public static Action LegacyTokenDelete = delegate
	{
		CredentialStore.DeleteToken("WindowsBuddy_Analytics_Token");
	};

	private readonly string _dir;

	private readonly string _settingsPath;

	private readonly string _cacheDir;

	private readonly object _lock = new object();

	private readonly Dictionary<string, int> _failures = new Dictionary<string, int>();

	private readonly Dictionary<string, DateTime> _retryAt = new Dictionary<string, DateTime>();

	private readonly HashSet<string> _inFlight = new HashSet<string>();

	private static volatile bool _networkPaused;

	public static AnalyticsService Instance => _instance ?? (_instance = new AnalyticsService());

	public AnalyticsSettings Settings { get; private set; }

	public List<AnalyticsSite> VisibleSites => Settings.Sites.Where((AnalyticsSite s) => AnalyticsFeatures.IsAvailable(s.Provider)).ToList();

	public AnalyticsSite ActiveSite
	{
		get
		{
			List<AnalyticsSite> visibleSites = VisibleSites;
			AnalyticsSite analyticsSite = visibleSites.FirstOrDefault((AnalyticsSite x) => x.Key == Settings.ActiveSiteKey);
			return analyticsSite ?? visibleSites.FirstOrDefault();
		}
	}

	public event Action SettingsChanged;

	public event Action CacheChanged;

	public AnalyticsService()
	{
		_dir = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "analytics");
		_settingsPath = Path.Combine(_dir, "settings.json");
		_cacheDir = Path.Combine(_dir, "cache");
		try
		{
			Directory.CreateDirectory(_cacheDir);
		}
		catch
		{
		}
		Settings = LoadSettings();
		MigrateClientSecret();
		GoogleAuth.ClientId = () => Settings.GoogleClientId;
		GoogleAuth.ClientSecret = () => SecretStore.Load("googleoauthclient");
	}

	private void MigrateClientSecret()
	{
		if (!string.IsNullOrEmpty(Settings.GoogleClientSecret))
		{
			if (!SecretStore.Save("googleoauthclient", Settings.GoogleClientSecret))
			{
				TransactionLogger.Warn("Analytics: the Google client secret could not be moved to the encrypted store; it was removed.");
			}
			Settings.GoogleClientSecret = null;
			Save();
		}
	}

	public void SetGoogleClientSecret(string secret)
	{
		if (string.IsNullOrEmpty(secret))
		{
			try
			{
				SecretStore.Delete("googleoauthclient");
				return;
			}
			catch
			{
				return;
			}
		}
		SecretStore.Save("googleoauthclient", secret);
	}

	private AnalyticsSettings LoadSettings()
	{
		try
		{
			if (File.Exists(_settingsPath))
			{
				AnalyticsSettings analyticsSettings = new JavaScriptSerializer().Deserialize<AnalyticsSettings>(PersistenceHelper.SafeReadAllText(_settingsPath));
				if (analyticsSettings != null && analyticsSettings.Sites != null)
				{
					return analyticsSettings;
				}
			}
			AnalyticsSettings analyticsSettings2 = MigrateV1();
			if (analyticsSettings2 != null)
			{
				return analyticsSettings2;
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Analytics: settings unreadable (" + ex.GetType().Name + ")");
		}
		return new AnalyticsSettings();
	}

	private AnalyticsSettings MigrateV1()
	{
		string text = Path.Combine(_dir, "config.json");
		if (!File.Exists(text))
		{
			return null;
		}
		AnalyticsSettings analyticsSettings = new AnalyticsSettings();
		bool flag = false;
		try
		{
			object o = J.Parse(PersistenceHelper.SafeReadAllText(text));
			bool flag2 = J.Get(o, "IsConfigured") is bool && (bool)J.Get(o, "IsConfigured");
			string text2 = J.Str(J.Get(o, "Provider"));
			string text3 = J.Str(J.Get(o, "SiteId"));
			string text4 = (flag2 ? LegacyTokenReader() : null);
			if (flag2 && string.IsNullOrEmpty(text4))
			{
				flag = true;
			}
			if (flag2 && !string.IsNullOrWhiteSpace(text3) && !string.IsNullOrEmpty(text4) && (text2 == "plausible" || (text2 == "datafast" && DataFastProvider.CheckKey(text4) == null)))
			{
				string text5 = J.Str(J.Get(o, "ServerUrl"));
				AnalyticsSite analyticsSite = new AnalyticsSite();
				analyticsSite.Provider = ((text2 == "plausible") ? "plausible" : "datafast");
				analyticsSite.SiteId = text3.Trim();
				analyticsSite.DisplayName = text3.Trim();
				analyticsSite.ServerUrl = ((text2 == "plausible" && text5 != null && text5.TrimEnd('/') != "https://plausible.io") ? text5 : null);
				AnalyticsSite analyticsSite2 = analyticsSite;
				if (SecretStore.Save(analyticsSite2.Key, text4))
				{
					analyticsSettings.Sites.Add(analyticsSite2);
					analyticsSettings.ActiveSiteKey = analyticsSite2.Key;
				}
				else
				{
					flag = true;
				}
			}
			if (!flag && !Save(analyticsSettings))
			{
				flag = true;
			}
			if (flag2 && !flag)
			{
				try
				{
					LegacyTokenDelete();
				}
				catch
				{
				}
			}
		}
		catch
		{
			flag = true;
		}
		try
		{
			string[] files = Directory.GetFiles(_dir, "cache_*.json");
			foreach (string path in files)
			{
				File.Delete(path);
			}
			if (!flag)
			{
				File.Delete(text);
			}
		}
		catch
		{
		}
		if (flag)
		{
			TransactionLogger.Warn("Analytics: the v1 credential was not migrated; it was kept and the migration will be retried.");
		}
		return analyticsSettings;
	}

	public void Save()
	{
		Save(Settings);
	}

	private bool Save(AnalyticsSettings s)
	{
		bool result;
		try
		{
			Directory.CreateDirectory(_dir);
			result = PersistenceHelper.SafeWriteAllText(_settingsPath, new JavaScriptSerializer().Serialize(s));
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_settingsPath, error);
			result = false;
		}
		Action settingsChanged = this.SettingsChanged;
		if (settingsChanged != null && s == Settings)
		{
			settingsChanged();
		}
		return result;
	}

	public void AddSite(AnalyticsSite site, string secret)
	{
		if (!SecretStore.Save(site.Key, secret))
		{
			throw new IOException("The site credential could not be saved on this PC.");
		}
		Settings.Sites.Add(site);
		Settings.ActiveSiteKey = site.Key;
		Save();
	}

	public void Disconnect(string siteKey)
	{
		AnalyticsSite analyticsSite = Settings.Sites.FirstOrDefault((AnalyticsSite s) => s.Key == siteKey);
		if (analyticsSite == null)
		{
			return;
		}
		SecretStore.Delete(analyticsSite.Key);
		GoogleAuth.Forget(analyticsSite);
		foreach (AnalyticsRange value in Enum.GetValues(typeof(AnalyticsRange)))
		{
			try
			{
				File.Delete(CachePath(analyticsSite.Key, value));
			}
			catch
			{
			}
		}
		Settings.Sites.Remove(analyticsSite);
		if (Settings.ActiveSiteKey == siteKey)
		{
			Settings.ActiveSiteKey = VisibleSites.Select((AnalyticsSite s) => s.Key).FirstOrDefault();
		}
		Save();
	}

	private string CachePath(string siteKey, AnalyticsRange r)
	{
		return Path.Combine(_cacheDir, string.Concat(siteKey, "_", r, ".json"));
	}

	public AnalyticsSnapshot Cached(string siteKey, AnalyticsRange r)
	{
		try
		{
			string text = CachePath(siteKey, r);
			if (!File.Exists(text))
			{
				return null;
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			javaScriptSerializer.MaxJsonLength = int.MaxValue;
			return javaScriptSerializer.Deserialize<AnalyticsSnapshot>(PersistenceHelper.SafeReadAllText(text));
		}
		catch
		{
			return null;
		}
	}

	private void StoreCache(AnalyticsSnapshot s)
	{
		try
		{
			PersistenceHelper.WriteCacheText(CachePath(s.SiteKey, s.Range), new JavaScriptSerializer
			{
				MaxJsonLength = int.MaxValue
			}.Serialize(s));
			this.CacheChanged?.Invoke();
		}
		catch
		{
		}
	}

	public DateTime RetryAt(string siteKey)
	{
		lock (_lock)
		{
			DateTime value;
			return _retryAt.TryGetValue(siteKey, out value) ? value : DateTime.MinValue;
		}
	}

	public static Action PauseNetwork()
	{
		_networkPaused = true;
		return delegate
		{
			_networkPaused = false;
		};
	}

	public bool Fetch(AnalyticsSite site, AnalyticsRange range, bool force, Dispatcher ui, Action<AnalyticsSnapshot, Exception> done)
	{
		if (site == null || _networkPaused)
		{
			return false;
		}
		string flight = site.Key + "|" + range;
		lock (_lock)
		{
			if (_inFlight.Contains(flight))
			{
				return false;
			}
			if (!force && RetryAt(site.Key) > DateTime.UtcNow)
			{
				return false;
			}
			_inFlight.Add(flight);
		}
		string tzBefore = site.TimeZone;
		Task.Factory.StartNew(delegate
		{
			AnalyticsSnapshot snap = null;
			Exception error = null;
			try
			{
				IAnalyticsProvider analyticsProvider = AnalyticsProviders.For(site.Provider);
				string text = SecretStore.Load(site.Key);
				if (analyticsProvider == null || string.IsNullOrEmpty(text))
				{
					throw new AnalyticsException(AnalyticsErrorKind.NotConfigured, "Connect this site again");
				}
				snap = analyticsProvider.Fetch(site, text, range);
				snap.FetchedUtc = DateTime.UtcNow;
				StoreCache(snap);
				lock (_lock)
				{
					_failures.Remove(site.Key);
					_retryAt.Remove(site.Key);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = (error = ex);
				AnalyticsException ex3 = ex2 as AnalyticsException;
				lock (_lock)
				{
					_failures.TryGetValue(site.Key, out var value);
					value = (_failures[site.Key] = value + 1);
					TimeSpan timeSpan = ((ex3 != null && ex3.RetryAfter.HasValue) ? ex3.RetryAfter.Value : TimeSpan.FromSeconds(Math.Min(900.0, 30.0 * Math.Pow(2.0, value - 1))));
					_retryAt[site.Key] = DateTime.UtcNow + timeSpan;
				}
				TransactionLogger.Warn("Analytics: " + site.Provider + " refresh failed (" + ((ex3 != null) ? ex3.Kind.ToString() : ex2.GetType().Name) + ")");
			}
			finally
			{
				lock (_lock)
				{
					_inFlight.Remove(flight);
				}
			}
			ui.BeginInvoke((Action)delegate
			{
				if (site.TimeZone != tzBefore)
				{
					Save();
				}
				done(snap, error);
			});
		});
		return true;
	}

	public void Realtime(AnalyticsSite site, Dispatcher ui, Action<int?> done)
	{
		if (site == null || _networkPaused)
		{
			done(null);
			return;
		}
		Task.Factory.StartNew(delegate
		{
			int? i = null;
			try
			{
				IAnalyticsProvider analyticsProvider = AnalyticsProviders.For(site.Provider);
				string text = SecretStore.Load(site.Key);
				if (analyticsProvider != null && !string.IsNullOrEmpty(text))
				{
					i = analyticsProvider.Realtime(site, text);
				}
			}
			catch
			{
				i = null;
			}
			ui.BeginInvoke((Action)delegate
			{
				done(i);
			});
		});
	}
}
