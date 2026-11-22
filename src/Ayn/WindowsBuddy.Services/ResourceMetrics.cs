using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class ResourceMetrics
{
	public sealed class Sample
	{
		public long WorkingSetKb;

		public long PrivateKb;

		public long ManagedKb;

		public int Gdi;

		public int User;

		public int Handles;

		public int Threads;

		public int Windows;

		public int DispatcherTimers;

		public int AnimationClocks;

		public int RenderingHandlers;

		public double DispatcherDelayMs;

		public double PeakDispatcherDelayMs;

		public SortedDictionary<string, int> Subscriptions = new SortedDictionary<string, int>(StringComparer.Ordinal);

		public int TotalSubscriptions
		{
			get
			{
				int num = 0;
				foreach (int value in Subscriptions.Values)
				{
					num += value;
				}
				return num;
			}
		}

		public string ToLine()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "ws={0}KB private={1}KB managed={2}KB gdi={3} user={4} handles={5} threads={6} windows={7} timers={8} subs={9} clocks={10} rendering={11}", WorkingSetKb, PrivateKb, ManagedKb, Gdi, User, Handles, Threads, Windows, DispatcherTimers, TotalSubscriptions, AnimationClocks, RenderingHandlers);
			foreach (KeyValuePair<string, int> subscription in Subscriptions)
			{
				stringBuilder.Append(' ').Append(subscription.Key).Append('=')
					.Append(subscription.Value);
			}
			stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " dispatcherMs={0:F1} peakDispatcherMs={1:F1}", DispatcherDelayMs, PeakDispatcherDelayMs);
			return stringBuilder.ToString();
		}
	}

	private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	public static readonly TimeSpan Interval = TimeSpan.FromMinutes(2.0);

	private static Timer _timer;

	private static Dispatcher _ui;

	private static volatile Sample _lastComplete;

	private static int _sampling;

	private static long _dispatcherDelayTicks;

	private static long _peakDelayTicks;

	public static string LastCompleteLine
	{
		get
		{
			Sample lastComplete = _lastComplete;
			if (lastComplete == null)
			{
				return "no completed sample yet";
			}
			return lastComplete.ToLine();
		}
	}

	public static void RecordDispatcherDelay(long ticks)
	{
		Interlocked.Exchange(ref _dispatcherDelayTicks, ticks);
		long num;
		do
		{
			num = Interlocked.Read(ref _peakDelayTicks);
		}
		while (ticks > num && Interlocked.CompareExchange(ref _peakDelayTicks, ticks, num) != num);
	}

	public static void Start()
	{
		if (_timer == null)
		{
			_ui = Dispatcher.CurrentDispatcher;
			_timer = new Timer(delegate
			{
				Log();
			}, null, TimeSpan.FromMinutes(1.0), Interval);
		}
	}

	private static void Log()
	{
		if (Interlocked.Exchange(ref _sampling, 1) != 0)
		{
			return;
		}
		try
		{
			Sample sample = Take(fullGc: false);
			LogWriter.Enqueue("metrics.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + sample.ToLine());
		}
		catch (Exception ex)
		{
			try
			{
				TransactionLogger.Warn("Metrics sample failed: " + ex.Message);
			}
			catch
			{
			}
		}
		finally
		{
			Interlocked.Exchange(ref _sampling, 0);
		}
	}

	public static Sample Take(bool fullGc, bool includeUi = true)
	{
		if (fullGc)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
		Sample sample = new Sample();
		using (Process process = Process.GetCurrentProcess())
		{
			process.Refresh();
			sample.WorkingSetKb = process.WorkingSet64 / 1024;
			sample.PrivateKb = process.PrivateMemorySize64 / 1024;
			sample.Handles = process.HandleCount;
			sample.Threads = process.Threads.Count;
			sample.Gdi = GetGuiResources(process.Handle, 0);
			sample.User = GetGuiResources(process.Handle, 1);
		}
		sample.ManagedKb = GC.GetTotalMemory(forceFullCollection: false) / 1024;
		sample.Windows = CountTopLevelWindows();
		sample.DispatcherDelayMs = (double)Interlocked.Read(ref _dispatcherDelayTicks) * 1000.0 / (double)Stopwatch.Frequency;
		sample.PeakDispatcherDelayMs = (double)Interlocked.Read(ref _peakDelayTicks) * 1000.0 / (double)Stopwatch.Frequency;
		if (!includeUi)
		{
			sample.DispatcherTimers = (sample.AnimationClocks = (sample.RenderingHandlers = -1));
			return sample;
		}
		Dispatcher ui = _ui ?? ((Application.Current != null) ? Application.Current.Dispatcher : null);
		if (ui != null)
		{
			if (ui.CheckAccess())
			{
				sample.DispatcherTimers = CountDispatcherTimers(ui);
				sample.AnimationClocks = CountAnimationClocks();
				sample.RenderingHandlers = CountRenderingHandlers();
			}
			else
			{
				try
				{
					int[] array = ui.Invoke(() => new int[3]
					{
						CountDispatcherTimers(ui),
						CountAnimationClocks(),
						CountRenderingHandlers()
					}, DispatcherPriority.Background, CancellationToken.None, TimeSpan.FromSeconds(2.0));
					sample.DispatcherTimers = array[0];
					sample.AnimationClocks = array[1];
					sample.RenderingHandlers = array[2];
				}
				catch
				{
					sample.DispatcherTimers = -1;
				}
			}
		}
		CountSystemEvents(sample.Subscriptions);
		CountNetworkChange(sample.Subscriptions);
		AddStaticEvent(sample.Subscriptions, "ThemeChanged", typeof(ThemeTokens), "ThemeChanged");
		AddStaticEvent(sample.Subscriptions, "SystemMonitor.PerfChanged", typeof(SystemMonitorService), "PerfChanged");
		sample.Subscriptions["ThemeListeners"] = ThemeTokens.ListenerCount;
		AddInstanceEvent(sample.Subscriptions, "NotchConfigChanged", NotchConfig.Instance, "ConfigChanged");
		CountExistingServiceEvents(sample.Subscriptions);
		_lastComplete = sample;
		return sample;
	}

	private static void CountExistingServiceEvents(IDictionary<string, int> into)
	{
		string[] array = new string[13]
		{
			"WindowsBuddy.Tools.Message.MessageService", "WindowsBuddy.Tools.ToolRegistry", "WindowsBuddy.Tools.Timer.TimerService", "WindowsBuddy.Tools.Clock.ClockService", "WindowsBuddy.Tools.ScreenTime.ScreenTimeService", "WindowsBuddy.BuddyCapsule.Activity.PillActivityArbiter", "WindowsBuddy.Tools.Weather.WeatherService", "WindowsBuddy.Tools.Clipboard.ClipboardService", "WindowsBuddy.Tools.Notes.NotesService", "WindowsBuddy.Tools.FileShelf.FileShelfService",
			"WindowsBuddy.Tools.AICoding.AICodingService", "WindowsBuddy.Tools.Calendar.CalendarService", "WindowsBuddy.Services.Media.MediaService"
		};
		string[] array2 = array;
		foreach (string name in array2)
		{
			try
			{
				Type type = typeof(ResourceMetrics).Assembly.GetType(name);
				object obj;
				if (!(type == null))
				{
					FieldInfo field = type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
					obj = ((field != null) ? field.GetValue(null) : null);
					if (obj == null || !obj.GetType().IsGenericType || !(obj.GetType().GetGenericTypeDefinition() == typeof(Lazy<>)))
					{
						goto IL_0149;
					}
					if ((bool)obj.GetType().GetProperty("IsValueCreated").GetValue(obj, null))
					{
						obj = obj.GetType().GetProperty("Value").GetValue(obj, null);
						goto IL_0149;
					}
				}
				goto end_IL_0096;
				IL_0149:
				if (obj != null)
				{
					EventInfo[] events = type.GetEvents(BindingFlags.Instance | BindingFlags.Public);
					foreach (EventInfo eventInfo in events)
					{
						AddInstanceEvent(into, type.Name + "." + eventInfo.Name, obj, eventInfo.Name);
					}
				}
				end_IL_0096:;
			}
			catch
			{
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

	private static int CountTopLevelWindows()
	{
		uint me = (uint)Process.GetCurrentProcess().Id;
		int i = 0;
		EnumWindows(delegate(IntPtr h, IntPtr l)
		{
			GetWindowThreadProcessId(h, out var pid);
			if (pid == me)
			{
				i++;
			}
			return true;
		}, IntPtr.Zero);
		return i;
	}

	private static object MediaContextNow()
	{
		Type type = typeof(Brushes).Assembly.GetType("System.Windows.Media.MediaContext");
		return type.GetProperty("CurrentMediaContext", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null);
	}

	public static int CountAnimationClocks()
	{
		try
		{
			object obj = MediaContextNow();
			object value = obj.GetType().GetField("_timeManager", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
			object value2 = value.GetType().GetField("_timeManagerClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
			if (!(value2.GetType().GetField("_rootChildren", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value2) is ICollection collection))
			{
				return -1;
			}
			int num = 0;
			foreach (object item in collection)
			{
				object obj2 = item;
				if (item is WeakReference weakReference)
				{
					obj2 = weakReference.Target;
				}
				if (obj2 is Clock { CurrentState: ClockState.Active })
				{
					num++;
				}
			}
			return num;
		}
		catch
		{
			return -1;
		}
	}

	public static int CountRenderingHandlers()
	{
		try
		{
			object obj = MediaContextNow();
			FieldInfo field = obj.GetType().GetField("Rendering", BindingFlags.Instance | BindingFlags.NonPublic);
			return DelegateCount(field.GetValue(obj) as Delegate);
		}
		catch
		{
			return -1;
		}
	}

	public static List<string> DescribeDispatcherTimers()
	{
		List<string> list = new List<string>();
		try
		{
			Dispatcher dispatcher = Application.Current.Dispatcher;
			FieldInfo field = typeof(Dispatcher).GetField("_timers", BindingFlags.Instance | BindingFlags.NonPublic);
			ICollection collection = ((field != null) ? (field.GetValue(dispatcher) as ICollection) : null);
			if (collection == null)
			{
				return list;
			}
			foreach (object item in collection)
			{
				if (item is DispatcherTimer dispatcherTimer)
				{
					FieldInfo field2 = typeof(DispatcherTimer).GetField("Tick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					Delegate @delegate = ((field2 != null) ? (field2.GetValue(dispatcherTimer) as Delegate) : null);
					string text = (((object)@delegate == null) ? "(no Tick)" : ((@delegate.Method.DeclaringType != null) ? @delegate.Method.DeclaringType.FullName : "?"));
					list.Add(string.Format("{0} every {1} ms, enabled={2}, priority {3}", text, (int)dispatcherTimer.Interval.TotalMilliseconds, dispatcherTimer.IsEnabled, (dispatcherTimer.Dispatcher != null) ? "" : ""));
				}
			}
		}
		catch (Exception ex)
		{
			list.Add("(unavailable: " + ex.GetType().Name + ")");
		}
		return list;
	}

	private static int CountDispatcherTimers(Dispatcher d)
	{
		try
		{
			FieldInfo field = typeof(Dispatcher).GetField("_timers", BindingFlags.Instance | BindingFlags.NonPublic);
			return ((field != null) ? (field.GetValue(d) as ICollection) : null)?.Count ?? (-1);
		}
		catch
		{
			return -1;
		}
	}

	private static void CountSystemEvents(IDictionary<string, int> into)
	{
		try
		{
			Type typeFromHandle = typeof(SystemEvents);
			FieldInfo field = typeFromHandle.GetField("_handlers", BindingFlags.Static | BindingFlags.NonPublic);
			IDictionary dictionary = ((field != null) ? (field.GetValue(null) as IDictionary) : null);
			if (dictionary == null)
			{
				return;
			}
			Dictionary<object, string> dictionary2 = new Dictionary<object, string>();
			FieldInfo[] fields = typeFromHandle.GetFields(BindingFlags.Static | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (fieldInfo.Name.StartsWith("On", StringComparison.Ordinal) && fieldInfo.Name.EndsWith("Event", StringComparison.Ordinal))
				{
					object value = fieldInfo.GetValue(null);
					if (value != null)
					{
						dictionary2[value] = fieldInfo.Name.Substring(2, fieldInfo.Name.Length - 7);
					}
				}
			}
			bool lockTaken = false;
			object obj2 = default(object);
			try
			{
				object obj = dictionary.SyncRoot ?? dictionary;
				obj2 = obj;
				Monitor.Enter(obj, ref lockTaken);
				foreach (DictionaryEntry item in dictionary)
				{
					if (item.Value is ICollection { Count: >0 } collection && dictionary2.TryGetValue(item.Key, out var value2))
					{
						into["Sys." + value2] = collection.Count;
					}
				}
			}
			finally
			{
				if (lockTaken)
				{
					Monitor.Exit(obj2);
				}
			}
		}
		catch
		{
		}
	}

	private static void CountNetworkChange(IDictionary<string, int> into)
	{
		try
		{
			Type typeFromHandle = typeof(NetworkChange);
			Type[] nestedTypes = typeFromHandle.GetNestedTypes(BindingFlags.NonPublic);
			foreach (Type type in nestedTypes)
			{
				FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (fieldInfo.GetValue(null) is IDictionary dictionary && fieldInfo.Name.ToLowerInvariant().Contains("caller"))
					{
						into["Net." + type.Name.Replace("ChangeListener", "")] = dictionary.Count;
					}
				}
			}
		}
		catch
		{
		}
	}

	private static int DelegateCount(Delegate d)
	{
		if ((object)d != null)
		{
			return d.GetInvocationList().Length;
		}
		return 0;
	}

	private static void AddStaticEvent(IDictionary<string, int> into, string label, Type t, string field)
	{
		try
		{
			FieldInfo field2 = t.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (field2 != null)
			{
				into[label] = DelegateCount(field2.GetValue(null) as Delegate);
			}
		}
		catch
		{
		}
	}

	private static void AddInstanceEvent(IDictionary<string, int> into, string label, object target, string field)
	{
		try
		{
			if (target != null)
			{
				FieldInfo field2 = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field2 != null)
				{
					into[label] = DelegateCount(field2.GetValue(target) as Delegate);
				}
			}
		}
		catch
		{
		}
	}
}
