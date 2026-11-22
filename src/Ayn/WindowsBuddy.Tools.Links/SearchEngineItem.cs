using System;

namespace WindowsBuddy.Tools.Links;

[Serializable]
public class SearchEngineItem
{
	public string Name { get; set; }

	public string SearchUrl { get; set; }

	public string Domain { get; set; }

	public QuickLinkType Type { get; set; }

	public string HomeUrl { get; set; }

	public SearchEngineItem()
	{
	}

	public SearchEngineItem(string name, string url, string domain)
	{
		Name = name ?? string.Empty;
		SearchUrl = url ?? string.Empty;
		Domain = domain ?? string.Empty;
		Type = QuickLinkType.SearchEngine;
		HomeUrl = null;
	}

	public string GetHomeUrl()
	{
		if (!string.IsNullOrWhiteSpace(HomeUrl))
		{
			return HomeUrl;
		}
		if (Type == QuickLinkType.DirectLink)
		{
			return SearchUrl;
		}
		if (!string.IsNullOrWhiteSpace(SearchUrl))
		{
			string text = SearchUrl.Split('?')[0];
			if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				return text.TrimEnd('/');
			}
		}
		return SearchUrl;
	}

	public string BuildSearchUrl(string query)
	{
		string text = SearchUrl ?? string.Empty;
		string newValue = Uri.EscapeDataString(query ?? string.Empty);
		return text.Replace("%s", newValue).Replace("{0}", newValue);
	}
}
