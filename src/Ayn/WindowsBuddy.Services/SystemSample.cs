using System;

namespace WindowsBuddy.Services;

public sealed class SystemSample
{
	public double Cpu;

	public double Ram;

	public double Disk;

	public double Down;

	public double Up;

	public ulong TotalPhys;

	public ulong AvailPhys;

	public bool NetConnected;

	public DateTime TimeUtc;

	public DateTime CpuSampleUtc;

	public DateTime RamSampleUtc;

	public DateTime DiskSampleUtc;

	public bool CpuAvailable;

	public bool RamAvailable;

	public bool DiskAvailable;

	public bool IsFresh(PerfMetric metric)
	{
		bool flag = metric switch
		{
			PerfMetric.Ram => RamAvailable, 
			PerfMetric.Cpu => CpuAvailable, 
			_ => DiskAvailable, 
		};
		DateTime dateTime = metric switch
		{
			PerfMetric.Ram => RamSampleUtc, 
			PerfMetric.Cpu => CpuSampleUtc, 
			_ => DiskSampleUtc, 
		};
		if (flag && dateTime != DateTime.MinValue)
		{
			return (DateTime.UtcNow - dateTime).TotalSeconds <= 5.0;
		}
		return false;
	}
}
