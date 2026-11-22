using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class BrandAssets
{
	private const string MarkResource = "Ayn.Brand.mark-128.png";

	private const string TrayDarkResource = "Ayn.Brand.tray-dark.ico";

	private const string TrayLightResource = "Ayn.Brand.tray-light.ico";

	private static BitmapSource _mark;

	public static FrameworkElement CreateLogo(double size)
	{
		return AynBrand.CreateLogo(size);
	}

	public static bool IsTaskbarLight()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", writable: false);
			object obj = registryKey?.GetValue("SystemUsesLightTheme");
			return obj is int && (int)obj == 1;
		}
		catch
		{
			return false;
		}
	}

	public static Icon LoadTrayIcon()
	{
		try
		{
			using Stream stream = typeof(BrandAssets).Assembly.GetManifestResourceStream(IsTaskbarLight() ? "Ayn.Brand.tray-light.ico" : "Ayn.Brand.tray-dark.ico");
			if (stream != null)
			{
				return new Icon(stream, SystemInformation.SmallIconSize);
			}
		}
		catch
		{
		}
		try
		{
			return Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
		}
		catch
		{
			return SystemIcons.Application;
		}
	}
}
