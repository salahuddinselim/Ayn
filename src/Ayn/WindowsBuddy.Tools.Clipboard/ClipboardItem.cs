using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace WindowsBuddy.Tools.Clipboard;

public class ClipboardItem
{
	internal BitmapSource PendingImage;

	private static readonly Regex EmailPattern = new Regex("^[A-Za-z0-9._%+\\-]+@[A-Za-z0-9.\\-]+\\.[A-Za-z]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public string Id { get; set; }

	public string Text { get; set; }

	public string Html { get; set; }

	public string Rtf { get; set; }

	public string ItemType { get; set; }

	public string Detail { get; set; }

	public string ImagePath { get; set; }

	public string ThumbPath { get; set; }

	public string ContentHash { get; set; }

	public List<string> FilePaths { get; set; }

	public string TimestampFormatted { get; set; }

	public long TimestampTicks { get; set; }

	public bool IsPinned { get; set; }

	public string SourceApp { get; set; }

	public string SourceAppExe { get; set; }

	public string SourceAppPath { get; set; }

	public int ImageWidth { get; set; }

	public int ImageHeight { get; set; }

	public long FileSizeBytes { get; set; }

	public int LineCount { get; set; }

	public string Filename { get; set; }

	public string PageTitle { get; set; }

	[ScriptIgnore]
	public ClipKind Kind
	{
		get
		{
			switch (ItemType)
			{
			case "image":
				return ClipKind.Image;
			case "url":
				return ClipKind.Link;
			case "files":
				return ClipKind.Files;
			case "code":
				return ClipKind.Code;
			default:
			{
				string text = ((Text != null) ? Text.Trim() : string.Empty);
				if (text.Length > 0 && text.Length < 254 && text.IndexOf(' ') < 0 && EmailPattern.IsMatch(text))
				{
					return ClipKind.Email;
				}
				return ClipKind.Text;
			}
			}
		}
	}

	[ScriptIgnore]
	public DateTime TimestampLocal => new DateTime(Math.Max(0L, TimestampTicks), DateTimeKind.Utc).ToLocalTime();

	[ScriptIgnore]
	public int WordCount
	{
		get
		{
			if (string.IsNullOrEmpty(Text))
			{
				return 0;
			}
			int num = 0;
			bool flag = false;
			string text = Text;
			foreach (char c in text)
			{
				if (char.IsWhiteSpace(c))
				{
					flag = false;
				}
				else if (!flag)
				{
					flag = true;
					num++;
				}
			}
			return num;
		}
	}

	[ScriptIgnore]
	public string LinkHost
	{
		get
		{
			Uri linkUri = LinkUri;
			if (linkUri == null)
			{
				return null;
			}
			string text = linkUri.Host;
			if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
			{
				text = text.Substring(4);
			}
			return text;
		}
	}

	[ScriptIgnore]
	public Uri LinkUri
	{
		get
		{
			if (string.IsNullOrWhiteSpace(Text))
			{
				return null;
			}
			string text = Text.Trim();
			if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				text = "https://" + text;
			}
			if (!Uri.TryCreate(text, UriKind.Absolute, out var result))
			{
				return null;
			}
			return result;
		}
	}

	public ClipboardItem()
	{
		Id = Guid.NewGuid().ToString("N");
		ItemType = "text";
		TimestampTicks = DateTime.UtcNow.Ticks;
		TimestampFormatted = DateTime.Now.ToString("h:mm tt");
		IsPinned = false;
		FilePaths = new List<string>();
	}

	public ClipboardItem(string text, string type = "text", string detail = null)
		: this()
	{
		Text = text ?? string.Empty;
		ItemType = type;
		Detail = detail;
	}

	public ClipboardItem CloneForPersistence()
	{
		ClipboardItem clipboardItem = (ClipboardItem)MemberwiseClone();
		clipboardItem.FilePaths = ((FilePaths == null) ? null : new List<string>(FilePaths));
		return clipboardItem;
	}

	public string GetTimeAgo()
	{
		if (TimestampTicks <= 0)
		{
			return string.Empty;
		}
		TimeSpan timeSpan = DateTime.UtcNow - new DateTime(TimestampTicks, DateTimeKind.Utc);
		if (timeSpan.TotalSeconds < 60.0)
		{
			return "Just now";
		}
		if (timeSpan.TotalMinutes < 60.0)
		{
			return string.Format(CultureInfo.CurrentCulture, "{0}m ago", Math.Max(1, (int)timeSpan.TotalMinutes));
		}
		if (timeSpan.TotalHours < 24.0)
		{
			return string.Format(CultureInfo.CurrentCulture, "{0}h ago", (int)timeSpan.TotalHours);
		}
		if (timeSpan.TotalDays < 7.0)
		{
			return string.Format(CultureInfo.CurrentCulture, "{0}d ago", (int)timeSpan.TotalDays);
		}
		return TimestampLocal.ToString("d MMM", CultureInfo.CurrentCulture);
	}

	public static string FormatFileSize(long bytes)
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		if (bytes <= 0)
		{
			return "0 KB";
		}
		if (bytes < 1024)
		{
			return string.Format(currentCulture, "{0} B", bytes);
		}
		if (bytes < 1048576)
		{
			return string.Format(currentCulture, "{0:0.#} KB", (double)bytes / 1024.0);
		}
		if (bytes < 1073741824)
		{
			return string.Format(currentCulture, "{0:0.#} MB", (double)bytes / 1048576.0);
		}
		return string.Format(currentCulture, "{0:0.#} GB", (double)bytes / 1073741824.0);
	}

	public string GetEffectiveFilename()
	{
		if (!string.IsNullOrEmpty(Filename))
		{
			return Filename;
		}
		if (Kind == ClipKind.Image)
		{
			return "Clipboard " + TimestampLocal.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture);
		}
		if (Kind == ClipKind.Files && FilePaths != null && FilePaths.Count > 0)
		{
			return Path.GetFileName(FilePaths[0]);
		}
		string text = (Text ?? string.Empty).Trim();
		int num = text.IndexOfAny(new char[2] { '\r', '\n' });
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, ' ');
		}
		text = Regex.Replace(text, "\\s+", " ").Trim();
		if (text.Length > 40)
		{
			text = text.Substring(0, 40).Trim();
		}
		if (text.Length <= 0)
		{
			return "Clipboard text";
		}
		return text;
	}
}
