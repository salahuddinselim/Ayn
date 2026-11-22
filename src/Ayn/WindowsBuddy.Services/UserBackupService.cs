using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using WindowsBuddy.Tools.Calendar;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.Tools.FileShelf;
using WindowsBuddy.Tools.Files;
using WindowsBuddy.Tools.Links;
using WindowsBuddy.Tools.Message;
using WindowsBuddy.Tools.Notes;
using WindowsBuddy.Tools.Sounds;
using WindowsBuddy.Tools.Timer;

namespace WindowsBuddy.Services;

public static class UserBackupService
{
	public sealed class Bundle
	{
		public string App = "Ayn";

		public string Kind = "backup";

		public int Version = 1;

		public string CreatedUtc = DateTime.UtcNow.ToString("o");

		public Dictionary<string, string> Files = new Dictionary<string, string>();

		public Dictionary<string, string> Assets = new Dictionary<string, string>();

		public Dictionary<string, string> AssetHashes = new Dictionary<string, string>();
	}

	public const long MaxBytes = 268435456L;

	private const string ClipPrefix = "VAYLI-CLIP-DPAPI1:";

	private static readonly string[] ContentFiles = new string[6] { "notes\\notes.json", "clipboard\\history.json", "calendar\\calendar.json", "shelf\\parked_shelves.json", "links\\bookmarks.json", "timer\\timers_state.json" };

	private static readonly string[] ExtraSettings = new string[4] { "message\\settings.json", "units\\units_state.json", "sounds\\mixer_state.json", "converter_state.json" };

	public static string DefaultFolder => Path.Combine(AppPaths.IsSandboxed ? Path.Combine(AppPaths.LocalAppData, "Documents") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"), "Ayn Backups");

	private static string DataPath(string relative)
	{
		return Path.Combine((relative == "converter_state.json") ? Path.Combine(AppPaths.RoamingAppData, AppPaths.FolderName) : AppPaths.DataDir, relative);
	}

	public static string LatestBackup()
	{
		if (!Directory.Exists(DefaultFolder))
		{
			return null;
		}
		foreach (string item in Directory.GetFiles(DefaultFolder, "*.aynbackup").OrderByDescending(File.GetLastWriteTimeUtc))
		{
			try
			{
				Read(item);
				return item;
			}
			catch
			{
			}
		}
		return null;
	}

	public static bool HasUserContent()
	{
		foreach (string item in ContentFiles.Where((string f) => !f.StartsWith("timer\\")))
		{
			string path = Path.Combine(AppPaths.DataDir, item);
			if (!File.Exists(path))
			{
				continue;
			}
			try
			{
				string text = PersistenceHelper.ReadShared(path);
				if (item.StartsWith("clipboard\\"))
				{
					text = DecodeClipboard(text);
				}
				object obj = Json().DeserializeObject(text);
				if (obj is object[] array && array.Length > 0)
				{
					return true;
				}
				if (item.StartsWith("calendar\\"))
				{
					CalendarData calendarData = Json().Deserialize<CalendarData>(text);
					if (calendarData.Events.Count + calendarData.Reminders.Count > 0)
					{
						return true;
					}
				}
				if (item.StartsWith("shelf\\"))
				{
					ShelfStorageDto shelfStorageDto = Json().Deserialize<ShelfStorageDto>(text);
					if (shelfStorageDto.Shelves.Any((ShelfTabDto t) => (t.Items != null && t.Items.Count > 0) || (t.Paths != null && t.Paths.Count > 0)))
					{
						return true;
					}
				}
			}
			catch
			{
				return true;
			}
		}
		return false;
	}

	public static void RunWeeklyBackup()
	{
		CheckFile(DefaultFolder);
		Directory.CreateDirectory(DefaultFolder);
		Export(Path.Combine(DefaultFolder, "Ayn-auto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".aynbackup"), images: false, shelfFiles: false);
		foreach (string item in Directory.GetFiles(DefaultFolder, "Ayn-auto-*.aynbackup").OrderByDescending(File.GetLastWriteTimeUtc).Skip(4))
		{
			File.Delete(item);
		}
	}

	public static bool WeeklyBackupDue()
	{
		if (Directory.Exists(DefaultFolder))
		{
			return !Directory.GetFiles(DefaultFolder, "Ayn-auto-*.aynbackup").Any((string p) => DateTime.UtcNow - File.GetLastWriteTimeUtc(p) < TimeSpan.FromDays(7.0));
		}
		return true;
	}

	private static JavaScriptSerializer Json()
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = 268435456;
		javaScriptSerializer.RecursionLimit = 80;
		return javaScriptSerializer;
	}

	public static string Digest(byte[] bytes)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(bytes)).Replace("-", "");
	}

