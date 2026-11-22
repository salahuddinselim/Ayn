using System;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class NotchLog
{
	public const string FileName = "notch.log";

	public static bool Enabled = true;

	public static void Transition(NotchPhase from, NotchPhase to, string trigger)
	{
		Write("STATE", string.Concat(from, " -> ", to, " | ", trigger));
	}

	public static void Info(string message)
	{
		Write("INFO", message);
	}

	public static void Warn(string message)
	{
		Write("WARN", message);
	}

	public static void Error(string message, Exception ex)
	{
		Write("ERROR", message + ((ex != null) ? (": " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace) : ""));
	}

	private static void Write(string level, string message)
	{
		if (!Enabled)
		{
			return;
		}
		try
		{
			LogWriter.Enqueue("notch.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message);
		}
		catch
		{
		}
	}
}
