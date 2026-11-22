namespace WindowsBuddy.Services.CoreAudio;

public sealed class AudioOutputDevice
{
	public string Id { get; set; }

	public string Name { get; set; }

	public bool IsDefault { get; set; }
}
