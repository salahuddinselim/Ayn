using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class ThemeQa
{
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
				Thread.Sleep(15);
			}
		}
		while (DateTime.UtcNow < dateTime);
	}

	public static int Run(CapsuleWindow win, string[] args)
	{
		string text = ((args.Length > 1 && !args[1].StartsWith("--")) ? args[1] : Path.Combine(Path.GetTempPath(), "AynThemeQa"));
		Directory.CreateDirectory(text);
		ToolRegistry reg = ToolRegistry.Instance;
		reg.ForceEnableCapsule();
		List<string> list;
		if (args.Length > 2 && !args[2].StartsWith("--") && args[2] != "all")
		{
			list = (from t in args[2].Split(',')
				select t.Trim() into t
				where t.Length > 0
				select t).ToList();
		}
		else if (args.Length > 2 && args[2] == "all")
		{
			list = ToolRegistry.CanonicalDefaultDockOrder.Where((string id) => reg.GetTool(id) != null).ToList();
		}
		else
		{
			List<string> list2 = new List<string>();
			list2.Add("home");
			list = list2;
		}
		bool flag = !args.Contains("--no-switch");
		string id2 = ThemeTokens.Current.Id;
		int num = 0;
		if (args.Contains("--startup"))
		{
			return RunStartup(win, list, text);
		}
		Console.WriteLine("THEME-QA: " + list.Count + " tab(s) x " + ThemeTokens.Ids.Length + " themes -> " + text);
		if (ThemeTokens.FrozenTokenCount > 0)
		{
			Console.WriteLine("  frozen at startup: " + ThemeTokens.FrozenTokenNames);
		}
		Pump(600);
		string[] ids = ThemeTokens.Ids;
		foreach (string text2 in ids)
		{
			ThemeTokens.Apply(text2, animate: false);
			Pump(250);
			try
			{
				Capture(win, Path.Combine(text, "rest_" + text2 + ".png"));
			}
			catch (Exception ex)
			{
				Console.WriteLine("  rest capture failed: " + ex.Message);
			}
		}
		ThemeTokens.Apply(id2, animate: false);
		win.RequestExpand("Theme-QA");
		Pump(900);
		if (ThemeTokens.FrozenTokenCount > 0)
		{
			Console.WriteLine("  frozen after expand: " + ThemeTokens.FrozenTokenNames);
		}
		foreach (string item in list)
		{
			ToolRouter.Instance.RouteTo(item);
			Pump(700);
			if (ThemeTokens.FrozenTokenCount > 0)
			{
				Console.WriteLine("  frozen after opening " + item + ": " + ThemeTokens.FrozenTokenNames);
			}
			string[] ids2 = ThemeTokens.Ids;
			foreach (string text3 in ids2)
			{
				ThemeTokens.Apply(text3, animate: false);
				Pump(350);
				string path = Path.Combine(text, item + "_" + text3 + ".png");
				try
				{
					Capture(win, path);
					Console.WriteLine("  saved " + Path.GetFileName(path));
				}
				catch (Exception ex2)
				{
					num++;
					Console.WriteLine("  FAIL capture " + item + "/" + text3 + ": " + ex2.Message);
				}
				List<string> list3 = ContrastScan.Scan(win, item + "/" + text3 + " (switched)");
				foreach (string item2 in list3)
				{
					Console.WriteLine("  FAIL contrast " + item2);
				}
				num += list3.Count;
			}
		}
		SystemTool systemTool = reg.GetTool("system") as SystemTool;
		if (args.Contains("--system-states") && systemTool != null)
		{
			ToolRouter.Instance.RouteTo("system");
			Pump(500);
			string[] ids3 = ThemeTokens.Ids;
			foreach (string text4 in ids3)
			{
				ThemeTokens.Apply(text4, animate: false);
				string[] array = new string[5] { "history", "desktop", "ethernet", "offline", "noaudio" };
				foreach (string text5 in array)
				{
					systemTool.ShowQaState(text5);
					Pump(300);
					string path2 = Path.Combine(text, "system_" + text5 + "_" + text4 + ".png");
					try
					{
						Capture(win, path2);
						Console.WriteLine("  saved " + Path.GetFileName(path2));
					}
					catch (Exception ex3)
					{
						num++;
						Console.WriteLine("  FAIL capture " + text5 + "/" + text4 + ": " + ex3.Message);
					}
					List<string> list4 = ContrastScan.Scan(win, "system/" + text5 + "/" + text4);
					foreach (string item3 in list4)
					{
						Console.WriteLine("  FAIL contrast " + item3);
					}
					num += list4.Count;
				}
			}
			systemTool.RefreshQa();
			ThemeTokens.Apply(id2, animate: false);
		}
		if (list.Contains("home"))
		{
			ToolRouter.Instance.RouteTo("home");
			ThemeTokens.Apply("Silver", animate: false);
			Pump(300);
			double[] array2 = new double[3] { 0.15, 0.4, 0.7 };
			foreach (double num2 in array2)
			{
				win.UpdateMorphState(num2);
				Pump(120);
				try
				{
					Capture(win, Path.Combine(text, $"morph_Silver_{num2 * 100.0:00}.png"));
				}
				catch (Exception ex4)
				{
					Console.WriteLine("  morph capture failed: " + ex4.Message);
				}
			}
			win.UpdateMorphState(1.0);
			Pump(200);
		}
		if (flag)
		{
			ToolRouter.Instance.RouteTo("home");
			Pump(500);
			int hangReportCount = UiHangWatchdog.HangReportCount;
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			long totalMemory = GC.GetTotalMemory(forceFullCollection: true);
			int listenerCount = ThemeTokens.ListenerCount;
			Stopwatch stopwatch = Stopwatch.StartNew();
			double num3 = 0.0;
			for (int n = 0; n < 20; n++)
			{
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				ThemeTokens.Apply(ThemeTokens.Ids[n % ThemeTokens.Ids.Length], animate: true);
				Pump(0);
				num3 = Math.Max(num3, stopwatch2.Elapsed.TotalMilliseconds);
				Pump(180);
			}
			ThemeTokens.Apply(id2, animate: false);
			Pump(300);
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			long totalMemory2 = GC.GetTotalMemory(forceFullCollection: true);
			int listenerCount2 = ThemeTokens.ListenerCount;
			int num4 = UiHangWatchdog.HangReportCount - hangReportCount;
			double num5 = (double)(totalMemory2 - totalMemory) / 1024.0;
			Console.WriteLine($"  20 switches in {stopwatch.Elapsed.TotalMilliseconds:F0} ms, worst apply {num3:F1} ms, managed heap {num5:+0;-0} KB, listeners {listenerCount} -> {listenerCount2}, hangs {num4}, frozen tokens {ThemeTokens.FrozenTokenCount}");
			if (num4 > 0)
			{
				num++;
				Console.WriteLine("  FAIL hang during theme switching");
			}
			if (num5 > 2048.0)
			{
				num++;
				Console.WriteLine("  FAIL managed heap grew > 2 MB");
			}
			if (listenerCount2 > listenerCount)
			{
				num++;
				Console.WriteLine("  FAIL theme listeners leaked");
			}
			if (num3 > 250.0)
			{
				num++;
				Console.WriteLine("  FAIL a theme switch blocked the UI > 250 ms");
			}
		}
		else
		{
			ThemeTokens.Apply(id2, animate: false);
		}
		if (ThemeTokens.FrozenTokenCount > 0)
		{
			num++;
			Console.WriteLine("  FAIL frozen tokens: " + ThemeTokens.FrozenTokenNames);
		}
		win.RequestCollapse("Theme-QA done");
		Pump(300);
		Console.WriteLine((num == 0) ? "THEME-QA PASSED" : ("THEME-QA FAILED: " + num));
		return num;
	}

	private static int RunStartup(CapsuleWindow win, List<string> tabs, string outDir)
	{
		string id = ThemeTokens.Current.Id;
		int num = 0;
		Console.WriteLine("THEME-QA STARTUP in " + id + ": " + tabs.Count + " tab(s) -> " + outDir);
		win.RequestExpand("Theme-QA startup");
		Pump(900);
		foreach (string tab in tabs)
		{
			ToolRouter.Instance.RouteTo(tab);
			Pump(700);
			try
			{
				Capture(win, Path.Combine(outDir, "startup_" + tab + "_" + id + ".png"));
			}
			catch (Exception ex)
			{
				Console.WriteLine("  capture failed: " + ex.Message);
			}
			List<string> list = ContrastScan.Scan(win, tab + "/" + id + " (startup)");
			foreach (string item in list)
			{
				Console.WriteLine("  FAIL contrast " + item);
			}
			num += list.Count;
		}
		if (ThemeTokens.FrozenTokenCount > 0)
		{
			num++;
			Console.WriteLine("  FAIL frozen tokens: " + ThemeTokens.FrozenTokenNames);
		}
		win.RequestCollapse("Theme-QA done");
		Pump(300);
		Console.WriteLine((num == 0) ? ("THEME-QA STARTUP PASSED (" + id + ")") : ("THEME-QA STARTUP FAILED (" + id + "): " + num));
		return num;
	}

	private static void Capture(Window win, string path)
	{
		FrameworkElement frameworkElement = (FrameworkElement)win.Content;
		PresentationSource presentationSource = PresentationSource.FromVisual(win);
		double num = ((presentationSource != null && presentationSource.CompositionTarget != null) ? presentationSource.CompositionTarget.TransformToDevice.M11 : 1.0);
		Rect rect = VisualTreeHelper.GetDescendantBounds(frameworkElement);
		if (rect.IsEmpty || rect.Width < 1.0)
		{
			rect = new Rect(0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight);
		}
		if (Environment.GetEnvironmentVariable("VAYLI_THEMEQA_DEBUG") == "1")
		{
			Console.WriteLine($"  capture root {frameworkElement.ActualWidth:F0}x{frameworkElement.ActualHeight:F0} bounds {rect} scale {num} win {win.ActualWidth:F0}x{win.ActualHeight:F0}");
		}
		int num2 = (int)Math.Ceiling(win.ActualWidth * num);
		int num3 = (int)Math.Ceiling(win.ActualHeight * num);
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(num2, num3, 96.0 * num, 96.0 * num, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(win);
		int num4 = num2 * 4;
		byte[] array = new byte[num4 * num3];
		renderTargetBitmap.CopyPixels(array, num4, 0);
		int num5 = num2;
		int num6 = num3;
		int num7 = -1;
		int num8 = -1;
		for (int i = 0; i < num3; i++)
		{
			for (int j = 0; j < num2; j++)
			{
				if (array[i * num4 + j * 4 + 3] > 200)
				{
					if (j < num5)
					{
						num5 = j;
					}
					if (j > num7)
					{
						num7 = j;
					}
					if (i < num6)
					{
						num6 = i;
					}
					if (i > num8)
					{
						num8 = i;
					}
				}
			}
		}
		BitmapSource source = renderTargetBitmap;
		if (num7 > num5 && num8 > num6)
		{
			source = new CroppedBitmap(renderTargetBitmap, new Int32Rect(num5, num6, num7 - num5 + 1, num8 - num6 + 1));
		}
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(source));
		using FileStream stream = File.Create(path);
		pngBitmapEncoder.Save(stream);
	}
}
