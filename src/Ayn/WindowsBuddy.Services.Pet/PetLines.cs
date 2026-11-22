using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsBuddy.Services.Pet;

public static class PetLines
{
	public static PetLineEntry Choose(PetHistory h, DateTime now, IEnumerable<string> groups, Random random, string name, int maxWords = 0)
	{
		ResetDay(h, now);
		foreach (string group in groups)
		{
			PetLineEntry[] array = (from l in PetContent.Lines(@group)
				where !h.DayLines.Contains(Seen(l.Text)) && (name.Length > 0 || !l.Text.Contains("{name}")) && (maxWords <= 0 || Words(l.Text.Replace("{name}", "Ayn")) <= maxWords)
				select l).ToArray();
			if (array.Length != 0)
			{
				PetLineEntry petLineEntry = array[random.Next(array.Length)];
				h.DayLines.Add(Seen(petLineEntry.Text));
				return new PetLineEntry(petLineEntry.Id, petLineEntry.Text.Replace("{name}", name));
			}
		}
		return null;
	}

	public static int Words(string text)
	{
		return (text ?? "").Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
	}

	private static string Seen(string text)
	{
		return text.Replace("{name}", "").Trim(' ', '!', '?', '.', ',').ToLowerInvariant();
	}

	public static void ResetDay(PetHistory h, DateTime now)
	{
		string text = PetStories.Key(now);
		if (h.LinesDate != text)
		{
			h.LinesDate = text;
			h.DayLines.Clear();
		}
	}

	public static string[] Groups(PetMood mood, DateTime now, string context)
	{
		List<string> list = new List<string>();
		if (context != null)
		{
			list.Add(context);
		}
		list.Add(PetContent.Mood(mood) + "/" + PetStrings.Part(now));
		foreach (PetMood item in from PetMood m in Enum.GetValues(typeof(PetMood))
			where m != mood
			select m)
		{
			list.Add(PetContent.Mood(item) + "/" + PetStrings.Part(now));
		}
		return list.ToArray();
	}

	public static bool CanSpeak(PetHistory h, DateTime now, bool quiet, bool typing)
	{
		if (!quiet && !typing)
		{
			return now - h.LastSpontaneous >= TimeSpan.FromMinutes(10.0);
		}
		return false;
	}
}
