using System;

namespace WindowsBuddy.Services.Pet;

public sealed class PetMoment
{
	public string Pose;

	public string Title;

	public string Subtitle;

	public PetExpression Expression;

	public bool Suggestion;

	public Action Primary;

	public string PrimaryLabel;

	public string Id;

	public string SecondaryLabel;

	public Action Secondary;

	public int Priority = 4;

	public DateTime Expires;
}
