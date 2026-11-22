using System;
using System.Collections.Generic;

namespace WindowsBuddy.Services.Pet;

public sealed class PetHistory
{
	public Dictionary<string, DateTime> Lines { get; set; }

	public Dictionary<string, bool> Parts { get; set; }

	public DateTime LastTalk { get; set; }

	public Dictionary<string, DateTime> TalkLines { get; set; }

	public DateTime LastLaunch { get; set; }

	public string SuggestionDate { get; set; }

	public int SuggestionsToday { get; set; }

	public Dictionary<string, DateTime> Scenes { get; set; }

	public Dictionary<string, string> Answers { get; set; }

	public Dictionary<string, DateTime> BigScenes { get; set; }

	public Dictionary<string, int> ActivityCounts { get; set; }

	public string ActivityDate { get; set; }

	public string LastActivity { get; set; }

	public int BigToday { get; set; }

	public List<string> Drawn { get; set; }

	public List<string> DayLines { get; set; }

	public string LinesDate { get; set; }

	public DateTime LastSpontaneous { get; set; }

	public string Mood { get; set; }

	public string MoodDate { get; set; }

	public string LastActiveDay { get; set; }

	public int ActiveDays { get; set; }

	public int RocketDay { get; set; }

	public int Rockets { get; set; }

	public string RocketLaunched { get; set; }

	public int PlantDay { get; set; }

	public int Plants { get; set; }

	public string PlantWatered { get; set; }

	public int StarJar { get; set; }

	public int Jars { get; set; }

	public string StarCaught { get; set; }

	public string BreakAsked { get; set; }

	public PetHistory()
	{
		Lines = new Dictionary<string, DateTime>();
		TalkLines = new Dictionary<string, DateTime>();
		Parts = new Dictionary<string, bool>();
		Scenes = new Dictionary<string, DateTime>();
		Answers = new Dictionary<string, string>();
		BigScenes = new Dictionary<string, DateTime>();
		ActivityCounts = new Dictionary<string, int>();
		Drawn = new List<string>();
		DayLines = new List<string>();
	}

	public PetHistory Normalize()
	{
		if (Lines == null)
		{
			Lines = new Dictionary<string, DateTime>();
		}
		if (TalkLines == null)
		{
			TalkLines = new Dictionary<string, DateTime>();
		}
		if (Parts == null)
		{
			Parts = new Dictionary<string, bool>();
		}
		if (Scenes == null)
		{
			Scenes = new Dictionary<string, DateTime>();
		}
		if (Answers == null)
		{
			Answers = new Dictionary<string, string>();
		}
		if (BigScenes == null)
		{
			BigScenes = new Dictionary<string, DateTime>();
		}
		if (ActivityCounts == null)
		{
			ActivityCounts = new Dictionary<string, int>();
		}
		if (Drawn == null)
		{
			Drawn = new List<string>();
		}
		if (DayLines == null)
		{
			DayLines = new List<string>();
		}
		return this;
	}
}
