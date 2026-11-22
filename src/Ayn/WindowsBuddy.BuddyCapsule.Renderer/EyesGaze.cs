using System;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal static class EyesGaze
{
	public const double MaxX = 7.0;

	public const double MaxY = 3.0;

	public const double VerticalReachDip = 220.0;

	public const double HorizontalShare = 0.55;

	public const double TauSeconds = 0.1105;

	public static void Map(double cursorX, double cursorY, double eyeX, double eyeY, double dpiScale, double cardWidthDip, out double gx, out double gy)
	{
		if (dpiScale <= 0.01)
		{
			dpiScale = 1.0;
		}
		double num = (cursorX - eyeX) / dpiScale;
		double num2 = (cursorY - eyeY) / dpiScale;
		double num3 = Math.Max(40.0, cardWidthDip) * 0.55;
		gx = Math.Max(-1.0, Math.Min(1.0, num / num3)) * 7.0;
		gy = Math.Max(-1.0, Math.Min(1.0, num2 / 220.0)) * 3.0;
	}
}
