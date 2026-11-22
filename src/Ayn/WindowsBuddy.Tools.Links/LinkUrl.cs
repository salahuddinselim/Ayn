using System;
using System.Linq;

namespace WindowsBuddy.Tools.Links;

public static class LinkUrl
{
	public const int MaxLength = 2048;

	public static bool TryNormalize(string raw, out Uri uri)
	{
		uri = null;
		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}
		string text = raw.Trim();
		if (text.Length > 2048)
		{
			return false;
		}
		if (text.IndexOfAny(new char[8] { ' ', '\t', '\r', '\n', '"', '<', '>', '\\' }) >= 0)
		{
			return false;
		}
		int num = text.IndexOf(':');
		if (num > 0)
		{
			string text2 = text.Substring(0, num);
			bool flag = char.IsLetter(text2[0]) && text2.All((char c) => char.IsLetterOrDigit(c) || c == '+' || c == '-' || c == '.');
			bool flag2 = num + 1 < text.Length && char.IsDigit(text[num + 1]);
			if (flag && !flag2)
			{
				if (!text2.Equals("http", StringComparison.OrdinalIgnoreCase) && !text2.Equals("https", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				if (!text.Substring(num).StartsWith("://", StringComparison.Ordinal))
				{
					return false;
				}
			}
			else
			{
				text = "https://" + text;
			}
		}
		else
		{
			text = "https://" + text;
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out var result))
		{
			return false;
		}
		if (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps)
		{
			return false;
		}
		string host = result.Host;
		if (string.IsNullOrWhiteSpace(host))
		{
			return false;
		}
		if (result.HostNameType != UriHostNameType.IPv4 && result.HostNameType != UriHostNameType.IPv6 && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && (!host.Contains(".") || host.StartsWith(".") || host.EndsWith(".") || !host.Any(char.IsLetter)))
		{
			return false;
		}
		uri = result;
		return true;
	}

	public static string Key(string url)
	{
		if (!TryNormalize(url, out var uri))
		{
			return (url ?? string.Empty).Trim().ToLowerInvariant();
		}
		string text = uri.Host.ToLowerInvariant();
		if (text.StartsWith("www."))
		{
			text = text.Substring(4);
		}
		string text2 = uri.AbsolutePath.TrimEnd('/');
		return text + (uri.IsDefaultPort ? "" : (":" + uri.Port)) + text2 + uri.Query;
	}

	public static string Host(string url)
	{
		if (!TryNormalize(url, out var uri))
		{
			return url ?? string.Empty;
		}
		string host = uri.Host;
		if (!host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
		{
			return host;
		}
		return host.Substring(4);
	}
}
