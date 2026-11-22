using System;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services.Pet;

namespace WindowsBuddy.Services.Messages;

public sealed class BuddyMessageService
{
	private static readonly Lazy<BuddyMessageService> _instance = new Lazy<BuddyMessageService>(() => new BuddyMessageService());

	private readonly object _syncLock = new object();

	private DateTime _lastMessageTime = DateTime.MinValue;

	private string _lastDatePartKey = string.Empty;

	private bool _startupGreetingShown;

	public static BuddyMessageService Instance => _instance.Value;

	private BuddyMessageService()
	{
	}

	public void TryShowStartupGreeting()
	{
		_startupGreetingShown = true;
		PetEngine.Instance.TryGreeting();
	}

	public void TryShowAmbientCompanionMessage()
	{
	}

	public void ShowShutdownGreeting()
	{
		try
		{
			NotchActivity notchActivity = new NotchActivity();
			notchActivity.Id = "msg_shutdown";
			notchActivity.Type = ActivityType.CompanionMessage;
			notchActivity.Priority = ActivityPriority.P0_Critical;
			notchActivity.Mode = ActivityMode.Temporary;
			notchActivity.Duration = TimeSpan.FromSeconds(1.0);
			notchActivity.Title = "See you soon";
			notchActivity.Subtitle = "Ayn is closing";
			notchActivity.IconFactory = (double sz, Brush br) => VectorIcons.CreateSparkleIcon(sz, br ?? BuddyTokens.SignatureOrangeBrush);
			NotchActivity activity = notchActivity;
			DynamicActivityService.Instance.PublishActivity(activity);
		}
		catch
		{
		}
	}

	private static string GetDayPart(int hour)
	{
		if (hour >= 5 && hour < 12)
		{
			return "morning";
		}
		if (hour >= 12 && hour < 18)
		{
			return "afternoon";
		}
		if (hour >= 18 && hour < 23)
		{
			return "evening";
		}
		return "night";
	}

	private static string GetDayPartGreeting(int hour)
	{
		if (hour >= 5 && hour < 12)
		{
			return "Good morning";
		}
		if (hour >= 12 && hour < 18)
		{
			return "Good afternoon";
		}
		if (hour >= 18 && hour < 23)
		{
			return "Good evening";
		}
		return "Ready when you are";
	}

	internal static string OpenHint()
	{
		try
		{
			AppPreferences instance = AppPreferences.Instance;
			if (instance.HotkeyKey != 0)
			{
				return "Press " + HotkeyService.Describe(instance.HotkeyModifiers, instance.HotkeyKey) + " to open Ayn";
			}
			if (NotchSettings.Instance.OpensOnClick)
			{
				return "Click the notch to open Ayn";
			}
		}
		catch
		{
		}
		return "Move to the top of the screen to open Ayn";
	}
}
