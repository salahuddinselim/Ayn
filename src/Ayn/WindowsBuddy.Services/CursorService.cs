using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class CursorService
{
	private const uint SPI_SETCURSORS = 87u;

	private const uint SPIF_UPDATEINIFILE = 1u;

	private const uint SPIF_SENDCHANGE = 2u;

	private const int HWND_BROADCAST = 65535;

	private const uint WM_SETTINGCHANGE = 26u;

	private const uint SMTO_ABORTIFHUNG = 2u;

	public static readonly string[] CursorRegistryKeys;

	private static readonly string SnapshotDirectory;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

	static CursorService()
	{
		CursorRegistryKeys = new string[15]
		{
			"Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
			"SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand"
		};
		string localAppData = AppPaths.LocalAppData;
		SnapshotDirectory = Path.Combine(localAppData, AppPaths.FolderName, "snapshots");
		try
		{
			if (!Directory.Exists(SnapshotDirectory))
			{
				Directory.CreateDirectory(SnapshotDirectory);
			}
		}
		catch
		{
		}
	}

	public static Dictionary<string, string> GetCurrentCursorScheme()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: false))
		{
			if (registryKey != null)
			{
				string[] cursorRegistryKeys = CursorRegistryKeys;
				foreach (string text in cursorRegistryKeys)
				{
					object value = registryKey.GetValue(text);
					dictionary[text] = ((value != null) ? value.ToString() : string.Empty);
				}
			}
		}
		return dictionary;
	}

	public static string GetCurrentSchemeName()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("");
				if (value != null && !string.IsNullOrEmpty(value.ToString()))
				{
					return value.ToString();
				}
			}
		}
		catch
		{
		}
		return "Windows Default";
	}

	public static int GetCurrentSchemeSource()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("Scheme Source");
				if (value != null && value is int)
				{
					return (int)value;
				}
			}
		}
		catch
		{
		}
		return 1;
	}

	public static bool HasSnapshot()
	{
		try
		{
			if (!Directory.Exists(SnapshotDirectory))
			{
				return false;
			}
			string[] files = Directory.GetFiles(SnapshotDirectory, "cursor_snapshot_*.json");
			return files != null && files.Length > 0;
		}
		catch
		{
			return false;
		}
	}

	public static string CreateSnapshot()
	{
		try
		{
			if (!Directory.Exists(SnapshotDirectory))
			{
				Directory.CreateDirectory(SnapshotDirectory);
			}
			CursorSnapshot cursorSnapshot = new CursorSnapshot();
			cursorSnapshot.Timestamp = DateTime.UtcNow.ToString("o");
			cursorSnapshot.SchemeName = GetCurrentSchemeName();
			cursorSnapshot.SchemeSource = GetCurrentSchemeSource();
			cursorSnapshot.CursorMap = GetCurrentCursorScheme();
			CursorSnapshot obj = cursorSnapshot;
			string path = $"cursor_snapshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json";
			string text = Path.Combine(SnapshotDirectory, path);
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			string content = javaScriptSerializer.Serialize(obj);
			PersistenceHelper.WriteAllTextOrThrow(text, content);
			TransactionLogger.Info($"Created cursor configuration snapshot: {text}");
			return text;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn($"Failed to create cursor snapshot: {ex.Message}");
			return null;
		}
	}

	public static CursorSnapshot GetLatestSnapshot(out string snapshotFilePath)
	{
		snapshotFilePath = null;
		try
		{
			if (!Directory.Exists(SnapshotDirectory))
			{
				return null;
			}
			string[] files = Directory.GetFiles(SnapshotDirectory, "cursor_snapshot_*.json");
			if (files == null || files.Length == 0)
			{
				return null;
			}
			Array.Sort(files);
			string input = PersistenceHelper.ReadShared(snapshotFilePath = files[files.Length - 1]);
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			return javaScriptSerializer.Deserialize<CursorSnapshot>(input);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn($"Failed to load latest snapshot: {ex.Message}");
			return null;
		}
	}

	public static CursorApplyResult ApplyCursorPack(CursorPack pack)
	{
		CursorApplyResult cursorApplyResult = new CursorApplyResult();
		cursorApplyResult.Success = false;
		cursorApplyResult.PackId = ((pack != null) ? pack.Id : "unknown");
		cursorApplyResult.PackName = ((pack != null) ? pack.Name : "unknown");
		CursorApplyResult cursorApplyResult2 = cursorApplyResult;
		if (pack == null || pack.Manifest == null)
		{
			cursorApplyResult2.ErrorMessage = "Cursor pack or manifest is null.";
			return cursorApplyResult2;
		}
		if (!CursorPackManager.ValidatePackFiles(pack, out var missingFiles))
		{
			cursorApplyResult2.ErrorMessage = "Pack validation failed: " + string.Join("; ", missingFiles.ToArray());
			TransactionLogger.Warn(cursorApplyResult2.ErrorMessage);
			return cursorApplyResult2;
		}
		string text2 = (cursorApplyResult2.SnapshotPath = CreateSnapshot());
		Dictionary<string, string> dictionary = pack.ResolveRegistryMapping();
		if (dictionary.Count < CursorRegistryKeys.Length)
		{
			cursorApplyResult2.ErrorMessage = $"Incomplete state mapping: expected {CursorRegistryKeys.Length} states, got {dictionary.Count}";
			return cursorApplyResult2;
		}
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: true))
			{
				if (registryKey == null)
				{
					throw new InvalidOperationException("Could not open registry key HKCU\\Control Panel\\Cursors");
				}
				string value = $"Ayn - {pack.Name}";
				registryKey.SetValue("", value, RegistryValueKind.String);
				registryKey.SetValue("Scheme Source", 1, RegistryValueKind.DWord);
				foreach (KeyValuePair<string, string> item in dictionary)
				{
					registryKey.SetValue(item.Key, item.Value, RegistryValueKind.String);
				}
				registryKey.Flush();
			}
			if (!SystemParametersInfo(87u, 0u, IntPtr.Zero, 3u))
			{
				int lastWin32Error = Marshal.GetLastWin32Error();
				TransactionLogger.Warn($"SystemParametersInfo(SPI_SETCURSORS) Win32 notice: {lastWin32Error}");
			}
			try
			{
				SendMessageTimeout((IntPtr)65535, 26u, UIntPtr.Zero, "Cursors", 2u, 1000u, out var _);
			}
			catch
			{
			}
			if (!VerifyCursorScheme(dictionary))
			{
				TransactionLogger.Warn("Cursor scheme verification failed after write. Initiating automatic rollback.");
				RollbackSnapshot(text2);
				cursorApplyResult2.ErrorMessage = "Registry verification mismatch after apply. Changes rolled back.";
				return cursorApplyResult2;
			}
			cursorApplyResult2.Success = true;
			cursorApplyResult2.StatesApplied = dictionary.Count;
			TransactionLogger.Info($"Successfully applied cursor pack '{pack.Name}' with {dictionary.Count} multi-res states.");
			return cursorApplyResult2;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error($"Exception applying cursor pack '{pack.Name}': {ex.Message}. Performing rollback.");
			if (!string.IsNullOrEmpty(text2))
			{
				RollbackSnapshot(text2);
			}
			cursorApplyResult2.ErrorMessage = ex.Message;
			return cursorApplyResult2;
		}
	}

	public static void ApplyCursorScheme(Dictionary<string, string> cursorMap, string schemeName = "Ayn Minimal")
	{
		if (cursorMap == null || cursorMap.Count == 0)
		{
			throw new ArgumentException("Cursor mapping dictionary is null or empty.");
		}
		using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: true))
		{
			if (registryKey == null)
			{
				throw new InvalidOperationException("Could not open registry key HKCU\\Control Panel\\Cursors");
			}
			registryKey.SetValue("", schemeName);
			registryKey.SetValue("Scheme Source", 1, RegistryValueKind.DWord);
			foreach (KeyValuePair<string, string> item in cursorMap)
			{
				string fullPath = Path.GetFullPath(item.Value);
				if (!File.Exists(fullPath))
				{
					throw new FileNotFoundException($"Cursor asset for state '{item.Key}' does not exist: {fullPath}");
				}
				registryKey.SetValue(item.Key, fullPath, RegistryValueKind.String);
			}
			registryKey.Flush();
		}
		SystemParametersInfo(87u, 0u, IntPtr.Zero, 3u);
		try
		{
			SendMessageTimeout((IntPtr)65535, 26u, UIntPtr.Zero, "Cursors", 2u, 1000u, out var _);
		}
		catch
		{
		}
		TransactionLogger.Info($"Applied cursor scheme '{schemeName}' with {cursorMap.Count} states and triggered live reload.");
	}

	public static void RestoreCursorScheme(Dictionary<string, string> savedScheme, string schemeName = "Windows Default", int schemeSource = 1)
	{
		if (savedScheme == null || savedScheme.Count == 0)
		{
			return;
		}
		using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: true))
		{
			if (registryKey != null)
			{
				registryKey.SetValue("", schemeName ?? "Windows Default");
				registryKey.SetValue("Scheme Source", schemeSource, RegistryValueKind.DWord);
				foreach (KeyValuePair<string, string> item in savedScheme)
				{
					registryKey.SetValue(item.Key, item.Value ?? string.Empty, RegistryValueKind.String);
				}
				registryKey.Flush();
			}
		}
		SystemParametersInfo(87u, 0u, IntPtr.Zero, 3u);
		try
		{
			SendMessageTimeout((IntPtr)65535, 26u, UIntPtr.Zero, "Cursors", 2u, 1000u, out var _);
		}
		catch
		{
		}
		TransactionLogger.Info("Previous cursor scheme successfully restored.");
	}

	public static bool RollbackSnapshot(string snapshotFilePath = null)
	{
		try
		{
			CursorSnapshot cursorSnapshot;
			if (!string.IsNullOrEmpty(snapshotFilePath) && File.Exists(snapshotFilePath))
			{
				string input = PersistenceHelper.ReadShared(snapshotFilePath);
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				cursorSnapshot = javaScriptSerializer.Deserialize<CursorSnapshot>(input);
			}
			else
			{
				cursorSnapshot = GetLatestSnapshot(out var snapshotFilePath2);
				snapshotFilePath = snapshotFilePath2;
			}
			if (cursorSnapshot == null || cursorSnapshot.CursorMap == null)
			{
				TransactionLogger.Warn("Rollback failed: No valid snapshot available.");
				return false;
			}
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: true))
			{
				if (registryKey == null)
				{
					return false;
				}
				registryKey.SetValue("", cursorSnapshot.SchemeName ?? "Windows Default", RegistryValueKind.String);
				registryKey.SetValue("Scheme Source", cursorSnapshot.SchemeSource, RegistryValueKind.DWord);
				foreach (KeyValuePair<string, string> item in cursorSnapshot.CursorMap)
				{
					registryKey.SetValue(item.Key, item.Value ?? string.Empty, RegistryValueKind.String);
				}
				registryKey.Flush();
			}
			SystemParametersInfo(87u, 0u, IntPtr.Zero, 3u);
			try
			{
				SendMessageTimeout((IntPtr)65535, 26u, UIntPtr.Zero, "Cursors", 2u, 1000u, out var _);
			}
			catch
			{
			}
			TransactionLogger.Info($"Rollback to snapshot '{snapshotFilePath}' completed successfully.");
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error($"Failed to execute rollback: {ex.Message}");
			return false;
		}
	}

	public static bool RestoreDefaultCursors()
	{
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: true))
			{
				if (registryKey == null)
				{
					return false;
				}
				registryKey.SetValue("", "Windows Default", RegistryValueKind.String);
				registryKey.SetValue("Scheme Source", 0, RegistryValueKind.DWord);
				string[] cursorRegistryKeys = CursorRegistryKeys;
				foreach (string name in cursorRegistryKeys)
				{
					registryKey.SetValue(name, string.Empty, RegistryValueKind.String);
				}
				registryKey.Flush();
			}
			SystemParametersInfo(87u, 0u, IntPtr.Zero, 3u);
			try
			{
				SendMessageTimeout((IntPtr)65535, 26u, UIntPtr.Zero, "Cursors", 2u, 1000u, out var _);
			}
			catch
			{
			}
			TransactionLogger.Info("Restored Windows Default cursors cleanly.");
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error($"Failed to restore default cursors: {ex.Message}");
			return false;
		}
	}

	public static bool VerifyCursorScheme(Dictionary<string, string> expectedMap)
	{
		if (expectedMap == null)
		{
			return false;
		}
		using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Cursors", writable: false))
		{
			if (registryKey == null)
			{
				return false;
			}
			foreach (KeyValuePair<string, string> item in expectedMap)
			{
				object value = registryKey.GetValue(item.Key);
				if (value == null)
				{
					return false;
				}
				string text = value.ToString();
				string fullPath = Path.GetFullPath(item.Value);
				if (!string.Equals(Path.GetFullPath(text), fullPath, StringComparison.OrdinalIgnoreCase))
				{
					TransactionLogger.Warn($"Cursor mismatch for '{item.Key}': reg='{text}', expected='{fullPath}'");
					return false;
				}
			}
		}
		return true;
	}
}
