using System;
using System.Collections.Generic;
using System.Windows;

namespace WindowsBuddy.Services.Pet;

public sealed class PetLife
{
	private readonly Random random;

	private double absence;

	private double nextBreath;

	private double nextIdle;

	private double started;

	private double until;

	private double lastStep;

	private double lastClick = -100.0;

	private double clickWindow;

	private double rubStart = -1.0;

	private double lastRub;

	private double previousX;

	private int clicks;

	private int direction;

	private int reversals;

	private string previousIdle = "";

	private double keyboardWindow;

	private double calmUntil;

	private double circle;

	private double previousAngle;

	private double lastCursor;

	private int keyboardSamples;

	private bool cursorKnown;

	private double baseSit;

	private double baseLeft = -15.0;

	private double baseRight = 15.0;

	private double baseTilt;

	private string[] loops;

	public PetMood DayMood = PetMood.Calm;

	private readonly List<KeyValuePair<double, string>> queued = new List<KeyValuePair<double, string>>();

	public double Affection = 0.5;

	public double HeadLift;

	public double Spin;

	public double Jump;

	public double Gait;

	public double Sit;

	public double Left = -15.0;

	public double Right = 15.0;

	public double Tilt;

	public double Breath;

	public double EyeScale = 1.0;

	public bool Near;

	public bool OnRobot;

	public double ActionTime;

	public double ActionPhase;

	public long MotionCount;

	public string BaseActivity { get; private set; }

	public int QueuedCount => queued.Count;

	public PetPresence Presence { get; private set; }

	public string Action { get; private set; }

	public Vector Look { get; private set; }

	public Vector? IdleLook { get; private set; }

	public bool Animating { get; private set; }

	public double ActionDuration => Math.Max(0.01, until - started);

	public bool ActivityMotion
	{
		get
		{
			if (Action != null && loops != null)
			{
				return Array.IndexOf(loops, Action) >= 0;
			}
			return false;
		}
	}

	public PetExpression Mood { get; private set; }

	public int Returns { get; private set; }

	public double LastAbsence { get; private set; }

	public PetLife(int seed = 0)
	{
		random = ((seed == 0) ? new Random() : new Random(seed));
		nextIdle = 5.0;
	}

	public void SetBase(string activity, double sit, double left, double right, double tilt)
	{
		BaseActivity = activity;
		baseSit = sit;
		baseLeft = left;
		baseRight = right;
		baseTilt = tilt;
		loops = Loops(activity);
	}

	public void ClearBase()
	{
		SetBase(null, 0.0, -15.0, 15.0, 0.0);
	}

	public static string[] Loops(string activity)
	{
		return activity switch
		{
			"after-table" => new string[3] { "sip", "sip", "look-left" }, 
			"after-plane" => new string[3] { "fold", "fold", "tilt" }, 
			"after-umbrella" => new string[2] { "twirl", "look-up" }, 
			"after-board" => new string[2] { "chalk-tap", "tilt" }, 
			"after-ball" => new string[3] { "bounce", "bounce", "nod" }, 
			"after-bucket" => new string[2] { "peer", "flap" }, 
			"after-kite" => new string[2] { "tug", "look-up" }, 
			"after-bat" => new string[3] { "swing-bat", "swing-bat", "nod" }, 
			"after-guitar" => new string[3] { "strum", "strum", "hum" }, 
			"after-wand" => new string[3] { "blow", "blow", "look-up" }, 
			"after-hat" => new string[3] { "wand", "wand", "tilt" }, 
			"after-yoyo" => new string[3] { "yoyo", "yoyo", "nod" }, 
			"after-bin" => new string[3] { "toss", "toss", "look-right" }, 
			"after-dominoes" => new string[3] { "place", "place", "tilt" }, 
			"reading" => new string[3] { "page", "nod-read", "page" }, 
			"laptop" => new string[3] { "type", "type", "nod-read" }, 
			"chai" => new string[3] { "sip", "sip", "look-left" }, 
			"headphones" => new string[3] { "nod", "nod", "hum" }, 
			"fishing" => new string[2] { "reel", "peer" }, 
			"stargazing" => new string[2] { "peer", "look-up" }, 
			"napping" => new string[1] { "snore" }, 
			"tinkering" => new string[2] { "hammer", "tap-gadget" }, 
			"gardening" => new string[2] { "water", "look-left" }, 
			"notebook" => new string[3] { "write", "write", "tilt" }, 
			"popcorn" => new string[2] { "munch", "watch" }, 
			"painting" => new string[3] { "brush", "brush", "tilt" }, 
			"puzzle" => new string[3] { "twist", "twist", "scratch" }, 
			"knitting" => new string[2] { "knit", "knit" }, 
			"yoga" => new string[2] { "pose", "breathe-deep" }, 
			"origami" => new string[3] { "fold", "fold", "tilt" }, 
			_ => null, 
		};
	}

