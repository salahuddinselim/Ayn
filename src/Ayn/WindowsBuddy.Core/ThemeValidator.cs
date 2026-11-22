using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.Services;

namespace WindowsBuddy.Core;

public static class ThemeValidator
{
	private static readonly string[] RequiredCursorKeys = new string[15]
	{
		"Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
		"SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand"
	};

	public static ThemePackage LoadAndValidate(string themeDirectory)
	{
		if (!Directory.Exists(themeDirectory))
		{
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, themeDirectory);
			if (Directory.Exists(text))
			{
				themeDirectory = text;
			}
			else
			{
				string text2 = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, themeDirectory);
				if (!Directory.Exists(text2))
				{
					throw new DirectoryNotFoundException($"Theme directory not found: {themeDirectory}");
				}
				themeDirectory = text2;
			}
		}
		string path = Path.Combine(themeDirectory, "theme.json");
		if (!File.Exists(path))
		{
			throw new FileNotFoundException($"theme.json not found in theme directory: {themeDirectory}");
		}
		string input = File.ReadAllText(path);
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		ThemePackage themePackage;
		try
		{
			themePackage = javaScriptSerializer.Deserialize<ThemePackage>(input);
		}
		catch (Exception ex)
		{
			throw new InvalidDataException($"Failed to parse theme.json: {ex.Message}", ex);
		}
		if (themePackage == null)
		{
			throw new InvalidDataException("theme.json deserialized to null object.");
		}
		themePackage.RootDirectory = Path.GetFullPath(themeDirectory);
		if (string.IsNullOrEmpty(themePackage.id))
		{
			throw new InvalidDataException("Theme missing required field: 'id'");
		}
		if (string.IsNullOrEmpty(themePackage.name))
		{
			throw new InvalidDataException("Theme missing required field: 'name'");
		}
		if (themePackage.wallpaper == null || string.IsNullOrEmpty(themePackage.wallpaper.file))
		{
			throw new InvalidDataException("Theme missing required wallpaper configuration.");
		}
		string text3 = Path.Combine(themePackage.RootDirectory, themePackage.wallpaper.file);
		if (!File.Exists(text3))
		{
			throw new FileNotFoundException($"Configured wallpaper file does not exist: {text3}");
		}
		themePackage.ResolvedWallpaperPath = text3;
		if (themePackage.cursors == null || themePackage.cursors.Count == 0)
		{
			throw new InvalidDataException("Theme missing required cursor family definitions.");
		}
		themePackage.ResolvedCursorPaths = new Dictionary<string, string>();
		string[] requiredCursorKeys = RequiredCursorKeys;
		foreach (string text4 in requiredCursorKeys)
		{
			if (!themePackage.cursors.ContainsKey(text4))
			{
				throw new InvalidDataException($"Theme cursors missing required state: '{text4}'");
			}
			string path2 = themePackage.cursors[text4];
			string text5 = Path.Combine(themePackage.RootDirectory, path2);
			if (!File.Exists(text5))
			{
				throw new FileNotFoundException($"Cursor asset for state '{text4}' does not exist: {text5}");
			}
			themePackage.ResolvedCursorPaths[text4] = text5;
		}
		if (themePackage.colors == null || string.IsNullOrEmpty(themePackage.colors.background) || string.IsNullOrEmpty(themePackage.colors.textPrimary))
		{
			throw new InvalidDataException("Theme missing core color tokens (background, textPrimary).");
		}
		TransactionLogger.Info($"Theme '{themePackage.name}' (v{themePackage.version}) validated successfully. 15 cursor states verified.");
		return themePackage;
	}
}
