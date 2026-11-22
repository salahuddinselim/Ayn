using System;
using System.Threading;

namespace WindowsBuddy.Core;

public static class Guard
{
	private static long _lastReportTicks;

	private static int _burst;

	public static bool Run(string source, Action action)
	{
		if (action == null)
		{
			return true;
		}
		try
		{
			action();
			return true;
		}
		catch (ThreadAbortException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			if (ex2 is OutOfMemoryException || ex2 is StackOverflowException)
			{
				throw;
			}
			Report(source, ex2);
			return false;
		}
	}

	public static T Run<T>(string source, Func<T> func, T fallback)
	{
		if (func == null)
		{
			return fallback;
		}
		try
		{
			return func();
		}
		catch (ThreadAbortException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			if (ex2 is OutOfMemoryException || ex2 is StackOverflowException)
			{
				throw;
			}
			Report(source, ex2);
			return fallback;
		}
	}

	public static void Background(string source, Action action)
	{
		try
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				Run(source, action);
			});
		}
		catch (Exception ex)
		{
			Report(source, ex);
		}
	}

	private static void Report(string source, Exception ex)
	{
		long ticks = DateTime.UtcNow.Ticks;
		if (ticks - Interlocked.Read(ref _lastReportTicks) > 100000000)
		{
			Interlocked.Exchange(ref _burst, 0);
		}
		Interlocked.Exchange(ref _lastReportTicks, ticks);
		if (Interlocked.Increment(ref _burst) > 20)
		{
			return;
		}
		try
		{
			TransactionLogger.Warn("[Guard] " + (source ?? "?") + " failed: " + ex.GetType().Name + " 0x" + ex.HResult.ToString("X8"));
		}
		catch
		{
		}
	}
}
