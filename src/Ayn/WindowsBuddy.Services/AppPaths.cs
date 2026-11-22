using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsBuddy.Services;

public static class AppPaths
{
	public const string OverrideVariable = "AYN_DATA_DIR";

	public const string LegacyOverrideVariable = "WINDOWSBUDDY_DATA_DIR";

	public const string CurrentFolderName = "Ayn";

	public const string LegacyFolderName = "WindowsBuddy";

	private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

	private static string _folderName = "Ayn";

	private static int _packaged = -1;

	private static string _familyName;

	public static string FolderName => _folderName;

	private static string Override
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("AYN_DATA_DIR");
			if (string.IsNullOrWhiteSpace(environmentVariable))
			{
				environmentVariable = Environment.GetEnvironmentVariable("WINDOWSBUDDY_DATA_DIR");
			}
			if (!string.IsNullOrWhiteSpace(environmentVariable))
			{
				return environmentVariable.Trim();
			}
			return null;
		}
	}

	public static bool IsSandboxed => Override != null;

	public static string InstanceSuffix
	{
		get
		{
			string @override = Override;
			if (@override == null)
			{
				return "";
			}
			uint num = 2166136261u;
			string text = @override.ToLowerInvariant();
			foreach (char c in text)
			{
				num ^= c;
				num *= 16777619;
			}
			return "_qa" + num.ToString("x8");
		}
	}

	public static string DataDir => Path.Combine(LocalAppData, FolderName);

	public static string LocalAppData
	{
		get
		{
			if (Override == null)
			{
				return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			}
			return Path.Combine(Override, "Local");
		}
	}

	public static string RoamingAppData
	{
		get
		{
			if (Override == null)
			{
				return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			}
			return Path.Combine(Override, "Roaming");
		}
	}

	public static bool IsPackaged
	{
		get
		{
			if (_packaged < 0)
			{
				try
				{
					int packageFullNameLength = 0;
					int currentPackageFullName = GetCurrentPackageFullName(ref packageFullNameLength, null);
					_packaged = ((currentPackageFullName != 15700) ? 1 : 0);
				}
				catch
				{
					_packaged = 0;
				}
			}
			return _packaged == 1;
		}
	}

	public static string PackageFamilyName
	{
		get
		{
			if (_familyName != null || !IsPackaged)
			{
				return _familyName;
			}
			try
			{
				int packageFamilyNameLength = 0;
				GetCurrentPackageFamilyName(ref packageFamilyNameLength, null);
				StringBuilder stringBuilder = new StringBuilder(packageFamilyNameLength);
				if (packageFamilyNameLength > 0 && GetCurrentPackageFamilyName(ref packageFamilyNameLength, stringBuilder) == 0)
				{
					_familyName = stringBuilder.ToString();
				}
			}
			catch
			{
			}
			return _familyName;
		}
	}

	public static string OpenableDataDir => ResolveOpenableDataDir(IsPackaged && !IsSandboxed, PackageFamilyName, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName, DataDir, Directory.Exists);

	internal static void UseLegacyFolderForThisSession()
	{
		_folderName = "WindowsBuddy";
	}

	public static string UseTempFolder(string name)
	{
		string text = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WindowsBuddyQA")) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(Path.Combine(text, name, "data"));
		if (!fullPath.StartsWith(text, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("QA profile must stay inside WindowsBuddyQA", "name");
		}
		try
		{
			if (Directory.Exists(fullPath))
			{
				Directory.Delete(fullPath, recursive: true);
			}
		}
		catch
		{
		}
		Directory.CreateDirectory(fullPath);
		Environment.SetEnvironmentVariable("AYN_DATA_DIR", fullPath);
		return fullPath;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, StringBuilder packageFamilyName);

	public static string PhysicalDataDirFor(string realLocalAppData, string familyName, string folderName)
	{
		return Path.Combine(realLocalAppData, "Packages", familyName, "LocalCache", "Local", folderName);
	}

	public static string ResolveOpenableDataDir(bool packaged, string familyName, string realLocalAppData, string folderName, string virtualDataDir, Func<string, bool> directoryExists)
	{
		if (!packaged || string.IsNullOrEmpty(familyName))
		{
			return virtualDataDir;
		}
		string text = PhysicalDataDirFor(realLocalAppData, familyName, folderName);
		if (!directoryExists(text))
		{
			return virtualDataDir;
		}
		return text;
	}
}
