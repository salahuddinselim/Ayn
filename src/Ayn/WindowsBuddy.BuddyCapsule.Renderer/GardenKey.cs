namespace WindowsBuddy.BuddyCapsule.Renderer;

public struct GardenKey
{
	public double Time;

	public double Value;

	public GardenEase Ease;

	public GardenKey(double time, double value, GardenEase ease)
	{
		Time = time;
		Value = value;
		Ease = ease;
	}
}
