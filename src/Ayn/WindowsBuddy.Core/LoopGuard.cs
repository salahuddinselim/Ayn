using System.Collections.Generic;

namespace WindowsBuddy.Core;

public static class LoopGuard
{
	private static readonly object _lock = new object();

	private static readonly Dictionary<object, string> _running = new Dictionary<object, string>();

	public static int Violations { get; private set; }

	public static int Running
	{
		get
		{
			lock (_lock)
			{
				return _running.Count;
			}
		}
	}

	public static void Begin(object key, string what)
	{
		if (key == null)
		{
			return;
		}
		lock (_lock)
		{
			if (_running.ContainsKey(key))
			{
				Violations++;
				try
				{
					TransactionLogger.Warn("[LoopGuard] '" + what + "' was started while already running (violation " + Violations + ")");
					return;
				}
				catch
				{
					return;
				}
			}
			_running[key] = what;
		}
	}

	public static void End(object key)
	{
		if (key == null)
		{
			return;
		}
		lock (_lock)
		{
			_running.Remove(key);
		}
	}
}
