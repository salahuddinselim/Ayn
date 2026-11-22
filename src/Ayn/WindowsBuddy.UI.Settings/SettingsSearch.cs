using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using WindowsBuddy.Tools;

namespace WindowsBuddy.UI.Settings;

internal static class SettingsSearch
{
	internal sealed class Hit
	{
		public string Page;

		public string Title;

		public string SettingId;

		public string Where;

		public int Score;
	}

	private sealed class Entry
	{
		public string Page;

		public string Title;

		public string Section;

		public string Keywords;

		public Entry(string page, string section, string title, string keywords)
		{
			Page = page;
			Section = section;
			Title = title;
			Keywords = keywords ?? "";
		}
	}

	private static readonly List<Entry> Entries = new List<Entry>
	{
		new Entry("general", "Startup", "Launch at login", "startup start with windows boot sign in autostart"),
		new Entry("general", "Keyboard", "Open the notch", "shortcut hotkey keyboard key combination"),
		new Entry("general", "Time & units", "Time format", "12 24 hour clock system am pm"),
		new Entry("general", "Time & units", "Temperature", "celsius fahrenheit units degrees"),
		new Entry("general", "Time & units", "Wind speed", "kmh mph units"),
		new Entry("general", "Language", "Language", "english locale translation"),
		new Entry("notch", "Ayn Pet (Beta)", "Ayn Pet (Beta)", "pet mascot companion eyes cursor greetings suggestions name"),
		new Entry("notch", "Eyes", "Eyes", "eyes follow cursor animated blink ember aurora nebula mercury skin fire chrome galaxy"),
		new Entry("notch", "Preview", "Preview", "live preview resting hover"),
		new Entry("notch", "Style", "Style", "line notch island shape pill bar"),
		new Entry("notch", "Style", "Notch shape", "default compact wide circular minimal dynamic silhouette"),
		new Entry("notch", "Style", "Show notch", "hide visible enable disable"),
		new Entry("notch", "Style", "Display", "display show on all monitors monitor screen primary multiple displays follow mouse cursor"),
		new Entry("notch", "Style", "Size", "scale bigger smaller width"),
		new Entry("notch", "Behavior", "Opens on", "hover click open trigger"),
		new Entry("notch", "Visibility", "Visibility", "always smart minimal hidden arrow tab over apps maximized mode show hide"),
		new Entry("notch", "Visibility", "Over maximized apps", "smart arrow only mini notch small collapsed tucked maximized"),
		new Entry("notch", "Behavior", "Hide in fullscreen", "fullscreen video presentation"),
		new Entry("notch", "Behavior", "Hide in fullscreen", "fullscreen gaming game"),
		new Entry("notch", "Behavior", "Show events over apps", "brief events hidden"),
		new Entry("notch", "Behavior", "Tab animations", "motion slide transitions"),
		new Entry("notch", "When closed, show", "Time", "clock resting"),
		new Entry("notch", "When closed, show", "Resting layout", "corners center centre time battery position"),
		new Entry("notch", "When closed, show", "Clock style", "analog digital"),
		new Entry("notch", "When closed, show", "Date", "day date"),
		new Entry("notch", "When closed, show", "Battery", "power charge percent"),
		new Entry("notch", "When closed, show", "Now playing", "music media spotify controls"),
		new Entry("notch", "When closed, show", "Bluetooth", "headphones devices"),
		new Entry("notch", "Edge light", "Edge light", "glow light border animation"),
		new Entry("notch", "Edge light", "Edge light style", "comet twin comets breathe sweep pulse"),
		new Entry("notch", "Edge light", "Color", "edge light colour swatch custom"),
		new Entry("notch", "Edge light", "Intensity", "brightness edge light"),
		new Entry("notch", "Edge light", "Speed", "edge light speed"),
		new Entry("notch", "Edge light", "When", "edge light always events hover animation"),
		new Entry("appearance", "Surface", "Outline", "border stroke hairline outline"),
		new Entry("appearance", "Surface", "Outline width", "border thickness outline width"),
		new Entry("appearance", "Surface", "Tint outline from wallpaper", "adapt wallpaper color tint"),
		new Entry("appearance", "Surface", "Shadow", "depth drop shadow"),
		new Entry("appearance", "Surface", "Soft edge", "blur soft edge background reduced effects"),
		new Entry("modules", null, "Modules", "dock tabs reorder order show hide drag"),
		new Entry("live", "Music", "Now playing", "media music live activity"),
		new Entry("live", "Music", "Music color", "beat color visualizer music"),
		new Entry("live", "Charging", "Battery charging", "battery charging plugged power live activity"),
		new Entry("live", "Volume", "Volume", "volume speaker sound"),
		new Entry("live", "Copied", "Clipboard copied", "copy toast"),
		new Entry("live", "Copied", "Hide copied text", "mask privacy clipboard toast"),
		new Entry("live", "Reminders", "Running timers", "pomodoro countdown stopwatch timer running live closed notch"),
		new Entry("live", "Reminders", "Timers", "pomodoro countdown water finished"),
		new Entry("live", "Reminders", "Calendar alerts", "events reminders"),
		new Entry("live", "Reminders", "Reminders", "reminders notification toast delivery windows"),
		new Entry("live", "Timing", "Event duration", "seconds how long display duration"),
		new Entry("privacy", "Links", "Show website icons in Links", "favicon website icons network privacy"),
		new Entry("privacy", "History", "Clipboard history", "clipboard recording history local save"),
		new Entry("privacy", "History", "Idle privacy reset", "privacy idle home timeout"),
		new Entry("privacy", "Clear", "Clipboard history", "clear delete clipboard unpinned"),
		new Entry("privacy", "Clear", "Screen Time data", "clear delete usage"),
		new Entry("privacy", "Location", "Weather location", "location permission gps"),
		new Entry("privacy", "Your settings", "Export settings", "backup save export"),
		new Entry("privacy", "Backup & restore", "Export backup", "backup notes clipboard calendar shelf links timers water widgets dock export"),
		new Entry("privacy", "Backup & restore", "Import backup", "restore backup merge replace reinstall new PC import"),
		new Entry("privacy", "Backup & restore", "Automatic weekly backup", "automatic weekly documents backup retention four"),
		new Entry("privacy", "Your settings", "Import settings", "restore load import"),
		new Entry("privacy", "Your settings", "Open data folder", "folder files appdata"),
		new Entry("privacy", "Start over", "Clear all data", "reset delete everything wipe"),
		new Entry("license", "Plan", "Plan", "license pro trial buy purchase upgrade price status"),
		new Entry("license", "License key", "License key", "activate key serial pro deactivate seat"),
		new Entry("license", "License key", "Activate", "activate key serial enter paste"),
		new Entry("license", "Updates", "Current version", "version build update"),
		new Entry("license", "Updates", "Updates", "update store download latest new version"),
		new Entry("about", null, "Support", "help email contact"),
		new Entry("about", null, "Privacy policy", "privacy"),
		new Entry("about", null, "Terms of use", "terms"),
		new Entry("about", null, "Inter", "font license third party"),
		new Entry("about", null, "Fluent UI System Icons", "icons license third party"),
		new Entry("module:clock", "Clock", "Show seconds", "seconds clock"),
		new Entry("module:clock", "Clock", "Date format", "date format clock"),
		new Entry("module:clock", "Clock", "Face", "analog digital clock face"),
		new Entry("module:clipboard", "History", "Keep up to", "clipboard history limit items"),
		new Entry("module:clipboard", "History", "Save clipboard history", "pause clipboard recording"),
		new Entry("module:clipboard", "History", "Clear history", "clipboard clear"),
		new Entry("module:timer", "Pomodoro", "Focus", "pomodoro focus minutes"),
		new Entry("module:timer", "Pomodoro", "Short break", "pomodoro break"),
		new Entry("module:timer", "Pomodoro", "Long break", "pomodoro long break"),
		new Entry("module:timer", "Countdown & water", "Default countdown", "countdown minutes"),
		new Entry("module:timer", "Countdown & water", "Water reminders", "hydration drink water"),
		new Entry("module:timer", "Notch", "Live timer on the closed notch", "timer resting live"),
		new Entry("module:calendar", "Holidays & festivals", "Public holidays", "holidays calendar"),
		new Entry("module:calendar", "Holidays & festivals", "Country", "holidays country region"),
		new Entry("module:calendar", "Holidays & festivals", "Observances & festivals", "festivals observances"),
		new Entry("module:calendar", "Subscribed calendars", "Subscribed calendars", "ics feed google outlook webcal subscribe"),
		new Entry("module:weather", "Location", "Use my location", "weather city location"),
		new Entry("module:screentime", "Daily limit", "Daily limit", "screen time limit hours"),
		new Entry("module:screentime", "Tracking", "Track screen time", "pause screen time"),
		new Entry("module:analytics", "Sites", "Sites", "analytics sites plausible datafast"),
		new Entry("module:analytics", "Notch", "Live visitors on the notch", "analytics realtime"),
		new Entry("module:aicoding", "Monthly budget", "All tools", "ai coding budget spend dollars"),
		new Entry("module:aicoding", "Notch", "Budget alerts", "ai coding alert")
	};

