using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WindowsBuddy.Tools.Calendar;

public static class Ics
{
	internal sealed class ExpansionBudget
	{
		private readonly Stopwatch _clock = Stopwatch.StartNew();

		private int _remaining = 100000;

		internal void Check()
		{
			if (--_remaining < 0 || _clock.ElapsedMilliseconds >= 50)
			{
				throw new ExpansionLimitException();
			}
		}
	}

	private sealed class ExpansionLimitException : Exception
	{
	}

	private const int MaxPeriods = 200000;

	private static readonly Dictionary<string, TimeZoneInfo> _zoneCache = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, string> IanaToWindows = BuildIana();

	public static IcsCalendar Parse(string text)
	{
		IcsCalendar icsCalendar = new IcsCalendar();
		if (string.IsNullOrEmpty(text))
		{
			return icsCalendar;
		}
		List<string> list = Unfold(text);
		Dictionary<string, TimeZoneInfo> vtz = ReadTimeZones(list);
		Func<string, string, IcsTime> time = (string value, string tzid) => ParseTime(value, tzid, vtz);
		IcsEvent icsEvent = null;
		int num = 0;
		foreach (string item in list)
		{
			if (!SplitLine(item, out var name, out var prms, out var value2))
			{
				continue;
			}
			if (name == "BEGIN")
			{
				string text2 = value2.Trim().ToUpperInvariant();
				if (text2 == "VEVENT" && icsEvent == null && num == 0)
				{
					icsEvent = new IcsEvent();
				}
				else if (icsEvent != null)
				{
					num++;
				}
			}
			else if (name == "END")
			{
				string text3 = value2.Trim().ToUpperInvariant();
				if (icsEvent != null && num > 0)
				{
					num--;
				}
				else if (text3 == "VEVENT" && icsEvent != null)
				{
					if (icsEvent.Start != null)
					{
						icsCalendar.Events.Add(icsEvent);
					}
					icsEvent = null;
				}
			}
			else if (icsEvent == null)
			{
				if (name == "X-WR-CALNAME" && icsCalendar.Name == null)
				{
					icsCalendar.Name = Unescape(value2).Trim();
				}
			}
			else
			{
				if (num > 0)
				{
					continue;
				}
				prms.TryGetValue("TZID", out var tz);
				try
				{
					switch (name)
					{
					case "UID":
						icsEvent.Uid = value2.Trim();
						break;
					case "SUMMARY":
						icsEvent.Summary = Unescape(value2).Trim();
						break;
					case "DESCRIPTION":
						icsEvent.Description = Unescape(value2);
						break;
					case "LOCATION":
						icsEvent.Location = Unescape(value2).Trim();
						break;
					case "STATUS":
						icsEvent.Status = value2.Trim();
						break;
					case "DTSTART":
						icsEvent.Start = time(value2, tz);
						break;
					case "DTEND":
						icsEvent.End = time(value2, tz);
						break;
					case "DURATION":
						icsEvent.Duration = ParseDuration(value2);
						break;
					case "RRULE":
						icsEvent.Rule = IcsRule.Parse(value2, (string v) => time(v, tz));
						break;
					case "RECURRENCE-ID":
						icsEvent.RecurrenceId = time(value2, tz);
						break;
					case "EXDATE":
					{
						string[] array2 = value2.Split(',');
						foreach (string arg2 in array2)
						{
							IcsTime icsTime2 = time(arg2, tz);
							if (icsTime2 != null)
							{
								icsEvent.ExDates.Add(icsTime2);
							}
						}
						break;
					}
					case "RDATE":
					{
						if (prms.TryGetValue("VALUE", out var value3) && value3.ToUpperInvariant() == "PERIOD")
						{
							break;
						}
						string[] array = value2.Split(',');
						foreach (string arg in array)
						{
							IcsTime icsTime = time(arg, tz);
							if (icsTime != null)
							{
								icsEvent.RDates.Add(icsTime);
							}
						}
						break;
					}
					}
				}
				catch
				{
				}
			}
		}
		return icsCalendar;
	}

	private static List<string> Unfold(string text)
	{
		List<string> list = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		int length = text.Length;
		while (num < length)
		{
			int num2 = text.IndexOf('\n', num);
			if (num2 < 0)
			{
				num2 = length;
			}
			int num3 = num2;
			if (num3 > num && text[num3 - 1] == '\r')
			{
				num3--;
			}
			string text2 = text.Substring(num, num3 - num);
			if (text2.Length > 0 && (text2[0] == ' ' || text2[0] == '\t'))
			{
				stringBuilder.Append(text2, 1, text2.Length - 1);
			}
			else
			{
				if (stringBuilder.Length > 0)
				{
					list.Add(stringBuilder.ToString());
				}
				stringBuilder.Clear();
				stringBuilder.Append(text2);
			}
			num = num2 + 1;
		}
		if (stringBuilder.Length > 0)
		{
			list.Add(stringBuilder.ToString());
		}
		return list;
	}

