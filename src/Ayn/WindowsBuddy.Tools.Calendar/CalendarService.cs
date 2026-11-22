using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Calendar;

public sealed class CalendarService
{
	public sealed class UpcomingItem
	{
		public DateTime At;

		public bool AllDay;

		public CalOccurrence Occ;

		public UserReminder Reminder;

		public string Title
		{
			get
			{
				if (Occ == null)
				{
					return Reminder.Title;
				}
				return Occ.Title;
			}
		}
	}

	private sealed class PendingAlert
	{
		public string Key;

		public DateTime At;

		public string Title;

		public string Subtitle;
	}

	private const int MissedGraceMinutes = 10;

	private const int MissedReminderGraceMinutes = 720;

	private static CalendarService _instance;

	public static readonly TimeSpan HolidayRefresh = TimeSpan.FromDays(7.0);

	public static readonly TimeSpan SubscriptionRefresh = TimeSpan.FromMinutes(30.0);

	private readonly string _dir;

	private readonly string _path;

	private CalendarData _data;

	private readonly Dispatcher _ui;

	private int _version = 1;

	private readonly object _saveLock = new object();

	private long _saveSeq;

	private int _cacheVersion = -1;

	private DateTime _cacheFrom;

	private DateTime _cacheTo;

	private readonly Dictionary<DateTime, List<CalOccurrence>> _byDay = new Dictionary<DateTime, List<CalOccurrence>>();

	private static readonly List<CalOccurrence> Empty = new List<CalOccurrence>();

	private DispatcherTimer _alertTimer;

	private DispatcherTimer _feedTimer;

	private bool _started;

	public static Action<string, string, string> AlertSink;

	private readonly CallbackLifetime _callbacks = new CallbackLifetime();

	private readonly List<IcsOccurrence> _scratch = new List<IcsOccurrence>(512);

