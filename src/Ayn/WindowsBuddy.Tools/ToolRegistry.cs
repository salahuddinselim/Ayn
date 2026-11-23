using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools;

public class ToolRegistry
{
	public const int DefaultDockCount = 13;

	public const int MaxDockTabs = 14;

	private const int CurrentDockVersion = 2;

	public const int HoverInstantMs = 0;

	public const int HoverNormalMs = 120;

	public const int HoverRelaxedMs = 300;

	public const string DockFullMessage = "Your dock is full. Remove a tool to add another.";

	private static ToolRegistry _instance;

	public static readonly string[] CanonicalDefaultDockOrder = new string[22]
	{
		"home", "system", "clipboard", "notes", "timer", "todo", "shelf", "drop", "weather", "clock",
		"breaks", "salat", "qr", "docs", "units", "links", "screentime",
		"calendar", "files", "analytics", "aicoding", "revenue"
	};

	private readonly List<IBuddyTool> _tools = new List<IBuddyTool>();

	private IBuddyTool _activeTool;

	private bool _capsuleEnabled = true;

	private string _primaryCollapsedToolId = "time";

	private string _hoverSensitivity = "normal";

	private bool _animationsEnabled = true;

	private string _position = "center";

	private string _pinnedDisplay = "primary";

	private bool _startWithWindows;

	private string _openOn = "hover";

	private int _hoverDelayMs = -1;

	private readonly string _configFilePath;

	private volatile bool _persistenceSealed;

