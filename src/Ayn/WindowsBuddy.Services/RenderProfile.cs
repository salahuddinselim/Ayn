using System;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class RenderProfile
{
	private static readonly object _lock = new object();

	private static bool _detected;

	private static bool _light;

	private static int _tier = -1;

	private static bool _software;

	private static string _reason = "";

	public static bool LightMode
	{
		get
		{
			Detect();
			return _light;
		}
	}

	public static int Tier
	{
		get
		{
			Detect();
			return _tier;
		}
	}

	public static bool SoftwareRendering
	{
		get
		{
			Detect();
			return _software;
		}
	}

	public static string Reason
	{
		get
		{
			Detect();
			return _reason;
		}
	}

	public static string Summary
	{
		get
		{
			Detect();
			return (_light ? "light" : "full") + " mode, render tier " + _tier + ", software " + (_software ? "yes" : "no") + " (" + _reason + ")";
		}
	}

	public static void Detect()
	{
		if (_detected)
		{
			return;
		}
		lock (_lock)
		{
			if (_detected)
			{
				return;
			}
			try
			{
				_tier = RenderCapability.Tier >> 16;
				bool flag = false;
				try
				{
					flag = RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly;
				}
				catch
				{
				}
				bool flag2 = false;
				try
				{
					flag2 = SystemParameters.IsRemoteSession;
				}
				catch
				{
				}
				_software = flag || _tier == 0 || flag2;
				string environmentVariable = Environment.GetEnvironmentVariable("VAYLI_LIGHT_MODE");
				if (environmentVariable == "1")
				{
					_light = true;
					_reason = "forced on";
				}
				else if (environmentVariable == "0")
				{
					_light = false;
					_reason = "forced off";
				}
				else if (flag)
				{
					_light = true;
					_reason = "software rendering";
				}
				else if (flag2)
				{
					_light = true;
					_reason = "remote session";
				}
				else if (_tier < 2)
				{
					_light = true;
					_reason = "render tier " + _tier + " below 2";
				}
				else
				{
					_light = false;
					_reason = "render tier 2";
				}
			}
			catch (Exception ex)
			{
				_light = false;
				_reason = "detection failed: " + ex.GetType().Name;
			}
			_detected = true;
			try
			{
				TransactionLogger.Info("Render: " + Summary);
				LogWriter.Enqueue("metrics.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " render " + Summary);
			}
			catch
			{
			}
		}
	}
}
