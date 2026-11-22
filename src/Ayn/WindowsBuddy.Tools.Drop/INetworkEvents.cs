using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Tools.Drop;

[ComImport]
[ComVisible(true)]
[Guid("DCB00004-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INetworkEvents
{
	void NetworkAdded(Guid networkId);

	void NetworkDeleted(Guid networkId);

	void NetworkConnectivityChanged(Guid networkId, int connectivity);

	void NetworkPropertyChanged(Guid networkId, int flags);
}
