using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Tools;

namespace WindowsBuddy.Services;

public static class DiagnosticsExport
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private class MEMORYSTATUSEX
	{
		public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;
	}

	public static readonly string[] LogFiles = new string[8] { "engine.log", "engine.log.1", "notch.log", "notch.log.1", "crash.log", "crash.log.1", "metrics.log", "metrics.log.1" };

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX s);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForSystem();

	public static string Export(string folder)
	{
		string text = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "logs");
		string path = "Ayn-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".zip";
		string text2 = Path.Combine(folder, path);
		string text3 = BuildSummary();
		using (FileStream stream = new FileStream(text2, FileMode.Create, FileAccess.Write))
		{
			using ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create);
			AddText(zip, "system-summary.txt", text3);
			string[] logFiles = LogFiles;
			foreach (string file in logFiles)
			{
				AddLog(zip, text, file);
			}
			try
			{
				if (Directory.Exists(text))
				{
					string[] files = Directory.GetFiles(text, "hang-*.txt");
					foreach (string path2 in files)
					{
						AddLog(zip, text, Path.GetFileName(path2));
					}
				}
			}
			catch
			{
			}
		}
		return text2;
	}

	private static string Scrub(string text)
	{
		string[] array = new string[2]
		{
			Environment.UserName,
			Environment.MachineName
		};
		foreach (string text2 in array)
		{
			if (!string.IsNullOrEmpty(text2) && text2.Length > 2)
			{
				text = text.Replace(text2, "<redacted>");
			}
		}
		return text;
	}

	private static void AddText(ZipArchive zip, string entry, string text)
	{
		ZipArchiveEntry zipArchiveEntry = zip.CreateEntry(entry, CompressionLevel.Optimal);
		using StreamWriter streamWriter = new StreamWriter(zipArchiveEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		streamWriter.Write(text);
	}

	private static void AddLog(ZipArchive zip, string dir, string file)
	{
		try
		{
			string path = Path.Combine(dir, file);
			if (!File.Exists(path))
			{
				return;
			}
			string text;
			using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			{
				using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
				text = streamReader.ReadToEnd();
			}
			AddText(zip, "logs/" + file, Scrub(text));
		}
		catch (Exception ex)
		{
			try
			{
				AddText(zip, "logs/" + file + ".error.txt", ex.GetType().Name);
			}
			catch
			{
			}
		}
	}

	public static string BuildSummary()
	{
		StringBuilder sb = new StringBuilder();
		Action<string, Func<string>> action = delegate(string label, Func<string> get)
		{
			string text;
			try
			{
				text = get();
			}
			catch (Exception ex)
			{
				text = "unavailable (" + ex.GetType().Name + ")";
			}
			sb.AppendLine(label.PadRight(22) + text);
		};
		sb.AppendLine("Ayn diagnostics, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
		sb.AppendLine("No personal content: logs, settings values and hardware details only.");
		sb.AppendLine();
		action("Ayn version", () => Assembly.GetExecutingAssembly().GetName().Version.ToString());
		action("Windows", delegate
		{
			using RegistryKey registryKey2 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
			return string.Format("{0} {1}, build {2}.{3}", registryKey2.GetValue("ProductName"), registryKey2.GetValue("DisplayVersion"), registryKey2.GetValue("CurrentBuild"), registryKey2.GetValue("UBR")) + (Environment.Is64BitOperatingSystem ? " (64-bit)" : " (32-bit)");
		});
		action("CPU", delegate
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0");
			return (((string)registryKey.GetValue("ProcessorNameString")) ?? "?").Trim() + ", " + Environment.ProcessorCount + " logical processors";
		});
		action("RAM", delegate
		{
			MEMORYSTATUSEX mEMORYSTATUSEX = new MEMORYSTATUSEX();
			return (!GlobalMemoryStatusEx(mEMORYSTATUSEX)) ? "unknown" : $"{(double)mEMORYSTATUSEX.ullTotalPhys / 1073741824.0:0.0} GB total, {(double)mEMORYSTATUSEX.ullAvailPhys / 1073741824.0:0.0} GB free";
		});
		action("GPU", delegate
		{
			List<string> list2 = new List<string>();
			using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController"))
			{
				foreach (ManagementObject item in managementObjectSearcher.Get())
				{
					long num = 0L;
					try
					{
						num = Convert.ToInt64(item["AdapterRAM"]);
					}
					catch
					{
					}
					list2.Add(string.Format("{0} (driver {1}, {2} MB)", item["Name"], item["DriverVersion"], num / 1048576));
				}
			}
			return (list2.Count != 0) ? string.Join("; ", list2.ToArray()) : "none reported";
		});
		action("Render", () => RenderProfile.Summary);
		action("Scaling", () => GetDpiForSystem() * 100 / 96 + "% (system DPI " + GetDpiForSystem() + ")");
		action("Monitors", delegate
		{
			Screen[] allScreens = Screen.AllScreens;
			List<string> list = new List<string>();
			Screen[] array = allScreens;
			foreach (Screen screen in array)
			{
				list.Add(screen.Bounds.Width + "x" + screen.Bounds.Height);
			}
			return allScreens.Length + " (" + string.Join(", ", list.ToArray()) + ")";
		});
		action("Battery saver", () => (!PowerSaver.IsBatterySaverOn) ? "off" : "on");
		action("Animations (Windows)", () => (!SystemParameters.ClientAreaAnimation) ? "off" : "on");
		sb.AppendLine();
		sb.AppendLine("Notch settings (no personal values)");
		NotchConfig cfg = NotchConfig.Instance;
		ToolRegistry reg = ToolRegistry.Instance;
		action("Shape / size", () => cfg.Style + " / " + (int)Math.Round(cfg.SizeScale * 100.0) + "%");
		action("Resting layout", () => cfg.RestingLayout);
		action("Animation speed", () => cfg.AnimationSpeed);
		action("Open on", () => reg.OpenOn + ", hover delay " + reg.HoverDelayMs + " ms");
		action("Edge Light", () => (!cfg.EdgeLightEnabled) ? "off" : (cfg.EdgeLightStyle + ", " + cfg.EdgeLightWhen + ", " + cfg.EdgeLightColor));
		action("Blur / shadow", () => cfg.BlurEffect + " / " + cfg.ShadowStrength);
		action("Theme", () => cfg.AppTheme);
		return sb.ToString();
	}
}
