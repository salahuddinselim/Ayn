using System.Threading;

namespace WindowsBuddy.UI.Settings;

internal sealed class SettingsOperationGate
{
	private int _busy;

	public bool IsBusy => Volatile.Read(ref _busy) != 0;

	public bool TryEnter()
	{
		return Interlocked.CompareExchange(ref _busy, 1, 0) == 0;
	}

	public void Exit()
	{
		Interlocked.Exchange(ref _busy, 0);
	}
}