	public static List<Hit> Find(string query)
	{
		string[] array = query.ToLowerInvariant().Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		List<Hit> list = new List<Hit>();
		if (array.Length == 0)
		{
			return list;
		}
		SettingsWindow current = SettingsWindow.Current;
		if (current != null)
		{
			foreach (KeyValuePair<string, UIElement> page in current.SearchPages())
			{
				KeyValuePair<string, UIElement> keyValuePair = page;
				foreach (FrameworkElement item in Rows(keyValuePair.Value, pageRoot: true))
				{
					string title = AutomationProperties.GetName(item);
					if (string.IsNullOrEmpty(title))
					{
						title = item.ToolTip as string;
					}
					if (string.IsNullOrEmpty(title))
					{
						title = ((string)item.Tag).Substring("setting-card-".Length).Replace("-", " ");
					}
					Entry[] array2 = Entries.Where(delegate(Entry e)
					{
						string page2 = e.Page;
						KeyValuePair<string, UIElement> keyValuePair5 = page;
						return page2 == keyValuePair5.Key && string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase);
					}).ToArray();
					if (array2.Length > 1 && ((string)item.Tag).StartsWith("setting-privacy-clear-"))
					{
						array2 = array2.Where((Entry e) => e.Section == "Clear").ToArray();
					}
					string text = string.Join(" ", array2.Select((Entry e) => e.Keywords));
					string text2 = ((array2.Length > 0) ? array2[0].Section : null);
					string[] array3 = new string[7] { title, " ", null, null, null, null, null };
					KeyValuePair<string, UIElement> keyValuePair2 = page;
					array3[2] = PageTitle(keyValuePair2.Key);
					array3[3] = " ";
					array3[4] = text2;
					array3[5] = " ";
					array3[6] = text;
					string hay3 = string.Concat(array3).ToLowerInvariant();
					if (array.All((string t) => hay3.Contains(t)))
					{
						Hit hit = new Hit();
						KeyValuePair<string, UIElement> keyValuePair3 = page;
						hit.Page = keyValuePair3.Key;
						hit.Title = title;
						hit.SettingId = (string)item.Tag;
						KeyValuePair<string, UIElement> keyValuePair4 = page;
						hit.Where = PageTitle(keyValuePair4.Key) + (string.IsNullOrEmpty(text2) ? "" : (" › " + text2));
						hit.Score = ((!title.ToLowerInvariant().StartsWith(array[0])) ? 1 : 0);
						list.Add(hit);
					}
				}
			}
		}
		PageInfo[] all = PageInfo.All;
		foreach (PageInfo pageInfo in all)
		{
			string hay2 = (pageInfo.Title + " " + pageInfo.Subtitle).ToLowerInvariant();
			if (array.All((string t) => hay2.Contains(t)))
			{
				list.Add(new Hit
				{
					Page = pageInfo.Key,
					Title = pageInfo.Title,
					Where = "Page",
					Score = ((!pageInfo.Title.ToLowerInvariant().StartsWith(array[0])) ? 1 : 0)
				});
			}
		}
		foreach (IBuddyTool item2 in ModuleCatalog.DockOrder())
		{
			ModuleInfo moduleInfo = ModuleCatalog.For(item2);
			string hay = (moduleInfo.Name + " " + moduleInfo.Description + " " + moduleInfo.Keywords).ToLowerInvariant();
			if (array.All((string t) => hay.Contains(t)))
			{
				list.Add(new Hit
				{
					Page = "module:" + item2.Id,
					Title = moduleInfo.Name,
					Where = "Module · " + moduleInfo.Description,
					Score = ((!moduleInfo.Name.ToLowerInvariant().StartsWith(array[0])) ? 2 : 0)
				});
			}
		}
		return (from h in list
			orderby h.Score, h.Title.Length
			select h).ToList();
	}

	internal static IEnumerable<FrameworkElement> Rows(DependencyObject root, bool pageRoot = false)
	{
		FrameworkElement fe = root as FrameworkElement;
		if (fe != null && !pageRoot && fe.Visibility == Visibility.Collapsed)
		{
			yield break;
		}
		if (fe != null && fe.Tag is string && ((string)fe.Tag).StartsWith("setting-"))
		{
			yield return fe;
		}
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (!(child is DependencyObject))
			{
				continue;
			}
			foreach (FrameworkElement item in Rows((DependencyObject)child))
			{
				yield return item;
			}
		}
	}

	private static string PageTitle(string key)
	{
		if (key.StartsWith("module:"))
		{
			ModuleInfo moduleInfo = ModuleCatalog.Get(key.Substring(7));
			if (moduleInfo == null)
			{
				return key.Substring(7);
			}
			return moduleInfo.Name;
		}
		PageInfo pageInfo = PageInfo.Get(key);
		if (pageInfo == null)
		{
			return key;
		}
		return pageInfo.Title;
	}

	public static FrameworkElement TileFor(string key, double size)
	{
		if (key.StartsWith("module:"))
		{
			ModuleInfo moduleInfo = ModuleCatalog.Get(key.Substring(7));
			if (moduleInfo != null)
			{
				return IconTile.Create(moduleInfo.Hue, moduleInfo.Glyph, size);
			}
		}
		PageInfo pageInfo = PageInfo.Get(key);
		if (pageInfo != null)
		{
			return pageInfo.Tile(size);
		}
		return IconTile.Create(Colors.Gray, "settings_f", size);
	}
}
