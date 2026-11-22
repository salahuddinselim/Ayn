namespace WindowsBuddy.Services;

public class SystemTelemetryData
{
	public bool HasBattery { get; set; }

	public int BatteryPercent { get; set; }

	public bool IsCharging { get; set; }

	public bool IsFullyCharged { get; set; }

	public bool IsACConnected { get; set; }

	public bool IsCritical { get; set; }

	public bool BatteryLifeTimeAvailable { get; set; }

	public int BatteryLifeTimeSeconds { get; set; }

	public bool BatteryFullLifeTimeAvailable { get; set; }

	public int BatteryHealthPercent { get; set; }

	public long BatteryFullChargeMWh { get; set; }

	public long BatteryDesignMWh { get; set; }

	public int BatteryFullLifeTimeSeconds { get; set; }

	public string PowerLineStatus { get; set; }

	public bool IsConnected { get; set; }

	public bool InternetAvailable { get; set; }

	public string NetworkConnectionState { get; set; }

	public string NetworkName { get; set; }

	public string ConnectionType { get; set; }

	public string AdapterName { get; set; }

	public long LinkSpeedMbps { get; set; }

	public string IPv4Address { get; set; }

	public string IPv6Address { get; set; }

	public bool IsWiFi { get; set; }

	public int WiFiSignalPercent { get; set; }

	public int VolumePercent { get; set; }

	public bool IsMuted { get; set; }

	public string AudioEndpointName { get; set; }

	public string CurrentTime { get; set; }

	public string CurrentDate { get; set; }

	public bool Is24HourFormat { get; set; }

	public SystemTelemetryData()
	{
		BatteryPercent = -1;
	}
}
