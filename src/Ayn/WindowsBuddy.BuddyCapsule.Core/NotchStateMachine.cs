using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsBuddy.BuddyCapsule.Core;

public sealed class NotchStateMachine
{
	public const int InconsistencyResetMs = 2000;

	public const int CloseGraceMs = 150;

	public const int HoverCloseGraceMs = 60;

	private const int PollActiveMs = 40;

	private const int PollRestingMs = 250;

	private const int WatchdogActiveMs = 250;

	private const int WatchdogIdleMs = 500;

	private const int TransitionLogSize = 40;

	public const int ContentReadyMs = 500;

	private const int TransientGraceMs = 2000;

	private readonly INotchSurface _surface;

	private readonly INotchScheduler _sched;

	private readonly NotchTransition[] _log = new NotchTransition[40];

	private int _logNext;

	private int _logCount;

	private NotchPhase _phase;

	private NotchPresence _presence = NotchPresence.None;

	private long _phaseSinceMs;

	private int _generation;

	private bool _pointerInside;

	private bool _closeOnLeave;

	private bool _openedByPointer;

	private bool _suppressUntilLeave;

	private bool _pendingCloseOnUnlock;

	private long _immunityUntilMs;

	private long _inconsistentSinceMs = -1L;

	private string _lastInconsistency;

	private bool _enabled = true;

	private bool _revealActive;

	private int _revealMaxMs = -1;

	private long _openingStartedMs = -1L;

	private int LeaveGraceMs
	{
		get
		{
			if (OpensOnClick || !_openedByPointer)
			{
				return 150;
			}
			return 60;
		}
	}

	private NotchPhase RestPhase
	{
		get
		{
			if (!_revealActive)
			{
				return NotchPhase.Resting;
			}
			return NotchPhase.Transient;
		}
	}

	public bool IsRevealActive => _revealActive;

	public int HoverDelayMs { get; private set; }

	public bool OpensOnClick { get; private set; }

	public int WatchdogResets { get; private set; }

	public Func<bool> ImmediateRecovery { get; set; }

	public Func<bool> HoldOpen { get; set; }

	private bool Held
	{
		get
		{
			try
			{
				return HoldOpen != null && HoldOpen();
			}
			catch
			{
				return false;
			}
		}
	}

	public int TransitionCount { get; private set; }

	public string LastResetReason { get; private set; }

	public NotchPhase Phase => _phase;

	public NotchPresence Presence => _presence;

	public bool PointerInside => _pointerInside;

	public long MsInPhase => _sched.NowMs - _phaseSinceMs;

	public int Generation => _generation;

	public bool IsLocked
	{
		get
		{
			if (_sched.NowMs >= _immunityUntilMs)
			{
				return SafeTextInputActive();
			}
			return true;
		}
	}

	public bool IsOpenOrOpening
	{
		get
		{
			if (_phase != NotchPhase.Open)
			{
				return _phase == NotchPhase.Opening;
			}
			return true;
		}
	}

	public bool IsResting
	{
		get
		{
			if (_phase != NotchPhase.Resting && _phase != NotchPhase.Transient)
			{
				return _phase == NotchPhase.Peeking;
			}
			return true;
		}
	}

	public string LastInconsistency => _lastInconsistency;

	public bool Enabled => _enabled;

	public event Action<string> TransientExpiredByWatchdog;

	public event Action<NotchPhase, NotchPhase, string> PhaseChanged;

	public event Action<string> Recovered;

	public NotchStateMachine(INotchSurface surface, INotchScheduler scheduler, int hoverDelayMs, bool opensOnClick)
	{
		if (surface == null)
		{
			throw new ArgumentNullException("surface");
		}
		if (scheduler == null)
		{
			throw new ArgumentNullException("scheduler");
		}
		_surface = surface;
		_sched = scheduler;
		HoverDelayMs = Math.Max(0, hoverDelayMs);
		OpensOnClick = opensOnClick;
		_phaseSinceMs = _sched.NowMs;
		ScheduleWatchdog();
	}

	public void Configure(int hoverDelayMs, bool opensOnClick)
	{
		HoverDelayMs = Math.Max(0, hoverDelayMs);
		if (OpensOnClick == opensOnClick)
		{
			return;
		}
		OpensOnClick = opensOnClick;
		if (_phase == NotchPhase.Peeking)
		{
			_sched.Cancel(NotchTimer.Dwell);
			if (!opensOnClick && _pointerInside)
			{
				_sched.Schedule(NotchTimer.Dwell, HoverDelayMs, OnDwellElapsed);
			}
		}
	}

