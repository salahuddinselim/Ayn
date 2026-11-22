using System;

namespace WindowsBuddy.Services.Pet;

public sealed class PetOptions
{
	public double Affection { get; set; }

	public DateTime AffectionSaved { get; set; }

	public bool Enabled { get; set; }

	public bool Greetings { get; set; }

	public bool FollowCursor { get; set; }

	public bool Suggestions { get; set; }

	public bool ShowName { get; set; }

	public bool Walks { get; set; }

	public bool Talks { get; set; }

	public string ActivityLevel { get; set; }

	public bool DanceToMusic { get; set; }

	public bool Hinglish { get; set; }

	public bool CheckIns { get; set; }

	public bool QuietHours { get; set; }

	public int QuietStart { get; set; }

	public int QuietEnd { get; set; }

	public bool FirstMeetDone { get; set; }

	public bool IntroV1 { get; set; }

	public string Birthday { get; set; }

	public PetOptions()
	{
		Affection = 0.5;
		Greetings = true;
		FollowCursor = true;
		Walks = true;
		Suggestions = true;
		ShowName = true;
		DanceToMusic = true;
		Talks = true;
		ActivityLevel = "Normal";
		CheckIns = true;
		QuietStart = 23;
		QuietEnd = 7;
	}
}
