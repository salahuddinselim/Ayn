using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Behavior;

public class NotchBehaviorConfig
{
	private class NotchBehaviorConfigDto
	{
		public int VisibilityMode { get; set; }

		public bool IsPinned { get; set; }

		public int MediaMode { get; set; }

		public bool ShowMediaWhenPlaybackStarts { get; set; }

		public bool? ShowBrowserMediaInNotch { get; set; }

		public int BrowserMediaPolicyVersion { get; set; }

		public int EventDurationSeconds { get; set; }

		public bool ShowBriefEventsWhenHidden { get; set; }

		public int OverMaximizedLook { get; set; }

		public bool? HideInFullscreen { get; set; }

		public bool HideInFullscreenApps { get; set; }

		public bool HideInFullscreenGames { get; set; }

		public bool HideDuringPresentations { get; set; }

		public bool IdlePrivacyReset { get; set; }

		public double AutoHideDelaySeconds { get; set; }

		public bool HideOnBatterySaver { get; set; }

		public bool SyncFocusAssist { get; set; }

		public bool HoverToRevealFullscreen { get; set; }

		public List<string> FullscreenExceptionApps { get; set; }

		public Dictionary<string, int> EventModes { get; set; }

		public List<AppVisibilityRuleDto> AppRules { get; set; }

		public NotchBehaviorConfigDto()
		{
			VisibilityMode = 2;
			ShowMediaWhenPlaybackStarts = true;
			ShowBriefEventsWhenHidden = true;
			IdlePrivacyReset = true;
			AutoHideDelaySeconds = 3.0;
			HoverToRevealFullscreen = true;
			FullscreenExceptionApps = new List<string>();
		}
	}

	private class AppVisibilityRuleDto
	{
		public string ExecutablePath { get; set; }

		public string ProcessName { get; set; }

		public int Rule { get; set; }
	}

	private static readonly object _syncLock;

	private static NotchBehaviorConfig _instance;

	private static readonly string _storagePath;

	private NotchVisibilityMode _visibilityMode = NotchVisibilityMode.AlwaysVisible;

	private bool _isPinned;

	private MediaVisibilityMode _mediaMode;

	private bool _showMediaWhenPlaybackStarts = true;

	private bool _showBrowserMediaInNotch = true;

	private int _eventDurationSeconds = 3;

	private OverMaximizedLook _overMaximizedLook;

	private bool _showBriefEventsWhenHidden = true;

	private bool _hideInFullscreen = true;

	private bool _idlePrivacyReset = true;

	private bool _hideDuringPresentations = true;

	private double _autoHideDelaySeconds = 3.0;

	private bool _hideOnBatterySaver;

	private bool _syncFocusAssist;

	private bool _hoverToRevealFullscreen = true;

	private bool _needsMigrationSave;

