using System;
using System.Diagnostics;
using System.Globalization;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Message;

internal sealed class MessageTiming : IDisposable
{
	private readonly string _stage;

	private readonly long _started = Stopwatch.GetTimestamp();

	internal MessageTiming(string stage)
	{
		_stage = stage;
	}

	public void Dispose()
	{
		double num = (double)(Stopwatch.GetTimestamp() - _started) * 1000.0 / (double)Stopwatch.Frequency;
		LogWriter.Enqueue("metrics.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " message " + _stage + "=" + num.ToString("0.00", CultureInfo.InvariantCulture) + "ms" + ((num >= 16.0) ? " SLOW" : ""));
	}
}
