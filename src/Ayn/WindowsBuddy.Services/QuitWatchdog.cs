using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

internal static class QuitWatchdog
{
	internal static Action<int> ExitAction = delegate(int code)
	{
		ExitNow(code, "watchdog");
	};

	internal static bool ForceNativeExitForQa;

	internal static Action<int> EndProcessForQa;

	internal static int ForceAfterCommitMs = 1500;

	internal static int OuterLimitMs = 30000;

	private static int _generation;

	internal static bool EnabledInSandboxForQa;

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool TerminateProcess(IntPtr process, uint code);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr GetModuleHandle(string name);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int capacity);

	private static bool UnsafeGraphicsTeardown()
	{
		if (AppPaths.IsSandboxed && ForceNativeExitForQa)
		{
			return true;
		}
		if (Environment.OSVersion.Version.Build != 26300)
		{
			return false;
		}
		try
		{
			IntPtr moduleHandle = GetModuleHandle("igd10umt64xe.dll");
			if (moduleHandle == IntPtr.Zero)
			{
				return false;
			}
			StringBuilder stringBuilder = new StringBuilder(1024);
			if (GetModuleFileName(moduleHandle, stringBuilder, stringBuilder.Capacity) == 0)
			{
				return false;
			}
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(stringBuilder.ToString());
			return versionInfo.FileMajorPart == 32 && versionInfo.FileMinorPart == 0 && versionInfo.FileBuildPart == 101 && versionInfo.FilePrivatePart == 8132;
		}
		catch
		{
			return false;
		}
	}

	internal static void ExitNow(int code, string why)
	{
		Trace("Quit: process exit (" + why + ")");
		if (code == 0 && UnsafeGraphicsTeardown())
		{
			CrashDiagnostics.MarkEnding("quit");
		}
		EndProcess(code);
	}

	internal static void EndProcess(int code)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			Thread.Sleep(1200);
			TerminateProcess(GetCurrentProcess(), (uint)code);
		});
		thread.IsBackground = true;
		thread.Name = "Ayn native exit guard";
		thread.Start();
		try
		{
			LogWriter.Flush(300);
		}
		catch
		{
		}
		try
		{
			Console.Out.Flush();
			Console.Error.Flush();
		}
		catch
		{
		}
		CrashDiagnostics.ReleaseNativeFilter();
		if (EndProcessForQa != null)
		{
			EndProcessForQa(code);
		}
		else if (!TerminateProcess(GetCurrentProcess(), (uint)code))
		{
			Trace("Quit: native exit failed Win32=" + Marshal.GetLastWin32Error() + "; using Environment.Exit");
			Environment.Exit(code);
		}
	}

	internal static void Trace(string message)
	{
		try
		{
			string text = Path.Combine(AppPaths.DataDir, "logs");
			Directory.CreateDirectory(text);
			File.AppendAllText(Path.Combine(text, "quit-trace.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
		}
		catch
		{
		}
	}

	internal static void Start(Func<bool> committed)
	{
		if (AppPaths.IsSandboxed && !EnabledInSandboxForQa)
		{
			return;
		}
		int generation = Interlocked.Increment(ref _generation);
		Thread thread = new Thread((ThreadStart)delegate
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			long num = -1L;
			bool flag = false;
			while (generation == Volatile.Read(ref _generation))
			{
				Thread.Sleep(50);
				bool flag2 = false;
				try
				{
					flag2 = committed();
				}
				catch
				{
				}
				if (flag2 && num < 0)
				{
					num = stopwatch.ElapsedMilliseconds;
					Trace("Quit watchdog: save-and-drain complete; the process has " + (double)ForceAfterCommitMs / 1000.0 + " s to exit");
				}
				if (num >= 0 && stopwatch.ElapsedMilliseconds - num >= ForceAfterCommitMs)
				{
					Trace("Quit watchdog: forcing exit");
					try
					{
						TransactionLogger.Warn("Quit watchdog: forcing exit");
						LogWriter.Flush(500);
					}
					catch
					{
					}
					ExitAction(0);
					break;
				}
				if (num < 0 && !flag && stopwatch.ElapsedMilliseconds >= OuterLimitMs)
				{
					flag = true;
					Trace("Quit watchdog: save-and-drain still running after " + OuterLimitMs / 1000 + " s; not forcing exit so no data is lost");
					try
					{
						TransactionLogger.Warn("Quit watchdog: save-and-drain has not finished after " + OuterLimitMs / 1000 + " s; not forcing exit so no data is lost");
					}
					catch
					{
					}
				}
			}
		});
		thread.IsBackground = true;
		thread.Name = "Quit watchdog";
		Thread thread2 = thread;
		thread2.Start();
	}

	internal static void Cancel()
	{
		Interlocked.Increment(ref _generation);
	}
}
