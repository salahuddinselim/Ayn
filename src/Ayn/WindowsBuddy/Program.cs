using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.State;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Engine;
using WindowsBuddy.Services;
using WindowsBuddy.Services.CoreAudio;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Weather;
using WindowsBuddy.UI.Onboarding;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy;

public class Program
{
	private const int ATTACH_PARENT_PROCESS = -1;

	private const string LegacySingleInstanceMutexName = "Global\\WindowsBuddy_SingleInstance";

	private const int StormLimit = 30;

	private const int StormWindowMs = 3000;

	internal static bool IsDesktopApplication;

	internal static bool RestoredLaunch;

	private static readonly Queue<long> _uiExceptionTicks = new Queue<long>();

	private static string SingleInstanceMutexName => "Global\\Ayn_SingleInstance" + AppPaths.InstanceSuffix;

	[DllImport("kernel32.dll")]
	private static extern bool AttachConsole(int dwProcessId);

	[STAThread]
	public static int Main(string[] args)
	{
		NetworkSecurity.Configure();
		int num = 1;
		try
		{
			num = Run(args);
		}
		finally
		{
			QuitWatchdog.EndProcess(num);
		}
		return num;
	}

	private static int Run(string[] args)
	{
		if (args.Length > 0 && string.Equals(args[0], "diagnostic-dump", StringComparison.OrdinalIgnoreCase))
		{
			return HangDumpWriter.Run(args);
		}
		AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", isEnabled: false);
		string text = ((args.Length > 0) ? args[0].ToLowerInvariant().TrimStart('-', '/') : "");
		int num;
		switch (text)
		{
		default:
			num = ((text == "reset-notch") ? 1 : 0);
			break;
		case "":
		case "capsule":
		case "buddy":
		case "settings":
		case "relaunch":
			num = 1;
			break;
		}
		bool flag = (byte)num != 0;
		if (flag)
		{
			try
			{
				string text2 = Path.Combine(AppPaths.LocalAppData, "AynJit");
				Directory.CreateDirectory(text2);
				ProfileOptimization.SetProfileRoot(text2);
				ProfileOptimization.StartProfile("Ayn.jit");
			}
			catch
			{
			}
		}
		CrashGuard.Install(flag);
		RestoredLaunch = text == "capsule" && args.Length > 1 && string.Equals(args[1], "restored", StringComparison.OrdinalIgnoreCase);
		InitializeProcessExceptionBoundaries();
		ApplyQaFaults();
		try
		{
			TransactionLogger.Info("Ayn " + SettingsPages.ShortVersion() + " | " + SettingsPages.BuildString().Replace("·", "-"));
		}
		catch
		{
		}
		try
		{
			AttachConsole(-1);
			StreamWriter streamWriter = new StreamWriter(Console.OpenStandardOutput());
			streamWriter.AutoFlush = true;
			StreamWriter @out = streamWriter;
			Console.SetOut(@out);
			StreamWriter streamWriter2 = new StreamWriter(Console.OpenStandardError());
			streamWriter2.AutoFlush = true;
			StreamWriter error = streamWriter2;
			Console.SetError(error);
		}
		catch
		{
		}
		try
		{
			string command = ((args.Length > 0) ? args[0].ToLowerInvariant().TrimStart('-', '/') : "");
			if (!IsReleaseCommand(command, AppPaths.IsPackaged, AppPaths.IsSandboxed))
			{
				string text3 = "'" + command + "' is not available in this build.";
				Console.Error.WriteLine(text3);
				try
				{
					TransactionLogger.Warn("Refused command line verb: " + text3);
				}
				catch
				{
				}
				return 2;
			}
			if (command == "pet-acceptance-qa" || command == "pet-contact-sheet")
			{
				PetPreferences.Instance.SetQaEnabled(false);
			}
			if (IsSandboxOnlyQa(command) && !AppPaths.IsSandboxed)
			{
				string text4 = "Refusing to run '" + command + "' on the real profile. Set AYN_DATA_DIR to a throwaway folder (for example a copy of your data) and run it again.";
				Console.Error.WriteLine(text4);
				try
				{
					TransactionLogger.Warn(text4);
				}
				catch
				{
				}
				return 2;
			}
			if (command == "version" || command == "v")
			{
				AssemblyInformationalVersionAttribute assemblyInformationalVersionAttribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(Program).Assembly, typeof(AssemblyInformationalVersionAttribute));
				string arg = ((assemblyInformationalVersionAttribute != null) ? assemblyInformationalVersionAttribute.InformationalVersion : typeof(Program).Assembly.GetName().Version.ToString());
				Console.WriteLine($"Ayn Version: {arg}");
				return 0;
			}
			if (command == "startup-status")
			{
				string text5 = StartupService.StartupStatusMessage();
				Console.WriteLine("Packaged=" + AppPaths.IsPackaged + "; Enabled=" + StartupService.IsAutoStartEnabled() + "; Restriction=" + (text5 ?? "none"));
				Console.WriteLine("Sandboxed=" + AppPaths.IsSandboxed + "; DataDir=" + AppPaths.DataDir);
				return (text5 != null && text5.Contains("unavailable")) ? 2 : 0;
			}
			if (command == "deactivate")
			{
				DeactivationResult result = LicenseService.Instance.DeactivateDeviceAsync().GetAwaiter().GetResult();
				Console.WriteLine(result.Message);
				return (!result.Success) ? 1 : 0;
			}
			if (command == "uninstall-autostart")
			{
				StartupService.DisableAutoStart();
				Console.WriteLine("Auto-start registration removed.");
				return 0;
			}
			if (command == "theme-contrast-qa")
			{
				return (ThemeTokens.RunContrastQa(Console.WriteLine) != 0) ? 1 : 0;
			}
			if (command == "stopped-prompt")
			{
				return CrashGuard.RunStoppedPrompt((args.Length > 1) ? args[1] : null);
			}
			if (command == "relaunch" && args.Length == 1 && !CrashGuard.TryConsumeRelaunch("native crash (Windows restart)"))
			{
				return 0;
			}
			if (command == "settings-qa")
			{
				AppPaths.UseTempFolder("settings-qa");
				string environmentVariable = Environment.GetEnvironmentVariable("VAYLI_QA_THEME");
				if (!string.IsNullOrEmpty(environmentVariable))
				{
					NotchConfig.Instance.AppTheme = environmentVariable;
				}
				return SettingsQa.Run((args.Length > 1) ? args[1] : null);
			}
			if (command == "export-diagnostics")
			{
				string folder = ((args.Length > 1) ? args[1] : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
				Console.WriteLine(DiagnosticsExport.Export(folder));
				return 0;
			}
			if (command == "settings-hover-qa")
			{
				return SettingsNavQa.RunHover(args);
			}
			if (command == "settings-nav-qa")
			{
				return SettingsNavQa.Run(args);
			}
			if (command == "storage-qa")
			{
				if (!AppPaths.IsSandboxed)
				{
					Console.WriteLine("storage-qa needs AYN_DATA_DIR (a copy of the data)");
					return 2;
				}
				Console.WriteLine("BEFORE");
				foreach (StorageJanitor.StoreSize item in StorageJanitor.Measure())
				{
					Console.WriteLine($"  {item.Name,-22} {(double)item.Bytes / 1024.0,9:N0} KB {item.Files,6} files");
				}
				List<string> list = StorageJanitor.Run();
				Console.WriteLine("AFTER");
				foreach (string item2 in list)
				{
					Console.WriteLine("  " + item2);
				}
				return 0;
			}
			if (command == "defaults-qa")
			{
				return EdgeLightQa.RunDefaults();
			}
			if (command == "onboarding-qa")
			{
				try
				{
					Directory.Delete(Path.Combine(Path.GetTempPath(), "WindowsBuddyQA", "onboarding-qa", "data"), recursive: true);
				}
				catch
				{
				}
				AppPaths.UseTempFolder("onboarding-qa");
				return OnboardingQa.Run((args.Length > 1) ? args[1] : null);
			}
			if (command == "relaunch" || command == "reset-notch")
			{
				SettingsData.WaitForPreviousInstance((args.Length > 1) ? args[1] : null);
			}
			bool createdNew;
			using (new Mutex(initiallyOwned: true, SingleInstanceMutexName, out createdNew))
			{
				if (!createdNew)
				{
					if (command == "quit")
					{
						TransactionLogger.Info(SettingsLauncher.SignalQuit() ? "Asked the running Ayn to quit." : "No running Ayn to quit.");
						return 0;
					}
					if (command == "" || command == "settings" || command == "firstrun" || command == "setup")
					{
						bool backgroundLaunch = command == "" && StartupService.IsSignInLaunch(command, Process.GetCurrentProcess().StartTime);
						TransactionLogger.Info(SettingsLauncher.SignalRunningInstance(backgroundLaunch) ? "Ayn already running: asked it to open Settings." : "Another Ayn instance is already running. Exiting.");
						LogWriter.Flush(300);
						return 0;
					}
					if (command == "run-message-qa" || command == "message-qa")
					{
						Console.Error.WriteLine("Close Ayn first: run-message-qa needs the notch to itself.");
					}
					TransactionLogger.Info("Another Ayn instance is already running. Exiting.");
					LogWriter.Flush(300);
					return (command == "run-message-qa" || command == "message-qa") ? 2 : 0;
				}
				if (command == "quit")
				{
					return 0;
				}
				if (!AppPaths.IsSandboxed)
				{
					CloseLegacyInstance();
				}
				LegacyMigration.Run();
				SettingsData.ApplyPendingReset();
				if (!AppPaths.IsSandboxed)
				{
					StartupService.MigrateLegacyRunEntry();
					StartupService.RepairRunEntry();
				}
				if (command == "run-message-qa" || command == "message-qa")
				{
					ToolRegistry.Instance.ForceEnableCapsule();
					Application msgApp = new Application();
					AttachDispatcherExceptionBoundary(msgApp);
					CapsuleWindow msgWin = new CapsuleWindow();
					msgWin.ContentRendered += delegate
					{
						msgApp.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
						{
							try
							{
								MessageE2ETestRunner.RunAll(msgWin);
							}
							catch (Exception ex4)
							{
								Console.WriteLine("EXCEPTION: " + ex4);
							}
							finally
							{
								msgApp.Shutdown();
							}
						});
					};
					msgWin.Show();
					msgApp.Run(msgWin);
					return 0;
				}
				if (command == "run-notch-qa" || command == "test-live-notch")
				{
					ToolRegistry.Instance.ForceEnableCapsule();
					Application qaApp3 = new Application();
					AttachDispatcherExceptionBoundary(qaApp3);
					CapsuleWindow qaWin3 = new CapsuleWindow();
					qaWin3.ContentRendered += delegate
					{
						qaApp3.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
						{
							try
							{
								LiveNotchE2ETestRunner.RunAll(qaWin3);
							}
							catch (Exception ex3)
							{
								TransactionLogger.Error("LiveNotchE2E test failed with exception: " + ex3);
								try
								{
									File.AppendAllText("e2e_results.log", string.Concat("EXCEPTION: ", ex3, Environment.NewLine));
								}
								catch
								{
								}
							}
							finally
							{
								qaApp3.Shutdown();
							}
						});
					};
					qaWin3.Show();
					qaApp3.Run(qaWin3);
					return 0;
				}
				if (command == "freeze-qa" || command == "reproduce-hang" || command == "theme-qa" || command == "stress-qa" || command == "soak-qa" || command == "edge-qa" || command == "tab-switch-qa")
				{
					ToolRegistry.Instance.ForceEnableCapsule();
					Application qaApp2 = new Application();
					AttachDispatcherExceptionBoundary(qaApp2);
					UiHangWatchdog.Start(qaApp2.Dispatcher, Thread.CurrentThread);
					CapsuleWindow qaWin2 = new CapsuleWindow();
					int exitCode = 0;
					qaWin2.ContentRendered += delegate
					{
						qaApp2.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
						{
							try
							{
								exitCode = ((command == "theme-qa") ? ThemeQa.Run(qaWin2, args) : ((command == "stress-qa") ? StressQa.Run(qaWin2, args) : ((command == "soak-qa") ? StressQa.RunSoak(qaWin2, args) : ((command == "edge-qa") ? EdgeLightQa.Run(qaWin2, args) : ((command == "tab-switch-qa") ? TabSwitchQa.Run(qaWin2, args) : FreezeQa.Run(qaWin2))))));
							}
							catch (Exception ex2)
							{
								Console.WriteLine("FreezeQa exception: " + ex2);
								exitCode = 1;
							}
							finally
							{
								UiHangWatchdog.Stop();
								qaApp2.Shutdown(exitCode);
							}
						});
					};
					qaWin2.Show();
					qaApp2.Run(qaWin2);
					return exitCode;
				}
				string localAppData = AppPaths.LocalAppData;
				string firstRunFile = Path.Combine(localAppData, AppPaths.FolderName, "firstrun.done");
				bool hasLaunchedBefore = StartupService.HasLaunchedBefore;
				bool flag2 = (!File.Exists(firstRunFile) && !hasLaunchedBefore && command != "capsule" && command != "reset-notch" && command != "relaunch") || command == "firstrun" || command == "setup";
				bool eulaAccepted = true;
				bool initFirstLaunch = flag || command == "firstrun" || command == "setup";
				if (initFirstLaunch && eulaAccepted)
				{
					StartupService.InitializeFirstLaunch(File.Exists(firstRunFile));
				}
				if (!eulaAccepted)
				{
					TransactionLogger.Info("End User License Agreement v" + EulaAgreement.RequiredVersion + " not accepted yet: " + (flag2 ? "the welcome tour opens on it." : "showing the one-time agreement screen."));
				}
				if (flag2)
				{
					Application app2 = new Application();
					AttachDispatcherExceptionBoundary(app2);
					app2.ShutdownMode = ShutdownMode.OnExplicitShutdown;
					UiHangWatchdog.Start(app2.Dispatcher, Thread.CurrentThread);
					SettingsLauncher.Listen(app2.Dispatcher);
					Func<CapsuleWindow> func = delegate
					{
						if (initFirstLaunch && !eulaAccepted)
						{
							StartupService.InitializeFirstLaunch(File.Exists(firstRunFile));
						}
						ToolRegistry.Instance.ForceEnableCapsule();
						FirstRunManager.MarkStarted();
						CapsuleWindow capsuleWindow2 = new CapsuleWindow();
						capsuleWindow2.ContentRendered += delegate
						{
							BootTimeline.FirstRender();
							BootTimeline.StartStallMonitor(app2.Dispatcher);
						};
						app2.MainWindow = capsuleWindow2;
						if (EntitlementGate.IsEntitled)
						{
							capsuleWindow2.Show();
						}
						else
						{
							new WindowInteropHelper(capsuleWindow2).EnsureHandle();
						}
						return capsuleWindow2;
					};
					Action onFinished = delegate
					{
						app2.ShutdownMode = ShutdownMode.OnExplicitShutdown;
						StartBackgroundUpkeep();
					};
					bool declined = false;
					OnboardingWindow onboardingWindow;
					if (eulaAccepted)
					{
						onboardingWindow = new OnboardingWindow(onFinished, func());
					}
					else
					{
						ThemeTokens.Initialize();
						onboardingWindow = new OnboardingWindow(onFinished, func, delegate
						{
							declined = true;
							app2.Shutdown(0);
						});
					}
					onboardingWindow.Show();
					app2.Run();
					QuitWatchdog.ExitNow(0, declined ? "agreement declined (tour)" : "Application.Run returned (tour)");
					return 0;
				}
				if (args.Length == 0 || command == "capsule" || command == "buddy" || command == "settings" || command == "reset-notch" || command == "relaunch" || command == "pet-acceptance-qa" || command == "pet-contact-sheet")
				{
					IsDesktopApplication = true;
					if (command == "relaunch")
					{
						ToolRegistry.Instance.ForceEnableCapsule();
					}
					Application app = new Application();
					app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
					AttachDispatcherExceptionBoundary(app);
					if (!eulaAccepted)
					{
						ThemeTokens.Initialize();
						SettingsLauncher.ListenForQuit(app.Dispatcher);
						if (!AgreementWindow.AskOnce())
						{
							QuitWatchdog.ExitNow(0, "agreement declined");
							return 0;
						}
						if (initFirstLaunch)
						{
							StartupService.InitializeFirstLaunch(File.Exists(firstRunFile));
						}
					}
					UiHangWatchdog.Start(app.Dispatcher, Thread.CurrentThread);
					WindowsBuddy.Extras.AynExtrasService.Instance.Start();
					CapsuleWindow win = new CapsuleWindow();
					win.ContentRendered += delegate
					{
						BootTimeline.FirstRender();
						BootTimeline.StartStallMonitor(app.Dispatcher);
					};
					bool isEntitled = EntitlementGate.IsEntitled;
					if (isEntitled)
					{
						win.Show();
					}
					else
					{
						new WindowInteropHelper(win).EnsureHandle();
						app.MainWindow = win;
					}
					StartBackgroundUpkeep();
					int num2 = QaCrashAfter(args);
					if (num2 > 0)
					{
						CrashGuard.ScheduleQaCrash(num2);
					}
					if (command == "pet-contact-sheet")
					{
						app.Dispatcher.BeginInvoke((Action)delegate
						{
							win.StartPetContactSheet((args.Length > 1) ? args[1] : Path.Combine(Environment.CurrentDirectory, "pet-contact"));
						});
					}
					if (command == "pet-acceptance-qa")
					{
						int duration = 600;
						if (args.Length > 2)
						{
							int.TryParse(args[2], out duration);
						}
						app.Dispatcher.BeginInvoke((Action)delegate
						{
							win.StartPetAcceptanceQa((args.Length > 1) ? args[1] : Path.Combine(Environment.CurrentDirectory, "pet-installed-qa"), Math.Max(10, duration));
						});
					}
					if (command == "settings")
					{
						app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
						{
							SettingsLauncher.Open((args.Length > 1) ? args[1] : null);
						});
					}
					else if (LicenseService.Instance.State == LicenseState.Revoked)
					{
						app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
						{
							SettingsLauncher.Open("license");
						});
					}
					else if (!isEntitled && (!StartupService.IsSignInLaunch(command, Process.GetCurrentProcess().StartTime) || !LicenseStub.AwaitingFirstServerContact))
					{
						app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
						{
							SettingsLauncher.OpenForNotEntitled();
						});
					}
					if (isEntitled)
					{
						app.Run(win);
					}
					else
					{
						app.Run();
					}
					QuitWatchdog.Trace("Quit: Application.Run returned; Main is returning");
					QuitWatchdog.ExitNow(0, "Application.Run returned");
					return 0;
				}
				switch (command)
				{
				case "run-notch-qa":
				case "test-live-notch":
				{
					ToolRegistry.Instance.ForceEnableCapsule();
					Application qaApp = new Application();
					AttachDispatcherExceptionBoundary(qaApp);
					CapsuleWindow qaWin = new CapsuleWindow();
					qaWin.Show();
					qaApp.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
					{
						try
						{
							LiveNotchE2ETestRunner.RunAll(qaWin);
						}
						finally
						{
							qaApp.Shutdown();
						}
					});
					qaApp.Run(qaWin);
					return 0;
				}
				case "apply":
				{
					string text6 = ((args.Length > 1) ? args[1] : "themes/minimal");
					if (!Directory.Exists(text6) && Directory.Exists(Path.Combine("themes", text6)))
					{
						text6 = Path.Combine("themes", text6);
					}
					ApplyResult applyResult = ThemeEngine.ApplyTheme(text6);
					return (!applyResult.Success) ? 1 : 0;
				}
				case "reset":
					TransactionLogger.Info("Received reset command.");
					return (!BackupService.RestoreSnapshot()) ? 1 : 0;
				case "status":
					PrintStatus();
					return 0;
				case "verify":
				{
					string text7 = ((args.Length > 1) ? args[1] : "themes/minimal");
					if (!Directory.Exists(text7) && Directory.Exists(Path.Combine("themes", text7)))
					{
						text7 = Path.Combine("themes", text7);
					}
					ThemePackage themePackage = ThemeValidator.LoadAndValidate(text7);
					TransactionLogger.Success($"Theme '{themePackage.name}' is 100% valid and verified for production use.");
					return 0;
				}
				case "install":
					if (AppPaths.IsPackaged)
					{
						Console.WriteLine("Ayn is installed from the Microsoft Store; Windows manages install and updates.");
						return 0;
					}
					InstallToLocalSystem();
					return 0;
				case "taskbar":
				case "studio":
				case "taskbarstudio":
				case "ui":
				case "home":
				{
					Console.WriteLine("Notice: Taskbar Studio and legacy Windows Forms Home UI have been decoupled from the production Capsule binary.");
					Console.WriteLine("Launching Ayn (Default UI)...");
					Application application = new Application();
					AttachDispatcherExceptionBoundary(application);
					CapsuleWindow capsuleWindow = new CapsuleWindow();
					capsuleWindow.Show();
					application.Run(capsuleWindow);
					return 0;
				}
				default:
					Console.WriteLine("Ayn CLI v1.0");
					Console.WriteLine("Usage: Ayn.exe [command] [options]");
					Console.WriteLine("Commands:");
					Console.WriteLine("  capsule / buddy     Launch the Ayn notch (Default)");
					Console.WriteLine("  taskbar / studio    Open Taskbar Studio customization window");
					Console.WriteLine("  ui                  Launch the Ayn notch");
					Console.WriteLine("  apply [themePath]   Apply theme (e.g. themes/minimal)");
					Console.WriteLine("  reset               Restore previous desktop configuration");
					Console.WriteLine("  status              Show live hardware telemetry & active theme");
					Console.WriteLine("  verify [themePath]  Validate theme schema & assets without applying");
					Console.WriteLine("  install             Install Ayn to %LocalAppData%\\Ayn");
					return 0;
				}
			}
		}
		catch (Exception ex)
		{
			CrashDiagnostics.Record(CrashBoundary.Startup, ex, terminating: true);
			if (ex is IOException || ex is InvalidDataException)
			{
				MessageBox.Show("Ayn could not safely load or change its data. Your recovery files have been preserved.\n\n" + ex.Message, "Ayn data recovery", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			return 1;
		}
	}

	private static int QaCrashAfter(string[] args)
	{
		foreach (string text in args)
		{
			if (text.StartsWith("--qa-crash-after=", StringComparison.OrdinalIgnoreCase) && int.TryParse(text.Substring(17), out var result))
			{
				return result;
			}
		}
		return 0;
	}

	internal static bool IsReleaseCommand(string command, bool packaged, bool sandboxed)
	{
		switch (command)
		{
		case "":
		case "capsule":
		case "buddy":
		case "settings":
		case "relaunch":
		case "reset-notch":
		case "stopped-prompt":
		case "quit":
		case "version":
		case "v":
		case "startup-status":
		case "export-diagnostics":
		case "firstrun":
		case "setup":
			return true;
		case "install":
		case "uninstall-autostart":
			return !packaged;
		case "pet-acceptance-qa":
		case "pet-contact-sheet":
		case "settings-qa":
		case "onboarding-qa":
		case "defaults-qa":
		case "settings-hover-qa":
		case "settings-nav-qa":
		case "theme-contrast-qa":
		case "storage-qa":
			return sandboxed;
		default:
			if (sandboxed)
			{
				return IsSandboxOnlyQa(command);
			}
			return false;
		}
	}

	private static bool IsSandboxOnlyQa(string command)
	{
		switch (command)
		{
		case "freeze-qa":
		case "reproduce-hang":
		case "theme-qa":
		case "stress-qa":
		case "soak-qa":
		case "edge-qa":
		case "tab-switch-qa":
		case "test-live-notch":
		case "run-notch-qa":
		case "run-message-qa":
		case "message-qa":
			return true;
		default:
			return false;
		}
	}

	private static void ApplyQaFaults()
	{
		if (!AppPaths.IsSandboxed)
		{
			return;
		}
		string text = (Environment.GetEnvironmentVariable("VAYLI_QA_FAULTS") ?? "").ToLowerInvariant();
		if (text.Length == 0)
		{
			return;
		}
		if (text.Contains("network"))
		{
			WebRequest.DefaultWebProxy = new WebProxy("http://10.255.255.1:9", BypassOnLocal: false);
		}
		if (text.Contains("audio"))
		{
			CoreAudioService.SlowInitForQa = delegate
			{
				Thread.Sleep(10000);
			};
		}
		if (text.Contains("location"))
		{
			WeatherService.LocationDeniedForQa = true;
		}
		if (text.Contains("quitwatchdog"))
		{
			QuitWatchdog.EnabledInSandboxForQa = true;
		}
		TransactionLogger.Info("QA faults: " + text);
	}

	private static void StartBackgroundUpkeep()
	{
		if (!AppPaths.IsSandboxed)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				Thread.Sleep(3000);
				try
				{
					CrashGuard.SetRestoreAfterReboot(StartupService.IsAutoStartEnabled());
				}
				catch
				{
				}
			});
		}
		try
		{
			RenderProfile.Detect();
		}
		catch
		{
		}
		try
		{
			StorageJanitor.Start();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Storage janitor did not start: " + ex.Message);
		}
		try
		{
			ResourceMetrics.Start();
		}
		catch (Exception ex2)
		{
			TransactionLogger.Warn("Metrics log did not start: " + ex2.Message);
		}
		try
		{
			PackageUpdateLifecycle.Start();
		}
		catch (Exception ex3)
		{
			TransactionLogger.Warn("Package update signals did not start: " + ex3.GetType().Name);
		}
		try
		{
			StoreUpdateService.Start();
		}
		catch (Exception ex4)
		{
			TransactionLogger.Warn("Store update checks did not start: " + ex4.GetType().Name);
		}
	}

	private static void CloseLegacyInstance()
	{
		if (!Mutex.TryOpenExisting("Global\\WindowsBuddy_SingleInstance", out var result))
		{
			return;
		}
		result.Dispose();
		try
		{
			Process[] processesByName = Process.GetProcessesByName("WindowsBuddy");
			foreach (Process process in processesByName)
			{
				TransactionLogger.Info("Closing pre-rename Windows Buddy instance (pid " + process.Id + ") before starting Ayn.");
				process.Kill();
				process.WaitForExit(3000);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not close the pre-rename Windows Buddy instance: " + ex.Message);
		}
	}

	private static void PrintStatus()
	{
		Console.WriteLine("==================================================");
		Console.WriteLine("               VAYLI SYSTEM STATUS               ");
		Console.WriteLine("==================================================");
		string activeThemeId = ThemeEngine.GetActiveThemeId();
		Console.WriteLine(string.Format("Active Theme      : {0}", string.IsNullOrEmpty(activeThemeId) ? "None (Default Windows)" : activeThemeId));
		Console.WriteLine(string.Format("Notch             : {0}", BuddyCapsuleService.IsRunning() ? "Active (Top-Center Floating Surface)" : "Inactive"));
		Console.WriteLine(string.Format("Has Backup Point  : {0}", BackupService.HasSnapshot() ? "Yes (Safe restore point ready)" : "No"));
		Console.WriteLine($"Current Wallpaper : {WallpaperService.GetCurrentWallpaper()}");
		SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
		Console.WriteLine($"Hardware Battery  : {liveTelemetry.BatteryPercent}% ({liveTelemetry.PowerLineStatus})");
		Console.WriteLine($"Network Adapter   : {liveTelemetry.NetworkName} ({liveTelemetry.ConnectionType}, {liveTelemetry.LinkSpeedMbps} Mbps)");
		Console.WriteLine($"Master Audio      : {liveTelemetry.VolumePercent}% ({liveTelemetry.AudioEndpointName})");
		Console.WriteLine($"System Clock      : {liveTelemetry.CurrentTime} | {liveTelemetry.CurrentDate}");
		Console.WriteLine("==================================================");
	}

	private static void InstallToLocalSystem()
	{
		string localAppData = AppPaths.LocalAppData;
		string text = Path.Combine(localAppData, AppPaths.FolderName);
		string text2 = Path.Combine(text, "bin");
		string text3 = Path.Combine(text, "themes");
		Directory.CreateDirectory(text2);
		Directory.CreateDirectory(text3);
		string location = Assembly.GetExecutingAssembly().Location;
		string text4 = Path.Combine(text2, "Ayn.exe");
		File.Copy(location, text4, overwrite: true);
		string fullPath = Path.GetFullPath("themes/minimal");
		string destDir = Path.Combine(text3, "minimal");
		CopyDirectory(fullPath, destDir);
		TransactionLogger.Success($"Ayn successfully installed to: {text}");
		TransactionLogger.Info($"Executable: {text4}");
	}

	private static void CopyDirectory(string sourceDir, string destDir)
	{
		Directory.CreateDirectory(destDir);
		string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			string path = text.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
			string text2 = Path.Combine(destDir, path);
			Directory.CreateDirectory(Path.GetDirectoryName(text2));
			File.Copy(text, text2, overwrite: true);
		}
	}

	private static void InitializeProcessExceptionBoundaries()
	{
		AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = e.ExceptionObject as Exception;
			CrashDiagnostics.Record(CrashBoundary.AppDomain, ex, e.IsTerminating);
			CrashGuard.OnUnhandled(ex, e.IsTerminating);
		};
		TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs e)
		{
			e.SetObserved();
			if (CrashGuard.ShouldWriteDump())
			{
				CrashDiagnostics.Record(CrashBoundary.UnobservedTask, e.Exception, terminating: false);
			}
		};
	}

	private static void AttachDispatcherExceptionBoundary(Application app)
	{
		if (app == null)
		{
			return;
		}
		PersistenceHelper.AttachUserNotifications(app);
		app.Startup += delegate
		{
			CrashDiagnostics.BeginSession(RestoredLaunch);
			TransactionLogger.Info(string.Concat("Boot: previous exit: ", CrashDiagnostics.LastPreviousExit, RestoredLaunch ? " (restored by Windows after a restart)" : "", CrashDiagnostics.HasPendingNotice() ? " (unclean-exit report recorded)" : ""));
			app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(BackupUi.Start));
		};
		app.Exit += delegate
		{
			CrashDiagnostics.EndSession();
		};
		app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			bool flag = e.Exception is OutOfMemoryException || e.Exception is StackOverflowException || e.Exception is AccessViolationException;
			if (flag || CrashGuard.ShouldWriteDump())
			{
				CrashDiagnostics.Record(CrashBoundary.Dispatcher, e.Exception, flag);
			}
			if (!flag && IsExceptionStorm())
			{
				e.Handled = true;
				CrashDiagnostics.Record(CrashBoundary.ExceptionStorm, e.Exception, terminating: true);
				CrashGuard.OnUnhandled(e.Exception, terminating: true);
				LogWriter.Flush(500);
				QuitWatchdog.EndProcess(3);
			}
			else
			{
				if (!flag)
				{
					e.Handled = true;
					TransactionLogger.Warn("CR-002: Recoverable Dispatcher exception intercepted. Restoring the notch to a safe state.");
					try
					{
						CapsuleWindow instance = CapsuleWindow.Instance;
						if (instance != null && instance.Machine != null)
						{
							instance.Machine.Reset("unhandled UI exception");
						}
						else
						{
							instance?.ForceSafeCollapsed();
						}
					}
					catch
					{
					}
					try
					{
						if (ToolRouter.Instance != null)
						{
							ToolRouter.Instance.ResetNavigationState();
							if (!string.Equals(ToolRouter.Instance.ActiveToolId, "home", StringComparison.OrdinalIgnoreCase))
							{
								ToolRouter.Instance.RouteTo("home");
							}
						}
						return;
					}
					catch (Exception error)
					{
						CrashDiagnostics.Record(CrashBoundary.Dispatcher, error, terminating: false);
						return;
					}
				}
				TransactionLogger.Error("CR-002: Fatal process-level exception detected. Flushing diagnostics before termination.");
			}
		};
	}

	private static bool IsExceptionStorm()
	{
		long num = Environment.TickCount;
		_uiExceptionTicks.Enqueue(num);
		while (_uiExceptionTicks.Count > 0 && num - _uiExceptionTicks.Peek() > 3000)
		{
			_uiExceptionTicks.Dequeue();
		}
		return _uiExceptionTicks.Count >= 30;
	}
}
