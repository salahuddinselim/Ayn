using System.Collections.Generic;
using System.Globalization;

namespace WindowsBuddy.Tools.Message;

public static class EmojiText
{
	private static readonly HashSet<int> Presentation = new HashSet<int>
	{
		8986, 8987, 9193, 9194, 9195, 9196, 9200, 9203, 9725, 9726,
		9748, 9749, 9800, 9801, 9802, 9803, 9804, 9805, 9806, 9807,
		9808, 9809, 9810, 9811, 9855, 9875, 9889, 9898, 9899, 9917,
		9918, 9924, 9925, 9934, 9940, 9962, 9970, 9971, 9973, 9978,
		9981, 9989, 9994, 9995, 10024, 10060, 10062, 10067, 10068, 10069,
		10071, 10133, 10134, 10135, 10160, 10175, 11035, 11036, 11088, 11093
	};

	public static List<string> Clusters(string s)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(s))
		{
			return list;
		}
		TextElementEnumerator textElementEnumerator = StringInfo.GetTextElementEnumerator(s);
		while (textElementEnumerator.MoveNext())
		{
			string textElement = textElementEnumerator.GetTextElement();
			int count = list.Count;
			if (count > 0 && JoinsPrevious(list[count - 1], textElement))
			{
				list[count - 1] += textElement;
			}
			else
			{
				list.Add(textElement);
			}
		}
		return list;
	}

	private static bool JoinsPrevious(string prev, string el)
	{
		int num = char.ConvertToUtf32(el, 0);
		int num2 = LastCodePoint(prev);
		if (num2 == 8205)
		{
			return true;
		}
		switch (num)
		{
		case 8205:
		case 65038:
		case 65039:
			return true;
		case 127995:
		case 127996:
		case 127997:
		case 127998:
		case 127999:
			return true;
		default:
			if (num >= 917536 && num <= 917631)
			{
				return true;
			}
			if (num == 8419)
			{
				return true;
			}
			if (IsRegional(num) && IsRegional(char.ConvertToUtf32(prev, 0)) && CodePoints(prev) == 1)
			{
				return true;
			}
			return false;
		}
	}

	private static bool IsRegional(int cp)
	{
		if (cp >= 127462)
		{
			return cp <= 127487;
		}
		return false;
	}

	private static int LastCodePoint(string s)
	{
		int num = s.Length - 1;
		if (num > 0 && char.IsLowSurrogate(s[num]) && char.IsHighSurrogate(s[num - 1]))
		{
			return char.ConvertToUtf32(s[num - 1], s[num]);
		}
		return s[num];
	}

	private static int CodePoints(string s)
	{
		int num = 0;
		for (int i = 0; i < s.Length; i++)
		{
			if (!char.IsLowSurrogate(s[i]))
			{
				num++;
			}
		}
		return num;
	}

	public static bool IsEmoji(string cluster)
	{
		if (string.IsNullOrEmpty(cluster))
		{
			return false;
		}
		int num = char.ConvertToUtf32(cluster, 0);
		if (IsRegional(num))
		{
			return false;
		}
		if (num >= 126976 && num <= 129791)
		{
			return true;
		}
		if (Presentation.Contains(num))
		{
			return true;
		}
		if (cluster.IndexOf('\ufe0f') < 0)
		{
			return cluster.IndexOf('\u20e3') >= 0;
		}
		return true;
	}

	public static bool HasEmoji(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		foreach (string item in Clusters(s))
		{
			if (IsEmoji(item))
			{
				return true;
			}
		}
		return false;
	}
}
