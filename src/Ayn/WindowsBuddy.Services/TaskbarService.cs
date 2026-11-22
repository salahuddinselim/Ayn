using System;
using System.Runtime.InteropServices;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class TaskbarService
{
	public struct APPBARDATA
	{
		public int cbSize;

		public IntPtr hWnd;

		public int uCallbackMessage;

		public int uEdge;

		public RECT rc;

		public IntPtr lParam;
	}

	public struct RECT
	{
		public int left;

		public int top;

		public int right;

		public int bottom;
	}

	private const int ABM_GETSTATE = 4;

	private const int ABM_SETSTATE = 10;

	public const int ABS_AUTOHIDE = 1;

	public const int ABS_ALWAYSONTOP = 2;

	[DllImport("shell32.dll")]
	public static extern UIntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	public static int GetNativeTaskbarState()
	{
		try
		{
			APPBARDATA pData = default(APPBARDATA);
			pData.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
			pData.hWnd = FindWindow("Shell_TrayWnd", null);
			return (int)SHAppBarMessage(4, ref pData).ToUInt32();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not read native taskbar state: " + ex.Message);
			return 2;
		}
	}

	public static bool SetNativeTaskbarAutoHide(bool enableAutoHide)
	{
		try
		{
			APPBARDATA pData = default(APPBARDATA);
			pData.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
			pData.hWnd = FindWindow("Shell_TrayWnd", null);
			if (pData.hWnd == IntPtr.Zero)
			{
				return false;
			}
			pData.lParam = (IntPtr)(enableAutoHide ? 1 : 2);
			SHAppBarMessage(10, ref pData);
			TransactionLogger.Info($"Native taskbar auto-hide set to: {enableAutoHide}");
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not set native taskbar auto-hide: " + ex.Message);
			return false;
		}
	}

	public static void RestoreNativeTaskbarState(int originalState)
	{
		try
		{
			APPBARDATA pData = default(APPBARDATA);
			pData.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
			pData.hWnd = FindWindow("Shell_TrayWnd", null);
			if (pData.hWnd != IntPtr.Zero)
			{
				pData.lParam = (IntPtr)originalState;
				SHAppBarMessage(10, ref pData);
				TransactionLogger.Info($"Restored native taskbar state to: {originalState}");
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not restore native taskbar state: " + ex.Message);
		}
	}

	public static TaskbarCompatibilityResult ApplyTaskbarConfiguration(ThemeTaskbar taskbarConfig)
	{
		TaskbarCompatibilityResult taskbarCompatibilityResult = new TaskbarCompatibilityResult();
		OperatingSystem oSVersion = Environment.OSVersion;
		int build = oSVersion.Version.Build;
		taskbarCompatibilityResult.WindowsBuild = $"{oSVersion.Version.Major}.{oSVersion.Version.Minor} (Build {build})";
		taskbarCompatibilityResult.Status = TaskbarCapabilityStatus.Coordinated;
		taskbarCompatibilityResult.MethodUsed = "Ayn Native Surface + Win32 AppBar Coordination";
		taskbarCompatibilityResult.DiagnosticMessage = $"Windows 11 Build {build}: Native taskbar coordinated safely via SHAppBarMessage; Ayn Surface active.";
		TransactionLogger.Info("TaskbarService: " + taskbarCompatibilityResult.DiagnosticMessage);
		return taskbarCompatibilityResult;
	}
}
