using System;
using System.Collections.Generic;

namespace WindowsBuddy.Services.Lifecycle;

public sealed class OperationGate
{
	private readonly object sync = new object();

	private readonly Dictionary<long, OperationReceipt> pending = new Dictionary<long, OperationReceipt>();

	private readonly HashSet<long> failed = new HashSet<long>();

	private readonly HashSet<long> acknowledged = new HashSet<long>();

	private long accepted;

	private long durable;

	private bool closed;

	private bool sealedWrites;

	private bool committed;

	private bool reopenDeferred;

	private int violations;

	public bool AdmissionOpen
	{
		get
		{
			lock (sync)
			{
				return !closed;
			}
		}
	}

	public bool ReopenDeferred
	{
		get
		{
			lock (sync)
			{
				return reopenDeferred;
			}
		}
	}

	internal event Action WriteViolation;

	public event Action DeferredReopenCompleted;

	public OperationReceipt TryAccept()
	{
		lock (sync)
		{
			if (closed)
			{
				return null;
			}
			OperationReceipt operationReceipt = new OperationReceipt(this, ++accepted);
			pending.Add(operationReceipt.Revision, operationReceipt);
			return operationReceipt;
		}
	}

	public OperationReceipt Retry(long revision)
	{
		lock (sync)
		{
			if (sealedWrites || !failed.Contains(revision) || pending.ContainsKey(revision))
			{
				throw new InvalidOperationException("Revision cannot be retried.");
			}
			OperationReceipt operationReceipt = new OperationReceipt(this, revision);
			pending.Add(revision, operationReceipt);
			return operationReceipt;
		}
	}

	public void CloseAdmission()
	{
		lock (sync)
		{
			closed = true;
			reopenDeferred = false;
		}
	}

	public OperationSnapshot Snapshot()
	{
		lock (sync)
		{
			return new OperationSnapshot(accepted, durable, pending.Count, failed.Count, violations);
		}
	}

	internal void Finish(OperationReceipt receipt, bool success)
	{
		bool flag = false;
		lock (sync)
		{
			if (!pending.TryGetValue(receipt.Revision, out var value) || value != receipt)
			{
				throw new InvalidOperationException("Receipt already completed or superseded.");
			}
			pending.Remove(receipt.Revision);
			if (!success)
			{
				failed.Add(receipt.Revision);
			}
			else
			{
				failed.Remove(receipt.Revision);
				acknowledged.Add(receipt.Revision);
				while (acknowledged.Remove(durable + 1))
				{
					durable++;
				}
			}
			if (reopenDeferred && pending.Count == 0 && !committed)
			{
				reopenDeferred = false;
				sealedWrites = false;
				closed = false;
				flag = true;
			}
		}
		if (flag)
		{
			RaiseDeferredReopenCompleted();
		}
	}

	private void RaiseDeferredReopenCompleted()
	{
		Action deferredReopenCompleted = this.DeferredReopenCompleted;
		if (deferredReopenCompleted == null)
		{
			return;
		}
		Delegate[] invocationList = deferredReopenCompleted.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			Action action = (Action)invocationList[i];
			try
			{
				action();
			}
			catch
			{
			}
		}
	}

	internal void AssertWrite(OperationReceipt receipt)
	{
		bool flag;
		lock (sync)
		{
			flag = sealedWrites || !pending.TryGetValue(receipt.Revision, out var value) || value != receipt;
			if (flag)
			{
				violations++;
			}
		}
		if (!flag)
		{
			return;
		}
		this.WriteViolation?.Invoke();
		throw new InvalidOperationException("Untracked or sealed persistence write.");
	}

	internal void Seal(bool requireDurability)
	{
		lock (sync)
		{
			if (!closed || pending.Count != 0 || violations != 0 || (requireDurability && (failed.Count != 0 || durable != accepted)))
			{
				throw new InvalidOperationException("Owner has not reached its durability barrier.");
			}
			sealedWrites = true;
		}
	}

	internal void CommitSeal()
	{
		lock (sync)
		{
			if (!sealedWrites)
			{
				throw new InvalidOperationException("Unsealed owner.");
			}
			committed = true;
		}
	}

	internal void Reopen()
	{
		lock (sync)
		{
			if (committed)
			{
				throw new InvalidOperationException("Owner cannot safely resume.");
			}
			if (pending.Count != 0)
			{
				reopenDeferred = true;
				throw new AdmissionDeferredException();
			}
			reopenDeferred = false;
			sealedWrites = false;
			closed = false;
		}
	}
}
