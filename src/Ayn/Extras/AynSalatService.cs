using System;
using System.Collections.Generic;

namespace WindowsBuddy.Extras;

public sealed record AynPrayerTime(string Name, DateTime Time);

public sealed class AynSalatService
{
    private static readonly Dictionary<string, (double Fajr, double Isha)> Methods = new()
    {
        ["Karachi"] = (18, 17),
        ["ISNA"] = (15, 15),
        ["Egyptian"] = (19.5, 17.5),
        ["Umm al-Qura"] = (18.5, -1),
        ["MWL"] = (18, 17)
    };

    public IReadOnlyList<AynPrayerTime> GetTimes(DateOnly date, double latitude, double longitude, string method, string timeZoneId)
    {
        var tz = ResolveZone(timeZoneId);
        var cfg = Methods.TryGetValue(method, out var m) ? m : Methods["Karachi"];
        var julian = ToJulian(date.Year, date.Month, date.Day) - longitude / 1440.0;
        var (declination, equationOfTime) = SunPosition(julian);
        var dhuhr = 12.0 - longitude / 15.0 - equationOfTime / 60.0;
        var asr = Math.Atan(1.0 / (1.0 + Math.Tan(Deg(Math.Abs(latitude - declination))))) * 180 / Math.PI;
        var maghrib = -0.833 - 0.04 * Math.Sqrt(Math.Max(0, 900 - latitude * latitude));

        var parts = new (string Name, double Fraction)[]
        {
            ("Fajr", Angle(-cfg.Fajr, false, latitude, declination, dhuhr)),
            ("Sunrise", Angle(-0.833, false, latitude, declination, dhuhr)),
            ("Dhuhr", dhuhr),
            ("Asr", Angle(asr, true, latitude, declination, dhuhr)),
            ("Maghrib", Angle(maghrib, true, latitude, declination, dhuhr)),
            ("Isha", cfg.Isha < 0
                ? Angle(maghrib, true, latitude, declination, dhuhr) + 1.5
                : Angle(-cfg.Isha, true, latitude, declination, dhuhr))
        };

        var result = new List<AynPrayerTime>();
        foreach (var (name, fraction) in parts)
        {
            var offset = tz.GetUtcOffset(new DateTime(date.Year, date.Month, date.Day, 12, 0, 0));
            var minutes = ((int)Math.Round(fraction * 60 + offset.TotalMinutes) % 1440 + 1440) % 1440;
            result.Add(new AynPrayerTime(name, new DateTime(date.Year, date.Month, date.Day).AddMinutes(minutes)));
        }
        return result;
    }

    public static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windows))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(windows); } catch { }
        }
        return TimeZoneInfo.Utc;
    }

    private static double Angle(double altitude, bool afterNoon, double latitude, double declination, double dhuhr)
    {
        var rhs = (Math.Sin(Deg(altitude)) - Math.Sin(Deg(latitude)) * Math.Sin(Deg(declination)))
                  / (Math.Cos(Deg(latitude)) * Math.Cos(Deg(declination)));
        if (rhs > 1 || rhs < -1) return dhuhr;
        var hourAngle = Math.Acos(rhs) * 180 / Math.PI / 15.0;
        return afterNoon ? dhuhr + hourAngle : dhuhr - hourAngle;
    }

    private static (double, double) SunPosition(double julian)
    {
        var n = julian - 2451545.0;
        var meanLongitude = (280.46 + 0.9856474 * n) % 360;
        var anomaly = Deg((357.528 + 0.9856003 * n) % 360);
        var ecliptic = Deg(meanLongitude + 1.915 * Math.Sin(anomaly) + 0.02 * Math.Sin(2 * anomaly));
        var obliquity = Deg(23.439 - 0.0000004 * n);
        var declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(ecliptic)) * 180 / Math.PI;
        var ra = Math.Atan2(Math.Sin(ecliptic) * Math.Cos(obliquity), Math.Cos(ecliptic)) * 180 / Math.PI;
        if (ra < 0) ra += 360;
        var eq = 4 * (meanLongitude - ra);
        if (eq > 20) eq -= 1440;
        if (eq < -20) eq += 1440;
        return (declination, eq);
    }

    private static double ToJulian(int y, int m, int d)
    {
        if (m <= 2) { y--; m += 12; }
        var a = y / 100;
        var b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
    }

    private static double Deg(double value) => value * Math.PI / 180.0;
}
