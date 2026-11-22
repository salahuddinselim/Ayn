using System;
using System.Globalization;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.Tools.AICoding;

public static class ClockFormat
{
	public static bool Uses24h => ClockSettings.Uses24Hour;

	public static string Hour(int h)
	{
		h = (h % 24 + 24) % 24;
		if (Uses24h)
		{
			return h.ToString("00", CultureInfo.InvariantCulture) + ":00";
		}
		return ((h % 12 == 0) ? 12 : (h % 12)).ToString(CultureInfo.InvariantCulture) + ((h < 12) ? " AM" : " PM");
	}

	public static string Range(int start, int end)
	{
		if (Uses24h)
		{
			return Hour(start) + " – " + Hour(end);
		}
		Func<int, string> func = (int h) => ((h % 12 == 0) ? 12 : (h % 12)).ToString(CultureInfo.InvariantCulture);
		Func<int, string> func2 = (int h) => (h % 24 >= 12) ? "PM" : "AM";
		string text = func2(start);
		string text2 = func2(end);
		if (!(text == text2))
		{
			return func(start) + " " + text + " – " + func(end) + " " + text2;
		}
		return func(start) + " – " + func(end) + " " + text2;
	}
}
