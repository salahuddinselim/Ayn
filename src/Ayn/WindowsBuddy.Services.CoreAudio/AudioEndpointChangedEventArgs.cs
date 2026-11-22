using System;

namespace WindowsBuddy.Services.CoreAudio;

public class AudioEndpointChangedEventArgs : EventArgs
{
	public string EndpointName { get; set; }

	public int VolumePercent { get; set; }

	public bool IsMuted { get; set; }

	public AudioEndpointChangedEventArgs(string name, int volume, bool muted)
	{
		EndpointName = name;
		VolumePercent = volume;
		IsMuted = muted;
	}
}
