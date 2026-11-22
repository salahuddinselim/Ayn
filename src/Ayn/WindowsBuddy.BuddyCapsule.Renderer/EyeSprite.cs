using System;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class EyeSprite
{
	public int W;

	public int H;

	public uint[] P;

	public EyeSprite(int w, int h)
	{
		W = Math.Max(1, w);
		H = Math.Max(1, h);
		P = new uint[W * H];
	}
}
