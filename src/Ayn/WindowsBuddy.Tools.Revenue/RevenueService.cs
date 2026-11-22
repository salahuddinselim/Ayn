using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Pet;

namespace WindowsBuddy.Tools.Revenue;

public sealed class RevenueService
{
	private static RevenueService _instance;

	public static string CredTarget = "WindowsBuddy_Revenue_Token";

	public static Func<DateTime> Clock = () => DateTime.Now;

	public static readonly StripeProvider Stripe = new StripeProvider();

	public static readonly GumroadProvider Gumroad = new GumroadProvider();

	private readonly string _dir;

	private readonly string _configPath;

	private readonly string _cacheDir;

	private readonly object _lock = new object();

	private int _failures;

	private DateTime _retryAt = DateTime.MinValue;

	private bool _inFlight;

	private static volatile bool _networkPaused;

	public static RevenueService Instance => _instance ?? (_instance = new RevenueService());

	public RevenueConfigModel Config { get; private set; }

	public bool IsConnected => Config.IsConfigured;

	public IRevenueProvider Provider => For(Config.Provider);

	public DateTime RetryAt
	{
		get
		{
			lock (_lock)
			{
				return _retryAt;
			}
		}
	}

	public event Action Changed;

	public static IRevenueProvider For(string id)
	{
		if (!(id == "gumroad"))
		{
			return (id == "stripe") ? Stripe : null;
		}
		return Gumroad;
	}

