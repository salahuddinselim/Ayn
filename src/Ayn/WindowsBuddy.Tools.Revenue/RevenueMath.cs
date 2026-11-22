using System;
using System.Collections.Generic;
using System.Globalization;
using WindowsBuddy.Tools.Analytics;

namespace WindowsBuddy.Tools.Revenue;

public static class RevenueMath
{
	public static int Digits(string currency)
	{
		string value = " " + (currency ?? "").ToUpperInvariant() + " ";
		if (" BIF CLP DJF GNF JPY KMF KRW MGA PYG RWF VND VUV XAF XOF XPF ".Contains(value))
		{
			return 0;
		}
		if (" BHD JOD KWD OMR TND ".Contains(value))
		{
			return 3;
		}
		return 2;
	}

	public static string Symbol(string currency)
	{
		return (currency ?? "").ToUpperInvariant() switch
		{
			"USD" => "$", 
			"EUR" => "€", 
			"GBP" => "£", 
			"INR" => "₹", 
			"JPY" => "¥", 
			"AUD" => "A$", 
			"CAD" => "C$", 
			_ => (currency ?? "").ToUpperInvariant() + " ", 
		};
	}

	public static decimal Major(long minor, string currency)
	{
		return Digits(currency) switch
		{
			3 => (decimal)minor / 1000m, 
			0 => minor, 
			_ => (decimal)minor / 100m, 
		};
	}

	public static string Format(long minor, string currency)
	{
		decimal num = Major(minor, currency);
		int num2 = Digits(currency);
		string text = ((Math.Abs(num) >= 10000m) ? Math.Abs(num).ToString("N0", CultureInfo.CurrentCulture) : Math.Abs(num).ToString("N" + num2, CultureInfo.CurrentCulture));
		return ((num < 0m) ? "−" : "") + Symbol(currency) + text;
	}

	public static void Plan(RevenueRange range, DateTime todayLocal, out DateTime start, out DateTime end)
	{
		end = todayLocal.Date;
		start = end.AddDays(-(range switch
		{
			RevenueRange.Week => 6, 
			RevenueRange.Today => 0, 
			_ => 29, 
		}));
	}

	internal static List<SeriesPoint> Series(Dictionary<DateTime, long> byBucket, DateTime start, DateTime end, bool hourly, DateTime nowLocal)
	{
		DateTime last = (hourly ? new DateTime(end.Year, end.Month, end.Day, (nowLocal.Date == end) ? nowLocal.Hour : 23, 0, 0) : end);
		return AnalyticsProviders.Fill(byBucket, start, last, hourly);
	}
}
