using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.UI.Settings;

internal static class BackupUi
{
	private static DispatcherTimer timer;

	private static bool busy;

	public static void Start()
	{
		DispatcherTimer dispatcherTimer = new DispatcherTimer();
		dispatcherTimer.Interval = TimeSpan.FromMinutes(30.0);
		timer = dispatcherTimer;
		timer.Tick += delegate
		{
			CheckWeekly();
		};
		timer.Start();
		OfferRestore();
		CheckWeekly();
	}

	private static async void CheckWeekly()
	{
		if (busy || !AppPreferences.Instance.AutomaticWeeklyBackup || PersistenceHelper.WritesSealed)
		{
			return;
		}
		busy = true;
		try
		{
			if (await Task.Run(() => UserBackupService.WeeklyBackupDue()))
			{
				PersistenceHelper.PrepareExit();
				await Task.Run(delegate
				{
					UserBackupService.RunWeeklyBackup();
				});
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Weekly backup did not finish: " + ex.GetType().Name);
		}
		finally
		{
			busy = false;
		}
	}

	private static async void OfferRestore()
	{
		string marker = Path.Combine(AppPaths.DataDir, "settings", "backup-offer-dismissed");
		try
		{
			if (File.Exists(marker) || UserBackupService.HasUserContent())
			{
				return;
			}
			string latest = await Task.Run(() => UserBackupService.LatestBackup());
			if (latest == null || UserBackupService.HasUserContent())
			{
				return;
			}
			UserBackupService.Bundle bundle = UserBackupService.Read(latest);
			CapsuleWindow notch = CapsuleWindow.Instance;
			Action dismiss = delegate
			{
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(marker));
					File.WriteAllText(marker, "dismissed");
				}
				catch (Exception ex3)
				{
					TransactionLogger.Warn("Backup dismissal could not be saved: " + ex3.GetType().Name);
				}
			};
			Action<Window, Action> restoreBackup = delegate(Window owner, Action close)
			{
				try
				{
					if (SUI.Confirm(owner, "Restore backup?", UserBackupService.Preview(bundle) + "\n\nAyn will merge this backup and restart.", "Restore and restart", danger: false))
					{
						PersistenceHelper.PrepareExit();
						if (!PersistenceHelper.FlushForExit())
						{
							throw new IOException("Pending data could not be saved.");
						}
						UserBackupService.QueueRestore(bundle, replace: false);
						try
						{
							SettingsData.Relaunch();
						}
						catch
						{
							SettingsTransaction.CancelQueued();
							throw;
						}
						close();
					}
				}
				catch (Exception ex2)
				{
					SUI.Confirm(owner, "Couldn’t restore backup", ex2.Message, "OK", danger: false);
				}
			};
			if (notch == null || !notch.TryShowBackupOffer("Backup from " + DateTime.Parse(bundle.CreatedUtc).ToLocalTime().ToString("d MMM"), dismiss, delegate(Action close)
			{
				restoreBackup(notch, close);
			}))
			{
				Window window = new Window
				{
					Title = "Restore Ayn data",
					Width = 400.0,
					SizeToContent = SizeToContent.Height,
					WindowStyle = WindowStyle.None,
					ResizeMode = ResizeMode.NoResize,
					ShowActivated = false,
					ShowInTaskbar = false,
					Topmost = true,
					Background = SettingsTheme.ContentBg
				};
				StackPanel stack = new StackPanel
				{
					Margin = new Thickness(20.0)
				};
				stack.Children.Add(SettingsTheme.Text("Restore your Ayn data from " + DateTime.Parse(bundle.CreatedUtc).ToLocalTime().ToString("d MMM yyyy") + "?", 15.0, SettingsTheme.TextPrimary, FontWeights.SemiBold));
				stack.Children.Add(SUI.Note("A backup was found in Documents\\Ayn Backups. Your licence is handled separately."));
				StackPanel actions = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					HorizontalAlignment = HorizontalAlignment.Right
				};
				SButton later = new SButton("Not now");
				SButton restore = new SButton("Restore", SButtonKind.Primary);
				later.Click += delegate
				{
					dismiss();
					window.Close();
				};
				restore.Click += delegate
				{
					restoreBackup(window, window.Close);
				};
				actions.Children.Add(later);
				actions.Children.Add(restore);
				stack.Children.Add(actions);
				window.Content = stack;
				window.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - window.Width) / 2.0;
				window.Top = SystemParameters.WorkArea.Top + 80.0;
				window.Show();
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Backup offer could not be shown: " + ex.GetType().Name);
		}
	}
}
