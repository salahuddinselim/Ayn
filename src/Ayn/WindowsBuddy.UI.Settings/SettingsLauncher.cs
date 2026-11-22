using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.UI.Settings;

public static class SettingsLauncher
{
	private static EventWaitHandle _event;

	private static RegisteredWaitHandle _wait;

	private static EventWaitHandle _backgroundEvent;

	private static RegisteredWaitHandle _backgroundWait;

	private static EventWaitHandle _quitEvent;

	private static RegisteredWaitHandle _quitWait;

	public static Action Redirect;

	internal static readonly TimeSpan StartupRaceWindow = TimeSpan.FromSeconds(60.0);

	private static string EventName => "Local\\Ayn_OpenSettings" + AppPaths.InstanceSuffix;

	private static string QuitEventName => "Local\\Ayn_Quit" + AppPaths.InstanceSuffix;

	public static bool IsOpen => SettingsWindow.IsOpen;

	[DllImport("user32.dll")]
	private static extern bool AllowSetForegroundWindow(int dwProcessId);

	public static void Listen(Dispatcher ui)
	{
		if (_event != null)
		{
			return;
		}
		try
		{
			_event = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, EventName);
			_wait = ThreadPool.RegisterWaitForSingleObject(_event, delegate
			{
				ui.BeginInvoke((Action)delegate
				{
					RevealForLaunch(backgroundLaunch: false);
				});
			}, null, -1, executeOnlyOnce: false);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Settings launch listener unavailable: " + ex.Message);
		}
		try
		{
			_backgroundEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, EventName + "_Background");
			_backgroundWait = ThreadPool.RegisterWaitForSingleObject(_backgroundEvent, delegate
			{
				ui.BeginInvoke((Action)delegate
				{
					RevealForLaunch(backgroundLaunch: true);
				});
			}, null, -1, executeOnlyOnce: false);
		}
		catch (Exception ex2)
		{
			TransactionLogger.Warn("Background launch listener unavailable: " + ex2.GetType().Name);
		}
		ListenForQuit(ui);
	}

	internal static void ListenForQuit(Dispatcher ui)
	{
		if (_quitEvent != null)
		{
			return;
		}
		try
		{
			_quitEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, QuitEventName);
			_quitWait = ThreadPool.RegisterWaitForSingleObject(_quitEvent, delegate
			{
				ui.BeginInvoke((Action)delegate
				{
					CapsuleWindow instance = CapsuleWindow.Instance;
					if (instance != null)
					{
						instance.RequestQuit("Ayn.exe quit");
					}
					else
					{
						QuitWatchdog.ExitNow(0, "Ayn.exe quit before the agreement was accepted (nothing had started)");
					}
				});
			}, null, -1, executeOnlyOnce: false);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Quit listener unavailable: " + ex.Message);
		}
	}

	public static bool SignalQuit()
	{
		try
		{
			if (!EventWaitHandle.TryOpenExisting(QuitEventName, out var result))
			{
				return false;
			}
			using (result)
			{
				result.Set();
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static bool ShouldHandleSecondLaunch(TimeSpan runningFor)
	{
		return runningFor >= StartupRaceWindow;
	}

	private static TimeSpan ProcessAge()
	{
		try
		{
			using Process process = Process.GetCurrentProcess();
			return DateTime.Now - process.StartTime;
		}
		catch
		{
			return TimeSpan.MaxValue;
		}
	}

	private static void RevealForSecondLaunch()
	{
		CapsuleWindow instance = CapsuleWindow.Instance;
		if (instance != null)
		{
			instance.HandleSecondLaunch();
		}
		else
		{
			Open(null);
		}
	}

	internal static void RevealForLaunch(bool backgroundLaunch)
	{
		if (!EntitlementGate.Evaluate())
		{
			if (!backgroundLaunch || !LicenseStub.AwaitingFirstServerContact)
			{
				OpenForNotEntitled();
			}
		}
		else if (!backgroundLaunch)
		{
			Action redirect = Redirect;
			if (redirect != null)
			{
				redirect();
			}
			else if (ShouldHandleSecondLaunch(ProcessAge()))
			{
				RevealForSecondLaunch();
			}
		}
	}

	public static bool SignalRunningInstance()
	{
		return SignalRunningInstance(backgroundLaunch: false);
	}

	public static bool SignalRunningInstance(bool backgroundLaunch)
	{
		try
		{
			if (!EventWaitHandle.TryOpenExisting(EventName + (backgroundLaunch ? "_Background" : ""), out var result))
			{
				return false;
			}
			using (result)
			{
				AllowSetForegroundWindow(-1);
				result.Set();
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static void Open(string page)
	{
		SettingsWindow.ShowOrActivate(page);
	}

	public static void OpenForNotEntitled()
	{
		AynConnectWindow.ShowOrActivate();
	}
}
