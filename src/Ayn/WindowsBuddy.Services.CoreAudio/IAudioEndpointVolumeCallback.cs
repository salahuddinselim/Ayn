using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("65780449-D39D-47FD-9A39-F9BADBBD1432")]
public interface IAudioEndpointVolumeCallback
{
	[PreserveSig]
	int OnNotify(IntPtr pNotify);
}
