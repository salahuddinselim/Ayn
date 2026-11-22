using System;

namespace WindowsBuddy.Services.Pet;

public sealed class PetSegment
{
	public PetSegmentKind Kind;

	public string Name;

	public string Outcome;

	public string Aftermath;

	public string Activity;

	public string Drawing;

	public string[] Micros = new string[0];

	public double Seconds;

	public double SceneSeconds;

	public override string ToString()
	{
		return string.Concat(Kind, ":", Name, (Outcome != null) ? ("/" + Outcome) : "", " ", Math.Round(Seconds), "s");
	}
}
