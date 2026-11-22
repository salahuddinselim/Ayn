using System;

namespace WindowsBuddy.Services.Pet;

public static class PetStories
{
	public static string Key(DateTime now)
	{
		return now.ToString("yyyy-MM-dd");
	}

	public static bool ObserveActiveDay(PetHistory h, DateTime now)
	{
		string text = Key(now);
		if (h.LastActiveDay == text)
		{
			return false;
		}
		h.LastActiveDay = text;
		h.ActiveDays++;
		if (h.RocketDay < 7)
		{
			h.RocketDay++;
		}
		h.PlantDay++;
		if (h.PlantDay > 37)
		{
			h.PlantDay = 1;
			h.Plants++;
		}
		return true;
	}

	public static bool LaunchDue(PetHistory h, DateTime now)
	{
		if (h.RocketDay >= 7)
		{
			return h.RocketLaunched != Key(now);
		}
		return false;
	}

	public static void Launch(PetHistory h, DateTime now)
	{
		h.RocketLaunched = Key(now);
		h.Rockets++;
		h.RocketDay = 0;
	}

	public static int PlantStage(int day)
	{
		if (day > 0)
		{
			if (day > 7)
			{
				if (day > 17)
				{
					if (day > 27)
					{
						return 4;
					}
					return 3;
				}
				return 2;
			}
			return 1;
		}
		return 0;
	}

	public static bool WaterDue(PetHistory h, DateTime now)
	{
		return h.PlantWatered != Key(now);
	}

	public static void Water(PetHistory h, DateTime now)
	{
		h.PlantWatered = Key(now);
	}

	public static bool TryCatchStar(PetHistory h, DateTime now, Random random)
	{
		if (!PetCalendar.Night(now) || h.StarCaught == Key(now) || random.NextDouble() >= 0.35)
		{
			return false;
		}
		h.StarCaught = Key(now);
		h.StarJar++;
		if (h.StarJar >= 21)
		{
			h.StarJar = 0;
			h.Jars++;
		}
		return true;
	}
}
