using System;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal static class EyesRaster
{
	private static int Mul255(int x, int y)
	{
		int num = x * y + 128;
		return num + (num >> 8) >> 8;
	}

	public static void Clear(uint[] buf)
	{
		Array.Clear(buf, 0, buf.Length);
	}

	public static void Blit(uint[] dst, int dw, int dh, EyeSprite sp, float cx, float cy, float sx, float sy, float rot, float alpha, bool add)
	{
		if (alpha <= 0.002f || sx <= 0.0001f || sy <= 0.0001f)
		{
			return;
		}
		float num = (float)Math.Cos(rot);
		float num2 = (float)Math.Sin(rot);
		float num3 = (float)sp.W * 0.5f;
		float num4 = (float)sp.H * 0.5f;
		float num5 = Math.Abs(num * sx) * num3 + Math.Abs(num2 * sy) * num4;
		float num6 = Math.Abs(num2 * sx) * num3 + Math.Abs(num * sy) * num4;
		int num7 = Math.Max(0, (int)Math.Floor(cx - num5));
		int num8 = Math.Min(dw - 1, (int)Math.Ceiling(cx + num5));
		int num9 = Math.Max(0, (int)Math.Floor(cy - num6));
		int num10 = Math.Min(dh - 1, (int)Math.Ceiling(cy + num6));
		if (num7 > num8 || num9 > num10)
		{
			return;
		}
		int num11 = (int)(Math.Min(1f, alpha) * 256f + 0.5f);
		float num12 = 1f / sx;
		float num13 = 1f / sy;
		uint[] p = sp.P;
		int w = sp.W;
		int h = sp.H;
		for (int i = num9; i <= num10; i++)
		{
			float num14 = (float)i + 0.5f - cy;
			int num15 = i * dw;
			for (int j = num7; j <= num8; j++)
			{
				float num16 = (float)j + 0.5f - cx;
				float num17 = (num * num16 + num2 * num14) * num12;
				float num18 = ((0f - num2) * num16 + num * num14) * num13;
				float num19 = num17 + num3 - 0.5f;
				float num20 = num18 + num4 - 0.5f;
				int num21 = (int)Math.Floor(num19);
				int num22 = (int)Math.Floor(num20);
				if (num21 < -1 || num22 < -1 || num21 >= w || num22 >= h)
				{
					continue;
				}
				int num23 = (int)((num19 - (float)num21) * 256f);
				int num24 = (int)((num20 - (float)num22) * 256f);
				uint num25 = ((num21 >= 0 && num22 >= 0) ? p[num22 * w + num21] : 0u);
				uint num26 = ((num21 + 1 < w && num22 >= 0) ? p[num22 * w + num21 + 1] : 0u);
				uint num27 = ((num21 >= 0 && num22 + 1 < h) ? p[(num22 + 1) * w + num21] : 0u);
				uint num28 = ((num21 + 1 < w && num22 + 1 < h) ? p[(num22 + 1) * w + num21 + 1] : 0u);
				int num29 = (256 - num23) * (256 - num24);
				int num30 = num23 * (256 - num24);
				int num31 = (256 - num23) * num24;
				int num32 = num23 * num24;
				int num33 = (int)((num25 & 0xFF) * num29 + (num26 & 0xFF) * num30 + (num27 & 0xFF) * num31 + (num28 & 0xFF) * num32 >> 16);
				int num34 = (int)(((num25 >> 8) & 0xFF) * num29 + ((num26 >> 8) & 0xFF) * num30 + ((num27 >> 8) & 0xFF) * num31 + ((num28 >> 8) & 0xFF) * num32 >> 16);
				int num35 = (int)(((num25 >> 16) & 0xFF) * num29 + ((num26 >> 16) & 0xFF) * num30 + ((num27 >> 16) & 0xFF) * num31 + ((num28 >> 16) & 0xFF) * num32 >> 16);
				int num36 = (int)((num25 >> 24) * num29 + (num26 >> 24) * num30 + (num27 >> 24) * num31 + (num28 >> 24) * num32 >> 16);
				if (num36 != 0 || num35 != 0 || num34 != 0 || num33 != 0)
				{
					num33 = num33 * num11 >> 8;
					num34 = num34 * num11 >> 8;
					num35 = num35 * num11 >> 8;
					num36 = num36 * num11 >> 8;
					uint num37 = dst[num15 + j];
					int num38 = (int)(num37 & 0xFF);
					int num39 = (int)((num37 >> 8) & 0xFF);
					int num40 = (int)((num37 >> 16) & 0xFF);
					int num41 = (int)(num37 >> 24);
					if (add)
					{
						num38 = Math.Min(255, num38 + num33);
						num39 = Math.Min(255, num39 + num34);
						num40 = Math.Min(255, num40 + num35);
						num41 = Math.Min(255, num41 + num36);
					}
					else
					{
						int y = 255 - num36;
						num38 = Math.Min(255, num33 + Mul255(num38, y));
						num39 = Math.Min(255, num34 + Mul255(num39, y));
						num40 = Math.Min(255, num35 + Mul255(num40, y));
						num41 = Math.Min(255, num36 + Mul255(num41, y));
					}
					dst[num15 + j] = (uint)(num38 | (num39 << 8) | (num40 << 16) | (num41 << 24));
				}
			}
		}
	}
}
