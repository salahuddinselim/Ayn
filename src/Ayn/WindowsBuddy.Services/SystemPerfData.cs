namespace WindowsBuddy.Services;

public class SystemPerfData
{
	public float CpuPercent { get; set; }

	public float RamPercent { get; set; }

	public float DiskPercent { get; set; }

	public string CpuTooltip { get; set; }

	public string RamTooltip { get; set; }

	public string DiskTooltip { get; set; }

	public ulong TotalPhysBytes { get; set; }

	public ulong AvailPhysBytes { get; set; }

	public long DiskTotalBytes { get; set; }

	public long DiskFreeBytes { get; set; }

	public string SystemDriveRoot { get; set; }
}
