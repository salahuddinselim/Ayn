namespace WindowsBuddy.Tools.Sounds;

public class SoundTrackConfig
{
	public string Id { get; set; }

	public bool IsPlaying { get; set; }

	public double Volume { get; set; }

	public SoundTrackConfig()
	{
		Volume = 0.5;
		IsPlaying = false;
	}
}
