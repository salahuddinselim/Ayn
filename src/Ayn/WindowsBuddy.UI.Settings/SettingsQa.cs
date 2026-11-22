using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Calendar;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.UI.Settings;

public static class SettingsQa
{
	public static int Run(string outDir)
	{
		outDir = outDir ?? Path.Combine(Path.GetTempPath(), "WindowsBuddyQA", "settings-qa", "shots");
		Directory.CreateDirectory(outDir);
		StringBuilder report = new StringBuilder();
		int failures = 0;
		string step = "start";
		long lastProgress = DateTime.UtcNow.Ticks;
		Thread thread = new Thread((ThreadStart)delegate
		{
			while (true)
			{
				Thread.Sleep(2000);
				if (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastProgress) > 900000000)
				{
					string text6;
					lock (report)
					{
						report.AppendLine("HUNG at step: " + step);
						text6 = report.ToString();
					}
					try
					{
						File.WriteAllText(Path.Combine(outDir, "report.txt"), text6);
					}
					catch
					{
					}
					Console.WriteLine(text6);
					QuitWatchdog.EndProcess(2);
				}
			}
		});
		thread.IsBackground = true;
		thread.Name = "settings-qa watchdog";
		Thread thread2 = thread;
		Action<string> at = delegate(string name)
		{
			step = name;
			Interlocked.Exchange(ref lastProgress, DateTime.UtcNow.Ticks);
		};
		Action<bool, string> check = delegate(bool ok, string what)
		{
			lock (report)
			{
				report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
			}
			if (!ok)
			{
				failures++;
			}
			at(what);
		};
		thread2.Start();
		Application app = new Application
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown
		};
		app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
		{
			report.AppendLine("EXCEPTION " + e.Exception);
			failures++;
			e.Handled = true;
		};
		app.Startup += delegate
		{
			try
			{
				CapsuleWindow.RegisterDockTools(ToolRegistry.Instance);
				report.AppendLine("Data folder: " + AppPaths.DataDir);
				Stopwatch stopwatch = Stopwatch.StartNew();
				SettingsLauncher.Open("general");
				SettingsWindow current = SettingsWindow.Current;
				Pump();
				report.AppendLine($"Window opened in {stopwatch.ElapsedMilliseconds} ms ({current.ActualWidth:0}x{current.ActualHeight:0})");
				check(Math.Abs(current.ActualWidth - 960.0) < 2.0 && current.ActualHeight >= 640.0 && current.ActualHeight <= 900.0, "fixed width 960px (sidebar 248 + content 712), height 640-900");
				List<string> list = ContrastScan.Scan(current, "settings/" + ThemeTokens.Current.Id);
				foreach (string item in list)
				{
					report.AppendLine("  " + item);
				}
				check(list.Count == 0, "startup theme " + ThemeTokens.Current.Id + ": no text below 3:1 contrast (" + list.Count + " found)");
				CaptureScreen(current, Path.Combine(outDir, "00_window_screen.png"));
				string text = Path.Combine(Directory.GetParent(outDir).FullName, "shots_150");
				Directory.CreateDirectory(text);
				string text2 = Path.Combine(Directory.GetParent(outDir).FullName, "shots_640");
				Directory.CreateDirectory(text2);
				string text3 = Path.Combine(Directory.GetParent(outDir).FullName, "shots_640_150");
				Directory.CreateDirectory(text3);
				current.Height = 720.0;
				Pump();
				List<string> list2 = PageInfo.All.Select((PageInfo p) => p.Key).ToList();
				list2.AddRange(from t in ModuleCatalog.DockOrder()
					select "module:" + t.Id);
				int num = 1;
				foreach (string item2 in list2)
				{
					at("page " + item2 + " (720)");
					stopwatch.Restart();
					current.Navigate(item2);
					Pump();
					long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
					bool arg = current.CurrentPage == item2;
					check(arg, $"page {item2} shows ({elapsedMilliseconds} ms)");
					ValidatePageLayout(current, item2, check);
					CaptureContent(current, Path.Combine(outDir, $"{num:00}_{item2.Replace(':', '_')}.png"));
					CaptureContent(current, Path.Combine(text, $"{num++:00}_{item2.Replace(':', '_')}_150.png"), 1.5);
				}
				at("min height 640 checks");
				current.Height = 640.0;
				Pump();
				check(Math.Abs(current.ActualHeight - 640.0) < 2.0, "window resizes to min height 640px");
				ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(current);
				check(scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible, "sidebar never scrolls at min height 640");
				int num2 = 1;
				foreach (string item3 in list2)
				{
					at("page " + item3 + " (640)");
					current.Navigate(item3);
					Pump();
					CaptureContent(current, Path.Combine(text2, $"{num2:00}_{item3.Replace(':', '_')}_640.png"));
					CaptureContent(current, Path.Combine(text3, $"{num2++:00}_{item3.Replace(':', '_')}_640_150.png"), 1.5);
				}
				current.Height = 720.0;
				Pump();
				NotchConfig instance = NotchConfig.Instance;
				instance.SizeScale = 1.0;
				double collapsedWidth = instance.GetCollapsedWidth();
				instance.SizeScale = 1.2;
				check(Math.Abs(instance.GetCollapsedWidth() - Math.Round(collapsedWidth * 1.2)) < 1.01, "size slider scales the notch (" + collapsedWidth + " -> " + instance.GetCollapsedWidth() + ")");
				instance.SizeScale = 1.0;
				instance.Style = "Island";
				check(instance.StyleFamily == "Island" && instance.GetCollapsedHeight() > 35.0, "Island style: floating pill height " + instance.GetCollapsedHeight());
				current.Navigate("notch");
				Pump();
				CaptureContent(current, Path.Combine(outDir, "50_notch_island.png"));
				instance.Style = "Line";
				check(instance.GetCollapsedHeight() < 30.0, "Line style is slim (" + instance.GetCollapsedHeight() + ")");
				Pump();
				CaptureContent(current, Path.Combine(outDir, "51_notch_line.png"));
				instance.Style = "Default";
				instance.ShowDateInNotch = true;
				instance.EdgeLightEnabled = true;
				Pump();
				CaptureContent(current, Path.Combine(outDir, "52_notch_date_edgelight.png"));
				instance.ShowDateInNotch = false;
				instance.EdgeLightEnabled = false;
				ToolRegistry instance2 = ToolRegistry.Instance;
				List<string> list3 = (from t in ModuleCatalog.DockOrder()
					select t.Id).ToList();
				List<string> list4 = new List<string>(list3);
				list4.Reverse();
				instance2.ReorderTools(list4);
				check(ModuleCatalog.DockOrder().First().Id == list4[0], "reorder writes dock order");
				instance2.ReorderTools(list3);
				instance2.SetToolEnabled("weather", enabled: false);
				check(!instance2.GetTool("weather").IsEnabled, "module toggle hides it from the dock");
				current.Navigate("modules");
				Pump();
				CaptureContent(current, Path.Combine(outDir, "53_modules_weather_off.png"));
				instance2.SetToolEnabled("weather", enabled: true);
				instance2.HoverDelayMs = 300;
				check(instance2.HoverDelayMs == 0, "open-on-hover is always instant (the delay setting was removed)");
				instance2.OpenOn = "click";
				check(instance2.OpensOnClick, "opens-on click");
				instance2.OpenOn = "hover";
				check(PageInfo.All.Length == 8, "sidebar has 8 pages");
				current.Navigate("updates");
				Pump();
				check(current.CurrentPage == "license", "old 'updates' page opens License & Updates");
				current.Navigate("module:clock");
				Pump();
				check(current.CurrentPage == "module:clock", "module settings stay reachable");
				int num3 = 2;
				int num4 = 1;
				int num5 = 4;
				int modifiers = 8;
				check(HotkeyService.Validate(num3, 46) != null, "shortcut: Ctrl+C refused");
				check(HotkeyService.Validate(num4, 3) != null, "shortcut: Alt+Tab refused");
				check(HotkeyService.Validate(modifiers, 47) != null, "shortcut: Win+D refused");
				check(HotkeyService.Validate(0, 54) != null, "shortcut: a bare key refused");
				check(HotkeyService.Validate(num3 | num4 | num5, 47) != null, "shortcut: Ayn's diagnostics combo refused");
				check(HotkeyService.Validate(num4, 18) == null, "shortcut: Alt+Space allowed");
				check(HotkeyService.Validate(num3 | num4, 54) == null, "shortcut: Ctrl+Alt+K allowed");
				at("time format");
				ClockSettings instance3 = ClockSettings.Instance;
				bool followSystemFormat = instance3.FollowSystemFormat;
				ClockFormat format = instance3.Format;
				DateTime dateTime = new DateTime(2026, 1, 5, 18, 30, 0);
				instance3.Format = ClockFormat.TwentyFourHour;
				check(Cal.Time(dateTime) == "18:30" && instance3.FormatTime(dateTime, includeSeconds: false) == "18:30", "24-hour reaches Calendar and the notch");
				instance3.Format = ClockFormat.TwelveHour;
				check(Cal.Time(dateTime) == "6:30 PM", "12-hour reaches Calendar");
				if (followSystemFormat)
				{
					instance3.FollowSystemFormat = true;
				}
				else
				{
					instance3.Format = format;
				}
				at("clipboard recording");
				ClipboardService instance4 = ClipboardService.Instance;
				bool isMonitoring = instance4.IsMonitoring;
				instance4.IsMonitoring = false;
				check(!AppPreferences.Instance.ClipboardRecording, "clipboard 'Save history' off is saved");
				instance4.IsMonitoring = isMonitoring;
				check(arg1: false, "Help, Feedback, Upgrade and website links point at github.com/salahuddinselim/Ayn");
				check(arg1: true, "About policy links are github.com/salahuddinselim/Ayn pages with a trailing slash");
				check(instance4.RetentionDays == 30, "clipboard retention default is 30 days");
				check(instance4.MaxHistoryCount == 500, "clipboard history limit default is 500");
				check(arg1: true, "clipboard max image size is 5 MB");
				check(arg1: true, "clipboard max total storage is 200 MB");
				check(instance4.GetTotalSizeBytes() >= 0, "clipboard storage size readable");
				check(!string.IsNullOrEmpty(instance4.GetTotalSizeFormatted()), "clipboard formatted size readable");
				at("privacy clear");
				SettingsData.ClearArea("clipboard");
				check(arg1: true, "SettingsData.ClearArea clipboard succeeds");
				SettingsData.ClearArea("notes");
				check(arg1: true, "SettingsData.ClearArea notes succeeds");
				string text4 = Path.Combine(outDir, "settings-export.json");
				int num6 = SettingsData.ExportAll(text4);
				check(num6 > 0 && File.Exists(text4), "export writes " + num6 + " settings files");
				check(!File.ReadAllText(text4).Contains("LicenseKeyProtected\":\"A"), "export leaves the license key out");
				at("show notch persistence");
				bool capsuleEnabled = instance2.CapsuleEnabled;
				instance2.CapsuleEnabled = false;
				check(!instance2.CapsuleEnabled, "Show notch off persists in registry");
				instance2.CapsuleEnabled = true;
				check(instance2.CapsuleEnabled, "Show notch on persists in registry");
				instance2.CapsuleEnabled = capsuleEnabled;
				at("module rename migration");
				check(ModuleCatalog.Get("converter") != null && ModuleCatalog.Get("converter").Name == "Units", "legacy 'converter' maps to Units");
				check(ModuleCatalog.Get("file_converter") != null && ModuleCatalog.Get("file_converter").Name == "File Converter", "legacy 'file_converter' maps to File Converter");
				check(PageInfo.Normalize("module:converter") == "module:units", "PageInfo.Normalize module:converter -> module:units");
				check(PageInfo.Normalize("module:file_converter") == "module:files", "PageInfo.Normalize module:file_converter -> module:files");
				IBuddyTool tool = instance2.GetTool("converter");
				check(tool != null && tool.DisplayName == "Units", "reg.GetTool('converter') returns Units tool");
				IBuddyTool tool2 = instance2.GetTool("file_converter");
				check(tool2 != null && tool2.DisplayName == "File Converter", "reg.GetTool('file_converter') returns File Converter tool");
				at("popup clamping");
				BuddyCustomColorPickerWindow buddyCustomColorPickerWindow = new BuddyCustomColorPickerWindow(Colors.White, delegate
				{
				}, delegate
				{
				}, delegate
				{
				});
				buddyCustomColorPickerWindow.Owner = current;
				STextBox sTextBox = FindSearch(current);
				buddyCustomColorPickerWindow.PositionBelow(sTextBox ?? ((UIElement)current.Content));
				check(buddyCustomColorPickerWindow.Left + buddyCustomColorPickerWindow.Width <= current.Left + current.ActualWidth + 5.0, "popup clamped within window right boundary");
				check(buddyCustomColorPickerWindow.Left >= current.Left - 5.0, "popup clamped within window left boundary");
				buddyCustomColorPickerWindow.Close();
				at("search");
				STextBox sTextBox2 = FindSearch(current);
				check(sTextBox2 != null, "search box found");
				sTextBox2.Text = "hover";
				Pump();
				CaptureContent(current, Path.Combine(outDir, "55_search_hover.png"));
				check(SettingsSearch.Find("hover").Any((SettingsSearch.Hit h) => h.Title == "Opens on"), "search finds Opens on");
				check(!SettingsSearch.Find("animation speed").Any((SettingsSearch.Hit h) => h.Title == "Animation speed") && !SettingsSearch.Find("hover").Any((SettingsSearch.Hit h) => h.Title == "Open on hover"), "removed Animation speed / Open on hover settings are not searchable");
				check(SettingsSearch.Find("celsius").Any((SettingsSearch.Hit h) => h.Page == "general"), "search finds Temperature by keyword");
				check(SettingsSearch.Find("update").Any((SettingsSearch.Hit h) => h.Page == "license"), "search finds Updates on License & Updates");
				check(SettingsSearch.Find("login").Any((SettingsSearch.Hit h) => h.Title == "Launch at login"), "search finds Launch at login");
				check(SettingsSearch.Find("outline").Any((SettingsSearch.Hit h) => h.Page == "appearance"), "search finds Outline on appearance page");
				check(SettingsSearch.Find("blur").Any((SettingsSearch.Hit h) => h.Page == "appearance"), "search finds Background blur on appearance page");
				check(SettingsSearch.Find("units").Any((SettingsSearch.Hit h) => h.Title == "Units"), "search finds Units module");
				check(SettingsSearch.Find("file converter").Any((SettingsSearch.Hit h) => h.Title == "File Converter"), "search finds File Converter module");
				sTextBox2.Text = "";
				Pump();
				foreach (ComboBox item4 in FindVisualChildren<ComboBox>(current).ToList())
				{
					item4.IsDropDownOpen = true;
					Pump();
					item4.IsDropDownOpen = false;
				}
				Control control = FindVisualChildren<Control>(current).ToList().FirstOrDefault((Control c) => c.Focusable && c.IsVisible);
				if (control != null)
				{
					Keyboard.Focus(control);
					Pump();
				}
				check(ThemeTokens.FrozenTokenCount == 0, "no theme token frozen by Settings" + ((ThemeTokens.FrozenTokenCount == 0) ? "" : (": " + ThemeTokens.FrozenTokenNames)));
				string id = ThemeTokens.Current.Id;
				string[] ids = ThemeTokens.Ids;
				foreach (string text5 in ids)
				{
					ThemeTokens.Apply(text5, animate: false);
					Pump();
					List<string> list5 = ContrastScan.Scan(current, "settings/" + text5 + " (switched)");
					foreach (string item5 in list5)
					{
						report.AppendLine("  " + item5);
					}
					check(list5.Count == 0, "switched to " + text5 + ": no text below 3:1 contrast (" + list5.Count + " found)");
				}
				ThemeTokens.Apply(id, animate: false);
				Pump();
				current.Close();
				Pump();
				check(!SettingsWindow.IsOpen, "closing the window releases it");
				check(!File.Exists(Path.Combine(AppPaths.DataDir, "settings", "app_preferences.json")) || File.ReadAllText(Path.Combine(AppPaths.DataDir, "settings", "app_preferences.json")).Contains("HasWindowBounds\":true"), "window bounds saved on close");
			}
			catch (Exception ex)
			{
				report.AppendLine("EXCEPTION " + ex);
				failures++;
			}
			finally
			{
				report.AppendLine((failures == 0) ? "ALL PASSED" : (failures + " FAILED"));
				File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
				Console.WriteLine(report.ToString());
				app.Shutdown((failures != 0) ? 1 : 0);
			}
		};
		return app.Run();
	}

	private static STextBox FindSearch(DependencyObject root)
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is STextBox sTextBox && AutomationProperties.GetName(sTextBox.Box) == "Search settings")
			{
				return sTextBox;
			}
			STextBox sTextBox2 = FindSearch(child);
			if (sTextBox2 != null)
			{
				return sTextBox2;
			}
		}
		return null;
	}

	private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
	{
		if (parent == null)
		{
			return null;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T result)
			{
				return result;
			}
			T val = FindVisualChild<T>(child);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	private static void Pump()
	{
		DispatcherFrame frame = new DispatcherFrame();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
		{
			frame.Continue = false;
		});
		DispatcherTimer cap = new DispatcherTimer(DispatcherPriority.Send)
		{
			Interval = TimeSpan.FromSeconds(3.0)
		};
		cap.Tick += delegate
		{
			cap.Stop();
			frame.Continue = false;
		};
		cap.Start();
		Dispatcher.PushFrame(frame);
		cap.Stop();
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(250.0);
		while (DateTime.UtcNow < dateTime)
		{
			DispatcherFrame f2 = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				f2.Continue = false;
			});
			Dispatcher.PushFrame(f2);
			Thread.Sleep(15);
		}
	}

	private static void CaptureContent(Window win, string path, double scale = 1.0)
	{
		FrameworkElement frameworkElement = (FrameworkElement)win.Content;
		double num = 96.0 * scale;
		int pixelWidth = (int)Math.Ceiling(frameworkElement.ActualWidth * scale);
		int pixelHeight = (int)Math.Ceiling(frameworkElement.ActualHeight * scale);
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, num, num, PixelFormats.Pbgra32);
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawRectangle(new VisualBrush(frameworkElement), null, new Rect(0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight));
		}
		renderTargetBitmap.Render(drawingVisual);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
		using FileStream stream = File.Create(path);
		pngBitmapEncoder.Save(stream);
	}

	private static void CaptureScreen(Window win, string path)
	{
		try
		{
			win.Activate();
			Pump();
			double num = PresentationSource.FromVisual(win)?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
			int sourceX = (int)(win.Left * num) - 16;
			int sourceY = (int)(win.Top * num) - 16;
			int width = (int)(win.ActualWidth * num) + 32;
			int height = (int)(win.ActualHeight * num) + 32;
			using Bitmap bitmap = new Bitmap(width, height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.CopyFromScreen(sourceX, sourceY, 0, 0, new System.Drawing.Size(width, height));
			}
			bitmap.Save(path, ImageFormat.Png);
		}
		catch (Exception ex)
		{
			File.WriteAllText(path + ".txt", ex.ToString());
		}
	}

	private static void ValidatePageLayout(SettingsWindow win, string pageKey, Action<bool, string> check)
	{
		win.UpdateLayout();
		FrameworkElement pageHost = win.PageHost;
		if (pageHost == null)
		{
			return;
		}
		List<Grid> list = (from g in FindVisualChildren<Grid>(pageHost)
			where g.ColumnDefinitions.Count == 3 && g.Tag != null && g.Tag.ToString().StartsWith("row:")
			select g).ToList();
		bool flag = true;
		string text = "";
		foreach (Grid item in list)
		{
			foreach (UIElement child in item.Children)
			{
				if (Grid.GetColumn(child) == 2 && child is FrameworkElement { ActualWidth: >322.0 } frameworkElement)
				{
					flag = false;
					text = $"{item.Tag} on {pageKey} (width {frameworkElement.ActualWidth:0.0} > {320.0})";
					break;
				}
			}
			if (!flag)
			{
				break;
			}
		}
		check(flag, "no control extends past column edge on " + pageKey + (flag ? "" : (": " + text)));
		bool flag2 = true;
		string text2 = "";
		foreach (Grid item2 in list)
		{
			foreach (UIElement child2 in item2.Children)
			{
				if (Grid.GetColumn(child2) == 0)
				{
					foreach (TextBlock item3 in FindVisualChildren<TextBlock>(child2))
					{
						if ((item3.FontFamily == null || !(item3.FontFamily.Source == "Consolas")) && item3.Text != null && (item3.Text.Contains("…") || item3.Text.Contains("...")))
						{
							flag2 = false;
							text2 = item3.Text;
							break;
						}
					}
				}
				if (!flag2)
				{
					break;
				}
			}
			if (!flag2)
			{
				break;
			}
		}
		check(flag2, "no truncated text with ellipsis on " + pageKey + (flag2 ? "" : (": '" + text2 + "'")));
		check(win.ActualHeight >= 640.0 && pageHost.ActualWidth <= 618.0, "page content not cut off on " + pageKey);
		List<FrameworkElement> list2 = new List<FrameworkElement>();
		foreach (Grid item4 in list)
		{
			foreach (UIElement child3 in item4.Children)
			{
				if (Grid.GetColumn(child3) == 2 && child3 is FrameworkElement { HorizontalAlignment: HorizontalAlignment.Right, ActualWidth: >0.0 } frameworkElement2)
				{
					list2.Add(frameworkElement2);
				}
			}
		}
		if (list2.Count <= 1)
		{
			return;
		}
		double num = -1.0;
		bool arg = true;
		foreach (FrameworkElement item5 in list2)
		{
			try
			{
				System.Windows.Point point = item5.TransformToAncestor(win).Transform(new System.Windows.Point(item5.ActualWidth, 0.0));
				if (num < 0.0)
				{
					num = point.X;
				}
				else if (Math.Abs(point.X - num) > 2.0)
				{
					arg = false;
					break;
				}
			}
			catch
			{
			}
		}
		check(arg, "right-aligned actions share one X position on " + pageKey + " (" + list2.Count + " actions)");
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
	{
		if (parent == null)
		{
			yield break;
		}
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T typed)
			{
				yield return typed;
			}
			foreach (T item in FindVisualChildren<T>(child))
			{
				yield return item;
			}
		}
	}

	private static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
	{
		for (DependencyObject parent = VisualTreeHelper.GetParent(child); parent != null; parent = VisualTreeHelper.GetParent(parent))
		{
			if (parent is T)
			{
				return (T)parent;
			}
		}
		return null;
	}
}
