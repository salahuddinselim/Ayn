using System;

namespace WindowsBuddy.Core;

public sealed class Backoff
{
	public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(2.0);

	private int _failures;

	private DateTime _nextTryUtc = DateTime.MinValue;

	public bool CanTry => DateTime.UtcNow >= _nextTryUtc;

	public int Failures => _failures;

	public void Success()
	{
		_failures = 0;
		_nextTryUtc = DateTime.MinValue;
	}

	public TimeSpan Failure()
	{
		_failures++;
		double value = Math.Min(MaxDelay.TotalSeconds, Math.Pow(2.0, Math.Min(_failures - 1, 10)));
		TimeSpan timeSpan = TimeSpan.FromSeconds(value);
		_nextTryUtc = DateTime.UtcNow + timeSpan;
		return timeSpan;
	}
}
