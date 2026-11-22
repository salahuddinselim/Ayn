using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace WindowsBuddy.Services.Pet;

public static class PetContent
{
	public static readonly PetActivity[] Activities = new PetActivity[16]
	{
		new PetActivity("reading", "book", 1.0, -70.0, 70.0, 6.0),
		new PetActivity("laptop", "laptop", 1.0, -55.0, 55.0, 4.0),
		new PetActivity("chai", "chai", 1.0, -15.0, 95.0, -3.0),
		new PetActivity("headphones", "headphones", 0.0, -15.0, 15.0, 0.0, "music"),
		new PetActivity("fishing", "rod", 1.0, -15.0, 120.0, -4.0),
		new PetActivity("stargazing", "telescope", 0.0, -15.0, 60.0, -9.0, "night"),
		new PetActivity("napping", "pillow", 1.0, -15.0, 15.0, 0.0, "sleepy"),
		new PetActivity("tinkering", "gadget", 1.0, -60.0, 85.0, 7.0),
		new PetActivity("gardening", "can", 0.0, -15.0, 100.0, 5.0, "plant"),
		new PetActivity("notebook", "notebook", 1.0, -15.0, 80.0, 8.0),
		new PetActivity("popcorn", "popcorn", 1.0, -15.0, 90.0, 0.0, "video"),
		new PetActivity("painting", "easel", 0.0, -15.0, 110.0, 6.0),
		new PetActivity("puzzle", "cube", 1.0, -75.0, 75.0, 8.0),
		new PetActivity("knitting", "yarn", 1.0, -60.0, 60.0, 5.0),
		new PetActivity("yoga", "mat", 0.0, -150.0, 150.0, 0.0),
		new PetActivity("origami", "paper", 1.0, -65.0, 65.0, 7.0)
	};

	public static readonly string[] Micros = new string[34]
	{
		"stretch", "yawn", "look-around", "look-left", "look-right", "time", "battery", "scratch", "page", "sip",
		"sneeze", "hop", "tap", "nod", "shrug", "tilt", "peek", "wipe-brow", "double-blink", "hum",
		"dust-off", "salute", "thumbs-up", "hiccup", "kick", "whistle", "look-up", "sigh", "clap", "wiggle",
		"wave-small", "cheer", "polish", "rub-eyes"
	};

	public static readonly PetBigScene[] BigScenes = new PetBigScene[14]
	{
		new PetBigScene("tea", "table", "perfect", "spill"),
		new PetBigScene("plane", "plane", "far", "nosedive", "stuck"),
		new PetBigScene("rain", "umbrella", "shelter", "inside-out"),
		new PetBigScene("chalk", "board", "erase", "crumple"),
		new PetBigScene("ball", "ball", "catch", "bonk"),
		new PetBigScene("catch", "bucket", "fish", "shoe", "nothing"),
		new PetBigScene("kite", "kite", "soar", "tangle"),
		new PetBigScene("cricket", "bat", "six", "bowled"),
		new PetBigScene("guitar", "guitar", "tune", "snap"),
		new PetBigScene("bubbles", "wand", "big", "pop"),
		new PetBigScene("magic", "hat", "star", "shrug"),
		new PetBigScene("yoyo", "yoyo", "tricks", "tangle"),
		new PetBigScene("hoop", "bin", "in", "miss"),
		new PetBigScene("dominoes", "dominoes", "all", "halfway")
	};