	public void SetEnabled(bool enabled, string reason)
	{
		if (_enabled != enabled)
		{
			_enabled = enabled;
			if (!enabled)
			{
				Reset("disabled: " + reason, _presence, countAsRecovery: false);
			}
			else
			{
				SetPresence(_presence, "enabled: " + reason, force: true);
			}
		}
	}

	public void PointerMoved(bool inside, string source)
	{
		if (!_enabled)
		{
			_pointerInside = false;
			return;
		}
		if (inside == _pointerInside)
		{
			if (inside && _phase == NotchPhase.Open)
			{
				_sched.Cancel(NotchTimer.Grace);
			}
			return;
		}
		_pointerInside = inside;
		if (inside)
		{
			switch (_phase)
			{
			case NotchPhase.Resting:
			case NotchPhase.Transient:
				if (!_suppressUntilLeave)
				{
					Enter(NotchPhase.Peeking, "pointer entered (" + source + ")");
					_surface.ShowPeek(on: true);
					if (!OpensOnClick)
					{
						_sched.Schedule(NotchTimer.Dwell, HoverDelayMs, OnDwellElapsed);
					}
				}
				break;
			case NotchPhase.Closing:
				if (!OpensOnClick && _openedByPointer)
				{
					BeginOpening("pointer re-entered while closing", byPointer: true, explicitClick: false);
				}
				break;
			case NotchPhase.Opening:
			case NotchPhase.Open:
				_closeOnLeave = true;
				_sched.Cancel(NotchTimer.Grace);
				break;
			case NotchPhase.Peeking:
				break;
			}
			return;
		}
		_suppressUntilLeave = false;
		switch (_phase)
		{
		case NotchPhase.Peeking:
			_sched.Cancel(NotchTimer.Dwell);
			_surface.ShowPeek(on: false);
			Enter(RestPhase, "pointer left before opening (" + source + ")");
			break;
		case NotchPhase.Opening:
			if (_closeOnLeave && !Held)
			{
				if (IsLocked)
				{
					_pendingCloseOnUnlock = true;
				}
				else
				{
					BeginClosing("pointer left while opening (" + source + ")");
				}
			}
			break;
		case NotchPhase.Open:
			if (_closeOnLeave && !Held)
			{
				if (IsLocked)
				{
					_pendingCloseOnUnlock = true;
				}
				else
				{
					_sched.Schedule(NotchTimer.Grace, LeaveGraceMs, OnGraceElapsed);
				}
			}
			break;
		}
	}

	public void Click(string source)
	{
		if (_enabled)
		{
			_suppressUntilLeave = false;
			switch (_phase)
			{
			case NotchPhase.Resting:
			case NotchPhase.Peeking:
			case NotchPhase.Closing:
			case NotchPhase.Transient:
				BeginOpening("click (" + source + ")", byPointer: true, explicitClick: true);
				break;
			case NotchPhase.Opening:
			case NotchPhase.Open:
				break;
			}
		}
	}

	public void RequestOpen(string source)
	{
		if (_enabled)
		{
			switch (_phase)
			{
			case NotchPhase.Hidden:
			case NotchPhase.Resting:
			case NotchPhase.Peeking:
			case NotchPhase.Closing:
			case NotchPhase.Transient:
				BeginOpening(source, byPointer: false, explicitClick: false);
				break;
			case NotchPhase.Opening:
			case NotchPhase.Open:
				break;
			}
		}
	}

	public void RequestClose(string source)
	{
		switch (_phase)
		{
		case NotchPhase.Opening:
		case NotchPhase.Open:
			if (_pointerInside)
			{
				_suppressUntilLeave = true;
			}
			BeginClosing(source);
			break;
		case NotchPhase.Peeking:
			_sched.Cancel(NotchTimer.Dwell);
			_surface.ShowPeek(on: false);
			_suppressUntilLeave = _pointerInside;
			Enter(RestPhase, source);
			break;
		}
	}

	public void Toggle(string source)
	{
		if (IsOpenOrOpening)
		{
			RequestClose(source);
		}
		else
		{
			RequestOpen(source);
		}
	}

