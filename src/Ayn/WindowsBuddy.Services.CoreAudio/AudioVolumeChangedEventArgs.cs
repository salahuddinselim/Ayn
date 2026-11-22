using System;

namespace WindowsBuddy.Services.CoreAudio;

public class AudioVolumeChangedEventArgs : EventArgs
{
	public float MasterVolumeScalar { get; set; }

	public int VolumePercent { get; set; }

	public bool IsMuted { get; set; }

	public Guid EventContext { get; set; }

	public AudioVolumeChangedEventArgs(float scalar, bool muted, Guid context)
	{
		MasterVolumeScalar = scalar;
		VolumePercent = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, scalar)) * 100.0);
		IsMuted = muted;
		EventContext = context;
	}
}
