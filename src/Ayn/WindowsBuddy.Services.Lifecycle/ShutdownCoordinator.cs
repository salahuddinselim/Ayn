using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsBuddy.Services.Lifecycle;

public sealed class ShutdownCoordinator
{
	private readonly ParticipantRegistry registry;

	private int busy;

	private int state;

	private readonly HashSet<OperationGate> observed = new HashSet<OperationGate>();

	public ShutdownState State => (ShutdownState)Volatile.Read(ref state);

	public Exception Failure { get; private set; }

	public ShutdownCoordinator(ParticipantRegistry registry)
	{
		this.registry = registry;
	}

	private void SetState(ShutdownState value)
	{
		Volatile.Write(ref state, (int)value);
	}

	public async Task<bool> ShutdownAsync(CancellationToken cancellation)
	{
		if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
		{
			return false;
		}
		List<IShutdownParticipant> attempted = new List<IShutdownParticipant>();
		IShutdownParticipant[] owners = null;
		bool committed = false;
		try
		{
			try
			{
				if (State != 0)
				{
					return false;
				}
				Failure = null;
				owners = registry.Freeze();
				IShutdownParticipant[] array = owners;
				foreach (IShutdownParticipant shutdownParticipant in array)
				{
					if (!observed.Add(shutdownParticipant.Operations))
					{
						continue;
					}
					shutdownParticipant.Operations.WriteViolation += delegate
					{
						if (State == ShutdownState.Committed || State == ShutdownState.Terminated)
						{
							Failure = new InvalidOperationException("Postcommit persistence invariant violated.");
							SetState(ShutdownState.RecoveryRequired);
						}
					};
				}
				SetState(ShutdownState.Preparing);
				int num = default(int);
				IShutdownParticipant[] array2 = default(IShutdownParticipant[]);
				int num2 = default(int);
				if (num != 0)
				{
					array2 = owners;
					num2 = 0;
					goto IL_022c;
				}
				num = -1;
				goto IL_020f;
				IL_022c:
				IShutdownParticipant owner4 = default(IShutdownParticipant);
				if (num2 < array2.Length)
				{
					owner4 = array2[num2];
					attempted.Add(owner4);
					cancellation.ThrowIfCancellationRequested();
					goto IL_020f;
				}
				SetState(ShutdownState.Quiescing);
				IShutdownParticipant[] array3 = owners;
				foreach (IShutdownParticipant shutdownParticipant2 in array3)
				{
					shutdownParticipant2.Operations.CloseAdmission();
				}
				IShutdownParticipant[] array4 = default(IShutdownParticipant[]);
				int num3 = default(int);
				if (num != 1)
				{
					array4 = owners;
					num3 = 0;
					goto IL_0342;
				}
				num = -1;
				goto IL_0325;
				IL_020f:
				await owner4.PrepareAsync(cancellation);
				num2++;
				goto IL_022c;
				IL_0342:
				IShutdownParticipant owner3 = default(IShutdownParticipant);
				if (num3 < array4.Length)
				{
					owner3 = array4[num3];
					cancellation.ThrowIfCancellationRequested();
					goto IL_0325;
				}
				SetState(ShutdownState.Verifying);
				foreach (IShutdownParticipant owner2 in owners.OrderBy((IShutdownParticipant p) => (p.Classification == PersistenceClass.RegenerableCache) ? 1 : 0))
				{
					cancellation.ThrowIfCancellationRequested();
					if (owner2.Classification != PersistenceClass.RegenerableCache)
					{
						await owner2.VerifyAsync(cancellation);
					}
				}
				cancellation.ThrowIfCancellationRequested();
				IShutdownParticipant[] array5 = owners;
				foreach (IShutdownParticipant shutdownParticipant3 in array5)
				{
					shutdownParticipant3.Operations.Seal(shutdownParticipant3.Classification != PersistenceClass.RegenerableCache && shutdownParticipant3.Classification != PersistenceClass.Diagnostics);
				}
				IShutdownParticipant[] array6 = owners;
				foreach (IShutdownParticipant shutdownParticipant4 in array6)
				{
					shutdownParticipant4.Operations.CommitSeal();
				}
				committed = true;
				SetState(ShutdownState.Committed);
				if (num == 3)
				{
					goto IL_059c;
				}
				IShutdownParticipant[] array7 = owners;
				int num4 = 0;
				goto IL_05b9;
				IL_05b9:
				IShutdownParticipant owner = default(IShutdownParticipant);
				if (num4 < array7.Length)
				{
					owner = array7[num4];
					goto IL_059c;
				}
				if (owners.Any((IShutdownParticipant p) => p.Operations.Snapshot().Violations != 0))
				{
					throw new InvalidOperationException("Post-barrier write violation.");
				}
				if (Interlocked.CompareExchange(ref state, 5, 4) != 4)
				{
					throw new InvalidOperationException("Shutdown entered recovery during disposal.");
				}
				return true;
				IL_059c:
				await owner.DisposeAsync();
				num4++;
				goto IL_05b9;
				IL_0325:
				await owner3.QuiesceAsync(cancellation);
				num3++;
				goto IL_0342;
			}
			catch (Exception failure)
			{
				Failure = failure;
				if (committed)
				{
					SetState(ShutdownState.RecoveryRequired);
					return false;
				}
			}
			if (owners != null)
			{
				IShutdownParticipant[] array8 = owners;
				foreach (IShutdownParticipant shutdownParticipant5 in array8)
				{
					shutdownParticipant5.Operations.CloseAdmission();
				}
			}
			bool restored = true;
			for (int i = attempted.Count - 1; i >= 0; i--)
			{
				try
				{
					await attempted[i].ResumeAsync();
				}
				catch (Exception ex)
				{
					Failure = new AggregateException(Failure, ex);
					restored = false;
				}
			}
			if (owners != null && owners.Any((IShutdownParticipant p) => p.Operations.Snapshot().Pending != 0))
			{
				restored = false;
			}
			if (restored && owners != null)
			{
				IShutdownParticipant[] array9 = owners;
				foreach (IShutdownParticipant shutdownParticipant6 in array9)
				{
					try
					{
						shutdownParticipant6.Operations.Reopen();
					}
					catch (Exception ex2)
					{
						Failure = new AggregateException(Failure, ex2);
						restored = false;
					}
				}
			}
			if (restored)
			{
				registry.Reopen();
				SetState(ShutdownState.Running);
			}
			else
			{
				SetState(ShutdownState.RecoveryRequired);
			}
			return false;
		}
		finally
		{
			Interlocked.Exchange(ref busy, 0);
		}
	}
}
