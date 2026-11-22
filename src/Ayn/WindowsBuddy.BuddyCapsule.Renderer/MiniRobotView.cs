using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Media;
using WindowsBuddy.Services.Pet;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class MiniRobotView : PetView
{
	private static readonly Random random = new Random();

	private static readonly Brush clay = Paint("#F7F4EE");

	private static readonly Brush shade = Paint("#DDD9D1");

	private static readonly Brush screen = Paint("#0A0A0A");

	private static readonly Brush green = Paint("#30D158");

	private static readonly Brush grey = Paint("#57575D");

	private static readonly Brush blue = Paint("#55CFFF");

	private static readonly Geometry face = Shape("M 30,9 L 70,9 L 70,27 Q 70,40 57,40 L 43,40 Q 30,40 30,27 Z");

	private static readonly Geometry bolt = Shape("M 53,61 L 40,79 L 49,79 L 45,92 L 62,72 L 53,72 Z");

	private static readonly Geometry drop = Shape("M 88,12 Q 79,25 88,26 Q 97,25 88,12 Z");

	private static DateTime lastBubble = DateTime.MinValue;

	private readonly DispatcherTimer actionTimer = new DispatcherTimer(DispatcherPriority.Background);

	private readonly Stopwatch watch = Stopwatch.StartNew();

	private string action = "idle";

	private string previousAction = "";

	private string context = "";

	private string bubble = "";

	private double started;

	private double phase;

	private double walkPosition = 0.5;

	private double from;

	private double to;

	private double relocationFrom;

	private double relocationTo;

	private double relocationStart;

	private bool relocating;

	private bool subscribed;

	private int wake;

	private static readonly Geometry MiniHill = FrozenHill();

	private Geometry clippedShape;

	private Geometry sourceShape;

	private Matrix clippedMatrix;

	public static readonly string[] Actions = new string[26]
	{
		"breathe", "blink", "look", "glance", "walk-left", "walk-right", "turn", "sit", "stretch", "yawn",
		"hop", "wave", "peek", "tap", "dance", "charge", "tired", "focused", "doze", "sleep",
		"wake", "nod", "point", "surprised", "happy", "wink"
	};

	public bool HeadOnly { get; set; }

	public string PropPose { get; set; }

	public Path NotchSurface { get; set; }

	public string CurrentAction => action;

	public string Bubble => bubble;

	protected override bool Loop => action != "idle";

	protected override double FrameInterval
	{
		get
		{
			if (!Loop)
			{
				return 33.3334;
			}
			return 16.666666666666668;
		}
	}

	private bool MusicPlaying => MediaService.Instance.IsPlaying;

	private static Geometry FrozenHill()
	{
		Geometry geometry = Geometry.Parse("M 71,73 L 71,69 L 77,65 L 82,69 L 86,66 L 91,70 L 91,73 Z");
		geometry.Freeze();
		return geometry;
	}

	private Geometry NotchClip()
	{
		if (NotchSurface == null || NotchSurface.Data == null)
		{
			return Geometry.Empty;
		}
		if (PresentationSource.FromVisual(this) == null || PresentationSource.FromVisual(this) != PresentationSource.FromVisual(NotchSurface))
		{
			return new RectangleGeometry(new Rect(0.0, 0.0, Math.Max(0.0, base.ActualWidth), Math.Max(0.0, base.ActualHeight)));
		}
		GeneralTransform generalTransform = NotchSurface.TransformToVisual(this);
		Point point = generalTransform.Transform(default(Point));
		Point point2 = generalTransform.Transform(new Point(1.0, 0.0));
		Point point3 = generalTransform.Transform(new Point(0.0, 1.0));
		Matrix matrix = new Matrix(point2.X - point.X, point2.Y - point.Y, point3.X - point.X, point3.Y - point.Y, point.X, point.Y);
		if (sourceShape != NotchSurface.Data || matrix != clippedMatrix)
		{
			sourceShape = NotchSurface.Data;
			clippedMatrix = matrix;
			clippedShape = Geometry.Combine(sourceShape, sourceShape.GetWidenedPathGeometry(new Pen(Brushes.Black, 1.0)), GeometryCombineMode.Exclude, null);
			Matrix matrix2 = ((clippedShape.Transform == null) ? Matrix.Identity : clippedShape.Transform.Value);
			matrix2.Append(matrix);
			clippedShape.Transform = new MatrixTransform(matrix2);
			clippedShape.Freeze();
		}
		return clippedShape;
	}

	public void Relocate(double oldLeft, double newLeft, double oldWidth, double newWidth)
	{
		double num = Math.Max(1.0, Math.Min(base.ActualHeight - 4.0, oldWidth - 4.0));
		double num2 = Math.Max(1.0, Math.Min(base.Height - 4.0, newWidth - 4.0));
		double num3 = oldLeft + 2.0 + Math.Max(0.0, oldWidth - num - 4.0) * walkPosition;
		double num4 = Math.Max(0.0, newWidth - num2 - 4.0);
		relocationFrom = ((num4 < 1.0) ? 0.5 : Math.Max(0.0, Math.Min(1.0, (num3 - newLeft - 2.0) / num4)));
		relocationTo = 0.5;
		relocationStart = watch.Elapsed.TotalMilliseconds;
		walkPosition = relocationFrom;
		relocating = true;
		if (base.Available && action != "relocate")
		{
			Perform("relocate");
		}
	}

	public MiniRobotView(double height)
		: base("idle", height, vectorOnly: true)
	{
		base.Width = height;
		base.IsHitTestVisible = true;
		base.FollowMood = false;
		base.Cursor = Cursors.Hand;
		DispatcherTimer dispatcherTimer = actionTimer;
		EventHandler value = delegate
		{
			actionTimer.Stop();
			Choose();
		};
		dispatcherTimer.Tick += value;
		base.Loaded += delegate
		{
			if (!subscribed)
			{
				PillActivityArbiter.Instance.ActivityChanged += Activity;
				subscribed = true;
			}
			OnPetSync();
		};
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			Perform((random.Next(2) == 0) ? "hop" : "wave");
			e.Handled = true;
		};
		AutomationProperties.SetName(this, "Ayn Pet robot");
	}

	private static Brush Paint(string hex)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static Geometry Shape(string path)
	{
		Geometry geometry = Geometry.Parse(path);
		geometry.Freeze();
		return geometry;
	}

	protected override void OnPetDetached()
	{
		actionTimer.Stop();
		if (subscribed)
		{
			PillActivityArbiter.Instance.ActivityChanged -= Activity;
			subscribed = false;
		}
	}

	private void Activity(object s, PillActivityChangedEventArgs e)
	{
		if (!base.Available)
		{
			return;
		}
		PillActivitySnapshot newActivity = e.NewActivity;
		if (newActivity != null)
		{
			if (newActivity.Type == ActivityType.ClipboardCopied)
			{
				Perform("nod");
			}
			else if (newActivity.Type == ActivityType.TimerComplete || newActivity.Type == ActivityType.CalendarAlert || newActivity.Type == ActivityType.HydrationReminder)
			{
				Perform("point");
			}
			else if (newActivity.Payload is PetMoment)
			{
				Perform("wave");
			}
		}
	}

	protected override void OnPetSync()
	{
		if (actionTimer == null)
		{
			return;
		}
		if (!base.Available)
		{
			actionTimer.Stop();
			action = "idle";
			bubble = "";
			InvalidateVisual();
			return;
		}
		PetEngine instance = PetEngine.Instance;
		string text = (MusicPlaying ? "music-01" : instance.Pose);
		if (wake != instance.WakeSequence)
		{
			wake = instance.WakeSequence;
			context = text;
			Perform("wake");
		}
		else if (text != context)
		{
			context = text;
			switch (text)
			{
			case "music-01":
				Perform("dance");
				break;
			case "charging":
				Perform("charge");
				break;
			case "low-battery":
				Perform("tired");
				break;
			case "sleepy":
				Perform("doze");
				break;
			case "sleeping":
				Perform("sleep");
				break;
			case "sitting":
				Perform("sit");
				break;
			default:
				action = "idle";
				Schedule();
				InvalidateVisual();
				break;
			}
		}
		if (action == "idle" && !actionTimer.IsEnabled)
		{
			Schedule();
		}
		if (text == "music-01" && action == "idle")
		{
			Perform("dance");
		}
	}

	private void Schedule()
	{
		if (base.Available)
		{
			string activityLevel = PetPreferences.Instance.ActivityLevel;
			int num = ((activityLevel == "Calm") ? 12 : ((activityLevel == "Playful") ? 3 : 5));
			int num2 = ((activityLevel == "Calm") ? 25 : ((activityLevel == "Playful") ? 8 : 12));
			double num3 = ((previousAction.Length == 0) ? 0.0 : Math.Min(2.4, (watch.Elapsed.TotalMilliseconds - started) / 1000.0));
			actionTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.1, (double)num + random.NextDouble() * (double)(num2 - num) - num3));
			actionTimer.Start();
		}
	}

	private void Choose()
	{
		if (base.Available)
		{
			string[] source = ((PetEngine.Instance.IdleSeconds >= 1800.0) ? new string[2] { "sleep", "doze" } : ((PetEngine.Instance.IdleSeconds >= 600.0) ? new string[3] { "doze", "yawn", "sit" } : ((PetEngine.Instance.IdleSeconds >= 120.0) ? new string[3] { "sit", "yawn", "look" } : ((PetEngine.Instance.Pose == "low-battery") ? new string[3] { "tired", "sit", "yawn" } : ((PetEngine.Instance.ContinuousSeconds > 3600.0) ? new string[3] { "focused", "stretch", "look" } : new string[13]
			{
				"blink", "look", "look", "walk-left", "walk-right", "turn", "sit", "stretch", "yawn", "hop",
				"wave", "peek", "tap"
			})))));
			source = source.Where((string p) => p != previousAction && (PetPreferences.Instance.Walks || !p.StartsWith("walk-"))).ToArray();
			Perform(source[random.Next(source.Length)]);
		}
	}

	public void Perform(string name)
	{
		if (base.Available)
		{
			if (!PetPreferences.Instance.Walks && (name.StartsWith("walk-") || name == "tired"))
			{
				name = "look";
			}
			previousAction = name;
			action = name;
			started = watch.Elapsed.TotalMilliseconds;
			phase = 0.0;
			from = walkPosition;
			if (name == "blink")
			{
				BeginBlink();
			}
			double num = 0.15 + random.NextDouble() * 0.65;
			to = name switch
			{
				"tired" => (walkPosition > 0.5) ? 0.05 : 0.95, 
				"walk-right" => Math.Min(1.0, walkPosition + num), 
				"walk-left" => Math.Max(0.0, walkPosition - num), 
				_ => walkPosition, 
			};
			TransactionLogger.Info("Pet: " + name + " " + ((name == "dance" && MusicPlaying) ? "continuous" : "2.4s"));
			actionTimer.Stop();
			StartClock();
			InvalidateVisual();
		}
	}

	private void Say(string text)
	{
		if (PetPreferences.Instance.Talks && !(DateTime.UtcNow - lastBubble < TimeSpan.FromMinutes(20.0)) && !PetEngine.Instance.Quiet)
		{
			lastBubble = DateTime.UtcNow;
			bubble = text;
			InvalidateVisual();
		}
	}

	protected override void OnPetTick(double unused)
	{
		if (action == "idle")
		{
			return;
		}
		phase = (watch.Elapsed.TotalMilliseconds - started) / 1000.0;
		if (action.StartsWith("walk-") || action == "tired")
		{
			double num = Math.Max(0.0, Math.Min(1.0, (phase - 0.15) / 2.1));
			double t = ((num < 0.5) ? (num * 2.0) : ((1.0 - num) * 2.0));
			walkPosition = from + (to - from) * Ease(t);
		}
		if (relocating)
		{
			double num2 = Math.Min(1.0, (watch.Elapsed.TotalMilliseconds - relocationStart) / 650.0);
			walkPosition = relocationFrom + (relocationTo - relocationFrom) * Ease(num2);
			if (num2 >= 1.0)
			{
				relocating = false;
			}
		}
		if ((!(action == "dance") || !MusicPlaying) && phase >= 2.4)
		{
			action = "idle";
			bubble = "";
			if (MusicPlaying)
			{
				Perform("dance");
			}
			else
			{
				Schedule();
			}
		}
	}

	private static double Ease(double t)
	{
		return t * t * (3.0 - 2.0 * t);
	}

	private static void Round(DrawingContext d, Brush b, double x, double y, double w, double h, double r)
	{
		d.DrawRoundedRectangle(b, null, new Rect(x, y, w, h), r, r);
	}

	private static void Line(DrawingContext d, Brush b, double width, Point a, Point c)
	{
		Pen pen = new Pen(b, width);
		pen.StartLineCap = PenLineCap.Round;
		pen.EndLineCap = PenLineCap.Round;
		Pen pen2 = pen;
		d.DrawLine(pen2, a, c);
	}

	private static void Curve(DrawingContext d, Point a, Point b, Point c, Brush color, double thickness)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(a, isFilled: false, isClosed: false);
			streamGeometryContext.QuadraticBezierTo(b, c, isStroked: true, isSmoothJoin: false);
		}
		streamGeometry.Freeze();
		d.DrawGeometry(null, new Pen(color, thickness)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		}, streamGeometry);
	}

	private static void Text(DrawingContext d, string text, double x, double y, double size, Brush brush)
	{
		d.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), size, brush), new Point(x, y));
	}

	protected override void OnRender(DrawingContext d)
	{
		d.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight));
		double val = Math.Max(1.0, base.ActualHeight - 4.0);
		double num = Math.Min(val, Math.Max(1.0, base.ActualWidth - 4.0));
		double num2 = num;
		double num3 = Math.Max(0.0, base.ActualWidth - num2 - 4.0);
		double offsetX = 2.0 + num3 * walkPosition;
		d.PushClip(new RectangleGeometry(new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight)));
		if (NotchSurface != null)
		{
			d.PushClip(NotchClip());
		}
		d.PushTransform(new TranslateTransform(offsetX, 2.0));
		d.PushTransform(new ScaleTransform(num / 100.0, num / 100.0));
		double num4 = Math.Sin(phase * Math.PI * 4.0);
		double num5 = ((action == "nod") ? (Math.Sin(phase * 9.0) * 5.0) : 0.0);
		bool flag = action.StartsWith("walk-") || action == "tired" || action == "relocate";
		double num6 = Math.Sin(phase * Math.PI * 2.0);
		double num7 = ((phase < 1.2) ? 1 : (-1)) * Math.Sign(to - from);
		double num8 = Math.Sin(Math.Min(1.0, phase / 2.4) * Math.PI);
		double num9 = ((action == "hop" || action == "charge" || action == "point") ? ((0.0 - Math.Sin(Math.Max(0.0, Math.Min(1.0, (phase - 0.18) / 0.8)) * Math.PI)) * 8.0) : 0.0);
		bool flag2 = MusicPlaying || action == "dance" || PropPose == "music-01";
		bool flag3 = PetEngine.Instance.IdleSeconds >= 1800.0 || action == "sleep";
		double num10 = ((action == "dance") ? (Math.Abs(num4) * 3.0) : ((action == "breathe") ? (Math.Sin(phase * 3.0) * 1.0) : 0.0));
		if (flag)
		{
			num10 = (0.0 - Math.Abs(num6)) * 100.0 / num;
		}
		d.PushTransform(new TranslateTransform((action == "dance") ? (num4 * 3.0) : 0.0, num9 + num10));
		double num11 = (flag ? (Math.Max(Math.Max(0.0, 1.0 - phase / 0.15), Math.Max(0.0, 1.0 - Math.Abs(phase - 1.2) / 0.12)) * 0.06) : 0.0);
		if (action == "hop" || action == "charge")
		{
			num11 = ((phase < 0.18) ? (Math.Sin(phase / 0.18 * Math.PI) * 0.1) : ((phase < 0.85) ? ((0.0 - Math.Sin((phase - 0.18) / 0.67 * Math.PI)) * 0.055) : ((phase < 1.05) ? (Math.Sin((phase - 0.85) / 0.2 * Math.PI) * 0.07) : 0.0)));
		}
		d.PushTransform(new ScaleTransform(1.0 + num11, 1.0 - num11 * 0.7, 50.0, 75.0));
		d.PushTransform(new RotateTransform(flag ? (num7 * num8 * 4.0) : 0.0, 50.0, 94.0));
		bool flag4 = action == "sit" || PetEngine.Instance.Pose == "sitting" || flag3;
		if (!HeadOnly)
		{
			for (int i = 0; i < 2; i++)
			{
				double num12 = ((i == 0) ? 34 : 53);
				d.PushTransform(new RotateTransform((action.StartsWith("walk-") || action == "dance") ? (num4 * (double)((i == 0) ? 12 : (-12))) : 0.0, num12 + 6.0, 88.0));
				d.PushTransform(new TranslateTransform(0.0, flag ? ((0.0 - Math.Max(0.0, (i == 0) ? num6 : (0.0 - num6))) * 100.0 / num) : 0.0));
				Round(d, shade, num12, 87.0, 13.0, 9.0, 4.0);
				Round(d, clay, num12, 86.0, 13.0, 8.0, 4.0);
				d.Pop();
				d.Pop();
			}
			Round(d, shade, 35.0, flag4 ? 76 : 65, 30.0, flag4 ? 16 : 25, 11.0);
			Round(d, clay, 36.0, flag4 ? 74 : 63, 28.0, flag4 ? 17 : 26, 11.0);
			for (int j = 0; j < 2; j++)
			{
				double num13 = ((j == 0) ? 35 : 65);
				double angle = ((j == 0) ? 20 : (-20));
				if (action == "wave" && j == 0)
				{
					angle = 135.0 + Math.Sin(phase * 16.0) * 25.0;
				}
				if (action == "stretch" || action == "wake")
				{
					angle = ((j == 0) ? 145 : (-145));
				}
				if (action == "point" && j == 1)
				{
					angle = -110.0;
				}
				if (action == "tap" && j == 1)
				{
					angle = -75.0 + Math.Sin(phase * 10.0) * 12.0;
				}
				if (action == "dance")
				{
					angle = ((j == 0) ? (65.0 + num4 * 25.0) : (-65.0 + num4 * 25.0));
				}
				d.PushTransform(new RotateTransform(angle, num13, 68.0));
				Round(d, clay, num13 - 5.0, 67.0, 10.0, 19.0, 5.0);
				d.Pop();
			}
		}
		d.PushTransform(new RotateTransform((action == "turn") ? (Math.Sin(phase * 3.0) * 12.0) : (flag2 ? (num4 * 4.0) : (flag ? ((0.0 - num7) * num8 * 2.0) : ((double)((action == "yawn") ? (-7) : 0)))), 50.0, 40.0));
		d.PushTransform(new TranslateTransform(0.0, HeadOnly ? 15.0 : (flag4 ? 14.0 : ((action == "peek") ? (Math.Abs(Math.Sin(phase * 3.0)) * 4.0) : num5))));
		if (flag3)
		{
			d.PushTransform(new RotateTransform(15.0, 50.0, 42.0));
		}
		Round(d, shade, 17.0, 11.0, 66.0, 53.0, 15.0);
		Round(d, clay, 17.0, 9.0, 66.0, 53.0, 15.0);
		d.DrawGeometry(screen, null, face);
		double num14 = Math.Max(-6.0, Math.Min(6.0, base.Gaze.X * 6.0 + ((action == "look") ? (Math.Sin(phase * 3.0) * 4.0) : 0.0)));
		double val2 = base.Gaze.Y * 4.65;
		double num15 = 1.0;
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		if (presentationSource != null && presentationSource.CompositionTarget != null)
		{
			num15 = presentationSource.CompositionTarget.TransformToDevice.M11;
		}
		double dpi = Math.Max(0.01, num / 100.0 * num15);
		d.PushClip(face);
		for (int k = 0; k < 2; k++)
		{
			double blink = ((action == "wink") ? ((k == 0) ? PetView.BlinkAt(phase * 1000.0) : 0.0) : base.BlinkClosure);
			PetExpression expression = (flag3 ? PetExpression.Closed : ((action == "happy" || action == "wave" || action == "hop" || action == "charge") ? PetExpression.Happy : ((action == "doze" || action == "yawn") ? PetExpression.Sleepy : ((action == "tired") ? PetExpression.Worried : base.Expression))));
			double num16 = 17.05 * ((action == "surprised") ? 1.2 : 1.0);
			double num17 = Math.Min(val2, 37.0 - (26.669999999999998 + num16 / 2.0));
			PetView.DrawEye(d, new Point((double)((k == 0) ? 42 : 58) + num14, 26.669999999999998 + num17), 8.8, num16, expression, blink, dpi, k == 0);
		}
		d.Pop();
		if (flag2)
		{
			double thickness = Math.Max(4.0, 200.0 / num);
			Curve(d, new Point(15.0, 38.0), new Point(9.0, 3.0), new Point(50.0, 5.0), grey, thickness);
			Curve(d, new Point(50.0, 5.0), new Point(91.0, 3.0), new Point(85.0, 38.0), grey, thickness);
			Round(d, screen, 7.0, 23.0, 14.0, 25.0, 5.0);
			Round(d, screen, 79.0, 23.0, 14.0, 25.0, 5.0);
			Round(d, grey, 9.0, 25.0, 8.0, 21.0, 3.0);
			Round(d, grey, 83.0, 25.0, 8.0, 21.0, 3.0);
		}
		if (action == "tired" || PetEngine.Instance.Pose == "low-battery" || PropPose == "low-battery")
		{
			d.DrawGeometry(blue, null, drop);
		}
		if (flag3)
		{
			d.Pop();
		}
		d.Pop();
		d.Pop();
		if (action == "charge" || PetEngine.Instance.Pose == "charging" || PropPose == "charging")
		{
			d.DrawGeometry(green, null, bolt);
		}
		if (action == "focused" || PropPose == "coffee-break")
		{
			Curve(d, new Point(84.0, 76.0), new Point(101.0, 80.0), new Point(84.0, 90.0), clay, 4.0);
			Round(d, clay, 67.0, 73.0, 19.0, 20.0, 4.0);
			d.DrawEllipse(screen, null, new Point(76.5, 75.0), 6.5, 2.0);
			Curve(d, new Point(74.0, 69.0), new Point(70.0, 64.0), new Point(75.0, 59.0), clay, 3.5);
		}
		if (PropPose == "photo")
		{
			Round(d, clay, 68.0, 58.0, 26.0, 22.0, 3.0);
			Round(d, blue, 71.0, 61.0, 20.0, 12.0, 1.5);
			d.DrawGeometry(green, null, MiniHill);
			d.DrawEllipse(Paint("#FFD54F"), null, new Point(86.0, 64.5), 2.4, 2.4);
		}
		if (PropPose == "paper")
		{
			Round(d, clay, 70.0, 54.0, 22.0, 28.0, 2.5);
			for (int l = 0; l < 4; l++)
			{
				Round(d, grey, 74.0, 60 + l * 5, (l == 3) ? 9 : 14, 2.0, 1.0);
			}
		}
		if (PropPose == "plug")
		{
			Round(d, clay, 72.0, 62.0, 16.0, 14.0, 3.0);
			Round(d, clay, 75.0, 55.0, 3.0, 8.0, 1.0);
			Round(d, clay, 82.0, 55.0, 3.0, 8.0, 1.0);
			Curve(d, new Point(80.0, 76.0), new Point(80.0, 88.0), new Point(92.0, 92.0), clay, 3.5);
			d.DrawGeometry(green, null, bolt);
		}
		if (action == "doze" || flag3)
		{
			Text(d, "z", 80.0, 7.0, 15.0, clay);
			Text(d, "z", 90.0, 0.0, 12.0, clay);
		}
		d.Pop();
		d.Pop();
		d.Pop();
		d.Pop();
		d.Pop();
		if (NotchSurface != null)
		{
			d.Pop();
		}
		d.Pop();
	}
}
