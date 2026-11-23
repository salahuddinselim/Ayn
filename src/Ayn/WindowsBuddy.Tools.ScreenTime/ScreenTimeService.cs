using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.ScreenTime;

public class ScreenTimeService
{
	private struct LASTINPUTINFO
	{
		public uint Size;

		public uint Tick;
	}

	private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	private const uint PROCESS_QUERY_LIMITED_INFORMATION = 4096u;

	public const int MinLimitSeconds = 900;

	public const int MaxLimitSeconds = 57600;

	private readonly Dispatcher _ui = ((Application.Current != null) ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);

	private volatile bool _sessionActive = true;

	private volatile bool _displayOn = true;

	private int _samplePending;

	private int _activityGeneration;

	private readonly object _saveLock = new object();

	private ScreenTimeHistoryModel _pendingSave;

	private bool _saving;

	private bool _historyLoadFailed;

	private readonly ManualResetEventSlim _saved = new ManualResetEventSlim(initialState: true);

	private readonly Dictionary<string, string> _exePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly object _exeLock = new object();

	private static ScreenTimeService _instance;

	private DispatcherTimer _trackerTimer;

	private readonly string _storagePath;

	private bool _isPaused;

	private DateTime _lastPollUtc = DateTime.UtcNow;

	private string _lastApp;

	private readonly Dictionary<string, int> _todayUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private readonly int[] _todayHourlySeconds = new int[24];

	private readonly List<DayScreenTimeRecord> _pastDays = new List<DayScreenTimeRecord>();

	private string _currentDateString;

	private int _dailyLimitSeconds = 25200;

	private bool _notifyOnLimitReached = true;

	private bool _showNotchAlertOnLimit = true;

	private string _lastLimitAlertDate = string.Empty;