	private static bool SplitLine(string line, out string name, out Dictionary<string, string> prms, out string value)
	{
		name = (value = null);
		prms = null;
		if (string.IsNullOrEmpty(line))
		{
			return false;
		}
		int num = -1;
		bool flag = false;
		for (int i = 0; i < line.Length; i++)
		{
			switch (line[i])
			{
			case '"':
				flag = !flag;
				continue;
			case ':':
				if (flag)
				{
					continue;
				}
				break;
			default:
				continue;
			}
			num = i;
			break;
		}
		if (num <= 0)
		{
			return false;
		}
		string text = line.Substring(0, num);
		value = line.Substring(num + 1);
		prms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string[] array = text.Split(';');
		name = array[0].Trim().ToUpperInvariant();
		for (int j = 1; j < array.Length; j++)
		{
			int num2 = array[j].IndexOf('=');
			if (num2 > 0)
			{
				prms[array[j].Substring(0, num2).Trim()] = array[j].Substring(num2 + 1).Trim().Trim('"');
			}
		}
		return true;
	}

	public static string Unescape(string v)
	{
		if (string.IsNullOrEmpty(v) || v.IndexOf('\\') < 0)
		{
			return v ?? string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(v.Length);
		for (int i = 0; i < v.Length; i++)
		{
			char c = v[i];
			if (c == '\\' && i + 1 < v.Length)
			{
				char c2 = v[++i];
				if (c2 == 'n' || c2 == 'N')
				{
					stringBuilder.Append('\n');
				}
				else
				{
					stringBuilder.Append(c2);
				}
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static IcsTime ParseTime(string value, string tzid, Dictionary<string, TimeZoneInfo> vtz)
	{
		string text = (value ?? string.Empty).Trim();
		if (text.Length < 8)
		{
			return null;
		}
		if (!int.TryParse(text.Substring(0, 4), out var result) || !int.TryParse(text.Substring(4, 2), out var result2) || !int.TryParse(text.Substring(6, 2), out var result3))
		{
			return null;
		}
		if (result < 1 || result2 < 1 || result2 > 12 || result3 < 1 || result3 > DateTime.DaysInMonth(result, result2))
		{
			return null;
		}
		if (text.Length < 15 || text[8] != 'T')
		{
			IcsTime icsTime = new IcsTime();
			icsTime.Value = new DateTime(result, result2, result3);
			icsTime.IsDate = true;
			return icsTime;
		}
		if (!int.TryParse(text.Substring(9, 2), out var result4) || !int.TryParse(text.Substring(11, 2), out var result5) || !int.TryParse(text.Substring(13, 2), out var result6))
		{
			return null;
		}
		if (result4 > 23)
		{
			result4 = 23;
		}
		if (result5 > 59)
		{
			result5 = 59;
		}
		if (result6 > 59)
		{
			result6 = 59;
		}
		IcsTime icsTime2 = new IcsTime();
		icsTime2.Value = new DateTime(result, result2, result3, result4, result5, result6);
		IcsTime icsTime3 = icsTime2;
		if (text.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
		{
			icsTime3.IsUtc = true;
		}
		else if (!string.IsNullOrEmpty(tzid))
		{
			icsTime3.Zone = ResolveZone(tzid, vtz);
		}
		return icsTime3;
	}

	public static TimeSpan? ParseDuration(string v)
	{
		v = (v ?? string.Empty).Trim().ToUpperInvariant();
		if (v.Length < 2)
		{
			return null;
		}
		bool flag = v[0] == '-';
		v = v.TrimStart('+', '-');
		if (v.Length == 0 || v[0] != 'P')
		{
			return null;
		}
		double num = 0.0;
		double num2 = 0.0;
		bool flag2 = false;
		bool flag3 = false;
		for (int i = 1; i < v.Length; i++)
		{
			char c = v[i];
			if (c == 'T')
			{
				flag2 = true;
				continue;
			}
			if (char.IsDigit(c))
			{
				num2 = num2 * 10.0 + (double)(c - 48);
				continue;
			}
			switch (c)
			{
			case 'W':
				num += num2 * 7.0 * 86400.0;
				break;
			case 'D':
				num += num2 * 86400.0;
				break;
			case 'H':
				num += num2 * 3600.0;
				break;
			case 'M':
				num += (flag2 ? (num2 * 60.0) : (num2 * 30.0 * 86400.0));
				break;
			case 'S':
				num += num2;
				break;
			default:
				return null;
			}
			num2 = 0.0;
			flag3 = true;
		}
		if (!flag3)
		{
			return null;
		}
		return TimeSpan.FromSeconds(flag ? (0.0 - num) : num);
	}

	public static TimeZoneInfo ResolveZone(string tzid, Dictionary<string, TimeZoneInfo> vtz)
	{
		if (string.IsNullOrWhiteSpace(tzid))
		{
			return null;
		}
		string text = tzid.Trim().Trim('"');
		lock (_zoneCache)
		{
			if (_zoneCache.TryGetValue(text, out var value))
			{
				return value;
			}
		}
		TimeZoneInfo value2 = FindZone(text);
		if (value2 != null)
		{
			lock (_zoneCache)
			{
				_zoneCache[text] = value2;
				return value2;
			}
		}
		vtz?.TryGetValue(text, out value2);
		return value2;
	}

	private static TimeZoneInfo FindZone(string id)
	{
		string text = id;
		if (id.StartsWith("/", StringComparison.Ordinal))
		{
			string[] array = id.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length >= 2)
			{
				text = array[array.Length - 2] + "/" + array[array.Length - 1];
			}
		}
		switch (text)
		{
		case "UTC":
		case "GMT":
		case "Etc/UTC":
		case "Etc/GMT":
		case "Z":
		case "Coordinated Universal Time":
			return TimeZoneInfo.Utc;
		default:
		{
			try
			{
				return TimeZoneInfo.FindSystemTimeZoneById(text);
			}
			catch
			{
			}
			if (IanaToWindows.TryGetValue(text, out var value))
			{
				try
				{
					return TimeZoneInfo.FindSystemTimeZoneById(value);
				}
				catch
				{
				}
			}
			foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
			{
				if (string.Equals(systemTimeZone.DisplayName, id, StringComparison.OrdinalIgnoreCase) || string.Equals(systemTimeZone.StandardName, id, StringComparison.OrdinalIgnoreCase))
				{
					return systemTimeZone;
				}
			}
			return null;
		}
		}
	}

	private static Dictionary<string, TimeZoneInfo> ReadTimeZones(List<string> lines)
	{
		Dictionary<string, TimeZoneInfo> dictionary = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);
		string text = null;
		string text2 = null;
		TimeSpan? timeSpan = null;
		TimeSpan? dst = null;
		string stdRule = null;
		string dstRule = null;
		string text3 = null;
		string text4 = null;
		string stdStart = null;
		string dstStart = null;
		TimeSpan? timeSpan2 = null;
		DateTime dateTime = DateTime.MinValue;
		DateTime dateTime2 = DateTime.MinValue;
		foreach (string line in lines)
		{
			if (!SplitLine(line, out var name, out var _, out var value))
			{
				continue;
			}
			string text5 = value.Trim().ToUpperInvariant();
			if (name == "BEGIN" && text5 == "VTIMEZONE")
			{
				text = null;
				timeSpan = (dst = (dst = null));
				stdRule = (dstRule = (stdStart = (dstStart = null)));
				dateTime = (dateTime2 = DateTime.MinValue);
			}
			else if (name == "BEGIN" && (text5 == "STANDARD" || text5 == "DAYLIGHT"))
			{
				text2 = text5;
				text3 = (text4 = null);
				timeSpan2 = null;
			}
			else if (name == "END" && (text5 == "STANDARD" || text5 == "DAYLIGHT") && text2 != null)
			{
				DateTime result = DateTime.MinValue;
				if (text4 != null && text4.Length >= 8)
				{
					DateTime.TryParseExact(text4.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
				}
				if (text2 == "STANDARD" && result >= dateTime)
				{
					dateTime = result;
					timeSpan = timeSpan2;
					stdRule = text3;
					stdStart = text4;
				}
				if (text2 == "DAYLIGHT" && result >= dateTime2)
				{
					dateTime2 = result;
					dst = timeSpan2;
					dstRule = text3;
					dstStart = text4;
				}
				text2 = null;
			}
			else if (name == "END" && text5 == "VTIMEZONE")
			{
				if (text != null && timeSpan.HasValue)
				{
					try
					{
						dictionary[text] = BuildZone(text, timeSpan.Value, dst, stdRule, stdStart, dstRule, dstStart);
					}
					catch
					{
					}
				}
				text = null;
			}
			else if (name == "TZID" && text2 == null)
			{
				text = value.Trim().Trim('"');
			}
			else if (text2 != null && name == "TZOFFSETTO")
			{
				timeSpan2 = ParseOffset(value);
			}
			else if (text2 != null && name == "RRULE")
			{
				text3 = value;
			}
			else if (text2 != null && name == "DTSTART")
			{
				text4 = value.Trim();
			}
		}
		return dictionary;
	}

	private static TimeZoneInfo BuildZone(string id, TimeSpan std, TimeSpan? dst, string stdRule, string stdStart, string dstRule, string dstStart)
	{
		TimeSpan baseUtcOffset = TimeSpan.FromMinutes(Math.Round(std.TotalMinutes));
		if (dst.HasValue && dst.Value != std && stdRule != null && dstRule != null)
		{
			TimeZoneInfo.TransitionTime? transitionTime = Transition(dstRule, dstStart);
			TimeZoneInfo.TransitionTime? transitionTime2 = Transition(stdRule, stdStart);
			if (transitionTime.HasValue && transitionTime2.HasValue)
			{
				TimeZoneInfo.AdjustmentRule adjustmentRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, dst.Value - std, transitionTime.Value, transitionTime2.Value);
				return TimeZoneInfo.CreateCustomTimeZone(id, baseUtcOffset, id, id, id, new TimeZoneInfo.AdjustmentRule[1] { adjustmentRule });
			}
		}
		return TimeZoneInfo.CreateCustomTimeZone(id, baseUtcOffset, id, id);
	}

	private static TimeZoneInfo.TransitionTime? Transition(string rrule, string dtstart)
	{
		IcsRule icsRule = IcsRule.Parse(rrule, null);
		if (icsRule == null || icsRule.Freq != IcsFreq.Yearly || icsRule.ByMonth.Count == 0)
		{
			return null;
		}
		DateTime timeOfDay = new DateTime(1, 1, 1, 2, 0, 0);
		if (dtstart != null && dtstart.Length >= 15 && int.TryParse(dtstart.Substring(9, 2), out var result) && int.TryParse(dtstart.Substring(11, 2), out var result2))
		{
			timeOfDay = new DateTime(1, 1, 1, result % 24, result2 % 60, 0);
		}
		if (icsRule.ByDay.Count == 0)
		{
			if (icsRule.ByMonthDay.Count == 0 || icsRule.ByMonthDay[0] < 1)
			{
				return null;
			}
			return TimeZoneInfo.TransitionTime.CreateFixedDateRule(timeOfDay, icsRule.ByMonth[0], icsRule.ByMonthDay[0]);
		}
		int key = icsRule.ByDay[0].Key;
		DayOfWeek value = icsRule.ByDay[0].Value;
		int week = ((key < 0) ? 5 : Math.Max(1, Math.Min(5, key)));
		if (key == 0 && icsRule.ByMonthDay.Count > 0)
		{
			week = Math.Min(5, (icsRule.ByMonthDay[0] + 6) / 7);
		}
		return TimeZoneInfo.TransitionTime.CreateFloatingDateRule(timeOfDay, icsRule.ByMonth[0], week, value);
	}

	private static TimeSpan? ParseOffset(string v)
	{
		v = (v ?? string.Empty).Trim();
		if (v.Length < 5)
		{
			return null;
		}
		int num = ((v[0] != '-') ? 1 : (-1));
		v = v.TrimStart('+', '-');
		int result = 0;
		if (!int.TryParse(v.Substring(0, 2), out var result2) || !int.TryParse(v.Substring(2, 2), out var result3))
		{
			return null;
		}
		if (v.Length >= 6)
		{
			int.TryParse(v.Substring(4, 2), out result);
		}
		return TimeSpan.FromSeconds(num * (result2 * 3600 + result3 * 60 + result));
	}

	public static DateTime WallToLocal(DateTime wall, IcsTime t)
	{
		if (t == null || t.IsDate)
		{
			return wall;
		}
		if (t.IsUtc)
		{
			return DateTime.SpecifyKind(DateTime.SpecifyKind(wall, DateTimeKind.Utc).ToLocalTime(), DateTimeKind.Unspecified);
		}
		if (t.Zone == null)
		{
			return wall;
		}
		try
		{
			DateTime dateTime = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
			if (t.Zone.IsInvalidTime(dateTime))
			{
				dateTime = dateTime.AddHours(1.0);
			}
			return DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(dateTime, t.Zone, TimeZoneInfo.Local), DateTimeKind.Unspecified);
		}
		catch
		{
			return wall;
		}
	}

	public static bool Expand(IList<IcsEvent> events, DateTime from, DateTime to, List<IcsOccurrence> into)
	{
		return Expand(events, from, to, into, new ExpansionBudget());
	}

	internal static bool Expand(IList<IcsEvent> events, DateTime from, DateTime to, List<IcsOccurrence> into, ExpansionBudget budget)
	{
		int count = into.Count;
		try
		{
			ExpandCore(events, from, to, into, budget);
			return true;
		}
		catch (ExpansionLimitException)
		{
			into.RemoveRange(count, into.Count - count);
			return false;
		}
	}

	private static void ExpandCore(IList<IcsEvent> events, DateTime from, DateTime to, List<IcsOccurrence> into, ExpansionBudget budget)
	{
		Dictionary<string, HashSet<long>> dictionary = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
		foreach (IcsEvent @event in events)
		{
			budget.Check();
			if (@event != null && @event.RecurrenceId != null && @event.Uid != null)
			{
				if (!dictionary.TryGetValue(@event.Uid, out var value))
				{
					value = (dictionary[@event.Uid] = new HashSet<long>());
				}
				value.Add(@event.RecurrenceId.Key());
			}
		}
		foreach (IcsEvent event2 in events)
		{
			budget.Check();
			if (event2 == null)
			{
				continue;
			}
			try
			{
				if (event2.Start == null)
				{
					continue;
				}
				if (event2.RecurrenceId != null)
				{
					if (!event2.Cancelled)
					{
						AddIfOverlaps(event2, event2.Start.Value, from, to, into);
					}
				}
				else
				{
					if (event2.Cancelled)
					{
						continue;
					}
					if (event2.Rule == null && event2.RDates.Count == 0)
					{
						AddIfOverlaps(event2, event2.Start.Value, from, to, into);
						continue;
					}
					HashSet<long> hashSet2 = null;
					HashSet<long> hashSet3 = null;
					if (event2.ExDates.Count > 0)
					{
						hashSet2 = new HashSet<long>();
						hashSet3 = new HashSet<long>();
						foreach (IcsTime exDate in event2.ExDates)
						{
							budget.Check();
							if (event2.AllDay || exDate.IsDate)
							{
								hashSet3.Add(exDate.Value.Date.Ticks);
							}
							else
							{
								hashSet2.Add(exDate.Key());
							}
						}
					}
					if (event2.Uid == null || !dictionary.TryGetValue(event2.Uid, out var value2))
					{
						value2 = null;
					}
					TimeSpan length = event2.Length;
					DateTime dateTime = to.AddDays(2.0);
					DateTime dateTime2 = from.AddDays(-2.0) - length;
					List<DateTime> list = new List<DateTime>();
					if (event2.Rule != null)
					{
						GenerateCore(event2.Start, event2.Rule, dateTime, list, budget);
					}
					else
					{
						list.Add(event2.Start.Value);
					}
					HashSet<long> hashSet4 = new HashSet<long>();
					foreach (DateTime item3 in list)
					{
						budget.Check();
						if (!(item3 < dateTime2) && !(item3 > dateTime))
						{
							IcsTime icsTime = event2.Start.WithValue(item3);
							long item = (event2.AllDay ? item3.Date.Ticks : icsTime.Key());
							if ((hashSet2 == null || (!hashSet2.Contains(item) && !hashSet3.Contains(item3.Date.Ticks))) && (value2 == null || (!value2.Contains(item) && !value2.Contains(item3.Date.Ticks))) && hashSet4.Add(item))
							{
								AddIfOverlaps(event2, item3, from, to, into);
							}
						}
					}
					foreach (IcsTime rDate in event2.RDates)
					{
						budget.Check();
						long item2 = (event2.AllDay ? rDate.Value.Date.Ticks : rDate.Key());
						if ((hashSet2 == null || (!hashSet2.Contains(item2) && !hashSet3.Contains(rDate.Value.Date.Ticks))) && (value2 == null || (!value2.Contains(item2) && !value2.Contains(rDate.Value.Date.Ticks))) && hashSet4.Add(item2))
						{
							AddIfOverlaps(event2, rDate.Value, from, to, into, rDate);
						}
					}
					continue;
				}
			}
			catch (ArgumentOutOfRangeException)
			{
			}
			catch (OverflowException)
			{
			}
		}
	}

	private static void AddIfOverlaps(IcsEvent e, DateTime wallStart, DateTime from, DateTime to, List<IcsOccurrence> into, IcsTime instance = null)
	{
		DateTime dateTime;
		DateTime dateTime2;
		if (e.AllDay)
		{
			dateTime = wallStart.Date;
			dateTime2 = dateTime + TimeSpan.FromDays(Math.Max(1.0, Math.Round(e.Length.TotalDays)));
		}
		else
		{
			dateTime = WallToLocal(wallStart, instance ?? e.Start);
			dateTime2 = dateTime + e.Length;
		}
		bool num;
		if (!(dateTime2 > dateTime))
		{
			if (!(dateTime >= from))
			{
				return;
			}
			num = dateTime < to;
		}
		else
		{
			if (!(dateTime < to))
			{
				return;
			}
			num = dateTime2 > from;
		}
		if (num)
		{
			into.Add(new IcsOccurrence
			{
				Event = e,
				Start = dateTime,
				End = dateTime2,
				AllDay = e.AllDay
			});
		}
	}

	public static void Generate(IcsTime start, IcsRule r, DateTime wallTo, List<DateTime> into)
	{
		GenerateCore(start, r, wallTo, into, new ExpansionBudget());
	}

	private static void GenerateCore(IcsTime start, IcsRule r, DateTime wallTo, List<DateTime> into, ExpansionBudget budget)
	{
		DateTime value = start.Value;
		int num = 0;
		Func<DateTime, bool> func = BuildUntil(start, r);
		if (func(value))
		{
			return;
		}
		into.Add(value);
		num++;
		if (r.Count > 0 && num >= r.Count)
		{
			return;
		}
		List<DateTime> list = new List<DateTime>(32);
		TimeSpan timeOfDay = value.TimeOfDay;
		int num2 = Math.Max(1, r.Interval);
		DateTime dateTime = value.Date.AddDays(-((value.DayOfWeek - r.WeekStart + 7) % 7));
		DateTime dateTime2 = new DateTime(value.Year, value.Month, 1);
		for (int i = 0; i < 200000; i++)
		{
			budget.Check();
			list.Clear();
			DateTime dateTime3;
			switch (r.Freq)
			{
			default:
				return;
			case IcsFreq.Daily:
			{
				DateTime dateTime6;
				try
				{
					dateTime6 = value.Date.AddDays((double)i * (double)num2);
				}
				catch
				{
					return;
				}
				dateTime3 = dateTime6;
				if (Matches(dateTime6, r, checkDay: true))
				{
					list.Add(dateTime6 + timeOfDay);
				}
				break;
			}
			case IcsFreq.Weekly:
			{
				DateTime dateTime4;
				try
				{
					dateTime4 = dateTime.AddDays(7.0 * (double)i * (double)num2);
				}
				catch
				{
					return;
				}
				dateTime3 = dateTime4;
				for (int j = 0; j < 7; j++)
				{
					DateTime dateTime5 = dateTime4.AddDays(j);
					if (((r.ByDay.Count == 0) ? (dateTime5.DayOfWeek == value.DayOfWeek) : HasDay(r, dateTime5.DayOfWeek)) && (r.ByMonth.Count == 0 || r.ByMonth.Contains(dateTime5.Month)))
					{
						list.Add(dateTime5 + timeOfDay);
					}
				}
				break;
			}
			case IcsFreq.Monthly:
			{
				DateTime dateTime7;
				try
				{
					dateTime7 = dateTime2.AddMonths(i * num2);
				}
				catch
				{
					return;
				}
				dateTime3 = dateTime7;
				if (r.ByMonth.Count == 0 || r.ByMonth.Contains(dateTime7.Month))
				{
					MonthDays(dateTime7, r, value, timeOfDay, list);
				}
				break;
			}
			case IcsFreq.Yearly:
			{
				long num3 = value.Year + (long)i * (long)num2;
				if (num3 > 9998)
				{
					return;
				}
				int num4 = (int)num3;
				dateTime3 = new DateTime(num4, 1, 1);
				if (r.ByMonth.Count > 0)
				{
					foreach (int item in r.ByMonth)
					{
						MonthDays(new DateTime(num4, item, 1), r, value, timeOfDay, list);
					}
				}
				else if (r.ByDay.Count > 0 && r.ByMonthDay.Count == 0)
				{
					YearDays(num4, r, timeOfDay, list);
				}
				else
				{
					MonthDays(new DateTime(num4, value.Month, 1), r, value, timeOfDay, list);
				}
				break;
			}
			}
			if (dateTime3 > wallTo)
			{
				break;
			}
			list.Sort();
			ApplySetPos(r, list);
			foreach (DateTime item2 in list)
			{
				if (!(item2 <= value))
				{
					if (func(item2) || item2 > wallTo)
					{
						return;
					}
					into.Add(item2);
					num++;
					if (r.Count > 0 && num >= r.Count)
					{
						return;
					}
				}
			}
		}
	}

	private static Func<DateTime, bool> BuildUntil(IcsTime start, IcsRule r)
	{
		if (r.Until == null)
		{
			return (DateTime c) => false;
		}
		if (r.Until.IsDate)
		{
			DateTime u = r.Until.Value.Date;
			return (DateTime c) => c.Date > u;
		}
		if (r.Until.IsUtc && !start.IsUtc && start.Zone != null)
		{
			DateTime uLocal = r.Until.ToLocal();
			return (DateTime c) => WallToLocal(c, start) > uLocal;
		}
		DateTime until = r.Until.Value;
		if (r.Until.IsUtc && !start.IsUtc && start.Zone == null)
		{
			until = r.Until.ToLocal();
		}
		return (DateTime c) => c > until;
	}

	private static bool HasDay(IcsRule r, DayOfWeek d)
	{
		foreach (KeyValuePair<int, DayOfWeek> item in r.ByDay)
		{
			if (item.Value == d)
			{
				return true;
			}
		}
		return false;
	}

	private static bool Matches(DateTime day, IcsRule r, bool checkDay)
	{
		if (r.ByMonth.Count > 0 && !r.ByMonth.Contains(day.Month))
		{
			return false;
		}
		if (r.ByMonthDay.Count > 0)
		{
			int num = DateTime.DaysInMonth(day.Year, day.Month);
			bool flag = false;
			foreach (int item in r.ByMonthDay)
			{
				if (((item > 0) ? item : (num + item + 1)) == day.Day)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
		}
		if (checkDay && r.ByDay.Count > 0 && !HasDay(r, day.DayOfWeek))
		{
			return false;
		}
		return true;
	}

	private static void MonthDays(DateTime first, IcsRule r, DateTime dt, TimeSpan tod, List<DateTime> into)
	{
		int num = DateTime.DaysInMonth(first.Year, first.Month);
		if (r.ByMonthDay.Count > 0)
		{
			foreach (int item in r.ByMonthDay)
			{
				int num2 = ((item > 0) ? item : (num + item + 1));
				if (num2 >= 1 && num2 <= num)
				{
					DateTime dateTime = new DateTime(first.Year, first.Month, num2);
					if (r.ByDay.Count <= 0 || HasDay(r, dateTime.DayOfWeek))
					{
						into.Add(dateTime + tod);
					}
				}
			}
			return;
		}
		if (r.ByDay.Count > 0)
		{
			foreach (KeyValuePair<int, DayOfWeek> item2 in r.ByDay)
			{
				int num3 = (item2.Value - first.DayOfWeek + 7) % 7 + 1;
				if (item2.Key == 0)
				{
					for (int i = num3; i <= num; i += 7)
					{
						into.Add(new DateTime(first.Year, first.Month, i) + tod);
					}
				}
				else if (item2.Key > 0)
				{
					int num4 = num3 + 7 * (item2.Key - 1);
					if (num4 <= num)
					{
						into.Add(new DateTime(first.Year, first.Month, num4) + tod);
					}
				}
				else
				{
					int num5 = num3 + 7 * ((num - num3) / 7);
					int num6 = num5 + 7 * (item2.Key + 1);
					if (num6 >= 1)
					{
						into.Add(new DateTime(first.Year, first.Month, num6) + tod);
					}
				}
			}
			return;
		}
		if (dt.Day <= num)
		{
			into.Add(new DateTime(first.Year, first.Month, dt.Day) + tod);
		}
	}

	private static void YearDays(int y, IcsRule r, TimeSpan tod, List<DateTime> into)
	{
		DateTime dateTime = new DateTime(y, 1, 1);
		int num = (DateTime.IsLeapYear(y) ? 366 : 365);
		foreach (KeyValuePair<int, DayOfWeek> item in r.ByDay)
		{
			int num2 = (item.Value - dateTime.DayOfWeek + 7) % 7;
			if (item.Key == 0)
			{
				for (int i = num2; i < num; i += 7)
				{
					into.Add(dateTime.AddDays(i) + tod);
				}
				continue;
			}
			if (item.Key > 0)
			{
				int num3 = num2 + 7 * (item.Key - 1);
				if (num3 < num)
				{
					into.Add(dateTime.AddDays(num3) + tod);
				}
				continue;
			}
			int num4 = num2 + 7 * ((num - 1 - num2) / 7);
			int num5 = num4 + 7 * (item.Key + 1);
			if (num5 >= 0)
			{
				into.Add(dateTime.AddDays(num5) + tod);
			}
		}
	}

	private static void ApplySetPos(IcsRule r, List<DateTime> cands)
	{
		if (r.BySetPos.Count == 0 || cands.Count == 0)
		{
			return;
		}
		List<DateTime> list = new List<DateTime>();
		foreach (int bySetPo in r.BySetPos)
		{
			int num = ((bySetPo > 0) ? (bySetPo - 1) : (cands.Count + bySetPo));
			if (num >= 0 && num < cands.Count && !list.Contains(cands[num]))
			{
				list.Add(cands[num]);
			}
		}
		list.Sort();
		cands.Clear();
		cands.AddRange(list);
	}

	private static Dictionary<string, string> BuildIana()
	{
		Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		Action<string, string> action = delegate(string win, string ids)
		{
			string[] array = ids.Split(' ');
			foreach (string key in array)
			{
				d[key] = win;
			}
		};
		action("India Standard Time", "Asia/Kolkata Asia/Calcutta");
		action("Sri Lanka Standard Time", "Asia/Colombo");
		action("Nepal Standard Time", "Asia/Kathmandu Asia/Katmandu");
		action("Bangladesh Standard Time", "Asia/Dhaka Asia/Thimphu");
		action("Pakistan Standard Time", "Asia/Karachi");
		action("Arabian Standard Time", "Asia/Dubai Asia/Muscat");
		action("Arab Standard Time", "Asia/Riyadh Asia/Kuwait Asia/Qatar Asia/Bahrain Asia/Aden");
		action("Arabic Standard Time", "Asia/Baghdad");
		action("Iran Standard Time", "Asia/Tehran");
		action("Afghanistan Standard Time", "Asia/Kabul");
		action("West Asia Standard Time", "Asia/Tashkent Asia/Samarkand");
		action("Central Asia Standard Time", "Asia/Almaty Asia/Bishkek");
		action("Myanmar Standard Time", "Asia/Yangon Asia/Rangoon");
		action("SE Asia Standard Time", "Asia/Bangkok Asia/Ho_Chi_Minh Asia/Saigon Asia/Jakarta Asia/Phnom_Penh Asia/Vientiane");
		action("Singapore Standard Time", "Asia/Singapore Asia/Kuala_Lumpur Asia/Manila Asia/Makassar Asia/Brunei");
		action("China Standard Time", "Asia/Shanghai Asia/Hong_Kong Asia/Chongqing Asia/Macau Asia/Urumqi");
		action("Taipei Standard Time", "Asia/Taipei");
		action("Tokyo Standard Time", "Asia/Tokyo");
		action("Korea Standard Time", "Asia/Seoul");
		action("Israel Standard Time", "Asia/Jerusalem Asia/Tel_Aviv");
		action("Middle East Standard Time", "Asia/Beirut");
		action("Jordan Standard Time", "Asia/Amman");
		action("Syria Standard Time", "Asia/Damascus");
		action("Caucasus Standard Time", "Asia/Yerevan");
		action("Georgian Standard Time", "Asia/Tbilisi");
		action("Azerbaijan Standard Time", "Asia/Baku");
		action("Ekaterinburg Standard Time", "Asia/Yekaterinburg");
		action("N. Central Asia Standard Time", "Asia/Novosibirsk");
		action("North Asia Standard Time", "Asia/Krasnoyarsk");
		action("North Asia East Standard Time", "Asia/Irkutsk");
		action("Vladivostok Standard Time", "Asia/Vladivostok");
		action("Ulaanbaatar Standard Time", "Asia/Ulaanbaatar");
		action("AUS Eastern Standard Time", "Australia/Sydney Australia/Melbourne Australia/Canberra");
		action("E. Australia Standard Time", "Australia/Brisbane");
		action("Cen. Australia Standard Time", "Australia/Adelaide");
		action("AUS Central Standard Time", "Australia/Darwin");
		action("W. Australia Standard Time", "Australia/Perth");
		action("Tasmania Standard Time", "Australia/Hobart");
		action("New Zealand Standard Time", "Pacific/Auckland");
		action("Fiji Standard Time", "Pacific/Fiji");
		action("West Pacific Standard Time", "Pacific/Guam Pacific/Port_Moresby");
		action("Hawaiian Standard Time", "Pacific/Honolulu");
		action("Alaskan Standard Time", "America/Anchorage");
		action("Pacific Standard Time", "America/Los_Angeles America/Vancouver America/Tijuana");
		action("Mountain Standard Time", "America/Denver America/Edmonton America/Boise");
		action("US Mountain Standard Time", "America/Phoenix");
		action("Central Standard Time", "America/Chicago America/Winnipeg");
		action("Central Standard Time (Mexico)", "America/Mexico_City America/Monterrey");
		action("Canada Central Standard Time", "America/Regina");
		action("Central America Standard Time", "America/Guatemala America/Costa_Rica America/El_Salvador America/Managua America/Tegucigalpa");
		action("Eastern Standard Time", "America/New_York America/Toronto America/Detroit America/Montreal");
		action("SA Pacific Standard Time", "America/Bogota America/Lima America/Panama America/Guayaquil");
		action("Venezuela Standard Time", "America/Caracas");
		action("Atlantic Standard Time", "America/Halifax Atlantic/Bermuda");
		action("SA Western Standard Time", "America/Puerto_Rico America/La_Paz America/Santo_Domingo");
		action("Newfoundland Standard Time", "America/St_Johns");
		action("Pacific SA Standard Time", "America/Santiago");
		action("E. South America Standard Time", "America/Sao_Paulo");
		action("Argentina Standard Time", "America/Argentina/Buenos_Aires America/Buenos_Aires");
		action("Montevideo Standard Time", "America/Montevideo");
		action("Cuba Standard Time", "America/Havana");
		action("GMT Standard Time", "Europe/London Europe/Dublin Europe/Lisbon Atlantic/Canary");
		action("Greenwich Standard Time", "Atlantic/Reykjavik Africa/Accra Africa/Abidjan Africa/Dakar");
		action("W. Europe Standard Time", "Europe/Berlin Europe/Amsterdam Europe/Rome Europe/Stockholm Europe/Vienna Europe/Zurich Europe/Oslo Europe/Luxembourg Europe/Monaco Europe/Malta");
		action("Romance Standard Time", "Europe/Paris Europe/Brussels Europe/Madrid Europe/Copenhagen");
		action("Central European Standard Time", "Europe/Warsaw Europe/Zagreb Europe/Sarajevo Europe/Skopje");
		action("Central Europe Standard Time", "Europe/Prague Europe/Budapest Europe/Belgrade Europe/Bratislava Europe/Ljubljana");
		action("GTB Standard Time", "Europe/Athens Europe/Bucharest");
		action("FLE Standard Time", "Europe/Helsinki Europe/Kiev Europe/Kyiv Europe/Riga Europe/Sofia Europe/Tallinn Europe/Vilnius");
		action("Turkey Standard Time", "Europe/Istanbul Asia/Istanbul");
		action("Russian Standard Time", "Europe/Moscow");
		action("Belarus Standard Time", "Europe/Minsk");
		action("Egypt Standard Time", "Africa/Cairo");
		action("South Africa Standard Time", "Africa/Johannesburg Africa/Harare Africa/Maputo");
		action("W. Central Africa Standard Time", "Africa/Lagos Africa/Algiers Africa/Kinshasa Africa/Luanda");
		action("E. Africa Standard Time", "Africa/Nairobi Africa/Addis_Ababa Africa/Dar_es_Salaam Africa/Kampala");
		action("Morocco Standard Time", "Africa/Casablanca");
		action("UTC", "Etc/UTC Etc/GMT UTC GMT Etc/Universal");
		return d;
	}
}
