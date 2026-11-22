using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Timer;

public class TimerService
{
	public const int FocusMinMin = 1;

	public const int FocusMaxMin = 120;

	public const int FocusDefaultMin = 25;

	public const int BreakMinMin = 1;

	public const int BreakMaxMin = 30;

	public const int BreakDefaultMin = 5;

	public const int LongMinMin = 5;

	public const int LongMaxMin = 60;

	public const int LongDefaultMin = 15;

	public const int CyclesMin = 1;

	public const int CyclesMax = 10;

	public const int CyclesDefault = 4;

	private static TimerService _instance;

	private readonly DispatcherTimer _clockTimer;

	private readonly DispatcherTimer _midnightTimer;

	private readonly string _storagePath;

	private readonly object _saveLock = new object();

	private TimerServiceState _pendingSave;

	private bool _saving;

	private readonly ManualResetEventSlim _saved = new ManualResetEventSlim(initialState: true);

	private long _timerGeneration = 1L;

	private long _pomoStartTicks;

	private long _pomoTargetTicks;

	private long _pomoAccumulatedTicks;

	private long _countStartTicks;

	private long _countTargetTicks;

	private long _countAccumulatedTicks;

	private string _lastPublishedTimerDisplay;

	private bool _lastPublishedTimerPaused;

	private bool _hasPublishedTimerState;

	private long _stopwatchStartTicks;

	private long _stopwatchAccumulatedTicks;

	private string _currentDateString = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

	private long _lastSecondTimestamp = Stopwatch.GetTimestamp();

	private static DateTime _lastChimeTime = DateTime.MinValue;

	private static byte[] _cachedChimeWav;

	private static readonly object _chimeLock = new object();

