using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services.CoreAudio;

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
public interface IMMDevice
{
	[PreserveSig]
	int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);

	[PreserveSig]
	int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);

	[PreserveSig]
	int GetId(out IntPtr ppstrId);

	[PreserveSig]
	int GetState(out int pdwState);
}
