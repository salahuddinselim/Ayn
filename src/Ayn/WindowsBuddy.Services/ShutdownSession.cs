using System;
using System.Threading.Tasks;

namespace WindowsBuddy.Services;

public sealed class ShutdownSession
{
	public bool Running { get; private set; }

	public bool Committed { get; private set; }

	public Exception RestorationFailure { get; private set; }

	public Exception PostCommitFailure { get; private set; }

	public bool RecoveryRequired
	{
		get
		{
			if (PostCommitFailure != null)
			{
				return true;
			}
			Exception restorationFailure = RestorationFailure;
			if (restorationFailure == null)
			{
				return false;
			}
			if (restorationFailure is ShutdownRestorationException ex)
			{
				return !ex.Recovered;
			}
			return true;
		}
	}

	public async Task<bool> RunAsync(Action prepare, Func<Action> pause, Func<bool> verify, Action dispose, Action<string> report, int deadlineMs = 5000, Action beforeCommit = null, Func<bool> commitGate = null, Action reopenGate = null)
	{
		if (Running || Committed)
		{
			return false;
		}
		Running = true;
		RestorationFailure = null;
		Action resume = null;
		bool gateClosed = false;
		try
		{
			prepare();
			resume = pause();
			Task<bool> verification = Task.Run(verify);
			int num = ((await Task.WhenAny(verification, Task.Delay(deadlineMs)) != verification) ? 1 : 0);
			bool expired = (byte)num != 0;
			if (expired)
			{
				report("Shutdown is waiting for storage. Services remain paused; no data is being discarded.");
			}
			bool saved = await verification;
			if (expired || !saved)
			{
				report("Shutdown cancelled. Pending data is retained.");
				return false;
			}
			if (commitGate != null)
			{
				if (!commitGate())
				{
					report("A save failed after verification. Shutdown cancelled; pending data is retained and will be retried.");
					return false;
				}
				gateClosed = true;
			}
			beforeCommit?.Invoke();
			Committed = true;
			dispose();
			return true;
		}
		catch (Exception ex)
		{
			if (Committed)
			{
				PostCommitFailure = ex;
				report("Shutdown could not finish after your data was saved: " + ex.Message + " Ayn is retaining this process for recovery.");
				return false;
			}
			AggregateException ex2 = ex as AggregateException;
			if (resume == null && ex2 != null)
			{
				foreach (Exception innerException in ex2.InnerExceptions)
				{
					if (innerException is ShutdownRestorationException)
					{
						RestorationFailure = innerException;
					}
				}
			}
			report("Shutdown failed: " + ex.Message);
			return false;
		}
		finally
		{
			if (!Committed && gateClosed && reopenGate != null)
			{
				try
				{
					reopenGate();
				}
				catch (Exception restorationFailure)
				{
					RestorationFailure = restorationFailure;
				}
			}
			if (!Committed && resume != null)
			{
				try
				{
					resume();
				}
				catch (Exception ex3)
				{
					Exception ex5 = (RestorationFailure = ex3);
					try
					{
						report("Shutdown cancelled, but some services could not resume and remain paused to protect pending data: " + ex5.Message);
					}
					catch
					{
					}
				}
			}
			Running = false;
		}
	}
}
