using System.Diagnostics;
using System.Threading;

namespace WindowsBuddy.Services.Lifecycle;

public sealed class ProducerFence
{
	private readonly object sync = new object();

	private int inFlight;

	private int rejected;

	private bool closed;

	private long generation;

	public bool IsOpen
	{
		get
		{
			lock (sync)
			{
				return !closed;
			}
		}
	}

	public int InFlight
	{
		get
		{
			lock (sync)
			{
				return inFlight;
			}
		}
	}

	public int Rejected
	{
		get
		{
			lock (sync)
			{
				return rejected;
			}
		}
	}

	public bool TryEnter(out long token)
	{
		lock (sync)
		{
			if (closed)
			{
				rejected++;
				token = -1L;
				return false;
			}
			inFlight++;
			token = generation;
			return true;
		}
	}

	public void Exit()
	{
		lock (sync)
		{
			if (inFlight > 0)
			{
				inFlight--;
			}
			if (inFlight == 0)
			{
				Monitor.PulseAll(sync);
			}
		}
	}

	public bool IsCurrent(long token)
	{
		lock (sync)
		{
			return !closed && token == generation;
		}
	}

	public void Close()
	{
		lock (sync)
		{
			closed = true;
			generation++;
		}
	}

	public void Reopen()
	{
		lock (sync)
		{
			closed = false;
		}
	}

	public bool WaitIdle(int timeoutMs)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		lock (sync)
		{
			while (inFlight > 0)
			{
				int num = timeoutMs - (int)stopwatch.ElapsedMilliseconds;
				if (num <= 0)
				{
					return false;
				}
				Monitor.Wait(sync, num);
			}
			return true;
		}
	}
}