	public void SetPresence(NotchPresence presence, string reason, bool force = false)
	{
		bool flag = presence != _presence;
		_presence = presence;
		NotchPresence notchPresence = (_enabled ? presence : NotchPresence.None);
		if (!flag && !force)
		{
			return;
		}
		NotchLog.Info(string.Concat("Presence ", presence, (IsResting || _phase == NotchPhase.Hidden) ? "" : " (deferred until closed)", ": ", reason));
		switch (_phase)
		{
		case NotchPhase.Hidden:
			if (notchPresence != NotchPresence.None)
			{
				Enter(RestPhase, "shown: " + reason);
				_surface.ApplyPresence(notchPresence, animate: true);
			}
			else if (force)
			{
				_surface.ApplyPresence(NotchPresence.None, animate: false);
			}
			break;
		case NotchPhase.Resting:
		case NotchPhase.Peeking:
		case NotchPhase.Transient:
			if (_phase == NotchPhase.Peeking)
			{
				_sched.Cancel(NotchTimer.Dwell);
				_surface.ShowPeek(on: false);
			}
			_pointerInside = false;
			if (notchPresence == NotchPresence.None)
			{
				Enter(NotchPhase.Hidden, "hidden: " + reason);
				_surface.ApplyPresence(NotchPresence.None, animate: true);
				break;
			}
			if (_phase == NotchPhase.Peeking)
			{
				Enter(RestPhase, "presence changed: " + reason);
			}
			_surface.ApplyPresence(notchPresence, animate: true);
			break;
		case NotchPhase.Opening:
		case NotchPhase.Open:
		case NotchPhase.Closing:
			break;
		}
	}

	public void SetReveal(bool active, int maxMs, string reason)
	{
		_revealActive = active;
		_revealMaxMs = (active ? maxMs : (-1));
		if (active && _phase == NotchPhase.Resting)
		{
			Enter(NotchPhase.Transient, "event: " + reason);
		}
		else if (!active && _phase == NotchPhase.Transient)
		{
			Enter(NotchPhase.Resting, "event ended: " + reason);
		}
		else if (active && _phase == NotchPhase.Transient)
		{
			_phaseSinceMs = _sched.NowMs;
		}
		if (!active && _enabled && _phase == NotchPhase.Resting && _presence != NotchPresence.None)
		{
			_surface.ApplyPresence(_presence, animate: true);
		}
	}

	public void NotifyToolSwitching(int immunityMs)
	{
		_immunityUntilMs = Math.Max(_immunityUntilMs, _sched.NowMs + immunityMs);
		_sched.Cancel(NotchTimer.Grace);
		if (_phase == NotchPhase.Open && !_pointerInside && _closeOnLeave)
		{
			_pendingCloseOnUnlock = true;
		}
		_sched.Schedule(NotchTimer.Unlock, immunityMs + 10, OnUnlockCheck);
	}

	public void NotifyTextInputChanged()
	{
		if (!SafeTextInputActive())
		{
			OnUnlockCheck();
		}
		else
		{
			_sched.Cancel(NotchTimer.Grace);
		}
	}

	public void HoldReleased(string source)
	{
		if (_phase != NotchPhase.Open || Held)
		{
			return;
		}
		RefreshPointer("hold released (" + source + ")");
		if (_phase == NotchPhase.Open && !_pointerInside && _closeOnLeave)
		{
			if (IsLocked)
			{
				_pendingCloseOnUnlock = true;
			}
			else
			{
				_sched.Schedule(NotchTimer.Grace, LeaveGraceMs, OnGraceElapsed);
			}
		}
	}

	public void FocusLost(string source)
	{
		NotifyTextInputChanged();
		if ((_phase != NotchPhase.Open && _phase != NotchPhase.Opening) || _pointerInside)
		{
			return;
		}
		_closeOnLeave = true;
		if (!Held)
		{
			if (IsLocked)
			{
				_pendingCloseOnUnlock = true;
				_sched.Schedule(NotchTimer.Unlock, Math.Max(10, (int)(_immunityUntilMs - _sched.NowMs) + 10), OnUnlockCheck);
			}
			else
			{
				BeginClosing("focus moved away (" + source + ")");
			}
		}
	}

