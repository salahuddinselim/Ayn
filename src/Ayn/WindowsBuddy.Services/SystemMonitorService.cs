using System;
using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using WindowsBuddy.Services.CoreAudio;

namespace WindowsBuddy.Services;

public static class SystemMonitorService
{
	private struct SYSTEM_POWER_STATUS
	{
		public byte ACLineStatus;

		public byte BatteryFlag;

		public byte BatteryLifePercent;

		public byte SystemStatusFlag;

		public int BatteryLifeTime;

		public int BatteryFullLifeTime;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private class MEMSTATUSEX2
	{
		public uint dwLength;

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;

		public MEMSTATUSEX2()
		{
			dwLength = (uint)Marshal.SizeOf(typeof(MEMSTATUSEX2));
		}
	}

	private static volatile SystemTelemetryData _cachedTelemetry;

	private static Timer _backgroundTimer;

	private static readonly object _syncLock;

	private static bool _cachedIs24Hour;

	private static volatile bool _is24HourLoaded;

	private static int _batteryHealthPercent;

	private static DateTime _batteryHealthReadUtc;

	private static int _batteryHealthReading;

	private static long _batteryFullMWh;

	private static long _batteryDesignMWh;

	private static volatile bool _powerNotificationsRegistered;

	public static int BatteryHealthPercent => _batteryHealthPercent;

	public static event EventHandler<SystemTelemetryData> PowerChanged;

	public static event EventHandler<SystemTelemetryData> NetworkChanged;

	public static event EventHandler<SystemPerfData> PerfChanged;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

	private static bool GetIs24HourFormat()
	{
		if (_is24HourLoaded)
		{
			return _cachedIs24Hour;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\International", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("sTimeFormat");
				_cachedIs24Hour = value != null && value.ToString().IndexOf("H") >= 0;
			}
		}
		catch
		{
		}
		_is24HourLoaded = true;
		return _cachedIs24Hour;
	}

	public static SystemPerfData GetLivePerf()
	{
		SystemSample latest = SystemSampler.Latest;
		if (latest != null)
		{
			return ToPerfData(latest);
		}
		SystemSample systemSample = new SystemSample();
		try
		{
			MEMSTATUSEX2 mEMSTATUSEX = new MEMSTATUSEX2();
			if (GlobalMemoryStatusEx2(mEMSTATUSEX) && mEMSTATUSEX.ullTotalPhys != 0)
			{
				systemSample.TotalPhys = mEMSTATUSEX.ullTotalPhys;
				systemSample.AvailPhys = mEMSTATUSEX.ullAvailPhys;
				systemSample.Ram = (double)(mEMSTATUSEX.ullTotalPhys - mEMSTATUSEX.ullAvailPhys) / (double)mEMSTATUSEX.ullTotalPhys * 100.0;
				systemSample.RamAvailable = true;
				systemSample.RamSampleUtc = DateTime.UtcNow;
			}
		}
		catch
		{
		}
		return ToPerfData(systemSample);
	}

