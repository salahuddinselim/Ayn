using System;

namespace WindowsBuddy.Tools.Weather;

public static class SolarCalculator
{
	private static DateTime _cachedDate = DateTime.MinValue;

	private static double _cachedLat = double.NaN;

	private static double _cachedLng = double.NaN;

	private static DateTime _cachedSunrise = DateTime.MinValue;

	private static DateTime _cachedSunset = DateTime.MinValue;

	private static readonly object _lock = new object();

	public static bool IsNight(double latitude, double longitude, DateTime now, bool? forcedNight = null)
	{
		if (forcedNight.HasValue)
		{
			return forcedNight.Value;
		}
		if (double.IsNaN(latitude) || double.IsNaN(longitude) || (Math.Abs(latitude) < 0.0001 && Math.Abs(longitude) < 0.0001))
		{
			if (now.Hour < 19)
			{
				return now.Hour < 6;
			}
			return true;
		}
		GetSunriseSunsetCached(latitude, longitude, now.Date, out var sunrise, out var sunset);
		if (sunrise == DateTime.MinValue || sunset == DateTime.MinValue)
		{
			if (now.Hour < 19)
			{
				return now.Hour < 6;
			}
			return true;
		}
		if (!(now < sunrise))
		{
			return now >= sunset;
		}
		return true;
	}

	public static void GetSunriseSunsetCached(double lat, double lng, DateTime date, out DateTime sunrise, out DateTime sunset)
	{
		lock (_lock)
		{
			if (_cachedDate == date && Math.Abs(_cachedLat - lat) < 0.001 && Math.Abs(_cachedLng - lng) < 0.001)
			{
				sunrise = _cachedSunrise;
				sunset = _cachedSunset;
				return;
			}
			ComputeSunriseSunset(lat, lng, date, out sunrise, out sunset);
			_cachedDate = date;
			_cachedLat = lat;
			_cachedLng = lng;
			_cachedSunrise = sunrise;
			_cachedSunset = sunset;
		}
	}

	private static void ComputeSunriseSunset(double lat, double lng, DateTime date, out DateTime sunrise, out DateTime sunset)
	{
		sunrise = DateTime.MinValue;
		sunset = DateTime.MinValue;
		try
		{
			int dayOfYear = date.DayOfYear;
			double num = lng / 15.0;
			double num2 = (double)dayOfYear + (6.0 - num) / 24.0;
			double num3 = 0.9856 * num2 - 3.289;
			double num4 = NormalizeDegrees(num3 + 1.916 * Math.Sin(ToRad(num3)) + 0.02 * Math.Sin(ToRad(2.0 * num3)) + 282.634);
			double num5 = NormalizeDegrees(ToDeg(Math.Atan(0.91764 * Math.Tan(ToRad(num4)))));
			double num6 = Math.Floor(num4 / 90.0) * 90.0;
			double num7 = Math.Floor(num5 / 90.0) * 90.0;
			num5 = (num5 + (num6 - num7)) / 15.0;
			double num8 = 0.39782 * Math.Sin(ToRad(num4));
			double num9 = Math.Cos(Math.Asin(num8));
			double num10 = (Math.Cos(ToRad(90.833)) - num8 * Math.Sin(ToRad(lat))) / (num9 * Math.Cos(ToRad(lat)));
			double num11 = (double)dayOfYear + (18.0 - num) / 24.0;
			double num12 = 0.9856 * num11 - 3.289;
			double num13 = NormalizeDegrees(num12 + 1.916 * Math.Sin(ToRad(num12)) + 0.02 * Math.Sin(ToRad(2.0 * num12)) + 282.634);
			double num14 = NormalizeDegrees(ToDeg(Math.Atan(0.91764 * Math.Tan(ToRad(num13)))));
			double num15 = Math.Floor(num13 / 90.0) * 90.0;
			double num16 = Math.Floor(num14 / 90.0) * 90.0;
			num14 = (num14 + (num15 - num16)) / 15.0;
			double num17 = 0.39782 * Math.Sin(ToRad(num13));
			double num18 = Math.Cos(Math.Asin(num17));
			double num19 = (Math.Cos(ToRad(90.833)) - num17 * Math.Sin(ToRad(lat))) / (num18 * Math.Cos(ToRad(lat)));
			if (!(num10 > 1.0) && !(num10 < -1.0) && !(num19 > 1.0) && !(num19 < -1.0))
			{
				double num20 = (360.0 - ToDeg(Math.Acos(num10))) / 15.0;
				double num21 = num20 + num5 - 0.06571 * num2 - 6.622;
				double num22 = (num21 - num) % 24.0;
				if (num22 < 0.0)
				{
					num22 += 24.0;
				}
				double num23 = ToDeg(Math.Acos(num19)) / 15.0;
				double num24 = num23 + num14 - 0.06571 * num11 - 6.622;
				double num25 = (num24 - num) % 24.0;
				if (num25 < 0.0)
				{
					num25 += 24.0;
				}
				DateTime value = date.Date.AddHours(num22);
				DateTime value2 = date.Date.AddHours(num25);
				sunrise = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeZoneInfo.Local);
				sunset = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value2, DateTimeKind.Utc), TimeZoneInfo.Local);
			}
		}
		catch
		{
		}
	}

	private static double ToRad(double deg)
	{
		return deg * Math.PI / 180.0;
	}

	private static double ToDeg(double rad)
	{
		return rad * 180.0 / Math.PI;
	}

	private static double NormalizeDegrees(double deg)
	{
		double num = deg % 360.0;
		if (!(num < 0.0))
		{
			return num;
		}
		return num + 360.0;
	}
}
