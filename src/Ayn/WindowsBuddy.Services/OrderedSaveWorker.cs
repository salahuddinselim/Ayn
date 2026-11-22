using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WindowsBuddy.Services;

public sealed class OrderedSaveWorker
{
	private readonly object gate = new object();

	private readonly string path;

	private readonly Func<string> serialize;

	private long requested;

	private long completed;

	private bool running;

	private bool flushing;

	private Exception error;

	public OrderedSaveWorker(string path, Func<string> serialize)
	{
		this.path = path;
		this.serialize = serialize;
	}

	public void Request()
	{
		lock (gate)
		{
			requested++;
			if (!running)
			{
				running = true;
				ThreadPool.QueueUserWorkItem(delegate
				{
					Run();
				});
			}
		}
	}

	private void Run()
	{
		while (true)
		{
			lock (gate)
			{
				if (!flushing)
				{
					Monitor.Wait(gate, 500);
				}
				flushing = false;
			}
			long num;
			lock (gate)
			{
				num = requested;
			}
			Exception ex = null;
			try
			{
				PersistenceHelper.WriteAllTextOrThrow(path, serialize());
			}
			catch (Exception ex2)
			{
				ex = ex2;
				PersistenceHelper.ReportSaveFailure(path, ex2);
			}
			lock (gate)
			{
				completed = num;
				error = ex;
				Monitor.PulseAll(gate);
				if (num != requested)
				{
					continue;
				}
				running = false;
				break;
			}
		}
	}

	public void Flush(int timeoutMs = 250)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		lock (gate)
		{
			flushing = true;
			requested++;
			if (!running)
			{
				running = true;
				ThreadPool.QueueUserWorkItem(delegate
				{
					Run();
				});
			}
			Monitor.PulseAll(gate);
			long num = requested;
			while (completed < num)
			{
				int num2 = timeoutMs - (int)stopwatch.ElapsedMilliseconds;
				if (num2 <= 0)
				{
					throw new TimeoutException("Save still pending: " + path);
				}
				Monitor.Wait(gate, num2);
			}
			if (error != null)
			{
				throw new IOException("Save failed: " + path, error);
			}
		}
	}
}