	public static bool IsDataFile(string path)
	{
		if (!ContentFiles.Contains(path) && !ExtraSettings.Contains(path))
		{
			if (SettingsTransaction.IsSettingsFile(path) && path != "analytics\\settings.json")
			{
				return path != "settings\\first_run_state.json";
			}
			return false;
		}
		return true;
	}

	public static bool IsAsset(string path)
	{
		if (string.IsNullOrEmpty(path) || path.Contains("..") || path.Contains(':') || path.Contains('/') || Path.IsPathRooted(path))
		{
			return false;
		}
		string[] array = path.Split('\\');
		if (array.Length == 4 && array[0] == "shelf" && array[1] == "stored" && Guid.TryParseExact(array[2], "N", out var result))
		{
			if (array[3].Length > 0 && array[3].Length <= 120 && array[3].IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
			{
				return !array[3].EndsWith(".");
			}
			return false;
		}
		if (array.Length == 3 && array[0] == "clipboard" && array[1] == "images" && Guid.TryParseExact(Path.GetFileNameWithoutExtension(array[2]), "N", out result))
		{
			return Path.GetExtension(path) == ".png";
		}
		return false;
	}

	private static void CheckFile(string path)
	{
		for (string text = Path.GetFullPath(path); text != null; text = Path.GetDirectoryName(text))
		{
			if ((File.Exists(text) || Directory.Exists(text)) && (File.GetAttributes(text) & FileAttributes.ReparsePoint) != 0)
			{
				throw new IOException("Backups cannot follow symbolic links or junctions.");
			}
		}
	}

	private static string AddAsset(Bundle bundle, string source, bool image)
	{
		CheckFile(source);
		if (!File.Exists(source))
		{
			throw new IOException("A selected backup file is missing. Export the list without file copies, or restore the missing file first.");
		}
		if (new FileInfo(source).Length > 134217728)
		{
			throw new IOException("A selected file is too large for a backup (128 MB maximum per file).");
		}
		string text = Path.GetFileName(source);
		if (text.Length > 120)
		{
			text = text.Substring(text.Length - 120);
		}
		string text2 = (image ? ("clipboard\\images\\" + Guid.NewGuid().ToString("N") + ".png") : ("shelf\\stored\\" + Guid.NewGuid().ToString("N") + "\\" + text));
		bundle.Assets.Add(text2, Convert.ToBase64String(File.ReadAllBytes(source)));
		if (((IEnumerable<string>)bundle.Assets.Values).Sum((Func<string, long>)((string v) => v.Length)) > 268435456)
		{
			throw new IOException("The selected backup exceeds 256 MB. Export without file copies.");
		}
		return text2;
	}

	public static string DecodeClipboard(string text)
	{
		if (!text.StartsWith("VAYLI-CLIP-DPAPI1:", StringComparison.Ordinal))
		{
			return text;
		}
		return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(text.Substring("VAYLI-CLIP-DPAPI1:".Length)), null, DataProtectionScope.CurrentUser));
	}

	public static string EncodeClipboard(string text)
	{
		return "VAYLI-CLIP-DPAPI1:" + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser));
	}

	public static Bundle Capture(bool images, bool shelfFiles)
	{
		Bundle bundle = new Bundle();
		List<string> list = new List<string>(ContentFiles);
		list.AddRange(ExtraSettings);
		string path = Path.Combine(AppPaths.DataDir, "settings");
		if (Directory.Exists(path))
		{
			list.AddRange(from p in Directory.GetFiles(path, "*.json")
				select "settings\\" + Path.GetFileName(p));
		}
		list.AddRange(new string[2] { "weather\\prefs.json", "links\\links_prefs.json" });
		foreach (string item in list.Distinct().Where(IsDataFile))
		{
			string text = DataPath(item);
			if (!File.Exists(text))
			{
				continue;
			}
			CheckFile(text);
			if (new FileInfo(text).Length > 67108864)
			{
				throw new IOException("A data store is too large to back up safely.");
			}
			string text2 = PersistenceHelper.ReadShared(text);
			if (item == "clipboard\\history.json")
			{
				List<ClipboardItem> source = Json().Deserialize<List<ClipboardItem>>(DecodeClipboard(text2));
				source = source.Where((ClipboardItem c) => c != null && (images || c.ItemType != "image")).ToList();
				foreach (ClipboardItem item2 in source)
				{
					if (item2.ItemType == "image")
					{
						item2.ImagePath = AddAsset(bundle, item2.ImagePath, image: true);
						item2.ThumbPath = (string.IsNullOrEmpty(item2.ThumbPath) ? null : AddAsset(bundle, item2.ThumbPath, image: true));
					}
					else
					{
						item2.ImagePath = null;
						item2.ThumbPath = null;
					}
				}
				text2 = Json().Serialize(source);
			}
			if (item == "calendar\\calendar.json")
			{
				CalendarData calendarData = Json().Deserialize<CalendarData>(text2);
				if (calendarData.Settings != null)
				{
					calendarData.Settings.Subscriptions = new List<CalendarSubscription>();
				}
				text2 = Json().Serialize(calendarData);
			}
			if (item == "shelf\\parked_shelves.json")
			{
				ShelfStorageDto shelfStorageDto = Json().Deserialize<ShelfStorageDto>(text2);
				shelfStorageDto.Copies = new List<ShelfCopyRecovery>();
				foreach (ShelfTabDto shelf in shelfStorageDto.Shelves)
				{
					foreach (ShelfItemDto item3 in shelf.Items ?? new List<ShelfItemDto>())
					{
						if (shelfFiles)
						{
							item3.Path = AddAsset(bundle, item3.Path, image: false);
							item3.Owned = true;
						}
						else
						{
							item3.Owned = false;
						}
					}
				}
				text2 = Json().Serialize(shelfStorageDto);
			}
			ValidateData(item, text2);
			bundle.Files.Add(item, text2);
		}
		return bundle;
	}

	public static long Export(string destination, bool images, bool shelfFiles)
	{
		if (!PersistenceHelper.FlushForExit())
		{
			throw new IOException("Pending data could not be saved. Backup cancelled.");
		}
		Bundle bundle = Capture(images, shelfFiles);
		string text = destination + ".tmp-" + Guid.NewGuid().ToString("N");
		try
		{
			using (ZipArchive zipArchive = ZipFile.Open(text, ZipArchiveMode.Create))
			{
				ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry("backup.json", CompressionLevel.Optimal);
				Dictionary<string, string> assets = bundle.Assets;
				bundle.Assets = new Dictionary<string, string>();
				bundle.AssetHashes = assets.ToDictionary((KeyValuePair<string, string> a) => a.Key, (KeyValuePair<string, string> a) => Digest(Convert.FromBase64String(a.Value)));
				string text2 = Json().Serialize(bundle);
				using (StreamWriter streamWriter = new StreamWriter(zipArchiveEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
				{
					streamWriter.Write(text2);
				}
				using (StreamWriter streamWriter2 = new StreamWriter(zipArchive.CreateEntry("backup.sha256").Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
				{
					streamWriter2.Write(Digest(Encoding.UTF8.GetBytes(text2)));
				}
				foreach (KeyValuePair<string, string> item in assets)
				{
					ZipArchiveEntry zipArchiveEntry2 = zipArchive.CreateEntry(item.Key.Replace('\\', '/'), CompressionLevel.Optimal);
					using Stream stream = zipArchiveEntry2.Open();
					byte[] array = Convert.FromBase64String(item.Value);
					stream.Write(array, 0, array.Length);
				}
			}
			Read(text);
			if (File.Exists(destination))
			{
				File.Replace(text, destination, null);
			}
			else
			{
				File.Move(text, destination);
			}
			return new FileInfo(destination).Length;
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}

	public static Bundle Read(string source)
	{
		if (new FileInfo(source).Length > 268435456)
		{
			throw new InvalidDataException("This backup exceeds the 256 MB safety limit.");
		}
		using ZipArchive zipArchive = ZipFile.OpenRead(source);
		if (zipArchive.Entries.Count > 10000 || zipArchive.Entries.Sum((ZipArchiveEntry e) => e.Length) > 268435456)
		{
			throw new InvalidDataException("Backup expands beyond the safety limit.");
		}
		ZipArchiveEntry entry = zipArchive.GetEntry("backup.json");
		if (entry == null || entry.Length > 67108864)
		{
			throw new InvalidDataException("Missing or oversized backup manifest.");
		}
		Bundle bundle;
		using (StreamReader streamReader = new StreamReader(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)))
		{
			string text = streamReader.ReadToEnd();
			ZipArchiveEntry entry2 = zipArchive.GetEntry("backup.sha256");
			if (entry2 == null || entry2.Length != 64)
			{
				throw new InvalidDataException("Missing backup integrity checksum.");
			}
			using (StreamReader streamReader2 = new StreamReader(entry2.Open()))
			{
				if (streamReader2.ReadToEnd() != Digest(Encoding.UTF8.GetBytes(text)))
				{
					throw new InvalidDataException("The backup manifest is damaged (checksum mismatch).");
				}
			}
			if (!(Json().DeserializeObject(text) is Dictionary<string, object> @object) || !new string[5] { "App", "Kind", "Version", "CreatedUtc", "Files" }.All(@object.ContainsKey))
			{
				throw new InvalidDataException("Incomplete backup manifest.");
			}
			bundle = Json().Deserialize<Bundle>(text);
		}
		if (bundle == null || bundle.App != "Ayn" || bundle.Kind != "backup" || bundle.Version != 1 || !DateTime.TryParse(bundle.CreatedUtc, out var _) || bundle.Files == null || bundle.Files.Count == 0 || bundle.Files.Count > 50 || bundle.Assets == null || bundle.Assets.Count != 0 || bundle.AssetHashes == null)
		{
			throw new InvalidDataException("Not a supported Ayn backup (version 1 required).");
		}
		foreach (KeyValuePair<string, string> file in bundle.Files)
		{
			ValidateData(file.Key, file.Value);
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ZipArchiveEntry entry3 in zipArchive.Entries)
		{
			if (!hashSet.Add(entry3.FullName))
			{
				throw new InvalidDataException("Duplicate archive entry.");
			}
			if (entry3 == entry || entry3.FullName == "backup.sha256")
			{
				continue;
			}
			string text2 = entry3.FullName.Replace('/', '\\');
			if (!IsAsset(text2) || entry3.Length > 134217728)
			{
				throw new InvalidDataException("Unexpected backup entry.");
			}
			using Stream stream = entry3.Open();
			using MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[81920];
			int num;
			while ((num = stream.Read(array, 0, array.Length)) > 0)
			{
				if (memoryStream.Length + num > entry3.Length)
				{
					throw new InvalidDataException("Oversized backup asset.");
				}
				memoryStream.Write(array, 0, num);
			}
			byte[] array2 = memoryStream.ToArray();
			if (memoryStream.Length != entry3.Length || !bundle.AssetHashes.TryGetValue(text2, out var value) || Digest(array2) != value)
			{
				throw new InvalidDataException("A backup file is damaged (checksum mismatch).");
			}
			bundle.Assets.Add(text2, Convert.ToBase64String(array2));
		}
		if (bundle.Assets.Count != bundle.AssetHashes.Count)
		{
			throw new InvalidDataException("Backup file copies are missing.");
		}
		ValidateReferences(bundle);
		return bundle;
	}

	private static void ValidateReferences(Bundle b)
	{
		if (b.Files.TryGetValue("clipboard\\history.json", out var value))
		{
			foreach (ClipboardItem item in Json().Deserialize<List<ClipboardItem>>(value))
			{
				string[] array = new string[2] { item.ImagePath, item.ThumbPath };
				foreach (string text in array)
				{
					if (text != null && (!IsAsset(text) || !text.StartsWith("clipboard\\") || !b.Assets.ContainsKey(text)))
					{
						throw new InvalidDataException("Missing clipboard image.");
					}
				}
			}
		}
		if (!b.Files.TryGetValue("shelf\\parked_shelves.json", out value))
		{
			return;
		}
		foreach (ShelfTabDto shelf in Json().Deserialize<ShelfStorageDto>(value).Shelves)
		{
			foreach (ShelfItemDto item2 in shelf.Items ?? new List<ShelfItemDto>())
			{
				if (item2.Owned && (!IsAsset(item2.Path) || !item2.Path.StartsWith("shelf\\") || !b.Assets.ContainsKey(item2.Path)))
				{
					throw new InvalidDataException("Missing shelf copy.");
				}
			}
		}
	}

	public static void ValidateData(string path, string text, bool storedData = false)
	{
		if (!IsDataFile(path) || text == null || (long)text.Length > 67108864L)
		{
			throw new InvalidDataException("Unsupported data store.");
		}
		object obj = Json().DeserializeObject(text);
		if (obj == null)
		{
			throw new InvalidDataException("Empty data store.");
		}
		switch (path)
		{
		case "notes\\notes.json":
		case "clipboard\\history.json":
		case "links\\bookmarks.json":
		{
			if (!(obj is object[] array) || array.Any((object v) => !(v is Dictionary<string, object>)))
			{
				throw new InvalidDataException("Invalid data list.");
			}
			HashSet<string> hashSet = new HashSet<string>();
			object[] array2 = array;
			for (int j = 0; j < array2.Length; j++)
			{
				Dictionary<string, object> dictionary = (Dictionary<string, object>)array2[j];
				if (!dictionary.TryGetValue("Id", out var value) || !(value is string) || string.IsNullOrWhiteSpace((string)value) || !hashSet.Add((string)value))
				{
					throw new InvalidDataException("Missing or duplicate item ID.");
				}
			}
			break;
		}
		case "calendar\\calendar.json":
		{
			CalendarData calendarData = Json().Deserialize<CalendarData>(text);
			if (calendarData.Version > 3 || calendarData.Events == null || calendarData.Reminders == null || calendarData.Events.Any((UserEvent e) => e == null) || calendarData.Reminders.Any((UserReminder e) => e == null) || (!storedData && calendarData.Settings != null && calendarData.Settings.Subscriptions != null && calendarData.Settings.Subscriptions.Count > 0))
			{
				throw new InvalidDataException("Invalid calendar or secret subscription data.");
			}
			break;
		}
		case "shelf\\parked_shelves.json":
		{
			ShelfStorageDto shelfStorageDto = Json().Deserialize<ShelfStorageDto>(text);
			if (shelfStorageDto.Version > 2 || shelfStorageDto.Shelves == null || shelfStorageDto.Shelves.Count == 0 || shelfStorageDto.Shelves.Any((ShelfTabDto t) => t == null || (t.Items == null && t.Paths == null) || (t.Items != null && t.Items.Any((ShelfItemDto i) => i == null || string.IsNullOrWhiteSpace(i.Id) || string.IsNullOrWhiteSpace(i.Path)))) || (shelfStorageDto.Copies != null && shelfStorageDto.Copies.Count > 0))
			{
				throw new InvalidDataException("Invalid shelf data.");
			}
			break;
		}
		case "timer\\timers_state.json":
			if (!(obj is Dictionary<string, object>))
			{
				throw new InvalidDataException("Invalid timer state.");
			}
			Json().Deserialize<TimerServiceState>(text);
			break;
		default:
			if (ExtraSettings.Contains(path))
			{
				if (!(obj is Dictionary<string, object>))
				{
					throw new InvalidDataException("Invalid tool settings.");
				}
				if (path == "message\\settings.json")
				{
					Json().Deserialize<MessageSettings>(text);
				}
				if (path == "sounds\\mixer_state.json")
				{
					Json().Deserialize<SoundMixerStateModel>(text);
				}
				if (path == "converter_state.json")
				{
					Json().Deserialize<ConvertSettings>(text);
				}
			}
			else
			{
				SettingsTransaction.ValidateFile(path, text);
			}
			break;
		}
		if (path == "notes\\notes.json")
		{
			JavaScriptSerializer javaScriptSerializer = Json();
			javaScriptSerializer.RegisterConverters(new JavaScriptConverter[1]
			{
				new NoteDocumentConverter()
			});
			javaScriptSerializer.Deserialize<List<NoteItem>>(text);
		}
		if (path == "clipboard\\history.json")
		{
			Json().Deserialize<List<ClipboardItem>>(text);
		}
		if (path == "links\\bookmarks.json")
		{
			Json().Deserialize<List<LinksBookmark>>(text);
		}
	}

	public static string Preview(Bundle b)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> file in b.Files)
		{
			object obj = Json().DeserializeObject(file.Value);
			if (obj is object[] array)
			{
				list.Add(file.Key.Split('\\')[0] + ": " + array.Length);
			}
			else if (file.Key == "calendar\\calendar.json")
			{
				CalendarData calendarData = Json().Deserialize<CalendarData>(file.Value);
				list.Add("events: " + calendarData.Events.Count + ", reminders: " + calendarData.Reminders.Count);
			}
			else if (file.Key == "shelf\\parked_shelves.json")
			{
				ShelfStorageDto shelfStorageDto = Json().Deserialize<ShelfStorageDto>(file.Value);
				list.Add("shelf files: " + shelfStorageDto.Shelves.Sum((ShelfTabDto t) => (t.Items != null) ? t.Items.Count : t.Paths.Count));
			}
		}
		list.Add("settings/state files: " + b.Files.Count((KeyValuePair<string, string> f) => !ContentFiles.Contains(f.Key)));
		if (b.Files.ContainsKey("timer\\timers_state.json"))
		{
			list.Add("timers and water: 1 state/history store");
		}
		list.Add("file/image copies: " + b.Assets.Count);
		return string.Join("\n", list);
	}

	private static object MergeValue(object local, object incoming)
	{
		object[] array = local as object[];
		object[] array2 = incoming as object[];
		if (array != null && array2 != null)
		{
			List<object> list = array.ToList();
			object[] array3 = array2;
			foreach (object v in array3)
			{
				Predicate<object> match = (object old) => Identity(old) == Identity(v);
				int num = list.FindIndex(match);
				if (num < 0)
				{
					list.Add(v);
				}
				else if (v is Dictionary<string, object> && ((Dictionary<string, object>)v).ContainsKey("Items"))
				{
					list[num] = MergeValue(list[num], v);
				}
			}
			return list.ToArray();
		}
		Dictionary<string, object> dictionary = local as Dictionary<string, object>;
		Dictionary<string, object> dictionary2 = incoming as Dictionary<string, object>;
		if (dictionary != null && dictionary2 != null)
		{
			foreach (KeyValuePair<string, object> item in dictionary2)
			{
				dictionary[item.Key] = (dictionary.TryGetValue(item.Key, out var value) ? MergeValue(value, item.Value) : item.Value);
			}
			return dictionary;
		}
		return local;
	}

	private static string Identity(object value)
	{
		if (!(value is Dictionary<string, object> dictionary) || !dictionary.TryGetValue("Id", out var value2))
		{
			return Json().Serialize(value);
		}
		return "id:" + value2;
	}

	public static void QueueRestore(Bundle b, bool replace)
	{
		ValidateReferences(b);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		Dictionary<string, string> assetPaths = b.Assets.Keys.ToDictionary((string p) => p, (string p) => (!p.StartsWith("clipboard\\")) ? ("shelf\\stored\\" + Guid.NewGuid().ToString("N") + "\\" + Path.GetFileName(p)) : ("clipboard\\images\\" + Guid.NewGuid().ToString("N") + ".png"));
		Dictionary<string, string> assets = b.Assets.ToDictionary((KeyValuePair<string, string> p) => assetPaths[p.Key], (KeyValuePair<string, string> p) => p.Value);
		foreach (KeyValuePair<string, string> file in b.Files)
		{
			ValidateData(file.Key, file.Value);
			string text = file.Value;
			if (file.Key == "clipboard\\history.json")
			{
				List<ClipboardItem> list = Json().Deserialize<List<ClipboardItem>>(text);
				foreach (ClipboardItem item in list)
				{
					if (item.ImagePath != null)
					{
						item.ImagePath = Path.Combine(AppPaths.DataDir, assetPaths[item.ImagePath]);
					}
					if (item.ThumbPath != null)
					{
						item.ThumbPath = Path.Combine(AppPaths.DataDir, assetPaths[item.ThumbPath]);
					}
				}
				text = Json().Serialize(list);
			}
			if (file.Key == "shelf\\parked_shelves.json")
			{
				ShelfStorageDto shelfStorageDto = Json().Deserialize<ShelfStorageDto>(text);
				foreach (ShelfTabDto shelf in shelfStorageDto.Shelves)
				{
					foreach (ShelfItemDto item2 in shelf.Items ?? new List<ShelfItemDto>())
					{
						if (item2.Owned)
						{
							item2.Path = Path.Combine(AppPaths.DataDir, assetPaths[item2.Path]);
						}
					}
				}
				text = Json().Serialize(shelfStorageDto);
			}
			string path = DataPath(file.Key);
			if (file.Key == "calendar\\calendar.json" && File.Exists(path))
			{
				CalendarData calendarData = Json().Deserialize<CalendarData>(text);
				CalendarData calendarData2 = Json().Deserialize<CalendarData>(PersistenceHelper.ReadShared(path));
				if (calendarData.Settings == null)
				{
					calendarData.Settings = new CalendarSettings();
				}
				calendarData.Settings.Subscriptions = ((calendarData2.Settings == null) ? new List<CalendarSubscription>() : calendarData2.Settings.Subscriptions);
				text = Json().Serialize(calendarData);
			}
			if (!replace && File.Exists(path))
			{
				string text2 = PersistenceHelper.ReadShared(path);
				if (file.Key == "clipboard\\history.json")
				{
					text2 = DecodeClipboard(text2);
				}
				text = Json().Serialize(MergeValue(Json().DeserializeObject(text2), Json().DeserializeObject(text)));
			}
			ValidateData(file.Key, text, storedData: true);
			if (file.Key == "clipboard\\history.json")
			{
				text = EncodeClipboard(text);
			}
			dictionary.Add(file.Key, text);
		}
		SettingsTransaction.QueueBackupRestore(dictionary, assets);
	}
}