	public void Queue(string[] micros, double now, double span)
	{
		queued.Clear();
		if (micros != null)
		{
			for (int i = 0; i < micros.Length; i++)
			{
				queued.Add(new KeyValuePair<double, string>(now + 1.0 + (double)i * Math.Max(3.0, (span - 4.0) / (double)Math.Max(1, micros.Length)), micros[i]));
			}
		}
	}

	public bool Calm(double now)
	{
		return now < calmUntil;
	}

	public static PetPresence PresenceAt(double idle, int hour)
	{
		bool flag = hour >= 23 || hour < 5;
		if (!(idle >= 1800.0))
		{
			if (!(idle >= 600.0))
			{
				if (!(idle >= (double)(flag ? 120 : 180)))
				{
					return PetPresence.Active;
				}
				return PetPresence.Sitting;
			}
			return PetPresence.Dozing;
		}
		return PetPresence.Sleeping;
	}

	public bool ObservePresence(double idle, int hour, double now)
	{
		PetPresence presence = PresenceAt(idle, hour);
		bool flag = Presence != 0 && idle < 3.0;
		if (idle > absence)
		{
			absence = idle;
		}
		Presence = presence;
		if (flag)
		{
			LastAbsence = absence;
			absence = 0.0;
			Returns++;
			Start("welcome", now, 3.0);
			Attention(0.03);
		}
		return flag;
	}

	public void Input(bool moved, double now)
	{
		if (moved)
		{
			keyboardSamples = 0;
			return;
		}
		if (now - keyboardWindow > 2.0)
		{
			keyboardWindow = now;
			keyboardSamples = 0;
		}
		if (++keyboardSamples >= 5)
		{
			calmUntil = now + 8.0;
		}
	}

	public static Vector Direction(Point cursor, Point robot)
	{
		return new Vector(Math.Max(-1.0, Math.Min(1.0, (cursor.X - robot.X) / 500.0)), Math.Max(-1.0, Math.Min(1.0, (cursor.Y - robot.Y) / 400.0)));
	}

	public void Cursor(Point cursor, Point robot, bool hit, double now)
	{
		Look = Direction(cursor, robot);
		Vector vector = cursor - robot;
		Near = vector.Length < 150.0;
		OnRobot = hit;
		double num = Math.Atan2(vector.Y, vector.X);
		double num2;
		for (num2 = num - previousAngle; num2 > Math.PI; num2 -= Math.PI * 2.0)
		{
		}
		for (; num2 < -Math.PI; num2 += Math.PI * 2.0)
		{
		}
		if (cursorKnown && now - lastCursor < 0.35 && vector.Length > 25.0)
		{
			circle = ((num2 * circle < 0.0) ? num2 : (circle + num2));
			if (Math.Abs(circle) > Math.PI * 3.0)
			{
				circle = 0.0;
				if (random.Next(3) == 0)
				{
					Start("dizzy", now, 1.6);
				}
			}
		}
		else
		{
			circle = 0.0;
		}
		previousAngle = num;
		lastCursor = now;
		cursorKnown = true;
	}

