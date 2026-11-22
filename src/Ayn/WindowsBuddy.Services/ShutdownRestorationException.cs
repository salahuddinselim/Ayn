using System;
using System.Collections.Generic;
using System.Linq;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Services;

public sealed class ShutdownRestorationException : AggregateException
{
	private readonly object sync = new object();

	private readonly HashSet<OperationGate> outstanding;

	private readonly bool hard;

	public bool HardFailure => hard;

	public int OutstandingOwners
	{
		get
		{
			lock (sync)
			{
				return outstanding.Count;
			}
		}
	}

	public bool Recovered
	{
		get
		{
			lock (sync)
			{
				return !hard && outstanding.Count == 0;
			}
		}
	}

	public ShutdownRestorationException(IEnumerable<Exception> failures, IEnumerable<OperationGate> deferred)
		: base("Shutdown restoration incomplete.", failures)
	{
		hard = deferred == null;
		outstanding = new HashSet<OperationGate>(deferred ?? Enumerable.Empty<OperationGate>());
		OperationGate[] array = outstanding.ToArray();
		foreach (OperationGate operationGate in array)
		{
			OperationGate owner = operationGate;
			Action latch = null;
			latch = delegate
			{
				lock (sync)
				{
					outstanding.Remove(owner);
				}
				owner.DeferredReopenCompleted -= latch;
			};
			owner.DeferredReopenCompleted += latch;
			if (!owner.ReopenDeferred && owner.AdmissionOpen)
			{
				latch();
			}
		}
	}
}
