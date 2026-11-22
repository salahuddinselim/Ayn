using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsBuddy.Services.Pet;

public sealed class PetDay
{
	public const double ActivityMin = 8.0;

	public const double ActivityMax = 15.0;

	public const double WalkMin = 4.0;

	public const double WalkMax = 8.0;

	public const double MicroMin = 4.0;

	public const double MicroMax = 8.0;

	public const double GapMin = 35.0;

	public const double GapMax = 110.0;

	public const double ArrivalAndExit = 6.0;

	public const double StageMax = 45.0;

	public const double AftermathMin = 8.0;

	public const double AftermathMax = 14.0;

	public const int BigPerDay = 2;

	public const int BigMax = 4;

	public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10.0);

	public static readonly TimeSpan WaterTeaCooldown = TimeSpan.FromMinutes(45.0);

	private readonly PetHistory h;

	private Random random;

	private string day;

	private DateTime dayStart;

	private DateTime nextActivityAt = DateTime.MinValue;

	private DateTime earliestNext = DateTime.MinValue;

	private DateTime lastTea = DateTime.MinValue;

	private PetActivity current;

	private PetSegmentKind? last;

	private bool musicNudge;

	private readonly Dictionary<string, DateTime> lastRan = new Dictionary<string, DateTime>();

	private readonly List<DateTime> bigAt = new List<DateTime>();

	public PetMood BaseMood { get; private set; }

	public string Festival { get; private set; }

	public PetActivity Current => current;

	public Random Random => random;

	public PetDay(PetHistory history)
	{
		h = history;
	}

	public void EndActivity()
	{
		musicNudge = true;
	}

	public static int Seed(DateTime date)
	{
		return date.Year * 7919 + date.Month * 104729 + date.Day * 1299709;
	}

	public PetMood Mood(PetContext c)
	{
		EnsureDay(c);
		int hour = c.Now.Hour;
		if (hour >= 23 || hour < 5)
		{
			return PetMood.Sleepy;
		}
		if (c.Affection < 0.25)
		{
			return PetMood.Bored;
		}
		if (c.Music && BaseMood != PetMood.Sleepy)
		{
			return PetMood.Excited;
		}
		return BaseMood;
	}

	private void EnsureDay(PetContext c)
	{
		string text = PetStories.Key(c.Now);
		if (!(text == day))
		{
			day = text;
			dayStart = c.Now;
			random = new Random(Seed(c.Now.Date));
			current = null;
			last = null;
			nextActivityAt = c.Now.AddSeconds(8 + random.Next(20));
			Festival = PetCalendar.Festival(c.Now, c.Birthday);
			BaseMood = PetCalendar.DayMood(c.Now.Date, Festival);
			h.Mood = BaseMood.ToString();
			h.MoodDate = text;
			if (h.ActivityDate != text)
			{
				h.ActivityDate = text;
				h.ActivityCounts.Clear();
				h.LastActivity = null;
				h.BigToday = 0;
				h.Drawn.Clear();
			}
			PetStories.ObserveActiveDay(h, c.Now);
			bigAt.Clear();
			bigAt.Add(c.Now.AddMinutes(60 + random.Next(90)));
			bigAt.Add(c.Now.AddMinutes(240 + random.Next(90)));
		}
	}

	private double Between(double a, double b)
	{
		return a + random.NextDouble() * (b - a);
	}

	public PetSegment Next(PetContext c)
	{
		EnsureDay(c);
		PetMood mood = Mood(c);
		bool flag = last == PetSegmentKind.Walk || last == PetSegmentKind.Micro || !last.HasValue;
		if (PetStories.LaunchDue(h, c.Now) && c.Now - dayStart >= TimeSpan.FromMinutes(30.0) && flag)
		{
			PetStories.Launch(h, c.Now);
			last = PetSegmentKind.Story;
			current = null;
			nextActivityAt = c.Now.AddSeconds(24.0 + Between(35.0, 110.0));
			PetSegment petSegment = new PetSegment();
			petSegment.Kind = PetSegmentKind.Story;
			petSegment.Name = "rocket-launch";
			petSegment.Seconds = 24.0;
			petSegment.SceneSeconds = 24.0;
			return petSegment;
		}
		PetSegmentKind? petSegmentKind = last;
		if ((petSegmentKind.GetValueOrDefault() == PetSegmentKind.Activity && petSegmentKind.HasValue) || last == PetSegmentKind.Big || last == PetSegmentKind.Story)
		{
			last = PetSegmentKind.Walk;
			current = null;
			return Walk();
		}
		if (flag && c.Now >= earliestNext && h.BigToday < Math.Min(2, 4) && bigAt.Count > 0 && c.Now >= bigAt[0])
		{
			PetSegment petSegment2 = Big(c);
			bigAt.RemoveAt(0);
			if (petSegment2 != null)
			{
				last = PetSegmentKind.Big;
				current = null;
				nextActivityAt = c.Now.AddSeconds(petSegment2.Seconds + Between(35.0, 110.0));
				earliestNext = c.Now.AddSeconds(petSegment2.Seconds + 30.0);
				return petSegment2;
			}
		}
		if (c.Music && musicNudge && flag && c.Now >= earliestNext)
		{
			musicNudge = false;
			PetActivity petActivity = PetContent.Activity("headphones");
			if (petActivity != null && Eligible(petActivity, c, mood))
			{
				return Start(petActivity, c);
			}
		}
		if (c.Now >= nextActivityAt)
		{
			PetActivity petActivity2 = Choose(c, mood);
			if (petActivity2 != null)
			{
				return Start(petActivity2, c);
			}
			nextActivityAt = c.Now.AddSeconds(20.0);
		}
		if (last == PetSegmentKind.Walk)
		{
			last = PetSegmentKind.Micro;
			return Micro(c, mood);
		}
		last = PetSegmentKind.Walk;
		return Walk();
	}

	private PetSegment Start(PetActivity a, PetContext c)
	{
		current = a;
		last = PetSegmentKind.Activity;
		lastRan[a.Name] = c.Now;
		if (a.Name == "chai")
		{
			lastTea = c.Now;
		}
		h.ActivityCounts.TryGetValue(a.Name, out var value);
		h.ActivityCounts[a.Name] = value + 1;
		h.LastActivity = a.Name;
		if (a.Requires == "plant")
		{
			PetStories.Water(h, c.Now);
		}
		double num = Between(8.0, 15.0);
		nextActivityAt = c.Now.AddSeconds(num + 6.0 + Between(35.0, 110.0));
		earliestNext = c.Now.AddSeconds(num + 6.0 + 30.0);
		PetSegment petSegment = new PetSegment();
		petSegment.Kind = PetSegmentKind.Activity;
		petSegment.Name = a.Name;
		petSegment.Activity = a.Name;
		petSegment.Seconds = num;
		return petSegment;
	}

	private PetSegment Walk()
	{
		PetSegment petSegment = new PetSegment();
		petSegment.Kind = PetSegmentKind.Walk;
		petSegment.Name = "walk";
		petSegment.Seconds = Between(4.0, 8.0);
		return petSegment;
	}

	private PetSegment Micro(PetContext c, PetMood mood)
	{
		List<string> list = new List<string>(PetContent.Micros);
		list.Remove("page");
		list.Remove("sip");
		if (mood == PetMood.Sleepy)
		{
			list.AddRange(new string[3] { "yawn", "rub-eyes", "stretch" });
		}
		if (mood == PetMood.Excited)
		{
			list.AddRange(new string[3] { "hop", "cheer", "wiggle" });
		}
		if (mood == PetMood.Bored)
		{
			list.AddRange(new string[3] { "sigh", "tap", "look-around" });
		}
		if (c.Music)
		{
			list.AddRange(new string[4] { "nod", "hum", "wiggle", "nod" });
		}
		if (c.Category == "gaming")
		{
			list.AddRange(new string[3] { "cheer", "cheer", "clap" });
		}
		int num = 1 + random.Next(2);
		List<string> list2 = new List<string>();
		if (LateWork(c))
		{
			list2.Add("yawn");
		}
		while (list2.Count < num)
		{
			string item = list[random.Next(list.Count)];
			if (!list2.Contains(item))
			{
				list2.Add(item);
			}
		}
		PetSegment petSegment = new PetSegment();
		petSegment.Kind = PetSegmentKind.Micro;
		petSegment.Name = string.Join("+", list2);
		petSegment.Micros = list2.ToArray();
		petSegment.Activity = null;
		petSegment.Seconds = Between(4.0, 8.0);
		return petSegment;
	}

	public static bool LateWork(PetContext c)
	{
		int hour = c.Now.Hour;
		if (hour >= 23 || hour < 4)
		{
			if (!(c.Category == "coding") && !(c.Category == "office"))
			{
				return c.Typing;
			}
			return true;
		}
		return false;
	}

	private PetSegment Big(PetContext c)
	{
		DateTime value;
		List<PetBigScene> fresh = PetContent.BigScenes.Where((PetBigScene b) => !h.BigScenes.TryGetValue(b.Name, out value) || c.Now - value >= TimeSpan.FromDays(7.0)).ToList();
		if (c.Weather != "rain")
		{
			fresh.RemoveAll((PetBigScene b) => b.Name == "rain" && fresh.Count > 1);
		}
		fresh.RemoveAll((PetBigScene b) => b.Name == "tea" && c.Now - lastTea < WaterTeaCooldown);
		if (fresh.Count == 0)
		{
			return null;
		}
		PetBigScene petBigScene = fresh[random.Next(fresh.Count)];
		string outcome = petBigScene.Outcomes[random.Next(petBigScene.Outcomes.Length)];
		h.BigScenes[petBigScene.Name] = c.Now;
		h.BigToday++;
		if (petBigScene.Name == "tea")
		{
			lastTea = c.Now;
		}
		string[] array = (from p in h.BigScenes
			where c.Now - p.Value > TimeSpan.FromDays(30.0)
			select p.Key).ToArray();
		foreach (string key in array)
		{
			h.BigScenes.Remove(key);
		}
		string text = null;
		if (petBigScene.Name == "chalk")
		{
			string[] array2 = (from d in PetContent.DrawingsFor(c.Now.Hour, c.Music, c.Weather, Festival, Mood(c), c.Continuous >= 5400.0)
				where !h.Drawn.Contains(d)
				select d).ToArray();
			text = ((array2.Length == 0) ? PetContent.Drawings[random.Next(PetContent.Drawings.Length)].Name : array2[random.Next(array2.Length)]);
			h.Drawn.Add(text);
		}
		double num = 16 + random.Next(5);
		PetSegment petSegment = new PetSegment();
		petSegment.Kind = PetSegmentKind.Big;
		petSegment.Name = petBigScene.Name;
		petSegment.Outcome = outcome;
		petSegment.Aftermath = petBigScene.Aftermath;
		petSegment.Drawing = text;
		petSegment.SceneSeconds = num;
		petSegment.Seconds = num + Between(8.0, 14.0);
		return petSegment;
	}

	private bool Eligible(PetActivity a, PetContext c, PetMood mood)
	{
		int hour = c.Now.Hour;
		DayOfWeek dayOfWeek = c.Now.DayOfWeek;
		TimeSpan timeSpan = ((a.Name == "chai" || a.Name == "gardening") ? WaterTeaCooldown : Cooldown);
		if (lastRan.TryGetValue(a.Name, out var value) && c.Now - value < timeSpan)
		{
			return false;
		}
		if (a.Name == "chai" && c.Now - lastTea < WaterTeaCooldown)
		{
			return false;
		}
		switch (a.Requires)
		{
		case "music":
			return c.Music;
		case "video":
			return c.Category == "video";
		case "night":
			return PetCalendar.Night(c.Now);
		case "sleepy":
			if (mood != PetMood.Sleepy && (hour < 13 || hour >= 16) && dayOfWeek != DayOfWeek.Saturday)
			{
				return dayOfWeek == DayOfWeek.Sunday;
			}
			return true;
		case "plant":
			return PetStories.WaterDue(h, c.Now);
		default:
			return true;
		}
	}

	private PetActivity Choose(PetContext c, PetMood mood)
	{
		int hour = c.Now.Hour;
		DayOfWeek dow = c.Now.DayOfWeek;
		List<PetActivity> list = PetContent.Activities.Where((PetActivity a) => Eligible(a, c, mood)).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		bool working = c.Category == "coding" || c.Category == "office" || c.Continuous >= 2700.0;
		double[] array = list.Select(delegate(PetActivity a)
		{
			double num2 = 1.0;
			num2 *= (lastRan.TryGetValue(a.Name, out var value) ? Math.Min(3.0, 1.0 + (c.Now - value).TotalMinutes / 20.0) : 3.0);
			if (working && (a.Name == "laptop" || a.Name == "notebook"))
			{
				num2 *= 6.0;
			}
			if (c.Category == "video" && a.Name == "popcorn")
			{
				num2 *= 8.0;
			}
			if (c.Music && a.Name == "headphones")
			{
				num2 *= 10.0;
			}
			if (hour >= 5 && hour < 11 && (a.Name == "gardening" || a.Name == "chai"))
			{
				num2 *= 2.0;
			}
			if (hour >= 23 || hour < 5)
			{
				if (a.Name == "napping" || a.Name == "stargazing")
				{
					num2 *= 4.0;
				}
				else if (a.Name == "tinkering" || a.Name == "painting" || a.Name == "yoga")
				{
					num2 *= 0.4;
				}
			}
			if (c.Charging && (a.Name == "chai" || a.Name == "reading"))
			{
				num2 *= 1.5;
			}
			if (dow == DayOfWeek.Monday && (a.Name == "napping" || a.Name == "chai"))
			{
				num2 *= 3.0;
			}
			if (dow == DayOfWeek.Friday && (a.Name == "tinkering" || a.Name == "headphones"))
			{
				num2 *= 2.0;
			}
			if ((dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday) && (a.Name == "reading" || a.Name == "fishing" || a.Name == "napping"))
			{
				num2 *= 2.0;
			}
			if (hour >= 17 && hour < 21 && a.Name == "chai")
			{
				num2 *= 3.0;
			}
			if (PetCalendar.Night(c.Now) && a.Name == "stargazing")
			{
				num2 *= 3.0;
			}
			if (mood == PetMood.Sleepy && (a.Name == "napping" || a.Name == "chai"))
			{
				num2 *= 2.0;
			}
			if (mood == PetMood.Excited && (a.Name == "tinkering" || a.Name == "fishing"))
			{
				num2 *= 1.5;
			}
			if (mood == PetMood.Bored && a.Name == "notebook")
			{
				num2 *= 2.0;
			}
			if (a.Name == "gardening")
			{
				num2 *= 2.0;
			}
			return num2;
		}).ToArray();
		double num = random.NextDouble() * array.Sum();
		for (int i = 0; i < list.Count; i++)
		{
			num -= array[i];
			if (num <= 0.0)
			{
				return list[i];
			}
		}
		return list[list.Count - 1];
	}
}