	public static readonly PetDrawing[] Drawings = new PetDrawing[17]
	{
		new PetDrawing("sun").Circle(0.5, 0.5, 0.2).Line(0.5, 0.05, 0.5, 0.2).Line(0.5, 0.8, 0.5, 0.95)
			.Line(0.05, 0.5, 0.2, 0.5)
			.Line(0.8, 0.5, 0.95, 0.5)
			.Line(0.18, 0.18, 0.28, 0.28)
			.Line(0.72, 0.72, 0.82, 0.82)
			.Line(0.82, 0.18, 0.72, 0.28)
			.Line(0.28, 0.72, 0.18, 0.82),
		new PetDrawing("moon").Arc(0.5, 0.5, 0.35, 0.4, 70.0, 290.0).Arc(0.62, 0.5, 0.25, 0.3, 95.0, 265.0),
		new PetDrawing("heart").Line(0.5, 0.9, 0.12, 0.45, 0.15, 0.2, 0.32, 0.1, 0.5, 0.28, 0.68, 0.1, 0.85, 0.2, 0.88, 0.45, 0.5, 0.9),
		new PetDrawing("star").Line(0.5, 0.05, 0.62, 0.38, 0.97, 0.38, 0.69, 0.6, 0.8, 0.95, 0.5, 0.74, 0.2, 0.95, 0.31, 0.6, 0.03, 0.38, 0.38, 0.38, 0.5, 0.05),
		new PetDrawing("cat").Circle(0.5, 0.58, 0.3).Line(0.25, 0.4, 0.22, 0.08, 0.42, 0.3).Line(0.58, 0.3, 0.78, 0.08, 0.75, 0.4)
			.Line(0.4, 0.55, 0.4, 0.6)
			.Line(0.6, 0.55, 0.6, 0.6)
			.Line(0.45, 0.72, 0.5, 0.76, 0.55, 0.72),
		new PetDrawing("coffee").Line(0.2, 0.35, 0.25, 0.9, 0.7, 0.9, 0.75, 0.35, 0.2, 0.35).Arc(0.78, 0.6, 0.14, 0.14, -90.0, 90.0).Line(0.35, 0.25, 0.38, 0.05)
			.Line(0.52, 0.25, 0.55, 0.05),
		new PetDrawing("flower").Circle(0.5, 0.35, 0.1).Circle(0.5, 0.13, 0.1).Circle(0.5, 0.57, 0.1)
			.Circle(0.28, 0.35, 0.1)
			.Circle(0.72, 0.35, 0.1)
			.Line(0.5, 0.6, 0.5, 0.95)
			.Line(0.5, 0.8, 0.7, 0.7),
		new PetDrawing("rocket").Line(0.5, 0.05, 0.68, 0.35, 0.68, 0.75, 0.32, 0.75, 0.32, 0.35, 0.5, 0.05).Circle(0.5, 0.4, 0.08).Line(0.32, 0.6, 0.15, 0.85, 0.32, 0.75)
			.Line(0.68, 0.6, 0.85, 0.85, 0.68, 0.75)
			.Line(0.42, 0.78, 0.5, 0.97, 0.58, 0.78),
		new PetDrawing("cloud").Arc(0.3, 0.6, 0.18, 0.18, 90.0, 270.0).Arc(0.5, 0.42, 0.22, 0.22, 180.0, 360.0).Arc(0.72, 0.6, 0.18, 0.18, -90.0, 90.0)
			.Line(0.3, 0.78, 0.72, 0.78),
		new PetDrawing("note").Line(0.35, 0.75, 0.35, 0.1, 0.8, 0.02, 0.8, 0.65).Circle(0.25, 0.78, 0.1).Circle(0.7, 0.68, 0.1)
			.Line(0.35, 0.25, 0.8, 0.17),
		new PetDrawing("battery").Line(0.1, 0.3, 0.85, 0.3, 0.85, 0.75, 0.1, 0.75, 0.1, 0.3).Line(0.85, 0.42, 0.95, 0.42, 0.95, 0.62, 0.85, 0.62).Line(0.2, 0.4, 0.2, 0.65)
			.Line(0.35, 0.4, 0.35, 0.65)
			.Line(0.5, 0.4, 0.5, 0.65),
		new PetDrawing("trophy").Line(0.28, 0.1, 0.72, 0.1, 0.68, 0.45, 0.5, 0.58, 0.32, 0.45, 0.28, 0.1).Arc(0.25, 0.25, 0.12, 0.12, 90.0, 270.0).Arc(0.75, 0.25, 0.12, 0.12, -90.0, 90.0)
			.Line(0.5, 0.58, 0.5, 0.78)
			.Line(0.3, 0.92, 0.7, 0.92, 0.62, 0.78, 0.38, 0.78, 0.3, 0.92),
		new PetDrawing("smiley").Circle(0.5, 0.5, 0.42).Line(0.36, 0.38, 0.36, 0.44).Line(0.64, 0.38, 0.64, 0.44)
			.Arc(0.5, 0.55, 0.2, 0.16, 20.0, 160.0),
		new PetDrawing("umbrella").Arc(0.5, 0.5, 0.42, 0.38, 180.0, 360.0).Line(0.08, 0.5, 0.92, 0.5).Line(0.5, 0.12, 0.5, 0.85)
			.Arc(0.58, 0.85, 0.08, 0.08, 0.0, 180.0),
		new PetDrawing("house").Line(0.15, 0.5, 0.5, 0.12, 0.85, 0.5).Line(0.22, 0.45, 0.22, 0.92, 0.78, 0.92, 0.78, 0.45).Line(0.42, 0.92, 0.42, 0.65, 0.58, 0.65, 0.58, 0.92),
		new PetDrawing("tree").Circle(0.5, 0.35, 0.28).Line(0.45, 0.6, 0.45, 0.95).Line(0.55, 0.6, 0.55, 0.95)
			.Line(0.3, 0.95, 0.7, 0.95),
		new PetDrawing("fish").Arc(0.45, 0.5, 0.3, 0.22, 0.0, 360.0, 20).Line(0.75, 0.5, 0.95, 0.3, 0.95, 0.7, 0.75, 0.5).Circle(0.3, 0.45, 0.04)
	};

