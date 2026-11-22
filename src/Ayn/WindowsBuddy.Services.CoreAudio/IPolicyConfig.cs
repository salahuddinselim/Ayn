using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
public interface IPolicyConfig
{
	[PreserveSig]
	int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr ppFormat);

	[PreserveSig]
	int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bDefault, IntPtr ppFormat);

	[PreserveSig]
	int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

	[PreserveSig]
	int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr pEndpointFormat, IntPtr mixFormat);

	[PreserveSig]
	int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);

	[PreserveSig]
	int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr pmftPeriod);

	[PreserveSig]
	int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr pMode);

	[PreserveSig]
	int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);

	[PreserveSig]
	int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bFxStore, IntPtr key, IntPtr pv);

	[PreserveSig]
	int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bFxStore, IntPtr key, IntPtr pv);

	[PreserveSig]
	int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

	[PreserveSig]
	int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bVisible);
}
