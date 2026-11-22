using System;
using System.Windows.Forms;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class ActivityPreview
{
	public const bool Enabled = true;

	private static DispatcherTimer _musicEnd;

	public static MenuItem CreateTrayMenu(Dispatcher ui)
	{
		MenuItem root = new MenuItem("Preview activity");
		Action<string, Action> action = delegate(string text, Action act)
		{
			MenuItem menuItem = new MenuItem(text);
			menuItem.Click += delegate
			{
				ui.BeginInvoke((Action)delegate
				{
					try
					{
						act();
					}
					catch (Exception ex)
					{
						TransactionLogger.Warn("[ActivityPreview] " + text + ": " + ex.Message);
					}
				});
			};
			root.MenuItems.Add(menuItem);
		};
		PillActivityArbiter arb = PillActivityArbiter.Instance;
		action("Music (compact)", delegate
		{
			Music(wide: false);
		});
		action("Music (wide)", delegate
		{
			Music(wide: true);
		});
		action("Timer done", delegate
		{
			arb.PublishTimerComplete("Timer done", "15:00 · Timer");
		});
		action("Drink water", delegate
		{
			arb.PublishHydrationNudge("3 of 8 glasses today");
		});
		action("Volume", delegate
		{
			arb.PublishVolume(64, isMuted: false);
		});
		action("Copied", delegate
		{
			arb.PublishClipboardCopied("text");
		});
		action("Charging", delegate
		{
			arb.PublishBatteryChargingState("Charging", "78%", isCharging: true, isFull: false);
		});
		root.MenuItems.Add("-");
		action("End music preview", EndMusic);
		return root;
	}

	private static void Music(bool wide)
	{
		PillActivityArbiter instance = PillActivityArbiter.Instance;
		instance.DismissCurrentTransient();
		DynamicActivityService.Instance.PublishActivity(new NotchActivity
		{
			Id = "media_session",
			Type = ActivityType.Media,
			Priority = ActivityPriority.P1_Media,
			Mode = ActivityMode.Persistent,
			Title = "Midnight City",
			Subtitle = "M83",
			IsPlaying = true,
			PlaybackProgress = 0.3,
			NavigateToToolId = "media",
			Payload = new MediaTrackInfo
			{
				Artist = "M83",
				PositionSeconds = 73.0,
				DurationSeconds = 243.0,
				UpdatedUtc = DateTime.UtcNow,
				Playing = true
			}
		});
		if (wide)
		{
			instance.ShowMediaWide();
		}
		else
		{
			instance.EndMediaWide();
		}
		if (_musicEnd == null)
		{
			DispatcherTimer dispatcherTimer = new DispatcherTimer();
			dispatcherTimer.Interval = TimeSpan.FromSeconds(20.0);
			_musicEnd = dispatcherTimer;
			_musicEnd.Tick += delegate
			{
				EndMusic();
			};
		}
		_musicEnd.Stop();
		_musicEnd.Start();
	}

	private static void EndMusic()
	{
		if (_musicEnd != null)
		{
			_musicEnd.Stop();
		}
		MediaService instance = MediaService.Instance;
		if (!string.IsNullOrEmpty(instance.CurrentTitle))
		{
			instance.DispatchMediaStateUpdate();
		}
		else
		{
			DynamicActivityService.Instance.ClearPersistentActivity(ActivityType.Media, 0L);
		}
	}
}