	public static CalendarService Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new CalendarService();
			}
			return _instance;
		}
	}

	public CalendarSettings Settings => _data.Settings;

	public int Version => _version;

	public string DataPath => _path;

	public IList<UserEvent> Events => _data.Events;

	public IList<UserReminder> Reminders => _data.Reminders;

	public string HolidayKey => "holidays_" + _data.Settings.Country;

	public string ExpansionError { get; private set; }

	public static bool NotchAlertsOn
	{
		get
		{
			try
			{
				if (!NotchConfig.Instance.ShowCalendarInNotch)
				{
					return false;
				}
				return NotchBehaviorConfig.Instance.GetEventPresentationMode(ActivityType.CalendarAlert) != EventPresentationMode.Off;
			}
			catch
			{
				return true;
			}
		}
	}

	public event EventHandler Changed;

	public event Action<string, string> AlertShown;

	public CalendarService()
	{
		_ui = ((Application.Current != null) ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);
		_dir = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "calendar");
		try
		{
			Directory.CreateDirectory(_dir);
		}
		catch
		{
		}
		_path = Path.Combine(_dir, "calendar.json");
		PersistenceHelper.RegisterExitFlush("calendar", Flush);
		Load();
		CalendarFeeds.Instance.Changed += delegate
		{
			Post(delegate
			{
				_version++;
				Raise();
			});
		};
	}

	public void Start()
	{
		if (!_started)
		{
			_started = true;
			PillActivityArbiter.ReminderStillDue = (string key) => ReminderForKey(key) != null;
			RefreshFeeds(force: false);
			_feedTimer = new DispatcherTimer(DispatcherPriority.Background, _ui)
			{
				Interval = SubscriptionRefresh
			};
			_feedTimer.Tick += delegate
			{
				RefreshFeeds(force: false);
			};
			_feedTimer.Start();
			_alertTimer = new DispatcherTimer(DispatcherPriority.Normal, _ui);
			_alertTimer.Tick += delegate
			{
				_alertTimer.Stop();
				CheckAlertsNow();
			};
			try
			{
				SystemEvents.TimeChanged += OnClockChanged;
				SystemEvents.PowerModeChanged += OnPowerChanged;
			}
			catch
			{
			}
			ScheduleAlerts();
		}
	}

	public Action PauseForShutdown()
	{
		_callbacks.Pause();
		CalendarFeeds.Fence.Close();
		bool feeds = _feedTimer != null && _feedTimer.IsEnabled;
		bool alerts = _alertTimer != null && _alertTimer.IsEnabled;
		if (_feedTimer != null)
		{
			_feedTimer.Stop();
		}
		if (_alertTimer != null)
		{
			_alertTimer.Stop();
		}
		return delegate
		{
			CalendarFeeds.Fence.Reopen();
			_callbacks.Resume();
			if (feeds)
			{
				_feedTimer.Start();
			}
			if (alerts)
			{
				_alertTimer.Start();
			}
			RefreshFeeds(force: false);
		};
	}

	public void Stop()
	{
		_callbacks.Stop();
		_started = false;
		if (_feedTimer != null)
		{
			_feedTimer.Stop();
		}
		if (_alertTimer != null)
		{
			_alertTimer.Stop();
		}
		try
		{
			SystemEvents.TimeChanged -= OnClockChanged;
			SystemEvents.PowerModeChanged -= OnPowerChanged;
		}
		catch
		{
		}
		Flush();
	}

	private void OnClockChanged(object s, EventArgs e)
	{
		Post(delegate
		{
			TimeZoneInfo.ClearCachedData();
			_version++;
			Raise();
			ScheduleAlerts();
		});
	}

	private void OnPowerChanged(object s, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.Resume)
		{
			Post(delegate
			{
				RefreshFeeds(force: false);
				ScheduleAlerts();
				Raise();
			});
		}
	}

	private void Post(Action a)
	{
		int token = _callbacks.Capture();
		Action action = delegate
		{
			if (_callbacks.Accepts(token))
			{
				a();
			}
		};
		if (_ui.CheckAccess())
		{
			action();
			return;
		}
		try
		{
			_ui.BeginInvoke(action);
		}
		catch
		{
		}
	}

	private void Raise()
	{
		EventHandler changed = this.Changed;
		if (changed != null)
		{
			try
			{
				changed(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	private void Touch()
	{
		_version++;
		Save();
		Raise();
		if (_started)
		{
			ScheduleAlerts();
		}
	}

	private void Load()
	{
		CalendarData calendarData = null;
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_path);
			if (!string.IsNullOrEmpty(text))
			{
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				javaScriptSerializer.MaxJsonLength = 67108864;
				calendarData = javaScriptSerializer.Deserialize<CalendarData>(text);
			}
		}
		catch
		{
		}
		bool flag = calendarData == null;
		if (calendarData == null)
		{
			calendarData = new CalendarData();
		}
		if (calendarData.Events == null)
		{
			calendarData.Events = new List<UserEvent>();
		}
		if (calendarData.Reminders == null)
		{
			calendarData.Reminders = new List<UserReminder>();
		}
		if (calendarData.Settings == null)
		{
			calendarData.Settings = new CalendarSettings();
		}
		if (calendarData.Settings.Subscriptions == null)
		{
			calendarData.Settings.Subscriptions = new List<CalendarSubscription>();
		}
		if (calendarData.Fired == null)
		{
			calendarData.Fired = new List<string>();
		}
		if (string.IsNullOrEmpty(calendarData.Settings.Country) || HolidayCountries.Find(calendarData.Settings.Country) == null)
		{
			calendarData.Settings.Country = HolidayCountries.DefaultCode();
		}
		if (calendarData.Settings.View != "month")
		{
			calendarData.Settings.View = "agenda";
		}
		calendarData.Events.RemoveAll((UserEvent e) => e == null || !Cal.ParseDate(e.Date).HasValue);
		foreach (UserEvent @event in calendarData.Events)
		{
			if (@event.Skips == null)
			{
				@event.Skips = new List<string>();
			}
			if (@event.Title == null)
			{
				@event.Title = "";
			}
		}
		calendarData.Reminders.RemoveAll((UserReminder r) => r == null || !Cal.ParseDate(r.Date).HasValue);
		_data = calendarData;
		if (flag && MigrateV2())
		{
			Save();
		}
	}

	private bool MigrateV2()
	{
		string text = Path.Combine(_dir, "events_v2.json");
		if (!File.Exists(text))
		{
			return false;
		}
		try
		{
			List<Dictionary<string, object>> list = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(PersistenceHelper.SafeReadAllText(text));
			if (list == null)
			{
				return false;
			}
			List<string> list2 = (from x in list
				group x by Convert.ToString(Get(x, "CreatedAtTicks"), Cal.Inv) into g
				where g.Count() >= 5
				select g.Key).ToList();
			int num = 0;
			foreach (Dictionary<string, object> item in list)
			{
				if (list2.Contains(Convert.ToString(Get(item, "CreatedAtTicks"), Cal.Inv)))
				{
					continue;
				}
				string text2 = Convert.ToString(Get(item, "Title"));
				string text3 = Convert.ToString(Get(item, "DateString"));
				if (!string.IsNullOrWhiteSpace(text2) && Cal.ParseDate(text3).HasValue)
				{
					string text4 = Convert.ToString(Get(item, "TimeString"));
					bool flag = Convert.ToBoolean(Get(item, "AllDay") ?? ((object)false)) || !Cal.ParseTime(text4).HasValue;
					Enum.TryParse<CalRepeat>(Convert.ToString(Get(item, "Repeat")), out var result);
					if (Convert.ToString(Get(item, "Type")) == "Reminder")
					{
						_data.Reminders.Add(new UserReminder
						{
							Title = text2,
							Date = text3,
							Time = (flag ? null : text4),
							Repeat = result,
							Completed = Convert.ToBoolean(Get(item, "Completed") ?? ((object)false))
						});
					}
					else
					{
						TimeSpan? timeSpan = Cal.ParseTime(text4);
						_data.Events.Add(new UserEvent
						{
							Title = text2,
							Date = text3,
							EndDate = text3,
							Start = (flag ? null : text4),
							End = ((flag || !timeSpan.HasValue) ? null : Cal.T(TimeSpan.FromTicks(Math.Min((timeSpan.Value + TimeSpan.FromHours(1.0)).Ticks, TimeSpan.FromMinutes(1439.0).Ticks)))),
							Repeat = result,
							Alert = CalAlert.AtTime
						});
					}
					num++;
				}
			}
			return num > 0;
		}
		catch
		{
			return false;
		}
	}

	private static object Get(Dictionary<string, object> d, string k)
	{
		if (!d.TryGetValue(k, out var value))
		{
			return null;
		}
		return value;
	}

	public void Save()
	{
		string json;
		try
		{
			json = new JavaScriptSerializer
			{
				MaxJsonLength = 67108864
			}.Serialize(_data);
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_path, error);
			return;
		}
		long seq = Interlocked.Increment(ref _saveSeq);
		ThreadPool.QueueUserWorkItem(delegate
		{
			lock (_saveLock)
			{
				if (seq != Interlocked.Read(ref _saveSeq))
				{
					return;
				}
				try
				{
					PersistenceHelper.SafeWriteAllText(_path, json);
				}
				catch
				{
				}
			}
		});
	}

	public void Flush()
	{
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			javaScriptSerializer.MaxJsonLength = 67108864;
			string content = javaScriptSerializer.Serialize(_data);
			Interlocked.Increment(ref _saveSeq);
			lock (_saveLock)
			{
				PersistenceHelper.SafeWriteAllText(_path, content);
			}
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_path, error);
		}
	}

	public UserEvent FindEvent(string id)
	{
		return _data.Events.FirstOrDefault((UserEvent e) => e.Id == id);
	}

	public UserEvent AddEvent(UserEvent e)
	{
		Normalize(e);
		_data.Events.Add(e);
		Touch();
		return e;
	}

	public void UpdateEvent(UserEvent updated)
	{
		int num = _data.Events.FindIndex((UserEvent e) => e.Id == updated.Id);
		if (num >= 0)
		{
			Normalize(updated);
			_data.Events[num] = updated;
			ForgetFired(updated.Id);
			Touch();
		}
	}

	public UserEvent DeleteEvent(string id)
	{
		UserEvent userEvent = FindEvent(id);
		if (userEvent == null)
		{
			return null;
		}
		_data.Events.Remove(userEvent);
		Touch();
		return userEvent;
	}

	public void RestoreEvent(UserEvent e)
	{
		if (e != null && FindEvent(e.Id) == null)
		{
			_data.Events.Add(e);
			Touch();
		}
	}

	public UserEvent DuplicateEvent(string id)
	{
		UserEvent userEvent = FindEvent(id);
		if (userEvent == null)
		{
			return null;
		}
		UserEvent userEvent2 = userEvent.Clone();
		userEvent2.Id = Guid.NewGuid().ToString("N");
		userEvent2.CreatedUtc = DateTime.UtcNow.Ticks;
		return AddEvent(userEvent2);
	}

	public UserEvent CopyToLocal(CalOccurrence o)
	{
		UserEvent userEvent = new UserEvent();
		userEvent.Title = o.Title;
		userEvent.Date = Cal.D(o.Start.Date);
		userEvent.EndDate = Cal.D((o.AllDay ? o.End.AddDays(-1.0) : o.End).Date);
		userEvent.Start = (o.AllDay ? null : Cal.T(o.Start.TimeOfDay));
		userEvent.End = (o.AllDay ? null : Cal.T(o.End.TimeOfDay));
		userEvent.Color = ((o.Color == CalColor.Grey) ? CalColor.Grey : o.Color);
		UserEvent e = userEvent;
		return AddEvent(e);
	}

	public void SkipOccurrence(string id, DateTime day)
	{
		UserEvent userEvent = FindEvent(id);
		if (userEvent != null)
		{
			string item = Cal.D(day);
			if (!userEvent.Skips.Contains(item))
			{
				userEvent.Skips.Add(item);
			}
			Touch();
		}
	}

	public void UnskipOccurrence(string id, DateTime day)
	{
		UserEvent userEvent = FindEvent(id);
		if (userEvent != null)
		{
			userEvent.Skips.Remove(Cal.D(day));
			Touch();
		}
	}

	private static void Normalize(UserEvent e)
	{
		if (e.Skips == null)
		{
			e.Skips = new List<string>();
		}
		e.Title = (e.Title ?? "").Trim();
		if (e.Title.Length == 0)
		{
			e.Title = "New event";
		}
		if (!Cal.ParseDate(e.Date).HasValue)
		{
			e.Date = Cal.D(DateTime.Today);
		}
		if (!Cal.ParseDate(e.EndDate).HasValue || Cal.ParseDate(e.EndDate) < Cal.ParseDate(e.Date))
		{
			e.EndDate = e.Date;
		}
		if (!Cal.ParseTime(e.Start).HasValue)
		{
			e.Start = null;
			e.End = null;
		}
		else if (!Cal.ParseTime(e.End).HasValue || (e.EndDate == e.Date && Cal.ParseTime(e.End) < Cal.ParseTime(e.Start)))
		{
			e.End = e.Start;
		}
	}

	public UserReminder FindReminder(string id)
	{
		return _data.Reminders.FirstOrDefault((UserReminder r) => r.Id == id);
	}

	public List<UserReminder> ActiveReminders(DateTime now)
	{
		return (from r in _data.Reminders
			where !r.Completed
			orderby (!r.IsOverdue(now)) ? 1 : 0, r.DueDate + (r.DueTime ?? TimeSpan.FromHours(23.99)), r.CreatedUtc
			select r).ToList();
	}

	public List<UserReminder> CompletedReminders()
	{
		return (from r in _data.Reminders
			where r.Completed
			orderby r.CompletedUtc descending
			select r).ToList();
	}

	public int OverdueCount(DateTime now)
	{
		return _data.Reminders.Count((UserReminder r) => r.IsOverdue(now));
	}

	public UserReminder AddReminder(UserReminder r)
	{
		r.Title = (r.Title ?? "").Trim();
		if (r.Title.Length == 0)
		{
			r.Title = "Reminder";
		}
		if (!Cal.ParseDate(r.Date).HasValue)
		{
			r.Date = Cal.D(DateTime.Today);
		}
		if (!Cal.ParseTime(r.Time).HasValue)
		{
			r.Time = null;
		}
		_data.Reminders.Add(r);
		Touch();
		return r;
	}

	public void UpdateReminder(UserReminder updated)
	{
		int num = _data.Reminders.FindIndex((UserReminder r) => r.Id == updated.Id);
		if (num >= 0)
		{
			if (!Cal.ParseTime(updated.Time).HasValue)
			{
				updated.Time = null;
			}
			_data.Reminders[num] = updated;
			ForgetFired(updated.Id);
			Touch();
		}
	}

	public UserReminder DeleteReminder(string id)
	{
		UserReminder userReminder = FindReminder(id);
		if (userReminder == null)
		{
			return null;
		}
		_data.Reminders.Remove(userReminder);
		Touch();
		return userReminder;
	}

	public void RestoreReminder(UserReminder r)
	{
		if (r != null && FindReminder(r.Id) == null)
		{
			_data.Reminders.Add(r);
			Touch();
		}
	}

	public bool CompleteReminder(string id)
	{
		UserReminder userReminder = FindReminder(id);
		if (userReminder == null)
		{
			return false;
		}
		if (userReminder.Repeat != 0)
		{
			DateTime after = ((userReminder.DueDate > DateTime.Today) ? userReminder.DueDate : DateTime.Today);
			userReminder.Date = Cal.D(Cal.NextDate(userReminder.DueDate, userReminder.Repeat, after));
			ForgetFired(userReminder.Id);
			Touch();
			return true;
		}
		userReminder.Completed = true;
		userReminder.CompletedUtc = DateTime.UtcNow.Ticks;
		Touch();
		return false;
	}

	public void ReopenReminder(string id)
	{
		UserReminder userReminder = FindReminder(id);
		if (userReminder != null)
		{
			userReminder.Completed = false;
			userReminder.CompletedUtc = 0L;
			Touch();
		}
	}

	public List<UserReminder> ClearCompleted()
	{
		List<UserReminder> list = _data.Reminders.Where((UserReminder r) => r.Completed).ToList();
		if (list.Count == 0)
		{
			return list;
		}
		_data.Reminders.RemoveAll((UserReminder r) => r.Completed);
		Touch();
		return list;
	}

	public void RestoreReminders(IEnumerable<UserReminder> list)
	{
		foreach (UserReminder item in list)
		{
			if (FindReminder(item.Id) == null)
			{
				_data.Reminders.Add(item);
			}
		}
		Touch();
	}

	public void SetView(string view)
	{
		if (!(_data.Settings.View == view))
		{
			_data.Settings.View = view;
			Save();
		}
	}

	public void SetCountry(string code)
	{
		if (HolidayCountries.Find(code) != null && !(_data.Settings.Country == code))
		{
			_data.Settings.Country = code.ToUpperInvariant();
			Touch();
			RefreshFeeds(force: false);
		}
	}

	public void SetHolidays(bool holidays, bool observances)
	{
		_data.Settings.Holidays = holidays;
		_data.Settings.Observances = observances;
		Touch();
		RefreshFeeds(force: false);
	}

	public void RefreshFeeds(bool force)
	{
		CalendarSettings settings = _data.Settings;
		if (settings.Holidays || settings.Observances)
		{
			CalendarFeeds.Instance.Ensure(HolidayKey, HolidayCountries.FeedUrl(settings.Country), HolidayRefresh, force, null, null, null);
		}
		foreach (CalendarSubscription item in settings.Subscriptions.ToList())
		{
			if (item.Enabled)
			{
				SyncSubscription(item, force, null);
			}
		}
	}

	private void SyncSubscription(CalendarSubscription sub, bool force, Action<bool, string> done)
	{
		string url = sub.Url;
		if (url == null)
		{
			if (done != null)
			{
				done(arg1: false, "Link unreadable — add it again");
			}
			return;
		}
		string id = sub.Id;
		int lifetime = _callbacks.Capture();
		CalendarFeeds.Instance.Ensure(sub.Key, url, SubscriptionRefresh, force, sub.ETag, sub.LastModified, delegate(FetchResult res, string name)
		{
			Post(delegate
			{
				if (_callbacks.Accepts(lifetime))
				{
					CalendarSubscription calendarSubscription = _data.Settings.Subscriptions.FirstOrDefault((CalendarSubscription x) => x.Id == id);
					if (calendarSubscription != null)
					{
						if (res != null)
						{
							if (res.Ok)
							{
								calendarSubscription.LastSyncUtc = DateTime.UtcNow.Ticks;
								calendarSubscription.LastError = null;
								if (!res.NotModified)
								{
									calendarSubscription.ETag = res.ETag;
									calendarSubscription.LastModified = res.LastModified;
								}
							}
							else
							{
								calendarSubscription.LastError = res.Error;
							}
						}
						if (!string.IsNullOrEmpty(name) && (string.IsNullOrEmpty(calendarSubscription.Name) || calendarSubscription.Name == HostOf(url)))
						{
							calendarSubscription.Name = name;
						}
						Save();
						_version++;
						Raise();
						if (done != null)
						{
							done(res == null || res.Ok, (res != null) ? res.Error : null);
						}
					}
				}
			});
		});
	}

	public CalendarSubscription AddSubscription(string link, Action<bool, string> done)
	{
		string url = CalendarFeeds.NormalizeUrl(link);
		if (url == null)
		{
			if (done != null)
			{
				done(arg1: false, string.IsNullOrWhiteSpace(link) ? "Paste a calendar link first" : "That isn't a valid link — it should start with https://, http:// or webcal://");
			}
			return null;
		}
		if (_data.Settings.Subscriptions.Any((CalendarSubscription s) => string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase)))
		{
			if (done != null)
			{
				done(arg1: false, "That calendar is already added");
			}
			return null;
		}
		List<CalColor> used = _data.Settings.Subscriptions.Select((CalendarSubscription s) => s.Color).ToList();
		CalColor[] source = new CalColor[4]
		{
			CalColor.Blue,
			CalColor.Green,
			CalColor.Amber,
			CalColor.Grey
		};
		CalendarSubscription sub = new CalendarSubscription
		{
			Name = HostOf(url),
			Url = url,
			Color = source.FirstOrDefault((CalColor c) => !used.Contains(c))
		};
		if (sub.Url == null)
		{
			if (done != null)
			{
				done(arg1: false, "Couldn't store that link securely");
			}
			return null;
		}
		_data.Settings.Subscriptions.Add(sub);
		Save();
		Raise();
		SyncSubscription(sub, force: true, delegate(bool ok, string err)
		{
			if (!ok)
			{
				_data.Settings.Subscriptions.Remove(sub);
				CalendarFeeds.Instance.Forget(sub.Key);
				Save();
				_version++;
				Raise();
				if (err != null && err.StartsWith("Offline"))
				{
					err = "You're offline — couldn't add the calendar";
				}
				else if (err != null && (err.StartsWith("Couldn't download") || err.StartsWith("Timed out")))
				{
					err = "Couldn't reach that link — check the connection and try again";
				}
				else if (err == null)
				{
					err = "Couldn't add that calendar";
				}
			}
			if (done != null)
			{
				done(ok, err);
			}
		});
		return sub;
	}

	public void RemoveSubscription(string id)
	{
		CalendarSubscription calendarSubscription = _data.Settings.Subscriptions.FirstOrDefault((CalendarSubscription x) => x.Id == id);
		if (calendarSubscription != null)
		{
			_data.Settings.Subscriptions.Remove(calendarSubscription);
			CalendarFeeds.Instance.Forget(calendarSubscription.Key);
			Touch();
		}
	}

	public void RestoreSubscription(CalendarSubscription s)
	{
		if (s != null && !_data.Settings.Subscriptions.Any((CalendarSubscription x) => x.Id == s.Id))
		{
			_data.Settings.Subscriptions.Add(s);
			Touch();
			if (s.Enabled)
			{
				SyncSubscription(s, force: true, null);
			}
		}
	}

	public void RefreshSubscription(string id, Action<bool, string> done)
	{
		CalendarSubscription calendarSubscription = _data.Settings.Subscriptions.FirstOrDefault((CalendarSubscription x) => x.Id == id);
		if (calendarSubscription == null)
		{
			done?.Invoke(arg1: false, "Calendar removed");
			return;
		}
		SyncSubscription(calendarSubscription, force: true, done);
		Raise();
	}

	public void SetSubscription(string id, bool? enabled, CalColor? color)
	{
		CalendarSubscription calendarSubscription = _data.Settings.Subscriptions.FirstOrDefault((CalendarSubscription x) => x.Id == id);
		if (calendarSubscription != null)
		{
			if (enabled.HasValue)
			{
				calendarSubscription.Enabled = enabled.Value;
			}
			if (color.HasValue)
			{
				calendarSubscription.Color = color.Value;
			}
			Touch();
			if (calendarSubscription.Enabled)
			{
				SyncSubscription(calendarSubscription, force: false, null);
			}
		}
	}

	public static string HostOf(string url)
	{
		if (Uri.TryCreate(url ?? "", UriKind.Absolute, out var result))
		{
			string text = (result.Host.StartsWith("www.") ? result.Host.Substring(4) : result.Host);
			if (text.Contains("google.com"))
			{
				return "Google Calendar";
			}
			if (text.Contains("outlook") || text.Contains("office365") || text.Contains("live.com"))
			{
				return "Outlook calendar";
			}
			if (text.Contains("icloud.com"))
			{
				return "iCloud calendar";
			}
			return text;
		}
		return "Calendar";
	}

	public List<CalOccurrence> Day(DateTime day)
	{
		EnsureRange(day.Date, day.Date.AddDays(1.0));
		if (!_byDay.TryGetValue(day.Date, out var value))
		{
			return Empty;
		}
		return value;
	}

	public List<CalOccurrence> Range(DateTime from, DateTime to)
	{
		EnsureRange(from, to);
		HashSet<string> hashSet = new HashSet<string>();
		List<CalOccurrence> list = new List<CalOccurrence>();
		DateTime dateTime = from.Date;
		while (dateTime < to)
		{
			if (_byDay.TryGetValue(dateTime, out var value))
			{
				foreach (CalOccurrence item in value)
				{
					if (item.Start < to && (item.End > from || item.Start >= from) && hashSet.Add(item.SourceId + item.Key))
					{
						list.Add(item);
					}
				}
			}
			dateTime = dateTime.AddDays(1.0);
		}
		list.Sort((CalOccurrence a, CalOccurrence b) => (!(a.Start != b.Start)) ? string.Compare(a.Title, b.Title, StringComparison.CurrentCulture) : a.Start.CompareTo(b.Start));
		return list;
	}

	private void EnsureRange(DateTime from, DateTime to)
	{
		if (_cacheVersion == _version && from >= _cacheFrom && to <= _cacheTo)
		{
			return;
		}
		DateTime dateTime = from.Date.AddDays(-45.0);
		DateTime dateTime2 = to.Date.AddDays(60.0);
		_byDay.Clear();
		List<CalOccurrence> list = new List<CalOccurrence>(256);
		ExpandAll(dateTime, dateTime2, list);
		foreach (CalOccurrence item in list)
		{
			DateTime dateTime3 = ((item.Start.Date < dateTime) ? dateTime : item.Start.Date);
			DateTime dateTime4 = ((!(item.End > item.Start)) ? item.Start.Date : ((item.End.TimeOfDay == TimeSpan.Zero) ? item.End.Date.AddDays(-1.0) : item.End.Date));
			if (dateTime4 >= dateTime2)
			{
				dateTime4 = dateTime2.AddDays(-1.0);
			}
			if ((dateTime4 - dateTime3).TotalDays > 366.0)
			{
				dateTime4 = dateTime3.AddDays(366.0);
			}
			DateTime dateTime5 = dateTime3;
			while (dateTime5 <= dateTime4)
			{
				if (!_byDay.TryGetValue(dateTime5, out var value))
				{
					value = (_byDay[dateTime5] = new List<CalOccurrence>());
				}
				value.Add(item);
				dateTime5 = dateTime5.AddDays(1.0);
			}
		}
		foreach (List<CalOccurrence> value2 in _byDay.Values)
		{
			value2.Sort(DayOrder);
		}
		_cacheFrom = dateTime;
		_cacheTo = dateTime2;
		_cacheVersion = _version;
	}

	private static int Rank(CalOccurrence o)
	{
		if (!o.AllDay)
		{
			return 10;
		}
		return o.Kind switch
		{
			OccKind.Holiday => 0, 
			OccKind.Observance => 1, 
			OccKind.Event => 2, 
			_ => 3, 
		};
	}

	private static int DayOrder(CalOccurrence a, CalOccurrence b)
	{
		int num = Rank(a);
		int num2 = Rank(b);
		if (num != num2)
		{
			return num.CompareTo(num2);
		}
		if (a.Start != b.Start)
		{
			return a.Start.CompareTo(b.Start);
		}
		if (a.End != b.End)
		{
			return a.End.CompareTo(b.End);
		}
		return string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
	}

	private void ExpandBounded(IList<IcsEvent> events, DateTime from, DateTime to, Ics.ExpansionBudget budget)
	{
		if (!Ics.Expand(events, from, to, _scratch, budget))
		{
			ExpansionError = "Calendar too complex to display. Reduce the recurrence range or disable the large calendar.";
		}
	}

	private void ExpandAll(DateTime from, DateTime to, List<CalOccurrence> into)
	{
		ExpansionError = null;
		Ics.ExpansionBudget budget = new Ics.ExpansionBudget();
		Dictionary<IcsEvent, UserEvent> dictionary = new Dictionary<IcsEvent, UserEvent>();
		List<IcsEvent> list = new List<IcsEvent>(_data.Events.Count);
		foreach (UserEvent @event in _data.Events)
		{
			IcsEvent icsEvent = ToIcs(@event);
			if (icsEvent != null)
			{
				dictionary[icsEvent] = @event;
				list.Add(icsEvent);
			}
		}
		_scratch.Clear();
		ExpandBounded(list, from, to, budget);
		foreach (IcsOccurrence item in _scratch)
		{
			UserEvent userEvent = dictionary[item.Event];
			into.Add(new CalOccurrence
			{
				Kind = OccKind.Event,
				SourceId = "user",
				SourceName = "My calendar",
				Id = userEvent.Id,
				Title = userEvent.Title,
				Start = item.Start,
				End = item.End,
				AllDay = item.AllDay,
				Color = userEvent.Color,
				Event = userEvent
			});
		}
		CalendarSettings settings = _data.Settings;
		if (settings.Holidays || settings.Observances)
		{
			FeedData feedData = CalendarFeeds.Instance.Get(HolidayKey);
			if (feedData != null)
			{
				_scratch.Clear();
				ExpandBounded(feedData.Events, from, to, budget);
				HashSet<string> hashSet = new HashSet<string>();
				foreach (IcsOccurrence item2 in _scratch)
				{
					bool flag = IsPublicHoliday(item2.Event);
					if (!(flag ? (!settings.Holidays) : (!settings.Observances)))
					{
						string text = CleanHolidayTitle(item2.Event.Summary);
						DateTime start = item2.Start;
						if (hashSet.Add(text + start.Ticks))
						{
							into.Add(new CalOccurrence
							{
								Kind = (flag ? OccKind.Holiday : OccKind.Observance),
								SourceId = HolidayKey,
								SourceName = (flag ? "Public holiday" : "Festival / observance"),
								Id = item2.Event.Uid,
								Title = text,
								Start = item2.Start,
								End = item2.End,
								AllDay = item2.AllDay,
								Color = CalColor.Grey
							});
						}
					}
				}
			}
		}
		foreach (CalendarSubscription subscription in settings.Subscriptions)
		{
			if (!subscription.Enabled)
			{
				continue;
			}
			FeedData feedData2 = CalendarFeeds.Instance.Get(subscription.Key);
			if (feedData2 == null)
			{
				continue;
			}
			_scratch.Clear();
			ExpandBounded(feedData2.Events, from, to, budget);
			foreach (IcsOccurrence item3 in _scratch)
			{
				into.Add(new CalOccurrence
				{
					Kind = OccKind.Subscribed,
					SourceId = subscription.Key,
					SourceName = subscription.Name,
					Id = (item3.Event.Uid ?? item3.Event.Summary),
					Title = (string.IsNullOrWhiteSpace(item3.Event.Summary) ? "(No title)" : item3.Event.Summary.Trim()),
					Location = item3.Event.Location,
					Start = item3.Start,
					End = item3.End,
					AllDay = item3.AllDay,
					Color = subscription.Color
				});
			}
		}
	}

	public static bool IsPublicHoliday(IcsEvent e)
	{
		string text = e.Description ?? "";
		if (text.StartsWith("Observance", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return true;
	}

	private static string CleanHolidayTitle(string s)
	{
		s = (s ?? "").Trim();
		if (s.Length != 0)
		{
			return s;
		}
		return "Holiday";
	}

	private static IcsEvent ToIcs(UserEvent e)
	{
		DateTime? dateTime = Cal.ParseDate(e.Date);
		if (!dateTime.HasValue)
		{
			return null;
		}
		IcsEvent icsEvent = new IcsEvent();
		icsEvent.Uid = e.Id;
		icsEvent.Summary = e.Title;
		IcsEvent icsEvent2 = icsEvent;
		if (e.AllDay)
		{
			icsEvent2.Start = new IcsTime
			{
				Value = dateTime.Value,
				IsDate = true
			};
			icsEvent2.End = new IcsTime
			{
				Value = e.LastDate.AddDays(1.0),
				IsDate = true
			};
		}
		else
		{
			icsEvent2.Start = new IcsTime
			{
				Value = e.StartAt
			};
			icsEvent2.End = new IcsTime
			{
				Value = e.EndAt
			};
		}
		if (e.Repeat != 0)
		{
			icsEvent2.Rule = new IcsRule
			{
				Freq = ((e.Repeat == CalRepeat.Daily) ? IcsFreq.Daily : ((e.Repeat == CalRepeat.Weekly || e.Repeat == CalRepeat.Weekdays) ? IcsFreq.Weekly : ((e.Repeat == CalRepeat.Monthly) ? IcsFreq.Monthly : IcsFreq.Yearly)))
			};
			if (e.Repeat == CalRepeat.Weekdays)
			{
				DayOfWeek[] array = new DayOfWeek[5]
				{
					DayOfWeek.Monday,
					DayOfWeek.Tuesday,
					DayOfWeek.Wednesday,
					DayOfWeek.Thursday,
					DayOfWeek.Friday
				};
				foreach (DayOfWeek value in array)
				{
					icsEvent2.Rule.ByDay.Add(new KeyValuePair<int, DayOfWeek>(0, value));
				}
			}
			if (e.Repeat == CalRepeat.Monthly && dateTime.Value.Day > 28)
			{
				icsEvent2.Rule.ByMonthDay.Add((dateTime.Value.Day == 31) ? (-1) : dateTime.Value.Day);
			}
			foreach (string skip in e.Skips)
			{
				DateTime? dateTime2 = Cal.ParseDate(skip);
				if (dateTime2.HasValue)
				{
					icsEvent2.ExDates.Add(new IcsTime
					{
						Value = dateTime2.Value,
						IsDate = true
					});
				}
			}
		}
		return icsEvent2;
	}

	public CalOccurrence UpNext(DateTime now, DateTime from)
	{
		List<CalOccurrence> list = Range((from < now) ? now : from, ((from < now) ? now : from).Date.AddDays(14.0));
		CalOccurrence result = null;
		foreach (CalOccurrence item in list)
		{
			if (!item.IsHoliday && !item.AllDay && (item.End > now || (item.End <= item.Start && item.Start >= now)) && (!(item.Start < from) || !(from > now)))
			{
				result = item;
				break;
			}
		}
		return result;
	}

	public List<UserReminder> RemindersOn(DateTime day)
	{
		return (from r in _data.Reminders
			where !r.Completed && r.DueDate == day.Date
			orderby r.DueTime ?? TimeSpan.FromHours(23.99), r.CreatedUtc
			select r).ToList();
	}

	public List<UpcomingItem> UpcomingAfter(DateTime day, int count)
	{
		DateTime dateTime = day.Date.AddDays(1.0);
		DateTime dateTime2 = dateTime.AddDays(60.0);
		List<UpcomingItem> list = new List<UpcomingItem>();
		foreach (CalOccurrence item in Range(dateTime, dateTime2))
		{
			if (item.Kind != OccKind.Observance && !(item.Start < dateTime))
			{
				list.Add(new UpcomingItem
				{
					At = item.Start,
					AllDay = item.AllDay,
					Occ = item
				});
				if (list.Count >= count * 3)
				{
					break;
				}
			}
		}
		foreach (UserReminder reminder in _data.Reminders)
		{
			if (!reminder.Completed && !(reminder.DueDate < dateTime) && !(reminder.DueDate >= dateTime2))
			{
				list.Add(new UpcomingItem
				{
					At = reminder.DueDate + (reminder.DueTime ?? TimeSpan.Zero),
					AllDay = !reminder.DueTime.HasValue,
					Reminder = reminder
				});
			}
		}
		return (from i in list
			orderby i.At, (!i.AllDay) ? 1 : 0
			select i).Take(count).ToList();
	}

	public int TodayCount(DateTime now)
	{
		return Day(now.Date).Count((CalOccurrence o) => !o.IsHoliday && !o.IsPast(now));
	}

	private static int GraceFor(string key)
	{
		if (key == null || !key.StartsWith("r|", StringComparison.Ordinal))
		{
			return 10;
		}
		return 720;
	}

	private List<PendingAlert> CollectAlerts(DateTime now, DateTime until)
	{
		List<PendingAlert> list = new List<PendingAlert>();
		foreach (CalOccurrence item in Range(now.Date.AddDays(-1.0), until.Date.AddDays(2.0)))
		{
			if (item.Event != null && item.Event.Alert != 0)
			{
				DateTime dateTime = (item.AllDay ? item.Start.Date.AddHours(9.0) : item.Start);
				DateTime dateTime2 = dateTime - Cal.AlertLead(item.Event.Alert);
				if (item.AllDay && item.Event.Alert == CalAlert.Day1)
				{
					dateTime2 = item.Start.Date.AddDays(-1.0).AddHours(9.0);
				}
				string subtitle;
				if (item.AllDay)
				{
					subtitle = ((item.Start.Date == dateTime2.Date) ? "Today" : "Tomorrow") + " · All day";
				}
				else
				{
					TimeSpan timeSpan = item.Start - dateTime2;
					subtitle = ((timeSpan.TotalMinutes < 1.0) ? ("Starting now · " + Cal.Time(item.Start)) : ((timeSpan.TotalHours >= 23.0) ? ("Tomorrow · " + Cal.Time(item.Start)) : ("In " + (int)Math.Round(timeSpan.TotalMinutes) + " min · " + Cal.Time(item.Start))));
				}
				list.Add(new PendingAlert
				{
					Key = "e|" + item.Id + "|" + item.Start.Ticks + "|" + (int)item.Event.Alert,
					At = dateTime2,
					Title = item.Title,
					Subtitle = subtitle
				});
			}
		}
		foreach (UserReminder reminder in _data.Reminders)
		{
			if (!reminder.Completed)
			{
				DateTime dateTime3 = reminder.DueDate + (reminder.DueTime ?? TimeSpan.FromHours(9.0));
				list.Add(new PendingAlert
				{
					Key = "r|" + reminder.Id + "|" + dateTime3.Ticks,
					At = dateTime3,
					Title = reminder.Title,
					Subtitle = "Reminder · " + (reminder.DueTime.HasValue ? Cal.Time(dateTime3) : "Today")
				});
			}
		}
		return list;
	}

	public int CheckAlertsNow()
	{
		DateTime now = DateTime.Now;
		int num = 0;
		HashSet<string> hashSet = new HashSet<string>(_data.Fired);
		bool flag = false;
		foreach (PendingAlert item in from x in CollectAlerts(now, now.AddDays(1.0))
			orderby x.At
			select x)
		{
			if (!(item.At > now) && !hashSet.Contains(item.Key))
			{
				hashSet.Add(item.Key);
				_data.Fired.Add(item.Key);
				flag = true;
				if (!((now - item.At).TotalMinutes > (double)GraceFor(item.Key)))
				{
					Show(item);
					num++;
				}
			}
		}
		if (_data.Fired.Count > 400)
		{
			_data.Fired.RemoveRange(0, _data.Fired.Count - 300);
			flag = true;
		}
		if (flag)
		{
			Save();
		}
		ScheduleAlerts();
		return num;
	}

	public DateTime? NextAlertAt()
	{
		DateTime now = DateTime.Now;
		HashSet<string> hashSet = new HashSet<string>(_data.Fired);
		DateTime? dateTime = null;
		foreach (PendingAlert item in CollectAlerts(now, now.AddDays(1.0)))
		{
			if (hashSet.Contains(item.Key))
			{
				continue;
			}
			if (item.At <= now && (now - item.At).TotalMinutes <= (double)GraceFor(item.Key))
			{
				return now;
			}
			if (!(item.At > now))
			{
				continue;
			}
			if (dateTime.HasValue)
			{
				DateTime at = item.At;
				DateTime? dateTime2 = dateTime;
				if (!(at < dateTime2))
				{
					continue;
				}
			}
			dateTime = item.At;
		}
		return dateTime;
	}

	private void ScheduleAlerts()
	{
		if (_alertTimer != null)
		{
			_alertTimer.Stop();
			DateTime? dateTime = NextAlertAt();
			TimeSpan timeSpan = (dateTime.HasValue ? (dateTime.Value - DateTime.Now) : TimeSpan.FromHours(1.0));
			if (timeSpan < TimeSpan.FromMilliseconds(200.0))
			{
				timeSpan = TimeSpan.FromMilliseconds(200.0);
			}
			if (timeSpan > TimeSpan.FromHours(1.0))
			{
				timeSpan = TimeSpan.FromHours(1.0);
			}
			_alertTimer.Interval = timeSpan;
			_alertTimer.Start();
		}
	}

	private void ForgetFired(string id)
	{
		int num = _data.Fired.RemoveAll((string k) => k.Contains("|" + id + "|"));
		if (num > 0)
		{
			Save();
		}
	}

	internal UserReminder ReminderForKey(string key)
	{
		string[] array = (key ?? "").Split('|');
		if (array.Length != 3 || array[0] != "r")
		{
			return null;
		}
		UserReminder userReminder = FindReminder(array[1]);
		if (userReminder == null || userReminder.Completed)
		{
			return null;
		}
		if (!long.TryParse(array[2], out var result) || (userReminder.DueDate + (userReminder.DueTime ?? TimeSpan.FromHours(9.0))).Ticks != result)
		{
			return null;
		}
		return userReminder;
	}

	internal void HandleReminderAction(string key, bool snooze)
	{
		if (snooze)
		{
			SnoozeReminder(key, DateTime.Now.AddMinutes(10.0));
		}
		else
		{
			CompleteReminderKey(key);
		}
	}

	internal bool CompleteReminderKey(string key)
	{
		UserReminder userReminder = ReminderForKey(key);
		if (userReminder == null)
		{
			return false;
		}
		CompleteReminder(userReminder.Id);
		return true;
	}

	internal bool SnoozeReminder(string key, DateTime until)
	{
		UserReminder userReminder = ReminderForKey(key);
		if (userReminder == null)
		{
			return false;
		}
		UserReminder userReminder2 = userReminder.Clone();
		userReminder2.Date = Cal.D(until.Date);
		userReminder2.Time = until.ToString("HH:mm:ss", Cal.Inv);
		UpdateReminder(userReminder2);
		return true;
	}

	internal static List<KeyValuePair<string, DateTime>> SnoozeChoices(DateTime now)
	{
		List<KeyValuePair<string, DateTime>> list = new List<KeyValuePair<string, DateTime>>();
		list.Add(new KeyValuePair<string, DateTime>("5 min", now.AddMinutes(5.0)));
		list.Add(new KeyValuePair<string, DateTime>("10 min", now.AddMinutes(10.0)));
		list.Add(new KeyValuePair<string, DateTime>("30 min", now.AddMinutes(30.0)));
		list.Add(new KeyValuePair<string, DateTime>("1 hour", now.AddHours(1.0)));
		list.Add(new KeyValuePair<string, DateTime>("Tomorrow 9 AM", now.Date.AddDays(1.0).AddHours(9.0)));
		return list;
	}

	private void Show(PendingAlert a)
	{
		try
		{
			if (AlertSink != null)
			{
				AlertSink(a.Title, a.Subtitle, a.Key);
			}
			else if (NotchConfig.Instance.ShowCalendarInNotch)
			{
				if (a.Key.StartsWith("r|", StringComparison.Ordinal))
				{
					PillActivityArbiter.Instance.PublishCalendarAlert(a.Title, a.Subtitle, a.Key);
				}
				else
				{
					ReminderDelivery.Deliver(ActivityType.CalendarAlert, delegate
					{
						PillActivityArbiter.Instance.PublishCalendarAlert(a.Title, a.Subtitle, a.Key);
					}, a.Title, a.Subtitle);
				}
			}
		}
		catch
		{
		}
		Action<string, string> alertShown = this.AlertShown;
		if (alertShown != null)
		{
			try
			{
				alertShown(a.Title, a.Subtitle);
			}
			catch
			{
			}
		}
	}
}
