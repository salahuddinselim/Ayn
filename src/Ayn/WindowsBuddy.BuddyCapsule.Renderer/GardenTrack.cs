using System;
using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class GardenTrack
{
	public readonly List<GardenKey> Keys = new List<GardenKey>();

	public GardenTrack Key(double t, double v)
	{
		Keys.Add(new GardenKey(t, v, GardenEase.Smooth));
		return this;
	}

	public double At(double t)
	{
		if (Keys.Count == 0)
		{
			return 0.0;
		}
		for (int i = 1; i < Keys.Count; i++)
		{
			if (t < Keys[i].Time)
			{
				GardenKey gardenKey = Keys[i - 1];
				GardenKey gardenKey2 = Keys[i];
				double num = Math.Max(0.0, Math.Min(1.0, (t - gardenKey.Time) / (gardenKey2.Time - gardenKey.Time)));
				if (gardenKey2.Ease == GardenEase.Arc)
				{
					GardenKey gardenKey3 = Keys[Math.Max(0, i - 2)];
					GardenKey gardenKey4 = Keys[Math.Min(Keys.Count - 1, i + 1)];
					double num2 = gardenKey2.Time - gardenKey.Time;
					double num3 = (gardenKey2.Value - gardenKey3.Value) / (gardenKey2.Time - gardenKey3.Time) * num2;
					double num4 = (gardenKey4.Value - gardenKey.Value) / (gardenKey4.Time - gardenKey.Time) * num2;
					return (2.0 * num * num * num - 3.0 * num * num + 1.0) * gardenKey.Value + (num * num * num - 2.0 * num * num + num) * num3 + (-2.0 * num * num * num + 3.0 * num * num) * gardenKey2.Value + (num * num * num - num * num) * num4;
				}
				if (gardenKey2.Ease == GardenEase.Smooth)
				{
					num = num * num * (3.0 - 2.0 * num);
				}
				else if (gardenKey2.Ease == GardenEase.In)
				{
					num *= num;
				}
				else if (gardenKey2.Ease == GardenEase.Out)
				{
					num = 1.0 - (1.0 - num) * (1.0 - num);
				}
				return gardenKey.Value + (gardenKey2.Value - gardenKey.Value) * num;
			}
		}
		return Keys[Keys.Count - 1].Value;
	}
}