	public bool Rub(double x, bool hit, double now)
	{
		if (!hit || now - lastRub > 0.5)
		{
			rubStart = -1.0;
			reversals = 0;
			direction = 0;
		}
		if (!hit)
		{
			return false;
		}
		if (rubStart < 0.0)
		{
			rubStart = now;
			previousX = x;
		}
		double value = x - previousX;
		if (Math.Abs(value) >= 2.0)
		{
			int num = Math.Sign(value);
			if (direction != 0 && num != direction)
			{
				reversals++;
			}
			direction = num;
			previousX = x;
		}
		lastRub = now;
		if (now - rubStart >= 0.9 && reversals >= 3)
		{
			rubStart = now;
			reversals = 0;
			Attention(0.04);
			Start("purr", now, 1.8);
			return true;
		}
		return false;
	}

	public string Click(double now)
	{
		if (now - clickWindow > 2.0)
		{
			clickWindow = now;
			clicks = 0;
		}
		clicks++;
		string text = ((clicks >= 6) ? "annoyed" : ((clicks >= 4) ? "dizzy" : ((now - lastClick < 0.45) ? "trick" : (new string[6] { "wave", "hop", "spin", "giggle", "salute", "peek" })[random.Next(6)])));
		lastClick = now;
		Attention(0.015);
		Start(text, now, (text == "annoyed") ? 3.0 : 1.8);
		return text;
	}

	public static string ClaimRoutine(IDictionary<string, bool> parts, DateTime now)
	{
		string text = ((now.Hour >= 5 && now.Hour < 11) ? "morning" : ((now.Hour >= 12 && now.Hour < 15) ? "lunch" : ((now.Hour >= 17 && now.Hour < 21) ? "evening" : ((now.Hour >= 23) ? "night" : null))));
		if (text == null)
		{
			return null;
		}
		string key = "routine/" + now.ToString("yyyy-MM-dd") + "/" + text;
		if (parts.ContainsKey(key))
		{
			return null;
		}
		parts[key] = true;
		return text;
	}

	public static double Hop(double u, double height)
	{
		u = Math.Max(0.0, Math.Min(1.0, u));
		if (u < 0.2)
		{
			return 1.4 * Math.Sin(Math.PI * u / 0.2);
		}
		if (u < 0.8)
		{
			return (0.0 - height) * Math.Sin(Math.PI * (u - 0.2) / 0.6);
		}
		return 1.0 * Math.Sin(Math.PI * (u - 0.8) / 0.2);
	}

	public void Attention(double amount)
	{
		Affection = Math.Max(0.0, Math.Min(1.0, Affection + amount));
	}

	public void Start(string action, double now, double duration)
	{
		Action = action;
		started = now;
		until = now + duration;
		Animating = true;
	}

