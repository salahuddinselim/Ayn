using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Media;

namespace WindowsBuddy.Tools.Message;

public static class MessageText
{
	public const int MaxLength = 60;

	public const int MaxChars = 240;

	public const int MaxSeconds = 3600;

	public static int Count(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return EmojiText.Clusters(Cap(s, 960)).Count;
		}
		return 0;
	}

	public static string Limit(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return string.Empty;
		}
		if (s.Length <= 60)
		{
			return s;
		}
		List<string> list = EmojiText.Clusters(Cap(s, 960));
		StringBuilder stringBuilder = new StringBuilder(Math.Min(s.Length, 240));
		for (int i = 0; i < list.Count && i < 60 && stringBuilder.Length + list[i].Length <= 240; i++)
		{
			stringBuilder.Append(list[i]);
		}
		if (stringBuilder.Length != s.Length)
		{
			return stringBuilder.ToString();
		}
		return s;
	}

	private static string Cap(string s, int max)
	{
		if (s.Length <= max)
		{
			return s;
		}
		if (char.IsHighSurrogate(s[max - 1]))
		{
			max--;
		}
		return s.Substring(0, max);
	}

	public static string Clean(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return string.Empty;
		}
		return Limit(s.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')
			.Trim());
	}

	public static int ClampSeconds(int s)
	{
		return Math.Max(1, Math.Min(3600, s));
	}

	public static string FormatSeconds(int s)
	{
		s = ClampSeconds(s);
		if (s < 60)
		{
			return s + "s";
		}
		if (s % 60 != 0)
		{
			return s / 60 + "m " + s % 60 + "s";
		}
		return s / 60 + "m";
	}

	public static Color? ParseColor(string hex)
	{
		if (string.IsNullOrEmpty(hex))
		{
			return null;
		}
		string text = hex.Trim().TrimStart('#');
		if (text.Length != 6)
		{
			return null;
		}
		if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
		{
			return null;
		}
		return Color.FromRgb((byte)(result >> 16), (byte)(result >> 8), (byte)result);
	}

	public static string ToHex(Color c)
	{
		return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
	}
}
