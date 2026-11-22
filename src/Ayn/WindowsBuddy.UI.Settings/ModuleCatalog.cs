using System;
using System.Collections.Generic;
using System.Linq;
using WindowsBuddy.Tools;

namespace WindowsBuddy.UI.Settings;

internal static class ModuleCatalog
{
	private static readonly Dictionary<string, ModuleInfo> _byId;

	static ModuleCatalog()
	{
		_byId = new Dictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);
		Add(new ModuleInfo("home", "Home", "Now playing, your next event and quick stats", "#3B82F6", "home", "overview dashboard media", "Essentials"));
		Add(new ModuleInfo("system", "System", "CPU, memory, disk and battery", "#14B8A6", "developer_board", "cpu ram disk battery power network audio", "Essentials"));
		Add(new ModuleInfo("clock", "Clock", "World clocks, digital or analog", "#F59E0B", "clock", "time world clock analog digital seconds", "Essentials"));
		Add(new ModuleInfo("timer", "Timers", "Pomodoro, countdown, stopwatch and water", "#F97316", "timer", "pomodoro countdown stopwatch hydration water focus", "Essentials"));
		Add(new ModuleInfo("calendar", "Calendar", "Agenda, festivals and reminders", "#F43F5E", "calendar_ltr", "events holidays festivals reminders ics feeds", "Essentials"));
		Add(new ModuleInfo("weather", "Weather", "Now, hourly and five-day forecast", "#38BDF8", "weather_partly_cloudy_day", "forecast temperature city location celsius fahrenheit", "Essentials"));
		Add(new ModuleInfo("clipboard", "Clipboard", "Everything you copied, searchable", "#6366F1", "clipboard_text_ltr", "copy paste history"));
		Add(new ModuleInfo("notes", "Notes", "Quick notes that stay with the notch", "#EAB308", "note_edit", "note write text"));
		Add(new ModuleInfo("shelf", "File Shelf", "Park files and drag them anywhere", "#06B6D4", "folder", "files drag drop park shelf"));
		Add(new ModuleInfo("drop", "Ayn Drop", "Share with your phone on your private network", "#B7A8FF", "phone_arrow_up", "phone qr local send receive wifi"));
		Add(new ModuleInfo("files", "File Converter", "Convert images and documents", "#D946EF", "document_arrow_right", "file converter files images pdf png"));
		Add(new ModuleInfo("units", "Units", "Length, weight, temperature and more", "#8B5CF6", "arrow_swap", "units convert conversion"));
		Add(new ModuleInfo("links", "Links", "Bookmarks and quick web searches", "#EF4444", "link", "bookmarks urls search engines"));
		Add(new ModuleInfo("message", "Message", "Flash your own text on the notch", "#EC4899", "slide_text", "banner text flash"));
		Add(new ModuleInfo("screentime", "Screen Time", "Where your day went, app by app", "#A855F7", "hourglass", "usage apps daily limit", "Insights"));
		Add(new ModuleInfo("analytics", "Analytics", "Website visitors, pages and sources", "#22C55E", "data_bar_vertical", "plausible datafast visitors sites pages live", "Insights"));
		Add(new ModuleInfo("aicoding", "AI Coding", "Claude Code and Codex usage", "#52525B", "bot", "claude codex tokens cost budget", "Insights"));
		Add(new ModuleInfo("revenue", "Revenue", "Sales and license revenue at a glance", "#84CC16", "money", "sales income", "Insights"));
	}

	private static void Add(ModuleInfo m)
	{
		_byId[m.Id] = m;
	}

	public static ModuleInfo Get(string id)
	{
		if (string.Equals(id, "converter", StringComparison.OrdinalIgnoreCase))
		{
			id = "units";
		}
		if (string.Equals(id, "file_converter", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "fileconverter", StringComparison.OrdinalIgnoreCase))
		{
			id = "files";
		}
		if (id != null && _byId.TryGetValue(id, out var value))
		{
			return value;
		}
		return null;
	}

	public static ModuleInfo For(IBuddyTool tool)
	{
		ModuleInfo moduleInfo = Get(tool.Id);
		return moduleInfo ?? new ModuleInfo(tool.Id, tool.DisplayName, tool.DisplayName, "#6B7280", "settings", "");
	}

	public static List<IBuddyTool> DockOrder()
	{
		return ToolRegistry.Instance.AllTools.Where((IBuddyTool t) => !string.Equals(t.Id, "settings", StringComparison.OrdinalIgnoreCase) && !string.Equals(t.Id, "notch_customization", StringComparison.OrdinalIgnoreCase)).ToList();
	}
}
