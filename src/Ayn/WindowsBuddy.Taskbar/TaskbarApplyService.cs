using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Taskbar;

public static class TaskbarApplyService
{
	private struct APPBARDATA
	{
		public int cbSize;

		public IntPtr hWnd;

		public int uCallbackMessage;

		public int uEdge;

		public RECT rc;

		public IntPtr lParam;
	}

	private struct RECT
	{
		public int left;

		public int top;

		public int right;

		public int bottom;
	}

	private const int ABM_SETSTATE = 10;

	private const int ABS_AUTOHIDE = 1;

	private const int ABS_ALWAYSONTOP = 2;

	private const int WM_SETTINGCHANGE = 26;

	private const uint SMTO_ABORTIFHUNG = 2u;

	private static readonly IntPtr HWND_BROADCAST = new IntPtr(65535);

	[DllImport("shell32.dll")]
	private static extern UIntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

	public static TaskbarApplyResult Apply(TaskbarConfiguration config)
	{
		TaskbarApplyResult taskbarApplyResult = new TaskbarApplyResult();
		taskbarApplyResult.Success = false;
		TaskbarApplyResult taskbarApplyResult2 = taskbarApplyResult;
		if (!TaskbarStateService.HasSnapshot())
		{
			TaskbarSnapshot snapshot = TaskbarSnapshot.CaptureCurrent();
			TaskbarStateService.SaveSnapshot(snapshot);
		}
		TaskbarSnapshot taskbarSnapshot = TaskbarStateService.LoadSnapshot();
		try
		{
			TransactionLogger.Info("Applying TaskbarConfiguration...");
			TaskbarCapabilities capabilities = TaskbarCapabilityService.GetCapabilities();
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", writable: true))
			{
				if (registryKey == null)
				{
					throw new InvalidOperationException("Cannot open registry key HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced");
				}
				if (capabilities.Alignment.Tier == CapabilityTier.RealNative)
				{
					TransactionLogger.Info("Setting TaskbarAl: " + (int)config.Alignment);
					registryKey.SetValue("TaskbarAl", (int)config.Alignment, RegistryValueKind.DWord);
				}
				if (capabilities.SearchMode.Tier == CapabilityTier.RealNative || capabilities.SearchMode.Tier == CapabilityTier.Partial)
				{
					TransactionLogger.Info("Setting TaskbarMn: " + (int)config.SearchMode);
					registryKey.SetValue("TaskbarMn", (int)config.SearchMode, RegistryValueKind.DWord);
				}
				if (capabilities.Widgets.Tier == CapabilityTier.RealNative)
				{
					TransactionLogger.Info("Setting TaskbarDa: " + (config.ShowWidgets ? 1 : 0));
					registryKey.SetValue("TaskbarDa", config.ShowWidgets ? 1 : 0, RegistryValueKind.DWord);
				}
				if (capabilities.TaskView.Tier == CapabilityTier.RealNative)
				{
					TransactionLogger.Info("Setting ShowTaskViewButton: " + (config.ShowTaskView ? 1 : 0));
					registryKey.SetValue("ShowTaskViewButton", config.ShowTaskView ? 1 : 0, RegistryValueKind.DWord);
				}
			}
			TransactionLogger.Info("Opening Themes Personalize key...");
			using (RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", writable: true))
			{
				if (registryKey2 != null)
				{
					TransactionLogger.Info("Setting EnableTransparency: " + (config.SystemTransparency ? 1 : 0));
					registryKey2.SetValue("EnableTransparency", config.SystemTransparency ? 1 : 0, RegistryValueKind.DWord);
				}
			}
			ApplyAutoHide(config.AutoHide);
			BroadcastTaskbarChange();
			TaskbarStateService.SaveConfiguration(config);
			taskbarApplyResult2.Success = true;
			taskbarApplyResult2.Message = "Taskbar customization applied successfully.";
			TransactionLogger.Success("TaskbarConfiguration applied cleanly.");
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Taskbar apply failed. Initiating automatic rollback: " + ex.ToString());
			if (taskbarSnapshot != null)
			{
				TaskbarRestoreService.RestoreFromSnapshot(taskbarSnapshot);
				taskbarApplyResult2.RolledBack = true;
			}
			taskbarApplyResult2.Success = false;
			taskbarApplyResult2.ErrorDetails = ex.Message;
			taskbarApplyResult2.Message = "Apply failed. Taskbar was automatically rolled back to your previous configuration.";
		}
		return taskbarApplyResult2;
	}

	private static void ApplyAutoHide(bool autoHide)
	{
		try
		{
			APPBARDATA pData = default(APPBARDATA);
			pData.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
			pData.hWnd = FindWindow("Shell_TrayWnd", null);
			if (pData.hWnd != IntPtr.Zero)
			{
				pData.lParam = (IntPtr)(autoHide ? 1 : 2);
				SHAppBarMessage(10, ref pData);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not update taskbar auto-hide via SHAppBarMessage: " + ex.Message);
		}
	}

	public static void BroadcastTaskbarChange()
	{
		try
		{
			SendMessageTimeout(HWND_BROADCAST, 26u, IntPtr.Zero, "TraySettings", 2u, 2000u, out var _);
		}
		catch
		{
		}
	}
}
