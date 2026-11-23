using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsBuddy.BuddyCapsule.Core;

namespace WindowsBuddy.Extras;

public sealed class AynExtrasService : IDisposable
{
    private readonly System.Threading.Timer _reminderTimer;
    private readonly AynSalatService _salat = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private TimeSpan _activeWork;
    private TimeSpan _activeBreak;
    private bool _onBreak;
    private readonly HashSet<string> _firedToday = [];
    private DateOnly _firedDate = DateOnly.FromDateTime(DateTime.Today);

    public static AynExtrasService Instance { get; } = new();

    public AynExtrasConfig Config { get; private set; } = AynExtrasConfig.Load();

    public TimeSpan ActiveWork => _activeWork;
    public TimeSpan ActiveBreak => _activeBreak;
    public bool OnBreak => _onBreak;

    private AynExtrasService()
    {
        _reminderTimer = new System.Threading.Timer(Tick, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));
    }

    public void Start() { }

    public void Save() => Config.Save();

    private void Tick(object? _)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_firedDate != today) { _firedDate = today; _firedToday.Clear(); }
        if (IsIdle()) return;

        if (Config.BreakEnabled)
        {
            if (_onBreak)
            {
                _activeBreak += TimeSpan.FromSeconds(15);
                if (_activeBreak >= TimeSpan.FromMinutes(Config.BreakMinutes))
                {
                    _onBreak = false;
                    _activeBreak = TimeSpan.Zero;
                    _activeWork = TimeSpan.Zero;
                    CapsuleWindow.ShowToast("Break over", "Welcome back — ready to focus again?");
                }
            }
            else
            {
                _activeWork += TimeSpan.FromSeconds(15);
                if (_activeWork >= TimeSpan.FromMinutes(Math.Max(1, Config.WorkMinutes)))
                {
                    _onBreak = true;
                    _activeBreak = TimeSpan.Zero;
                    CapsuleWindow.ShowToast("Time for a break",
                        $"You have been at the computer for {Config.WorkMinutes} minutes. Rest for {Config.BreakMinutes} minutes.");
                }
            }
        }

        if (Config.MealEnabled)
        {
            FireOnce("lunch", Config.LunchTime, "Lunch time", "You are still at the computer. Take a moment to eat.");
            FireOnce("dinner", Config.DinnerTime, "Dinner time", "You are still at the computer. Take a moment to eat.");
        }

        if (Config.SalatEnabled)
        {
            foreach (var prayer in _salat.GetTimes(today, Config.Latitude, Config.Longitude, Config.SalatMethod, Config.TimezoneId))
            {
                if (DateTime.Now >= prayer.Time && DateTime.Now < prayer.Time.AddMinutes(30))
                    FireOnce("salat-" + prayer.Name, prayer.Time.ToString("HH:mm"),
                        prayer.Name + " time", $"{prayer.Name} has begun. It is a good time to pause and pray.");
            }
        }
    }

    private void FireOnce(string key, string time, string title, string message)
    {
        if (_firedToday.Contains(key)) return;
        if (!TimeOnly.TryParse(time, out var target)) return;
        var now = TimeOnly.FromDateTime(DateTime.Now);
        if (now < target || now > target.AddMinutes(20)) return;
        _firedToday.Add(key);
        CapsuleWindow.ShowToast(title, message);
    }

    public void StartBreak()
    {
        _onBreak = true;
        _activeBreak = TimeSpan.Zero;
    }

    public void SkipBreak()
    {
        _onBreak = false;
        _activeBreak = TimeSpan.Zero;
        _activeWork = TimeSpan.Zero;
    }

    private static bool IsIdle()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) && Environment.TickCount - info.dwTime > 120_000;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public async Task<bool> SetAreaFromCityAsync(string city)
    {
        if (string.IsNullOrWhiteSpace(city)) return false;
        try
        {
            var url = "https://geocoding-api.open-meteo.com/v1/search?count=1&name=" + Uri.EscapeDataString(city.Trim());
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0) return false;
            var first = results[0];
            Config.Latitude = first.GetProperty("latitude").GetDouble();
            Config.Longitude = first.GetProperty("longitude").GetDouble();
            Config.City = city.Trim();
            if (first.TryGetProperty("country", out var country)) Config.Country = country.GetString() ?? Config.Country;
            if (first.TryGetProperty("timezone", out var zone)) Config.TimezoneId = zone.GetString() ?? Config.TimezoneId;
            Config.Save();
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> DetectAreaAsync()
    {
        try
        {
            var json = await _http.GetStringAsync("https://ipapi.co/json/");
            using var doc = JsonDocument.Parse(json);
            Config.Latitude = doc.RootElement.GetProperty("latitude").GetDouble();
            Config.Longitude = doc.RootElement.GetProperty("longitude").GetDouble();
            Config.City = doc.RootElement.GetProperty("city").GetString() ?? Config.City;
            Config.Country = doc.RootElement.GetProperty("country_name").GetString() ?? Config.Country;
            Config.TimezoneId = doc.RootElement.GetProperty("timezone").GetString() ?? Config.TimezoneId;
            Config.Save();
            return true;
        }
        catch { return false; }
    }

    public IReadOnlyList<AynPrayerTime> TodayPrayers() =>
        _salat.GetTimes(DateOnly.FromDateTime(DateTime.Today), Config.Latitude, Config.Longitude,
            Config.SalatMethod, Config.TimezoneId);

    public void Dispose()
    {
        _reminderTimer.Dispose();
        _http.Dispose();
    }
}
