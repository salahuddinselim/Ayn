using System.Collections.Generic;

namespace WindowsBuddy.Tools.Links;

internal static class SearchEngines
{
	public static List<SearchEngineItem> CreateDefaults()
	{
		List<SearchEngineItem> list = new List<SearchEngineItem>();
		list.Add(new SearchEngineItem("Google", "https://www.google.com/search?q={0}", "google.com"));
		list.Add(new SearchEngineItem("YouTube", "https://www.youtube.com/results?search_query={0}", "youtube.com"));
		list.Add(new SearchEngineItem("GitHub", "https://github.com/search?q={0}", "github.com"));
		list.Add(new SearchEngineItem("Stack Overflow", "https://stackoverflow.com/search?q={0}", "stackoverflow.com"));
		list.Add(new SearchEngineItem("ChatGPT", "https://chatgpt.com/?q={0}", "chatgpt.com"));
		list.Add(new SearchEngineItem("Perplexity", "https://www.perplexity.ai/search?q={0}", "perplexity.ai"));
		return list;
	}
}