	public static ScreenTimeService Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new ScreenTimeService();
			}
			return _instance;
		}
	}

	public string ForegroundCategory { get; private set; }

	public int DailyLimitSeconds
	{
		get
		{
			return _dailyLimitSeconds;
		}
		set
		{
			_dailyLimitSeconds = NormalizeLimit(value);
		}
	}

	public bool HasLimit => _dailyLimitSeconds > 0;

	public int LimitRemainingSeconds
	{
		get
		{
			if (!HasLimit)
			{
				return 0;
			}
			return _dailyLimitSeconds - TodayTotalSeconds;
		}
	}

	public bool NotifyOnLimitReached
	{
		get
		{
			return _notifyOnLimitReached;
		}
		set
		{
			_notifyOnLimitReached = value;
		}
	}

	public bool ShowNotchAlertOnLimit
	{
		get
		{
			return _showNotchAlertOnLimit;
		}
		set
		{
			_showNotchAlertOnLimit = value;
		}
	}

	public string LimitFormatted
	{
		get
		{
			if (!HasLimit)
			{
				return "No limit";
			}
			return FormatDuration(_dailyLimitSeconds);
		}
	}

	public double LimitProgressFraction
	{
		get
		{
			if (_dailyLimitSeconds <= 0)
			{
				return 0.0;
			}
			return Math.Max(0.0, Math.Min(1.0, (double)TodayTotalSeconds / (double)_dailyLimitSeconds));
		}
	}

	public int LimitPercentInt
	{
		get
		{
			if (_dailyLimitSeconds <= 0)
			{
				return 0;
			}
			return (int)Math.Round((double)TodayTotalSeconds / (double)_dailyLimitSeconds * 100.0);
		}
	}

	public string TimeRemainingFormatted
	{
		get
		{
			int num = _dailyLimitSeconds - TodayTotalSeconds;
			if (num >= 0)
			{
				int num2 = num / 3600;
				int num3 = num % 3600 / 60;
				if (num2 > 0)
				{
					return $"{num2}h {num3}m left";
				}
				return $"{Math.Max(1, num3)}m left";
			}
			int num4 = Math.Abs(num);
			int num5 = num4 / 3600;
			int num6 = num4 % 3600 / 60;
			if (num5 > 0)
			{
				return $"{num5}h {num6}m over limit";
			}
			return $"{Math.Max(1, num6)}m over limit";
		}
	}

	public bool IsPaused
	{
		get
		{
			return _isPaused;
		}
		set
		{
			if (_isPaused != value)
			{
				_isPaused = value;
				Interlocked.Increment(ref _activityGeneration);
				_lastPollUtc = DateTime.UtcNow;
				SaveHistory();
				NotifyUsageUpdated();
			}
		}
	}

	public int TodayTotalSeconds
	{
		get
		{
			int num = 0;
			foreach (KeyValuePair<string, int> item in _todayUsage)
			{
				if (item.Value > 0 && !IsInternalSystemHost(item.Key))
				{
					num += item.Value;
				}
			}
			return num;
		}
	}

	public int AppsUsedToday
	{
		get
		{
			int num = 0;
			foreach (KeyValuePair<string, int> item in _todayUsage)
			{
				if (item.Value > 0 && !IsInternalSystemHost(item.Key))
				{
					num++;
				}
			}
			return num;
		}
	}

	public string TodayTotalFormatted
	{
		get
		{
			int todayTotalSeconds = TodayTotalSeconds;
			int num = todayTotalSeconds / 3600;
			int num2 = todayTotalSeconds % 3600 / 60;
			if (num <= 0)
			{
				return $"{num2}m";
			}
			return $"{num}h {num2}m";
		}
	}

	public string TopAppToday
	{
		get
		{
			List<KeyValuePair<string, int>> topAppsToday = GetTopAppsToday(1);
			if (topAppsToday.Count <= 0)
			{
				return "None";
			}
			return topAppsToday[0].Key;
		}
	}

	public IReadOnlyDictionary<string, int> TodayUsage => _todayUsage;

	public int[] TodayHourlySeconds => _todayHourlySeconds;

	public IReadOnlyList<DayScreenTimeRecord> PastDays => _pastDays;

	public event EventHandler UsageUpdated;

	public event EventHandler<InputActivitySample> ActivitySampled;

	[DllImport("user32.dll")]
	private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

	public void SetDisplayOn(bool on)
	{
		_displayOn = on;
		Interlocked.Increment(ref _activityGeneration);
		_lastPollUtc = DateTime.UtcNow;
	}

	private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
	{
		bool sessionActive = e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.SessionLogon || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect;
		_sessionActive = sessionActive;
		Interlocked.Increment(ref _activityGeneration);
		_ui.BeginInvoke((Action)delegate
		{
			_lastPollUtc = DateTime.UtcNow;
		});
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr handle);

	[DllImport("user32.dll")]
	private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

	private static uint HostedAppPid(IntPtr frame, uint hostPid)
	{
		uint found = 0u;
		try
		{
			EnumChildWindows(frame, delegate(IntPtr child, IntPtr l)
			{
				GetWindowThreadProcessId(child, out var lpdwProcessId);
				if (lpdwProcessId != 0 && lpdwProcessId != hostPid)
				{
					found = lpdwProcessId;
					return false;
				}
				return true;
			}, IntPtr.Zero);
		}
		catch
		{
		}
		return found;
	}

	public string GetExePath(string appName)
	{
		if (string.IsNullOrEmpty(appName))
		{
			return null;
		}
		lock (_exeLock)
		{
			if (_exePaths.TryGetValue(appName, out var value) && value != null && File.Exists(value))
			{
				return value;
			}
		}
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				if (!string.Equals(GetFriendlyAppName(process.ProcessName), appName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string text = ImagePath((uint)process.Id);
				if (text == null)
				{
					continue;
				}
				lock (_exeLock)
				{
					_exePaths[appName] = text;
				}
				return text;
			}
			catch
			{
			}
			finally
			{
				process.Dispose();
			}
		}
		return null;
	}

	private static string ImagePath(uint pid)
	{
		IntPtr intPtr = OpenProcess(4096u, inherit: false, pid);
		if (intPtr == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(1024);
			int size = stringBuilder.Capacity;
			return QueryFullProcessImageName(intPtr, 0, stringBuilder, ref size) ? stringBuilder.ToString(0, size) : null;
		}
		finally
		{
			CloseHandle(intPtr);
		}
	}

	public static string ClassifyPetCategory(string process)
	{
		switch ((process ?? "").ToLowerInvariant())
		{
		case "code":
		case "devenv":
		case "rider64":
		case "pycharm64":
		case "idea64":
		case "windowsTerminal":
		case "windowsterminal":
			return "coding";
		case "vlc":
		case "mpv":
		case "wmplayer":
		case "moviesandtv":
		case "video.ui":
			return "video";
		case "spotify":
		case "music.ui":
		case "itunes":
		case "foobar2000":
			return "music";
		case "winword":
		case "excel":
		case "powerpnt":
		case "onenote":
		case "outlook":
		case "olk":
		case "notion":
		case "acrobat":
		case "soffice.bin":
			return "office";
		case "minecraft":
		case "javaw":
		case "robloxplayerbeta":
		case "valorant":
		case "cs2":
		case "csgo":
		case "gta5":
		case "league of legends":
		case "fortniteclient-win64-shipping":
		case "rocketleague":
		case "eldenring":
		case "overwatch":
			return "gaming";
		default:
			return "other";
		}
	}

	private static int NormalizeLimit(int seconds)
	{
		if (seconds <= 0)
		{
			return 0;
		}
		return Math.Max(900, Math.Min(57600, seconds));
	}

	public static string FormatDuration(int seconds)
	{
		if (seconds < 60)
		{
			if (seconds <= 0)
			{
				return "0m";
			}
			return "<1m";
		}
		int num = seconds / 3600;
		int num2 = seconds % 3600 / 60;
		if (num == 0)
		{
			return num2 + "m";
		}
		if (num2 != 0)
		{
			return num + "h " + num2 + "m";
		}
		return num + "h";
	}

	public static int DayTotal(DayScreenTimeRecord d)
	{
		if (d == null)
		{
			return 0;
		}
		if (d.AppSeconds == null || d.AppSeconds.Count == 0)
		{
			return Math.Max(0, d.TotalSeconds);
		}
		int num = 0;
		foreach (KeyValuePair<string, int> appSecond in d.AppSeconds)
		{
			if (appSecond.Value > 0 && !IsInternalSystemHost(appSecond.Key))
			{
				num += appSecond.Value;
			}
		}
		return num;
	}

	public static KeyValuePair<string, int>? DayTopApp(DayScreenTimeRecord d)
	{
		if (d == null || d.AppSeconds == null)
		{
			return null;
		}
		KeyValuePair<string, int>? result = null;
		foreach (KeyValuePair<string, int> appSecond in d.AppSeconds)
		{
			if (appSecond.Value > 0 && !IsInternalSystemHost(appSecond.Key) && (!result.HasValue || appSecond.Value > result.Value.Value))
			{
				result = appSecond;
			}
		}
		return result;
	}

	public List<DayScreenTimeRecord> GetRolling7Days()
	{
		List<DayScreenTimeRecord> list = new List<DayScreenTimeRecord>();
		for (int num = 6; num >= 0; num--)
		{
			string dStr = DateTime.Today.AddDays(-num).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			if (dStr == _currentDateString)
			{
				list.Add(new DayScreenTimeRecord
				{
					DateString = _currentDateString,
					TotalSeconds = TodayTotalSeconds,
					AppSeconds = new Dictionary<string, int>(_todayUsage, StringComparer.OrdinalIgnoreCase),
					HourlySeconds = (int[])_todayHourlySeconds.Clone()
				});
			}
			else
			{
				DayScreenTimeRecord dayScreenTimeRecord = _pastDays.FirstOrDefault((DayScreenTimeRecord d) => d.DateString == dStr);
				if (dayScreenTimeRecord != null)
				{
					list.Add(dayScreenTimeRecord);
				}
				else
				{
					list.Add(new DayScreenTimeRecord
					{
						DateString = dStr,
						TotalSeconds = 0,
						AppSeconds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
						HourlySeconds = new int[24]
					});
				}
			}
		}
		return list;
	}

	public int GetWeeklyAverageSeconds()
	{
		List<DayScreenTimeRecord> rolling7Days = GetRolling7Days();
		int num = 0;
		foreach (DayScreenTimeRecord item in rolling7Days)
		{
			num += DayTotal(item);
		}
		return num / 7;
	}

	public List<KeyValuePair<string, int>> GetTopAppsToday(int maxCount = 20)
	{
		IOrderedEnumerable<KeyValuePair<string, int>> source = from kv in _todayUsage
			where kv.Value > 0 && !IsInternalSystemHost(kv.Key)
			orderby kv.Value descending
			select kv;
		if (maxCount <= 0)
		{
			return source.ToList();
		}
		return source.Take(maxCount).ToList();
	}

	public static bool IsInternalSystemHost(string appName)
	{
		if (string.IsNullOrEmpty(appName))
		{
			return true;
		}
		string text = appName.ToLowerInvariant();
		switch (text)
		{
		default:
			return text == "pickerhost";
		case "lockapp":
		case "logonui":
		case "shellexperiencehost":
		case "applicationframehost":
		case "searchhost":
		case "asuskeyboardhost":
		case "system":
		case "idle":
		case "credentialuibroker":
		case "startmenuexperiencehost":
		case "shellhost":
		case "textinputhost":
			return true;
		}
	}

	public static string GetCategoryForApp(string appName)
	{
		if (string.IsNullOrEmpty(appName))
		{
			return "Other";
		}
		string text = appName.ToLowerInvariant();
		if (text.Contains("code") || text.Contains("visual studio") || text.Contains("antigravity") || text.Contains("terminal") || text.Contains("devenv") || text.Contains("cmd") || text.Contains("powershell") || text.Contains("notepad") || text.Contains("git"))
		{
			return "Development";
		}
		if (text.Contains("spotify") || text.Contains("steam") || text.Contains("media") || text.Contains("music") || text.Contains("vlc") || text.Contains("game") || text.Contains("netflix") || text.Contains("youtube"))
		{
			return "Entertainment";
		}
		if (text.Contains("discord") || text.Contains("slack") || text.Contains("teams") || text.Contains("telegram") || text.Contains("whatsapp") || text.Contains("messenger") || text.Contains("social") || text.Contains("chat"))
		{
			return "Social";
		}
		return "Other";
	}

	public Dictionary<string, int> GetCategoryUsageToday()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		dictionary.Add("Development", 0);
		dictionary.Add("Entertainment", 0);
		dictionary.Add("Social", 0);
		dictionary.Add("Other", 0);
		Dictionary<string, int> dictionary2 = dictionary;
		foreach (KeyValuePair<string, int> item in _todayUsage)
		{
			string categoryForApp = GetCategoryForApp(item.Key);
			if (dictionary2.ContainsKey(categoryForApp))
			{
				dictionary2[categoryForApp] += item.Value;
			}
			else
			{
				dictionary2["Other"] += item.Value;
			}
		}
		return dictionary2;
	}

	public ScreenTimeService()
	{
		string localAppData = AppPaths.LocalAppData;
		string path = Path.Combine(localAppData, AppPaths.FolderName, "screentime");
		_storagePath = Path.Combine(path, "history.json");
		_currentDateString = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		LoadHistory();
		SystemEvents.SessionSwitch += OnSessionSwitch;
		StartTracking();
	}

	private void StartTracking()
	{
		_lastPollUtc = DateTime.UtcNow;
		_trackerTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(5.0)
		};
		_trackerTimer.Tick += delegate
		{
			PollForegroundWindow();
		};
		_trackerTimer.Start();
	}

	private void PollForegroundWindow()
	{
		DateTime utcNow = DateTime.UtcNow;
		double elapsedSeconds = (utcNow - _lastPollUtc).TotalSeconds;
		_lastPollUtc = utcNow;
		string todayStr = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		if (todayStr != _currentDateString)
		{
			RollOverNewDay(todayStr);
		}
		if (_isPaused || !_sessionActive || !_displayOn || elapsedSeconds <= 0.0 || elapsedSeconds > 30.0 || Interlocked.CompareExchange(ref _samplePending, 1, 0) != 0)
		{
			return;
		}
		int generation = _activityGeneration;
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				LASTINPUTINFO lASTINPUTINFO = default(LASTINPUTINFO);
				lASTINPUTINFO.Size = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
				LASTINPUTINFO info = lASTINPUTINFO;
				if (GetLastInputInfo(ref info))
				{
					uint tickCount = (uint)Environment.TickCount;
					double idle = (double)(tickCount - info.Tick) / 1000.0;
					_ui.BeginInvoke((Action)delegate
					{
						if (generation == _activityGeneration && _sessionActive && _displayOn)
						{
							this.ActivitySampled?.Invoke(this, new InputActivitySample(idle, elapsedSeconds));
						}
					});
					if (InputIsRecent(tickCount, info.Tick))
					{
						IntPtr foregroundWindow = GetForegroundWindow();
						if (!(foregroundWindow == IntPtr.Zero))
						{
							GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
							if (lpdwProcessId != 0)
							{
								string processName;
								using (Process process = Process.GetProcessById((int)lpdwProcessId))
								{
									processName = process.ProcessName;
								}
								if (string.Equals(processName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
								{
									uint num = HostedAppPid(foregroundWindow, lpdwProcessId);
									if (num != 0)
									{
										lpdwProcessId = num;
										using Process process2 = Process.GetProcessById((int)lpdwProcessId);
										processName = process2.ProcessName;
									}
								}
								string petCategory = ClassifyPetCategory(processName);
								_ui.BeginInvoke((Action)delegate
								{
									if (generation == _activityGeneration)
									{
										ForegroundCategory = petCategory;
									}
								});
								string friendlyName = GetFriendlyAppName(processName);
								if (!IsInternalSystemHost(friendlyName))
								{
									bool flag;
									lock (_exeLock)
									{
										flag = _exePaths.ContainsKey(friendlyName);
									}
									if (!flag)
									{
										string text = ImagePath(lpdwProcessId);
										if (text != null)
										{
											lock (_exeLock)
											{
												_exePaths[friendlyName] = text;
											}
										}
									}
									_ui.BeginInvoke((Action)delegate
									{
										if (generation == _activityGeneration && !_isPaused && _sessionActive && _displayOn && !(todayStr != _currentDateString))
										{
											int num2 = (int)Math.Round(elapsedSeconds);
											if (num2 > 0)
											{
												if (_todayUsage.ContainsKey(friendlyName))
												{
													_todayUsage[friendlyName] += num2;
												}
												else
												{
													_todayUsage[friendlyName] = num2;
												}
												_lastApp = friendlyName;
												int hour = DateTime.Now.Hour;
												if (hour >= 0 && hour < 24)
												{
													_todayHourlySeconds[hour] += num2;
												}
												CheckDailyLimitThreshold();
												if (TodayTotalSeconds % 30 < 3)
												{
													SaveHistory();
												}
												NotifyUsageUpdated();
											}
										}
									});
								}
							}
						}
					}
				}
			}
			catch
			{
			}
			finally
			{
				Interlocked.Exchange(ref _samplePending, 0);
			}
		});
	}

	public void CheckDailyLimitThreshold()
	{
		if (!HasLimit || TodayTotalSeconds < _dailyLimitSeconds || string.Equals(_lastLimitAlertDate, _currentDateString, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		_lastLimitAlertDate = _currentDateString;
		SaveHistory();
		string text = $"{FormatDuration(TodayTotalSeconds)} on screen today (limit {LimitFormatted})";
		if (_showNotchAlertOnLimit)
		{
			ReminderDelivery.Deliver(ActivityType.ScreenTimeLimit, delegate
			{
				DynamicActivityService.Instance.PublishActivity(new NotchActivity
				{
					Id = "screentime_limit",
					Type = ActivityType.ScreenTimeLimit,
					Priority = ActivityPriority.P2_Timer,
					Mode = ActivityMode.Temporary,
					Duration = TimeSpan.FromSeconds(12.0),
					Title = "Daily limit reached",
					Subtitle = $"{FormatDuration(TodayTotalSeconds)} on screen today",
					IconFactory = (double sz, Brush b) => NotchGlyphs.Create("hourglass", sz, b),
					NavigateToToolId = "screentime"
				});
			}, "Daily limit reached", text);
		}
		else if (_notifyOnLimitReached)
		{
			try
			{
				CapsuleWindow.ShowToast("Daily limit reached", text);
			}
			catch
			{
			}
		}
	}

	private static bool InputIsRecent(uint now, uint lastInput)
	{
		return now - lastInput < 120000;
	}

	public void SetDailyLimit(int seconds, bool notify, bool showNotch)
	{
		_dailyLimitSeconds = NormalizeLimit(seconds);
		_notifyOnLimitReached = notify;
		_showNotchAlertOnLimit = showNotch;
		if (!HasLimit || TodayTotalSeconds < _dailyLimitSeconds)
		{
			_lastLimitAlertDate = string.Empty;
		}
		else
		{
			CheckDailyLimitThreshold();
		}
		SaveHistory();
		NotifyUsageUpdated();
	}

	private string GetFriendlyAppName(string raw)
	{
		if (string.IsNullOrEmpty(raw))
		{
			return "System";
		}
		string text = raw.ToLowerInvariant();
		if (text.Contains("antigravity"))
		{
			return "Antigravity IDE";
		}
		switch (text)
		{
		case "chrome":
			return "Google Chrome";
		case "msedge":
			return "Microsoft Edge";
		case "firefox":
			return "Firefox";
		case "code":
			return "Visual Studio Code";
		case "devenv":
			return "Visual Studio";
		case "spotify":
			return "Spotify";
		case "discord":
			return "Discord";
		case "slack":
			return "Slack";
		case "explorer":
			return "File Explorer";
		case "windowsbuddy":
		case "ayn":
			return "Ayn";
		case "notepad":
			return "Notepad";
		case "cmd":
		case "powershell":
		case "windowsterminal":
			return "Terminal";
		case "steam":
			return "Steam";
		default:
			if (!text.Contains("screensketch"))
			{
				if (text == "systemsettings" || text.Contains("immersivecontrolpanel"))
				{
					return "Settings";
				}
				return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
			}
			goto case "snippingtool";
		case "snippingtool":
			return "SnippingTool";
		}
	}

	public void Refresh()
	{
		PollForegroundWindow();
		SaveHistory();
		NotifyUsageUpdated();
	}

	public void ClearAllHistory()
	{
		_todayUsage.Clear();
		Array.Clear(_todayHourlySeconds, 0, 24);
		_pastDays.Clear();
		lock (_exeLock)
		{
			_exePaths.Clear();
		}
		_lastLimitAlertDate = string.Empty;
		SaveHistory();
		NotifyUsageUpdated();
	}

	public void ResetToday()
	{
		_todayUsage.Clear();
		Array.Clear(_todayHourlySeconds, 0, 24);
		_lastLimitAlertDate = string.Empty;
		SaveHistory();
		NotifyUsageUpdated();
	}

	private void RollOverNewDay(string newDateStr)
	{
		DayScreenTimeRecord dayScreenTimeRecord = new DayScreenTimeRecord();
		dayScreenTimeRecord.DateString = _currentDateString;
		dayScreenTimeRecord.TotalSeconds = TodayTotalSeconds;
		dayScreenTimeRecord.AppSeconds = new Dictionary<string, int>(_todayUsage);
		dayScreenTimeRecord.HourlySeconds = (int[])_todayHourlySeconds.Clone();
		DayScreenTimeRecord item = dayScreenTimeRecord;
		_pastDays.RemoveAll((DayScreenTimeRecord d) => d.DateString == _currentDateString);
		_pastDays.Insert(0, item);
		DayScreenTimeRecord dayScreenTimeRecord2 = _pastDays.FirstOrDefault((DayScreenTimeRecord d) => d.DateString == newDateStr);
		if (dayScreenTimeRecord2 != null)
		{
			_pastDays.Remove(dayScreenTimeRecord2);
		}
		while (_pastDays.Count > 7)
		{
			_pastDays.RemoveAt(_pastDays.Count - 1);
		}
		_todayUsage.Clear();
		Array.Clear(_todayHourlySeconds, 0, 24);
		if (dayScreenTimeRecord2 != null)
		{
			if (dayScreenTimeRecord2.AppSeconds != null)
			{
				foreach (KeyValuePair<string, int> appSecond in dayScreenTimeRecord2.AppSeconds)
				{
					_todayUsage[appSecond.Key] = appSecond.Value;
				}
			}
			if (dayScreenTimeRecord2.HourlySeconds != null && dayScreenTimeRecord2.HourlySeconds.Length == 24)
			{
				Array.Copy(dayScreenTimeRecord2.HourlySeconds, _todayHourlySeconds, 24);
			}
		}
		_currentDateString = newDateStr;
		_lastLimitAlertDate = string.Empty;
		SaveHistory();
	}

	private void NotifyUsageUpdated()
	{
		EventHandler handler = this.UsageUpdated;
		if (handler == null)
		{
			return;
		}
		Application current = Application.Current;
		if (current != null && current.Dispatcher != null && !current.Dispatcher.CheckAccess())
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				try
				{
					handler(this, EventArgs.Empty);
				}
				catch
				{
				}
			});
			return;
		}
		try
		{
			handler(this, EventArgs.Empty);
		}
		catch
		{
		}
	}

	private void LoadHistory()
	{
		try
		{
			if (!File.Exists(_storagePath))
			{
				return;
			}
			string text = PersistenceHelper.ReadShared(_storagePath);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidDataException("Empty screen time history");
			}
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			ScreenTimeHistoryModel screenTimeHistoryModel = javaScriptSerializer.Deserialize<ScreenTimeHistoryModel>(text);
			if (screenTimeHistoryModel == null || screenTimeHistoryModel.Days == null)
			{
				throw new InvalidDataException("Invalid screen time history");
			}
			HashSet<string> hashSet = new HashSet<string>();
			foreach (DayScreenTimeRecord day in screenTimeHistoryModel.Days)
			{
				if (day == null || !DateTime.TryParseExact(day.DateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _) || !hashSet.Add(day.DateString) || day.TotalSeconds < 0 || day.AppSeconds == null || day.AppSeconds.Values.Any((int v) => v < 0) || day.HourlySeconds == null || day.HourlySeconds.Length != 24 || day.HourlySeconds.Any((int v) => v < 0))
				{
					throw new InvalidDataException("Invalid screen time day");
				}
			}
			if (screenTimeHistoryModel == null)
			{
				return;
			}
			_isPaused = screenTimeHistoryModel.IsTrackingPaused;
			if (screenTimeHistoryModel.DailyLimitSeconds >= 0)
			{
				_dailyLimitSeconds = NormalizeLimit(screenTimeHistoryModel.DailyLimitSeconds);
			}
			if (screenTimeHistoryModel.ExePaths != null)
			{
				lock (_exeLock)
				{
					foreach (KeyValuePair<string, string> exePath in screenTimeHistoryModel.ExePaths)
					{
						if (!string.IsNullOrEmpty(exePath.Value))
						{
							_exePaths[exePath.Key] = exePath.Value;
						}
					}
				}
			}
			_notifyOnLimitReached = screenTimeHistoryModel.NotifyOnLimitReached;
			_showNotchAlertOnLimit = screenTimeHistoryModel.ShowNotchAlertOnLimit;
			_lastLimitAlertDate = screenTimeHistoryModel.LastLimitAlertDate ?? string.Empty;
			if (screenTimeHistoryModel.Days == null)
			{
				return;
			}
			DayScreenTimeRecord dayScreenTimeRecord = screenTimeHistoryModel.Days.FirstOrDefault((DayScreenTimeRecord d) => d.DateString == _currentDateString);
			if (dayScreenTimeRecord != null)
			{
				if (dayScreenTimeRecord.AppSeconds != null)
				{
					foreach (KeyValuePair<string, int> appSecond in dayScreenTimeRecord.AppSeconds)
					{
						_todayUsage[appSecond.Key] = appSecond.Value;
					}
				}
				if (dayScreenTimeRecord.HourlySeconds != null && dayScreenTimeRecord.HourlySeconds.Length == 24)
				{
					Array.Copy(dayScreenTimeRecord.HourlySeconds, _todayHourlySeconds, 24);
				}
			}
			_pastDays.Clear();
			_pastDays.AddRange((from d in screenTimeHistoryModel.Days
				where d.DateString != null && d.DateString != _currentDateString
				orderby d.DateString descending
				select d).Take(7));
		}
		catch (Exception ex2)
		{
			_historyLoadFailed = true;
			try
			{
				File.Copy(_storagePath, _storagePath + ".corrupt-" + Guid.NewGuid().ToString("N") + ".bak", overwrite: false);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Screen time history backup failed: " + ex.GetType().Name);
			}
			TransactionLogger.Warn("Screen time history could not be loaded; saving disabled: " + ex2.GetType().Name);
		}
	}

	public void SaveHistory()
	{
		if (_historyLoadFailed)
		{
			return;
		}
		try
		{
			List<DayScreenTimeRecord> list = new List<DayScreenTimeRecord>();
			list.Add(new DayScreenTimeRecord
			{
				DateString = _currentDateString,
				TotalSeconds = TodayTotalSeconds,
				AppSeconds = new Dictionary<string, int>(_todayUsage),
				HourlySeconds = (int[])_todayHourlySeconds.Clone()
			});
			list.AddRange(_pastDays);
			ScreenTimeHistoryModel screenTimeHistoryModel = new ScreenTimeHistoryModel();
			screenTimeHistoryModel.IsTrackingPaused = _isPaused;
			screenTimeHistoryModel.DailyLimitSeconds = _dailyLimitSeconds;
			screenTimeHistoryModel.NotifyOnLimitReached = _notifyOnLimitReached;
			screenTimeHistoryModel.ShowNotchAlertOnLimit = _showNotchAlertOnLimit;
			screenTimeHistoryModel.LastLimitAlertDate = _lastLimitAlertDate;
			screenTimeHistoryModel.Days = list;
			ScreenTimeHistoryModel screenTimeHistoryModel2 = screenTimeHistoryModel;
			lock (_exeLock)
			{
				screenTimeHistoryModel2.ExePaths = new Dictionary<string, string>(_exePaths, StringComparer.OrdinalIgnoreCase);
			}
			lock (_saveLock)
			{
				_pendingSave = screenTimeHistoryModel2;
				_saved.Reset();
				if (_saving)
				{
					return;
				}
				_saving = true;
			}
			ThreadPool.QueueUserWorkItem(delegate
			{
				while (true)
				{
					ScreenTimeHistoryModel pendingSave;
					lock (_saveLock)
					{
						pendingSave = _pendingSave;
						_pendingSave = null;
						if (pendingSave == null)
						{
							_saving = false;
							_saved.Set();
							break;
						}
					}
					try
					{
						PersistenceHelper.SafeWriteAllText(_storagePath, new JavaScriptSerializer().Serialize(pendingSave));
					}
					catch (Exception ex)
					{
						TransactionLogger.Warn("Screen time save failed: " + ex.GetType().Name);
					}
				}
			});
		}
		catch
		{
		}
	}

	public Action PauseForShutdown()
	{
		bool enabled = _trackerTimer.IsEnabled;
		_trackerTimer.Stop();
		Interlocked.Increment(ref _activityGeneration);
		SaveHistory();
		return delegate
		{
			if (enabled)
			{
				_trackerTimer.Start();
			}
		};
	}

	public void WaitForPersistence()
	{
		if (!_saved.Wait(2000))
		{
			throw new TimeoutException("Screen time remains pending.");
		}
	}

	public void Shutdown()
	{
		_trackerTimer.Stop();
		SystemEvents.SessionSwitch -= OnSessionSwitch;
		Interlocked.Increment(ref _activityGeneration);
		if (!_saved.Wait(TimeSpan.FromSeconds(3.0)))
		{
			TransactionLogger.Error("Screen time write still outstanding at disposal.");
		}
	}
}
