using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceEnumerator
{
	[PreserveSig]
	int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);

	[PreserveSig]
	int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);

	int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);

	[PreserveSig]
	int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);

	[PreserveSig]
	int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
}