	private static SystemPerfData ToPerfData(SystemSample s)
	{
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		SystemPerfData systemPerfData = new SystemPerfData();
		systemPerfData.CpuPercent = (s.IsFresh(PerfMetric.Cpu) ? ((float)Math.Round(s.Cpu)) : float.NaN);
		systemPerfData.RamPercent = (s.IsFresh(PerfMetric.Ram) ? ((float)Math.Round(s.Ram)) : float.NaN);
		systemPerfData.DiskPercent = (s.IsFresh(PerfMetric.Disk) ? ((float)Math.Round(s.Disk)) : float.NaN);
		systemPerfData.CpuTooltip = (s.IsFresh(PerfMetric.Cpu) ? string.Format(invariantCulture, "CPU {0:0}%", s.Cpu) : "CPU unavailable");
		systemPerfData.RamTooltip = ((s.IsFresh(PerfMetric.Ram) && s.TotalPhys != 0) ? string.Format(invariantCulture, "{0:0.0} GB of {1:0.0} GB in use", (double)(s.TotalPhys - s.AvailPhys) / 1073741824.0, (double)s.TotalPhys / 1073741824.0) : "RAM unavailable");
		systemPerfData.DiskTooltip = (s.IsFresh(PerfMetric.Disk) ? string.Format(invariantCulture, "Active time {0:0}%", s.Disk) : "Disk unavailable");
		systemPerfData.TotalPhysBytes = s.TotalPhys;
		systemPerfData.AvailPhysBytes = s.AvailPhys;
		return systemPerfData;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalMemoryStatusEx2([In][Out] MEMSTATUSEX2 lpBuffer);

	public static void RefreshBatteryHealthAsync()
	{
		if ((DateTime.UtcNow - _batteryHealthReadUtc).TotalMinutes < 30.0 || Interlocked.CompareExchange(ref _batteryHealthReading, 1, 0) != 0)
		{
			return;
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				long full;
				long design;
				int num = ReadBatteryHealth(out full, out design);
				_batteryHealthReadUtc = DateTime.UtcNow;
				if (num != _batteryHealthPercent || full != _batteryFullMWh || design != _batteryDesignMWh)
				{
					_batteryFullMWh = full;
					_batteryDesignMWh = design;
					_batteryHealthPercent = num;
					SystemTelemetryData cachedTelemetry = _cachedTelemetry;
					if (cachedTelemetry != null)
					{
						cachedTelemetry.BatteryHealthPercent = num;
						cachedTelemetry.BatteryFullChargeMWh = full;
						cachedTelemetry.BatteryDesignMWh = design;
						SystemMonitorService.PowerChanged?.Invoke(null, cachedTelemetry);
					}
				}
			}
			catch
			{
			}
			finally
			{
				Interlocked.Exchange(ref _batteryHealthReading, 0);
			}
		});
	}

	private static int ReadBatteryHealth(out long full, out long design)
	{
		design = SumWmi("SELECT DesignedCapacity FROM BatteryStaticData", "DesignedCapacity");
		full = SumWmi("SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", "FullChargedCapacity");
		if (design <= 0 || full <= 0)
		{
			full = 0L;
			design = 0L;
			return -1;
		}
		return (int)Math.Round((double)full * 100.0 / (double)design);
	}

	private static long SumWmi(string query, string property)
	{
		long num = 0L;
		try
		{
			ManagementScope scope = new ManagementScope("root\\WMI");
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
			using ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get();
			foreach (ManagementBaseObject item in managementObjectCollection)
			{
				using (item)
				{
					object obj = item[property];
					if (obj != null)
					{
						num += Convert.ToInt64(obj);
					}
				}
			}
			return num;
		}
		catch
		{
			return 0L;
		}
	}

	public static void OnPowerNotificationsRegistered()
	{
		_powerNotificationsRegistered = true;
		if (_backgroundTimer != null)
		{
			_backgroundTimer.Change(-1, -1);
		}
	}

	static SystemMonitorService()
	{
		_syncLock = new object();
		_cachedIs24Hour = false;
		_is24HourLoaded = false;
		_batteryHealthPercent = -1;
		_batteryHealthReadUtc = DateTime.MinValue;
		Action value = delegate
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					SystemTelemetryData cachedTelemetry = FetchLiveTelemetryInternal();
					lock (_syncLock)
					{
						_cachedTelemetry = cachedTelemetry;
					}
				}
				catch
				{
				}
			});
		};
		NetworkService.InitialSnapshotReady += value;
		CoreAudioService.InitialBindReady += value;
		_cachedTelemetry = FetchLiveTelemetryInternal();
		SystemSampler.Sampled += delegate(object s, SystemSample e)
		{
			SystemMonitorService.PerfChanged?.Invoke(null, ToPerfData(e));
		};
		SystemSampler.Start();
		RefreshBatteryHealthAsync();
		try
		{
			CoreAudioService instance = CoreAudioService.Instance;
			instance.VolumeChanged += delegate(object s, AudioVolumeChangedEventArgs e)
			{
				if (_cachedTelemetry != null)
				{
					_cachedTelemetry.VolumePercent = e.VolumePercent;
					_cachedTelemetry.IsMuted = e.IsMuted;
				}
			};
			instance.DefaultEndpointChanged += delegate(object s, AudioEndpointChangedEventArgs e)
			{
				if (_cachedTelemetry != null)
				{
					_cachedTelemetry.AudioEndpointName = e.EndpointName;
					_cachedTelemetry.VolumePercent = e.VolumePercent;
					_cachedTelemetry.IsMuted = e.IsMuted;
				}
			};
		}
		catch
		{
		}
		try
		{
			NetworkService.Instance.NetworkChanged += delegate
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					try
					{
						SystemTelemetryData systemTelemetryData = FetchLiveTelemetryInternal();
						lock (_syncLock)
						{
							_cachedTelemetry = systemTelemetryData;
						}
						try
						{
							SystemMonitorService.NetworkChanged?.Invoke(null, systemTelemetryData);
						}
						catch
						{
						}
					}
					catch
					{
					}
				});
			};
		}
		catch
		{
		}
		try
		{
			SystemEvents.PowerModeChanged += OnPowerModeChanged;
			SystemEvents.UserPreferenceChanged += delegate
			{
				_is24HourLoaded = false;
			};
		}
		catch
		{
		}
		_backgroundTimer = new Timer(delegate
		{
			RefreshAsync();
		}, null, -1, -1);
		ScheduleNextFallbackPoll(_cachedTelemetry);
	}

	private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		RefreshAsync();
	}

	private static void ScheduleNextFallbackPoll(SystemTelemetryData data)
	{
		if (_backgroundTimer != null && !_powerNotificationsRegistered)
		{
			if (data == null || !data.HasBattery)
			{
				_backgroundTimer.Change(-1, -1);
			}
			else if (data.IsFullyCharged)
			{
				_backgroundTimer.Change(-1, -1);
			}
			else
			{
				_backgroundTimer.Change(60000, -1);
			}
		}
	}

	public static void RefreshAsync()
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				SystemTelemetryData systemTelemetryData = FetchLiveTelemetryInternal();
				lock (_syncLock)
				{
					_cachedTelemetry = systemTelemetryData;
				}
				ScheduleNextFallbackPoll(systemTelemetryData);
				RefreshBatteryHealthAsync();
				try
				{
					SystemMonitorService.PowerChanged?.Invoke(null, systemTelemetryData);
				}
				catch
				{
				}
			}
			catch
			{
			}
		});
	}

	public static bool SetMasterVolume(int percent)
	{
		try
		{
			percent = Math.Max(0, Math.Min(100, percent));
			bool flag = CoreAudioService.Instance.SetMasterVolumePercent(percent);
			if (flag && _cachedTelemetry != null)
			{
				_cachedTelemetry.VolumePercent = percent;
				_cachedTelemetry.IsMuted = percent == 0;
			}
			return flag;
		}
		catch
		{
			return false;
		}
	}

	public static SystemTelemetryData GetLiveTelemetry()
	{
		SystemTelemetryData systemTelemetryData = _cachedTelemetry;
		if (systemTelemetryData == null)
		{
			systemTelemetryData = (_cachedTelemetry = FetchLiveTelemetryInternal());
		}
		DateTime now = DateTime.Now;
		systemTelemetryData.CurrentTime = (systemTelemetryData.Is24HourFormat ? now.ToString("HH:mm") : now.ToString("h:mm tt"));
		systemTelemetryData.CurrentDate = now.ToString("ddd, d MMM yyyy");
		return systemTelemetryData;
	}

	private static SystemTelemetryData FetchLiveTelemetryInternal()
	{
		SystemTelemetryData systemTelemetryData = new SystemTelemetryData();
		systemTelemetryData.BatteryHealthPercent = _batteryHealthPercent;
		systemTelemetryData.BatteryFullChargeMWh = _batteryFullMWh;
		systemTelemetryData.BatteryDesignMWh = _batteryDesignMWh;
		if (GetSystemPowerStatus(out var lpSystemPowerStatus))
		{
			bool flag2 = (systemTelemetryData.IsACConnected = lpSystemPowerStatus.ACLineStatus == 1);
			bool flag3 = (lpSystemPowerStatus.BatteryFlag & 0x80) != 0;
			bool flag4 = (lpSystemPowerStatus.BatteryFlag & 8) != 0;
			bool flag5 = (lpSystemPowerStatus.BatteryFlag & 4) != 0;
			systemTelemetryData.HasBattery = !flag3;
			systemTelemetryData.IsCharging = systemTelemetryData.HasBattery && flag4;
			systemTelemetryData.IsCritical = systemTelemetryData.HasBattery && flag5 && !flag4;
			if (systemTelemetryData.HasBattery && lpSystemPowerStatus.BatteryLifePercent <= 100)
			{
				systemTelemetryData.BatteryPercent = lpSystemPowerStatus.BatteryLifePercent;
			}
			else
			{
				systemTelemetryData.BatteryPercent = -1;
			}
			systemTelemetryData.IsFullyCharged = flag2 && !flag4 && systemTelemetryData.BatteryPercent == 100 && systemTelemetryData.HasBattery;
			if (lpSystemPowerStatus.BatteryLifeTime > 0 && lpSystemPowerStatus.BatteryLifeTime != -1)
			{
				systemTelemetryData.BatteryLifeTimeAvailable = true;
				systemTelemetryData.BatteryLifeTimeSeconds = lpSystemPowerStatus.BatteryLifeTime;
			}
			else
			{
				systemTelemetryData.BatteryLifeTimeAvailable = false;
				systemTelemetryData.BatteryLifeTimeSeconds = 0;
			}
			if (lpSystemPowerStatus.BatteryFullLifeTime > 0 && lpSystemPowerStatus.BatteryFullLifeTime != -1)
			{
				systemTelemetryData.BatteryFullLifeTimeAvailable = true;
				systemTelemetryData.BatteryFullLifeTimeSeconds = lpSystemPowerStatus.BatteryFullLifeTime;
			}
			else
			{
				systemTelemetryData.BatteryFullLifeTimeAvailable = false;
				systemTelemetryData.BatteryFullLifeTimeSeconds = 0;
			}
			if (!systemTelemetryData.HasBattery)
			{
				systemTelemetryData.PowerLineStatus = (flag2 ? "AC Connected (Wall Power)" : "Unknown");
			}
			else if (systemTelemetryData.IsFullyCharged)
			{
				systemTelemetryData.PowerLineStatus = "Fully Charged";
			}
			else if (flag4)
			{
				systemTelemetryData.PowerLineStatus = "Charging · AC Power";
			}
			else if (flag2)
			{
				systemTelemetryData.PowerLineStatus = "Plugged in, not charging";
			}
			else
			{
				systemTelemetryData.PowerLineStatus = "Battery Discharging";
			}
		}
		else
		{
			systemTelemetryData.HasBattery = false;
			systemTelemetryData.IsACConnected = true;
			systemTelemetryData.BatteryPercent = -1;
			systemTelemetryData.PowerLineStatus = "Power status unavailable";
		}
		try
		{
			NetworkSnapshot snapshot = NetworkService.Instance.GetSnapshot();
			systemTelemetryData.IsConnected = snapshot.IsConnected;
			systemTelemetryData.InternetAvailable = snapshot.InternetAvailable;
			systemTelemetryData.NetworkConnectionState = snapshot.ConnectionState.ToString();
			systemTelemetryData.NetworkName = ((!string.IsNullOrEmpty(snapshot.SSID)) ? snapshot.SSID : snapshot.ConnectionType);
			systemTelemetryData.ConnectionType = snapshot.ConnectionType;
			systemTelemetryData.AdapterName = snapshot.AdapterName;
			systemTelemetryData.LinkSpeedMbps = snapshot.LinkSpeedMbps;
			systemTelemetryData.IPv4Address = snapshot.IPv4;
			systemTelemetryData.IPv6Address = snapshot.IPv6;
			systemTelemetryData.IsWiFi = snapshot.IsWiFi;
			systemTelemetryData.WiFiSignalPercent = snapshot.SignalPercent;
		}
		catch
		{
			systemTelemetryData.IsConnected = NetworkInterface.GetIsNetworkAvailable();
			systemTelemetryData.NetworkName = "Network";
			systemTelemetryData.ConnectionType = "Ethernet";
			systemTelemetryData.NetworkConnectionState = "Unknown";
		}
		try
		{
			CoreAudioService instance = CoreAudioService.Instance;
			systemTelemetryData.VolumePercent = instance.GetMasterVolumePercent();
			systemTelemetryData.IsMuted = instance.GetMute();
			systemTelemetryData.AudioEndpointName = instance.CurrentDeviceName;
		}
		catch
		{
			systemTelemetryData.VolumePercent = 50;
			systemTelemetryData.IsMuted = false;
			systemTelemetryData.AudioEndpointName = "Audio Endpoint";
		}
		DateTime now = DateTime.Now;
		bool flag6 = (systemTelemetryData.Is24HourFormat = GetIs24HourFormat());
		systemTelemetryData.CurrentTime = (flag6 ? now.ToString("HH:mm") : now.ToString("h:mm tt"));
		systemTelemetryData.CurrentDate = now.ToString("ddd, d MMM yyyy");
		return systemTelemetryData;
	}
}
