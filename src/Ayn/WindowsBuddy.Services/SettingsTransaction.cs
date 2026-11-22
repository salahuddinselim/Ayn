using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Analytics;
using WindowsBuddy.Tools.Clock;
using WindowsBuddy.Tools.Links;
using WindowsBuddy.Tools.Weather;

namespace WindowsBuddy.Services;

public static class SettingsTransaction
{
	public class Item
	{
		public string Root;

		public string Relative;

		public string Kind;

		public string Text;

		public bool Started;

		public bool HadOriginal;

		public bool WasDirectory;
	}

	public class Journal
	{
		public string Id;

		public string State;

		public bool ClearAll;

		public List<Item> Items = new List<Item>();
	}

	private class LegacyScratchpadSettings
	{
		public bool IsPinned { get; set; }

		public bool WordWrap { get; set; }

		public bool MovingCapsuleText { get; set; }
	}

	private static readonly object Gate = new object();

	private static readonly string[] Areas = new string[12]
	{
		"clipboard", "notes", "screentime", "shelf", "calendar", "weather", "analytics", "timer", "links", "message",
		"settings", "logs"
	};

	private static string JournalPath => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName + "-transactions", "pending.json");

	private static Type Schema(string relative)
	{
		return relative.ToLowerInvariant() switch
		{
			"settings\\app_preferences.json" => typeof(AppPreferencesModel), 
			"settings\\notch_config.json" => typeof(NotchConfigModel), 
			"settings\\notch_behavior.json" => typeof(NotchBehaviorConfig).GetNestedType("NotchBehaviorConfigDto", BindingFlags.NonPublic), 
			"settings\\tools_config.json" => typeof(ToolConfigModel), 
			"settings\\clock_settings.json" => typeof(ClockSettingsModel), 
			"settings\\scratchpad_state.json" => typeof(LegacyScratchpadSettings), 
			"weather\\prefs.json" => typeof(WeatherPrefs), 
			"analytics\\settings.json" => typeof(AnalyticsSettings), 
			"links\\links_prefs.json" => typeof(LinksPreferences), 
			_ => null, 
		};
	}

	public static bool IsSettingsFile(string relative)
	{
		if (relative != null)
		{
			if (!(Schema(relative) != null))
			{
				return relative.Equals("settings\\first_run_state.json", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	public static void RecoverStores()
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string path = Path.Combine(AppPaths.DataDir, "settings");
		if (Directory.Exists(path))
		{
			foreach (string item2 in from p in Directory.GetFiles(path, "*.json*")
				where p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase)
				select p)
			{
				hashSet.Add("settings\\" + Path.GetFileName(item2).Replace(".json.bak", ".json"));
			}
		}
		string[] array = new string[4] { "weather\\prefs.json", "analytics\\settings.json", "links\\links_prefs.json", "message\\settings.json" };
		foreach (string item in array)
		{
			hashSet.Add(item);
		}
		foreach (string item3 in hashSet)
		{
			string text2 = Path.Combine(AppPaths.DataDir, item3);
			if (!File.Exists(text2) && !File.Exists(text2 + ".bak"))
			{
				continue;
			}
			Type schema = Schema(item3);
			PersistenceHelper.ReadWithBackup(text2, delegate(string text)
			{
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer
				{
					MaxJsonLength = 67108864
				};
				if (!(javaScriptSerializer.DeserializeObject(text) is Dictionary<string, object> obj))
				{
					throw new InvalidDataException("Expected a settings object.");
				}
				if (schema != null)
				{
					javaScriptSerializer.ConvertToType(obj, schema);
				}
				return text;
			});
		}
	}

	public static void ValidateFile(string relative, string text)
	{
		if (!IsSettingsFile(relative) || text == null || text.Length > 4194304)
		{
			throw new InvalidDataException("Unsupported settings file: " + relative);
		}
		JavaScriptSerializer serializer = new JavaScriptSerializer
		{
			MaxJsonLength = 4194304
		};
		if (!(serializer.DeserializeObject(text) is Dictionary<string, object> { Count: not 0 } dictionary))
		{
			throw new InvalidDataException("Expected a nonempty settings object: " + relative);
		}
		Type type = Schema(relative);
		if (type == null)
		{
			if (!dictionary.TryGetValue("state", out var value) || !(value is string) || !new string[4] { "NotStarted", "Started", "Completed", "Dismissed" }.Contains((string)value))
			{
				throw new InvalidDataException("Invalid first-run state.");
			}
			return;
		}
		ValidateShape(dictionary, type);
		object model = serializer.ConvertToType(dictionary, type);
		if (model is NotchConfigModel notchConfigModel)
		{
			Dictionary<string, string> before = dictionary.Keys.ToDictionary((string k) => k, (string k) => serializer.Serialize(type.GetProperty(k).GetValue(model, null)));
			notchConfigModel.Validate();
			if (dictionary.Keys.Any((string k) => before[k] != serializer.Serialize(type.GetProperty(k).GetValue(model, null))))
			{
				throw new InvalidDataException("Notch settings contain invalid values.");
			}
		}
		if (model is AppPreferencesModel appPreferencesModel && (appPreferencesModel.ClipboardHistoryLimit < 1 || appPreferencesModel.ClipboardRetentionDays < 0 || appPreferencesModel.Language != "en"))
		{
			throw new InvalidDataException("Invalid app preferences.");
		}
		if (model is ToolConfigModel toolConfigModel && (toolConfigModel.Tools == null || toolConfigModel.Tools.Any((ToolItemConfig t) => t == null || string.IsNullOrWhiteSpace(t.Id)) || toolConfigModel.Tools.Select((ToolItemConfig t) => t.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != toolConfigModel.Tools.Count || (toolConfigModel.OpenOn != "hover" && toolConfigModel.OpenOn != "click")))
		{
			throw new InvalidDataException("Invalid module settings.");
		}
		if (model is ClockSettingsModel clockSettingsModel && (clockSettingsModel.WorldClocks == null || clockSettingsModel.WorldClocks.Any((WorldClockCity c) => c == null)))
		{
			throw new InvalidDataException("Invalid clock settings.");
		}
		if (model is WeatherPrefs weatherPrefs && (weatherPrefs.Recent == null || weatherPrefs.Recent.Any((RecentCity c) => c == null) || (!string.IsNullOrEmpty(weatherPrefs.WindUnit) && weatherPrefs.WindUnit != "kmh" && weatherPrefs.WindUnit != "mph")))
		{
			throw new InvalidDataException("Invalid weather preferences.");
		}
		if (!(model is AnalyticsSettings analyticsSettings) || (analyticsSettings.Sites != null && !analyticsSettings.Sites.Any((AnalyticsSite s) => s == null || string.IsNullOrWhiteSpace(s.Key) || s.Key.Any((char c) => !char.IsLetterOrDigit(c)) || !new string[3] { "plausible", "datafast", "ga4" }.Contains(s.Provider))))
		{
			return;
		}
		throw new InvalidDataException("Invalid analytics settings.");
	}

	private static void ValidateShape(object value, Type type)
	{
		Type underlyingType = Nullable.GetUnderlyingType(type);
		if (value == null)
		{
			if (type.IsValueType && underlyingType == null)
			{
				throw new InvalidDataException("Null numeric/boolean setting.");
			}
			return;
		}
		if (underlyingType != null)
		{
			type = underlyingType;
		}
		if (type == typeof(string))
		{
			if (value is string)
			{
				return;
			}
			throw new InvalidDataException("Expected text setting.");
		}
		if (type == typeof(bool))
		{
			if (value is bool)
			{
				return;
			}
			throw new InvalidDataException("Expected boolean setting.");
		}
		if (type.IsEnum)
		{
			if (value is int && Enum.IsDefined(type, value))
			{
				return;
			}
			throw new InvalidDataException("Invalid option value.");
		}
		if (type.IsPrimitive || type == typeof(decimal))
		{
			if (!(value is int) && !(value is long) && !(value is double) && !(value is decimal))
			{
				throw new InvalidDataException("Expected numeric setting.");
			}
			double num = Convert.ToDouble(value);
			if (!double.IsNaN(num) && !double.IsInfinity(num) && ((!(type == typeof(int)) && !(type == typeof(long))) || num == Math.Truncate(num)))
			{
				return;
			}
			throw new InvalidDataException("Invalid numeric setting.");
		}
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<, >))
		{
			if (!(value is Dictionary<string, object> dictionary))
			{
				throw new InvalidDataException("Expected settings map.");
			}
			{
				foreach (object value3 in dictionary.Values)
				{
					ValidateShape(value3, type.GetGenericArguments()[1]);
				}
				return;
			}
		}
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
		{
			if (!(value is object[] array))
			{
				throw new InvalidDataException("Expected settings list.");
			}
			object[] array2 = array;
			foreach (object value2 in array2)
			{
				ValidateShape(value2, type.GetGenericArguments()[0]);
			}
			return;
		}
		if (!(value is Dictionary<string, object> dictionary2))
		{
			throw new InvalidDataException("Expected settings object.");
		}
		foreach (KeyValuePair<string, object> item in dictionary2)
		{
			PropertyInfo property = type.GetProperty(item.Key);
			if (property == null || !property.CanWrite)
			{
				throw new InvalidDataException("Unknown setting: " + item.Key);
			}
			ValidateShape(item.Value, property.PropertyType);
		}
	}

	public static void QueueImport(Dictionary<string, string> files)
	{
		if (files == null || files.Count == 0)
		{
			throw new InvalidDataException("The bundle has no settings.");
		}
		Journal journal = NewJournal();
		foreach (KeyValuePair<string, string> file in files)
		{
			ValidateFile(file.Key, file.Value);
			journal.Items.Add(new Item
			{
				Root = "local",
				Relative = file.Key,
				Kind = "Write",
				Text = file.Value
			});
			journal.Items.Add(new Item
			{
				Root = "local",
				Relative = file.Key + ".bak",
				Kind = "Write",
				Text = file.Value
			});
		}
		Queue(journal);
	}

	public static void QueueBackupRestore(Dictionary<string, string> files, Dictionary<string, string> assets)
	{
		if (files == null || files.Count == 0)
		{
			throw new InvalidDataException("The backup has no data.");
		}
		Journal journal = NewJournal();
		foreach (KeyValuePair<string, string> asset in assets)
		{
			journal.Items.Add(new Item
			{
				Root = "local",
				Relative = asset.Key,
				Kind = "BackupAsset",
				Text = asset.Value
			});
		}
		foreach (KeyValuePair<string, string> file in files)
		{
			string[] array = new string[2] { "", ".bak" };
			foreach (string text in array)
			{
				journal.Items.Add(new Item
				{
					Root = ((file.Key == "converter_state.json") ? "roaming" : "local"),
					Relative = file.Key + text,
					Kind = "BackupData",
					Text = file.Value
				});
			}
		}
		Queue(journal);
	}

	public static void QueueClear(string area)
	{
		if (!Areas.Contains(area))
		{
			throw new ArgumentException("Unknown data area.", "area");
		}
		Journal journal = NewJournal();
		string[] array = new string[2] { "local", "roaming" };
		foreach (string root in array)
		{
			journal.Items.Add(new Item
			{
				Root = root,
				Relative = area,
				Kind = "Clear"
			});
		}
		if (area == "settings")
		{
			string[] array2 = new string[2] { "weather\\prefs.json", "links\\links_prefs.json" };
			foreach (string text in array2)
			{
				string[] array3 = new string[2] { "", ".bak" };
				foreach (string text2 in array3)
				{
					journal.Items.Add(new Item
					{
						Root = "local",
						Relative = text + text2,
						Kind = "Clear"
					});
				}
			}
		}
		Queue(journal);
	}

	public static void QueueClearAll()
	{
		Journal journal = NewJournal();
		journal.ClearAll = true;
		string[] array = new string[2] { "local", "roaming" };
		foreach (string root in array)
		{
			foreach (string item in Areas.Where((string a) => a != "logs"))
			{
				journal.Items.Add(new Item
				{
					Root = root,
					Relative = item,
					Kind = "Clear"
				});
			}
		}
		ExpandClearAll(journal);
		Queue(journal);
	}

	private static bool ProtectedName(string name)
	{
		if (!name.Equals("logs", StringComparison.OrdinalIgnoreCase) && !name.Equals("trial", StringComparison.OrdinalIgnoreCase) && !(name == ".migrated-from-windowsbuddy") && !(name == ".reset-pending") && !name.StartsWith("Ayn_Ayn.dat", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Ayn_DevAyn.dat", StringComparison.OrdinalIgnoreCase))
		{
			return name.Contains(".ayn-");
		}
		return true;
	}

	private static void ExpandClearAll(Journal journal)
	{
		string[] array = new string[2] { "local", "roaming" };
		foreach (string root in array)
		{
			string path = ((root == "local") ? AppPaths.DataDir : Path.Combine(AppPaths.RoamingAppData, AppPaths.FolderName));
			if (!Directory.Exists(path))
			{
				continue;
			}
			string[] fileSystemEntries = Directory.GetFileSystemEntries(path);
			foreach (string path2 in fileSystemEntries)
			{
				string name = Path.GetFileName(path2);
				if (!ProtectedName(name) && !journal.Items.Any((Item i) => i.Root == root && i.Relative.Equals(name, StringComparison.OrdinalIgnoreCase)))
				{
					journal.Items.Add(new Item
					{
						Root = root,
						Relative = name,
						Kind = "ClearAll"
					});
				}
			}
		}
	}

	private static Journal NewJournal()
	{
		Journal journal = new Journal();
		journal.Id = Guid.NewGuid().ToString("N");
		journal.State = "Queued";
		return journal;
	}

	private static void Queue(Journal journal)
	{
		lock (Gate)
		{
			if (File.Exists(JournalPath))
			{
				throw new IOException("A data operation is already pending. Restart Ayn to finish it first.");
			}
			foreach (Item item in journal.Items)
			{
				Target(item);
			}
			WriteJournal(journal);
		}
	}

	public static void CancelQueued()
	{
		lock (Gate)
		{
			if (File.Exists(JournalPath))
			{
				Journal journal = ReadJournal();
				if (journal.State != "Queued")
				{
					throw new IOException("An operation already started; restart to recover it.");
				}
				File.Delete(JournalPath);
			}
		}
	}

	private static string Target(Item item)
	{
		if (item.Root != "local" && item.Root != "roaming")
		{
			throw new InvalidDataException("Invalid transaction root.");
		}
		string relative = item.Relative;
		if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains("..") || relative.Contains(':') || relative.Contains('/'))
		{
			throw new InvalidDataException("Invalid transaction path.");
		}
		string text = (relative.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ? relative.Substring(0, relative.Length - 4) : relative);
		if (item.Kind != "Write" && item.Kind != "Clear" && item.Kind != "ClearAll" && item.Kind != "BackupData" && item.Kind != "BackupAsset")
		{
			throw new InvalidDataException("Invalid data operation.");
		}
		if (item.Kind == "BackupData")
		{
			UserBackupService.ValidateData(text, (text == "clipboard\\history.json") ? UserBackupService.DecodeClipboard(item.Text) : item.Text, storedData: true);
		}
		else if (item.Kind == "BackupAsset")
		{
			if (!UserBackupService.IsAsset(relative) || item.Text == null || (long)item.Text.Length > 268435456L)
			{
				throw new InvalidDataException("Invalid backup asset.");
			}
			Convert.FromBase64String(item.Text);
		}
		else if (item.Kind == "Write")
		{
			ValidateFile(text, item.Text);
		}
		else if (item.Kind == "ClearAll")
		{
			if (relative.Contains('\\') || ProtectedName(relative))
			{
				throw new InvalidDataException("Protected clear target.");
			}
		}
		else if (!Areas.Contains(relative) && !IsSettingsFile(text))
		{
			throw new InvalidDataException("Invalid clear target.");
		}
		string text2 = ((item.Root == "local") ? AppPaths.DataDir : Path.Combine(AppPaths.RoamingAppData, AppPaths.FolderName));
		string fullPath = Path.GetFullPath(Path.Combine(text2, relative));
		if (!fullPath.StartsWith(Path.GetFullPath(text2) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Target outside Ayn data.");
		}
		for (string text3 = fullPath; text3 != null; text3 = Path.GetDirectoryName(text3))
		{
			if ((File.Exists(text3) || Directory.Exists(text3)) && (File.GetAttributes(text3) & FileAttributes.ReparsePoint) != 0)
			{
				throw new IOException("Data operations cannot traverse junctions or symbolic links.");
			}
		}
		return fullPath;
	}

	private static void AtomicText(string path, string text)
	{
		if (PersistenceHelper.WritesSealed)
		{
			PersistenceHelper.RecordSealViolation(path, "settings-journal-after-seal");
			throw new IOException("Ayn is closing; the settings operation was not queued.");
		}
		PersistenceHelper.WriteAllTextOrThrow(path, text);
	}

	private static void WriteJournal(Journal journal)
	{
		AtomicText(JournalPath, new JavaScriptSerializer
		{
			MaxJsonLength = 536870912
		}.Serialize(journal));
	}

	private static Journal ReadJournal()
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = 536870912;
		Journal journal = javaScriptSerializer.Deserialize<Journal>(PersistenceHelper.ReadShared(JournalPath));
		if (journal == null || !Guid.TryParseExact(journal.Id, "N", out var _) || journal.Items == null || journal.Items.Count == 0 || !new string[3] { "Queued", "Applying", "Committed" }.Contains(journal.State))
		{
			throw new InvalidDataException("Data-operation journal is invalid. It has been preserved.");
		}
		foreach (Item item in journal.Items)
		{
			Target(item);
		}
		if (journal.Items.Select((Item i) => Target(i)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != journal.Items.Count)
		{
			throw new InvalidDataException("Duplicate transaction targets.");
		}
		return journal;
	}

	private static string Backup(string target, Journal journal)
	{
		return target + ".ayn-rollback-" + journal.Id;
	}

	private static bool Exists(string path)
	{
		if (!File.Exists(path))
		{
			return Directory.Exists(path);
		}
		return true;
	}

	private static void Remove(string path)
	{
		if (Directory.Exists(path))
		{
			Directory.Delete(path, recursive: true);
		}
		else if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private static void Preflight(string target)
	{
		if (!Exists(target))
		{
			return;
		}
		Stack<string> stack = new Stack<string>();
		stack.Push(target);
		while (stack.Count > 0)
		{
			string path = stack.Pop();
			FileAttributes attributes = File.GetAttributes(path);
			if ((attributes & FileAttributes.ReparsePoint) != 0)
			{
				throw new IOException("Data contains a symbolic link or junction.");
			}
			if (Directory.Exists(path))
			{
				string[] fileSystemEntries = Directory.GetFileSystemEntries(path);
				foreach (string item in fileSystemEntries)
				{
					stack.Push(item);
				}
			}
			else
			{
				if ((attributes & FileAttributes.ReadOnly) != 0)
				{
					throw new IOException("A data file is read-only: " + Path.GetFileName(path));
				}
				using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
				{
				}
			}
		}
	}

	private static void Rollback(Journal journal)
	{
		foreach (Item item in from i in journal.Items.AsEnumerable().Reverse()
			where i.Started
			select i)
		{
			string text = Target(item);
			string text2 = Backup(text, journal);
			if (Exists(text2))
			{
				Remove(text);
				if (item.WasDirectory)
				{
					Directory.Move(text2, text);
				}
				else
				{
					File.Move(text2, text);
				}
			}
			else if (!item.HadOriginal)
			{
				Remove(text);
			}
			Remove(text + ".ayn-incoming-" + journal.Id);
			item.Started = false;
		}
		journal.State = "Queued";
		WriteJournal(journal);
	}

	public static void ApplyPending()
	{
		lock (Gate)
		{
			if (!File.Exists(JournalPath))
			{
				return;
			}
			Journal journal = ReadJournal();
			if (journal.State == "Applying")
			{
				Rollback(journal);
			}
			if (journal.State != "Committed")
			{
				if (journal.ClearAll)
				{
					ExpandClearAll(journal);
					WriteJournal(journal);
				}
				foreach (Item item in journal.Items)
				{
					Preflight(Target(item));
				}
				journal.State = "Applying";
				WriteJournal(journal);
				try
				{
					foreach (Item item2 in journal.Items)
					{
						string text = Target(item2);
						string text2 = Backup(text, journal);
						item2.HadOriginal = Exists(text);
						item2.WasDirectory = Directory.Exists(text);
						item2.Started = true;
						WriteJournal(journal);
						if (item2.HadOriginal)
						{
							if (item2.WasDirectory)
							{
								Directory.Move(text, text2);
							}
							else
							{
								File.Move(text, text2);
							}
						}
						if (!(item2.Kind == "Write") && !(item2.Kind == "BackupData") && !(item2.Kind == "BackupAsset"))
						{
							continue;
						}
						string text3 = text + ".ayn-incoming-" + journal.Id;
						if (item2.Kind == "BackupAsset")
						{
							Directory.CreateDirectory(Path.GetDirectoryName(text3));
							using FileStream fileStream = new FileStream(text3, FileMode.CreateNew, FileAccess.Write, FileShare.None);
							byte[] array = Convert.FromBase64String(item2.Text);
							fileStream.Write(array, 0, array.Length);
							fileStream.Flush(flushToDisk: true);
						}
						else
						{
							AtomicText(text3, item2.Text);
						}
						File.Move(text3, text);
					}
					journal.State = "Committed";
					WriteJournal(journal);
				}
				catch
				{
					Rollback(journal);
					throw;
				}
			}
			foreach (Item item3 in journal.Items)
			{
				Remove(Backup(Target(item3), journal));
			}
			File.Delete(JournalPath);
		}
	}
}
