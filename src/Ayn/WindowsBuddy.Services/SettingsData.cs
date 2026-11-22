using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class SettingsData
{
	private const string ResetMarker = ".reset-pending";

	private static IEnumerable<string> SettingFiles()
	{
		string dir = Path.Combine(AppPaths.DataDir, "settings");
		if (Directory.Exists(dir))
		{
			try
			{
				string[] files = Directory.GetFiles(dir, "*.json");
				foreach (string f in files)
				{
					if (SettingsTransaction.IsSettingsFile("settings\\" + Path.GetFileName(f)))
					{
						yield return "settings\\" + Path.GetFileName(f);
					}
				}
			}
			finally
			{
			}
		}
		try
		{
			string[] array = new string[3] { "weather\\prefs.json", "analytics\\settings.json", "links\\links_prefs.json" };
			foreach (string rel in array)
			{
				if (File.Exists(Path.Combine(AppPaths.DataDir, rel)))
				{
					yield return rel;
				}
			}
		}
		finally
		{
		}
	}

	public static int ExportAll(string destination)
	{
		if (!PersistenceHelper.FlushForExit())
		{
			throw new IOException("Pending settings could not be saved; export was cancelled.");
		}
		string result = AppPreferences.Instance.ExportSnapshotAsync().GetAwaiter().GetResult();
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (string item in SettingFiles())
		{
			string value = PersistenceHelper.SafeReadAllText(Path.Combine(AppPaths.DataDir, item));
			if (!string.IsNullOrEmpty(value))
			{
				if (item.EndsWith("app_preferences.json", StringComparison.OrdinalIgnoreCase))
				{
					value = result;
				}
				dictionary[item] = value;
			}
		}
		dictionary["settings\\app_preferences.json"] = result;
		Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
		dictionary2.Add("app", "Ayn");
		dictionary2.Add("kind", "settings");
		dictionary2.Add("version", 1);
		dictionary2.Add("exported", DateTime.UtcNow.ToString("o"));
		dictionary2.Add("files", dictionary);
		Dictionary<string, object> obj = dictionary2;
		PersistenceHelper.WriteAllTextOrThrow(destination, new JavaScriptSerializer
		{
			MaxJsonLength = int.MaxValue
		}.Serialize(obj));
		TransactionLogger.Info($"Exported {dictionary.Count} settings files to {destination}");
		return dictionary.Count;
	}

	public static bool ImportAll(string source, out string message)
	{
		try
		{
			if (new FileInfo(source).Length > 67108864)
			{
				throw new InvalidDataException("The settings bundle is too large.");
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			javaScriptSerializer.MaxJsonLength = 67108864;
			Dictionary<string, object> dictionary = javaScriptSerializer.Deserialize<Dictionary<string, object>>(PersistenceHelper.ReadShared(source));
			if (dictionary == null || !dictionary.TryGetValue("app", out var value) || !"Ayn".Equals(value) || !dictionary.TryGetValue("kind", out var value2) || !"settings".Equals(value2) || !dictionary.TryGetValue("version", out var value3) || !(value3 is int) || (int)value3 != 1 || !dictionary.TryGetValue("files", out var value4) || !(value4 is Dictionary<string, object>))
			{
				throw new InvalidDataException("This is not a supported Ayn settings bundle.");
			}
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, object> item in (Dictionary<string, object>)value4)
			{
				string text = item.Key.Replace('/', '\\');
				if (!(item.Value is string))
				{
					throw new InvalidDataException("Invalid settings entry: " + text);
				}
				SettingsTransaction.ValidateFile(text, (string)item.Value);
				dictionary2.Add(text, (string)item.Value);
			}
			SettingsTransaction.QueueImport(dictionary2);
			message = "Settings validated. They will be imported together on restart.";
			return true;
		}
		catch (Exception ex)
		{
			message = "Nothing was imported. " + ex.Message;
			return false;
		}
	}

	private static string StripLicense(string json)
	{
		return json;
	}

	public static void ScheduleReset()
	{
		SettingsTransaction.QueueClearAll();
	}

	public static void ApplyPendingReset()
	{
		string path = Path.Combine(AppPaths.DataDir, ".reset-pending");
		if (File.Exists(path))
		{
			SettingsTransaction.QueueClearAll();
			File.Delete(path);
		}
		SettingsTransaction.ApplyPending();
		SettingsTransaction.RecoverStores();
	}

	public static void Relaunch()
	{
		CapsuleWindow instance = CapsuleWindow.Instance;
		if (instance == null)
		{
			throw new IOException("Restart requires the running Ayn shutdown coordinator.");
		}
		instance.ShutdownApplication(delegate
		{
			try
			{
				string fileName = Process.GetCurrentProcess().MainModule.FileName;
				Process.Start(new ProcessStartInfo(fileName, "relaunch " + Process.GetCurrentProcess().Id)
				{
					UseShellExecute = false
				});
			}
			catch (Exception innerException)
			{
				SettingsTransaction.CancelQueued();
				throw new IOException("Restart launch failed; shutdown was cancelled.", innerException);
			}
		});
	}

	public static void WaitForPreviousInstance(string pidArg)
	{
		if (!int.TryParse(pidArg, out var result))
		{
			return;
		}
		try
		{
			using Process process = Process.GetProcessById(result);
			if (!string.Equals(process.MainModule.FileName, Process.GetCurrentProcess().MainModule.FileName, StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException("Relaunch target is not this Ayn artifact.");
			}
			process.WaitForExit();
		}
		catch (ArgumentException)
		{
		}
	}

	public static long SafeDirectorySize(string path)
	{
		if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
		{
			return 0L;
		}
		long num = 0L;
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(path);
			foreach (FileInfo item in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
			{
				try
				{
					num += item.Length;
				}
				catch
				{
				}
			}
		}
		catch
		{
			try
			{
				string[] files = Directory.GetFiles(path);
				foreach (string fileName in files)
				{
					try
					{
						num += new FileInfo(fileName).Length;
					}
					catch
					{
					}
				}
				string[] directories = Directory.GetDirectories(path);
				foreach (string path2 in directories)
				{
					num += SafeDirectorySize(path2);
				}
			}
			catch
			{
			}
		}
		return num;
	}

	public static long GetAreaBytes(string areaKey)
	{
		if (string.IsNullOrEmpty(areaKey))
		{
			return 0L;
		}
		try
		{
			string path = Path.Combine(AppPaths.DataDir, areaKey);
			if (!Directory.Exists(path))
			{
				return 0L;
			}
			return SafeDirectorySize(path);
		}
		catch
		{
			return 0L;
		}
	}

	public static List<KeyValuePair<string, long>> UsageByArea()
	{
		List<KeyValuePair<string, long>> list = new List<KeyValuePair<string, long>>();
		try
		{
			if (!Directory.Exists(AppPaths.DataDir))
			{
				return list;
			}
			string[] directories = Directory.GetDirectories(AppPaths.DataDir);
			foreach (string path in directories)
			{
				long value = SafeDirectorySize(path);
				list.Add(new KeyValuePair<string, long>(Path.GetFileName(path), value));
			}
		}
		catch
		{
		}
		return list;
	}

	public static string FormatBytes(long b)
	{
		if (b <= 0)
		{
			return "Nothing yet";
		}
		if (b < 1024)
		{
			return b + " B";
		}
		if (b < 1048576)
		{
			return ((double)b / 1024.0).ToString("0.#") + " KB";
		}
		return ((double)b / 1048576.0).ToString("0.#") + " MB";
	}

	public static void SafeDeleteDirectoryContents(string dir)
	{
		if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
		{
			return;
		}
		try
		{
			string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
			foreach (string path in files)
			{
				try
				{
					File.Delete(path);
				}
				catch
				{
				}
			}
			string[] directories = Directory.GetDirectories(dir);
			foreach (string path2 in directories)
			{
				try
				{
					Directory.Delete(path2, recursive: true);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	public static bool TryClearArea(string key, out string message)
	{
		try
		{
			SettingsTransaction.QueueClear((key ?? "").ToLowerInvariant());
			message = "Data will be cleared together on restart.";
			return true;
		}
		catch (Exception ex)
		{
			message = "Nothing was cleared. " + ex.Message;
			return false;
		}
	}

	public static void ClearArea(string key)
	{
		if (!TryClearArea(key, out var message))
		{
			throw new IOException(message);
		}
	}
}