	public void OnAnimationCompleted(int generation)
	{
		if (generation != _generation)
		{
			return;
		}
		if (_phase == NotchPhase.Opening)
		{
			Enter(NotchPhase.Open, "open animation completed");
			RefreshPointer("open completed");
			if (_closeOnLeave && !_pointerInside && !Held)
			{
				if (IsLocked)
				{
					_pendingCloseOnUnlock = true;
				}
				else
				{
					_sched.Schedule(NotchTimer.Grace, LeaveGraceMs, OnGraceElapsed);
				}
			}
		}
		else if (_phase == NotchPhase.Closing)
		{
			FinishClosing("close animation completed");
		}
	}

	public void Reset(string reason)
	{
		Reset(reason, _presence, countAsRecovery: true);
	}

	public void Reset(string reason, NotchPresence presence, bool countAsRecovery)
	{
		_sched.Cancel(NotchTimer.Dwell);
		_sched.Cancel(NotchTimer.Grace);
		_sched.Cancel(NotchTimer.Unlock);
		_generation++;
		_presence = presence;
		_pendingCloseOnUnlock = false;
		_immunityUntilMs = 0L;
		_closeOnLeave = false;
		_openedByPointer = false;
		_pointerInside = false;
		_inconsistentSinceMs = -1L;
		_openingStartedMs = -1L;
		NotchPresence notchPresence = (_enabled ? presence : NotchPresence.None);
		Enter((notchPresence != NotchPresence.None) ? RestPhase : NotchPhase.Hidden, "RESET: " + reason);
		try
		{
			_surface.ResetVisuals(notchPresence);
		}
		catch (Exception ex)
		{
			NotchLog.Error("ResetVisuals failed", ex);
		}
		if (!countAsRecovery)
		{
			return;
		}
		WatchdogResets++;
		LastResetReason = reason;
		NotchLog.Warn(string.Concat("Notch reset to ", _phase, ": ", reason));
		Action<string> recovered = this.Recovered;
		if (recovered != null)
		{
			try
			{
				recovered(reason);
			}
			catch
			{
			}
		}
	}

	private void BeginOpening(string trigger, bool byPointer, bool explicitClick)
	{
		bool flag = _phase == NotchPhase.Hidden || IsResting;
		_sched.Cancel(NotchTimer.Dwell);
		_sched.Cancel(NotchTimer.Grace);
		_pendingCloseOnUnlock = false;
		_suppressUntilLeave = false;
		if (flag)
		{
			_openedByPointer = byPointer;
		}
		else
		{
			_openedByPointer |= byPointer;
		}
		_closeOnLeave = byPointer || _pointerInside;
		if (_phase == NotchPhase.Peeking)
		{
			_surface.ShowPeek(on: false);
		}
		_openingStartedMs = _sched.NowMs;
		_generation++;
		Enter(NotchPhase.Opening, trigger);
		_surface.AnimateOpen(_generation, flag);
	}

	private void BeginClosing(string trigger)
	{
		_sched.Cancel(NotchTimer.Dwell);
		_sched.Cancel(NotchTimer.Grace);
		_pendingCloseOnUnlock = false;
		_generation++;
		Enter(NotchPhase.Closing, trigger);
		_surface.AnimateClose(_generation);
	}

	private void FinishClosing(string trigger)
	{
		_closeOnLeave = false;
		_openingStartedMs = -1L;
		_openedByPointer = false;
		NotchPresence notchPresence = (_enabled ? _presence : NotchPresence.None);
		if (notchPresence == NotchPresence.None)
		{
			Enter(NotchPhase.Hidden, trigger);
			_surface.ApplyPresence(NotchPresence.None, animate: true);
			return;
		}
		Enter(RestPhase, trigger);
		_surface.ApplyPresence(notchPresence, animate: true);
		bool flag = false;
		try
		{
			flag = _surface.IsPointerInside();
		}
		catch
		{
		}
		_pointerInside = false;
		if (!flag)
		{
			_suppressUntilLeave = false;
		}
		else if (_suppressUntilLeave)
		{
			_pointerInside = true;
		}
		else
		{
			PointerMoved(inside: true, "on the notch as it closed");
		}
	}

	private void OnDwellElapsed()
	{
		if (_phase == NotchPhase.Peeking && !OpensOnClick)
		{
			RefreshPointer("dwell");
			if (_phase == NotchPhase.Peeking && _pointerInside)
			{
				BeginOpening("hover delay elapsed", byPointer: true, explicitClick: false);
			}
		}
	}

