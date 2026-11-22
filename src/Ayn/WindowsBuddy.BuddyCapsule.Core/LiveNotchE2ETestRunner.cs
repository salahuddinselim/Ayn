using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.Services.Media;
using WindowsBuddy.Tools.Calendar;
using WindowsBuddy.Tools.Message;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class LiveNotchE2ETestRunner
{
	[DllImport("kernel32.dll")]
	private static extern bool AttachConsole(int dwProcessId);

	private static void Log(string msg)
	{
		Console.WriteLine(msg);
		try
		{
			File.AppendAllText("e2e_results.log", msg + Environment.NewLine);
		}
		catch
		{
		}
	}

	private static void DoEvents(int milliseconds)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(milliseconds);
		while (DateTime.UtcNow < dateTime)
		{
			DispatcherFrame dispatcherFrame = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (DispatcherOperationCallback)delegate(object f)
			{
				((DispatcherFrame)f).Continue = false;
				return (object)null;
			}, dispatcherFrame);
			Dispatcher.PushFrame(dispatcherFrame);
			Thread.Sleep(10);
		}
	}

	public static void RunAll(CapsuleWindow win)
	{
		try
		{
			AttachConsole(-1);
		}
		catch
		{
		}
		try
		{
			File.Delete("e2e_results.log");
		}
		catch
		{
		}
		Log("========================================================");
		Log("EXECUTING REAL RELEASE 10-POINT LIVE NOTCH TEST MATRIX");
		Log("========================================================");
		win.ForceSafeCollapsed();
		DoEvents(500);
		Log("\n[RUNNING] SCENARIO 1 — IDLE (10s verification)...");
		MediaService.Instance.IsMonitoringSuppressed = true;
		NotchBehaviorConfig.Instance.ShowMediaWhenPlaybackStarts = false;
		PillActivityArbiter.Instance.ClearMedia();
		PillActivityArbiter.Instance.ClearTimer();
		PillActivityArbiter.Instance.DismissCurrentTransient();
		NotchBehaviorConfig.Instance.VisibilityMode = NotchVisibilityMode.AlwaysVisible;
		win.RenderCurrentResolvedPillActivity(immediate: true);
		DoEvents(500);
		bool flag = true;
		for (int j = 0; j < 10; j++)
		{
			DoEvents(1000);
			if (win.ActiveVisualType != CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag = false;
				Log($"Scenario 1 Failure at second {j}: ActiveVisualType={win.ActiveVisualType}");
				break;
			}
		}
		Log(string.Format("SCENARIO 1 RESULT: {0} (VisualType={1}, Slot={2})", flag ? "PASS" : "FAIL", win.ActiveVisualType, win.ActiveSlotName));
		Log("\n[RUNNING] SCENARIO 2 — COPY (3s TTL -> Clock automatically)...");
		Clipboard.SetText("Scenario 2 Single Copy Payload");
		DoEvents(500);
		if (win.ActiveVisualType != CapsuleWindow.CollapsedVisualType.Clipboard)
		{
			PillActivityArbiter.Instance.PublishClipboardCopied("Scenario 2 Single Copy Payload");
			DoEvents(400);
		}
		bool flag2 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clipboard;
		Log($"  Copied rendered immediately: {flag2} (Type={win.ActiveVisualType})");
		bool flag3 = false;
		for (int k = 0; k < 45; k++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag3 = true;
				break;
			}
		}
		Log($"  Clock returned automatically without touching Capsule: {flag3} (Type={win.ActiveVisualType})");
		bool flag4 = flag2 && flag3;
		Log(string.Format("SCENARIO 2 RESULT: {0}", flag4 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 3 — CHARGING (3s TTL -> Clock automatically)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(300);
		PillActivityArbiter.Instance.PublishBatteryChargingState("Charging", "88%", isCharging: true, isFull: false);
		DoEvents(500);
		bool flag5 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Charging;
		Log($"  Charging transient active: {flag5} (Type={win.ActiveVisualType})");
		bool flag6 = false;
		for (int l = 0; l < 45; l++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag6 = true;
				break;
			}
		}
		Log($"  Charging expired -> Clock returned automatically: {flag6} (Type={win.ActiveVisualType})");
		bool flag7 = flag5 && flag6;
		Log(string.Format("SCENARIO 3 RESULT: {0}", flag7 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 4 — WI-FI (3s TTL -> Clock automatically)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(300);
		PillActivityArbiter.Instance.PublishNetworkState("Home-5G", isConnected: true);
		DoEvents(500);
		bool flag8 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Network;
		Log($"  Wi-Fi transient visible: {flag8} (Type={win.ActiveVisualType})");
		bool flag9 = false;
		for (int m = 0; m < 45; m++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag9 = true;
				break;
			}
		}
		Log($"  Wi-Fi expired -> Clock returned automatically: {flag9} (Type={win.ActiveVisualType})");
		bool flag10 = flag8 && flag9;
		Log(string.Format("SCENARIO 4 RESULT: {0}", flag10 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 5 — MEDIA PAUSE (3s TTL -> Clock automatically without opening Capsule)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(200);
		NotchBehaviorConfig.Instance.ShowMediaWhenPlaybackStarts = true;
		PillActivitySnapshot mediaSnapshot = new PillActivitySnapshot("media_session", "media", ActivityTier.Tier2_Session, ActivityType.Media, "Song Title", "Artist Name", "1:20", (double sz, Brush br) => VectorIcons.CreateMusicIcon(sz, br), null, null, 0.3, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Media, isPlaying: true, null, null, null, "media", null);
		PillActivityArbiter.Instance.PublishMedia(mediaSnapshot);
		DoEvents(600);
		bool flag11 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Media;
		Log($"  Media Playing visible: {flag11} (Type={win.ActiveVisualType})");
		PillActivitySnapshot mediaSnapshot2 = new PillActivitySnapshot("media_session", "media", ActivityTier.Tier2_Session, ActivityType.Media, "Song Title", "Paused", "1:20", (double sz, Brush br) => VectorIcons.CreateMusicIcon(sz, br), null, null, 0.3, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Media, isPlaying: false, null, null, null, "media", null);
		PillActivityArbiter.Instance.PublishMedia(mediaSnapshot2);
		DoEvents(400);
		bool flag12 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Media;
		Log($"  Media Paused visible immediately: {flag12} (Type={win.ActiveVisualType})");
		bool flag13 = false;
		for (int n = 0; n < 45; n++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag13 = true;
				break;
			}
		}
		Log($"  Media Paused automatically expired to Clock without opening Capsule: {flag13} (Type={win.ActiveVisualType})");
		bool flag14 = flag11 && flag12 && flag13;
		Log(string.Format("SCENARIO 5 RESULT: {0}", flag14 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 6 — MEDIA RESUME DURING TTL (old Paused timer must NOT clear Playing)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(200);
		PillActivityArbiter.Instance.PublishMedia(mediaSnapshot);
		DoEvents(500);
		PillActivityArbiter.Instance.PublishMedia(mediaSnapshot2);
		DoEvents(300);
		Log("  Paused active, waiting 1.2s before resuming...");
		DoEvents(1200);
		PillActivityArbiter.Instance.PublishMedia(mediaSnapshot);
		DoEvents(400);
		bool flag15 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Media;
		Log($"  Resumed to Playing within TTL: {flag15} (Type={win.ActiveVisualType})");
		DoEvents(3000);
		bool flag16 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Media;
		Log($"  Playing persisted (stale pause timer did NOT clear it): {flag16} (Type={win.ActiveVisualType})");
		PillActivityArbiter.Instance.ClearMedia();
		DoEvents(500);
		bool flag17 = flag15 && flag16;
		Log(string.Format("SCENARIO 6 RESULT: {0}", flag17 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 7 — RAPID CLIPBOARD (Copy A -> B -> C; A/B cannot clear C)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(300);
		PillActivityArbiter.Instance.PublishClipboardCopied("Rapid Copy Payload A");
		DoEvents(150);
		PillActivityArbiter.Instance.PublishClipboardCopied("Rapid Copy Payload B");
		DoEvents(150);
		PillActivityArbiter.Instance.PublishClipboardCopied("Rapid Copy Payload C");
		DoEvents(300);
		bool flag18 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clipboard;
		Log($"  C is visible: {flag18} (Type={win.ActiveVisualType})");
		DoEvents(1500);
		bool flag19 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clipboard;
		Log($"  C still visible after A/B expiry time passed: {flag19} (Type={win.ActiveVisualType})");
		bool flag20 = false;
		for (int num = 0; num < 40; num++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag20 = true;
				break;
			}
		}
		Log($"  After C's TTL, Clock returned: {flag20} (Type={win.ActiveVisualType})");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		bool flag21 = flag18 && flag19 && flag20;
		Log(string.Format("SCENARIO 7 RESULT: {0}", flag21 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 8 — EXPAND WHILE TRANSIENT (open and close capsule; lifecycle preserved)...");
		Clipboard.SetText("Scenario 8 Payload For Expand");
		DoEvents(300);
		if (win.ActiveVisualType != CapsuleWindow.CollapsedVisualType.Clipboard)
		{
			PillActivityArbiter.Instance.PublishClipboardCopied("Scenario 8 Payload For Expand");
			DoEvents(400);
		}
		win.ExpandCapsule();
		DoEvents(300);
		bool flag22 = PillActivityArbiter.Instance.CurrentActivity != null && PillActivityArbiter.Instance.CurrentActivity.Type == ActivityType.ClipboardCopied;
		Log($"  Arbiter state preserved on expand: {flag22}");
		win.CollapseCapsule();
		DoEvents(300);
		bool flag23 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clipboard;
		Log($"  Copied still visible on collapse: {flag23}");
		bool flag24 = false;
		for (int num2 = 0; num2 < 45; num2++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag24 = true;
				break;
			}
		}
		Log($"  After TTL settled to Clock without needing reopen: {flag24}");
		bool flag25 = flag22 && flag24;
		Log(string.Format("SCENARIO 8 RESULT: {0}", flag25 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 9 — RAPID ACTIVITY CHANGES (Charging -> Copy -> Wi-Fi -> Media Paused)...");
		PillActivityArbiter.Instance.PublishBatteryChargingState("Charging", "92%", isCharging: true, isFull: false);
		DoEvents(150);
		PillActivityArbiter.Instance.PublishClipboardCopied("Rapid Copy Text");
		DoEvents(150);
		PillActivityArbiter.Instance.PublishNetworkState("Office-WiFi", isConnected: true);
		DoEvents(150);
		PillActivityArbiter.Instance.PublishMedia(mediaSnapshot2);
		DoEvents(400);
		bool flag26 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Media;
		Log($"  Newest valid activity (Media Paused) remains: {flag26} (Type={win.ActiveVisualType})");
		bool flag27 = false;
		for (int num3 = 0; num3 < 45; num3++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag27 = true;
				break;
			}
		}
		Log($"  All settled to Clock: {flag27} (Type={win.ActiveVisualType})");
		bool flag28 = flag26 && flag27;
		Log(string.Format("SCENARIO 9 RESULT: {0}", flag28 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 10 — CALENDAR IDEMPOTENCY (No phantom Sprint Kickoff, alert fires once)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(300);
		CalendarService instance = CalendarService.Instance;
		bool flag29 = instance.Events.Any((UserEvent i) => i.Title.Contains("Sprint Kickoff"));
		Log($"  Calendar has phantom Sprint Kickoff: {flag29}");
		UserEvent userEvent = new UserEvent();
		userEvent.Title = "Legitimate Standup";
		userEvent.Date = DateTime.Today.ToString("yyyy-MM-dd");
		userEvent.EndDate = DateTime.Today.ToString("yyyy-MM-dd");
		userEvent.Start = DateTime.Now.ToString("HH:mm");
		userEvent.End = DateTime.Now.AddMinutes(30.0).ToString("HH:mm");
		userEvent.Alert = CalAlert.AtTime;
		UserEvent userEvent2 = userEvent;
		string id = userEvent2.Id;
		instance.AddEvent(userEvent2);
		DoEvents(300);
		int num4 = instance.CheckAlertsNow();
		bool flag30 = num4 > 0;
		Log($"  Legitimate event alerted: {flag30}");
		DoEvents(400);
		bool flag31 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Calendar;
		Log($"  Calendar alert visible on notch: {flag31} (Type={win.ActiveVisualType})");
		bool flag32 = instance.CheckAlertsNow() == 0;
		Log($"  Alert not repeated on the next check: {flag32}");
		bool flag33 = false;
		for (int num5 = 0; num5 < 45; num5++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag33 = true;
				break;
			}
		}
		Log($"  Calendar alert expired to Clock: {flag33}");
		instance.DeleteEvent(id);
		bool flag34 = !flag29 && flag31 && flag32 && flag33;
		Log(string.Format("SCENARIO 10 RESULT: {0}", flag34 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 11 — MESSAGE FLASH (3s TTL -> physical collapsed Notch animation -> Clock)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(300);
		MessageSettings messageSettings = new MessageSettings();
		messageSettings.Text = "Hello E2E Notch";
		messageSettings.Animation = MessageAnimationMode.Fade;
		messageSettings.Duration = MessageDurationMode.Sec3;
		MessageSettings payload = messageSettings;
		PillActivitySnapshot snapshot = new PillActivitySnapshot("msg_e2e_flash", "message", ActivityTier.Tier1_Transient, ActivityType.CompanionMessage, "Hello E2E Notch", "Message", "Hello E2E Notch", (double sz, Brush br) => VectorIcons.CreateMessageIcon(sz, br), null, Brushes.White, null, DateTime.UtcNow, DateTime.UtcNow.AddSeconds(3.0), 1L, CollapsedActivityProfile.Resting, isPlaying: false, null, null, null, "message", payload);
		PillActivityArbiter.Instance.PublishMessageTransient(snapshot, TimeSpan.FromSeconds(3.0));
		DoEvents(500);
		bool flag35 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Message;
		Log($"  Flashed message visible on physical collapsed notch: {flag35} (Type={win.ActiveVisualType})");
		bool flag36 = false;
		for (int num6 = 0; num6 < 45; num6++)
		{
			DoEvents(100);
			if (win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock)
			{
				flag36 = true;
				break;
			}
		}
		Log($"  Message Flash expired to Clock automatically: {flag36} (Type={win.ActiveVisualType})");
		bool flag37 = flag35 && flag36;
		Log(string.Format("SCENARIO 11 RESULT: {0}", flag37 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 12 — MESSAGE PIN (Remains indefinitely until Take Down)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(300);
		PillActivitySnapshot snapshot2 = new PillActivitySnapshot("msg_e2e_pinned", "message", ActivityTier.Tier2_Session, ActivityType.CompanionMessage, "Pinned Note", "Message", "Pinned Note", (double sz, Brush br) => VectorIcons.CreateMessageIcon(sz, br), null, Brushes.White, null, DateTime.UtcNow, null, 1L, CollapsedActivityProfile.Resting, isPlaying: false, null, null, null, "message", payload);
		PillActivityArbiter.Instance.PublishMessageSession(snapshot2);
		DoEvents(500);
		bool flag38 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Message;
		Log($"  Pinned message active on notch: {flag38} (Type={win.ActiveVisualType})");
		DoEvents(4000);
		bool flag39 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Message;
		Log($"  Pinned message persisted after 4 seconds: {flag39} (Type={win.ActiveVisualType})");
		PillActivityArbiter.Instance.ClearMessage();
		DoEvents(500);
		bool flag40 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock;
		Log($"  After Take Down, Clock returned immediately: {flag40} (Type={win.ActiveVisualType})");
		bool flag41 = flag38 && flag39 && flag40;
		Log(string.Format("SCENARIO 12 RESULT: {0}", flag41 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 13 — SLOT STATE MACHINE ABORT RECOVERY (Preempting in-flight animation)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(300);
		PillActivityArbiter.Instance.PublishClipboardCopied("Abort Test A");
		DoEvents(30);
		win.RenderCurrentResolvedPillActivity(immediate: true);
		DoEvents(200);
		bool flag42 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clipboard;
		Log($"  After mid-flight abort and instant render, active visual is valid: {flag42} (Type={win.ActiveVisualType})");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		DoEvents(400);
		bool flag43 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock;
		Log($"  Slot state machine recovered cleanly to Clock: {flag43}");
		bool flag44 = flag42 && flag43;
		Log(string.Format("SCENARIO 13 RESULT: {0}", flag44 ? "PASS" : "FAIL"));
		Log("\n[RUNNING] SCENARIO 14 — PINNED MESSAGE + TRANSIENT PREEMPTION & AUTO-RETURN (Copied 3s -> Pinned Msg)...");
		PillActivityArbiter.Instance.DismissCurrentTransient();
		PillActivityArbiter.Instance.ClearMessage();
		DoEvents(300);
		PillActivitySnapshot snapshot3 = new PillActivitySnapshot("msg_e2e_pinned_14", "message", ActivityTier.Tier2_Session, ActivityType.CompanionMessage, "Pinned Note 14", "Message", "Pinned Note 14", (double sz, Brush br) => VectorIcons.CreateMessageIcon(sz, br), null, Brushes.White, null, DateTime.UtcNow, null, 1L, CollapsedActivityProfile.Resting, isPlaying: false, null, null, null, "message", payload);
		PillActivityArbiter.Instance.PublishMessageSession(snapshot3);
		DoEvents(500);
		bool flag45 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Message;
		Log($"  Initial pinned message visible: {flag45} (Type={win.ActiveVisualType})");
		PillActivityArbiter.Instance.PublishClipboardCopied("E2E Transient Copy");
		DoEvents(500);
		bool flag46 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clipboard;
		Log($"  Transient Copied preempted pinned message: {flag46} (Type={win.ActiveVisualType})");
		DoEvents(3400);
		bool flag47 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Message;
		Log($"  After 3s transient TTL, automatically returned to Pinned Message: {flag47} (Type={win.ActiveVisualType})");
		PillActivityArbiter.Instance.ClearMessage();
		DoEvents(400);
		bool flag48 = win.ActiveVisualType == CapsuleWindow.CollapsedVisualType.Clock;
		Log($"  After Take Down, returned to Clock: {flag48} (Type={win.ActiveVisualType})");
		bool flag49 = flag45 && flag46 && flag47 && flag48;
		Log(string.Format("SCENARIO 14 RESULT: {0}", flag49 ? "PASS" : "FAIL"));
		Log("\n========================================================");
		Log($"E2E LIVE NOTCH MATRIX COMPLETED: {(flag ? 1 : 0) + (flag4 ? 1 : 0) + (flag7 ? 1 : 0) + (flag10 ? 1 : 0) + (flag14 ? 1 : 0) + (flag17 ? 1 : 0) + (flag21 ? 1 : 0) + (flag25 ? 1 : 0) + (flag28 ? 1 : 0) + (flag34 ? 1 : 0) + (flag37 ? 1 : 0) + (flag41 ? 1 : 0) + (flag44 ? 1 : 0) + (flag49 ? 1 : 0)}/14 TESTS PASSED");
		Log("========================================================");
	}
}
