using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Clock;

public class ClockSettings
{
	internal const int CurrentSchema = 2;

	public const int MaxWorldClocks = 5;

	private static ClockSettings _instance;

	private static readonly object _syncLock = new object();

	private readonly string _settingsFilePath;

	private ClockSettingsModel _model;

	private readonly object _queueLock = new object();

	private readonly object _writeLock = new object();

	private string _pendingJson;

	private long _saveSeq;

	private long _pendingSeq;

	private long _writtenSeq;

	private bool _writerQueued;

	private bool _exitHooked;

	private static bool _sysH24;

	private static long _sysReadAt;

	private static bool _localeHooked;

	private bool _loadFailed;

	public static ClockSettings Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_syncLock)
				{
					if (_instance == null)
					{
						_instance = new ClockSettings();
					}
				}
			}
			return _instance;
		}
	}

	public ClockFormat Format
	{
		get
		{
			if (!_model.FollowSystemFormat)
			{
				return _model.Format;
			}
			return SystemFormat();
		}
		set
		{
			if (_model.Format != value || _model.FollowSystemFormat)
			{
				_model.Format = value;
				_model.FollowSystemFormat = false;
				Save();
				NotifyChanged();
			}
		}
	}

	public bool FollowSystemFormat
	{
		get
		{
			return _model.FollowSystemFormat;
		}
		set
		{
			if (_model.FollowSystemFormat != value)
			{
				_model.FollowSystemFormat = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public static bool Uses24Hour
	{
		get
		{
			try
			{
				return Instance.Format == ClockFormat.TwentyFourHour;
			}
			catch
			{
				return SystemFormat() == ClockFormat.TwentyFourHour;
			}
		}
	}

	public ClockDisplayMode DisplayMode
	{
		get
		{
			return _model.DisplayMode;
		}
		set
		{
			if (_model.DisplayMode != value)
			{
				_model.DisplayMode = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public bool ShowSeconds
	{
		get
		{
			return _model.ShowSeconds;
		}
		set
		{
			if (_model.ShowSeconds != value)
			{
				_model.ShowSeconds = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public bool ShowDate
	{
		get
		{
			return _model.ShowDate;
		}
		set
		{
			if (_model.ShowDate != value)
			{
				_model.ShowDate = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public ClockDateFormat DateFormat
	{
		get
		{
			return _model.DateFormat;
		}
		set
		{
			if (_model.DateFormat != value)
			{
				_model.DateFormat = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public bool AnalogShowSecondHand
	{
		get
		{
			return _model.AnalogShowSecondHand;
		}
		set
		{
			if (_model.AnalogShowSecondHand != value)
			{
				_model.AnalogShowSecondHand = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public bool AnalogShowNumerals
	{
		get
		{
			return _model.AnalogShowNumerals;
		}
		set
		{
			if (_model.AnalogShowNumerals != value)
			{
				_model.AnalogShowNumerals = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public bool AnalogShowMinuteTicks
	{
		get
		{
			return _model.AnalogShowMinuteTicks;
		}
		set
		{
			if (_model.AnalogShowMinuteTicks != value)
			{
				_model.AnalogShowMinuteTicks = value;
				Save();
				NotifyChanged();
			}
		}
	}

	public bool ShowInNotch
	{
		get
		{
			return NotchConfig.Instance.ShowTime;
		}
		set
		{
			NotchConfig.Instance.ShowTime = value;
		}
	}

	public IReadOnlyList<WorldClockCity> WorldClocks => _model.WorldClocks.AsReadOnly();

	public bool CanAddWorldClock => _model.WorldClocks.Count < 5;

	public event EventHandler SettingsChanged;

	public static ClockFormat SystemFormat()
	{
		try
		{
			long ticks = DateTime.UtcNow.Ticks;
			if (_sysReadAt == 0)
			{
				string text = null;
				try
				{
					using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\International", writable: false);
					if (registryKey != null)
					{
						text = (registryKey.GetValue("sShortTime") ?? registryKey.GetValue("sTimeFormat")) as string;
					}
				}
				catch
				{
				}
				if (string.IsNullOrEmpty(text))
				{
					text = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern ?? string.Empty;
				}
				_sysH24 = text.Contains("H");
				_sysReadAt = ticks;
			}
			return _sysH24 ? ClockFormat.TwentyFourHour : ClockFormat.TwelveHour;
		}
		catch
		{
			return ClockFormat.TwelveHour;
		}
	}

	private void HookLocaleChanges()
	{
		if (_localeHooked)
		{
			return;
		}
		_localeHooked = true;
		try
		{
			SystemEvents.UserPreferenceChanged += delegate(object s, UserPreferenceChangedEventArgs e)
			{
				if (e.Category == UserPreferenceCategory.Locale)
				{
					_sysReadAt = 0L;
					try
					{
						CultureInfo.CurrentCulture.ClearCachedData();
					}
					catch
					{
					}
					if (_model.FollowSystemFormat)
					{
						Application current = Application.Current;
						if (current != null && !current.Dispatcher.CheckAccess())
						{
							current.Dispatcher.BeginInvoke(new Action(NotifyChanged));
						}
						else
						{
							NotifyChanged();
						}
					}
				}
			};
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Region change notifications unavailable: " + ex.Message);
		}
	}

	private ClockSettings()
	{
		string localAppData = AppPaths.LocalAppData;
		string text = Path.Combine(localAppData, AppPaths.FolderName, "settings");
		if (!Directory.Exists(text))
		{
			try
			{
				Directory.CreateDirectory(text);
			}
			catch
			{
			}
		}
		_settingsFilePath = Path.Combine(text, "clock_settings.json");
		Load();
		PersistenceHelper.RegisterExitFlush("clock", SaveNow);
		HookLocaleChanges();
		HookExit();
	}

	private void Load()
	{
		ClockSettingsModel clockSettingsModel = null;
		string text = null;
		string[] array = new string[2]
		{
			_settingsFilePath,
			_settingsFilePath + ".bak"
		};
		foreach (string text2 in array)
		{
			try
			{
				text = ((text2 == _settingsFilePath) ? PersistenceHelper.SafeReadAllText(text2) : (File.Exists(text2) ? PersistenceHelper.ReadShared(text2) : null));
				if (!string.IsNullOrWhiteSpace(text))
				{
					clockSettingsModel = new JavaScriptSerializer().Deserialize<ClockSettingsModel>(text);
					if (clockSettingsModel != null)
					{
						break;
					}
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Clock settings unreadable (" + ((text2 == _settingsFilePath) ? "file" : "backup") + "): " + ex.GetType().Name);
				clockSettingsModel = null;
			}
		}
		if (clockSettingsModel == null)
		{
			_loadFailed = File.Exists(_settingsFilePath) || File.Exists(_settingsFilePath + ".bak");
			_model = new ClockSettingsModel();
			return;
		}
		_model = clockSettingsModel;
		bool flag = false;
		if (_model.SchemaVersion < 2)
		{
			if (text.IndexOf("\"FollowSystemFormat\"", StringComparison.Ordinal) < 0)
			{
				_model.FollowSystemFormat = false;
			}
			_model.SchemaVersion = 2;
			flag = true;
		}
		Sanitize(_model);
		if (flag)
		{
			Save();
		}
	}

	private static void Sanitize(ClockSettingsModel m)
	{
		if (!Enum.IsDefined(typeof(ClockFormat), m.Format))
		{
			m.Format = ClockFormat.TwelveHour;
		}
		if (!Enum.IsDefined(typeof(ClockDisplayMode), m.DisplayMode))
		{
			m.DisplayMode = ClockDisplayMode.Digital;
		}
		if (!Enum.IsDefined(typeof(ClockDateFormat), m.DateFormat))
		{
			m.DateFormat = ClockDateFormat.Full;
		}
		List<WorldClockCity> list = new List<WorldClockCity>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		bool flag = false;
		if (m.WorldClocks != null)
		{
			foreach (WorldClockCity worldClock in m.WorldClocks)
			{
				if (worldClock == null)
				{
					continue;
				}
				if (worldClock.IsLocal)
				{
					if (!flag)
					{
						flag = true;
						worldClock.Id = (string.IsNullOrEmpty(worldClock.Id) ? "local" : worldClock.Id);
						worldClock.CityName = "Local";
						worldClock.TimeZoneId = TimeZoneInfo.Local.Id;
						list.Insert(0, worldClock);
					}
				}
				else if (!string.IsNullOrWhiteSpace(worldClock.TimeZoneId) && !string.IsNullOrWhiteSpace(worldClock.CityName))
				{
					if (string.IsNullOrEmpty(worldClock.Id))
					{
						worldClock.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
					}
					if (worldClock.CityName.Length > 40)
					{
						worldClock.CityName = worldClock.CityName.Substring(0, 40);
					}
					if (hashSet.Add(worldClock.CityName + "|" + worldClock.TimeZoneId))
					{
						list.Add(worldClock);
					}
				}
			}
		}
		if (!flag)
		{
			list.Insert(0, new WorldClockCity("local", "Local", TimeZoneInfo.Local.Id, isLocal: true));
		}
		m.WorldClocks = list;
	}

	public void Save()
	{
		Save(sync: false);
	}

	public void SaveNow()
	{
		Save(sync: true);
	}

	private void Save(bool sync)
	{
		if (_loadFailed)
		{
			return;
		}
		string text;
		try
		{
			text = new JavaScriptSerializer().Serialize(_model);
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_settingsFilePath, error);
			return;
		}
		long num;
		lock (_queueLock)
		{
			num = ++_saveSeq;
			if (sync)
			{
				_pendingJson = null;
			}
			else
			{
				_pendingJson = text;
				_pendingSeq = num;
				if (_writerQueued)
				{
					return;
				}
				_writerQueued = true;
			}
		}
		if (sync)
		{
			Write(text, num);
			return;
		}
		try
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				WriteQueued();
			});
		}
		catch (Exception ex)
		{
			lock (_queueLock)
			{
				_writerQueued = false;
				_pendingJson = null;
			}
			TransactionLogger.Warn("Clock settings save queue failed: " + ex.GetType().Name);
			Write(text, num);
		}
	}

	private void WriteQueued()
	{
		while (true)
		{
			string pendingJson;
			long pendingSeq;
			lock (_queueLock)
			{
				pendingJson = _pendingJson;
				pendingSeq = _pendingSeq;
				_pendingJson = null;
				if (pendingJson == null)
				{
					_writerQueued = false;
					break;
				}
			}
			Write(pendingJson, pendingSeq);
		}
	}

	private void Write(string json, long seq)
	{
		lock (_writeLock)
		{
			if (seq <= _writtenSeq)
			{
				return;
			}
			try
			{
				if (!PersistenceHelper.SafeWriteAllText(_settingsFilePath, json))
				{
					return;
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Failed to save clock settings: " + ex.GetType().Name + ": " + ex.Message);
			}
			_writtenSeq = seq;
		}
	}

	private void HookExit()
	{
		if (_exitHooked)
		{
			return;
		}
		Application current = Application.Current;
		if (current == null)
		{
			return;
		}
		try
		{
			current.Exit += delegate
			{
				bool flag;
				lock (_queueLock)
				{
					flag = _pendingJson != null;
				}
				if (flag)
				{
					TransactionLogger.Error("Clock settings changed after shutdown verification; not written after commit.");
				}
			};
			_exitHooked = true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Clock settings exit hook failed: " + ex.GetType().Name);
		}
	}

	private void NotifyChanged()
	{
		EventHandler settingsChanged = this.SettingsChanged;
		if (settingsChanged == null)
		{
			return;
		}
		Delegate[] invocationList = settingsChanged.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler eventHandler = (EventHandler)invocationList[i];
			try
			{
				eventHandler(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Clock settings listener failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
	}

	public string FormatTime(DateTime dt, bool includeSeconds)
	{
		if (Format == ClockFormat.TwentyFourHour)
		{
			if (!includeSeconds)
			{
				return dt.ToString("HH:mm", CultureInfo.InvariantCulture);
			}
			return dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
		}
		string text = dt.ToString(includeSeconds ? "h:mm:ss" : "h:mm", CultureInfo.InvariantCulture);
		string text2 = null;
		try
		{
			DateTimeFormatInfo dateTimeFormat = CultureInfo.CurrentCulture.DateTimeFormat;
			text2 = ((dt.Hour < 12) ? dateTimeFormat.AMDesignator : dateTimeFormat.PMDesignator);
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text2))
		{
			text2 = ((dt.Hour < 12) ? "AM" : "PM");
		}
		return text + " " + text2;
	}

	public string FormatDate(DateTime dt)
	{
		return FormatDate(dt, DateFormat);
	}

	public static string FormatDate(DateTime dt, ClockDateFormat style)
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		try
		{
			string text = currentCulture.DateTimeFormat.LongDatePattern ?? "D";
			string text2 = Regex.Replace(text, "^\\s*dddd[,\\s]*|[,\\s]*dddd\\s*$", "").Trim();
			if (text2.Length == 0 || text2.Contains("dddd"))
			{
				text2 = "d MMMM yyyy";
			}
			if (!text.Contains("dddd"))
			{
				text = "dddd, " + text;
			}
			return style switch
			{
				ClockDateFormat.Standard => dt.ToString(text.Replace("dddd", "ddd").Replace("MMMM", "MMM"), currentCulture), 
				ClockDateFormat.Compact => dt.ToString(text2, currentCulture), 
				_ => dt.ToString(text, currentCulture), 
			};
		}
		catch (FormatException)
		{
			return style switch
			{
				ClockDateFormat.Standard => dt.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture), 
				ClockDateFormat.Compact => dt.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), 
				_ => dt.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture), 
			};
		}
	}

	public string GetCurrentFormattedTime()
	{
		return FormatTime(DateTime.Now, ShowSeconds);
	}

	public string GetCurrentFormattedDate()
	{
		return FormatDate(DateTime.Now);
	}

	public bool AddWorldClock(string cityName, string timeZoneId)
	{
		if (string.IsNullOrWhiteSpace(cityName) || string.IsNullOrEmpty(timeZoneId) || !CanAddWorldClock)
		{
			return false;
		}
		cityName = cityName.Trim();
		if (cityName.Length > 40)
		{
			cityName = cityName.Substring(0, 40);
		}
		foreach (WorldClockCity worldClock in _model.WorldClocks)
		{
			if (!worldClock.IsLocal && string.Equals(worldClock.TimeZoneId, timeZoneId, StringComparison.OrdinalIgnoreCase) && string.Equals(worldClock.CityName, cityName, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		string id = Guid.NewGuid().ToString("N").Substring(0, 8);
		_model.WorldClocks.Add(new WorldClockCity(id, cityName, timeZoneId));
		Save();
		NotifyChanged();
		return true;
	}

	public void RemoveWorldClock(string id)
	{
		if (!string.IsNullOrEmpty(id))
		{
			int num = _model.WorldClocks.RemoveAll((WorldClockCity c) => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase) && !c.IsLocal);
			if (num > 0)
			{
				Save();
				NotifyChanged();
			}
		}
	}

	public void MoveWorldClock(string id, bool moveUp)
	{
		int num = _model.WorldClocks.FindIndex((WorldClockCity c) => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
		if (num >= 0)
		{
			int num2 = (moveUp ? (num - 1) : (num + 1));
			if (num2 >= 0 && num2 < _model.WorldClocks.Count)
			{
				WorldClockCity item = _model.WorldClocks[num];
				_model.WorldClocks.RemoveAt(num);
				_model.WorldClocks.Insert(num2, item);
				Save();
				NotifyChanged();
			}
		}
	}
}
