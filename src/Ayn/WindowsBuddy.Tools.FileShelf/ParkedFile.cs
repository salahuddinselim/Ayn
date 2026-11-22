using System;
using System.Globalization;
using System.IO;

namespace WindowsBuddy.Tools.FileShelf;

public class ParkedFile
{
	internal long ViewOrder;

	public string Id { get; set; }

	public string FullPath { get; set; }

	public string FileName { get; set; }

	public string Extension { get; set; }

	public long FileSizeBytes { get; set; }

	public string FormattedSize { get; set; }

	public DateTime AddedUtc { get; set; }

	public string AddedAtFormatted => AddedUtc.ToLocalTime().ToString("h:mm tt");

	public bool IsDirectory { get; set; }

	public bool IsOwned { get; set; }

	public bool IsMissing { get; private set; }

	public bool IsProbed { get; private set; }

	public long LastWriteTicks { get; private set; }

	public bool Exists
	{
		get
		{
			try
			{
				if (IsDirectory)
				{
					return Directory.Exists(FullPath);
				}
				return File.Exists(FullPath) || Directory.Exists(FullPath);
			}
			catch
			{
				return false;
			}
		}
	}

	public bool IsNetworkPath => IsNetwork(FullPath);

	public ParkedFile Clone()
	{
		return (ParkedFile)MemberwiseClone();
	}

	internal void ApplyProbeStatus(ParkedFile probe)
	{
		Extension = probe.Extension;
		FileSizeBytes = probe.FileSizeBytes;
		FormattedSize = probe.FormattedSize;
		IsDirectory = probe.IsDirectory;
		IsMissing = probe.IsMissing;
		IsProbed = probe.IsProbed;
		LastWriteTicks = probe.LastWriteTicks;
	}

	public ParkedFile()
	{
		Id = Guid.NewGuid().ToString("N");
		AddedUtc = DateTime.UtcNow;
		FormattedSize = string.Empty;
	}

	public ParkedFile(string fullPath)
		: this()
	{
		SetPath(fullPath);
	}

	public void SetPath(string fullPath)
	{
		FullPath = fullPath;
		FileName = SafeGetFileName(fullPath);
		Extension = SafeGetExtension(fullPath);
		IsProbed = false;
	}

	public static bool IsNetwork(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		if (path.StartsWith("\\\\", StringComparison.Ordinal) && !path.StartsWith("\\\\?\\", StringComparison.Ordinal))
		{
			return true;
		}
		try
		{
			if (path.Length >= 2 && path[1] == ':')
			{
				return new DriveInfo(path.Substring(0, 1)).DriveType == DriveType.Network;
			}
		}
		catch
		{
		}
		return false;
	}

	public bool Probe()
	{
		bool flag = true;
		bool flag2 = false;
		long num = 0L;
		long num2 = 0L;
		try
		{
			FileInfo fileInfo = OpenInfo(FullPath);
			if (fileInfo != null && fileInfo.Exists)
			{
				flag = false;
				num = fileInfo.Length;
				num2 = fileInfo.LastWriteTimeUtc.Ticks;
			}
			else if (Directory.Exists(FullPath) || Directory.Exists(LongPath(FullPath)))
			{
				flag = false;
				flag2 = true;
				try
				{
					num2 = Directory.GetLastWriteTimeUtc(FullPath).Ticks;
				}
				catch
				{
				}
			}
		}
		catch
		{
			flag = true;
		}
		string formattedSize = (flag ? FormattedSize : (flag2 ? "Folder" : FormatBytes(num)));
		bool result = !IsProbed || flag != IsMissing || flag2 != IsDirectory || (!flag && (num != FileSizeBytes || num2 != LastWriteTicks));
		IsProbed = true;
		IsMissing = flag;
		if (!flag)
		{
			IsDirectory = flag2;
			FileSizeBytes = num;
			LastWriteTicks = num2;
			FormattedSize = formattedSize;
			if (flag2)
			{
				Extension = "Folder";
			}
		}
		return result;
	}

	private static FileInfo OpenInfo(string path)
	{
		try
		{
			return new FileInfo(path);
		}
		catch (PathTooLongException)
		{
			try
			{
				return new FileInfo(LongPath(path));
			}
			catch
			{
				return null;
			}
		}
	}

	private static string LongPath(string path)
	{
		if (string.IsNullOrEmpty(path) || path.StartsWith("\\\\?\\", StringComparison.Ordinal))
		{
			return path;
		}
		if (path.StartsWith("\\\\", StringComparison.Ordinal))
		{
			return "\\\\?\\UNC\\" + path.Substring(2);
		}
		return "\\\\?\\" + path;
	}

	private static string SafeGetFileName(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return "Unknown";
		}
		string text = path.TrimEnd('\\', '/');
		int num = Math.Max(text.LastIndexOf('/'), text.LastIndexOf('\\'));
		string text2 = ((num >= 0 && num < text.Length - 1) ? text.Substring(num + 1) : text);
		if (!string.IsNullOrEmpty(text2))
		{
			return text2;
		}
		return path;
	}

	private static string SafeGetExtension(string path)
	{
		string text = SafeGetFileName(path);
		int num = text.LastIndexOf('.');
		if (num > 0 && num < text.Length - 1)
		{
			return text.Substring(num + 1).ToUpperInvariant();
		}
		return string.Empty;
	}

	public static string FormatBytes(long bytes)
	{
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		if (bytes < 1024)
		{
			return string.Format(invariantCulture, "{0} B", bytes);
		}
		if (bytes < 1048576)
		{
			return string.Format(invariantCulture, "{0:0} KB", (double)bytes / 1024.0);
		}
		if (bytes < 1073741824)
		{
			return string.Format(invariantCulture, "{0:0.0} MB", (double)bytes / 1048576.0);
		}
		if (bytes < 1099511627776L)
		{
			return string.Format(invariantCulture, "{0:0.0} GB", (double)bytes / 1073741824.0);
		}
		return string.Format(invariantCulture, "{0:0.0} TB", (double)bytes / 1099511627776.0);
	}
}
