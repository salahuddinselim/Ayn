using System;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class EyesEngine
{
	public const double RefW = 240.0;

	public const double RefH = 44.0;

	public const double CY = 22.0;

	public const double X0 = 60.0;

	public const double ViewW = 120.0;

	private const float RH = 44f;

	public readonly string Skin;

	public int W;

	public int H;

	public uint[] Buf;

	private double ps;

	private readonly EyesCanvas c = new EyesCanvas();

	private EyePaint glow;

	private EyePaint pill;

	private EyePaint sclera;

	private EyePaint pupilG;

	private EyePaint iris;

	private EyePaint irisPupil;

	private EyePaint lidShadow;

	private EyePaint solid;

	private static readonly float[] Fib = BuildFib();

	public EyesEngine(string skin)
	{
		Skin = EyeSkins.Normalize(skin);
	}

	private static float[] BuildFib()
	{
		float[] array = new float[30];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (float)((double)i / 30.0 * Math.PI * 2.0 + 0.12 * Math.Sin((double)i * 3.7));
		}
		return array;
	}

	public void Build(double pxPerRef, double zoom = 1.0)
	{
		double num = Math.Max(0.2, pxPerRef);
		ps = num * Math.Max(1.0, zoom);
		W = Math.Max(8, (int)Math.Ceiling(120.0 * num));
		H = Math.Max(4, (int)Math.Ceiling(44.0 * num));
		Buf = new uint[W * H];
		solid = new EyePaint();
		switch (Skin)
		{
		case "bubble":
			glow = GlowPaint(255, 255, 255, 0.05f);
			sclera = new EyePaint().Stop(0f, "#ffffff").Stop(0.62f, "#f1f2f6").Stop(1f, "#c4c8d4")
				.Bake();
			pupilG = new EyePaint().Stop(0f, "#2a2b35").Stop(1f, "#050508").Bake();
			break;
		case "honey":
			glow = GlowPaint(255, 190, 90, 0.06f);
			sclera = new EyePaint().Stop(0f, "#fbfaf7").Stop(0.55f, "#ebe7e0").Stop(1f, "#aea79d")
				.Bake();
			iris = new EyePaint().Stop(0f, "#ffe08a").Stop(0.3f, "#e9a93a").Stop(0.7f, "#9a5a17")
				.Stop(0.92f, "#4a2509")
				.Stop(1f, "#1d0e04")
				.Bake();
			irisPupil = new EyePaint().Stop(0f, "#000000").Stop(1f, "#160b03").Bake();
			lidShadow = LidShadow();
			break;
		case "jade":
			glow = GlowPaint(80, 230, 150, 0.08f);
			iris = new EyePaint().Stop(0f, "#f3ffb0").Stop(0.25f, "#b8f06a").Stop(0.55f, "#4cc77a")
				.Stop(0.8f, "#13805a")
				.Stop(1f, "#04382a")
				.Bake();
			lidShadow = LidShadow();
			break;
		default:
			glow = GlowPaint(255, 255, 255, 0.07f);
			pill = new EyePaint().Stop(0f, "#ffffff").Stop(1f, "#e6e7ec").Bake();
			break;
		}
	}

	public void Warm()
	{
	}

	private static EyePaint GlowPaint(int r, int g, int b, float a)
	{
		return new EyePaint().Stop(0f, r, g, b, a).Stop(1f, r, g, b, 0f).Bake();
	}

	private static EyePaint LidShadow()
	{
		return new EyePaint().Stop(0f, 0, 0, 0, 0.5f).Stop(1f, 0, 0, 0, 0f).Bake();
	}

	private void Glow(float x, float y, float r)
	{
		glow.Radial(x, y, 0f, x, y, r);
		c.BeginPath();
		c.Rect(x - r, y - r, r * 2f, r * 2f);
		c.Fill(glow, 1f);
	}

	private void Almond(float x0, float y0, float x1, float y1, float top, float bot)
	{
		float num = x1 - x0;
		c.MoveTo(x0, y0);
		c.BezierTo(x0 + num * 0.2f, top, x1 - num * 0.2f, top, x1, y1);
		c.BezierTo(x1 - num * 0.2f, bot, x0 + num * 0.2f, bot, x0, y0);
		c.Close();
	}

	private void Lash(float x0, float y0, float x1, float y1, float bot, float o, int r, int g, int b)
	{
		if (!(o > 0.6f))
		{
			float num = x1 - x0;
			c.BeginPath();
			c.MoveTo(x0, y0);
			c.BezierTo(x0 + num * 0.2f, bot, x1 - num * 0.2f, bot, x1, y1);
			solid.SetSolid(r, g, b, 1f);
			c.Stroke(solid, (0.6f - o) / 0.6f * 0.6f, 0.9f, roundCaps: true);
		}
	}

	private void SmileClip(float cx, float yCut, float rx)
	{
		c.BeginPath();
		c.Rect(0f, 0f, 240f, 44f);
		c.Ellipse(cx, yCut + 12f, rx, 12f, 0f);
		c.Clip(evenOdd: true);
	}

	private void Fill(int r, int g, int b, float a)
	{
		solid.SetSolid(r, g, b, a);
		c.Fill(solid, 1f);
	}

	private void Ayn(float gx, float gy, float h, float o)
	{
		for (int i = 0; i < 2; i++)
		{
			int num = ((i != 0) ? 1 : (-1));
			float num2 = (float)(120 + num * 16) + gx * 5f;
			float num3 = 22f + gy * 3.5f;
			float num4 = 10f;
			float num5 = Math.Max(22f * o * (1f - h * 0.12f), 1.6f);
			Glow(num2, num3, 20f);
			c.ResetClip();
			if (h > 0.02f)
			{
				SmileClip(num2, num3 + num5 / 2f - h * num5 * 0.5f, num4 * 1.3f);
			}
			pill.Linear(0f, num3 - num5 / 2f, 0f, num3 + num5 / 2f);
			c.BeginPath();
			c.RoundRect(num2 - num4 / 2f, num3 - num5 / 2f, num4, num5, Math.Min(num4 / 2f, num5 / 2f));
			c.Fill(pill, 1f);
			c.ResetClip();
		}
	}

	private void Bubble(float gx, float gy, float n, float h, float o)
	{
		for (int i = 0; i < 2; i++)
		{
			int num = ((i != 0) ? 1 : (-1));
			float num2 = 120 + num * 19;
			float num3 = 22f;
			Glow(num2, num3, 24f);
			c.ScaleYAbout(num3, Math.Max(o, 0.07f));
			c.ResetClip();
			if (h > 0.02f)
			{
				SmileClip(num2, num3 + 11.5f - h * 11.5f * 1.05f, 14.95f);
			}
			sclera.Radial(num2 - 3f, num3 - 4f, 1f, num2, num3, 11.5f);
			c.BeginPath();
			c.Circle(num2, num3, 11.5f);
			c.Fill(sclera, 1f);
			float num4 = 6.1f + n * 1.7f;
			float num5 = 11.5f - num4 - 1.1f;
			float num6 = gx * num5 - (float)num * n * 1.1f;
			float num7 = gy * num5 * 0.85f;
			float num8 = (float)Math.Sqrt(num6 * num6 + num7 * num7);
			if (num8 > num5)
			{
				num6 *= num5 / num8;
				num7 *= num5 / num8;
			}
			float num9 = num2 + num6;
			float num10 = num3 + num7;
			pupilG.Radial(num9 - 1f, num10 - 1f, 0f, num9, num10, num4);
			c.BeginPath();
			c.Circle(num9, num10, num4);
			c.Fill(pupilG, 1f);
			c.BeginPath();
			c.Circle(num9 - 2.3f, num10 - 2.5f, 2.4f);
			Fill(255, 255, 255, 1f);
			c.BeginPath();
			c.Circle(num9 + 2.5f, num10 + 2.2f, 1.05f);
			solid.SetSolid(255, 255, 255, 1f);
			c.Fill(solid, 0.85f);
			c.ResetClip();
			c.ResetTransform();
		}
	}

	private void Honey(float gx, float gy, float n, float h, float o)
	{
		for (int i = 0; i < 2; i++)
		{
			int num = ((i != 0) ? 1 : (-1));
			float num2 = 120 + num * 21;
			float num3 = num2 - 15f;
			float x = num2 + 15f;
			float num4 = 23f;
			float num5 = 35f - h * 8f;
			float num6 = num5 + (8f - num5) * o;
			Glow(num2, 22f, 26f);
			c.BeginPath();
			Almond(num3, num4, x, num4, num6, num5);
			c.Clip();
			sclera.Radial(num2, 24f, 0f, num2, 24f, 18f);
			c.BeginPath();
			c.Rect(num3, 0f, 30f, 44f);
			c.Fill(sclera, 1f);
			float num7 = num2 + gx * 6.5f;
			float num8 = num4 + gy * 3.5f;
			float num9 = 8.6f;
			iris.Radial(num7, num8, 0f, num7, num8, num9);
			c.BeginPath();
			c.Circle(num7, num8, num9);
			c.Fill(iris, 1f);
			for (int j = 0; j < Fib.Length; j++)
			{
				float num10 = Fib[j];
				float num11 = (float)Math.Cos(num10);
				float num12 = (float)Math.Sin(num10);
				c.BeginPath();
				c.MoveTo(num7 + num11 * num9 * 0.38f, num8 + num12 * num9 * 0.38f);
				c.LineTo(num7 + num11 * num9 * 0.88f, num8 + num12 * num9 * 0.88f);
				if ((j & 1) == 1)
				{
					solid.SetSolid(255, 230, 160, 1f);
				}
				else
				{
					solid.SetSolid(70, 35, 8, 1f);
				}
				c.Stroke(solid, ((j & 1) == 1) ? 0.24f : 0.28f, 0.35f, roundCaps: false);
			}
			c.BeginPath();
			c.Circle(num7, num8, num9 - 0.3f);
			solid.SetSolid(25, 12, 3, 1f);
			c.Stroke(solid, 0.7f, 0.7f, roundCaps: false);
			float num13 = 2.6f + n * 2.2f;
			irisPupil.Radial(num7, num8, 0f, num7, num8, num13);
			c.BeginPath();
			c.Circle(num7, num8, num13);
			c.Fill(irisPupil, 1f);
			c.BeginPath();
			c.Ellipse(num7 - 3.1f, num8 - 3.2f, 1.9f, 1.5f, -0.5f);
			solid.SetSolid(255, 255, 255, 1f);
			c.Fill(solid, 0.95f);
			c.BeginPath();
			c.Circle(num7 + 2.7f, num8 + 2.5f, 0.7f);
			c.Fill(solid, 0.55f);
			float num14 = 0.25f * num4 + 0.75f * num6;
			lidShadow.Linear(0f, num14, 0f, num14 + 8f);
			c.BeginPath();
			c.Rect(num3, num14 - 2f, 30f, 10f);
			c.Fill(lidShadow, 1f);
			c.ResetClip();
			if (o > 0.15f)
			{
				c.BeginPath();
				Almond(num3, num4, x, num4, num6, num5);
				solid.SetSolid(255, 240, 220, 1f);
				c.Stroke(solid, 0.18f, 0.5f, roundCaps: false);
			}
			Lash(num3, num4, x, num4, num5, o, 235, 225, 210);
		}
	}

	private void Jade(float gx, float gy, float n, float h, float o)
	{
		for (int i = 0; i < 2; i++)
		{
			int num = ((i != 0) ? 1 : (-1));
			float num2 = 120 + num * 21;
			float num3 = num2 - 15f;
			float x = num2 + 15f;
			float y = 22f + (float)(num * 3);
			float y2 = 22f - (float)(num * 3);
			float num4 = 34f - h * 8f;
			float num5 = num4 + (9f - num4) * o;
			Glow(num2, 22f, 26f);
			c.BeginPath();
			Almond(num3, y, x, y2, num5, num4);
			c.Clip();
			float num6 = num2 + gx * 5f;
			float num7 = 22f + gy * 3f;
			iris.Radial(num6, num7, 0f, num6, num7, 16f);
			c.BeginPath();
			c.Rect(num3, 0f, 30f, 44f);
			c.Fill(iris, 1f);
			solid.SetSolid(255, 255, 210, 1f);
			for (int j = 0; j < Fib.Length; j++)
			{
				float num8 = Fib[j];
				float num9 = (float)Math.Cos(num8);
				float num10 = (float)Math.Sin(num8);
				c.BeginPath();
				c.MoveTo(num6 + num9 * 4f, num7 + num10 * 4f);
				c.LineTo(num6 + num9 * 12f, num7 + num10 * 12f);
				c.Stroke(solid, 0.13f, 0.3f, roundCaps: false);
			}
			c.BeginPath();
			c.Ellipse(num6, num7, 1.1f + n * 4.4f, 9.5f - n * 1.8f, 0f);
			Fill(3, 6, 5, 1f);
			c.BeginPath();
			c.Ellipse(num6 - 4f, num7 - 4f, 1.9f, 1.5f, -0.5f);
			solid.SetSolid(255, 255, 255, 1f);
			c.Fill(solid, 0.92f);
			c.BeginPath();
			c.Circle(num6 + 3f, num7 + 3.4f, 0.65f);
			c.Fill(solid, 0.5f);
			float num11 = 5.5f + 0.75f * num5;
			lidShadow.Linear(0f, num11, 0f, num11 + 8f);
			c.BeginPath();
			c.Rect(num3, num11 - 2f, 30f, 10f);
			c.Fill(lidShadow, 1f);
			c.ResetClip();
			if (o > 0.15f)
			{
				c.BeginPath();
				Almond(num3, y, x, y2, num5, num4);
				solid.SetSolid(200, 255, 220, 1f);
				c.Stroke(solid, 0.25f, 0.5f, roundCaps: false);
			}
			Lash(num3, y, x, y2, num4, o, 200, 240, 215);
		}
	}

	public void Render(float t, float dt, float gx, float gy, float blink)
	{
		Render(t, dt, gx, gy, blink, 0.3f, 0f);
	}

	public void Render(float t, float dt, float gx, float gy, float blink, float near, float smile)
	{
		if (Buf != null)
		{
			Array.Clear(Buf, 0, Buf.Length);
			c.Begin(Buf, W, H, ps, 120.0 - (double)W / 2.0 / ps, 22.0 - (double)H / 2.0 / ps);
			float gx2 = Math.Max(-1f, Math.Min(1f, gx / 7f));
			float gy2 = Math.Max(-1f, Math.Min(1f, gy / 3f));
			near = Math.Max(0f, Math.Min(1f, near));
			smile = Math.Max(0f, Math.Min(1f, smile));
			blink = Math.Max(0f, Math.Min(1f, blink));
			string emotion = EyeEmotions.Normalize(EyesPreferences.Instance.Emotion);
			if (emotion == "auto") emotion = EyeEmotions.Evaluate();
			switch (emotion)
			{
			case "happy":
				smile = 1f;
				near = 0.65f;
				break;
			case "sad":
				smile = 0f;
				gy2 = Math.Max(gy2, 0.55f);
				near = 0.18f;
				blink = Math.Max(blink, 0.14f);
				break;
			case "cry":
				smile = 0f;
				gy2 = 1f;
				near = 0.25f;
				blink = Math.Max(blink, 0.32f);
				break;
			case "ego":
				smile = 0f;
				near = 0.9f;
				blink = Math.Max(blink, 0.5f);
				gx2 *= 0.4f;
				break;
			case "surprised":
				smile = 0f;
				near = 1f;
				blink = 0f;
				break;
			case "sleepy":
				smile = 0.15f;
				near = 0.1f;
				blink = Math.Max(blink, 0.7f);
				break;
			}
			switch (Skin)
			{
			case "bubble":
				Bubble(gx2, gy2, near, smile, blink);
				break;
			case "honey":
				Honey(gx2, gy2, near, smile, blink);
				break;
			case "jade":
				Jade(gx2, gy2, near, smile, blink);
				break;
			default:
				Ayn(gx2, gy2, smile, blink);
				break;
			}
			DrawEmotionOverlay(emotion, t);
		}
	}

	private void DrawEmotionOverlay(string emotion, float t)
	{
		c.ResetClip();
		if (emotion == "sad" || emotion == "cry" || emotion == "ego")
		{
			solid.SetSolid(20, 20, 24, 0.72f);
			float tilt = emotion == "ego" ? -0.32f : 0.32f;
			c.Ellipse(104f, 8.5f, 7.5f, 1.6f, tilt);
			c.Fill(solid, 1f);
			c.Ellipse(136f, 8.5f, 7.5f, 1.6f, -tilt);
			c.Fill(solid, 1f);
		}
		if (emotion == "cry")
		{
			var pulse = 0.9f + 0.1f * (float)Math.Sin(t * 2.4);
			solid.SetSolid(110, 190, 255, 0.9f);
			c.Ellipse(104f, 36f, 1.9f, 3.4f * pulse, -0.12f);
			c.Fill(solid, 1f);
			c.Ellipse(136f, 37f, 1.6f, 2.9f * pulse, 0.12f);
			c.Fill(solid, 1f);
			solid.SetSolid(235, 248, 255, 0.85f);
			c.Ellipse(103.4f, 34.8f, 0.6f, 0.9f, 0f);
			c.Fill(solid, 1f);
			c.Ellipse(135.5f, 35.8f, 0.5f, 0.7f, 0f);
			c.Fill(solid, 1f);
		}
	}
}