	public static readonly Rect BoardRect = new Rect(150.0, 2.0, 68.0, 34.0);

	public static readonly Rect DrawingRect = new Rect(169.0, 5.0, 30.0, 28.0);

	public static readonly string[] Parts = new string[4] { "morning", "afternoon", "evening", "late" };

	private static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>
	{
		{
			"greet/morning",
			new string[8] { "Good morning, {name}!", "Morning, {name}!", "Rise and shine, {name}", "Hello {name}, new day!", "Ready for today, {name}?", "Coffee first, {name}?", "Morning {name}, let's go", "Good morning! Fresh start" }
		},
		{
			"greet/afternoon",
			new string[8] { "Good afternoon, {name}!", "Hey {name}, had lunch?", "Afternoon, {name}!", "Hi {name}, halfway there", "Hello again, {name}", "Nice to see you, {name}", "Good afternoon! Steady pace", "Hey {name}, how's it going?" }
		},
		{
			"greet/evening",
			new string[8] { "Good evening, {name}!", "Evening, {name}!", "Hey {name}, wrapping up?", "Hi {name}, nice work today", "Good evening! Tea time", "Hello {name}, almost done?", "Evening vibes, {name}", "Cosy evening, {name}?" }
		},
		{
			"greet/late",
			new string[8] { "Still up, {name}?", "Night owl {name}!", "Hey {name}, it's late", "Hi {name}, rest soon?", "Late night, {name}?", "Hello {name}, quiet night", "Good night soon, {name}", "Long day, {name}?" }
		},
		{
			"happy/morning",
			new string[8] { "Great morning!", "Today feels good!", "Feeling fresh!", "Let's do this!", "Sunshine mode", "Good vibes only", "Ready, set, go!", "Smile on!" }
		},
		{
			"happy/afternoon",
			new string[8] { "Going great!", "Nice progress!", "Looking good so far", "Halfway, yay!", "Doing well!", "Keep it up!", "Happy hours", "All good here" }
		},
		{
			"happy/evening",
			new string[8] { "Good day today!", "Nice work!", "What a lovely day", "Evening glow", "Proud of today", "Yay, evening!", "Happy wrap-up", "What a day!" }
		},
		{
			"happy/late",
			new string[8] { "Peaceful night", "Stars are out!", "Night is nice", "Cosy time", "Quiet and happy", "Good night vibes", "The moon says hi", "Sweet night" }
		},
		{
			"calm/morning",
			new string[8] { "Slow and steady", "Easy morning", "No hurry", "One thing first", "Deep breath", "Gentle start", "Soft morning", "Take your time" }
		},
		{
			"calm/afternoon",
			new string[8] { "Steady pace", "All calm here", "Everything's fine", "Easy does it", "Nice and quiet", "Smooth going", "Keep it simple", "Calm afternoon" }
		},
		{
			"calm/evening",
			new string[8] { "Winding down", "Slow evening", "A little rest", "Calm and cosy", "Easy evening", "Breathe out", "Quiet evening", "Peace mode" }
		},
		{
			"calm/late",
			new string[8] { "Shh, quiet time", "Soft night", "Night calm", "Slow night", "Restful mode", "Nice and slow", "Calm night", "Quiet hours" }
		},
		{
			"sleepy/morning",
			new string[8] { "Five more minutes?", "Still waking up", "Coffee, please", "Yawn... morning", "Sleepy start", "Eyes half open", "I need tea", "Slow wake-up" }
		},
		{
			"sleepy/afternoon",
			new string[8] { "Post-lunch nap?", "Getting a bit sleepy", "Sleepy afternoon", "Yawn...", "Need a stretch", "Eyes feel heavy", "Tea time?", "Power nap?" }
		},
		{
			"sleepy/evening",
			new string[8] { "Getting sleepy", "Long day, yawn", "I'm tired", "Cosy and sleepy", "Bed soon?", "Sleepy evening", "Eyes closing", "Almost nap time" }
		},
		{
			"sleepy/late",
			new string[8] { "So sleepy...", "Can't keep my eyes open", "Bedtime?", "Yawn... it's late", "Sleep soon?", "Good night?", "My pillow is calling", "Zzz soon" }
		},
		{
			"bored/morning",
			new string[8] { "Shall we do something?", "Hmm, quiet", "Anything fun?", "A little bored", "Play later?", "What's next?", "Hmm...", "I need a game" }
		},
		{
			"bored/afternoon",
			new string[8] { "Getting a bit bored", "Hmm, quiet day", "Play with me?", "Anything new?", "What shall we do?", "Bored, hehe", "Poke me?", "Tap tap?" }
		},
		{
			"bored/evening",
			new string[8] { "Quiet evening...", "Something new?", "Slightly bored", "Fun time?", "Pet me?", "Hmm, what now", "Game later?", "Hello? Hehe" }
		},
		{
			"bored/late",
			new string[8] { "So quiet...", "Anyone there?", "Bored at night", "Hmm, stargazing?", "Quiet night...", "Late and bored", "Hi, moon", "A bedtime story?" }
		},
		{
			"excited/morning",
			new string[8] { "Big day!", "Let's go!", "Full energy!", "Energy on!", "Woohoo, morning!", "Can't wait!", "Let's get started!", "Here we go!" }
		},
		{
			"excited/afternoon",
			new string[8] { "Woohoo!", "How exciting!", "Super day!", "Let's go!", "On fire today!", "So exciting!", "High five!", "Yay, yay!" }
		},
		{
			"excited/evening",
			new string[8] { "Party time!", "Evening fun!", "Let's celebrate!", "Let's dance!", "What a vibe!", "Celebrate!", "Woohoo, evening", "Friday feeling?" }
		},
		{
			"excited/late",
			new string[8] { "Night party!", "Wow, the stars!", "Still buzzing!", "Late night fun", "Can't sleep, yay", "Moon dance!", "Woohoo, night", "Sparkly night" }
		},
		{
			"ctx/coding",
			new string[10] { "Found the bug?", "Did it build?", "Coding mode on", "Tests green?", "Ship it!", "Committed yet?", "Your debugging buddy", "Semicolon check", "One more fix?", "Clean code!" }
		},
		{
			"ctx/office",
			new string[8] { "Docs mode", "Inbox zero?", "Slides look good", "Busy bee", "Meeting notes?", "Work mode on", "Spreadsheet hero", "Almost done?" }
		},
		{
			"ctx/music",
			new string[8] { "Nice song!", "Good vibes", "Love this one", "Bop bop", "Volume up?", "Dance break?", "Love this beat", "Groove time" }
		},
		{
			"ctx/video",
			new string[8] { "Popcorn time", "Enjoying it?", "Movie mode", "Shh, watching", "Good show?", "Snack break?", "One more episode?", "Cinema vibes" }
		},
		{
			"ctx/gaming",
			new string[8] { "GG!", "Go, go, go!", "Nice move!", "Level up!", "You've got this!", "Boss fight?", "Pro gamer", "Combo!" }
		},
		{
			"ctx/break",
			new string[8] { "Take a short break?", "Stretch time?", "Time for a water break?", "Tea break?", "Rest your eyes?", "Walk a little?", "Five-minute break?", "Pause for a bit?" }
		},
		{
			"ctx/latenight",
			new string[8] { "We're both sleepy", "Late work, yawn", "Just a little more?", "Sleep soon, both of us?", "Night shift buddies", "Yawn together", "Save and sleep?", "Finish it tomorrow?" }
		},
		{
			"ctx/rain",
			new string[6] { "It's raining!", "Rainy day", "Umbrella ready", "Cosy rain", "Drip, drop", "Perfect weather for tea" }
		},
		{
			"ctx/hot",
			new string[6] { "It's so hot!", "Hot day!", "Fan on!", "Water, please", "The sun is strong", "Stay cool" }
		},
		{
			"ctx/cold",
			new string[6] { "It's cold!", "Scarf on", "Chilly day", "Brrr...", "Warm tea?", "Cosy weather" }
		},
		{
			"ctx/monday",
			new string[5] { "Monday again...", "Sleepy Monday", "New week, slowly", "Monday coffee first", "Easy does it, Monday" }
		},
		{
			"ctx/friday",
			new string[6] { "Friday!", "Almost the weekend!", "Friday dance?", "Party mood!", "TGIF!", "Friday feeling" }
		},
		{
			"ctx/weekend",
			new string[6] { "Weekend vibes", "Chill day", "Relax mode", "Day off?", "Slow Sunday", "Lazy weekend" }
		},
		{
			"ctx/return",
			new string[6] { "Welcome back!", "You're back!", "Back already? I missed you.", "There you are!", "Hello again!", "Back, yay!" }
		},
		{
			"festival/diwali",
			new string[5] { "Happy Diwali!", "Wishing you a bright Diwali!", "Light a diya!", "A night full of lights", "Time for sweets!" }
		},
		{
			"festival/holi",
			new string[5] { "Happy Holi!", "Let the colours fly!", "Colours everywhere!", "A splash of colour!", "Holi vibes!" }
		},
		{
			"festival/christmas",
			new string[5] { "Merry Christmas!", "Ho ho ho!", "Santa hat on", "Cake time!", "Jingle jingle!" }
		},
		{
			"festival/newyear",
			new string[5] { "Happy New Year!", "Cheers to the new year!", "Confetti time!", "New year, new fun", "Cheers!" }
		},
		{
			"festival/birthday",
			new string[5] { "Happy birthday, {name}!", "Cake time, {name}!", "Happy birthday to you!", "Birthday vibes!", "Make a wish!" }
		},
		{
			"story/rocket",
			new string[8] { "Rocket part added!", "My rocket is coming along", "Just a few more days", "New rocket piece!", "Building my rocket", "Almost ready...", "Bolt tight!", "Engines soon!" }
		},
		{
			"story/launch",
			new string[4] { "3... 2... 1... liftoff!", "My rocket flew!", "To the stars!", "We did it!" }
		},
		{
			"story/plant",
			new string[8] { "Watering my plant", "Grow, little plant", "A new leaf!", "Watering time", "My plant says hi", "Look, it grew!", "A bud!", "It flowered!" }
		},
		{
			"story/star",
			new string[6] { "Caught a star!", "One more star", "The jar is glowing", "Star collector", "Got one!", "The jar is full!" }
		},
		{
			"outcome/good",
			new string[8] { "Yes!", "Perfect!", "Nailed it!", "How about that!", "Woohoo!", "Nice!", "Just perfect", "Too good!" }
		},
		{
			"outcome/oops",
			new string[8] { "Oops!", "Whoops!", "Ugh...", "Next time!", "Hehe, oops", "Ouch!", "Haha, fail", "Again?" }
		},
		{
			"first",
			new string[1] { "Hi, I'm Ayn!" }
		}
	};

