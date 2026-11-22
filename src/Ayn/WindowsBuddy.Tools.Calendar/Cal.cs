using System;
using System.Globalization;
using System.Text.RegularExpressions;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.Tools.Calendar;

public static class Cal
{
	public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	private static readonly Regex ReClockText = new Regex("^(?:at\\s*)?(\\d{1,2})(?:\\s*[:.h]\\s*(\\d{2})|(\\d{2}))?\\s*(a\\.?m?\\.?|p\\.?m?\\.?)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static DateTime? ParseDate(string s)
	{
		if (!string.IsNullOrEmpty(s) && DateTime.TryParseExact(s, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var result))
		{
			return result;
		}
		return null;
	}

	public static TimeSpan? ParseTime(string s)
	{
		if (!string.IsNullOrEmpty(s) && TimeSpan.TryParseExact(s, new string[2] { "hh\\:mm", "hh\\:mm\\:ss" }, Inv, out var result) && result >= TimeSpan.Zero && result < TimeSpan.FromDays(1.0))
		{
			return result;
		}
		return null;
	}

	public static string D(DateTime d)
	{
		return d.ToString("yyyy-MM-dd", Inv);
	}

	public static string T(TimeSpan t)
	{
		return new DateTime(1, 1, 1).Add(t).ToString("HH:mm", Inv);
	}

	public static string Time(DateTime t)
	{
		if (!ClockSettings.Uses24Hour)
		{
			return t.ToString("h:mm tt", Inv);
		}
		return t.ToString("HH:mm", Inv);
	}

	public static string Range(DateTime s, DateTime e)
	{
		if (e <= s)
		{
			return Time(s);
		}
		if (!ClockSettings.Uses24Hour && s.Date == e.Date && s.ToString("tt", Inv) == e.ToString("tt", Inv))
		{
			return s.ToString("h:mm", Inv) + " – " + e.ToString("h:mm tt", Inv);
		}
		return Time(s) + " – " + Time(e);
	}

	public static string Until(DateTime now, DateTime t)
	{
		TimeSpan timeSpan = t - now;
		if (timeSpan.TotalSeconds < 60.0)
		{
			return "now";
		}
		int num = (int)Math.Ceiling(timeSpan.TotalMinutes);
		if (num < 60)
		{
			return "in " + num + "m";
		}
		int num2 = num / 60;
		int num3 = num % 60;
		if (num2 < 24)
		{
			if (num3 != 0)
			{
				return "in " + num2 + "h " + num3 + "m";
			}
			return "in " + num2 + "h";
		}
		int num4 = (int)Math.Round(timeSpan.TotalDays);
		if (num4 != 1)
		{
			return "in " + num4 + " days";
		}
		return "tomorrow";
	}

	public static string DayName(DateTime day, DateTime today)
	{
		if (day == today)
		{
			return "Today";
		}
		if (day == today.AddDays(1.0))
		{
			return "Tomorrow";
		}
		if (day == today.AddDays(-1.0))
		{
			return "Yesterday";
		}
		return day.ToString((day.Year == today.Year) ? "ddd d MMM" : "ddd d MMM yyyy", Inv);
	}

	public static string RepeatLabel(CalRepeat r)
	{
		return r switch
		{
			CalRepeat.Daily => "Daily", 
			CalRepeat.Weekdays => "Weekdays", 
			CalRepeat.Weekly => "Weekly", 
			CalRepeat.Monthly => "Monthly", 
			CalRepeat.Yearly => "Yearly", 
			_ => "", 
		};
	}

	public static bool TryParseClock(string text, out TimeSpan time, out string error)
	{
		bool prefer24h = false;
		try
		{
			prefer24h = ClockSettings.Uses24Hour;
		}
		catch
		{
		}
		return TryParseClock(text, prefer24h, out time, out error);
	}

	public static bool TryParseClock(string text, bool prefer24h, out TimeSpan time, out string error)
	{
		time = TimeSpan.Zero;
		error = null;
		string text2 = (text ?? "").Trim().ToLowerInvariant();
		if (text2.Length == 0)
		{
			error = "Enter a time, like 7pm or 19:30";
			return false;
		}
		switch (text2)
		{
		case "noon":
		case "midday":
			time = TimeSpan.FromHours(12.0);
			return true;
		case "midnight":
			time = TimeSpan.Zero;
			return true;
		default:
		{
			Match match = ReClockText.Match(text2);
			if (!match.Success)
			{
				error = "Couldn't read “" + text.Trim() + "” — try 7pm or 19:30";
				return false;
			}
			int num = int.Parse(match.Groups[1].Value, Inv);
			string text3 = (match.Groups[2].Success ? match.Groups[2].Value : (match.Groups[3].Success ? match.Groups[3].Value : null));
			int num2 = ((text3 != null) ? int.Parse(text3, Inv) : 0);
			string text4 = (match.Groups[4].Success ? match.Groups[4].Value : null);
			if (num2 > 59)
			{
				error = "Minutes go up to 59";
				return false;
			}
			if (text4 != null)
			{
				if (num < 1 || num > 12)
				{
					error = "With AM / PM the hour is 1–12";
					return false;
				}
				bool flag = text4[0] == 'p';
				num = num % 12 + (flag ? 12 : 0);
			}
			else
			{
				if (num > 23)
				{
					error = "Hours go up to 23";
					return false;
				}
				if (!prefer24h && !match.Groups[1].Value.StartsWith("0", StringComparison.Ordinal) && num >= 1 && num <= 6)
				{
					num += 12;
				}
			}
			time = new TimeSpan(num, num2, 0);
			return true;
		}
		}
	}

	public static TimeSpan AlertLead(CalAlert a)
	{
		return a switch
		{
			CalAlert.Min5 => TimeSpan.FromMinutes(5.0), 
			CalAlert.Min10 => TimeSpan.FromMinutes(10.0), 
			CalAlert.Min30 => TimeSpan.FromMinutes(30.0), 
			CalAlert.Day1 => TimeSpan.FromDays(1.0), 
			_ => TimeSpan.Zero, 
		};
	}

	public static DateTime NextDate(DateTime origin, CalRepeat r, DateTime after)
	{
		if (r == CalRepeat.None)
		{
			return origin;
		}
		DateTime dateTime = origin;
		for (int i = 1; i < 100000; i++)
		{
			if (!(dateTime <= after))
			{
				break;
			}
			switch (r)
			{
			case CalRepeat.Daily:
				dateTime = origin.AddDays(i);
				break;
			case CalRepeat.Weekdays:
				dateTime = dateTime.AddDays((dateTime.DayOfWeek == DayOfWeek.Friday) ? 3 : ((dateTime.DayOfWeek != DayOfWeek.Saturday) ? 1 : 2));
				break;
			case CalRepeat.Weekly:
				dateTime = origin.AddDays(7 * i);
				break;
			case CalRepeat.Monthly:
				dateTime = origin.AddMonths(i);
				break;
			case CalRepeat.Yearly:
				dateTime = origin.AddYears(i);
				break;
			}
		}
		return dateTime;
	}
}
