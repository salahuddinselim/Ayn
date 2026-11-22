using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Media;
using WindowsBuddy.Tools.ScreenTime;
using WindowsBuddy.Tools.Timer;
using WindowsBuddy.Tools.Weather;

namespace WindowsBuddy.Services.Pet;

public sealed class PetEngine : IDisposable
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct UserInfo10
	{
		public IntPtr Name;

		public IntPtr Comment;

		public IntPtr UserComment;

		public IntPtr FullName;
	}

	private static readonly Lazy<PetEngine> instance = new Lazy<PetEngine>(() => new PetEngine());

	private readonly string historyPath = Path.Combine(AppPaths.DataDir, "settings", "pet-history.json");

	private readonly Dispatcher ui = ((Application.Current != null) ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);

	private readonly OrderedSaveWorker historyWriter;

	private string historySnapshot;

	private volatile bool historySaveRequested;

	public readonly PetLife Life = new PetLife();

	private bool firstMeetFlag;

	private bool wasEnabled;

	private bool startedOnce;

	private readonly Stopwatch lifeClock = Stopwatch.StartNew();

	private bool started;

	private bool returned;

	private readonly List<PetMoment> pending = new List<PetMoment>();

	private readonly Random random = new Random();

	private DateTime nextCheckIn;

	private bool greetingPending;

	private DateTime policyChecked;

	private bool policyQuiet;

	private bool observedQuiet;

	private int lowBand;

	private readonly List<FileSystemWatcher> fileWatches = new List<FileSystemWatcher>();

	private readonly Dictionary<string, DateTime> fileSeen = new Dictionary<string, DateTime>();

	private int pendingFiles;

	private readonly List<DispatcherTimer> fileSettling = new List<DispatcherTimer>();

	private readonly Dictionary<string, DateTime> reactedAt = new Dictionary<string, DateTime>();

	private DateTime lastMessage = DateTime.MinValue;

	private DateTime lastSuggestion = DateTime.MinValue;

	private SystemTelemetryData power = new SystemTelemetryData();

	public Func<bool> QuietOverride;

	internal Func<bool> MusicOverride;

	internal Func<double> IdleOverride;

	internal Func<string> CategoryOverride;

	public Func<DateTime> Clock = () => DateTime.Now;

	private string firstName;

	public static PetEngine Instance => instance.Value;

	public PetHistory History { get; private set; }

	public PetDay Day { get; private set; }

	public bool FirstMeetPending
	{
		get
		{
			if (!firstMeetFlag)
			{
				if (PetPreferences.Instance.Enabled && !Met)
				{
					return !PetPreferences.Instance.QaEnabled.HasValue;
				}
				return false;
			}
			return true;
		}
		private set
		{
			firstMeetFlag = value;
		}
	}

	public bool ReturnWave { get; set; }

	public double LifeSeconds => lifeClock.Elapsed.TotalSeconds;

	public double IdleSeconds { get; private set; }

	public double ContinuousSeconds { get; private set; }

	public int WakeSequence { get; private set; }

	public bool IsMusicPlaying
	{
		get
		{
			if (MusicOverride == null)
			{
				return MediaService.Instance.IsPlaying;
			}
			return MusicOverride();
		}
	}

	public SystemTelemetryData Power => power;

	public bool Met => PetPreferences.Instance.IntroSeen;

	public PetMood Mood => Day.Mood(Context());

	public string Festival => PetCalendar.Festival(Clock(), PetPreferences.Instance.Birthday);

	public string Name
	{
		get
		{
			if (!PetPreferences.Instance.ShowName)
			{
				return "";
			}
			return FirstName();
		}
	}

	public string Pose
	{
		get
		{
			if (Life.Presence == PetPresence.Sleeping)
			{
				return "sleeping";
			}
			if (Life.Presence == PetPresence.Dozing)
			{
				return "sleepy";
			}
			if (Life.Presence == PetPresence.Sitting)
			{
				return "sitting";
			}
			if (power != null && power.HasBattery && power.IsCharging)
			{
				return "charging";
			}
			if (power != null && power.HasBattery && power.BatteryPercent >= 0 && power.BatteryPercent <= 20)
			{
				return "low-battery";
			}
			if (MediaService.Instance.IsPlaying)
			{
				return "music-01";
			}
			return "idle";
		}
	}

	public PetExpression Expression
	{
		get
		{
			if (Life.Presence == PetPresence.Sleeping)
			{
				return PetExpression.Closed;
			}
			if (Life.Presence == PetPresence.Dozing)
			{
				return PetExpression.Sleepy;
			}
			if (Pose == "charging")
			{
				return PetExpression.Happy;
			}
			if (Pose == "low-battery")
			{
				return PetExpression.Worried;
			}
			if (IdleSeconds >= 1800.0)
			{
				return PetExpression.Closed;
			}
			if (IdleSeconds >= 600.0 || Clock().Hour >= 23 || Clock().Hour < 5)
			{
				return PetExpression.Sleepy;
			}
			return PetExpression.Normal;
		}
	}

	public bool EnvironmentVisible
	{
		get
		{
			NotchVisibilityEngine notchVisibilityEngine = NotchVisibilityEngine.Instance;
			if (!notchVisibilityEngine.SessionLocked)
			{
				return notchVisibilityEngine.CurrentPresence == NotchPresence.Full;
			}
			return false;
		}
	}

	public bool MotionAllowed
	{
		get
		{
			if (EnvironmentVisible && !Quiet && !PowerSaver.IsBatterySaverOn)
			{
				return CapsuleMotion.AnimationsEnabled;
			}
			return false;
		}
	}

	public bool InQuietHours
	{
		get
		{
			PetPreferences petPreferences = PetPreferences.Instance;
			int hour = Clock().Hour;
			if (petPreferences.QuietHours)
			{
				if (petPreferences.QuietStart != petPreferences.QuietEnd)
				{
					if (petPreferences.QuietStart >= petPreferences.QuietEnd)
					{
						if (hour < petPreferences.QuietStart)
						{
							return hour < petPreferences.QuietEnd;
						}
						return true;
					}
					if (hour >= petPreferences.QuietStart)
					{
						return hour < petPreferences.QuietEnd;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public bool Quiet
	{
		get
		{
			if (!EnvironmentVisible || !EntitlementGate.IsEntitled)
			{
				return true;
			}
			if (NotchVisibilityEngine.Instance.LastInputs.FullscreenOnMonitor || PowerSaver.IsBatterySaverOn || (TimerService.Instance.PomoIsRunning && !TimerService.Instance.PomoIsPaused))
			{
				return true;
			}
			if (QuietOverride != null && AppPaths.IsSandboxed)
			{
				return QuietOverride();
			}
			if (Clock() - policyChecked < TimeSpan.FromSeconds(2.0))
			{
				return policyQuiet;
			}
			policyChecked = Clock();
			policyQuiet = ReadQuietPolicy();
			return policyQuiet;
		}
	}

	public event EventHandler Changed;

	public static void ObservePassive(string kind)
	{
		if (instance.IsValueCreated)
		{
			instance.Value.Passive(kind);
		}
	}

	public bool BubbleAllowed()
	{
		if (PetPreferences.Instance.Enabled && PetPreferences.Instance.Talks && !Quiet && !InQuietHours)
		{
			return Clock() - History.LastTalk >= TimeSpan.FromMinutes(10.0);
		}
		return false;
	}

	private void Passive(string kind)
	{
		if (!PetPreferences.Instance.Enabled)
		{
			return;
		}
		string text;
		string pose;
		string text2;
		switch (kind)
		{
		default:
			return;
		case "clipboard":
			text = "react-clipboard";
			pose = "paper";
			text2 = "Copied";
			break;
		case "charging":
			text = "react-charging";
			pose = "plug";
			text2 = "Charging";
			break;
		}
		DateTime dateTime = Clock();
		if (!reactedAt.TryGetValue(text, out var value) || !(dateTime - value < TimeSpan.FromSeconds(30.0)))
		{
			reactedAt[text] = dateTime;
			string title = "";
			if (BubbleAllowed())
			{
				title = text2;
				History.LastTalk = dateTime;
				Save();
			}
			Enqueue(new PetMoment
			{
				Id = text,
				Pose = pose,
				Title = title,
				Priority = 7,
				Expression = PetExpression.Happy
			});
		}
	}

	public static void ObserveExternal(string id, string pose, string title)
	{
		if (instance.IsValueCreated)
		{
			instance.Value.Enqueue(new PetMoment
			{
				Id = id,
				Pose = pose,
				Title = title,
				Expression = PetExpression.Happy
			});
		}
	}

	[DllImport("shell32.dll")]
	private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);

	private static string KnownFolder(string id)
	{
		Guid id2 = new Guid(id);
		if (SHGetKnownFolderPath(ref id2, 0u, IntPtr.Zero, out var path) != 0)
		{
			return null;
		}
		try
		{
			return Marshal.PtrToStringUni(path);
		}
		finally
		{
			Marshal.FreeCoTaskMem(path);
		}
	}

	private void WatchFiles()
	{
		foreach (DispatcherTimer item in fileSettling)
		{
			item.Stop();
		}
		fileSettling.Clear();
		pendingFiles = 0;
		foreach (FileSystemWatcher fileWatch in fileWatches)
		{
			fileWatch.Dispose();
		}
		fileWatches.Clear();
		if (PetPreferences.Instance.Enabled && !AppPaths.IsSandboxed)
		{
			WatchFolder(KnownFolder("374DE290-123F-4565-9164-39C4925E467B"), screenshot: false);
			WatchFolder(KnownFolder("B7BEDE81-DF94-4682-A7D8-57A52620B86F"), screenshot: true);
		}
	}

	private void WatchFolder(string folder, bool screenshot)
	{
		if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
		{
			return;
		}
		try
		{
			FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(folder);
			fileSystemWatcher.NotifyFilter = NotifyFilters.FileName;
			fileSystemWatcher.IncludeSubdirectories = false;
			FileSystemWatcher fileSystemWatcher2 = fileSystemWatcher;
			FileSystemEventHandler saved = delegate(object s, FileSystemEventArgs e)
			{
				Post(delegate
				{
					string text = Path.GetExtension(e.Name).ToLowerInvariant();
					switch (text)
					{
					default:
					{
						if (File.Exists(e.FullPath) && (!screenshot || !(text != ".png") || !(text != ".jpg")) && (!fileSeen.TryGetValue(e.Name, out var value) || !(Clock() - value < TimeSpan.FromMinutes(1.0))))
						{
							string[] array = (from k in fileSeen
								where Clock() - k.Value > TimeSpan.FromMinutes(2.0)
								select k.Key).ToArray();
							foreach (string key in array)
							{
								fileSeen.Remove(key);
							}
							if (fileSeen.Count >= 64)
							{
								fileSeen.Remove(fileSeen.OrderBy((KeyValuePair<string, DateTime> k) => k.Value).First().Key);
							}
							fileSeen[e.Name] = Clock();
							if (pendingFiles < 16)
							{
								pendingFiles++;
								long size = -1L;
								int stable = 0;
								int attempts = 0;
								DispatcherTimer settled = new DispatcherTimer(DispatcherPriority.Background)
								{
									Interval = TimeSpan.FromSeconds(1.0)
								};
								settled.Tick += delegate
								{
									try
									{
										FileInfo fileInfo = new FileInfo(e.FullPath);
										if (!fileInfo.Exists)
										{
											throw new IOException();
										}
										stable = ((fileInfo.Length == size && DateTime.Now - fileInfo.LastWriteTime >= TimeSpan.FromSeconds(2.0)) ? (stable + 1) : 0);
										size = fileInfo.Length;
										if (stable < 2 && ++attempts < 120)
										{
											return;
										}
										if (stable >= 2)
										{
											Enqueue(new PetMoment
											{
												Id = (screenshot ? "screenshot" : "download"),
												Pose = (screenshot ? "camera" : "box"),
												Title = (screenshot ? "Screenshot saved" : e.Name)
											});
										}
									}
									catch (IOException)
									{
									}
									catch (UnauthorizedAccessException)
									{
									}
									settled.Stop();
									fileSettling.Remove(settled);
									pendingFiles--;
								};
								fileSettling.Add(settled);
								settled.Start();
							}
						}
						break;
					}
					case ".crdownload":
						break;
					case ".part":
						break;
					case ".download":
						break;
					}
				});
			};
			fileSystemWatcher2.Created += saved;
			fileSystemWatcher2.Renamed += delegate(object s, RenamedEventArgs e)
			{
				saved(s, e);
			};
			fileSystemWatcher2.EnableRaisingEvents = true;
			fileWatches.Add(fileSystemWatcher2);
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	public PetEngine()
	{
		Life.Affection = PetPreferences.Instance.Affection;
		DateTime affectionSaved = PetPreferences.Instance.LegacyOptions.AffectionSaved;
		if (affectionSaved != DateTime.MinValue)
		{
			Life.Attention((0.0 - Math.Max(0.0, Math.Min(7.0, (DateTime.Now - affectionSaved.ToLocalTime()).TotalDays))) * 0.12);
		}
		History = (PetPreferences.Read<PetHistory>(historyPath) ?? new PetHistory()).Normalize();
		historySnapshot = new JavaScriptSerializer().Serialize(History);
		historyWriter = new OrderedSaveWorker(historyPath, () => Volatile.Read(ref historySnapshot));
		if (!PetPreferences.Instance.QaEnabled.HasValue)
		{
			PersistenceHelper.RegisterExitFlush("pet-history", delegate
			{
				if (historySaveRequested)
				{
					historyWriter.Flush(5000);
				}
			});
		}
		History.LastTalk = Local(History.LastTalk);
		History.LastLaunch = Local(History.LastLaunch);
		History.LastSpontaneous = Local(History.LastSpontaneous);
		Dictionary<string, DateTime>[] array = new Dictionary<string, DateTime>[4] { History.Lines, History.Scenes, History.TalkLines, History.BigScenes };
		foreach (Dictionary<string, DateTime> dictionary in array)
		{
			if (dictionary != null)
			{
				string[] array2 = dictionary.Keys.ToArray();
				foreach (string key in array2)
				{
					dictionary[key] = Local(dictionary[key]);
				}
			}
		}
		Day = new PetDay(History);
		wasEnabled = PetPreferences.Instance.Enabled;
		if (wasEnabled && !Met && !PetPreferences.Instance.QaEnabled.HasValue)
		{
			FirstMeetPending = true;
		}
	}

	public PetContext Context()
	{
		PetContext petContext = new PetContext();
		petContext.Now = Clock();
		petContext.Category = ((CategoryOverride != null) ? CategoryOverride() : (ScreenTimeService.Instance.ForegroundCategory ?? "other"));
		petContext.Music = IsMusicPlaying;
		petContext.Continuous = ContinuousSeconds;
		petContext.Typing = Life.Calm(LifeSeconds);
		petContext.Affection = Life.Affection;
		petContext.Birthday = PetPreferences.Instance.Birthday;
		petContext.Charging = power != null && power.HasBattery && power.IsCharging;
		petContext.LowBattery = power != null && power.HasBattery && !power.IsCharging && power.BatteryPercent >= 0 && power.BatteryPercent <= 20;
		PetContext petContext2 = petContext;
		try
		{
			WeatherService existing = WeatherService.Existing;
			if (existing != null && existing.IsConfigured && existing.Current != null && !existing.Current.IsOffline && existing.Current.HasData)
			{
				petContext2.Weather = PetCalendar.WeatherBucket(existing.Current.WeatherCode, existing.Current.Temperature);
			}
		}
		catch
		{
		}
		return petContext2;
	}

	public string Line(string context, int maxWords = 0)
	{
		PetLineEntry petLineEntry = PetLines.Choose(History, Clock(), PetLines.Groups(Mood, Clock(), context), random, Name, maxWords);
		Save();
		return petLineEntry?.Text;
	}

	public string LineFrom(string group, int maxWords = 0)
	{
		PetLineEntry petLineEntry = PetLines.Choose(History, Clock(), new string[1] { group }, random, Name, maxWords);
		Save();
		return petLineEntry?.Text;
	}

	public void CompleteFirstMeet()
	{
		FirstMeetPending = false;
		PetPreferences.Instance.CompleteFirstMeet();
	}

	public void ForceFirstMeetForQa()
	{
		FirstMeetPending = true;
	}

	private static DateTime Local(DateTime value)
	{
		if (!(value == DateTime.MinValue))
		{
			if (value.Kind != DateTimeKind.Utc)
			{
				return value;
			}
			return value.ToLocalTime();
		}
		return value;
	}

	public void Start()
	{
		if (!PetPreferences.Instance.Enabled || started)
		{
			return;
		}
		started = true;
		if (!PetPreferences.Instance.QaEnabled.HasValue)
		{
			if (!Met)
			{
				FirstMeetPending = true;
			}
			else if (startedOnce)
			{
				ReturnWave = true;
			}
		}
		startedOnce = true;
		DateTime dateTime = Clock();
		returned = History.LastLaunch != DateTime.MinValue && dateTime - History.LastLaunch >= TimeSpan.FromDays(3.0);
		History.LastLaunch = dateTime;
		Save();
		power = SystemMonitorService.GetLiveTelemetry();
		ScreenTimeService.Instance.ActivitySampled += OnSample;
		SystemMonitorService.PowerChanged += OnPower;
		MediaService.Instance.MediaStateChanged += OnChanged;
		NotchVisibilityEngine.Instance.VisibilityChanged += OnVisibility;
		NotchVisibilityEngine.Instance.EnvironmentChanged += OnChanged;
		PillActivityArbiter.Instance.ActivityChanged += OnActivity;
		PetPreferences.Instance.Changed += OnPreferences;
		WatchFiles();
		SystemEvents.PowerModeChanged += OnResume;
		SystemEvents.SessionSwitch += OnSession;
	}

	[DllImport("shell32.dll")]
	private static extern int SHQueryUserNotificationState(out int state);

	[DllImport("secur32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool GetUserNameEx(int format, StringBuilder text, ref int length);

	[DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
	private static extern int NetUserGetInfo(string server, string user, int level, out IntPtr data);

	[DllImport("netapi32.dll")]
	private static extern int NetApiBufferFree(IntPtr data);

	private static bool MicrophoneBusy(RegistryKey key, int depth)
	{
		if (key == null)
		{
			return false;
		}
		long num = Convert.ToInt64(key.GetValue("LastUsedTimeStart", 0L));
		long num2 = Convert.ToInt64(key.GetValue("LastUsedTimeStop", 0L));
		if (num > 0 && num > num2)
		{
			return true;
		}
		if (depth > 0)
		{
			string[] subKeyNames = key.GetSubKeyNames();
			foreach (string name in subKeyNames)
			{
				using RegistryKey key2 = key.OpenSubKey(name);
				if (MicrophoneBusy(key2, depth - 1))
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool ReadQuietPolicy()
	{
		try
		{
			if (SHQueryUserNotificationState(out var state) == 0 && state != 5)
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			using RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone");
			if (MicrophoneBusy(key, 2))
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			using RegistryKey key2 = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam");
			if (MicrophoneBusy(key2, 2))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private string FirstName()
	{
		if (firstName != null)
		{
			return firstName;
		}
		firstName = "";
		try
		{
			int length = 256;
			StringBuilder stringBuilder = new StringBuilder(length);
			if (GetUserNameEx(3, stringBuilder, ref length))
			{
				string source = stringBuilder.ToString().Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
				firstName = new string(source.Where((char c) => !char.IsControl(c)).Take(24).ToArray());
			}
			if (firstName.Length == 0 && NetUserGetInfo(null, Environment.UserName, 10, out var data) == 0)
			{
				try
				{
					string source2 = (Marshal.PtrToStringUni(((UserInfo10)Marshal.PtrToStructure(data, typeof(UserInfo10))).FullName) ?? "").Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
					firstName = new string(source2.Where((char c) => !char.IsControl(c)).Take(24).ToArray());
				}
				finally
				{
					NetApiBufferFree(data);
				}
			}
		}
		catch
		{
		}
		return firstName;
	}

	public bool TryGreeting()
	{
		Start();
		PetPreferences petPreferences = PetPreferences.Instance;
		DateTime now = Clock();
		if (!petPreferences.Enabled || !petPreferences.Greetings || greetingPending || Quiet || InQuietHours)
		{
			return false;
		}
		PetLine line = PetStrings.Choose(now, History, returned);
		if (line == null)
		{
			return false;
		}
		string festival = Festival;
		string title = ((festival != null) ? LineFrom("festival/" + festival) : null) ?? (returned ? LineFrom("ctx/return") : null) ?? LineFrom("greet/" + PetStrings.Part(now)) ?? line.Title;
		greetingPending = true;
		Enqueue(new PetMoment
		{
			Id = "greeting",
			Pose = "wave",
			Title = title,
			Priority = 6,
			Expression = PetExpression.Happy,
			Primary = delegate
			{
				PetStrings.Record(now, History, line);
				returned = false;
				greetingPending = false;
				Save();
			}
		});
		return true;
	}

	public void Enqueue(PetMoment moment)
	{
		if (!ui.CheckAccess())
		{
			Post(delegate
			{
				Enqueue(moment);
			});
		}
		else if (PetPreferences.Instance.Enabled && !Quiet && (!InQuietHours || moment.Priority <= 3) && !pending.Any((PetMoment m) => m.Id == moment.Id))
		{
			moment.Expires = Clock().AddMinutes(moment.Suggestion ? 1 : 2);
			if (pending.Count >= 8)
			{
				pending.RemoveAt(pending.Count - 1);
			}
			pending.Add(moment);
			pending.Sort((PetMoment a, PetMoment b) => a.Priority.CompareTo(b.Priority));
			TransactionLogger.Info("Pet director: queued " + moment.Id);
		}
	}

	public PetMoment TakeMoment()
	{
		if (Quiet || Life.Presence == PetPresence.Sleeping)
		{
			pending.Clear();
			greetingPending = false;
			return null;
		}
		if (Life.Calm(LifeSeconds))
		{
			pending.RemoveAll((PetMoment item) => item.Priority > 3);
		}
		if (pending.Any((PetMoment item) => item.Id == "greeting" && item.Expires < Clock()))
		{
			greetingPending = false;
		}
		pending.RemoveAll((PetMoment item) => item.Expires < Clock() || (InQuietHours && item.Priority > 3));
		if (pending.Count == 0)
		{
			return null;
		}
		PetMoment petMoment = pending[0];
		pending.RemoveAt(0);
		if (petMoment.Suggestion)
		{
			string text = Clock().ToString("yyyy-MM-dd");
			if (History.SuggestionDate != text)
			{
				History.SuggestionDate = text;
				History.SuggestionsToday = 0;
			}
			if (History.SuggestionsToday >= 3)
			{
				return null;
			}
			History.SuggestionsToday++;
			lastSuggestion = Clock();
			Save();
		}
		else if (petMoment.Id == "greeting" && petMoment.Primary != null)
		{
			petMoment.Primary();
			petMoment.Primary = null;
		}
		if (petMoment.Id.StartsWith("talk-"))
		{
			if (History.TalkLines == null)
			{
				History.TalkLines = new Dictionary<string, DateTime>();
			}
			History.LastTalk = Clock();
			History.TalkLines[petMoment.Title] = Clock();
			string[] array = (from p in History.TalkLines
				where p.Value.Date < Clock().Date
				select p.Key).ToArray();
			foreach (string key in array)
			{
				History.TalkLines.Remove(key);
			}
			Save();
		}
		lastMessage = Clock();
		TransactionLogger.Info("Pet director: show " + petMoment.Id);
		return petMoment;
	}

	public void Answer(PetMoment moment, bool primary)
	{
		if (moment != null && moment.Suggestion && !Quiet)
		{
			string text = (primary ? moment.PrimaryLabel : moment.SecondaryLabel);
			History.Answers[Clock().ToString("yyyy-MM-dd") + "/" + moment.Id] = text;
			string[] array = History.Answers.Keys.Where((string k) => string.CompareOrdinal(k, Clock().AddDays(-30.0).ToString("yyyy-MM-dd")) < 0).ToArray();
			foreach (string key in array)
			{
				History.Answers.Remove(key);
			}
			Save();
			TransactionLogger.Info("Pet director: answered " + moment.Id + " " + text);
			(primary ? moment.Primary : moment.Secondary)?.Invoke();
		}
	}

	public void CheckIn(bool requested = false)
	{
		if (PetPreferences.Instance.CheckIns || requested)
		{
			DateTime dateTime = Clock();
			if (requested || !(dateTime < nextCheckIn))
			{
				nextCheckIn = dateTime.AddMinutes(15 + random.Next(16));
				string title = dateTime.ToString("h:mm tt") + (power.HasBattery ? (" · " + power.BatteryPercent + "%") : "");
				Enqueue(new PetMoment
				{
					Id = "check-in",
					Pose = "clock",
					Title = title,
					Priority = 6
				});
			}
		}
	}

	public string ChooseStory(IEnumerable<string> names)
	{
		List<string> list = names.ToList();
		DateTime now = Clock();
		List<string> list2 = list.Where((string n) => !History.Scenes.ContainsKey(n) || History.Scenes[n] < now.AddDays(-3.0)).ToList();
		if (list2.Count == 0)
		{
			list2 = list.OrderBy((string n) => (!History.Scenes.ContainsKey(n)) ? DateTime.MinValue : History.Scenes[n]).Take(Math.Max(1, list.Count / 2)).ToList();
		}
		string text = ((IdleSeconds < 15.0 && list2.Contains("tea") && random.Next(3) != 0) ? "tea" : list2[random.Next(list2.Count)]);
		History.Scenes[text] = now;
		Save();
		return text;
	}

	public void Ambient()
	{
		if (Quiet || InQuietHours || Life.Calm(LifeSeconds) || Life.Presence == PetPresence.Sleeping)
		{
			return;
		}
		DateTime dateTime = Clock();
		if (IdleSeconds < 60.0)
		{
			CheckIn();
			TrySuggestion(dateTime);
		}
		if (PetPreferences.Instance.Talks && !(dateTime - lastMessage < TimeSpan.FromMinutes(10.0)) && !pending.Any((PetMoment m) => m.Id.StartsWith("talk-")) && !(Context().Category == "gaming") && PetLines.CanSpeak(History, dateTime, Quiet, Life.Calm(LifeSeconds)))
		{
			string text = Line(TalkContext(dateTime), 4);
			if (text != null)
			{
				History.LastSpontaneous = dateTime;
				History.LastTalk = dateTime;
				Save();
				Enqueue(new PetMoment
				{
					Id = "talk-" + text,
					Pose = "wave",
					Title = text,
					Priority = 6
				});
			}
		}
	}

	private string TalkContext(DateTime now)
	{
		PetContext petContext = Context();
		string festival = Festival;
		if (festival != null && random.Next(3) == 0)
		{
			return "festival/" + festival;
		}
		if (PetDay.LateWork(petContext))
		{
			return "ctx/latenight";
		}
		if (petContext.Category == "coding" || petContext.Category == "office" || petContext.Category == "video" || petContext.Category == "gaming")
		{
			return "ctx/" + petContext.Category;
		}
		if (petContext.Music)
		{
			return "ctx/music";
		}
		if (petContext.Weather != null && random.Next(2) == 0)
		{
			return "ctx/" + petContext.Weather;
		}
		if (now.DayOfWeek == DayOfWeek.Monday && now.Hour < 12)
		{
			return "ctx/monday";
		}
		if (now.DayOfWeek == DayOfWeek.Friday)
		{
			return "ctx/friday";
		}
		if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
		{
			return "ctx/weekend";
		}
		return null;
	}

	public void ObserveInput(double idleSeconds, double elapsedSeconds, DateTime now)
	{
		string pose = Pose;
		PetExpression expression = Expression;
		bool flag = Life.ObservePresence(idleSeconds, now.Hour, LifeSeconds);
		IdleSeconds = Math.Max(0.0, idleSeconds);
		if (PetPreferences.Instance.Enabled)
		{
			PetPreferences.Instance.SaveAffection(Life.Affection, now);
			if (flag && !Quiet && !Life.Calm(LifeSeconds))
			{
				Enqueue(new PetMoment
				{
					Id = "welcome",
					Pose = "wave",
					Title = (LineFrom("ctx/return", 4) ?? PetStrings.Welcome(Life.LastAbsence)),
					Expression = PetExpression.Happy,
					Priority = 6
				});
			}
			Routine(now, flag);
		}
		if (flag)
		{
			WakeSequence++;
		}
		if (idleSeconds >= 300.0 || elapsedSeconds > 30.0)
		{
			ContinuousSeconds = 0.0;
		}
		else if (idleSeconds < 60.0)
		{
			ContinuousSeconds += Math.Max(0.0, elapsedSeconds);
		}
		if (flag || pose != Pose || expression != Expression)
		{
			Raise();
		}
		TrySuggestion(now);
	}

	private void Routine(DateTime now, bool welcomed)
	{
		if (Quiet || InQuietHours || Life.Calm(LifeSeconds) || IdleSeconds >= 3.0)
		{
			return;
		}
		string text = PetLife.ClaimRoutine(History.Parts, now);
		if (text != null)
		{
			string[] array = History.Parts.Keys.Where((string k) => k.StartsWith("routine/") && !k.Contains(now.ToString("yyyy-MM-dd"))).ToArray();
			foreach (string key in array)
			{
				History.Parts.Remove(key);
			}
			if (!welcomed)
			{
				Life.Start(text switch
				{
					"night" => "stargaze", 
					"evening" => "chai", 
					"morning" => "stretch", 
					_ => "wave", 
				}, LifeSeconds, 3.0);
			}
			if (!welcomed && (text == "morning" || text == "lunch"))
			{
				Enqueue(new PetMoment
				{
					Id = "routine-" + text,
					Pose = "wave",
					Title = (((text == "morning") ? LineFrom("greet/morning") : LineFrom("sleepy/afternoon")) ?? PetStrings.Routine(text)),
					Expression = PetExpression.Happy,
					Priority = 6
				});
			}
			Save();
		}
	}

	public void WakeFromPointer()
	{
		if (IdleSeconds >= 120.0)
		{
			ObserveInput(0.0, 0.0, Clock());
		}
	}

	private void OnSample(object s, InputActivitySample e)
	{
		ObserveInput((IdleOverride != null) ? IdleOverride() : e.IdleSeconds, e.ElapsedSeconds, Clock());
		if (PetPreferences.Instance.Enabled)
		{
			bool quiet = Quiet;
			if (quiet != observedQuiet)
			{
				observedQuiet = quiet;
				Raise();
			}
		}
	}

	public void SetPower(SystemTelemetryData data)
	{
		power = data ?? new SystemTelemetryData();
		Raise();
		bool flag = power.HasBattery && !power.IsCharging && power.BatteryPercent >= 0 && power.BatteryPercent <= 20;
		int num = (flag ? ((power.BatteryPercent <= 10) ? 10 : 20) : 0);
		if (flag && power.BatteryPercent > 5 && num != lowBand && PetPreferences.Instance.Enabled && !Quiet)
		{
			DynamicActivityService.Instance.PublishActivity(new NotchActivity
			{
				Id = "pet_battery_low",
				Type = ActivityType.BatteryLow,
				Priority = ActivityPriority.P3_Transient,
				Mode = ActivityMode.Temporary,
				Duration = TimeSpan.FromSeconds(4.0),
				Title = "Battery low",
				Subtitle = power.BatteryPercent + "%"
			});
		}
		lowBand = num;
	}

	private void OnPower(object s, SystemTelemetryData d)
	{
		Post(delegate
		{
			SetPower(d);
		});
	}

	public bool TrySuggestion(DateTime now)
	{
		PetPreferences petPreferences = PetPreferences.Instance;
		TimerService t = TimerService.Instance;
		if (!petPreferences.Enabled || !petPreferences.Suggestions || IdleSeconds >= 60.0 || Quiet || InQuietHours || t.PomoIsRunning || t.CountIsRunning || now - lastMessage < TimeSpan.FromMinutes(20.0) || now - lastSuggestion < TimeSpan.FromHours(2.0) || pending.Any((PetMoment m) => m.Suggestion))
		{
			return false;
		}
		string text = now.ToString("yyyy-MM-dd");
		if (History.SuggestionDate != text)
		{
			History.SuggestionDate = text;
			History.SuggestionsToday = 0;
		}
		if (History.SuggestionsToday >= 3)
		{
			return false;
		}
		PetMoment petMoment2;
		if (ContinuousSeconds >= 3600.0 && History.BreakAsked != text)
		{
			History.BreakAsked = text;
			PetMoment petMoment = new PetMoment();
			petMoment.Id = "ask-break";
			petMoment.Pose = "cup";
			petMoment.Title = LineFrom("ctx/break", 4) ?? "Take a short break?";
			petMoment.PrimaryLabel = "Start";
			petMoment.SecondaryLabel = "Later";
			petMoment.Primary = delegate
			{
				ContinuousSeconds = 0.0;
				StartBreak();
			};
			petMoment2 = petMoment;
		}
		else if (now.Hour >= 17 && !History.Answers.ContainsKey(text + "/ask-day"))
		{
			PetMoment petMoment3 = new PetMoment();
			petMoment3.Id = "ask-day";
			petMoment3.Pose = "hug";
			petMoment3.Title = "How was your day?";
			petMoment3.PrimaryLabel = "Good";
			petMoment3.SecondaryLabel = "Okay";
			petMoment2 = petMoment3;
		}
		else
		{
			if (!t.HydrationEnabled || now.Hour < 9 || now.Hour >= 22 || t.HydrationGlassesConsumed >= t.HydrationDailyTarget)
			{
				return false;
			}
			PetMoment petMoment4 = new PetMoment();
			petMoment4.Id = "ask-water";
			petMoment4.Pose = "glass";
			petMoment4.Title = "Had some water?";
			petMoment4.PrimaryLabel = "Yes";
			petMoment4.SecondaryLabel = "Later";
			petMoment4.Primary = delegate
			{
				t.AddHydrationGlass();
			};
			petMoment2 = petMoment4;
		}
		petMoment2.Suggestion = true;
		petMoment2.Priority = 5;
		Enqueue(petMoment2);
		return true;
	}

	public static void StartBreak()
	{
		TimerService timerService = TimerService.Instance;
		if (!timerService.CountIsRunning && !timerService.PomoIsRunning)
		{
			timerService.SetCountdown(300);
			timerService.SetCountdownLabel("Break");
			timerService.StartCountdown();
		}
	}

	private void OnChanged(object s, EventArgs e)
	{
		Post(Raise);
	}

	private void OnPreferences(object s, EventArgs e)
	{
		bool enabled = PetPreferences.Instance.Enabled;
		if (!enabled || fileWatches.Count == 0)
		{
			WatchFiles();
		}
		if (!enabled)
		{
			pending.Clear();
			greetingPending = false;
		}
		if (enabled && !wasEnabled && !PetPreferences.Instance.QaEnabled.HasValue)
		{
			if (!Met)
			{
				FirstMeetPending = true;
			}
			else
			{
				ReturnWave = true;
			}
		}
		wasEnabled = enabled;
	}

	public void ResetMemory()
	{
		History = new PetHistory
		{
			LastLaunch = Clock()
		};
		Day = new PetDay(History);
		pending.Clear();
		greetingPending = false;
		lastSuggestion = (lastMessage = DateTime.MinValue);
		Save();
	}

	private void OnVisibility(object s, VisibilityChangeEventArgs e)
	{
		Post(Raise);
	}

	private void OnActivity(object s, PillActivityChangedEventArgs e)
	{
		if ((e.NewActivity != null && e.NewActivity.Type != 0 && e.NewActivity.Type != ActivityType.Clock) || (e.OldActivity != null && e.OldActivity.Type != 0 && e.OldActivity.Type != ActivityType.Clock))
		{
			lastMessage = Clock();
		}
	}

	private void OnResume(object s, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.Resume)
		{
			Post(delegate
			{
				ObserveInput(0.0, 0.0, Clock());
			});
		}
	}

	private void OnSession(object s, SessionSwitchEventArgs e)
	{
		if (e.Reason == SessionSwitchReason.SessionUnlock)
		{
			Post(delegate
			{
				ObserveInput(0.0, 0.0, Clock());
			});
		}
	}

	private void Post(Action a)
	{
		Action action = delegate
		{
			try
			{
				a();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Pet engine callback recovered: " + ex);
			}
		};
		if (ui.CheckAccess())
		{
			action();
		}
		else if (!ui.HasShutdownStarted)
		{
			ui.BeginInvoke(action);
		}
	}

	private void Raise()
	{
		EventHandler changed = this.Changed;
		if (changed == null)
		{
			return;
		}
		Delegate[] invocationList = changed.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler eventHandler = (EventHandler)invocationList[i];
			try
			{
				eventHandler(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Pet observer recovered: " + ex);
			}
		}
	}

	private void Save()
	{
		if (PetPreferences.Instance.QaEnabled.HasValue)
		{
			return;
		}
		try
		{
			Volatile.Write(ref historySnapshot, new JavaScriptSerializer().Serialize(History));
			historySaveRequested = true;
			historyWriter.Request();
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(historyPath, error);
		}
	}

	public void Dispose()
	{
		if (!started)
		{
			return;
		}
		started = false;
		PetPreferences.Instance.SaveAffection(Life.Affection, Clock(), force: true);
		foreach (DispatcherTimer item in fileSettling)
		{
			item.Stop();
		}
		fileSettling.Clear();
		pendingFiles = 0;
		foreach (FileSystemWatcher fileWatch in fileWatches)
		{
			fileWatch.Dispose();
		}
		fileWatches.Clear();
		PetPreferences.Instance.Changed -= OnPreferences;
		ScreenTimeService.Instance.ActivitySampled -= OnSample;
		SystemMonitorService.PowerChanged -= OnPower;
		MediaService.Instance.MediaStateChanged -= OnChanged;
		NotchVisibilityEngine.Instance.VisibilityChanged -= OnVisibility;
		PillActivityArbiter.Instance.ActivityChanged -= OnActivity;
		NotchVisibilityEngine.Instance.EnvironmentChanged -= OnChanged;
		SystemEvents.PowerModeChanged -= OnResume;
		SystemEvents.SessionSwitch -= OnSession;
	}
}
