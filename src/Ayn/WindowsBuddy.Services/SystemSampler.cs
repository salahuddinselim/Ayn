using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace WindowsBuddy.Services;

public static class SystemSampler
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private class MEMORYSTATUSEX
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

		public MEMORYSTATUSEX()
		{
			dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
		}
	}

	public const int HistoryLength = 60;

	private const int OpenIntervalMs = 1000;

	private const int ClosedIntervalMs = 5000;

	private static readonly object _lock;

	private static readonly double[][] _history;

	private static int _count;

	private static int _next;

	private static Timer _timer;

	private static int _interval;

	private static int _sampling;

	private static volatile SystemSample _latest;

	private static DateTime _cpuRetryUtc;

	private static DateTime _diskRetryUtc;

	private static int _cpuBackoff;

	private static int _diskBackoff;

	private static Timer _freshnessTimer;

	private static PerformanceCounter _cpu;

	private static PerformanceCounter _diskIdle;

	private static string _netAdapterId;

	private static NetworkInterface _netAdapter;

	private static long _prevRx;

	private static long _prevTx;

	private static DateTime _prevNetUtc;

	public static SystemSample Latest => _latest;

	public static event EventHandler<SystemSample> Sampled;

	static SystemSampler()
	{
		_lock = new object();
		_history = new double[5][];
		_interval = 5000;
		_cpuBackoff = 1;
		_diskBackoff = 1;
		_prevNetUtc = DateTime.MinValue;
		for (int i = 0; i < _history.Length; i++)
		{
			_history[i] = new double[60];
		}
		_freshnessTimer = new Timer(delegate
		{
			SystemSample latest = _latest;
			if (latest == null || (DateTime.UtcNow - latest.TimeUtc).TotalSeconds <= 5.0)
			{
				return;
			}
			EventHandler<SystemSample> sampled = SystemSampler.Sampled;
			try
			{
				sampled?.Invoke(null, latest);
			}
			catch
			{
			}
		}, null, 1000, 1000);
	}

	public static void Start()
	{
		lock (_lock)
		{
			if (_timer == null)
			{
				_timer = new Timer(OnTick, null, 0, _interval);
			}
		}
	}

	public static void SetCapsuleOpen(bool open)
	{
		lock (_lock)
		{
			int num = (open ? 1000 : 5000);
			if (_timer == null)
			{
				_interval = num;
				_timer = new Timer(OnTick, null, 0, _interval);
			}
			else if (num != _interval)
			{
				_interval = num;
				_timer.Change((!open) ? num : 0, num);
			}
		}
	}

	public static double[] History(PerfMetric metric)
	{
		lock (_lock)
		{
			double[] array = _history[(int)metric];
			double[] array2 = new double[_count];
			int num = (_next - _count + 60) % 60;
			for (int i = 0; i < _count; i++)
			{
				array2[i] = array[(num + i) % 60];
			}
			return array2;
		}
	}

	private static void OnTick(object state)
	{
		if (Interlocked.CompareExchange(ref _sampling, 1, 0) != 0)
		{
			return;
		}
		try
		{
			SystemSample systemSample = TakeSample();
			lock (_lock)
			{
				_history[0][_next] = systemSample.Cpu;
				_history[1][_next] = systemSample.Ram;
				_history[2][_next] = systemSample.Disk;
				_history[3][_next] = systemSample.Down;
				_history[4][_next] = systemSample.Up;
				_next = (_next + 1) % 60;
				if (_count < 60)
				{
					_count++;
				}
			}
			_latest = systemSample;
			SystemSampler.Sampled?.Invoke(null, systemSample);
		}
		catch
		{
		}
		finally
		{
			Interlocked.Exchange(ref _sampling, 0);
		}
	}

	private static SystemSample TakeSample()
	{
		EnsureCounters();
		SystemSample latest = _latest;
		SystemSample systemSample = new SystemSample();
		systemSample.TimeUtc = DateTime.UtcNow;
		SystemSample systemSample2 = systemSample;
		systemSample2.Cpu = double.NaN;
		systemSample2.CpuSampleUtc = latest?.CpuSampleUtc ?? DateTime.MinValue;
		if (_cpu != null)
		{
			try
			{
				systemSample2.Cpu = Clamp(_cpu.NextValue());
				systemSample2.CpuAvailable = !double.IsNaN(systemSample2.Cpu);
				systemSample2.CpuSampleUtc = DateTime.UtcNow;
			}
			catch
			{
				_cpu.Dispose();
				_cpu = null;
				ScheduleRetry(ref _cpuRetryUtc, ref _cpuBackoff);
			}
		}
		systemSample2.Ram = double.NaN;
		systemSample2.RamSampleUtc = latest?.RamSampleUtc ?? DateTime.MinValue;
		try
		{
			MEMORYSTATUSEX mEMORYSTATUSEX = new MEMORYSTATUSEX();
			if (GlobalMemoryStatusEx(mEMORYSTATUSEX) && mEMORYSTATUSEX.ullTotalPhys != 0)
			{
				systemSample2.TotalPhys = mEMORYSTATUSEX.ullTotalPhys;
				systemSample2.AvailPhys = mEMORYSTATUSEX.ullAvailPhys;
				systemSample2.Ram = Clamp((double)(mEMORYSTATUSEX.ullTotalPhys - mEMORYSTATUSEX.ullAvailPhys) / (double)mEMORYSTATUSEX.ullTotalPhys * 100.0);
				systemSample2.RamAvailable = true;
				systemSample2.RamSampleUtc = DateTime.UtcNow;
			}
		}
		catch
		{
		}
		systemSample2.Disk = double.NaN;
		systemSample2.DiskSampleUtc = latest?.DiskSampleUtc ?? DateTime.MinValue;
		if (_diskIdle != null)
		{
			try
			{
				systemSample2.Disk = Clamp(100.0 - (double)_diskIdle.NextValue());
				systemSample2.DiskAvailable = !double.IsNaN(systemSample2.Disk);
				systemSample2.DiskSampleUtc = DateTime.UtcNow;
			}
			catch
			{
				_diskIdle.Dispose();
				_diskIdle = null;
				ScheduleRetry(ref _diskRetryUtc, ref _diskBackoff);
			}
		}
		SampleNetwork(systemSample2);
		return systemSample2;
	}

	private static void EnsureCounters()
	{
		string text = ((Environment.OSVersion.Version.Build >= 26100) ? "% Processor Time" : "% Processor Utility");
		string text2 = ((text == "% Processor Time") ? "% Processor Utility" : "% Processor Time");
		if (_cpu == null && DateTime.UtcNow >= _cpuRetryUtc)
		{
			string[] array = new string[2] { text, text2 };
			foreach (string counterName in array)
			{
				try
				{
					_cpu = new PerformanceCounter("Processor Information", counterName, "_Total", readOnly: true);
					_cpu.NextValue();
				}
				catch
				{
					if (_cpu != null)
					{
						_cpu.Dispose();
					}
					_cpu = null;
					continue;
				}
				break;
			}
			if (_cpu == null)
			{
				ScheduleRetry(ref _cpuRetryUtc, ref _cpuBackoff);
			}
			else
			{
				_cpuBackoff = 1;
			}
		}
		if (_diskIdle != null || !(DateTime.UtcNow >= _diskRetryUtc))
		{
			return;
		}
		try
		{
			_diskIdle = new PerformanceCounter("PhysicalDisk", "% Idle Time", SystemDiskInstance(), readOnly: true);
			_diskIdle.NextValue();
		}
		catch
		{
			if (_diskIdle != null)
			{
				_diskIdle.Dispose();
			}
			_diskIdle = null;
		}
		if (_diskIdle == null)
		{
			ScheduleRetry(ref _diskRetryUtc, ref _diskBackoff);
		}
		else
		{
			_diskBackoff = 1;
		}
	}

	private static void ScheduleRetry(ref DateTime next, ref int backoff)
	{
		next = DateTime.UtcNow.AddSeconds(backoff);
		backoff = Math.Min(60, backoff * 2);
	}

	private static string SystemDiskInstance()
	{
		try
		{
			string b = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
			string[] instanceNames = new PerformanceCounterCategory("PhysicalDisk").GetInstanceNames();
			foreach (string text in instanceNames)
			{
				if (text == "_Total")
				{
					continue;
				}
				string[] array = text.Split(' ');
				foreach (string a in array)
				{
					if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
					{
						return text;
					}
				}
			}
		}
		catch
		{
		}
		return "_Total";
	}

	private static void SampleNetwork(SystemSample s)
	{
		NetworkSnapshot networkSnapshot = null;
		try
		{
			networkSnapshot = NetworkService.Instance.GetSnapshot();
		}
		catch
		{
		}
		if (networkSnapshot == null || !networkSnapshot.IsConnected || string.IsNullOrEmpty(networkSnapshot.ActiveAdapterId))
		{
			s.NetConnected = false;
			ResetNetBaseline(null);
			return;
		}
		s.NetConnected = true;
		try
		{
			if (_netAdapter == null || _netAdapterId != networkSnapshot.ActiveAdapterId)
			{
				ResetNetBaseline(networkSnapshot.ActiveAdapterId);
				NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				foreach (NetworkInterface networkInterface in allNetworkInterfaces)
				{
					if (networkInterface.Id == networkSnapshot.ActiveAdapterId)
					{
						_netAdapter = networkInterface;
						break;
					}
				}
				if (_netAdapter == null)
				{
					return;
				}
			}
			IPInterfaceStatistics iPStatistics = _netAdapter.GetIPStatistics();
			long bytesReceived = iPStatistics.BytesReceived;
			long bytesSent = iPStatistics.BytesSent;
			DateTime utcNow = DateTime.UtcNow;
			if (_prevNetUtc != DateTime.MinValue)
			{
				double totalSeconds = (utcNow - _prevNetUtc).TotalSeconds;
				long num = bytesReceived - _prevRx;
				long num2 = bytesSent - _prevTx;
				if (totalSeconds > 0.2 && totalSeconds < 30.0 && num >= 0 && num2 >= 0)
				{
					s.Down = (double)num / totalSeconds;
					s.Up = (double)num2 / totalSeconds;
				}
			}
			_prevRx = bytesReceived;
			_prevTx = bytesSent;
			_prevNetUtc = utcNow;
		}
		catch
		{
			ResetNetBaseline(null);
		}
	}

	private static void ResetNetBaseline(string adapterId)
	{
		_netAdapterId = adapterId;
		_netAdapter = null;
		_prevNetUtc = DateTime.MinValue;
	}

	private static double Clamp(double v)
	{
		if (double.IsNaN(v) || double.IsInfinity(v))
		{
			return double.NaN;
		}
		return Math.Max(0.0, Math.Min(100.0, v));
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX lpBuffer);
}
