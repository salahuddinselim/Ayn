using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools.Message;

namespace WindowsBuddy.BuddyCapsule.Activity;

public sealed class PillActivityArbiter
{
	private sealed class Deadline
	{
		private readonly PillActivityArbiter _owner;

		private readonly Action<object> _callback;

		public object State;

		public DateTime DueUtc = DateTime.MaxValue;

		public Deadline(PillActivityArbiter owner, Action<object> callback)
		{
			_owner = owner;
			_callback = callback;
		}

		public void Change(TimeSpan due, TimeSpan period)
		{
			DueUtc = ((due < TimeSpan.Zero) ? DateTime.MaxValue : (DateTime.UtcNow + due));
			_owner.Reschedule();
		}

		public void Change(int dueMs, int periodMs)
		{
			DueUtc = ((dueMs < 0) ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(dueMs));
			_owner.Reschedule();
		}

		public bool Fire(DateTime now)
		{
			if (DueUtc > now)
			{
				return false;
			}
			DueUtc = DateTime.MaxValue;
			try
			{
				_callback(State);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[Notch] timeout handler failed: " + ex.GetType().Name);
			}
			return true;
		}
	}

	public const double MediaWideSeconds = 4.0;

	private static readonly Lazy<PillActivityArbiter> _instance = new Lazy<PillActivityArbiter>(() => new PillActivityArbiter());

	private readonly object _syncLock = new object();

	private readonly Queue<PillActivitySnapshot> _transientQueue = new Queue<PillActivitySnapshot>();

	private DispatcherTimer _clock;

	private readonly Deadline _transientExpiryTimer;

	private readonly Deadline _timerPauseExpiryTimer;

	private bool _timerPauseHoldExpired;

	private bool _isTimerPauseTimerActive;

	private long _activeTimerPauseSequence;

	private readonly Deadline _mediaPauseExpiryTimer;

	private readonly Deadline _revealTimer;

	private readonly Deadline _mediaWideTimer;

	private DateTime _mediaWideUntil = DateTime.MinValue;

	private string _mediaTrackKey;

	private bool _mediaPauseHoldExpired;

	private bool _isMediaPauseTimerActive;

	private long _activeMediaPauseSequence;

	private long _globalSequence = 1L;

	private long _activeTransientSequence;

	private PillActivitySnapshot _criticalSlot;

	private PillActivitySnapshot _currentTransient;

	private PillActivitySnapshot _messageSession;

	private PillActivitySnapshot _customSession;

	private PillActivitySnapshot _mediaSession;

	private PillActivitySnapshot _timerSession;

	private PillActivitySnapshot _ambientClock;

	private DateTime _lastBluetoothTime = DateTime.MinValue;

	private string _lastBluetoothDevice;

	private readonly List<ActivityTimelineEntry> _timeline = new List<ActivityTimelineEntry>();

	private PillActivitySnapshot _petHeldMedia;

	private bool _suppressed;

	private long _hoveredReminderSequence;

	private TimeSpan _reminderRemaining;

	private TimeSpan _currentTransientDuration;

	public static readonly TimeSpan ReminderDuration = TimeSpan.FromSeconds(8.0);

	private static readonly TimeSpan ReminderRetry = TimeSpan.FromSeconds(3.0);

	private readonly List<PillActivitySnapshot> _reminderQueue = new List<PillActivitySnapshot>();

	private Deadline _reminderRetryTimer;

	public static Func<bool> ReminderPresentableProbe;

	public static Func<string, bool> ReminderStillDue;

	private ActivityType? _revealType;

	private bool _revealPersistent;

	private bool _revealTiedToTransient;

	private string _lastMediaRevealSig;

	private string _lastTimerRevealSig;

	private string _lastPinnedRevealId;

	public static PillActivityArbiter Instance => _instance.Value;

	public long CurrentSequenceNumber
	{
		get
		{
			lock (_syncLock)
			{
				return _globalSequence;
			}
		}
	}

	public PillActivitySnapshot CurrentActivity
	{
		get
		{
			lock (_syncLock)
			{
				return ResolveCurrentSnapshotLocked();
			}
		}
	}

	public string MessageSessionId
	{
		get
		{
			lock (_syncLock)
			{
				return (_messageSession != null) ? _messageSession.Id : null;
			}
		}
	}

	public PillActivitySnapshot CurrentSnapshot => CurrentActivity;

	public ActivityTier CurrentTier
	{
		get
		{
			lock (_syncLock)
			{
				return ResolveCurrentSnapshotLocked().Tier;
			}
		}
	}

	public bool HasActiveNoticeOrSession
	{
		get
		{
			lock (_syncLock)
			{
				if (_criticalSlot != null)
				{
					return true;
				}
				if (_currentTransient != null)
				{
					return true;
				}
				if (_messageSession != null)
				{
					return true;
				}
				if (_customSession != null)
				{
					return true;
				}
				PillActivitySnapshot pillActivitySnapshot = ResolveTier2SessionLocked();
				if (pillActivitySnapshot != null)
				{
					return true;
				}
				return false;
			}
		}
	}

	public bool HasActiveTransient
	{
		get
		{
			lock (_syncLock)
			{
				return _currentTransient != null;
			}
		}
	}

	private static bool PetOn => PetPreferences.Instance.Enabled;

	public bool IsSuppressed
	{
		get
		{
			lock (_syncLock)
			{
				return _suppressed;
			}
		}
	}

	public int PendingReminderCount
	{
		get
		{
			lock (_syncLock)
			{
				return _reminderQueue.Count;
			}
		}
	}

	public bool IsMediaWide => DateTime.UtcNow < _mediaWideUntil;

	public bool IsRevealActive => _revealType.HasValue;

	public double RevealRemainingSeconds
	{
		get
		{
			if (!_revealType.HasValue)
			{
				return 0.0;
			}
			DateTime dateTime = (_revealTiedToTransient ? _transientExpiryTimer.DueUtc : _revealTimer.DueUtc);
			if (dateTime == DateTime.MaxValue)
			{
				return 0.0;
			}
			return Math.Max(0.0, (dateTime - DateTime.UtcNow).TotalSeconds);
		}
	}

	public event EventHandler<PillActivityChangedEventArgs> ActivityChanged;

	public event EventHandler<TimerUpdateEventArgs> TimerUpdated;

	public event Action<bool, int, string> RevealChanged;

	public IReadOnlyList<ActivityTimelineEntry> GetTimeline()
	{
		lock (_syncLock)
		{
			return _timeline.ToArray();
		}
	}

	private static bool PetOwnsPassive(string kind)
	{
		if (!PetOn)
		{
			return false;
		}
		PetEngine.ObservePassive(kind);
		return true;
	}

	private bool PetHoldsMedia(PillActivitySnapshot snapshot)
	{
		if (!PetOn)
		{
			_petHeldMedia = null;
			return false;
		}
		_petHeldMedia = snapshot;
		return true;
	}

	private void OnPetPreferencesChanged()
	{
		Application current = Application.Current;
		if (current != null && current.Dispatcher != null && !current.Dispatcher.CheckAccess())
		{
			try
			{
				current.Dispatcher.BeginInvoke(new Action(OnPetPreferencesChanged));
				return;
			}
			catch
			{
				return;
			}
		}
		if (PetOn)
		{
			ClearMedia();
		}
		else if (_petHeldMedia != null)
		{
			PillActivitySnapshot petHeldMedia = _petHeldMedia;
			_petHeldMedia = null;
			PublishMedia(petHeldMedia);
		}
	}

