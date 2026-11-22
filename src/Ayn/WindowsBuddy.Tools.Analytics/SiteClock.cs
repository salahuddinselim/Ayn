using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsBuddy.Tools.Analytics;

public static class SiteClock
{
	private const int UCAL_ZONE_OFFSET = 15;

	private const int UCAL_DST_OFFSET = 16;

	public static Func<DateTime> UtcNow = () => DateTime.UtcNow;

	[DllImport("icu.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
	private static extern IntPtr ucal_open(string zoneID, int len, [MarshalAs(UnmanagedType.LPStr)] string locale, int type, ref int status);

	[DllImport("icu.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void ucal_close(IntPtr cal);

	[DllImport("icu.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void ucal_setMillis(IntPtr cal, double millis, ref int status);

	[DllImport("icu.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int ucal_get(IntPtr cal, int field, ref int status);

	[DllImport("icu.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
	private static extern int ucal_getCanonicalTimeZoneID(string id, int len, StringBuilder result, int cap, out byte isSystemID, ref int status);

	[DllImport("icu.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
	private static extern int ucal_getTimeZoneIDForWindowsID(string winid, int len, [MarshalAs(UnmanagedType.LPStr)] string region, StringBuilder id, int cap, ref int status);

	public static TimeSpan? Offset(string iana, DateTime utc)
	{
		if (string.IsNullOrWhiteSpace(iana))
		{
			return null;
		}
		switch (iana)
		{
		case "UTC":
		case "Etc/UTC":
		case "GMT":
			return TimeSpan.Zero;
		default:
			try
			{
				int status = 0;
				StringBuilder stringBuilder = new StringBuilder(64);
				ucal_getCanonicalTimeZoneID(iana, iana.Length, stringBuilder, stringBuilder.Capacity, out var isSystemID, ref status);
				if (status > 0 || isSystemID == 0)
				{
					return null;
				}
				status = 0;
				IntPtr intPtr = ucal_open(iana, iana.Length, null, 1, ref status);
				if (status > 0 || intPtr == IntPtr.Zero)
				{
					return null;
				}
				try
				{
					double totalMilliseconds = (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
					ucal_setMillis(intPtr, totalMilliseconds, ref status);
					int num = ucal_get(intPtr, 15, ref status);
					int num2 = ucal_get(intPtr, 16, ref status);
					if (status > 0)
					{
						return null;
					}
					return TimeSpan.FromMilliseconds(num + num2);
				}
				finally
				{
					ucal_close(intPtr);
				}
			}
			catch
			{
				return null;
			}
		}
	}

	public static string LocalIana()
	{
		try
		{
			string id = TimeZoneInfo.Local.Id;
			int status = 0;
			StringBuilder stringBuilder = new StringBuilder(64);
			int num = ucal_getTimeZoneIDForWindowsID(id, id.Length, null, stringBuilder, stringBuilder.Capacity, ref status);
			return (status > 0 || num <= 0) ? null : stringBuilder.ToString(0, num);
		}
		catch
		{
			return null;
		}
	}

	public static DateTimeOffset Now(string iana)
	{
		DateTime dateTime = UtcNow();
		TimeSpan timeSpan = Offset(iana, dateTime) ?? TimeZoneInfo.Local.GetUtcOffset(dateTime);
		return new DateTimeOffset(DateTime.SpecifyKind(dateTime + timeSpan, DateTimeKind.Unspecified), timeSpan);
	}

	public static string Iso(DateTime wall, TimeSpan offset)
	{
		string text = ((offset < TimeSpan.Zero) ? "-" : "+");
		TimeSpan timeSpan = offset.Duration();
		return wall.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + text + timeSpan.Hours.ToString("00") + ":" + timeSpan.Minutes.ToString("00");
	}

	public static string Day(DateTime d)
	{
		return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	}
}