	private void OnGraceElapsed()
	{
		if (_phase != NotchPhase.Open)
		{
			return;
		}
		RefreshPointer("grace");
		if (!_pointerInside && _closeOnLeave && !Held)
		{
			if (IsLocked)
			{
				_pendingCloseOnUnlock = true;
			}
			else
			{
				BeginClosing("pointer left the open notch");
			}
		}
	}

	private void OnUnlockCheck()
	{
		if (IsLocked)
		{
			long num = _immunityUntilMs - _sched.NowMs;
			if (num > 0)
			{
				_sched.Schedule(NotchTimer.Unlock, (int)num + 10, OnUnlockCheck);
			}
			return;
		}
		if (_phase != NotchPhase.Open)
		{
			_pendingCloseOnUnlock = false;
			return;
		}
		RefreshPointer("unlock");
		if ((_pendingCloseOnUnlock || _closeOnLeave) && !_pointerInside)
		{
			_pendingCloseOnUnlock = false;
			_sched.Schedule(NotchTimer.Grace, LeaveGraceMs, OnGraceElapsed);
		}
	}

	public void RecheckPointer(string source)
	{
		RefreshPointer(source);
	}

	private void RefreshPointer(string source)
	{
		bool flag;
		try
		{
			flag = _surface.IsPointerInside();
		}
		catch
		{
			flag = false;
		}
		if (flag != _pointerInside)
		{
			PointerMoved(flag, source);
		}
	}

	private void OnPoll()
	{
		if (_phase != 0)
		{
			RefreshPointer("poll");
			SchedulePoll();
		}
	}

	private void SchedulePoll()
	{
		if (_phase == NotchPhase.Hidden)
		{
			_sched.Cancel(NotchTimer.Poll);
			return;
		}
		bool flag = _phase != NotchPhase.Resting && _phase != NotchPhase.Transient;
		_sched.Schedule(NotchTimer.Poll, flag ? 40 : 250, OnPoll);
	}

	private void ScheduleWatchdog()
	{
		bool flag = _phase == NotchPhase.Open && _openingStartedMs >= 0 && _sched.NowMs - _openingStartedMs < 1500;
		bool flag2 = _phase == NotchPhase.Opening || _phase == NotchPhase.Closing || _phase == NotchPhase.Peeking || flag || _inconsistentSinceMs >= 0;
		_sched.Schedule(NotchTimer.Watchdog, flag2 ? 250 : 500, OnWatchdog);
	}

	private void OnWatchdog()
	{
		try
		{
			RunWatchdog();
		}
		catch (Exception ex)
		{
			NotchLog.Error("Watchdog check failed", ex);
		}
		finally
		{
			ScheduleWatchdog();
		}
	}

	private void RunWatchdog()
	{
		long nowMs = _sched.NowMs;
		string text = null;
		int num = Math.Max(800, _surface.AnimationBudgetMs * 3);
		if (_phase == NotchPhase.Transient && _revealMaxMs > 0 && nowMs - _phaseSinceMs > _revealMaxMs + 2000)
		{
			string text2 = $"brief event still shown after {nowMs - _phaseSinceMs} ms (limit {_revealMaxMs} ms)";
			NotchLog.Warn("Watchdog: " + text2);
			WatchdogResets++;
			LastResetReason = text2;
			_revealActive = false;
			_revealMaxMs = -1;
			Enter(NotchPhase.Resting, "watchdog: " + text2);
			Action<string> transientExpiredByWatchdog = this.TransientExpiredByWatchdog;
			if (transientExpiredByWatchdog != null)
			{
				try
				{
					transientExpiredByWatchdog(text2);
					return;
				}
				catch (Exception ex)
				{
					NotchLog.Error("TransientExpiredByWatchdog handler", ex);
					return;
				}
			}
			return;
		}
		if ((_phase == NotchPhase.Open || _phase == NotchPhase.Opening) && _openingStartedMs >= 0 && nowMs - _openingStartedMs > 500)
		{
			bool flag = true;
			try
			{
				flag = _surface.IsContentReady();
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				Reset($"watchdog: panel content not on screen {nowMs - _openingStartedMs} ms after opening");
				return;
			}
		}
		if ((_phase == NotchPhase.Opening || _phase == NotchPhase.Closing) && nowMs - _phaseSinceMs > num)
		{
			text = $"{_phase} animation never completed ({nowMs - _phaseSinceMs} ms)";
		}
		else if (_phase == NotchPhase.Peeking && !OpensOnClick && _pointerInside && nowMs - _phaseSinceMs > HoverDelayMs + num)
		{
			text = "hover delay elapsed but the notch never opened";
		}
		else
		{
			try
			{
				text = _surface.CheckConsistency(_phase, _enabled ? _presence : NotchPresence.None);
			}
			catch (Exception ex2)
			{
				text = "consistency check threw: " + ex2.Message;
			}
		}
		if (text == null)
		{
			_inconsistentSinceMs = -1L;
			_lastInconsistency = null;
			return;
		}
		if (ImmediateRecovery != null && ImmediateRecovery() && _enabled && _presence != NotchPresence.None)
		{
			Reset("pet watchdog: " + text);
			return;
		}
		if (_inconsistentSinceMs < 0)
		{
			_inconsistentSinceMs = nowMs;
			_lastInconsistency = text;
			if (_phase != NotchPhase.Opening && _phase != NotchPhase.Closing)
			{
				return;
			}
		}
		_lastInconsistency = text;
		if (nowMs - _inconsistentSinceMs >= 2000 || _phase == NotchPhase.Opening || _phase == NotchPhase.Closing)
		{
			Reset("watchdog: " + text);
		}
	}

