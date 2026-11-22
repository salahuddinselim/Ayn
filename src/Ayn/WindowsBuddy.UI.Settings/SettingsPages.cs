using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Extras;
using WindowsBuddy.Tools.Personal;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.AICoding;
using WindowsBuddy.Tools.Analytics;
using WindowsBuddy.Tools.Calendar;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.Tools.Clock;
using WindowsBuddy.Tools.Drop;
using WindowsBuddy.Tools.ScreenTime;
using WindowsBuddy.Tools.Timer;
using WindowsBuddy.Tools.Weather;
using WindowsBuddy.UI.Onboarding;

namespace WindowsBuddy.UI.Settings;

internal static class SettingsPages
{
	internal const string StartupHint = "Starts silently each time you sign in. Quit applies only to this session. To have it back seconds after a restart, turn on \"Restart apps\" in Windows Settings â€º Accounts â€º Sign-in options.";

	private static int _notchCodePreparationStarted;

	private static int _windowCodePreparationStarted;

	private static readonly string[] ShapeKeys = new string[6] { "Default", "Compact", "Wide", "Circular", "Minimal", "Dynamic" };

	private static readonly Dictionary<string, string[]> DataAreas = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		{
			"clipboard",
			new string[2] { "Clipboard history", "Text, links and images you copied" }
		},
		{
			"notes",
			new string[2] { "Notes", "Everything you wrote in Notes" }
		},
		{
			"screentime",
			new string[2] { "Screen Time", "Minutes per app per day, last 7 days" }
		},
		{
			"shelf",
			new string[2] { "File Shelf", "Copies of files you parked" }
		},
		{
			"calendar",
			new string[2] { "Calendar", "Your events, reminders and cached feeds" }
		},
		{
			"weather",
			new string[2] { "Weather", "Your place and the last forecast" }
		},
		{
			"analytics",
			new string[2] { "Analytics", "Connected sites and cached reports (keys are encrypted)" }
		},
		{
			"timer",
			new string[2] { "Timers", "Timer state and today's sessions" }
		},
		{
			"links",
			new string[2] { "Links", "Bookmarks and favicons" }
		},
		{
			"message",
			new string[2] { "Message", "Your saved messages" }
		},
		{
			"settings",
			new string[2] { "Settings", "Every choice on these pages" }
		},
		{
			"logs",
			new string[2] { "Logs", "Diagnostics, capped at 1 MB per file" }
		}
	};

	private static readonly DateTime SampleDate = new DateTime(2026, 9, 22);

	internal static Task WindowCodePrepared { get; private set; }

	internal static void PrepareNotchCode()
	{
		if (Interlocked.Exchange(ref _notchCodePreparationStarted, 1) != 0)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
				Type[] array = new Type[10]
				{
					typeof(SettingsPages),
					typeof(ChoiceCards),
					typeof(NotchPreview),
					typeof(SDropdown),
					typeof(SSlider),
					typeof(SSettingBinding),
					typeof(EdgeLightOutline),
					typeof(NotchGeometryBuilder),
					typeof(EdgeLightWpf),
					typeof(SUI)
				};
				foreach (Type type in array)
				{
					MethodInfo[] methods = type.GetMethods(bindingAttr);
					foreach (MethodInfo methodInfo in methods)
					{
						if (!methodInfo.ContainsGenericParameters && (!(type == typeof(SettingsPages)) || methodInfo.Name.StartsWith("AddNotch") || methodInfo.Name == "NotchPage" || methodInfo.Name.EndsWith("Preview")) && (!(type == typeof(SUI)) || methodInfo.Name.EndsWith("Row") || methodInfo.Name == "Group" || methodInfo.Name == "AddToGroup"))
						{
							RuntimeHelpers.PrepareMethod(methodInfo.MethodHandle);
						}
					}
					ConstructorInfo[] constructors = type.GetConstructors(bindingAttr);
					foreach (ConstructorInfo constructorInfo in constructors)
					{
						RuntimeHelpers.PrepareMethod(constructorInfo.MethodHandle);
					}
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Settings code preparation unavailable: " + ex.GetType().Name);
			}
		});
	}

	internal static void PrepareWindowCode()
	{
		if (Interlocked.Exchange(ref _windowCodePreparationStarted, 1) != 0)
		{
			return;
		}
		TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
		WindowCodePrepared = done.Task;
		List<RuntimeMethodHandle> methods = new List<RuntimeMethodHandle>();
		try
		{
			BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			Type[] types = typeof(SettingsPages).Assembly.GetTypes();
			foreach (Type type in types)
			{
				if (type.Namespace == null || !type.Namespace.StartsWith("WindowsBuddy.UI.Settings", StringComparison.Ordinal) || type.ContainsGenericParameters)
				{
					continue;
				}
				MethodInfo[] methods2 = type.GetMethods(bindingAttr);
				foreach (MethodInfo methodInfo in methods2)
				{
					if (!methodInfo.ContainsGenericParameters && !methodInfo.IsAbstract)
					{
						methods.Add(methodInfo.MethodHandle);
					}
				}
				ConstructorInfo[] constructors = type.GetConstructors(bindingAttr);
				foreach (ConstructorInfo constructorInfo in constructors)
				{
					methods.Add(constructorInfo.MethodHandle);
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Settings window code preparation unavailable: " + ex.GetType().Name);
			methods.Clear();
		}
		Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
		int next = 0;
		Action slice = null;
		slice = delegate
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			for (; next < methods.Count; next++)
			{
				if (stopwatch.ElapsedMilliseconds >= 4)
				{
					break;
				}
				try
				{
					RuntimeHelpers.PrepareMethod(methods[next]);
				}
				catch
				{
				}
			}
			if (next < methods.Count)
			{
				dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, slice);
			}
			else
			{
				done.TrySetResult(result: true);
			}
		};
		dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, slice);
	}

	public static UIElement Build(string key, PageContext ctx)
	{
		key = PageInfo.Normalize(key);
		if (key.StartsWith("module:"))
		{
			return ModulePage(key.Substring(7), ctx);
		}
		return key switch
		{
			"notch" => NotchPage(ctx), 
			"appearance" => AppearancePage(ctx), 
			"modules" => ModulesPage(ctx), 
			"live" => LiveActivitiesPage(ctx), 
			"privacy" => PrivacyPage(ctx), 
			"breaks" => ExtrasPage(ctx, "breaks"), 
			"salat" => ExtrasPage(ctx, "salat"), 
			"guard" => ExtrasPage(ctx, "guard"), 
			"todo" => PersonalToolPage(ctx, "todo"), 
			"wifi" => PersonalToolPage(ctx, "wifi"), 
			"pets" => PersonalToolPage(ctx, "pets"), 

			"license" => LicenseAndUpdatesPage(ctx), 
			"about" => AboutPage(ctx), 
			_ => GeneralPage(ctx), 
		};
	}

	private static StackPanel Page(string key)
	{
		return SettingsPageLayout.Create(key);
	}

	private static T Safe<T>(string what, Func<T> read, T fallback)
	{
		try
		{
			return read();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Settings: couldn't read " + what + ", showing the default: " + ex.Message);
			return fallback;
		}
	}

	private static void Try(string what, Action apply)
	{
		try
		{
			apply();
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Settings: couldn't apply " + what + ": " + ex.Message, ex);
		}
	}

	private static TextBlock InlineMessage()
	{
		TextBlock textBlock = SUI.Note("");
		textBlock.Foreground = SettingsTheme.Danger;
		textBlock.Visibility = Visibility.Collapsed;
		return textBlock;
	}

	private static void Say(TextBlock line, string text, bool error)
	{
		line.Text = text ?? "";
		line.Foreground = (error ? SettingsTheme.Danger : SettingsTheme.TextSecondary);
		line.Visibility = (string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible);
	}

	private static UIElement GeneralPage(PageContext ctx)
	{
		StackPanel stackPanel = Page("general");
		AppPreferences prefs = AppPreferences.Instance;
		TextBlock startupNote = InlineMessage();
		SToggle loginToggle = null;
		bool alive = true;
		ctx.OnClose(delegate
		{
			alive = false;
		});
		Action refreshLogin = delegate
		{
			Task.Run(() => Tuple.Create(SafeAutoStartEnabled(), StartupService.StartupStatusMessage())).ContinueWith(delegate(Task<Tuple<bool, string>> t)
			{
				if (alive && loginToggle != null && loginToggle.IsEnabled)
				{
					loginToggle.IsOn = t.Result.Item1;
					Say(startupNote, t.Result.Item2, t.Result.Item2 != null);
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		};
		Grid grid = SUI.ToggleRow("Start Ayn when Windows starts", "Starts silently each time you sign in. Quit applies only to this session. To have it back seconds after a restart, turn on \"Restart apps\" in Windows Settings â€º Accounts â€º Sign-in options.", value: false, delegate(bool v)
		{
			loginToggle.IsEnabled = false;
			Say(startupNote, null, error: false);
			Task.Run(() => SetAutoStart(v)).ContinueWith(delegate(Task<bool> t)
			{
				if (alive)
				{
					loginToggle.IsEnabled = true;
					bool flag2 = ((!AppPaths.IsSandboxed) ? SafeAutoStartEnabled() : (t.Result ? v : (!v)));
					loginToggle.IsOn = flag2;
					if (flag2 != v)
					{
						Say(startupNote, AppPaths.IsPackaged ? "Windows has Ayn turned off. Re-enable it in Task Manager > Startup apps." : "Windows didn't allow the change. Try again, or check Settings â€º Apps â€º Startup.", error: true);
					}
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}, null, "setting-launch-at-login-001", () => SafeAutoStartEnabled());
		loginToggle = grid.Children.OfType<SToggle>().First();
		refreshLogin();
		EventHandler onActivated = delegate
		{
			if (ctx.Window.CurrentPage == "general")
			{
				refreshLogin();
			}
		};
		ctx.Window.Activated += onActivated;
		ctx.OnClose(delegate
		{
			ctx.Window.Activated -= onActivated;
		});
		HotkeyService hotkeys = HotkeyService.Instance;
		SHotkeyRecorder recorder = new SHotkeyRecorder(prefs.HotkeyModifiers, prefs.HotkeyKey);
		TextBlock hotkeyNote = InlineMessage();
		recorder.Validate = HotkeyService.Validate;
		recorder.Message += delegate(string m)
		{
			Say(hotkeyNote, m, error: true);
		};
		recorder.RecordingChanged += delegate(bool on)
		{
			if (on)
			{
				hotkeys.Suspend();
			}
			else
			{
				hotkeys.Resume();
			}
		};
		ctx.OnLeave(delegate
		{
			hotkeys.Resume();
		});
		recorder.Recorded += delegate(int mods, int key)
		{
			int hotkeyModifiers = prefs.HotkeyModifiers;
			int hotkeyKey = prefs.HotkeyKey;
			if (!prefs.Operations.AdmissionOpen)
			{
				recorder.SetValue(hotkeyModifiers, hotkeyKey);
			}
			else if (hotkeys.Apply(mods, key))
			{
				try
				{
					prefs.SetHotkey(mods, key);
				}
				catch (Exception ex)
				{
					bool flag = hotkeys.Apply(hotkeyModifiers, hotkeyKey);
					recorder.SetValue(hotkeyModifiers, hotkeyKey);
					Say(hotkeyNote, flag ? ("Shortcut change was not accepted: " + ex.Message) : "Shortcut change was not accepted and Windows could not restore the previous shortcut.", error: true);
					return;
				}
				Say(hotkeyNote, (key == 0) ? "Shortcut cleared. Open the notch from the tray icon, or record a new one." : null, error: false);
			}
			else
			{
				Say(hotkeyNote, hotkeys.LastError ?? "That shortcut isn't available.", error: true);
				hotkeys.Apply(hotkeyModifiers, hotkeyKey);
				recorder.SetValue(hotkeyModifiers, hotkeyKey);
			}
		};
		SSettingBinding.Attach(recorder, delegate
		{
			if (!recorder.IsRecording)
			{
				recorder.SetValue(prefs.HotkeyModifiers, prefs.HotkeyKey);
			}
		});
		Grid grid2 = SUI.Row("Open the notch", "Opens or closes the notch from anywhere", recorder, null, isRightAlignedAction: false, "setting-open-the-notch-002");
		ClockSettings clock = ClockSettings.Instance;
		string selected = Safe("time format", () => (!clock.FollowSystemFormat) ? ((clock.Format != WindowsBuddy.Tools.Clock.ClockFormat.TwentyFourHour) ? "12" : "24") : "system", "system");
		WeatherService weather = WeatherService.Instance;
		Grid grid3 = SUI.SegmentedRow("Time format", "Used by the notch, Home and Clock", new string[3] { "System", "12-hour", "24-hour" }, new string[3] { "system", "12", "24" }, selected, delegate(string k)
		{
			Try("time format", delegate
			{
				if (k == "system")
				{
					clock.FollowSystemFormat = true;
				}
				else
				{
					clock.Format = ((k == "24") ? WindowsBuddy.Tools.Clock.ClockFormat.TwentyFourHour : WindowsBuddy.Tools.Clock.ClockFormat.TwelveHour);
				}
			});
		}, "setting-time-format-003", () => Safe("time format", () => (!clock.FollowSystemFormat) ? ((clock.Format != WindowsBuddy.Tools.Clock.ClockFormat.TwentyFourHour) ? "12" : "24") : "system", "system"));
		Grid grid4 = SUI.SegmentedRow("Temperature", "Weather, Home and the notch", new string[2] { "Â°C", "Â°F" }, new string[2] { "c", "f" }, Safe("temperature unit", () => (!weather.UseFahrenheit) ? "c" : "f", "c"), delegate(string k)
		{
			Try("temperature unit", delegate
			{
				weather.UseFahrenheit = k == "f";
			});
		}, "setting-temperature-004", () => Safe("temperature unit", () => (!weather.UseFahrenheit) ? "c" : "f", "c"));
		Grid grid5 = SUI.SegmentedRow("Wind speed", "Used by Weather and Home", new string[2] { "km/h", "mph" }, new string[2] { "kmh", "mph" }, Safe("wind unit", () => (!weather.WindInMph) ? "kmh" : "mph", "kmh"), delegate(string k)
		{
			Try("wind unit", delegate
			{
				weather.WindInMph = k == "mph";
			});
		}, "setting-wind-speed-005", () => Safe("wind unit", () => (!weather.WindInMph) ? "kmh" : "mph", "kmh"));
		Grid grid6 = SUI.ValueRow("Language", "More languages are on the way", "English", null, "setting-language-006");
		stackPanel.Children.Add(SUI.Group(grid, grid2, grid3, grid4, grid5, grid6));
		stackPanel.Children.Add(startupNote);
		stackPanel.Children.Add(hotkeyNote);
		return stackPanel;
	}

	private static bool SafeAutoStartEnabled()
	{
		try
		{
			return StartupService.IsAutoStartEnabled();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Launch at login state unavailable: " + ex.Message);
			return false;
		}
	}

	private static bool SetAutoStart(bool on)
	{
		if (AppPaths.IsSandboxed)
		{
			TransactionLogger.Info("QA sandbox: launch-at-login change to " + on + " not written to Windows.");
			return true;
		}
		try
		{
			return on ? StartupService.EnableAutoStart() : StartupService.DisableAutoStart();
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Launch at login change failed: " + ex.Message, ex);
			return false;
		}
	}

	private static UIElement NotchPage(PageContext ctx)
	{
		StackPanel stackPanel = Page("notch");
		NotchPreview notchPreview = new NotchPreview(ctx);
		notchPreview.Margin = new Thickness(0.0, 4.0, 0.0, 16.0);
		stackPanel.Children.Add(notchPreview);
		AddNotchVisibility(stackPanel);
		AddNotchPlacement(stackPanel);
		AddNotchContents(stackPanel, ctx);
		AddNotchEdge(stackPanel, ctx);
		return stackPanel;
	}

	private static void AddNotchVisibility(StackPanel page)
	{
		NotchSettings notch = NotchSettings.Instance;
		page.Children.Add(SUI.SectionLabel("Visibility"));
		ChoiceCards modeCards = new ChoiceCards(new ChoiceCards.Option[4]
		{
			new ChoiceCards.Option("AlwaysVisible", "Always", "Always on screen. Fullscreen rules below still apply.", ModePreview(NotchVisibilityMode.AlwaysVisible)),
			new ChoiceCards.Option("Smart", "Smart", "Full on the desktop; tucked away over maximized apps.", ModePreview(NotchVisibilityMode.Smart)),
			new ChoiceCards.Option("Minimal", "Minimal", "Just a small arrow at the top. Hover to peek, open on hover or click.", ModePreview(NotchVisibilityMode.Minimal)),
			new ChoiceCards.Option("Hidden", "Hidden", HiddenModeDescription(), ModePreview(NotchVisibilityMode.Hidden))
		}, notch.VisibilityMode.ToString(), "Visibility", 2);
		modeCards.Tag = "setting-card-visibility";
		AutomationProperties.SetName(modeCards, "Visibility");
		page.Children.Add(modeCards);
		FrameworkElement maxLabel = SUI.SectionLabel("Over maximized apps");
		ChoiceCards maxCards = new ChoiceCards(new ChoiceCards.Option[2]
		{
			new ChoiceCards.Option("ArrowOnly", "Arrow only", "A small arrow at the top edge. Hover to peek, click to open.", MaximizedLookPreview(OverMaximizedLook.ArrowOnly)),
			new ChoiceCards.Option("MiniNotch", "Mini notch", "A small notch instead of the arrow.", MaximizedLookPreview(OverMaximizedLook.MiniNotch))
		}, notch.OverMaximizedLook.ToString(), "Over maximized apps");
		maxCards.Tag = "setting-card-over-maximized-apps";
		AutomationProperties.SetName(maxCards, "Over maximized apps");
		maxCards.Changed += delegate(string k)
		{
			notch.OverMaximizedLook = (OverMaximizedLook)Enum.Parse(typeof(OverMaximizedLook), k);
		};
		Action<NotchVisibilityMode> showMaxRow = delegate(NotchVisibilityMode m)
		{
			Visibility visibility = ((m != NotchVisibilityMode.Smart) ? Visibility.Collapsed : Visibility.Visible);
			maxLabel.Visibility = visibility;
			maxCards.Visibility = visibility;
		};
		showMaxRow(notch.VisibilityMode);
		page.Children.Add(maxLabel);
		page.Children.Add(maxCards);
		SSettingBinding.Attach(modeCards, delegate
		{
			modeCards.SetSelection(notch.VisibilityMode.ToString());
			showMaxRow(notch.VisibilityMode);
		});
		SSettingBinding.Attach(maxCards, delegate
		{
			maxCards.SetSelection(notch.OverMaximizedLook.ToString());
		});
		modeCards.Changed += delegate(string k)
		{
			NotchVisibilityMode notchVisibilityMode = (NotchVisibilityMode)Enum.Parse(typeof(NotchVisibilityMode), k);
			notch.VisibilityMode = notchVisibilityMode;
			showMaxRow(notchVisibilityMode);
		};
	}

	private static void AddNotchPlacement(StackPanel page)
	{
		NotchConfig cfg = NotchConfig.Instance;
		ToolRegistry reg = ToolRegistry.Instance;
		page.Children.Add(SUI.SectionLabel("Style"));
		ChoiceCards styleCards = new ChoiceCards(new ChoiceCards.Option[3]
		{
			new ChoiceCards.Option("Line", "Line", "A slim bar along the top edge", StylePreview("Line")),
			new ChoiceCards.Option("Notch", "Notch", "Grows out of the screen edge", StylePreview("Default")),
			new ChoiceCards.Option("Island", "Island", "A pill floating just below the edge", StylePreview("Island"))
		}, cfg.StyleFamily, "Style", 3);
		styleCards.Tag = "setting-card-style";
		AutomationProperties.SetName(styleCards, "Style");
		page.Children.Add(styleCards);
		string selected = (ShapeKeys.Contains(cfg.Style) ? cfg.Style : ((cfg.Style == "SquareMinimal") ? "Minimal" : "Default"));
		Grid shapeRow = SUI.DropdownRow("Notch shape", "Fine-tune the Notch style's silhouette", SUI.Items("Default", "Default", "Compact", "Compact", "Wide", "Wide", "Circular", "Circular", "Minimal", "Minimal", "Dynamic", "Dynamic"), selected, delegate(string k)
		{
			cfg.Style = k;
		}, 240.0, "setting-notch-shape-007", () => (!ShapeKeys.Contains(cfg.Style)) ? ((!(cfg.Style == "SquareMinimal")) ? null : "Minimal") : cfg.Style);
		shapeRow.Visibility = ((!(cfg.StyleFamily == "Notch")) ? Visibility.Collapsed : Visibility.Visible);
		SDropdown shapeDropdown = shapeRow.Children.OfType<SDropdown>().First();
		SSettingBinding.Attach(styleCards, delegate
		{
			styleCards.SetSelection(cfg.StyleFamily);
			shapeRow.Visibility = ((!(cfg.StyleFamily == "Notch")) ? Visibility.Collapsed : Visibility.Visible);
		});
		styleCards.Changed += delegate(string k)
		{
			cfg.Style = ((k == "Notch") ? shapeDropdown.Selected : k);
			shapeRow.Visibility = ((!(k == "Notch")) ? Visibility.Collapsed : Visibility.Visible);
		};
		List<KeyValuePair<string, string>> displayItems = DisplayItems();
		string display = reg.PinnedDisplay ?? "primary";
		if (!displayItems.Any((KeyValuePair<string, string> kv) => kv.Key == display))
		{
			display = "primary";
		}
		Border border = SUI.Group(shapeRow, SUI.SliderRow("Size", "Scales the resting notch", 90.0, 120.0, 5.0, Math.Round(cfg.SizeScale * 100.0), (double v) => $"{v:0}%", delegate(double v)
		{
			cfg.SizeScale = v / 100.0;
		}, 180.0, "setting-size-009", () => Math.Round(cfg.SizeScale * 100.0)), SUI.SegmentedRow("Resting layout", "Time and battery in corners or center", new string[2] { "Corners", "Center" }, new string[2] { "Corners", "Center" }, cfg.RestingLayout, delegate(string k)
		{
			cfg.RestingLayout = k;
		}, "setting-resting-layout-010", () => cfg.RestingLayout), SUI.SegmentedRow("Opens on", "Hover to open at once, or click to open on purpose", new string[2] { "Hover", "Click" }, new string[2] { "hover", "click" }, reg.OpenOn, delegate(string k)
		{
			reg.OpenOn = k;
		}, "setting-opens-on-011", () => reg.OpenOn), SUI.DropdownRow("Display", "Which monitor the notch lives on", displayItems, display, delegate(string k)
		{
			reg.PinnedDisplay = k;
		}, 220.0, "setting-display-012", () => (!displayItems.Any((KeyValuePair<string, string> kv) => kv.Key == (reg.PinnedDisplay ?? "primary"))) ? "primary" : (reg.PinnedDisplay ?? "primary")), SUI.ToggleRow("Show notch", "Hide it without quitting; the tray icon brings it back", reg.CapsuleEnabled, delegate(bool v)
		{
			if (v)
			{
				reg.ForceEnableCapsule();
			}
			else
			{
				reg.CapsuleEnabled = false;
			}
		}, null, "setting-show-notch-013", () => reg.CapsuleEnabled));
		border.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		page.Children.Add(border);
	}

	private static void AddNotchContents(StackPanel page, PageContext ctx)
	{
		NotchConfig cfg = NotchConfig.Instance;
		ToolRegistry reg = ToolRegistry.Instance;
		NotchBehaviorConfig beh = NotchBehaviorConfig.Instance;
		page.Children.Add(SUI.SectionLabel("Behavior"));
		page.Children.Add(SUI.Group(SUI.ToggleRow("Hide in fullscreen", "Apps, videos and games on the notch's display", beh.HideInFullscreen, delegate(bool v)
		{
			beh.HideInFullscreen = v;
		}, null, "setting-hide-in-fullscreen-014", () => beh.HideInFullscreen), SUI.ToggleRow("Show events over apps", "When the notch is tucked away, grow it briefly for events like volume, timers and a new song", beh.ShowBriefEventsWhenHidden, delegate(bool v)
		{
			beh.ShowBriefEventsWhenHidden = v;
		}, null, "setting-show-events-over-apps-015", () => beh.ShowBriefEventsWhenHidden), SUI.ToggleRow("Tab animations", "Slide between tabs inside the open notch", reg.AnimationsEnabled, delegate(bool v)
		{
			reg.AnimationsEnabled = v;
		}, null, "setting-tab-animations-017", () => reg.AnimationsEnabled)));
		page.Children.Add(SUI.SectionLabel("Ayn Pet (Beta)"));
		PetPreferences pet = PetPreferences.Instance;
		page.Children.Add(SUI.Group(SUI.ToggleRow("Ayn Pet (Beta)", "A small companion in your notch", pet.Enabled, delegate(bool v)
		{
			pet.Enabled = v;
		}, null, "setting-ayn-pet", () => pet.Enabled)));
		page.Children.Add(SUI.SectionLabel("Eyes"));
		EyesPreferences eyes = EyesPreferences.Instance;
		page.Children.Add(SUI.Group(SUI.ToggleRow("Eyes", "Two animated eyes that follow your cursor. While Eyes is on, the closed notch shows only the eyes. Volume, timers, music and other events still appear, then the eyes return.", eyes.Enabled, delegate(bool v)
		{
			eyes.Enabled = v;
		}, null, "setting-ayn-eyes", () => eyes.Enabled)));
		TextBlock eyesPetNote = SUI.Note("The Ayn Pet is on, so Eyes stay hidden. Turn the Pet off to see them.");
		eyesPetNote.Visibility = ((!pet.Enabled) ? Visibility.Collapsed : Visibility.Visible);
		SSettingBinding.Attach(eyesPetNote, delegate
		{
			eyesPetNote.Visibility = ((!pet.Enabled) ? Visibility.Collapsed : Visibility.Visible);
		});
		page.Children.Add(eyesPetNote);
		ChoiceCards eyeCards = new ChoiceCards(new ChoiceCards.Option[4]
		{
			new ChoiceCards.Option("ayn", "Ayn", EyeSkins.Tagline("ayn"), EyesSettingsPreview("ayn")),
			new ChoiceCards.Option("bubble", "Bubble", EyeSkins.Tagline("bubble"), EyesSettingsPreview("bubble")),
			new ChoiceCards.Option("honey", "Honey", EyeSkins.Tagline("honey"), EyesSettingsPreview("honey")),
			new ChoiceCards.Option("jade", "Jade", EyeSkins.Tagline("jade"), EyesSettingsPreview("jade"))
		}, eyes.Skin, "Eye skin", 2);
		eyeCards.Tag = "setting-card-eye-skin";
		AutomationProperties.SetName(eyeCards, "Eye skin");
		eyeCards.Changed += delegate(string k)
		{
			eyes.Skin = k;
		};
		SSettingBinding.Attach(eyeCards, delegate
		{
			eyeCards.SetSelection(eyes.Skin);
		});
		page.Children.Add(eyeCards);
		page.Children.Add(SUI.SectionLabel("When closed, show"));
		Grid clockStyleRow = SUI.SegmentedRow("Clock style", null, new string[2] { "Digital", "Analog" }, new string[2] { "digital", "analog" }, cfg.NotchAnalogClock ? "analog" : "digital", delegate(string k)
		{
			cfg.NotchAnalogClock = k == "analog";
		}, "setting-clock-style-018", () => (!cfg.NotchAnalogClock) ? "digital" : "analog");
		SUI.SetRowEnabled(clockStyleRow, cfg.ShowTime);
		Grid dateRow = SUI.ToggleRow("Date", "Day and date next to the time, e.g. Sat 3 Â· 9:41", cfg.ShowDateInNotch, delegate(bool v)
		{
			cfg.ShowDateInNotch = v;
		}, null, "setting-date-019", () => cfg.ShowDateInNotch);
		SUI.SetRowEnabled(dateRow, cfg.ShowTime && !cfg.NotchAnalogClock);
		clockStyleRow.Children.OfType<SSegmented>().First().Changed += delegate(string k)
		{
			SUI.SetRowEnabled(dateRow, cfg.ShowTime && k != "analog");
		};
		Border element = SUI.Group(SUI.ToggleRow("Time", "The resting clock that events return to", cfg.ShowTime, delegate(bool v)
		{
			cfg.ShowTime = v;
			SUI.SetRowEnabled(clockStyleRow, v);
			SUI.SetRowEnabled(dateRow, v && !cfg.NotchAnalogClock);
		}, null, "setting-time-020", () => cfg.ShowTime), clockStyleRow, dateRow, SUI.ToggleRow("Battery", "Level and charging", cfg.ShowBattery, delegate(bool v)
		{
			cfg.ShowBattery = v;
		}, null, "setting-battery-021", () => cfg.ShowBattery), SUI.ToggleRow("Now playing", "Compact media controls while music or video plays", cfg.ShowNowPlaying, delegate(bool v)
		{
			cfg.ShowNowPlaying = v;
		}, null, "setting-now-playing-022", () => cfg.ShowNowPlaying), SUI.ToggleRow("Bluetooth", "While a device is connected", cfg.ShowBluetooth, delegate(bool v)
		{
			cfg.ShowBluetooth = v;
		}, null, "setting-bluetooth-025", () => cfg.ShowBluetooth));
		page.Children.Add(element);
		SButton sButton = new SButton("Restore defaults");
		sButton.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		sButton.Click += delegate
		{
			cfg.ShowTime = true;
			cfg.NotchAnalogClock = false;
			cfg.ShowDateInNotch = false;
			cfg.ShowBattery = true;
			cfg.ShowBluetooth = false;
			cfg.ShowNowPlaying = false;
			cfg.RestingLayout = "Corners";
			ctx.Reload();
		};
		page.Children.Add(sButton);
	}

	private static void AddNotchEdge(StackPanel page, PageContext ctx)
	{
		NotchConfig cfg = NotchConfig.Instance;
		page.Children.Add(SUI.SectionLabel("Edge light"));
		ChoiceCards edgeStyleCards = new ChoiceCards(new ChoiceCards.Option[3]
		{
			new ChoiceCards.Option("Comet", "Comet", "One light travels along the outline", EdgeStylePreview(EdgeLightStyle.Comet)),
			new ChoiceCards.Option("Twin comets", "Twin comets", "Two lights meet at the bottom, then return", EdgeStylePreview(EdgeLightStyle.TwinComets)),
			new ChoiceCards.Option("Breathe", "Breathe", "The whole outline glows softly", EdgeStylePreview(EdgeLightStyle.Breathe))
		}, cfg.EdgeLightStyle, "Edge light style");
		edgeStyleCards.Changed += delegate(string k)
		{
			cfg.EdgeLightStyle = k;
		};
		edgeStyleCards.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		edgeStyleCards.Tag = "setting-card-edge-light-style";
		AutomationProperties.SetName(edgeStyleCards, "Edge light style");
		List<Grid> edgeRows = new List<Grid>
		{
			SUI.Row("Color", null, new EdgeSwatches(cfg), null, isRightAlignedAction: false, "setting-color-027"),
			SUI.SliderRow("Intensity", null, 0.0, 100.0, 1.0, Math.Round(cfg.EdgeLightIntensity * 100.0), (double v) => $"{v:0}%", delegate(double v)
			{
				cfg.EdgeLightIntensity = v / 100.0;
			}, 180.0, "setting-intensity-028", () => Math.Round(cfg.EdgeLightIntensity * 100.0)),
			SUI.SegmentedRow("Speed", null, new string[3] { "Slow", "Normal", "Fast" }, new string[3] { "Slow", "Normal", "Fast" }, cfg.EdgeLightSpeed, delegate(string k)
			{
				cfg.EdgeLightSpeed = k;
			}, "setting-speed-029", () => cfg.EdgeLightSpeed),
			SUI.SegmentedRow("When", "On events & hover plays the light for a few seconds, then it fades out", new string[2] { "Always", "On events & hover" }, new string[2] { "Always", "Events" }, cfg.EdgeLightWhen, delegate(string k)
			{
				cfg.EdgeLightWhen = k;
			}, "setting-when-030", () => cfg.EdgeLightWhen)
		};
		Border border = SUI.Group(edgeRows.ToArray());
		border.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		Action<bool> setEdge = delegate(bool on)
		{
			foreach (Grid item in edgeRows)
			{
				SUI.SetRowEnabled(item, on);
			}
			edgeStyleCards.IsEnabled = on;
			edgeStyleCards.Opacity = 1.0;
		};
		page.Children.Add(SUI.Group(SUI.ToggleRow("Edge light", "Light that runs along the notch outline", cfg.EdgeLightEnabled, delegate(bool v)
		{
			cfg.EdgeLightEnabled = v;
			setEdge(v);
		}, null, "setting-edge-light-031", () => cfg.EdgeLightEnabled)));
		page.Children.Add(edgeStyleCards);
		page.Children.Add(border);
		setEdge(cfg.EdgeLightEnabled);
		SSettingBinding.Attach(edgeStyleCards, delegate
		{
			edgeStyleCards.SetSelection(cfg.EdgeLightStyle);
			setEdge(cfg.EdgeLightEnabled);
		});
		SButton sButton = new SButton("Reset notch to defaults");
		sButton.Margin = new Thickness(0.0, 24.0, 0.0, 0.0);
		sButton.Click += delegate
		{
			if (SUI.Confirm(ctx.Window, "Reset the notch?", "Shape, size, what it shows, surface and edge light go back to their defaults. Modules and their data are not touched.", "Reset", danger: false))
			{
				cfg.ResetToDefaults();
				ctx.Reload();
			}
		};
		page.Children.Add(sButton);
	}

	private static List<KeyValuePair<string, string>> DisplayItems()
	{
		List<KeyValuePair<string, string>> list = SUI.Items("primary", "Primary display", "cursor", "Follow the mouse");
		try
		{
			Screen[] allScreens = Screen.AllScreens;
			if (allScreens.Length > 1)
			{
				for (int i = 0; i < allScreens.Length; i++)
				{
					Screen screen = allScreens[i];
					string value = string.Format("Display {0} Â· {1}Ã—{2}{3}", i + 1, screen.Bounds.Width, screen.Bounds.Height, screen.Primary ? " (primary)" : "");
					list.Add(new KeyValuePair<string, string>(screen.DeviceName, value));
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static void SetRowDescription(Grid row, string text)
	{
		StackPanel stackPanel = row.Children.OfType<StackPanel>().FirstOrDefault();
		if (stackPanel != null && stackPanel.Children.Count > 1)
		{
			((TextBlock)stackPanel.Children[1]).Text = text;
		}
	}

	private static string HiddenModeDescription()
	{
		AppPreferences instance = AppPreferences.Instance;
		string text = ((instance.HotkeyKey != 0) ? HotkeyService.Describe(instance.HotkeyModifiers, instance.HotkeyKey) : null);
		if (text == null)
		{
			return "Nothing on screen. Open it from the tray icon (or set a shortcut in General).";
		}
		return "Nothing on screen. Open it with " + text + " or the tray icon.";
	}

	private static FrameworkElement ArrowTabShape()
	{
		Border border = new Border();
		border.Width = 14.0;
		border.Height = 6.0;
		border.Background = Brushes.Black;
		border.CornerRadius = new CornerRadius(0.0, 0.0, 3.0, 3.0);
		border.BorderBrush = SettingsTheme.Alpha(110, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		border.BorderThickness = new Thickness(0.5, 0.0, 0.5, 0.5);
		border.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		border.VerticalAlignment = VerticalAlignment.Top;
		border.Child = new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse("M0,0 L2,2 L4,0"),
			Stroke = Brushes.White,
			StrokeThickness = 0.8,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		return border;
	}

	internal static FrameworkElement EyesSettingsPreview(string skin)
	{
		EyesView eyes = new EyesView
		{
			CardWidthDip = 240.0,
			HoverHalfWidthDip = 63.0,
			HoverHalfHeightDip = 22.0
		};
		Border border = new Border();
		border.Width = 126.0;
		border.Height = 44.0;
		border.CornerRadius = new CornerRadius(0.0, 0.0, 14.0, 14.0);
		border.Background = Brushes.Black;
		border.ClipToBounds = true;
		border.Child = eyes;
		Border result = border;
		Action<bool> sync = delegate(bool on)
		{
			try
			{
				if (!on)
				{
					eyes.Release();
				}
				else
				{
					eyes.Configure(skin, 36.0, VisualTreeHelper.GetDpi(eyes).DpiScaleX);
					eyes.Start();
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Eyes preview: " + ex.Message);
			}
		};
		eyes.Loaded += delegate
		{
			sync(eyes.IsVisible);
		};
		eyes.Unloaded += delegate
		{
			sync(obj: false);
		};
		eyes.IsVisibleChanged += delegate
		{
			sync(eyes.IsVisible && eyes.IsLoaded);
		};
		return result;
	}

	internal static FrameworkElement MaximizedLookPreview(OverMaximizedLook look)
	{
		Grid grid = new Grid();
		grid.Width = 150.0;
		grid.Height = 40.0;
		grid.Background = SettingsTheme.Solid(44, 44, 48);
		Grid grid2 = grid;
		if (look == OverMaximizedLook.MiniNotch)
		{
			grid2.Children.Add(new Border
			{
				Width = 40.0,
				Height = 8.0,
				Background = Brushes.Black,
				CornerRadius = new CornerRadius(0.0, 0.0, 4.0, 4.0),
				BorderBrush = SettingsTheme.Alpha(110, byte.MaxValue, byte.MaxValue, byte.MaxValue),
				BorderThickness = new Thickness(0.5, 0.0, 0.5, 0.5),
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Top
			});
		}
		else
		{
			grid2.Children.Add(ArrowTabShape());
		}
		return grid2;
	}

	internal static FrameworkElement ModePreview(NotchVisibilityMode mode)
	{
		Grid grid = new Grid();
		grid.Width = 150.0;
		grid.Height = 40.0;
		Grid grid2 = grid;
		SolidColorBrush borderStrong = ThemeTokens.BorderStrong;
		Func<double, FrameworkElement> func = (double w) => new System.Windows.Shapes.Path
		{
			Data = NotchGeometryBuilder.BuildGeometry("Default", w, w * 0.22),
			Fill = Brushes.Black,
			Stroke = SettingsTheme.Alpha(90, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			StrokeThickness = 1.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top
		};
		switch (mode)
		{
		case NotchVisibilityMode.Smart:
		{
			Grid grid3 = new Grid();
			grid3.ColumnDefinitions.Add(new ColumnDefinition());
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(8.0)
			});
			grid3.ColumnDefinitions.Add(new ColumnDefinition());
			Grid grid4 = new Grid();
			grid4.Children.Add(new Border
			{
				Height = 1.0,
				VerticalAlignment = VerticalAlignment.Top,
				Background = borderStrong
			});
			grid4.Children.Add(func(46.0));
			Grid grid5 = new Grid();
			grid5.Background = SettingsPreviewStage.AppSurface;
			Grid grid6 = grid5;
			grid6.Children.Add(ArrowTabShape());
			Grid.SetColumn(grid6, 2);
			grid3.Children.Add(grid4);
			grid3.Children.Add(grid6);
			grid2.Children.Add(grid3);
			break;
		}
		case NotchVisibilityMode.Minimal:
			grid2.Children.Add(new Border
			{
				Height = 1.0,
				VerticalAlignment = VerticalAlignment.Top,
				Background = borderStrong
			});
			grid2.Children.Add(ArrowTabShape());
			break;
		case NotchVisibilityMode.Hidden:
		{
			grid2.Children.Add(new Border
			{
				Height = 1.0,
				VerticalAlignment = VerticalAlignment.Top,
				Background = borderStrong
			});
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
			path.Data = NotchGeometryBuilder.BuildGeometry("Default", 92.0, 20.0);
			path.Stroke = ThemeTokens.BorderStrong;
			path.StrokeThickness = 1.0;
			path.StrokeDashArray = new DoubleCollection { 3.0, 3.0 };
			path.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			path.VerticalAlignment = VerticalAlignment.Top;
			System.Windows.Shapes.Path element = path;
			grid2.Children.Add(element);
			break;
		}
		default:
			grid2.Children.Add(new Border
			{
				Height = 1.0,
				VerticalAlignment = VerticalAlignment.Top,
				Background = borderStrong
			});
			grid2.Children.Add(func(92.0));
			break;
		}
		return grid2;
	}

	private static FrameworkElement EdgeStylePreview(EdgeLightStyle style)
	{
		Grid g = new Grid
		{
			Width = 150.0,
			Height = 40.0
		};
		g.Children.Add(new Border
		{
			Height = 1.0,
			VerticalAlignment = VerticalAlignment.Top,
			Background = ThemeTokens.BorderStrong
		});
		Grid grid = new Grid();
		grid.Width = 92.0;
		grid.Height = 20.0;
		grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		Grid grid2 = grid;
		EdgeLightWpf light = new EdgeLightWpf(30, preview: true);
		grid2.Children.Add(light);
		grid2.Children.Add(new System.Windows.Shapes.Path
		{
			Data = NotchGeometryBuilder.BuildGeometry("Default", 92.0, 20.0),
			Fill = Brushes.Black
		});
		g.Children.Add(grid2);
		light.SetOutline(EdgeLightOutline.For("Default", 92.0, 20.0, -1.0), default(Point), 1.0);
		ScrollViewer viewport = null;
		Action apply = delegate
		{
			if (viewport != null)
			{
				if (!g.IsDescendantOf(viewport))
				{
					light.SetMode(EdgeLightMode.Off);
					return;
				}
				Rect rect = g.TransformToAncestor(viewport).TransformBounds(new Rect(g.RenderSize));
				if (!g.IsVisible || !rect.IntersectsWith(new Rect(viewport.RenderSize)))
				{
					light.SetMode(EdgeLightMode.Off);
					return;
				}
			}
			EdgeLightLook edgeLightLook = EdgeLightLook.FromConfig(NotchConfig.Instance).With(style);
			edgeLightLook.Intensity = Math.Max(0.35, edgeLightLook.Intensity);
			light.SetLook(edgeLightLook);
			light.SetMode(EdgeLightMode.Animated);
		};
		EventHandler changed = delegate
		{
			g.Dispatcher.BeginInvoke(apply);
		};
		ScrollChangedEventHandler scrolled = delegate
		{
			apply();
		};
		g.Loaded += delegate
		{
			DependencyObject parent = VisualTreeHelper.GetParent(g);
			while (parent != null && (viewport = parent as ScrollViewer) == null)
			{
				parent = VisualTreeHelper.GetParent(parent);
			}
			if (viewport != null)
			{
				viewport.ScrollChanged += scrolled;
			}
			NotchConfig.Instance.ConfigChanged += changed;
			apply();
		};
		g.Unloaded += delegate
		{
			if (viewport != null)
			{
				viewport.ScrollChanged -= scrolled;
			}
			viewport = null;
			NotchConfig.Instance.ConfigChanged -= changed;
			light.SetMode(EdgeLightMode.Off);
		};
		return g;
	}

	private static FrameworkElement StylePreview(string style)
	{
		double width = ((style == "Line") ? 100 : 92);
		double height = ((style == "Line") ? 13 : ((style == "Island") ? 22 : 20));
		Grid grid = new Grid();
		grid.Width = 150.0;
		grid.Height = 40.0;
		Grid grid2 = grid;
		grid2.Children.Add(new Border
		{
			Height = 1.0,
			VerticalAlignment = VerticalAlignment.Top,
			Background = ThemeTokens.BorderStrong
		});
		grid2.Children.Add(new System.Windows.Shapes.Path
		{
			Data = NotchGeometryBuilder.BuildGeometry(style, width, height),
			Fill = Brushes.Black,
			Stroke = SettingsTheme.Alpha(90, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			StrokeThickness = 1.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top
		});
		return grid2;
	}

	private static UIElement AppearancePage(PageContext ctx)
	{
		StackPanel stackPanel = Page("appearance");
		NotchConfig cfg = NotchConfig.Instance;
		stackPanel.Children.Add(SUI.SectionLabel("Theme"));
		ChoiceCards themeCards = new ChoiceCards(new ChoiceCards.Option[4]
		{
			new ChoiceCards.Option("PureBlack", "Pure Black", "Monochrome on true black", ThemePreview("PureBlack")),
			new ChoiceCards.Option("EmberDark", "Ember Dark", "Black with an orange accent", ThemePreview("EmberDark")),
			new ChoiceCards.Option("Vivid", "Vivid", "Black with a blue accent", ThemePreview("Vivid")),
			new ChoiceCards.Option("Silver", "Silver", "Light panel; the notch stays black", ThemePreview("Silver"))
		}, cfg.AppTheme, "Theme", 4);
		themeCards.Tag = "setting-card-theme";
		AutomationProperties.SetName(themeCards, "Theme");
		SSettingBinding.Attach(themeCards, delegate
		{
			themeCards.SetSelection(cfg.AppTheme);
		});
		themeCards.Changed += delegate(string k)
		{
			cfg.AppTheme = k;
		};
		stackPanel.Children.Add(themeCards);
		stackPanel.Children.Add(SUI.Group(SUI.SliderRow("Closed notch opacity", "Background only", 40.0, 100.0, 1.0, Math.Round(cfg.ClosedNotchOpacity * 100.0), (double v) => $"{v:0}%", delegate(double v)
		{
			cfg.ClosedNotchOpacity = v / 100.0;
		}, 180.0, "setting-closed-notch-opacity", () => Math.Round(cfg.ClosedNotchOpacity * 100.0)), SUI.ActionRow("Reset opacity", "Restore the closed notch to 100%", "Reset", delegate
		{
			cfg.ResetNotchOpacity();
		}, null, "setting-reset-notch-opacity")));
		stackPanel.Children.Add(SUI.SectionLabel("Surface"));
		Grid outlineWidth = SUI.SegmentedRow("Outline width", null, new string[3] { "1 px", "2 px", "3 px" }, new string[3] { "1", "2", "3" }, ((int)Math.Round(cfg.BorderThickness)).ToString(), delegate(string k)
		{
			cfg.BorderThickness = double.Parse(k);
		}, "setting-outline-width-032", () => ((int)Math.Round(cfg.BorderThickness)).ToString());
		Grid wallpaperRow = SUI.ToggleRow("Tint outline from wallpaper", "Picks up your wallpaper's main color", cfg.AdaptToWallpaper, delegate(bool v)
		{
			cfg.AdaptToWallpaper = v;
		}, null, "setting-tint-outline-from-wallpaper-033", () => cfg.AdaptToWallpaper);
		SUI.SetRowEnabled(outlineWidth, cfg.BorderOutlineEnabled);
		SUI.SetRowEnabled(wallpaperRow, cfg.BorderOutlineEnabled);
		stackPanel.Children.Add(SUI.Group(SUI.ToggleRow("Outline", "A hairline around the notch", cfg.BorderOutlineEnabled, delegate(bool v)
		{
			cfg.BorderOutlineEnabled = v;
			SUI.SetRowEnabled(outlineWidth, v);
			SUI.SetRowEnabled(wallpaperRow, v);
		}, null, "setting-outline-034", () => cfg.BorderOutlineEnabled), outlineWidth, wallpaperRow, SUI.SegmentedRow("Shadow", "Depth under the open notch", new string[4] { "None", "Subtle", "Medium", "Deep" }, new string[4] { "None", "Subtle", "Medium", "Deep" }, cfg.ShadowStrength ?? "Medium", delegate(string k)
		{
			cfg.ShadowStrength = k;
		}, "setting-shadow-035", () => cfg.ShadowStrength ?? "Medium"), SUI.SegmentedRow("Soft edge", "Softens the notch edge; unavailable in reduced-effects mode", new string[4] { "None", "Low", "Medium", "High" }, new string[4] { "None", "Low", "Medium", "High" }, cfg.BlurEffect ?? "None", delegate(string k)
		{
			cfg.BlurEffect = k;
		}, "setting-background-blur-036", () => cfg.BlurEffect ?? "None")));
		return stackPanel;
	}

	internal static FrameworkElement ThemePreview(string id)
	{
		ThemePalette themePalette = ThemeTokens.Palette(id);
		Func<Color, Brush> func = delegate(Color c)
		{
			SolidColorBrush solidColorBrush = new SolidColorBrush(c);
			solidColorBrush.Freeze();
			return solidColorBrush;
		};
		Canvas c2 = new Canvas
		{
			Width = 168.0,
			Height = 92.0,
			ClipToBounds = true
		};
		c2.Children.Add(new Border
		{
			Width = 168.0,
			Height = 92.0,
			CornerRadius = new CornerRadius(8.0),
			Background = func(themePalette.Bg)
		});
		Action<UIElement, double, double> action = delegate(UIElement el, double x, double y)
		{
			Canvas.SetLeft(el, x);
			Canvas.SetTop(el, y);
			c2.Children.Add(el);
		};
		action(new Border
		{
			Width = 18.0,
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = func(themePalette.Text)
		}, 8.0, 7.0);
		action(new Border
		{
			Width = 1.0,
			Height = 6.0,
			Background = func(themePalette.BorderStrong)
		}, 30.0, 6.0);
		action(new Border
		{
			Width = 26.0,
			Height = 3.0,
			CornerRadius = new CornerRadius(1.5),
			Background = func(themePalette.Text2)
		}, 34.0, 7.5);
		Color[] array = new Color[6] { themePalette.CScreen, themePalette.CSystem, themePalette.CWeather, themePalette.CFocus, themePalette.CClipboard, themePalette.CNotes };
		for (int i = 0; i < 6; i++)
		{
			double num = 8.0 + (double)(i % 3) * 52.0;
			double num2 = 15.0 + (double)(i / 3) * 32.0;
			action(new Border
			{
				Width = 48.0,
				Height = 28.0,
				CornerRadius = new CornerRadius(5.0),
				Background = func(themePalette.Surface),
				BorderBrush = func(themePalette.Border),
				BorderThickness = new Thickness(0.75)
			}, num, num2);
			action(new Border
			{
				Width = 5.0,
				Height = 5.0,
				CornerRadius = new CornerRadius(themePalette.UseIconTiles ? 1.5 : 2.5),
				Background = func(themePalette.UseIconTiles ? array[i] : themePalette.Icon)
			}, num + 4.0, num2 + 4.0);
			action(new Border
			{
				Width = 14.0,
				Height = 2.0,
				CornerRadius = new CornerRadius(1.0),
				Background = func(themePalette.Text2)
			}, num + 11.0, num2 + 5.5);
			if (i == 1)
			{
				Color[] array2 = new Color[3] { themePalette.Ring1, themePalette.Ring2, themePalette.Ring3 };
				for (int j = 0; j < 3; j++)
				{
					action(new Ellipse
					{
						Width = 11.0,
						Height = 11.0,
						Stroke = func(themePalette.Track),
						StrokeThickness = 1.6
					}, num + 5.0 + (double)(j * 14), num2 + 13.0);
					System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
					path.Stroke = func(array2[j]);
					path.StrokeThickness = 1.6;
					path.StrokeStartLineCap = PenLineCap.Round;
					path.StrokeEndLineCap = PenLineCap.Round;
					path.Data = Geometry.Parse("M 5.5,0.8 A 4.7,4.7 0 1 1 1.2,7.4");
					System.Windows.Shapes.Path arg = path;
					action(arg, num + 5.0 + (double)(j * 14), num2 + 13.0);
				}
				continue;
			}
			action(new Border
			{
				Width = ((i == 3) ? 18 : 20),
				Height = 5.0,
				CornerRadius = new CornerRadius(1.5),
				Background = func(themePalette.Text)
			}, num + 4.0, num2 + 12.0);
			if (i == 0)
			{
				for (int k = 0; k < 6; k++)
				{
					action(new Border
					{
						Width = 2.5,
						Height = ((k == 5) ? 5 : 4),
						CornerRadius = new CornerRadius(1.0),
						Background = func((k == 5) ? themePalette.CScreen : themePalette.Bar)
					}, num + 4.0 + (double)(k * 6), num2 + (double)((k == 5) ? 20 : 21));
				}
			}
			if (i == 3)
			{
				action(new Ellipse
				{
					Width = 10.0,
					Height = 10.0,
					Stroke = func(themePalette.PlayInk),
					StrokeThickness = 1.0
				}, num + 48.0 - 14.0, num2 + 28.0 - 13.0);
			}
			if (i == 2)
			{
				action(new Ellipse
				{
					Width = 8.0,
					Height = 8.0,
					Fill = func(themePalette.SunInk)
				}, num + 48.0 - 12.0, num2 + 11.0);
			}
		}
		action(new Border
		{
			Width = 74.0,
			Height = 11.0,
			CornerRadius = new CornerRadius(5.5),
			Background = func(themePalette.Surface),
			BorderBrush = func(themePalette.Border),
			BorderThickness = new Thickness(0.75)
		}, 47.0, 78.0);
		action(new Ellipse
		{
			Width = 8.0,
			Height = 8.0,
			Fill = func(themePalette.Accent)
		}, 49.0, 79.5);
		for (int l = 1; l < 8; l++)
		{
			action(new Ellipse
			{
				Width = 3.0,
				Height = 3.0,
				Fill = func(themePalette.IconMuted)
			}, 50.0 + (double)(l * 9), 82.0);
		}
		Viewbox viewbox = new Viewbox();
		viewbox.Stretch = Stretch.Uniform;
		viewbox.Child = c2;
		viewbox.IsHitTestVisible = false;
		return viewbox;
	}

	private static UIElement ModulesPage(PageContext ctx)
	{
		StackPanel stackPanel = Page("modules");
		stackPanel.Children.Add(SUI.SectionLabel("Dock"));
		ModuleList list = new ModuleList(ctx);
		stackPanel.Children.Add(list);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel2.Margin = new Thickness(0.0, 16.0, 0.0, 0.0);
		StackPanel stackPanel3 = stackPanel2;
		SButton sButton = new SButton("Reset order");
		sButton.Click += delegate
		{
			if (SUI.Confirm(ctx.Window, "Reset modules?", "Every module goes back to its default place in the dock and is shown again.", "Reset", danger: false))
			{
				ToolRegistry.Instance.ResetToDefaults();
				list.Build();
			}
		};
		stackPanel3.Children.Add(sButton);
		stackPanel.Children.Add(stackPanel3);
		stackPanel.Children.Add(SUI.Note("Tip: drag the handle, or focus a row and press Alt+Up / Alt+Down. Changes show in the dock right away."));
		stackPanel.Children.Add(SUI.SectionLabel("Home widgets"));
		Border widgets = SUI.Group();
		stackPanel.Children.Add(widgets);
		Action refreshWidgets = delegate
		{
			StackPanel stackPanel4 = SUI.GroupStack(widgets);
			stackPanel4.Children.Clear();
			string[] widgetOrder = NotchEditing.WidgetOrder;
			foreach (string text in widgetOrder)
			{
				string id = text;
				SUI.AddToGroup(stackPanel4, SUI.ToggleRow(HomeWidgetCard.Name(id), (NotchEditing.HasRoom || NotchEditing.WidgetVisible(id)) ? "Show on Home" : "Remove a widget to make room", NotchEditing.WidgetVisible(id), delegate(bool v)
				{
					NotchEditing.SetWidgetVisible(id, v);
				}, null, "setting-home-widget-" + id, () => NotchEditing.WidgetVisible(id)));
			}
		};
		NotchEditing.Changed += refreshWidgets;
		ctx.OnClose(delegate
		{
			NotchEditing.Changed -= refreshWidgets;
		});
		refreshWidgets();
		return stackPanel;
	}

	private static bool EventOn(params ActivityType[] types)
	{
		NotchBehaviorConfig b = NotchBehaviorConfig.Instance;
		return types.Any((ActivityType t) => b.GetEventPresentationMode(t) != EventPresentationMode.Off);
	}

	private static void SetEvents(bool on, params ActivityType[] types)
	{
		NotchBehaviorConfig instance = NotchBehaviorConfig.Instance;
		foreach (ActivityType type in types)
		{
			EventPresentationMode eventPresentationMode = instance.GetEventPresentationMode(type);
			if (on && eventPresentationMode == EventPresentationMode.Off)
			{
				instance.SetEventPresentationMode(type, EventPresentationMode.Brief);
			}
			else if (!on && eventPresentationMode != 0)
			{
				instance.SetEventPresentationMode(type, EventPresentationMode.Off);
			}
		}
	}

	private static FrameworkElement Tile(string moduleId, string glyph)
	{
		ModuleInfo moduleInfo = ModuleCatalog.Get(moduleId);
		return IconTile.Create(moduleInfo?.Hue ?? SettingsTheme.Hex("#5A5A5A"), glyph ?? ((moduleInfo != null) ? moduleInfo.Glyph : "flash"), 28.0);
	}

	private static UIElement LiveActivitiesPage(PageContext ctx)
	{
		StackPanel stackPanel = Page("live");
		NotchConfig cfg = NotchConfig.Instance;
		NotchBehaviorConfig beh = NotchBehaviorConfig.Instance;
		stackPanel.Children.Add(SUI.SectionLabel("Music"));
		Grid grid = SUI.ToggleRow("Now playing", "Music from your player", beh.ShowMediaWhenPlaybackStarts, delegate(bool v)
		{
			beh.ShowMediaWhenPlaybackStarts = v;
		}, Tile("home", "play"), "setting-now-playing-037", () => beh.ShowMediaWhenPlaybackStarts);
		Grid grid2 = SUI.ToggleRow("Show browser media in notch", "Includes videos and ads reported by your browser", beh.ShowBrowserMediaInNotch, delegate(bool v)
		{
			beh.ShowBrowserMediaInNotch = v;
		}, Tile("home", "play"), "setting-show-browser-media-in-notch-038", () => beh.ShowBrowserMediaInNotch);
		Grid grid3 = SUI.Row("Music color", "Beat color for now playing", new MusicSwatches(cfg), null, isRightAlignedAction: false, "setting-music-color-039");
		stackPanel.Children.Add(SUI.Group(grid, grid2, grid3));
		stackPanel.Children.Add(SUI.SectionLabel("Charging"));
		stackPanel.Children.Add(SUI.Group(SUI.ToggleRow("Battery charging", "Plugged in, unplugged and fully charged", EventOn(ActivityType.BatteryCharging, ActivityType.BatteryChargingStopped, ActivityType.BatteryFull), delegate(bool v)
		{
			SetEvents(v, ActivityType.BatteryCharging, ActivityType.BatteryChargingStopped, ActivityType.BatteryFull);
		}, Tile("system", "battery_charge"), "setting-battery-charging-040", () => EventOn(ActivityType.BatteryCharging, ActivityType.BatteryChargingStopped, ActivityType.BatteryFull))));
		stackPanel.Children.Add(SUI.SectionLabel("Volume"));
		stackPanel.Children.Add(SUI.Group(SUI.ToggleRow("Volume", "When you change the volume", EventOn(ActivityType.Volume), delegate(bool v)
		{
			SetEvents(v, ActivityType.Volume);
		}, Tile("system", "speaker_2"), "setting-volume-041", () => EventOn(ActivityType.Volume))));
		stackPanel.Children.Add(SUI.SectionLabel("Copied"));
		Grid maskRow = SUI.ToggleRow("Hide copied text", "Show â€œTextâ€ or â€œImageâ€ instead of what you copied", cfg.MaskClipboardToast, delegate(bool v)
		{
			cfg.MaskClipboardToast = v;
		}, new Border
		{
			Width = 28.0,
			Height = 28.0
		}, "setting-hide-copied-text-042", () => cfg.MaskClipboardToast);
		SUI.SetRowEnabled(maskRow, cfg.ShowClipboardToast);
		SSettingBinding.Attach(maskRow, delegate
		{
			SUI.SetRowEnabled(maskRow, cfg.ShowClipboardToast);
		});
		stackPanel.Children.Add(SUI.Group(SUI.ToggleRow("Clipboard copied", "A short confirmation when you copy something", cfg.ShowClipboardToast, delegate(bool v)
		{
			cfg.ShowClipboardToast = v;
			if (v)
			{
				SetEvents(true, ActivityType.ClipboardCopied);
			}
			SUI.SetRowEnabled(maskRow, v);
		}, Tile("clipboard", null), "setting-clipboard-copied-043", () => cfg.ShowClipboardToast), maskRow));
		stackPanel.Children.Add(SUI.SectionLabel("Reminders"));
		stackPanel.Children.Add(SUI.Group(SUI.ToggleRow("Running timers", "Show a running Pomodoro, countdown or stopwatch on the closed notch; click it to open Timers", cfg.ShowTimersInNotch, delegate(bool v)
		{
			cfg.ShowTimersInNotch = v;
		}, Tile("timer", null), "setting-running-timers-117", () => cfg.ShowTimersInNotch), SUI.ToggleRow("Timers", "Pomodoro, countdown and water reminders when they finish", EventOn(ActivityType.TimerComplete, ActivityType.HydrationReminder), delegate(bool v)
		{
			SetEvents(v, ActivityType.TimerComplete, ActivityType.HydrationReminder);
		}, Tile("timer", null), "setting-timers-044", () => EventOn(ActivityType.TimerComplete, ActivityType.HydrationReminder)), SUI.ToggleRow("Calendar alerts", "Upcoming events and reminders that are due", cfg.ShowCalendarInNotch, delegate(bool v)
		{
			cfg.ShowCalendarInNotch = v;
			if (v)
			{
				SetEvents(true, ActivityType.CalendarAlert);
			}
		}, Tile("calendar", null), "setting-calendar-alerts-045", () => cfg.ShowCalendarInNotch), SUI.SegmentedRow("Reminders", "Where upcoming reminders appear", new string[2] { "Notch only", "Notch + Windows" }, new string[2] { "Notch", "NotchAndWindows" }, cfg.Reminders, delegate(string k)
		{
			cfg.Reminders = k;
		}, "setting-reminders-046", () => cfg.Reminders)));
		stackPanel.Children.Add(SUI.SectionLabel("Timing"));
		stackPanel.Children.Add(SUI.Group(SUI.SegmentedRow("Event duration", "How long an event stays before the notch settles", new string[3] { "2 s", "3 s", "5 s" }, new string[3] { "2", "3", "5" }, beh.EventDurationSeconds.ToString(), delegate(string k)
		{
			beh.EventDurationSeconds = int.Parse(k);
		}, "setting-event-duration-047", () => beh.EventDurationSeconds.ToString())));
		return stackPanel;
	}

	private static UIElement PrivacyPage(PageContext ctx)
	{
		StackPanel stackPanel = Page("privacy");
		stackPanel.Children.Add(SUI.Note("Ayn Pet uses only a local app category (never window titles or content), activity, battery, media, timer, and water-goal information already available on this PC to show local greetings and suggestions; this information is not sent anywhere."));
		stackPanel.Children.Add(SUI.Note("Ayn stores your data in your user folder on this PC. Network requests include weather, calendar feeds and analytics sites you connect, plus license-server communication to activate, validate or deactivate your license."));
		AddBackupControls(stackPanel, ctx);
		stackPanel.Children.Add(SUI.SectionLabel("Links"));
		stackPanel.Children.Add(SUI.Group(SUI.ToggleRow("Show website icons in Links", "Download icons directly from websites you save", AppPreferences.Instance.ShowWebsiteIconsInLinks, delegate(bool v)
		{
			AppPreferences.Instance.ShowWebsiteIconsInLinks = v;
		}, null, "setting-links-website-icons", () => AppPreferences.Instance.ShowWebsiteIconsInLinks)));
		stackPanel.Children.Add(SUI.SectionLabel("Stored on this PC"));
		Border border = SUI.Group();
		Dictionary<string, TextBlock> sizeLabels = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
		bool alive = true;
		ctx.OnClose(delegate
		{
			alive = false;
		});
		ClipboardService clip = ClipboardService.Instance;
		NotchBehaviorConfig beh = NotchBehaviorConfig.Instance;
		TextBlock clipSizeText = null;
		Action action = delegate
		{
			Task.Run(delegate
			{
				Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
				foreach (KeyValuePair<string, string[]> dataArea in DataAreas)
				{
					dictionary[dataArea.Key] = SettingsData.GetAreaBytes(dataArea.Key);
				}
				return dictionary;
			}).ContinueWith(delegate(Task<Dictionary<string, long>> t)
			{
				if (alive && t.Result != null)
				{
					Dictionary<string, long> result = t.Result;
					foreach (KeyValuePair<string, TextBlock> item in sizeLabels)
					{
						item.Value.Text = ((result.TryGetValue(item.Key, out var value) && value > 0) ? SettingsData.FormatBytes(value) : "Nothing yet");
					}
					if (clipSizeText != null)
					{
						clipSizeText.Text = clip.GetTotalSizeFormatted();
					}
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		};
		foreach (KeyValuePair<string, string[]> dataArea2 in DataAreas)
		{
			string key = dataArea2.Key;
			string title = dataArea2.Value[0];
			string description = dataArea2.Value[1];
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			stackPanel2.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel3 = stackPanel2;
			TextBlock textBlock = SettingsTheme.Text("â€¦", 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock.TextWrapping = TextWrapping.NoWrap;
			textBlock.Margin = new Thickness(0.0, 0.0, 16.0, 0.0);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			sizeLabels[key] = textBlock;
			stackPanel3.Children.Add(textBlock);
			SButton sButton = new SButton("Clear");
			sButton.VerticalAlignment = VerticalAlignment.Center;
			sButton.Click += delegate
			{
				if (SUI.Confirm(ctx.Window, "Clear " + title + "?", "Ayn will restart and remove data stored for " + title + " together. If the operation cannot finish, your existing data is preserved.", "Clear and restart", danger: true))
				{
					if (SettingsData.TryClearArea(key, out var message2))
					{
						try
						{
							SettingsData.Relaunch();
							return;
						}
						catch (Exception ex5)
						{
							SUI.Confirm(ctx.Window, "Couldn't restart", ex5.Message, "OK", danger: false);
							return;
						}
					}
					SUI.Confirm(ctx.Window, "Couldn't clear data", message2, "OK", danger: false);
				}
			};
			stackPanel3.Children.Add(sButton);
			SUI.AddToGroup(SUI.GroupStack(border), SUI.Row(title, description, stackPanel3, null, isRightAlignedAction: true, "setting-privacy-clear-" + key));
		}
		stackPanel.Children.Add(border);
		action();
		stackPanel.Children.Add(SUI.SectionLabel("Clipboard"));
		string selected = clip.RetentionDays switch
		{
			0 => "0", 
			7 => "7", 
			_ => "30", 
		};
		Grid grid = SUI.SegmentedRow("Keep history for", "Unpinned items older than this are deleted automatically", new string[3] { "7 days", "30 days", "Forever" }, new string[3] { "7", "30", "0" }, selected, delegate(string k)
		{
			clip.RetentionDays = int.Parse(k);
			clip.TriggerMaintenance();
		}, "setting-keep-history-for-049", () => (clip.RetentionDays != 7) ? ((clip.RetentionDays != 0) ? "30" : "0") : "7");
		clipSizeText = SettingsTheme.Text(clip.GetTotalSizeFormatted(), 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		clipSizeText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		clipSizeText.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Children.Add(SUI.Group(SUI.ToggleRow("Clipboard history", "Record text, links and images you copy so you can search them later", clip.IsMonitoring, delegate(bool v)
		{
			clip.IsMonitoring = v;
		}, null, "setting-clipboard-history-050", () => clip.IsMonitoring), grid, SUI.Row("Storage size", "Total space used by clipboard history and images on this PC", clipSizeText, null, isRightAlignedAction: true, "setting-storage-size-051"), SUI.ToggleRow("Idle privacy reset", "After a minute away, the notch returns to Home if Clipboard or Notes was open", beh.IdlePrivacyReset, delegate(bool v)
		{
			beh.IdlePrivacyReset = v;
		}, null, "setting-idle-privacy-reset-052", () => beh.IdlePrivacyReset)));
		stackPanel.Children.Add(SUI.SectionLabel("Location"));
		string text = LocationPermission();
		SButton sButton2 = new SButton("Location settings", SButtonKind.Secondary, "external");
		sButton2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton2.Click += delegate
		{
			AppLinks.Open("ms-settings:privacy-location");
		};
		WeatherService instance = WeatherService.Instance;
		stackPanel.Children.Add(SUI.Group(SUI.Row("Weather location", "Windows location is " + text + ". " + (instance.FollowLocation ? "Weather follows your location; without it, Ayn estimates your city from your network." : "Weather uses the city you picked, so location isn't needed."), sButton2, IconTile.Create(SettingsTheme.Hex("#38BDF8"), "location", 28.0), isRightAlignedAction: true, "setting-weather-location-053")));
		stackPanel.Children.Add(SUI.SectionLabel("Your settings"));
		SButton export = new SButton("Exportâ€¦");
		export.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		export.Click += async delegate
		{
			Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Ayn settings (*.json)|*.json",
				FileName = "Ayn settings " + DateTime.Now.ToString("yyyy-MM-dd") + ".json",
				Title = "Export Ayn settings"
			};
			if (dlg.ShowDialog(ctx.Window) != true)
			{
				return;
			}
			bool wasEnabled = ctx.Window.IsEnabled;
			try
			{
				int num = default(int);
				_ = num;
				_ = 0;
				try
				{
					PersistenceHelper.PrepareExit();
					export.IsEnabled = false;
					ctx.Window.IsEnabled = false;
					await Task.Run(() => SettingsData.ExportAll(dlg.FileName));
					if (ctx.CanUpdateUi)
					{
						export.Text = "Exported";
					}
				}
				catch (Exception ex4)
				{
					if (ctx.CanUpdateUi)
					{
						SUI.Confirm(ctx.Window, "Export failed", ex4.Message, "OK", danger: false);
					}
				}
			}
			finally
			{
				if (ctx.CanUpdateUi)
				{
					export.IsEnabled = true;
					ctx.Window.IsEnabled = wasEnabled;
				}
			}
		};
		SButton sButton3 = new SButton("Importâ€¦");
		sButton3.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton3.Click += delegate
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Ayn settings (*.json)|*.json",
				Title = "Import Ayn settings"
			};
			bool? flag = openFileDialog.ShowDialog(ctx.Window);
			if (flag.GetValueOrDefault() && flag.HasValue && SUI.Confirm(ctx.Window, "Import settings?", "Your current settings are replaced and Ayn restarts. Notes, clipboard history and other content are not touched.", "Import and restart", danger: false))
			{
				if (SettingsData.ImportAll(openFileDialog.FileName, out var message))
				{
					try
					{
						SettingsData.Relaunch();
						return;
					}
					catch (Exception ex3)
					{
						SUI.Confirm(ctx.Window, "Couldn't restart", ex3.Message, "OK", danger: false);
						return;
					}
				}
				SUI.Confirm(ctx.Window, "Couldn't import", message, "OK", danger: false);
			}
		};
		SButton sButton4 = new SButton("Open", SButtonKind.Secondary, "folder_open");
		sButton4.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton4.Click += delegate
		{
			try
			{
				string openableDataDir = AppPaths.OpenableDataDir;
				Directory.CreateDirectory(openableDataDir);
				Process.Start("explorer.exe", "\"" + openableDataDir + "\"");
			}
			catch (Exception ex2)
			{
				TransactionLogger.Warn("Open data folder: " + ex2.Message);
			}
		};
		stackPanel.Children.Add(SUI.Group(SUI.Row("Export settings", "Save every setting to one file, e.g. to move to another PC", export, null, isRightAlignedAction: true, "setting-export-settings-054"), SUI.Row("Import settings", "Load settings from an exported file", sButton3, null, isRightAlignedAction: true, "setting-import-settings-055"), SUI.Row("Open data folder", AppPaths.OpenableDataDir, sButton4, null, isRightAlignedAction: true, "setting-open-data-folder-056")));
		stackPanel.Children.Add(SUI.SectionLabel("Start over"));
		SButton sButton5 = new SButton("Clear all dataâ€¦", SButtonKind.Danger);
		sButton5.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton5.Click += delegate
		{
			if (!SUI.Confirm(ctx.Window, "Clear all Ayn data?", "Notes, clipboard history, shelf files, calendar events, connected sites and every setting are deleted from this PC, then Ayn restarts as new. Your license stays activated with the store you bought from. This can't be undone.", "Clear everything", danger: true))
			{
				return;
			}
			try
			{
				SettingsData.ScheduleReset();
				SettingsData.Relaunch();
			}
			catch (Exception ex)
			{
				SUI.Confirm(ctx.Window, "Couldn't clear data", ex.Message, "OK", danger: false);
			}
		};
		stackPanel.Children.Add(SUI.Group(SUI.Row("Clear all data", "Delete everything Ayn stored and restart", sButton5, null, isRightAlignedAction: true, "setting-clear-all-data-057")));
		return stackPanel;
	}

	private static void AddBackupControls(StackPanel page, PageContext ctx)
	{
		bool replace = false;
		page.Children.Add(SUI.SectionLabel("Backup & restore"));
		page.Children.Add(SUI.Note("Backups stay on this PC unless you move them. Save a backup before uninstalling or moving to another PC. Backups are not encrypted: keep them somewhere private. Licence keys, integration secrets, calendar feed URLs, trial files and logs are excluded."));
		SButton sButton = new SButton("Export backupâ€¦");
		SButton sButton2 = new SButton("Import backupâ€¦");
		TextBlock status = SettingsTheme.Text("No backup exported this session", 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		sButton.Click += async delegate
		{
			Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Ayn backup (*.aynbackup)|*.aynbackup",
				FileName = "Ayn-" + DateTime.Now.ToString("yyyy-MM-dd") + ".aynbackup",
				Title = "Export Ayn backup"
			};
			if (dialog.ShowDialog(ctx.Window) != true)
			{
				return;
			}
			try
			{
				int num = default(int);
				_ = num;
				_ = 0;
				try
				{
					PersistenceHelper.PrepareExit();
					ctx.Window.IsEnabled = false;
					bool images = AppPreferences.Instance.BackupIncludeClipboardImages;
					bool copies = AppPreferences.Instance.BackupIncludeShelfFiles;
					long size = await Task.Run(() => UserBackupService.Export(dialog.FileName, images, copies));
					if (ctx.CanUpdateUi)
					{
						status.Text = "Backup saved Â· " + SettingsData.FormatBytes(size);
					}
				}
				catch (Exception ex2)
				{
					if (ctx.CanUpdateUi)
					{
						SUI.Confirm(ctx.Window, "Backup failed", ex2.Message, "OK", danger: false);
					}
				}
			}
			finally
			{
				if (ctx.CanUpdateUi)
				{
					ctx.Window.IsEnabled = true;
				}
			}
		};
		sButton2.Click += delegate
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Ayn backup (*.aynbackup)|*.aynbackup",
				Title = "Import Ayn backup"
			};
			if (openFileDialog.ShowDialog(ctx.Window) != true)
			{
				return;
			}
			try
			{
				UserBackupService.Bundle bundle = UserBackupService.Read(openFileDialog.FileName);
				string message = SettingsData.FormatBytes(new FileInfo(openFileDialog.FileName).Length) + " Â· " + bundle.CreatedUtc + "\n\n" + UserBackupService.Preview(bundle) + "\n\n" + (replace ? "Replace the stores included in this backup. Missing stores remain unchanged." : "Merge items by ID. Existing values win conflicts; new items are added.") + " Ayn will restart to restore all tools together.";
				if (!SUI.Confirm(ctx.Window, "Restore backup?", message, (replace ? "Replace" : "Merge") + " and restart", replace))
				{
					return;
				}
				PersistenceHelper.PrepareExit();
				if (!PersistenceHelper.FlushForExit())
				{
					throw new IOException("Pending data could not be saved. Restore cancelled.");
				}
				UserBackupService.QueueRestore(bundle, replace);
				try
				{
					SettingsData.Relaunch();
				}
				catch
				{
					SettingsTransaction.CancelQueued();
					throw;
				}
			}
			catch (Exception ex)
			{
				SUI.Confirm(ctx.Window, "Couldnâ€™t restore backup", "Your data has been preserved. " + ex.Message, "OK", danger: false);
			}
		};
		page.Children.Add(SUI.Group(SUI.ToggleRow("Include clipboard images", "Text history is always included; password-manager exclusions still apply", AppPreferences.Instance.BackupIncludeClipboardImages, delegate(bool v)
		{
			AppPreferences.Instance.BackupIncludeClipboardImages = v;
		}, null, "setting-backup-images", () => AppPreferences.Instance.BackupIncludeClipboardImages), SUI.ToggleRow("Include copies of shelf files", "Otherwise only the list is saved; original files may be unavailable on another PC", AppPreferences.Instance.BackupIncludeShelfFiles, delegate(bool v)
		{
			AppPreferences.Instance.BackupIncludeShelfFiles = v;
		}, null, "setting-backup-shelf-files", () => AppPreferences.Instance.BackupIncludeShelfFiles), SUI.Row("Export backup", "Notes, clipboard, calendar, links, shelf, timers, water, widgets, dock and settings", sButton, null, isRightAlignedAction: true, "setting-export-backup"), SUI.Row("Last export", "Compressed backup file size", status, null, isRightAlignedAction: true, "setting-backup-size"), SUI.SegmentedRow("Restore mode", "Merge keeps existing values; Replace uses the backed-up stores", new string[2] { "Merge", "Replace" }, new string[2] { "merge", "replace" }, "merge", delegate(string v)
		{
			replace = v == "replace";
		}, "setting-backup-mode"), SUI.Row("Import backup", "Preview counts before restoring", sButton2, null, isRightAlignedAction: true, "setting-import-backup"), SUI.ToggleRow("Automatic weekly backup", "While Ayn runs Â· Documents\\Ayn Backups Â· last 4 Â· text and lists only", AppPreferences.Instance.AutomaticWeeklyBackup, delegate(bool v)
		{
			AppPreferences.Instance.AutomaticWeeklyBackup = v;
		}, null, "setting-backup-weekly", () => AppPreferences.Instance.AutomaticWeeklyBackup)));
	}

	private static string LocationPermission()
	{
		try
		{
			string a = Read(Registry.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location");
			string a2 = Read(Registry.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location");
			string a3 = Read(Registry.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location\\NonPackaged");
			if (string.Equals(a, "Deny", StringComparison.OrdinalIgnoreCase))
			{
				return "off for this device";
			}
			if (string.Equals(a2, "Deny", StringComparison.OrdinalIgnoreCase))
			{
				return "off";
			}
			if (!AppPaths.IsPackaged && string.Equals(a3, "Deny", StringComparison.OrdinalIgnoreCase))
			{
				return "blocked for desktop apps";
			}
			return "allowed";
		}
		catch
		{
			return "unknown";
		}
	}

	private static string Read(RegistryKey root, string path)
	{
		using RegistryKey registryKey = root.OpenSubKey(path, writable: false);
		return (registryKey != null) ? (registryKey.GetValue("Value") as string) : null;
	}

	private static UIElement LicenseAndUpdatesPage(PageContext ctx)
	{
		StackPanel page = Page("license");
		page.Children.Add(SUI.SectionLabel("Ayn"));
		page.Children.Add(SUI.Note("Ayn is an open-source Windows app by SabrWare. No account, no purchase, no subscription - everything is available to everyone."));
		page.Children.Add(SUI.SectionLabel("Updates"));
		page.Children.Add(SUI.Note("Version " + SettingsPages.ShortVersion() + ". Built " + SettingsPages.BuildString()));
		page.Children.Add(SUI.SectionLabel("Source"));
		page.Children.Add(SUI.Note("Source code and issue tracker: https://github.com/"));
		return page;
	}
	internal static string VersionString()
	{
		try
		{
			var attr = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(SettingsPages).Assembly, typeof(AssemblyInformationalVersionAttribute));
			return (attr != null) ? attr.InformationalVersion : typeof(SettingsPages).Assembly.GetName().Version.ToString();
		}
		catch { return "1.0"; }
	}

	private static void ReadStamp(out string number, out string commit, out string time)
	{
		number = commit = time = null;
		try
		{
			foreach (AssemblyMetadataAttribute a in Attribute.GetCustomAttributes(typeof(SettingsPages).Assembly, typeof(AssemblyMetadataAttribute)))
			{
				if (a.Key == "BuildNumber") number = a.Value;
				else if (a.Key == "Commit") commit = a.Value;
				else if (a.Key == "BuildTime") time = a.Value;
			}
		}
		catch { }
	}

	internal static string BuildString()
	{
		ReadStamp(out var number, out var commit, out var time);
		if (number == null && commit == null) return "Development build";
		return string.Format("Build {0} Â· {1} Â· {2}", number ?? "?", commit ?? "?", time ?? "?");
	}

	internal static string VersionLine()
	{
		ReadStamp(out var number, out var commit, out var _);
		string text = "Version " + ShortVersion();
		if (number == null && commit == null) return text + " Â· dev build";
		return text + " Â· " + (number ?? "?") + " (" + (commit ?? "?") + ")";
	}

	internal static string ShortVersion()
	{
		string text = VersionString();
		int num = text.IndexOf(' ');
		return num <= 0 ? text : text.Substring(0, num);
	}
	private static UIElement AboutPage(PageContext ctx)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.MaxWidth = 616.0;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
		stackPanel.Margin = new Thickness(0.0, 0.0, 0.0, 40.0);
		StackPanel stackPanel2 = stackPanel;
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel3.Margin = new Thickness(0.0, 16.0, 0.0, 24.0);
		StackPanel stackPanel4 = stackPanel3;
		FrameworkElement frameworkElement = BrandAssets.CreateLogo(64.0);
		frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel4.Children.Add(frameworkElement);
		TextBlock textBlock = SettingsTheme.Text("Ayn", 28.0, SettingsTheme.TextPrimary, AynType.Semibold);
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		stackPanel4.Children.Add(textBlock);
		TextBlock textBlock2 = SettingsTheme.Text("The notch for Windows", 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock2.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel4.Children.Add(textBlock2);
		ReadStamp(out var number, out var commit, out var time);
		string text2 = string.Format("Version {0} ({1})", ShortVersion(), number ?? "57");
		TextBlock textBlock3 = SettingsTheme.Text(text2, 12.0, SettingsTheme.TextLabel, FontWeights.Normal);
		textBlock3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock3.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		if (!string.IsNullOrEmpty(commit))
		{
			textBlock3.ToolTip = string.Format("Commit {0}{1}", commit, (!string.IsNullOrEmpty(time)) ? (" Â· " + time) : "");
		}
		stackPanel4.Children.Add(textBlock3);
		stackPanel2.Children.Add(stackPanel4);
		FrameworkElement frameworkElement2 = SUI.SectionLabel("Ayn");
		frameworkElement2.Margin = new Thickness(0.0, 16.0, 0.0, 12.0);
		stackPanel2.Children.Add(frameworkElement2);
		Func<string, string, SButton> func = delegate(string text, string url)
		{
			SButton sButton2 = new SButton(text);
			sButton2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			sButton2.Click += delegate
			{
				AppLinks.Open(url);
			};
			return sButton2;
		};
		TextBlock textBlock4 = SettingsTheme.Text("SabrWare", 15.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock4.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		textBlock4.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(SUI.Group(SUI.Row("Made by", null, textBlock4, null, isRightAlignedAction: true, "setting-made-by-064"), SUI.Row("Source code", "github.com/salahuddinselim/Ayn", func("Open", "https://github.com/salahuddinselim/Ayn"), null, isRightAlignedAction: true, "setting-website-065"), SUI.Row("Issues", "Report a bug or request a feature", func("Open", "https://github.com/salahuddinselim/Ayn/issues"), null, isRightAlignedAction: true, "setting-support-066")));
		if (AppPaths.IsPackaged || StoreUpdateService.ForceEnabled)
		{
			FrameworkElement frameworkElement3 = SUI.SectionLabel("Updates");
			frameworkElement3.Margin = new Thickness(0.0, 24.0, 0.0, 12.0);
			stackPanel2.Children.Add(frameworkElement3);
			bool updateAvailable = StoreUpdateService.UpdateAvailable;
			SButton action = new SButton(updateAvailable ? "Update" : "Check now", updateAvailable ? SButtonKind.Primary : SButtonKind.Secondary, updateAvailable ? "store" : null);
			action.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			bool alive = true;
			action.Click += async delegate
			{
				action.IsEnabled = false;
				if (!StoreUpdateService.UpdateAvailable)
				{
					await StoreUpdateService.CheckNowAsync();
				}
				else
				{
					await StoreUpdateService.UpdateNowAsync();
				}
				if (alive)
				{
					action.IsEnabled = true;
				}
			};
			string description = ((!updateAvailable) ? ((StoreUpdateService.State == StoreUpdateState.UpToDate) ? ("Version " + ShortVersion() + " is the latest.") : "Microsoft Store delivers new versions.") : (string.IsNullOrWhiteSpace(StoreUpdateService.AvailableVersion) ? "An update is ready in Microsoft Store." : ("Ayn " + StoreUpdateService.AvailableVersion + " is ready in Microsoft Store.")));
			stackPanel2.Children.Add(SUI.Group(SUI.Row(StoreUpdateService.StatusTitle, description, action, null, isRightAlignedAction: true, "setting-store-update-status")));
			EventHandler changed = delegate
			{
				if (alive)
				{
					ctx.Window.Dispatcher.BeginInvoke((Action)delegate
					{
						if (alive)
						{
							ctx.Window.InvalidatePage("about");
						}
					});
				}
			};
			StoreUpdateService.Changed += changed;
			ctx.OnClose(delegate
			{
				alive = false;
				StoreUpdateService.Changed -= changed;
			});
		}
		FrameworkElement frameworkElement4 = SUI.SectionLabel("Diagnostics");
		frameworkElement4.Margin = new Thickness(0.0, 24.0, 0.0, 12.0);
		stackPanel2.Children.Add(frameworkElement4);
		SButton copyDiagnostics = new SButton("Copy diagnostics");
		copyDiagnostics.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		copyDiagnostics.Click += delegate
		{
			copyDiagnostics.Text = (CrashNotice.CopyDiagnostics() ? "Copied" : "Try again");
		};
		stackPanel2.Children.Add(SUI.Group(SUI.Row("Copy diagnostics", "Version, Windows and a recent exception summary. No personal content.", copyDiagnostics, null, isRightAlignedAction: true, "setting-copy-crash-diagnostics")));
		SButton sendDiagnostics = new SButton("Send diagnostics");
		sendDiagnostics.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sendDiagnostics.Click += delegate
		{
			string report = CrashDiagnostics.CopyText();
			if (SUI.ReviewDiagnostics(ctx.Window, report))
			{
				sendDiagnostics.Text = CrashNotice.Send(report);
			}
		};
		stackPanel2.Children.Add(SUI.Group(SUI.Row("Send diagnostics", CrashDiagnostics.HasPendingNotice() ? "Ayn didn't close normally last time. Review the short report, then send it by email." : "Review the short report, then send it to support by email.", sendDiagnostics, null, isRightAlignedAction: true, "setting-send-crash-diagnostics")));
		SButton export = new SButton("Export");
		export.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		bool exporting = false;
		DispatcherTimer exportReset = null;
		Action stopReset = delegate
		{
			if (exportReset != null)
			{
				exportReset.Stop();
				exportReset = null;
			}
		};
		ctx.OnLeave(stopReset);
		ctx.OnClose(stopReset);
		ctx.OnReturn(delegate
		{
			if (!exporting)
			{
				export.Text = "Export";
				export.IsEnabled = true;
			}
		});
		export.Click += delegate
		{
			if (!exporting && ctx.CanUpdateUi)
			{
				stopReset();
				exporting = true;
				export.IsEnabled = false;
				export.Text = "Exportingâ€¦";
				Dispatcher ui = export.Dispatcher;
				ThreadPool.QueueUserWorkItem(delegate
				{
					string path = null;
					try
					{
						path = DiagnosticsExport.Export(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
					}
					catch (Exception ex)
					{
						TransactionLogger.Warn("Export diagnostics failed: " + ex.GetType().Name + ": " + ex.Message);
					}
					if (ui.HasShutdownStarted || ui.HasShutdownFinished)
					{
						return;
					}
					try
					{
						ui.BeginInvoke((Action)delegate
						{
							exporting = false;
							if (ctx.CanUpdateUi)
							{
								export.IsEnabled = true;
								export.Text = ((path != null) ? "Saved to Desktop" : "Couldn't save");
								if (path != null)
								{
									try
									{
										Process.Start("explorer.exe", "/select,\"" + path + "\"");
									}
									catch
									{
									}
								}
								exportReset = new DispatcherTimer
								{
									Interval = TimeSpan.FromSeconds(5.0)
								};
								exportReset.Tick += delegate
								{
									stopReset();
									if (ctx.CanUpdateUi)
									{
										export.Text = "Export";
									}
								};
								exportReset.Start();
							}
						});
					}
					catch (InvalidOperationException ex2)
					{
						if (!ui.HasShutdownStarted && !ui.HasShutdownFinished)
						{
							TransactionLogger.Warn("Export completion couldn't reach Settings: " + ex2.Message);
						}
					}
				});
			}
		};
		SButton sButton = new SButton("Show");
		sButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton.Click += delegate
		{
			OnboardingWindow.ShowReplay();
		};
		stackPanel2.Children.Add(SUI.Group(SUI.Row("Welcome tour", "Replay the first-run introduction. Your settings stay as they are.", sButton, null, isRightAlignedAction: true, "setting-welcome-tour-069"), SUI.Row("Export diagnostics", "Saves a zip with Ayn's logs and a system summary to your Desktop, to send to support. No personal content.", export, null, isRightAlignedAction: true, "setting-export-diagnostics-070")));
		FrameworkElement frameworkElement5 = SUI.SectionLabel("Third-party licenses");
		frameworkElement5.Margin = new Thickness(0.0, 24.0, 0.0, 12.0);
		stackPanel2.Children.Add(frameworkElement5);
		stackPanel2.Children.Add(SUI.Group(LicenseRow("Inter", "Typeface by Rasmus Andersson Â· SIL Open Font License 1.1", "Ayn.Licenses.Inter-OFL.txt"), LicenseRow("Geist", "Typeface by Vercel Â· SIL Open Font License 1.1", "Ayn.Licenses.Geist-OFL.txt"), LicenseRow("Microsoft Fluent Emoji", "Color notch illustrations Â· MIT License", "Ayn.Licenses.FluentEmoji-MIT.txt"), LicenseRow("Fluent UI System Icons", "Filled notch vectors by Microsoft Â· MIT License", "Ayn.Licenses.FluentSystemIcons-MIT.txt")));
		TextBlock textBlock5 = SettingsTheme.Text("Â© " + DateTime.Now.Year + " SabrWare. All rights reserved.", 13.5, SettingsTheme.TextLabel, FontWeights.Normal);
		textBlock5.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock5.Margin = new Thickness(0.0, 24.0, 0.0, 40.0);
		stackPanel2.Children.Add(textBlock5);
		return stackPanel2;
	}

	private static UIElement LicenseRow(string title, string desc, string resource)
	{
		StackPanel stackPanel = new StackPanel();
		Border text = new Border
		{
			Background = SettingsTheme.InputBg,
			CornerRadius = new CornerRadius(6.0),
			Margin = new Thickness(16.0, 0.0, 16.0, 12.0),
			Padding = new Thickness(12.0, 12.0, 12.0, 12.0),
			MaxHeight = 240.0,
			Visibility = Visibility.Collapsed
		};
		SButton view = new SButton("View");
		view.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		view.Click += delegate
		{
			if (text.Child == null)
			{
				string text2 = ReadResource(resource) ?? "The license text isn't bundled with this build.";
				TextBlock textBlock = SettingsTheme.Text(text2, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
				AynType.Tabular(textBlock);
				text.Child = new BuddyScrollViewer
				{
					Content = textBlock,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto
				};
			}
			bool flag = text.Visibility != Visibility.Visible;
			text.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			view.Text = (flag ? "Hide" : "View");
		};
		stackPanel.Children.Add(SUI.Row(title, desc, view, null, isRightAlignedAction: true, "setting-license-text-" + resource));
		stackPanel.Children.Add(text);
		return stackPanel;
	}

	private static string ReadResource(string name)
	{
		try
		{
			using Stream stream = typeof(SettingsPages).Assembly.GetManifestResourceStream(name);
			if (stream == null)
			{
				return null;
			}
			using StreamReader streamReader = new StreamReader(stream);
			return streamReader.ReadToEnd();
		}
		catch
		{
			return null;
		}
	}

	private static UIElement ModulePage(string id, PageContext ctx)
	{
		IBuddyTool tool = ToolRegistry.Instance.GetTool(id);
		if (tool == null)
		{
			return GeneralPage(ctx);
		}
		ModuleInfo moduleInfo = ModuleCatalog.For(tool);
		StackPanel stackPanel = new StackPanel();
		stackPanel.MaxWidth = 616.0;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
		stackPanel.Margin = new Thickness(0.0, 0.0, 0.0, 40.0);
		StackPanel stackPanel2 = stackPanel;
		SButton sButton = new SButton("Modules", SButtonKind.Ghost, "chevron_left");
		sButton.Margin = new Thickness(-8.0, 0.0, 0.0, 8.0);
		sButton.Click += delegate
		{
			ctx.Navigate("modules");
		};
		stackPanel2.Children.Add(sButton);
		stackPanel2.Children.Add(SUI.PageHeader(moduleInfo.Name, moduleInfo.Description, IconTile.Create(moduleInfo.Hue, moduleInfo.Glyph, 28.0)));
		stackPanel2.Children.Add(SUI.SectionLabel("In the notch"));
		SButton sButton2 = new SButton("Open in notch", SButtonKind.Secondary, "external");
		sButton2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton2.Click += delegate
		{
			CapsuleWindow.Instance?.OpenToolFromSettings(id);
		};
		TextBlock dockFull = SUI.Note("Your dock is full. Remove a tool to add another.");
		dockFull.Foreground = SettingsTheme.Danger;
		dockFull.Visibility = Visibility.Collapsed;
		dockFull.Tag = "setting-dock-full-note";
		Grid grid = SUI.ToggleRow((id == "drop") ? "Ayn Drop" : "Show in dock", (id == "drop") ? "Turn off to stop local sharing and remove the drop target" : "Hidden modules keep working; they just leave the dock", tool.IsEnabled, delegate(bool v)
		{
			if (v && !ToolRegistry.Instance.CanAddToDock(id))
			{
				dockFull.Visibility = Visibility.Visible;
				return;
			}
			dockFull.Visibility = Visibility.Collapsed;
			ctx.Window.SuppressToolsRefresh(on: true);
			try
			{
				ToolRegistry.Instance.SetToolEnabled(id, v);
			}
			finally
			{
				ctx.Window.SuppressToolsRefresh(on: false);
			}
		}, null, "setting-show-in-dock-072", () => tool.IsEnabled);
		stackPanel2.Children.Add(SUI.Group(grid, SUI.Row("Open " + moduleInfo.Name, "Opens the notch on this tab", sButton2, null, isRightAlignedAction: true, "setting-open-073")));
		stackPanel2.Children.Add(dockFull);
		switch (id)
		{
		case "drop":
			DropSection(stackPanel2, ctx);
			break;
		case "clock":
			ClockSettingsSection(stackPanel2);
			break;
		case "clipboard":
			ClipboardSection(stackPanel2, ctx);
			break;
		case "timer":
			TimerSection(stackPanel2, ctx);
			break;
		case "calendar":
			CalendarSection(stackPanel2, ctx);
			break;
		case "weather":
			WeatherSection(stackPanel2, ctx);
			break;
		case "screentime":
			ScreenTimeSection(stackPanel2, ctx);
			break;
		case "analytics":
			AnalyticsSection(stackPanel2, ctx);
			break;
		case "aicoding":
			AICodingSection(stackPanel2, ctx);
			break;
		default:
			stackPanel2.Children.Add(SUI.Note(moduleInfo.Name + " has no other settings. Everything else happens right in its tab."));
			break;
		}
		return stackPanel2;
	}

	private static List<KeyValuePair<string, string>> NumberItems(IEnumerable<int> values, string unit)
	{
		return values.Select((int v) => new KeyValuePair<string, string>(v.ToString(CultureInfo.InvariantCulture), v + unit)).ToList();
	}

	private static List<KeyValuePair<string, string>> WithCurrent(List<KeyValuePair<string, string>> items, int current, string unit)
	{
		string key = current.ToString(CultureInfo.InvariantCulture);
		if (!items.Any((KeyValuePair<string, string> kv) => kv.Key == key))
		{
			items.Add(new KeyValuePair<string, string>(key, current + unit));
			items = items.OrderBy((KeyValuePair<string, string> kv) => int.Parse(kv.Key, CultureInfo.InvariantCulture)).ToList();
		}
		return items;
	}

	private static void DropSection(StackPanel page, PageContext ctx)
	{
		page.Children.Add(SUI.SectionLabel("Receiving"));
		Grid grid = SUI.ToggleRow("Require an upload PIN", "Your phone enters a 4-digit code shown next to the QR before it can send files", AppPreferences.Instance.DropRequirePin, delegate(bool v)
		{
			AppPreferences.Instance.DropRequirePin = v;
		}, null, "setting-drop-pin", () => AppPreferences.Instance.DropRequirePin);
		SButton sButton = new SButton("Changeâ€¦", SButtonKind.Secondary, "folder");
		sButton.Click += delegate
		{
			DropTool.PickSaveFolder();
			ctx.Reload();
		};
		page.Children.Add(SUI.Group(grid, SUI.Row("Save folder", DropTool.SaveFolder, sButton, null, isRightAlignedAction: true, "setting-drop-folder")));
	}

	private static void ClockSettingsSection(StackPanel page)
	{
		ClockSettings c = ClockSettings.Instance;
		page.Children.Add(SUI.SectionLabel("Clock"));
		List<Grid> analogRows = new List<Grid>
		{
			SUI.ToggleRow("Second hand", null, c.AnalogShowSecondHand, delegate(bool v)
			{
				c.AnalogShowSecondHand = v;
			}, null, "setting-second-hand-074", () => c.AnalogShowSecondHand),
			SUI.ToggleRow("Numerals", null, c.AnalogShowNumerals, delegate(bool v)
			{
				c.AnalogShowNumerals = v;
			}, null, "setting-numerals-075", () => c.AnalogShowNumerals),
			SUI.ToggleRow("Minute ticks", null, c.AnalogShowMinuteTicks, delegate(bool v)
			{
				c.AnalogShowMinuteTicks = v;
			}, null, "setting-minute-ticks-076", () => c.AnalogShowMinuteTicks)
		};
		Action<bool> setAnalog = delegate(bool on)
		{
			foreach (Grid item in analogRows)
			{
				SUI.SetRowEnabled(item, on);
			}
		};
		Border border = SUI.Group(SUI.SegmentedRow("Face", "How the Clock tab shows the time", new string[2] { "Digital", "Analog" }, new string[2] { "Digital", "Analog" }, c.DisplayMode.ToString(), delegate(string k)
		{
			c.DisplayMode = (ClockDisplayMode)Enum.Parse(typeof(ClockDisplayMode), k);
			setAnalog(k == "Analog");
		}, "setting-face-077", () => c.DisplayMode.ToString()), SUI.ToggleRow("Show seconds", "In the Clock tab and on the resting notch", c.ShowSeconds, delegate(bool v)
		{
			c.ShowSeconds = v;
		}, null, "setting-show-seconds-078", () => c.ShowSeconds), SUI.ToggleRow("Show date", "Under the time in the Clock tab", c.ShowDate, delegate(bool v)
		{
			c.ShowDate = v;
		}, null, "setting-show-date-079", () => c.ShowDate), SUI.DropdownRow("Date format", null, SUI.Items("Full", ClockSettings.FormatDate(SampleDate, ClockDateFormat.Full), "Standard", ClockSettings.FormatDate(SampleDate, ClockDateFormat.Standard), "Compact", ClockSettings.FormatDate(SampleDate, ClockDateFormat.Compact)), c.DateFormat.ToString(), delegate(string k)
		{
			c.DateFormat = (ClockDateFormat)Enum.Parse(typeof(ClockDateFormat), k);
		}, 230.0, "setting-date-format-080", () => c.DateFormat.ToString()));
		foreach (Grid item2 in analogRows)
		{
			SUI.AddToGroup(SUI.GroupStack(border), item2);
		}
		setAnalog(c.DisplayMode == ClockDisplayMode.Analog);
		SSettingBinding.Attach(border, delegate
		{
			setAnalog(c.DisplayMode == ClockDisplayMode.Analog);
		});
		page.Children.Add(border);
		page.Children.Add(SUI.Note("Time format (12 / 24-hour) is on the General page. Add or remove world clocks in the Clock tab."));
		SSettingBinding.WatchStructure(page, () => string.Join("|", from w in c.WorldClocks
			where !w.IsLocal
			select w.Id + ":" + w.CityName + ":" + w.TimeZoneId));
		List<WorldClockCity> list = c.WorldClocks.Where((WorldClockCity w) => !w.IsLocal).ToList();
		if (list.Count <= 0)
		{
			return;
		}
		page.Children.Add(SUI.SectionLabel("World clocks"));
		Border wg = SUI.Group();
		foreach (WorldClockCity item3 in list)
		{
			string cid = item3.Id;
			SButton sButton = new SButton("Remove", SButtonKind.Ghost);
			sButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			Grid row = null;
			sButton.Click += delegate
			{
				c.RemoveWorldClock(cid);
				StackPanel stackPanel = SUI.GroupStack(wg);
				int num = stackPanel.Children.IndexOf(row);
				stackPanel.Children.Remove(row);
				if (num > 0)
				{
					stackPanel.Children.RemoveAt(num - 1);
				}
				else if (stackPanel.Children.Count > 0)
				{
					stackPanel.Children.RemoveAt(0);
				}
			};
			string description = item3.TimeZoneId;
			try
			{
				description = TimeZoneInfo.FindSystemTimeZoneById(item3.TimeZoneId).DisplayName;
			}
			catch
			{
			}
			row = SUI.Row(item3.CityName, description, sButton, null, isRightAlignedAction: true, "setting-world-clock-" + cid);
			SUI.AddToGroup(SUI.GroupStack(wg), row);
		}
		page.Children.Add(wg);
	}

	private static void ClipboardSection(StackPanel page, PageContext ctx)
	{
		ClipboardService svc = ClipboardService.Instance;
		NotchConfig cfg = NotchConfig.Instance;
		page.Children.Add(SUI.SectionLabel("History"));
		List<KeyValuePair<string, string>> items = WithCurrent(NumberItems(new int[4] { 50, 100, 200, 500 }, " items"), svc.MaxHistoryCount, " items");
		SButton sButton = new SButton("Clearâ€¦");
		sButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton.Click += delegate
		{
			if (SUI.Confirm(ctx.Window, "Clear clipboard history?", "Every unpinned item is removed. Pinned items stay.", "Clear", danger: true))
			{
				svc.ClearHistory();
				ctx.Reload();
			}
		};
		SButton sButton2 = new SButton("Clear pinned tooâ€¦", SButtonKind.Ghost);
		sButton2.Click += delegate
		{
			if (SUI.Confirm(ctx.Window, "Clear everything?", "All clipboard items, pinned ones included, are removed.", "Clear all", danger: true))
			{
				svc.ClearHistory(includePinned: true);
				ctx.Reload();
			}
		};
		string selected = svc.RetentionDays switch
		{
			0 => "0", 
			7 => "7", 
			_ => "30", 
		};
		Grid grid = SUI.SegmentedRow("Keep history for", "Unpinned items older than this are deleted automatically", new string[3] { "7 days", "30 days", "Forever" }, new string[3] { "7", "30", "0" }, selected, delegate(string k)
		{
			svc.RetentionDays = int.Parse(k);
		}, "setting-keep-history-for-082", () => (svc.RetentionDays != 7) ? ((svc.RetentionDays != 0) ? "30" : "0") : "7");
		TextBlock textBlock = SettingsTheme.Text(svc.GetTotalSizeFormatted(), 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Border border = SUI.Group(SUI.ToggleRow("Save clipboard history", "Off pauses recording; nothing new is kept", svc.IsMonitoring, delegate(bool v)
		{
			svc.IsMonitoring = v;
		}, null, "setting-save-clipboard-history-083", () => svc.IsMonitoring), grid, SUI.DropdownRow("Keep up to", "Older unpinned items are removed first; pinned items always stay", items, svc.MaxHistoryCount.ToString(CultureInfo.InvariantCulture), delegate(string k)
		{
			svc.MaxHistoryCount = int.Parse(k, CultureInfo.InvariantCulture);
		}, 240.0, "setting-keep-up-to-084", () => svc.MaxHistoryCount.ToString(CultureInfo.InvariantCulture)), SUI.Row("Storage size", "Total space used by clipboard text and images", textBlock, null, isRightAlignedAction: true, "setting-storage-size-085"), SUI.Row("Clear history", svc.Items.Count + " item" + ((svc.Items.Count == 1) ? "" : "s") + " saved", sButton, null, isRightAlignedAction: true, "setting-clear-history-086"));
		SUI.AddToGroup(SUI.GroupStack(border), SUI.ButtonBar(sButton2));
		page.Children.Add(border);
		page.Children.Add(SUI.SectionLabel("Notch"));
		Grid maskRow = SUI.ToggleRow("Hide copied text", "Show â€œTextâ€ or â€œImageâ€ instead of what you copied", cfg.MaskClipboardToast, delegate(bool v)
		{
			cfg.MaskClipboardToast = v;
		}, null, "setting-hide-copied-text-087", () => cfg.MaskClipboardToast);
		SUI.SetRowEnabled(maskRow, cfg.ShowClipboardToast);
		SSettingBinding.Attach(maskRow, delegate
		{
			SUI.SetRowEnabled(maskRow, cfg.ShowClipboardToast);
		});
		page.Children.Add(SUI.Group(SUI.ToggleRow("Copied toast", "A short confirmation in the notch when you copy", cfg.ShowClipboardToast, delegate(bool v)
		{
			cfg.ShowClipboardToast = v;
			SUI.SetRowEnabled(maskRow, v);
		}, null, "setting-copied-toast-088", () => cfg.ShowClipboardToast), maskRow));
	}

	private static void TimerSection(StackPanel page, PageContext ctx)
	{
		TimerService t = TimerService.Instance;
		NotchConfig cfg = NotchConfig.Instance;
		int current = t.PomoWorkSeconds / 60;
		int current2 = t.PomoBreakSeconds / 60;
		int current3 = t.PomoLongBreakSeconds / 60;
		int pomoTargetCycles = t.PomoTargetCycles;
		page.Children.Add(SUI.SectionLabel("Pomodoro"));
		Border element = SUI.Group(SUI.DropdownRow("Focus", null, WithCurrent(NumberItems(new int[8] { 15, 20, 25, 30, 45, 50, 60, 90 }, " min"), current, " min"), current.ToString(), delegate(string k)
		{
			t.SetPomodoroSettings(int.Parse(k), t.PomoBreakSeconds / 60, t.PomoLongBreakSeconds / 60, t.PomoTargetCycles);
		}, 240.0, "setting-focus-089", () => (t.PomoWorkSeconds / 60).ToString()), SUI.DropdownRow("Short break", null, WithCurrent(NumberItems(new int[4] { 3, 5, 10, 15 }, " min"), current2, " min"), current2.ToString(), delegate(string k)
		{
			t.SetPomodoroSettings(t.PomoWorkSeconds / 60, int.Parse(k), t.PomoLongBreakSeconds / 60, t.PomoTargetCycles);
		}, 240.0, "setting-short-break-090", () => (t.PomoBreakSeconds / 60).ToString()), SUI.DropdownRow("Long break", null, WithCurrent(NumberItems(new int[4] { 10, 15, 20, 30 }, " min"), current3, " min"), current3.ToString(), delegate(string k)
		{
			t.SetPomodoroSettings(t.PomoWorkSeconds / 60, t.PomoBreakSeconds / 60, int.Parse(k), t.PomoTargetCycles);
		}, 240.0, "setting-long-break-091", () => (t.PomoLongBreakSeconds / 60).ToString()), SUI.DropdownRow("Sessions before a long break", null, WithCurrent(NumberItems(new int[5] { 2, 3, 4, 5, 6 }, ""), pomoTargetCycles, ""), pomoTargetCycles.ToString(), delegate(string k)
		{
			t.SetPomodoroSettings(t.PomoWorkSeconds / 60, t.PomoBreakSeconds / 60, t.PomoLongBreakSeconds / 60, int.Parse(k));
		}, 240.0, "setting-sessions-before-a-long-break-092", () => t.PomoTargetCycles.ToString()));
		page.Children.Add(element);
		page.Children.Add(SUI.Note("Changes apply from the next session; a running Pomodoro keeps its times."));
		page.Children.Add(SUI.SectionLabel("Countdown & water"));
		int current4 = Math.Max(1, t.CountTotalSeconds / 60);
		int current5 = ((t.HydrationIntervalMinutes > 0) ? t.HydrationIntervalMinutes : 60);
		Grid intervalRow = SUI.DropdownRow("Remind me every", null, WithCurrent(NumberItems(new int[6] { 15, 30, 45, 60, 90, 120 }, " min"), current5, " min"), current5.ToString(), delegate(string k)
		{
			t.SetHydration(t.HydrationEnabled, int.Parse(k));
		}, 240.0, "setting-remind-me-every-093", () => ((t.HydrationIntervalMinutes > 0) ? t.HydrationIntervalMinutes : 60).ToString());
		Grid grid = SUI.DropdownRow("Daily goal", null, WithCurrent(NumberItems(new int[6] { 4, 6, 8, 10, 12, 16 }, " glasses"), Math.Max(1, t.HydrationDailyTarget), " glasses"), Math.Max(1, t.HydrationDailyTarget).ToString(), delegate(string k)
		{
			t.SetHydrationTarget(int.Parse(k));
		}, 240.0, "setting-daily-goal-094", () => Math.Max(1, t.HydrationDailyTarget).ToString());
		SUI.SetRowEnabled(intervalRow, t.HydrationEnabled);
		SSettingBinding.Attach(intervalRow, delegate
		{
			SUI.SetRowEnabled(intervalRow, t.HydrationEnabled);
		});
		TextBlock countNote = InlineMessage();
		SDropdown countDd = null;
		Grid grid2 = SUI.DropdownRow("Default countdown", "Used when you start a countdown", WithCurrent(NumberItems(new int[9] { 1, 3, 5, 10, 15, 20, 30, 45, 60 }, " min"), current4, " min"), current4.ToString(), delegate(string k)
		{
			if (t.CountIsRunning || t.CountIsPaused)
			{
				countDd.Selected = Math.Max(1, t.CountTotalSeconds / 60).ToString();
				Say(countNote, "Stop the current countdown first, then change its default.", error: true);
			}
			else
			{
				Say(countNote, null, error: false);
				t.SetCountdownMinutes(int.Parse(k));
			}
		}, 240.0, "setting-default-countdown-095", () => Math.Max(1, t.CountTotalSeconds / 60).ToString());
		countDd = grid2.Children.OfType<SDropdown>().First();
		page.Children.Add(SUI.Group(grid2, SUI.ToggleRow("Water reminders", "A gentle nudge to drink water", t.HydrationEnabled, delegate(bool v)
		{
			t.SetHydration(v, (t.HydrationIntervalMinutes > 0) ? t.HydrationIntervalMinutes : 60);
			SUI.SetRowEnabled(intervalRow, v);
		}, null, "setting-water-reminders-096", () => t.HydrationEnabled), intervalRow, grid));
		page.Children.Add(countNote);
		page.Children.Add(SUI.SectionLabel("Notch"));
		page.Children.Add(SUI.Group(SUI.ToggleRow("Live timer on the closed notch", "Shows a running Pomodoro, countdown or stopwatch while the notch is closed", cfg.ShowTimersInNotch, delegate(bool v)
		{
			cfg.ShowTimersInNotch = v;
		}, null, "setting-live-timer-on-the-closed-notch-097", () => cfg.ShowTimersInNotch)));
	}

	private static void CalendarSection(StackPanel page, PageContext ctx)
	{
		CalendarService cal = CalendarService.Instance;
		CalendarSettings st = cal.Settings;
		SSettingBinding.WatchStructure(page, () => string.Join("|", cal.Settings.Subscriptions.Select((CalendarSubscription item) => item.Id + ":" + item.Name + ":" + item.LastSyncUtc + ":" + item.LastError)));
		page.Children.Add(SUI.SectionLabel("Holidays & festivals"));
		List<KeyValuePair<string, string>> items = HolidayCountries.All.Select((string[] c) => new KeyValuePair<string, string>(c[0], c[1])).ToList();
		Grid countryRow = SUI.DropdownRow("Country", "Whose public holidays and festivals to show", items, st.Country, delegate(string k)
		{
			cal.SetCountry(k);
		}, 220.0, "setting-country-098", () => cal.Settings.Country);
		Grid obsRow = SUI.ToggleRow("Observances & festivals", "Days that aren't public holidays, like festivals", st.Observances, delegate(bool v)
		{
			cal.SetHolidays(st.Holidays, v);
		}, null, "setting-observances-festivals-099", () => cal.Settings.Observances);
		SUI.SetRowEnabled(countryRow, st.Holidays);
		SUI.SetRowEnabled(obsRow, st.Holidays);
		SSettingBinding.Attach(countryRow, delegate
		{
			SUI.SetRowEnabled(countryRow, cal.Settings.Holidays);
			SUI.SetRowEnabled(obsRow, cal.Settings.Holidays);
		});
		page.Children.Add(SUI.Group(SUI.ToggleRow("Public holidays", null, st.Holidays, delegate(bool v)
		{
			cal.SetHolidays(v, st.Observances);
			SUI.SetRowEnabled(countryRow, v);
			SUI.SetRowEnabled(obsRow, v);
		}, null, "setting-public-holidays-100", () => cal.Settings.Holidays), countryRow, obsRow));
		page.Children.Add(SUI.SectionLabel("Subscribed calendars"));
		Border border = SUI.Group();
		foreach (CalendarSubscription sub in st.Subscriptions.ToList())
		{
			string sid = sub.Id;
			SButton sButton = new SButton("Remove", SButtonKind.Ghost);
			sButton.Click += delegate
			{
				if (SUI.Confirm(ctx.Window, "Remove " + (sub.Name ?? "this calendar") + "?", "Its events disappear from Ayn. The calendar itself isn't changed.", "Remove", danger: true))
				{
					cal.RemoveSubscription(sid);
					ctx.Reload();
				}
			};
			SToggle tog = new SToggle(sub.Enabled, "Show " + sub.Name);
			SSettingBinding.Attach(tog, delegate
			{
				CalendarSubscription calendarSubscription = cal.Settings.Subscriptions.FirstOrDefault((CalendarSubscription item) => item.Id == sid);
				if (calendarSubscription != null)
				{
					tog.IsOn = calendarSubscription.Enabled;
				}
			});
			tog.Toggled += delegate(bool v)
			{
				cal.SetSubscription(sid, v, null);
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
			StackPanel stackPanel2 = stackPanel;
			stackPanel2.Children.Add(sButton);
			tog.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
			stackPanel2.Children.Add(tog);
			string description = ((!string.IsNullOrEmpty(sub.LastError)) ? sub.LastError : ((sub.LastSyncUtc > 0) ? ("Synced " + new DateTime(sub.LastSyncUtc, DateTimeKind.Utc).ToLocalTime().ToString("g", CultureInfo.CurrentCulture)) : "Not synced yet"));
			SUI.AddToGroup(SUI.GroupStack(border), SUI.Row(string.IsNullOrEmpty(sub.Name) ? "Calendar" : sub.Name, description, stackPanel2, null, isRightAlignedAction: false, "setting-calendar-subscription-" + sid));
		}
		STextBox url = new STextBox("", "https:// or webcal:// link to an .ics calendar", 0.0, "Calendar link");
		url.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
		SButton add = new SButton("Add");
		TextBlock status = SUI.Note("");
		status.Visibility = Visibility.Collapsed;
		Action doAdd = delegate
		{
			add.IsEnabled = false;
			cal.AddSubscription(url.Text.Trim(), delegate(bool ok, string msg)
			{
				ctx.Window.Dispatcher.BeginInvoke((Action)delegate
				{
					add.IsEnabled = true;
					if (ok)
					{
						ctx.Reload();
					}
					else
					{
						status.Text = msg ?? "That link didn't work.";
						status.Foreground = SettingsTheme.Danger;
						status.Visibility = Visibility.Visible;
					}
				});
			});
		};
		add.Click += delegate
		{
			doAdd();
		};
		url.Box.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				doAdd();
				e.Handled = true;
			}
		};
		Grid grid = new Grid();
		grid.Margin = new Thickness(16.0, 12.0, 16.0, 12.0);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.Children.Add(url);
		add.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		Grid.SetColumn(add, 1);
		grid2.Children.Add(add);
		SUI.AddToGroup(SUI.GroupStack(border), grid2);
		page.Children.Add(border);
		page.Children.Add(status);
		SButton refresh = new SButton("Refresh now", SButtonKind.Ghost, "sync");
		refresh.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		refresh.Click += delegate
		{
			cal.RefreshFeeds(force: true);
			refresh.Text = "Refreshingâ€¦";
		};
		page.Children.Add(refresh);
		page.Children.Add(SUI.SectionLabel("Notch"));
		NotchConfig cfg = NotchConfig.Instance;
		page.Children.Add(SUI.Group(SUI.ToggleRow("Calendar alerts", "Upcoming events and due reminders expand the notch", cfg.ShowCalendarInNotch, delegate(bool v)
		{
			cfg.ShowCalendarInNotch = v;
		}, null, "setting-calendar-alerts-102", () => cfg.ShowCalendarInNotch)));
	}

	private static void WeatherSection(StackPanel page, PageContext ctx)
	{
		WeatherService w = WeatherService.Instance;
		page.Children.Add(SUI.SectionLabel("Units"));
		page.Children.Add(SUI.Group(SUI.SegmentedRow("Temperature", null, new string[2] { "Â°C", "Â°F" }, new string[2] { "c", "f" }, w.UseFahrenheit ? "f" : "c", delegate(string k)
		{
			w.UseFahrenheit = k == "f";
		}, "setting-temperature-103", () => (!w.UseFahrenheit) ? "c" : "f"), SUI.SegmentedRow("Wind speed", null, new string[2] { "km/h", "mph" }, new string[2] { "kmh", "mph" }, w.WindInMph ? "mph" : "kmh", delegate(string k)
		{
			w.WindInMph = k == "mph";
		}, "setting-wind-speed-104", () => (!w.WindInMph) ? "kmh" : "mph")));
		page.Children.Add(SUI.SectionLabel("Location"));
		SSettingBinding.WatchStructure(page, () => ((w.Current == null) ? "" : w.Current.City) + "|" + w.FollowLocation + "|" + string.Join("|", w.RecentCities.Select((RecentCity r) => r.Latitude.ToString("R", CultureInfo.InvariantCulture) + "," + r.Longitude.ToString("R", CultureInfo.InvariantCulture))));
		string title = ((w.Current != null && w.Current.IsConfigured) ? w.Current.City : "Not set yet");
		SButton useLoc = new SButton("Use my location", SButtonKind.Secondary, "location");
		useLoc.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		useLoc.Click += delegate
		{
			w.UseCurrentLocation();
			useLoc.Text = "Locatingâ€¦";
		};
		Border border = SUI.Group(SUI.Row(title, w.FollowLocation ? "Following your location" : "Picked city", useLoc, null, isRightAlignedAction: true, "setting-row-105"));
		STextBox search = new STextBox("", "Search for a city", 0.0, "City", "search");
		search.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
		StackPanel results = new StackPanel();
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(16.0, 12.0, 16.0, 12.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(search);
		stackPanel2.Children.Add(results);
		DispatcherTimer debounce = null;
		debounce = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(350.0)
		};
		int gen = 0;
		debounce.Tick += delegate
		{
			debounce.Stop();
			string text = search.Text.Trim();
			results.Children.Clear();
			if (text.Length >= 2)
			{
				int my = ++gen;
				results.Children.Add(SUI.Note("Searchingâ€¦"));
				w.SearchCitiesAsync(text, delegate(List<GeocodedCity> list, string err)
				{
					ctx.Window.Dispatcher.BeginInvoke((Action)delegate
					{
						if (my == gen)
						{
							results.Children.Clear();
							if (list != null && list.Count != 0)
							{
								foreach (GeocodedCity item in list.Take(6))
								{
									GeocodedCity c = item;
									SClickable sClickable = new SClickable(6.0, c.DisplayName)
									{
										VisualRole = SVisualRole.Navigation,
										Padding = new Thickness(12.0, 8.0, 12.0, 8.0),
										Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
										Background = Brushes.Transparent
									};
									sClickable.Child = SettingsTheme.Text(c.DisplayName, 15.0, SettingsTheme.TextPrimary, FontWeights.Normal);
									sClickable.Click += delegate
									{
										w.SetCity(c);
										ctx.Reload();
									};
									results.Children.Add(sClickable);
								}
								return;
							}
							results.Children.Add(SUI.Note(err ?? "No places found."));
						}
					});
				});
			}
		};
		search.Box.TextChanged += delegate
		{
			debounce.Stop();
			debounce.Start();
		};
		ctx.OnLeave(delegate
		{
			debounce.Stop();
		});
		SUI.AddToGroup(SUI.GroupStack(border), stackPanel2);
		page.Children.Add(border);
		List<RecentCity> list2 = w.RecentCities.ToList();
		if (list2.Count <= 0)
		{
			return;
		}
		page.Children.Add(SUI.SectionLabel("Recent places"));
		Border border2 = SUI.Group();
		foreach (RecentCity item2 in list2)
		{
			RecentCity rc = item2;
			SButton sButton = new SButton("Use", SButtonKind.Ghost);
			sButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			sButton.Click += delegate
			{
				w.SetRecentCity(rc);
				ctx.Reload();
			};
			SUI.AddToGroup(SUI.GroupStack(border2), SUI.Row(rc.Name, rc.Detail, sButton, null, isRightAlignedAction: true, "setting-weather-recent-" + rc.Latitude.ToString("R", CultureInfo.InvariantCulture) + "-" + rc.Longitude.ToString("R", CultureInfo.InvariantCulture)));
		}
		page.Children.Add(border2);
	}

	private static void ScreenTimeSection(StackPanel page, PageContext ctx)
	{
		ScreenTimeService st = ScreenTimeService.Instance;
		page.Children.Add(SUI.SectionLabel("Daily limit"));
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		list.Add(new KeyValuePair<string, string>("0", "No limit"));
		List<KeyValuePair<string, string>> list2 = list;
		int[] array = new int[12]
		{
			30, 60, 90, 120, 180, 240, 300, 360, 420, 480,
			600, 720
		};
		foreach (int num in array)
		{
			list2.Add(new KeyValuePair<string, string>((num * 60).ToString(), ScreenTimeService.FormatDuration(num * 60)));
		}
		string cur = st.DailyLimitSeconds.ToString();
		if (!list2.Any((KeyValuePair<string, string> kv) => kv.Key == cur))
		{
			list2.Add(new KeyValuePair<string, string>(cur, ScreenTimeService.FormatDuration(st.DailyLimitSeconds)));
		}
		Grid notifyRow = SUI.ToggleRow("Windows notification", "When you reach the limit", st.NotifyOnLimitReached, delegate(bool v)
		{
			st.SetDailyLimit(st.DailyLimitSeconds, v, st.ShowNotchAlertOnLimit);
		}, null, "setting-windows-notification-107", () => st.NotifyOnLimitReached);
		Grid notchRow = SUI.ToggleRow("Notch alert", "The notch expands when you reach the limit", st.ShowNotchAlertOnLimit, delegate(bool v)
		{
			st.SetDailyLimit(st.DailyLimitSeconds, st.NotifyOnLimitReached, v);
		}, null, "setting-notch-alert-108", () => st.ShowNotchAlertOnLimit);
		SUI.SetRowEnabled(notifyRow, st.HasLimit);
		SSettingBinding.Attach(notifyRow, delegate
		{
			SUI.SetRowEnabled(notifyRow, st.HasLimit);
			SUI.SetRowEnabled(notchRow, st.HasLimit);
		});
		SUI.SetRowEnabled(notchRow, st.HasLimit);
		page.Children.Add(SUI.Group(SUI.DropdownRow("Daily limit", "Total screen time per day", list2, cur, delegate(string k)
		{
			st.SetDailyLimit(int.Parse(k), st.NotifyOnLimitReached, st.ShowNotchAlertOnLimit);
			SUI.SetRowEnabled(notifyRow, st.HasLimit);
			SUI.SetRowEnabled(notchRow, st.HasLimit);
		}, 240.0, "setting-daily-limit-109", () => st.DailyLimitSeconds.ToString()), notifyRow, notchRow));
		page.Children.Add(SUI.SectionLabel("Tracking"));
		SButton sButton = new SButton("Reset today");
		sButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton.Click += delegate
		{
			if (SUI.Confirm(ctx.Window, "Reset today?", "Today's recorded time is set back to zero.", "Reset", danger: true))
			{
				st.ResetToday();
			}
		};
		page.Children.Add(SUI.Group(SUI.ToggleRow("Track screen time", "Off pauses recording until you turn it back on", !st.IsPaused, delegate(bool v)
		{
			st.IsPaused = !v;
		}, null, "setting-track-screen-time-110", () => !st.IsPaused), SUI.Row("Today", ScreenTimeService.FormatDuration(st.TodayTotalSeconds) + " so far", sButton, null, isRightAlignedAction: true, "setting-today-111")));
		page.Children.Add(SUI.Note("To delete every recorded day, use Privacy."));
	}

	private static void AnalyticsSection(StackPanel page, PageContext ctx)
	{
		AnalyticsService an = AnalyticsService.Instance;
		SSettingBinding.WatchStructure(page, () => string.Join("|", an.VisibleSites.Select((AnalyticsSite site) => site.SiteId + ":" + site.DisplayName)));
		page.Children.Add(SUI.SectionLabel("Sites"));
		Border border = SUI.Group();
		List<AnalyticsSite> visibleSites = an.VisibleSites;
		if (visibleSites.Count == 0)
		{
			SUI.AddToGroup(SUI.GroupStack(border), SUI.Row("No sites connected", AnalyticsFeatures.GoogleAnalytics ? "Connect Plausible, Google Analytics or DataFast from the Analytics tab" : "Connect Plausible or DataFast from the Analytics tab", null, null, isRightAlignedAction: false, "setting-no-sites-connected-112"));
		}
		foreach (AnalyticsSite item in visibleSites)
		{
			AnalyticsSite s2 = item;
			SButton sButton = new SButton("Disconnect", SButtonKind.Ghost);
			sButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			sButton.Click += delegate
			{
				if (SUI.Confirm(ctx.Window, "Disconnect " + s2.DisplayName + "?", "Its saved key and cached reports are removed from this PC.", "Disconnect", danger: true))
				{
					try
					{
						an.Disconnect(s2.Key);
					}
					catch (Exception ex)
					{
						TransactionLogger.Warn("Analytics disconnect failed: " + ex.GetType().Name);
						System.Windows.MessageBox.Show("Ayn couldn't remove this site's saved key from this PC. Please try again.", "Disconnect", MessageBoxButton.OK, MessageBoxImage.Exclamation);
						return;
					}
					ctx.Reload();
				}
			};
			string text = s2.Provider ?? "";
			text = ((text.Length > 0) ? (char.ToUpperInvariant(text[0]) + text.Substring(1)) : "Site");
			SUI.AddToGroup(SUI.GroupStack(border), SUI.Row(string.IsNullOrEmpty(s2.DisplayName) ? s2.SiteId : s2.DisplayName, text + " Â· " + s2.SiteId, sButton, null, isRightAlignedAction: true, "setting-analytics-site-" + s2.SiteId));
		}
		SButton sButton2 = new SButton("Add a site in the notch", SButtonKind.Secondary, "external");
		sButton2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		sButton2.Click += delegate
		{
			CapsuleWindow.Instance?.OpenToolFromSettings("analytics");
		};
		SUI.AddToGroup(SUI.GroupStack(border), SUI.ButtonBar(sButton2));
		page.Children.Add(border);
		page.Children.Add(SUI.Note(AnalyticsFeatures.GoogleAnalytics ? "Adding a site needs its key or a Google sign-in, which happens in the Analytics tab." : "Adding a site needs its read-only key, which you paste in the Analytics tab."));
		page.Children.Add(SUI.SectionLabel("Notch"));
		page.Children.Add(SUI.Group(SUI.ToggleRow("Live visitors on the notch", "Shows the active site's live visitor count", an.Settings.ShowLiveOnNotch, delegate(bool v)
		{
			an.Settings.ShowLiveOnNotch = v;
			an.Save();
		}, null, "setting-live-visitors-on-the-notch-114", () => an.Settings.ShowLiveOnNotch)));
	}

	private static void AICodingSection(StackPanel page, PageContext ctx)
	{
		AICodingService ai = AICodingService.Instance;
		NotchConfig cfg = NotchConfig.Instance;
		page.Children.Add(SUI.SectionLabel("Monthly budget"));
		Border border = SUI.Group();
		AICodingToolType[] array = new AICodingToolType[4]
		{
			AICodingToolType.All,
			AICodingToolType.ClaudeCode,
			AICodingToolType.Codex,
			AICodingToolType.OpenCode
		};
		string[] array2 = new string[4] { "All tools", "Claude Code", "Codex", "OpenCode" };
		for (int i = 0; i < array.Length; i++)
		{
			AICodingToolType type = array[i];
			double budget = ai.GetBudget(type);
			STextBox box = new STextBox((budget > 0.0) ? budget.ToString("0.##", CultureInfo.InvariantCulture) : "", "No budget", 120.0, array2[i] + " budget");
			SSettingBinding.Attach(box, delegate
			{
				if (!box.Box.IsKeyboardFocusWithin)
				{
					double budget2 = ai.GetBudget(type);
					box.Text = ((budget2 > 0.0) ? budget2.ToString("0.##", CultureInfo.InvariantCulture) : "");
				}
			});
			TextBlock textBlock = SettingsTheme.Text("$", 15.0, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
			StackPanel stackPanel2 = stackPanel;
			stackPanel2.Children.Add(textBlock);
			stackPanel2.Children.Add(box);
			Action commit = delegate
			{
				string text = box.Text.Trim().TrimStart('$');
				double result;
				if (text.Length == 0)
				{
					ai.SetBudget(type, 0.0);
				}
				else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result) || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
				{
					ai.SetBudget(type, Math.Max(0.0, result));
					box.BorderBrush = SettingsTheme.InputBorder;
				}
				else
				{
					box.BorderBrush = SettingsTheme.Danger;
				}
			};
			box.Box.LostKeyboardFocus += delegate
			{
				commit();
			};
			box.Box.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
			{
				if (e.Key == Key.Return)
				{
					commit();
					e.Handled = true;
				}
			};
			SUI.AddToGroup(SUI.GroupStack(border), SUI.Row(array2[i], (i == 0) ? "Across every agent" : null, stackPanel2, null, isRightAlignedAction: false, "setting-ai-budget-" + type));
		}
		page.Children.Add(border);
		page.Children.Add(SUI.Note("Spend is estimated from local usage logs at list prices. Leave empty for no budget."));
		page.Children.Add(SUI.SectionLabel("Notch"));
		page.Children.Add(SUI.Group(SUI.ToggleRow("Budget alerts", "The notch expands at 75% and 100% of a budget", cfg.ShowBudgetAlertInNotch, delegate(bool v)
		{
			cfg.ShowBudgetAlertInNotch = v;
		}, null, "setting-budget-alerts-116", () => cfg.ShowBudgetAlertInNotch)));
	}

	private static StackPanel ExtrasPage(PageContext ctx, string section = "all")
	{
		StackPanel page = Page("extras");
		AynExtrasService extras = AynExtrasService.Instance;
		AynExtrasConfig cfg = extras.Config;

		System.Windows.Controls.TextBox MakeBox(string value, Action<string> changed, double width = 130.0, bool multiline = false)
		{
			System.Windows.Controls.TextBox box = new System.Windows.Controls.TextBox
			{
				Text = value,
				Width = width,
				AcceptsReturn = multiline,
				TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
				Height = multiline ? 64.0 : 28.0,
				Background = new SolidColorBrush(Color.FromRgb(31, 31, 31)),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
				BorderThickness = new Thickness(1),
				Padding = new Thickness(6, 4, 6, 4)
			};
			box.TextChanged += delegate { changed(box.Text); };
			return box;
		}

		if (section == "all" || section == "breaks")
		{
		page.Children.Add(SUI.SectionLabel("Breaks"));
		page.Children.Add(SUI.Group(
			SUI.ToggleRow("Break reminders", "Nudge you to step away after focused computer time.", cfg.BreakEnabled, delegate(bool v)
			{
				cfg.BreakEnabled = v;
				cfg.Save();
			}, null, "setting-extras-break", () => cfg.BreakEnabled),
			SUI.SliderRow("After", "Minutes of computer use before a break", 5, 180, 5, cfg.WorkMinutes,
				delegate(double v) { return v + " min"; },
				delegate(double v) { cfg.WorkMinutes = (int)v; cfg.Save(); }),
			SUI.SliderRow("Break length", "How long the break should last", 1, 30, 1, cfg.BreakMinutes,
				delegate(double v) { return v + " min"; },
				delegate(double v) { cfg.BreakMinutes = (int)v; cfg.Save(); })
		));

		page.Children.Add(SUI.SectionLabel("Meals"));
		page.Children.Add(SUI.Group(
			SUI.ToggleRow("Meal reminders", "Only notifies while you are using the PC.", cfg.MealEnabled, delegate(bool v)
			{
				cfg.MealEnabled = v;
				cfg.Save();
			}, null, "setting-extras-meals", () => cfg.MealEnabled),
			SUI.Row("Lunch", "24-hour time", MakeBox(cfg.LunchTime, delegate(string v) { cfg.LunchTime = v; cfg.Save(); }, 90)),
			SUI.Row("Dinner", "24-hour time", MakeBox(cfg.DinnerTime, delegate(string v) { cfg.DinnerTime = v; cfg.Save(); }, 90))
		));

		}
		if (section == "all" || section == "salat")
		{
		page.Children.Add(SUI.SectionLabel("Salat (prayer) times"));
		System.Windows.Controls.TextBox cityBox = MakeBox(cfg.City, delegate(string v) { cfg.City = v.Trim(); cfg.Save(); }, 180);
		TextBlock locationStatus = SettingsTheme.Text(cfg.City + ", " + cfg.Country, 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		SButton detectButton = new SButton("Use my area");
		detectButton.Click += delegate
		{
			_ = Task.Run(async () =>
			{
				var ok = await extras.DetectAreaAsync();
				await Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
				{
					locationStatus.Text = ok ? cfg.City + ", " + cfg.Country : "Could not detect your area.";
					cityBox.Text = cfg.City;
				}));
			});
		};
		SButton findButton = new SButton("Find city");
		findButton.Click += delegate
		{
			_ = Task.Run(async () =>
			{
				var ok = await extras.SetAreaFromCityAsync(cityBox.Text);
				await Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
					locationStatus.Text = ok ? cfg.City + ", " + cfg.Country : "City not found."));
			});
		};
		page.Children.Add(SUI.Group(
			SUI.ToggleRow("Prayer notifications", "Notify when each prayer time begins.", cfg.SalatEnabled, delegate(bool v)
			{
				cfg.SalatEnabled = v;
				cfg.Save();
			}, null, "setting-extras-salat", () => cfg.SalatEnabled),
			SUI.Row("Method", "Calculation angle", MakeBox(cfg.SalatMethod, delegate(string v) { cfg.SalatMethod = v; cfg.Save(); }, 130)),
			SUI.Row("City / area", "Prayer times follow this location", cityBox),
			SUI.Row("Find location", "", SUI.ButtonBar(detectButton, findButton)),
			SUI.Row("Detected", "", locationStatus),
			SUI.Row("Latitude", "Your location", MakeBox(cfg.Latitude.ToString(CultureInfo.InvariantCulture), delegate(string v)
			{
				if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { cfg.Latitude = d; cfg.Save(); }
			}, 90)),
			SUI.Row("Longitude", "Your location", MakeBox(cfg.Longitude.ToString(CultureInfo.InvariantCulture), delegate(string v)
			{
				if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { cfg.Longitude = d; cfg.Save(); }
			}, 90))
		));
		TextBlock prayers = SettingsTheme.Text("", 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		foreach (AynPrayerTime prayer in extras.TodayPrayers())
			prayers.Text += prayer.Name.PadRight(12) + prayer.Time.ToString("h:mm tt") + "\n";
		page.Children.Add(SUI.Group(prayers));

		}
		if (section == "all" || section == "guard")
		{
		page.Children.Add(SUI.SectionLabel("Network Guard"));
		page.Children.Add(SUI.Group(
			SUI.ToggleRow("Network Guard", "Local DNS sinkhole for every device on your Wi-Fi.", cfg.BlockerEnabled, delegate(bool v)
			{
				cfg.BlockerEnabled = v;
				cfg.Save();
			}, null, "setting-extras-guard", () => cfg.BlockerEnabled),
			SUI.ToggleRow("Block pornographic sites", "Adult domains resolve to 0.0.0.0.", cfg.BlockPorn, delegate(bool v) { cfg.BlockPorn = v; cfg.Save(); }, null, "setting-extras-porn", () => cfg.BlockPorn),
			SUI.ToggleRow("Block betting sites and apps", "Betting domains and known processes.", cfg.BlockBetting, delegate(bool v) { cfg.BlockBetting = v; cfg.Save(); }, null, "setting-extras-betting", () => cfg.BlockBetting),
			SUI.ToggleRow("Block ads", "Common advertising and tracking domains.", cfg.BlockAds, delegate(bool v) { cfg.BlockAds = v; cfg.Save(); }, null, "setting-extras-ads", () => cfg.BlockAds),
			SUI.ToggleRow("Close betting apps", "Watch for and close known betting processes.", cfg.BlockApps, delegate(bool v) { cfg.BlockApps = v; cfg.Save(); }, null, "setting-extras-apps", () => cfg.BlockApps),
			SUI.Row("DNS port", "Point your router's DNS at this PC", MakeBox(cfg.DnsPort.ToString(CultureInfo.InvariantCulture), delegate(string v)
			{
				if (int.TryParse(v, out var n)) { cfg.DnsPort = n; cfg.Save(); }
			}, 70))
		));
		TextBlock guardStatus = SettingsTheme.Text("", 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		page.Children.Add(SUI.Group(guardStatus));
		DispatcherTimer guardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
		guardTimer.Tick += delegate
		{
			guardStatus.Text = extras.DnsRunning
				? $"Running on UDP {cfg.DnsPort} Â· {extras.DnsBlocked:N0} blocked Â· {extras.DnsAllowed:N0} allowed"
				: "Stopped. Enable Network Guard above, then set your router DNS to this PC's IP.";
		};
		guardTimer.Start();
		ctx.OnClose(delegate { guardTimer.Stop(); });

		}
		return page;
	}
	private static UIElement PersonalToolPage(PageContext ctx, string id)
	{
		StackPanel page = Page(id);
		if (id == "wifi")
		{
			page.Children.Add(SUI.SectionLabel("Wi-Fi QR"));
			page.Children.Add(SUI.Note("Open the QR & Wi-Fi Share tool, pick your saved network, then press 'Make Wi-Fi QR'. Press 'Share on LAN' to let other devices scan it from http://your-pc:8787."));
		}
		else if (id == "pets")
		{
			page.Children.Add(SUI.SectionLabel("Pets"));
			page.Children.Add(SUI.Note("Choose a companion below. It is drawn locally with no downloads."));
			var pet = new CompanionPetCanvas
			{
				Kind = AynExtrasService.Instance.Config.PetKind,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				Margin = new Thickness(0, 12, 0, 0)
			};
			page.Children.Add(pet);
			var choices = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
			foreach (var kind in new[] { "ditto", "cat", "husky", "pikachu", "doraemon", "dots", "grok", "robot" })
			{
				var b = new SButton(char.ToUpperInvariant(kind[0]) + kind.Substring(1), SButtonKind.Secondary);
				b.Margin = new Thickness(0, 0, 6, 6);
				var chosen = kind;
				b.Click += delegate
				{
					pet.Kind = chosen;
					AynExtrasService.Instance.Config.PetKind = chosen;
					AynExtrasService.Instance.Config.Save();
				};
				choices.Children.Add(b);
			}
			page.Children.Add(choices);
		}
		else
		{
			page.Children.Add(SUI.SectionLabel("To-Do List"));
			page.Children.Add(SUI.Note("Open the To-Do List tool to add tasks, tick them off and clear completed items. Everything is saved locally on this PC."));
		}
		SButton button = new SButton("Open in notch", SButtonKind.Secondary, "external");
		button.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		button.Margin = new Thickness(0, 16, 0, 0);
		button.Click += delegate
		{
			CapsuleWindow.Instance?.OpenToolFromSettings(id == "wifi" ? "qr" : (id == "pets" ? "pets" : "todo"));
		};
		page.Children.Add(button);
		return page;
	}}
