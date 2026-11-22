using System;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class GardenScenes
{
	public const double Side = 78.0;

	public const double Wing = 340.0;

	public static readonly string[] Stories = new string[7] { "tea", "plane", "rain", "chalk", "ball", "sleep", "dance" };

	public static GardenScene Story(string name, double x)
	{
		return name switch
		{
			"chalk" => new GardenScene(name, 16.0).Track("x", 0.0, x, 2.0, -35.0, 3.0, -35.0, 5.0, 96.0, 10.0, 174.0, 12.0, 96.0, 14.0, -35.0, 16.0, 140.0).Track("chalk.x", 0.0, -35.0, 3.0, -35.0, 5.0, 108.0, 10.0, 186.0, 12.0, 108.0, 14.0, -35.0, 16.0, -35.0).Track("writing", 0.0, 0.0, 5.0, 0.0, 9.0, 1.0, 10.0, 1.0, 12.0, 0.0)
				.Track("armR", 0.0, 15.0, 5.0, 95.0, 6.0, 65.0, 7.0, 100.0, 8.0, 65.0, 9.0, 100.0, 12.0, 75.0, 16.0, 15.0)
				.Event(5.0, "write")
				.Event(10.0, "wipe"), 
			"ball" => new GardenScene(name, 18.0).Track("x", 0.0, x, 2.0, -35.0, 3.0, -35.0, 5.0, 110.0, 8.0, 170.0, 11.0, 130.0, 14.0, 210.0, 16.0, 320.0, 18.0, 140.0).Track("ball.x", 0.0, -35.0, 3.0, -35.0, 5.0, 122.0, 7.0, 190.0, 9.0, 145.0, 11.0, 145.0, 13.0, 225.0, 16.0, 320.0, 18.0, 320.0).Track("ball.y", 0.0, 33.0, 5.0, 33.0, 6.0, 14.0, 7.0, 33.0, 8.0, 10.0, 9.0, 29.0, 10.0, 13.0, 11.0, 33.0, 12.0, 17.0, 13.0, 33.0, 14.0, 24.0, 15.0, 33.0, 16.0, 33.0)
				.Track("jump", 0.0, 0.0, 7.0, 0.0, 7.2, 2.0, 7.5, -5.0, 8.0, 0.0, 11.0, 0.0, 11.2, 2.0, 11.5, -6.0, 12.0, 0.0)
				.Arc("ball.x", 5.0)
				.Arc("ball.y", 5.0)
				.Event(5.0, "kick")
				.Event(9.0, "juggle")
				.Event(16.0, "fetch"), 
			"sleep" => new GardenScene(name, 20.0).Track("x", 0.0, x, 2.0, -35.0, 3.0, -35.0, 5.0, 140.0, 14.0, 140.0, 17.0, -35.0, 18.0, -35.0, 20.0, 140.0).Track("pillow.x", 0.0, -35.0, 3.0, -35.0, 5.0, 140.0, 14.0, 140.0, 17.0, -35.0, 20.0, -35.0).Track("pillow.y", 0.0, 24.0, 5.0, 24.0, 6.0, 32.0, 14.0, 32.0, 15.0, 24.0, 20.0, 24.0)
				.Track("sleep", 0.0, 0.0, 6.0, 0.0, 8.0, 1.0, 12.0, 1.0, 14.0, 0.0, 20.0, 0.0)
				.Track("head", 0.0, 0.0, 5.0, -10.0, 6.0, 0.0, 14.0, 0.0, 15.0, -8.0, 16.0, 0.0)
				.Event(6.0, "yawn")
				.Event(8.0, "sleep")
				.Event(13.0, "stretch"), 
			"dance" => new GardenScene(name, 16.0).Track("x", 0.0, x, 2.0, -35.0, 3.0, -35.0, 5.0, 140.0, 6.0, 132.0, 7.0, 148.0, 8.0, 132.0, 9.0, 148.0, 10.0, 132.0, 11.0, 140.0, 14.0, -35.0, 16.0, 140.0).Track("headphones.x", 0.0, -35.0, 3.0, -35.0, 5.0, 140.0, 6.0, 132.0, 7.0, 148.0, 8.0, 132.0, 9.0, 148.0, 10.0, 132.0, 11.0, 140.0, 14.0, -35.0, 16.0, -35.0).Track("headphones.y", 0.0, 24.0, 5.0, 24.0, 5.5, 12.0, 11.0, 12.0, 12.0, 24.0, 16.0, 24.0)
				.Track("armL", 0.0, -15.0, 5.0, -15.0, 6.0, -100.0, 7.0, -15.0, 8.0, -100.0, 9.0, -15.0, 10.0, -100.0, 11.0, -15.0)
				.Track("armR", 0.0, 15.0, 5.0, 15.0, 6.0, 100.0, 7.0, 15.0, 8.0, 100.0, 9.0, 15.0, 10.0, 100.0, 11.0, 15.0)
				.Track("head", 0.0, 0.0, 5.0, 0.0, 6.0, -8.0, 7.0, 8.0, 8.0, -8.0, 9.0, 8.0, 10.0, -8.0, 11.0, 0.0)
				.Event(5.0, "headphones-on")
				.Event(12.0, "headphones-off"), 
			"tea" => new GardenScene(name, 20.0).Track("x", 0.0, x, 1.0, -78.0, 2.0, -78.0, 5.0, 113.0, 6.0, -28.0, 7.0, -28.0, 9.0, 113.0, 10.0, 113.0, 14.0, 113.0, 15.0, -28.0, 16.0, -28.0, 17.0, 113.0, 18.7, -78.0, 19.0, -78.0, 20.0, 140.0).Track("table.x", 0.0, -48.0, 2.0, -48.0, 5.0, 143.0, 17.0, 143.0, 18.7, -48.0, 20.0, -48.0).Track("cup.x", 0.0, -28.0, 7.0, -28.0, 9.0, 129.0, 14.0, 129.0, 15.0, -28.0)
				.Track("cup.y", 0.0, 23.0, 10.0, 23.0, 10.7, 15.0, 11.3, 15.0, 12.0, 23.0, 12.7, 15.0, 13.2, 15.0, 14.0, 23.0)
				.Track("sit", 0.0, 0.0, 9.5, 0.0, 10.0, 1.0, 13.5, 1.0, 14.0, 0.0)
				.Track("armR", 0.0, 15.0, 2.0, 65.0, 5.0, 65.0, 6.0, 15.0, 7.0, 65.0, 9.0, 65.0, 10.0, 70.0, 11.0, 105.0, 12.0, 70.0, 13.0, 105.0, 14.0, 65.0, 15.0, 65.0, 16.0, 15.0, 17.0, 65.0, 18.7, 65.0, 19.2, 15.0, 20.0, 15.0)
				.Event(2.0, "drag-table")
				.Event(7.0, "fetch-cup")
				.Event(10.0, "sip")
				.Event(17.0, "return-table"), 
			"plane" => new GardenScene(name, 16.0).Track("x", 0.0, x, 1.5, -28.0, 2.0, -28.0, 4.0, 104.0, 9.0, 104.0, 10.0, 130.0, 12.0, 165.0, 14.0, 182.0, 16.0, 140.0).Track("paper.x", 0.0, -28.0, 2.0, -28.0, 4.0, 119.0, 8.0, 119.0, 8.5, 108.0, 9.0, 129.0, 10.0, 225.0, 11.0, 242.0, 12.0, 162.0, 13.0, 95.0, 14.0, 140.0, 16.0, 310.0).Track("paper.y", 0.0, 24.0, 4.0, 24.0, 8.0, 24.0, 8.5, 26.0, 9.0, 17.0, 10.0, 7.0, 11.0, 12.0, 12.0, 22.0, 13.0, 12.0, 14.0, 7.0, 16.0, 16.0)
				.Track("fold", 0.0, 0.0, 4.8, 0.0, 5.5, 1.0, 6.0, 1.0, 6.7, 2.0, 7.2, 2.0, 8.0, 3.0)
				.Track("paper.angle", 0.0, 0.0, 8.0, 0.0, 8.5, -20.0, 9.0, -30.0, 10.0, 0.0, 11.0, 100.0, 12.0, 180.0, 13.0, 280.0, 14.0, 360.0, 16.0, 360.0)
				.Track("armL", 0.0, -15.0, 4.0, -15.0, 5.0, -80.0, 5.5, -25.0, 6.0, -90.0, 6.7, -25.0, 7.2, -85.0, 8.0, -25.0, 10.0, -15.0, 11.0, -100.0, 11.3, -40.0, 11.6, -100.0, 12.0, -40.0, 13.0, -15.0)
				.Track("armR", 0.0, 15.0, 4.0, 70.0, 5.0, 100.0, 5.5, 40.0, 6.0, 100.0, 6.7, 40.0, 7.2, 100.0, 8.0, 70.0, 8.5, 35.0, 9.0, 140.0, 10.0, 15.0, 11.0, 100.0, 11.3, 40.0, 11.6, 100.0, 12.0, 40.0, 13.0, 15.0)
				.Track("jump", 0.0, 0.0, 10.0, 0.0, 10.2, 2.0, 10.5, -6.0, 10.9, 0.0, 11.1, 1.0, 11.4, 0.0)
				.Arc("paper.x", 9.0)
				.Arc("paper.y", 9.0)
				.Event(5.0, "fold-1")
				.Event(6.2, "fold-2")
				.Event(7.4, "fold-3")
				.Event(9.0, "throw")
				.Event(11.0, "clap"), 
			"rain" => new GardenScene(name, 19.0).Track("x", 0.0, x, 2.0, 140.0, 3.0, 140.0, 4.0, -28.0, 5.0, -28.0, 7.0, 140.0, 13.0, 140.0, 16.5, 140.0, 17.5, -32.0, 19.0, 140.0).Track("cloud.x", 0.0, -55.0, 3.0, 120.0, 12.0, 180.0, 16.0, 330.0).Track("umbrella.x", 0.0, -28.0, 5.0, -28.0, 7.0, 148.0, 16.5, 148.0, 17.5, -28.0)
				.Track("umbrella.open", 0.0, 0.0, 7.0, 0.0, 7.8, 1.0, 16.0, 1.0, 16.5, 0.0)
				.Track("head", 0.0, 0.0, 2.0, -12.0, 3.0, -12.0, 4.0, 0.0, 7.0, -8.0, 13.0, -8.0, 15.0, 0.0)
				.Track("armR", 0.0, 15.0, 5.0, 75.0, 7.0, 115.0, 16.0, 115.0, 16.5, 75.0, 17.5, 75.0, 19.0, 15.0)
				.Event(3.0, "rain")
				.Event(5.0, "fetch-umbrella")
				.Event(7.8, "shelter")
				.Event(16.5, "close-umbrella"), 
			_ => throw new ArgumentException("Unknown Garden story: " + name), 
		};
	}

	public static GardenScene Big(string name, string outcome, double x)
	{
		GardenScene gardenScene = new GardenScene(name, 14.0);
		gardenScene.Track("x", 0.0, x, 1.2, 78.0, 14.0, 78.0);
		switch (name)
		{
		case "tea":
			gardenScene.Track("p.x", 0.0, 340.0, 0.8, 340.0, 2.6, 176.0, 11.5, 176.0, 13.4, 340.0, 14.0, 340.0).Track("pour", 0.0, 0.0, 3.0, 0.0, 3.6, 1.0, 5.6, 1.0, 6.2, 0.0).Track("fill", 0.0, 0.0, 3.6, 0.0, 5.6, 1.0, 14.0, 1.0)
				.Track("armR", 0.0, 15.0, 6.4, 15.0, 7.0, 95.0, 8.0, 95.0, 8.6, 15.0)
				.Event(8.4, "outcome");
			if (outcome == "perfect")
			{
				gardenScene.Track("heart", 0.0, 0.0, 8.4, 0.0, 9.0, 1.0, 11.0, 1.0, 11.4, 0.0).Track("jump", 0.0, 0.0, 8.6, 0.0, 8.8, 1.0, 9.1, -4.0, 9.5, 0.0);
			}
			else
			{
				gardenScene.Track("tip", 0.0, 0.0, 8.2, 0.0, 8.6, 1.0, 14.0, 1.0).Track("puddle", 0.0, 0.0, 8.5, 0.0, 9.6, 1.0, 14.0, 1.0).Track("head", 0.0, 0.0, 8.6, 0.0, 8.9, -9.0, 9.3, 8.0, 9.7, -9.0, 10.1, 0.0);
			}
			break;
		case "plane":
			gardenScene.Duration = 13.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 13.0, 78.0);
			gardenScene.Track("fold", 0.0, 0.0, 1.2, 0.0, 3.6, 3.0).Track("armR", 0.0, 15.0, 1.2, 70.0, 3.6, 70.0, 4.2, 150.0, 4.6, 40.0, 5.0, 15.0).Event(7.0, "outcome");
			if (outcome == "far")
			{
				gardenScene.Track("pl.x", 0.0, 96.0, 4.2, 96.0, 6.0, 170.0, 8.0, 250.0, 10.0, 340.0).Track("pl.y", 0.0, 20.0, 4.2, 20.0, 6.0, 12.0, 8.0, 8.0, 10.0, -8.0).Track("pl.a", 0.0, 0.0, 4.2, -10.0, 10.0, -15.0)
					.Track("jump", 0.0, 0.0, 7.2, 0.0, 7.4, 1.0, 7.7, -4.0, 8.1, 0.0);
			}
			else if (outcome == "nosedive")
			{
				gardenScene.Track("pl.x", 0.0, 96.0, 4.2, 96.0, 5.6, 160.0, 6.6, 194.0, 7.2, 204.0, 11.0, 204.0, 12.6, 340.0).Track("pl.y", 0.0, 20.0, 4.2, 20.0, 5.6, 8.0, 6.6, 18.0, 7.2, 31.0, 13.0, 31.0).Track("pl.a", 0.0, 0.0, 4.2, -10.0, 5.6, -10.0, 6.6, 30.0, 7.2, 38.0, 13.0, 38.0)
					.Track("head", 0.0, 0.0, 7.2, 0.0, 7.6, -9.0, 9.4, -9.0, 9.8, 0.0);
			}
			else
			{
				gardenScene.Track("pl.x", 0.0, 96.0, 4.2, 96.0, 6.0, 170.0, 7.0, 200.0, 10.4, 200.0, 11.2, 212.0, 12.6, 340.0).Track("pl.y", 0.0, 20.0, 4.2, 20.0, 6.0, 8.0, 7.0, 3.0, 10.4, 3.0, 11.2, 31.0, 13.0, 31.0).Track("pl.a", 0.0, 0.0, 4.2, -15.0, 7.0, -25.0, 10.4, -25.0, 11.2, 0.0, 13.0, 0.0)
					.Track("wiggle", 0.0, 0.0, 7.0, 0.0, 7.3, 1.0, 7.6, -1.0, 7.9, 1.0, 8.2, -1.0, 8.5, 0.0)
					.Track("armL", 0.0, -15.0, 7.4, -15.0, 7.8, -140.0, 9.6, -140.0, 10.0, -15.0);
			}
			break;
		case "rain":
			gardenScene.Track("cloud.x", 0.0, 340.0, 3.0, 120.0, 11.0, 120.0, 13.6, -80.0).Track("rain", 0.0, 0.0, 2.6, 0.0, 3.2, 1.0, 11.0, 1.0, 11.6, 0.0).Track("head", 0.0, 0.0, 2.4, 0.0, 2.8, -10.0, 3.6, -10.0, 4.0, 0.0)
				.Track("armR", 0.0, 15.0, 4.0, 15.0, 4.8, 100.0, 12.0, 100.0, 12.6, 15.0)
				.Event(8.0, "outcome");
			if (outcome == "shelter")
			{
				gardenScene.Track("umb", 0.0, 0.0, 4.0, 0.0, 4.8, 1.0, 12.0, 1.0, 12.6, 0.0);
				break;
			}
			gardenScene.Track("umb", 0.0, 0.0, 4.0, 0.0, 4.8, 1.0, 7.6, 1.0, 8.0, -1.0, 12.0, -1.0, 12.6, 0.0).Track("wet", 0.0, 0.0, 8.0, 0.0, 8.6, 1.0, 11.6, 1.0, 12.4, 0.0).Track("head2", 0.0, 0.0, 8.0, 0.0, 8.3, 1.0, 12.0, 1.0, 12.2, 0.0);
			break;
		case "chalk":
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.4, 78.0, 2.8, 122.0, 11.6, 122.0, 13.0, 78.0, 14.0, 78.0);
			gardenScene.Track("b.x", 0.0, 340.0, 0.6, 340.0, 2.6, 190.0, 12.0, 190.0, 13.6, 340.0, 14.0, 340.0).Track("writing", 0.0, 0.0, 3.2, 0.0, 8.0, 1.0, 14.0, 1.0).Track("armR", 0.0, 15.0, 3.0, 95.0, 4.0, 70.0, 5.0, 100.0, 6.0, 70.0, 7.0, 100.0, 8.0, 75.0, 11.6, 75.0, 12.4, 15.0)
				.Event(9.0, "outcome");
			if (outcome == "erase")
			{
				gardenScene.Track("wipe", 0.0, 0.0, 9.0, 0.0, 10.6, 1.0, 14.0, 1.0).Track("armL", 0.0, -15.0, 8.9, -100.0, 9.3, -70.0, 9.7, -100.0, 10.1, -70.0, 10.6, -15.0);
			}
			else
			{
				gardenScene.Track("crumple", 0.0, 0.0, 9.0, 0.0, 9.6, 1.0, 14.0, 1.0).Track("wad.x", 0.0, 190.0, 9.6, 190.0, 11.2, 340.0).Track("wad.y", 0.0, 18.0, 9.6, 18.0, 10.4, 4.0, 11.2, 22.0)
					.Track("armL", 0.0, -15.0, 9.2, -60.0, 9.5, -150.0, 9.9, -15.0)
					.Arc("wad.x", 9.6)
					.Arc("wad.y", 9.6);
			}
			break;
		case "ball":
			gardenScene.Duration = 13.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 13.0, 78.0);
			gardenScene.Track("ball.x", 0.0, 340.0, 1.0, 340.0, 3.4, 104.0, 4.0, 104.0).Track("ball.y", 0.0, 32.0, 4.0, 32.0).Track("kick", 0.0, 0.0, 3.8, 0.0, 4.0, 1.0, 4.3, 0.0)
				.Event(7.0, "outcome");
			if (outcome == "catch")
			{
				gardenScene.Tracks["ball.x"].Key(5.4, 190.0).Key(7.0, 92.0).Key(9.4, 92.0)
					.Key(11.0, 340.0);
				gardenScene.Tracks["ball.y"].Key(5.4, 4.0).Key(7.0, 9.0).Key(9.4, 9.0)
					.Key(10.2, 0.0)
					.Key(11.0, 20.0);
				gardenScene.Track("armL", 0.0, -15.0, 6.4, -15.0, 6.9, -150.0, 9.4, -150.0, 9.8, -15.0).Track("armR", 0.0, 15.0, 6.4, 15.0, 6.9, 150.0, 9.4, 150.0, 9.8, 15.0).Track("jump", 0.0, 0.0, 7.2, 0.0, 7.4, 1.0, 7.7, -4.0, 8.1, 0.0);
			}
			else
			{
				gardenScene.Tracks["ball.x"].Key(5.4, 180.0).Key(7.0, 80.0).Key(8.6, 150.0)
					.Key(11.0, 340.0);
				gardenScene.Tracks["ball.y"].Key(5.4, 4.0).Key(7.0, 2.0).Key(8.0, 24.0)
					.Key(8.6, 32.0)
					.Key(11.0, 32.0);
				gardenScene.Track("stars", 0.0, 0.0, 7.0, 0.0, 7.2, 1.0, 10.0, 1.0, 10.4, 0.0).Track("head", 0.0, 0.0, 7.0, 0.0, 7.3, -12.0, 8.5, 6.0, 9.5, -6.0, 10.4, 0.0);
			}
			gardenScene.Arc("ball.x", 4.0).Arc("ball.y", 4.0);
			break;
		case "catch":
			gardenScene.Track("w.x", 0.0, 340.0, 0.6, 340.0, 2.6, 184.0, 12.0, 184.0, 13.8, 340.0, 14.0, 340.0).Track("line", 0.0, 0.0, 3.2, 0.0, 4.0, 1.0, 14.0, 1.0).Track("armR", 0.0, 15.0, 2.6, 150.0, 3.2, 60.0, 9.0, 60.0, 9.4, 150.0, 10.4, 90.0, 12.0, 90.0, 12.6, 15.0)
				.Track("bob", 0.0, 0.0, 5.0, 0.0, 5.3, 1.0, 5.7, 0.0, 6.6, 1.0, 7.0, 0.0, 8.0, 1.4, 8.4, 0.0)
				.Event(9.6, "outcome");
			if (outcome == "fish")
			{
				gardenScene.Track("hook", 0.0, 0.0, 9.2, 0.0, 10.4, 1.0, 12.0, 1.0).Track("jump", 0.0, 0.0, 10.4, 0.0, 10.6, 1.0, 10.9, -4.0, 11.3, 0.0);
			}
			else if (outcome == "shoe")
			{
				gardenScene.Track("hook", 0.0, 0.0, 9.2, 0.0, 10.4, 1.0, 12.0, 1.0).Track("head", 0.0, 0.0, 10.4, 0.0, 10.8, -9.0, 11.8, -9.0, 12.2, 0.0);
			}
			else
			{
				gardenScene.Track("hook", 0.0, 0.0, 9.2, 0.0, 10.4, 1.0, 12.0, 1.0).Track("armL", 0.0, -15.0, 10.4, -15.0, 10.8, -60.0, 11.8, -60.0, 12.2, -15.0).Track("head", 0.0, 0.0, 10.4, 0.0, 10.8, 7.0, 11.2, -7.0, 11.6, 0.0);
			}
			break;
		case "kite":
			gardenScene.Track("k.x", 0.0, 96.0, 1.5, 96.0, 5.0, 196.0, 11.0, 206.0, 13.4, 340.0).Track("k.y", 0.0, 22.0, 1.5, 22.0, 5.0, 10.0, 11.0, 6.0, 13.4, -6.0).Track("armR", 0.0, 15.0, 1.2, 120.0, 11.0, 120.0, 12.0, 15.0)
				.Event(7.4, "outcome");
			if (outcome == "soar")
			{
				gardenScene.Track("sway", 0.0, 0.0, 5.0, 0.0, 6.0, 1.0, 7.0, -1.0, 8.0, 1.0, 9.0, -1.0, 10.0, 1.0, 11.0, 0.0).Track("jump", 0.0, 0.0, 7.4, 0.0, 7.6, 1.0, 7.9, -4.0, 8.3, 0.0);
				break;
			}
			gardenScene.Tracks.Remove("k.x");
			gardenScene.Tracks.Remove("k.y");
			gardenScene.Track("k.x", 0.0, 96.0, 1.5, 96.0, 5.0, 196.0, 7.0, 206.0, 8.4, 214.0, 12.4, 214.0, 13.6, 340.0).Track("k.y", 0.0, 22.0, 1.5, 22.0, 5.0, 10.0, 7.0, 8.0, 8.4, 32.0, 13.6, 32.0).Track("spin", 0.0, 0.0, 7.0, 0.0, 8.4, 2.0, 14.0, 2.0)
				.Track("tangle", 0.0, 0.0, 8.2, 0.0, 8.8, 1.0, 14.0, 1.0)
				.Track("head", 0.0, 0.0, 8.4, 0.0, 8.8, -9.0, 10.4, -9.0, 10.8, 0.0);
			break;
		case "cricket":
			gardenScene.Duration = 13.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 13.0, 78.0);
			gardenScene.Track("st.x", 0.0, -40.0, 0.6, -40.0, 2.2, 50.0, 11.4, 50.0, 12.8, -40.0, 13.0, -40.0).Track("ball.x", 0.0, 340.0, 3.4, 340.0, 5.0, 100.0).Track("ball.y", 0.0, 30.0, 5.0, 30.0)
				.Track("swing", 0.0, 0.0, 4.7, 0.0, 5.0, 1.0, 5.4, 0.0)
				.Event(5.6, "outcome");
			if (outcome == "six")
			{
				gardenScene.Tracks["ball.x"].Key(7.0, 340.0);
				gardenScene.Tracks["ball.y"].Key(6.0, -2.0).Key(7.0, -12.0);
				gardenScene.Arc("ball.x", 5.0).Arc("ball.y", 5.0);
				gardenScene.Track("armL", 0.0, -15.0, 5.6, -150.0, 7.4, -150.0, 7.8, -15.0).Track("jump", 0.0, 0.0, 5.8, 0.0, 6.0, 1.0, 6.3, -5.0, 6.7, 0.0);
			}
			else
			{
				gardenScene.Tracks["ball.x"].Key(5.3, 50.0).Key(13.0, 50.0);
				gardenScene.Tracks["ball.y"].Key(13.0, 30.0);
				gardenScene.Track("bowled", 0.0, 0.0, 5.3, 0.0, 5.7, 1.0, 11.4, 1.0).Track("head", 0.0, 0.0, 5.5, 0.0, 5.9, -10.0, 7.6, -10.0, 8.0, 0.0);
			}
			break;
		case "guitar":
			gardenScene.Duration = 13.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 13.0, 78.0);
			gardenScene.Track("g.x", 0.0, 340.0, 0.6, 340.0, 2.6, 138.0, 10.8, 138.0, 12.8, 340.0, 13.0, 340.0).Track("sit", 0.0, 0.0, 2.6, 0.0, 3.0, 1.0, 10.4, 1.0, 10.8, 0.0).Track("armR", 0.0, 15.0, 3.0, 70.0, 3.3, 95.0, 3.6, 70.0, 3.9, 95.0, 4.2, 70.0, 4.5, 95.0, 4.8, 70.0, 5.1, 95.0, 5.4, 70.0, 9.6, 70.0, 10.4, 15.0)
				.Event(6.4, "outcome");
			if (outcome == "tune")
			{
				gardenScene.Track("notes", 0.0, 0.0, 3.2, 0.0, 10.0, 1.0, 10.6, 0.0).Track("head", 0.0, 0.0, 3.4, 0.0, 4.4, 5.0, 5.4, -5.0, 6.4, 5.0, 7.4, -5.0, 8.4, 0.0);
			}
			else
			{
				gardenScene.Track("notes", 0.0, 0.0, 3.2, 0.0, 5.8, 0.4, 6.1, 0.0).Track("snap", 0.0, 0.0, 6.0, 0.0, 6.3, 1.0, 10.0, 1.0, 10.3, 0.0).Track("head", 0.0, 0.0, 6.1, 0.0, 6.4, -10.0, 8.4, -10.0, 8.8, 0.0);
			}
			break;
		case "bubbles":
			gardenScene.Duration = 13.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 13.0, 78.0);
			gardenScene.Track("armR", 0.0, 15.0, 1.4, 90.0, 11.0, 90.0, 11.6, 15.0).Event(7.0, "outcome");
			if (outcome == "big")
			{
				gardenScene.Track("bub.r", 0.0, 0.0, 3.0, 0.0, 7.0, 13.0, 13.0, 13.0).Track("bub.x", 0.0, 104.0, 3.0, 104.0, 7.0, 150.0, 8.0, 168.0, 11.0, 180.0).Track("bub.y", 0.0, 20.0, 3.0, 20.0, 7.0, 18.0, 8.0, 15.0, 11.0, -20.0)
					.Track("jump", 0.0, 0.0, 7.4, 0.0, 7.6, 1.0, 7.9, -4.0, 8.3, 0.0);
			}
			else
			{
				gardenScene.Track("bub.r", 0.0, 0.0, 3.0, 0.0, 6.6, 9.0, 7.0, 9.0, 7.05, 0.0).Track("bub.x", 0.0, 104.0, 3.0, 104.0, 6.6, 140.0, 13.0, 140.0).Track("bub.y", 0.0, 20.0, 3.0, 20.0, 6.6, 18.0, 13.0, 18.0)
					.Track("pop", 0.0, 0.0, 7.0, 0.0, 7.1, 1.0, 8.0, 1.0, 8.2, 0.0)
					.Track("head", 0.0, 0.0, 7.0, 0.0, 7.2, -9.0, 8.2, 6.0, 9.0, 0.0);
			}
			break;
		case "magic":
			gardenScene.Duration = 13.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 13.0, 78.0);
			gardenScene.Track("h.x", 0.0, 340.0, 0.6, 340.0, 2.6, 150.0, 10.8, 150.0, 12.8, 340.0, 13.0, 340.0).Track("armR", 0.0, 15.0, 3.0, 80.0, 3.5, 130.0, 4.0, 80.0, 4.5, 130.0, 5.0, 80.0, 5.5, 130.0, 6.0, 80.0, 10.0, 80.0, 10.6, 15.0).Event(6.6, "outcome");
			if (outcome == "star")
			{
				gardenScene.Track("star", 0.0, 0.0, 6.0, 0.0, 7.4, 1.0, 10.4, 1.0, 10.8, 0.0).Track("jump", 0.0, 0.0, 7.4, 0.0, 7.6, 1.0, 7.9, -4.0, 8.3, 0.0);
			}
			else
			{
				gardenScene.Track("puff", 0.0, 0.0, 6.0, 0.0, 6.4, 1.0, 8.4, 1.0, 9.0, 0.0).Track("armL", 0.0, -15.0, 7.0, -15.0, 7.4, -80.0, 9.0, -80.0, 9.4, -15.0).Track("head", 0.0, 0.0, 7.0, 0.0, 7.4, 7.0, 8.0, -7.0, 8.6, 0.0);
			}
			break;
		case "yoyo":
			gardenScene.Duration = 12.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 12.0, 78.0);
			gardenScene.Track("armR", 0.0, 15.0, 1.4, 85.0, 10.0, 85.0, 10.6, 15.0).Track("yo", 0.0, 0.0, 1.6, 1.0, 10.0, 1.0, 10.4, 0.0).Event(7.0, "outcome");
			if (outcome == "tricks")
			{
				gardenScene.Track("yy", 0.0, 0.0, 2.0, 0.0, 2.6, 1.0, 3.2, 0.0, 3.8, 1.0, 4.4, 0.0, 5.0, 1.0, 5.6, 0.0, 9.6, 0.0).Track("loop", 0.0, 0.0, 6.0, 0.0, 7.6, 1.0, 8.0, 0.0).Track("jump", 0.0, 0.0, 8.0, 0.0, 8.2, 1.0, 8.5, -4.0, 8.9, 0.0);
			}
			else
			{
				gardenScene.Track("yy", 0.0, 0.0, 2.0, 0.0, 2.6, 1.0, 3.2, 0.0, 3.8, 1.0, 4.4, 0.0, 5.0, 1.0, 5.6, 0.0, 6.4, 0.6, 10.0, 0.6).Track("tangle", 0.0, 0.0, 6.2, 0.0, 6.8, 1.0, 10.0, 1.0).Track("head", 0.0, 0.0, 6.8, 0.0, 7.2, -9.0, 8.8, -9.0, 9.2, 0.0);
			}
			break;
		case "hoop":
			gardenScene.Duration = 13.0;
			gardenScene.Tracks.Remove("x");
			gardenScene.Track("x", 0.0, x, 1.2, 78.0, 13.0, 78.0);
			gardenScene.Track("bin.x", 0.0, 340.0, 0.6, 340.0, 2.6, 214.0, 10.8, 214.0, 12.8, 340.0, 13.0, 340.0).Track("wad.x", 0.0, 96.0, 4.0, 96.0, 5.6, 214.0).Track("wad.y", 0.0, 24.0, 4.0, 24.0, 4.8, 2.0, 5.6, 22.0)
				.Track("armR", 0.0, 15.0, 3.4, 60.0, 3.8, 150.0, 4.2, 15.0)
				.Event(5.8, "outcome");
			if (outcome == "in")
			{
				gardenScene.Tracks["wad.y"].Key(6.0, 28.0);
				gardenScene.Track("burst", 0.0, 0.0, 5.7, 0.0, 5.9, 1.0, 7.4, 1.0, 7.8, 0.0).Track("armL", 0.0, -15.0, 5.9, -150.0, 7.4, -150.0, 7.8, -15.0).Track("jump", 0.0, 0.0, 5.9, 0.0, 6.1, 1.0, 6.4, -5.0, 6.8, 0.0);
			}
			else
			{
				gardenScene.Tracks.Remove("wad.x");
				gardenScene.Tracks.Remove("wad.y");
				gardenScene.Track("wad.x", 0.0, 96.0, 4.0, 96.0, 5.6, 204.0, 6.4, 232.0, 7.6, 246.0, 10.8, 246.0, 12.8, 340.0).Track("wad.y", 0.0, 24.0, 4.0, 24.0, 4.8, 2.0, 5.6, 14.0, 6.4, 33.0, 13.0, 33.0).Track("head", 0.0, 0.0, 6.4, 0.0, 6.7, -8.0, 8.0, -8.0, 8.4, 0.0);
			}
			gardenScene.Arc("wad.x", 4.0).Arc("wad.y", 4.0);
			break;
		case "dominoes":
			gardenScene.Track("d.x", 0.0, 340.0, 0.6, 340.0, 2.6, 180.0, 11.8, 180.0, 13.8, 340.0, 14.0, 340.0).Track("armR", 0.0, 15.0, 3.6, 15.0, 4.0, 80.0, 4.4, 15.0).Track("fall", 0.0, 0.0, 4.2, 0.0, 7.0, (outcome == "all") ? 1.0 : 0.5, 14.0, (outcome == "all") ? 1.0 : 0.5)
				.Event(7.2, "outcome");
			if (outcome == "all")
			{
				gardenScene.Track("jump", 0.0, 0.0, 7.2, 0.0, 7.4, 1.0, 7.7, -5.0, 8.1, 0.0).Track("armL", 0.0, -15.0, 7.2, -150.0, 8.6, -150.0, 9.0, -15.0);
			}
			else
			{
				gardenScene.Track("armL", 0.0, -15.0, 7.4, -15.0, 7.8, -130.0, 9.4, -130.0, 9.8, -15.0).Track("head", 0.0, 0.0, 7.4, 0.0, 7.8, -8.0, 9.4, -8.0, 9.8, 0.0);
			}
			break;
		default:
			throw new ArgumentException("Unknown big scene: " + name);
		}
		gardenScene.Variant = outcome;
		return gardenScene;
	}

	public static double OutcomeTime(GardenScene s)
	{
		return s.Events.Find((GardenEvent v) => v.Name == "outcome")?.Time ?? (s.Duration / 2.0);
	}

	public static GardenScene Bring(string prop, double anchor, double x)
	{
		return Bring(prop, anchor, x, 78.0);
	}

	public static GardenScene Bring(string prop, double anchor, double x, double to)
	{
		return new GardenScene("bring-" + prop, 3.4).Track("x", 0.0, x, 1.2, x, 3.2, to, 3.4, to).Track("act.x", 0.0, 340.0, 0.6, 340.0, 2.6, anchor, 3.2, anchor).Track("armR", 0.0, 15.0, 0.8, 110.0, 1.6, 60.0, 2.4, 110.0, 3.2, 15.0);
	}

	public static GardenScene Swap(string from, double fromAnchor, string to, double toAnchor, double x)
	{
		return Swap(from, fromAnchor, to, toAnchor, x, 78.0);
	}

	public static GardenScene Swap(string from, double fromAnchor, string to, double toAnchor, double x, double work)
	{
		return new GardenScene("swap-" + from + "-" + to, 4.2).Track("x", 0.0, x, 0.4, x, 1.4, 78.0, 2.6, 78.0, 4.0, work, 4.2, work).Track("act.x", 0.0, fromAnchor, 0.2, fromAnchor, 1.6, 340.0, 4.0, 340.0).Track("next.x", 0.0, 340.0, 1.8, 340.0, 3.6, toAnchor, 4.0, toAnchor)
			.Track("armR", 0.0, 15.0, 0.6, 110.0, 1.6, 60.0, 2.6, 110.0, 4.0, 15.0);
	}

	public static GardenScene Walk(double from, double to)
	{
		double num = Math.Max(2.5, Math.Min(8.0, Math.Abs(to - from) / 32.0));
		return new GardenScene("walk", num).Track("x", 0.0, from, num, to);
	}

	public static GardenScene Done(string prop, double x)
	{
		return new GardenScene("done-" + prop, 1.6).Track("x", 0.0, x, 1.6, x).Track("armR", 0.0, 15.0, 0.4, 15.0, 0.8, 130.0, 1.3, 130.0, 1.6, 15.0).Track("burst", 0.0, 0.0, 0.6, 0.0, 0.9, 1.0, 1.4, 1.0, 1.6, 0.0)
			.Track("jump", 0.0, 0.0, 0.7, 0.0, 0.8, 1.0, 1.0, -3.0, 1.2, 0.0, 1.3, 0.6, 1.4, 0.0);
	}

	public static GardenScene PutAway(string prop, double anchor, double x)
	{
		return new GardenScene("away-" + prop, 2.0).Track("x", 0.0, x, 2.0, x).Track("act.x", 0.0, anchor, 0.2, anchor, 1.8, 340.0, 2.0, 340.0).Track("armR", 0.0, 15.0, 0.5, 100.0, 1.5, 100.0, 2.0, 15.0);
	}

	public static GardenScene FirstMeet()
	{
		return new GardenScene("first-meet", 6.4).Track("box.x", 0.0, 340.0, 1.1, 146.0, 1.3, 152.0, 1.45, 150.0, 6.4, 150.0).Track("x", 0.0, 340.0, 1.1, 146.0, 1.3, 152.0, 1.45, 150.0, 4.1, 150.0, 4.75, 112.0, 6.4, 112.0).Track("box.rot", 0.0, 0.0, 1.6, 0.0, 1.75, 7.0, 1.95, -7.0, 2.1, 0.0, 2.25, 6.0, 2.4, -6.0, 2.55, 0.0)
			.Track("box.lid", 0.0, 0.0, 2.75, 0.0, 2.95, 1.0, 6.4, 1.0)
			.Track("inbox", 0.0, 1.0, 3.0, 1.0, 3.45, 0.06, 3.85, 0.06, 4.05, 0.16, 4.35, 0.0, 6.4, 0.0)
			.Track("jump", 0.0, 0.0, 4.1, 0.0, 4.45, -9.0, 4.75, 0.0, 4.85, 1.6, 5.0, 0.0)
			.Track("head", 0.0, 0.0, 3.45, 0.0, 3.6, -7.0, 3.75, 7.0, 3.85, 0.0, 5.0, 0.0, 5.2, -8.0, 5.45, 8.0, 5.65, 0.0)
			.Track("armL", 0.0, -15.0, 5.6, -15.0, 5.8, -150.0, 6.0, -110.0, 6.15, -150.0, 6.4, -120.0)
			.Event(1.45, "arrive")
			.Event(2.95, "open")
			.Event(3.45, "peek")
			.Event(4.1, "jump")
			.Event(5.6, "wave")
			.Event(5.8, "outcome");
	}

	public static GardenScene IntroKick(double x)
	{
		return new GardenScene("intro-kick", 3.0).Track("x", 0.0, x, 0.9, 128.0, 1.5, 128.0, 3.0, x).Track("box.x", 0.0, 150.0, 1.15, 150.0, 1.3, 156.0, 2.2, 340.0, 3.0, 340.0).Track("box.lid", 0.0, 1.0, 3.0, 1.0)
			.Track("box.rot", 0.0, 0.0, 1.15, 0.0, 1.4, 10.0, 2.2, 20.0)
			.Track("jump", 0.0, 0.0, 0.95, 0.0, 1.05, 1.0, 1.2, -2.0, 1.35, 0.0)
			.Track("armR", 0.0, 15.0, 0.95, 15.0, 1.15, 70.0, 1.5, 15.0)
			.Event(1.2, "kick");
	}

	public static GardenScene Launch(double x)
	{
		return new GardenScene("rocket-launch", 14.0).Track("x", 0.0, x, 1.2, 78.0, 14.0, 78.0).Track("rocket.x", 0.0, 340.0, 0.6, 340.0, 3.0, 178.0, 14.0, 178.0).Track("rocket.y", 0.0, 0.0, 7.6, 0.0, 8.6, -6.0, 10.6, -60.0, 14.0, -60.0)
			.Track("flame", 0.0, 0.0, 7.0, 0.0, 7.6, 1.0, 11.0, 1.0, 11.4, 0.0)
			.Track("count", 0.0, 0.0, 4.0, 0.0, 7.0, 3.0, 14.0, 3.0)
			.Track("smoke", 0.0, 0.0, 7.0, 0.0, 7.6, 1.0, 11.0, 1.0, 12.0, 0.0)
			.Track("armL", 0.0, -15.0, 8.6, -15.0, 9.0, -150.0, 11.0, -150.0, 11.4, -15.0)
			.Track("armR", 0.0, 15.0, 8.6, 15.0, 9.0, 150.0, 11.0, 150.0, 11.4, 15.0)
			.Track("jump", 0.0, 0.0, 8.8, 0.0, 9.0, 1.0, 9.3, -5.0, 9.7, 0.0, 9.9, 1.0, 10.2, -5.0, 10.6, 0.0)
			.Event(4.0, "countdown")
			.Event(7.6, "launch")
			.Event(9.0, "outcome");
	}

	public static GardenScene Accessory(string name, double x, bool bring)
	{
		GardenScene gardenScene = new GardenScene(name, 8.0).Track("x", 0.0, x, 2.0, -32.0, 3.0, -32.0, 5.0, 140.0, 8.0, 140.0).Track(name + ".x", 0.0, bring ? (-32.0) : (x + 10.0), 2.0, -32.0, 3.0, -32.0, 5.0, bring ? 150 : (-32), 8.0, bring ? 140 : (-32)).Track(name + ".y", 0.0, 24.0, 5.0, 24.0, 6.0, (name == "headphones") ? 12 : 32, 8.0, (name == "headphones") ? 12 : 32);
		if (name == "pillow")
		{
			gardenScene.Track("sleep", 0.0, 0.0, 6.0, 0.0, 8.0, 1.0);
		}
		return gardenScene;
	}
}
