using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services.Media;

public sealed class MediaService : IDisposable
{
	[Flags]
	private enum Dirty
	{
		None = 0,
		Session = 1,
		Playback = 2,
		Properties = 4,
		Timeline = 8,
		All = 0xF
	}

	private const int CoalesceMs = 35;

	private const int ResyncMs = 750;

	private const int TimelinePublishMs = 1000;

	private static readonly Lazy<MediaService> _instance = new Lazy<MediaService>(() => new MediaService());

	private static readonly TimeSpan PauseClearAfter = TimeSpan.FromSeconds(8.0);

	private int _dirty;

	private readonly AutoResetEvent _signal = new AutoResetEvent(initialState: false);

	private readonly ConcurrentQueue<Action> _commands = new ConcurrentQueue<Action>();

	private int _commandCount;

	private int _notificationPending;

	private long _generation;

	private long _artworkStartedMs;

	private bool _present;

	private bool _canPlayPause;

	private bool _canNext;

	private bool _canPrevious;

	private bool _canSeek;

	private bool _canToggle;

	private volatile MediaViewState _view;

	private readonly Thread _worker;

	private volatile bool _stop;

	private readonly Backoff _initBackoff = new Backoff();

	private object _sessionManager;

	private object _currentSession;

	private string _currentAppId;

	private readonly Dictionary<object, DateTime> _lastActive = new Dictionary<object, DateTime>();

	private readonly Dictionary<object, int> _lastStatus = new Dictionary<object, int>();

	private readonly Dictionary<object, double> _lastPosition = new Dictionary<object, double>();

	private readonly Dictionary<object, long> _sessionIds = new Dictionary<object, long>();

	private long _nextSessionId;

	private long _currentSessionId;

	private MethodInfo _asTaskGeneric;

	private static MethodInfo _asStreamForRead;

	private static MethodInfo _asStreamGeneric;

	private object _currentSessionChangedToken;

	private object _sessionsChangedToken;

	private object _playbackInfoToken;

	private object _mediaPropertiesToken;

	private object _timelinePropertiesToken;

	private Delegate _sessionChangedDelegate;

	private Delegate _sessionsChangedDelegate;

	private Delegate _playbackInfoDelegate;

	private Delegate _mediaPropertiesDelegate;

	private Delegate _timelinePropertiesDelegate;

