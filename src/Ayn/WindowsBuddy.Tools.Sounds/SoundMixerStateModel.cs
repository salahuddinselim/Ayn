using System.Collections.Generic;

namespace WindowsBuddy.Tools.Sounds;

public class SoundMixerStateModel
{
	public double MasterVolume { get; set; }

	public List<SoundTrackConfig> Tracks { get; set; }

	public SoundMixerStateModel()
	{
		MasterVolume = 0.8;
		Tracks = new List<SoundTrackConfig>();
	}
}
