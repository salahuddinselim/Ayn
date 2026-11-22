using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Message;

public sealed class MessageService
{
	private static readonly Lazy<MessageService> _instance = new Lazy<MessageService>(() => new MessageService());

	private readonly string _storagePath;

	private DispatcherTimer _saveTimer;

	private bool _dirty;

	private bool _loadFailed;

	private readonly object _queueLock = new object();

	private readonly object _writeLock = new object();

	private string _pendingJson;

	private long _saveSeq;

	private long _pendingSeq;

	private long _writtenSeq;

	private bool _writerQueued;

	private long _session;

	private string _liveId;

	private bool _arbiterHooked;

	private bool _exitHooked;

	private long _noticeId;

	public static MessageService Instance => _instance.Value;

	public MessageSettings Settings { get; private set; }

	public bool IsLive { get; private set; }

	public bool IsPinned { get; private set; }

	public string LastError { get; private set; }

	public event EventHandler StateChanged;

	private MessageService()
	{
		string path = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "message");
		_storagePath = Path.Combine(path, "settings.json");
		Settings = Load(_storagePath);
		Action prepare = delegate
		{
			if (_saveTimer != null)
			{
				_saveTimer.Stop();
			}
		};
		PersistenceHelper.RegisterExitPreparation("message timer", prepare);
		PersistenceHelper.RegisterExitFlush("message", FlushNow);
		HookExit();
	}

	public bool Flash()
	{
		string text = MessageText.Clean(Settings.Text);
		if (text.Length == 0)
		{
			return false;
		}
		MessageSettings messageSettings = Settings.Clone();
		messageSettings.Text = text;
		bool repeatEnabled = messageSettings.RepeatEnabled;
		LastError = null;
		long num = ++_session;
		_liveId = "msg_" + num;
		IsLive = true;
		IsPinned = repeatEnabled;
		HookArbiter();
		HookExit();
		TimeSpan durationSpan = messageSettings.DurationSpan;
		PillActivitySnapshot snapshot = new PillActivitySnapshot(_liveId, "message", (!repeatEnabled) ? ActivityTier.Tier1_Transient : ActivityTier.Tier2_Session, ActivityType.CompanionMessage, text, "Message", text, (double sz, Brush br) => VectorIcons.CreateMessageIcon(sz, br), null, MessageRenderer.CreateForegroundBrush(messageSettings), null, DateTime.UtcNow, repeatEnabled ? null : new DateTime?(DateTime.UtcNow.Add(durationSpan)), num, CollapsedActivityProfile.Resting, isPlaying: false, null, null, null, "message", messageSettings);
		try
		{
			if (repeatEnabled)
			{
				PillActivityArbiter.Instance.PublishMessageSession(snapshot);
			}
			else
			{
				PillActivityArbiter.Instance.PublishMessageTransient(snapshot, durationSpan, userRequested: true);
			}
			bool flag;
			if (repeatEnabled)
			{
				flag = PillActivityArbiter.Instance.MessageSessionId == _liveId;
			}
			else
			{
				PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
				flag = currentActivity != null && currentActivity.Id == _liveId;
			}
			if (!flag)
			{
				throw new InvalidOperationException("the notch did not take the banner");
			}
		}
		catch (Exception ex)
		{
			IsLive = false;
			_liveId = null;
			LastError = "Couldn't flash. Try again";
			Log("Message flash failed: " + ex.GetType().Name + ": " + ex.Message);
		}
		SaveSoon();
		Notify();
		return IsLive;
	}

	public bool ShowNotice(string text, TimeSpan duration)
	{
		text = MessageText.Clean(text);
		if (text.Length == 0)
		{
			return false;
		}
		MessageSettings messageSettings = new MessageSettings();
		messageSettings.Text = text;
		messageSettings.Animation = MessageAnimationMode.Fade;
		MessageSettings messageSettings2 = messageSettings;
		messageSettings2.Sanitize();
		string text2 = "notice_" + ++_noticeId;
		PillActivitySnapshot snapshot = new PillActivitySnapshot(text2, "message", ActivityTier.Tier1_Transient, ActivityType.CompanionMessage, text, "Ayn", text, (double sz, Brush br) => VectorIcons.CreateMessageIcon(sz, br), null, MessageRenderer.CreateForegroundBrush(messageSettings2), null, DateTime.UtcNow, DateTime.UtcNow.Add(duration), 0L, CollapsedActivityProfile.Resting, isPlaying: false, null, null, null, "message", messageSettings2);
		try
		{
			PillActivityArbiter.Instance.PublishMessageTransient(snapshot, duration, userRequested: true);
			PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
			return currentActivity != null && currentActivity.Id == text2;
		}
		catch (Exception ex)
		{
			Log("Notice failed: " + ex.GetType().Name + ": " + ex.Message);
			return false;
		}
	}

	public void TakeDown()
	{
		if (IsLive)
		{
			IsLive = false;
			IsPinned = false;
			_liveId = null;
			_session++;
			try
			{
				PillActivityArbiter.Instance.ClearMessage();
			}
			catch (Exception ex)
			{
				Log("Message take down failed: " + ex.GetType().Name + ": " + ex.Message);
			}
			Notify();
		}
	}

	public void SaveSoon()
	{
		_dirty = true;
		try
		{
			if (_saveTimer == null)
			{
				_saveTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(400.0)
				};
				_saveTimer.Tick += delegate
				{
					_saveTimer.Stop();
					Save(sync: false);
				};
			}
			_saveTimer.Stop();
			_saveTimer.Start();
		}
		catch (Exception ex)
		{
			Log("Message save timer failed: " + ex.GetType().Name);
			Save(sync: false);
		}
	}

	public void Flush()
	{
		if (_saveTimer != null)
		{
			_saveTimer.Stop();
		}
		if (_dirty)
		{
			Save(sync: false);
		}
	}

	public void FlushNow()
	{
		if (_saveTimer != null)
		{
			_saveTimer.Stop();
		}
		bool flag;
		lock (_queueLock)
		{
			flag = _pendingJson != null || _writerQueued;
		}
		if (_dirty || flag)
		{
			Save(sync: true);
		}
	}

	public void Shutdown()
	{
		if (_saveTimer != null)
		{
			_saveTimer.Stop();
		}
		bool flag;
		lock (_queueLock)
		{
			flag = _pendingJson != null || _writerQueued;
		}
		if (_dirty || flag)
		{
			TransactionLogger.Error("Message settings changed after shutdown verification; not written after commit.");
		}
		TakeDown();
		if (_arbiterHooked)
		{
			try
			{
				PillActivityArbiter.Instance.ActivityChanged -= OnArbiterChanged;
			}
			catch
			{
			}
			_arbiterHooked = false;
		}
	}

	private void HookArbiter()
	{
		if (!_arbiterHooked)
		{
			PillActivityArbiter.Instance.ActivityChanged += OnArbiterChanged;
			_arbiterHooked = true;
		}
	}

	private void HookExit()
	{
		if (_exitHooked)
		{
			return;
		}
		Application current = Application.Current;
		if (current == null)
		{
			return;
		}
		try
		{
			current.Exit += delegate
			{
				Shutdown();
			};
			_exitHooked = true;
		}
		catch
		{
		}
	}

	private void OnArbiterChanged(object sender, PillActivityChangedEventArgs e)
	{
		Application current = Application.Current;
		if (current != null && !current.Dispatcher.CheckAccess())
		{
			current.Dispatcher.BeginInvoke(new Action(CheckLiveSafe));
		}
		else
		{
			CheckLiveSafe();
		}
	}

	private void CheckLiveSafe()
	{
		try
		{
			CheckLive();
		}
		catch (Exception ex)
		{
			Log("Message live check failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private void CheckLive()
	{
		if (!IsLive)
		{
			return;
		}
		if (IsPinned)
		{
			if (PillActivityArbiter.Instance.MessageSessionId == _liveId)
			{
				return;
			}
		}
		else
		{
			PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
			if (currentActivity != null && currentActivity.Id == _liveId)
			{
				return;
			}
		}
		IsPinned = false;
		IsLive = false;
		_liveId = null;
		Notify();
	}

	private void Save(bool sync)
	{
		if (_loadFailed)
		{
			return;
		}
		string text;
		try
		{
			text = new JavaScriptSerializer().Serialize(Settings);
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_storagePath, error);
			return;
		}
		_dirty = false;
		long num = ++_saveSeq;
		if (!sync)
		{
			lock (_queueLock)
			{
				_pendingJson = text;
				_pendingSeq = num;
				if (_writerQueued)
				{
					return;
				}
				_writerQueued = true;
			}
			try
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					WriteQueued();
				});
				return;
			}
			catch (Exception ex)
			{
				lock (_queueLock)
				{
					_writerQueued = false;
					_pendingJson = null;
				}
				Log("Message settings save queue failed: " + ex.GetType().Name);
				Write(text, num);
				return;
			}
		}
		lock (_queueLock)
		{
			_pendingJson = null;
		}
		Write(text, num);
	}

	private void WriteQueued()
	{
		while (true)
		{
			string pendingJson;
			long pendingSeq;
			lock (_queueLock)
			{
				pendingJson = _pendingJson;
				pendingSeq = _pendingSeq;
				_pendingJson = null;
				if (pendingJson == null)
				{
					_writerQueued = false;
					break;
				}
			}
			Write(pendingJson, pendingSeq);
		}
	}

	private void Write(string json, long seq)
	{
		lock (_writeLock)
		{
			if (seq <= _writtenSeq)
			{
				return;
			}
			try
			{
				if (!PersistenceHelper.SafeWriteAllText(_storagePath, json))
				{
					return;
				}
			}
			catch (Exception ex)
			{
				Log("Message settings save failed: " + ex.GetType().Name + ": " + ex.Message);
			}
			_writtenSeq = seq;
		}
	}

	private MessageSettings Load(string path)
	{
		MessageSettings messageSettings = null;
		try
		{
			messageSettings = PersistenceHelper.ReadWithBackup(path, (string text) => new JavaScriptSerializer().Deserialize<MessageSettings>(text));
		}
		catch (Exception ex)
		{
			_loadFailed = true;
			Log("Message settings unreadable; saving disabled: " + ex.GetType().Name);
		}
		if (messageSettings == null)
		{
			messageSettings = new MessageSettings();
		}
		try
		{
			messageSettings.Sanitize();
		}
		catch (Exception ex2)
		{
			Log("Message settings invalid, using defaults: " + ex2.GetType().Name);
			messageSettings = new MessageSettings();
		}
		return messageSettings;
	}

	private void Notify()
	{
		EventHandler stateChanged = this.StateChanged;
		if (stateChanged == null)
		{
			return;
		}
		try
		{
			stateChanged(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			Log("Message state listener failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static void Log(string msg)
	{
		try
		{
			TransactionLogger.Warn(msg);
		}
		catch
		{
		}
	}
}
