using System;

namespace WindowsBuddy.Core;

public static class TransactionLogger
{
	private const string FileName = "engine.log";

	public static string LogFilePath => LogWriter.PathFor("engine.log");

	public static void Info(string message)
	{
		Log("INFO", message);
	}

	public static void Success(string message)
	{
		Log("SUCCESS", message);
	}

	public static void Warn(string message)
	{
		Log("WARN", message);
	}

	public static void Error(string message, Exception ex = null)
	{
		string message2 = ((ex != null) ? $"{message} -> {ex.Message}" : message);
		Log("ERROR", message2);
	}

	private static void Log(string level, string message)
	{
		string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
		LogWriter.Enqueue("engine.log", line);
	}
}