	public static ToolRegistry Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new ToolRegistry();
			}
			return _instance;
		}
	}

	public bool CapsuleEnabled
	{
		get
		{
			return _capsuleEnabled;
		}
		set
		{
			if (_capsuleEnabled != value)
			{
				_capsuleEnabled = value;
				SaveUserConfiguration();
				NotifyCapsuleSettingsChanged();
			}
		}
	}

	public string HoverSensitivity
	{
		get
		{
			return _hoverSensitivity;
		}
		set
		{
			if (_hoverSensitivity != value)
			{
				_hoverSensitivity = value;
				SaveUserConfiguration();
				NotifyCapsuleSettingsChanged();
			}
		}
	}

	public bool AnimationsEnabled
	{
		get
		{
			return _animationsEnabled;
		}
		set
		{
			if (_animationsEnabled != value)
			{
				_animationsEnabled = value;
				SaveUserConfiguration();
				NotifyCapsuleSettingsChanged();
			}
		}
	}

	public string Position
	{
		get
		{
			return _position;
		}
		set
		{
			if (_position != value)
			{
				_position = value;
				SaveUserConfiguration();
				NotifyCapsuleSettingsChanged();
			}
		}
	}

	public string PinnedDisplay
	{
		get
		{
			return _pinnedDisplay;
		}
		set
		{
			if (_pinnedDisplay != value)
			{
				_pinnedDisplay = value;
				SaveUserConfiguration();
				NotifyCapsuleSettingsChanged();
			}
		}
	}

	public string OpenOn
	{
		get
		{
			return _openOn;
		}
		set
		{
			string text = (string.Equals(value, "click", StringComparison.OrdinalIgnoreCase) ? "click" : "hover");
			if (_openOn != text)
			{
				_openOn = text;
				SaveUserConfiguration();
				NotifyCapsuleSettingsChanged();
			}
		}
	}

	public bool OpensOnClick => _openOn == "click";

	public int HoverDelayMs
	{
		get
		{
			return 0;
		}
		set
		{
		}
	}

	public bool StartWithWindows
	{
		get
		{
			return _startWithWindows;
		}
		set
		{
			if (_startWithWindows != value)
			{
				_startWithWindows = value;
				SaveUserConfiguration();
				NotifyCapsuleSettingsChanged();
			}
		}
	}

	public string PrimaryCollapsedToolId
	{
		get
		{
			return _primaryCollapsedToolId;
		}
		set
		{
			if (!(_primaryCollapsedToolId != value))
			{
				return;
			}
			_primaryCollapsedToolId = value;
			SaveUserConfiguration();
			if (this.PrimaryCollapsedToolChanged != null)
			{
				try
				{
					this.PrimaryCollapsedToolChanged(this, EventArgs.Empty);
				}
				catch
				{
				}
			}
		}
	}

	public IReadOnlyList<IBuddyTool> AllTools => _tools.OrderBy((IBuddyTool t) => t.OrderIndex).ToList();

	public IReadOnlyList<IBuddyTool> EnabledTools => (from t in _tools
		where t.IsEnabled
		orderby t.OrderIndex
		select t).ToList();

	public IReadOnlyList<IBuddyTool> DockTools => (from t in EnabledTools
		where !string.Equals(t.Id, "settings", StringComparison.OrdinalIgnoreCase) && !string.Equals(t.Id, "notch_customization", StringComparison.OrdinalIgnoreCase)
		orderby (!string.Equals(t.Id, "home", StringComparison.OrdinalIgnoreCase)) ? 1 : 0
		select t).Take(14).ToList();

	public bool DockFull => DockTools.Count >= 14;

	public IBuddyTool ActiveTool
	{
		get
		{
			return _activeTool;
		}
		set
		{
			if (_activeTool == value)
			{
				return;
			}
			_activeTool = value;
			if (this.ActiveToolChanged != null)
			{
				try
				{
					this.ActiveToolChanged(this, EventArgs.Empty);
				}
				catch (Exception ex)
				{
					TransactionLogger.Error("Error notifying ActiveToolChanged: " + ex.Message, ex);
				}
			}
		}
	}

	public event EventHandler ActiveToolChanged;

	public event EventHandler ToolsUpdated;

	public event EventHandler PrimaryCollapsedToolChanged;

	public event EventHandler CapsuleSettingsChanged;

	public static bool IsDefaultDockTab(string id)
	{
		int num = Array.FindIndex(CanonicalDefaultDockOrder, (string d) => d.Equals(id, StringComparison.OrdinalIgnoreCase));
		if (num >= 0)
		{
			return num < 13;
		}
		return false;
	}

	public void ForceEnableCapsule()
	{
		_capsuleEnabled = true;
		SaveUserConfiguration();
		NotifyCapsuleSettingsChanged();
	}

	public static int SnapHoverDelay(int ms)
	{
		if (ms <= 60)
		{
			return 0;
		}
		if (ms <= 210)
		{
			return 120;
		}
		return 300;
	}

	private void NotifyCapsuleSettingsChanged()
	{
		if (this.CapsuleSettingsChanged != null)
		{
			try
			{
				this.CapsuleSettingsChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public bool IsInDock(string id)
	{
		return DockTools.Any((IBuddyTool t) => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	public bool CanAddToDock(string id)
	{
		if (GetTool(id) == null)
		{
			return false;
		}
		if (!IsInDock(id))
		{
			return !DockFull;
		}
		return true;
	}

	public bool TryAddToDock(string id)
	{
		if (!CanAddToDock(id))
		{
			return false;
		}
		SetToolEnabled(id, enabled: true);
		return true;
	}

	public ToolRegistry()
	{
		string localAppData = AppPaths.LocalAppData;
		string text = Path.Combine(localAppData, AppPaths.FolderName, "settings");
		if (!Directory.Exists(text))
		{
			try
			{
				Directory.CreateDirectory(text);
			}
			catch
			{
			}
		}
		_configFilePath = Path.Combine(text, "tools_config.json");
		PersistenceHelper.RegisterExitFlush("modules", SaveUserConfiguration);
		LoadInitialConfiguration();
	}

	private void LoadInitialConfiguration()
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_configFilePath);
			if (string.IsNullOrEmpty(text))
			{
				_hoverDelayMs = 0;
			}
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			ToolConfigModel toolConfigModel = javaScriptSerializer.Deserialize<ToolConfigModel>(text);
			if (toolConfigModel == null)
			{
				return;
			}
			_capsuleEnabled = toolConfigModel.CapsuleEnabled;
			if (!string.IsNullOrEmpty(toolConfigModel.PrimaryCollapsedToolId))
			{
				_primaryCollapsedToolId = toolConfigModel.PrimaryCollapsedToolId;
			}
			if (!string.IsNullOrEmpty(toolConfigModel.HoverSensitivity))
			{
				_hoverSensitivity = toolConfigModel.HoverSensitivity;
			}
			_animationsEnabled = toolConfigModel.AnimationsEnabled;
			if (!string.IsNullOrEmpty(toolConfigModel.Position))
			{
				_position = toolConfigModel.Position;
			}
			if (!string.IsNullOrEmpty(toolConfigModel.PinnedDisplay))
			{
				_pinnedDisplay = toolConfigModel.PinnedDisplay;
			}
			_startWithWindows = toolConfigModel.StartWithWindows;
			_openOn = ((toolConfigModel.OpenOn == "click") ? "click" : "hover");
			_hoverDelayMs = 0;
			if (toolConfigModel.HoverDelayMs != 0 || (toolConfigModel.HoverSensitivity ?? "normal") != "normal")
			{
				toolConfigModel.HoverDelayMs = 0;
				toolConfigModel.HoverSensitivity = "normal";
				_hoverSensitivity = "normal";
				try
				{
					PersistenceHelper.SafeWriteAllText(_configFilePath, javaScriptSerializer.Serialize(toolConfigModel));
				}
				catch
				{
				}
			}
			if (string.Equals(_primaryCollapsedToolId, "scratchpad", StringComparison.OrdinalIgnoreCase))
			{
				_primaryCollapsedToolId = "message";
			}
			else if (string.Equals(_primaryCollapsedToolId, "stats", StringComparison.OrdinalIgnoreCase))
			{
				_primaryCollapsedToolId = "system";
			}
			else if (string.Equals(_primaryCollapsedToolId, "sounds", StringComparison.OrdinalIgnoreCase))
			{
				_primaryCollapsedToolId = "time";
			}
			if (toolConfigModel.Tools == null || toolConfigModel.Tools.Count <= 0)
			{
				return;
			}
			bool flag = false;
			if (!toolConfigModel.Tools.Any((ToolItemConfig t) => t.Id.Equals("drop", StringComparison.OrdinalIgnoreCase)))
			{
				ToolItemConfig toolItemConfig = toolConfigModel.Tools.FirstOrDefault((ToolItemConfig t) => t.Id.Equals("shelf", StringComparison.OrdinalIgnoreCase));
				int at = ((toolItemConfig == null) ? toolConfigModel.Tools.Count : (toolItemConfig.OrderIndex + 1));
				foreach (ToolItemConfig item in toolConfigModel.Tools.Where((ToolItemConfig t) => t.OrderIndex >= at))
				{
					item.OrderIndex++;
				}
				toolConfigModel.Tools.Add(new ToolItemConfig
				{
					Id = "drop",
					IsEnabled = true,
					OrderIndex = at
				});
				flag = true;
			}
			List<string> list = (from t in toolConfigModel.Tools
				where t.IsEnabled
				orderby t.OrderIndex
				select t.Id.ToLowerInvariant()).ToList();
			bool flag2 = list.Count >= 3 && list[0] == "home" && list[1] == "clock" && list[2] == "system";
			bool flag3 = list.Count >= 3 && list[0] == "home" && list[1] == "stats" && list[2] == "system";
			if (flag2 || flag3)
			{
				TransactionLogger.Info("Migrating dock order to clean canonical default (home, system, clipboard, notes, timer, shelf, calendar)");
				for (int i = 0; i < CanonicalDefaultDockOrder.Length; i++)
				{
					string id = CanonicalDefaultDockOrder[i];
					ToolItemConfig toolItemConfig2 = toolConfigModel.Tools.FirstOrDefault((ToolItemConfig t) => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
					if (toolItemConfig2 != null)
					{
						toolItemConfig2.IsEnabled = i < 13;
						toolItemConfig2.OrderIndex = i;
						continue;
					}
					toolConfigModel.Tools.Add(new ToolItemConfig
					{
						Id = id,
						IsEnabled = (i < 13),
						OrderIndex = i
					});
				}
				flag = true;
			}
			int num = toolConfigModel.Tools.RemoveAll((ToolItemConfig t) => t.Id.Equals("scratchpad", StringComparison.OrdinalIgnoreCase) || t.Id.Equals("sounds", StringComparison.OrdinalIgnoreCase) || t.Id.Equals("stats", StringComparison.OrdinalIgnoreCase));
			if (num > 0)
			{
				flag = true;
				TransactionLogger.Info($"Sanitized {num} obsolete tool entries from persisted configuration.");
			}
			List<ToolItemConfig> list2 = toolConfigModel.Tools.OrderBy((ToolItemConfig t) => t.OrderIndex).ToList();
			for (int j = 0; j < list2.Count; j++)
			{
				if (list2[j].OrderIndex != j)
				{
					list2[j].OrderIndex = j;
					flag = true;
				}
			}
			if (flag)
			{
				try
				{
					string content = javaScriptSerializer.Serialize(toolConfigModel);
					PersistenceHelper.SafeWriteAllText(_configFilePath, content);
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not load initial tool configuration: " + ex.Message);
		}
	}

	public void RegisterTool(IBuddyTool tool)
	{
		if (tool != null && !_tools.Any((IBuddyTool t) => t.Id.Equals(tool.Id, StringComparison.OrdinalIgnoreCase)))
		{
			_tools.Add(tool);
			ApplySavedToolConfig(tool);
			if (_activeTool == null && tool.IsEnabled)
			{
				ActiveTool = tool;
			}
			NotifyToolsUpdated();
		}
	}

	public void EnsureInitialized(IBuddyTool tool)
	{
		if (tool == null || tool.IsInitialized || !EntitlementGate.IsEntitled)
		{
			return;
		}
		try
		{
			tool.Initialize();
		}
		catch (Exception ex)
		{
			TransactionLogger.Error($"Error initializing tool '{tool.Id}': {ex.Message}", ex);
		}
	}

	public IBuddyTool GetTool(string id)
	{
		if (string.Equals(id, "converter", StringComparison.OrdinalIgnoreCase))
		{
			id = "units";
		}
		if (string.Equals(id, "file_converter", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "fileconverter", StringComparison.OrdinalIgnoreCase))
		{
			id = "files";
		}
		return _tools.FirstOrDefault((IBuddyTool t) => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
	}

	public IEnumerable<IBuddyTool> GetToolsByCategory(ToolCategory category)
	{
		return from t in _tools
			where t.Category == category
			orderby t.OrderIndex
			select t;
	}

	public void SelectTool(string id)
	{
		IBuddyTool tool = GetTool(id);
		if (tool != null && tool.IsEnabled)
		{
			ToolRouter.Instance.RouteTo(id);
		}
	}

	public void SetToolEnabled(string toolId, bool enabled)
	{
		if (toolId == "home" && !enabled)
		{
			return;
		}
		IBuddyTool tool = GetTool(toolId);
		if (tool != null && tool.IsEnabled != enabled)
		{
			tool.IsEnabled = enabled;
			if (!enabled && _activeTool == tool)
			{
				ActiveTool = EnabledTools.FirstOrDefault();
			}
			SaveUserConfiguration();
			NotifyToolsUpdated();
		}
	}

	public void MoveToolUp(string toolId)
	{
		List<IBuddyTool> list = AllTools.ToList();
		int num = list.FindIndex((IBuddyTool t) => t.Id.Equals(toolId, StringComparison.OrdinalIgnoreCase));
		if (num > 0)
		{
			IBuddyTool buddyTool = list[num];
			IBuddyTool buddyTool2 = list[num - 1];
			int orderIndex = buddyTool.OrderIndex;
			buddyTool.OrderIndex = buddyTool2.OrderIndex;
			buddyTool2.OrderIndex = orderIndex;
			NormalizeOrderIndices();
			SaveUserConfiguration();
			NotifyToolsUpdated();
		}
	}

	public void SetDockOrder(IEnumerable<string> ids)
	{
		List<IBuddyTool> ordered = (from t in ids.Distinct().Select(GetTool)
			where t != null
			select t).ToList();
		IBuddyTool tool = GetTool("home");
		if (tool != null)
		{
			ordered.Remove(tool);
			ordered.Insert(0, tool);
		}
		ordered.AddRange(AllTools.Where((IBuddyTool t) => !ordered.Contains(t)));
		for (int i = 0; i < ordered.Count; i++)
		{
			ordered[i].OrderIndex = i;
		}
		SaveUserConfiguration();
		NotifyToolsUpdated();
	}

	public void MoveToolDown(string toolId)
	{
		List<IBuddyTool> list = AllTools.ToList();
		int num = list.FindIndex((IBuddyTool t) => t.Id.Equals(toolId, StringComparison.OrdinalIgnoreCase));
		if (num >= 0 && num < list.Count - 1)
		{
			IBuddyTool buddyTool = list[num];
			IBuddyTool buddyTool2 = list[num + 1];
			int orderIndex = buddyTool.OrderIndex;
			buddyTool.OrderIndex = buddyTool2.OrderIndex;
			buddyTool2.OrderIndex = orderIndex;
			NormalizeOrderIndices();
			SaveUserConfiguration();
			NotifyToolsUpdated();
		}
	}

	private void NormalizeOrderIndices()
	{
		List<IBuddyTool> list = _tools.OrderBy((IBuddyTool t) => t.OrderIndex).ToList();
		for (int i = 0; i < list.Count; i++)
		{
			list[i].OrderIndex = i;
		}
	}

	private void NotifyToolsUpdated()
	{
		if (this.ToolsUpdated != null)
		{
			try
			{
				this.ToolsUpdated(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public void SaveUserConfiguration()
	{
		if (_persistenceSealed)
		{
			TransactionLogger.Error("Module configuration change after shutdown commit was not written (registry sealed).");
			return;
		}
		try
		{
			ToolConfigModel toolConfigModel = new ToolConfigModel();
			toolConfigModel.CapsuleEnabled = _capsuleEnabled;
			toolConfigModel.PrimaryCollapsedToolId = _primaryCollapsedToolId;
			toolConfigModel.HoverSensitivity = _hoverSensitivity;
			toolConfigModel.AnimationsEnabled = _animationsEnabled;
			toolConfigModel.Position = _position;
			toolConfigModel.PinnedDisplay = _pinnedDisplay;
			toolConfigModel.StartWithWindows = _startWithWindows;
			toolConfigModel.OpenOn = _openOn;
			toolConfigModel.HoverDelayMs = _hoverDelayMs;
			toolConfigModel.DockVersion = 2;
			toolConfigModel.Tools = _tools.Select((IBuddyTool t) => new ToolItemConfig
			{
				Id = t.Id,
				IsEnabled = t.IsEnabled,
				OrderIndex = t.OrderIndex
			}).ToList();
			ToolConfigModel toolConfigModel2 = toolConfigModel;
			ToolConfigModel toolConfigModel3 = null;
			try
			{
				string text = PersistenceHelper.SafeReadAllText(_configFilePath);
				if (!string.IsNullOrEmpty(text))
				{
					toolConfigModel3 = new JavaScriptSerializer().Deserialize<ToolConfigModel>(text);
				}
			}
			catch
			{
			}
			if (toolConfigModel3 != null && toolConfigModel3.Tools != null)
			{
				List<ToolItemConfig> list = toolConfigModel3.Tools.Where((ToolItemConfig s) => s != null && s.Id != null && !_tools.Any((IBuddyTool t) => t.Id.Equals(s.Id, StringComparison.OrdinalIgnoreCase))).ToList();
				if (list.Count > 0)
				{
					toolConfigModel2.Tools.AddRange(list);
					toolConfigModel2.DockVersion = Math.Min(toolConfigModel3.DockVersion, 2);
				}
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			string content = javaScriptSerializer.Serialize(toolConfigModel2);
			PersistenceHelper.SafeWriteAllText(_configFilePath, content);
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_configFilePath, error);
		}
	}

	private void ApplySavedToolConfig(IBuddyTool tool)
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_configFilePath);
			ToolConfigModel toolConfigModel = (string.IsNullOrEmpty(text) ? null : new JavaScriptSerializer().Deserialize<ToolConfigModel>(text));
			if (toolConfigModel == null || toolConfigModel.Tools == null || toolConfigModel.Tools.Count == 0)
			{
				int num = Array.FindIndex(CanonicalDefaultDockOrder, (string id) => id.Equals(tool.Id, StringComparison.OrdinalIgnoreCase));
				if (num >= 0)
				{
					tool.IsEnabled = num < 13;
					tool.OrderIndex = num;
				}
				else
				{
					tool.IsEnabled = false;
					tool.OrderIndex = 20;
				}
				return;
			}
			ToolConfigModel toolConfigModel2 = toolConfigModel;
			ToolItemConfig toolItemConfig = toolConfigModel2.Tools.FirstOrDefault((ToolItemConfig c) => c.Id.Equals(tool.Id, StringComparison.OrdinalIgnoreCase));
			if (toolItemConfig != null)
			{
				tool.IsEnabled = toolItemConfig.IsEnabled;
				tool.OrderIndex = toolItemConfig.OrderIndex;
				if (tool.Id is "pets" or "todo" or "qr")
				{
					tool.IsEnabled = true;
					tool.OrderIndex = tool.Id switch
					{
						"todo" => 5,
						"pets" => 6,
						_ => 7
					};
				}
			}
			else
			{
				int canonical = Array.FindIndex(CanonicalDefaultDockOrder,
					(string id) => id.Equals(tool.Id, StringComparison.OrdinalIgnoreCase));
				if (canonical >= 0)
				{
					tool.IsEnabled = canonical < 14;
					tool.OrderIndex = canonical;
				}
				else
				{
					tool.IsEnabled = false;
					tool.OrderIndex = 100;
				}
			}
			if (toolConfigModel2.DockVersion < 2 && IsDefaultDockTab(tool.Id))
			{
				tool.IsEnabled = true;
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not load tool config for " + tool.Id + ": " + ex.Message);
		}
		finally
		{
			if (string.Equals(tool.Id, "home", StringComparison.OrdinalIgnoreCase))
			{
				tool.IsEnabled = true;
			}
		}
	}

	public void ReorderTools(List<string> orderedIds)
	{
		for (int i = 0; i < orderedIds.Count; i++)
		{
			IBuddyTool tool = GetTool(orderedIds[i]);
			if (tool != null)
			{
				tool.OrderIndex = i;
			}
		}
		NormalizeOrderIndices();
		SaveUserConfiguration();
		NotifyToolsUpdated();
	}

	public void ResetToDefaults()
	{
		string[] array = CanonicalDefaultDockOrder.Take(13).ToArray();
		foreach (IBuddyTool tool2 in _tools)
		{
			tool2.IsEnabled = false;
		}
		for (int i = 0; i < array.Length; i++)
		{
			IBuddyTool tool = GetTool(array[i]);
			if (tool != null)
			{
				tool.IsEnabled = true;
				tool.OrderIndex = i;
			}
		}
		int num = array.Length;
		foreach (IBuddyTool item in _tools.Where((IBuddyTool t) => !t.IsEnabled))
		{
			item.OrderIndex = num++;
		}
		SaveUserConfiguration();
		NotifyToolsUpdated();
	}

	public void ShutdownAll()
	{
		_persistenceSealed = true;
		foreach (IBuddyTool tool in _tools)
		{
			try
			{
				tool.Shutdown();
			}
			catch
			{
			}
		}
		_tools.Clear();
		_activeTool = null;
	}
}
