using System;
using System.Diagnostics;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class AppLinks
{
	public const string Website = "https://github.com/salahuddinselim/Ayn";

	public const string Buy = "https://github.com/salahuddinselim/Ayn";

	public const string Privacy = "https://github.com/salahuddinselim/Ayn";

	public const string Terms = "https://github.com/salahuddinselim/Ayn";

	public const string Refund = "https://github.com/salahuddinselim/Ayn";

	public const string Contact = "https://github.com/salahuddinselim/Ayn";

	public const string SupportEmail = "contact@modistudio.online";

	public const string StoreProductId = "9PJ5B1HXNSKV";

	public static void Open(string url)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(url);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not open " + url + ": " + ex.Message);
		}
	}
}
