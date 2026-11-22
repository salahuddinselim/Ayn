using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class GardenScene
{
	public string Name;

	public double Duration;

	public string Variant;

	public readonly Dictionary<string, GardenTrack> Tracks = new Dictionary<string, GardenTrack>();

	public readonly List<GardenEvent> Events = new List<GardenEvent>();

	public GardenScene(string name, double duration)
	{
		Name = name;
		Duration = duration;
	}

	public GardenScene Track(string name, params double[] keys)
	{
		GardenTrack gardenTrack = new GardenTrack();
		for (int i = 0; i < keys.Length; i += 2)
		{
			gardenTrack.Key(keys[i], keys[i + 1]);
		}
		Tracks.Add(name, gardenTrack);
		return this;
	}

	public GardenScene Event(double t, string name)
	{
		Events.Add(new GardenEvent
		{
			Time = t,
			Name = name
		});
		return this;
	}

	public GardenScene Arc(string name, double from)
	{
		GardenTrack gardenTrack = Tracks[name];
		for (int i = 0; i < gardenTrack.Keys.Count; i++)
		{
			if (gardenTrack.Keys[i].Time >= from)
			{
				GardenKey value = gardenTrack.Keys[i];
				value.Ease = GardenEase.Arc;
				gardenTrack.Keys[i] = value;
			}
		}
		return this;
	}

	public double At(string name, double t, double fallback)
	{
		if (!Tracks.TryGetValue(name, out var value))
		{
			return fallback;
		}
		return value.At(t);
	}
}
