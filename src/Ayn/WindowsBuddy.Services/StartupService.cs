using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class StartupService
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct WTSINFO
	{
		public int State;

		public int SessionId;

		public int IncomingBytes;

		public int OutgoingBytes;

		public int IncomingFrames;

		public int OutgoingFrames;

		public int IncomingCompressedBytes;

		public int OutgoingCompressedBytes;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string WinStationName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
		public string Domain;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)]
		public string UserName;

		public long ConnectTime;

		public long DisconnectTime;

		public long LastInputTime;

		public long LogonTime;

		public long CurrentTime;
	}

	private static class PackagedStartupTask
	{
		public const int Unknown = -1;

		public const int Disabled = 0;

		public const int DisabledByUser = 1;

		public const int Enabled = 2;

		public const int DisabledByPolicy = 3;

		public const int EnabledByPolicy = 4;

		public static string Describe(int s)
		{
			return s switch
			{
				0 => "Disabled", 
				1 => "DisabledByUser (turned off in Windows Settings > Apps > Startup)", 
				2 => "Enabled", 
				3 => "DisabledByPolicy", 
				4 => "EnabledByPolicy", 
				_ => "Unknown", 
			};
		}

		public static int GetState()
		{
			object task = GetTask();
			if (task == null)
			{
				return -1;
			}
			try
			{
				return Convert.ToInt32(task.GetType().GetProperty("State").GetValue(task, null));
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("StartupTask.State failed: " + ex.Message);
				return -1;
			}
		}

		public static int RequestEnable()
		{
			object task = GetTask();
			if (task == null)
			{
				return -1;
			}
			try
			{
				int num = Convert.ToInt32(task.GetType().GetProperty("State").GetValue(task, null));
				if (num != 0)
				{
					return num;
				}
				object asyncOp = task.GetType().GetMethod("RequestEnableAsync").Invoke(task, null);
				return Convert.ToInt32(Await(asyncOp, StateType()));
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("StartupTask.RequestEnableAsync failed: " + ex.Message);
				return -1;
			}
		}

		public static void Disable()
		{
			object task = GetTask();
			if (task == null)
			{
				return;
			}
			try
			{
				task.GetType().GetMethod("Disable").Invoke(task, null);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("StartupTask.Disable failed: " + ex.Message);
			}
		}

		private static object GetTask()
		{
			try
			{
				Type type = WinRt("Windows.ApplicationModel.StartupTask");
				if (type == null)
				{
					return null;
				}
				object asyncOp = type.GetMethod("GetAsync", new Type[1] { typeof(string) }).Invoke(null, new object[1] { "AynStartup" });
				return Await(asyncOp, type);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("StartupTask 'AynStartup' unavailable: " + ex.Message);
				return null;
			}
		}

		private static Type StateType()
		{
			return WinRt("Windows.ApplicationModel.StartupTaskState");
		}

		private static Type WinRt(string name)
		{
			return Type.GetType(name + ", Windows.ApplicationModel, ContentType=WindowsRuntime") ?? Type.GetType(name + ", Windows, ContentType=WindowsRuntime");
		}

		private static object Await(object asyncOp, Type resultType)
		{
			Assembly assembly = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			MethodInfo methodInfo = null;
			MethodInfo[] methods = assembly.GetType("System.WindowsRuntimeSystemExtensions").GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo2 in methods)
			{
				if (methodInfo2.Name == "AsTask" && methodInfo2.IsGenericMethod && methodInfo2.GetParameters().Length == 1 && methodInfo2.GetParameters()[0].ParameterType.Name.StartsWith("IAsyncOperation`"))
				{
					methodInfo = methodInfo2;
				}
			}
			Task task = (Task)methodInfo.MakeGenericMethod(resultType).Invoke(null, new object[1] { asyncOp });
			if (!Task.Run(delegate
			{
				task.Wait();
			}).Wait(5000))
			{
				throw new TimeoutException("StartupTask call timed out");
			}
			return task.GetType().GetProperty("Result").GetValue(task, null);
		}
	}

	private const string DefaultRunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string AppValueName = "Ayn";

	private const string LegacyValueName = "WindowsBuddy";

	public const string StartupTaskId = "AynStartup";

	private const int WTS_CURRENT_SESSION = -1;

	private const int WTSSessionInfo = 24;

	private const string StartupApprovedPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run";

	public static readonly TimeSpan SignInWindow = TimeSpan.FromMinutes(5.0);

	internal static Func<DateTime?> LogonTimeProvider = SessionLogonTime;

	private static string _runKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	public static bool HasLaunchedBefore => File.Exists(Path.Combine(AppPaths.DataDir, "startup.initialized"));

	public static string RunKeyPath
	{
		get
		{
			return _runKeyPath;
		}
		set
		{
			_runKeyPath = (string.IsNullOrEmpty(value) ? "Software\\Microsoft\\Windows\\CurrentVersion\\Run" : value);
		}
	}

	[DllImport("wtsapi32.dll", CharSet = CharSet.Unicode)]
	private static extern bool WTSQuerySessionInformation(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytes);

	[DllImport("wtsapi32.dll")]
	private static extern void WTSFreeMemory(IntPtr memory);

	private static DateTime? SessionLogonTime()
	{
		if (AppPaths.IsSandboxed && Environment.GetEnvironmentVariable("VAYLI_QA_SHELL_START") == "now")
		{
			return ShellStartTime();
		}
		try
		{
			if (WTSQuerySessionInformation(IntPtr.Zero, -1, 24, out var buffer, out var _) && buffer != IntPtr.Zero)
			{
				try
				{
					WTSINFO wTSINFO = (WTSINFO)Marshal.PtrToStructure(buffer, typeof(WTSINFO));
					if (wTSINFO.LogonTime > 0)
					{
						return DateTime.FromFileTimeUtc(wTSINFO.LogonTime).ToLocalTime();
					}
				}
				finally
				{
					WTSFreeMemory(buffer);
				}
			}
		}
		catch
		{
		}
		return ShellStartTime();
	}

	internal static DateTime? ShellStartTime()
	{
		try
		{
			if (AppPaths.IsSandboxed && Environment.GetEnvironmentVariable("VAYLI_QA_SHELL_START") == "now")
			{
				return Process.GetCurrentProcess().StartTime.AddSeconds(-30.0);
			}
			DateTime? dateTime = null;
			Process[] processesByName = Process.GetProcessesByName("explorer");
			foreach (Process process in processesByName)
			{
				using (process)
				{
					try
					{
						DateTime startTime = process.StartTime;
						if (!dateTime.HasValue)
						{
							goto IL_0098;
						}
						DateTime value = startTime;
						DateTime? dateTime2 = dateTime;
						if (value < dateTime2)
						{
							goto IL_0098;
						}
						goto end_IL_0066;
						IL_0098:
						dateTime = startTime;
						end_IL_0066:;
					}
					catch
					{
					}
				}
			}
			return dateTime;
		}
		catch
		{
			return null;
		}
	}

	public static bool IsSignInLaunch(string command, DateTime processStartLocal)
	{
		switch (command)
		{
		case "capsule":
		case "relaunch":
		case "buddy":
			return true;
		default:
		{
			if (command != "")
			{
				return false;
			}
			DateTime? dateTime = LogonTimeProvider();
			if (dateTime.HasValue && processStartLocal >= dateTime.Value)
			{
				return processStartLocal - dateTime.Value <= SignInWindow;
			}
			return false;
		}
		}
	}

	public static void InitializeFirstLaunch(bool onboardingCompleted)
	{
		if (!AppPaths.IsSandboxed && !HasLaunchedBefore)
		{
			PersistenceHelper.SafeWriteAllText(Path.Combine(AppPaths.DataDir, "startup.initialized"), "1");
		}
	}

	public static string StartupStatusMessage()
	{
		if (!AppPaths.IsPackaged)
		{
			if (!IsDisabledInStartupApproved())
			{
				return null;
			}
			return "Disabled in Task Manager > Startup apps. Re-enable Ayn there.";
		}
		return DescribeStartupRestriction(PackagedStartupTask.GetState());
	}

	public static string DescribeStartupRestriction(int state)
	{
		return state switch
		{
			1 => "Disabled in Task Manager > Startup apps. Re-enable Ayn there; Ayn cannot override your choice.", 
			3 => "Startup is disabled by your organization's policy.", 
			-1 => "Windows startup status is unavailable. Check Task Manager > Startup apps.", 
			_ => null, 
		};
	}

	public static string GetExecutablePath()
	{
		try
		{
			Assembly entryAssembly = Assembly.GetEntryAssembly();
			if (entryAssembly != null && !string.IsNullOrEmpty(entryAssembly.Location))
			{
				return entryAssembly.Location;
			}
			return Assembly.GetExecutingAssembly().Location;
		}
		catch
		{
			return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ayn.exe");
		}
	}

	public static bool IsAutoStartEnabled()
	{
		if (AppPaths.IsPackaged)
		{
			int state = PackagedStartupTask.GetState();
			if (state != 2)
			{
				return state == 4;
			}
			return true;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
			if (registryKey == null)
			{
				return false;
			}
			object value = registryKey.GetValue("Ayn");
			if (value == null)
			{
				return false;
			}
			string text = value.ToString();
			string executablePath = GetExecutablePath();
			if (text.IndexOf(executablePath, StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			return !IsDisabledInStartupApproved();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Failed to check auto-start status: " + ex.Message);
			return false;
		}
	}

	public static bool EnableAutoStart()
	{
		bool flag = EnableAutoStartCore();
		CrashGuard.SetRestoreAfterReboot(flag);
		return flag;
	}

	private static bool EnableAutoStartCore()
	{
		if (AppPaths.IsPackaged)
		{
			int num = PackagedStartupTask.RequestEnable();
			TransactionLogger.Info("StartupTask enable requested, state: " + PackagedStartupTask.Describe(num));
			if (num != 2)
			{
				return num == 4;
			}
			return true;
		}
		try
		{
			if (IsDisabledInStartupApproved())
			{
				return false;
			}
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
			if (registryKey == null)
			{
				return false;
			}
			string text = $"\"{GetExecutablePath()}\" capsule";
			registryKey.SetValue("Ayn", text);
			registryKey.DeleteValue("WindowsBuddy", throwOnMissingValue: false);
			TransactionLogger.Info("Auto-start registered: " + text);
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Failed to enable auto-start: " + ex.Message, ex);
			return false;
		}
	}

	public static bool DisableAutoStart()
	{
		CrashGuard.SetRestoreAfterReboot(startWithWindows: false);
		return DisableAutoStartCore();
	}

	private static bool DisableAutoStartCore()
	{
		if (AppPaths.IsPackaged)
		{
			PackagedStartupTask.Disable();
			return true;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
			if (registryKey == null)
			{
				return true;
			}
			registryKey.DeleteValue("Ayn", throwOnMissingValue: false);
			registryKey.DeleteValue("WindowsBuddy", throwOnMissingValue: false);
			ClearStartupApproved();
			TransactionLogger.Info("Auto-start removed.");
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Failed to disable auto-start: " + ex.Message, ex);
			return false;
		}
	}

	private static bool IsDisabledInStartupApproved()
	{
		if (RunKeyPath != "Software\\Microsoft\\Windows\\CurrentVersion\\Run")
		{
			return false;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run", writable: false);
			byte[] array = ((registryKey != null) ? (registryKey.GetValue("Ayn") as byte[]) : null);
			return array != null && array.Length > 0 && (array[0] & 1) == 1;
		}
		catch
		{
			return false;
		}
	}

	private static void ClearStartupApproved()
	{
		if (RunKeyPath != "Software\\Microsoft\\Windows\\CurrentVersion\\Run")
		{
			return;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run", writable: true);
			registryKey?.DeleteValue("Ayn", throwOnMissingValue: false);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("StartupApproved cleanup failed: " + ex.Message);
		}
	}

	public static void RepairRunEntry()
	{
		if (AppPaths.IsPackaged)
		{
			return;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
			string text = ((registryKey != null) ? (registryKey.GetValue("Ayn") as string) : null);
			if (!string.IsNullOrEmpty(text))
			{
				string text2 = text.Trim();
				if (text2.StartsWith("\""))
				{
					int num = text2.IndexOf('"', 1);
					text2 = ((num > 1) ? text2.Substring(1, num - 1) : text2.Trim('"'));
				}
				if (!File.Exists(text2))
				{
					string text3 = $"\"{GetExecutablePath()}\" capsule";
					registryKey.SetValue("Ayn", text3);
					TransactionLogger.Info("Auto-start entry pointed at a missing exe; repaired to " + text3);
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Auto-start repair skipped: " + ex.Message);
		}
	}

	public static bool MigrateLegacyRunEntry()
	{
		if (AppPaths.IsPackaged)
		{
			return false;
		}
		try
		{
			bool flag;
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
			{
				flag = registryKey != null && registryKey.GetValue("WindowsBuddy") != null;
				if (flag)
				{
					registryKey.DeleteValue("WindowsBuddy", throwOnMissingValue: false);
				}
			}
			if (!flag)
			{
				return false;
			}
			bool flag2 = EnableAutoStart();
			TransactionLogger.Info("Migrated legacy 'WindowsBuddy' startup entry to Ayn: " + (flag2 ? "enabled" : "could not enable"));
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Legacy startup entry migration failed: " + ex.Message);
			return false;
		}
	}
}
