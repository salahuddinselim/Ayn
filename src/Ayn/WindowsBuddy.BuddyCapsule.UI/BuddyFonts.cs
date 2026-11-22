using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyFonts
{
	internal const string Version = "v15";

	private const string FallbackChain = "Segoe UI Variable Text, Segoe UI, Segoe UI Emoji, Segoe UI Symbol";

	private static readonly string[] Files = new string[3] { "Geist-Regular.ttf", "Geist-Medium.ttf", "Geist-SemiBold.ttf" };

	private static FontFamily _geist;

	private static bool _extracted;

	private static readonly List<FileStream> _pins = new List<FileStream>();

	public static FontFamily Geist
	{
		get
		{
			if (_geist == null)
			{
				_geist = Load("Geist", out var resolved);
				FontWeight[] array = new FontWeight[3]
				{
					FontWeights.Normal,
					FontWeights.Medium,
					FontWeights.SemiBold
				};
				foreach (FontWeight fontWeight in array)
				{
					resolved &= new Typeface(_geist, FontStyles.Normal, fontWeight, FontStretches.Normal).TryGetGlyphTypeface(out var glyphTypeface) && FontDirectory != null && glyphTypeface.FontUri.LocalPath.StartsWith(FontDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && glyphTypeface.Weight == fontWeight;
				}
				IsGeistLoaded = resolved;
				if (!resolved)
				{
					TransactionLogger.Warn("[Fonts] One or more Geist faces did not resolve to their embedded font files.");
				}
			}
			return _geist;
		}
	}

	public static bool IsGeistLoaded { get; private set; }

	public static string FontDirectory { get; private set; }

	private static void Extract()
	{
		if (_extracted)
		{
			return;
		}
		string text = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "fonts");
		string text2 = Path.Combine(text, "Inter-v15");
		if (!TryExtractAndPin(text2))
		{
			text2 = Path.Combine(text, "Inter-v15-" + DateTime.UtcNow.Ticks.ToString("x"));
			if (!TryExtractAndPin(text2))
			{
				throw new IOException("Font files could not be prepared");
			}
			TryRemoveStaleFolders(text, text2);
		}
		FontDirectory = text2;
		_extracted = true;
	}

	private static bool TryExtractAndPin(string dir)
	{
		List<FileStream> list = new List<FileStream>();
		try
		{
			Directory.CreateDirectory(dir);
			Assembly assembly = typeof(BuddyFonts).Assembly;
			string[] files = Files;
			foreach (string text in files)
			{
				using Stream stream = assembly.GetManifestResourceStream("WindowsBuddy.Fonts." + text);
				if (stream == null)
				{
					throw new FileNotFoundException("Embedded font missing", text);
				}
				string text2 = Path.Combine(dir, text);
				if (!File.Exists(text2) || new FileInfo(text2).Length != stream.Length)
				{
					using FileStream destination = new FileStream(text2, FileMode.Create, FileAccess.Write, FileShare.Read);
					stream.CopyTo(destination);
				}
				list.Add(new FileStream(text2, FileMode.Open, FileAccess.Read, FileShare.Read));
			}
			lock (_pins)
			{
				_pins.AddRange(list);
			}
			return true;
		}
		catch (Exception ex)
		{
			foreach (FileStream item in list)
			{
				try
				{
					item.Dispose();
				}
				catch
				{
				}
			}
			TransactionLogger.Warn("[Fonts] Font folder not usable (" + ex.GetType().Name + "); trying a fresh one.");
			return false;
		}
	}

	private static void TryRemoveStaleFolders(string root, string keep)
	{
		try
		{
			string[] directories = Directory.GetDirectories(root, "Inter-v15-*");
			foreach (string text in directories)
			{
				if (!string.Equals(text, keep, StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						Directory.Delete(text, recursive: true);
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
	}

	private static FontFamily Load(string familyName, out bool resolved)
	{
		resolved = false;
		try
		{
			Extract();
			string fontDirectory = FontDirectory;
			FontFamily fontFamily = new FontFamily(new Uri(fontDirectory + Path.DirectorySeparatorChar), "./#" + familyName + ", Segoe UI Variable Text, Segoe UI, Segoe UI Emoji, Segoe UI Symbol");
			Typeface typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
			resolved = typeface.TryGetGlyphTypeface(out var glyphTypeface) && glyphTypeface.FontUri.LocalPath.StartsWith(fontDirectory, StringComparison.OrdinalIgnoreCase);
			if (!resolved)
			{
				TransactionLogger.Warn("[Fonts] " + familyName + " did not resolve; using Segoe UI fallback.");
			}
			return fontFamily;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[Fonts] Could not load " + familyName + ": " + ex.Message);
			return new FontFamily("Segoe UI Variable Text, Segoe UI, Segoe UI Emoji, Segoe UI Symbol");
		}
	}
}
