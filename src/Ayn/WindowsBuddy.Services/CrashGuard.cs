using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class CrashGuard
{
	private const uint SEM_FAILCRITICALERRORS = 1u;

	private const uint SEM_NOGPFAULTERRORBOX = 2u;

	private const uint SEM_NOOPENFILEERRORBOX = 32768u;

	private const uint WER_FAULT_REPORTING_NO_UI = 32u;

	private const int RESTART_NO_REBOOT = 8;

	internal const int CrashRestartFlags = 8;

	internal const int RebootRestoreFlags = 0;

	public const string RestoredArgument = "restored";

	public const int MaxRelaunches = 2;

	private const string MarkerName = "crash-relaunch.marker";

	private static readonly object RestartSync = new object();

	private static bool _restoreAfterReboot;

	private static bool _unregistered;

	public static readonly TimeSpan RelaunchWindow = TimeSpan.FromMinutes(10.0);

	private static bool _relaunchAllowed;

	private static int _dumpsThisMinute;

	private static DateTime _dumpMinute = DateTime.MinValue;

	private static int _relaunching;

	internal static string RegisteredCommand { get; private set; }

	internal static int RegisteredFlags { get; private set; }

	public static bool RecentlyRelaunched => RecentRelaunches().Count > 0;

	private static string MarkerPath => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "logs", "crash-relaunch.marker");

	[DllImport("kernel32.dll")]
	private static extern uint SetErrorMode(uint mode);

	[DllImport("kernel32.dll")]
	private static extern int WerSetFlags(uint flags);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int RegisterApplicationRestart(string commandLine, int flags);

	[DllImport("kernel32.dll")]
	private static extern int UnregisterApplicationRestart();

	private static void Register(string command, int flags)
	{
		if (!_relaunchAllowed || RecentMarker())
		{
			return;
		}
		try
		{
			RegisterApplicationRestart(command, flags);
			RegisteredCommand = command;
			RegisteredFlags = flags;
		}
		catch
		{
		}
	}

	public static void SetRestoreAfterReboot(bool startWithWindows)
	{
		lock (RestartSync)
		{
			if (_unregistered)
			{
				return;
			}
			_restoreAfterReboot = startWithWindows;
			Register("capsule", (!startWithWindows) ? 8 : 0);
		}
		TransactionLogger.Info("Restart registration: " + (startWithWindows ? "crash, hang and Windows restart (startup is on)" : "crash and hang only (startup is off)") + ".");
	}

	public static void PrepareForSessionEnd()
	{
		lock (RestartSync)
		{
			if (!_unregistered && _restoreAfterReboot)
			{
				Register("capsule restored", 0);
			}
		}
	}

	public static void PrepareForUpdate()
	{
		lock (RestartSync)
		{
			if (!_unregistered)
			{
				Register("capsule restored", (!_restoreAfterReboot) ? 8 : 0);
			}
		}
	}

	public static void UnregisterForQuit()
	{
		lock (RestartSync)
		{
			_unregistered = true;
			try
			{
				UnregisterApplicationRestart();
			}
			catch
			{
			}
			RegisteredCommand = "";
			RegisteredFlags = 0;
		}
	}

	private static bool RecentMarker()
	{
		return RecentRelaunches().Count >= 2;
	}

	public static void Install(bool relaunchAllowed)
	{
		_relaunchAllowed = relaunchAllowed;
		try
		{
			SetErrorMode(32771u);
			WerSetFlags(32u);
		}
		catch
		{
		}
		if (!relaunchAllowed)
		{
			return;
		}
		try
		{
			Register("capsule", 8);
		}
		catch
		{
		}
	}

	public static bool ShouldWriteDump()
	{
		DateTime utcNow = DateTime.UtcNow;
		utcNow = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0, DateTimeKind.Utc);
		if (utcNow != _dumpMinute)
		{
			_dumpMinute = utcNow;
			_dumpsThisMinute = 0;
		}
		return ++_dumpsThisMinute <= 10;
	}

	public static void OnUnhandled(Exception ex, bool terminating)
	{
		if (!terminating || !_relaunchAllowed || Interlocked.Exchange(ref _relaunching, 1) == 1)
		{
			return;
		}
		try
		{
			try
			{
				UnregisterApplicationRestart();
			}
			catch
			{
			}
			if (TryConsumeRelaunch("fatal exception"))
			{
				string fileName = Process.GetCurrentProcess().MainModule.FileName;
				ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName, "relaunch " + Process.GetCurrentProcess().Id);
				processStartInfo.UseShellExecute = false;
				ProcessStartInfo startInfo = processStartInfo;
				Process.Start(startInfo);
			}
		}
		catch (Exception error)
		{
			CrashDiagnostics.Record(CrashBoundary.AppDomain, error, terminating: true);
		}
	}

	public static void ScheduleQaCrash(int afterMs)
	{
		if (AppPaths.IsSandboxed)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				Thread.Sleep(afterMs);
				throw new InvalidOperationException("QA: deliberate background-thread crash");
			});
			thread.IsBackground = true;
			thread.Name = "Ayn.QaCrash";
			Thread thread2 = thread;
			thread2.Start();
		}
	}

	public static bool TryConsumeRelaunch(string reason)
	{
		List<DateTime> list = RecentRelaunches();
		if (list.Count < 2)
		{
			list.Add(DateTime.UtcNow);
			WriteRelaunches(list);
			return true;
		}
		try
		{
			TransactionLogger.Warn("Crash guard: " + list.Count + " automatic restarts in " + (int)RelaunchWindow.TotalMinutes + " min; not restarting again (" + (reason ?? "unknown") + ").");
		}
		catch
		{
		}
		ClearRelaunches();
		return false;
	}

	public static void ClearRelaunches()
	{
		try
		{
			File.Delete(MarkerPath);
		}
		catch
		{
		}
	}

	public static int RunStoppedPrompt(string oldPid)
	{
		if (int.TryParse(oldPid, out var result))
		{
			try
			{
				Process processById = Process.GetProcessById(result);
				if (!processById.WaitForExit(5000))
				{
					processById.Kill();
				}
			}
			catch
			{
			}
		}
		return 0;
	}

	private static List<DateTime> RecentRelaunches()
	{
		List<DateTime> list = new List<DateTime>();
		try
		{
			string markerPath = MarkerPath;
			if (!File.Exists(markerPath))
			{
				return list;
			}
			string[] array = File.ReadAllLines(markerPath);
			foreach (string text in array)
			{
				if (long.TryParse(text.Trim(), out var result))
				{
					DateTime dateTime = new DateTime(result, DateTimeKind.Utc);
					if (DateTime.UtcNow - dateTime < RelaunchWindow)
					{
						list.Add(dateTime);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static void WriteRelaunches(List<DateTime> times)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath));
			string[] array = new string[times.Count];
			for (int i = 0; i < times.Count; i++)
			{
				array[i] = times[i].Ticks.ToString();
			}
			File.WriteAllLines(MarkerPath, array);
		}
		catch
		{
		}
	}
}
