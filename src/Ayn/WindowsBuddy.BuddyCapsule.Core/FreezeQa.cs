using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Message;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class FreezeQa
{
	private static void PumpDispatcher()
	{
		DispatcherFrame frame = new DispatcherFrame();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			frame.Continue = false;
		});
		Dispatcher.PushFrame(frame);
	}

	public static int Run(CapsuleWindow win)
	{
		Console.WriteLine("==================================================");
		Console.WriteLine("  FREEZE-QA: REPRODUCING NOTCH HANG SCENARIOS");
		Console.WriteLine("==================================================");
		ToolRegistry reg = ToolRegistry.Instance;
		reg.ForceEnableCapsule();
		List<string> list = ToolRegistry.CanonicalDefaultDockOrder.Where((string id) => reg.GetTool(id) != null).ToList();
		Console.WriteLine("Active dock tabs: " + string.Join(", ", list));
		int hangReportCount = UiHangWatchdog.HangReportCount;
		Console.WriteLine("\n[1/4] Running 100 rapid tab switches...");
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<double> list2 = new List<double>();
		for (int i = 0; i < 100; i++)
		{
			string toolId = list[i % list.Count];
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			ToolRouter.Instance.RouteTo(toolId);
			PumpDispatcher();
			stopwatch2.Stop();
			list2.Add(stopwatch2.Elapsed.TotalMilliseconds);
			if ((i + 1) % 25 == 0)
			{
				Console.WriteLine($"  Switches {i + 1}/100 complete (latest: {stopwatch2.Elapsed.TotalMilliseconds:F1} ms)");
			}
		}
		stopwatch.Stop();
		double num = list2.Average();
		double num2 = list2.Max();
		Console.WriteLine($"  100 switches finished in {stopwatch.Elapsed.TotalMilliseconds:F0} ms (avg: {num:F1} ms, max: {num2:F1} ms)");
		Console.WriteLine("\n[2/4] Flashing 20 messages back to back...");
		ToolRouter.Instance.RouteTo("message");
		PumpDispatcher();
		Stopwatch stopwatch3 = Stopwatch.StartNew();
		for (int j = 0; j < 20; j++)
		{
			MessageService.Instance.Settings.Text = "Freeze-QA Message " + (j + 1);
			MessageService.Instance.Flash();
			win.RequestCollapse("Message flashed");
			PumpDispatcher();
			Thread.Sleep(30);
		}
		stopwatch3.Stop();
		Console.WriteLine($"  20 flashes finished in {stopwatch3.Elapsed.TotalMilliseconds:F0} ms");
		Console.WriteLine("\n[3/4] Alternating between Clipboard and Home...");
		Stopwatch stopwatch4 = Stopwatch.StartNew();
		for (int k = 0; k < 10; k++)
		{
			ToolRouter.Instance.RouteTo("clipboard");
			PumpDispatcher();
			Thread.Sleep(50);
			ToolRouter.Instance.RouteTo("home");
			PumpDispatcher();
			Thread.Sleep(50);
		}
		stopwatch4.Stop();
		Console.WriteLine($"  10 Clipboard/Home cycles finished in {stopwatch4.Elapsed.TotalMilliseconds:F0} ms");
		Console.WriteLine("\n[4/4] Opening and closing notch repeatedly (30 cycles)...");
		Stopwatch stopwatch5 = Stopwatch.StartNew();
		for (int l = 0; l < 30; l++)
		{
			win.RequestExpand("Freeze-QA expand");
			PumpDispatcher();
			Thread.Sleep(40);
			win.RequestCollapse("Freeze-QA collapse");
			PumpDispatcher();
			Thread.Sleep(40);
		}
		stopwatch5.Stop();
		Console.WriteLine($"  30 Open/Close cycles finished in {stopwatch5.Elapsed.TotalMilliseconds:F0} ms");
		int num3 = UiHangWatchdog.HangReportCount - hangReportCount;
		Console.WriteLine("\n==================================================");
		Console.WriteLine($"  FREEZE-QA RESULT: {num3} HANGS DETECTED");
		Console.WriteLine("==================================================");
		string text = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "logs");
		if (Directory.Exists(text))
		{
			string[] files = Directory.GetFiles(text, "hang-*.txt");
			Console.WriteLine($"Total hang report files in {text}: {files.Length}");
			foreach (string item in files.OrderByDescending((string f) => File.GetLastWriteTime(f)).Take(3))
			{
				Console.WriteLine("  " + Path.GetFileName(item) + " (" + new FileInfo(item).Length + " bytes)");
			}
		}
		return num3;
	}
}
