using System.Threading;

namespace WindowsBuddy.Services;

public sealed class CallbackLifetime
{
	private int epoch;

	private int paused;

	private int stopped;

	public int Capture()
	{
		return Volatile.Read(ref epoch);
	}

	public bool Accepts(int token)
	{
		if (token == Capture() && Volatile.Read(ref paused) == 0)
		{
			return Volatile.Read(ref stopped) == 0;
		}
		return false;
	}

	public void Pause()
	{
		Interlocked.Exchange(ref paused, 1);
		Interlocked.Increment(ref epoch);
	}

	public void Resume()
	{
		Interlocked.Increment(ref epoch);
		Interlocked.Exchange(ref paused, 0);
	}

	public void Stop()
	{
		Interlocked.Exchange(ref stopped, 1);
		Pause();
	}
}