	private PillActivityArbiter()
	{
		PetPreferences instance = PetPreferences.Instance;
		EventHandler value = delegate
		{
			OnPetPreferencesChanged();
		};
		instance.Changed += value;
		_transientExpiryTimer = new Deadline(this, OnTransientExpiryTimerCallback);
		_timerPauseExpiryTimer = new Deadline(this, OnTimerPauseExpiryCallback);
		_mediaPauseExpiryTimer = new Deadline(this, OnMediaPauseExpiryCallback);
		_revealTimer = new Deadline(this, OnRevealDeadline);
		_mediaWideTimer = new Deadline(this, OnMediaWideDeadline);
		_reminderRetryTimer = new Deadline(this, OnReminderRetry);
		DateTime utcNow = DateTime.UtcNow;
		_ambientClock = new PillActivitySnapshot("canonical_clock", "clock", ActivityTier.Tier3_Ambient, ActivityType.RestingClock, DateTime.Now.ToString("h:mm tt"), DateTime.Now.ToString("dddd · MMM d"), string.Empty, null, null, null, null, utcNow, null, 1L, CollapsedActivityProfile.Resting, isPlaying: false, null, null, null, "clock", null);
	}

	public void SetSuppressed(bool suppressed)
	{
		lock (_syncLock)
		{
			if (_suppressed != suppressed)
			{
				PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
				_suppressed = suppressed;
				if (suppressed && IsCalendarReminder(_currentTransient))
				{
					PillActivitySnapshot currentTransient = _currentTransient;
					CancelTransientTimerLocked();
					_currentTransient = null;
					_activeTransientSequence = 0L;
					_reminderQueue.Insert(0, currentTransient);
					ScheduleReminderRetryLocked();
				}
				else if (!suppressed && _reminderQueue.Count > 0 && !IsCalendarReminder(_currentTransient) && ShowNextReminderLocked(_currentTransient ?? pillActivitySnapshot))
				{
					return;
				}
				PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
				if (pillActivitySnapshot != pillActivitySnapshot2)
				{
					NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, isUpdateOnly: false, suppressed ? ActivityTransitionType.ExitToRight : ActivityTransitionType.EnterFromLeft);
				}
			}
		}
	}

	private PillActivitySnapshot ResolveCurrentSnapshotLocked()
	{
		if (_suppressed)
		{
			return _ambientClock;
		}
		if (_criticalSlot != null)
		{
			return _criticalSlot;
		}
		if (_currentTransient != null)
		{
			return _currentTransient;
		}
		PillActivitySnapshot pillActivitySnapshot = ResolveTier2SessionLocked();
		if (pillActivitySnapshot != null)
		{
			return pillActivitySnapshot;
		}
		return _ambientClock;
	}

	private PillActivitySnapshot ResolveTier2SessionLocked()
	{
		if (_messageSession != null)
		{
			return _messageSession;
		}
		if (_customSession != null)
		{
			return _customSession;
		}
		bool flag = _mediaSession != null && (_mediaSession.IsPlaying || !_mediaPauseHoldExpired);
		bool flag2 = _timerSession != null && (_timerSession.IsPlaying || !_timerPauseHoldExpired);
		if (!flag && !flag2)
		{
			return null;
		}
		if (flag && !flag2)
		{
			return _mediaSession;
		}
		if (!flag && flag2)
		{
			return _timerSession;
		}
		bool isPlaying = _mediaSession.IsPlaying;
		bool isPlaying2 = _timerSession.IsPlaying;
		if (isPlaying)
		{
			return _mediaSession;
		}
		if (isPlaying2)
		{
			return _timerSession;
		}
		return _mediaSession;
	}

	public void SetCriticalAlert(PillActivitySnapshot alert)
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
				_criticalSlot = ((alert != null) ? alert.WithSequenceNumber(++_globalSequence) : null);
				PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
				if (pillActivitySnapshot != pillActivitySnapshot2)
				{
					LogTimelineEvent((alert != null) ? "CRITICAL_SET" : "CRITICAL_CLEARED", pillActivitySnapshot2, pillActivitySnapshot2.Title);
					NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, isUpdateOnly: false, ActivityTransitionType.EnterFromLeft);
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				SetCriticalAlert(alert);
			});
		}
		catch
		{
		}
	}

	public void SetBatteryLowAlert(int percent)
	{
		PillActivitySnapshot criticalAlert = new PillActivitySnapshot("bat_low", "battery_low", ActivityTier.Tier0_Critical, ActivityType.BatteryLow, "Battery low", $"{percent}%", $"{percent}%", (double sz, Brush br) => VectorIcons.CreateBatteryIcon(13.0, br, NotchInk.Danger, percent, isCharging: false), null, NotchInk.Danger, null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.System, isPlaying: false, null, null, null, "system", percent);
		SetCriticalAlert(criticalAlert);
	}

	public void ClearBatteryLowAlert()
	{
		lock (_syncLock)
		{
			if (_criticalSlot != null && _criticalSlot.Type == ActivityType.BatteryLow)
			{
				SetCriticalAlert(null);
			}
		}
	}

	public static TimeSpan EventDuration()
	{
		int num = NotchBehaviorConfig.Instance?.EventDurationSeconds ?? 3;
		return TimeSpan.FromSeconds((num > 0) ? num : 3);
	}

	public void PublishTransient(PillActivitySnapshot snapshot, TimeSpan duration)
	{
		PublishTransient(snapshot, duration, userRequested: false);
	}

	private void PublishTransient(PillActivitySnapshot snapshot, TimeSpan duration, bool userRequested)
	{
		if (snapshot == null)
		{
			return;
		}
		NotchBehaviorConfig instance = NotchBehaviorConfig.Instance;
		if (!userRequested && instance != null && instance.GetEventPresentationMode(snapshot.Type) == EventPresentationMode.Off)
		{
			return;
		}
		if (snapshot.Type != ActivityType.CompanionMessage && !IsCalendarReminder(snapshot))
		{
			duration = EventDuration();
		}
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (!userRequested && IsCalendarReminder(_currentTransient) && !IsCalendarReminder(snapshot))
				{
					LogTimelineEvent("TRANSIENT_HELD_BY_REMINDER", snapshot, snapshot.Title);
				}
				else
				{
					DateTime utcNow = DateTime.UtcNow;
					snapshot = snapshot.WithSequenceNumber(++_globalSequence).WithExpiresAt(utcNow.Add(duration));
					if (snapshot.Type == ActivityType.Volume && _currentTransient != null && _currentTransient.Type == ActivityType.Volume)
					{
						PillActivitySnapshot currentTransient = _currentTransient;
						_currentTransient = snapshot;
						_activeTransientSequence = snapshot.SequenceNumber;
						_currentTransientDuration = duration;
						RestartTransientTimerLocked(duration, snapshot.SequenceNumber);
						LogTimelineEvent("VOLUME_COALESCED", snapshot, snapshot.Title);
						NotchStabilityCounters.VolumeUpdates++;
						NotifyTransitionLocked(currentTransient, _currentTransient, isUpdateOnly: true, ActivityTransitionType.InPlaceUpdate);
						NotifyVisibilityEngine(snapshot);
					}
					else
					{
						if (snapshot.Type == ActivityType.BluetoothConnected || snapshot.Type == ActivityType.BluetoothDisconnected)
						{
							if (string.Equals(_lastBluetoothDevice, snapshot.Title, StringComparison.OrdinalIgnoreCase) && (utcNow - _lastBluetoothTime).TotalSeconds < 3.0)
							{
								return;
							}
							_lastBluetoothDevice = snapshot.Title;
							_lastBluetoothTime = utcNow;
						}
						if (_currentTransient != null)
						{
							PillActivitySnapshot currentTransient2 = _currentTransient;
							_transientQueue.Clear();
							_currentTransient = snapshot;
							_activeTransientSequence = snapshot.SequenceNumber;
							_currentTransientDuration = duration;
							RestartTransientTimerLocked(duration, snapshot.SequenceNumber);
							LogTimelineEvent("TRANSIENT_UPDATED", snapshot, snapshot.Title);
							NotifyVisibilityEngine(snapshot);
							bool flag = currentTransient2.Type == snapshot.Type;
							if (flag && snapshot.Type == ActivityType.Volume)
							{
								NotchStabilityCounters.VolumeUpdates++;
							}
							NotifyTransitionLocked(currentTransient2, _currentTransient, flag, flag ? ActivityTransitionType.InPlaceUpdate : ActivityTransitionType.EnterFromLeft);
						}
						else
						{
							if (_isMediaPauseTimerActive)
							{
								_mediaPauseHoldExpired = true;
								_isMediaPauseTimerActive = false;
								_activeMediaPauseSequence = 0L;
								_mediaPauseExpiryTimer.Change(-1, -1);
							}
							PillActivitySnapshot oldAct = ResolveCurrentSnapshotLocked();
							_currentTransient = snapshot;
							_activeTransientSequence = snapshot.SequenceNumber;
							_currentTransientDuration = duration;
							RestartTransientTimerLocked(duration, snapshot.SequenceNumber);
							LogTimelineEvent("TRANSIENT_ACTIVATED", snapshot, snapshot.Title);
							NotifyVisibilityEngine(snapshot);
							NotifyTransitionLocked(oldAct, _currentTransient, isUpdateOnly: false, ActivityTransitionType.EnterFromLeft);
						}
					}
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				PublishTransient(snapshot, duration, userRequested);
			});
		}
		catch
		{
		}
	}

	private static string OneLine(string s)
	{
		return Regex.Replace(s ?? "", "\\s+", " ").Trim();
	}

	public void PublishClipboardCopied(string label = null)
	{
		if (PetOwnsPassive("clipboard"))
		{
			return;
		}
		NotchConfig instance = NotchConfig.Instance;
		if (instance != null && !instance.ShowClipboardToast)
		{
			return;
		}
		string text;
		if (instance != null && instance.MaskClipboardToast)
		{
			text = (string.Equals(label, "Image", StringComparison.OrdinalIgnoreCase) ? "Image" : "Text");
		}
		else
		{
			text = ((!string.IsNullOrEmpty(label)) ? label : "Clipboard");
			text = OneLine(text);
			if (text.Length > 32)
			{
				text = text.Substring(0, 31).TrimEnd() + "…";
			}
		}
		PillActivitySnapshot snapshot = new PillActivitySnapshot("clipboard_copied", "clipboard", ActivityTier.Tier1_Transient, ActivityType.ClipboardCopied, "Copied", text, "Copied", (double sz, Brush br) => NotchGlyphs.Create("clipboard.check", sz, br), null, null, null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Clipboard, isPlaying: false, null, null, null, "clipboard", null);
		PublishTransient(snapshot, TimeSpan.FromSeconds(3.0));
	}

	public void PublishVolume(int volumePercent, bool isMuted)
	{
		if (!PetOwnsPassive("volume"))
		{
			PillActivitySnapshot snapshot = new PillActivitySnapshot("volume_feedback", "volume", ActivityTier.Tier1_Transient, ActivityType.Volume, isMuted ? "Muted" : (volumePercent + "%"), "Volume", volumePercent + "%", (double sz, Brush br) => VectorIcons.CreateVolumeIcon(sz, br, isMuted, volumePercent), null, isMuted ? NotchInk.Danger : null, (double)volumePercent / 100.0, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Volume, isPlaying: false, null, null, null, "system", volumePercent);
			PublishTransient(snapshot, TimeSpan.FromSeconds(3.0));
		}
	}

	public void PublishHydrationNudge(string detail = null)
	{
		PillActivitySnapshot snapshot = new PillActivitySnapshot("hyd_nudge_" + DateTime.UtcNow.Ticks, "hydration", ActivityTier.Tier1_Transient, ActivityType.HydrationReminder, "Drink some water", detail ?? "", "Drink some water", (double sz, Brush br) => NotchGlyphs.Create("drop.fill", sz, NotchGlyphs.WaterBlueBrush), null, NotchGlyphs.WaterBlueBrush, null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Reminder, isPlaying: false, null, null, null, "timer", null);
		PublishTransient(snapshot, TimeSpan.FromSeconds(ReminderDelivery.ReminderSeconds));
	}

	public void PublishTimerComplete(string title = "Timer done", string detail = null)
	{
		lock (_syncLock)
		{
			_timerSession = null;
		}
		PillActivitySnapshot snapshot = new PillActivitySnapshot("timer_complete_" + DateTime.UtcNow.Ticks, "timer_complete", ActivityTier.Tier1_Transient, ActivityType.TimerComplete, title, detail ?? "", title, (double sz, Brush br) => NotchGlyphs.Create("timer", sz, br), null, null, null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Reminder, isPlaying: false, null, null, null, "timer", null);
		PublishTransient(snapshot, TimeSpan.FromSeconds(ReminderDelivery.ReminderSeconds));
	}

	public void PublishBluetooth(string deviceName, bool isConnected, Brush accent = null)
	{
		if (!PetOwnsPassive("bluetooth"))
		{
			PillActivitySnapshot snapshot = new PillActivitySnapshot("bt_" + (deviceName ?? "device"), "bluetooth", ActivityTier.Tier1_Transient, isConnected ? ActivityType.BluetoothConnected : ActivityType.BluetoothDisconnected, deviceName ?? "Device", isConnected ? "Connected" : "Disconnected", isConnected ? "Connected" : "Disconnected", (double sz, Brush br) => VectorIcons.CreateHeadphonesIcon(sz, br), null, accent, null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Bluetooth, isPlaying: false, null, null, null, "system", deviceName);
			PublishTransient(snapshot, TimeSpan.FromSeconds(3.0));
		}
	}

	public void PublishNetworkState(string networkName, bool isConnected)
	{
		if (!PetOwnsPassive("network"))
		{
			PillActivitySnapshot snapshot = new PillActivitySnapshot("net_status", "network", ActivityTier.Tier1_Transient, isConnected ? ActivityType.NetworkConnected : ActivityType.NetworkDisconnected, isConnected ? "Wi-Fi connected" : "Offline", (!isConnected) ? "No network connection" : (string.IsNullOrEmpty(networkName) ? "Connected" : networkName), isConnected ? "Connected" : "Offline", (double sz, Brush br) => VectorIcons.CreateWiFiIcon(sz, br), null, isConnected ? NotchInk.Success : NotchInk.Danger, null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.WiFi, isPlaying: false, null, null, null, "system", networkName);
			PublishTransient(snapshot, TimeSpan.FromSeconds(3.0));
		}
	}

	public void PublishBatteryChargingState(string title, string percent, bool isCharging, bool isFull)
	{
		if (!PetOwnsPassive((isCharging || isFull) ? "charging" : "unplugged"))
		{
			PillActivitySnapshot snapshot = new PillActivitySnapshot(isFull ? "bat_full" : "bat_charging", "battery", ActivityTier.Tier1_Transient, isFull ? ActivityType.BatteryFull : ActivityType.BatteryCharging, title, percent, percent, (double sz, Brush br) => VectorIcons.CreateLightningIcon(sz, NotchInk.Success), null, NotchInk.Success, null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Charging, isPlaying: false, null, null, null, "system", percent);
			PublishTransient(snapshot, TimeSpan.FromSeconds(3.0));
		}
	}

	internal static bool IsCalendarReminder(PillActivitySnapshot snap)
	{
		if (snap != null && snap.Type == ActivityType.CalendarAlert)
		{
			return ((snap.Payload as string) ?? "").StartsWith("r|", StringComparison.Ordinal);
		}
		return false;
	}

	internal static bool IsHoldable(PillActivitySnapshot snap)
	{
		if (!IsCalendarReminder(snap))
		{
			if (snap != null)
			{
				return ReminderDelivery.IsReminder(snap.Type);
			}
			return false;
		}
		return true;
	}

	internal void SetMessageHover(string id, bool hovering)
	{
		SetCalendarReminderHover(id, hovering);
	}

	internal void SetCalendarReminderHover(string id, bool hovering)
	{
		lock (_syncLock)
		{
			if (!IsHoldable(_currentTransient) || _currentTransient.Id != id)
			{
				return;
			}
			if (hovering)
			{
				if (_hoveredReminderSequence != _currentTransient.SequenceNumber)
				{
					_hoveredReminderSequence = _currentTransient.SequenceNumber;
					_reminderRemaining = _transientExpiryTimer.DueUtc - DateTime.UtcNow;
					if (_reminderRemaining < TimeSpan.FromMilliseconds(1.0))
					{
						_reminderRemaining = TimeSpan.FromMilliseconds(1.0);
					}
					CancelTransientTimerLocked();
					if (_revealType.HasValue)
					{
						RaiseReveal(active: true, -1, "Reminder hover");
					}
				}
			}
			else if (_hoveredReminderSequence == _currentTransient.SequenceNumber)
			{
				_hoveredReminderSequence = 0L;
				_currentTransient = _currentTransient.WithExpiresAt(DateTime.UtcNow + _reminderRemaining);
				RestartTransientTimerLocked(_reminderRemaining, _currentTransient.SequenceNumber);
				if (_revealType.HasValue)
				{
					RaiseReveal(active: true, (int)_reminderRemaining.TotalMilliseconds + 1, "Reminder resumed");
				}
			}
		}
	}

	private bool ReminderPresentableLocked()
	{
		if (_suppressed)
		{
			return false;
		}
		try
		{
			string reason;
			return ReminderPresentableProbe?.Invoke() ?? NotchVisibilityEngine.Instance?.WouldShowEvent(out reason) ?? true;
		}
		catch
		{
			return true;
		}
	}

	private static bool StillDue(PillActivitySnapshot s)
	{
		Func<string, bool> reminderStillDue = ReminderStillDue;
		if (reminderStillDue == null)
		{
			return true;
		}
		try
		{
			return reminderStillDue(s.Payload as string);
		}
		catch
		{
			return true;
		}
	}

	private void ScheduleReminderRetryLocked()
	{
		if (_reminderQueue.Count == 0)
		{
			_reminderRetryTimer.Change(-1, -1);
		}
		else if (_reminderRetryTimer.DueUtc == DateTime.MaxValue)
		{
			_reminderRetryTimer.Change(ReminderRetry, TimeSpan.FromMilliseconds(-1.0));
		}
	}

	private void OnReminderRetry(object state)
	{
		lock (_syncLock)
		{
			_reminderRetryTimer.Change(-1, -1);
			if (!IsCalendarReminder(_currentTransient))
			{
				ShowNextReminderLocked(null);
			}
			ScheduleReminderRetryLocked();
		}
	}

	private bool ShowNextReminderLocked(PillActivitySnapshot old)
	{
		_reminderQueue.RemoveAll((PillActivitySnapshot s) => !StillDue(s));
		if (_reminderQueue.Count == 0 || !ReminderPresentableLocked())
		{
			ScheduleReminderRetryLocked();
			return false;
		}
		PillActivitySnapshot snap = _reminderQueue[0];
		_reminderQueue.RemoveAt(0);
		ActivateReminderLocked(snap, old);
		ScheduleReminderRetryLocked();
		return true;
	}

	private void ActivateReminderLocked(PillActivitySnapshot snap, PillActivitySnapshot old)
	{
		if (_isMediaPauseTimerActive)
		{
			_mediaPauseHoldExpired = true;
			_isMediaPauseTimerActive = false;
			_activeMediaPauseSequence = 0L;
			_mediaPauseExpiryTimer.Change(-1, -1);
		}
		PillActivitySnapshot oldAct = old ?? ResolveCurrentSnapshotLocked();
		snap = snap.WithSequenceNumber(++_globalSequence).WithExpiresAt(DateTime.UtcNow.Add(ReminderDuration));
		_hoveredReminderSequence = 0L;
		_currentTransient = snap;
		_activeTransientSequence = snap.SequenceNumber;
		_transientQueue.Clear();
		RestartTransientTimerLocked(ReminderDuration, snap.SequenceNumber);
		LogTimelineEvent("REMINDER_SHOWN", snap, snap.Title);
		NotifyVisibilityEngine(snap);
		NotifyTransitionLocked(oldAct, _currentTransient, isUpdateOnly: false, ActivityTransitionType.EnterFromLeft);
	}

	public bool GetReminderProgress(string id, out TimeSpan remaining, out bool paused)
	{
		TimeSpan total;
		return GetMessageProgress(id, out remaining, out total, out paused);
	}

	public bool GetMessageProgress(string id, out TimeSpan remaining, out TimeSpan total, out bool paused)
	{
		lock (_syncLock)
		{
			remaining = TimeSpan.Zero;
			total = TimeSpan.Zero;
			paused = false;
			if (_currentTransient == null || _currentTransient.Id != id)
			{
				return false;
			}
			total = (IsCalendarReminder(_currentTransient) ? ReminderDuration : (ReminderDelivery.IsReminder(_currentTransient.Type) ? TimeSpan.FromSeconds(ReminderDelivery.ReminderSeconds) : _currentTransientDuration));
			if (total <= TimeSpan.Zero)
			{
				return false;
			}
			paused = IsHoldable(_currentTransient) && _hoveredReminderSequence == _currentTransient.SequenceNumber;
			remaining = (paused ? _reminderRemaining : (_transientExpiryTimer.DueUtc - DateTime.UtcNow));
			if (remaining < TimeSpan.Zero)
			{
				remaining = TimeSpan.Zero;
			}
			if (remaining > total)
			{
				remaining = total;
			}
			return true;
		}
	}

	public void PublishCalendarAlert(string title, string subtitle, string itemId)
	{
		PillActivitySnapshot snap = new PillActivitySnapshot("cal_" + (itemId ?? "alert"), "calendar", ActivityTier.Tier1_Transient, ActivityType.CalendarAlert, title ?? "Calendar Alert", subtitle ?? "Upcoming", subtitle ?? "Upcoming", (double sz, Brush br) => VectorIcons.CreateCalendarIcon(sz, br), null, new SolidColorBrush(Color.FromRgb(79, 157, 122)), null, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.System, isPlaying: false, null, null, null, "calendar", itemId);
		if (!IsCalendarReminder(snap))
		{
			PublishTransient(snap, TimeSpan.FromSeconds(3.0));
			return;
		}
		Application current = Application.Current;
		if (current != null && current.Dispatcher != null && !current.Dispatcher.CheckAccess())
		{
			try
			{
				current.Dispatcher.BeginInvoke((Action)delegate
				{
					PublishCalendarAlert(title, subtitle, itemId);
				});
				return;
			}
			catch
			{
				return;
			}
		}
		NotchBehaviorConfig instance = NotchBehaviorConfig.Instance;
		if (instance != null && instance.GetEventPresentationMode(ActivityType.CalendarAlert) == EventPresentationMode.Off)
		{
			return;
		}
		lock (_syncLock)
		{
			if (_reminderQueue.Exists((PillActivitySnapshot s) => s.Id == snap.Id) || (_currentTransient != null && _currentTransient.Id == snap.Id))
			{
				return;
			}
			if (IsCalendarReminder(_currentTransient))
			{
				_reminderQueue.Add(snap);
				LogTimelineEvent("REMINDER_QUEUED", snap, snap.Title);
				NotifyTransitionLocked(_currentTransient, _currentTransient, isUpdateOnly: true, ActivityTransitionType.InPlaceUpdate);
				return;
			}
			_reminderQueue.Add(snap);
			if (!ShowNextReminderLocked(_currentTransient))
			{
				LogTimelineEvent("REMINDER_WAITING_FOR_NOTCH", snap, snap.Title);
			}
		}
	}

	public void DismissCurrentTransient(bool force = true)
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_currentTransient != null)
				{
					CancelTransientTimerLocked();
					PillActivitySnapshot currentTransient = _currentTransient;
					_currentTransient = null;
					_activeTransientSequence = 0L;
					_transientQueue.Clear();
					if (!IsCalendarReminder(currentTransient) || !ShowNextReminderLocked(currentTransient))
					{
						PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
						LogTimelineEvent("TRANSIENT_DISMISSED_MANUAL", pillActivitySnapshot, pillActivitySnapshot.Title);
						NotifyTransitionLocked(currentTransient, pillActivitySnapshot, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
					}
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				DismissCurrentTransient(force);
			});
		}
		catch
		{
		}
	}

	private void OnTransientExpiryTimerCallback(object state)
	{
		if (state == null || !(state is long))
		{
			return;
		}
		long num = (long)state;
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_currentTransient != null && num == _activeTransientSequence && _currentTransient.SequenceNumber == num)
				{
					CancelTransientTimerLocked();
					PillActivitySnapshot currentTransient = _currentTransient;
					_currentTransient = null;
					_activeTransientSequence = 0L;
					_transientQueue.Clear();
					if (!IsCalendarReminder(currentTransient) || !ShowNextReminderLocked(currentTransient))
					{
						PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
						LogTimelineEvent("TRANSIENT_EXPIRED", pillActivitySnapshot, pillActivitySnapshot.Title);
						NotifyTransitionLocked(currentTransient, pillActivitySnapshot, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
					}
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				OnTransientExpiryTimerCallback(state);
			});
		}
		catch
		{
		}
	}

	private void CancelTransientTimerLocked()
	{
		_transientExpiryTimer.State = null;
		_transientExpiryTimer.Change(-1, -1);
	}

	private void RestartTransientTimerLocked(TimeSpan duration, long sequenceNumber)
	{
		if (duration.TotalMilliseconds > 0.0 && sequenceNumber > 0)
		{
			_transientExpiryTimer.State = sequenceNumber;
			_transientExpiryTimer.Change(duration, TimeSpan.FromMilliseconds(-1.0));
		}
		else
		{
			CancelTransientTimerLocked();
		}
	}

	private void Reschedule()
	{
		Application current = Application.Current;
		Dispatcher dispatcher = ((current != null) ? current.Dispatcher : Dispatcher.CurrentDispatcher);
		if (!dispatcher.CheckAccess())
		{
			try
			{
				dispatcher.BeginInvoke(new Action(Reschedule));
				return;
			}
			catch
			{
				return;
			}
		}
		DateTime dateTime = DateTime.MaxValue;
		Deadline[] array = new Deadline[6] { _transientExpiryTimer, _timerPauseExpiryTimer, _mediaPauseExpiryTimer, _revealTimer, _mediaWideTimer, _reminderRetryTimer };
		foreach (Deadline deadline in array)
		{
			if (deadline != null && deadline.DueUtc < dateTime)
			{
				dateTime = deadline.DueUtc;
			}
		}
		if (_clock == null)
		{
			_clock = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
			_clock.Tick += delegate
			{
				OnClockTick();
			};
		}
		if (dateTime == DateTime.MaxValue)
		{
			_clock.Stop();
			return;
		}
		double value = Math.Max(1.0, (dateTime - DateTime.UtcNow).TotalMilliseconds);
		_clock.Stop();
		_clock.Interval = TimeSpan.FromMilliseconds(value);
		_clock.Start();
	}

	private void OnClockTick()
	{
		_clock.Stop();
		DateTime now = DateTime.UtcNow.AddMilliseconds(1.0);
		_transientExpiryTimer.Fire(now);
		_mediaPauseExpiryTimer.Fire(now);
		_timerPauseExpiryTimer.Fire(now);
		_revealTimer.Fire(now);
		_mediaWideTimer.Fire(now);
		if (_reminderRetryTimer != null)
		{
			_reminderRetryTimer.Fire(now);
		}
		Reschedule();
	}

	public void PublishMessageSession(PillActivitySnapshot snapshot)
	{
		if (snapshot == null)
		{
			return;
		}
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				PillActivitySnapshot oldAct = ResolveCurrentSnapshotLocked();
				if (_currentTransient != null && _currentTransient.Type == ActivityType.CompanionMessage)
				{
					CancelTransientTimerLocked();
					_currentTransient = null;
					_activeTransientSequence = 0L;
					_transientQueue.Clear();
				}
				_messageSession = snapshot.WithSequenceNumber(++_globalSequence);
				PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
				LogTimelineEvent("MESSAGE_SESSION_PUBLISHED", pillActivitySnapshot, pillActivitySnapshot.Title);
				NotifyTransitionLocked(oldAct, pillActivitySnapshot, isUpdateOnly: false, ActivityTransitionType.EnterFromLeft);
				NotifyVisibilityEngine(pillActivitySnapshot);
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				PublishMessageSession(snapshot);
			});
		}
		catch
		{
		}
	}

	public void PublishMessageTransient(PillActivitySnapshot snapshot, TimeSpan duration)
	{
		PublishMessageTransient(snapshot, duration, userRequested: false);
	}

	public void PublishMessageTransient(PillActivitySnapshot snapshot, TimeSpan duration, bool userRequested)
	{
		lock (_syncLock)
		{
			_messageSession = null;
		}
		PublishTransient(snapshot, duration, userRequested);
	}

	public void ClearMessage()
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
				_messageSession = null;
				if (_currentTransient != null && _currentTransient.Type == ActivityType.CompanionMessage)
				{
					CancelTransientTimerLocked();
					_currentTransient = null;
					_activeTransientSequence = 0L;
					_transientQueue.Clear();
				}
				PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
				if (pillActivitySnapshot != pillActivitySnapshot2)
				{
					LogTimelineEvent("MESSAGE_CLEARED", pillActivitySnapshot2, (pillActivitySnapshot2 != null) ? pillActivitySnapshot2.Title : "");
					NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke(new Action(ClearMessage));
		}
		catch
		{
		}
	}

	public void PublishCustomSession(PillActivitySnapshot snapshot)
	{
		if (snapshot == null)
		{
			return;
		}
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				PillActivitySnapshot oldAct = ResolveCurrentSnapshotLocked();
				_customSession = snapshot.WithSequenceNumber(++_globalSequence);
				PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
				LogTimelineEvent("CUSTOM_SESSION_PUBLISHED", pillActivitySnapshot, pillActivitySnapshot.Title);
				NotifyTransitionLocked(oldAct, pillActivitySnapshot, isUpdateOnly: false, ActivityTransitionType.EnterFromLeft);
				NotifyVisibilityEngine(pillActivitySnapshot);
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				PublishCustomSession(snapshot);
			});
		}
		catch
		{
		}
	}

	public void ClearCustom()
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_customSession != null)
				{
					PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
					_customSession = null;
					PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
					if (pillActivitySnapshot != pillActivitySnapshot2)
					{
						LogTimelineEvent("CUSTOM_CLEARED", pillActivitySnapshot2, (pillActivitySnapshot2 != null) ? pillActivitySnapshot2.Title : "");
						NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
					}
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke(new Action(ClearCustom));
		}
		catch
		{
		}
	}

	public void PublishMedia(PillActivitySnapshot mediaSnapshot)
	{
		if (mediaSnapshot == null || PetHoldsMedia(mediaSnapshot))
		{
			return;
		}
		NotchBehaviorConfig instance = NotchBehaviorConfig.Instance;
		if (instance != null && !instance.ShowMediaWhenPlaybackStarts)
		{
			return;
		}
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
				_mediaSession = mediaSnapshot.WithSequenceNumber(++_globalSequence);
				MediaTrackInfo mediaTrackInfo = mediaSnapshot.Payload as MediaTrackInfo;
				string text = (mediaSnapshot.Title ?? "") + "|" + ((mediaTrackInfo != null) ? mediaTrackInfo.Artist : mediaSnapshot.SubText);
				if (text != _mediaTrackKey)
				{
					_mediaTrackKey = text;
					if (mediaSnapshot.IsPlaying)
					{
						_mediaWideUntil = DateTime.UtcNow.AddSeconds(4.0);
						_mediaWideTimer.Change(TimeSpan.FromSeconds(4.0), TimeSpan.FromMilliseconds(-1.0));
					}
				}
				if (mediaSnapshot.IsPlaying)
				{
					_mediaPauseHoldExpired = false;
					_isMediaPauseTimerActive = false;
					_activeMediaPauseSequence = 0L;
					_mediaPauseExpiryTimer.Change(-1, -1);
				}
				else
				{
					if (_mediaPauseHoldExpired)
					{
						return;
					}
					if (_currentTransient != null && !IsCalendarReminder(_currentTransient))
					{
						CancelTransientTimerLocked();
						_currentTransient = null;
						_activeTransientSequence = 0L;
						_transientQueue.Clear();
					}
					if (!_isMediaPauseTimerActive)
					{
						_isMediaPauseTimerActive = true;
						_activeMediaPauseSequence = _mediaSession.SequenceNumber;
						_mediaPauseExpiryTimer.Change(EventDuration(), TimeSpan.FromMilliseconds(-1.0));
					}
					else
					{
						_activeMediaPauseSequence = _mediaSession.SequenceNumber;
					}
				}
				PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
				if (pillActivitySnapshot == _mediaSession || pillActivitySnapshot2 == _mediaSession)
				{
					bool flag = pillActivitySnapshot != null && pillActivitySnapshot.Type == ActivityType.Media;
					if (flag)
					{
						NotchStabilityCounters.MediaUpdates++;
					}
					NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, flag, flag ? ActivityTransitionType.InPlaceUpdate : ActivityTransitionType.EnterFromLeft);
				}
				NotifyVisibilityEngine(mediaSnapshot);
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				PublishMedia(mediaSnapshot);
			});
		}
		catch
		{
		}
	}

	private void OnMediaPauseExpiryCallback(object state)
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_mediaSession != null && !_mediaSession.IsPlaying && _mediaSession.SequenceNumber == _activeMediaPauseSequence)
				{
					_mediaPauseHoldExpired = true;
					_isMediaPauseTimerActive = false;
					_activeMediaPauseSequence = 0L;
					_mediaPauseExpiryTimer.Change(-1, -1);
					PillActivitySnapshot mediaSession = _mediaSession;
					PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
					LogTimelineEvent("MEDIA_PAUSE_HOLD_EXPIRED", pillActivitySnapshot, pillActivitySnapshot.Title);
					NotifyTransitionLocked(mediaSession, pillActivitySnapshot, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				OnMediaPauseExpiryCallback(state);
			});
		}
		catch
		{
		}
	}

	public void ShowMediaWide()
	{
		lock (_syncLock)
		{
			_mediaWideUntil = DateTime.UtcNow.AddSeconds(4.0);
			_mediaWideTimer.Change(TimeSpan.FromSeconds(4.0), TimeSpan.FromMilliseconds(-1.0));
			if (_mediaSession != null && ResolveCurrentSnapshotLocked() == _mediaSession)
			{
				NotifyTransitionLocked(_mediaSession, _mediaSession, isUpdateOnly: true, ActivityTransitionType.InPlaceUpdate);
			}
		}
	}

	public void EndMediaWide()
	{
		_mediaWideTimer.Change(-1, -1);
		OnMediaWideDeadline(null);
	}

	private void OnMediaWideDeadline(object state)
	{
		lock (_syncLock)
		{
			_mediaWideUntil = DateTime.MinValue;
			if (_mediaSession != null && ResolveCurrentSnapshotLocked() == _mediaSession)
			{
				NotifyTransitionLocked(_mediaSession, _mediaSession, isUpdateOnly: true, ActivityTransitionType.InPlaceUpdate);
			}
		}
	}

	public void ClearMedia()
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_mediaSession != null)
				{
					_mediaPauseHoldExpired = false;
					_isMediaPauseTimerActive = false;
					_activeMediaPauseSequence = 0L;
					_mediaPauseExpiryTimer.Change(-1, -1);
					PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
					_mediaSession = null;
					PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
					if (pillActivitySnapshot != pillActivitySnapshot2)
					{
						NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
					}
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke(new Action(ClearMedia));
		}
		catch
		{
		}
	}

	public void PublishTimerSession(string display, bool isPaused, double? progress = null)
	{
		PillActivitySnapshot timerSnapshot = new PillActivitySnapshot("timer_running", "timer", ActivityTier.Tier2_Session, ActivityType.TimerRunning, display, "Timer", display, (double sz, Brush br) => NotchGlyphs.Create("timer", sz, br), null, null, progress, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Timer, !isPaused, null, null, null, "timer", null);
		PublishTimer(timerSnapshot);
	}

	public void PublishTimer(PillActivitySnapshot timerSnapshot)
	{
		if (timerSnapshot == null)
		{
			return;
		}
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
				_timerSession = timerSnapshot.WithSequenceNumber(++_globalSequence);
				if (timerSnapshot.IsPlaying)
				{
					_timerPauseHoldExpired = false;
					_isTimerPauseTimerActive = false;
					_activeTimerPauseSequence = 0L;
					_timerPauseExpiryTimer.Change(-1, -1);
				}
				else if (!_timerPauseHoldExpired && !_isTimerPauseTimerActive)
				{
					_isTimerPauseTimerActive = true;
					_activeTimerPauseSequence = _timerSession.SequenceNumber;
					_timerPauseExpiryTimer.Change(EventDuration(), TimeSpan.FromMilliseconds(-1.0));
				}
				else if (_isTimerPauseTimerActive)
				{
					_activeTimerPauseSequence = _timerSession.SequenceNumber;
				}
				PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
				if (pillActivitySnapshot2 == _timerSession || pillActivitySnapshot == _timerSession)
				{
					bool flag = pillActivitySnapshot != null && pillActivitySnapshot.Type == ActivityType.TimerRunning;
					if (flag)
					{
						NotchStabilityCounters.TimerUpdates++;
					}
					NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, flag, flag ? ActivityTransitionType.InPlaceUpdate : ActivityTransitionType.EnterFromLeft);
				}
				NotifyVisibilityEngine(timerSnapshot);
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				PublishTimer(timerSnapshot);
			});
		}
		catch
		{
		}
	}

	public void UpdateTimerTick(string display, bool isPaused, double? progress = null)
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_timerSession != null)
				{
					_timerSession = _timerSession.WithSessionData(display, "Timer", display, !isPaused, progress);
					if (!isPaused)
					{
						if (_timerPauseHoldExpired)
						{
							PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
							_timerPauseHoldExpired = false;
							_isTimerPauseTimerActive = false;
							_activeTimerPauseSequence = 0L;
							_timerPauseExpiryTimer.Change(-1, -1);
							PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
							if (pillActivitySnapshot != pillActivitySnapshot2)
							{
								NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, isUpdateOnly: false, ActivityTransitionType.EnterFromLeft);
							}
							return;
						}
						_timerPauseHoldExpired = false;
						_isTimerPauseTimerActive = false;
						_activeTimerPauseSequence = 0L;
						_timerPauseExpiryTimer.Change(-1, -1);
					}
					else
					{
						if (_timerPauseHoldExpired)
						{
							return;
						}
						if (!_isTimerPauseTimerActive)
						{
							_isTimerPauseTimerActive = true;
							_activeTimerPauseSequence = _timerSession.SequenceNumber;
							_timerPauseExpiryTimer.Change(EventDuration(), TimeSpan.FromMilliseconds(-1.0));
						}
					}
					NotchStabilityCounters.TimerUpdates++;
					EventHandler<TimerUpdateEventArgs> timerUpdated = this.TimerUpdated;
					if (timerUpdated != null)
					{
						try
						{
							timerUpdated(this, new TimerUpdateEventArgs(_timerSession.SequenceNumber, display, isPaused));
							return;
						}
						catch
						{
							return;
						}
					}
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				UpdateTimerTick(display, isPaused, progress);
			});
		}
		catch
		{
		}
	}

	private void OnTimerPauseExpiryCallback(object state)
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_timerSession != null && !_timerSession.IsPlaying && _timerSession.SequenceNumber == _activeTimerPauseSequence)
				{
					_timerPauseHoldExpired = true;
					_isTimerPauseTimerActive = false;
					_activeTimerPauseSequence = 0L;
					_timerPauseExpiryTimer.Change(-1, -1);
					PillActivitySnapshot timerSession = _timerSession;
					PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
					LogTimelineEvent("TIMER_PAUSE_HOLD_EXPIRED", pillActivitySnapshot, pillActivitySnapshot.Title);
					NotifyTransitionLocked(timerSession, pillActivitySnapshot, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				OnTimerPauseExpiryCallback(state);
			});
		}
		catch
		{
		}
	}

	public void ClearTimer()
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				if (_timerSession != null)
				{
					_timerPauseHoldExpired = false;
					_isTimerPauseTimerActive = false;
					_activeTimerPauseSequence = 0L;
					_timerPauseExpiryTimer.Change(-1, -1);
					PillActivitySnapshot pillActivitySnapshot = ResolveCurrentSnapshotLocked();
					_timerSession = null;
					PillActivitySnapshot pillActivitySnapshot2 = ResolveCurrentSnapshotLocked();
					if (pillActivitySnapshot != pillActivitySnapshot2)
					{
						NotifyTransitionLocked(pillActivitySnapshot, pillActivitySnapshot2, isUpdateOnly: false, ActivityTransitionType.ExitToRight);
					}
				}
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke(new Action(ClearTimer));
		}
		catch
		{
		}
	}

	public void UpdateClock(string timeText, string dateText, string batteryText, bool isAnalog)
	{
		lock (_syncLock)
		{
			_ambientClock = new PillActivitySnapshot("ambient_clock", "clock", ActivityTier.Tier3_Ambient, isAnalog ? ActivityType.Clock : ActivityType.RestingClock, timeText ?? string.Empty, dateText ?? string.Empty, batteryText ?? string.Empty, null, null, null, null, DateTime.UtcNow, null, (_ambientClock != null) ? _ambientClock.SequenceNumber : 1, isAnalog ? CollapsedActivityProfile.Clock : CollapsedActivityProfile.Resting, isPlaying: false, null, null, null, "clock", isAnalog ? "analog" : "digital");
			NotchStabilityCounters.ClockUpdates++;
		}
	}

	public void ResetToRestingClock()
	{
		lock (_syncLock)
		{
			CancelTransientTimerLocked();
			_timerPauseExpiryTimer.Change(-1, -1);
			_timerPauseHoldExpired = false;
			_isTimerPauseTimerActive = false;
			_activeTimerPauseSequence = 0L;
			_mediaPauseExpiryTimer.Change(-1, -1);
			_mediaPauseHoldExpired = false;
			_isMediaPauseTimerActive = false;
			_activeMediaPauseSequence = 0L;
			_transientQueue.Clear();
			_criticalSlot = null;
			_currentTransient = null;
			_messageSession = null;
			_customSession = null;
			_mediaSession = null;
			_timerSession = null;
			PillActivitySnapshot ambientClock = _ambientClock;
			NotifyTransitionLocked(null, ambientClock, isUpdateOnly: false, ActivityTransitionType.RestingInstant);
		}
	}

	private void NotifyTransitionLocked(PillActivitySnapshot oldAct, PillActivitySnapshot newAct, bool isUpdateOnly, ActivityTransitionType transition)
	{
		SyncRevealLocked(newAct);
		EventHandler<PillActivityChangedEventArgs> handler = this.ActivityChanged;
		if (handler == null)
		{
			return;
		}
		Application current = Application.Current;
		if (current != null && current.Dispatcher != null)
		{
			try
			{
				current.Dispatcher.BeginInvoke((Action)delegate
				{
					try
					{
						handler(this, new PillActivityChangedEventArgs(oldAct, newAct, isUpdateOnly, transition));
					}
					catch (Exception)
					{
					}
				});
				return;
			}
			catch
			{
			}
		}
		try
		{
			handler(this, new PillActivityChangedEventArgs(oldAct, newAct, isUpdateOnly, transition));
		}
		catch (Exception)
		{
		}
	}

	private void NotifyVisibilityEngine(PillActivitySnapshot snapshot)
	{
		try
		{
			ConsiderRevealLocked(snapshot);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[Notch] reveal decision failed: " + ex.GetType().Name);
		}
	}

	private void ConsiderRevealLocked(PillActivitySnapshot snap)
	{
		if (snap == null)
		{
			return;
		}
		NotchBehaviorConfig instance = NotchBehaviorConfig.Instance;
		if (instance == null)
		{
			return;
		}
		double totalSeconds = EventDuration().TotalSeconds;
		switch (snap.Type)
		{
		case ActivityType.RestingClock:
		case ActivityType.Clock:
			return;
		case ActivityType.Media:
		{
			string text2 = (snap.Title ?? "") + "|" + (snap.SubText ?? "") + "|" + snap.IsPlaying;
			bool flag2 = text2 != _lastMediaRevealSig;
			_lastMediaRevealSig = text2;
			if (flag2 && snap.IsPlaying && instance.ShowMediaWhenPlaybackStarts && instance.MediaMode != MediaVisibilityMode.NeverOverApps && instance.MediaMode != MediaVisibilityMode.FollowVisibility)
			{
				StartRevealLocked(snap.Type, Math.Max(2.5, totalSeconds), persistent: false, tiedToTransient: false);
			}
			return;
		}
		case ActivityType.TimerRunning:
		{
			string text = (snap.Title ?? "") + "|" + snap.IsPlaying;
			bool flag = text != _lastTimerRevealSig;
			_lastTimerRevealSig = text;
			if (flag && instance.ShowBriefEventsWhenHidden)
			{
				StartRevealLocked(snap.Type, totalSeconds, persistent: false, tiedToTransient: false);
			}
			return;
		}
		}
		if (snap.Type == ActivityType.CompanionMessage && snap.Payload is MessageSettings)
		{
			if (!snap.ExpiresAt.HasValue || snap.ExpiresAt.Value == DateTime.MaxValue)
			{
				if (!(snap.Id == _lastPinnedRevealId))
				{
					_lastPinnedRevealId = snap.Id;
					StartRevealLocked(snap.Type, totalSeconds, persistent: false, tiedToTransient: false);
				}
			}
			else
			{
				StartRevealLocked(snap.Type, Math.Max(1.0, (snap.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds), persistent: false, snap.Tier == ActivityTier.Tier1_Transient);
			}
		}
		else if (instance.ShowBriefEventsWhenHidden)
		{
			EventPresentationMode eventPresentationMode = instance.GetEventPresentationMode(snap.Type);
			if (eventPresentationMode != 0)
			{
				bool flag3 = eventPresentationMode == EventPresentationMode.Persistent;
				StartRevealLocked(snap.Type, totalSeconds, flag3, !flag3 && snap.Tier == ActivityTier.Tier1_Transient);
			}
		}
	}

	private void StartRevealLocked(ActivityType type, double seconds, bool persistent, bool tiedToTransient)
	{
		_revealType = type;
		_revealPersistent = persistent;
		_revealTiedToTransient = tiedToTransient;
		int maxMs;
		if (persistent || tiedToTransient)
		{
			_revealTimer.Change(-1, -1);
			maxMs = (persistent ? (-1) : ((int)Math.Max(0.0, (_transientExpiryTimer.DueUtc == DateTime.MaxValue) ? (seconds * 1000.0) : (_transientExpiryTimer.DueUtc - DateTime.UtcNow).TotalMilliseconds)));
		}
		else
		{
			_revealTimer.Change(TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(-1.0));
			maxMs = (int)(seconds * 1000.0);
		}
		RaiseReveal(active: true, maxMs, type.ToString());
	}

	private void EndRevealLocked(string reason, bool force)
	{
		if (_revealType.HasValue)
		{
			if (!force && NotchVisibilityEngine.Instance.IsHoveringNotch)
			{
				_revealTiedToTransient = false;
				_revealPersistent = false;
				_revealTimer.Change(300, -1);
			}
			else
			{
				_revealType = null;
				_revealPersistent = false;
				_revealTiedToTransient = false;
				_revealTimer.Change(-1, -1);
				RaiseReveal(active: false, -1, reason);
			}
		}
	}

	private void OnRevealDeadline(object state)
	{
		lock (_syncLock)
		{
			EndRevealLocked("event duration elapsed", force: false);
		}
	}

	private void SyncRevealLocked(PillActivitySnapshot resolved)
	{
		if (_revealType.HasValue)
		{
			if (_revealTiedToTransient && _currentTransient == null)
			{
				EndRevealLocked("event ended", force: false);
			}
			else if (_revealPersistent && (resolved == null || resolved.Type != _revealType.Value))
			{
				EndRevealLocked("event no longer shown", force: false);
			}
		}
	}

	public void Shutdown()
	{
		Deadline[] array = new Deadline[6] { _transientExpiryTimer, _timerPauseExpiryTimer, _mediaPauseExpiryTimer, _revealTimer, _mediaWideTimer, _reminderRetryTimer };
		foreach (Deadline deadline in array)
		{
			deadline.DueUtc = DateTime.MaxValue;
		}
		if (_clock != null)
		{
			_clock.Stop();
		}
	}

	public void EndReveal(string reason)
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null || current.Dispatcher.CheckAccess())
		{
			lock (_syncLock)
			{
				EndRevealLocked(reason, force: true);
				return;
			}
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				EndReveal(reason);
			});
		}
		catch
		{
		}
	}

	private void RaiseReveal(bool active, int maxMs, string reason)
	{
		Application current = Application.Current;
		if (current != null && current.Dispatcher != null)
		{
			try
			{
				current.Dispatcher.BeginInvoke((Action)delegate
				{
					try
					{
						NotchVisibilityEngine.Instance.SetRevealActive(active, reason);
					}
					catch (Exception ex3)
					{
						TransactionLogger.Warn("[Notch] reveal publish failed: " + ex3.GetType().Name);
					}
					Action<bool, int, string> revealChanged2 = this.RevealChanged;
					if (revealChanged2 != null)
					{
						try
						{
							revealChanged2(active, maxMs, reason);
						}
						catch (Exception ex4)
						{
							TransactionLogger.Warn("[Notch] reveal listener failed: " + ex4.GetType().Name);
						}
					}
				});
				return;
			}
			catch
			{
			}
		}
		try
		{
			NotchVisibilityEngine.Instance.SetRevealActive(active, reason);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[Notch] reveal publish failed: " + ex.GetType().Name);
		}
		Action<bool, int, string> revealChanged = this.RevealChanged;
		if (revealChanged != null)
		{
			try
			{
				revealChanged(active, maxMs, reason);
			}
			catch (Exception ex2)
			{
				TransactionLogger.Warn("[Notch] reveal listener failed: " + ex2.GetType().Name);
			}
		}
	}

	private void LogTimelineEvent(string eventType, PillActivitySnapshot snap, string details)
	{
		if (_timeline.Count >= 50)
		{
			_timeline.RemoveAt(0);
		}
		_timeline.Add(new ActivityTimelineEntry(eventType, (snap != null) ? snap.Id : "", (ActivityPriority)(snap?.Tier ?? ((ActivityTier)4)), details));
	}
}
