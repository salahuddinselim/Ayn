using System;
using System.Runtime.InteropServices;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class PowerSaver
{
	private struct SYSTEM_POWER_STATUS
	{
		public byte ACLineStatus;

		public byte BatteryFlag;

		public byte BatteryLifePercent;

		public byte SystemStatusFlag;

		public int BatteryLifeTime;

		public int BatteryFullLifeTime;
	}

	private static bool _on;

	private static bool _read;

	private static bool? _qaOverride;

	private static bool _onBattery;

	private static bool? _qaBattery;

	public static bool OnBattery
	{
		get
		{
			if (_qaBattery.HasValue)
			{
				return _qaBattery.Value;
			}
			if (!_read)
			{
				Refresh();
			}
			return _onBattery;
		}
	}

	public static bool LimitFrameRate
	{
		get
		{
			if (!IsBatterySaverOn)
			{
				return OnBattery;
			}
			return true;
		}
	}

	public static bool IsBatterySaverOn
	{
		get
		{
			if (_qaOverride.HasValue)
			{
				return _qaOverride.Value;
			}
			if (!_read)
			{
				Refresh();
			}
			return _on;
		}
	}

	public static event EventHandler Changed;

	[DllImport("kernel32.dll")]
	private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

	public static void SetQaBattery(bool? onBattery)
	{
		_qaBattery = onBattery;
	}

	public static void SetQaOverride(bool? on)
	{
		_qaOverride = on;
		EventHandler changed = PowerSaver.Changed;
		if (changed != null)
		{
			try
			{
				changed(null, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public static void Refresh()
	{
		bool flag = false;
		bool onBattery = false;
		try
		{
			if (GetSystemPowerStatus(out var s))
			{
				flag = s.SystemStatusFlag == 1;
				onBattery = s.ACLineStatus == 0;
			}
		}
		catch
		{
		}
		_onBattery = onBattery;
		bool flag2 = _read && flag != _on;
		_on = flag;
		_read = true;
		if (!flag2)
		{
			return;
		}
		EventHandler changed = PowerSaver.Changed;
		if (changed != null)
		{
			try
			{
				changed(null, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}
}
