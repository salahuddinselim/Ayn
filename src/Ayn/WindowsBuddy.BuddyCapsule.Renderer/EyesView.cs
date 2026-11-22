using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class EyesView : Image
{
	private struct NativePoint
	{
		public int X;

		public int Y;
	}

	public const int FrameMs = 33;

	public const double RefHeight = 44.0;

	public const double Zoom = 1.3;

	private const double ShiftX = 0.7;

	private const double ShiftY = 0.5;

	private const double Settled = 0.004;

	public const long IdleAfterMs = 2500L;

	private const float SkinGazeEpsilon = 0.002f;

	private const float SkinLevelEpsilon = 0.001f;

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	internal static bool? ForceStatic;

	private EyesEngine engine;

	private WriteableBitmap bitmap;

	private Int32Rect fullRect;

	private int stride;

	private DispatcherTimer timer;

	private string skin;

	private double heightDip;

	private double dpi = 1.0;

	private double ps = 1.0;

	private bool animated = true;

	private double gazeX;

	private double gazeY;

	private double eyeScreenX;

	private double eyeScreenY;

	private long lastCenterMs;

	private long lastGazeTicks;

	private bool hooked;

	private EventHandler renderingHandler;

	private volatile bool running;

	private readonly object sync = new object();

	private int workerGeneration;

	private readonly object renderGate = new object();

	private EyeSprite readySp;

	private EyeSprite backSp;

	private uint[] presentBuf;

	private float readyGx;

	private float readyGy;

	private long seq;

	private long presentedSeq;

	private float lastOx = float.NaN;

	private float lastOy = float.NaN;

	private long lastPresentTicks;

	private uint rng = 2654435769u;

	private float nextBlink = 1.2f;

	private float blinkStart = -9f;

	private bool doubleBlink;

	private double near = 0.3;

	private double smile;

	private double lastCurX = double.NaN;

	private double lastCurY = double.NaN;

	private long lastMoveMs = -100000L;

	private double sacX;

	private double sacY;

	private double nextSaccade;

	private bool haveCursor;

	private uint rngUi = 625341585u;

	public double HoverHalfWidthDip = double.NaN;

	public double HoverHalfHeightDip = double.NaN;

	internal bool RecordIntervals;

	internal readonly float[] IntervalsMs = new float[16384];

	internal int IntervalCount;

	public double CardWidthDip = 240.0;

	private long framesRendered;

	private long framesSkipped;

	public long PresentCount;

	internal Func<Point> CursorOverride;

	internal double MaxPresentCostMs;

	private long lastGazeLoopTicks;

	internal long SkippedPresents;

	public static bool AnimationAvailable
	{
		get
		{
			if (ForceStatic.HasValue)
			{
				return !ForceStatic.Value;
			}
			try
			{
				return SystemParameters.ClientAreaAnimation && RenderCapability.Tier >> 16 >= 1;
			}
			catch
			{
				return true;
			}
		}
	}

	public double Near => near;

	public double Smile => smile;

	public bool IsIdle
	{
		get
		{
			if (haveCursor)
			{
				return Clock.ElapsedMilliseconds - lastMoveMs >= 2500;
			}
			return true;
		}
	}

	public long FramesRendered => Interlocked.Read(ref framesRendered);

	public long FramesSkipped => Interlocked.Read(ref framesSkipped);

	public bool IsRunning => running;

	public bool GazeHooked => hooked;

	public bool IsBuilt => engine != null;

	public string Skin => skin;

	public double HeightDip => heightDip;

	public bool IsAnimated => animated;

	public double GazeX => gazeX;

	public double GazeY => gazeY;

	public int PixelWidth
	{
		get
		{
			if (engine != null)
			{
				return engine.W;
			}
			return 0;
		}
	}

	public int PixelHeight
	{
		get
		{
			if (engine != null)
			{
				return engine.H;
			}
			return 0;
		}
	}

	public uint[] Pixels => presentBuf;

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out NativePoint p);

	private float RndUi()
	{
		rngUi ^= rngUi << 13;
		rngUi ^= rngUi >> 17;
		rngUi ^= rngUi << 5;
		return (float)(rngUi & 0xFFFFFFu) / 16777216f;
	}

	public EyesView()
	{
		base.IsHitTestVisible = false;
		base.Stretch = Stretch.None;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
		base.HorizontalAlignment = HorizontalAlignment.Center;
		base.VerticalAlignment = VerticalAlignment.Center;
	}

	public static Size DipSize(double heightDip)
	{
		double num = heightDip / 44.0;
		return new Size(120.0 * num, heightDip);
	}

	public void Configure(string skinId, double heightInDip, double dpiScale)
	{
		skinId = EyeSkins.Normalize(skinId);
		if (dpiScale <= 0.01)
		{
			dpiScale = 1.0;
		}
		heightInDip = Math.Max(6.0, heightInDip);
		if (engine == null || !(skinId == skin) || !(Math.Abs(heightInDip - heightDip) < 0.01) || !(Math.Abs(dpiScale - dpi) < 0.001) || animated != AnimationAvailable)
		{
			Stop();
			skin = skinId;
			heightDip = heightInDip;
			dpi = dpiScale;
			animated = AnimationAvailable;
			engine = new EyesEngine(skin);
			double num = heightDip * dpi / 44.0;
			ps = num * 1.3;
			engine.Build(num, 1.3);
			int num2 = engine.W * engine.H;
			readySp = new EyeSprite(engine.W, engine.H);
			backSp = new EyeSprite(engine.W, engine.H);
			presentBuf = new uint[num2];
			bitmap = new WriteableBitmap(engine.W, engine.H, 96.0 * dpi, 96.0 * dpi, PixelFormats.Pbgra32, null);
			fullRect = new Int32Rect(0, 0, engine.W, engine.H);
			stride = engine.W * 4;
			base.Source = bitmap;
			base.Width = (double)engine.W / dpi;
			base.Height = (double)engine.H / dpi;
			gazeX = (gazeY = 0.0);
			blinkStart = -9f;
			nextBlink = 1.2f;
			doubleBlink = false;
			seq = (presentedSeq = 0L);
			lastOx = (lastOy = float.NaN);
			near = 0.3;
			smile = 0.0;
			haveCursor = false;
			lastMoveMs = Clock.ElapsedMilliseconds - 100000;
			sacX = (sacY = 0.0);
			nextSaccade = 0.0;
			QaFrame(1f, 0f, 0.0, 0.0, 1f);
		}
	}

	public void Release()
	{
		Stop();
		base.Source = null;
		bitmap = null;
		engine = null;
		skin = null;
		readySp = (backSp = null);
		presentBuf = null;
	}

	public void Start()
	{
		if (engine == null || !animated || running)
		{
			return;
		}
		running = true;
		lastCenterMs = Clock.ElapsedMilliseconds - 10000;
		lastGazeTicks = Stopwatch.GetTimestamp();
		RefreshCenter();
		int generation = Interlocked.Increment(ref workerGeneration);
		ThreadPool.QueueUserWorkItem(delegate
		{
			if (Volatile.Read(ref workerGeneration) == generation)
			{
				Thread thread = new Thread((ThreadStart)delegate
				{
					SkinLoop(generation);
				});
				thread.IsBackground = true;
				thread.Name = "Ayn eyes skin";
				thread.Priority = ThreadPriority.BelowNormal;
				thread.Start();
			}
		});
		if (timer == null)
		{
			timer = new DispatcherTimer(DispatcherPriority.Render)
			{
				Interval = TimeSpan.FromMilliseconds(33.0)
			};
			timer.Tick += OnTick;
		}
		timer.Start();
	}

	public void Stop()
	{
		running = false;
		if (timer != null)
		{
			timer.Stop();
			timer.Tick -= OnTick;
			timer = null;
		}
		Unhook();
		Interlocked.Increment(ref workerGeneration);
	}

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
	{
		base.OnDpiChanged(oldDpi, newDpi);
		if (engine != null)
		{
			bool flag = running;
			Configure(skin, heightDip, newDpi.DpiScaleX);
			if (flag)
			{
				Start();
			}
		}
	}

	public void RefreshCenter()
	{
		try
		{
			if (PresentationSource.FromVisual(this) == null)
			{
				return;
			}
			Point point = PointToScreen(new Point(base.ActualWidth / 2.0, base.ActualHeight / 2.0));
			eyeScreenX = point.X;
			eyeScreenY = point.Y;
			DpiScale dpiScale = VisualTreeHelper.GetDpi(this);
			if (Math.Abs(dpiScale.DpiScaleX - dpi) > 0.001 && engine != null)
			{
				bool flag = running;
				Configure(skin, heightDip, dpiScale.DpiScaleX);
				if (flag)
				{
					Start();
				}
			}
		}
		catch (InvalidOperationException)
		{
		}
	}

	private float Rnd()
	{
		rng ^= rng << 13;
		rng ^= rng >> 17;
		rng ^= rng << 5;
		return (float)(rng & 0xFFFFFFu) / 16777216f;
	}

	private static float Ease(float t)
	{
		if (!(t < 0.5f))
		{
			return 1f - (float)Math.Pow(-2f * t + 2f, 2.0) / 2f;
		}
		return 2f * t * t;
	}

	private float Blink(float t)
	{
		if (t >= nextBlink)
		{
			blinkStart = t;
			nextBlink = t + 2.4f + Rnd() * 3.8f;
			doubleBlink = Rnd() < 0.2f;
		}
		float num = t - blinkStart;
		if (doubleBlink && num > 0.25f)
		{
			num -= 0.25f;
		}
		if (num < 0f || num > 0.17f)
		{
			return 1f;
		}
		float num2 = num / 0.17f;
		if (!(num2 < 0.42f))
		{
			return Ease((num2 - 0.42f) / 0.58f);
		}
		return 1f - Ease(num2 / 0.42f);
	}

	private void SkinLoop(int generation)
	{
		EyesEngine eyesEngine = engine;
		long num = Clock.ElapsedMilliseconds;
		long num2 = num;
		float num3 = float.NaN;
		float num4 = 0f;
		float num5 = 0f;
		float num6 = 0f;
		float num7 = 0f;
		while (Volatile.Read(ref workerGeneration) == generation)
		{
			long elapsedMilliseconds = Clock.ElapsedMilliseconds;
			float dt = Math.Min(0.1f, (float)(elapsedMilliseconds - num2) / 1000f);
			num2 = elapsedMilliseconds;
			float t = (float)elapsedMilliseconds / 1000f;
			float num8 = (float)Volatile.Read(ref gazeX);
			float num9 = (float)Volatile.Read(ref gazeY);
			float num10 = Blink(t);
			float num11 = (float)Volatile.Read(ref near);
			float num12 = (float)Volatile.Read(ref smile);
			if (float.IsNaN(num3) || !(Math.Abs(num8 - num3) < 0.002f) || !(Math.Abs(num9 - num4) < 0.002f) || num10 != num5 || !(Math.Abs(num11 - num6) < 0.001f) || !(Math.Abs(num12 - num7) < 0.001f))
			{
				lock (renderGate)
				{
					if (Volatile.Read(ref workerGeneration) != generation)
					{
						break;
					}
					eyesEngine.Render(t, dt, num8, num9, num10, num11, num12);
					num3 = num8;
					num4 = num9;
					num5 = num10;
					num6 = num11;
					num7 = num12;
					lock (sync)
					{
						EyeSprite eyeSprite = backSp;
						if (eyeSprite == null || eyeSprite.P.Length != eyesEngine.Buf.Length || eyesEngine != engine || Volatile.Read(ref workerGeneration) != generation)
						{
							break;
						}
						Array.Copy(eyesEngine.Buf, eyeSprite.P, eyesEngine.Buf.Length);
						backSp = readySp;
						readySp = eyeSprite;
						readyGx = num8;
						readyGy = num9;
						seq++;
					}
					Interlocked.Increment(ref framesRendered);
				}
			}
			else
			{
				Interlocked.Increment(ref framesSkipped);
			}
			num += 33;
			long num13 = num - Clock.ElapsedMilliseconds;
			if (num13 < -100)
			{
				num = Clock.ElapsedMilliseconds;
			}
			else if (num13 > 0)
			{
				Thread.Sleep((int)num13);
			}
		}
	}

	private void ReadCursor(out double cx, out double cy)
	{
		NativePoint p;
		if (CursorOverride != null)
		{
			Point point = CursorOverride();
			cx = point.X;
			cy = point.Y;
		}
		else if (GetCursorPos(out p))
		{
			cx = p.X;
			cy = p.Y;
		}
		else
		{
			cx = eyeScreenX;
			cy = eyeScreenY;
		}
	}

	private bool UpdateGaze()
	{
		long timestamp = Stopwatch.GetTimestamp();
		double num = Math.Min(0.1, (double)(timestamp - lastGazeTicks) / (double)Stopwatch.Frequency);
		lastGazeTicks = timestamp;
		ReadCursor(out var cx, out var cy);
		long elapsedMilliseconds = Clock.ElapsedMilliseconds;
		if (cx != lastCurX || cy != lastCurY)
		{
			if (haveCursor)
			{
				lastMoveMs = elapsedMilliseconds;
			}
			haveCursor = true;
			lastCurX = cx;
			lastCurY = cy;
		}
		double gx;
		double gy;
		double num4;
		double num7;
		if (haveCursor && elapsedMilliseconds - lastMoveMs < 2500)
		{
			EyesGaze.Map(cx, cy, eyeScreenX, eyeScreenY, dpi, CardWidthDip, out gx, out gy);
			double num2 = (cx - eyeScreenX) / dpi;
			double num3 = (cy - eyeScreenY) / dpi;
			num4 = 1.0 - Math.Max(0.0, Math.Min(1.0, Math.Sqrt(num2 * num2 + num3 * num3) / 380.0));
			double num5 = (double.IsNaN(HoverHalfWidthDip) ? (CardWidthDip / 2.0) : HoverHalfWidthDip) + 8.0;
			double num6 = (double.IsNaN(HoverHalfHeightDip) ? (heightDip / 2.0) : HoverHalfHeightDip) + 8.0;
			num7 = ((Math.Abs(num2) < num5 && Math.Abs(num3) < num6) ? 1 : 0);
		}
		else
		{
			double num8 = (double)elapsedMilliseconds / 1000.0;
			if (num8 > nextSaccade)
			{
				sacX = (double)(RndUi() * 2f - 1f) * 0.8;
				sacY = (double)(RndUi() * 2f - 1f) * 0.6;
				nextSaccade = num8 + 0.9 + (double)RndUi() * 1.9;
			}
			gx = sacX * 7.0;
			gy = sacY * 3.0;
			num4 = 0.3;
			num7 = 0.0;
		}
		double num9 = 1.0 - Math.Exp((0.0 - num) / 0.1105);
		double num10 = gazeX + (gx - gazeX) * num9;
		double num11 = gazeY + (gy - gazeY) * num9;
		Volatile.Write(ref gazeX, num10);
		Volatile.Write(ref gazeY, num11);
		Volatile.Write(ref near, near + (num4 - near) * (1.0 - Math.Exp((0.0 - num) * 6.0)));
		Volatile.Write(ref smile, smile + (num7 - smile) * (1.0 - Math.Exp((0.0 - num) * 9.0)));
		if (!(Math.Abs(gx - num10) > 0.004))
		{
			return Math.Abs(gy - num11) > 0.004;
		}
		return true;
	}

	private void Hook()
	{
		if (!hooked)
		{
			if (renderingHandler == null)
			{
				renderingHandler = OnRendering;
			}
			lastGazeLoopTicks = 0L;
			CompositionTarget.Rendering += renderingHandler;
			hooked = true;
		}
	}

	private void Unhook()
	{
		if (hooked)
		{
			CompositionTarget.Rendering -= renderingHandler;
			hooked = false;
		}
	}

	private void OnRendering(object sender, EventArgs e)
	{
		if (!running)
		{
			Unhook();
			return;
		}
		long timestamp = Stopwatch.GetTimestamp();
		if (RecordIntervals && IntervalCount < IntervalsMs.Length && lastGazeLoopTicks != 0)
		{
			IntervalsMs[IntervalCount++] = (float)((double)(timestamp - lastGazeLoopTicks) * 1000.0 / (double)Stopwatch.Frequency);
		}
		lastGazeLoopTicks = timestamp;
		bool flag = UpdateGaze();
		Present();
		if (!flag)
		{
			Unhook();
		}
	}

	private void OnTick(object sender, EventArgs e)
	{
		long elapsedMilliseconds = Clock.ElapsedMilliseconds;
		if (elapsedMilliseconds - lastCenterMs > 500)
		{
			lastCenterMs = elapsedMilliseconds;
			RefreshCenter();
		}
		if (UpdateGaze())
		{
			Hook();
		}
		Present();
	}

	private void Present()
	{
		if (bitmap == null)
		{
			return;
		}
		long num = (RecordIntervals ? Stopwatch.GetTimestamp() : 0);
		lock (sync)
		{
			if (readySp == null || seq == 0)
			{
				return;
			}
			float num2 = (float)((gazeX - (double)readyGx) * 0.7 * ps);
			float num3 = (float)((gazeY - (double)readyGy) * 0.5 * ps);
			if (seq == presentedSeq && Math.Abs(num2 - lastOx) < 0.02f && Math.Abs(num3 - lastOy) < 0.02f)
			{
				return;
			}
			if (!bitmap.TryLock(TimeSpan.Zero))
			{
				SkippedPresents++;
				return;
			}
			try
			{
				presentedSeq = seq;
				lastOx = num2;
				lastOy = num3;
				if (Math.Abs(num2) < 0.02f && Math.Abs(num3) < 0.02f)
				{
					bitmap.WritePixels(fullRect, readySp.P, stride, 0);
				}
				else
				{
					EyesRaster.Clear(presentBuf);
					EyesRaster.Blit(presentBuf, engine.W, engine.H, readySp, (float)engine.W * 0.5f + num2, (float)engine.H * 0.5f + num3, 1f, 1f, 0f, 1f, add: false);
					bitmap.WritePixels(fullRect, presentBuf, stride, 0);
				}
			}
			finally
			{
				bitmap.Unlock();
			}
		}
		PresentCount++;
		if (RecordIntervals)
		{
			double num4 = (double)(Stopwatch.GetTimestamp() - num) * 1000.0 / (double)Stopwatch.Frequency;
			if (num4 > MaxPresentCostMs)
			{
				MaxPresentCostMs = num4;
			}
		}
	}

	internal void QaFrame(float t, float dt, double gx, double gy, float blink, float nearness = 0.3f, float smileAmount = 0f)
	{
		if (engine != null && bitmap != null)
		{
			Volatile.Write(ref gazeX, gx);
			Volatile.Write(ref gazeY, gy);
			engine.Render(t, dt, (float)gx, (float)gy, blink, nearness, smileAmount);
			Array.Copy(engine.Buf, presentBuf, engine.Buf.Length);
			bitmap.WritePixels(fullRect, presentBuf, stride, 0);
			Interlocked.Increment(ref framesRendered);
		}
	}
}
