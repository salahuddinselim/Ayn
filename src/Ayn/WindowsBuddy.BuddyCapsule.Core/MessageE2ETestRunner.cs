using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.State;
using WindowsBuddy.Services;
using WindowsBuddy.Tools.Message;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class MessageE2ETestRunner
{
	private struct Pt
	{
		public int X;

		public int Y;
	}

	private struct Rc
	{
		public int L;

		public int T;

		public int R;

		public int B;
	}

	private struct PointerInfo
	{
		public uint Type;

		public uint Id;

		public uint FrameId;

		public uint Flags;

		public IntPtr Device;

		public IntPtr Hwnd;

		public Pt Pixel;

		public Pt Himetric;

		public Pt PixelRaw;

		public Pt HimetricRaw;

		public uint Time;

		public uint HistoryCount;

		public int InputData;

		public uint KeyStates;

		public ulong PerfCount;

		public int ButtonChange;
	}

	private struct PointerTouchInfo
	{
		public PointerInfo Info;

		public uint TouchFlags;

		public uint TouchMask;

		public Rc Contact;

		public Rc ContactRaw;

		public uint Orientation;

		public uint Pressure;
	}

	private static readonly string Dir = Path.Combine(Path.GetTempPath(), "WindowsBuddyQA", "message-e2e");

	private static int _pass;

	private static int _fail;

	private static bool _touchReady;

	private static bool MessageOnNotch
	{
		get
		{
			PillActivitySnapshot currentActivity = PillActivityArbiter.Instance.CurrentActivity;
			if (currentActivity != null)
			{
				return currentActivity.Type == ActivityType.CompanionMessage;
			}
			return false;
		}
	}

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

	[DllImport("user32.dll")]
	private static extern bool InitializeTouchInjection(uint maxCount, uint mode);

	[DllImport("user32.dll")]
	private static extern bool InjectTouchInput(uint count, [In] PointerTouchInfo[] contacts);

	private static void Log(string msg)
	{
		Console.WriteLine(msg);
		try
		{
			File.AppendAllText(Path.Combine(Dir, "results.log"), msg + Environment.NewLine);
		}
		catch
		{
		}
	}

	private static void Check(string name, bool ok)
	{
		Log((ok ? "[PASS] " : "[FAIL] ") + name);
		if (ok)
		{
			_pass++;
		}
		else
		{
			_fail++;
		}
	}

	private static void DoEvents(int ms)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ms);
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

	private static T Field<T>(object o, string name) where T : class
	{
		FieldInfo field = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
		if (!(field != null))
		{
			return null;
		}
		return field.GetValue(o) as T;
	}

	private static object Call(object o, string name, params object[] args)
	{
		return o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
	}

	private static bool HitsItself(Window win, FrameworkElement el, string name)
	{
		if (el == null || !el.IsVisible)
		{
			Log("  " + name + ": not visible");
			return false;
		}
		System.Windows.Point point = el.TranslatePoint(new System.Windows.Point(el.ActualWidth / 2.0, el.ActualHeight / 2.0), win);
		HitTestResult hitTestResult = VisualTreeHelper.HitTest(win, point);
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		int num = 0;
		DependencyObject dependencyObject = hitTestResult?.VisualHit;
		while (dependencyObject != null && num < 12)
		{
			if (dependencyObject == el)
			{
				flag = true;
				break;
			}
			stringBuilder.Append(dependencyObject.GetType().Name).Append(" < ");
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			num++;
		}
		Log(string.Format("  {0}: hit-test {1} | IsHitTestVisible={2} IsEnabled={3} Opacity={4:F2} | chain: {5}", name, flag ? "reaches it" : "BLOCKED", el.IsHitTestVisible, el.IsEnabled, el.Opacity, stringBuilder));
		return flag;
	}

	private static System.Windows.Point ScreenCenter(FrameworkElement el)
	{
		return el.PointToScreen(new System.Windows.Point(el.ActualWidth / 2.0, el.ActualHeight / 2.0));
	}

	private static void MouseClick(FrameworkElement el)
	{
		System.Windows.Point point = ScreenCenter(el);
		SetCursorPos((int)point.X, (int)point.Y);
		DoEvents(150);
		mouse_event(2u, 0u, 0u, 0u, UIntPtr.Zero);
		DoEvents(60);
		mouse_event(4u, 0u, 0u, 0u, UIntPtr.Zero);
	}

	private static bool TouchTap(FrameworkElement el)
	{
		if (!_touchReady)
		{
			_touchReady = InitializeTouchInjection(1u, 1u);
		}
		if (!_touchReady)
		{
			return false;
		}
		System.Windows.Point point = ScreenCenter(el);
		int num = (int)point.X;
		int num2 = (int)point.Y;
		PointerTouchInfo pointerTouchInfo = default(PointerTouchInfo);
		pointerTouchInfo.Info.Type = 2u;
		pointerTouchInfo.Info.Pixel = new Pt
		{
			X = num,
			Y = num2
		};
		pointerTouchInfo.TouchMask = 7u;
		pointerTouchInfo.Contact = new Rc
		{
			L = num - 2,
			T = num2 - 2,
			R = num + 2,
			B = num2 + 2
		};
		pointerTouchInfo.Orientation = 90u;
		pointerTouchInfo.Pressure = 32000u;
		pointerTouchInfo.Info.Flags = 65542u;
		bool flag = InjectTouchInput(1u, new PointerTouchInfo[1] { pointerTouchInfo });
		DoEvents(60);
		pointerTouchInfo.Info.Flags = 262144u;
		return flag & InjectTouchInput(1u, new PointerTouchInfo[1] { pointerTouchInfo });
	}

	private static void Press(string mode, FrameworkElement el)
	{
		if (mode == "mouse")
		{
			MouseClick(el);
		}
		else if (!TouchTap(el))
		{
			Log("  touch injection unavailable");
		}
	}

	private static void Screenshot(string name)
	{
		try
		{
			Rectangle bounds = Screen.PrimaryScreen.Bounds;
			double num = ((SystemParameters.PrimaryScreenWidth > 0.0) ? ((double)bounds.Width / SystemParameters.PrimaryScreenWidth) : 1.0);
			int num2 = (int)(800.0 * num);
			int height = (int)(110.0 * num);
			using Bitmap bitmap = new Bitmap(num2, height);
			using Graphics graphics = Graphics.FromImage(bitmap);
			graphics.CopyFromScreen(bounds.Left + (bounds.Width - num2) / 2, bounds.Top, 0, 0, bitmap.Size);
			bitmap.Save(Path.Combine(Dir, name), ImageFormat.Png);
		}
		catch (Exception ex)
		{
			Log("  screenshot failed: " + ex.Message);
		}
	}

	private static MessageBannerTool OpenTab(CapsuleWindow win)
	{
		ToolRouter.Instance.RouteTo("message");
		win.RequestExpand("message-e2e");
		DoEvents(1400);
		return ToolRouter.Instance.ActiveTool as MessageBannerTool;
	}

	public static void RunAll(CapsuleWindow win)
	{
		Directory.CreateDirectory(Dir);
		try
		{
			File.Delete(Path.Combine(Dir, "results.log"));
		}
		catch
		{
		}
		Log("MESSAGE TAB - REAL CAPSULE E2E  (data: " + AppPaths.LocalAppData + ")");
		NotchBehaviorConfig.Instance.VisibilityMode = NotchVisibilityMode.AlwaysVisible;
		MessageService instance = MessageService.Instance;
		instance.TakeDown();
		win.ForceSafeCollapsed();
		DoEvents(600);
		string[] array = new string[2] { "mouse", "touch" };
		foreach (string text in array)
		{
			EventPresentationMode[] array2 = new EventPresentationMode[2]
			{
				EventPresentationMode.Brief,
				EventPresentationMode.Off
			};
			foreach (EventPresentationMode eventPresentationMode in array2)
			{
				NotchBehaviorConfig.Instance.SetEventPresentationMode(ActivityType.CompanionMessage, eventPresentationMode);
				string text2 = text + ", Message banners " + eventPresentationMode;
				Log("\n-- " + text2);
				MessageBannerTool messageBannerTool = OpenTab(win);
				Check(text2 + ": Message tab open in the expanded capsule", messageBannerTool != null && win.CapsuleDisplayState == CapsuleDisplayState.Expanded);
				if (messageBannerTool != null)
				{
					Call(messageBannerTool, "SetText", "E2E hello \ud83d\udc4b " + text);
					DoEvents(400);
					FrameworkElement el = Field<FrameworkElement>(messageBannerTool, "_flash");
					FrameworkElement el2 = Field<FrameworkElement>(messageBannerTool, "_secondary");
					Check(text2 + ": Flash reachable (hit-test)", HitsItself(win, el, "Flash"));
					Check(text2 + ": Clear reachable (hit-test)", HitsItself(win, el2, "Clear"));
					Press(text, el);
					DoEvents(900);
					Log($"  after Flash: live={instance.IsLive} onNotch={MessageOnNotch} capsule={win.CapsuleDisplayState}");
					Check(text2 + ": Flash puts the banner on the notch", instance.IsLive && MessageOnNotch);
					Check(text2 + ": the panel closes so the banner shows", win.CapsuleDisplayState == CapsuleDisplayState.Collapsed);
					Screenshot(string.Concat(text, "_", eventPresentationMode, "_banner.png"));
					messageBannerTool = OpenTab(win);
					el2 = Field<FrameworkElement>(messageBannerTool, "_secondary");
					TextBlock textBlock = Field<TextBlock>(messageBannerTool, "_secondaryText");
					Check(text2 + ": the reopened tab offers Take down", textBlock != null && textBlock.Text == "Take down");
					Press(text, el2);
					DoEvents(700);
					Check(text2 + ": Take down removes the banner", !instance.IsLive && !MessageOnNotch);
					Press(text, el2);
					DoEvents(400);
					Check(text2 + ": Clear empties the input", (string)Call(messageBannerTool, "GetText") == "");
					win.ForceSafeCollapsed();
					DoEvents(500);
				}
			}
		}
		NotchBehaviorConfig.Instance.SetEventPresentationMode(ActivityType.CompanionMessage, EventPresentationMode.Brief);
		Log($"\nMESSAGE E2E: {_pass} PASSED, {_fail} FAILED");
	}
}
