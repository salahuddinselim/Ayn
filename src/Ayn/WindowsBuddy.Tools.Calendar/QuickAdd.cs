using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowsBuddy.Tools.Calendar;

public static class QuickAdd
{
	private const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

	private const string Months = "jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?";

	private const string Days = "sun(?:day)?|mon(?:day)?|tue(?:s(?:day)?)?|wed(?:nesday)?|thu(?:r(?:s(?:day)?)?)?|fri(?:day)?|sat(?:urday)?";

	private const string Mer = "(a\\.?m\\.?|p\\.?m\\.?|a|p)";

	private static readonly Regex ReWeekdays = new Regex("\\b(?:every\\s+weekday|weekdays|on\\s+weekdays)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReEvery = new Regex("\\b(?:every\\s*day|everyday|daily)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReEveryDay = new Regex("\\bevery\\s+(sun(?:day)?|mon(?:day)?|tue(?:s(?:day)?)?|wed(?:nesday)?|thu(?:r(?:s(?:day)?)?)?|fri(?:day)?|sat(?:urday)?)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReWeekly = new Regex("\\b(?:every\\s+week|weekly)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReMonthly = new Regex("\\b(?:every\\s+month|monthly)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReYearly = new Regex("\\b(?:every\\s+year|yearly|annually)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReRange = new Regex("(?:\\b(?:from|at)\\s+)?\\b(\\d{1,2})(?:[:.](\\d{2}))?\\s*(a\\.?m\\.?|p\\.?m\\.?|a|p)?\\s*(?:-|–|to|till|until)\\s*(\\d{1,2})(?:[:.](\\d{2}))?\\s*(a\\.?m\\.?|p\\.?m\\.?|a|p)?(?![\\w/])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReClock = new Regex("(?:\\bat\\s+)?\\b(\\d{1,2})[:.](\\d{2})\\s*(a\\.?m\\.?|p\\.?m\\.?|a|p)?(?![\\w/.])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReMer = new Regex("(?:\\bat\\s+)?\\b(\\d{1,2})\\s*(a\\.?m\\.?|p\\.?m\\.?|a|p)(?![\\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReAt = new Regex("\\bat\\s+(\\d{1,2})\\b(?![:/.\\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReWords = new Regex("\\b(?:at\\s+)?(noon|midday|midnight|morning|afternoon|evening|tonight)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReFor = new Regex("\\bfor\\s+(\\d+(?:\\.\\d+)?)\\s*(h|hr|hrs|hours?|m|mins?|minutes?)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReDayAfter = new Regex("\\b(?:the\\s+)?day\\s+after\\s+tomorrow\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReTomorrow = new Regex("\\b(?:tomorrow|tmrw|tmr)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReToday = new Regex("\\b(?:today|tonight)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReIn = new Regex("\\bin\\s+(\\d{1,3})\\s+(days?|weeks?|months?)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReNextWeek = new Regex("\\bnext\\s+(week|month)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReDayMonth = new Regex("(?:\\bon\\s+)?\\b(\\d{1,2})(?:st|nd|rd|th)?\\s+(?:of\\s+)?(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\\.?(?:,?\\s+(\\d{4}))?\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReMonthDay = new Regex("(?:\\bon\\s+)?\\b(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\\.?\\s+(\\d{1,2})(?:st|nd|rd|th)?(?:,?\\s+(\\d{4}))?\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReNumeric = new Regex("(?:\\bon\\s+)?\\b(\\d{1,2})[/.-](\\d{1,2})(?:[/.-](\\d{2,4}))?\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReOrdinal = new Regex("(?:\\bon\\s+)?(?:\\bthe\\s+)?\\b(\\d{1,2})(?:st|nd|rd|th)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ReWeekday = new Regex("(?:\\bon\\s+)?\\b(?:(next|this)\\s+)?(sun(?:day)?|mon(?:day)?|tue(?:s(?:day)?)?|wed(?:nesday)?|thu(?:r(?:s(?:day)?)?)?|fri(?:day)?|sat(?:urday)?)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static QuickAddResult Parse(string text, DateTime now)
	{
		QuickAddResult r = new QuickAddResult
		{
			Date = now.Date
		};
		string s = " " + (text ?? string.Empty).Trim() + " ";
		DateTime today = now.Date;
		DayOfWeek? everyDay = null;
		Take(ref s, ReWeekdays, delegate
		{
			r.Repeat = CalRepeat.Weekdays;
			return true;
		});
		Take(ref s, ReEveryDay, delegate(Match m)
		{
			everyDay = DayOf(m.Groups[1].Value);
			r.Repeat = CalRepeat.Weekly;
			return true;
		});
		Take(ref s, ReEvery, delegate
		{
			r.Repeat = CalRepeat.Daily;
			return true;
		});
		Take(ref s, ReWeekly, delegate
		{
			r.Repeat = CalRepeat.Weekly;
			return true;
		});
		Take(ref s, ReMonthly, delegate
		{
			r.Repeat = CalRepeat.Monthly;
			return true;
		});
		Take(ref s, ReYearly, delegate
		{
			r.Repeat = CalRepeat.Yearly;
			return true;
		});
		bool tonight = Regex.IsMatch(s, "\\btonight\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		Take(ref s, ReRange, delegate(Match m)
		{
			string value = m.Groups[3].Value;
			string value2 = m.Groups[6].Value;
			bool flag3 = m.Groups[2].Success || m.Groups[5].Success;
			bool flag4 = m.Value.TrimStart().StartsWith("from", StringComparison.OrdinalIgnoreCase) || m.Value.TrimStart().StartsWith("at", StringComparison.OrdinalIgnoreCase);
			if (value.Length == 0 && value2.Length == 0 && !flag3 && !flag4)
			{
				return false;
			}
			int result11 = 0;
			int result12 = 0;
			if (!int.TryParse(m.Groups[1].Value, out var result13) || !int.TryParse(m.Groups[4].Value, out var result14))
			{
				return false;
			}
			if (m.Groups[2].Success)
			{
				int.TryParse(m.Groups[2].Value, out result11);
			}
			if (m.Groups[5].Success)
			{
				int.TryParse(m.Groups[5].Value, out result12);
			}
			if (result13 > 23 || result14 > 23 || result11 > 59 || result12 > 59)
			{
				return false;
			}
			bool? flag5 = Pm(value2);
			bool? flag6 = Pm(value);
			if (!flag5.HasValue && flag6.HasValue)
			{
				flag5 = ((result14 >= result13) ? flag6 : (!flag6));
			}
			if (!flag6.HasValue && flag5.HasValue)
			{
				flag6 = ((result13 <= result14 || result13 == 12) ? flag5 : (!flag5));
			}
			if (!flag6.HasValue)
			{
				flag6 = Guess(result13);
				flag5 = Guess(result14);
				if (result14 < result13 && result13 <= 12)
				{
					flag5 = true;
				}
			}
			r.Start = H(result13, result11, flag6);
			r.End = H(result14, result12, flag5);
			return true;
		});
		if (!r.Start.HasValue)
		{
			Take(ref s, ReClock, delegate(Match m)
			{
				if (!int.TryParse(m.Groups[1].Value, out var result9) || !int.TryParse(m.Groups[2].Value, out var result10) || result9 > 23 || result10 > 59)
				{
					return false;
				}
				bool? flag2 = Pm(m.Groups[3].Value);
				r.Start = H(result9, result10, flag2 ?? ((result9 > 12) ? null : ((result9 < 12 && (tonight || result9 < 7)) ? new bool?(true) : null)));
				return true;
			});
		}
		if (!r.Start.HasValue)
		{
			Take(ref s, ReMer, delegate(Match m)
			{
				if (!int.TryParse(m.Groups[1].Value, out var result8) || result8 < 1 || result8 > 12)
				{
					return false;
				}
				r.Start = H(result8, 0, Pm(m.Groups[2].Value));
				return true;
			});
		}
		if (!r.Start.HasValue)
		{
			Take(ref s, ReAt, delegate(Match m)
			{
				if (!int.TryParse(m.Groups[1].Value, out var result7) || result7 > 23)
				{
					return false;
				}
				r.Start = H(result7, 0, (result7 > 12) ? null : (tonight ? new bool?(true) : Guess(result7)));
				return true;
			});
		}
		if (!r.Start.HasValue)
		{
			Take(ref s, ReWords, delegate(Match m)
			{
				switch (m.Groups[1].Value.ToLowerInvariant())
				{
				case "noon":
				case "midday":
					r.Start = TimeSpan.FromHours(12.0);
					break;
				case "midnight":
					r.Start = TimeSpan.Zero;
					break;
				case "morning":
					r.Start = TimeSpan.FromHours(9.0);
					break;
				case "afternoon":
					r.Start = TimeSpan.FromHours(14.0);
					break;
				default:
					r.Start = TimeSpan.FromHours(19.0);
					break;
				}
				if (m.Groups[1].Value.ToLowerInvariant() == "tonight")
				{
					r.Date = today;
					r.HasDate = true;
				}
				return true;
			});
		}
		Take(ref s, ReFor, delegate(Match m)
		{
			if (!r.Start.HasValue || !double.TryParse(m.Groups[1].Value, NumberStyles.Float, Cal.Inv, out var result6) || result6 <= 0.0)
			{
				return false;
			}
			TimeSpan timeSpan = (m.Groups[2].Value.StartsWith("h", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromHours(result6) : TimeSpan.FromMinutes(result6));
			if (timeSpan > TimeSpan.FromHours(23.0))
			{
				return false;
			}
			r.End = r.Start.Value + timeSpan;
			return true;
		});
		Func<DateTime, bool> setDate = delegate(DateTime d)
		{
			r.Date = d.Date;
			r.HasDate = true;
			return true;
		};
		if (!r.HasDate)
		{
			Take(ref s, ReDayAfter, (Match m) => setDate(today.AddDays(2.0)));
		}
		if (!r.HasDate)
		{
			Take(ref s, ReTomorrow, (Match m) => setDate(today.AddDays(1.0)));
		}
		if (!r.HasDate)
		{
			Take(ref s, ReToday, (Match m) => setDate(today));
		}
		if (!r.HasDate)
		{
			Take(ref s, ReIn, delegate(Match m)
			{
				if (!int.TryParse(m.Groups[1].Value, out var result5))
				{
					return false;
				}
				string text2 = m.Groups[2].Value.ToLowerInvariant();
				return setDate(text2.StartsWith("day") ? today.AddDays(result5) : (text2.StartsWith("week") ? today.AddDays(7 * result5) : today.AddMonths(result5)));
			});
		}
		if (!r.HasDate)
		{
			Take(ref s, ReNextWeek, (Match m) => setDate((m.Groups[1].Value.ToLowerInvariant() == "week") ? today.AddDays(7.0) : new DateTime(today.Year, today.Month, 1).AddMonths(1)));
		}
		if (!r.HasDate)
		{
			Take(ref s, ReDayMonth, (Match m) => DayMonth(r, today, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
		}
		if (!r.HasDate)
		{
			Take(ref s, ReMonthDay, (Match m) => DayMonth(r, today, m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value));
		}
		if (!r.HasDate)
		{
			Take(ref s, ReNumeric, delegate(Match m)
			{
				int result2 = 0;
				if (!int.TryParse(m.Groups[1].Value, out var result3) || !int.TryParse(m.Groups[2].Value, out var result4))
				{
					return false;
				}
				if (m.Groups[3].Success)
				{
					int.TryParse(m.Groups[3].Value, out result2);
					if (result2 < 100)
					{
						result2 += 2000;
					}
				}
				bool flag = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.TrimStart().StartsWith("M", StringComparison.OrdinalIgnoreCase);
				int num3 = (flag ? result4 : result3);
				int num4 = (flag ? result3 : result4);
				if (num4 > 12 && num3 <= 12)
				{
					int num5 = num3;
					num3 = num4;
					num4 = num5;
				}
				return SetDmy(r, today, num3, num4, result2);
			});
		}
		if (!r.HasDate)
		{
			Take(ref s, ReOrdinal, delegate(Match m)
			{
				if (!int.TryParse(m.Groups[1].Value, out var result) || result < 1 || result > 31)
				{
					return false;
				}
				DateTime dateTime = new DateTime(today.Year, today.Month, 1);
				for (int i = 0; i < 13; i++)
				{
					DateTime dateTime2 = dateTime.AddMonths(i);
					if (result <= DateTime.DaysInMonth(dateTime2.Year, dateTime2.Month) && new DateTime(dateTime2.Year, dateTime2.Month, result) >= today)
					{
						return setDate(new DateTime(dateTime2.Year, dateTime2.Month, result));
					}
				}
				return false;
			});
		}
		if (!r.HasDate)
		{
			Take(ref s, ReWeekday, delegate(Match m)
			{
				DayOfWeek? dayOfWeek = DayOf(m.Groups[2].Value);
				if (!dayOfWeek.HasValue)
				{
					return false;
				}
				int num2 = (dayOfWeek.Value - today.DayOfWeek + 7) % 7;
				if (m.Groups[1].Value.ToLowerInvariant() == "next" && num2 == 0)
				{
					num2 = 7;
				}
				return setDate(today.AddDays(num2));
			});
		}
		if (everyDay.HasValue && !r.HasDate)
		{
			int num = (everyDay.Value - today.DayOfWeek + 7) % 7;
			r.Date = today.AddDays(num);
			r.HasDate = true;
		}
		if (!r.HasDate && r.Start.HasValue && today + r.Start.Value <= now)
		{
			r.Date = (everyDay.HasValue ? today.AddDays(7.0) : today.AddDays(1.0));
		}
		if (r.HasDate && r.Date == today && everyDay.HasValue && r.Start.HasValue && today + r.Start.Value <= now)
		{
			r.Date = today.AddDays(7.0);
		}
		if (r.Start.HasValue && r.End.HasValue && r.End.Value <= r.Start.Value)
		{
			r.End = ((r.End.Value + TimeSpan.FromHours(12.0) > r.Start.Value && r.End.Value + TimeSpan.FromHours(12.0) < TimeSpan.FromDays(1.0)) ? new TimeSpan?(r.End.Value + TimeSpan.FromHours(12.0)) : null);
		}
		r.Title = CleanTitle(s);
		return r;
	}

	private static bool DayMonth(QuickAddResult r, DateTime today, string dayText, string monthText, string yearText)
	{
		int result = 0;
		if (!int.TryParse(dayText, out var result2))
		{
			return false;
		}
		if (!string.IsNullOrEmpty(yearText))
		{
			int.TryParse(yearText, out result);
		}
		int num = MonthOf(monthText);
		if (num > 0)
		{
			return SetDmy(r, today, result2, num, result);
		}
		return false;
	}

	private static bool SetDmy(QuickAddResult r, DateTime today, int d, int month, int y)
	{
		if (month < 1 || month > 12 || d < 1)
		{
			return false;
		}
		int num = ((y > 0) ? y : today.Year);
		if (num < 1900 || num > 9000 || d > DateTime.DaysInMonth(num, month))
		{
			return false;
		}
		DateTime dateTime = new DateTime(num, month, d);
		if (y == 0 && dateTime < today)
		{
			num++;
			if (d > DateTime.DaysInMonth(num, month))
			{
				return false;
			}
			dateTime = new DateTime(num, month, d);
		}
		r.Date = dateTime;
		r.HasDate = true;
		return true;
	}

	private static void Take(ref string s, Regex re, Func<Match, bool> apply)
	{
		Match match = re.Match(s);
		while (match.Success)
		{
			if (apply(match))
			{
				s = s.Substring(0, match.Index) + " " + s.Substring(match.Index + match.Length);
				break;
			}
			match = match.NextMatch();
		}
	}

	private static string CleanTitle(string s)
	{
		string text = Regex.Replace(s, "\\s+", " ").Trim();
		for (int i = 0; i < 4; i++)
		{
			string text2 = text;
			text = Regex.Replace(text, "^(?:on|at|from|by|for|every|next|this|the|,|-|–|·)\\s+", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
			text = Regex.Replace(text, "\\s+(?:on|at|from|by|for|every|next|this|the|,|-|–|·)$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
			text = text.Trim(',', '-', '–', '·', ' ');
			if (text == text2)
			{
				break;
			}
		}
		return text;
	}

	private static bool? Pm(string mer)
	{
		if (string.IsNullOrEmpty(mer))
		{
			return null;
		}
		return char.ToLowerInvariant(mer[0]) == 'p';
	}

	private static bool? Guess(int h)
	{
		if (h == 0 || h > 12)
		{
			return null;
		}
		return h == 12 || h < 7;
	}

	private static TimeSpan H(int h, int m, bool? pm)
	{
		if (pm.HasValue && h <= 12)
		{
			if (pm.Value && h < 12)
			{
				h += 12;
			}
			if (!pm.Value && h == 12)
			{
				h = 0;
			}
		}
		return new TimeSpan(h % 24, m, 0);
	}

	private static int MonthOf(string s)
	{
		string text = (s ?? "").ToLowerInvariant();
		string[] array = new string[12]
		{
			"jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct",
			"nov", "dec"
		};
		for (int i = 0; i < 12; i++)
		{
			if (text.StartsWith(array[i], StringComparison.Ordinal))
			{
				return i + 1;
			}
		}
		return 0;
	}

	private static DayOfWeek? DayOf(string s)
	{
		string text = (s ?? "").ToLowerInvariant();
		if (text.StartsWith("sun"))
		{
			return DayOfWeek.Sunday;
		}
		if (text.StartsWith("mon"))
		{
			return DayOfWeek.Monday;
		}
		if (text.StartsWith("tue"))
		{
			return DayOfWeek.Tuesday;
		}
		if (text.StartsWith("wed"))
		{
			return DayOfWeek.Wednesday;
		}
		if (text.StartsWith("thu"))
		{
			return DayOfWeek.Thursday;
		}
		if (text.StartsWith("fri"))
		{
			return DayOfWeek.Friday;
		}
		if (text.StartsWith("sat"))
		{
			return DayOfWeek.Saturday;
		}
		return null;
	}
}
