using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[StructLayout(LayoutKind.Explicit)]
public struct PROPVARIANT
{
	[FieldOffset(0)]
	public ushort vt;

	[FieldOffset(8)]
	public IntPtr pwszVal;
}
