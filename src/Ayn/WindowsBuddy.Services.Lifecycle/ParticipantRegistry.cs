using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsBuddy.Services.Lifecycle;

public sealed class ParticipantRegistry
{
	private readonly object sync = new object();

	private readonly List<IShutdownParticipant> owners = new List<IShutdownParticipant>();

	private bool frozen;

	public void Register(IShutdownParticipant participant)
	{
		if (participant == null || participant.Operations == null || string.IsNullOrWhiteSpace(participant.Id))
		{
			throw new ArgumentException("Invalid participant.");
		}
		lock (sync)
		{
			if (frozen || owners.Any((IShutdownParticipant p) => p.Id == participant.Id || p.Operations == participant.Operations))
			{
				throw new InvalidOperationException("Frozen registry or duplicate ownership.");
			}
			owners.Add(participant);
		}
	}

	internal IShutdownParticipant[] Freeze()
	{
		lock (sync)
		{
			frozen = true;
			return owners.ToArray();
		}
	}

	internal void Reopen()
	{
		lock (sync)
		{
			frozen = false;
		}
	}
}
