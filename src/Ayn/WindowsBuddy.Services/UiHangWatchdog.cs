using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Tools;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.Services;

public static class UiHangWatchdog
{
	private const int MaxDumps = 3;

	private const int ActivePingMs = 500;

	private const int RestingPingMs = 2000;

	private static Process _dumpHelper;

	private static readonly TimeSpan DumpMaxAge = TimeSpan.FromDays(7.0);

	private static Thread _watchdogThread;

	private static Dispatcher _dispatcher;

	private static Thread _uiThread;

	private static volatile bool _running;

	private static volatile bool _pingPending;

	private static long _pingSentMs;

	private static long _lastReportMs = -4611686018427387904L;

	private static volatile bool _hangNoted;

	private static int _hangReportCount = 0;

	private static long _pingSentTicks;

	private static uint _uiNativeThreadId;

	internal static Func<long> Clock = () => (!QueryUnbiasedInterruptTime(out var unbiasedTime)) ? ((long)Environment.TickCount) : ((long)(unbiasedTime / 10000));

	internal static Func<bool> Resting = delegate
	{
		CapsuleWindow instance = CapsuleWindow.Instance;
		if (instance == null || SettingsLauncher.IsOpen)
		{
			return false;
		}
		NotchPhase notchPhase = instance.NotchPhase;
		return notchPhase == NotchPhase.Resting || notchPhase == NotchPhase.Hidden;
	};

