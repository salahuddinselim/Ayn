using System;
using System.Collections.Generic;
using System.Linq;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class NotchEditing
{
	public static readonly string[] LegacyWidgets = new string[6] { "screentime", "system", "weather", "timer", "clipboard", "notes" };

	public static readonly string[] Widgets = new string[19]
	{
		"screentime", "system", "weather", "timer", "clipboard", "notes", "clock", "calendar", "music", "links",
		"shelf", "units", "files", "network", "water", "stopwatch", "analytics", "revenue", "aicoding"
	};

	public static int AddSlot = -1;

	public static bool IsEditing { get; private set; }

	public static string[] Slots
	{
		get
		{
			string text = AppPreferences.Instance.HomeWidgetSlots;
			if (text == null)
			{
				if (ToolRegistry.Instance.GetTool("notes") == null)
				{
					return (string[])LegacyWidgets.Clone();
				}
				string hidden = AppPreferences.Instance.WindowState.HiddenHomeWidgets;
				string[] first = (AppPreferences.Instance.HomeWidgetOrder ?? string.Join(",", LegacyWidgets)).Split(',');
				List<string> list = first.Concat(LegacyWidgets).Where(((IEnumerable<string>)LegacyWidgets).Contains<string>).Where(delegate(string id)
				{
					if (hidden != null)
					{
						return !hidden.Split(',').Contains(id);
					}
					return ToolRegistry.Instance.GetTool(id) == null || ToolRegistry.Instance.GetTool(id).IsEnabled;
				})
					.Distinct()
					.Take(6)
					.ToList();
				while (list.Count < 6)
				{
					list.Add("");
				}
				text = string.Join(",", list);
				AppPreferences.Instance.HomeWidgetSlots = text;
			}
			string[] array = new string[6];
			HashSet<string> hashSet = new HashSet<string>();
			string[] array2 = text.Split(',');
			for (int i = 0; i < 6; i++)
			{
				array[i] = ((i < array2.Length && Widgets.Contains(array2[i]) && hashSet.Add(array2[i])) ? array2[i] : "");
			}
			return array;
		}
	}

	public static bool HasRoom => Slots.Contains("");

	public static string[] WidgetOrder
	{
		get
		{
			string[] source = (AppPreferences.Instance.HomeWidgetOrder ?? string.Join(",", Widgets)).Split(',');
			return Slots.Where((string id) => id != "").Concat(source.Where(((IEnumerable<string>)Widgets).Contains<string>)).Concat(Widgets)
				.Distinct()
				.ToArray();
		}
	}

	public static event Action Changed;

	public static event Action AddWidgetRequested;

	public static bool PlaceWidget(string id, int slot)
	{
		string[] slots = Slots;
		if (!Widgets.Contains(id) || slots.Contains(id))
		{
			return false;
		}
		if (slot < 0)
		{
			slot = Array.IndexOf(slots, "");
		}
		if (slot < 0 || slot >= 6 || slots[slot] != "")
		{
			return false;
		}
		slots[slot] = id;
		SaveSlots(slots);
		return true;
	}

	public static void SaveSlots(IEnumerable<string> values)
	{
		string[] list = values.Take(6).ToArray();
		if (list.Length == 6 && list.Where((string x) => x != "").Distinct().Count() == list.Count((string x) => x != "") && !list.Any((string x) => x != "" && !Widgets.Contains(x)))
		{
			AppPreferences.Instance.HomeWidgetSlots = string.Join(",", list);
			AppPreferences.Instance.HiddenHomeWidgets = string.Join(",", Widgets.Where((string id) => !list.Contains(id)));
			Notify();
		}
	}

	public static void SwapSlots(int from, int to)
	{
		string[] slots = Slots;
		if (from >= 0 && to >= 0 && from < 6 && to < 6)
		{
			string text = slots[from];
			slots[from] = slots[to];
			slots[to] = text;
			SaveSlots(slots);
		}
	}

	public static bool WidgetVisible(string id)
	{
		return Slots.Contains(id);
	}

	public static void SetWidgetVisible(string id, bool value)
	{
		if (value)
		{
			if (!PlaceWidget(id, -1))
			{
				Notify();
			}
			return;
		}
		string[] slots = Slots;
		for (int i = 0; i < 6; i++)
		{
			if (slots[i] == id)
			{
				slots[i] = "";
			}
		}
		SaveSlots(slots);
	}

	public static void SetWidgetOrder(IEnumerable<string> ids)
	{
		AppPreferences.Instance.HomeWidgetOrder = string.Join(",", ids);
		Notify();
	}

	public static void Set(bool value)
	{
		if (IsEditing != value)
		{
			IsEditing = value;
			Notify();
		}
	}

	public static void Notify()
	{
		NotchEditing.Changed?.Invoke();
	}

	public static void AddWidget()
	{
		AddWidget(-1);
	}

	public static void AddWidget(int slot)
	{
		AddSlot = ((slot < 0) ? Array.IndexOf(Slots, "") : slot);
		NotchEditing.AddWidgetRequested?.Invoke();
	}
}
