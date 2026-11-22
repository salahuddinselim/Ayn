namespace WindowsBuddy.Services.Pet;

public sealed class PetLine
{
	public string Id;

	public string Title;

	public string Subtitle;

	public PetLine(string id, string title, string subtitle)
	{
		Id = id;
		Title = title;
		Subtitle = subtitle;
	}
}