	private bool SafeTextInputActive()
	{
		try
		{
			return _surface.IsTextInputActive();
		}
		catch
		{
			return false;
		}
	}

	private void Enter(NotchPhase to, string trigger)
	{
		NotchPhase phase = _phase;
		if (phase == to && !trigger.StartsWith("RESET", StringComparison.Ordinal))
		{
			return;
		}
		_phase = to;
		_phaseSinceMs = _sched.NowMs;
		TransitionCount++;
		NotchTransition notchTransition = _log[_logNext] ?? (_log[_logNext] = new NotchTransition());
		notchTransition.Time = DateTime.Now;
		notchTransition.From = phase;
		notchTransition.To = to;
		notchTransition.Trigger = trigger;
		_logNext = (_logNext + 1) % _log.Length;
		if (_logCount < _log.Length)
		{
			_logCount++;
		}
		NotchLog.Transition(phase, to, trigger);
		if (to == NotchPhase.Resting || to == NotchPhase.Transient || to == NotchPhase.Hidden)
		{
			_sched.Cancel(NotchTimer.Unlock);
		}
		SchedulePoll();
		ScheduleWatchdog();
		Action<NotchPhase, NotchPhase, string> phaseChanged = this.PhaseChanged;
		if (phaseChanged != null)
		{
			try
			{
				phaseChanged(phase, to, trigger);
			}
			catch (Exception ex)
			{
				NotchLog.Error("PhaseChanged handler", ex);
			}
		}
	}

	public List<NotchTransition> RecentTransitions(int max)
	{
		List<NotchTransition> list = new List<NotchTransition>();
		for (int i = 0; i < Math.Min(max, _logCount); i++)
		{
			int num = (_logNext - 1 - i + _log.Length * 2) % _log.Length;
			NotchTransition notchTransition = _log[num];
			if (notchTransition != null)
			{
				list.Add(new NotchTransition
				{
					Time = notchTransition.Time,
					From = notchTransition.From,
					To = notchTransition.To,
					Trigger = notchTransition.Trigger
				});
			}
		}
		return list;
	}

	public string Describe()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("Phase      {0} ({1} ms)   presence {2}{3}\n", _phase, MsInPhase, _presence, _enabled ? "" : " (notch off)");
		stringBuilder.AppendFormat("Pointer    {0}   locked {1}   opens on {2}   hover delay {3} ms\n", _pointerInside ? "inside" : "outside", IsLocked, OpensOnClick ? "click" : "hover", HoverDelayMs);
		stringBuilder.AppendFormat("Generation {0}   transitions {1}   watchdog resets {2}\n", _generation, TransitionCount, WatchdogResets);
		if (_revealActive)
		{
			stringBuilder.AppendFormat("Event      showing (limit {0})\n", (_revealMaxMs < 0) ? "until it changes" : (_revealMaxMs + " ms"));
		}
		if (_lastInconsistency != null)
		{
			stringBuilder.AppendFormat("Mismatch   {0}\n", _lastInconsistency);
		}
		if (LastResetReason != null)
		{
			stringBuilder.AppendFormat("Last reset {0}\n", LastResetReason);
		}
		return stringBuilder.ToString();
	}
}