	public static int HangReportCount => _hangReportCount;

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("kernel32.dll")]
	private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);

	public static void Start(Dispatcher dispatcher, Thread uiThread = null)
	{
		if (!_running)
		{
			_dispatcher = dispatcher;
			_uiThread = uiThread ?? Thread.CurrentThread;
			_uiNativeThreadId = GetCurrentThreadId();
			_running = true;
			_pingPending = false;
			Thread thread = new Thread(WatchdogLoop);
			thread.Name = "Ayn.UiHangWatchdog";
			thread.IsBackground = true;
			thread.Priority = ThreadPriority.AboveNormal;
			_watchdogThread = thread;
			_watchdogThread.Start();
			TransactionLogger.Info("UiHangWatchdog started (500 ms ping, 2 s while the notch rests; 2 s hang threshold; external dump; process preserved).");
		}
	}

	public static void Stop()
	{
		_running = false;
	}

	private static void WatchdogLoop()
	{
		while (_running)
		{
			try
			{
				Thread.Sleep(500);
				if (!_running)
				{
					break;
				}
				bool flag = false;
				try
				{
					flag = Resting();
				}
				catch
				{
				}
				if (!_pingPending && (!flag || Clock() - Interlocked.Read(ref _pingSentMs) >= 2000))
				{
					_pingPending = true;
					Interlocked.Exchange(ref _pingSentMs, Clock());
					Interlocked.Exchange(ref _pingSentTicks, Stopwatch.GetTimestamp());
					try
					{
						_dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
						{
							ResourceMetrics.RecordDispatcherDelay(Stopwatch.GetTimestamp() - Interlocked.Read(ref _pingSentTicks));
							_pingPending = false;
							if (_hangNoted)
							{
								_hangNoted = false;
								CrashDiagnostics.HangEnded();
							}
						});
					}
					catch
					{
						_pingPending = false;
					}
					continue;
				}
				long num = Clock();
				double num2 = num - Interlocked.Read(ref _pingSentMs);
				if (num2 >= 2000.0)
				{
					_hangNoted = true;
					CrashDiagnostics.NoteExitReason("hang");
					if (num - _lastReportMs >= 4000)
					{
						_lastReportMs = num;
						Interlocked.Increment(ref _hangReportCount);
						WriteHangReport(num2);
					}
				}
			}
			catch (ThreadAbortException)
			{
				break;
			}
			catch (Exception ex2)
			{
				try
				{
					TransactionLogger.Warn("Watchdog error: " + ex2.Message);
				}
				catch
				{
				}
			}
		}
	}

	private static void WriteHangReport(double hangMs)
	{
		try
		{
			string text = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "logs");
			Directory.CreateDirectory(text);
			string arg = DateTime.Now.ToString("yyyyMMdd-HHmmss");
			string path = Path.Combine(text, $"hang-{arg}.txt");
			string text2 = Path.Combine(text, $"hang-{arg}.dmp");
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("================================================================================");
			stringBuilder.AppendLine($"VAYLI UI-THREAD HANG REPORT: {DateTime.Now} ({hangMs:F0} ms unresponsive)");
			stringBuilder.AppendLine("================================================================================");
			stringBuilder.AppendLine($"Process ID: {Process.GetCurrentProcess().Id}");
			stringBuilder.AppendLine($"Hang Duration: {hangMs / 1000.0:F1} s");
			stringBuilder.AppendLine("Last UI interaction snapshot: " + CapsuleWindow.LastInteractionSnapshot);
			stringBuilder.AppendLine("UI native thread ID: " + _uiNativeThreadId);
			stringBuilder.AppendLine("Current process metrics (no dispatcher wait): " + ResourceMetrics.Take(fullGc: false, includeUi: false).ToLine());
			stringBuilder.AppendLine("Last completed UI/subscription sample: " + ResourceMetrics.LastCompleteLine);
			try
			{
				CapsuleWindow instance = CapsuleWindow.Instance;
				if (instance != null && instance.Machine != null)
				{
					NotchStateMachine machine = instance.Machine;
					stringBuilder.AppendLine($"Notch State: Phase={machine.Phase}, Presence={machine.Presence}, PointerInside={machine.PointerInside}, MsInPhase={machine.MsInPhase}");
				}
				else
				{
					stringBuilder.AppendLine("Notch State: CapsuleWindow or Machine is null");
				}
			}
			catch (Exception ex)
			{
				stringBuilder.AppendLine("Error reading Notch State: " + ex.Message);
			}
			try
			{
				IBuddyTool buddyTool = ((ToolRegistry.Instance != null) ? ToolRegistry.Instance.ActiveTool : null);
				stringBuilder.AppendLine(string.Format("Active Tab: {0} ({1})", (buddyTool != null) ? buddyTool.Id : "none", (buddyTool != null) ? buddyTool.DisplayName : "none"));
			}
			catch (Exception ex2)
			{
				stringBuilder.AppendLine("Error reading Active Tab: " + ex2.Message);
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("--------------------------------------------------------------------------------");
			stringBuilder.AppendLine("UI THREAD STACK CAPTURE");
			stringBuilder.AppendLine("--------------------------------------------------------------------------------");
			stringBuilder.AppendLine("UI stack and thread context are in the paired minidump; select native thread ID above with a debugger and CLR stack support.");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("--------------------------------------------------------------------------------");
			stringBuilder.AppendLine("PROCESS THREADS DIAGNOSTICS");
			stringBuilder.AppendLine("--------------------------------------------------------------------------------");
			try
			{
				using Process process = Process.GetCurrentProcess();
				foreach (ProcessThread thread in process.Threads)
				{
					using (thread)
					{
						string text3 = thread.ThreadState.ToString();
						if (thread.ThreadState == System.Diagnostics.ThreadState.Wait)
						{
							object obj = text3;
							text3 = string.Concat(obj, " (WaitReason=", thread.WaitReason, ")");
						}
						stringBuilder.AppendLine($"Thread ID: {thread.Id,6} | State: {text3,-28} | UserTime: {thread.UserProcessorTime} | TotalTime: {thread.TotalProcessorTime}");
					}
				}
			}
			catch (Exception ex3)
			{
				stringBuilder.AppendLine("Error enumerating process threads: " + ex3.Message);
			}
			File.WriteAllText(path, stringBuilder.ToString(), Encoding.UTF8);
			try
			{
				if (_dumpHelper != null && _dumpHelper.HasExited)
				{
					_dumpHelper.Dispose();
					_dumpHelper = null;
				}
				if (_dumpHelper == null)
				{
					using Process process2 = Process.GetCurrentProcess();
					ProcessStartInfo processStartInfo = new ProcessStartInfo(process2.MainModule.FileName, "diagnostic-dump " + process2.Id + " \"" + text2 + "\"");
					processStartInfo.UseShellExecute = false;
					processStartInfo.CreateNoWindow = true;
					processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					ProcessStartInfo startInfo = processStartInfo;
					_dumpHelper = Process.Start(startInfo);
					HelperProcessJob.Adopt(_dumpHelper);
					if (_dumpHelper.WaitForExit(5000) && _dumpHelper.ExitCode != 0)
					{
						TransactionLogger.Warn("Watchdog dump helper failed: exit " + _dumpHelper.ExitCode);
					}
				}
			}
			catch (Exception ex4)
			{
				TransactionLogger.Warn("Watchdog minidump error: " + ex4.Message);
			}
			PruneDumps(text);
			TransactionLogger.Warn($"UI Hang watchdog captured report: {Path.GetFileName(path)} ({hangMs:F0} ms)");
		}
		catch (Exception ex5)
		{
			try
			{
				TransactionLogger.Error("Failed to write hang report: " + ex5.Message);
			}
			catch
			{
			}
		}
	}

	internal static void PruneDumps(string logsDir)
	{
		try
		{
			FileInfo[] files = new DirectoryInfo(logsDir).GetFiles("*.dmp");
			Array.Sort(files, (FileInfo a, FileInfo b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
			for (int i = 0; i < files.Length; i++)
			{
				if (i >= 3 || DateTime.UtcNow - files[i].LastWriteTimeUtc > DumpMaxAge)
				{
					try
					{
						files[i].Delete();
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
	}
}