	public static NotchBehaviorConfig Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_syncLock)
				{
					if (_instance == null)
					{
						_instance = Load();
					}
				}
			}
			return _instance;
		}
	}

	public NotchVisibilityMode VisibilityMode
	{
		get
		{
			return _visibilityMode;
		}
		set
		{
			lock (_syncLock)
			{
				if (_visibilityMode != value)
				{
					_visibilityMode = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool IsPinned
	{
		get
		{
			return _isPinned;
		}
		set
		{
			lock (_syncLock)
			{
				if (_isPinned != value)
				{
					_isPinned = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public MediaVisibilityMode MediaMode
	{
		get
		{
			return _mediaMode;
		}
		set
		{
			lock (_syncLock)
			{
				if (_mediaMode != value)
				{
					_mediaMode = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool ShowBrowserMediaInNotch
	{
		get
		{
			return _showBrowserMediaInNotch;
		}
		set
		{
			lock (_syncLock)
			{
				if (_showBrowserMediaInNotch != value)
				{
					_showBrowserMediaInNotch = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool ShowMediaWhenPlaybackStarts
	{
		get
		{
			return _showMediaWhenPlaybackStarts;
		}
		set
		{
			lock (_syncLock)
			{
				if (_showMediaWhenPlaybackStarts != value)
				{
					_showMediaWhenPlaybackStarts = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public int EventDurationSeconds
	{
		get
		{
			return _eventDurationSeconds;
		}
		set
		{
			lock (_syncLock)
			{
				int num = ((value == 2 || value == 5) ? value : 3);
				if (_eventDurationSeconds != num)
				{
					_eventDurationSeconds = num;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public OverMaximizedLook OverMaximizedLook
	{
		get
		{
			return _overMaximizedLook;
		}
		set
		{
			lock (_syncLock)
			{
				if (_overMaximizedLook != value)
				{
					_overMaximizedLook = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool ShowBriefEventsWhenHidden
	{
		get
		{
			return _showBriefEventsWhenHidden;
		}
		set
		{
			lock (_syncLock)
			{
				if (_showBriefEventsWhenHidden != value)
				{
					_showBriefEventsWhenHidden = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool HideInFullscreen
	{
		get
		{
			return _hideInFullscreen;
		}
		set
		{
			lock (_syncLock)
			{
				if (_hideInFullscreen != value)
				{
					_hideInFullscreen = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool HideInFullscreenApps
	{
		get
		{
			return HideInFullscreen;
		}
		set
		{
			lock (_syncLock)
			{
				HideInFullscreen = value;
			}
		}
	}

	public bool HideInFullscreenGames
	{
		get
		{
			return HideInFullscreen;
		}
		set
		{
			lock (_syncLock)
			{
				HideInFullscreen = value;
			}
		}
	}

	public bool IdlePrivacyReset
	{
		get
		{
			return _idlePrivacyReset;
		}
		set
		{
			lock (_syncLock)
			{
				if (_idlePrivacyReset != value)
				{
					_idlePrivacyReset = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool HideDuringPresentations
	{
		get
		{
			return _hideDuringPresentations;
		}
		set
		{
			lock (_syncLock)
			{
				if (_hideDuringPresentations != value)
				{
					_hideDuringPresentations = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public double AutoHideDelaySeconds
	{
		get
		{
			return _autoHideDelaySeconds;
		}
		set
		{
			lock (_syncLock)
			{
				double num = Math.Max(1.0, Math.Min(15.0, value));
				if (Math.Abs(_autoHideDelaySeconds - num) > 0.1)
				{
					_autoHideDelaySeconds = num;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool HideOnBatterySaver
	{
		get
		{
			return _hideOnBatterySaver;
		}
		set
		{
			lock (_syncLock)
			{
				if (_hideOnBatterySaver != value)
				{
					_hideOnBatterySaver = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool SyncFocusAssist
	{
		get
		{
			return _syncFocusAssist;
		}
		set
		{
			lock (_syncLock)
			{
				if (_syncFocusAssist != value)
				{
					_syncFocusAssist = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public bool HoverToRevealFullscreen
	{
		get
		{
			return _hoverToRevealFullscreen;
		}
		set
		{
			lock (_syncLock)
			{
				if (_hoverToRevealFullscreen != value)
				{
					_hoverToRevealFullscreen = value;
					Save();
					NotifyChanged();
				}
			}
		}
	}

	public List<string> FullscreenExceptionApps { get; set; }

	public Dictionary<string, EventPresentationMode> EventModes { get; set; }

	public List<AppVisibilityRule> AppRules { get; set; }

	public event EventHandler ConfigChanged;

	static NotchBehaviorConfig()
	{
		_syncLock = new object();
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
		_storagePath = Path.Combine(text, "notch_behavior.json");
	}

	public NotchBehaviorConfig()
	{
		EventModes = new Dictionary<string, EventPresentationMode>(StringComparer.OrdinalIgnoreCase);
		AppRules = new List<AppVisibilityRule>();
		FullscreenExceptionApps = new List<string>();
		InitializeDefaultEventModes();
	}

	private void InitializeDefaultEventModes()
	{
		EventModes["BluetoothConnected"] = EventPresentationMode.Brief;
		EventModes["BluetoothDisconnected"] = EventPresentationMode.Brief;
		EventModes["Volume"] = EventPresentationMode.Brief;
		EventModes["BatteryCharging"] = EventPresentationMode.Brief;
		EventModes["BatteryChargingStopped"] = EventPresentationMode.Brief;
		EventModes["BatteryFull"] = EventPresentationMode.Brief;
		EventModes["NetworkConnected"] = EventPresentationMode.Brief;
		EventModes["NetworkDisconnected"] = EventPresentationMode.Brief;
		EventModes["TimerComplete"] = EventPresentationMode.Brief;
		EventModes["ClipboardCopied"] = EventPresentationMode.Brief;
		EventModes["CompanionMessage"] = EventPresentationMode.Brief;
		EventModes["CalendarAlert"] = EventPresentationMode.Brief;
	}

	public EventPresentationMode GetEventPresentationMode(ActivityType type)
	{
		lock (_syncLock)
		{
			string key = type.ToString();
			if (EventModes.TryGetValue(key, out var value))
			{
				return value;
			}
			return EventPresentationMode.Brief;
		}
	}

	public void SetEventPresentationMode(ActivityType type, EventPresentationMode mode)
	{
		lock (_syncLock)
		{
			EventModes[type.ToString()] = mode;
			Save();
			NotifyChanged();
		}
	}

	public PerAppVisibility GetAppRule(string processName, string executablePath)
	{
		lock (_syncLock)
		{
			if (!string.IsNullOrEmpty(executablePath))
			{
				string b = executablePath.Trim();
				foreach (AppVisibilityRule appRule in AppRules)
				{
					if (!string.IsNullOrEmpty(appRule.ExecutablePath) && string.Equals(appRule.ExecutablePath.Trim(), b, StringComparison.OrdinalIgnoreCase))
					{
						return appRule.Rule;
					}
				}
			}
			if (!string.IsNullOrEmpty(processName))
			{
				string b2 = processName.Trim();
				foreach (AppVisibilityRule appRule2 in AppRules)
				{
					if (string.Equals(appRule2.ProcessName.Trim(), b2, StringComparison.OrdinalIgnoreCase))
					{
						return appRule2.Rule;
					}
				}
			}
			return PerAppVisibility.FollowDefault;
		}
	}

	public PerAppVisibility GetAppRule(string processName)
	{
		return GetAppRule(processName, null);
	}

	public void SetAppRule(string processName, PerAppVisibility rule)
	{
		SetAppRule(processName, null, rule);
	}

	public void ResetToDefaults()
	{
		lock (_syncLock)
		{
			_visibilityMode = NotchVisibilityMode.AlwaysVisible;
			_isPinned = false;
			_mediaMode = MediaVisibilityMode.BriefOnStart;
			_eventDurationSeconds = 3;
			_autoHideDelaySeconds = 3.0;
			_hideOnBatterySaver = false;
			_syncFocusAssist = true;
			_hoverToRevealFullscreen = true;
			_showBriefEventsWhenHidden = true;
			_overMaximizedLook = OverMaximizedLook.ArrowOnly;
			_showMediaWhenPlaybackStarts = true;
			_showBrowserMediaInNotch = true;
			_hideInFullscreen = true;
			_hideDuringPresentations = true;
			if (AppRules != null)
			{
				AppRules.Clear();
			}
			if (FullscreenExceptionApps != null)
			{
				FullscreenExceptionApps.Clear();
			}
			InitializeDefaultEventModes();
			Save();
			NotifyChanged();
		}
	}

	public static NotchBehaviorConfig LoadFromDisk()
	{
		return Load();
	}

	public void SetAppRule(string processName, string executablePath, PerAppVisibility rule)
	{
		lock (_syncLock)
		{
			string text = (processName ?? string.Empty).Trim();
			string text2 = (executablePath ?? string.Empty).Trim();
			AppVisibilityRule appVisibilityRule = null;
			foreach (AppVisibilityRule appRule in AppRules)
			{
				if ((!string.IsNullOrEmpty(text2) && string.Equals(appRule.ExecutablePath, text2, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrEmpty(text) && string.Equals(appRule.ProcessName, text, StringComparison.OrdinalIgnoreCase)))
				{
					appVisibilityRule = appRule;
					break;
				}
			}
			if (rule == PerAppVisibility.FollowDefault)
			{
				if (appVisibilityRule != null)
				{
					AppRules.Remove(appVisibilityRule);
				}
			}
			else if (appVisibilityRule != null)
			{
				appVisibilityRule.Rule = rule;
				if (!string.IsNullOrEmpty(text2))
				{
					appVisibilityRule.ExecutablePath = text2;
				}
			}
			else
			{
				AppRules.Add(new AppVisibilityRule(text, text2, rule));
			}
			Save();
			NotifyChanged();
		}
	}

	public void RemoveAppRule(string processName)
	{
		lock (_syncLock)
		{
			AppRules.RemoveAll((AppVisibilityRule r) => string.Equals(r.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
			Save();
			NotifyChanged();
		}
	}

	public void NotifyChanged()
	{
		EventHandler configChanged = this.ConfigChanged;
		if (configChanged != null)
		{
			try
			{
				configChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public void Save()
	{
		lock (_syncLock)
		{
			try
			{
				NotchBehaviorConfigDto notchBehaviorConfigDto = new NotchBehaviorConfigDto();
				notchBehaviorConfigDto.VisibilityMode = (int)VisibilityMode;
				notchBehaviorConfigDto.IsPinned = IsPinned;
				notchBehaviorConfigDto.MediaMode = (int)MediaMode;
				notchBehaviorConfigDto.ShowMediaWhenPlaybackStarts = ShowMediaWhenPlaybackStarts;
				notchBehaviorConfigDto.ShowBrowserMediaInNotch = ShowBrowserMediaInNotch;
				notchBehaviorConfigDto.BrowserMediaPolicyVersion = 1;
				notchBehaviorConfigDto.EventDurationSeconds = EventDurationSeconds;
				notchBehaviorConfigDto.ShowBriefEventsWhenHidden = ShowBriefEventsWhenHidden;
				notchBehaviorConfigDto.OverMaximizedLook = (int)OverMaximizedLook;
				notchBehaviorConfigDto.HideInFullscreen = HideInFullscreen;
				notchBehaviorConfigDto.HideDuringPresentations = HideDuringPresentations;
				notchBehaviorConfigDto.IdlePrivacyReset = IdlePrivacyReset;
				notchBehaviorConfigDto.AutoHideDelaySeconds = AutoHideDelaySeconds;
				notchBehaviorConfigDto.HideOnBatterySaver = HideOnBatterySaver;
				notchBehaviorConfigDto.SyncFocusAssist = SyncFocusAssist;
				notchBehaviorConfigDto.HoverToRevealFullscreen = HoverToRevealFullscreen;
				notchBehaviorConfigDto.FullscreenExceptionApps = ((FullscreenExceptionApps != null) ? new List<string>(FullscreenExceptionApps) : new List<string>());
				notchBehaviorConfigDto.EventModes = new Dictionary<string, int>();
				notchBehaviorConfigDto.AppRules = new List<AppVisibilityRuleDto>();
				NotchBehaviorConfigDto notchBehaviorConfigDto2 = notchBehaviorConfigDto;
				foreach (KeyValuePair<string, EventPresentationMode> eventMode in EventModes)
				{
					notchBehaviorConfigDto2.EventModes[eventMode.Key] = (int)eventMode.Value;
				}
				foreach (AppVisibilityRule appRule in AppRules)
				{
					notchBehaviorConfigDto2.AppRules.Add(new AppVisibilityRuleDto
					{
						ExecutablePath = appRule.ExecutablePath,
						ProcessName = appRule.ProcessName,
						Rule = (int)appRule.Rule
					});
				}
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				string content = javaScriptSerializer.Serialize(notchBehaviorConfigDto2);
				PersistenceHelper.SafeWriteAllText(_storagePath, content);
			}
			catch (Exception error)
			{
				PersistenceHelper.ReportSaveFailure(_storagePath, error);
			}
		}
	}

	private static NotchBehaviorConfig Load()
	{
		NotchBehaviorConfig notchBehaviorConfig = new NotchBehaviorConfig();
		try
		{
			if (File.Exists(_storagePath) || File.Exists(_storagePath + ".bak"))
			{
				string text = PersistenceHelper.SafeReadAllText(_storagePath);
				if (!string.IsNullOrEmpty(text))
				{
					JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
					NotchBehaviorConfigDto notchBehaviorConfigDto = javaScriptSerializer.Deserialize<NotchBehaviorConfigDto>(text);
					if (notchBehaviorConfigDto != null)
					{
						notchBehaviorConfig._visibilityMode = MigrateMode(notchBehaviorConfigDto.VisibilityMode);
						notchBehaviorConfig._isPinned = false;
						if (notchBehaviorConfigDto.IsPinned || notchBehaviorConfigDto.VisibilityMode == 0)
						{
							TransactionLogger.Info($"Notch behavior migrated: mode {notchBehaviorConfigDto.VisibilityMode} -> {notchBehaviorConfig._visibilityMode}, retired pin {notchBehaviorConfigDto.IsPinned} cleared.");
							notchBehaviorConfig._needsMigrationSave = true;
						}
						notchBehaviorConfig._mediaMode = (MediaVisibilityMode)notchBehaviorConfigDto.MediaMode;
						notchBehaviorConfig._showMediaWhenPlaybackStarts = notchBehaviorConfigDto.ShowMediaWhenPlaybackStarts;
						notchBehaviorConfig._showBrowserMediaInNotch = notchBehaviorConfigDto.BrowserMediaPolicyVersion < 1 || (notchBehaviorConfigDto.ShowBrowserMediaInNotch ?? true);
						if (notchBehaviorConfigDto.BrowserMediaPolicyVersion < 1)
						{
							notchBehaviorConfig._needsMigrationSave = true;
						}
						notchBehaviorConfig._eventDurationSeconds = ((notchBehaviorConfigDto.EventDurationSeconds > 0) ? notchBehaviorConfigDto.EventDurationSeconds : 3);
						notchBehaviorConfig._showBriefEventsWhenHidden = notchBehaviorConfigDto.ShowBriefEventsWhenHidden;
						notchBehaviorConfig._overMaximizedLook = ((notchBehaviorConfigDto.OverMaximizedLook == 1) ? OverMaximizedLook.MiniNotch : OverMaximizedLook.ArrowOnly);
						notchBehaviorConfig._hideInFullscreen = notchBehaviorConfigDto.HideInFullscreen ?? (notchBehaviorConfigDto.HideInFullscreenApps || notchBehaviorConfigDto.HideInFullscreenGames);
						if (!notchBehaviorConfigDto.HideInFullscreen.HasValue)
						{
							notchBehaviorConfig._needsMigrationSave = true;
						}
						notchBehaviorConfig._hideDuringPresentations = notchBehaviorConfigDto.HideDuringPresentations;
						notchBehaviorConfig._idlePrivacyReset = notchBehaviorConfigDto.IdlePrivacyReset;
						notchBehaviorConfig._autoHideDelaySeconds = ((notchBehaviorConfigDto.AutoHideDelaySeconds > 0.0) ? notchBehaviorConfigDto.AutoHideDelaySeconds : 3.0);
						notchBehaviorConfig._hideOnBatterySaver = notchBehaviorConfigDto.HideOnBatterySaver;
						notchBehaviorConfig._syncFocusAssist = notchBehaviorConfigDto.SyncFocusAssist;
						notchBehaviorConfig._hoverToRevealFullscreen = notchBehaviorConfigDto.HoverToRevealFullscreen;
						notchBehaviorConfig.FullscreenExceptionApps = notchBehaviorConfigDto.FullscreenExceptionApps ?? new List<string>();
						if (notchBehaviorConfigDto.EventModes != null)
						{
							foreach (KeyValuePair<string, int> eventMode in notchBehaviorConfigDto.EventModes)
							{
								notchBehaviorConfig.EventModes[eventMode.Key] = (EventPresentationMode)eventMode.Value;
							}
						}
						if (notchBehaviorConfigDto.AppRules != null)
						{
							foreach (AppVisibilityRuleDto appRule in notchBehaviorConfigDto.AppRules)
							{
								notchBehaviorConfig.AppRules.Add(new AppVisibilityRule(appRule.ProcessName, appRule.ExecutablePath, (PerAppVisibility)appRule.Rule));
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not load NotchBehaviorConfig: " + ex.Message);
		}
		if (notchBehaviorConfig._needsMigrationSave)
		{
			notchBehaviorConfig.Save();
		}
		PersistenceHelper.RegisterExitFlush("behavior", notchBehaviorConfig.Save);
		return notchBehaviorConfig;
	}

	private static NotchVisibilityMode MigrateMode(int stored)
	{
		if (stored == 0)
		{
			return NotchVisibilityMode.Smart;
		}
		if (Enum.IsDefined(typeof(NotchVisibilityMode), stored))
		{
			return (NotchVisibilityMode)stored;
		}
		return NotchVisibilityMode.AlwaysVisible;
	}
}
