namespace WindowsBuddy.Services;

public class NetworkSnapshot
{
	public bool IsConnected { get; set; }

	public bool InternetAvailable { get; set; }

	public NetworkConnectionState ConnectionState { get; set; }

	public string ConnectionType { get; set; }

	public string SSID { get; set; }

	public string AdapterName { get; set; }

	public long LinkSpeedMbps { get; set; }

	public string IPv4 { get; set; }

	public string IPv6 { get; set; }

	public bool IsWiFi { get; set; }

	public int SignalPercent { get; set; }

	public string WiFiStandard { get; set; }

	public string ActiveAdapterId { get; set; }

	public NetworkSnapshot Clone()
	{
		return (NetworkSnapshot)MemberwiseClone();
	}
}
