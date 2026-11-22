using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class WallpaperService
{
	private const int SPI_SETDESKWALLPAPER = 20;

	private const int SPI_GETDESKWALLPAPER = 115;

	private const int SPIF_UPDATEINIFILE = 1;

	private const int SPIF_SENDCHANGE = 2;

	private static Color? _cachedDominantColor;

	private static string _cachedWallpaperPath;

	private static DateTime _lastWallpaperCheck = DateTime.MinValue;

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int SystemParametersInfo(int uAction, int uParam, StringBuilder lpvParam, int fuWinIni);

	public static string GetCurrentWallpaper()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			SystemParametersInfo(115, stringBuilder.Capacity, stringBuilder, 0);
			string text = stringBuilder.ToString();
			if (!string.IsNullOrEmpty(text) && File.Exists(text))
			{
				return text;
			}
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("Wallpaper");
				if (value != null && File.Exists(value.ToString()))
				{
					return value.ToString();
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn($"Could not read current wallpaper: {ex.Message}");
		}
		return string.Empty;
	}

	public static string GetCurrentWallpaperStyle()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("WallpaperStyle");
				if (value != null)
				{
					return value.ToString();
				}
			}
		}
		catch
		{
		}
		return "10";
	}

	public static string GetCurrentTileWallpaper()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("TileWallpaper");
				if (value != null)
				{
					return value.ToString();
				}
			}
		}
		catch
		{
		}
		return "0";
	}

	public static void ApplyWallpaper(string imagePath, string style = "Fill")
	{
		if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
		{
			throw new FileNotFoundException("Wallpaper file does not exist: " + imagePath);
		}
		string fullPath = Path.GetFullPath(imagePath);
		using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", writable: true))
		{
			if (registryKey != null)
			{
				if (style.Equals("Fill", StringComparison.OrdinalIgnoreCase) || style == "10")
				{
					registryKey.SetValue("WallpaperStyle", "10");
					registryKey.SetValue("TileWallpaper", "0");
				}
				else if (style.Equals("Fit", StringComparison.OrdinalIgnoreCase) || style == "6")
				{
					registryKey.SetValue("WallpaperStyle", "6");
					registryKey.SetValue("TileWallpaper", "0");
				}
				else if (style.Equals("Center", StringComparison.OrdinalIgnoreCase) || style == "0")
				{
					registryKey.SetValue("WallpaperStyle", "0");
					registryKey.SetValue("TileWallpaper", "0");
				}
				else if (style.Equals("Tile", StringComparison.OrdinalIgnoreCase) || style == "1")
				{
					registryKey.SetValue("WallpaperStyle", "0");
					registryKey.SetValue("TileWallpaper", "1");
				}
				else
				{
					registryKey.SetValue("WallpaperStyle", "2");
					registryKey.SetValue("TileWallpaper", "0");
				}
			}
		}
		if (SystemParametersInfo(20, 0, fullPath, 3) == 0)
		{
			int lastWin32Error = Marshal.GetLastWin32Error();
			throw new InvalidOperationException($"SystemParametersInfo failed with Win32 error: {lastWin32Error}");
		}
		TransactionLogger.Info($"SystemParametersInfo(SPI_SETDESKWALLPAPER) executed for: {fullPath}");
	}

	public static void RestoreWallpaper(string path, string style, string tile)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return;
		}
		using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", writable: true))
		{
			if (registryKey != null)
			{
				if (!string.IsNullOrEmpty(style))
				{
					registryKey.SetValue("WallpaperStyle", style);
				}
				if (!string.IsNullOrEmpty(tile))
				{
					registryKey.SetValue("TileWallpaper", tile);
				}
			}
		}
		SystemParametersInfo(20, 0, Path.GetFullPath(path), 3);
		TransactionLogger.Info($"Restored wallpaper '{path}' with style '{style}', tile '{tile}'.");
	}

	public static bool VerifyWallpaper(string expectedPath)
	{
		string currentWallpaper = GetCurrentWallpaper();
		bool flag = string.Equals(Path.GetFullPath(currentWallpaper), Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase);
		if (!flag)
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("Wallpaper");
				if (value != null)
				{
					flag = string.Equals(Path.GetFullPath(value.ToString()), Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase);
				}
			}
		}
		return flag;
	}

	public static Color GetDominantColor()
	{
		if (_cachedDominantColor.HasValue && (DateTime.UtcNow - _lastWallpaperCheck).TotalSeconds < 10.0)
		{
			return _cachedDominantColor.Value;
		}
		_lastWallpaperCheck = DateTime.UtcNow;
		string text = GetCurrentWallpaper();
		if (string.IsNullOrEmpty(text) || !File.Exists(text))
		{
			string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\Themes\\TranscodedWallpaper");
			if (File.Exists(text2))
			{
				text = text2;
			}
		}
		if (string.IsNullOrEmpty(text) || !File.Exists(text))
		{
			_cachedDominantColor = Color.FromRgb(120, 140, 180);
			return _cachedDominantColor.Value;
		}
		if (_cachedDominantColor.HasValue && string.Equals(text, _cachedWallpaperPath, StringComparison.OrdinalIgnoreCase))
		{
			return _cachedDominantColor.Value;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(text);
			bitmapImage.DecodePixelWidth = 32;
			bitmapImage.DecodePixelHeight = 32;
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(bitmapImage, PixelFormats.Bgra32, null, 0.0);
			formatConvertedBitmap.Freeze();
			int num = 128;
			byte[] array = new byte[32 * num];
			formatConvertedBitmap.CopyPixels(array, num, 0);
			long num2 = 0L;
			long num3 = 0L;
			long num4 = 0L;
			int num5 = 0;
			for (int i = 0; i < array.Length; i += 4)
			{
				byte b = array[i];
				byte b2 = array[i + 1];
				byte b3 = array[i + 2];
				int num6 = (b3 + b2 + b) / 3;
				if (num6 > 25 && num6 < 240)
				{
					num2 += b3;
					num3 += b2;
					num4 += b;
					num5++;
				}
			}
			if (num5 > 0)
			{
				byte r = (byte)(num2 / num5);
				byte g = (byte)(num3 / num5);
				byte b4 = (byte)(num4 / num5);
				_cachedDominantColor = Color.FromRgb(r, g, b4);
			}
			else
			{
				_cachedDominantColor = Color.FromRgb(120, 140, 180);
			}
			_cachedWallpaperPath = text;
			return _cachedDominantColor.Value;
		}
		catch
		{
			_cachedDominantColor = Color.FromRgb(120, 140, 180);
			return _cachedDominantColor.Value;
		}
	}
}