	public static TimerService Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new TimerService();
			}
			return _instance;
		}
	}

	public long CurrentTimerGeneration => _timerGeneration;

	public int PomoWorkSeconds { get; set; }

	public int PomoBreakSeconds { get; set; }

	public int PomoLongBreakSeconds { get; set; }

	public int PomoRemainingSeconds { get; private set; }

	public bool PomoIsWork { get; private set; }

	public int PomoCycles { get; private set; }

	public int PomoCurrentCycle { get; set; }

	public int PomoTargetCycles { get; set; }

	public bool PomoIsRunning { get; private set; }

	public bool PomoIsPaused { get; private set; }

	public string PomodoroFormattedTime => $"{PomoRemainingSeconds / 60:D2}:{PomoRemainingSeconds % 60:D2}";

	public bool IsPomodoroRunning => PomoIsRunning;

	public bool IsPomodoroBreak => !PomoIsWork;

	public int PomoSessionsToday { get; private set; }

	public bool PomoIsLongBreak
	{
		get
		{
			if (!PomoIsWork && PomoTargetCycles > 0)
			{
				return PomoCurrentCycle >= PomoTargetCycles;
			}
			return false;
		}
	}

	public int PomoPhaseTotalSeconds
	{
		get
		{
			if (!PomoIsWork)
			{
				if (!PomoIsLongBreak)
				{
					return PomoBreakSeconds;
				}
				return PomoLongBreakSeconds;
			}
			return PomoWorkSeconds;
		}
	}

	public double PomoRemainingExact
	{
		get
		{
			if (!PomoIsRunning || PomoIsPaused)
			{
				return PomoRemainingSeconds;
			}
			long num = _pomoAccumulatedTicks + (Stopwatch.GetTimestamp() - _pomoStartTicks);
			return Math.Max(0.0, (double)(_pomoTargetTicks - num) / (double)Stopwatch.Frequency);
		}
	}

	public double CountRemainingExact
	{
		get
		{
			if (!CountIsRunning || CountIsPaused)
			{
				return CountRemainingSeconds;
			}
			long num = _countAccumulatedTicks + (Stopwatch.GetTimestamp() - _countStartTicks);
			return Math.Max(0.0, (double)(_countTargetTicks - num) / (double)Stopwatch.Frequency);
		}
	}

	public int CountTotalSeconds { get; private set; }

	public int CountRemainingSeconds { get; private set; }

	public bool CountIsRunning { get; private set; }

	public bool CountIsPaused { get; private set; }

	public bool StopwatchIsRunning { get; private set; }

	public List<TimeSpan> StopwatchLaps { get; private set; }

	public TimeSpan StopwatchElapsed
	{
		get
		{
			if (StopwatchIsRunning)
			{
				long num = _stopwatchAccumulatedTicks + (Stopwatch.GetTimestamp() - _stopwatchStartTicks);
				return TimeSpan.FromSeconds((double)num / (double)Stopwatch.Frequency);
			}
			return TimeSpan.FromSeconds((double)_stopwatchAccumulatedTicks / (double)Stopwatch.Frequency);
		}
	}

	public bool HydrationEnabled { get; set; }

	public int HydrationIntervalMinutes { get; set; }

	public int HydrationRemainingSeconds { get; private set; }

	public int HydrationGlassesConsumed { get; set; }

	public int HydrationDailyTarget { get; set; }

	public bool PomoAutoStartNext { get; private set; }

	public string CountLabel { get; private set; }

	public List<int> CountCustomPresets { get; private set; }

	public Dictionary<string, int> HydrationHistory { get; private set; }

	public event EventHandler Tick;

	public event EventHandler<string> Alert;

	public event EventHandler TimerStarted;

	public event EventHandler TimerPaused;

	public event EventHandler TimerResumed;

	public event EventHandler TimerCancelled;

	public event EventHandler TimerCompleted;

	public event EventHandler HydrationChanged;

	public event EventHandler SettingsChanged;

	public TimerService()
	{
		PomoAutoStartNext = true;
		CountLabel = "";
		CountCustomPresets = new List<int>();
		HydrationHistory = new Dictionary<string, int>();
		PomoWorkSeconds = 1500;
		PomoBreakSeconds = 300;
		PomoLongBreakSeconds = 900;
		PomoRemainingSeconds = PomoWorkSeconds;
		PomoIsWork = true;
		PomoCycles = 0;
		PomoCurrentCycle = 1;
		PomoTargetCycles = 4;
		CountTotalSeconds = 900;
		CountRemainingSeconds = CountTotalSeconds;
		StopwatchLaps = new List<TimeSpan>();
		HydrationEnabled = true;
		HydrationIntervalMinutes = 45;
		HydrationRemainingSeconds = HydrationIntervalMinutes * 60;
		HydrationGlassesConsumed = 0;
		HydrationDailyTarget = 8;
		string localAppData = AppPaths.LocalAppData;
		string path = Path.Combine(localAppData, AppPaths.FolderName, "timer");
		_storagePath = Path.Combine(path, "timers_state.json");
		LoadState();
		_clockTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(200.0)
		};
		_clockTimer.Tick += OnClockTick;
		UpdateClockSchedule();
		_midnightTimer = new DispatcherTimer(DispatcherPriority.Background);
		DispatcherTimer midnightTimer = _midnightTimer;
		EventHandler value = delegate
		{
			_midnightTimer.Stop();
			OnClockTick(null, EventArgs.Empty);
			ScheduleMidnight();
		};
		midnightTimer.Tick += value;
		ScheduleMidnight();
	}

	private void ScheduleMidnight()
	{
		_midnightTimer.Interval = TimeSpan.FromSeconds(Math.Max(1.0, (DateTime.Today.AddDays(1.0).ToUniversalTime() - DateTime.UtcNow).TotalSeconds));
		_midnightTimer.Start();
	}

	private void UpdateClockSchedule()
	{
		if (_clockTimer == null)
		{
			return;
		}
		bool flag = (PomoIsRunning && !PomoIsPaused) || (CountIsRunning && !CountIsPaused) || StopwatchIsRunning;
		if (!flag && !HydrationEnabled)
		{
			_clockTimer.Stop();
			return;
		}
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(flag ? 200 : 1000);
		if (_clockTimer.Interval != timeSpan)
		{
			_clockTimer.Interval = timeSpan;
		}
		if (!_clockTimer.IsEnabled)
		{
			_lastSecondTimestamp = Stopwatch.GetTimestamp();
			_clockTimer.Start();
		}
	}

	private void OnClockTick(object sender, EventArgs e)
	{
		long timestamp = Stopwatch.GetTimestamp();
		string text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		if (text != _currentDateString)
		{
			ArchiveHydrationDay(_currentDateString, HydrationGlassesConsumed);
			_currentDateString = text;
			PomoCycles = 0;
			PomoCurrentCycle = 1;
			PomoSessionsToday = 0;
			HydrationGlassesConsumed = 0;
			SaveState();
			EventHandler hydrationChanged = this.HydrationChanged;
			if (hydrationChanged != null)
			{
				try
				{
					hydrationChanged(this, EventArgs.Empty);
				}
				catch
				{
				}
			}
		}
		if (PomoIsRunning && !PomoIsPaused)
		{
			long num = _pomoAccumulatedTicks + (timestamp - _pomoStartTicks);
			long num2 = Math.Max(0L, _pomoTargetTicks - num);
			PomoRemainingSeconds = (int)Math.Ceiling((double)num2 / (double)Stopwatch.Frequency);
			if (PomoRemainingSeconds <= 0)
			{
				PlayChime();
				if (PomoIsWork)
				{
					PomoSessionsToday++;
					PomoIsWork = false;
					bool flag = PomoTargetCycles > 0 && PomoCurrentCycle >= PomoTargetCycles;
					int num4 = (PomoRemainingSeconds = (flag ? PomoLongBreakSeconds : PomoBreakSeconds));
					_pomoTargetTicks = num4 * Stopwatch.Frequency;
					_pomoStartTicks = timestamp;
					_pomoAccumulatedTicks = 0L;
					_timerGeneration++;
					TriggerAlert(flag ? "Cycles complete. Take a long break" : "Focus done. Take a break");
					string focusTitle = (flag ? "Cycles complete" : "Focus done");
					string breakText = (flag ? "" : (Clock(PomoWorkSeconds) + " · ")) + "Take a " + ReminderDuration(num4) + " break";
					ReminderDelivery.Deliver(ActivityType.TimerComplete, delegate
					{
						PillActivityArbiter.Instance.PublishTimerComplete(focusTitle, breakText);
					}, focusTitle, breakText);
					EventHandler timerCompleted = this.TimerCompleted;
					if (timerCompleted != null)
					{
						try
						{
							timerCompleted(this, EventArgs.Empty);
						}
						catch
						{
						}
					}
				}
				else
				{
					if (PomoTargetCycles > 0 && PomoCurrentCycle >= PomoTargetCycles)
					{
						PomoCycles++;
						PomoCurrentCycle = 1;
					}
					else
					{
						PomoCurrentCycle++;
					}
					PomoIsWork = true;
					PomoRemainingSeconds = PomoWorkSeconds;
					_pomoTargetTicks = PomoWorkSeconds * Stopwatch.Frequency;
					_pomoStartTicks = timestamp;
					_pomoAccumulatedTicks = 0L;
					_timerGeneration++;
					TriggerAlert("Break over. Start your next focus session");
					EventHandler timerCompleted2 = this.TimerCompleted;
					if (timerCompleted2 != null)
					{
						try
						{
							timerCompleted2(this, EventArgs.Empty);
						}
						catch
						{
						}
					}
					ReminderDelivery.Deliver(ActivityType.TimerComplete, delegate
					{
						PillActivityArbiter.Instance.PublishTimerComplete("Break over", "Start your next focus session");
					}, "Break over", "Start your next focus session");
				}
				if (!PomoAutoStartNext)
				{
					PomoIsPaused = true;
				}
				SaveState();
			}
		}
		if (CountIsRunning && !CountIsPaused)
		{
			long num5 = _countAccumulatedTicks + (timestamp - _countStartTicks);
			long num6 = Math.Max(0L, _countTargetTicks - num5);
			CountRemainingSeconds = (int)Math.Ceiling((double)num6 / (double)Stopwatch.Frequency);
			if (CountRemainingSeconds <= 0)
			{
				CountRemainingSeconds = 0;
				CountIsRunning = false;
				CountIsPaused = false;
				_timerGeneration++;
				PlayChime();
				TriggerAlert(string.IsNullOrEmpty(CountLabel) ? "Timer done" : ("Timer done · " + CountLabel));
				EventHandler timerCompleted3 = this.TimerCompleted;
				if (timerCompleted3 != null)
				{
					try
					{
						timerCompleted3(this, EventArgs.Empty);
					}
					catch
					{
					}
				}
				PillActivityArbiter.Instance.ClearTimer();
				string timerName = Clock(CountTotalSeconds) + " · " + (string.IsNullOrEmpty(CountLabel) ? "Timer" : CountLabel);
				ReminderDelivery.Deliver(ActivityType.TimerComplete, delegate
				{
					PillActivityArbiter.Instance.PublishTimerComplete("Timer done", timerName);
				}, "Timer done", timerName);
				SaveState();
			}
		}
		if (timestamp - _lastSecondTimestamp >= Stopwatch.Frequency)
		{
			int num7 = (int)((timestamp - _lastSecondTimestamp) / Stopwatch.Frequency);
			_lastSecondTimestamp = timestamp;
			if (HydrationEnabled)
			{
				HydrationRemainingSeconds -= num7;
				if (HydrationRemainingSeconds <= 0)
				{
					HydrationRemainingSeconds = HydrationIntervalMinutes * 60;
					PlayChime();
					TriggerAlert("Drink some water");
					string glasses = ((HydrationDailyTarget > 0) ? $"{HydrationGlassesConsumed} of {HydrationDailyTarget} glasses today" : null);
					ReminderDelivery.Deliver(ActivityType.HydrationReminder, delegate
					{
						PillActivityArbiter.Instance.PublishHydrationNudge(glasses);
					}, "Drink some water", glasses ?? "Click to log a glass", delegate
					{
						AddHydrationGlass();
					});
					SaveState();
				}
			}
		}
		RefreshNotchPresentation();
		if (this.Tick != null)
		{
			try
			{
				this.Tick(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		UpdateClockSchedule();
	}

	public void RefreshNotchPresentation()
	{
		if (!PomoIsRunning && !CountIsRunning && !StopwatchIsRunning)
		{
			return;
		}
		if (NotchConfig.Instance != null && !NotchConfig.Instance.ShowTimersInNotch)
		{
			if (_hasPublishedTimerState)
			{
				_hasPublishedTimerState = false;
				_lastPublishedTimerDisplay = null;
				_lastPublishedTimerPaused = false;
				PillActivityArbiter.Instance.ClearTimer();
			}
			return;
		}
		string text;
		bool flag;
		if (PomoIsRunning)
		{
			text = PomodoroFormattedTime;
			flag = PomoIsPaused;
		}
		else if (CountIsRunning)
		{
			text = CountDisplay();
			flag = CountIsPaused;
		}
		else
		{
			TimeSpan stopwatchElapsed = StopwatchElapsed;
			text = $"{(int)stopwatchElapsed.TotalMinutes:D2}:{stopwatchElapsed.Seconds:D2}";
			flag = !StopwatchIsRunning;
		}
		if (!_hasPublishedTimerState || text != _lastPublishedTimerDisplay || flag != _lastPublishedTimerPaused)
		{
			bool hasPublishedTimerState = _hasPublishedTimerState;
			_lastPublishedTimerDisplay = text;
			_lastPublishedTimerPaused = flag;
			_hasPublishedTimerState = true;
			if (hasPublishedTimerState)
			{
				PillActivityArbiter.Instance.UpdateTimerTick(text, flag);
			}
			else
			{
				PillActivityArbiter.Instance.PublishTimerSession(text, flag);
			}
		}
	}

	public void StartPomodoro()
	{
		if (CountIsRunning)
		{
			ResetCountdown();
		}
		if (PomoRemainingSeconds <= 0)
		{
			PomoRemainingSeconds = PomoPhaseTotalSeconds;
		}
		_pomoTargetTicks = PomoRemainingSeconds * Stopwatch.Frequency;
		_pomoStartTicks = Stopwatch.GetTimestamp();
		_pomoAccumulatedTicks = 0L;
		PomoIsRunning = true;
		PomoIsPaused = false;
		_timerGeneration++;
		_lastPublishedTimerDisplay = PomodoroFormattedTime;
		_lastPublishedTimerPaused = false;
		_hasPublishedTimerState = true;
		PillActivityArbiter.Instance.PublishTimerSession(PomodoroFormattedTime, isPaused: false);
		EventHandler timerStarted = this.TimerStarted;
		if (timerStarted != null)
		{
			try
			{
				timerStarted(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void PausePomodoro()
	{
		if (!PomoIsRunning || PomoIsPaused)
		{
			return;
		}
		_pomoAccumulatedTicks += Stopwatch.GetTimestamp() - _pomoStartTicks;
		PomoIsPaused = true;
		_timerGeneration++;
		_lastPublishedTimerDisplay = PomodoroFormattedTime;
		_lastPublishedTimerPaused = true;
		_hasPublishedTimerState = true;
		PillActivityArbiter.Instance.UpdateTimerTick(PomodoroFormattedTime, isPaused: true);
		EventHandler timerPaused = this.TimerPaused;
		if (timerPaused != null)
		{
			try
			{
				timerPaused(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void ResumePomodoro()
	{
		if (CountIsRunning)
		{
			ResetCountdown();
		}
		if (!PomoIsRunning || !PomoIsPaused)
		{
			return;
		}
		_pomoStartTicks = Stopwatch.GetTimestamp();
		PomoIsPaused = false;
		_timerGeneration++;
		_lastPublishedTimerDisplay = PomodoroFormattedTime;
		_lastPublishedTimerPaused = false;
		_hasPublishedTimerState = true;
		PillActivityArbiter.Instance.UpdateTimerTick(PomodoroFormattedTime, isPaused: false);
		EventHandler timerResumed = this.TimerResumed;
		if (timerResumed != null)
		{
			try
			{
				timerResumed(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void ResetPomodoro()
	{
		PomoIsRunning = false;
		PomoIsPaused = false;
		PomoIsWork = true;
		PomoRemainingSeconds = PomoWorkSeconds;
		_pomoAccumulatedTicks = 0L;
		_timerGeneration++;
		_lastPublishedTimerDisplay = null;
		_lastPublishedTimerPaused = false;
		_hasPublishedTimerState = false;
		PillActivityArbiter.Instance.ClearTimer();
		EventHandler timerCancelled = this.TimerCancelled;
		if (timerCancelled != null)
		{
			try
			{
				timerCancelled(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void SkipPomodoro()
	{
		bool flag = PomoIsRunning && !PomoIsPaused;
		if (PomoIsWork)
		{
			PomoIsWork = false;
			bool flag2 = PomoTargetCycles > 0 && PomoCurrentCycle >= PomoTargetCycles;
			PomoRemainingSeconds = (flag2 ? PomoLongBreakSeconds : PomoBreakSeconds);
		}
		else
		{
			if (PomoTargetCycles > 0 && PomoCurrentCycle >= PomoTargetCycles)
			{
				PomoCycles++;
				PomoCurrentCycle = 1;
			}
			else
			{
				PomoCurrentCycle++;
			}
			PomoIsWork = true;
			PomoRemainingSeconds = PomoWorkSeconds;
		}
		_pomoTargetTicks = PomoRemainingSeconds * Stopwatch.Frequency;
		_pomoStartTicks = Stopwatch.GetTimestamp();
		_pomoAccumulatedTicks = 0L;
		_timerGeneration++;
		if (flag)
		{
			EventHandler timerStarted = this.TimerStarted;
			if (timerStarted != null)
			{
				try
				{
					timerStarted(this, EventArgs.Empty);
				}
				catch
				{
				}
			}
		}
		else
		{
			PomoIsRunning = false;
			PomoIsPaused = false;
			EventHandler timerCancelled = this.TimerCancelled;
			if (timerCancelled != null)
			{
				try
				{
					timerCancelled(this, EventArgs.Empty);
				}
				catch
				{
				}
			}
		}
		SaveState();
	}

	private string CountDisplay()
	{
		string text = $"{CountRemainingSeconds / 60:D2}:{CountRemainingSeconds % 60:D2}";
		if (!string.IsNullOrEmpty(CountLabel))
		{
			return text + " · " + CountLabel;
		}
		return text;
	}

	public void SetAutoStartNext(bool enabled)
	{
		PomoAutoStartNext = enabled;
		SaveState();
		RaiseSettingsChanged();
	}

	public void SetCountdownLabel(string label)
	{
		label = (label ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
		CountLabel = label.Substring(0, Math.Min(40, label.Length));
		_hasPublishedTimerState = false;
		RefreshNotchPresentation();
		SaveState();
	}

	public void SaveCountdownPreset(int seconds)
	{
		seconds = Clamp(seconds, 1, 43200);
		if (!CountCustomPresets.Contains(seconds) && CountCustomPresets.Count < 12)
		{
			CountCustomPresets.Add(seconds);
		}
		SaveState();
		RaiseSettingsChanged();
	}

	public void RemoveCountdownPreset(int seconds)
	{
		CountCustomPresets.Remove(seconds);
		SaveState();
		RaiseSettingsChanged();
	}

	public void AddCountdownSeconds(int seconds)
	{
		int num = Math.Max(0, Math.Min(seconds, 43200 - CountTotalSeconds));
		if (num != 0)
		{
			if (!CountIsRunning)
			{
				SetCountdown(CountTotalSeconds + num);
				RaiseSettingsChanged();
				return;
			}
			CountTotalSeconds += num;
			CountRemainingSeconds += num;
			_countTargetTicks += num * Stopwatch.Frequency;
			SaveState();
			RaiseSettingsChanged();
			RefreshNotchPresentation();
		}
	}

	private void ArchiveHydrationDay(string date, int glasses)
	{
		if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) && result < DateTime.Today && result >= DateTime.Today.AddDays(-7.0))
		{
			HydrationHistory[date] = Math.Max(0, glasses);
		}
		string[] array = HydrationHistory.Keys.ToArray();
		foreach (string text in array)
		{
			if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result) || result < DateTime.Today.AddDays(-7.0) || result >= DateTime.Today)
			{
				HydrationHistory.Remove(text);
			}
		}
	}

	public void SetCountdown(int seconds)
	{
		CountTotalSeconds = Math.Max(1, seconds);
		CountRemainingSeconds = CountTotalSeconds;
		_countTargetTicks = CountTotalSeconds * Stopwatch.Frequency;
		_countAccumulatedTicks = 0L;
		CountIsRunning = false;
		CountIsPaused = false;
		SaveState();
	}

	public void StartCountdown()
	{
		if (PomoIsRunning)
		{
			ResetPomodoro();
		}
		if (CountRemainingSeconds <= 0)
		{
			CountRemainingSeconds = CountTotalSeconds;
		}
		_countTargetTicks = CountRemainingSeconds * Stopwatch.Frequency;
		_countStartTicks = Stopwatch.GetTimestamp();
		_countAccumulatedTicks = 0L;
		CountIsRunning = true;
		CountIsPaused = false;
		_timerGeneration++;
		string display = (_lastPublishedTimerDisplay = CountDisplay());
		_lastPublishedTimerPaused = false;
		_hasPublishedTimerState = true;
		PillActivityArbiter.Instance.PublishTimerSession(display, isPaused: false);
		EventHandler timerStarted = this.TimerStarted;
		if (timerStarted != null)
		{
			try
			{
				timerStarted(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void PauseCountdown()
	{
		if (!CountIsRunning || CountIsPaused)
		{
			return;
		}
		_countAccumulatedTicks += Stopwatch.GetTimestamp() - _countStartTicks;
		CountIsPaused = true;
		_timerGeneration++;
		string display = (_lastPublishedTimerDisplay = CountDisplay());
		_lastPublishedTimerPaused = true;
		_hasPublishedTimerState = true;
		PillActivityArbiter.Instance.UpdateTimerTick(display, isPaused: true);
		EventHandler timerPaused = this.TimerPaused;
		if (timerPaused != null)
		{
			try
			{
				timerPaused(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void ResumeCountdown()
	{
		if (PomoIsRunning)
		{
			ResetPomodoro();
		}
		if (!CountIsRunning || !CountIsPaused)
		{
			return;
		}
		_countStartTicks = Stopwatch.GetTimestamp();
		CountIsPaused = false;
		_timerGeneration++;
		string display = (_lastPublishedTimerDisplay = CountDisplay());
		_lastPublishedTimerPaused = false;
		_hasPublishedTimerState = true;
		PillActivityArbiter.Instance.UpdateTimerTick(display, isPaused: false);
		EventHandler timerResumed = this.TimerResumed;
		if (timerResumed != null)
		{
			try
			{
				timerResumed(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void ResetCountdown()
	{
		CountIsRunning = false;
		CountIsPaused = false;
		CountRemainingSeconds = CountTotalSeconds;
		_countAccumulatedTicks = 0L;
		_timerGeneration++;
		_lastPublishedTimerDisplay = null;
		_lastPublishedTimerPaused = false;
		_hasPublishedTimerState = false;
		PillActivityArbiter.Instance.ClearTimer();
		EventHandler timerCancelled = this.TimerCancelled;
		if (timerCancelled != null)
		{
			try
			{
				timerCancelled(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void StartStopwatch()
	{
		if (StopwatchIsRunning)
		{
			return;
		}
		_stopwatchStartTicks = Stopwatch.GetTimestamp();
		StopwatchIsRunning = true;
		_timerGeneration++;
		if (NotchConfig.Instance == null || NotchConfig.Instance.ShowTimersInNotch)
		{
			TimeSpan stopwatchElapsed = StopwatchElapsed;
			string display = (_lastPublishedTimerDisplay = $"{(int)stopwatchElapsed.TotalMinutes:D2}:{stopwatchElapsed.Seconds:D2}");
			_lastPublishedTimerPaused = false;
			_hasPublishedTimerState = true;
			PillActivityArbiter.Instance.PublishTimerSession(display, isPaused: false);
		}
		EventHandler timerStarted = this.TimerStarted;
		if (timerStarted != null)
		{
			try
			{
				timerStarted(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void PauseStopwatch()
	{
		if (!StopwatchIsRunning)
		{
			return;
		}
		_stopwatchAccumulatedTicks += Stopwatch.GetTimestamp() - _stopwatchStartTicks;
		StopwatchIsRunning = false;
		if ((NotchConfig.Instance == null || NotchConfig.Instance.ShowTimersInNotch) && _hasPublishedTimerState)
		{
			TimeSpan stopwatchElapsed = StopwatchElapsed;
			string display = (_lastPublishedTimerDisplay = $"{(int)stopwatchElapsed.TotalMinutes:D2}:{stopwatchElapsed.Seconds:D2}");
			_lastPublishedTimerPaused = true;
			PillActivityArbiter.Instance.UpdateTimerTick(display, isPaused: true);
		}
		EventHandler timerPaused = this.TimerPaused;
		if (timerPaused != null)
		{
			try
			{
				timerPaused(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void ResetStopwatch()
	{
		StopwatchIsRunning = false;
		_stopwatchAccumulatedTicks = 0L;
		StopwatchLaps.Clear();
		_timerGeneration++;
		if (!PomoIsRunning && !CountIsRunning)
		{
			_lastPublishedTimerDisplay = null;
			_lastPublishedTimerPaused = false;
			_hasPublishedTimerState = false;
			PillActivityArbiter.Instance.ClearTimer();
		}
		EventHandler timerCancelled = this.TimerCancelled;
		if (timerCancelled != null)
		{
			try
			{
				timerCancelled(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
		SaveState();
	}

	public void AddStopwatchLap()
	{
		StopwatchLaps.Insert(0, StopwatchElapsed);
		SaveState();
	}

	public void SetPomodoroSettings(int focusMin, int breakMin, int longMin, int cycles)
	{
		if (PomoIsRunning)
		{
			return;
		}
		int num = Clamp(focusMin, 1, 120);
		int num2 = Clamp(breakMin, 1, 30);
		int num3 = Math.Max(num2, Clamp(longMin, 5, 60));
		int num4 = Clamp(cycles, 1, 10);
		if (num * 60 != PomoWorkSeconds || num2 * 60 != PomoBreakSeconds || num3 * 60 != PomoLongBreakSeconds || num4 != PomoTargetCycles)
		{
			PomoWorkSeconds = num * 60;
			PomoBreakSeconds = num2 * 60;
			PomoLongBreakSeconds = num3 * 60;
			PomoTargetCycles = num4;
			if (PomoCurrentCycle > PomoTargetCycles)
			{
				PomoCurrentCycle = PomoTargetCycles;
			}
			PomoRemainingSeconds = PomoPhaseTotalSeconds;
			SaveState();
			RaiseSettingsChanged();
		}
	}

	private static int Clamp(int v, int min, int max)
	{
		return Math.Max(min, Math.Min(max, v));
	}

	private static int MigrateMinutes(int seconds, int min, int max, int def)
	{
		if (seconds <= 0)
		{
			return def;
		}
		return Clamp((int)Math.Round((double)seconds / 60.0, MidpointRounding.AwayFromZero), min, max);
	}

	public void SetCountdownMinutes(int minutes)
	{
		if (!CountIsRunning)
		{
			SetCountdown(Math.Max(1, Math.Min(720, minutes)) * 60);
			RaiseSettingsChanged();
		}
	}

	public void SetHydrationTarget(int glasses)
	{
		HydrationDailyTarget = Math.Max(1, Math.Min(30, glasses));
		SaveState();
		EventHandler hydrationChanged = this.HydrationChanged;
		if (hydrationChanged != null)
		{
			try
			{
				hydrationChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	private void RaiseSettingsChanged()
	{
		EventHandler settingsChanged = this.SettingsChanged;
		if (settingsChanged != null)
		{
			try
			{
				settingsChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public void ResetCycles()
	{
		PomoCycles = 0;
		PomoCurrentCycle = 1;
		SaveState();
	}

	public void SetHydration(bool enabled, int intervalMinutes)
	{
		HydrationEnabled = enabled;
		HydrationIntervalMinutes = Math.Max(5, intervalMinutes);
		HydrationRemainingSeconds = HydrationIntervalMinutes * 60;
		SaveState();
		EventHandler hydrationChanged = this.HydrationChanged;
		if (hydrationChanged != null)
		{
			try
			{
				hydrationChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public void AddHydrationGlass()
	{
		if (HydrationGlassesConsumed < 99)
		{
			HydrationGlassesConsumed++;
		}
		SaveState();
		EventHandler hydrationChanged = this.HydrationChanged;
		if (hydrationChanged != null)
		{
			try
			{
				hydrationChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public void RemoveHydrationGlass()
	{
		if (HydrationGlassesConsumed > 0)
		{
			HydrationGlassesConsumed--;
		}
		SaveState();
		EventHandler hydrationChanged = this.HydrationChanged;
		if (hydrationChanged != null)
		{
			try
			{
				hydrationChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public void ResetHydrationGlasses()
	{
		HydrationGlassesConsumed = 0;
		SaveState();
		EventHandler hydrationChanged = this.HydrationChanged;
		if (hydrationChanged != null)
		{
			try
			{
				hydrationChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	private void PlayChime()
	{
		lock (_chimeLock)
		{
			DateTime utcNow = DateTime.UtcNow;
			if ((utcNow - _lastChimeTime).TotalMilliseconds < 1000.0)
			{
				return;
			}
			_lastChimeTime = utcNow;
		}
		try
		{
			if (_cachedChimeWav == null)
			{
				lock (_chimeLock)
				{
					if (_cachedChimeWav == null)
					{
						_cachedChimeWav = GenerateChimeWav();
					}
				}
			}
			using MemoryStream stream = new MemoryStream(_cachedChimeWav);
			using SoundPlayer soundPlayer = new SoundPlayer(stream);
			soundPlayer.Play();
		}
		catch
		{
			try
			{
				SystemSounds.Asterisk.Play();
			}
			catch
			{
			}
		}
	}

	private static byte[] GenerateChimeWav()
	{
		int num = 44100;
		int num2 = 650;
		int num3 = num * num2 / 1000;
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
		binaryWriter.Write(36 + num3 * 2);
		binaryWriter.Write(Encoding.ASCII.GetBytes("WAVE"));
		binaryWriter.Write(Encoding.ASCII.GetBytes("fmt "));
		binaryWriter.Write(16);
		binaryWriter.Write((short)1);
		binaryWriter.Write((short)1);
		binaryWriter.Write(num);
		binaryWriter.Write(num * 2);
		binaryWriter.Write((short)2);
		binaryWriter.Write((short)16);
		binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
		binaryWriter.Write(num3 * 2);
		for (int i = 0; i < num3; i++)
		{
			double num4 = (double)i / (double)num;
			double num5 = Math.Exp(-5.5 * num4);
			double num6 = 0.55 * Math.Sin(4142.189913758142 * num4) + 0.35 * Math.Sin(Math.PI * 1760.0 * num4);
			short value = (short)(num6 * num5 * 22000.0);
			binaryWriter.Write(value);
		}
		return memoryStream.ToArray();
	}

	internal static string Clock(int seconds)
	{
		seconds = Math.Max(0, seconds);
		int num = seconds / 3600;
		int num2 = seconds % 3600 / 60;
		int num3 = seconds % 60;
		if (num <= 0)
		{
			return $"{num2:D2}:{num3:D2}";
		}
		return $"{num}:{num2:D2}:{num3:D2}";
	}

	internal static string ReminderDuration(int seconds)
	{
		if (seconds < 60)
		{
			return Math.Max(1, seconds) + " s";
		}
		int num = seconds / 3600;
		int num2 = seconds % 3600 / 60;
		int num3 = seconds % 60;
		if (num > 0)
		{
			if (num2 <= 0)
			{
				return num + " h";
			}
			return $"{num} h {num2} min";
		}
		if (num3 <= 0 || num2 >= 5)
		{
			return num2 + " min";
		}
		return $"{num2} min {num3} s";
	}

	private void TriggerAlert(string message)
	{
		if (this.Alert != null)
		{
			try
			{
				this.Alert(this, message);
			}
			catch
			{
			}
		}
	}

	public void SaveState()
	{
		UpdateClockSchedule();
		try
		{
			long num = _stopwatchAccumulatedTicks;
			if (StopwatchIsRunning)
			{
				num += Stopwatch.GetTimestamp() - _stopwatchStartTicks;
			}
			List<string> list = new List<string>();
			if (StopwatchLaps != null)
			{
				foreach (TimeSpan stopwatchLap in StopwatchLaps)
				{
					list.Add(stopwatchLap.ToString());
				}
			}
			TimerServiceState timerServiceState = new TimerServiceState();
			timerServiceState.PomoAutoStartNext = PomoAutoStartNext;
			timerServiceState.CountLabel = CountLabel;
			timerServiceState.CountCustomPresets = new List<int>(CountCustomPresets);
			timerServiceState.HydrationHistory = new Dictionary<string, int>(HydrationHistory);
			timerServiceState.PomoWorkSeconds = PomoWorkSeconds;
			timerServiceState.PomoBreakSeconds = PomoBreakSeconds;
			timerServiceState.PomoLongBreakSeconds = PomoLongBreakSeconds;
			timerServiceState.PomoRemainingSeconds = PomoRemainingSeconds;
			timerServiceState.PomoIsWork = PomoIsWork;
			timerServiceState.PomoCycles = PomoCycles;
			timerServiceState.PomoCurrentCycle = PomoCurrentCycle;
			timerServiceState.PomoTargetCycles = PomoTargetCycles;
			timerServiceState.PomoIsRunning = PomoIsRunning;
			timerServiceState.PomoIsPaused = PomoIsRunning && PomoIsPaused;
			timerServiceState.PomoSessionsToday = PomoSessionsToday;
			timerServiceState.CountTotalSeconds = CountTotalSeconds;
			timerServiceState.CountRemainingSeconds = CountRemainingSeconds;
			timerServiceState.CountIsRunning = CountIsRunning;
			timerServiceState.CountIsPaused = CountIsRunning && CountIsPaused;
			timerServiceState.StopwatchIsRunning = StopwatchIsRunning;
			timerServiceState.StopwatchElapsedTicks = num;
			timerServiceState.StopwatchLaps = list;
			timerServiceState.HydrationEnabled = HydrationEnabled;
			timerServiceState.HydrationIntervalMinutes = HydrationIntervalMinutes;
			timerServiceState.HydrationRemainingSeconds = HydrationRemainingSeconds;
			timerServiceState.HydrationGlassesConsumed = HydrationGlassesConsumed;
			timerServiceState.HydrationDailyTarget = HydrationDailyTarget;
			timerServiceState.LastRecordedDate = _currentDateString;
			timerServiceState.SavedAtUtcTicks = DateTime.UtcNow.Ticks;
			TimerServiceState pendingSave = timerServiceState;
			lock (_saveLock)
			{
				_pendingSave = pendingSave;
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
					TimerServiceState pendingSave2;
					lock (_saveLock)
					{
						pendingSave2 = _pendingSave;
						_pendingSave = null;
						if (pendingSave2 == null)
						{
							_saving = false;
							_saved.Set();
							break;
						}
					}
					try
					{
						PersistenceHelper.SafeWriteAllText(_storagePath, new JavaScriptSerializer().Serialize(pendingSave2));
					}
					catch (Exception ex)
					{
						TransactionLogger.Warn("Timer save failed: " + ex.GetType().Name);
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
		bool enabled = _clockTimer.IsEnabled;
		SaveState();
		_clockTimer.Stop();
		_midnightTimer.Stop();
		return delegate
		{
			if (enabled)
			{
				_clockTimer.Start();
			}
			ScheduleMidnight();
		};
	}

	public void WaitForPersistence()
	{
		if (!_saved.Wait(2000))
		{
			throw new TimeoutException("Timers remain pending.");
		}
	}

	public void Shutdown()
	{
		_clockTimer.Stop();
		_midnightTimer.Stop();
		if (!_saved.Wait(TimeSpan.FromSeconds(3.0)))
		{
			TransactionLogger.Error("Timer write still outstanding at disposal.");
		}
	}

	private void LoadState()
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_storagePath);
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			TimerServiceState timerServiceState = javaScriptSerializer.Deserialize<TimerServiceState>(text);
			if (timerServiceState == null)
			{
				return;
			}
			PomoAutoStartNext = timerServiceState.PomoAutoStartNext;
			CountLabel = (timerServiceState.CountLabel ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (CountLabel.Length > 40)
			{
				CountLabel = CountLabel.Substring(0, 40);
			}
			CountCustomPresets = (timerServiceState.CountCustomPresets ?? new List<int>()).Where((int v) => v > 0 && v <= 43200).Distinct().Take(12)
				.ToList();
			HydrationHistory = timerServiceState.HydrationHistory ?? new Dictionary<string, int>();
			ArchiveHydrationDay(timerServiceState.LastRecordedDate, timerServiceState.HydrationGlassesConsumed);
			int num = MigrateMinutes(timerServiceState.PomoWorkSeconds, 1, 120, 25);
			int num2 = MigrateMinutes(timerServiceState.PomoBreakSeconds, 1, 30, 5);
			int num3 = Math.Max(num2, MigrateMinutes(timerServiceState.PomoLongBreakSeconds, 5, 60, 15));
			PomoWorkSeconds = num * 60;
			PomoBreakSeconds = num2 * 60;
			PomoLongBreakSeconds = num3 * 60;
			PomoTargetCycles = ((timerServiceState.PomoTargetCycles > 0) ? Clamp(timerServiceState.PomoTargetCycles, 1, 10) : 4);
			PomoIsWork = timerServiceState.PomoIsWork;
			PomoCurrentCycle = ((timerServiceState.PomoCurrentCycle <= 0) ? 1 : Math.Min(timerServiceState.PomoCurrentCycle, PomoTargetCycles));
			PomoRemainingSeconds = ((timerServiceState.PomoRemainingSeconds > 0) ? Math.Min(timerServiceState.PomoRemainingSeconds, PomoPhaseTotalSeconds) : PomoPhaseTotalSeconds);
			CountTotalSeconds = ((timerServiceState.CountTotalSeconds > 0) ? timerServiceState.CountTotalSeconds : 900);
			CountRemainingSeconds = ((timerServiceState.CountRemainingSeconds > 0) ? timerServiceState.CountRemainingSeconds : CountTotalSeconds);
			HydrationEnabled = timerServiceState.HydrationEnabled;
			HydrationIntervalMinutes = ((timerServiceState.HydrationIntervalMinutes > 0) ? timerServiceState.HydrationIntervalMinutes : 45);
			HydrationRemainingSeconds = ((timerServiceState.HydrationRemainingSeconds > 0) ? timerServiceState.HydrationRemainingSeconds : (HydrationIntervalMinutes * 60));
			HydrationDailyTarget = ((timerServiceState.HydrationDailyTarget > 0) ? timerServiceState.HydrationDailyTarget : 8);
			string text2 = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			if (!string.IsNullOrEmpty(timerServiceState.LastRecordedDate) && timerServiceState.LastRecordedDate != text2)
			{
				PomoCycles = 0;
				PomoCurrentCycle = 1;
				PomoSessionsToday = 0;
				HydrationGlassesConsumed = 0;
				_currentDateString = text2;
			}
			else
			{
				PomoCycles = timerServiceState.PomoCycles;
				PomoSessionsToday = Math.Max(0, timerServiceState.PomoSessionsToday);
				HydrationGlassesConsumed = ((timerServiceState.HydrationGlassesConsumed >= 0) ? timerServiceState.HydrationGlassesConsumed : 0);
				_currentDateString = text2;
			}
			if (!timerServiceState.PomoIsRunning)
			{
				PomoRemainingSeconds = PomoPhaseTotalSeconds;
			}
			PomoIsRunning = timerServiceState.PomoIsRunning;
			PomoIsPaused = timerServiceState.PomoIsRunning && timerServiceState.PomoIsPaused;
			CountIsRunning = timerServiceState.CountIsRunning;
			CountIsPaused = timerServiceState.CountIsRunning && timerServiceState.CountIsPaused;
			long num4 = (DateTime.UtcNow.Ticks - timerServiceState.SavedAtUtcTicks) / 10000000;
			if (num4 > 0 && num4 < 86400)
			{
				if (timerServiceState.PomoIsRunning && !PomoIsPaused)
				{
					PomoRemainingSeconds = Math.Max(0, PomoRemainingSeconds - (int)num4);
					if (PomoRemainingSeconds <= 0)
					{
						PomoRemainingSeconds = 0;
						PomoIsRunning = false;
						PomoIsPaused = false;
					}
				}
				if (timerServiceState.CountIsRunning && !CountIsPaused)
				{
					CountRemainingSeconds = Math.Max(0, CountRemainingSeconds - (int)num4);
					if (CountRemainingSeconds <= 0)
					{
						CountRemainingSeconds = 0;
						CountIsRunning = false;
						CountIsPaused = false;
					}
				}
			}
			if (PomoIsRunning)
			{
				_pomoTargetTicks = PomoRemainingSeconds * Stopwatch.Frequency;
				_pomoStartTicks = Stopwatch.GetTimestamp();
				_pomoAccumulatedTicks = 0L;
				PillActivityArbiter.Instance.PublishTimerSession(PomodoroFormattedTime, PomoIsPaused);
			}
			if (CountIsRunning)
			{
				_countTargetTicks = CountRemainingSeconds * Stopwatch.Frequency;
				_countStartTicks = Stopwatch.GetTimestamp();
				_countAccumulatedTicks = 0L;
				string display = CountDisplay();
				PillActivityArbiter.Instance.PublishTimerSession(display, CountIsPaused);
			}
			_stopwatchAccumulatedTicks = timerServiceState.StopwatchElapsedTicks;
			if (_stopwatchAccumulatedTicks < 0 || (double)_stopwatchAccumulatedTicks / (double)Stopwatch.Frequency >= TimeSpan.MaxValue.TotalSeconds - 86400.0)
			{
				_stopwatchAccumulatedTicks = 0L;
			}
			if (timerServiceState.StopwatchIsRunning)
			{
				if (num4 > 0 && num4 < 86400)
				{
					_stopwatchAccumulatedTicks += num4 * Stopwatch.Frequency;
				}
				_stopwatchStartTicks = Stopwatch.GetTimestamp();
				StopwatchIsRunning = true;
			}
			else
			{
				StopwatchIsRunning = false;
			}
			StopwatchLaps = new List<TimeSpan>();
			if (timerServiceState.StopwatchLaps == null)
			{
				return;
			}
			foreach (string stopwatchLap in timerServiceState.StopwatchLaps)
			{
				if (TimeSpan.TryParse(stopwatchLap, out var result))
				{
					StopwatchLaps.Add(result);
				}
			}
		}
		catch
		{
		}
	}
}
