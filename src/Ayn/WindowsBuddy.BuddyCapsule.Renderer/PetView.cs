using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Pet;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public class PetView : FrameworkElement
{
	private static class PetPointer
	{
		private struct InputInfo
		{
			public uint Size;

			public uint Time;
		}

		private struct XY
		{
			public int X;

			public int Y;
		}

		private static DispatcherTimer timer;

		private static bool fast;

		private static bool hasPosition;

		private static XY previous;

		private static long lastRead;

		private static long lastMovement;

		private static long samples;

		private static uint lastInput;

		private static bool Needed
		{
			get
			{
				if (PetPreferences.Instance.FollowCursor)
				{
					return views.Select((WeakReference w) => w.Target as PetView).Any((PetView v) => v?.Available ?? false);
				}
				return false;
			}
		}

		[DllImport("user32.dll")]
		private static extern bool GetLastInputInfo(ref InputInfo info);

		[DllImport("user32.dll")]
		private static extern bool GetCursorPos(out XY point);

		private static void SetFast(bool value)
		{
			fast = value;
			timer.Interval = (value ? TimeSpan.FromMilliseconds(100.0) : TimeSpan.FromMilliseconds(500.0));
		}

		public static void Sync()
		{
			if (!Needed)
			{
				if (timer != null)
				{
					timer.Stop();
				}
				hasPosition = false;
				fast = false;
				return;
			}
			if (timer == null)
			{
				timer = new DispatcherTimer(DispatcherPriority.Background);
				timer.Tick += Sample;
			}
			if (!timer.IsEnabled)
			{
				hasPosition = false;
				SetFast(value: false);
				timer.Start();
				Sample(null, EventArgs.Empty);
			}
		}

		private static void Sample(object sender, EventArgs e)
		{
			if (!Needed)
			{
				Sync();
				return;
			}
			long timestamp = Stopwatch.GetTimestamp();
			if ((double)(timestamp - lastRead) * 1000.0 / (double)Stopwatch.Frequency < 100.0)
			{
				return;
			}
			InputInfo inputInfo = default(InputInfo);
			inputInfo.Size = 8u;
			InputInfo info = inputInfo;
			if (!GetLastInputInfo(ref info))
			{
				SetFast(value: false);
				return;
			}
			if (hasPosition && info.Time == lastInput)
			{
				if (fast)
				{
					SetFast(value: false);
				}
				return;
			}
			lastInput = info.Time;
			lastRead = timestamp;
			if (!GetCursorPos(out var point))
			{
				SetFast(value: false);
				hasPosition = false;
				return;
			}
			samples++;
			if (!hasPosition)
			{
				previous = point;
				hasPosition = true;
				return;
			}
			bool flag = point.X != previous.X || point.Y != previous.Y;
			PetEngine.Instance.Life.Input(flag, PetEngine.Instance.LifeSeconds);
			if (flag)
			{
				previous = point;
				lastMovement = timestamp;
				if (!fast)
				{
					SetFast(value: true);
				}
				PetView[] array = (from w in views
					select w.Target as PetView into v
					where v != null
					select v).ToArray();
				foreach (PetView petView in array)
				{
					petView.Pointer(new Point(point.X, point.Y));
				}
			}
			else if (fast && (double)(timestamp - lastMovement) * 1000.0 / (double)Stopwatch.Frequency >= 100.0)
			{
				SetFast(value: false);
			}
		}
	}

	private static readonly List<WeakReference> views = new List<WeakReference>();

	private static readonly Random random = new Random();

	private readonly DispatcherTimer blinkTimer;

	private readonly DispatcherTimer centreTimer;

	private readonly DispatcherTimer idleFrameTimer;

	private readonly Stopwatch time = Stopwatch.StartNew();

	private string pose;

	private PetFrame frame;

	private bool loaded;

	private bool clock;

	private bool composition;

	private bool blinking;

	private bool doubleBlink;

	private double blinkStart;

	private double lastFrame;

	private double lastTick;

	private double closure;

	private Vector gaze;

	private Vector target;

	private int wakeSequence;

	private string stateKey;

	private readonly List<UIElement> opacityOwners = new List<UIElement>();

	private static readonly DependencyPropertyDescriptor opacityProperty = DependencyPropertyDescriptor.FromProperty(UIElement.OpacityProperty, typeof(UIElement));

	internal bool RecordIntervals;

	internal readonly float[] IntervalsMs = new float[16384];

	internal readonly float[] IntervalSpeed = new float[16384];

	internal int IntervalCount;

	private double lastPresented;

	internal static long TickTicks;

	internal static long TickCalls;

	public bool FollowMood { get; set; }

	public PetExpression? ExpressionOverride { get; set; }

	public PetExpression Expression => ExpressionOverride ?? PetEngine.Instance.Expression;

	public string Pose => pose;

	public Vector Gaze => gaze;

	public bool CompositionActive => composition;

	public double BlinkClosure => closure;

	protected bool Available
	{
		get
		{
			if (!loaded || !base.IsVisible || !PetPreferences.Instance.Enabled || !PetEngine.Instance.MotionAllowed)
			{
				return false;
			}
			DependencyObject dependencyObject = this;
			while (dependencyObject is UIElement)
			{
				UIElement uIElement = (UIElement)dependencyObject;
				if (uIElement.Visibility != 0 || uIElement.Opacity < 0.01)
				{
					return false;
				}
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			return true;
		}
	}

	protected virtual bool Loop
	{
		get
		{
			if (!pose.StartsWith("wave-", StringComparison.Ordinal))
			{
				return pose.StartsWith("music-", StringComparison.Ordinal);
			}
			return true;
		}
	}

	protected bool EyesAnimating
	{
		get
		{
			if (!blinking)
			{
				return gaze != target;
			}
			return true;
		}
	}

	protected virtual bool UsesCompositionClock => true;

	protected virtual double FrameInterval => 33.3334;

	protected virtual double MotionSpeed => 0.0;

	protected virtual Point CursorAnchor => new Point(base.ActualWidth / 2.0, base.ActualHeight / 2.0);

	public PetView(string pose, double height, bool vectorOnly = false)
	{
		this.pose = ((pose == "sitting") ? "idle" : pose);
		frame = (vectorOnly ? null : PetAssets.Get(this.pose));
		base.Height = height;
		base.Width = height * ((frame == null) ? 1.0 : frame.Aspect);
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		base.IsHitTestVisible = false;
		RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
		blinkTimer = new DispatcherTimer(DispatcherPriority.Background);
		blinkTimer.Tick += delegate
		{
			blinkTimer.Stop();
			BeginBlink();
		};
		centreTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(2.0)
		};
		centreTimer.Tick += delegate
		{
			centreTimer.Stop();
			target = default(Vector);
			if (Available)
			{
				StartClock();
			}
		};
		idleFrameTimer = new DispatcherTimer(DispatcherPriority.Render);
		idleFrameTimer.Tick += RenderTick;
		base.Loaded += delegate
		{
			Attach();
		};
		base.Unloaded += delegate
		{
			Detach();
		};
		base.IsVisibleChanged += delegate
		{
			Sync();
		};
		if (pose.StartsWith("wave-", StringComparison.Ordinal))
		{
			string[] array = new string[3] { "wave-01", "wave-02", "wave-03" };
			foreach (string text in array)
			{
				PetAssets.Get(text);
			}
		}
		if (pose.StartsWith("music-", StringComparison.Ordinal))
		{
			PetAssets.Get("music-01");
			PetAssets.Get("music-02");
		}
	}

	protected virtual void KeepClockSteady()
	{
	}

	protected virtual void OnPetSync()
	{
	}

	protected virtual void OnPetTick(double milliseconds)
	{
	}

	protected virtual void OnPetFailure(Exception error)
	{
	}

	protected virtual void PresentFrame()
	{
		InvalidateVisual();
	}

	protected virtual void OnPetDetached()
	{
	}

	private void Attach()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		views.Add(new WeakReference(this));
		for (DependencyObject parent = VisualTreeHelper.GetParent(this); parent != null; parent = VisualTreeHelper.GetParent(parent))
		{
			if (parent is UIElement uIElement)
			{
				opacityOwners.Add(uIElement);
				opacityProperty.AddValueChanged(uIElement, OnChanged);
			}
		}
		PetEngine.Instance.Changed += OnChanged;
		CapsuleMotion.AnimationsPolicyChanged += OnChanged;
		PowerSaver.Changed += OnChanged;
		SystemParameters.StaticPropertyChanged += OnSystemPolicy;
		wakeSequence = PetEngine.Instance.WakeSequence;
		Sync();
	}

	private void Detach()
	{
		if (!loaded)
		{
			return;
		}
		loaded = false;
		blinkTimer.Stop();
		centreTimer.Stop();
		StopClock();
		foreach (UIElement opacityOwner in opacityOwners)
		{
			opacityProperty.RemoveValueChanged(opacityOwner, OnChanged);
		}
		opacityOwners.Clear();
		PetEngine.Instance.Changed -= OnChanged;
		CapsuleMotion.AnimationsPolicyChanged -= OnChanged;
		SystemParameters.StaticPropertyChanged -= OnSystemPolicy;
		PowerSaver.Changed -= OnChanged;
		views.RemoveAll((WeakReference w) => !w.IsAlive || object.ReferenceEquals(w.Target, this));
		PetPointer.Sync();
		OnPetDetached();
	}

	private void OnSystemPolicy(object s, PropertyChangedEventArgs e)
	{
		Sync();
	}

	private void OnChanged(object s, EventArgs e)
	{
		Sync();
	}

	public static void RefreshAll()
	{
		views.RemoveAll((WeakReference w) => !w.IsAlive);
		PetView[] array = (from w in views
			select w.Target as PetView into v
			where v != null
			select v).ToArray();
		foreach (PetView petView in array)
		{
			petView.Sync();
		}
		PetPointer.Sync();
	}

	public static void Shutdown()
	{
		PetView[] array = (from w in views
			select w.Target as PetView into v
			where v != null
			select v).ToArray();
		foreach (PetView petView in array)
		{
			petView.Detach();
		}
		PetPointer.Sync();
	}

	private void SetFrame(string next)
	{
		if (next == "sitting")
		{
			next = "idle";
		}
		if (!(next == pose))
		{
			PetFrame petFrame = PetAssets.Get(next);
			frame = petFrame;
			pose = next;
			InvalidateVisual();
		}
	}

	private void Sync()
	{
		if (!loaded)
		{
			return;
		}
		OnPetSync();
		if (FollowMood)
		{
			SetFrame(PetEngine.Instance.Pose);
		}
		string text = pose + "/" + Expression;
		if (text != stateKey)
		{
			stateKey = text;
			InvalidateVisual();
		}
		if (!Available)
		{
			blinkTimer.Stop();
			centreTimer.Stop();
			blinking = false;
			closure = 0.0;
			gaze = (target = (target = default(Vector)));
			StopClock();
			InvalidateVisual();
		}
		else
		{
			if (!PetPreferences.Instance.FollowCursor)
			{
				centreTimer.Stop();
				gaze = (target = (target = default(Vector)));
				InvalidateVisual();
				if (!Loop && !blinking)
				{
					StopClock();
				}
			}
			if (wakeSequence != PetEngine.Instance.WakeSequence)
			{
				wakeSequence = PetEngine.Instance.WakeSequence;
				BeginBlink();
			}
			if (!blinkTimer.IsEnabled && !blinking)
			{
				ScheduleBlink();
			}
			if (Loop)
			{
				StartClock();
			}
		}
		PetPointer.Sync();
	}

	private void ScheduleBlink()
	{
		if (Available)
		{
			blinkTimer.Interval = TimeSpan.FromMilliseconds(random.Next(3000, 7001));
			blinkTimer.Start();
		}
	}

	public void BeginBlink()
	{
		if (Available)
		{
			blinkTimer.Stop();
			blinkStart = time.Elapsed.TotalMilliseconds;
			blinking = true;
			doubleBlink = random.Next(6) == 0;
			StartClock();
		}
	}

	protected void StartClock()
	{
		if (Available)
		{
			if (!clock)
			{
				clock = true;
				lastTick = time.Elapsed.TotalMilliseconds;
				lastPresented = 0.0;
			}
			SetClockMode();
		}
	}

	private void SetClockMode()
	{
		if (Loop && UsesCompositionClock)
		{
			idleFrameTimer.Stop();
			if (!composition)
			{
				composition = true;
				CompositionTarget.Rendering += RenderTick;
			}
			return;
		}
		if (composition)
		{
			composition = false;
			CompositionTarget.Rendering -= RenderTick;
		}
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(FrameInterval);
		if (idleFrameTimer.Interval != timeSpan)
		{
			idleFrameTimer.Interval = timeSpan;
		}
		if (!idleFrameTimer.IsEnabled)
		{
			idleFrameTimer.Start();
		}
	}

	private void StopClock()
	{
		clock = false;
		idleFrameTimer.Stop();
		if (composition)
		{
			composition = false;
			CompositionTarget.Rendering -= RenderTick;
		}
	}

	protected static double BlinkAt(double ms)
	{
		if (!(ms < 0.0))
		{
			if (!(ms < 90.0))
			{
				if (!(ms < 210.0))
				{
					return 0.0;
				}
				return 1.0 - (ms - 90.0) / 120.0;
			}
			return ms / 90.0;
		}
		return 0.0;
	}

	private void RenderTick(object s, EventArgs e)
	{
		long timestamp = Stopwatch.GetTimestamp();
		try
		{
			RenderTickCore(s, e);
		}
		finally
		{
			TickTicks += Stopwatch.GetTimestamp() - timestamp;
			TickCalls++;
		}
	}

	private void RenderTickCore(object s, EventArgs e)
	{
		if (!Available)
		{
			Sync();
			return;
		}
		double totalMilliseconds = time.Elapsed.TotalMilliseconds;
		double frameInterval = FrameInterval;
		if (totalMilliseconds - lastFrame < frameInterval - ((frameInterval > 20.0) ? 6.0 : 0.5))
		{
			if (frameInterval > 20.0 && frameInterval < 40.0)
			{
				KeepClockSteady();
			}
			return;
		}
		lastFrame += frameInterval;
		if (totalMilliseconds - lastFrame > ((frameInterval > 20.0) ? (frameInterval * 0.5) : (frameInterval * 2.0)))
		{
			lastFrame = totalMilliseconds;
		}
		if (RecordIntervals)
		{
			if (lastPresented != 0.0 && IntervalCount < IntervalsMs.Length)
			{
				IntervalSpeed[IntervalCount] = (float)MotionSpeed;
				IntervalsMs[IntervalCount++] = (float)(totalMilliseconds - lastPresented);
			}
			lastPresented = totalMilliseconds;
		}
		else
		{
			lastPresented = 0.0;
		}
		double num = Math.Min(100.0, totalMilliseconds - lastTick);
		lastTick = totalMilliseconds;
		gaze += (target - gaze) * (1.0 - Math.Exp((0.0 - num) / 120.0));
		if ((target - gaze).Length < 0.1)
		{
			gaze = target;
		}
		if (blinking)
		{
			double num2 = totalMilliseconds - blinkStart;
			closure = BlinkAt(num2);
			if (doubleBlink && num2 >= 290.0)
			{
				closure = BlinkAt(num2 - 290.0);
			}
			if (num2 > (double)(doubleBlink ? 500 : 210))
			{
				blinking = false;
				closure = 0.0;
				ScheduleBlink();
			}
		}
		try
		{
			OnPetTick(totalMilliseconds);
			if (Loop && GetType() == typeof(PetView))
			{
				if (pose.StartsWith("wave-", StringComparison.Ordinal))
				{
					SetFrame("wave-0" + (1 + (int)(totalMilliseconds / 230.0) % 3));
				}
				else
				{
					SetFrame("music-0" + (1 + (int)(totalMilliseconds / 750.0) % 2));
				}
			}
			PresentFrame();
			if (!Loop && !blinking && gaze == target)
			{
				StopClock();
			}
			else
			{
				SetClockMode();
			}
		}
		catch (Exception ex)
		{
			StopClock();
			blinking = false;
			closure = 0.0;
			TransactionLogger.Warn("Pet animation recovered: " + ex);
			OnPetFailure(ex);
			ScheduleBlink();
		}
	}

	private void Pointer(Point screen)
	{
		if (Available && PetPreferences.Instance.FollowCursor)
		{
			PetEngine.Instance.WakeFromPointer();
			Point point;
			try
			{
				point = PointToScreen(CursorAnchor);
			}
			catch
			{
				return;
			}
			Vector vector = PetLife.Direction(screen, point);
			OnPointer(screen, point);
			centreTimer.Stop();
			if (!((vector - target).Length < 0.05))
			{
				target = vector;
				StartClock();
			}
		}
	}

	protected virtual void OnPointer(Point screen, Point centre)
	{
	}

	protected virtual void OnCursorWave()
	{
	}

	protected void LookUp()
	{
		if (Available)
		{
			target = new Vector(0.0, -1.0);
			centreTimer.Stop();
			centreTimer.Start();
			StartClock();
		}
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		if (frame == null)
		{
			return;
		}
		double num = ((base.ActualHeight > 0.0) ? base.ActualHeight : base.Height);
		double num2 = ((base.ActualWidth > 0.0) ? base.ActualWidth : base.Width);
		double num3 = Math.Min(num2 / (double)frame.Crop.Width, num / (double)frame.Crop.Height);
		double num4 = (num2 - (double)frame.Crop.Width * num3) / 2.0;
		double num5 = (num - (double)frame.Crop.Height * num3) / 2.0;
		Rect rectangle = new Rect(num4, num5, (double)frame.Crop.Width * num3, (double)frame.Crop.Height * num3);
		dc.DrawImage(frame.Body, rectangle);
		if (frame.Props != null)
		{
			dc.DrawImage(frame.Props, rectangle);
		}
		Point point = frame.Eyes[0];
		Point point2 = frame.Eyes[1];
		Point point3 = frame.Eyes[3];
		Vector vector = point2 - point;
		Vector vector2 = point3 - point;
		double angle = Math.Atan2(vector.Y, vector.X) * 180.0 / Math.PI;
		double num6 = 1.0;
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		if (presentationSource != null && presentationSource.CompositionTarget != null)
		{
			num6 = presentationSource.CompositionTarget.TransformToDevice.M11;
		}
		double num7 = Math.Max(1.5, vector.Length * num3 * 0.22);
		double num8 = Math.Max(2.0, vector2.Length * num3 * 0.55);
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(new Point(num4 + point.X * num3, num5 + point.Y * num3), isFilled: true, isClosed: true);
			Point[] array = new Point[3]
			{
				point2,
				point2 + (point3 - point),
				point3
			};
			for (int i = 0; i < array.Length; i++)
			{
				Point point4 = array[i];
				streamGeometryContext.LineTo(new Point(num4 + point4.X * num3, num5 + point4.Y * num3), isStroked: true, isSmoothJoin: false);
			}
		}
		streamGeometry.Freeze();
		dc.PushClip(streamGeometry);
		double[] array2 = new double[2] { 0.3, 0.7 };
		foreach (double num9 in array2)
		{
			Point point5 = point + vector * num9 + vector2 * 0.57;
			point5 = new Point(num4 + point5.X * num3 + gaze.X * vector.Length * num3 * 0.15, num5 + point5.Y * num3 + gaze.Y * vector2.Length * num3 * 0.15);
			point5 = new Point(Math.Round(point5.X * num6) / num6, Math.Round(point5.Y * num6) / num6);
			double w = Math.Max(1.0 / num6, Math.Round(num7 * num6) / num6);
			double h = Math.Max(1.0 / num6, Math.Round(num8 * num6) / num6);
			dc.PushTransform(new RotateTransform(angle, point5.X, point5.Y));
			DrawEye(dc, point5, w, h, Expression, closure, num6, num9 < 0.5);
			dc.Pop();
		}
		dc.Pop();
	}

	protected static void DrawEye(DrawingContext dc, Point c, double w, double h, PetExpression expression, double blink, double dpi, bool left)
	{
		if (expression == PetExpression.Surprised)
		{
			h *= 1.3;
		}
		h = Math.Max(1.0 / dpi, h * (1.0 - Math.Max(0.0, Math.Min(1.0, blink))));
		Rect rectangle = new Rect(c.X - w / 2.0, c.Y - h / 2.0, w, h);
		double num = Math.Min(w, h) / 2.0;
		dc.PushGuidelineSet(new GuidelineSet(new double[2] { rectangle.Left, rectangle.Right }, new double[2] { rectangle.Top, rectangle.Bottom }));
		double thickness = Math.Max(1.0 / dpi, Math.Round(w * 0.27 * dpi) / dpi);
		Pen pen = new Pen(Brushes.White, thickness);
		pen.StartLineCap = PenLineCap.Round;
		pen.EndLineCap = PenLineCap.Round;
		Pen pen2 = pen;
		pen2.Freeze();
		if (blink > 0.0)
		{
			dc.DrawRoundedRectangle(Brushes.White, null, rectangle, num, num);
		}
		else if (expression == PetExpression.Happy)
		{
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(new Point(c.X - w * 0.5, c.Y + h * 0.1), isFilled: false, isClosed: false);
				streamGeometryContext.QuadraticBezierTo(new Point(c.X, c.Y - h * 0.65), new Point(c.X + w * 0.5, c.Y + h * 0.1), isStroked: true, isSmoothJoin: false);
			}
			streamGeometry.Freeze();
			dc.DrawGeometry(null, pen2, streamGeometry);
		}
		else if (expression == PetExpression.Sleepy || expression == PetExpression.Closed || (expression == PetExpression.Wink && left))
		{
			dc.DrawLine(pen2, new Point(c.X - w * 0.4, c.Y), new Point(c.X + w * 0.4, c.Y));
		}
		else
		{
			if (expression == PetExpression.Worried)
			{
				dc.PushTransform(new RotateTransform(left ? 12 : (-12), c.X, c.Y));
			}
			dc.DrawRoundedRectangle(Brushes.White, null, rectangle, num, num);
			if (expression == PetExpression.Worried)
			{
				dc.Pop();
			}
		}
		dc.Pop();
	}
}
