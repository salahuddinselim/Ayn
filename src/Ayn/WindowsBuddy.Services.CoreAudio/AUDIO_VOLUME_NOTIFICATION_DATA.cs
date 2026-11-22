using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

public struct AUDIO_VOLUME_NOTIFICATION_DATA
{
	public Guid guidEventContext;

	[MarshalAs(UnmanagedType.Bool)]
	public bool bMuted;

	public float fMasterVolume;

	public uint nChannels;
}
