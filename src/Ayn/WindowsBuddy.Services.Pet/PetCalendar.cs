using System;
using System.Linq;

namespace WindowsBuddy.Services.Pet;

public static class PetCalendar
{
	private static readonly DateTime[] diwali = new DateTime[6]
	{
		new DateTime(2025, 10, 20),
		new DateTime(2026, 11, 8),
		new DateTime(2027, 10, 29),
		new DateTime(2028, 10, 17),
		new DateTime(2029, 11, 5),
		new DateTime(2030, 10, 26)
	};

	private static readonly DateTime[] holi = new DateTime[6]
	{
		new DateTime(2025, 3, 14),
		new DateTime(2026, 3, 4),
		new DateTime(2027, 3, 22),
		new DateTime(2028, 3, 11),
		new DateTime(2029, 3, 1),
		new DateTime(2030, 3, 20)
	};

	public static string Festival(DateTime now, string birthday = null)
	{
		DateTime d = now.Date;
		if (!string.IsNullOrEmpty(birthday) && d.ToString("MM-dd") == birthday)
		{
			return "birthday";
		}
		if (diwali.Any((DateTime x) => Math.Abs((d - x).TotalDays) <= 2.0))
		{
			return "diwali";
		}
		if (holi.Any((DateTime x) => (x - d).TotalDays >= 0.0 && (x - d).TotalDays <= 1.0))
		{
			return "holi";
		}
		if (d.Month == 12 && d.Day >= 24 && d.Day <= 26)
		{
			return "christmas";
		}
		if ((d.Month == 1 && d.Day == 1) || (d.Month == 12 && d.Day == 31 && now.Hour >= 18))
		{
			return "newyear";
		}
		return null;
	}

	public static string Part(DateTime now)
	{
		return PetStrings.Part(now);
	}

	public static bool Night(DateTime now)
	{
		if (now.Hour < 20)
		{
			return now.Hour < 5;
		}
		return true;
	}

	public static PetMood DayMood(DateTime date, string festival)
	{
		if (festival != null)
		{
			return PetMood.Excited;
		}
		switch (date.DayOfWeek)
		{
		case DayOfWeek.Monday:
			return PetMood.Sleepy;
		case DayOfWeek.Friday:
			return PetMood.Excited;
		case DayOfWeek.Sunday:
		case DayOfWeek.Saturday:
			return PetMood.Calm;
		default:
		{
			Random random = new Random(PetDay.Seed(date) ^ 0x5F3);
			return (new PetMood[5]
			{
				PetMood.Happy,
				PetMood.Calm,
				PetMood.Happy,
				PetMood.Calm,
				PetMood.Bored
			})[random.Next(5)];
		}
		}
	}

	public static string WeatherBucket(int code, double celsius)
	{
		if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82) || code >= 95)
		{
			return "rain";
		}
		if (celsius >= 32.0)
		{
			return "hot";
		}
		if (celsius <= 12.0)
		{
			return "cold";
		}
		return null;
	}
}
