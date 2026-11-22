using System;
using System.Collections.Generic;
using System.Globalization;

namespace WindowsBuddy.BuddyCapsule.Core;

internal static class RestingFit
{
	public const int ShortenText = 1;

	public const int IconOnly = 2;

	public const int DropItems = 3;

	public const int SqueezeText = 4;

	public const double MaxGrowth = 1.5;

	public const double MaxScreenShare = 0.35;

	public static double MaxWidth(double baseWidth, double monitorWidthDip)
	{
		double num = Math.Round(baseWidth * 1.5);
		double val = ((monitorWidthDip > 0.0) ? Math.Round(monitorWidthDip * 0.35) : num);
		return Math.Max(baseWidth, Math.Min(num, val));
	}

	public static double Total(IList<RestingFitItem> items)
	{
		double num = 0.0;
		foreach (RestingFitItem item in items)
		{
			num += item.Width;
		}
		return num;
	}

	public static void Reset(IList<RestingFitItem> items)
	{
		foreach (RestingFitItem item in items)
		{
			item.StepsApplied = 0;
			item.Dropped = false;
		}
	}

	public static bool Fit(IList<RestingFitItem> items, double available)
	{
		for (int i = 1; i <= 4; i++)
		{
			while (Total(items) > available + 0.01)
			{
				RestingFitItem restingFitItem = null;
				foreach (RestingFitItem item in items)
				{
					if (!item.Dropped && item.StepsApplied < item.Steps.Count && item.Steps[item.StepsApplied].Stage <= i && (restingFitItem == null || item.DropOrder < restingFitItem.DropOrder))
					{
						restingFitItem = item;
					}
				}
				if (restingFitItem == null)
				{
					break;
				}
				if (restingFitItem.Steps[restingFitItem.StepsApplied++].Drops)
				{
					restingFitItem.Dropped = true;
				}
			}
			if (Total(items) <= available + 0.01)
			{
				return true;
			}
		}
		return Total(items) <= available + 0.01;
	}

	public static RestingFitResult Plan(IList<RestingFitItem> items, double baseWidth, double maxWidth, double gap, Func<double, double> insetFor)
	{
		Reset(items);
		double num = Total(items);
		double num2 = baseWidth;
		for (int i = 0; i < 4; i++)
		{
			double a = num + gap + 2.0 * insetFor(num2);
			double num3 = Math.Min(maxWidth, Math.Max(baseWidth, Math.Ceiling(a)));
			if (Math.Abs(num3 - num2) < 0.5)
			{
				num2 = num3;
				break;
			}
			num2 = num3;
		}
		double available = num2 - 2.0 * insetFor(num2) - gap;
		bool fits = Fit(items, available);
		RestingFitResult restingFitResult = new RestingFitResult();
		restingFitResult.CardWidth = num2;
		restingFitResult.Fits = fits;
		return restingFitResult;
	}

	public static string TimeText(DateTime now, int form, bool showDate, bool showSeconds, Func<DateTime, bool, string> formatTime)
	{
		switch (form)
		{
		case 0:
		{
			string text = formatTime(now, showSeconds);
			if (showDate)
			{
				text = now.ToString("ddd d", CultureInfo.CurrentCulture) + " · " + text;
			}
			return text;
		}
		case 1:
		{
			string text = formatTime(now, arg2: false);
			if (showDate)
			{
				text = now.ToString("d ", CultureInfo.CurrentCulture).Trim() + " · " + text;
			}
			return text;
		}
		case 2:
			return formatTime(now, arg2: false);
		default:
			return StripMeridiem(formatTime(now, arg2: false));
		}
	}

	public static string StripMeridiem(string time)
	{
		DateTimeFormatInfo dateTimeFormat = CultureInfo.CurrentCulture.DateTimeFormat;
		string[] array = new string[2] { dateTimeFormat.AMDesignator, dateTimeFormat.PMDesignator };
		foreach (string text in array)
		{
			if (!string.IsNullOrEmpty(text))
			{
				time = time.Replace(text, "");
			}
		}
		return time.Trim();
	}
}