	public RevenueService()
	{
		_dir = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "revenue");
		_configPath = Path.Combine(_dir, "config.json");
		_cacheDir = Path.Combine(_dir, "cache");
		Config = Load();
	}

	public static Action PauseNetwork()
	{
		_networkPaused = true;
		return delegate
		{
			_networkPaused = false;
		};
	}

	private RevenueConfigModel Load()
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_configPath);
			if (!string.IsNullOrEmpty(text))
			{
				RevenueConfigModel revenueConfigModel = new JavaScriptSerializer().Deserialize<RevenueConfigModel>(text);
				if (revenueConfigModel != null)
				{
					if (string.IsNullOrEmpty(revenueConfigModel.Provider) || For(revenueConfigModel.Provider) == null)
					{
						revenueConfigModel.Provider = "stripe";
					}
					return revenueConfigModel;
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Revenue: settings unreadable (" + ex.GetType().Name + ")");
		}
		return new RevenueConfigModel();
	}

	public bool Save()
	{
		this.Changed?.Invoke();
		try
		{
			Directory.CreateDirectory(_dir);
			return PersistenceHelper.SafeWriteAllText(_configPath, new JavaScriptSerializer().Serialize(Config));
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_configPath, error);
			return false;
		}
	}

	public RevenueTestResult Connect(string providerId, string key)
	{
		IRevenueProvider revenueProvider = For(providerId);
		if (revenueProvider == null)
		{
			throw new RevenueException(RevenueErrorKind.NotConfigured, "Choose Stripe or Gumroad", null);
		}
		key = (key ?? "").Trim();
		RevenueTestResult revenueTestResult = revenueProvider.Test(key);
		if (!CredentialStore.SaveToken(CredTarget, key))
		{
			throw new RevenueException(RevenueErrorKind.NotConfigured, "Ayn couldn't save the key on this PC", "Try again. If it keeps failing, restart Ayn.");
		}
		ClearCache();
		Config.Provider = providerId;
		Config.IsConfigured = true;
		Config.AccountLabel = revenueTestResult.AccountLabel;
		Config.TestMode = revenueTestResult.TestMode;
		Save();
		lock (_lock)
		{
			_failures = 0;
			_retryAt = DateTime.MinValue;
			return revenueTestResult;
		}
	}

	public bool Disconnect()
	{
		CredentialResult credentialResult = CredentialStore.Delete(CredTarget);
		if (!credentialResult.Committed)
		{
			TransactionLogger.Warn(string.Concat("Revenue: the saved key could not be removed (", credentialResult.State, ")"));
			return false;
		}
		if (credentialResult.ReconciliationPending)
		{
			CredentialResult credentialResult2 = CredentialStore.Reconcile(CredTarget);
			if (credentialResult2.ReconciliationPending)
			{
				TransactionLogger.Warn("Revenue: the removed key is still pending deletion in one store; it will be retried.");
			}
		}
		ClearCache();
		Config.IsConfigured = false;
		Config.AccountLabel = null;
		Config.TestMode = false;
		Save();
		return true;
	}

	private string CachePath(RevenueRange r)
	{
		return Path.Combine(_cacheDir, string.Concat(r, ".json"));
	}

	public RevenueSnapshot Cached(RevenueRange r)
	{
		try
		{
			string text = CachePath(r);
			if (!File.Exists(text) || new FileInfo(text).Length > 2097152)
			{
				return null;
			}
			RevenueSnapshot revenueSnapshot = new JavaScriptSerializer().Deserialize<RevenueSnapshot>(PersistenceHelper.SafeReadAllText(text));
			return (revenueSnapshot != null && revenueSnapshot.Provider == Config.Provider) ? revenueSnapshot : null;
		}
		catch
		{
			return null;
		}
	}

	private void StoreCache(RevenueSnapshot s)
	{
		RevenueSnapshot revenueSnapshot = Cached(s.Range);
		if (revenueSnapshot != null && revenueSnapshot.RangeStart == s.RangeStart && revenueSnapshot.RangeEnd == s.RangeEnd && revenueSnapshot.Currency == s.Currency && revenueSnapshot.Sales.HasValue && s.Sales.HasValue)
		{
			int? sales = s.Sales;
			int? sales2 = revenueSnapshot.Sales;
			if (sales.GetValueOrDefault() > sales2.GetValueOrDefault() && (sales.HasValue & sales2.HasValue) && s.Gross.HasValue && revenueSnapshot.Gross.HasValue && s.Gross > revenueSnapshot.Gross)
			{
				PetEngine.ObserveExternal("sale", "confetti", "New sale · " + ((s.Currency == "inr" || s.Currency == "INR") ? "₹" : (s.Currency + " ")) + ((decimal)(s.Gross.Value - revenueSnapshot.Gross.Value) / 100m).ToString("0.##", CultureInfo.CurrentCulture));
			}
		}
		try
		{
			Directory.CreateDirectory(_cacheDir);
			PersistenceHelper.WriteCacheText(CachePath(s.Range), new JavaScriptSerializer().Serialize(s));
			this.Changed?.Invoke();
		}
		catch
		{
		}
	}

	private void ClearCache()
	{
		foreach (RevenueRange value in Enum.GetValues(typeof(RevenueRange)))
		{
			try
			{
				File.Delete(CachePath(value));
			}
			catch
			{
			}
		}
	}

	public bool Fetch(RevenueRange range, bool force, Dispatcher ui, Action<RevenueSnapshot, Exception> done)
	{
		if (!Config.IsConfigured || _networkPaused)
		{
			return false;
		}
		lock (_lock)
		{
			if (_inFlight)
			{
				return false;
			}
			if (!force && _retryAt > DateTime.UtcNow)
			{
				return false;
			}
			_inFlight = true;
		}
		IRevenueProvider provider = Provider;
		string providerId = Config.Provider;
		Task.Factory.StartNew(delegate
		{
			RevenueSnapshot snap = null;
			Exception error = null;
			try
			{
				string text = CredentialStore.LoadToken(CredTarget);
				if (provider == null || string.IsNullOrEmpty(text))
				{
					throw new RevenueException(RevenueErrorKind.NotConfigured, "Ayn can't find the saved key", "Connect your account again.");
				}
				snap = provider.Fetch(text, range, Clock());
				snap.FetchedUtc = DateTime.UtcNow;
				StoreCache(snap);
				lock (_lock)
				{
					_failures = 0;
					_retryAt = DateTime.MinValue;
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = (error = ex);
				RevenueException ex3 = ex2 as RevenueException;
				lock (_lock)
				{
					_failures++;
					TimeSpan timeSpan = ((ex3 != null && ex3.RetryAfter.HasValue) ? ex3.RetryAfter.Value : TimeSpan.FromSeconds(Math.Min(900.0, 30.0 * Math.Pow(2.0, _failures - 1))));
					_retryAt = DateTime.UtcNow + timeSpan;
				}
				TransactionLogger.Warn("Revenue: " + providerId + " refresh failed (" + ((ex3 != null) ? ex3.Kind.ToString() : ex2.GetType().Name) + ")");
			}
			finally
			{
				lock (_lock)
				{
					_inFlight = false;
				}
			}
			ui.BeginInvoke((Action)delegate
			{
				done(snap, error);
			});
		});
		return true;
	}

	public static void Describe(Exception ex, out string title, out string advice)
	{
		if (ex is RevenueException ex2)
		{
			title = ex2.Message;
			advice = ex2.Advice;
		}
		else
		{
			title = "Something went wrong";
			advice = "Try Refresh in a minute.";
		}
	}
}
