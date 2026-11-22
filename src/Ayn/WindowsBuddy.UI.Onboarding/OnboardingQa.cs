using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.State;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

public static class OnboardingQa
{
	public static int Run(string outDir)
	{
		outDir = outDir ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowsBuddyQA", "onboarding-qa", "shots");
		Directory.CreateDirectory(outDir);
		StringBuilder report = new StringBuilder();
		int failures = 0;
		Action<bool, string> check = delegate(bool ok, string what)
		{
			report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
			if (!ok)
			{
				failures++;
			}
		};
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
			CapsuleWindow capsuleWindow = null;
			try
			{
				CapsuleWindow.RegisterDockTools(ToolRegistry.Instance);
				report.AppendLine("Data folder: " + AppPaths.DataDir);
				EulaQa.ResetSandboxAyn(report);
				NotchConfig instance = NotchConfig.Instance;
				string path = System.IO.Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "firstrun.done");
				check(!File.Exists(path), "fresh profile: no firstrun.done");
				check(instance.EdgeLightEnabled && instance.EdgeLightStyle == "Twin comets" && instance.AppTheme == "PureBlack", "fresh defaults: Pure Black, Edge Light on, Twin comets");
				ThemeTokens.Initialize();
				EulaQa.Static(check, report);
				EulaQa.Screens(check, report, System.IO.Path.Combine(outDir, "eula"));
				int notchStarts = 0;
				int declinedTour = 0;
				Func<CapsuleWindow> startNotch = delegate
				{
					notchStarts++;
					ToolRegistry.Instance.ForceEnableCapsule();
					FirstRunManager.MarkStarted();
					CapsuleWindow capsuleWindow2 = new CapsuleWindow();
					capsuleWindow2.Show();
					return capsuleWindow2;
				};
				int handedOver = 0;
				OnboardingWindow onboardingWindow = new OnboardingWindow(delegate
				{
					handedOver++;
				}, startNotch, delegate
				{
					declinedTour++;
				});
				onboardingWindow.Show();
				onboardingWindow.Activate();
				Pump(600);
				check(onboardingWindow.IsAgreementShowing && notchStarts == 0 && !LicenseStub.AynStarted, "first run: the agreement is on screen; no notch and no trial yet");
				SCheckBox agreementBox = onboardingWindow.AgreementBox;
				if (agreementBox != null)
				{
					agreementBox.Focus();
					agreementBox.Toggle();
				}
				Pump(200);
				Press(onboardingWindow, Key.Return);
				Pump(200);
				capsuleWindow = onboardingWindow.Notch;
				check(!onboardingWindow.IsAgreementShowing && onboardingWindow.Step == 0 && notchStarts == 1 && capsuleWindow != null && declinedTour == 0, "ticked + Enter: accepted, the notch starts once and the tour moves to Welcome (step " + onboardingWindow.Step + ")");
				check(FindButton(onboardingWindow, "Decline and exit") == null, "after Accept: Decline is gone from the bar");
				Pump(150);
				LetterReveal letterReveal = Find<LetterReveal>(onboardingWindow);
				double num = letterReveal?.Clock ?? (-2.0);
				Capture(onboardingWindow, System.IO.Path.Combine(outDir, "00_intro_start.png"));
				Pump(450);
				double num2 = letterReveal?.Clock ?? (-2.0);
				Capture(onboardingWindow, System.IO.Path.Combine(outDir, "00_intro_mid.png"));
				bool flag = CapsuleMotion.AnimationsEnabled && !PowerSaver.IsBatterySaverOn && !RenderProfile.LightMode;
				check(letterReveal != null && letterReveal.Text == "Welcome to Ayn" && letterReveal.FamilyName.StartsWith("Geist"), "Welcome: 'Welcome to Ayn' is the letter-by-letter headline, in Geist (" + ((letterReveal != null) ? letterReveal.FamilyName : "-") + ")");
				check(!flag || (num < num2 && num2 > 0.0 && num2 < letterReveal.DurationMs), $"Welcome: the letters are mid-reveal after 600 ms (clock {num:0} → {num2:0} of {letterReveal?.DurationMs ?? 0.0:0} ms)");
				check(!PageText(onboardingWindow).Contains("Hello"), "Welcome: no 'Hello' screen any more");
				Pump(1100);
				Capture(onboardingWindow, System.IO.Path.Combine(outDir, "00_intro_end.png"));
				check(Math.Abs(onboardingWindow.ActualWidth - 760.0) < 0.5 && Math.Abs(onboardingWindow.ActualHeight - 540.0) < 0.5, $"window is 760 x 540 ({onboardingWindow.ActualWidth} x {onboardingWindow.ActualHeight})");
				check(onboardingWindow.WindowStyle == WindowStyle.None && onboardingWindow.AllowsTransparency, "no title bar, rounded shell");
				check(LicenseStub.AynStarted && InAynWindow(), "the trial runs from the first launch: " + LicenseStub.StatusLine);
				CaptureScreen(onboardingWindow, System.IO.Path.Combine(outDir, "00_screen.png"));
				string text = PageText(onboardingWindow);
				check(letterReveal != null && letterReveal.Clock == double.MaxValue, "Welcome: the reveal has settled (crisp) by 1.7 s");
				check(text.Contains("The notch, reimagined for Windows.") && FindButtonText(onboardingWindow) == "Get started" && FindButton(onboardingWindow, "Skip") != null && FindButton(onboardingWindow, "Skip").IsVisible, "Welcome: the line, then 'Get started' and a quiet 'Skip' (" + text.Replace("\n", "|") + ")");
				foreach (TrackedText item in FindAll<TrackedText>(onboardingWindow))
				{
					check(item.FamilyName.StartsWith("Geist"), "headline '" + item.Text + "' is set in Geist (" + item.FamilyName + ")");
				}
				Pump(900);
				LiveNotchTile liveNotchTile = Find<LiveNotchTile>(onboardingWindow);
				System.Windows.Shapes.Rectangle rectangle = ((liveNotchTile != null) ? Find<System.Windows.Shapes.Rectangle>(liveNotchTile) : null);
				check(liveNotchTile != null && liveNotchTile.Height >= 130.0, "Welcome: tile is tall enough (" + ((liveNotchTile != null) ? liveNotchTile.Height.ToString("0") : "-") + " DIP)");
				check(liveNotchTile != null && rectangle != null && rectangle.Fill is VisualBrush && rectangle.Width > 100.0, "Welcome: the tile shows a live picture of the real notch card (" + ((rectangle != null) ? rectangle.Width.ToString("0") : "-") + " DIP wide)");
				string text2 = "";
				for (int i = 0; i < 90; i++)
				{
					if (text2.Contains("media"))
					{
						break;
					}
					Pump(100);
					PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
					if (currentActivity != null && currentActivity.Type == ActivityType.Media)
					{
						text2 += "media";
					}
				}
				check(text2.Contains("media"), "Welcome: the demo reached music through the real activity pipeline");
				Pump(700);
				check(liveNotchTile != null && liveNotchTile.PictureBottom + 16.0 <= liveNotchTile.Height, "Welcome: the whole music notch fits the tile with 16 px to spare (picture ends at " + ((liveNotchTile != null) ? liveNotchTile.PictureBottom.ToString("0") : "-") + " of " + ((liveNotchTile != null) ? liveNotchTile.Height.ToString("0") : "-") + ")");
				CaptureScreen(onboardingWindow, System.IO.Path.Combine(outDir, "00_screen_demo.png"));
				onboardingWindow.Go(1);
				Pump(600);
				string text3 = PageText(onboardingWindow);
				check(text3.Contains("Open the notch") && (text3.Contains("Move your pointer to the top of your screen") || text3.Contains("Click the notch at the top of your screen")), "Try it: title and the hover / click line");
				check(onboardingWindow.Step == 1, "Try it: waits until the notch is opened");
				Pump(1500);
				check(onboardingWindow.Step == 1, "Try it: still waiting after 1.5 s without opening it");
				capsuleWindow.RequestExpand("QA");
				Pump(700);
				check(PageText(onboardingWindow).Contains("That's it"), "Try it: opening the real notch shows the green check");
				Pump(1700);
				check(onboardingWindow.Step == 2, "Try it: auto-advances 1 s after the check");
				capsuleWindow.RequestCollapse("QA");
				Pump(800);
				onboardingWindow.Go(1);
				Pump(400);
				onboardingWindow.Go(2);
				Pump(500);
				string text4 = PageText(onboardingWindow);
				check(text4.Contains("Pick a look") && text4.Contains("Pure Black") && text4.Contains("Vivid") && text4.Contains("Ember Dark") && text4.Contains("Silver"), "Look: four theme cards");
				check(text4.Contains("Edge Light") && text4.Contains("Twin comets") && text4.Contains("Comet") && text4.Contains("Breathe"), "Look: Edge Light toggle and its three styles");
				check(instance.EdgeLightEnabled && instance.EdgeLightStyle == "Twin comets", "Look: Edge Light on with Twin comets by default");
				string[] array = new string[4] { "Vivid", "EmberDark", "Silver", "PureBlack" };
				foreach (string text5 in array)
				{
					string text6 = text5 switch
					{
						"Silver" => "Silver", 
						"EmberDark" => "Ember Dark", 
						"Vivid" => "Vivid", 
						_ => "Pure Black", 
					};
					SClickable sClickable = FindClickable(onboardingWindow, "Theme: " + text6);
					check(sClickable != null, "Look: card '" + text6 + "' found");
					if (sClickable != null)
					{
						sClickable.RaiseClick();
						Pump(500);
						instance.Flush();
						check(instance.AppTheme == text5, "picking " + text6 + " applies the theme live (" + instance.AppTheme + ")");
						check(ThemeTokens.Current.Id == text5, "  the whole app re-themed (" + ThemeTokens.Current.Id + ")");
						string text7 = (File.Exists(instance.FilePath) ? File.ReadAllText(instance.FilePath) : "");
						check(text7.Contains("\"" + text5 + "\""), "  saved immediately to notch_config.json");
						Capture(onboardingWindow, System.IO.Path.Combine(outDir, "pick_" + text5 + ".png"));
					}
				}
				SToggle sToggle = Find<SToggle>(onboardingWindow);
				SSegmented sSegmented = Find<SSegmented>(onboardingWindow);
				check(sToggle != null && sSegmented != null, "Look: Edge Light toggle and style control present");
				if (sToggle != null && sSegmented != null)
				{
					sSegmented.Select("Breathe", raise: true);
					Pump(200);
					instance.Flush();
					check(instance.EdgeLightStyle == "Breathe", "style chip Breathe applies (" + instance.EdgeLightStyle + ")");
					sSegmented.Select("Comet", raise: true);
					Pump(200);
					check(instance.EdgeLightStyle == "Comet", "style chip Comet applies");
					sSegmented.Select("Twin comets", raise: true);
					Pump(200);
					sToggle.Flip();
					Pump(200);
					check(!instance.EdgeLightEnabled, "toggle off turns Edge Light off on the real notch");
					sToggle.Flip();
					Pump(200);
					check(instance.EdgeLightEnabled && instance.EdgeLightStyle == "Twin comets", "toggle on again: Twin comets");
				}
				SButton sButton = FindButton(onboardingWindow, "Preview on notch");
				check(sButton?.IsEnabled ?? false, "Look: 'Preview on notch' button");
				if (sButton != null)
				{
					sButton.RaiseClick();
					Pump(900);
					check(capsuleWindow.NotchPhase == NotchPhase.Open || capsuleWindow.NotchPhase == NotchPhase.Opening, string.Concat("Preview on notch opens the real panel (", capsuleWindow.NotchPhase, ")"));
					Pump(3200);
					check(capsuleWindow.NotchPhase == NotchPhase.Resting || capsuleWindow.NotchPhase == NotchPhase.Closing || capsuleWindow.NotchPhase == NotchPhase.Peeking, string.Concat("  and closes again after about 3 s (", capsuleWindow.NotchPhase, ")"));
				}
				onboardingWindow.Go(3);
				Pump(500);
				string text8 = PageText(onboardingWindow);
				check(text8.Contains("You're all set") && text8.Contains("Your 3-day Ayn has started") && text8.Contains("Launch at login") && text8.Contains("Everything stays on this PC") && text8.Contains(string.Format("Ayn · {0} day{1} left", LicenseStub.AynDaysLeft, (LicenseStub.AynDaysLeft == 1) ? "" : "s")), "Ready: title, three rows, trial status (" + text8.Replace("\n", " | ") + ")");
				check(Find<SToggle>(onboardingWindow) != null && !Find<SToggle>(onboardingWindow).IsOn, "Ready: launch at login defaults off until chosen");
				check(FindButtonText(onboardingWindow) == "Start using Ayn", "Ready: primary button reads 'Start using Ayn'");
				array = ThemeTokens.Ids;
				for (int j = 0; j < array.Length; j++)
				{
					string text10 = (instance.AppTheme = array[j]);
					Pump(500);
					for (int k = 0; k < 4; k++)
					{
						onboardingWindow.Go(k);
						Pump();
						onboardingWindow.SettleIntro();
						Pump(500);
						string text11 = text10 + " step " + (k + 1);
						Capture(onboardingWindow, System.IO.Path.Combine(outDir, $"step{k + 1}_{text10}.png"));
						List<string> list = ContrastScan.Scan(onboardingWindow, "onboarding/" + text11);
						foreach (string item2 in list)
						{
							report.AppendLine("  " + item2);
						}
						check(list.Count == 0, text11 + ": no text below 3:1 contrast (" + list.Count + " found)");
						string text12 = Clipped(onboardingWindow);
						check(text12 == null, text11 + ": nothing clipped or outside the window" + ((text12 != null) ? (" (" + text12 + ")") : ""));
					}
				}
				instance.AppTheme = "PureBlack";
				Pump(400);
				double[] array2 = new double[4];
				double[] array3 = new double[4];
				for (int l = 1; l < 4; l++)
				{
					onboardingWindow.Go(l);
					Pump(600);
					FrameworkElement frameworkElement = (FrameworkElement)onboardingWindow.Content;
					TrackedText trackedText = Find<TrackedText>(onboardingWindow.PageView);
					array2[l] = trackedText.TransformToAncestor(frameworkElement).Transform(new System.Windows.Point(0.0, 0.0)).Y;
					Grid grid = onboardingWindow.PageView as Grid;
					FrameworkElement frameworkElement2 = grid.Children.OfType<FrameworkElement>().First((FrameworkElement c) => Grid.GetRow(c) == 1);
					array3[l] = frameworkElement2.TransformToAncestor(frameworkElement).Transform(new System.Windows.Point(0.0, 0.0)).Y;
					Rect rect = frameworkElement2.TransformToAncestor(frameworkElement).TransformBounds(new Rect(0.0, 0.0, frameworkElement2.ActualWidth, frameworkElement2.ActualHeight));
					check(rect.Bottom <= frameworkElement.ActualHeight - 72.0 + 0.5, "step " + (l + 1) + ": content ends at " + rect.Bottom.ToString("0") + ", above the bottom bar (" + (frameworkElement.ActualHeight - 72.0).ToString("0") + ")");
					check(array2[l] >= 36.0, "step " + (l + 1) + ": title sits " + array2[l].ToString("0") + " DIP from the top (not touching the edge)");
				}
				check(Math.Abs(array2[1] - array2[2]) < 0.6 && Math.Abs(array2[2] - array2[3]) < 0.6, "titles at the same position on steps 2, 3 and 4 (" + array2[1].ToString("0") + ", " + array2[2].ToString("0") + ", " + array2[3].ToString("0") + ")");
				check(Math.Abs(array3[1] - array3[2]) < 0.6 && Math.Abs(array3[2] - array3[3]) < 0.6, "content starts at the same position on steps 2, 3 and 4 (" + array3[1].ToString("0") + ", " + array3[2].ToString("0") + ", " + array3[3].ToString("0") + ")");
				OnboardingWindow onboardingWindow2 = new OnboardingWindow(delegate
				{
				}, capsuleWindow, replay: true);
				onboardingWindow2.Show();
				Pump(500);
				onboardingWindow2.SettleIntro();
				Pump();
				Press(onboardingWindow2, Key.Return);
				Pump(500);
				check(onboardingWindow2.Step == 1, "Enter on step 1 continues to step 2");
				Press(onboardingWindow2, Key.Right);
				Pump(500);
				check(onboardingWindow2.Step == 2, "Right arrow goes to step 3");
				Press(onboardingWindow2, Key.Return);
				Pump(500);
				check(onboardingWindow2.Step == 3, "Enter goes to step 4");
				Press(onboardingWindow2, Key.Right);
				Pump();
				check(onboardingWindow2.Step == 3, "Right arrow on the last step stays put");
				Press(onboardingWindow2, Key.Left);
				Pump(500);
				check(onboardingWindow2.Step == 2, "Left arrow goes back to step 3");
				Press(onboardingWindow2, Key.Left);
				Pump(500);
				Press(onboardingWindow2, Key.Left);
				Pump(500);
				check(onboardingWindow2.Step == 0, "Left arrow twice more reaches step 1");
				Press(onboardingWindow2, Key.Left);
				Pump();
				check(onboardingWindow2.Step == 0, "Left arrow on the first step stays put");
				for (int m = 1; m < 4; m++)
				{
					onboardingWindow2.Go(m);
					Pump(500);
					SButton sButton2 = FindButton(onboardingWindow2, "Back");
					check(sButton2?.IsVisible ?? false, "step " + (m + 1) + ": Back button is there");
					sButton2?.RaiseClick();
					Pump(500);
					check(onboardingWindow2.Step == m - 1, "Back on step " + (m + 1) + " returns to step " + m);
				}
				onboardingWindow2.Go(0);
				Pump();
				check(FindButton(onboardingWindow2, "Back") == null || !FindButton(onboardingWindow2, "Back").IsVisible, "no Back button on step 1");
				DispatcherTimer dlgTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(250.0)
				};
				string dialogSeen = null;
				dlgTimer.Tick += delegate
				{
					foreach (Window window2 in Application.Current.Windows)
					{
						if (window2.Title == "Skip setup?")
						{
							dialogSeen = window2.Title;
							SButton sButton4 = FindButton(window2, "Cancel");
							if (sButton4 != null)
							{
								dlgTimer.Stop();
								sButton4.RaiseClick();
							}
						}
					}
				};
				dlgTimer.Start();
				Press(onboardingWindow2, Key.Escape);
				dlgTimer.Stop();
				check(dialogSeen == "Skip setup?", "Esc opens the 'Skip setup?' dialog");
				check(onboardingWindow2.IsVisible, "Cancel keeps the tour open");
				dialogSeen = null;
				DispatcherTimer dlgTimer2 = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(250.0)
				};
				dlgTimer2.Tick += delegate
				{
					foreach (Window window3 in Application.Current.Windows)
					{
						if (window3.Title == "Skip setup?")
						{
							dialogSeen = window3.Title;
							SButton sButton3 = FindButton(window3, "Skip");
							if (sButton3 != null)
							{
								dlgTimer2.Stop();
								sButton3.RaiseClick();
							}
						}
					}
				};
				dlgTimer2.Start();
				Press(onboardingWindow2, Key.Escape);
				dlgTimer2.Stop();
				Pump(700);
				check(dialogSeen == "Skip setup?" && !onboardingWindow2.IsVisible, "Esc, then Skip closes the tour");
				onboardingWindow.Go(3);
				Pump(500);
				onboardingWindow.Finish();
				Pump(800);
				check(handedOver == 1, "finishing hands over once");
				check(LicenseStub.AynStarted && LicenseStub.Status == LicenseStatus.Ayn && InAynWindow(), "Start using Ayn: " + LicenseStub.StatusLine);
				check(File.Exists(path) && FirstRunManager.CurrentState == FirstRunState.Completed, "first run is recorded as done (never shows again)");
				check(!onboardingWindow.IsVisible, "the tour closes");
				Pump(1200);
				check(capsuleWindow.NotchPhase == NotchPhase.Opening || capsuleWindow.NotchPhase == NotchPhase.Open, string.Concat("the notch opens once as a welcome (", capsuleWindow.NotchPhase, ")"));
				Pump(4000);
				int trialDaysLeft = LicenseStub.AynDaysLeft;
				OnboardingWindow onboardingWindow3 = new OnboardingWindow(delegate
				{
					handedOver++;
				}, capsuleWindow, replay: true);
				onboardingWindow3.Show();
				Pump(700);
				check(onboardingWindow3.IsReplay && onboardingWindow3.Step == 0 && !onboardingWindow3.IsAgreementShowing, "replay starts at step 1 (no agreement screen)");
				onboardingWindow3.Go(3);
				Pump(400);
				onboardingWindow3.Finish();
				Pump(600);
				check(handedOver == 1 && LicenseStub.AynDaysLeft == trialDaysLeft && File.Exists(path), "replay changes nothing (no hand-over, license and first-run untouched)");
				OnboardingWindow onboardingWindow4 = new OnboardingWindow(null, capsuleWindow);
				onboardingWindow4.Show();
				Pump(500);
				onboardingWindow4.Finish();
				Pump(600);
				check(!onboardingWindow4.IsVisible && instance.EdgeLightEnabled && instance.AppTheme == "PureBlack", "skip: closes with the defaults in place");
				EulaQa.AfterAccept(check);
			}
			catch (Exception ex)
			{
				report.AppendLine("EXCEPTION " + ex);
				failures++;
			}
			finally
			{
				report.AppendLine((failures == 0) ? "ALL PASSED" : (failures + " FAILED"));
				File.WriteAllText(System.IO.Path.Combine(outDir, "report.txt"), report.ToString());
				Console.WriteLine(report.ToString());
				try
				{
					capsuleWindow?.Close();
				}
				catch
				{
				}
				app.Shutdown((failures != 0) ? 1 : 0);
			}
		};
		return app.Run();
	}

	internal static T Find<T>(DependencyObject root) where T : DependencyObject
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is T result)
			{
				return result;
			}
			T val = Find<T>(child);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	internal static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject
	{
		List<T> list = new List<T>();
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is T item)
			{
				list.Add(item);
			}
			list.AddRange(FindAll<T>(child));
		}
		return list;
	}

	internal static void Press(Window w, Key key)
	{
		PresentationSource inputSource = PresentationSource.FromVisual(w);
		KeyEventArgs keyEventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key);
		keyEventArgs.RoutedEvent = Keyboard.PreviewKeyDownEvent;
		KeyEventArgs e = keyEventArgs;
		w.RaiseEvent(e);
	}

	private static SClickable FindClickable(DependencyObject root, string name)
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is SClickable sClickable && AutomationProperties.GetName(sClickable) == name)
			{
				return sClickable;
			}
			SClickable sClickable2 = FindClickable(child, name);
			if (sClickable2 != null)
			{
				return sClickable2;
			}
		}
		return null;
	}

	internal static SButton FindButton(DependencyObject root, string text)
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is SButton sButton && sButton.Text == text && sButton.IsVisible)
			{
				return sButton;
			}
			SButton sButton2 = FindButton(child, text);
			if (sButton2 != null)
			{
				return sButton2;
			}
		}
		return null;
	}

	private static string FindButtonText(DependencyObject root)
	{
		string[] array = new string[3] { "Start using Ayn", "Continue", "Get started" };
		foreach (string text in array)
		{
			if (FindButton(root, text) != null)
			{
				return text;
			}
		}
		return "";
	}

	internal static string PageText(DependencyObject root)
	{
		StringBuilder stringBuilder = new StringBuilder();
		Collect(root, stringBuilder);
		return stringBuilder.ToString();
	}

	private static void Collect(DependencyObject d, StringBuilder sb)
	{
		if (!(d is UIElement uIElement) || (uIElement.IsVisible && !(uIElement.Opacity <= 0.01)))
		{
			if (d is TextBlock textBlock)
			{
				sb.Append(textBlock.Text).Append('\n');
			}
			TrackedText trackedText = d as TrackedText;
			if (d is LetterReveal letterReveal)
			{
				sb.Append(letterReveal.Text).Append('\n');
			}
			if (trackedText != null)
			{
				sb.Append(trackedText.Text).Append('\n');
			}
			int childrenCount = VisualTreeHelper.GetChildrenCount(d);
			for (int i = 0; i < childrenCount; i++)
			{
				Collect(VisualTreeHelper.GetChild(d, i), sb);
			}
		}
	}

	internal static string Clipped(Window win)
	{
		FrameworkElement root = (FrameworkElement)win.Content;
		string found = null;
		Action<DependencyObject, bool> walk = null;
		walk = delegate(DependencyObject d, bool scrolled)
		{
			if (found == null)
			{
				FrameworkElement frameworkElement = d as FrameworkElement;
				if (frameworkElement == null || (frameworkElement.IsVisible && !(frameworkElement.Opacity <= 0.01)))
				{
					if (d is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
					{
						double num = textBlock.DesiredSize.Width - textBlock.Margin.Left - textBlock.Margin.Right;
						double num2 = textBlock.DesiredSize.Height - textBlock.Margin.Top - textBlock.Margin.Bottom;
						if (num > textBlock.ActualWidth + 1.0 || num2 > textBlock.ActualHeight + 1.0)
						{
							found = "'" + textBlock.Text + "' is " + textBlock.ActualWidth.ToString("0") + " x " + textBlock.ActualHeight.ToString("0") + " but wants " + num.ToString("0") + " x " + num2.ToString("0");
						}
					}
					if (frameworkElement != null && !scrolled && frameworkElement.ActualWidth > 0.0 && frameworkElement.IsDescendantOf(root) && !(frameworkElement is Window))
					{
						Rect rect = frameworkElement.TransformToAncestor(root).TransformBounds(new Rect(0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight));
						if ((rect.Left < -1.0 || rect.Top < -1.0 || rect.Right > root.ActualWidth + 1.0 || rect.Bottom > root.ActualHeight + 1.0) && (!(frameworkElement is Panel) || ((Panel)frameworkElement).Background != null) && (frameworkElement is TextBlock || frameworkElement is Control || frameworkElement is SClickable || (frameworkElement is Border && ((Border)frameworkElement).Child is TextBlock)))
						{
							found = string.Concat(frameworkElement.GetType().Name, " at ", rect, " outside ", root.ActualWidth.ToString("0"), " x ", root.ActualHeight.ToString("0"));
						}
					}
					int childrenCount = VisualTreeHelper.GetChildrenCount(d);
					for (int i = 0; i < childrenCount; i++)
					{
						walk(VisualTreeHelper.GetChild(d, i), scrolled || d is ScrollContentPresenter);
					}
				}
			}
		};
		walk(root, arg2: false);
		return found;
	}

	private static bool InAynWindow()
	{
		if (LicenseStub.AynDaysLeft >= 1)
		{
			return LicenseStub.AynDaysLeft <= 3;
		}
		return false;
	}

	internal static void Pump(int ms = 300)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ms);
		do
		{
			DispatcherFrame f2 = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				f2.Continue = false;
			});
			Dispatcher.PushFrame(f2);
			Thread.Sleep(15);
		}
		while (DateTime.UtcNow < dateTime);
	}

	internal static void Capture(Window win, string path)
	{
		FrameworkElement frameworkElement = (FrameworkElement)win.Content;
		PresentationSource presentationSource = PresentationSource.FromVisual(win);
		double num = ((presentationSource != null) ? (96.0 * presentationSource.CompositionTarget.TransformToDevice.M11) : 96.0);
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Ceiling(frameworkElement.ActualWidth * num / 96.0), (int)Math.Ceiling(frameworkElement.ActualHeight * num / 96.0), num, num, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(frameworkElement);
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
			int sourceY = 0;
			int width = (int)(win.ActualWidth * num) + 32;
			int height = (int)((win.Top + win.ActualHeight) * num) + 16;
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
}
