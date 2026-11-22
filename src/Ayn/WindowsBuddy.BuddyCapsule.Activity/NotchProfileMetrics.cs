using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.Activity;

public struct NotchProfileMetrics
{
	private static readonly Dictionary<CollapsedActivityProfile, NotchProfileMetrics> _registry = new Dictionary<CollapsedActivityProfile, NotchProfileMetrics>
	{
		{
			CollapsedActivityProfile.Resting,
			new NotchProfileMetrics(CollapsedActivityProfile.Resting, 228.0, 220.0, 235.0)
		},
		{
			CollapsedActivityProfile.Timer,
			new NotchProfileMetrics(CollapsedActivityProfile.Timer, 190.0, 175.0, 200.0)
		},
		{
			CollapsedActivityProfile.Charging,
			new NotchProfileMetrics(CollapsedActivityProfile.Charging, 228.0, 220.0, 235.0)
		},
		{
			CollapsedActivityProfile.Volume,
			new NotchProfileMetrics(CollapsedActivityProfile.Volume, 240.0, 230.0, 250.0)
		},
		{
			CollapsedActivityProfile.WiFi,
			new NotchProfileMetrics(CollapsedActivityProfile.WiFi, 280.0, 260.0, 300.0)
		},
		{
			CollapsedActivityProfile.System,
			new NotchProfileMetrics(CollapsedActivityProfile.System, 235.0, 220.0, 250.0)
		},
		{
			CollapsedActivityProfile.Clipboard,
			new NotchProfileMetrics(CollapsedActivityProfile.Clipboard, 280.0, 260.0, 300.0)
		},
		{
			CollapsedActivityProfile.Bluetooth,
			new NotchProfileMetrics(CollapsedActivityProfile.Bluetooth, 280.0, 260.0, 300.0)
		},
		{
			CollapsedActivityProfile.Media,
			new NotchProfileMetrics(CollapsedActivityProfile.Media, 320.0, 300.0, 330.0, 35.0, 8.0, 8.0)
		},
		{
			CollapsedActivityProfile.Clock,
			new NotchProfileMetrics(CollapsedActivityProfile.Clock, 175.0, 160.0, 195.0)
		},
		{
			CollapsedActivityProfile.Reminder,
			new NotchProfileMetrics(CollapsedActivityProfile.Reminder, 260.0, 240.0, 280.0, 43.0)
		},
		{
			CollapsedActivityProfile.MinimalResting,
			new NotchProfileMetrics(CollapsedActivityProfile.MinimalResting, 80.0, 70.0, 90.0, 35.0, 4.0, 4.0)
		}
	};

	public CollapsedActivityProfile Profile { get; set; }

	public double TargetWidth { get; set; }

	public double MinWidth { get; set; }

	public double MaxWidth { get; set; }

	public double Height { get; set; }

	public double HorizontalPadding { get; set; }

	public double ContentSpacing { get; set; }

	public NotchProfileMetrics(CollapsedActivityProfile profile, double targetWidth, double minWidth, double maxWidth, double height = 35.0, double horizontalPadding = 8.0, double contentSpacing = 6.0)
	{
		this = default(NotchProfileMetrics);
		Profile = profile;
		TargetWidth = targetWidth;
		MinWidth = minWidth;
		MaxWidth = maxWidth;
		Height = height;
		HorizontalPadding = horizontalPadding;
		ContentSpacing = contentSpacing;
	}

	public static NotchProfileMetrics Get(CollapsedActivityProfile profile)
	{
		if (_registry.TryGetValue(profile, out var value))
		{
			return value;
		}
		return _registry[CollapsedActivityProfile.Resting];
	}

	public static CollapsedActivityProfile FromActivityType(ActivityType type)
	{
		switch (type)
		{
		case ActivityType.Media:
			return CollapsedActivityProfile.Media;
		case ActivityType.TimerRunning:
			return CollapsedActivityProfile.Timer;
		case ActivityType.TimerComplete:
		case ActivityType.HydrationReminder:
			return CollapsedActivityProfile.Reminder;
		case ActivityType.Volume:
			return CollapsedActivityProfile.Volume;
		case ActivityType.BluetoothConnected:
		case ActivityType.BluetoothDisconnected:
			return CollapsedActivityProfile.Bluetooth;
		case ActivityType.BatteryCharging:
		case ActivityType.BatteryChargingStopped:
		case ActivityType.BatteryFull:
			return CollapsedActivityProfile.Charging;
		case ActivityType.NetworkState:
		case ActivityType.NetworkConnected:
		case ActivityType.NetworkDisconnected:
			return CollapsedActivityProfile.WiFi;
		case ActivityType.ClipboardCopied:
			return CollapsedActivityProfile.Clipboard;
		case ActivityType.CompanionMessage:
		case ActivityType.CalendarAlert:
		case ActivityType.Custom:
			return CollapsedActivityProfile.System;
		case ActivityType.Clock:
			return CollapsedActivityProfile.Clock;
		default:
			return CollapsedActivityProfile.Resting;
		}
	}
}
