using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel;
using Windows.Foundation;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class PackageUpdateLifecycle
{
	public const int ExitDeadlineMs = 5000;

	private static object _catalog;

	private static int _prepared;

	private static int _deadlineArmed;

	internal static string LastSignal = "";

	internal static Action<uint> Terminate = delegate(uint code)
	{
		TerminateProcess(GetCurrentProcess(), code);
	};

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll")]
	private static extern bool TerminateProcess(IntPtr process, uint exitCode);

	public static void Start()
	{
		if (!AppPaths.IsPackaged || _catalog != null)
		{
			return;
		}
		try
		{
			Subscribe();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Package update signals unavailable (" + ex.GetType().Name + ").");
		}
	}

	private unsafe static void Subscribe()
	{
		PackageCatalog packageCatalog = PackageCatalog.OpenForCurrentPackage();
		WindowsRuntimeMarshal.AddEventHandler(new Func<TypedEventHandler<PackageCatalog, PackageUpdatingEventArgs>, EventRegistrationToken>(packageCatalog, (nint)__ldftn(PackageCatalog.add_PackageUpdating)), new Action<EventRegistrationToken>(packageCatalog, (nint)__ldftn(PackageCatalog.remove_PackageUpdating)), delegate(PackageCatalog s, PackageUpdatingEventArgs e)
		{
			string targetVersion = "?";
			try
			{
				PackageVersion version = e.TargetPackage.Id.Version;
				targetVersion = version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;
			}
			catch
			{
			}
			OnUpdating(targetVersion, e.Progress, e.IsComplete);
		});
		WindowsRuntimeMarshal.AddEventHandler(new Func<TypedEventHandler<PackageCatalog, PackageStagingEventArgs>, EventRegistrationToken>(packageCatalog, (nint)__ldftn(PackageCatalog.add_PackageStaging)), new Action<EventRegistrationToken>(packageCatalog, (nint)__ldftn(PackageCatalog.remove_PackageStaging)), delegate(PackageCatalog s, PackageStagingEventArgs e)
		{
			if (e.IsComplete)
			{
				TransactionLogger.Info("Package update staged; Windows installs it when Ayn closes or is closed.");
			}
		});
		_catalog = packageCatalog;
		TransactionLogger.Info("Package update signals: listening.");
	}

	internal static void OnUpdating(string targetVersion, double progress, bool complete)
	{
		LastSignal = "updating " + targetVersion + " " + Math.Round(progress) + "%" + (complete ? " complete" : "");
		if (Interlocked.Exchange(ref _prepared, 1) != 0)
		{
			return;
		}
		TransactionLogger.Info("Package update to " + targetVersion + " started: ending helpers and saving so Windows can replace Ayn.");
		HelperProcessJob.EndAll("package update");
		CrashGuard.PrepareForUpdate();
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				PersistenceHelper.FlushForExit();
				LogWriter.Flush(200);
			}
			catch
			{
			}
		});
	}

	public static void ArmExitDeadline(string reason)
	{
		if (Interlocked.Exchange(ref _deadlineArmed, 1) != 0)
		{
			return;
		}
		Thread thread = new Thread((ThreadStart)delegate
		{
			Thread.Sleep(5000);
			Terminate(0u);
		});
		thread.IsBackground = true;
		thread.Name = "Ayn exit deadline";
		Thread thread2 = thread;
		thread2.Start();
		Thread thread3 = new Thread((ThreadStart)delegate
		{
			try
			{
				HelperProcessJob.EndAll("session ending");
			}
			catch
			{
			}
			try
			{
				PersistenceHelper.FlushForExit();
			}
			catch
			{
			}
			try
			{
				LogWriter.Flush(300);
			}
			catch
			{
			}
		});
		thread3.IsBackground = true;
		thread3.Name = "Ayn session-end save";
		thread3.Start();
	}
}
