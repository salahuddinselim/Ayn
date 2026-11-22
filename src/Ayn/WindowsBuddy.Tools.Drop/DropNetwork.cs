using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Tools.Drop;

public static class DropNetwork
{
	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsDual)]
	[Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B")]
	private interface INetworkListManager
	{
		[return: MarshalAs(UnmanagedType.IUnknown)]
		object GetNetworks(int flags);

		[return: MarshalAs(UnmanagedType.IUnknown)]
		object GetNetwork(Guid networkId);

		IEnumNetworkConnections GetNetworkConnections();
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsDual)]
	[Guid("DCB00006-570F-4A9B-8D69-199FDBA5723B")]
	private interface IEnumNetworkConnections
	{
		[return: MarshalAs(UnmanagedType.IUnknown)]
		object NewEnum();

		[PreserveSig]
		int Next(uint count, out INetworkConnection connection, out uint fetched);
	}

	[ComImport]
	[Guid("DCB00005-570F-4A9B-8D69-199FDBA5723B")]
	[InterfaceType(ComInterfaceType.InterfaceIsDual)]
	private interface INetworkConnection
	{
		INetwork GetNetwork();

		short IsConnectedToInternet();

		short IsConnected();

		int GetConnectivity();

		Guid GetConnectionId();

		Guid GetAdapterId();
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsDual)]
	[Guid("DCB00002-570F-4A9B-8D69-199FDBA5723B")]
	private interface INetwork
	{
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetName();

		void SetName([MarshalAs(UnmanagedType.BStr)] string name);

		[return: MarshalAs(UnmanagedType.BStr)]
		string GetDescription();

		void SetDescription([MarshalAs(UnmanagedType.BStr)] string description);

		Guid GetNetworkId();

		int GetDomainType();

		[return: MarshalAs(UnmanagedType.IUnknown)]
		object GetNetworkConnections();

		void GetTimeCreatedAndConnected(out uint createdLow, out uint createdHigh, out uint connectedLow, out uint connectedHigh);

		short IsConnectedToInternet();

		short IsConnected();

		int GetConnectivity();

		int GetCategory();
	}

	public const string PublicMessage = "Your network is set to Public; switch to Private to use Ayn Drop";

	private const int IfRow2Size = 1352;

	private const int IfRow2Index = 8;

	private const int IfRow2Flags = 1152;

	private static readonly string[] VirtualMarks = new string[20]
	{
		"vpn", "virtual", "hyper-v", "vethernet", "wsl", "tunnel", "wireguard", "tap-", "tailscale", "zerotier",
		"vmware", "virtualbox", "vbox", "npcap", "wi-fi direct", "loopback", "bluetooth", "personal area network", "docker", "hamachi"
	};

	public static bool IsPrivate(IPAddress ip)
	{
		if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}
		byte[] addressBytes = ip.GetAddressBytes();
		if (addressBytes[0] != 10 && (addressBytes[0] != 172 || addressBytes[1] < 16 || addressBytes[1] > 31))
		{
			if (addressBytes[0] == 192)
			{
				return addressBytes[1] == 168;
			}
			return false;
		}
		return true;
	}

	public static bool IsVirtual(string description)
	{
		string @object = (description ?? "").ToLowerInvariant();
		return VirtualMarks.Any(@object.Contains);
	}

	public static bool Eligible(NetworkInterfaceType type, string description, OperationalStatus status)
	{
		if (status == OperationalStatus.Up && (type == NetworkInterfaceType.Ethernet || type == NetworkInterfaceType.Wireless80211))
		{
			return !IsVirtual(description);
		}
		return false;
	}

	public static List<DropAdapter> GetAdapters()
	{
		return GetAdapters(null);
	}

	public static List<DropAdapter> GetAdapters(Action<string> explain)
	{
		List<DropNic> list = new List<DropNic>();
		int num = BestInterface();
		NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
		foreach (NetworkInterface networkInterface in allNetworkInterfaces)
		{
			DropNic dropNic = new DropNic();
			dropNic.Id = networkInterface.Id;
			dropNic.Name = networkInterface.Name;
			dropNic.Description = networkInterface.Description;
			dropNic.Type = networkInterface.NetworkInterfaceType;
			dropNic.Status = networkInterface.OperationalStatus;
			dropNic.Addresses = new string[0];
			dropNic.Gateways = new string[0];
			dropNic.IfIndex = -1;
			DropNic dropNic2 = dropNic;
			try
			{
				IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
				dropNic2.Addresses = (from a in iPProperties.UnicastAddresses
					where a.Address.AddressFamily == AddressFamily.InterNetwork
					select a.Address.ToString()).ToArray();
				dropNic2.Gateways = (from g in iPProperties.GatewayAddresses
					where g.Address.AddressFamily == AddressFamily.InterNetwork
					select g.Address.ToString()).ToArray();
				IPv4InterfaceProperties iPv4Properties = iPProperties.GetIPv4Properties();
				if (iPv4Properties != null)
				{
					dropNic2.IfIndex = iPv4Properties.Index;
				}
			}
			catch
			{
			}
			dropNic2.Hardware = IsHardware(dropNic2.IfIndex);
			dropNic2.DefaultRoute = dropNic2.IfIndex >= 0 && dropNic2.IfIndex == num;
			list.Add(dropNic2);
		}
		string error;
		Dictionary<string, DropProfile> profiles = ReadProfiles(out error);
		if (error != null)
		{
			explain?.Invoke("Network List Manager unavailable (" + error + "): every adapter fails closed");
		}
		return Classify(list, profiles, explain);
	}

	public static List<DropAdapter> Classify(IEnumerable<DropNic> nics, IDictionary<string, DropProfile> profiles, Action<string> explain)
	{
		List<DropAdapter> list = new List<DropAdapter>();
		foreach (DropNic nic in nics)
		{
			string who = string.Concat(nic.Name ?? "?", " (", nic.Type, ", ifIndex ", nic.IfIndex, ")");
			if (nic.Status != OperationalStatus.Up)
			{
				Say(explain, who, "rejected: not connected");
				continue;
			}
			if (nic.Type != NetworkInterfaceType.Ethernet && nic.Type != NetworkInterfaceType.Wireless80211)
			{
				Say(explain, who, "rejected: not Wi-Fi or Ethernet");
				continue;
			}
			if (IsVirtual(nic.Description + " " + nic.Name))
			{
				Say(explain, who, "rejected: virtual/VPN adapter");
				continue;
			}
			if (nic.Hardware == false)
			{
				Say(explain, who, "rejected: not a hardware adapter");
				continue;
			}
			IPAddress parsed;
			List<string> list2 = (nic.Addresses ?? new string[0]).Where((string a) => IPAddress.TryParse(a, out parsed) && IsPrivate(parsed)).ToList();
			if (list2.Count == 0)
			{
				Say(explain, who, "rejected: no private IPv4 address");
				continue;
			}
			bool flag = (nic.Gateways ?? new string[0]).Any((string g) => IPAddress.TryParse(g, out parsed) && IsPrivate(parsed));
			if (!flag)
			{
				Say(explain, who, "rejected: no IPv4 default gateway");
				continue;
			}
			DropProfile value = null;
			profiles?.TryGetValue(Key(nic.Id), out value);
			bool flag2 = value != null && value.Category == 1;
			string text = ((value == null) ? "no profile" : ((value.Category == 1) ? "Private" : ((value.Category == 2) ? "Domain" : "Public")));
			Say(explain, who, (flag2 ? "accepted" : "listed as not private") + ": profile " + text + ", " + Mask(list2[0]) + ", default gateway" + (nic.DefaultRoute ? ", active default route" : ""));
			foreach (string item in list2)
			{
				list.Add(new DropAdapter
				{
					Id = nic.Id,
					Name = nic.Name,
					Address = item,
					Private = flag2,
					Gateway = flag,
					DefaultRoute = nic.DefaultRoute
				});
			}
		}
		return OrderAdapters(list);
	}

	public static DropNetworkState Assess(IList<DropAdapter> adapters)
	{
		if (adapters == null || adapters.Count == 0)
		{
			return DropNetworkState.NoNetwork;
		}
		if (adapters.Any((DropAdapter a) => a.Private))
		{
			return DropNetworkState.Ready;
		}
		return DropNetworkState.Public;
	}

	public static List<DropAdapter> OrderAdapters(IEnumerable<DropAdapter> adapters)
	{
		return (from a in adapters
			orderby a.Private descending, a.DefaultRoute descending, a.Gateway descending, a.Name
			select a).ToList();
	}

	public static DropAdapter ChooseAdapter(IEnumerable<DropAdapter> adapters, string selectedAddress)
	{
		List<DropAdapter> source = OrderAdapters(adapters);
		return source.FirstOrDefault((DropAdapter a) => a.Private && a.Address == selectedAddress) ?? source.FirstOrDefault((DropAdapter a) => a.Private);
	}

	[DllImport("iphlpapi.dll")]
	private static extern int GetIfEntry2(IntPtr row);

	[DllImport("iphlpapi.dll")]
	private static extern int GetBestInterface(uint destination, out uint index);

	public static bool? IsHardware(int ifIndex)
	{
		if (ifIndex < 0)
		{
			return null;
		}
		IntPtr intPtr = Marshal.AllocHGlobal(1352);
		try
		{
			for (int i = 0; i < 1352; i++)
			{
				Marshal.WriteByte(intPtr, i, 0);
			}
			Marshal.WriteInt32(intPtr, 8, ifIndex);
			if (GetIfEntry2(intPtr) != 0)
			{
				return null;
			}
			return (Marshal.ReadByte(intPtr, 1152) & 1) != 0;
		}
		catch
		{
			return null;
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}

	public static int BestInterface()
	{
		try
		{
			uint index;
			return (GetBestInterface(16843009u, out index) == 0) ? ((int)index) : (-1);
		}
		catch
		{
			return -1;
		}
	}

	private static string Key(string id)
	{
		return (id ?? "").Trim('{', '}').ToUpperInvariant();
	}

	private static string Mask(string ip)
	{
		int num = ip.LastIndexOf('.');
		if (num >= 0)
		{
			return ip.Substring(0, num) + ".x";
		}
		return ip;
	}

	private static void Say(Action<string> explain, string who, string why)
	{
		explain?.Invoke("Drop adapter " + who + " " + why);
	}

	public static Dictionary<string, DropProfile> ReadProfiles(out string error)
	{
		error = null;
		Dictionary<string, DropProfile> dictionary = new Dictionary<string, DropProfile>(StringComparer.OrdinalIgnoreCase);
		INetworkListManager networkListManager = null;
		IEnumNetworkConnections enumNetworkConnections = null;
		try
		{
			networkListManager = (INetworkListManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B")));
			enumNetworkConnections = networkListManager.GetNetworkConnections();
			INetworkConnection connection;
			uint fetched;
			while (enumNetworkConnections.Next(1u, out connection, out fetched) == 0 && fetched != 0 && connection != null)
			{
				INetwork network = null;
				try
				{
					network = connection.GetNetwork();
					string name = null;
					try
					{
						name = network.GetName();
					}
					catch
					{
					}
					dictionary[Key(connection.GetAdapterId().ToString("D"))] = new DropProfile
					{
						Name = name,
						Category = network.GetCategory()
					};
				}
				catch (Exception ex)
				{
					error = ex.GetType().Name;
				}
				finally
				{
					if (network != null)
					{
						Marshal.ReleaseComObject(network);
					}
					Marshal.ReleaseComObject(connection);
				}
			}
		}
		catch (Exception ex2)
		{
			error = ex2.GetType().Name;
			return null;
		}
		finally
		{
			if (enumNetworkConnections != null)
			{
				Marshal.ReleaseComObject(enumNetworkConnections);
			}
			if (networkListManager != null)
			{
				Marshal.ReleaseComObject(networkListManager);
			}
		}
		return dictionary;
	}
}
