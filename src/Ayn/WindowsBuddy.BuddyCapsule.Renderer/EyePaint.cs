using System;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class EyePaint
{
	public const int Steps = 256;

	public readonly uint[] Lut = new uint[256];

	public int Kind;

	public uint SolidPm;

	public float X0;

	public float Y0;

	public float R0;

	public float X1;

	public float Y1;

	public float R1;

	private readonly float[] off = new float[8];

	private readonly float[] sr = new float[8];

	private readonly float[] sg = new float[8];

	private readonly float[] sb = new float[8];

	private readonly float[] sa = new float[8];

	private int count;

	public static uint Pack(float a, int r, int g, int b)
	{
		int num = (int)(Math.Max(0f, Math.Min(1f, a)) * 255f + 0.5f);
		return (uint)(((b * num + 127) / 255) | ((g * num + 127) / 255 << 8) | ((r * num + 127) / 255 << 16) | (num << 24));
	}

	public EyePaint Stop(float offset, int r, int g, int b, float a)
	{
		if (count < off.Length)
		{
			off[count] = offset;
			sr[count] = r;
			sg[count] = g;
			sb[count] = b;
			sa[count] = a;
			count++;
		}
		return this;
	}

	public EyePaint Stop(float offset, string hex, float a = 1f)
	{
		int num = Convert.ToInt32(hex.TrimStart('#'), 16);
		return Stop(offset, (num >> 16) & 0xFF, (num >> 8) & 0xFF, num & 0xFF, a);
	}

	public EyePaint Bake()
	{
		for (int i = 0; i < 256; i++)
		{
			float num = (float)i / 255f;
			int j = 0;
			float num2;
			float num3;
			float num4;
			float a;
			if (num <= off[0])
			{
				num2 = sr[0];
				num3 = sg[0];
				num4 = sb[0];
				a = sa[0];
			}
			else if (num >= off[count - 1])
			{
				num2 = sr[count - 1];
				num3 = sg[count - 1];
				num4 = sb[count - 1];
				a = sa[count - 1];
			}
			else
			{
				for (; j < count - 2 && num > off[j + 1]; j++)
				{
				}
				float num5 = Math.Max(1E-06f, off[j + 1] - off[j]);
				float num6 = (num - off[j]) / num5;
				num2 = sr[j] + (sr[j + 1] - sr[j]) * num6;
				num3 = sg[j] + (sg[j + 1] - sg[j]) * num6;
				num4 = sb[j] + (sb[j + 1] - sb[j]) * num6;
				a = sa[j] + (sa[j + 1] - sa[j]) * num6;
			}
			Lut[i] = Pack(a, (int)(num2 + 0.5f), (int)(num3 + 0.5f), (int)(num4 + 0.5f));
		}
		return this;
	}

	public void SetSolid(int r, int g, int b, float a)
	{
		Kind = 0;
		SolidPm = Pack(a, r, g, b);
	}

	public void Linear(float x0, float y0, float x1, float y1)
	{
		Kind = 1;
		X0 = x0;
		Y0 = y0;
		X1 = x1;
		Y1 = y1;
	}

	public void Radial(float x0, float y0, float r0, float x1, float y1, float r1)
	{
		Kind = 2;
		X0 = x0;
		Y0 = y0;
		R0 = r0;
		X1 = x1;
		Y1 = y1;
		R1 = r1;
	}
}
