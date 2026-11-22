using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace WindowsBuddy.Services;

public static class LegacyFolderCleanup
{
	public static readonly TimeSpan Grace = TimeSpan.FromDays(3.0);

	public static string LegacyLocal => Path.Combine(AppPaths.LocalAppData, "WindowsBuddy");

	private static string LegacyRoaming => Path.Combine(AppPaths.RoamingAppData, "WindowsBuddy");

	private static string Archive => Path.Combine(AppPaths.DataDir, "backups", "legacy-windowsbuddy");

	public static string Run()
	{
		try
		{
			if (!Directory.Exists(LegacyLocal) && !Directory.Exists(LegacyRoaming))
			{
				return "";
			}
			if (!string.Equals(AppPaths.FolderName, "Ayn", StringComparison.Ordinal))
			{
				return "Legacy folder: still in use this session.";
			}
			string path = Path.Combine(AppPaths.DataDir, ".migrated-from-windowsbuddy");
			if (!File.Exists(path))
			{
				return "Legacy folder: not migrated yet.";
			}
			if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < Grace)
			{
				return "Legacy folder: kept until 3 days after migration.";
			}
			if (OldAppRunning())
			{
				return "Legacy folder: the old app is running; retrying later.";
			}
			return "Legacy folder retained for data safety; automatic retirement disabled.";
		}
		catch (Exception ex)
		{
			return "Legacy folder cleanup skipped: " + ex.Message;
		}
	}

	private static bool OldAppRunning()
	{
		if (Mutex.TryOpenExisting("Global\\WindowsBuddy_SingleInstance", out var result))
		{
			result.Dispose();
			return true;
		}
		return false;
	}

	private static bool UninstallerRegistered()
	{
		try
		{
			RegistryKey[] array = new RegistryKey[2]
			{
				Registry.CurrentUser,
				Registry.LocalMachine
			};
			foreach (RegistryKey registryKey in array)
			{
				using RegistryKey registryKey2 = registryKey.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\WindowsBuddy");
				string text = ((registryKey2 != null) ? (registryKey2.GetValue("UninstallString") as string) : null);
				if (text != null && text.IndexOf(LegacyLocal, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	internal static int PreserveUserContent(string legacy, string current)
	{
		int num = 0;
		if (!Directory.Exists(legacy))
		{
			return 0;
		}
		string path = Path.Combine(legacy, "notes");
		if (Directory.Exists(path))
		{
			string[] files = Directory.GetFiles(path, "*.json*");
			foreach (string text in files)
			{
				if (!text.Contains(".tmp"))
				{
					string text2 = Path.Combine(current, "notes", Path.GetFileName(text));
					if (!File.Exists(text2) || !SameBytes(text, text2))
					{
						num += CopyToArchive(text, Path.Combine("notes", Path.GetFileName(text)));
					}
				}
			}
		}
		string path2 = Path.Combine(legacy, "clipboard", "history.json");
		if (File.Exists(path2))
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			javaScriptSerializer.MaxJsonLength = int.MaxValue;
			JavaScriptSerializer javaScriptSerializer2 = javaScriptSerializer;
			List<Dictionary<string, object>> source = ReadItems(javaScriptSerializer2, path2);
			HashSet<string> have = new HashSet<string>(from i in ReadItems(javaScriptSerializer2, Path.Combine(current, "clipboard", "history.json"))
				select Str(i, "Id") into s
				where s != null
				select s);
			List<Dictionary<string, object>> list = source.Where((Dictionary<string, object> i) => i.ContainsKey("IsPinned") && i["IsPinned"] is bool && (bool)i["IsPinned"] && !have.Contains(Str(i, "Id") ?? "")).ToList();
			if (list.Count > 0)
			{
				Directory.CreateDirectory(Path.Combine(Archive, "clipboard"));
				PersistenceHelper.WriteAllTextOrThrow(Path.Combine(Archive, "clipboard", "pinned-items.json"), javaScriptSerializer2.Serialize(list));
				num++;
				foreach (Dictionary<string, object> item in list)
				{
					string[] array = new string[2] { "ImagePath", "ThumbPath" };
					foreach (string k2 in array)
					{
						string text3 = Str(item, k2);
						if (!string.IsNullOrEmpty(text3))
						{
							string text4 = (File.Exists(text3) ? text3 : Path.Combine(legacy, "clipboard", "images", Path.GetFileName(text3)));
							if (File.Exists(text4))
							{
								num += CopyToArchive(text4, Path.Combine("clipboard", "images", Path.GetFileName(text4)));
							}
						}
					}
				}
			}
		}
		return num;
	}

	private static List<Dictionary<string, object>> ReadItems(JavaScriptSerializer ser, string path)
	{
		List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
		try
		{
			if (!File.Exists(path))
			{
				return list;
			}
			string text = PersistenceHelper.ReadShared(path);
			if (text.StartsWith("VAYLI-CLIP-DPAPI1:", StringComparison.Ordinal))
			{
				text = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(text.Substring("VAYLI-CLIP-DPAPI1:".Length)), null, DataProtectionScope.CurrentUser));
			}
			object obj = ser.DeserializeObject(text);
			object[] array = obj as object[];
			Dictionary<string, object> dictionary = obj as Dictionary<string, object>;
			if (array == null && dictionary != null)
			{
				foreach (object value in dictionary.Values)
				{
					if (value is object[])
					{
						array = (object[])value;
						break;
					}
				}
			}
			if (array == null)
			{
				throw new InvalidDataException("Expected a legacy history array.");
			}
			if (array != null)
			{
				object[] array2 = array;
				foreach (object obj2 in array2)
				{
					if (obj2 is Dictionary<string, object> item)
					{
						list.Add(item);
					}
				}
			}
		}
		catch (Exception innerException)
		{
			throw new InvalidDataException("Legacy history unreadable; retain the source tree.", innerException);
		}
		return list;
	}

	private static string Str(Dictionary<string, object> d, string k)
	{
		if (!d.TryGetValue(k, out var value) || value == null)
		{
			return null;
		}
		return value.ToString();
	}

	private static int CopyToArchive(string src, string rel)
	{
		string text = Path.Combine(Archive, rel);
		Directory.CreateDirectory(Path.GetDirectoryName(text));
		if (File.Exists(text) && SameBytes(src, text))
		{
			return 0;
		}
		File.Copy(src, text, overwrite: true);
		return 1;
	}

	private static bool SameBytes(string a, string b)
	{
		FileInfo fileInfo = new FileInfo(a);
		FileInfo fileInfo2 = new FileInfo(b);
		if (fileInfo.Length != fileInfo2.Length)
		{
			return false;
		}
		return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
	}

	private static void Retire(string root, bool keepUninstaller, ref int deleted, ref int kept, ref long freed)
	{
		if (!Directory.Exists(root))
		{
			return;
		}
		foreach (string item in StorageJanitor.SafeFiles(root, "*"))
		{
			string text = item.Substring(root.Length).TrimStart('\\');
			if (keepUninstaller && (text.Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase) || text.StartsWith("bin\\", StringComparison.OrdinalIgnoreCase)))
			{
				kept++;
				continue;
			}
			long num = 0L;
			try
			{
				num = new FileInfo(item).Length;
			}
			catch
			{
			}
			if (StorageJanitor.TryDelete(item))
			{
				deleted++;
				freed += num;
			}
			else
			{
				kept++;
			}
		}
		try
		{
			foreach (string item2 in from d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
				orderby d.Length descending
				select d)
			{
				try
				{
					if (!Directory.EnumerateFileSystemEntries(item2).Any())
					{
						Directory.Delete(item2);
					}
				}
				catch
				{
				}
			}
			if (!Directory.EnumerateFileSystemEntries(root).Any())
			{
				Directory.Delete(root);
			}
		}
		catch
		{
		}
	}
}
