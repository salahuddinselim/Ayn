using System;

namespace WindowsBuddy.Services.Pet;

public sealed class PetContext
{
	public DateTime Now;

	public string Category = "other";

	public bool Music;

	public string Weather;

	public double Continuous;

	public bool Typing;

	public double Affection = 0.5;

	public string Birthday;

	public bool Charging;

	public bool LowBattery;
}
