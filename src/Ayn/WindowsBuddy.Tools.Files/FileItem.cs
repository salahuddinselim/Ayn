using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace WindowsBuddy.Tools.Files;

public sealed class FileItem
{
	public string Path;

	public string Name;

	public string Ext;

	public long SizeBytes;

	public int Width;

	public int Height;

	public int PageCount = 1;

	public bool IsPdf;

	public bool Probed;

	public string InputError;

	public ImageSource Thumb;

	public ItemState State;

	public double Progress;

	public string Error;

	public string Note;

	public OutFormat DoneFormat;

	public long OutputBytes;

	public readonly List<string> Outputs = new List<string>();

	public string TypeLabel
	{
		get
		{
			string text = (Ext ?? "").TrimStart('.').ToUpperInvariant();
			switch (text)
			{
			case "JPEG":
			case "JFIF":
				return "JPG";
			case "TIF":
				return "TIFF";
			default:
				if (text.Length != 0)
				{
					return text;
				}
				return "FILE";
			}
		}
	}

	public event EventHandler Changed;

	public void RaiseChanged()
	{
		this.Changed?.Invoke(this, EventArgs.Empty);
	}

	public static string FormatBytes(long bytes)
	{
		if (bytes < 1024)
		{
			return Math.Max(0L, bytes) + " B";
		}
		if (bytes < 1048576)
		{
			return $"{(double)bytes / 1024.0:0} KB";
		}
		if (bytes < 1073741824)
		{
			return $"{(double)bytes / 1048576.0:0.#} MB";
		}
		return $"{(double)bytes / 1073741824.0:0.##} GB";
	}

	public static string FormatName(OutFormat f)
	{
		return f switch
		{
			OutFormat.Png => "PNG", 
			OutFormat.Pdf => "PDF", 
			OutFormat.Ico => "ICO", 
			OutFormat.Bmp => "BMP", 
			OutFormat.Gif => "GIF", 
			OutFormat.Tiff => "TIFF", 
			_ => "JPG", 
		};
	}
}
