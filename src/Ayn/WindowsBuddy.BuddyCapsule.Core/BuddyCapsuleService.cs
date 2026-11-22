using System.Threading;
using System.Windows;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class BuddyCapsuleService
{
	public const string MutexName = "Local\\Ayn_BuddyCapsule_Active";

	private static Mutex _activeMutex;

	private static Thread _capsuleThread;

	private static CapsuleWindow _capsuleWindow;

	private static Application _wpfApp;

	public static bool IsRunning()
	{
		try
		{
			if (Mutex.TryOpenExisting("Local\\Ayn_BuddyCapsule_Active", out var result))
			{
				result.Close();
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static void StartCapsule()
	{
		if (IsRunning())
		{
			return;
		}
		_capsuleThread = new Thread((ThreadStart)delegate
		{
			_activeMutex = new Mutex(initiallyOwned: true, "Local\\Ayn_BuddyCapsule_Active", out var _);
			_wpfApp = new Application();
			_capsuleWindow = new CapsuleWindow();
			_capsuleWindow.Show();
			_wpfApp.Run(_capsuleWindow);
			if (_activeMutex != null)
			{
				try
				{
					_activeMutex.ReleaseMutex();
				}
				catch
				{
				}
				_activeMutex = null;
			}
		});
		_capsuleThread.SetApartmentState(ApartmentState.STA);
		_capsuleThread.IsBackground = false;
		_capsuleThread.Name = "Ayn_CapsuleSurface";
		_capsuleThread.Start();
	}

	public static void StopCapsule()
	{
		if (_capsuleWindow == null)
		{
			return;
		}
		try
		{
			_capsuleWindow.Dispatcher.Invoke(delegate
			{
				_capsuleWindow.Close();
				if (_wpfApp != null)
				{
					_wpfApp.Shutdown();
				}
			});
		}
		catch
		{
		}
		_capsuleWindow = null;
		_wpfApp = null;
	}
}
