using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class HelperProcessJob
{
	private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
	{
		public long PerProcessUserTimeLimit;

		public long PerJobUserTimeLimit;

		public uint LimitFlags;

		public UIntPtr MinimumWorkingSetSize;

		public UIntPtr MaximumWorkingSetSize;

		public uint ActiveProcessLimit;

		public UIntPtr Affinity;

		public uint PriorityClass;

		public uint SchedulingClass;
	}

	private struct IO_COUNTERS
	{
		public ulong ReadOperationCount;

		public ulong WriteOperationCount;

		public ulong OtherOperationCount;

		public ulong ReadTransferCount;

		public ulong WriteTransferCount;

		public ulong OtherTransferCount;
	}

	private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
	{
		public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;

		public IO_COUNTERS IoInfo;

		public UIntPtr ProcessMemoryLimit;

		public UIntPtr JobMemoryLimit;

		public UIntPtr PeakProcessMemoryUsed;

		public UIntPtr PeakJobMemoryUsed;
	}

	private const int JobObjectExtendedLimitInformation = 9;

	private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 8192u;

	private static readonly object Sync = new object();

	private static IntPtr _job;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateJobObject(IntPtr attributes, string name);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int length);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool result);

	private static IntPtr Job()
	{
		lock (Sync)
		{
			if (_job != IntPtr.Zero)
			{
				return _job;
			}
			IntPtr intPtr = CreateJobObject(IntPtr.Zero, null);
			if (intPtr == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = default(JOBOBJECT_EXTENDED_LIMIT_INFORMATION);
			info.BasicLimitInformation.LimitFlags = 8192u;
			if (!SetInformationJobObject(intPtr, 9, ref info, Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION))))
			{
				TransactionLogger.Warn("Helper job: limits not set (" + Marshal.GetLastWin32Error() + ").");
				return IntPtr.Zero;
			}
			_job = intPtr;
			return _job;
		}
	}

	public static bool Adopt(Process helper)
	{
		try
		{
			if (helper == null || helper.HasExited)
			{
				return false;
			}
			IntPtr intPtr = Job();
			if (intPtr == IntPtr.Zero)
			{
				return false;
			}
			if (AssignProcessToJobObject(intPtr, helper.Handle))
			{
				return true;
			}
			TransactionLogger.Warn("Helper job: could not adopt a helper (" + Marshal.GetLastWin32Error() + ").");
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Helper job: " + ex.GetType().Name);
		}
		return false;
	}

	internal static bool Contains(Process process)
	{
		try
		{
			bool result;
			return _job != IntPtr.Zero && IsProcessInJob(process.Handle, _job, out result) && result;
		}
		catch
		{
			return false;
		}
	}

	public static void EndAll(string reason)
	{
		lock (Sync)
		{
			if (_job == IntPtr.Zero)
			{
				return;
			}
			try
			{
				TerminateJobObject(_job, 0u);
				TransactionLogger.Info("Helper processes ended (" + reason + ").");
			}
			catch
			{
			}
		}
	}
}
