using System;
using System.Windows;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.Activity;

public static class ReminderDelivery
{
	public static Action<string, string, Action> ToastSink;

	public static double ReminderSeconds => PillActivityArbiter.EventDuration().TotalSeconds;

	public static string LastDecision { get; private set; }

	public static int ToastCount { get; private set; }

	public static bool IsReminder(ActivityType t)
	{
		if (t != ActivityType.TimerComplete)
		{
			return t == ActivityType.HydrationReminder;
		}
		return true;
	}

	public static void Deliver(ActivityType type, Action showInNotch, string toastTitle, string toastBody, Action toastClick = null)
	{
		Application current = Application.Current;
		if (current != null && current.Dispatcher != null && !current.Dispatcher.CheckAccess())
		{
			try
			{
				current.Dispatcher.BeginInvoke((Action)delegate
				{
					Deliver(type, showInNotch, toastTitle, toastBody, toastClick);
				});
				return;
			}
			catch
			{
				return;
			}
		}
		string reason;
		bool flag = NotchCanShow(type, out reason);
		if (showInNotch != null)
		{
			try
			{
				showInNotch();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[Reminder] notch: " + ex.Message);
				flag = false;
				reason = "notch error";
			}
		}
		bool flag2 = NotchConfig.Instance.Reminders == "NotchAndWindows";
		bool flag3 = !flag || flag2;
		LastDecision = ((!flag) ? ("windows (" + reason + ")") : (flag2 ? "notch+windows" : "notch"));
		if (!flag3)
		{
			return;
		}
		ToastCount++;
		try
		{
			Action<string, string, Action> toastSink = ToastSink;
			if (toastSink != null)
			{
				toastSink(toastTitle, toastBody, toastClick);
			}
			else
			{
				CapsuleWindow.ShowToast(toastTitle, toastBody, toastClick);
			}
		}
		catch (Exception ex2)
		{
			TransactionLogger.Warn("[Reminder] notification: " + ex2.Message);
		}
	}

	public static bool NotchCanShow(ActivityType type, out string reason)
	{
		reason = null;
		try
		{
			if (!ToolRegistry.Instance.CapsuleEnabled)
			{
				reason = "notch turned off";
				return false;
			}
			NotchBehaviorConfig instance = NotchBehaviorConfig.Instance;
			if (instance != null && instance.GetEventPresentationMode(type) == EventPresentationMode.Off)
			{
				reason = "event type off";
				return false;
			}
			CapsuleWindow instance2 = CapsuleWindow.Instance;
			if (instance2 == null)
			{
				reason = "no notch window";
				return false;
			}
			if (instance2.Machine != null && instance2.Machine.IsOpenOrOpening)
			{
				reason = "panel open";
				return false;
			}
			return NotchVisibilityEngine.Instance.WouldShowEvent(out reason);
		}
		catch (Exception ex)
		{
			reason = "check failed: " + ex.GetType().Name;
			return false;
		}
	}
}
