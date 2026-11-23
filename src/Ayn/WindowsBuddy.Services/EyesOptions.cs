namespace WindowsBuddy.Services;

public sealed class EyesOptions
{
	public bool Enabled { get; set; }

	public string Skin { get; set; }

	public EyesOptions()
	{
		Skin = "ayn";
	}
}
