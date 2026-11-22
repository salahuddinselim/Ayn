using System;
using System.Net;
using Microsoft.Win32;

namespace WindowsBuddy.Services;

public static class NetworkSecurity
{
	private const SecurityProtocolType Tls13 = SecurityProtocolType.Tls13;

	public static SecurityProtocolType Policy
	{
		get
		{
			if (!SupportsTls13())
			{
				return SecurityProtocolType.Tls12;
			}
			return SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
		}
	}

	public static void Configure()
	{
		try
		{
			ServicePointManager.SecurityProtocol = Policy;
		}
		catch (NotSupportedException)
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		}
	}

	private static bool SupportsTls13()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", writable: false);
			int result;
			return registryKey != null && int.TryParse(Convert.ToString(registryKey.GetValue("CurrentBuildNumber")), out result) && result >= 20348;
		}
		catch
		{
			return false;
		}
	}
}
