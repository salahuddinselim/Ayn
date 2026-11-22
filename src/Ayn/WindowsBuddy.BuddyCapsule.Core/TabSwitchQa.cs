using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools.Message;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class TabSwitchQa
{
	private const uint LEFTDOWN = 2u;

	private const uint LEFTUP = 4u;

	private static int _fails;

	private static long _maxBlockTicks;

	private static volatile bool _meterOn;

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

	[DllImport("gdi32.dll")]
	private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);

	[DllImport("user32.dll")]
	private static extern IntPtr GetDC(IntPtr h);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(IntPtr h, IntPtr dc);

	private static byte[] GrabScreen(int x, int y, int w, int h)
	{
		using Bitmap bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			IntPtr hdc = graphics.GetHdc();
			IntPtr dC = GetDC(IntPtr.Zero);
			BitBlt(hdc, 0, 0, w, h, dC, x, y, 1087111200);
			ReleaseDC(IntPtr.Zero, dC);
			graphics.ReleaseHdc(hdc);
		}
		BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		byte[] array = new byte[bitmapData.Stride * h];
		Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
		bitmap.UnlockBits(bitmapData);
		return array;
	}

	private static double ScreenVsRender(FrameworkElement el, double sc)
	{
		int num = (int)Math.Round(el.ActualWidth * sc);
		int num2 = (int)Math.Round(el.ActualHeight * sc);
		if (num < 8 || num2 < 8)
		{
			return 0.0;
		}
		System.Windows.Point point = el.PointToScreen(new System.Windows.Point(0.0, 0.0));
		byte[] array = GrabScreen((int)Math.Round(point.X), (int)Math.Round(point.Y), num, num2);
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(num, num2, 96.0 * sc, 96.0 * sc, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(el);
		int num3 = num * 4;
		byte[] array2 = new byte[num3 * num2];
		renderTargetBitmap.CopyPixels(array2, num3, 0);
		long num4 = 0L;
		long num5 = 0L;
		for (int i = 0; i < num2; i += 2)
		{
			for (int j = 0; j < num; j += 2)
			{
				int num6 = i * num3 + j * 4;
				int num7 = i * (array.Length / num2) + j * 4;
				num5++;
				if (Math.Abs(array[num7] - array2[num6]) > 48 || Math.Abs(array[num7 + 1] - array2[num6 + 1]) > 48 || Math.Abs(array[num7 + 2] - array2[num6 + 2]) > 48)
				{
					num4++;
				}
			}
		}
		if (num5 != 0)
		{
			return (double)num4 / (double)num5;
		}
		return 0.0;
	}

	private static double PillCentreX(FrameworkElement dock, double sc)
	{
		int num = (int)Math.Round(dock.ActualWidth * sc);
		int num2 = (int)Math.Round(dock.ActualHeight * sc);
		System.Windows.Point point = dock.PointToScreen(new System.Windows.Point(0.0, 0.0));
		byte[] array = GrabScreen((int)Math.Round(point.X), (int)Math.Round(point.Y), num, num2);
		int num3 = array.Length / num2;
		double num4 = 0.0;
		double num5 = 0.0;
		for (int i = 0; i < num; i++)
		{
			int num6 = 0;
			for (int j = 0; j < num2; j++)
			{
				int num7 = j * num3 + i * 4;
				if (array[num7] >= 30 && array[num7] <= 90 && Math.Abs(array[num7] - array[num7 + 1]) < 12 && Math.Abs(array[num7] - array[num7 + 2]) < 12)
				{
					num6++;
				}
			}
			if (num6 >= (int)((double)num2 * 0.5))
			{
				num4 += (double)(i * num6);
				num5 += (double)num6;
			}
		}
		if (!(num5 < 50.0))
		{
			return point.X + num4 / num5;
		}
		return double.NaN;
	}

	private static void Check(bool ok, string what)
	{
		Console.WriteLine((ok ? "  PASS " : "  FAIL ") + what);
		if (!ok)
		{
			_fails++;
		}
	}

	private static T Field<T>(object o, string name)
	{
		Type type = o.GetType();
		while (type != null)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				return (T)field.GetValue(o);
			}
			type = type.BaseType;
		}
		throw new MissingFieldException(o.GetType().Name, name);
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
				Thread.Sleep(2);
			}
		}
		while (DateTime.UtcNow < dateTime);
	}

	private static void StartMeter(Dispatcher ui)
	{
		_meterOn = true;
		Thread thread = new Thread((ThreadStart)delegate
		{
			while (_meterOn)
			{
				long sent = Stopwatch.GetTimestamp();
				ManualResetEventSlim done = new ManualResetEventSlim(initialState: false);
				try
				{
					ui.BeginInvoke(DispatcherPriority.Send, (Action)delegate
					{
						long num = Stopwatch.GetTimestamp() - sent;
						long num2;
						do
						{
							num2 = Interlocked.Read(ref _maxBlockTicks);
						}
						while (num > num2 && Interlocked.CompareExchange(ref _maxBlockTicks, num, num2) != num2);
						done.Set();
					});
				}
				catch
				{
					break;
				}
				done.Wait(2000);
				Thread.Sleep(4);
			}
		});
		thread.IsBackground = true;
		thread.Name = "tab-switch-qa meter";
		Thread thread2 = thread;
		thread2.Start();
	}

	private static double TakeMaxBlockMs()
	{
		long num = Interlocked.Exchange(ref _maxBlockTicks, 0L);
		return (double)num * 1000.0 / (double)Stopwatch.Frequency;
	}

	public static int Run(CapsuleWindow win, string[] args)
	{
		_fails = 0;
		int result = 500;
		if (args.Length > 1)
		{
			int.TryParse(args[1], out result);
		}
		NotchConfig instance = NotchConfig.Instance;
		Dispatcher dispatcher = win.Dispatcher;
		Console.WriteLine("TAB-SWITCH-QA: " + result + " real-click switches. Data: " + AppPaths.DataDir);
		MessageSettings settings = MessageService.Instance.Settings;
		Console.WriteLine($"  Message setup: animation {settings.Animation}, speed {settings.Speed}, duration {settings.Duration}, colour {settings.ColorHex}, finish {settings.Finish}, text '{settings.Text}'");
		instance.EdgeLightEnabled = true;
		instance.EdgeLightWhen = "Always";
		instance.EdgeLightStyle = "Twin comets";
		Pump(1500);
		PillActivityArbiter instance2 = PillActivityArbiter.Instance;
		instance2.PublishMedia(new PillActivitySnapshot("media_session", "media", ActivityTier.Tier2_Session, ActivityType.Media, "Starboy", "The Weeknd", "1:20", (double sz, System.Windows.Media.Brush br) => VectorIcons.CreateMusicIcon(sz, br), null, null, 0.3, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Media, isPlaying: true, null, null, null, "media", null));
		Pump(800);
		SoundPlayer soundPlayer = null;
		try
		{
			MemoryStream memoryStream = new MemoryStream();
			int num = 44100;
			int num2 = 2;
			int num3 = num * num2;
			BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(new char[4] { 'R', 'I', 'F', 'F' });
			binaryWriter.Write(36 + num3 * 4);
			binaryWriter.Write(new char[8] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
			binaryWriter.Write(16);
			binaryWriter.Write((short)1);
			binaryWriter.Write((short)2);
			binaryWriter.Write(num);
			binaryWriter.Write(num * 4);
			binaryWriter.Write((short)4);
			binaryWriter.Write((short)16);
			binaryWriter.Write(new char[4] { 'd', 'a', 't', 'a' });
			binaryWriter.Write(num3 * 4);
			for (int j = 0; j < num3; j++)
			{
				double num4 = (double)j / (double)num;
				double num5 = Math.Exp(-6.0 * (num4 % 0.5));
				short value = (short)(Math.Sin(Math.PI * 140.0 * num4) * num5 * 2200.0 + Math.Sin(Math.PI * 880.0 * num4) * 300.0);
				binaryWriter.Write(value);
				binaryWriter.Write(value);
			}
			memoryStream.Position = 0L;
			soundPlayer = new SoundPlayer(memoryStream);
			soundPlayer.PlayLooping();
		}
		catch (Exception ex)
		{
			Console.WriteLine("  (no audio: " + ex.Message + ")");
		}
		win.RequestExpand("Tab-QA");
		Pump(900);
		Dictionary<string, Button> dictionary = Field<Dictionary<string, Button>>(win, "_dockButtons");
		Grid grid = Field<Grid>(win, "_viewportHost");
		StackPanel stackPanel = Field<StackPanel>(win, "_navTabsPanel");
		List<string> list = dictionary.Keys.Where((string k) => k != "more").ToList();
		Pump(1500);
		Console.WriteLine("  beat running: " + win.BeatActive + " (real audio playing, quiet)");
		Console.WriteLine("  tabs: " + string.Join(", ", list.ToArray()));
		double m = PresentationSource.FromVisual(win).CompositionTarget.TransformToDevice.M11;
		StartMeter(dispatcher);
		bool frameSeen = false;
		long tUp = 0L;
		long tFrame = 0L;
		bool gotUp = false;
		long tNav = 0L;
		bool navSeen = false;
		EventHandler value2 = delegate
		{
			frameSeen = true;
			if (navSeen && tFrame == 0)
			{
				tFrame = Stopwatch.GetTimestamp();
			}
		};
		EventHandler<ToolNavigationEventArgs> value3 = delegate(object s, ToolNavigationEventArgs e)
		{
			if (!e.IsSwap && !navSeen)
			{
				navSeen = true;
				tNav = Stopwatch.GetTimestamp();
			}
		};
		ToolRouter.Instance.Navigated += value3;
		MouseButtonEventHandler handler = delegate
		{
			tUp = Stopwatch.GetTimestamp();
			gotUp = true;
		};
		win.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, handler, handledEventsToo: true);
		CompositionTarget.Rendering += value2;
		List<string> others = list.Where((string k) => k != "message").ToList();
		Func<int, string> func = (int i) => (i % 2 != 1) ? others[i / 2 % others.Count] : "message";
		List<double> list2 = new List<double>();
		double num6 = 0.0;
		double num7 = 0.0;
		string text = "";
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		Dictionary<string, double> dictionary2 = new Dictionary<string, double>();
		Dictionary<string, double> perTabBlock = new Dictionary<string, double>();
		Dictionary<string, List<int>> dictionary3 = new Dictionary<string, List<int>>();
		int num11 = 0;
		int num12 = 0;
		List<double> list3 = new List<double>();
		List<double> list4 = new List<double>();
		int num13 = 0;
		List<double> list5 = new List<double>();
		List<double> list6 = new List<double>();
		List<double> list7 = new List<double>();
		Dictionary<string, List<double>> dictionary4 = new Dictionary<string, List<double>>();
		Dictionary<string, int> dictionary5 = new Dictionary<string, int>();
		int num14 = ResourceMetrics.CountAnimationClocks();
		Stopwatch stopwatch = new Stopwatch();
		ResourceMetrics.Sample sample = null;
		ResourceMetrics.Sample sample2 = null;
		ResourceMetrics.Sample sample3 = null;
		Console.WriteLine("  switch  clocks rendering timers subs managedKB  handles  gdi user | last first-frame ms, longest block ms");
		for (int n = 0; n < result; n++)
		{
			if (win.NotchPhase != NotchPhase.Open)
			{
				num13++;
				win.RequestExpand("Tab-QA reopen");
				Pump(900);
				System.Windows.Point point = dictionary["home"].PointToScreen(new System.Windows.Point(5.0, 5.0));
				SetCursorPos((int)point.X, (int)point.Y);
				Pump(200);
			}
			string text2 = func(n);
			if (string.Equals(ToolRouter.Instance.ActiveToolId, text2, StringComparison.OrdinalIgnoreCase))
			{
				text2 = others[(n + 1) % others.Count];
			}
			Button button = dictionary[text2];
			System.Windows.Point point2 = button.PointToScreen(new System.Windows.Point(button.ActualWidth / 2.0, button.ActualHeight / 2.0));
			SetCursorPos((int)point2.X, (int)point2.Y);
			Pump(20);
			TakeMaxBlockMs();
			frameSeen = false;
			gotUp = false;
			navSeen = false;
			tFrame = 0L;
			stopwatch.Restart();
			long timestamp = Stopwatch.GetTimestamp();
			mouse_event(2u, 0u, 0u, 0u, UIntPtr.Zero);
			mouse_event(4u, 0u, 0u, 0u, UIntPtr.Zero);
			double num15 = -1.0;
			while (stopwatch.ElapsedMilliseconds < 4000)
			{
				DispatcherFrame f = new DispatcherFrame();
				Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
				{
					f.Continue = false;
				});
				Dispatcher.PushFrame(f);
				if (num15 < 0.0 && string.Equals(ToolRouter.Instance.ActiveToolId, text2, StringComparison.OrdinalIgnoreCase))
				{
					num15 = stopwatch.Elapsed.TotalMilliseconds;
					frameSeen = false;
				}
				if (num15 >= 0.0 && frameSeen)
				{
					break;
				}
				Thread.Sleep(1);
			}
			if (num15 >= 0.0)
			{
				list5.Add(ToolRouter.LastRouteMs);
				list6.Add(ToolRouter.LastNavigatedMs);
			}
			double num16 = (gotUp ? ((double)(tUp - timestamp) * 1000.0 / (double)Stopwatch.Frequency) : 0.0);
			double num17 = ((gotUp && tFrame >= tUp && tFrame > 0) ? ((double)(tFrame - tUp) * 1000.0 / (double)Stopwatch.Frequency) : stopwatch.Elapsed.TotalMilliseconds);
			list4.Add(num16);
			if (num15 < 0.0)
			{
				num10++;
				Console.WriteLine("  switch " + n + ": " + text2 + " never became active within 4 s");
			}
			list7.Add(num15);
			dictionary5.TryGetValue(text2, out var value4);
			dictionary5[text2] = value4 + 1;
			if (value4 >= 1)
			{
				if (!dictionary4.TryGetValue(text2, out var value5))
				{
					value5 = (dictionary4[text2] = new List<double>());
				}
				value5.Add(num17);
			}
			if (value4 == 0 && Environment.GetEnvironmentVariable("VAYLI_TRACE") == "1")
			{
				Console.WriteLine($"    first visit {text2,-10} click->app {num16,5:0.0}  until active {num15,6:0.0}  route {ToolRouter.LastRouteMs,5:0.0}  navigated {ToolRouter.LastNavigatedMs,5:0.0}  click->first frame {num17,6:0.0}");
			}
			list2.Add(num17);
			if (num17 > num6)
			{
				num6 = num17;
				text = text2;
			}
			dictionary2.TryGetValue(text2, out var value6);
			if (num17 > value6)
			{
				dictionary2[text2] = num17;
			}
			Pump(420);
			int num18 = ResourceMetrics.CountAnimationClocks();
			if (!dictionary3.TryGetValue(text2, out var value7))
			{
				value7 = (dictionary3[text2] = new List<int>());
			}
			value7.Add(num18 - num14);
			num14 = num18;
			double num19 = TakeMaxBlockMs();
			if (num19 > num7)
			{
				num7 = num19;
			}
			perTabBlock.TryGetValue(text2, out value6);
			if (num19 > value6)
			{
				perTabBlock[text2] = num19;
			}
			string text3 = null;
			foreach (UIElement child in grid.Children)
			{
				if (child is FrameworkElement frameworkElement)
				{
					bool flag = child == Field<UIElement>(win, "_currentToolView");
					if (!flag && child.Visibility == Visibility.Visible && child.Opacity > 0.01)
					{
						text3 = "another tab is still visible (" + frameworkElement.GetType().Name + ", opacity " + child.Opacity.ToString("0.00") + ")";
					}
					if (flag && (child.Visibility != 0 || child.Opacity < 0.99))
					{
						text3 = "the new tab is not fully shown (opacity " + child.Opacity.ToString("0.00") + ")";
					}
				}
			}
			if (text3 != null)
			{
				num9++;
				Console.WriteLine("  switch " + n + " -> " + text2 + ": " + text3);
			}
			double num20 = ScreenVsRender(grid, m);
			if (Environment.GetEnvironmentVariable("VAYLI_TRACE") == "1")
			{
				list3.Add(num20);
			}
			int num21;
			switch (text2)
			{
			default:
				num21 = ((text2 == "system") ? 1 : 0);
				break;
			case "home":
			case "clock":
			case "timer":
			case "message":
			case "screentime":
			case "analytics":
			case "aicoding":
			case "weather":
				num21 = 1;
				break;
			}
			bool flag2 = (byte)num21 != 0;
			if (num20 > (flag2 ? 0.3 : 0.15))
			{
				num11++;
				if (num11 <= 5)
				{
					Console.WriteLine("  switch " + n + " -> " + text2 + ": the screen differs from the tab in " + (num20 * 100.0).ToString("0") + "% of the viewport");
				}
			}
			double num22 = PillCentreX(stackPanel, m);
			double num23 = button.PointToScreen(new System.Windows.Point(0.0, 0.0)).X + button.ActualWidth * m / 2.0;
			if (double.IsNaN(num22) || Math.Abs(num22 - num23) > 6.0 * m)
			{
				num12++;
				if (num12 <= 5)
				{
					Console.WriteLine("  switch " + n + " -> " + text2 + ": white dock pill on screen at x " + (double.IsNaN(num22) ? "(none)" : num22.ToString("0")) + ", selected icon centre " + num23.ToString("0"));
				}
			}
			Border border = Field<Border>(win, "_dockIndicator");
			TranslateTransform translateTransform = Field<TranslateTransform>(win, "_dockIndicatorTransform");
			double x = button.TranslatePoint(new System.Windows.Point(0.0, 0.0), stackPanel).X;
			_ = button.Content;
			if (Math.Abs(translateTransform.X - x) > 1.5 || border.Opacity < 0.99)
			{
				num8++;
				if (num8 <= 5)
				{
					Console.WriteLine("  switch " + n + " -> " + text2 + ": dock pill at " + translateTransform.X.ToString("0.0") + ", selected icon at " + x.ToString("0.0"));
				}
			}
			if ((n + 1) % 50 == 0 || n == 0)
			{
				ResourceMetrics.Sample sample4 = ResourceMetrics.Take(fullGc: true);
				if (sample3 == null)
				{
					sample3 = sample4;
				}
				if (n + 1 == 50)
				{
					sample = sample4;
				}
				if (n + 1 == result)
				{
					sample2 = sample4;
				}
				double num24 = list2.Skip(Math.Max(0, list2.Count - 50)).Average();
				Console.WriteLine($"  {n + 1,6}  {sample4.AnimationClocks,6} {sample4.RenderingHandlers,9} {sample4.DispatcherTimers,6} {sample4.TotalSubscriptions,4} {sample4.ManagedKb,9} {sample4.Handles,8} {sample4.Gdi,4} {sample4.User,4} | avg {num24:0.0}, worst {num6:0.0}, block {num7:0.0}");
			}
		}
		CompositionTarget.Rendering -= value2;
		win.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, handler);
		ToolRouter.Instance.Navigated -= value3;
		_meterOn = false;
		if (sample2 == null)
		{
			sample2 = ResourceMetrics.Take(fullGc: true);
		}
		if (sample == null)
		{
			sample = sample3;
		}
		Console.WriteLine("  per tab (worst first frame ms / worst UI block ms): " + string.Join(", ", (from k in dictionary2
			orderby k.Value descending
			select k.Key + " " + k.Value.ToString("0") + "/" + perTabBlock[k.Key].ToString("0")).ToArray()));
		Func<List<double>, double, double> pct = delegate(List<double> l, double q)
		{
			List<double> list11 = l.OrderBy((double v) => v).ToList();
			return (list11.Count != 0) ? list11[Math.Min(list11.Count - 1, (int)(q * (double)list11.Count))] : 0.0;
		};
		List<double> list10 = dictionary4.Values.SelectMany((List<double> v) => v).ToList();
		Console.WriteLine($"  after the first visit of each tab ({list10.Count} switches): first frame p50 {pct(list10, 0.5):0.0}, p90 {pct(list10, 0.9):0.0}, max {((list10.Count == 0) ? 0.0 : list10.Max()):0.0} ms; time until the router shows the tab p50 {pct(list7, 0.5):0.0}, max {list7.Max():0.0} ms");
		Console.WriteLine($"  Windows input latency (mouse_event -> the click reaches the app): p50 {pct(list4, 0.5):0.0}, max {((list4.Count == 0) ? 0.0 : list4.Max()):0.0} ms (not counted)");
		Console.WriteLine($"  router work per switch: RouteTo p50 {pct(list5, 0.5):0.0}, max {((list5.Count == 0) ? 0.0 : list5.Max()):0.0} ms; Navigated handlers (the visible switch) p50 {pct(list6, 0.5):0.0}, max {((list6.Count == 0) ? 0.0 : list6.Max()):0.0} ms");
		Console.WriteLine("  warm first-frame p50 per tab: " + string.Join(", ", (from k in dictionary4
			orderby pct(k.Value, 0.5) descending
			select k.Key + " " + pct(k.Value, 0.5).ToString("0")).ToArray()));
		Console.WriteLine("  animation clocks gained per switch TO each tab (average): " + string.Join(", ", (from k in dictionary3
			orderby k.Value.Average() descending
			select k.Key + " " + k.Value.Average().ToString("+0.0;-0.0;0.0")).ToArray()));
		Console.WriteLine("  live dispatcher timers (owner class, interval):");
		foreach (string item in ResourceMetrics.DescribeDispatcherTimers())
		{
			Console.WriteLine("    " + item);
		}
		Console.WriteLine("  OnActivated (worst ms): " + string.Join(", ", (from k in ToolRouter.ActivationMs
			orderby k.Value descending
			select k.Key + " " + k.Value.ToString("0.0")).ToArray()));
		int num25 = list2.Count((double v) => v >= 50.0);
		if (num13 > 0)
		{
			Console.WriteLine("  note: the panel had to be re-opened " + num13 + " time(s) (the mouse was moved by someone during the run)");
		}
		Check(num10 == 0, "every click showed its tab (" + num10 + " did not)");
		Check(num25 == 0, "first frame under 50 ms for every switch: " + num25 + " of " + list2.Count + " took 50 ms or more (worst " + num6.ToString("0.0") + " ms on " + text + ")");
		Check(num7 < 100.0, "UI thread never blocked 100 ms or more (longest " + num7.ToString("0.0") + " ms)");
		if (list3.Count > 0)
		{
			Console.WriteLine("  screen-vs-render difference of the viewport: p50 " + (pct(list3, 0.5) * 100.0).ToString("0.0") + "%, p95 " + (pct(list3, 0.95) * 100.0).ToString("0.0") + "%, max " + (list3.Max() * 100.0).ToString("0.0") + "%");
		}
		Check(num11 == 0, "pixel check: the screen shows only the new tab (no previous tab behind it): " + num11 + " switches differ");
		Check(num12 == 0, "pixel check: the white dock pill ends on the selected icon (within 6 DIP): " + num12 + " switches off");
		Check(num9 == 0, "no previous tab visible behind the new one: " + num9 + " switches");
		Check(num8 == 0, "dock pill always on the selected icon: " + num8 + " switches");
		Check(sample2.AnimationClocks <= sample.AnimationClocks + 2, "animation clocks do not grow after switch 50 (" + sample.AnimationClocks + " -> " + sample2.AnimationClocks + ")");
		Check(sample2.RenderingHandlers <= sample.RenderingHandlers, "Rendering handlers do not grow (" + sample.RenderingHandlers + " -> " + sample2.RenderingHandlers + ")");
		Check(sample2.DispatcherTimers <= sample.DispatcherTimers + 1, "dispatcher timers do not grow (" + sample.DispatcherTimers + " -> " + sample2.DispatcherTimers + ")");
		Check(sample2.TotalSubscriptions <= sample.TotalSubscriptions, "event subscriptions do not grow (" + sample.TotalSubscriptions + " -> " + sample2.TotalSubscriptions + ")");
		Check(sample2.ManagedKb <= sample.ManagedKb + 8192, "managed memory does not grow more than 8 MB (" + sample.ManagedKb + " -> " + sample2.ManagedKb + " KB)");
		Check(sample2.Handles <= sample.Handles + 60 && sample2.User <= sample.User + 10 && sample2.Gdi <= sample.Gdi + 10, "handles / USER / GDI flat (" + sample.Handles + "/" + sample.User + "/" + sample.Gdi + " -> " + sample2.Handles + "/" + sample2.User + "/" + sample2.Gdi + ")");
		foreach (KeyValuePair<string, double> activationM in ToolRouter.ActivationMs)
		{
			if (activationM.Value >= 8.0)
			{
				Check(ok: false, "OnActivated of '" + activationM.Key + "' took " + activationM.Value.ToString("0.0") + " ms (limit 8)");
			}
		}
		Check(LoopGuard.Violations == 0, "no loop was started while already running (" + LoopGuard.Violations + ")");
		SetCursorPos(0, 0);
		soundPlayer?.Stop();
		Console.WriteLine((_fails == 0) ? "TAB-SWITCH-QA PASSED" : ("TAB-SWITCH-QA FAILED: " + _fails));
		if (_fails != 0)
		{
			return 1;
		}
		return 0;
	}
}
