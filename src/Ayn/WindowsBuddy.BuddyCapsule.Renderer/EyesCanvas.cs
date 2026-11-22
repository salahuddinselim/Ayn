using System;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class EyesCanvas
{
	private const int SS = 8;

	private const int MaxPts = 3072;

	private const int MaxSub = 96;

	private const int MaxEdges = 6144;

	private const int MaxCross = 512;

	private uint[] buf;

	private int W;

	private int H;

	private float ps;

	private float refX0;

	private float refY0;

	private float ax = 1f;

	private float bx;

	private float ay = 1f;

	private float by;

	private byte[] clipMask;

	private bool hasClip;

	public float Alpha = 1f;

	private readonly float[] px = new float[3072];

	private readonly float[] py = new float[3072];

	private readonly int[] subStart = new int[96];

	private readonly bool[] subClosed = new bool[96];

	private int nPts;

	private int nSub;

	private bool open;

	private float lastX;

	private float lastY;

	private readonly float[] eTop = new float[6144];

	private readonly float[] eBot = new float[6144];

	private readonly float[] eX = new float[6144];

	private readonly float[] eSlope = new float[6144];

	private readonly sbyte[] eDir = new sbyte[6144];

	private int nEdges;

	private readonly int[] active = new int[6144];

	private readonly float[] crossX = new float[512];

	private readonly sbyte[] crossD = new sbyte[512];

	private float[] cov = new float[8];

	private int rowMin;

	private int rowMax;

	public void Begin(uint[] buffer, int w, int h, double pixelsPerRef, double refX0Offset, double refY0Offset = 0.0)
	{
		buf = buffer;
		W = w;
		H = h;
		ps = (float)pixelsPerRef;
		refX0 = (float)refX0Offset;
		refY0 = (float)refY0Offset;
		if (cov.Length < w + 2)
		{
			cov = new float[w + 2];
		}
		if (clipMask == null || clipMask.Length != w * h)
		{
			clipMask = new byte[w * h];
		}
		ResetTransform();
		ResetClip();
		Alpha = 1f;
	}

	public void ResetTransform()
	{
		ax = 1f;
		bx = 0f;
		ay = 1f;
		by = 0f;
	}

	public void ScaleYAbout(float pivot, float k)
	{
		ax = 1f;
		bx = 0f;
		ay = k;
		by = pivot * (1f - k);
	}

	public void ResetClip()
	{
		hasClip = false;
	}

	private float Tx(float u)
	{
		return (u * ax + bx - refX0) * ps;
	}

	private float Ty(float v)
	{
		return (v * ay + by - refY0) * ps;
	}

	public void BeginPath()
	{
		nPts = 0;
		nSub = 0;
		open = false;
	}

	private void StartSub()
	{
		if (nSub < 96)
		{
			subStart[nSub] = nPts;
			subClosed[nSub] = false;
			nSub++;
		}
		open = true;
	}

	private void AddPx(float x, float y)
	{
		if (nPts < 3072)
		{
			px[nPts] = x;
			py[nPts] = y;
			nPts++;
		}
		lastX = x;
		lastY = y;
	}

	public void MoveTo(float u, float v)
	{
		StartSub();
		AddPx(Tx(u), Ty(v));
	}

	public void LineTo(float u, float v)
	{
		if (!open)
		{
			StartSub();
		}
		AddPx(Tx(u), Ty(v));
	}

	public void Close()
	{
		if (nSub > 0)
		{
			subClosed[nSub - 1] = true;
		}
		open = false;
	}

	public void BezierTo(float c1u, float c1v, float c2u, float c2v, float u, float v)
	{
		float num = lastX;
		float num2 = lastY;
		float num3 = Tx(c1u);
		float num4 = Ty(c1v);
		float num5 = Tx(c2u);
		float num6 = Ty(c2v);
		float num7 = Tx(u);
		float num8 = Ty(v);
		float num9 = Dist(num, num2, num3, num4) + Dist(num3, num4, num5, num6) + Dist(num5, num6, num7, num8);
		int num10 = Math.Max(6, Math.Min(48, (int)(num9 * 0.5f) + 4));
		for (int i = 1; i <= num10; i++)
		{
			float num11 = (float)i / (float)num10;
			float num12 = 1f - num11;
			float num13 = num12 * num12 * num12;
			float num14 = 3f * num12 * num12 * num11;
			float num15 = 3f * num12 * num11 * num11;
			float num16 = num11 * num11 * num11;
			AddPx(num13 * num + num14 * num3 + num15 * num5 + num16 * num7, num13 * num2 + num14 * num4 + num15 * num6 + num16 * num8);
		}
	}

	private static float Dist(float x0, float y0, float x1, float y1)
	{
		float num = x1 - x0;
		float num2 = y1 - y0;
		return (float)Math.Sqrt(num * num + num2 * num2);
	}

	public void Rect(float u, float v, float w, float h)
	{
		StartSub();
		AddPx(Tx(u), Ty(v));
		AddPx(Tx(u + w), Ty(v));
		AddPx(Tx(u + w), Ty(v + h));
		AddPx(Tx(u), Ty(v + h));
		Close();
	}

	public void Ellipse(float cu, float cv, float ru, float rv, float rot)
	{
		int num = Math.Max(16, Math.Min(96, (int)(Math.Max(ru, rv) * ps * 1.6f) + 14));
		float num2 = (float)Math.Cos(rot);
		float num3 = (float)Math.Sin(rot);
		StartSub();
		for (int i = 0; i < num; i++)
		{
			double num4 = (double)i * (Math.PI * 2.0 / (double)num);
			float num5 = ru * (float)Math.Cos(num4);
			float num6 = rv * (float)Math.Sin(num4);
			AddPx(Tx(cu + num5 * num2 - num6 * num3), Ty(cv + num5 * num3 + num6 * num2));
		}
		Close();
	}

	public void Circle(float cu, float cv, float r)
	{
		Ellipse(cu, cv, r, r, 0f);
	}

	public void RoundRect(float u, float v, float w, float h, float r)
	{
		r = Math.Max(0f, Math.Min(r, Math.Min(w, h) * 0.5f));
		int num = Math.Max(4, Math.Min(16, (int)(r * ps * 0.9f) + 3));
		StartSub();
		float[] array = new float[4]
		{
			u + w - r,
			u + w - r,
			u + r,
			u + r
		};
		float[] array2 = new float[4]
		{
			v + r,
			v + h - r,
			v + h - r,
			v + r
		};
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j <= num; j++)
			{
				double num2 = (double)(i * 90 + 270) * Math.PI / 180.0 + (double)j * (Math.PI / 2.0) / (double)num;
				AddPx(Tx(array[i] + r * (float)Math.Cos(num2)), Ty(array2[i] + r * (float)Math.Sin(num2)));
			}
		}
		Close();
	}

	public void Fill(EyePaint p, float alpha, bool evenOdd = false)
	{
		nEdges = 0;
		for (int i = 0; i < nSub; i++)
		{
			int num = subStart[i];
			int num2 = ((i + 1 < nSub) ? subStart[i + 1] : nPts);
			if (num2 - num >= 2)
			{
				for (int j = num; j < num2 - 1; j++)
				{
					AddEdge(px[j], py[j], px[j + 1], py[j + 1]);
				}
				AddEdge(px[num2 - 1], py[num2 - 1], px[num], py[num]);
			}
		}
		Raster(p, alpha * Alpha, evenOdd, toClip: false);
	}

	public void Clip(bool evenOdd = false)
	{
		nEdges = 0;
		for (int i = 0; i < nSub; i++)
		{
			int num = subStart[i];
			int num2 = ((i + 1 < nSub) ? subStart[i + 1] : nPts);
			if (num2 - num >= 2)
			{
				for (int j = num; j < num2 - 1; j++)
				{
					AddEdge(px[j], py[j], px[j + 1], py[j + 1]);
				}
				AddEdge(px[num2 - 1], py[num2 - 1], px[num], py[num]);
			}
		}
		Array.Clear(clipMask, 0, clipMask.Length);
		Raster(null, 1f, evenOdd, toClip: true);
		hasClip = true;
	}

	public void Stroke(EyePaint p, float alpha, float width, bool roundCaps)
	{
		float num = Math.Max(0.05f, width * ps * 0.5f);
		nEdges = 0;
		for (int i = 0; i < nSub; i++)
		{
			int num2 = subStart[i];
			int num3 = ((i + 1 < nSub) ? subStart[i + 1] : nPts);
			if (num3 - num2 >= 2)
			{
				for (int j = num2; j < num3 - 1; j++)
				{
					Quad(px[j], py[j], px[j + 1], py[j + 1], num);
				}
				if (subClosed[i])
				{
					Quad(px[num3 - 1], py[num3 - 1], px[num2], py[num2], num);
				}
				else if (roundCaps)
				{
					Disc(px[num2], py[num2], num);
					Disc(px[num3 - 1], py[num3 - 1], num);
				}
			}
		}
		Raster(p, alpha * Alpha, evenOdd: false, toClip: false);
	}

	private void Quad(float x0, float y0, float x1, float y1, float hw)
	{
		float num = x1 - x0;
		float num2 = y1 - y0;
		float num3 = (float)Math.Sqrt(num * num + num2 * num2);
		if (!(num3 < 0.0001f))
		{
			float num4 = (0f - num2) / num3 * hw;
			float num5 = num / num3 * hw;
			AddEdge(x0 + num4, y0 + num5, x1 + num4, y1 + num5);
			AddEdge(x1 + num4, y1 + num5, x1 - num4, y1 - num5);
			AddEdge(x1 - num4, y1 - num5, x0 - num4, y0 - num5);
			AddEdge(x0 - num4, y0 - num5, x0 + num4, y0 + num5);
		}
	}

	private void Disc(float cx, float cy, float r)
	{
		float x = cx + r;
		float y = cy;
		for (int i = 1; i <= 12; i++)
		{
			double num = (double)(-i) * (Math.PI / 6.0);
			float num2 = cx + r * (float)Math.Cos(num);
			float num3 = cy + r * (float)Math.Sin(num);
			AddEdge(x, y, num2, num3);
			x = num2;
			y = num3;
		}
	}

	private void AddEdge(float x0, float y0, float x1, float y1)
	{
		if (y0 != y1 && nEdges < 6144)
		{
			sbyte b = 1;
			if (y0 > y1)
			{
				float num = x0;
				x0 = x1;
				x1 = num;
				num = y0;
				y0 = y1;
				y1 = num;
				b = -1;
			}
			eTop[nEdges] = y0;
			eBot[nEdges] = y1;
			eX[nEdges] = x0;
			eSlope[nEdges] = (x1 - x0) / (y1 - y0);
			eDir[nEdges] = b;
			nEdges++;
		}
	}

	private void Raster(EyePaint paint, float alpha, bool evenOdd, bool toClip)
	{
		if (nEdges == 0 || buf == null)
		{
			return;
		}
		float num = float.MaxValue;
		float num2 = float.MinValue;
		for (int i = 0; i < nEdges; i++)
		{
			if (eTop[i] < num)
			{
				num = eTop[i];
			}
			if (eBot[i] > num2)
			{
				num2 = eBot[i];
			}
		}
		int num3 = Math.Max(0, (int)Math.Floor(num));
		int num4 = Math.Min(H - 1, (int)Math.Ceiling(num2) - 1);
		for (int j = num3; j <= num4; j++)
		{
			int num5 = 0;
			for (int k = 0; k < nEdges; k++)
			{
				if (eBot[k] > (float)j && eTop[k] < (float)(j + 1))
				{
					active[num5++] = k;
				}
			}
			if (num5 == 0)
			{
				continue;
			}
			rowMin = int.MaxValue;
			rowMax = -1;
			for (int l = 0; l < 8; l++)
			{
				float num6 = (float)j + ((float)l + 0.5f) * 0.125f;
				int num7 = 0;
				for (int m = 0; m < num5; m++)
				{
					int num8 = active[m];
					if (!(num6 < eTop[num8]) && !(num6 >= eBot[num8]) && num7 < 512)
					{
						float num9 = eX[num8] + (num6 - eTop[num8]) * eSlope[num8];
						sbyte b = eDir[num8];
						int num10 = num7++;
						while (num10 > 0 && crossX[num10 - 1] > num9)
						{
							crossX[num10] = crossX[num10 - 1];
							crossD[num10] = crossD[num10 - 1];
							num10--;
						}
						crossX[num10] = num9;
						crossD[num10] = b;
					}
				}
				int num11 = 0;
				float xa = 0f;
				bool flag = false;
				for (int n = 0; n < num7; n++)
				{
					num11 += crossD[n];
					bool flag2 = (evenOdd ? ((n & 1) == 0) : (num11 != 0));
					if (flag2 && !flag)
					{
						xa = crossX[n];
						flag = true;
					}
					else if (!flag2 && flag)
					{
						Span(xa, crossX[n], 0.125f);
						flag = false;
					}
				}
			}
			if (rowMax < rowMin)
			{
				continue;
			}
			int num12 = Math.Min(rowMax, W - 1);
			if (toClip)
			{
				for (int num13 = rowMin; num13 <= num12; num13++)
				{
					float val = cov[num13];
					cov[num13] = 0f;
					clipMask[j * W + num13] = (byte)(Math.Min(1f, val) * 255f + 0.5f);
				}
			}
			else
			{
				Shade(paint, alpha, j, rowMin, num12);
			}
			for (int num14 = rowMin; num14 <= rowMax && num14 <= W; num14++)
			{
				cov[num14] = 0f;
			}
			cov[W] = 0f;
		}
	}

	private void Span(float xa, float xb, float inv)
	{
		if (xa < 0f)
		{
			xa = 0f;
		}
		if (xb > (float)W)
		{
			xb = W;
		}
		if (xb <= xa)
		{
			return;
		}
		int num = (int)xa;
		int num2 = (int)xb;
		if (num < rowMin)
		{
			rowMin = num;
		}
		if (num2 > rowMax)
		{
			rowMax = num2;
		}
		if (num == num2)
		{
			cov[num] += (xb - xa) * inv;
			return;
		}
		cov[num] += ((float)(num + 1) - xa) * inv;
		for (int i = num + 1; i < num2; i++)
		{
			cov[i] += inv;
		}
		cov[num2] += (xb - (float)num2) * inv;
	}

	private static int Mul255(int x, int y)
	{
		int num = x * y + 128;
		return num + (num >> 8) >> 8;
	}

	private void Shade(EyePaint p, float alpha, int y, int xa, int xb)
	{
		int num = y * W;
		float num2 = ((float)y + 0.5f) / ps + refY0;
		float num3 = (num2 - by) / ay;
		int kind = p.Kind;
		float num4 = 0f;
		float num5 = 0f;
		float num6 = 0f;
		float num7 = 0f;
		float num8 = 0f;
		float num9 = 0f;
		float num10 = 0f;
		switch (kind)
		{
		case 1:
		{
			num4 = p.X1 - p.X0;
			num5 = p.Y1 - p.Y0;
			float num11 = num4 * num4 + num5 * num5;
			num6 = ((num11 > 1E-09f) ? (1f / num11) : 0f);
			break;
		}
		case 2:
			num7 = p.X1 - p.X0;
			num8 = p.Y1 - p.Y0;
			num9 = p.R1 - p.R0;
			num10 = num7 * num7 + num8 * num8 - num9 * num9;
			break;
		}
		for (int i = xa; i <= xb; i++)
		{
			float num12 = cov[i];
			if (num12 <= 0.002f)
			{
				continue;
			}
			if (num12 > 1f)
			{
				num12 = 1f;
			}
			float num13 = num12 * alpha;
			if (hasClip)
			{
				int num14 = clipMask[num + i];
				if (num14 == 0)
				{
					continue;
				}
				num13 *= (float)num14 * 0.003921569f;
			}
			if (num13 <= 0.002f)
			{
				continue;
			}
			uint num15;
			if (kind == 0)
			{
				num15 = p.SolidPm;
			}
			else
			{
				float num16 = (((float)i + 0.5f) / ps + refX0 - bx) / ax;
				float num17;
				if (kind == 1)
				{
					num17 = ((num16 - p.X0) * num4 + (num3 - p.Y0) * num5) * num6;
				}
				else
				{
					float num18 = num16 - p.X0;
					float num19 = num3 - p.Y0;
					float num20 = num18 * num7 + num19 * num8 + p.R0 * num9;
					float num21 = num18 * num18 + num19 * num19 - p.R0 * p.R0;
					if (Math.Abs(num10) < 1E-06f)
					{
						if (Math.Abs(num20) < 1E-09f)
						{
							continue;
						}
						num17 = num21 / (2f * num20);
					}
					else
					{
						float num22 = num20 * num20 - num10 * num21;
						if (num22 < 0f)
						{
							continue;
						}
						float num23 = (float)Math.Sqrt(num22);
						float val = (num20 + num23) / num10;
						float val2 = (num20 - num23) / num10;
						float num24 = Math.Max(val, val2);
						float num25 = Math.Min(val, val2);
						if (p.R0 + num24 * num9 >= 0f)
						{
							num17 = num24;
						}
						else
						{
							if (!(p.R0 + num25 * num9 >= 0f))
							{
								continue;
							}
							num17 = num25;
						}
					}
				}
				num15 = p.Lut[(!(num17 <= 0f)) ? ((num17 >= 1f) ? 255 : ((int)(num17 * 255f + 0.5f))) : 0];
			}
			int num26 = (int)(num13 * 256f + 0.5f);
			int num27 = (int)((num15 & 0xFF) * num26 >> 8);
			int num28 = (int)(((num15 >> 8) & 0xFF) * num26 >> 8);
			int num29 = (int)(((num15 >> 16) & 0xFF) * num26 >> 8);
			int num30 = (int)((num15 >> 24) * num26 >> 8);
			uint num31 = buf[num + i];
			int y2 = 255 - num30;
			int num32 = Math.Min(255, num27 + Mul255((int)(num31 & 0xFF), y2));
			int num33 = Math.Min(255, num28 + Mul255((int)((num31 >> 8) & 0xFF), y2));
			int num34 = Math.Min(255, num29 + Mul255((int)((num31 >> 16) & 0xFF), y2));
			int num35 = Math.Min(255, num30 + Mul255((int)(num31 >> 24), y2));
			buf[num + i] = (uint)(num32 | (num33 << 8) | (num34 << 16) | (num35 << 24));
		}
	}
}
