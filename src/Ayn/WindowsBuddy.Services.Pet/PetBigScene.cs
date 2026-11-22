namespace WindowsBuddy.Services.Pet;

public sealed class PetBigScene
{
	public string Name;

	public string[] Outcomes;

	public string Aftermath;

	public PetBigScene(string name, string aftermath, params string[] outcomes)
	{
		Name = name;
		Aftermath = aftermath;
		Outcomes = outcomes;
	}
}
