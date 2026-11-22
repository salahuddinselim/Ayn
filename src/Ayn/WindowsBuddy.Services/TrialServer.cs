using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class AynServer
{
	internal sealed class DeviceIdentity
	{
		public SortedDictionary<string, string> Factors = new SortedDictionary<string, string>(StringComparer.Ordinal);

		public string Composite;
	}

	private const string SignedPrefix = "ayn-trial-v1|";

	private const string SignedPrefixV2 = "ayn-trial-v2|";

	private const string DeviceSalt = "ayn-trial-device-v1|";

	private const string FactorSalt = "ayn-trial-factor-v2|";

	private const string CompositeSalt = "ayn-trial-device-v2|";

	private const string DefaultUrl = "https://github.com/salahuddinselim/Ayn";

	private const string DefaultPublicKey = "5zoraaMNaQLPTj1LNozJkF+et8caumnoJ7hyxm/Kszc=";

	public const string UrlVariable = "VAYLI_TRIAL_URL";

	public const string DeviceIdVariable = "VAYLI_TRIAL_DEVICE_ID";

	public const string PublicKeyVariable = "VAYLI_TRIAL_PUBLIC_KEY";

	internal static readonly string[] HardwareFactorNames = new string[4] { "smbios", "bios", "board", "disk" };

	internal static Func<string, string, Task<string>> Transport = PostJsonAsync;

	internal static Func<string> MachineIdProvider = ReadMachineId;

	internal static Func<IDictionary<string, string>> HardwareProvider = ReadHardware;

	internal static Func<DateTime> LocalClock = () => DateTime.UtcNow;

	internal static Func<long> MonotonicMs = () => Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);

	internal static Func<long> LocalStartMsProvider = () => 0L;

	internal static int RequestTimeoutMs = 10000;

	internal static int HardwareTimeoutMs = 8000;

	internal static string UrlOverride;

	internal static byte[] PublicKeyOverride;

	private static readonly string[] JunkFragments = new string[16]
	{
		"to be filled", "o.e.m", "oem", "default string", "not applicable", "not specified", "system serial", "chassis serial", "base board serial", "serial number",
		"none", "n/a", "123456789", "invalid", "unknown", "empty"
	};

	private static readonly string[] JunkUuids = new string[5] { "00000000-0000-0000-0000-000000000000", "ffffffff-ffff-ffff-ffff-ffffffffffff", "03000200-0400-0500-0006-000700080009", "00020003-0004-0005-0006-000700080009", "12345678-1234-5678-90ab-cddeefaabbcc" };

	private static string _deviceHash;

	private static readonly object Sync = new object();

	private static SignedAynAnchor _anchor;

	private static bool _loaded;

	private static long _sessionServerMs;

	private static long _sessionMonotonicMs;

	private static bool _hasSession;

	private static int _lastFailure;

	internal static Func<bool> NetworkAvailable = delegate
	{
		try
		{
			return NetworkInterface.GetIsNetworkAvailable();
		}
		catch
		{
			return true;
		}
	};

	private static readonly int[] RetryDelaysMs = new int[5] { 30000, 120000, 600000, 1800000, 3600000 };

	private static int _syncRunning;

	private static readonly AutoResetEvent NetworkWake = new AutoResetEvent(initialState: false);

	private static int _networkHooked;

	internal static Action<int> RetryDelay = delegate(int ms)
	{
		NetworkWake.WaitOne(ms);
	};

	internal static int PendingWindowMs = 20000;

	private static int _checkState;

	private static long _pendingSinceTicks;

	internal static byte[] PublicKeyForQa => PublicKey;

	public static string Url
	{
		get
		{
			if (UrlOverride != null)
			{
				return UrlOverride;
			}
			return "https://github.com/salahuddinselim/Ayn";
		}
	}

	private static byte[] PublicKey
	{
		get
		{
			if (PublicKeyOverride != null)
			{
				return PublicKeyOverride;
			}
			if ("5zoraaMNaQLPTj1LNozJkF+et8caumnoJ7hyxm/Kszc=".Length != 0)
			{
				return Convert.FromBase64String("5zoraaMNaQLPTj1LNozJkF+et8caumnoJ7hyxm/Kszc=");
			}
			return null;
		}
	}

	public static bool Enabled
	{
		get
		{
			if (!string.IsNullOrEmpty(Url))
			{
				return PublicKey != null;
			}
			return false;
		}
	}

	public static string DeviceHash
	{
		get
		{
			if (_deviceHash != null)
			{
				return _deviceHash;
			}
			return _deviceHash = Sha256Hex("ayn-trial-device-v1|" + MachineIdProvider());
		}
	}

	private static string AnchorPath => Path.Combine(AppPaths.DataDir, "trial", "server-trial.json");

	public static SignedAynAnchor Current
	{
		get
		{
			lock (Sync)
			{
				if (!_loaded)
				{
					_loaded = true;
					_anchor = LoadStored();
				}
				return _anchor;
			}
		}
	}

	public static DateTime? SessionServerNowUtc
	{
		get
		{
			lock (Sync)
			{
				if (!_hasSession)
				{
					return null;
				}
				long num = Math.Max(0L, MonotonicMs() - _sessionMonotonicMs);
				return FromUnixMs(_sessionServerMs + num);
			}
		}
	}

	public static bool LastAttemptOffline => Volatile.Read(ref _lastFailure) == 1;

	public static bool LastAttemptServerUnreachable => Volatile.Read(ref _lastFailure) == 2;

	public static bool FirstCheckRunning
	{
		get
		{
			if (Volatile.Read(ref _syncRunning) == 1 && Volatile.Read(ref _lastFailure) == 0)
			{
				return Current == null;
			}
			return false;
		}
	}

	public static bool CheckPending
	{
		get
		{
			if (Volatile.Read(ref _checkState) == 1)
			{
				return DateTime.UtcNow.Ticks - Interlocked.Read(ref _pendingSinceTicks) < TimeSpan.FromMilliseconds(PendingWindowMs).Ticks;
			}
			return false;
		}
	}

	private static string SimulatedMachineId()
	{
		return null;
	}

	private static string ReadMachineId()
	{
		string text = SimulatedMachineId();
		if (text != null)
		{
			return text;
		}
		using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
		using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography", writable: false);
		object obj = registryKey2?.GetValue("MachineGuid");
		if (obj == null)
		{
			throw new InvalidOperationException("MachineGuid unavailable.");
		}
		return obj.ToString();
	}

	private static string Wmi(string query)
	{
		try
		{
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(query);
			using ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get();
			foreach (ManagementBaseObject item in managementObjectCollection)
			{
				using (item)
				{
					foreach (PropertyData property in item.Properties)
					{
						if (property.Value != null)
						{
							return Convert.ToString(property.Value, CultureInfo.InvariantCulture);
						}
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static IDictionary<string, string> ReadHardware()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string text = SimulatedMachineId();
		if (text != null)
		{
			string[] hardwareFactorNames = HardwareFactorNames;
			foreach (string text2 in hardwareFactorNames)
			{
				dictionary[text2] = text + "|" + text2;
			}
			return dictionary;
		}
		dictionary["smbios"] = Wmi("SELECT UUID FROM Win32_ComputerSystemProduct");
		dictionary["bios"] = Wmi("SELECT SerialNumber FROM Win32_BIOS");
		dictionary["board"] = Wmi("SELECT SerialNumber FROM Win32_BaseBoard");
		try
		{
			string text3 = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").TrimEnd('\\');
			if (text3.Length == 2 && text3[1] == ':' && char.IsLetter(text3[0]))
			{
				using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + text3 + "'} WHERE AssocClass=Win32_LogicalDiskToPartition");
				using ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get();
				foreach (ManagementObject item in managementObjectCollection)
				{
					using (item)
					{
						using ManagementObjectCollection managementObjectCollection2 = item.GetRelated("Win32_DiskDrive");
						foreach (ManagementObject item2 in managementObjectCollection2)
						{
							using (item2)
							{
								if (!dictionary.ContainsKey("disk"))
								{
									dictionary["disk"] = Convert.ToString(item2["SerialNumber"]);
								}
							}
						}
					}
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	internal static string NormalizeFactor(string name, string value)
	{
		if (value == null)
		{
			return null;
		}
		string text = value.Trim();
		char[] trimChars = new char[1];
		string text2 = text.Trim(trimChars).Trim();
		if (text2.Length < 4)
		{
			return null;
		}
		string text3 = text2.ToLowerInvariant();
		if (text3.Distinct().Count() <= 2)
		{
			return null;
		}
		string[] junkFragments = JunkFragments;
		foreach (string value2 in junkFragments)
		{
			if (text3.Contains(value2))
			{
				return null;
			}
		}
		if (name == "smbios" && JunkUuids.Contains(text3))
		{
			return null;
		}
		return text2.ToUpperInvariant();
	}

	private static string Sha256Hex(string text)
	{
		using SHA256 sHA = SHA256.Create();
		byte[] array = sHA.ComputeHash(Encoding.UTF8.GetBytes(text));
		StringBuilder stringBuilder = new StringBuilder(64);
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			stringBuilder.Append(b.ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	internal static string CompositeOf(IDictionary<string, string> factors)
	{
		return Sha256Hex("ayn-trial-device-v2|" + string.Join(";", from kv in factors.OrderBy((KeyValuePair<string, string> kv) => kv.Key, StringComparer.Ordinal)
			select kv.Key + "=" + kv.Value));
	}

	internal static DeviceIdentity ComputeIdentity()
	{
		DeviceIdentity deviceIdentity = new DeviceIdentity();
		deviceIdentity.Factors["mg"] = DeviceHash;
		IDictionary<string, string> dictionary = null;
		try
		{
			Task<IDictionary<string, string>> task = Task.Run(() => HardwareProvider());
			if (task.Wait(HardwareTimeoutMs))
			{
				dictionary = task.Result;
			}
			else
			{
				TransactionLogger.Warn("Ayn device ids: hardware read timed out; using the Windows id only.");
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Ayn device ids: hardware read failed (" + ex.GetType().Name + ").");
		}
		if (dictionary != null)
		{
			string[] hardwareFactorNames = HardwareFactorNames;
			foreach (string text in hardwareFactorNames)
			{
				if (dictionary.TryGetValue(text, out var value))
				{
					string text2 = NormalizeFactor(text, value);
					if (text2 != null)
					{
						deviceIdentity.Factors[text] = Sha256Hex("ayn-trial-factor-v2|" + text + "|" + text2);
					}
				}
			}
		}
		deviceIdentity.Composite = CompositeOf(deviceIdentity.Factors);
		return deviceIdentity;
	}

	internal static string SignedMessage(SignedAynAnchor a)
	{
		if (a.Version >= 2)
		{
			return "ayn-trial-v2|" + a.DeviceHash + "|" + a.Mg + "|" + a.AynStartMs + "|" + a.ServerTimeMs + "|" + a.Nonce;
		}
		return "ayn-trial-v1|" + a.DeviceHash + "|" + a.AynStartMs + "|" + a.ServerTimeMs;
	}

	internal static bool VerifyAnchor(SignedAynAnchor anchor, string expectedDeviceHash)
	{
		if (anchor == null || PublicKey == null || string.IsNullOrEmpty(anchor.Signature))
		{
			return false;
		}
		if (anchor.Version >= 2)
		{
			if (anchor.Version != 2 || !IsHex(anchor.DeviceHash, 64) || !IsHex(anchor.Nonce, 32))
			{
				return false;
			}
			if (!string.Equals(anchor.Mg, expectedDeviceHash, StringComparison.Ordinal))
			{
				return false;
			}
		}
		else if (!string.Equals(anchor.DeviceHash, expectedDeviceHash, StringComparison.Ordinal))
		{
			return false;
		}
		if (anchor.AynStartMs <= 0 || anchor.ServerTimeMs < anchor.AynStartMs)
		{
			return false;
		}
		byte[] signature;
		try
		{
			signature = Convert.FromBase64String(anchor.Signature);
		}
		catch (FormatException)
		{
			return false;
		}
		return Ed25519.Verify(signature, Encoding.UTF8.GetBytes(SignedMessage(anchor)), PublicKey);
	}

	private static bool IsHex(string s, int length)
	{
		if (s == null || s.Length != length)
		{
			return false;
		}
		foreach (char c in s)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
			{
				return false;
			}
		}
		return true;
	}

	private static SignedAynAnchor LoadStored()
	{
		if (!Enabled)
		{
			return null;
		}
		try
		{
			if (!File.Exists(AnchorPath))
			{
				return null;
			}
			SignedAynAnchor signedAynAnchor = new JavaScriptSerializer().Deserialize<SignedAynAnchor>(PersistenceHelper.ReadShared(AnchorPath));
			if (VerifyAnchor(signedAynAnchor, DeviceHash))
			{
				return signedAynAnchor;
			}
			TransactionLogger.Warn("Stored trial anchor rejected (signature or device mismatch); it will be fetched again.");
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Stored trial anchor unreadable (" + ex.GetType().Name + ").");
		}
		return null;
	}

	public static DateTime FromUnixMs(long ms)
	{
		return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms);
	}

	private static async Task<string> PostJsonAsync(string url, string json)
	{
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
		request.Method = "POST";
		request.ContentType = "application/json";
		request.Timeout = 8000;
		byte[] body = Encoding.UTF8.GetBytes(json);
		using (Stream stream = await request.GetRequestStreamAsync())
		{
			await stream.WriteAsync(body, 0, body.Length);
		}
		HttpWebResponse httpWebResponse = (HttpWebResponse)(await request.GetResponseAsync());
		using HttpWebResponse response = httpWebResponse;
		using StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
		return await reader.ReadToEndAsync();
	}

	private static async Task<string> PostBounded(string json)
	{
		Task<string> pending = Transport(Url, json);
		if (await Task.WhenAny(pending, Task.Delay(RequestTimeoutMs)) != pending)
		{
			throw new TimeoutException("trial server did not answer");
		}
		return await pending;
	}

	internal static int HttpStatusOf(Exception ex)
	{
		for (Exception ex2 = ex; ex2 != null; ex2 = ex2.InnerException)
		{
			HttpWebResponse httpWebResponse = ((ex2 is WebException ex3) ? (ex3.Response as HttpWebResponse) : null);
			if (httpWebResponse != null)
			{
				return (int)httpWebResponse.StatusCode;
			}
			if (ex2.Data.Contains("HttpStatus") && ex2.Data["HttpStatus"] is int)
			{
				return (int)ex2.Data["HttpStatus"];
			}
		}
		return 0;
	}

	private static string NewNonce()
	{
		byte[] array = new byte[16];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		return string.Concat(array.Select((byte b) => b.ToString("x2")));
	}

	private static SignedAynAnchor ParseAnswer(string json)
	{
		Dictionary<string, object> dictionary = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
		object value;
		int num = ((!dictionary.TryGetValue("v", out value) || value == null) ? 1 : Convert.ToInt32(value));
		SignedAynAnchor signedAynAnchor = new SignedAynAnchor();
		signedAynAnchor.Version = ((num < 2) ? 1 : num);
		signedAynAnchor.DeviceHash = dictionary["device_hash"] as string;
		signedAynAnchor.Mg = (dictionary.TryGetValue("mg", out var value2) ? (value2 as string) : null);
		signedAynAnchor.Nonce = (dictionary.TryGetValue("nonce", out var value3) ? (value3 as string) : null);
		signedAynAnchor.AynStartMs = Convert.ToInt64(dictionary["trial_start"]);
		signedAynAnchor.ServerTimeMs = Convert.ToInt64(dictionary["server_time"]);
		signedAynAnchor.Signature = dictionary["sig"] as string;
		return signedAynAnchor;
	}

	public static async Task<bool> SyncOnceAsync()
	{
		if (!Enabled)
		{
			return false;
		}
		string hash;
		try
		{
			hash = DeviceHash;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Ayn device id unavailable (" + ex.GetType().Name + ").");
			return false;
		}
		DeviceIdentity identity = ComputeIdentity();
		SignedAynAnchor previous = Current;
		if (previous != null && previous.Version >= 2 && !string.Equals(previous.DeviceHash, identity.Composite, StringComparison.Ordinal))
		{
			TransactionLogger.Warn("Stored trial anchor belongs to other hardware; it is ignored and fetched again.");
			lock (Sync)
			{
				_anchor = null;
			}
			previous = null;
		}
		string nonce = NewNonce();
		SignedAynAnchor answer;
		try
		{
			Dictionary<string, object> fields = new Dictionary<string, object>
			{
				{ "v", 2 },
				{ "device_hash", identity.Composite },
				{
					"factors",
					new Dictionary<string, object>(((IEnumerable<KeyValuePair<string, string>>)identity.Factors).ToDictionary((Func<KeyValuePair<string, string>, string>)((KeyValuePair<string, string> kv) => kv.Key), (Func<KeyValuePair<string, string>, object>)((KeyValuePair<string, string> kv) => kv.Value)))
				},
				{ "nonce", nonce }
			};
			long localStart = 0L;
			try
			{
				localStart = LocalStartMsProvider();
			}
			catch
			{
			}
			if (localStart > 0 && previous == null)
			{
				fields["local_start_ms"] = localStart;
			}
			string text = null;
			Exception first = null;
			try
			{
				text = await PostBounded(new JavaScriptSerializer().Serialize(fields));
			}
			catch (Exception ex2)
			{
				first = ex2;
			}
			if (first != null)
			{
				int status = HttpStatusOf(first);
				if ((status != 400 && status != 413) || (previous != null && previous.Version >= 2))
				{
					throw first;
				}
				Dictionary<string, object> legacy = new Dictionary<string, object> { { "device_hash", hash } };
				if (fields.ContainsKey("local_start_ms"))
				{
					legacy["local_start_ms"] = fields["local_start_ms"];
				}
				text = await PostBounded(new JavaScriptSerializer().Serialize(legacy));
			}
			answer = ParseAnswer(text);
		}
		catch (Exception ex3)
		{
			Volatile.Write(ref _lastFailure, IsOfflineFailure(ex3) ? 1 : 2);
			TransactionLogger.Info("Ayn server unreachable (" + ex3.GetType().Name + "); will retry.");
			return false;
		}
		Volatile.Write(ref _lastFailure, 0);
		if (answer.Version >= 2)
		{
			if (!VerifyAnchor(answer, hash) || !string.Equals(answer.DeviceHash, identity.Composite, StringComparison.Ordinal) || !string.Equals(answer.Nonce, nonce, StringComparison.Ordinal))
			{
				TransactionLogger.Warn("Ayn server answer rejected: invalid signature, device mismatch or not an answer to this request.");
				return false;
			}
		}
		else if (!VerifyAnchor(answer, hash) || (previous != null && previous.Version >= 2))
		{
			TransactionLogger.Warn("Ayn server answer rejected: invalid signature, device mismatch or an older answer format.");
			return false;
		}
		long trustedServerMs = answer.ServerTimeMs;
		if (answer.Version < 2 && previous != null)
		{
			trustedServerMs = Math.Max(trustedServerMs, previous.ServerTimeMs);
		}
		lock (Sync)
		{
			_loaded = true;
			if (_anchor == null || answer.Version > _anchor.Version || answer.ServerTimeMs >= _anchor.ServerTimeMs)
			{
				_anchor = answer;
			}
			_sessionServerMs = trustedServerMs;
			_sessionMonotonicMs = MonotonicMs();
			_hasSession = true;
		}
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(AnchorPath));
			PersistenceHelper.WriteAllTextOrThrow(AnchorPath, new JavaScriptSerializer().Serialize(_anchor), keepBackup: false);
		}
		catch (Exception ex4)
		{
			TransactionLogger.Warn("Ayn anchor not saved (" + ex4.GetType().Name + "); it is fetched again next time.");
		}
		string startedUtc = FromUnixMs(answer.AynStartMs).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
		TransactionLogger.Info((previous == null && answer.ServerTimeMs - answer.AynStartMs <= 60000) ? ("Ayn started (server trial_start " + startedUtc + ").") : ("Existing trial resumed (started " + startedUtc + ")."));
		return true;
	}

	internal static bool IsOfflineFailure(Exception ex)
	{
		if (!NetworkAvailable())
		{
			return true;
		}
		for (Exception ex2 = ex; ex2 != null; ex2 = ex2.InnerException)
		{
			if (ex2 is WebException ex3 && (ex3.Status == WebExceptionStatus.NameResolutionFailure || ex3.Status == WebExceptionStatus.ProxyNameResolutionFailure))
			{
				return true;
			}
		}
		return false;
	}

	public static void RetryNow()
	{
		NetworkWake.Set();
	}

	private static void HookNetworkWake()
	{
		if (Interlocked.Exchange(ref _networkHooked, 1) != 0)
		{
			return;
		}
		try
		{
			NetworkChange.NetworkAvailabilityChanged += delegate(object s, NetworkAvailabilityEventArgs e)
			{
				if (e.IsAvailable)
				{
					NetworkWake.Set();
				}
			};
			NetworkChange.NetworkAddressChanged += delegate
			{
				NetworkWake.Set();
			};
		}
		catch
		{
		}
	}

	private static void EndPending(int state, Action notify)
	{
		if (Interlocked.CompareExchange(ref _checkState, state, 1) == 1 && notify != null)
		{
			try
			{
				notify();
			}
			catch
			{
			}
		}
	}

	public static void EnsureSyncInBackground(Func<bool> stillNeeded, Action onAnswered, Action onPendingEnded = null)
	{
		if (!Enabled || Interlocked.CompareExchange(ref _syncRunning, 1, 0) != 0)
		{
			return;
		}
		HookNetworkWake();
		if (Current == null)
		{
			Interlocked.Exchange(ref _pendingSinceTicks, DateTime.UtcNow.Ticks);
			Volatile.Write(ref _checkState, 1);
			Task.Delay(PendingWindowMs + 200).ContinueWith(delegate
			{
				EndPending(3, onPendingEnded);
			});
		}
		Task.Run(async delegate
		{
			int num = default(int);
			_ = num;
			_ = 0;
			try
			{
				_ = num;
				_ = 0;
				try
				{
					int attempt = 0;
					while (stillNeeded())
					{
						bool answered;
						using ((attempt == 0) ? BootTimeline.Service("trial server check") : null)
						{
							answered = await SyncOnceAsync();
						}
						if (answered)
						{
							Volatile.Write(ref _checkState, 2);
							if (onAnswered != null)
							{
								onAnswered();
							}
							break;
						}
						if (attempt == 0)
						{
							EndPending(3, onPendingEnded);
						}
						RetryDelay(RetryDelaysMs[Math.Min(attempt, RetryDelaysMs.Length - 1)]);
						attempt++;
					}
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Ayn sync stopped (" + ex.GetType().Name + ").");
				}
			}
			finally
			{
				Interlocked.Exchange(ref _syncRunning, 0);
			}
		});
	}

	internal static void ResetForQa()
	{
		lock (Sync)
		{
			_anchor = null;
			_loaded = false;
			_hasSession = false;
			_sessionServerMs = 0L;
			_sessionMonotonicMs = 0L;
		}
		Volatile.Write(ref _checkState, 0);
		PendingWindowMs = 20000;
		Volatile.Write(ref _lastFailure, 0);
		_deviceHash = null;
	}
}
