using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class BootTimeline
{
	private sealed class Token : IDisposable
	{
		internal static readonly Token None = new Token(null, null);

		private readonly string _name;

		private readonly Stopwatch _watch;

		private int _done;

		internal Token(string name, Stopwatch watch)
		{
			_name = name;
			_watch = watch;
		}

		public void Dispose()
		{
			if (_name != null && Interlocked.Exchange(ref _done, 1) == 0)
			{
				Log("service " + _name + " end +" + SinceStartMs + " ms (took " + _watch.ElapsedMilliseconds + " ms)");
			}
		}
	}

	public const int StallWindowMs = 20000;

	public const int StallThresholdMs = 200;

	public const int ServiceWindowMs = 120000;

	private static readonly DateTime ProcessStart = ReadProcessStart();

	private static DateTime _firstRender;

	private static int _rendered;

	private static int _reported;

	private static int _monitoring;

	private static long _maxStallMs;

	private static long _stalls;

	internal static Action<string> Sink;

	public static DateTime ProcessStartLocal => ProcessStart;

	public static long MaxStallMs => Interlocked.Read(ref _maxStallMs);

	public static long StallsOverThreshold => Interlocked.Read(ref _stalls);

	public static int FirstRenderMs
	{
		get
		{
			if (Volatile.Read(ref _rendered) != 0)
			{
				return (int)(_firstRender - ProcessStart).TotalMilliseconds;
			}
			return -1;
		}
	}

	private static int SinceStartMs => (int)(DateTime.Now - ProcessStart).TotalMilliseconds;

	private static DateTime ReadProcessStart()
	{
		try
		{
			using Process process = Process.GetCurrentProcess();
			return process.StartTime;
		}
		catch
		{
			return DateTime.Now;
		}
	}

	private static void Log(string line)
	{
		TransactionLogger.Info("Boot: " + line);
		Action<string> sink = Sink;
		if (sink != null)
		{
			try
			{
				sink(line);
			}
			catch
			{
			}
		}
	}

	private static string Clock(DateTime t)
	{
		return t.ToString("HH:mm:ss.f", CultureInfo.InvariantCulture);
	}

	public static void Mark(string phase)
	{
		Log(phase + " +" + SinceStartMs + " ms");
	}

	public static IDisposable Service(string name)
	{
		if (SinceStartMs > 120000)
		{
			return Token.None;
		}
		Log("service " + name + " start +" + SinceStartMs + " ms");
		return new Token(name, Stopwatch.StartNew());
	}

	public static void FirstRender()
	{
		if (Interlocked.Exchange(ref _rendered, 1) == 0)
		{
			_firstRender = DateTime.Now;
			Log("first notch render " + Clock(_firstRender) + " +" + FirstRenderMs + " ms after process start");
			ThreadPool.QueueUserWorkItem(delegate
			{
				Thread.Sleep(20500);
				Report();
			});
		}
	}

	public static void StartStallMonitor(Dispatcher dispatcher)
	{
		if (dispatcher == null || Interlocked.Exchange(ref _monitoring, 1) != 0)
		{
			return;
		}
		Thread thread = new Thread((ThreadStart)delegate
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (stopwatch.ElapsedMilliseconds < 20000 && !dispatcher.HasShutdownStarted)
			{
				ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(initialState: false);
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				try
				{
					dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(manualResetEventSlim.Set));
				}
				catch
				{
					return;
				}
				while (!manualResetEventSlim.Wait(250))
				{
					if (dispatcher.HasShutdownStarted)
					{
						return;
					}
				}
				long elapsedMilliseconds = stopwatch2.ElapsedMilliseconds;
				long num;
				do
				{
					num = Interlocked.Read(ref _maxStallMs);
				}
				while (elapsedMilliseconds > num && Interlocked.CompareExchange(ref _maxStallMs, elapsedMilliseconds, num) != num);
				if (elapsedMilliseconds > 200)
				{
					Interlocked.Increment(ref _stalls);
					Log("UI thread blocked " + elapsedMilliseconds + " ms at +" + SinceStartMs + " ms");
				}
				Thread.Sleep(50);
			}
			Log("UI thread longest stall " + MaxStallMs + " ms; " + StallsOverThreshold + " over " + 200 + " ms in the first " + 20 + " s");
		});
		thread.IsBackground = true;
		thread.Name = "Boot stall monitor";
		thread.Priority = ThreadPriority.AboveNormal;
		Thread thread2 = thread;
		thread2.Start();
	}

	internal static EventRecord Latest(string log, string xpath, DateTime after, DateTime before, bool newest = true)
	{
		try
		{
			string text = "TimeCreated[@SystemTime>='" + after.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) + "' and @SystemTime<='" + before.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) + "']";
			EventLogQuery eventLogQuery = new EventLogQuery(log, PathType.LogName, "*[System[" + xpath + " and " + text + "]]");
			eventLogQuery.ReverseDirection = newest;
			EventLogQuery eventQuery = eventLogQuery;
			using EventLogReader eventLogReader = new EventLogReader(eventQuery);
			return eventLogReader.ReadEvent(TimeSpan.FromSeconds(3.0));
		}
		catch
		{
			return null;
		}
	}

	private static DateTime? Time(EventRecord e)
	{
		if (e == null)
		{
			return null;
		}
		using (e)
		{
			return e.TimeCreated;
		}
	}

	internal static string BootKind(int type)
	{
		return type switch
		{
			2 => "resume from hibernation", 
			1 => "Fast Startup (hybrid boot)", 
			0 => "full boot", 
			_ => "boot type " + type, 
		};
	}

	internal static string Verdict(double windowsGapS, double renderGapS)
	{
		if (renderGapS > 15.0)
		{
			return "Ayn rendered late (" + renderGapS.ToString("0.0", CultureInfo.InvariantCulture) + " s from process start to the notch)";
		}
		if (windowsGapS > 15.0)
		{
			return "Windows launched Ayn late (" + windowsGapS.ToString("0", CultureInfo.InvariantCulture) + " s from sign-in to process start; Ayn itself took " + renderGapS.ToString("0.0", CultureInfo.InvariantCulture) + " s)";
		}
		return "on time (" + (windowsGapS + renderGapS).ToString("0.0", CultureInfo.InvariantCulture) + " s from sign-in to the notch)";
	}

	internal static void Report()
	{
		if (Interlocked.Exchange(ref _reported, 1) != 0)
		{
			return;
		}
		try
		{
			DateTime dateTime = ProcessStart.AddHours(-24.0);
			DateTime before = ProcessStart.AddSeconds(10.0);
			DateTime? dateTime2 = null;
			string text = null;
			EventRecord eventRecord = Latest("System", "Provider[@Name='Microsoft-Windows-Kernel-Boot'] and EventID=27", dateTime, before);
			if (eventRecord != null)
			{
				using (eventRecord)
				{
					dateTime2 = eventRecord.TimeCreated;
					try
					{
						text = BootKind(Convert.ToInt32(eventRecord.Properties[0].Value, CultureInfo.InvariantCulture));
					}
					catch
					{
					}
				}
			}
			DateTime? dateTime3 = Time(Latest("System", "Provider[@Name='Microsoft-Windows-Winlogon'] and EventID=7001", dateTime2 ?? dateTime, before));
			DateTime? dateTime4 = null;
			try
			{
				dateTime4 = StartupService.ShellStartTime();
			}
			catch
			{
			}
			DateTime? dateTime5 = Time(Latest("Microsoft-Windows-Shell-Core/Operational", "EventID=9705", dateTime3 ?? dateTime2 ?? dateTime, before, newest: false));
			DateTime? dateTime6 = null;
			EventRecord eventRecord3 = Latest("Microsoft-Windows-Shell-Core/Operational", "EventID=62408", ProcessStart.AddSeconds(-30.0), before);
			if (eventRecord3 != null)
			{
				using (eventRecord3)
				{
					string text2 = null;
					try
					{
						text2 = eventRecord3.FormatDescription();
					}
					catch
					{
					}
					if (text2 != null && text2.IndexOf("Ayn.exe", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						dateTime6 = eventRecord3.TimeCreated;
					}
				}
			}
			bool flag = dateTime6.HasValue || (dateTime3.HasValue && ProcessStart - dateTime3.Value < TimeSpan.FromMinutes(10.0));
			StringBuilder stringBuilder = new StringBuilder("timeline");
			if (dateTime2.HasValue)
			{
				stringBuilder.Append(" · Windows started " + Clock(dateTime2.Value) + ((text != null) ? (" (" + text + ")") : ""));
			}
			if (dateTime3.HasValue)
			{
				stringBuilder.Append(" · sign-in " + Clock(dateTime3.Value));
			}
			if (dateTime4.HasValue)
			{
				stringBuilder.Append(" · desktop (explorer) " + Clock(dateTime4.Value));
			}
			if (dateTime5.HasValue && flag)
			{
				stringBuilder.Append(" · startup apps began " + Clock(dateTime5.Value));
			}
			if (dateTime6.HasValue)
			{
				stringBuilder.Append(" · Windows launched Ayn (StartupTask) " + Clock(dateTime6.Value));
			}
			stringBuilder.Append(" · process " + Clock(ProcessStart));
			if (FirstRenderMs >= 0)
			{
				stringBuilder.Append(" · first notch render " + Clock(_firstRender) + " (+" + ((double)FirstRenderMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s)");
			}
			stringBuilder.Append(" · UI longest stall " + MaxStallMs + " ms");
			Log(stringBuilder.ToString());
			if (!flag || !dateTime3.HasValue)
			{
				Log("verdict: manual launch (not right after sign-in); process start to notch " + ((double)FirstRenderMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s");
				return;
			}
			double totalSeconds = (ProcessStart - dateTime3.Value).TotalSeconds;
			double renderGapS = (double)Math.Max(0, FirstRenderMs) / 1000.0;
			string text3 = (dateTime5.HasValue ? (" Windows began startup apps " + (dateTime5.Value - dateTime3.Value).TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s after sign-in" + (dateTime6.HasValue ? (" and reached Ayn " + (dateTime6.Value - dateTime5.Value).TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s later.") : ".")) : "");
			Log("verdict: " + Verdict(totalSeconds, renderGapS) + "." + text3);
		}
		catch (Exception ex)
		{
			Log("timeline unavailable (" + ex.GetType().Name + ")");
		}
	}
}
