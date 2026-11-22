using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.Services;

namespace WindowsBuddy.Core;

public static class CursorPackManager
{
	private static readonly Dictionary<string, CursorPack> CachedPacks = new Dictionary<string, CursorPack>(StringComparer.OrdinalIgnoreCase);

	public static readonly string[] RequiredRegistryKeys = new string[15]
	{
		"Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
		"SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand"
	};

	public static void RescanPacks()
	{
		CachedPacks.Clear();
		List<string> list = new List<string>();
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		list.Add(Path.Combine(baseDirectory, "assets", "cursors"));
		list.Add(Path.Combine(Directory.GetCurrentDirectory(), "assets", "cursors"));
		list.Add(Path.Combine(baseDirectory, "themes"));
		list.Add(Path.Combine(Directory.GetCurrentDirectory(), "themes"));
		string localAppData = AppPaths.LocalAppData;
		list.Add(Path.Combine(localAppData, AppPaths.FolderName, "assets", "cursors"));
		list.Add(Path.Combine(localAppData, AppPaths.FolderName, "themes"));
		foreach (string item in list)
		{
			if (!Directory.Exists(item))
			{
				continue;
			}
			string[] directories = Directory.GetDirectories(item);
			foreach (string text in directories)
			{
				string path = Path.Combine(text, "manifest.json");
				if (!File.Exists(path))
				{
					continue;
				}
				try
				{
					CursorPack cursorPack = LoadPackFromDirectory(text);
					if (cursorPack != null && !CachedPacks.ContainsKey(cursorPack.Id))
					{
						CachedPacks[cursorPack.Id] = cursorPack;
					}
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn($"Failed to load cursor pack from '{text}': {ex.Message}");
				}
			}
		}
	}

	public static CursorPack LoadPackFromDirectory(string directory)
	{
		string path = Path.Combine(directory, "manifest.json");
		if (!File.Exists(path))
		{
			throw new FileNotFoundException($"manifest.json not found in cursor pack directory: {directory}");
		}
		string input = File.ReadAllText(path);
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		CursorPackManifest cursorPackManifest = javaScriptSerializer.Deserialize<CursorPackManifest>(input);
		if (cursorPackManifest == null || string.IsNullOrEmpty(cursorPackManifest.Id))
		{
			throw new InvalidDataException("Invalid cursor pack manifest: missing id");
		}
		CursorPack cursorPack = new CursorPack();
		cursorPack.Manifest = cursorPackManifest;
		cursorPack.DirectoryPath = Path.GetFullPath(directory);
		return cursorPack;
	}

	public static CursorPack GetPack(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		if (CachedPacks.Count == 0)
		{
			RescanPacks();
		}
		if (CachedPacks.TryGetValue(id, out var value))
		{
			return value;
		}
		string text = Path.Combine("assets", "cursors", id);
		if (Directory.Exists(text) && File.Exists(Path.Combine(text, "manifest.json")))
		{
			value = LoadPackFromDirectory(text);
			CachedPacks[value.Id] = value;
			return value;
		}
		return null;
	}

	public static IEnumerable<CursorPack> GetAllPacks()
	{
		if (CachedPacks.Count == 0)
		{
			RescanPacks();
		}
		return CachedPacks.Values;
	}

	public static bool ValidatePackFiles(CursorPack pack, out List<string> missingFiles)
	{
		missingFiles = new List<string>();
		if (pack == null || pack.Manifest == null)
		{
			missingFiles.Add("Null pack or manifest");
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, CursorStateMetadata> state in pack.Manifest.States)
		{
			CursorStateMetadata value = state.Value;
			hashSet.Add(value.RegistryKey);
			string text = Path.Combine(pack.DirectoryPath, value.File);
			if (!File.Exists(text))
			{
				missingFiles.Add($"State '{state.Key}': file missing '{value.File}'");
				continue;
			}
			FileInfo fileInfo = new FileInfo(text);
			if (fileInfo.Length < 64)
			{
				missingFiles.Add($"State '{state.Key}': file too small ({fileInfo.Length} bytes)");
			}
		}
		string[] requiredRegistryKeys = RequiredRegistryKeys;
		foreach (string text2 in requiredRegistryKeys)
		{
			if (!hashSet.Contains(text2))
			{
				missingFiles.Add($"Missing required Windows cursor state: '{text2}'");
			}
		}
		return missingFiles.Count == 0;
	}
}
