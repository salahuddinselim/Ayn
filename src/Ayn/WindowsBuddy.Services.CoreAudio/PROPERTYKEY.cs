using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PROPERTYKEY
{
	public Guid fmtid;

	public uint pid;
}
