using System;
using System.Collections.Concurrent;
using System.Windows.Media;

namespace WindowsBuddy.Tools.Clipboard;

public static class LinkMetadataService
{
	private static readonly ConcurrentDictionary<string, LinkMetadata> _cache = new ConcurrentDictionary<string, LinkMetadata>(StringComparer.OrdinalIgnoreCase);

	public static LinkMetadata GetCached(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return null;
		}
		_cache.TryGetValue(url, out var value);
		return value;
	}

	public static void SetCache(string url, string title, ImageSource favicon)
	{
		if (!string.IsNullOrEmpty(url))
		{
			_cache[url] = new LinkMetadata
			{
				Url = url,
				Title = title,
				Favicon = favicon,
				IsLoaded = true
			};
		}
	}
}
