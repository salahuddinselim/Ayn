using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace WindowsBuddy.Services;

public sealed class NetworkService : IDisposable
{
	private static NetworkService _instance;

	private static readonly object _instanceLock = new object();

	private NetworkSnapshot _snapshot;

	private readonly object _snapLock = new object();

	internal static Action SlowBuildForQa;

	public static NetworkService Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_instanceLock)
				{
					if (_instance == null)
					{
						_instance = new NetworkService();
					}
				}
			}
			return _instance;
		}
	}

	public event EventHandler<NetworkSnapshot> NetworkChanged;

	internal static event Action InitialSnapshotReady;

	private NetworkService()
	{
		_snapshot = new NetworkSnapshot
		{
			ConnectionState = NetworkConnectionState.Unknown,
			SignalPercent = -1,
			SSID = string.Empty,
			AdapterName = string.Empty,
			IPv4 = string.Empty,
			IPv6 = string.Empty,
			ConnectionType = "Unknown",
			WiFiStandard = string.Empty,
			ActiveAdapterId = string.Empty
		};
		ThreadStart start = delegate
		{
			using (BootTimeline.Service("network snapshot"))
			{
				try
				{
					NetworkSnapshot snapshot = BuildSnapshot();
					lock (_snapLock)
					{
						_snapshot = snapshot;
					}
					NetworkService.InitialSnapshotReady?.Invoke();
				}
				catch
				{
				}
			}
		};
		new Thread(start)
		{
			IsBackground = true,
			Name = "Network snapshot (start-up)"
		}.Start();
		try
		{
			NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
			NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
		}
		catch
		{
		}
		try
		{
			SystemEvents.PowerModeChanged += OnPowerModeChanged;
		}
		catch
		{
		}
	}

	public NetworkSnapshot GetSnapshot()
	{
		lock (_snapLock)
		{
			return _snapshot.Clone();
		}
	}

	public void RefreshAsync()
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			RebuildAndBroadcast();
		});
	}

	private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			RebuildAndBroadcast();
		});
	}

	private void OnNetworkAddressChanged(object sender, EventArgs e)
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			RebuildAndBroadcast();
		});
	}

	private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.Resume)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				Thread.Sleep(1500);
				RebuildAndBroadcast();
			});
		}
	}

	private void RebuildAndBroadcast()
	{
		try
		{
			NetworkSnapshot networkSnapshot = BuildSnapshot();
			lock (_snapLock)
			{
				_snapshot = networkSnapshot;
			}
			this.NetworkChanged?.Invoke(this, networkSnapshot.Clone());
		}
		catch
		{
		}
	}

	private static NetworkSnapshot BuildSnapshot()
	{
		SlowBuildForQa?.Invoke();
		NetworkSnapshot networkSnapshot = new NetworkSnapshot();
		networkSnapshot.ConnectionState = NetworkConnectionState.Unknown;
		networkSnapshot.SignalPercent = -1;
		networkSnapshot.SSID = string.Empty;
		networkSnapshot.AdapterName = string.Empty;
		networkSnapshot.IPv4 = string.Empty;
		networkSnapshot.IPv6 = string.Empty;
		networkSnapshot.ConnectionType = "Unknown";
		NetworkSnapshot networkSnapshot2 = networkSnapshot;
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			NetworkInterface[] array = new NetworkInterface[2];
			NetworkInterface[] array2 = new NetworkInterface[2];
			NetworkInterface[] array3 = new NetworkInterface[2];
			NetworkInterface[] array4 = allNetworkInterfaces;
			foreach (NetworkInterface networkInterface in array4)
			{
				if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
				{
					continue;
				}
				IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
				bool flag = false;
				foreach (GatewayIPAddressInformation gatewayAddress in iPProperties.GatewayAddresses)
				{
					if (gatewayAddress.Address != null && gatewayAddress.Address.AddressFamily == AddressFamily.InterNetwork && !gatewayAddress.Address.Equals(IPAddress.Any))
					{
						flag = true;
						break;
					}
				}
				if (networkInterface.Description.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0 && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 && !flag)
				{
					continue;
				}
				int num = ((!flag) ? 1 : 0);
				bool flag2 = false;
				foreach (UnicastIPAddressInformation unicastAddress in iPProperties.UnicastAddresses)
				{
					if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork && !unicastAddress.Address.Equals(IPAddress.Loopback) && !unicastAddress.Address.ToString().StartsWith("169.254"))
					{
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					continue;
				}
				if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
				{
					if (array[num] == null)
					{
						array[num] = networkInterface;
					}
				}
				else if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet || networkInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet || networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx || networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT)
				{
					if (array2[num] == null)
					{
						array2[num] = networkInterface;
					}
				}
				else if (array3[num] == null)
				{
					array3[num] = networkInterface;
				}
			}
			NetworkInterface networkInterface2 = array[0] ?? array2[0] ?? array3[0] ?? array[1] ?? array2[1] ?? array3[1];
			if (networkInterface2 == null)
			{
				networkSnapshot2.IsConnected = false;
				networkSnapshot2.ConnectionState = NetworkConnectionState.Disconnected;
				networkSnapshot2.SSID = string.Empty;
				networkSnapshot2.ConnectionType = "Disconnected";
				return networkSnapshot2;
			}
			networkSnapshot2.IsConnected = true;
			networkSnapshot2.IsWiFi = networkInterface2.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
			networkSnapshot2.AdapterName = TruncateAdapter(networkInterface2.Description, 36);
			networkSnapshot2.ActiveAdapterId = networkInterface2.Id;
			long speed = networkInterface2.Speed;
			if (speed > 0)
			{
				networkSnapshot2.LinkSpeedMbps = speed / 1000000;
			}
			if (networkSnapshot2.IsWiFi)
			{
				networkSnapshot2.ConnectionType = "Wi-Fi";
				TryGetWiFiViaNativeApi(out var ssid, out var signal, out var standard);
				networkSnapshot2.SSID = ((!string.IsNullOrEmpty(ssid)) ? ssid : networkInterface2.Name);
				networkSnapshot2.SignalPercent = signal;
				networkSnapshot2.WiFiStandard = standard ?? string.Empty;
			}
			else
			{
				networkSnapshot2.ConnectionType = "Ethernet";
				networkSnapshot2.SSID = "Ethernet";
			}
			IPInterfaceProperties iPProperties2 = networkInterface2.GetIPProperties();
			foreach (UnicastIPAddressInformation unicastAddress2 in iPProperties2.UnicastAddresses)
			{
				if (unicastAddress2.Address.AddressFamily == AddressFamily.InterNetwork && !unicastAddress2.Address.Equals(IPAddress.Loopback) && !unicastAddress2.Address.ToString().StartsWith("169.254"))
				{
					if (string.IsNullOrEmpty(networkSnapshot2.IPv4))
					{
						networkSnapshot2.IPv4 = unicastAddress2.Address.ToString();
					}
				}
				else if (unicastAddress2.Address.AddressFamily == AddressFamily.InterNetworkV6 && !unicastAddress2.Address.IsIPv6LinkLocal && !unicastAddress2.Address.IsIPv6SiteLocal && string.IsNullOrEmpty(networkSnapshot2.IPv6))
				{
					networkSnapshot2.IPv6 = unicastAddress2.Address.ToString();
				}
			}
			networkSnapshot2.InternetAvailable = NetworkInterface.GetIsNetworkAvailable();
			networkSnapshot2.ConnectionState = ((!networkSnapshot2.InternetAvailable) ? NetworkConnectionState.Limited : NetworkConnectionState.Connected);
		}
		catch
		{
			networkSnapshot2.IsConnected = NetworkInterface.GetIsNetworkAvailable();
			networkSnapshot2.ConnectionState = (networkSnapshot2.IsConnected ? NetworkConnectionState.Unknown : NetworkConnectionState.Disconnected);
		}
		return networkSnapshot2;
	}

	[DllImport("wlanapi.dll")]
	private static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

	[DllImport("wlanapi.dll")]
	private static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

	[DllImport("wlanapi.dll")]
	private static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

	[DllImport("wlanapi.dll")]
	private static extern void WlanFreeMemory(IntPtr pMemory);

	[DllImport("wlanapi.dll")]
	private static extern int WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, int opCode, IntPtr pReserved, out uint pdwDataSize, out IntPtr ppData, IntPtr pWlanOpcodeValueType);

	private static bool TryGetWiFiViaNativeApi(out string ssid, out int signal, out string standard)
	{
		ssid = string.Empty;
		signal = -1;
		standard = string.Empty;
		IntPtr phClientHandle = IntPtr.Zero;
		IntPtr ppInterfaceList = IntPtr.Zero;
		try
		{
			if (WlanOpenHandle(2u, IntPtr.Zero, out var _, out phClientHandle) != 0)
			{
				return false;
			}
			if (WlanEnumInterfaces(phClientHandle, IntPtr.Zero, out ppInterfaceList) != 0 || ppInterfaceList == IntPtr.Zero)
			{
				return false;
			}
			int num = Marshal.ReadInt32(ppInterfaceList, 0);
			for (int i = 0; i < num; i++)
			{
				IntPtr source = new IntPtr(ppInterfaceList.ToInt64() + 8 + i * 532);
				byte[] array = new byte[16];
				Marshal.Copy(source, array, 0, 16);
				Guid pInterfaceGuid = new Guid(array);
				if (WlanQueryInterface(phClientHandle, ref pInterfaceGuid, 7, IntPtr.Zero, out var _, out var ppData, IntPtr.Zero) != 0 || !(ppData != IntPtr.Zero))
				{
					continue;
				}
				try
				{
					int num2 = 520;
					int num3 = Marshal.ReadInt32(ppData, num2);
					if (num3 > 0 && num3 <= 32)
					{
						byte[] array2 = new byte[num3];
						Marshal.Copy(new IntPtr(ppData.ToInt64() + num2 + 4), array2, 0, num3);
						ssid = Encoding.UTF8.GetString(array2);
					}
					standard = WiFiStandardName(Marshal.ReadInt32(ppData, num2 + 48));
					int num4 = Marshal.ReadInt32(ppData, num2 + 56);
					if (num4 >= 0 && num4 <= 100)
					{
						signal = num4;
					}
				}
				finally
				{
					WlanFreeMemory(ppData);
				}
				if (!string.IsNullOrEmpty(ssid))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		finally
		{
			if (ppInterfaceList != IntPtr.Zero)
			{
				WlanFreeMemory(ppInterfaceList);
			}
			if (phClientHandle != IntPtr.Zero)
			{
				WlanCloseHandle(phClientHandle, IntPtr.Zero);
			}
		}
		return !string.IsNullOrEmpty(ssid);
	}

	private static string WiFiStandardName(int phyType)
	{
		return phyType switch
		{
			11 => "Wi-Fi 7", 
			10 => "Wi-Fi 6", 
			8 => "Wi-Fi 5", 
			7 => "Wi-Fi 4", 
			6 => "802.11g", 
			5 => "802.11b", 
			4 => "802.11a", 
			_ => string.Empty, 
		};
	}

	private static string TruncateAdapter(string name, int maxLen)
	{
		if (string.IsNullOrEmpty(name))
		{
			return string.Empty;
		}
		name = name.Replace("(R)", "").Replace("(TM)", "").Replace("  ", " ")
			.Trim();
		if (name.Length > maxLen)
		{
			return name.Substring(0, maxLen - 1) + "…";
		}
		return name;
	}

	public static string FormatRate(double bytesPerSec)
	{
		if (bytesPerSec >= 1048576.0)
		{
			return $"{bytesPerSec / 1048576.0:0.0} MB/s";
		}
		if (bytesPerSec >= 1024.0)
		{
			return $"{bytesPerSec / 1024.0:0} KB/s";
		}
		return $"{Math.Max(0.0, bytesPerSec):0} B/s";
	}

	public static string FormatLinkSpeed(long mbps)
	{
		if (mbps <= 0)
		{
			return "—";
		}
		if (mbps >= 1000 && mbps % 1000 == 0)
		{
			return $"{mbps / 1000} Gbps";
		}
		if (mbps >= 1000)
		{
			return $"{(double)mbps / 1000.0:0.0} Gbps";
		}
		return $"{mbps} Mbps";
	}

	public static string SignalLabel(int pct)
	{
		if (pct < 0)
		{
			return "—";
		}
		if (pct >= 80)
		{
			return "Excellent";
		}
		if (pct >= 60)
		{
			return "Good";
		}
		if (pct >= 40)
		{
			return "Fair";
		}
		return "Weak";
	}

	public void Dispose()
	{
		try
		{
			NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
			NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
		}
		catch
		{
		}
		try
		{
			SystemEvents.PowerModeChanged -= OnPowerModeChanged;
		}
		catch
		{
		}
	}
}
