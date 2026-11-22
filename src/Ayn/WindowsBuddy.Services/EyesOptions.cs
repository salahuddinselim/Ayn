namespace WindowsBuddy.Services;

public sealed class EyesOptions
{
	public bool Enabled { get; set; }

	public string Skin { get; set; }

	public string Emotion { get; set; }

	public string Companion { get; set; }

	public EyesOptions()
	{
		Skin = "ayn";
		Emotion = "auto";
		Companion = "eyes";
	}
}
