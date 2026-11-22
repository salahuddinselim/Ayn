using System;
using System.Collections.Generic;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Services;

public sealed class ShutdownRestoration
{
	private readonly List<Action> steps = new List<Action>();

	private readonly List<OperationGate> gates = new List<OperationGate>();

	public void Add(Action resume, OperationGate gate = null)
	{
		steps.Add(resume);
		gates.Add(gate);
	}

	public void Run()
	{
		List<Exception> list = new List<Exception>();
		List<OperationGate> list2 = new List<OperationGate>();
		bool flag = false;
		for (int num = steps.Count - 1; num >= 0; num--)
		{
			try
			{
				steps[num]();
			}
			catch (Exception ex)
			{
				list.Add(ex);
				if (gates[num] != null && ex is AdmissionDeferredException)
				{
					list2.Add(gates[num]);
				}
				else
				{
					flag = true;
				}
			}
		}
		if (list.Count != 0)
		{
			throw new ShutdownRestorationException(list, flag ? null : list2);
		}
	}
}
