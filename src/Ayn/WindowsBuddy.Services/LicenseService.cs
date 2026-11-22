using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public class LicenseService
{
	private sealed class AynRuntimeClock
	{
		private readonly object _lock = new object();

		private bool _loaded;

		private long _baseMs;

		private long _runMs;

		private long _lastMono = -1L;

		private long _savedBaseMs;

		private long _savedRunMs;

		private static readonly long SaveEveryMs = (long)TimeSpan.FromMinutes(5.0).TotalMilliseconds;

		private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Ayn_AynClock_2026");

		private static string FilePath => Path.Combine(AppPaths.DataDir, "trial", "clock.dat");

		private static string RegistryKey()
		{
			return AynRegistryKeyName();
		}

		private void Load()
		{
			if (_loaded)
			{
				return;
			}
			_loaded = true;
			long result = 0L;
			long result2 = 0L;
			long result3 = 0L;
			long result4 = 0L;
			try
			{
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RegistryKey(), writable: false);
				if (registryKey != null)
				{
					long.TryParse(Convert.ToString(registryKey.GetValue("AynRuntimeBaseMs")), out result);
					long.TryParse(Convert.ToString(registryKey.GetValue("AynRuntimeMs")), out result2);
				}
			}
			catch
			{
			}
			try
			{
				if (File.Exists(FilePath))
				{
					string[] array = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser)).Split('|');
					if (array.Length >= 2)
					{
						long.TryParse(array[0], out result3);
						long.TryParse(array[1], out result4);
					}
				}
			}
			catch
			{
			}
			if (result2 < 0)
			{
				result2 = 0L;
			}
			if (result4 < 0)
			{
				result4 = 0L;
			}
			if (result3 + result4 > result + result2)
			{
				_baseMs = result3;
				_runMs = result4;
			}
			else
			{
				_baseMs = result;
				_runMs = result2;
			}
			_savedBaseMs = _baseMs;
			_savedRunMs = _runMs;
		}

		private void Tick()
		{
			long num = AynServer.MonotonicMs();
			if (_lastMono >= 0 && num > _lastMono)
			{
				_runMs += num - _lastMono;
			}
			_lastMono = num;
		}

		public long FloorMs(long signedServerMs)
		{
			lock (_lock)
			{
				Load();
				Tick();
				if (signedServerMs > _baseMs)
				{
					_baseMs = signedServerMs;
					_runMs = 0L;
				}
				if (_baseMs != _savedBaseMs || _runMs - _savedRunMs >= SaveEveryMs)
				{
					Save();
				}
				return _baseMs + _runMs;
			}
		}

		public void Restart(long serverNowMs)
		{
			lock (_lock)
			{
				Load();
				Tick();
				if (serverNowMs > _baseMs + _runMs || serverNowMs < _baseMs)
				{
					_baseMs = serverNowMs;
					_runMs = 0L;
					if (Math.Abs(_baseMs - (_savedBaseMs + _savedRunMs)) >= SaveEveryMs)
					{
						Save();
					}
				}
			}
		}

		private void Save()
		{
			_savedBaseMs = _baseMs;
			_savedRunMs = _runMs;
			try
			{
				using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(RegistryKey());
				if (registryKey != null)
				{
					registryKey.SetValue("AynRuntimeBaseMs", _baseMs.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
					registryKey.SetValue("AynRuntimeMs", _runMs.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Ayn clock record (registry) not saved (" + ex.GetType().Name + ").");
			}
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				PersistenceHelper.WriteBytesOrThrow(FilePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(_baseMs + "|" + _runMs), Entropy, DataProtectionScope.CurrentUser));
			}
			catch (Exception ex2)
			{
				TransactionLogger.Warn("Ayn clock record (file) not saved (" + ex2.GetType().Name + ").");
			}
		}
	}

	public const int AynDays = 3;

	public const int OfflineGraceDays = 14;

	public const string DodoLiveBaseUrl = "https://live.dodopayments.com";

	public const string LiveProductId = "pdt_0NprIkKjEimUv2d8bembH";

	public const string DodoBaseUrl = "https://live.dodopayments.com";

	private const string CredentialTargetName = "Ayn_LicenseRecord";

	private const string AynFileName = "Ayn_Ayn.dat";

	private const string RegSubKey = "Software\\ModiStudio\\Ayn";

	private const string BusyMessage = "Another license operation is in progress. Please wait for it to finish.";

	private const string RefreshNotSavedMessage = "Ayn is active on this PC. Ayn could not save the latest license check yet; your saved activation is unaffected and Ayn will retry.";

	internal const string KeyActiveElsewhereMessage = "This key is already active on another PC. On that PC, open Ayn, go to Settings > License and click \"Deactivate this PC\", then activate here. If you no longer have that PC, email contact@modistudio.online and we will free the seat.";

	internal const string KeyDisabledMessage = "This key has been refunded or disabled, so it can't be activated. If you think this is a mistake, email contact@modistudio.online.";

	internal const string InvalidKeyMessage = "That key wasn't recognised. Check that you pasted the whole key from your purchase email and try again.";

	internal const string WrongProductMessage = "This key is for a different product, not Ayn. Paste the key from your Ayn purchase email.";

	internal const string NoNetworkMessage = "Couldn't reach the license server. Check your internet connection and try again.";

	internal const string RevokedMessage = "This PC's license is no longer active: the key was refunded or disabled, or this PC's activation was removed. Enter a key to activate again, or email contact@modistudio.online.";

	private const string ActivationUndoneMessage = "Ayn could not save the license on this PC, so the activation was undone and no device seat is in use. Free some disk space or check that your user folder is writable, then activate again.";

	private const string ActivationNotSavedMessage = "Ayn is active for this session only: the license could not be saved on this PC and the license server could not be reached to undo the activation. Keep Ayn open while it retries saving. If Ayn closes first, this PC's seat may stay in use; contact support if another activation reports the device limit.";

	private static LicenseService _instance;

	private static readonly object _syncLock = new object();

	private static readonly byte[] AynEntropy = Encoding.UTF8.GetBytes("Ayn_AynTamperEntropy_2026");

	private Timer _validationTimer;

	private StoredLicenseRecord _storedRecord;

	private readonly object _stateLock = new object();

	private CredentialState _recordState;

	private StoredLicenseRecord _pendingRecord;

	private bool _pendingIsRefresh;

	private bool _deletionPending;

	private string _notice;

	private readonly Func<string, string, int, Task<string>> _post;

	private readonly AynRuntimeClock _trialClock = new AynRuntimeClock();

	private int _operationPending;

	private List<PendingSeatRelease> _pendingReleases = new List<PendingSeatRelease>();

	private bool _releaseListDirty;

	private CredentialState _releaseStoreState;

	public static LicenseService Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_syncLock)
				{
					if (_instance == null)
					{
						_instance = new LicenseService();
					}
				}
			}
			return _instance;
		}
	}

	public static string ExpectedProductId => "pdt_0NprIkKjEimUv2d8bembH";

	public static string ActiveBaseUrl => "https://live.dodopayments.com";

	public bool AwaitingFirstServerContact
	{
		get
		{
			lock (_stateLock)
			{
				return AynServer.Enabled && !HasStoredKey && AynServer.Current == null;
			}
		}
	}

	public AccessState Access
	{
		get
		{
			if (IsUnlocked)
			{
				if (!HasStoredKey)
				{
					return AccessState.Ayn;
				}
				return AccessState.Ayn;
			}
			if (HasStoredKey || !AwaitingFirstServerContact)
			{
				return AccessState.NeedsActivation;
			}
			if (AynServer.LastAttemptOffline)
			{
				return AccessState.Offline;
			}
			if (AynServer.LastAttemptServerUnreachable)
			{
				return AccessState.ServerUnreachable;
			}
			return AccessState.Checking;
		}
	}

	public bool AynStarted
	{
		get
		{
			GetAynWindow(out var startedUtcTicks, out var _);
			return startedUtcTicks > 0;
		}
	}

	public int AynDaysLeft
	{
		get
		{
			GetAynWindow(out var startedUtcTicks, out var now);
			if (startedUtcTicks <= 0)
			{
				if (!AwaitingFirstServerContact)
				{
					return 3;
				}
				return 0;
			}
			DateTime dateTime = new DateTime(startedUtcTicks, DateTimeKind.Utc);
			double totalDays = (now - dateTime).TotalDays;
			int val = (int)Math.Ceiling(3.0 - totalDays);
			return Math.Max(0, val);
		}
	}

	public LicenseStatus Status
	{
		get
		{
			lock (_stateLock)
			{
				if (_storedRecord != null && !string.IsNullOrEmpty(_storedRecord.LicenseKey) && !string.IsNullOrEmpty(_storedRecord.InstanceId))
				{
					if (_storedRecord.LicenseKey.StartsWith("DEV-", StringComparison.OrdinalIgnoreCase))
					{
						return LicenseStatus.NotActivated;
					}
					GetAynAndClockState(out var _, out var _, out var effectiveNow);
					if (_storedRecord.LastValidatedUtcTicks > 0)
					{
						DateTime dateTime = new DateTime(_storedRecord.LastValidatedUtcTicks, DateTimeKind.Utc);
						double totalDays = (effectiveNow - dateTime).TotalDays;
						if (totalDays <= 14.0)
						{
							return LicenseStatus.Active;
						}
						TransactionLogger.Warn("Offline grace expired (> 14 days without validation).");
						return LicenseStatus.NotActivated;
					}
					return LicenseStatus.Active;
				}
				if (AynDaysLeft > 0)
				{
					return LicenseStatus.Ayn;
				}
				return LicenseStatus.NotActivated;
			}
		}
	}

	public LicenseState State => Status switch
	{
		LicenseStatus.Active => LicenseState.Active, 
		LicenseStatus.Ayn => LicenseState.NotActivated, 
		_ => LicenseState.NotActivated, 
	};

	public bool IsUnlocked
	{
		get
		{
			return true;
		}
	}

	public string StatusLine
	{
		get
		{
			return "Ayn · SabrWare";
		}
	}

	public bool HasStoredKey
	{
		get
		{
			if (_storedRecord != null)
			{
				return !string.IsNullOrEmpty(_storedRecord.LicenseKey);
			}
			return false;
		}
	}

	public string StoredKey
	{
		get
		{
			if (_storedRecord == null)
			{
				return null;
			}
			return _storedRecord.LicenseKey;
		}
	}

	public string MaskedKey
	{
		get
		{
			string storedKey = StoredKey;
			if (string.IsNullOrEmpty(storedKey))
			{
				return string.Empty;
			}
			if (storedKey.Contains("-"))
			{
				string[] array = storedKey.Split('-');
				if (array.Length >= 3)
				{
					for (int i = 1; i < array.Length - 1; i++)
					{
						array[i] = new string('•', Math.Max(4, array[i].Length));
					}
					return string.Join("-", array);
				}
			}
			if (storedKey.Length > 8)
			{
				return storedKey.Substring(0, 4) + "-••••-••••-" + storedKey.Substring(storedKey.Length - 4);
			}
			return new string('•', storedKey.Length);
		}
	}

	private string ReleaseTarget => "Ayn_LicenseRecord.PendingRelease";

	public SeatReleaseState PendingSeatReleaseState
	{
		get
		{
			lock (_stateLock)
			{
				if (_pendingReleases.Count == 0)
				{
					return SeatReleaseState.None;
				}
				return (!_pendingReleases.Any((PendingSeatRelease e) => e.NeedsAttention)) ? SeatReleaseState.Pending : SeatReleaseState.NeedsAttention;
			}
		}
	}

	public int? OfflineGraceDaysLeft
	{
		get
		{
			if (Status != LicenseStatus.Active)
			{
				return null;
			}
			long num;
			lock (_stateLock)
			{
				num = ((_storedRecord != null) ? _storedRecord.LastValidatedUtcTicks : 0);
			}
			if (num <= 0)
			{
				return null;
			}
			GetAynAndClockState(out var _, out var _, out var effectiveNow);
			double totalDays = (effectiveNow - new DateTime(num, DateTimeKind.Utc)).TotalDays;
			if (totalDays <= 2.0)
			{
				return null;
			}
			return Math.Max(0, (int)Math.Floor(14.0 - totalDays));
		}
	}

	public CredentialState RecordState
	{
		get
		{
			lock (_stateLock)
			{
				return _recordState;
			}
		}
	}

	public bool PersistencePending
	{
		get
		{
			lock (_stateLock)
			{
				return _pendingRecord != null || _deletionPending;
			}
		}
	}

	public string PersistenceWarning
	{
		get
		{
			lock (_stateLock)
			{
				if (_notice != null)
				{
					return _notice;
				}
				if (_pendingRecord != null)
				{
					return _pendingIsRefresh ? "Ayn is active on this PC. Ayn could not save the latest license check yet; your saved activation is unaffected and Ayn will retry." : "Ayn is active for this session only: the license could not be saved on this PC and the license server could not be reached to undo the activation. Keep Ayn open while it retries saving. If Ayn closes first, this PC's seat may stay in use; contact support if another activation reports the device limit.";
				}
				PendingSeatRelease pendingSeatRelease = _pendingReleases.FirstOrDefault((PendingSeatRelease e) => e.NeedsAttention);
				if (pendingSeatRelease != null)
				{
					return "The license server did not confirm releasing a previous activation of this PC (HTTP " + pendingSeatRelease.LastStatusCode + "). If another PC reports the device limit, retry the release or contact support.";
				}
				if (_pendingReleases.Count > 0)
				{
					return "A previous activation of this PC still holds a license seat because the license server could not be reached. Ayn retries automatically; you can also retry now.";
				}
				if (_releaseStoreState == CredentialState.Corrupt || _releaseStoreState == CredentialState.Unavailable)
				{
					return "Ayn could not read its record of a seat to release. It was kept; if another PC reports the device limit, contact support.";
				}
				if (_deletionPending)
				{
					return "Ayn could not fully remove the license from this PC yet. It will retry automatically.";
				}
				if (_storedRecord == null && _recordState == CredentialState.Corrupt)
				{
					return "Ayn found a saved license it cannot read. It was kept; activate again with your key to replace it.";
				}
				if (_storedRecord == null && _recordState == CredentialState.Unavailable)
				{
					return "Ayn could not read the saved license from Windows. It will try again.";
				}
				return null;
			}
		}
	}

	public event EventHandler LicenseChanged;

	public LicenseService()
		: this(null, startBackgroundWork: true)
	{
	}

	private LicenseService(Func<string, string, int, Task<string>> post, bool startBackgroundWork)
	{
		_post = post ?? new Func<string, string, int, Task<string>>(PostJsonAsync);
		try
		{
			CredentialStore.MigrateLicenseMirror("Ayn_LicenseRecord");
			CredentialStore.MigrateLicenseMirror("Ayn_LicenseRecord.PendingRelease");
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("License storage migration pending: " + ex.GetType().Name);
		}
		LoadStoredRecord();
		LoadPendingReleases();
		if (!startBackgroundWork)
		{
			return;
		}
		PersistenceHelper.RegisterExitPreparation("license record", delegate
		{
			try
			{
				RetryPersistence();
			}
			catch (Exception ex2)
			{
				TransactionLogger.Warn("License persistence retry failed: " + ex2.GetType().Name);
			}
		});
		StartPeriodicValidation();
	}

	internal static LicenseService CreateForQa(Func<string, string, int, Task<string>> post)
	{
		if (!AppPaths.IsSandboxed || post == null)
		{
			throw new InvalidOperationException("QA license services require a sandboxed profile and a fake transport.");
		}
		return new LicenseService(post, startBackgroundWork: false);
	}

	private static async Task<string> PostJsonAsync(string endpoint, string reqJson, int timeoutMs)
	{
		HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(endpoint);
		httpReq.Method = "POST";
		httpReq.ContentType = "application/json";
		httpReq.Timeout = timeoutMs;
		TimerCallback callback = delegate
		{
			try
			{
				httpReq.Abort();
			}
			catch
			{
			}
		};
		using (new Timer(callback, null, timeoutMs, -1))
		{
			byte[] postBytes = Encoding.UTF8.GetBytes(reqJson);
			using (Stream reqStream = await httpReq.GetRequestStreamAsync())
			{
				await reqStream.WriteAsync(postBytes, 0, postBytes.Length);
			}
			HttpWebResponse httpWebResponse = (HttpWebResponse)(await httpReq.GetResponseAsync());
			using HttpWebResponse resp = httpWebResponse;
			using StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8);
			return await reader.ReadToEndAsync();
		}
	}

	private string GetAynFilePath()
	{
		string dataDir = AppPaths.DataDir;
		if (!Directory.Exists(dataDir))
		{
			try
			{
				Directory.CreateDirectory(dataDir);
			}
			catch
			{
			}
		}
		return Path.Combine(dataDir, "Ayn_Ayn.dat");
	}

	private string GetRegistryKeyName()
	{
		return AynRegistryKeyName();
	}

	private static string AynRegistryKeyName()
	{
		return "Software\\ModiStudio\\Ayn" + (AppPaths.IsSandboxed ? AppPaths.InstanceSuffix : "");
	}

	private void ReadAynFromRegistry(out long startedTicks, out long lastSeenTicks)
	{
		startedTicks = 0L;
		lastSeenTicks = 0L;
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(GetRegistryKeyName(), writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("AynStartedUtcTicks");
				if (value != null)
				{
					long.TryParse(value.ToString(), out startedTicks);
				}
				object value2 = registryKey.GetValue("LastSeenUtcTicks");
				if (value2 != null)
				{
					long.TryParse(value2.ToString(), out lastSeenTicks);
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("ReadAynFromRegistry error: " + ex.Message);
		}
	}

	private void ReadAynFromFile(out long startedTicks, out long lastSeenTicks)
	{
		startedTicks = 0L;
		lastSeenTicks = 0L;
		try
		{
			string trialFilePath = GetAynFilePath();
			if (!File.Exists(trialFilePath))
			{
				return;
			}
			byte[] array = File.ReadAllBytes(trialFilePath);
			if (array != null && array.Length > 0)
			{
				byte[] bytes = ProtectedData.Unprotect(array, AynEntropy, DataProtectionScope.CurrentUser);
				string @string = Encoding.UTF8.GetString(bytes);
				string[] array2 = @string.Split('|');
				if (array2.Length >= 1)
				{
					long.TryParse(array2[0], out startedTicks);
				}
				if (array2.Length >= 2)
				{
					long.TryParse(array2[1], out lastSeenTicks);
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("ReadAynFromFile error: " + ex.Message);
		}
	}

	private void WriteAynData(long startedTicks, long lastSeenTicks)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(GetRegistryKeyName());
			if (registryKey != null)
			{
				registryKey.SetValue("AynStartedUtcTicks", startedTicks.ToString(), RegistryValueKind.String);
				registryKey.SetValue("LastSeenUtcTicks", lastSeenTicks.ToString(), RegistryValueKind.String);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("WriteAynRegistry error: " + ex.Message);
		}
		try
		{
			string trialFilePath = GetAynFilePath();
			string s = startedTicks + "|" + lastSeenTicks;
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			byte[] content = ProtectedData.Protect(bytes, AynEntropy, DataProtectionScope.CurrentUser);
			PersistenceHelper.WriteBytesOrThrow(trialFilePath, content);
		}
		catch (Exception ex2)
		{
			TransactionLogger.Warn("WriteAynFile error: " + ex2.Message);
		}
	}

	public void GetAynAndClockState(out long startedUtcTicks, out long lastSeenUtcTicks, out DateTime effectiveNow)
	{
		lock (_stateLock)
		{
			ReadAynFromRegistry(out var startedTicks, out var lastSeenTicks);
			ReadAynFromFile(out var startedTicks2, out var lastSeenTicks2);
			if (startedTicks > 0 && startedTicks2 > 0)
			{
				startedUtcTicks = Math.Min(startedTicks, startedTicks2);
			}
			else if (startedTicks > 0)
			{
				startedUtcTicks = startedTicks;
			}
			else if (startedTicks2 > 0)
			{
				startedUtcTicks = startedTicks2;
			}
			else
			{
				startedUtcTicks = 0L;
			}
			lastSeenUtcTicks = Math.Max(lastSeenTicks, lastSeenTicks2);
			DateTime? sessionServerNowUtc = AynServer.SessionServerNowUtc;
			DateTime dateTime = sessionServerNowUtc ?? AynServer.LocalClock();
			if (!sessionServerNowUtc.HasValue && lastSeenUtcTicks > 0 && dateTime.Ticks < lastSeenUtcTicks - TimeSpan.FromHours(1.0).Ticks)
			{
				TransactionLogger.Warn("Clock rollback detected (>1h). Using LastSeen timestamp for trial and grace calculations.");
				effectiveNow = new DateTime(lastSeenUtcTicks, DateTimeKind.Utc);
			}
			else
			{
				effectiveNow = dateTime;
				if (sessionServerNowUtc.HasValue || dateTime.Ticks > lastSeenUtcTicks)
				{
					lastSeenUtcTicks = dateTime.Ticks;
				}
			}
			long ticks = TimeSpan.FromMinutes(5.0).Ticks;
			bool flag = startedTicks != startedUtcTicks || startedTicks2 != startedUtcTicks || lastSeenTicks > lastSeenUtcTicks || lastSeenTicks2 > lastSeenUtcTicks || lastSeenTicks < lastSeenUtcTicks - ticks || lastSeenTicks2 < lastSeenUtcTicks - ticks;
			if (startedUtcTicks > 0 && flag)
			{
				WriteAynData(startedUtcTicks, lastSeenUtcTicks);
			}
		}
	}

	public void StartAyn()
	{
		bool flag = false;
		bool flag2 = false;
		lock (_stateLock)
		{
			GetAynAndClockState(out var startedUtcTicks, out var _, out var _);
			if (startedUtcTicks > 0)
			{
				EnsureAynAnchor();
				return;
			}
			if (AynServer.Enabled && AynServer.Current == null)
			{
				flag2 = true;
				TransactionLogger.Info("Ayn not started yet: waiting for the first answer from the trial server.");
			}
			else if (!AynServer.Enabled)
			{
				long ticks = AynServer.LocalClock().Ticks;
				WriteAynData(ticks, ticks);
				TransactionLogger.Info("Ayn Ayn started (" + 3 + " days).");
				flag = true;
			}
		}
		EnsureAynAnchor();
		if (flag || flag2)
		{
			NotifyLicenseChanged();
		}
	}

	public void CheckAynNow()
	{
		EnsureAynAnchor();
		AynServer.RetryNow();
	}

	private void GetAynWindow(out long startedUtcTicks, out DateTime now)
	{
		GetAynAndClockState(out startedUtcTicks, out var lastSeenUtcTicks, out now);
		SignedAynAnchor current = AynServer.Current;
		if (current == null)
		{
			if (AynServer.Enabled)
			{
				startedUtcTicks = 0L;
			}
			return;
		}
		long num = startedUtcTicks;
		startedUtcTicks = AynServer.FromUnixMs(current.AynStartMs).Ticks;
		DateTime? sessionServerNowUtc = AynServer.SessionServerNowUtc;
		if (sessionServerNowUtc.HasValue)
		{
			now = sessionServerNowUtc.Value;
			_trialClock.Restart(AynClockMs(now));
		}
		else
		{
			long ms = Math.Max(current.ServerTimeMs, _trialClock.FloorMs(current.ServerTimeMs));
			DateTime dateTime = AynServer.FromUnixMs(ms);
			if (lastSeenUtcTicks > dateTime.Ticks)
			{
				dateTime = new DateTime(lastSeenUtcTicks, DateTimeKind.Utc);
			}
			if (dateTime > now)
			{
				now = dateTime;
			}
		}
		if (num != startedUtcTicks)
		{
			lock (_stateLock)
			{
				WriteAynData(startedUtcTicks, now.Ticks);
			}
		}
	}

	private static long AynClockMs(DateTime utc)
	{
		return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
	}

	private void EnsureAynAnchor()
	{
		if (!HasStoredKey && AynServer.Enabled)
		{
			AynServer.EnsureSyncInBackground(() => !HasStoredKey, delegate
			{
				AlignLocalAynWithAnchor();
				NotifyLicenseChanged();
			}, delegate
			{
				NotifyLicenseChanged();
			});
		}
	}

	internal void AlignLocalAynWithAnchor()
	{
		SignedAynAnchor current = AynServer.Current;
		DateTime? sessionServerNowUtc = AynServer.SessionServerNowUtc;
		if (current != null && sessionServerNowUtc.HasValue)
		{
			lock (_stateLock)
			{
				WriteAynData(AynServer.FromUnixMs(current.AynStartMs).Ticks, sessionServerNowUtc.Value.Ticks);
			}
			_trialClock.Restart(AynClockMs(sessionServerNowUtc.Value));
		}
	}

	public async Task<ActivationResult> ActivateAsync(string licenseKey)
	{
		if (Interlocked.CompareExchange(ref _operationPending, 1, 0) != 0)
		{
			return new ActivationResult
			{
				Success = false,
				Status = ActivationStatus.Busy,
				Message = "Another license operation is in progress. Please wait for it to finish."
			};
		}
		try
		{
			return await ActivateCoreAsync(licenseKey);
		}
		finally
		{
			Interlocked.Exchange(ref _operationPending, 0);
		}
	}

	private async Task<ActivationResult> ActivateCoreAsync(string licenseKey)
	{
		string key = LicenseKeyInput.Normalize(licenseKey) ?? "";
		if (string.IsNullOrEmpty(key))
		{
			ActivationResult activationResult = new ActivationResult();
			activationResult.Success = false;
			activationResult.Status = ActivationStatus.InvalidKey;
			activationResult.Message = "Please enter a license key.";
			return activationResult;
		}
		if (key.StartsWith("DEV-", StringComparison.OrdinalIgnoreCase))
		{
			return new ActivationResult
			{
				Success = false,
				Status = ActivationStatus.InvalidKey,
				Message = "Invalid license key."
			};
		}
		GetAynAndClockState(out var _, out var _, out var effectiveNow);
		ActivationResult reused = await TryReuseInstanceAsync(key, effectiveNow.Ticks);
		if (reused != null)
		{
			return reused;
		}
		try
		{
			string endpoint = ActiveBaseUrl + "/licenses/activate";
			JavaScriptSerializer serializer = new JavaScriptSerializer();
			string reqJson = serializer.Serialize(new
			{
				license_key = key,
				name = Environment.MachineName
			});
			Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(await _post(endpoint, reqJson, 12000));
			string instanceId = null;
			if (dict != null)
			{
				if (dict.ContainsKey("id") && dict["id"] != null)
				{
					instanceId = dict["id"].ToString();
				}
				else if (dict.ContainsKey("license_key_instance_id") && dict["license_key_instance_id"] != null)
				{
					instanceId = dict["license_key_instance_id"].ToString();
				}
			}
			if (string.IsNullOrEmpty(instanceId))
			{
				TransactionLogger.Warn("Dodo activation response had no instance id; not activated.");
				ActivationResult activationResult2 = new ActivationResult();
				activationResult2.Success = false;
				activationResult2.Status = ActivationStatus.NetworkError;
				activationResult2.Message = "The license server sent an incomplete answer, so this PC was not activated. Please try again; if it keeps happening, contact support.";
				return activationResult2;
			}
			string expectedProduct = ExpectedProductId;
			if (expectedProduct != null && !string.Equals(ProductIdOf(dict), expectedProduct, StringComparison.Ordinal))
			{
				TransactionLogger.Warn("Activated key belongs to another product; activation undone.");
				await PostReleaseAsync(key, instanceId);
				return new ActivationResult
				{
					Success = false,
					Status = ActivationStatus.WrongProduct,
					Message = "This key is for a different product, not Ayn. Paste the key from your Ayn purchase email."
				};
			}
			StoredLicenseRecord record = new StoredLicenseRecord
			{
				LicenseKey = key,
				InstanceId = instanceId,
				LastValidatedUtcTicks = effectiveNow.Ticks
			};
			return await CommitActivationAsync(record);
		}
		catch (WebException ex)
		{
			if (ex.Response is HttpWebResponse httpWebResponse)
			{
				try
				{
					using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8);
					string input = streamReader.ReadToEnd();
					JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
					Dictionary<string, object> dictionary = javaScriptSerializer.Deserialize<Dictionary<string, object>>(input);
					string text = null;
					string text2 = null;
					if (dictionary != null)
					{
						if (dictionary.ContainsKey("message") && dictionary["message"] != null)
						{
							text = dictionary["message"].ToString();
						}
						else if (dictionary.ContainsKey("error") && dictionary["error"] != null)
						{
							text = dictionary["error"].ToString();
						}
						if (dictionary.ContainsKey("code") && dictionary["code"] != null)
						{
							text2 = dictionary["code"].ToString();
						}
					}
					int statusCode = (int)httpWebResponse.StatusCode;
					if (statusCode == 422 || text2 == "LICENSE_KEY_LIMIT_REACHED" || (text != null && (text.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0)))
					{
						ActivationResult activationResult3 = new ActivationResult();
						activationResult3.Success = false;
						activationResult3.Status = ActivationStatus.DeviceLimitReached;
						activationResult3.Message = "This key is already active on another PC. On that PC, open Ayn, go to Settings > License and click \"Deactivate this PC\", then activate here. If you no longer have that PC, email contact@modistudio.online and we will free the seat.";
						return activationResult3;
					}
					switch (statusCode)
					{
					case 403:
					{
						ActivationResult activationResult5 = new ActivationResult();
						activationResult5.Success = false;
						activationResult5.Status = ActivationStatus.KeyDisabled;
						activationResult5.Message = "This key has been refunded or disabled, so it can't be activated. If you think this is a mistake, email contact@modistudio.online.";
						return activationResult5;
					}
					default:
						if (text == null || (text.IndexOf("not found", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) < 0))
						{
							break;
						}
						goto case 404;
					case 404:
					{
						ActivationResult activationResult4 = new ActivationResult();
						activationResult4.Success = false;
						activationResult4.Status = ActivationStatus.InvalidKey;
						activationResult4.Message = "That key wasn't recognised. Check that you pasted the whole key from your purchase email and try again.";
						return activationResult4;
					}
					}
					ActivationResult activationResult6 = new ActivationResult();
					activationResult6.Success = false;
					activationResult6.Status = ActivationStatus.InvalidKey;
					activationResult6.Message = ((!string.IsNullOrEmpty(text)) ? text : ("Activation error (" + statusCode + ")."));
					return activationResult6;
				}
				catch
				{
				}
			}
			TransactionLogger.Warn("Network error during activation: " + ex.Message);
			ActivationResult activationResult7 = new ActivationResult();
			activationResult7.Success = false;
			activationResult7.Status = ActivationStatus.NetworkError;
			activationResult7.Message = "Couldn't reach the license server. Check your internet connection and try again.";
			return activationResult7;
		}
		catch (Exception ex2)
		{
			switch (ServerStatus(ex2))
			{
			case 422:
			{
				ActivationResult activationResult11 = new ActivationResult();
				activationResult11.Success = false;
				activationResult11.Status = ActivationStatus.DeviceLimitReached;
				activationResult11.Message = "This key is already active on another PC. On that PC, open Ayn, go to Settings > License and click \"Deactivate this PC\", then activate here. If you no longer have that PC, email contact@modistudio.online and we will free the seat.";
				return activationResult11;
			}
			case 403:
			{
				ActivationResult activationResult10 = new ActivationResult();
				activationResult10.Success = false;
				activationResult10.Status = ActivationStatus.KeyDisabled;
				activationResult10.Message = "This key has been refunded or disabled, so it can't be activated. If you think this is a mistake, email contact@modistudio.online.";
				return activationResult10;
			}
			case 404:
			{
				ActivationResult activationResult9 = new ActivationResult();
				activationResult9.Success = false;
				activationResult9.Status = ActivationStatus.InvalidKey;
				activationResult9.Message = "That key wasn't recognised. Check that you pasted the whole key from your purchase email and try again.";
				return activationResult9;
			}
			default:
			{
				TransactionLogger.Error("Activation unexpected error (" + ex2.GetType().Name + ").");
				ActivationResult activationResult8 = new ActivationResult();
				activationResult8.Success = false;
				activationResult8.Status = ActivationStatus.NetworkError;
				activationResult8.Message = "Activation failed: unexpected response from the license server. Please try again.";
				return activationResult8;
			}
			}
		}
	}

	private static string ProductIdOf(Dictionary<string, object> activation)
	{
		object value;
		Dictionary<string, object> dictionary = ((activation != null && activation.TryGetValue("product", out value)) ? (value as Dictionary<string, object>) : null);
		if (dictionary == null || !dictionary.TryGetValue("product_id", out var value2) || value2 == null)
		{
			return null;
		}
		return value2.ToString();
	}

	private async Task<ActivationResult> CommitActivationAsync(StoredLicenseRecord record)
	{
		StoredLicenseRecord previous;
		lock (_stateLock)
		{
			previous = _storedRecord;
		}
		if (AdoptActivatedRecord(record))
		{
			NotifyLicenseChanged();
			ActivationResult activationResult = new ActivationResult();
			activationResult.Success = true;
			activationResult.Status = ActivationStatus.Success;
			activationResult.Message = "Ayn successfully activated!";
			return activationResult;
		}
		if (!string.IsNullOrEmpty(record.InstanceId) && IsSuccess(await PostReleaseAsync(record.LicenseKey, record.InstanceId)))
		{
			lock (_stateLock)
			{
				if (object.ReferenceEquals(_storedRecord, record))
				{
					_storedRecord = previous;
					_pendingRecord = null;
				}
				_notice = null;
			}
			TransactionLogger.Warn("License activation undone: it could not be stored on this PC.");
			NotifyLicenseChanged();
			ActivationResult activationResult2 = new ActivationResult();
			activationResult2.Success = false;
			activationResult2.Status = ActivationStatus.PersistenceFailed;
			activationResult2.Message = "Ayn could not save the license on this PC, so the activation was undone and no device seat is in use. Free some disk space or check that your user folder is writable, then activate again.";
			return activationResult2;
		}
		NotifyLicenseChanged();
		return new ActivationResult
		{
			Success = true,
			Status = ActivationStatus.Success,
			Message = "Ayn is active for this session only: the license could not be saved on this PC and the license server could not be reached to undo the activation. Keep Ayn open while it retries saving. If Ayn closes first, this PC's seat may stay in use; contact support if another activation reports the device limit.",
			PersistencePending = true
		};
	}

	private async Task<ActivationResult> TryReuseInstanceAsync(string key, long nowTicks)
	{
		StoredLicenseRecord current;
		PendingSeatRelease release;
		lock (_stateLock)
		{
			current = _storedRecord;
			release = _pendingReleases.FirstOrDefault((PendingSeatRelease r) => string.Equals(r.LicenseKey, key, StringComparison.Ordinal) && !string.IsNullOrEmpty(r.InstanceId));
		}
		bool currentMatches = current != null && string.Equals(current.LicenseKey, key, StringComparison.Ordinal) && !string.IsNullOrEmpty(current.InstanceId);
		if (!currentMatches && release == null)
		{
			await RetryPendingReleasesCoreAsync(manual: false);
			return null;
		}
		string instance = (currentMatches ? current.InstanceId : release.InstanceId);
		bool? valid = await ValidateInstanceAsync(key, instance);
		if (!valid.HasValue)
		{
			return new ActivationResult
			{
				Success = false,
				Status = ActivationStatus.NetworkError,
				Message = "Could not reach the license server to check this PC's existing activation. No new activation was started, so no extra seat was used. Please try again."
			};
		}
		if (valid == false)
		{
			if (!currentMatches)
			{
				RemovePendingRelease(release);
			}
			return null;
		}
		StoredLicenseRecord record = new StoredLicenseRecord
		{
			LicenseKey = key,
			InstanceId = instance,
			LastValidatedUtcTicks = nowTicks
		};
		bool saved;
		lock (_stateLock)
		{
			saved = PersistStoredRecord(record).Committed || PersistStoredRecord(record).Committed;
			if (!saved && currentMatches)
			{
				_storedRecord = record;
				_pendingRecord = record;
				_pendingIsRefresh = true;
			}
			if (saved)
			{
				_notice = null;
			}
		}
		if (!currentMatches)
		{
			if (!saved)
			{
				ActivationResult activationResult = new ActivationResult();
				activationResult.Success = false;
				activationResult.Status = ActivationStatus.PersistenceFailed;
				activationResult.Message = "Ayn could not save the license on this PC. Your previous activation is still reserved for this PC; free some disk space and activate again.";
				return activationResult;
			}
			RemovePendingRelease(release);
		}
		NotifyLicenseChanged();
		return new ActivationResult
		{
			Success = true,
			Status = ActivationStatus.Success,
			Message = (saved ? "Ayn is active on this PC again. Its existing activation was reused, so no additional seat was used." : "Ayn is active on this PC. Ayn could not save the latest license check yet; your saved activation is unaffected and Ayn will retry."),
			PersistencePending = !saved
		};
	}

	private static string InstancePayload(string key, string instanceId)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("license_key", key);
		Dictionary<string, object> dictionary2 = dictionary;
		if (!string.IsNullOrEmpty(instanceId))
		{
			dictionary2["license_key_instance_id"] = instanceId;
		}
		return new JavaScriptSerializer().Serialize(dictionary2);
	}

	private static int ServerStatus(Exception ex)
	{
		if (ex is WebException { Response: HttpWebResponse response })
		{
			return (int)response.StatusCode;
		}
		if (!(ex is LicenseServerException ex3))
		{
			return 0;
		}
		return ex3.StatusCode;
	}

	private static string ServerCode(Exception ex)
	{
		if (ex is LicenseServerException ex2)
		{
			return ex2.Code;
		}
		HttpWebResponse httpWebResponse = ((ex is WebException ex3) ? (ex3.Response as HttpWebResponse) : null);
		if (httpWebResponse == null)
		{
			return null;
		}
		try
		{
			using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8);
			Dictionary<string, object> dictionary = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(streamReader.ReadToEnd());
			object value;
			return (dictionary != null && dictionary.TryGetValue("code", out value) && value != null) ? value.ToString() : null;
		}
		catch
		{
			return null;
		}
	}

	private static bool IsSuccess(int status)
	{
		if (status >= 200)
		{
			return status < 300;
		}
		return false;
	}

	private static bool IsDefinitiveRejection(int status)
	{
		if (status >= 400 && status < 500 && status != 408)
		{
			return status != 429;
		}
		return false;
	}

	private async Task<int> PostReleaseAsync(string key, string instanceId)
	{
		int num = default(int);
		_ = num;
		_ = 0;
		try
		{
			await _post(ActiveBaseUrl + "/licenses/deactivate", InstancePayload(key, instanceId), 8000);
			TransactionLogger.Info("Dodo remote deactivation confirmed.");
			return 200;
		}
		catch (Exception ex)
		{
			int num2 = ServerStatus(ex);
			if (num2 == 403 && ServerCode(ex) == "LICENSE_KEY_NOT_FOUND")
			{
				TransactionLogger.Info("Dodo reports the instance is already released (403 LICENSE_KEY_NOT_FOUND).");
				return 200;
			}
			TransactionLogger.Warn("Dodo remote deactivation not confirmed (" + ((num2 == 0) ? "no response" : ("HTTP " + num2)) + ").");
			return num2;
		}
	}

	private async Task<bool?> ValidateInstanceAsync(string key, string instanceId)
	{
		int num = default(int);
		_ = num;
		_ = 0;
		try
		{
			string json = await _post(ActiveBaseUrl + "/licenses/validate", InstancePayload(key, instanceId), 10000);
			Dictionary<string, object> dict = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
			return dict != null && dict.ContainsKey("valid") && dict["valid"] is bool && (bool)dict["valid"];
		}
		catch (Exception ex)
		{
			int num2 = ServerStatus(ex);
			if (num2 == 404)
			{
				return false;
			}
			TransactionLogger.Info("Instance validation skipped (" + ((num2 == 0) ? "no response" : ("HTTP " + num2)) + ").");
			return null;
		}
	}

	public async Task<bool> ValidateStoredLicenseAsync()
	{
		try
		{
			RetryPersistence();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("License persistence retry failed: " + ex.GetType().Name);
		}
		StoredLicenseRecord record;
		lock (_stateLock)
		{
			record = _storedRecord;
		}
		if (record == null || string.IsNullOrEmpty(record.LicenseKey))
		{
			return false;
		}
		GetAynAndClockState(out var _, out var _, out var effectiveNow);
		if (record.LicenseKey.StartsWith("DEV-", StringComparison.OrdinalIgnoreCase))
		{
			RevokeIfCurrent(record);
			NotifyLicenseChanged();
			return false;
		}
		if (string.IsNullOrEmpty(record.InstanceId))
		{
			TransactionLogger.Warn("Stored license has no instance id; not activated.");
			return false;
		}
		try
		{
			string endpoint = ActiveBaseUrl + "/licenses/validate";
			JavaScriptSerializer serializer = new JavaScriptSerializer();
			string reqJson = InstancePayload(record.LicenseKey, record.InstanceId);
			Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(await _post(endpoint, reqJson, 10000));
			bool isValid = false;
			if (dict != null && dict.ContainsKey("valid") && dict["valid"] is bool)
			{
				isValid = (bool)dict["valid"];
			}
			if (isValid)
			{
				RecordValidated(record, effectiveNow.Ticks);
				TransactionLogger.Info("Dodo license validated successfully.");
				return true;
			}
			TransactionLogger.Warn("Dodo license returned valid=false. Revoking license.");
			RevokeIfCurrent(record);
			NotifyLicenseChanged();
			return false;
		}
		catch (WebException ex2)
		{
			if (ex2.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound })
			{
				TransactionLogger.Warn("Dodo license returned 404 Key Not Found. Revoking license.");
				RevokeIfCurrent(record);
				NotifyLicenseChanged();
				return false;
			}
			TransactionLogger.Info("Dodo periodic validation skipped (offline/network): " + ex2.Message);
			if (record.LastValidatedUtcTicks > 0)
			{
				DateTime dateTime = new DateTime(record.LastValidatedUtcTicks, DateTimeKind.Utc);
				double totalDays = (effectiveNow - dateTime).TotalDays;
				if (totalDays > 14.0)
				{
					TransactionLogger.Warn("Dodo 14-day offline grace expired without network validation.");
					NotifyLicenseChanged();
					return false;
				}
			}
			return true;
		}
		catch (Exception ex3)
		{
			TransactionLogger.Warn("Dodo periodic validation unexpected error: " + ex3.Message);
			return true;
		}
	}

	public async Task<bool> DeactivateCurrentDeviceAsync()
	{
		return (await DeactivateDeviceAsync()).Success;
	}

	public async Task<DeactivationResult> DeactivateDeviceAsync()
	{
		if (Interlocked.CompareExchange(ref _operationPending, 1, 0) != 0)
		{
			return new DeactivationResult
			{
				LocalCleared = false,
				Remote = RemoteRelease.NotRequired,
				Message = "Another license operation is in progress. Please wait for it to finish."
			};
		}
		try
		{
			return await DeactivateCoreAsync();
		}
		finally
		{
			Interlocked.Exchange(ref _operationPending, 0);
		}
	}

	private async Task<DeactivationResult> DeactivateCoreAsync()
	{
		StoredLicenseRecord record;
		lock (_stateLock)
		{
			record = _storedRecord;
		}
		DeactivationResult result = new DeactivationResult
		{
			Remote = RemoteRelease.NotRequired
		};
		int status = 0;
		bool mock = record != null && record.InstanceId != null && record.InstanceId.StartsWith("mock_instance_", StringComparison.Ordinal);
		if (record != null && !string.IsNullOrEmpty(record.LicenseKey) && !mock)
		{
			if (string.IsNullOrEmpty(record.InstanceId))
			{
				result.Remote = RemoteRelease.Failed;
			}
			else
			{
				status = await PostReleaseAsync(record.LicenseKey, record.InstanceId);
				if (IsSuccess(status))
				{
					result.Remote = RemoteRelease.Released;
				}
				else
				{
					result.Remote = RemoteRelease.Failed;
					PendingSeatRelease pendingSeatRelease = new PendingSeatRelease();
					pendingSeatRelease.LicenseKey = record.LicenseKey;
					pendingSeatRelease.InstanceId = record.InstanceId;
					pendingSeatRelease.Attempts = 1;
					pendingSeatRelease.LastAttemptUtcTicks = DateTime.UtcNow.Ticks;
					pendingSeatRelease.LastStatusCode = status;
					pendingSeatRelease.NeedsAttention = IsDefinitiveRejection(status);
					PendingSeatRelease entry = pendingSeatRelease;
					if (!AddPendingRelease(entry))
					{
						result.Message = "Ayn could not reach the license server and could not save a retry on this PC, so the license stays active here. Please try again later.";
						lock (_stateLock)
						{
							_notice = result.Message;
						}
						NotifyLicenseChanged();
						return result;
					}
					result.ReleasePending = true;
				}
			}
		}
		CredentialResult cleared = null;
		bool changed;
		lock (_stateLock)
		{
			changed = !object.ReferenceEquals(_storedRecord, record);
			if (!changed)
			{
				cleared = ClearStoredRecord();
			}
		}
		result.LocalCleared = !changed && cleared.Committed;
		if (changed)
		{
			result.Message = "The license changed while this PC was being deactivated, so nothing was removed. Please try again.";
		}
		else if (!result.LocalCleared)
		{
			result.Message = "Ayn could not fully remove the license from this PC. It will retry automatically.";
		}
		else if (result.Remote == RemoteRelease.Released)
		{
			result.Message = "This PC was deactivated and its license seat was released.";
		}
		else if (result.ReleasePending && IsDefinitiveRejection(status))
		{
			result.Message = "The license was removed from this PC, but the license server did not confirm releasing its seat (HTTP " + status + "). If another PC reports the device limit, contact support.";
		}
		else if (result.ReleasePending)
		{
			result.Message = "The license was removed from this PC. The license server could not be reached to release its seat yet; Ayn keeps retrying automatically, and you can retry from Settings > License.";
		}
		else if (result.Remote == RemoteRelease.Failed)
		{
			result.Message = "The license was removed from this PC, but Ayn has no activation ID for it, so it cannot release the seat. If another PC reports the device limit, contact support.";
		}
		else
		{
			result.Message = "The license was removed from this PC.";
		}
		lock (_stateLock)
		{
			_notice = (result.Success ? null : result.Message);
		}
		NotifyLicenseChanged();
		if (result.LocalCleared)
		{
			try
			{
				EnsureAynAnchor();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Ayn check after deactivation (" + ex.GetType().Name + ").");
			}
		}
		return result;
	}

	private void LoadPendingReleases()
	{
		CredentialResult credentialResult = CredentialStore.Read(ReleaseTarget);
		_releaseStoreState = credentialResult.State;
		if (string.IsNullOrEmpty(credentialResult.Value))
		{
			return;
		}
		try
		{
			PendingSeatReleaseList pendingSeatReleaseList = new JavaScriptSerializer().Deserialize<PendingSeatReleaseList>(credentialResult.Value);
			if (pendingSeatReleaseList != null && pendingSeatReleaseList.Entries != null)
			{
				_pendingReleases = pendingSeatReleaseList.Entries.Where((PendingSeatRelease e) => e != null && !string.IsNullOrEmpty(e.LicenseKey) && !string.IsNullOrEmpty(e.InstanceId)).ToList();
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Pending seat-release record is unreadable (" + ex.GetType().Name + "); it was kept.");
			_releaseStoreState = CredentialState.Corrupt;
		}
	}

	private bool StorePendingReleases(List<PendingSeatRelease> list)
	{
		CredentialResult credentialResult = ((list.Count == 0) ? CredentialStore.Delete(ReleaseTarget) : CredentialStore.Save(ReleaseTarget, new JavaScriptSerializer().Serialize(new PendingSeatReleaseList
		{
			Entries = list
		})));
		if (credentialResult.Committed)
		{
			_releaseStoreState = ((list.Count != 0) ? credentialResult.State : CredentialState.Absent);
		}
		return credentialResult.Committed;
	}

	private bool AddPendingRelease(PendingSeatRelease entry)
	{
		lock (_stateLock)
		{
			List<PendingSeatRelease> list = _pendingReleases.Where((PendingSeatRelease e) => !string.Equals(e.InstanceId, entry.InstanceId, StringComparison.Ordinal)).ToList();
			list.Add(entry);
			if (!StorePendingReleases(list))
			{
				return false;
			}
			_pendingReleases = list;
			_releaseListDirty = false;
			return true;
		}
	}

	private void RemovePendingRelease(PendingSeatRelease entry)
	{
		lock (_stateLock)
		{
			_pendingReleases = _pendingReleases.Where((PendingSeatRelease e) => !string.Equals(e.InstanceId, entry.InstanceId, StringComparison.Ordinal)).ToList();
			_releaseListDirty = !StorePendingReleases(_pendingReleases);
		}
	}

	public async Task<SeatReleaseState> RetryPendingReleasesAsync(bool manual)
	{
		if (Interlocked.CompareExchange(ref _operationPending, 1, 0) != 0)
		{
			return PendingSeatReleaseState;
		}
		try
		{
			return await RetryPendingReleasesCoreAsync(manual);
		}
		finally
		{
			Interlocked.Exchange(ref _operationPending, 0);
		}
	}

	private async Task<SeatReleaseState> RetryPendingReleasesCoreAsync(bool manual)
	{
		List<PendingSeatRelease> snapshot;
		lock (_stateLock)
		{
			snapshot = _pendingReleases.ToList();
		}
		Dictionary<string, int> outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (PendingSeatRelease entry in snapshot)
		{
			if (!entry.NeedsAttention || manual)
			{
				outcomes[entry.InstanceId] = await PostReleaseAsync(entry.LicenseKey, entry.InstanceId);
			}
		}
		if (outcomes.Count == 0)
		{
			return PendingSeatReleaseState;
		}
		lock (_stateLock)
		{
			bool flag = false;
			List<PendingSeatRelease> list = new List<PendingSeatRelease>();
			foreach (PendingSeatRelease pendingRelease in _pendingReleases)
			{
				if (!outcomes.TryGetValue(pendingRelease.InstanceId, out var value))
				{
					list.Add(pendingRelease);
					continue;
				}
				if (IsSuccess(value))
				{
					flag = true;
					continue;
				}
				list.Add(new PendingSeatRelease
				{
					LicenseKey = pendingRelease.LicenseKey,
					InstanceId = pendingRelease.InstanceId,
					Attempts = pendingRelease.Attempts + 1,
					LastAttemptUtcTicks = DateTime.UtcNow.Ticks,
					LastStatusCode = value,
					NeedsAttention = IsDefinitiveRejection(value)
				});
			}
			_pendingReleases = list;
			_releaseListDirty = !StorePendingReleases(list);
			_notice = ((flag && list.Count == 0) ? "The license seat of this PC's previous activation was released." : null);
		}
		NotifyLicenseChanged();
		return PendingSeatReleaseState;
	}

	private void StartPeriodicValidation()
	{
		Task.Run(async delegate
		{
			try
			{
				await Task.Delay(3000);
				await RetryPendingReleasesAsync(manual: false);
				await ValidateStoredLicenseAsync();
			}
			catch
			{
			}
		});
		try
		{
			_validationTimer = new Timer(async delegate
			{
				try
				{
					await RetryPendingReleasesAsync(manual: false);
					await ValidateStoredLicenseAsync();
				}
				catch
				{
				}
			}, null, TimeSpan.FromHours(24.0), TimeSpan.FromHours(24.0));
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not start 24h validation timer: " + ex.Message);
		}
	}

	private void LoadStoredRecord()
	{
		CredentialResult credentialResult = CredentialStore.Read("Ayn_LicenseRecord");
		_recordState = credentialResult.State;
		_storedRecord = null;
		if (string.IsNullOrEmpty(credentialResult.Value))
		{
			return;
		}
		try
		{
			_storedRecord = new JavaScriptSerializer().Deserialize<StoredLicenseRecord>(credentialResult.Value);
			if (_storedRecord == null)
			{
				throw new InvalidDataException("Empty license record.");
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Stored license record is unreadable (" + ex.GetType().Name + "); it was kept for recovery.");
			_storedRecord = null;
			_recordState = CredentialState.Corrupt;
		}
	}

	private CredentialResult PersistStoredRecord(StoredLicenseRecord record)
	{
		CredentialResult credentialResult = CredentialStore.Save("Ayn_LicenseRecord", new JavaScriptSerializer().Serialize(record));
		if (credentialResult.Committed)
		{
			_storedRecord = record;
			_pendingRecord = null;
			_deletionPending = false;
			_recordState = credentialResult.State;
		}
		return credentialResult;
	}

	private CredentialResult ClearStoredRecord()
	{
		_storedRecord = null;
		_pendingRecord = null;
		CredentialResult credentialResult = CredentialStore.Delete("Ayn_LicenseRecord");
		_deletionPending = !credentialResult.Committed;
		_recordState = ((!credentialResult.Committed) ? credentialResult.State : CredentialState.Absent);
		return credentialResult;
	}

	private bool AdoptActivatedRecord(StoredLicenseRecord record)
	{
		lock (_stateLock)
		{
			_notice = null;
			if (PersistStoredRecord(record).Committed || PersistStoredRecord(record).Committed)
			{
				return true;
			}
			_storedRecord = record;
			_pendingRecord = record;
			_pendingIsRefresh = false;
			return false;
		}
	}

	private void RecordValidated(StoredLicenseRecord validated, long validatedUtcTicks)
	{
		lock (_stateLock)
		{
			if (object.ReferenceEquals(_storedRecord, validated))
			{
				StoredLicenseRecord storedLicenseRecord = new StoredLicenseRecord();
				storedLicenseRecord.LicenseKey = validated.LicenseKey;
				storedLicenseRecord.InstanceId = validated.InstanceId;
				storedLicenseRecord.LastValidatedUtcTicks = validatedUtcTicks;
				StoredLicenseRecord storedLicenseRecord2 = storedLicenseRecord;
				if (!PersistStoredRecord(storedLicenseRecord2).Committed)
				{
					_storedRecord = storedLicenseRecord2;
					_pendingRecord = storedLicenseRecord2;
					_pendingIsRefresh = true;
				}
			}
		}
	}

	private void RevokeIfCurrent(StoredLicenseRecord revoked)
	{
		lock (_stateLock)
		{
			if (object.ReferenceEquals(_storedRecord, revoked))
			{
				ClearStoredRecord();
				_notice = "This PC's license is no longer active: the key was refunded or disabled, or this PC's activation was removed. Enter a key to activate again, or email contact@modistudio.online.";
			}
		}
	}

	public CredentialResult RetryPersistence()
	{
		lock (_stateLock)
		{
			if (_releaseListDirty)
			{
				_releaseListDirty = !StorePendingReleases(_pendingReleases);
			}
			if (_pendingRecord != null)
			{
				return PersistStoredRecord(_pendingRecord);
			}
			if (_deletionPending)
			{
				return ClearStoredRecord();
			}
			CredentialResult credentialResult = CredentialStore.Reconcile("Ayn_LicenseRecord");
			if (_storedRecord == null && (_recordState == CredentialState.Unavailable || _recordState == CredentialState.Corrupt || _recordState == CredentialState.Degraded))
			{
				LoadStoredRecord();
			}
			else if (credentialResult.State != CredentialState.Failed)
			{
				_recordState = credentialResult.State;
			}
			return credentialResult;
		}
	}

	private void NotifyLicenseChanged()
	{
		EventHandler licenseChanged = this.LicenseChanged;
		if (licenseChanged != null)
		{
			try
			{
				licenseChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}
}
