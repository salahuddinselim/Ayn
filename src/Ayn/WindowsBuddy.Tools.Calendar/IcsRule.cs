using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar;

public sealed class IcsRule
{
	public IcsFreq Freq;

	public int Interval = 1;

	public int Count;

	public IcsTime Until;

	public List<KeyValuePair<int, DayOfWeek>> ByDay = new List<KeyValuePair<int, DayOfWeek>>();

	public List<int> ByMonthDay = new List<int>();

	public List<int> ByMonth = new List<int>();

	public List<int> BySetPos = new List<int>();

	public DayOfWeek WeekStart = DayOfWeek.Monday;

	public static IcsRule Parse(string value, Func<string, IcsTime> parseTime)
	{
		IcsRule icsRule = new IcsRule();
		string[] array = (value ?? string.Empty).Split(';');
		foreach (string text in array)
		{
			int num = text.IndexOf('=');
			if (num <= 0)
			{
				continue;
			}
			string text2 = text.Substring(0, num).Trim().ToUpperInvariant();
			string text3 = text.Substring(num + 1).Trim();
			switch (text2)
			{
			case "FREQ":
				switch (text3.ToUpperInvariant())
				{
				case "DAILY":
					icsRule.Freq = IcsFreq.Daily;
					break;
				case "WEEKLY":
					icsRule.Freq = IcsFreq.Weekly;
					break;
				case "MONTHLY":
					icsRule.Freq = IcsFreq.Monthly;
					break;
				case "YEARLY":
					icsRule.Freq = IcsFreq.Yearly;
					break;
				}
				break;
			case "INTERVAL":
			{
				if (int.TryParse(text3, out var result3) && result3 > 0)
				{
					icsRule.Interval = result3;
				}
				break;
			}
			case "COUNT":
			{
				if (int.TryParse(text3, out var result2) && result2 > 0)
				{
					icsRule.Count = result2;
				}
				break;
			}
			case "UNTIL":
				icsRule.Until = parseTime?.Invoke(text3);
				break;
			case "WKST":
			{
				if (TryDay(text3, out var d2))
				{
					icsRule.WeekStart = d2;
				}
				break;
			}
			case "BYDAY":
			{
				string[] array2 = text3.Split(',');
				foreach (string text4 in array2)
				{
					string text5 = text4.Trim().ToUpperInvariant();
					if (text5.Length < 2 || !TryDay(text5.Substring(text5.Length - 2), out var d))
					{
						continue;
					}
					int result = 0;
					if ((text5.Length <= 2 || int.TryParse(text5.Substring(0, text5.Length - 2), out result)) && result >= -53 && result <= 53)
					{
						KeyValuePair<int, DayOfWeek> item = new KeyValuePair<int, DayOfWeek>(result, d);
						if (!icsRule.ByDay.Contains(item))
						{
							icsRule.ByDay.Add(item);
						}
					}
				}
				break;
			}
			case "BYMONTHDAY":
				Ints(text3, icsRule.ByMonthDay, -31, 31);
				break;
			case "BYMONTH":
				Ints(text3, icsRule.ByMonth, 1, 12);
				break;
			case "BYSETPOS":
				Ints(text3, icsRule.BySetPos, -366, 366);
				break;
			}
		}
		if (icsRule.Freq != 0)
		{
			return icsRule;
		}
		return null;
	}

	private static void Ints(string v, List<int> into, int min, int max)
	{
		string[] array = v.Split(',');
		foreach (string text in array)
		{
			if (int.TryParse(text.Trim(), out var result) && result != 0 && result >= min && result <= max && !into.Contains(result))
			{
				into.Add(result);
			}
		}
	}

	internal static bool TryDay(string s, out DayOfWeek d)
	{
		switch ((s ?? string.Empty).Trim().ToUpperInvariant())
		{
		case "SU":
			d = DayOfWeek.Sunday;
			return true;
		case "MO":
			d = DayOfWeek.Monday;
			return true;
		case "TU":
			d = DayOfWeek.Tuesday;
			return true;
		case "WE":
			d = DayOfWeek.Wednesday;
			return true;
		case "TH":
			d = DayOfWeek.Thursday;
			return true;
		case "FR":
			d = DayOfWeek.Friday;
			return true;
		case "SA":
			d = DayOfWeek.Saturday;
			return true;
		default:
			d = DayOfWeek.Sunday;
			return false;
		}
	}
}
