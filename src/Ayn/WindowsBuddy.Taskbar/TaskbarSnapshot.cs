using System;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Taskbar;

public class TaskbarSnapshot
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

	private const int ABM_GETSTATE = 4;

	private const int ABS_AUTOHIDE = 1;

	public string Timestamp { get; set; }

	public int WindowsBuild { get; set; }

	public int Alignment { get; set; }

	public bool AutoHide { get; set; }

	public int SearchMode { get; set; }

	public int ShowWidgets { get; set; }

	public int ShowTaskView { get; set; }

	public int EnableTransparency { get; set; }

	public TaskbarSnapshot()
	{
		Timestamp = DateTime.UtcNow.ToString("o");
		WindowsBuild = Environment.OSVersion.Version.Build;
	}

	public static TaskbarSnapshot CaptureCurrent()
	{
		TaskbarSnapshot taskbarSnapshot = new TaskbarSnapshot();
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", writable: false))
			{
				if (registryKey != null)
				{
					object value = registryKey.GetValue("TaskbarAl");
					taskbarSnapshot.Alignment = ((!(value is int)) ? 1 : ((int)value));
					object value2 = registryKey.GetValue("TaskbarMn");
					taskbarSnapshot.SearchMode = ((value2 is int) ? ((int)value2) : 3);
					object value3 = registryKey.GetValue("TaskbarDa");
					taskbarSnapshot.ShowWidgets = ((!(value3 is int)) ? 1 : ((int)value3));
					object value4 = registryKey.GetValue("ShowTaskViewButton");
					taskbarSnapshot.ShowTaskView = ((!(value4 is int)) ? 1 : ((int)value4));
				}
			}
			taskbarSnapshot.AutoHide = ReadCurrentAutoHideState();
			using (RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", writable: false))
			{
				if (registryKey2 != null)
				{
					object value5 = registryKey2.GetValue("EnableTransparency");
					taskbarSnapshot.EnableTransparency = ((!(value5 is int)) ? 1 : ((int)value5));
				}
			}
			TransactionLogger.Info($"TaskbarSnapshot captured: Alignment={taskbarSnapshot.Alignment}, AutoHide={taskbarSnapshot.AutoHide}, Search={taskbarSnapshot.SearchMode}, Transparency={taskbarSnapshot.EnableTransparency}");
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Error capturing TaskbarSnapshot: " + ex.Message);
		}
		return taskbarSnapshot;
	}

	[DllImport("shell32.dll")]
	private static extern UIntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	private static bool ReadCurrentAutoHideState()
	{
		try
		{
			APPBARDATA pData = default(APPBARDATA);
			pData.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
			pData.hWnd = FindWindow("Shell_TrayWnd", null);
			int num = (int)SHAppBarMessage(4, ref pData).ToUInt32();
			return (num & 1) != 0;
		}
		catch
		{
			return false;
		}
	}

	public string ToJson()
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		return javaScriptSerializer.Serialize(this);
	}

	public static TaskbarSnapshot FromJson(string json)
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		return javaScriptSerializer.Deserialize<TaskbarSnapshot>(json);
	}
}
