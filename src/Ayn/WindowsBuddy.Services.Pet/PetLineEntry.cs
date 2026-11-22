namespace WindowsBuddy.Services.Pet;

public sealed class PetLineEntry
{
	public string Id;

	public string Text;

	public PetLineEntry(string id, string text)
	{
		Id = id;
		Text = text;
	}
}
