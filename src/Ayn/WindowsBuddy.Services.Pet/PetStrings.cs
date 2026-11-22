using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsBuddy.Services.Pet;

public static class PetStrings
{
	public const string Privacy = "Ayn Pet uses only a local app category (never window titles or content), activity, battery, media, timer, and water-goal information already available on this PC to show local greetings and suggestions; this information is not sent anywhere.";

	public const string BreakTitle = "2 hours of focus";

	public const string BreakSubtitle = "Take a 5 min break?";

	public const string WaterTitle = "Time for some water?";

	public const string WaterSubtitle = "A small pause for a glass";

	public const string StartBreak = "Start break";

	public const string Later = "Later";

	public const string AddGlass = "+1 glass";

	public const string WaterQuestion = "Had some water?";

	public const string BreakQuestion = "Take a short break?";

	public const string DayQuestion = "How was your day?";

	public const string Yes = "Yes";

	public const string Start = "Start";

	public const string Good = "Good";

	public const string Okay = "Okay";

	public const string Online = "Back online";

	public const string Offline = "Offline";

	public const string ScreenshotSaved = "Screenshot saved";

	public const string ShelfSaved = "Saved to Shelf";

	public static readonly string[] Talk = new string[4] { "hi", "water?", "nice", "brb" };

	public static readonly string[] Hinglish = new string[3] { "Tea break?", "Just five more minutes?", "Full focus today" };

	private static readonly string[][] subtitles = new string[4][]
	{
		new string[10] { "Ready when you are", "Let's make today count", "One thing at a time", "A fresh start", "Room for a good day", "Ease into your day", "Your day, your pace", "A little focus goes a long way", "Start with what matters", "Take it as it comes" },
		new string[10] { "How is your day going?", "A little room to refocus", "Keep a steady pace", "Make space for a pause", "One small step next", "Back to what matters", "Plenty of day left", "Keep it manageable", "Pick up where you left off", "A good time to reset" },
		new string[10] { "Wrapping up?", "Leave some room for yourself", "A calmer end to the day", "Take stock, then take a breath", "You can leave some for tomorrow", "A little time to wind down", "Finish at your own pace", "Make room for a quiet evening", "One last thing, perhaps", "Glad to see you back" },
		new string[10] { "Don't forget to rest", "Keep a little energy for tomorrow", "A quiet moment to finish", "Take it gently", "A small pause might help", "Rest is part of the work", "Tomorrow can wait", "Give yourself a soft landing", "Keep tonight manageable", "Leave room for sleep" }
	};

	private static readonly string[][] titles = new string[4][]
	{
		new string[10] { "Good morning", "Rise and shine", "Coffee first?", "Morning, sunshine", "A fresh start", "Ready for today?", "Hello, new day", "Morning stretch?", "Easy does it", "Tea to start?" },
		new string[10] { "Good afternoon", "Lunch done?", "Halfway there", "Afternoon pause?", "Keep it steady", "One step ahead", "Nice progress", "Room to breathe", "Tea time soon", "Back to it" },
		new string[10] { "Good evening", "Wrapping up?", "Nice work today", "Evening already", "Time to unwind", "A little rest?", "Take a breath", "You did well", "Home stretch", "Tea break?" },
		new string[10] { "Working late?", "Night owl mode", "Rest soon?", "A quiet night", "Soft landing", "Tomorrow can wait", "Easy tonight", "Sleep matters", "Lights down?", "One last thing?" }
	};

	public static string Welcome(double absence)
	{
		return "Welcome back!";
	}

	public static string Routine(string part)
	{
		if (!(part == "morning"))
		{
			return "Lunch break?";
		}
		return "Good morning!";
	}

	public static string[] ContextTalk(string category, bool music, double continuous, double idle, DateTime now)
	{
		List<string> list = new List<string>();
		if (idle >= 60.0)
		{
			return new string[2] { "I'm right here", "All good?" };
		}
		if (category == "coding")
		{
			list.AddRange(new string[3] { "Found the bug?", "Did it build?", "How's it going?" });
		}
		else if (category == "video")
		{
			list.AddRange(new string[2] { "Popcorn?", "Enjoying it?" });
		}
		else if (music || category == "music")
		{
			list.AddRange(new string[2] { "Nice song!", "Good vibes" });
		}
		if (continuous >= 3600.0)
		{
			list.AddRange(new string[2] { "Take a short break?", "Tea break?" });
		}
		if (now.Hour >= 5 && now.Hour < 12)
		{
			list.AddRange(new string[2] { "Good morning", "Coffee first?" });
		}
		if (list.Count == 0)
		{
			list.Add("I'm right here");
		}
		return list.ToArray();
	}

	public static string Part(DateTime now)
	{
		if (now.Hour < 5 || now.Hour >= 12)
		{
			if (now.Hour < 12 || now.Hour >= 17)
			{
				if (now.Hour < 17 || now.Hour >= 22)
				{
					return "late";
				}
				return "evening";
			}
			return "afternoon";
		}
		return "morning";
	}

	public static string PartKey(DateTime now)
	{
		return ((now.Hour < 5) ? now.Date.AddDays(-1.0) : now.Date).ToString("yyyyMMdd") + "/" + Part(now);
	}

	public static IEnumerable<PetLine> Pool(DateTime now, bool returned)
	{
		if (returned)
		{
			yield return new PetLine("return-0", "Welcome back", "Nice to see you again");
			yield return new PetLine("return-1", "Back at it", "Pick up at your own pace");
		}
		if (now.DayOfWeek == DayOfWeek.Monday)
		{
			yield return new PetLine("monday-0", "A new week", "Start with one small thing");
			yield return new PetLine("monday-1", "Hello, Monday", "Let's keep this week manageable");
		}
		if (now.DayOfWeek == DayOfWeek.Friday)
		{
			yield return new PetLine("friday-0", "Hello, Friday", "Leave a little room for the weekend");
			yield return new PetLine("friday-1", "Nearly the weekend", "Finish at a comfortable pace");
		}
		if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
		{
			yield return new PetLine("weekend-0", "Hello, weekend", "A little more room to breathe");
			yield return new PetLine("weekend-1", "A slower kind of day", "Go at your own pace");
		}
		string part = Part(now);
		int index = part switch
		{
			"evening" => 2, 
			"afternoon" => 1, 
			"morning" => 0, 
			_ => 3, 
		};
		for (int i = 0; i < subtitles[index].Length; i++)
		{
			yield return new PetLine(part + "-" + i, titles[index][i], subtitles[index][i]);
		}
	}

	public static PetLine Choose(DateTime now, PetHistory history, bool returned)
	{
		if (history.Parts.ContainsKey(PartKey(now)))
		{
			return null;
		}
		return Pool(now, returned).FirstOrDefault((PetLine line) => !history.Lines.ContainsKey(line.Id) || history.Lines[line.Id] <= now.AddDays(-5.0));
	}

	public static void Record(DateTime now, PetHistory history, PetLine line)
	{
		history.Parts[PartKey(now)] = true;
		history.Lines[line.Id] = now;
		string[] array = (from p in history.Lines
			where p.Value < now.AddDays(-14.0)
			select p.Key).ToArray();
		foreach (string key in array)
		{
			history.Lines.Remove(key);
		}
		string[] array2 = history.Parts.Keys.Where((string k) => string.CompareOrdinal(k, now.AddDays(-14.0).ToString("yyyyMMdd")) < 0).ToArray();
		foreach (string key2 in array2)
		{
			history.Parts.Remove(key2);
		}
	}
}
