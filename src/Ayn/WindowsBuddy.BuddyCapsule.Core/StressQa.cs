using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.CoreAudio;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Message;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class StressQa
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private static MethodInfo _raise;

	public static int RunSoak(CapsuleWindow win, string[] args)
	{
		if (!AppPaths.IsSandboxed)
		{
			throw new InvalidOperationException("soak-qa requires AYN_DATA_DIR");
		}
		int num = 1000;
		if (args.Length > 1 && int.TryParse(args[1], out var result))
		{
			num = Math.Max(40, Math.Min(20000, result));
		}
		int num2 = 0;
		int num3 = Array.IndexOf(args, "--duration-seconds");
		if (num3 >= 0 && num3 + 1 < args.Length && int.TryParse(args[num3 + 1], out result))
		{
			num2 = Math.Max(0, Math.Min(86400, result));
		}
		ToolRegistry instance = ToolRegistry.Instance;
		List<string> list = (from t in instance.AllTools
			where ToolRegistry.CanonicalDefaultDockOrder.Contains(t.Id)
			select t.Id).ToList();
		Dispatcher ui = Dispatcher.CurrentDispatcher;
		PillActivityArbiter instance2 = PillActivityArbiter.Instance;
		MessageService instance3 = MessageService.Instance;
		MessageAnimationMode[] array = (MessageAnimationMode[])Enum.GetValues(typeof(MessageAnimationMode));
		NotchBehaviorConfig instance4 = NotchBehaviorConfig.Instance;
		instance4.IsPinned = true;
		if (!args.Contains("--watchdog-probe") && !args.Contains("--starvation-probe"))
		{
			instance4.EventDurationSeconds = 2;
			instance4.SetEventPresentationMode(ActivityType.ClipboardCopied, EventPresentationMode.Brief);
			instance3.Settings.Duration = MessageDurationMode.Custom;
			instance3.Settings.CustomDurationSeconds = 1;
		}
		FieldInfo field = typeof(MessageBannerTool).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
		if (args.Contains("--watchdog-probe") || args.Contains("--starvation-probe"))
		{
			ResourceMetrics.Take(fullGc: false);
			int hangReportCount = UiHangWatchdog.HangReportCount;
			if (args.Contains("--starvation-probe"))
			{
				Stopwatch busyTime = Stopwatch.StartNew();
				Action busy = null;
				busy = delegate
				{
					Thread.SpinWait(10000);
					if (busyTime.ElapsedMilliseconds < 3500)
					{
						ui.BeginInvoke(DispatcherPriority.Render, busy);
					}
				};
				ui.BeginInvoke(DispatcherPriority.Render, busy);
				Pump(4000);
			}
			else
			{
				Thread.Sleep(args.Contains("--long-watchdog-probe") ? 12500 : 3500);
				Pump(1000);
			}
			bool flag = UiHangWatchdog.HangReportCount > hangReportCount;
			Console.WriteLine(flag ? "WATCHDOG-PROBE PASSED" : "WATCHDOG-PROBE FAILED");
			LogWriter.Flush(2000);
			if (!flag)
			{
				return 1;
			}
			return 0;
		}
		int failures = 0;
		int pendingPing = 0;
		long peakDelay = 0L;
		using (new Timer(delegate
		{
			if (Interlocked.CompareExchange(ref pendingPing, 1, 0) != 0)
			{
				return;
			}
			long sent = Stopwatch.GetTimestamp();
			try
			{
				ui.BeginInvoke(DispatcherPriority.Background, (Action)delegate
				{
					long num6 = Stopwatch.GetTimestamp() - sent;
					ResourceMetrics.RecordDispatcherDelay(num6);
					long num7;
					do
					{
						num7 = Interlocked.Read(ref peakDelay);
					}
					while (num6 > num7 && Interlocked.CompareExchange(ref peakDelay, num6, num7) != num7);
					Interlocked.Exchange(ref pendingPing, 0);
				});
			}
			catch
			{
				Interlocked.Exchange(ref pendingPing, 0);
			}
		}, null, 10, 10))
		{
			Console.WriteLine("SOAK-QA minimumCycles=" + num + " minimumSeconds=" + num2 + " tabs=" + string.Join(",", list));
			foreach (string item in list)
			{
				instance.GetTool(item).IsEnabled = true;
				ToolRouter.Instance.RouteTo(item);
				Pump(80);
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			long num4 = 60000L;
			int hangReportCount2 = UiHangWatchdog.HangReportCount;
			ResourceMetrics.Sample sample = null;
			int num5 = Math.Max(10, num / 4);
			for (int i = 0; i < num || stopwatch.Elapsed.TotalSeconds < (double)num2; i++)
			{
				try
				{
					win.RequestExpand("Soak-QA");
					Pump(100);
					if (win.NotchPhase != NotchPhase.Open)
					{
						Pump(200);
					}
					ToolRouter.Instance.RouteTo(list[i % list.Count]);
					Pump(35);
					ToolRouter.Instance.RouteTo("message");
					Pump(35);
					if (!(field.GetValue(instance.GetTool("message")) is FrameworkElement { IsLoaded: not false, IsVisible: not false }))
					{
						Console.WriteLine("FAIL Message view is not actually visible; soak is invalid");
						return 1;
					}
					if (i == 0)
					{
						Console.WriteLine("PASS real Message view loaded and visible");
					}
					instance3.Settings.Text = "Soak " + i % 10 + " ✨";
					instance3.Settings.Animation = array[i % array.Length];
					instance3.Settings.RepeatEnabled = i % 2 == 0;
					instance3.SaveSoon();
					if (!instance3.Flash())
					{
						Console.WriteLine("FAIL Flash cycle " + i);
						failures++;
					}
					win.RequestCollapse("Soak banner");
					Pump(100);
					instance3.TakeDown();
					instance2.PublishClipboardCopied();
					Pump(10);
					instance2.DismissCurrentTransient();
					instance2.PublishVolume(i % 101, isMuted: false);
					Pump(10);
					instance2.DismissCurrentTransient();
					instance2.PublishNetworkState("Soak network", isConnected: true);
					Pump(10);
					instance2.DismissCurrentTransient();
					instance2.PublishBatteryChargingState("Charging", "50%", isCharging: true, isFull: false);
					Pump(10);
					instance2.DismissCurrentTransient();
					instance2.PublishTimerSession("00:10", isPaused: false, 0.5);
					Pump(10);
					instance2.PublishTimerSession("00:10", isPaused: true, 0.5);
					Pump(10);
					instance2.ClearTimer();
					if (i % 50 == 49)
					{
						instance3.Settings.RepeatEnabled = false;
						instance3.Flash();
						Pump(1200);
						if (instance3.IsLive)
						{
							failures++;
							Console.WriteLine("FAIL timed Message expiry");
						}
						instance2.PublishClipboardCopied();
						Pump(2200);
						if (instance2.HasActiveTransient)
						{
							failures++;
							Console.WriteLine("FAIL transient expiry");
						}
					}
				}
				catch (Exception ex)
				{
					failures++;
					Console.WriteLine("FAIL cycle=" + i + " " + ex);
				}
				if (stopwatch.ElapsedMilliseconds >= num4)
				{
					string text = "minute cycle=" + (i + 1) + " " + ResourceMetrics.Take(fullGc: false).ToLine();
					Console.WriteLine(text);
					LogWriter.Enqueue("soak.log", text);
					num4 = stopwatch.ElapsedMilliseconds + 60000;
				}
				if ((i + 1) % num5 != 0 && i + 1 != num)
				{
					continue;
				}
				instance3.TakeDown();
				instance2.ResetToRestingClock();
				ToolRouter.Instance.RouteTo("home");
				win.RequestCollapse("Soak checkpoint");
				Pump(2500);
				ResourceMetrics.Sample sample2 = ResourceMetrics.Take(fullGc: true);
				string text2 = "checkpoint cycle=" + (i + 1) + " " + sample2.ToLine();
				Console.WriteLine(text2);
				LogWriter.Enqueue("soak.log", text2);
				if (sample == null)
				{
					sample = sample2;
					continue;
				}
				Action<string, long, long, long> action = delegate(string label, long a, long b, long slack)
				{
					if (a < 0 || b < 0)
					{
						failures++;
						Console.WriteLine("FAIL unavailable " + label);
					}
					else if (b - a > slack)
					{
						failures++;
						Console.WriteLine("FAIL growing " + label + " " + a + " -> " + b);
					}
				};
				action("GDI", sample.Gdi, sample2.Gdi, 5L);
				action("USER", sample.User, sample2.User, 5L);
				action("handles", sample.Handles, sample2.Handles, 40L);
				action("threads", sample.Threads, sample2.Threads, 8L);
				action("windows", sample.Windows, sample2.Windows, 0L);
				action("timers", sample.DispatcherTimers, sample2.DispatcherTimers, 0L);
				action("clocks", sample.AnimationClocks, sample2.AnimationClocks, 0L);
				action("rendering", sample.RenderingHandlers, sample2.RenderingHandlers, 0L);
				action("managedKB", sample.ManagedKb, sample2.ManagedKb, 6144L);
				action("privateKB", sample.PrivateKb, sample2.PrivateKb, 32768L);
				foreach (KeyValuePair<string, int> subscription in sample2.Subscriptions)
				{
					sample.Subscriptions.TryGetValue(subscription.Key, out var value);
					action(subscription.Key, value, subscription.Value, 0L);
				}
			}
			if (UiHangWatchdog.HangReportCount != hangReportCount2)
			{
				failures++;
				Console.WriteLine("FAIL hang reports");
			}
			Console.WriteLine("SOAK elapsedSec=" + stopwatch.Elapsed.TotalSeconds.ToString("F1") + " peakDispatcherMs=" + ((double)Interlocked.Read(ref peakDelay) * 1000.0 / (double)Stopwatch.Frequency).ToString("F1"));
			Console.WriteLine((failures == 0) ? "SOAK-QA PASSED" : ("SOAK-QA FAILED " + failures));
			LogWriter.Flush(2000);
			return (failures != 0) ? 1 : 0;
		}
	}

	private static void Pump(int ms)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ms);
		do
		{
			DispatcherFrame frame = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
			{
				frame.Continue = false;
			});
			Dispatcher.PushFrame(frame);
			if (ms > 0)
			{
				Thread.Sleep(5);
			}
		}
		while (DateTime.UtcNow < dateTime);
	}

	public static int Run(CapsuleWindow win, string[] args)
	{
		int result = 200;
		if (args.Length > 1)
		{
			int.TryParse(args[1], out result);
		}
		List<KeyValuePair<string, Action>> list = Events(win);
		Console.WriteLine("STRESS-QA: " + list.Count + " event kinds x " + result + " cycles");
		win.RequestExpand("Stress-QA");
		Pump(800);
		win.RequestCollapse("Stress-QA");
		Pump(500);
		foreach (KeyValuePair<string, Action> item in list)
		{
			for (int i = 0; i < 5; i++)
			{
				Fire(item.Value, item.Key);
				Pump(10);
			}
		}
		Pump(3000);
		ResourceMetrics.Sample sample = ResourceMetrics.Take(fullGc: true);
		Console.WriteLine("  before: " + sample.ToLine());
		int hangReportCount = UiHangWatchdog.HangReportCount;
		Stopwatch stopwatch = Stopwatch.StartNew();
		Round(list, result, print: true);
		sample = ResourceMetrics.Take(fullGc: true);
		Console.WriteLine("  after round 1: " + sample.ToLine());
		Round(list, result, print: false);
		ResourceMetrics.Sample sample2 = ResourceMetrics.Take(fullGc: true);
		Console.WriteLine("  after round 2: " + sample2.ToLine());
		int failures = 0;
		Action<string, long, long, long> action = delegate(string what, long a, long b, long slack)
		{
			bool flag = b - a <= slack;
			Console.WriteLine(string.Format("  {0} {1,-20} {2} -> {3} (allowed +{4})", flag ? "PASS" : "FAIL", what, a, b, slack));
			if (!flag)
			{
				failures++;
			}
		};
		action("kernel handles", sample.Handles, sample2.Handles, 60L);
		action("GDI objects", sample.Gdi, sample2.Gdi, 10L);
		action("USER objects", sample.User, sample2.User, 10L);
		action("threads", sample.Threads, sample2.Threads, 4L);
		action("top-level windows", sample.Windows, sample2.Windows, 0L);
		action("dispatcher timers", sample.DispatcherTimers, sample2.DispatcherTimers, 2L);
		action("managed heap KB", sample.ManagedKb, sample2.ManagedKb, 6144L);
		action("private bytes KB", sample.PrivateKb, sample2.PrivateKb, 25600L);
		foreach (KeyValuePair<string, int> subscription in sample2.Subscriptions)
		{
			sample.Subscriptions.TryGetValue(subscription.Key, out var value);
			action("subs " + subscription.Key, value, subscription.Value, 0L);
		}
		int num = UiHangWatchdog.HangReportCount - hangReportCount;
		action("hang reports", 0L, num, 0L);
		Console.WriteLine($"  total {stopwatch.Elapsed.TotalSeconds:F1} s");
		Console.WriteLine((failures == 0) ? "STRESS-QA PASSED" : ("STRESS-QA FAILED: " + failures));
		return failures;
	}

	private static void Round(List<KeyValuePair<string, Action>> events, int cycles, bool print)
	{
		foreach (KeyValuePair<string, Action> @event in events)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < cycles; i++)
			{
				Fire(@event.Value, @event.Key);
				if (i % 10 == 0)
				{
					Pump(0);
				}
			}
			Pump(200);
			if (print)
			{
				Console.WriteLine($"  {@event.Key,-16} x{cycles} in {stopwatch.ElapsedMilliseconds} ms");
			}
		}
		Pump(6000);
	}

	private static void Fire(Action a, string name)
	{
		try
		{
			a();
		}
		catch (Exception ex)
		{
			Console.WriteLine("  (" + name + " threw " + ex.GetType().Name + ": " + ex.Message + ")");
		}
	}

	private static List<KeyValuePair<string, Action>> Events(CapsuleWindow win)
	{
		List<KeyValuePair<string, Action>> list = new List<KeyValuePair<string, Action>>();
		list.Add(new KeyValuePair<string, Action>("sleep/resume", delegate
		{
			RaiseSystemEvent("OnPowerModeChangedEvent", new PowerModeChangedEventArgs(PowerModes.Suspend));
			RaiseSystemEvent("OnPowerModeChangedEvent", new PowerModeChangedEventArgs(PowerModes.Resume));
		}));
		list.Add(new KeyValuePair<string, Action>("lock/unlock", delegate
		{
			RaiseSystemEvent("OnSessionSwitchEvent", new SessionSwitchEventArgs(SessionSwitchReason.SessionLock));
			RaiseSystemEvent("OnSessionSwitchEvent", new SessionSwitchEventArgs(SessionSwitchReason.SessionUnlock));
		}));
		list.Add(new KeyValuePair<string, Action>("monitor change", delegate
		{
			RaiseSystemEvent("OnDisplaySettingsChangingEvent", EventArgs.Empty);
			RaiseSystemEvent("OnDisplaySettingsChangedEvent", EventArgs.Empty);
		}));
		list.Add(new KeyValuePair<string, Action>("network change", delegate
		{
			RaiseNetworkChange();
		}));
		list.Add(new KeyValuePair<string, Action>("DPI change", delegate
		{
			SendDpiChanged(win);
		}));
		list.Add(new KeyValuePair<string, Action>("audio device", delegate
		{
			IMMNotificationClient instance = CoreAudioService.Instance;
			instance.OnDefaultDeviceChanged(EDataFlow.eRender, ERole.eMultimedia, "stress-qa");
		}));
		List<string> tabs = ToolRegistry.CanonicalDefaultDockOrder.Where((string id) => ToolRegistry.Instance.GetTool(id) != null).ToList();
		int next = 0;
		list.Add(new KeyValuePair<string, Action>("panel+tab", delegate
		{
			win.RequestExpand("Stress-QA");
			Pump(25);
			if (tabs.Count > 0)
			{
				ToolRouter.Instance.RouteTo(tabs[next++ % tabs.Count]);
			}
			Pump(25);
			win.RequestCollapse("Stress-QA");
			Pump(25);
		}));
		list.Add(new KeyValuePair<string, Action>("pref/time change", delegate
		{
			RaiseSystemEvent("OnUserPreferenceChangedEvent", new UserPreferenceChangedEventArgs(UserPreferenceCategory.General));
			RaiseSystemEvent("OnTimeChangedEvent", EventArgs.Empty);
		}));
		return list;
	}

	private static void RaiseSystemEvent(string keyField, EventArgs args)
	{
		Type typeFromHandle = typeof(SystemEvents);
		object value = typeFromHandle.GetField(keyField, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
		if (_raise == null)
		{
			_raise = typeFromHandle.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).First((MethodInfo m) => m.Name == "RaiseEvent" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(object));
		}
		_raise.Invoke(null, new object[2]
		{
			value,
			new object[2] { null, args }
		});
	}

	private static void RaiseNetworkChange()
	{
		Type[] nestedTypes = typeof(NetworkChange).GetNestedTypes(BindingFlags.NonPublic);
		object obj2 = default(object);
		foreach (Type type in nestedTypes)
		{
			FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (!(fieldInfo.GetValue(null) is IDictionary dictionary) || !fieldInfo.Name.ToLowerInvariant().Contains("caller"))
				{
					continue;
				}
				bool lockTaken = false;
				List<object> list;
				try
				{
					object obj = dictionary.SyncRoot ?? dictionary;
					obj2 = obj;
					Monitor.Enter(obj, ref lockTaken);
					list = dictionary.Keys.Cast<object>().ToList();
				}
				finally
				{
					if (lockTaken)
					{
						Monitor.Exit(obj2);
					}
				}
				foreach (object item in list)
				{
					NetworkAddressChangedEventHandler addr = item as NetworkAddressChangedEventHandler;
					if (addr != null)
					{
						ThreadPool.QueueUserWorkItem(delegate
						{
							addr(null, EventArgs.Empty);
						});
					}
					NetworkAvailabilityChangedEventHandler avail = item as NetworkAvailabilityChangedEventHandler;
					if (avail != null)
					{
						ConstructorInfo constructor = typeof(NetworkAvailabilityEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[1] { typeof(bool) }, null);
						NetworkAvailabilityEventArgs ea = (NetworkAvailabilityEventArgs)constructor.Invoke(new object[1] { true });
						ThreadPool.QueueUserWorkItem(delegate
						{
							avail(null, ea);
						});
					}
				}
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr h, out RECT r);

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(IntPtr h);

	private static void SendDpiChanged(Window win)
	{
		IntPtr handle = new WindowInteropHelper(win).Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}
		uint num = 96u;
		try
		{
			num = GetDpiForWindow(handle);
		}
		catch
		{
		}
		GetWindowRect(handle, out var r);
		IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));
		try
		{
			Marshal.StructureToPtr(r, intPtr, fDeleteOld: false);
			SendMessage(handle, 736, (IntPtr)((num << 16) | num), intPtr);
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}
}
