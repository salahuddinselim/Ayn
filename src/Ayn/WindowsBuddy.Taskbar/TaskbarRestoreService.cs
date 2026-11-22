using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Taskbar;

public static class TaskbarRestoreService
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

	public static bool RestorePrevious()
	{
		TaskbarSnapshot taskbarSnapshot = TaskbarStateService.LoadSnapshot();
		if (taskbarSnapshot == null)
		{
			TransactionLogger.Warn("No previous taskbar snapshot found to restore.");
			return false;
		}
		bool flag = RestoreFromSnapshot(taskbarSnapshot);
		if (flag)
		{
			TaskbarStateService.ClearSnapshot();
		}
		return flag;
	}

	public static bool RestoreFromSnapshot(TaskbarSnapshot snapshot)
	{
		if (snapshot == null)
		{
			return false;
		}
		try
		{
			TransactionLogger.Info("Restoring taskbar to captured snapshot state...");
			TaskbarCapabilities capabilities = TaskbarCapabilityService.GetCapabilities();
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", writable: true))
			{
				if (registryKey != null)
				{
					if (capabilities.Alignment.Tier == CapabilityTier.RealNative)
					{
						registryKey.SetValue("TaskbarAl", snapshot.Alignment, RegistryValueKind.DWord);
					}
					if (capabilities.SearchMode.Tier == CapabilityTier.RealNative || capabilities.SearchMode.Tier == CapabilityTier.Partial)
					{
						registryKey.SetValue("TaskbarMn", snapshot.SearchMode, RegistryValueKind.DWord);
					}
					if (capabilities.Widgets.Tier == CapabilityTier.RealNative)
					{
						registryKey.SetValue("TaskbarDa", snapshot.ShowWidgets, RegistryValueKind.DWord);
					}
					if (capabilities.TaskView.Tier == CapabilityTier.RealNative)
					{
						registryKey.SetValue("ShowTaskViewButton", snapshot.ShowTaskView, RegistryValueKind.DWord);
					}
				}
			}
			using (RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", writable: true))
			{
				registryKey2?.SetValue("EnableTransparency", snapshot.EnableTransparency, RegistryValueKind.DWord);
			}
			try
			{
				APPBARDATA pData = default(APPBARDATA);
				pData.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
				pData.hWnd = FindWindow("Shell_TrayWnd", null);
				if (pData.hWnd != IntPtr.Zero)
				{
					pData.lParam = (IntPtr)(snapshot.AutoHide ? 1 : 2);
					SHAppBarMessage(10, ref pData);
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Could not restore taskbar auto-hide via SHAppBarMessage: " + ex.Message);
			}
			try
			{
				SendMessageTimeout(HWND_BROADCAST, 26u, IntPtr.Zero, "TraySettings", 2u, 2000u, out var _);
			}
			catch
			{
			}
			TransactionLogger.Success("Taskbar successfully restored to previous snapshot state.");
			return true;
		}
		catch (Exception ex2)
		{
			TransactionLogger.Error("Failed to restore taskbar snapshot: " + ex2.ToString());
			return false;
		}
	}
}
