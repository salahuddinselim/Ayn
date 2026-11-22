using System;
using System.Collections.Generic;
using System.Globalization;

namespace WindowsBuddy.Tools.Timer;

public class TimerServiceState
{
	public int PomoWorkSeconds { get; set; }

	public int PomoBreakSeconds { get; set; }

	public int PomoLongBreakSeconds { get; set; }

	public int PomoRemainingSeconds { get; set; }

	public bool PomoIsWork { get; set; }

	public int PomoCycles { get; set; }

	public int PomoCurrentCycle { get; set; }

	public int PomoTargetCycles { get; set; }

	public bool PomoIsRunning { get; set; }

	public bool PomoIsPaused { get; set; }

	public int PomoSessionsToday { get; set; }

	public bool PomoAutoStartNext { get; set; }

	public string CountLabel { get; set; }

	public List<int> CountCustomPresets { get; set; }

	public Dictionary<string, int> HydrationHistory { get; set; }

	public int CountTotalSeconds { get; set; }

	public int CountRemainingSeconds { get; set; }

	public bool CountIsRunning { get; set; }

	public bool CountIsPaused { get; set; }

	public bool StopwatchIsRunning { get; set; }

	public long StopwatchElapsedTicks { get; set; }

	public List<string> StopwatchLaps { get; set; }

	public bool HydrationEnabled { get; set; }

	public int HydrationIntervalMinutes { get; set; }

	public int HydrationRemainingSeconds { get; set; }

	public int HydrationGlassesConsumed { get; set; }

	public int HydrationDailyTarget { get; set; }

	public string LastRecordedDate { get; set; }

	public long SavedAtUtcTicks { get; set; }

	public TimerServiceState()
	{
		PomoAutoStartNext = true;
		CountLabel = "";
		CountCustomPresets = new List<int>();
		HydrationHistory = new Dictionary<string, int>();
		PomoWorkSeconds = 1500;
		PomoBreakSeconds = 300;
		PomoLongBreakSeconds = 900;
		PomoRemainingSeconds = 1500;
		PomoIsWork = true;
		PomoCycles = 0;
		PomoCurrentCycle = 1;
		PomoTargetCycles = 4;
		PomoIsRunning = false;
		CountTotalSeconds = 300;
		CountRemainingSeconds = 300;
		CountIsRunning = false;
		StopwatchIsRunning = false;
		StopwatchElapsedTicks = 0L;
		StopwatchLaps = new List<string>();
		HydrationEnabled = true;
		HydrationIntervalMinutes = 45;
		HydrationRemainingSeconds = 2700;
		HydrationGlassesConsumed = 0;
		HydrationDailyTarget = 8;
		LastRecordedDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		SavedAtUtcTicks = DateTime.UtcNow.Ticks;
	}
}