	private readonly Dictionary<string, BitmapSource> _artworkCache = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);

	private string _trackSignature;

	private int _artworkAttempts;

	private long _artworkRetryAt;

	private string _artworkHash;

	private DateTime? _pausedSinceUtc;

	private DateTime? _noMediaSinceUtc;

	private bool _clearedWhilePaused;

	private string _lastPublishKey;

	private long _lastTimelinePublishMs;

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private DateTime _positionUpdatedUtc = DateTime.UtcNow;

	private volatile BitmapSource _cachedArtwork;

	public static MediaService Instance => _instance.Value;

	public MediaViewState View => _view;

	public bool IsPlaying { get; private set; }

	public double CurrentProgress { get; private set; }

	public TimeSpan CurrentPosition { get; set; }

	public TimeSpan CurrentDuration { get; set; }

	public string CurrentTitle { get; private set; }

	public string CurrentArtist { get; private set; }

	public BitmapSource CurrentArtwork => _cachedArtwork;

	public bool IsMonitoringSuppressed { get; set; }

	public event EventHandler MediaStateChanged;

	[Conditional("DEBUG")]
	private static void LogDiagnostic(string message)
	{
		LogWriter.Enqueue("media_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
	}

	private MediaService()
		: this(startWorker: true)
	{
	}

	internal static MediaService CreateForQa()
	{
		if (!AppPaths.IsSandboxed)
		{
			throw new InvalidOperationException("Disposable media fixture required.");
		}
		return new MediaService(startWorker: false);
	}

	private MediaService(bool startWorker)
	{
		CurrentProgress = -1.0;
		_worker = new Thread(Run)
		{
			IsBackground = true,
			Name = "Ayn.Media"
		};
		_worker.SetApartmentState(ApartmentState.MTA);
		if (startWorker)
		{
			_worker.Start();
		}
		NotchBehaviorConfig.Instance.ConfigChanged += OnPolicyChanged;
	}

	private void OnPolicyChanged(object sender, EventArgs e)
	{
		MarkDirty(Dirty.All);
	}

	internal static bool IsBrowserSource(string source)
	{
		string text = (source ?? "").ToLowerInvariant();
		string[] array = new string[9] { "chrome", "msedge", "microsoftedge", "firefox", "brave", "opera", "vivaldi", "arcbrowser", "zenbrowser" };
		string[] array2 = array;
		foreach (string value in array2)
		{
			if (text.Contains(value))
			{
				return true;
			}
		}
		return false;
	}

	private static string DiagnosticSource(string source)
	{
		return Path.GetFileName(source ?? "").Replace('.', '_');
	}

	internal static bool IsEligibleSource(string source, bool browserEnabled, bool reportsMusic)
	{
		if (IsBrowserSource(source))
		{
			return browserEnabled;
		}
		return true;
	}

	private bool EligibleSession(object session)
	{
		if (session == null)
		{
			return false;
		}
		return IsEligibleSource(GetSessionAppId(session), NotchBehaviorConfig.Instance.ShowBrowserMediaInNotch, reportsMusic: false);
	}

	private void MarkDirty(Dirty d)
	{
		int dirty;
		int value;
		do
		{
			dirty = _dirty;
			value = dirty | (int)d;
		}
		while (Interlocked.CompareExchange(ref _dirty, value, dirty) != dirty);
		try
		{
			_signal.Set();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private void OnCurrentSessionChanged(object sender, object args)
	{
		MarkDirty(Dirty.Session);
	}

	private void OnSessionsChanged(object sender, object args)
	{
		MarkDirty(Dirty.Session);
	}

	private void OnPlaybackInfoChanged(object sender, object args)
	{
		if (sender == null || object.ReferenceEquals(sender, _currentSession))
		{
			MarkDirty(Dirty.Session | Dirty.Playback);
		}
	}

	private void OnMediaPropertiesChanged(object sender, object args)
	{
		if (sender == null || object.ReferenceEquals(sender, _currentSession))
		{
			MarkDirty(Dirty.Playback | Dirty.Properties);
		}
	}

	private void OnTimelinePropertiesChanged(object sender, object args)
	{
		if (sender == null || object.ReferenceEquals(sender, _currentSession))
		{
			MarkDirty(Dirty.Timeline);
		}
	}

	private void Run()
	{
		try
		{
			while (!_stop)
			{
				if (_sessionManager == null)
				{
					if (!Guard.Run("media init", TryInitialize, fallback: false))
					{
						TimeSpan timeout = _initBackoff.Failure();
						_signal.WaitOne(timeout);
						continue;
					}
					_initBackoff.Success();
					MarkDirty(Dirty.All);
				}
				bool flag = _signal.WaitOne(NextWaitMs());
				if (_stop)
				{
					break;
				}
				if (flag)
				{
					long elapsedMilliseconds = _clock.ElapsedMilliseconds;
					while (_clock.ElapsedMilliseconds - elapsedMilliseconds < 150 && _signal.WaitOne(35))
					{
						if (_stop)
						{
							return;
						}
					}
				}
				Dirty d = (Dirty)Interlocked.Exchange(ref _dirty, 0);
				if (!flag && d == Dirty.None)
				{
					d = Dirty.Session | Dirty.Playback;
				}
				if (!IsMonitoringSuppressed)
				{
					Action result;
					while (_commands.TryDequeue(out result))
					{
						Interlocked.Decrement(ref _commandCount);
						Guard.Run("media command", result);
					}
					d |= (Dirty)Interlocked.Exchange(ref _dirty, 0);
					if (!Guard.Run("media refresh", delegate
					{
						Refresh(d);
					}))
					{
						DropSessionManager();
					}
					Guard.Run("media pause", CheckPauseClear);
				}
			}
		}
		finally
		{
			DropSessionManager();
		}
	}

	private int NextWaitMs()
	{
		int num = 750;
		if (_noMediaSinceUtc.HasValue)
		{
			num = Math.Min(num, 350);
		}
		if (_artworkRetryAt > 0)
		{
			num = (int)Math.Max(50L, Math.Min(num, _artworkRetryAt - _clock.ElapsedMilliseconds));
		}
		if (((uint)_dirty & 8u) != 0)
		{
			num = (int)Math.Max(50L, Math.Min(num, 1000 - (_clock.ElapsedMilliseconds - _lastTimelinePublishMs)));
		}
		if (!IsPlaying && _pausedSinceUtc.HasValue && !_clearedWhilePaused)
		{
			double totalMilliseconds = (PauseClearAfter - (DateTime.UtcNow - _pausedSinceUtc.Value)).TotalMilliseconds;
			return (int)Math.Max(50.0, Math.Min(num, totalMilliseconds + 20.0));
		}
		return num;
	}

	private bool TryInitialize()
	{
		Type type = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media, ContentType=WindowsRuntime");
		if (type == null)
		{
			return false;
		}
		Assembly assembly = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
		Type type2 = assembly.GetType("System.WindowsRuntimeSystemExtensions");
		MethodInfo[] methods = type2.GetMethods(BindingFlags.Static | BindingFlags.Public);
		foreach (MethodInfo methodInfo in methods)
		{
			if (methodInfo.Name == "AsTask" && methodInfo.IsGenericMethod && methodInfo.GetGenericArguments().Length == 1 && methodInfo.GetParameters().Length == 1 && methodInfo.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1")
			{
				_asTaskGeneric = methodInfo;
				break;
			}
		}
		Type type3 = assembly.GetType("System.IO.WindowsRuntimeStreamExtensions");
		if (type3 != null)
		{
			MethodInfo[] methods2 = type3.GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo2 in methods2)
			{
				if (methodInfo2.Name == "AsStreamForRead" && methodInfo2.GetParameters().Length == 1)
				{
					_asStreamForRead = methodInfo2;
				}
				else if (methodInfo2.Name == "AsStream" && methodInfo2.GetParameters().Length == 1 && _asStreamGeneric == null)
				{
					_asStreamGeneric = methodInfo2;
				}
			}
		}
		MethodInfo method = type.GetMethod("RequestAsync", BindingFlags.Static | BindingFlags.Public);
		object obj = AwaitAsync(type, method.Invoke(null, null), 3000);
		if (obj == null)
		{
			return false;
		}
		_sessionManager = obj;
		_sessionChangedDelegate = MakeDelegate(obj, "CurrentSessionChanged", "OnCurrentSessionChanged");
		_currentSessionChangedToken = Subscribe(obj, "CurrentSessionChanged", _sessionChangedDelegate);
		_sessionsChangedDelegate = MakeDelegate(obj, "SessionsChanged", "OnSessionsChanged");
		_sessionsChangedToken = Subscribe(obj, "SessionsChanged", _sessionsChangedDelegate);
		return true;
	}

	private void DropSessionManager()
	{
		Guard.Run("media unhook", delegate
		{
			UnhookSession();
			if (_sessionManager != null)
			{
				Unsubscribe(_sessionManager, "CurrentSessionChanged", _currentSessionChangedToken);
				Unsubscribe(_sessionManager, "SessionsChanged", _sessionsChangedToken);
			}
		});
		_currentSessionChangedToken = (_sessionsChangedToken = null);
		_sessionManager = null;
		_lastActive.Clear();
		_lastStatus.Clear();
		_lastPosition.Clear();
		_sessionIds.Clear();
	}

	private void Refresh(Dirty d)
	{
		if (_sessionManager == null)
		{
			return;
		}
		if (_artworkRetryAt > 0 && _clock.ElapsedMilliseconds >= _artworkRetryAt)
		{
			_artworkRetryAt = 0L;
			d |= Dirty.Properties;
		}
		if ((d & Dirty.Session) != 0)
		{
			object obj = FindSession();
			if (!object.ReferenceEquals(obj, _currentSession))
			{
				UnhookSession();
				_currentSession = obj;
				_pausedSinceUtc = null;
				_clearedWhilePaused = false;
				_generation++;
				_currentAppId = GetSessionAppId(obj);
				_currentSessionId = ((obj != null && _sessionIds.ContainsKey(obj)) ? _sessionIds[obj] : 0);
				TransactionLogger.Info("Media selected: session=" + _currentSessionId + " source=" + DiagnosticSource(_currentAppId));
				if (obj != null)
				{
					HookSession(obj);
				}
				_trackSignature = null;
				_cachedArtwork = null;
				_artworkHash = null;
				_artworkAttempts = 0;
				_artworkRetryAt = 0L;
				_artworkStartedMs = _clock.ElapsedMilliseconds;
				string currentTitle = (CurrentArtist = null);
				CurrentTitle = currentTitle;
				d = Dirty.All;
			}
		}
		if (_currentSession == null)
		{
			ConfirmStopped();
			return;
		}
		bool isPlaying = IsPlaying;
		if ((d & Dirty.Playback) != 0)
		{
			int num = ReadPlaybackStatus(_currentSession);
			if (num != 4 && num != 5)
			{
				ConfirmStopped();
				return;
			}
			_noMediaSinceUtc = null;
			IsPlaying = num == 4;
			_present = true;
			ReadCapabilities();
			if (IsPlaying)
			{
				_pausedSinceUtc = null;
				_clearedWhilePaused = false;
			}
			else if (isPlaying || !_pausedSinceUtc.HasValue)
			{
				_pausedSinceUtc = DateTime.UtcNow;
			}
		}
		if ((d & Dirty.Properties) != 0 || _trackSignature == null)
		{
			ReadProperties();
		}
		if ((d & (Dirty.Playback | Dirty.Properties | Dirty.Timeline)) != 0)
		{
			ReadTimeline();
		}
		PublishView();
		if (_clearedWhilePaused && !IsPlaying)
		{
			return;
		}
		bool flag = d == Dirty.Timeline;
		NotchBehaviorConfig instance = NotchBehaviorConfig.Instance;
		string text2 = _currentAppId + "|" + (CurrentTitle ?? "") + "|" + (CurrentArtist ?? "") + "|" + IsPlaying + "|" + _artworkHash + "|" + (int)(CurrentProgress * 100.0) + "|" + instance.ShowBrowserMediaInNotch + "|" + instance.ShowMediaWhenPlaybackStarts + "|" + _canPlayPause + _canNext + _canPrevious + _canSeek;
		if (text2 == _lastPublishKey)
		{
			return;
		}
		if (flag && _clock.ElapsedMilliseconds - _lastTimelinePublishMs < 1000)
		{
			MarkDirtyLater();
			return;
		}
		_lastPublishKey = text2;
		_lastTimelinePublishMs = _clock.ElapsedMilliseconds;
		if (!flag)
		{
			TransactionLogger.Info("Media state: source=" + (IsBrowserSource(_currentAppId) ? "browser" : "player") + " playing=" + IsPlaying + " artwork=" + (_cachedArtwork != null) + " playPause=" + _canPlayPause + " previous=" + _canPrevious + " next=" + _canNext + " seek=" + _canSeek);
		}
		DispatchMediaStateUpdate();
	}

	private void MarkDirtyLater()
	{
		int dirty;
		int value;
		do
		{
			dirty = _dirty;
			value = dirty | 8;
		}
		while (Interlocked.CompareExchange(ref _dirty, value, dirty) != dirty);
	}

	private void SetStopped()
	{
		bool flag = IsPlaying || CurrentTitle != null || _lastPublishKey != null;
		IsPlaying = false;
		_present = false;
		_pausedSinceUtc = null;
		_clearedWhilePaused = false;
		_lastPublishKey = null;
		_trackSignature = null;
		string currentTitle = (CurrentArtist = null);
		CurrentTitle = currentTitle;
		CurrentProgress = -1.0;
		_cachedArtwork = null;
		_artworkRetryAt = 0L;
		TimeSpan currentPosition = (CurrentDuration = TimeSpan.Zero);
		CurrentPosition = currentPosition;
		PublishView();
		if (flag)
		{
			DynamicActivityService.Instance.ClearPersistentActivity(ActivityType.Media, 0L);
			RaiseChanged();
		}
	}

	private void CheckPauseClear()
	{
		if (!IsPlaying && _pausedSinceUtc.HasValue && !_clearedWhilePaused && !(DateTime.UtcNow - _pausedSinceUtc.Value < PauseClearAfter))
		{
			_clearedWhilePaused = true;
			DynamicActivityService.Instance.ClearPersistentActivity(ActivityType.Media, 0L);
			RaiseChanged();
		}
	}

	private object FindSession()
	{
		MethodInfo method = _sessionManager.GetType().GetMethod("GetCurrentSession");
		object obj = ((method != null) ? method.Invoke(_sessionManager, null) : null);
		MethodInfo method2 = _sessionManager.GetType().GetMethod("GetSessions");
		IEnumerable enumerable = ((method2 != null) ? method2.Invoke(_sessionManager, null) : null) as IEnumerable;
		HashSet<object> hashSet = new HashSet<object>();
		object obj2 = null;
		int num = 0;
		DateTime dateTime = DateTime.MinValue;
		if (enumerable == null)
		{
			return obj;
		}
		foreach (object item in enumerable)
		{
			hashSet.Add(item);
			if (!_sessionIds.ContainsKey(item))
			{
				_sessionIds[item] = ++_nextSessionId;
			}
			try
			{
				int num2 = ReadPlaybackStatus(item);
				DateTime value;
				bool flag = _lastActive.TryGetValue(item, out value);
				if (!flag)
				{
					value = DateTime.MinValue;
				}
				if (_lastStatus.TryGetValue(item, out var value2) && value2 != num2)
				{
					value = DateTime.UtcNow;
				}
				_lastStatus[item] = num2;
				object obj3 = item.GetType().GetMethod("GetTimelineProperties").Invoke(item, null);
				PropertyInfo propertyInfo = obj3?.GetType().GetProperty("LastUpdatedTime");
				object obj4 = ((propertyInfo != null) ? propertyInfo.GetValue(obj3, null) : null);
				if (!flag && obj4 is DateTimeOffset { UtcDateTime: var utcDateTime } && utcDateTime <= DateTime.UtcNow && utcDateTime > value)
				{
					value = utcDateTime;
				}
				PropertyInfo propertyInfo2 = obj3?.GetType().GetProperty("Position");
				if (((propertyInfo2 != null) ? propertyInfo2.GetValue(obj3, null) : null) is TimeSpan { TotalSeconds: var totalSeconds })
				{
					if (flag && num2 == 5 && _lastPosition.TryGetValue(item, out var value3) && Math.Abs(totalSeconds - value3) > 0.5)
					{
						value = DateTime.UtcNow;
					}
					_lastPosition[item] = totalSeconds;
				}
				_lastActive[item] = value;
				if ((num2 == 4 || num2 == 5) && (obj2 == null || (num2 == 4 && num != 4) || (num2 == num && (value > dateTime || (value == dateTime && object.ReferenceEquals(item, obj))))))
				{
					obj2 = item;
					num = num2;
					dateTime = value;
				}
			}
			catch
			{
			}
		}
		foreach (object item2 in new List<object>(_sessionIds.Keys))
		{
			if (!hashSet.Contains(item2))
			{
				_lastActive.Remove(item2);
				_lastStatus.Remove(item2);
				_lastPosition.Remove(item2);
				_sessionIds.Remove(item2);
			}
		}
		return obj2;
	}

	private void ConfirmStopped()
	{
		if (_present && !_noMediaSinceUtc.HasValue)
		{
			_noMediaSinceUtc = DateTime.UtcNow;
		}
		else if (!_present || !(DateTime.UtcNow - _noMediaSinceUtc.Value < TimeSpan.FromMilliseconds(350.0)))
		{
			_noMediaSinceUtc = null;
			SetStopped();
		}
	}

	private static int ReadPlaybackStatus(object session)
	{
		if (session == null)
		{
			return 0;
		}
		MethodInfo method = session.GetType().GetMethod("GetPlaybackInfo");
		object obj = ((method != null) ? method.Invoke(session, null) : null);
		if (obj == null)
		{
			return 0;
		}
		PropertyInfo property = obj.GetType().GetProperty("PlaybackStatus");
		if (!(property != null))
		{
			return 0;
		}
		return Convert.ToInt32(property.GetValue(obj, null));
	}

	private void ReadProperties()
	{
		Type type = _currentSession.GetType();
		MethodInfo method = type.GetMethod("TryGetMediaPropertiesAsync");
		if (method == null)
		{
			return;
		}
		object obj = AwaitAsync(method.ReturnType.GetGenericArguments()[0], method.Invoke(_currentSession, null), 2000);
		if (obj == null)
		{
			ScheduleArtworkRetry();
			return;
		}
		Type type2 = obj.GetType();
		string text = ((type2.GetProperty("Title") != null) ? (type2.GetProperty("Title").GetValue(obj, null) as string) : null);
		string text2 = ((type2.GetProperty("Artist") != null) ? (type2.GetProperty("Artist").GetValue(obj, null) as string) : null);
		if (string.IsNullOrWhiteSpace(text2) && type2.GetProperty("AlbumArtist") != null)
		{
			text2 = type2.GetProperty("AlbumArtist").GetValue(obj, null) as string;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "Now Playing";
		}
		if (string.IsNullOrWhiteSpace(text2))
		{
			text2 = null;
		}
		CurrentTitle = text;
		CurrentArtist = text2;
		string text3 = _currentAppId + "|" + text + "|" + text2;
		if (text3 != _trackSignature)
		{
			_generation++;
			_clearedWhilePaused = false;
			_artworkAttempts = 0;
			_artworkRetryAt = 0L;
			_artworkHash = null;
			_cachedArtwork = null;
			_artworkStartedMs = _clock.ElapsedMilliseconds;
		}
		if (_trackSignature != null && text3 != _trackSignature)
		{
			_lastActive[_currentSession] = DateTime.UtcNow;
		}
		_trackSignature = text3;
		_artworkCache.TryGetValue(text3, out var value);
		BitmapSource bitmapSource = ExtractThumbnailBitmap(obj);
		if (bitmapSource != null)
		{
			value = bitmapSource;
			if (_artworkCache.Count >= 32 && !_artworkCache.ContainsKey(text3))
			{
				_artworkCache.Clear();
			}
			_artworkCache[text3] = value;
			_artworkRetryAt = 0L;
			TransactionLogger.Info("Media artwork: source=SMTC thumbnail attempt=" + (_artworkAttempts + 1) + " elapsedMs=" + (_clock.ElapsedMilliseconds - _artworkStartedMs));
		}
		_cachedArtwork = value;
		if (value == null)
		{
			ScheduleArtworkRetry();
		}
	}

	private void ScheduleArtworkRetry()
	{
		if (++_artworkAttempts < 4)
		{
			_artworkRetryAt = _clock.ElapsedMilliseconds + 750;
			return;
		}
		_artworkRetryAt = 0L;
		TransactionLogger.Info("Media artwork: source=placeholder attempts=4");
	}

	private void ReadTimeline()
	{
		double currentProgress = -1.0;
		CurrentDuration = TimeSpan.Zero;
		MethodInfo method = _currentSession.GetType().GetMethod("GetTimelineProperties");
		object obj = ((method != null) ? method.Invoke(_currentSession, null) : null);
		if (obj != null)
		{
			Type type = obj.GetType();
			object obj2 = ((type.GetProperty("Position") != null) ? type.GetProperty("Position").GetValue(obj, null) : null);
			object obj3 = ((type.GetProperty("EndTime") != null) ? type.GetProperty("EndTime").GetValue(obj, null) : null);
			object obj4 = ((type.GetProperty("StartTime") != null) ? type.GetProperty("StartTime").GetValue(obj, null) : null);
			if (obj2 is TimeSpan && obj3 is TimeSpan)
			{
				TimeSpan timeSpan = ((obj4 is TimeSpan) ? ((TimeSpan)obj4) : TimeSpan.Zero);
				TimeSpan currentPosition = (TimeSpan)obj2 - timeSpan;
				TimeSpan timeSpan2 = (TimeSpan)obj3 - timeSpan;
				CurrentPosition = currentPosition;
				CurrentDuration = timeSpan2;
				_positionUpdatedUtc = DateTime.UtcNow;
				try
				{
					if (((type.GetProperty("LastUpdatedTime") != null) ? type.GetProperty("LastUpdatedTime").GetValue(obj, null) : null) is DateTimeOffset { UtcDateTime: var utcDateTime } && utcDateTime <= DateTime.UtcNow.AddSeconds(1.0) && DateTime.UtcNow - utcDateTime < timeSpan2)
					{
						_positionUpdatedUtc = utcDateTime;
					}
				}
				catch
				{
				}
				if (timeSpan2.TotalSeconds > 1.0)
				{
					currentProgress = Math.Max(0.0, Math.Min(1.0, currentPosition.TotalSeconds / timeSpan2.TotalSeconds));
				}
			}
		}
		CurrentProgress = currentProgress;
	}

	public void DispatchMediaStateUpdate()
	{
		if (!NotchBehaviorConfig.Instance.ShowMediaWhenPlaybackStarts || !EligibleSession(_currentSession))
		{
			DynamicActivityService.Instance.ClearPersistentActivity(ActivityType.Media, 0L);
			RaiseChanged();
			return;
		}
		Guard.Run("media publish", delegate
		{
			string title = ((!string.IsNullOrEmpty(CurrentTitle)) ? CurrentTitle : "Media");
			string subtitle = ((!IsPlaying) ? "Paused" : (string.IsNullOrEmpty(CurrentArtist) ? "Playing" : CurrentArtist));
			DynamicActivityService.Instance.PublishActivity(new NotchActivity
			{
				Id = "media_session",
				Type = ActivityType.Media,
				Priority = ActivityPriority.P1_Media,
				Mode = ActivityMode.Persistent,
				Duration = TimeSpan.FromSeconds(3.0),
				Title = title,
				Subtitle = subtitle,
				Artwork = _cachedArtwork,
				IconFactory = (double sz, System.Windows.Media.Brush br) => VectorIcons.CreateMusicIcon(sz, br),
				IsPlaying = IsPlaying,
				PlaybackProgress = CurrentProgress,
				SourceAppId = _currentAppId,
				NavigateToToolId = "media",
				PlayPauseAction = TogglePlayPause,
				NextAction = SkipNext,
				PreviousAction = SkipPrevious,
				Payload = new MediaTrackInfo
				{
					Artist = CurrentArtist,
					PositionSeconds = CurrentPosition.TotalSeconds,
					DurationSeconds = CurrentDuration.TotalSeconds,
					UpdatedUtc = _positionUpdatedUtc,
					Playing = IsPlaying
				}
			});
		});
		RaiseChanged();
	}

	private void RaiseChanged()
	{
		Application current = Application.Current;
		if (this.MediaStateChanged == null || current == null || Interlocked.Exchange(ref _notificationPending, 1) != 0)
		{
			return;
		}
		try
		{
			current.Dispatcher.BeginInvoke((Action)delegate
			{
				Interlocked.Exchange(ref _notificationPending, 0);
				EventHandler h = this.MediaStateChanged;
				if (h != null)
				{
					Guard.Run("media listeners", delegate
					{
						h(this, EventArgs.Empty);
					});
				}
			});
		}
		catch
		{
			Interlocked.Exchange(ref _notificationPending, 0);
		}
	}

	private void PublishView()
	{
		_view = new MediaViewState(_present, IsPlaying, CurrentTitle, CurrentArtist, _currentAppId, _cachedArtwork, new MediaTrackInfo
		{
			Artist = CurrentArtist,
			PositionSeconds = CurrentPosition.TotalSeconds,
			DurationSeconds = CurrentDuration.TotalSeconds,
			UpdatedUtc = _positionUpdatedUtc,
			Playing = IsPlaying
		}, _canPlayPause, _canNext, _canPrevious, _canSeek, _generation, _currentSessionId, _pausedSinceUtc);
	}

	private void ReadCapabilities()
	{
		ReadCapabilities(_currentSession, IsPlaying, out _canToggle, out _canPlayPause, out _canNext, out _canPrevious, out _canSeek);
	}

	private static void ReadCapabilities(object session, bool playing, out bool toggle, out bool playPause, out bool next, out bool previous, out bool seek)
	{
		object obj = session.GetType().GetMethod("GetPlaybackInfo").Invoke(session, null);
		object controls = obj.GetType().GetProperty("Controls").GetValue(obj, null);
		Func<string, bool> func = delegate(string name)
		{
			PropertyInfo propertyInfo = ((controls != null) ? controls.GetType().GetProperty(name) : null);
			return propertyInfo != null && (bool)propertyInfo.GetValue(controls, null);
		};
		toggle = func("IsPlayPauseToggleEnabled");
		playPause = toggle || func(playing ? "IsPauseEnabled" : "IsPlayEnabled");
		next = func("IsNextEnabled");
		previous = func("IsPreviousEnabled");
		seek = func("IsPlaybackPositionEnabled");
	}

	public void TogglePlayPause()
	{
		TogglePlayPause(View);
	}

	public void SkipNext()
	{
		SkipNext(View);
	}

	public void SkipPrevious()
	{
		SkipPrevious(View);
	}

	public void TogglePlayPause(MediaViewState state)
	{
		Command("toggle", state);
	}

	public void SkipNext(MediaViewState state)
	{
		Command("TrySkipNextAsync", state);
	}

	public void SkipPrevious(MediaViewState state)
	{
		Command("TrySkipPreviousAsync", state);
	}

	public void Seek(double fraction)
	{
		Seek(fraction, View);
	}

	public void Seek(double fraction, MediaViewState state)
	{
		if (state != null && state.CanSeek && !(state.Timeline.DurationSeconds <= 0.0) && !double.IsNaN(fraction) && !double.IsInfinity(fraction))
		{
			Command("TryChangePlaybackPositionAsync", state, (long)(Math.Max(0.0, Math.Min(1.0, fraction)) * state.Timeline.DurationSeconds * 10000000.0));
		}
	}

	private void Command(string method, MediaViewState state, long? seekTicks = null)
	{
		if (state == null || !state.Present)
		{
			return;
		}
		if (Interlocked.Increment(ref _commandCount) > 8)
		{
			Interlocked.Decrement(ref _commandCount);
			TransactionLogger.Info("Media command: session=" + state.SessionId + " result=queue-full");
			RaiseChanged();
			return;
		}
		_commands.Enqueue(delegate
		{
			object obj = null;
			IEnumerable enumerable = ((_sessionManager != null) ? (_sessionManager.GetType().GetMethod("GetSessions").Invoke(_sessionManager, null) as IEnumerable) : null);
			if (enumerable != null)
			{
				foreach (object item in enumerable)
				{
					if (_sessionIds.TryGetValue(item, out var value) && value == state.SessionId)
					{
						obj = item;
						break;
					}
				}
			}
			string text = "Media command: session=" + state.SessionId + " source=" + DiagnosticSource(state.Source) + " action=" + method;
			if (obj == null)
			{
				TransactionLogger.Info(text + " result=stale-session");
				RaiseChanged();
				MarkDirty(Dirty.All);
			}
			else
			{
				bool flag = ReadPlaybackStatus(obj) == 4;
				ReadCapabilities(obj, flag, out var toggle, out var playPause, out var next, out var previous, out var seek);
				if ((method == "toggle" && !playPause) || (method == "TrySkipNextAsync" && !next) || (method == "TrySkipPreviousAsync" && !previous) || (seekTicks.HasValue && !seek))
				{
					TransactionLogger.Info(text + " result=unsupported");
					RaiseChanged();
					MarkDirty(Dirty.Playback);
				}
				else
				{
					string text2 = ((!(method == "toggle")) ? method : (toggle ? "TryTogglePlayPauseAsync" : (flag ? "TryPauseAsync" : "TryPlayAsync")));
					MethodInfo method2 = obj.GetType().GetMethod(text2);
					if (!(method2 == null))
					{
						try
						{
							if (method == "toggle" && object.ReferenceEquals(obj, _currentSession))
							{
								IsPlaying = !flag;
								PublishView();
								DispatchMediaStateUpdate();
							}
							if (seekTicks.HasValue)
							{
								object timeline = obj.GetType().GetMethod("GetTimelineProperties").Invoke(obj, null);
								Func<string, long> func = delegate(string name)
								{
									PropertyInfo property = timeline.GetType().GetProperty(name);
									return (!(((property != null) ? property.GetValue(timeline, null) : null) is TimeSpan timeSpan)) ? 0 : timeSpan.Ticks;
								};
								long num = func("StartTime");
								long num2 = Math.Max(num, func("MinSeekTime"));
								long num3 = func("MaxSeekTime");
								if (num3 <= num2)
								{
									num3 = func("EndTime");
								}
								if (num3 > num2)
								{
									num3 = Math.Max(num2, num3 - ((num3 - num2 > 20000000) ? 10000000 : 10000));
								}
								seekTicks = Math.Max(num2, Math.Min(num3, num + seekTicks.Value));
								text = text + " positionTicks=" + seekTicks.Value;
							}
							object obj2 = AwaitAsync(typeof(bool), method2.Invoke(obj, seekTicks.HasValue ? new object[1] { seekTicks.Value } : null), 1500);
							TransactionLogger.Info(text + " api=" + text2 + " result=" + ((obj2 == null) ? "timeout-or-failure" : obj2.ToString()));
							return;
						}
						catch (Exception ex2)
						{
							TransactionLogger.Info(text + " result=exception type=" + ex2.GetType().Name);
							return;
						}
						finally
						{
							_lastPublishKey = null;
							MarkDirty(Dirty.Session | Dirty.Playback | Dirty.Timeline | ((!(method == "toggle") && !seekTicks.HasValue) ? Dirty.Properties : Dirty.None));
						}
					}
					TransactionLogger.Info(text + " result=missing-api");
					PublishView();
					RaiseChanged();
				}
			}
		});
		try
		{
			_signal.Set();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private Delegate MakeDelegate(object target, string eventName, string handlerName)
	{
		EventInfo @event = target.GetType().GetEvent(eventName);
		if (@event == null)
		{
			return null;
		}
		MethodInfo method = typeof(MediaService).GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
		return Delegate.CreateDelegate(@event.EventHandlerType, this, method);
	}

	private static object Subscribe(object target, string eventName, Delegate handler)
	{
		if (target == null || (object)handler == null)
		{
			return null;
		}
		EventInfo @event = target.GetType().GetEvent(eventName);
		MethodInfo methodInfo = ((@event != null) ? @event.GetAddMethod() : null);
		if (!(methodInfo != null))
		{
			return null;
		}
		return methodInfo.Invoke(target, new object[1] { handler }) ?? handler;
	}

	private static void Unsubscribe(object target, string eventName, object token)
	{
		if (target == null || token == null)
		{
			return;
		}
		try
		{
			EventInfo @event = target.GetType().GetEvent(eventName);
			MethodInfo methodInfo = ((@event != null) ? @event.GetRemoveMethod() : null);
			if (methodInfo != null)
			{
				methodInfo.Invoke(target, new object[1] { token });
			}
		}
		catch
		{
		}
	}

	private void HookSession(object session)
	{
		_playbackInfoDelegate = MakeDelegate(session, "PlaybackInfoChanged", "OnPlaybackInfoChanged");
		_playbackInfoToken = Subscribe(session, "PlaybackInfoChanged", _playbackInfoDelegate);
		_mediaPropertiesDelegate = MakeDelegate(session, "MediaPropertiesChanged", "OnMediaPropertiesChanged");
		_mediaPropertiesToken = Subscribe(session, "MediaPropertiesChanged", _mediaPropertiesDelegate);
		_timelinePropertiesDelegate = MakeDelegate(session, "TimelinePropertiesChanged", "OnTimelinePropertiesChanged");
		_timelinePropertiesToken = Subscribe(session, "TimelinePropertiesChanged", _timelinePropertiesDelegate);
	}

	private void UnhookSession()
	{
		object currentSession = _currentSession;
		if (currentSession != null)
		{
			Unsubscribe(currentSession, "PlaybackInfoChanged", _playbackInfoToken);
			Unsubscribe(currentSession, "MediaPropertiesChanged", _mediaPropertiesToken);
			Unsubscribe(currentSession, "TimelinePropertiesChanged", _timelinePropertiesToken);
		}
		_playbackInfoToken = (_mediaPropertiesToken = (_timelinePropertiesToken = null));
		_playbackInfoDelegate = (_mediaPropertiesDelegate = (_timelinePropertiesDelegate = null));
		_currentSession = null;
	}

	private static string GetSessionAppId(object session)
	{
		if (session == null)
		{
			return string.Empty;
		}
		try
		{
			Type type = session.GetType();
			PropertyInfo propertyInfo = type.GetProperty("SourceAppUserModelId") ?? type.GetProperty("SourceAppId");
			object obj = ((propertyInfo != null) ? propertyInfo.GetValue(session, null) : null);
			return (obj != null) ? obj.ToString() : string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private object AwaitAsync(Type resultType, object asyncOp, int timeoutMs)
	{
		if (_asTaskGeneric == null || asyncOp == null)
		{
			return null;
		}
		try
		{
			Task task = (Task)_asTaskGeneric.MakeGenericMethod(resultType).Invoke(null, new object[1] { asyncOp });
			task.ContinueWith(delegate(Task t)
			{
				_ = t.Exception;
			}, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
			if (!task.Wait(timeoutMs))
			{
				return null;
			}
			return task.GetType().GetProperty("Result").GetValue(task, null);
		}
		catch
		{
			return null;
		}
	}

	private BitmapSource ExtractThumbnailBitmap(object props)
	{
		object obj = null;
		try
		{
			PropertyInfo property = props.GetType().GetProperty("Thumbnail");
			object obj2 = ((property != null) ? property.GetValue(props, null) : null);
			if (obj2 == null)
			{
				return null;
			}
			MethodInfo method = obj2.GetType().GetMethod("OpenReadAsync");
			if (method == null)
			{
				Type[] interfaces = obj2.GetType().GetInterfaces();
				foreach (Type type in interfaces)
				{
					method = type.GetMethod("OpenReadAsync");
					if (method != null)
					{
						break;
					}
				}
			}
			if (method == null)
			{
				return null;
			}
			Type resultType = (method.ReturnType.IsGenericType ? method.ReturnType.GetGenericArguments()[0] : Type.GetType("Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage, ContentType=WindowsRuntime"));
			object obj3 = AwaitAsync(resultType, method.Invoke(obj2, null), 2000);
			obj = obj3;
			MethodInfo methodInfo = _asStreamForRead ?? _asStreamGeneric;
			if (obj3 == null || methodInfo == null)
			{
				return null;
			}
			byte[] array2;
			using (Stream stream = (Stream)methodInfo.Invoke(null, new object[1] { obj3 }))
			{
				using MemoryStream memoryStream = new MemoryStream();
				byte[] array = new byte[16384];
				int num;
				while ((num = stream.Read(array, 0, array.Length)) > 0)
				{
					if (memoryStream.Length + num > 8388608)
					{
						return null;
					}
					memoryStream.Write(array, 0, num);
				}
				array2 = memoryStream.ToArray();
			}
			if (array2.Length == 0 || array2.Length > 8388608)
			{
				return null;
			}
			string text;
			using (SHA256 sHA = SHA256.Create())
			{
				text = Convert.ToBase64String(sHA.ComputeHash(array2));
			}
			if (text == _artworkHash && _cachedArtwork != null)
			{
				return _cachedArtwork;
			}
			using MemoryStream streamSource = new MemoryStream(array2);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = 88;
			bitmapImage.DecodePixelHeight = 88;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			_artworkHash = text;
			return bitmapImage;
		}
		catch
		{
			return null;
		}
		finally
		{
			if (obj is IDisposable disposable)
			{
				try
				{
					disposable.Dispose();
				}
				catch
				{
				}
			}
		}
	}

	private static BitmapSource ExtractAppIconFallback(string appId)
	{
		try
		{
			string text = appId ?? string.Empty;
			if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
			{
				text = Path.GetFileNameWithoutExtension(text);
			}
			if (string.IsNullOrWhiteSpace(text) || text.IndexOf('!') >= 0)
			{
				return null;
			}
			Process[] processesByName = Process.GetProcessesByName(text);
			foreach (Process process in processesByName)
			{
				using (process)
				{
					try
					{
						string fileName = process.MainModule.FileName;
						using Icon icon = Icon.ExtractAssociatedIcon(fileName);
						if (icon == null)
						{
							continue;
						}
						BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(88, 88));
						bitmapSource.Freeze();
						return bitmapSource;
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public void Dispose()
	{
		NotchBehaviorConfig.Instance.ConfigChanged -= OnPolicyChanged;
		_stop = true;
		try
		{
			_signal.Set();
		}
		catch
		{
		}
		try
		{
			if (_worker != null && _worker.IsAlive)
			{
				_worker.Join(1500);
			}
		}
		catch
		{
		}
	}
}
