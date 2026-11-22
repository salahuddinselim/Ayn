using System;
using System.Globalization;

namespace WindowsBuddy.Tools.Units;

internal static class Expr
{
	public static decimal? Evaluate(string text)
	{
		if (text != null && text.Length > 256)
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string text2 = text.Replace(" ", "").Replace("\u00a0", "").Replace('×', '*')
			.Replace('÷', '/')
			.Replace('−', '-')
			.Replace('x', '*')
			.Replace('X', '*');
		text2 = ((text2.Contains(".") && text2.Contains(",")) ? text2.Replace(",", "") : text2.Replace(',', '.'));
		int p = 0;
		try
		{
			decimal value = Sum(text2, ref p);
			return (p == text2.Length) ? new decimal?(value) : null;
		}
		catch
		{
			return null;
		}
	}

	private static decimal Sum(string s, ref int p)
	{
		decimal num = Product(s, ref p);
		while (p < s.Length && (s[p] == '+' || s[p] == '-'))
		{
			char c = s[p++];
			decimal num2 = Product(s, ref p);
			num = ((c == '+') ? (num + num2) : (num - num2));
		}
		return num;
	}

	private static decimal Product(string s, ref int p)
	{
		decimal num = Unary(s, ref p);
		while (p < s.Length && (s[p] == '*' || s[p] == '/'))
		{
			char c = s[p++];
			decimal num2 = Unary(s, ref p);
			num = ((c == '*') ? (num * num2) : (num / num2));
		}
		return num;
	}

	private static decimal Unary(string s, ref int p)
	{
		if (p < s.Length && (s[p] == '-' || s[p] == '+'))
		{
			bool flag = s[p++] == '-';
			decimal num = Unary(s, ref p);
			if (!flag)
			{
				return num;
			}
			return -num;
		}
		if (p < s.Length && s[p] == '(')
		{
			p++;
			decimal result = Sum(s, ref p);
			if (p >= s.Length || s[p] != ')')
			{
				throw new FormatException();
			}
			p++;
			return result;
		}
		int num2 = p;
		while (p < s.Length && (char.IsDigit(s[p]) || s[p] == '.'))
		{
			p++;
		}
		if (p == num2)
		{
			throw new FormatException();
		}
		return decimal.Parse(s.Substring(num2, p - num2), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
	}
}