	public void Step(double now, bool allowed, int hour)
	{
		double num = ((lastStep == 0.0) ? 0.0 : Math.Max(0.0, Math.Min(60.0, now - lastStep)));
		lastStep = now;
		Attention((0.0 - num) / 86400.0 * 0.12);
		IdleLook = null;
		HeadLift = 0.0;
		Spin = (Jump = (Gait = (Breath = 0.0)));
		Tilt = baseTilt;
		Left = baseLeft;
		Right = baseRight;
		Sit = Math.Max(baseSit, (Presence >= PetPresence.Sitting) ? 1 : 0);
		Mood = ((Presence == PetPresence.Sleeping) ? PetExpression.Closed : ((Presence == PetPresence.Dozing || BaseActivity == "napping") ? PetExpression.Sleepy : ((Affection < 0.25) ? PetExpression.Worried : ((DayMood == PetMood.Sleepy && hour >= 13 && hour < 16) ? PetExpression.Sleepy : PetExpression.Normal))));
		EyeScale = 1.0;
		if (!allowed || Presence == PetPresence.Sleeping)
		{
			Action = null;
			Animating = false;
			return;
		}
		if (now >= until)
		{
			Action = null;
			Animating = false;
		}
		if (Action == null && queued.Count > 0 && now >= queued[0].Key)
		{
			Start(queued[0].Value, now, 2.2);
			queued.RemoveAt(0);
			nextIdle = Math.Max(nextIdle, now + 6.0);
		}
		if (Action == null && now >= nextIdle && Presence <= PetPresence.Sitting && loops != null)
		{
			string action = loops[random.Next(loops.Length)];
			double num2 = 0.8;
			Start(action, now, num2);
			MotionCount++;
			nextIdle = now + num2 + 1.4 + random.NextDouble() * 0.4;
		}
		if (Action == null && now >= nextIdle && Presence == PetPresence.Active)
		{
			string[] array = (Calm(now) ? new string[3] { "look-left", "look-right", "time" } : ((hour >= 23) ? new string[3] { "yawn", "stargaze", "tilt" } : ((Affection > 0.7) ? new string[6] { "hop", "stretch", "swing", "scratch", "look-left", "look-right" } : new string[14]
			{
				"look-left", "look-left", "look-right", "look-right", "tilt", "tilt", "tap", "stretch", "time", "time",
				"battery", "scratch", "hop", "swing"
			})));
			string text;
			do
			{
				text = array[random.Next(array.Length)];
			}
			while (text == previousIdle);
			previousIdle = text;
			Start(text, now, 2.0);
			nextIdle = now + 8.0 + random.NextDouble() * 10.0;
		}
		if (Action == null && now >= nextBreath && loops == null)
		{
			Start("breathe", now, 1.0);
			nextBreath = now + 5.0;
		}
		double num3 = Math.Max(0.0, now - started);
		double num4 = Math.Sin(Math.PI * Math.Min(1.0, num3 / Math.Max(0.01, until - started)));
		ActionTime = ((Action == null) ? 0.0 : num3);
		ActionPhase = ((Action == null) ? 0.0 : num4);
		HeadLift = (Near ? (Math.Min(0.0, Look.Y) * 3.0) : 0.0);
		if (Near)
		{
			Tilt = Look.X * 7.0;
		}
		EyeScale = (Near ? 1.12 : 1.0);
		if (OnRobot)
		{
			Mood = PetExpression.Happy;
		}
		if (Action == null)
		{
			return;
		}
		Animating = true;
		if (now - lastCursor > 1.2)
		{
			if (Action == "look-left" || Action == "battery")
			{
				IdleLook = new Vector(0.0 - num4, 0.0);
			}
			if (Action == "look-right" || Action == "time")
			{
				IdleLook = new Vector(num4, 0.0);
			}
			if (Action == "stargaze")
			{
				IdleLook = new Vector(0.0, 0.0 - num4);
			}
		}
		switch (Action)
		{
		case "breathe":
			Breath = 0.0 - Math.Sin(Math.PI * Math.Min(1.0, num3));
			break;
		case "look-left":
			Tilt = -8.0 * num4;
			break;
		case "look-right":
			Tilt = 8.0 * num4;
			break;
		case "time":
			Tilt = 7.0 * num4;
			break;
		case "battery":
			Tilt = -7.0 * num4;
			break;
		case "tilt":
			Tilt = 9.0 * num4;
			break;
		case "tap":
			Gait = Math.Sin(num3 * 15.0) * 0.5 * num4;
			break;
		case "stretch":
		case "yawn":
			Left -= 120.0 * num4;
			Right += 120.0 * num4;
			break;
		case "scratch":
			Left -= 120.0 * num4;
			Tilt = -7.0 * num4;
			break;
		case "hop":
			Jump = Hop(num3 / Math.Max(0.01, until - started), 4.0);
			break;
		case "swing":
			Sit = num4;
			Gait = Math.Sin(num3 * 10.0) * num4;
			break;
		case "wave":
			Right += 110.0 * num4 + 15.0 * Math.Sin(num3 * 18.0) * num4;
			break;
		case "welcome":
			Left -= 100.0 * num4;
			Right += 100.0 * num4 + 20.0 * Math.Sin(num3 * 14.0) * num4;
			Jump = (0.0 - Math.Min(3.0, LastAbsence / 300.0)) * num4;
			Mood = PetExpression.Happy;
			break;
		case "spin":
		case "trick":
			Spin = 360.0 * (0.5 - 0.5 * Math.Cos(Math.PI * Math.Min(1.0, num3 / (until - started))));
			Tilt = 0.0;
			Jump = -3.0 * num4;
			Gait = Math.Sin(num3 * 12.0) * num4;
			Left -= 70.0 * num4;
			Right += 70.0 * num4;
			break;
		case "salute":
			Right += 140.0 * num4;
			break;
		case "peek":
			Left -= 140.0 * num4;
			Tilt = 8.0 * num4;
			break;
		case "giggle":
		case "purr":
			Tilt = 4.0 * Math.Sin(num3 * 20.0) * num4;
			Mood = PetExpression.Happy;
			break;
		case "dizzy":
			Tilt = 12.0 * Math.Sin(num3 * 12.0) * num4;
			Mood = PetExpression.Surprised;
			break;
		case "annoyed":
			Tilt = -6.0 * num4;
			Mood = PetExpression.Worried;
			break;
		case "chai":
			Sit = num4;
			Right += 75.0 * num4;
			break;
		case "stargaze":
			Tilt = -8.0 * num4;
			break;
		case "look-around":
			Tilt = 8.0 * Math.Sin(num3 * 3.0) * num4;
			if (now - lastCursor > 1.2)
			{
				IdleLook = new Vector(Math.Sin(num3 * 3.0) * num4, 0.0);
			}
			break;
		case "look-up":
		case "peer":
			Tilt = baseTilt - 6.0 * num4;
			if (now - lastCursor > 1.2)
			{
				IdleLook = new Vector(0.0, 0.0 - num4);
			}
			break;
		case "page":
			Right += 55.0 * num4;
			Tilt = baseTilt + 3.0 * num4;
			break;
		case "sip":
			Right += 40.0 * num4;
			Tilt = -5.0 * num4;
			break;
		case "sneeze":
			Tilt = baseTilt + ((num3 < 0.9) ? (-6.0 * num4) : (9.0 * Math.Sin(Math.PI * Math.Min(1.0, (num3 - 0.9) / 0.3))));
			Jump = ((num3 > 0.9 && num3 < 1.2) ? (-2) : 0);
			break;
		case "nod":
		case "nod-read":
			Tilt = baseTilt + 6.0 * Math.Sin(num3 * 6.0) * num4;
			break;
		case "shrug":
			Left -= 40.0 * num4;
			Right += 40.0 * num4;
			HeadLift = -2.0 * num4;
			break;
		case "peek-small":
		case "wave-small":
			Right += 90.0 * num4 + 10.0 * Math.Sin(num3 * 16.0) * num4;
			break;
		case "wipe-brow":
			Left -= 130.0 * num4;
			Tilt = -4.0 * num4;
			break;
		case "double-blink":
			Mood = ((num3 < 0.3 || (num3 > 0.6 && num3 < 0.9)) ? PetExpression.Closed : Mood);
			break;
		case "hum":
			Tilt = baseTilt + 5.0 * Math.Sin(num3 * 4.0) * num4;
			break;
		case "dust-off":
			Left += 20.0 * Math.Sin(num3 * 14.0) * num4;
			Right -= 20.0 * Math.Sin(num3 * 14.0) * num4;
			break;
		case "thumbs-up":
			Right += 95.0 * num4;
			Mood = PetExpression.Happy;
			break;
		case "hiccup":
			Jump = (((num3 > 0.6 && num3 < 0.8) || (num3 > 1.4 && num3 < 1.6)) ? (-2) : 0);
			break;
		case "kick":
			Gait = Math.Sin(num3 * 9.0) * num4;
			break;
		case "whistle":
			Tilt = baseTilt + 4.0 * num4;
			break;
		case "sigh":
			Breath = -1.5 * num4;
			Tilt = baseTilt - 3.0 * num4;
			break;
		case "clap":
			Left -= 60.0 * num4 + 15.0 * Math.Sin(num3 * 18.0) * num4;
			Right += 60.0 * num4 + 15.0 * Math.Sin(num3 * 18.0) * num4;
			break;
		case "wiggle":
			Gait = Math.Sin(num3 * 12.0) * 0.8 * num4;
			Tilt = 5.0 * Math.Sin(num3 * 12.0) * num4;
			break;
		case "cheer":
			Left -= 140.0 * num4;
			Right += 140.0 * num4;
			Jump = Hop(num3 / Math.Max(0.01, until - started), 2.5);
			Mood = PetExpression.Happy;
			break;
		case "polish":
			Right += 30.0 * Math.Sin(num3 * 10.0) * num4;
			break;
		case "rub-eyes":
			Left -= 110.0 * num4;
			Right += 110.0 * num4;
			Mood = PetExpression.Sleepy;
			break;
		case "type":
			Left += 9.0 * Math.Sin(num3 * 20.0) * num4;
			Right += 9.0 * Math.Sin(num3 * 20.0 + 1.5) * num4;
			break;
		case "reel":
			Right += 25.0 * Math.Sin(num3 * 8.0) * num4;
			break;
		case "snore":
			Breath = (0.0 - Math.Sin(Math.PI * Math.Min(1.0, num3 / 3.0))) * 1.5;
			break;
		case "hammer":
		case "tap-gadget":
			Right += 50.0 * Math.Abs(Math.Sin(num3 * 6.0)) * num4;
			break;
		case "water":
			Right += 30.0 * num4;
			Tilt = baseTilt + 5.0 * num4;
			break;
		case "write":
			Right += 8.0 * Math.Sin(num3 * 14.0) * num4;
			break;
		case "munch":
			Right += 50.0 * Math.Abs(Math.Sin(num3 * 4.0)) * num4;
			break;
		case "watch":
			if (now - lastCursor > 1.2)
			{
				IdleLook = new Vector(0.3 * num4, 0.0);
			}
			break;
		case "brush":
			Right += 30.0 * Math.Sin(num3 * 5.0) * num4;
			break;
		case "twist":
			Left += 15.0 * Math.Sin(num3 * 7.0) * num4;
			Right -= 15.0 * Math.Sin(num3 * 7.0) * num4;
			break;
		case "knit":
			Left += 12.0 * Math.Sin(num3 * 9.0) * num4;
			Right += 12.0 * Math.Sin(num3 * 9.0 + Math.PI) * num4;
			break;
		case "pose":
			Left -= 20.0 * num4;
			Right += 20.0 * num4;
			Tilt = 6.0 * Math.Sin(num3 * 2.0) * num4;
			break;
		case "breathe-deep":
			Breath = -1.5 * num4;
			break;
		case "fold":
			Left += 20.0 * num4;
			Right -= 20.0 * num4;
			break;
		case "polish-screen":
			Right += 30.0 * Math.Sin(num3 * 10.0) * num4;
			break;
		case "twirl":
			Right += 10.0 * Math.Sin(num3 * 8.0) * num4;
			Tilt = baseTilt + 4.0 * Math.Sin(num3 * 4.0) * num4;
			break;
		case "chalk-tap":
			Right += 25.0 * Math.Abs(Math.Sin(num3 * 9.0)) * num4;
			break;
		case "bounce":
			Right += 35.0 * Math.Abs(Math.Sin(num3 * Math.PI * 2.5)) * num4;
			break;
		case "flap":
			Tilt = baseTilt + 6.0 * num4;
			if (now - lastCursor > 1.2)
			{
				IdleLook = new Vector(0.6 * num4, 0.4 * num4);
			}
			break;
		case "tug":
			Right += 25.0 * Math.Sin(num3 * 6.0) * num4;
			Tilt = baseTilt - 5.0 * num4;
			break;
		case "swing-bat":
			Right += 70.0 * Math.Sin(Math.PI * Math.Min(1.0, num3 / 1.2)) * num4;
			Tilt = baseTilt + 4.0 * num4;
			break;
		case "strum":
			Right += 18.0 * Math.Sin(num3 * 14.0) * num4;
			Tilt = baseTilt + 5.0 * Math.Sin(num3 * 4.0) * num4;
			break;
		case "blow":
			Right += 20.0 * num4;
			HeadLift = -1.0 * num4;
			break;
		case "wand":
			Right += 40.0 * Math.Sin(num3 * 7.0) * num4;
			break;
		case "yoyo":
			Right += 20.0 * Math.Abs(Math.Sin(num3 * Math.PI * 1.25)) * num4;
			break;
		case "toss":
			Right += 110.0 * Math.Sin(Math.PI * Math.Min(1.0, num3 / 1.0)) * num4;
			break;
		case "place":
			Right += 40.0 * num4;
			Tilt = baseTilt + 6.0 * num4;
			break;
		}
	}
}
