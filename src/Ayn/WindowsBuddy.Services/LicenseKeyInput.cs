using System.Text;
using System.Text.RegularExpressions;

namespace WindowsBuddy.Services;

public static class LicenseKeyInput
{
	private static readonly Regex Grouped = new Regex("[A-Za-z0-9]+(?:[-_][A-Za-z0-9]+)+");

	private static readonly Regex Plain = new Regex("[A-Za-z0-9]{12,}");

	public static string Normalize(string input)
	{
		if (input == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder(input.Length);
		foreach (char c in input)
		{
			if (!IsInvisible(c))
			{
				stringBuilder.Append(IsQuote(c) ? ' ' : (char.IsWhiteSpace(c) ? ' ' : c));
			}
		}
		string text = stringBuilder.ToString().Trim();
		if (text.Length == 0)
		{
			return null;
		}
		text = Regex.Replace(text, "\\s*([-_])\\s+|\\s+([-_])\\s*", (Match m) => (!m.Groups[1].Success) ? m.Groups[2].Value : m.Groups[1].Value);
		string text2 = text.Replace(" ", "");
		if (IsKeyText(text2))
		{
			return text2;
		}
		string text3 = Longest(Grouped.Matches(text));
		if (text3 == null)
		{
			text3 = Longest(Plain.Matches(text));
		}
		if (text3 != null)
		{
			return text3;
		}
		string text4 = text2.Trim('.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '<', '>', '{', '}', '*', '`', '|');
		if (text4.Length != 0)
		{
			return text4;
		}
		return null;
	}

	private static string Longest(MatchCollection matches)
	{
		string text = null;
		foreach (Match match in matches)
		{
			if (text == null || match.Value.Length > text.Length)
			{
				text = match.Value;
			}
		}
		return text;
	}

	private static bool IsKeyText(string s)
	{
		if (s.Length == 0)
		{
			return false;
		}
		foreach (char c in s)
		{
			if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '-' && c != '_')
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsInvisible(char c)
	{
		if (c != '\u200b' && c != '\u200c' && c != '\u200d' && c != '\u2060' && c != '\ufeff' && c != '\u00ad' && c != '\u180e')
		{
			if (char.IsControl(c))
			{
				return !char.IsWhiteSpace(c);
			}
			return false;
		}
		return true;
	}

	private static bool IsQuote(char c)
	{
		if (c != '"' && c != '\'' && c != '‘' && c != '’' && c != '“' && c != '”' && c != '«' && c != '»')
		{
			return c == '`';
		}
		return true;
	}
}
