using System;
using Microsoft.Win32;

namespace WindowsBuddy.Taskbar;

public static class TaskbarCapabilityService
{
	private static readonly TaskbarCapabilities _cachedCapabilities;

	static TaskbarCapabilityService()
	{
		_cachedCapabilities = EvaluateCapabilities();
	}

	public static TaskbarCapabilities GetCapabilities()
	{
		return _cachedCapabilities;
	}

	private static TaskbarCapabilities EvaluateCapabilities()
	{
		int build = Environment.OSVersion.Version.Build;
		bool flag = build >= 22000;
		bool flag2 = build >= 22621;
		TaskbarCapabilities taskbarCapabilities = new TaskbarCapabilities();
		taskbarCapabilities.Alignment = new TaskbarPropertyCapability("Alignment", "Taskbar Alignment", (!flag) ? CapabilityTier.PreviewOnly : CapabilityTier.RealNative, flag ? "Safely changes taskbar alignment between Left and Center in Windows 11." : "Alignment is fixed on Windows 10.", flag);
		taskbarCapabilities.AutoHide = new TaskbarPropertyCapability("AutoHide", "Taskbar Auto-Hide", CapabilityTier.RealNative, "Officially supported across all Windows builds via SHAppBarMessage.", isSupported: true);
		bool flag3 = CanWriteRegistryValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarMn");
		taskbarCapabilities.SearchMode = new TaskbarPropertyCapability("SearchMode", "Search Button Treatment", (!flag3 && !flag2) ? CapabilityTier.Partial : CapabilityTier.RealNative, flag3 ? "Controls search icon, label, or box directly via Windows Explorer registry." : "Search mode options restricted on this build.", isSupported: true);
		bool flag4 = flag && CanWriteRegistryValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarDa");
		taskbarCapabilities.Widgets = new TaskbarPropertyCapability("Widgets", "Widgets / Weather Icon", (!flag4) ? CapabilityTier.PreviewOnly : CapabilityTier.RealNative, flag4 ? "Toggles Windows 11 Widgets and taskbar weather badge." : "Widgets toggle is locked by Windows policy on this build. Demonstrated in Studio Preview.", flag4);
		bool flag5 = CanWriteRegistryValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowTaskViewButton");
		taskbarCapabilities.TaskView = new TaskbarPropertyCapability("TaskView", "Task View Button", (!flag5) ? CapabilityTier.PreviewOnly : CapabilityTier.RealNative, flag5 ? "Supported on Windows 10 and Windows 11 via Explorer Advanced settings." : "Task View toggle restricted on this system.", flag5);
		taskbarCapabilities.Transparency = new TaskbarPropertyCapability("Transparency", "System Transparency", CapabilityTier.Partial, "Toggles DWM transparency effects system-wide without DLL injection.", isSupported: true);
		taskbarCapabilities.Blur = new TaskbarPropertyCapability("Blur", "Taskbar Blur Material", CapabilityTier.PreviewOnly, "Demonstrated in Studio preview. Stock Windows Explorer controls its own acrylic shader.", isSupported: false);
		taskbarCapabilities.CornerRadius = new TaskbarPropertyCapability("CornerRadius", "Floating Corner Radius", CapabilityTier.PreviewOnly, "Demonstrated in Studio preview. Stock Windows taskbar is docked full-width.", isSupported: false);
		taskbarCapabilities.Height = new TaskbarPropertyCapability("Height", "Taskbar Height", CapabilityTier.PreviewOnly, "Demonstrated in Studio preview. Windows 11 enforces 48px standard taskbar height.", isSupported: false);
		return taskbarCapabilities;
	}

	private static bool CanWriteRegistryValue(string subKey, string valueName)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
			if (registryKey == null)
			{
				return false;
			}
			object value = registryKey.GetValue(valueName);
			if (value != null)
			{
				registryKey.SetValue(valueName, value);
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}
}
