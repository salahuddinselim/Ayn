using System;
using System.Globalization;

namespace WindowsBuddy.Tools.Units;

internal static class Format
{
	public static string Number(double x)
	{
		if (double.IsNaN(x) || double.IsInfinity(x))
		{
			return "—";
		}
		if (x == 0.0)
		{
			return "0";
		}
		double num = Math.Abs(x);
		if (num >= 1000000000000.0 || num < 1E-06)
		{
			return x.ToString("0.#####E+0", CultureInfo.InvariantCulture);
		}
		int num2 = (int)Math.Floor(Math.Log10(num));
		decimal num3 = (decimal)x;
		if (num2 >= 5)
		{
			decimal num4 = (decimal)Math.Pow(10.0, num2 - 5);
			num3 = Math.Round(num3 / num4, 0, MidpointRounding.AwayFromZero) * num4;
		}
		else
		{
			num3 = Math.Round(num3, Math.Min(28, 5 - num2), MidpointRounding.AwayFromZero);
		}
		return num3.ToString("0.############", CultureInfo.InvariantCulture);
	}
}