	public static IEnumerable<string> Groups => lines.Keys;

	public static int LineCount => lines.Values.Sum((string[] v) => v.Length);

	public static PetBigScene Big(string name)
	{
		return BigScenes.FirstOrDefault((PetBigScene b) => b.Name == name);
	}

	public static PetActivity Activity(string name)
	{
		return Activities.FirstOrDefault((PetActivity a) => a.Name == name);
	}

	public static PetDrawing Drawing(string name)
	{
		return Drawings.FirstOrDefault((PetDrawing d) => d.Name == name);
	}

	public static Point OnBoard(Point unit)
	{
		return new Point(DrawingRect.X + unit.X * DrawingRect.Width, DrawingRect.Y + unit.Y * DrawingRect.Height);
	}

	public static string[] DrawingsFor(int hour, bool music, string weather, string festival, PetMood mood, bool workLong)
	{
		List<string> list = new List<string>();
		if (hour >= 19 || hour < 5)
		{
			list.AddRange(new string[2] { "moon", "star" });
		}
		else
		{
			list.Add("sun");
		}
		if (music)
		{
			list.Add("note");
		}
		if (weather == "rain")
		{
			list.AddRange(new string[2] { "cloud", "umbrella" });
		}
		if (festival != null)
		{
			list.AddRange(new string[2] { "star", "heart" });
		}
		if (mood == PetMood.Happy || mood == PetMood.Excited)
		{
			list.AddRange(new string[3] { "heart", "smiley", "trophy" });
		}
		if (mood == PetMood.Sleepy)
		{
			list.Add("coffee");
		}
		if (workLong)
		{
			list.AddRange(new string[2] { "coffee", "battery" });
		}
		list.AddRange(new string[6] { "cat", "flower", "rocket", "house", "tree", "fish" });
		return list.Distinct().ToArray();
	}

	public static PetLineEntry[] Lines(string group)
	{
		if (!lines.TryGetValue(group, out var value))
		{
			return new PetLineEntry[0];
		}
		return value.Select((string t, int i) => new PetLineEntry(group + "#" + i, t)).ToArray();
	}

	public static string Mood(PetMood mood)
	{
		return mood.ToString().ToLowerInvariant();
	}
}
