using System;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Message;
using WindowsBuddy.Tools.Timer;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class EdgeLightQa
{
	private static int _fails;

	private static void Check(bool ok, string what)
	{
		Console.WriteLine((ok ? "  PASS " : "  FAIL ") + what);
		if (!ok)
		{
			_fails++;
		}
	}

	public static int RunDefaults()
	{
		AppPaths.UseTempFolder("defaults-qa");
		_fails = 0;
		Console.WriteLine("DEFAULTS-QA: fresh profile");
		NotchConfig instance = NotchConfig.Instance;
		ToolRegistry instance2 = ToolRegistry.Instance;
		Check(instance.Style == "Compact", "notch shape Compact (" + instance.Style + ")");
		Check(Math.Abs(instance.SizeScale - 0.9) < 0.001, "size 90% (" + instance.SizeScale + ")");
		Check(instance.RestingLayout == "Corners", "resting layout Corners (" + instance.RestingLayout + ")");
		Check(instance.AnimationSpeed == "Fast", "animation speed Fast (" + instance.AnimationSpeed + ")");
		Check(instance2.HoverDelayMs == 0 && !instance2.OpensOnClick, "open on hover, fast preset (" + instance2.HoverDelayMs + " ms)");
		Check(instance.EdgeLightEnabled, "Edge Light on");
		Check(instance.EdgeLightStyle == "Twin comets", "Edge Light style Twin comets (" + instance.EdgeLightStyle + ")");
		Check(instance.EdgeLightWhen == "Always", "Edge Light when Always (" + instance.EdgeLightWhen + ")");
		Check(instance.EdgeLightColor == "White", "Edge Light colour white (" + instance.EdgeLightColor + ")");
		NotchConfigModel notchConfigModel = new NotchConfigModel();
		Check(notchConfigModel.Style == "Default" && notchConfigModel.SizeScale == 0.0 && notchConfigModel.AnimationSpeed == "Normal" && !notchConfigModel.EdgeLightEnabled && notchConfigModel.EdgeLightStyle == "Comet" && notchConfigModel.EdgeLightAnimationMode == "Events", "a model read from an older file keeps the previous defaults");
		string input = new JavaScriptSerializer().Serialize(new
		{
			Style = "Wide",
			EdgeLightEnabled = false
		});
		NotchConfigModel notchConfigModel2 = new JavaScriptSerializer().Deserialize<NotchConfigModel>(input);
		Check(notchConfigModel2.Style == "Wide" && !notchConfigModel2.EdgeLightEnabled && notchConfigModel2.AnimationSpeed == "Normal" && notchConfigModel2.SizeScale == 0.0, "a partial existing file keeps its own values and old defaults for missing keys");
		Console.WriteLine((_fails == 0) ? "DEFAULTS-QA PASSED" : ("DEFAULTS-QA FAILED: " + _fails));
		if (_fails != 0)
		{
			return 1;
		}
		return 0;
	}

	private static void Pump(int ms)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ms);
		do
		{
			DispatcherFrame frame = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
			{
				frame.Continue = false;
			});
			Dispatcher.PushFrame(frame);
			if (ms > 0)
			{
				Thread.Sleep(4);
			}
		}
		while (DateTime.UtcNow < dateTime);
	}

	private static T Field<T>(object o, string name)
	{
		Type type = o.GetType();
		while (type != null)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				return (T)field.GetValue(o);
			}
			type = type.BaseType;
		}
		throw new MissingFieldException(o.GetType().Name, name);
	}

	private static void CheckFrame(CapsuleWindow win, string label)
	{
		NotchConfig instance = NotchConfig.Instance;
		Grid grid = Field<Grid>(win, "_capsuleCard");
		NotchEdgeLight notchEdgeLight = Field<NotchEdgeLight>(win, "_edgeLight");
		if (!(Field<IEdgeLightSurface>(notchEdgeLight, "_surface") is EdgeLightComposition edgeLightComposition))
		{
			Check(ok: false, label + ": no compositor surface");
			return;
		}
		grid.UpdateLayout();
		double m = PresentationSource.FromVisual(win).CompositionTarget.TransformToDevice.M11;
		Point point = grid.PointToScreen(new Point(0.0, 0.0));
		EdgeLightOutline edgeLightOutline = EdgeLightOutline.For(instance.Style, grid.ActualWidth, grid.ActualHeight, instance.CornerRadius);
		Point[] appliedPathScreen = edgeLightComposition.AppliedPathScreen;
		if (edgeLightOutline == null || appliedPathScreen == null || appliedPathScreen.Length != edgeLightOutline.Points.Length)
		{
			Check(ok: false, label + ": light path missing or a different shape (" + ((appliedPathScreen == null) ? "none" : appliedPathScreen.Length.ToString()) + " vs " + ((edgeLightOutline == null) ? "none" : edgeLightOutline.Points.Length.ToString()) + " points)");
			return;
		}
		double num = 0.0;
		for (int i = 0; i < appliedPathScreen.Length; i++)
		{
			double num2 = appliedPathScreen[i].X - (point.X + edgeLightOutline.Points[i].X * m);
			double num3 = appliedPathScreen[i].Y - (point.Y + edgeLightOutline.Points[i].Y * m);
			num = Math.Max(num, Math.Sqrt(num2 * num2 + num3 * num3));
		}
		bool flag = notchEdgeLight.IsShown && notchEdgeLight.IsAnimating && notchEdgeLight.RunningAnimations > 0;
		if (num > 1.0)
		{
			EdgeLightOutline edgeLightOutline2 = Field<EdgeLightOutline>(notchEdgeLight, "_outline");
			Console.WriteLine(string.Concat("  alignment detail: actual=", grid.ActualWidth, ",", grid.ActualHeight, " requested=", grid.Width, ",", grid.Height, " outline=", edgeLightOutline2.Width, ",", edgeLightOutline2.Height, " origin=", point, " offset=", Field<Point>(notchEdgeLight, "_offset"), " scale=", m));
		}
		Check(num <= 1.0 && flag, $"{label}: light {grid.ActualWidth:0}x{grid.ActualHeight:0} DIP, worst point error {num:0.00} px, running {flag}");
	}

	private static void State(CapsuleWindow win, string label)
	{
		NotchEdgeLight o = Field<NotchEdgeLight>(win, "_edgeLight");
		EdgeLightComposition edgeLightComposition = Field<IEdgeLightSurface>(o, "_surface") as EdgeLightComposition;
		int num = edgeLightComposition?.BuildCount ?? 0;
		int[] array = new int[4] { 0, 16, 50, 150 };
		foreach (int num2 in array)
		{
			Pump(num2);
			CheckFrame(win, label + " +" + num2 + " ms");
		}
		if (edgeLightComposition != null)
		{
			Check(edgeLightComposition.BuildCount == num, label + ": no stroke rebuild (comet keeps its progress)");
		}
	}

	private static void WaitResting(CapsuleWindow win)
	{
		Grid grid = Field<Grid>(win, "_capsuleCard");
		NotchEdgeLight notchEdgeLight = Field<NotchEdgeLight>(win, "_edgeLight");
		DateTime dateTime = DateTime.UtcNow.AddSeconds(15.0);
		do
		{
			Pump(100);
		}
		while ((!notchEdgeLight.IsShown || grid.ActualWidth > NotchConfig.Instance.GetCollapsedWidth() + 2.0) && DateTime.UtcNow < dateTime);
		Pump(400);
	}

	private static void FrameStep()
	{
		Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, (Action)delegate
		{
		});
		Thread.Sleep(4);
	}

	private static double FrameError(CapsuleWindow win)
	{
		NotchConfig instance = NotchConfig.Instance;
		Grid grid = Field<Grid>(win, "_capsuleCard");
		NotchEdgeLight notchEdgeLight = Field<NotchEdgeLight>(win, "_edgeLight");
		if (!(Field<IEdgeLightSurface>(notchEdgeLight, "_surface") is EdgeLightComposition edgeLightComposition) || !notchEdgeLight.IsShown)
		{
			return -1.0;
		}
		grid.UpdateLayout();
		double m = PresentationSource.FromVisual(win).CompositionTarget.TransformToDevice.M11;
		Point point = grid.PointToScreen(new Point(0.0, 0.0));
		EdgeLightOutline edgeLightOutline = EdgeLightOutline.For(instance.Style, grid.ActualWidth, grid.ActualHeight, instance.CornerRadius);
		Point[] appliedPathScreen = edgeLightComposition.AppliedPathScreen;
		if (edgeLightOutline == null || appliedPathScreen == null || appliedPathScreen.Length != edgeLightOutline.Points.Length)
		{
			return 9999.0;
		}
		double num = 0.0;
		for (int i = 0; i < appliedPathScreen.Length; i++)
		{
			double num2 = appliedPathScreen[i].X - (point.X + edgeLightOutline.Points[i].X * m);
			double num3 = appliedPathScreen[i].Y - (point.Y + edgeLightOutline.Points[i].Y * m);
			num = Math.Max(num, Math.Sqrt(num2 * num2 + num3 * num3));
		}
		Path path = Field<Path>(win, "_notchSilhouettePath");
		if (path != null && path.Data != null)
		{
			Rect bounds = path.Data.Bounds;
			num = Math.Max(num, Math.Max(Math.Abs(bounds.Width - grid.ActualWidth), Math.Abs(bounds.Height - grid.ActualHeight)) * m);
		}
		return num;
	}

	private static void Watch(CapsuleWindow win, string label, int ms)
	{
		Grid grid = Field<Grid>(win, "_capsuleCard");
		double actualWidth = grid.ActualWidth;
		double num = actualWidth;
		double num2 = actualWidth;
		double num3 = 0.0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ms);
		while (DateTime.UtcNow < dateTime)
		{
			FrameStep();
			num4++;
			double num7 = FrameError(win);
			num = Math.Min(num, grid.ActualWidth);
			num2 = Math.Max(num2, grid.ActualWidth);
			if (!(num7 < 0.0))
			{
				num5++;
				if (num7 > num3)
				{
					num3 = num7;
				}
				if (num7 > 1.0)
				{
					num6++;
				}
			}
		}
		double num8 = FrameError(win);
		bool flag = num6 == 0 && num8 >= 0.0 && num8 <= 1.0;
		if (!flag)
		{
			Path path = Field<Path>(win, "_notchSilhouettePath");
			NotchEdgeLight o = Field<NotchEdgeLight>(win, "_edgeLight");
			EdgeLightOutline edgeLightOutline = ((!(Field<IEdgeLightSurface>(o, "_surface") is EdgeLightComposition o2)) ? null : Field<EdgeLightOutline>(o2, "_drawnOutline"));
			Console.WriteLine(string.Format("    detail: card {0:0.##}x{1:0.##}, silhouette {2:0.##}x{3:0.##}, light outline {4}", grid.ActualWidth, grid.ActualHeight, path.Data.Bounds.Width, path.Data.Bounds.Height, (edgeLightOutline == null) ? "none" : (edgeLightOutline.Width.ToString("0.##") + "x" + edgeLightOutline.Height.ToString("0.##"))));
		}
		Check(flag, string.Format("{0}: notch {1:0}->{2:0} DIP (range {3:0}-{4:0}), {5} frames, light on the outline in {6}/{7} shown frames (worst {8:0.0} px), settled {9}", label, actualWidth, grid.ActualWidth, num, num2, num4, num5 - num6, num5, num3, (num8 < 0.0) ? "light not showing" : (num8.ToString("0.0") + " px")));
	}

	private static void Scenario(CapsuleWindow win, string name, bool whileOpen, Action enter, Action change, Action leave)
	{
		if (whileOpen)
		{
			win.RequestExpand("Edge-QA");
			Pump(500);
			enter();
			Pump(200);
			win.RequestCollapse("Edge-QA");
			Watch(win, name + " (started while the panel was open, then closed)", 1800);
		}
		else
		{
			Pump(300);
			enter();
			Watch(win, name + " enter", 900);
		}
		if (change != null)
		{
			change();
			Watch(win, name + " change", 900);
		}
		if (whileOpen)
		{
			win.RequestExpand("Edge-QA");
			Pump(500);
			leave();
			Pump(200);
			win.RequestCollapse("Edge-QA");
			Watch(win, name + " (ended while the panel was open, then closed)", 1800);
		}
		else
		{
			leave();
			Watch(win, name + " leave", 1200);
		}
	}

	private static void Activities(CapsuleWindow win)
	{
		Console.WriteLine("  -- live activities (per-frame, light vs notch) --");
		PillActivityArbiter arb = PillActivityArbiter.Instance;
		Func<string, string, bool, PillActivitySnapshot> media = (string title, string artist, bool playing) => new PillActivitySnapshot("media_session", "media", ActivityTier.Tier2_Session, ActivityType.Media, title, artist, "1:20", (double sz, Brush br) => VectorIcons.CreateMusicIcon(sz, br), null, null, 0.3, DateTime.UtcNow, null, 0L, CollapsedActivityProfile.Media, playing, null, null, null, "media", null);
		NotchConfig.Instance.ShowTimersInNotch = true;
		TimerService timers = TimerService.Instance;
		Scenario(win, "timer tool countdown", whileOpen: false, delegate
		{
			timers.SetCountdownMinutes(25);
			timers.StartCountdown();
		}, delegate
		{
			timers.PauseCountdown();
		}, delegate
		{
			timers.ResetCountdown();
		});
		Scenario(win, "timer tool countdown", whileOpen: true, delegate
		{
			ToolRouter.Instance.RouteTo("timer");
			timers.SetCountdownMinutes(25);
			timers.StartCountdown();
		}, null, delegate
		{
			timers.ResetCountdown();
		});
		Scenario(win, "timer", whileOpen: false, delegate
		{
			arb.PublishTimerSession("20:37", isPaused: false, 0.5);
		}, delegate
		{
			arb.UpdateTimerTick("20:36", isPaused: true, 0.5);
		}, delegate
		{
			arb.ClearTimer();
		});
		Scenario(win, "timer", whileOpen: true, delegate
		{
			arb.PublishTimerSession("20:37", isPaused: false, 0.5);
		}, null, delegate
		{
			arb.ClearTimer();
		});
		Scenario(win, "music", whileOpen: false, delegate
		{
			arb.PublishMedia(media("Starboy", "The Weeknd", arg3: true));
		}, delegate
		{
			arb.PublishMedia(media("Blinding Lights", "The Weeknd", arg3: true));
		}, delegate
		{
			arb.ClearMedia();
		});
		Scenario(win, "music", whileOpen: true, delegate
		{
			arb.PublishMedia(media("Starboy", "The Weeknd", arg3: true));
		}, null, delegate
		{
			arb.ClearMedia();
		});
		Scenario(win, "charging", whileOpen: false, delegate
		{
			arb.PublishBatteryChargingState("Charging", "92%", isCharging: true, isFull: false);
		}, null, delegate
		{
			arb.DismissCurrentTransient();
		});
		Scenario(win, "volume", whileOpen: false, delegate
		{
			arb.PublishVolume(60, isMuted: false);
		}, delegate
		{
			arb.PublishVolume(0, isMuted: true);
		}, delegate
		{
			arb.DismissCurrentTransient();
		});
		Scenario(win, "copied", whileOpen: false, delegate
		{
			arb.PublishClipboardCopied("A long copied text for the notch");
		}, null, delegate
		{
			arb.DismissCurrentTransient();
		});
		Scenario(win, "reminder", whileOpen: false, delegate
		{
			arb.PublishCalendarAlert("Stand-up in 5 minutes", "Calendar", "qa-1");
		}, null, delegate
		{
			arb.DismissCurrentTransient();
		});
		MessageService msg = MessageService.Instance;
		Scenario(win, "message flash", whileOpen: false, delegate
		{
			msg.ShowNotice("Back in five minutes, please wait", TimeSpan.FromSeconds(2.0));
		}, null, delegate
		{
			msg.TakeDown();
		});
		msg.Settings.Text = "Meeting, do not disturb";
		msg.Settings.RepeatEnabled = true;
		Scenario(win, "message pinned", whileOpen: false, delegate
		{
			msg.Flash();
		}, null, delegate
		{
			msg.TakeDown();
		});
		msg.Settings.RepeatEnabled = false;
		Pump(1500);
	}

	public static int Run(CapsuleWindow win, string[] args)
	{
		_fails = 0;
		NotchConfig instance = NotchConfig.Instance;
		Console.WriteLine("EDGE-QA: Edge Light follows the notch outline through every geometry change");
		instance.EdgeLightEnabled = true;
		instance.EdgeLightColor = "White";
		instance.EdgeLightStyle = "Twin comets";
		instance.EdgeLightWhen = "Always";
		instance.EdgeLightIntensity = 1.0;
		instance.Style = "Compact";
		instance.SizeScale = 0.9;
		win.RequestCollapse("Edge-QA");
		WaitResting(win);
		NotchEdgeLight notchEdgeLight = Field<NotchEdgeLight>(win, "_edgeLight");
		Console.WriteLine("  backend " + notchEdgeLight.Backend + ", animating " + notchEdgeLight.IsAnimating + ", shown " + notchEdgeLight.IsShown);
		Check(notchEdgeLight.Backend == "compositor" && notchEdgeLight.IsShown && notchEdgeLight.IsAnimating, "light is running on the compositor before the changes");
		State(win, "start 90%");
		instance.SizeScale = 1.2;
		State(win, "size 90>120");
		instance.SizeScale = 0.9;
		State(win, "size 120>90");
		instance.SizeScale = 1.2;
		instance.SizeScale = 0.9;
		instance.SizeScale = 1.1;
		State(win, "size slider drag");
		instance.SizeScale = 0.9;
		State(win, "size back to 90");
		string[] array = new string[5] { "Wide", "Default", "Circular", "Minimal", "Compact" };
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = (instance.Style = array[i]);
			Pump(0);
			State(win, "shape " + text2);
		}
		instance.EdgeLightStyle = "Comet";
		State(win, "light style Comet");
		instance.EdgeLightStyle = "Twin comets";
		instance.SizeScale = 1.2;
		instance.Style = "Wide";
		State(win, "style + size + shape together");
		instance.SizeScale = 0.9;
		instance.Style = "Compact";
		State(win, "back to Compact 90%");
		win.RequestExpand("Edge-QA");
		Pump(700);
		win.RequestCollapse("Edge-QA");
		WaitResting(win);
		State(win, "after open/close");
		win.RequestExpand("Edge-QA");
		Pump(300);
		instance.SizeScale = 1.2;
		Pump(300);
		win.RequestCollapse("Edge-QA");
		WaitResting(win);
		State(win, "size changed while open");
		instance.SizeScale = 0.9;
		State(win, "final 90%");
		PetPreferences instance2 = PetPreferences.Instance;
		bool enabled = instance2.Enabled;
		instance2.Enabled = true;
		Pump(900);
		WaitResting(win);
		Check(notchEdgeLight.IsShown && notchEdgeLight.IsAnimating, "pet on: Edge Light is shown and running (shown " + notchEdgeLight.IsShown + ", animating " + notchEdgeLight.IsAnimating + ")");
		State(win, "pet on");
		instance.SizeScale = 1.2;
		State(win, "pet on: size 90>120");
		instance.Style = "Wide";
		State(win, "pet on: shape Wide");
		instance.Style = "Compact";
		instance.SizeScale = 0.9;
		State(win, "pet on: back to Compact 90%");
		win.RequestExpand("Edge-QA");
		Pump(700);
		win.RequestCollapse("Edge-QA");
		WaitResting(win);
		Check(notchEdgeLight.IsShown, "pet on: the light is back after open/close");
		State(win, "pet on: after open/close");
		instance2.Enabled = enabled;
		Pump(900);
		WaitResting(win);
		Check(notchEdgeLight.IsShown, "pet back to " + (enabled ? "on" : "off") + ": the light stays on");
		State(win, "pet restored");
		Activities(win);
		Console.WriteLine((_fails == 0) ? "EDGE-QA PASSED" : ("EDGE-QA FAILED: " + _fails));
		if (_fails != 0)
		{
			return 1;
		}
		return 0;
	}
}
