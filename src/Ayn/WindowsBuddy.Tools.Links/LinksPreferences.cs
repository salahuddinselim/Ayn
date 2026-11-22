using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Links;

[Serializable]
public class LinksPreferences
{
	public string SelectedEngine { get; set; }

	public List<SearchEngineItem> CustomEngines { get; set; }

	public LinksPreferences()
	{
		SelectedEngine = "Google";
		CustomEngines = null;
	}
}
