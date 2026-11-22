using System;
using System.Collections.Generic;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class EntitlementGate
{
	private sealed class Producer
	{
		public string Name;

		public Func<Action> Pause;

		public Action Resume;
	}

	internal static Func<bool> Probe = () => true;

	private static readonly object Sync = new object();

	private static readonly List<Producer> Producers = new List<Producer>();

	private static bool _entitled = true;

	private static int _holds;

	private static bool? _deferred;

	public static bool IsEntitled
	{
		get
		{
			lock (Sync)
			{
				return _entitled;
			}
		}
	}

	public static event Action<bool> Changed;

	public static void Register(string name, Func<Action> pause)
	{
		if (string.IsNullOrEmpty(name) || pause == null)
		{
			throw new ArgumentException("A producer needs a name and a pause hook.");
		}
		Producer producer = new Producer();
		producer.Name = name;
		producer.Pause = pause;
		Producer producer2 = producer;
		bool flag;
		lock (Sync)
		{
			if (Producers.Exists((Producer p) => p.Name == name))
			{
				throw new InvalidOperationException("Producer already registered: " + name);
			}
			Producers.Add(producer2);
			flag = !_entitled && _holds == 0;
		}
		if (flag)
		{
			Suspend(producer2);
		}
	}

	public static bool IsSuspended(string name)
	{
		lock (Sync)
		{
			Producer producer = Producers.Find((Producer x) => x.Name == name);
			return producer != null && producer.Resume != null;
		}
	}

	public static bool Evaluate()
	{
		bool flag;
		try
		{
			flag = Probe();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Entitlement check failed (" + ex.GetType().Name + "); state unchanged.");
			return IsEntitled;
		}
		Apply(flag);
		return flag;
	}

	private static void Apply(bool entitled)
	{
		List<Producer> list;
		lock (Sync)
		{
			if (_holds > 0)
			{
				_deferred = entitled;
				return;
			}
			if (entitled == _entitled)
			{
				return;
			}
			_entitled = entitled;
			list = new List<Producer>(Producers);
		}
		TransactionLogger.Info(entitled ? "Entitlement restored: resuming tools and background services." : "Not entitled: suspending tools and background services (data is kept).");
		if (entitled)
		{
			list.Reverse();
			foreach (Producer item in list)
			{
				Resume(item);
			}
		}
		else
		{
			foreach (Producer item2 in list)
			{
				Suspend(item2);
			}
		}
		Action<bool> changed = EntitlementGate.Changed;
		if (changed == null)
		{
			return;
		}
		Delegate[] invocationList = changed.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			Action<bool> action = (Action<bool>)invocationList[i];
			try
			{
				action(entitled);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Entitlement observer failed: " + ex.GetType().Name);
			}
		}
	}

	private static void Suspend(Producer p)
	{
		lock (Sync)
		{
			if (p.Resume != null)
			{
				return;
			}
		}
		try
		{
			Action action = p.Pause();
			lock (Sync)
			{
				p.Resume = action ?? ((Action)delegate
				{
				});
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Could not suspend " + p.Name + " (" + ex.GetType().Name + ").");
		}
	}

	private static void Resume(Producer p)
	{
		Action resume;
		lock (Sync)
		{
			resume = p.Resume;
			p.Resume = null;
		}
		if (resume == null)
		{
			return;
		}
		try
		{
			resume();
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Could not resume " + p.Name + " (" + ex.GetType().Name + ").");
		}
	}

	public static Action HoldForShutdown()
	{
		lock (Sync)
		{
			_holds++;
		}
		bool released = false;
		return delegate
		{
			bool? flag;
			lock (Sync)
			{
				if (released)
				{
					return;
				}
				released = true;
				_holds--;
				flag = ((_holds == 0) ? _deferred : null);
				if (_holds == 0)
				{
					_deferred = null;
				}
			}
			if (flag.HasValue)
			{
				Apply(flag.Value);
			}
		};
	}

	public static Action PauseUnlessSuspended(string name, Func<Action> pause)
	{
		if (IsSuspended(name))
		{
			return delegate
			{
			};
		}
		return pause();
	}

	internal static void ResetForQa()
	{
		lock (Sync)
		{
			Producers.Clear();
			_entitled = true;
			_holds = 0;
			_deferred = null;
		}
		EntitlementGate.Changed = null;
	}
}
