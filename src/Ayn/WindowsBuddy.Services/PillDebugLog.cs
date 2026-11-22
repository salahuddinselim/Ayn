using System;
using System.Diagnostics;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class PillDebugLog
{
	private const string FileName = "pill_debug.log";

	public static string LogPath => LogWriter.PathFor("pill_debug.log");

	[Conditional("DEBUG")]
	public static void Write(string category, string message)
	{
		LogWriter.Enqueue("pill_debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}");
	}
}
