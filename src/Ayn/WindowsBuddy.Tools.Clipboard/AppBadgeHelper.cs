using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WindowsBuddy.Tools.Clipboard;

public static class AppBadgeHelper
{
	private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConcurrentDictionary<string, bool> _pending = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

	public static ImageSource GetAppIcon(ClipboardItem item, Action<ImageSource> onLoaded)
	{
		if (item == null)
		{
			return null;
		}
		string key = CacheKey(item);
		if (key == null)
		{
			return null;
		}
		if (_iconCache.TryGetValue(key, out var value))
		{
			return value;
		}
		if (!_pending.TryAdd(key, value: true))
		{
			return null;
		}
		string path = item.SourceAppPath;
		string name = ((!string.IsNullOrEmpty(item.SourceAppExe)) ? item.SourceAppExe : item.SourceApp);
		Dispatcher ui = ((Application.Current != null) ? Application.Current.Dispatcher : null);
		ThreadPool.QueueUserWorkItem(delegate
		{
			ImageSource icon = null;
			try
			{
				string exePath = ((!string.IsNullOrEmpty(path) && File.Exists(path)) ? path : FindAppPath(name));
				icon = Extract(exePath);
			}
			catch
			{
			}
			_iconCache[key] = icon;
			_pending.TryRemove(key, out var _);
			if (icon != null && onLoaded != null && ui != null)
			{
				ui.BeginInvoke((Action)delegate
				{
					try
					{
						onLoaded(icon);
					}
					catch
					{
					}
				});
			}
		});
		return null;
	}

	public static ImageSource GetAppIcon(string appName, string exeName)
	{
		string text = ((!string.IsNullOrEmpty(exeName)) ? exeName : appName);
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		if (_iconCache.TryGetValue(text, out var value))
		{
			return value;
		}
		ImageSource imageSource = null;
		try
		{
			imageSource = Extract(FindAppPath(text));
		}
		catch
		{
		}
		_iconCache[text] = imageSource;
		return imageSource;
	}

	public static void SetAppIcon(string key, ImageSource icon)
	{
		if (!string.IsNullOrEmpty(key) && icon != null)
		{
			_iconCache[key] = icon;
		}
	}

	private static string CacheKey(ClipboardItem item)
	{
		if (!string.IsNullOrEmpty(item.SourceAppPath))
		{
			return item.SourceAppPath;
		}
		if (!string.IsNullOrEmpty(item.SourceAppExe))
		{
			return item.SourceAppExe;
		}
		if (!string.IsNullOrEmpty(item.SourceApp) && !string.Equals(item.SourceApp, "System", StringComparison.OrdinalIgnoreCase))
		{
			return item.SourceApp;
		}
		return null;
	}

	private static ImageSource Extract(string exePath)
	{
		if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
		{
			return null;
		}
		using Icon icon = Icon.ExtractAssociatedIcon(exePath);
		if (icon == null)
		{
			return null;
		}
		BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		bitmapSource.Freeze();
		return bitmapSource;
	}

	private static string FindAppPath(string appKey)
	{
		if (string.IsNullOrEmpty(appKey))
		{
			return null;
		}
		string text = appKey.Replace(" ", "").ToLowerInvariant();
		if (text == "googlechrome")
		{
			text = "chrome";
		}
		if (text == "visualstudiocode")
		{
			text = "code";
		}
		if (text == "microsoftedge")
		{
			text = "msedge";
		}
		if (text == "word")
		{
			text = "winword";
		}
		if (text == "powerpoint")
		{
			text = "powerpnt";
		}
		string text2 = (text.EndsWith(".exe") ? text : (text + ".exe"));
		string name = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\" + text2;
		using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(name) ?? Registry.CurrentUser.OpenSubKey(name))
		{
			if (registryKey != null)
			{
				object value = registryKey.GetValue(null);
				if (value != null)
				{
					string text3 = value.ToString().Trim('"');
					if (File.Exists(text3))
					{
						return text3;
					}
				}
			}
		}
		switch (text)
		{
		case "notepad":
			return Path.Combine(Environment.SystemDirectory, "notepad.exe");
		case "explorer":
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
		case "code":
		{
			string text4 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
			if (File.Exists(text4))
			{
				return text4;
			}
			break;
		}
		}
		return null;
	}
}
