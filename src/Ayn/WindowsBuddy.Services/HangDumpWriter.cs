using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WindowsBuddy.Services;

internal static class HangDumpWriter
{
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);

	[DllImport("dbghelp.dll", SetLastError = true)]
	private static extern bool MiniDumpWriteDump(SafeProcessHandle process, uint pid, SafeFileHandle file, uint flags, IntPtr exception, IntPtr stream, IntPtr callback);

	public static int Run(string[] args)
	{
		try
		{
			if (args.Length != 3 || !int.TryParse(args[1], out var result) || result <= 0)
			{
				return 2;
			}
			string fullPath = Path.GetFullPath(args[2]);
			string value = Path.GetFullPath(Path.Combine(AppPaths.DataDir, "logs")) + Path.DirectorySeparatorChar;
			if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fullPath).StartsWith("hang-", StringComparison.Ordinal) || !fullPath.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase))
			{
				return 2;
			}
			using (Process process = Process.GetCurrentProcess())
			{
				using Process process2 = Process.GetProcessById(result);
				if (result == process.Id || !string.Equals(process2.MainModule.FileName, process.MainModule.FileName, StringComparison.OrdinalIgnoreCase))
				{
					return 2;
				}
			}
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
			using (SafeProcessHandle safeProcessHandle = OpenProcess(1104u, inherit: false, result))
			{
				using FileStream fileStream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
				if (safeProcessHandle.IsInvalid)
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				if (!MiniDumpWriteDump(safeProcessHandle, (uint)result, fileStream.SafeFileHandle, 4096u, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}
			}
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("Diagnostic dump failed: " + ex.GetType().Name + ": " + ex.Message);
			return 1;
		}
	}
}
