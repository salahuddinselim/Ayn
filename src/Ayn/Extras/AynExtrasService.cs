using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WindowsBuddy.BuddyCapsule.Core;

namespace WindowsBuddy.Extras;

public sealed class AynExtrasService : IDisposable
{
    private static readonly string[] PornDomains =
    [
        "pornhub.com", "xvideos.com", "xnxx.com", "redtube.com", "youporn.com", "tube8.com",
        "xhamster.com", "spankwire.com", "porn.com", "sex.com", "adult.com", "brazzers.com",
        "livejasmin.com", "xxx", "porno", "nude", "escort"
    ];

    private static readonly string[] BettingDomains =
    [
        "bet365.com", "betfair.com", "pinnacle.com", "dafabet.com", "betway.com", "1xbet.com",
        "bovada.lv", "betmgm.com", "draftkings.com", "fanduel.com", "williamhill.com", "betsson.com",
        "unibet.com", "casino", "betting", "poker", "slots", "lottery"
    ];

    private static readonly string[] AdDomains =
    [
        "doubleclick.net", "googleadservices.com", "googlesyndication.com", "adservice.google.com",
        "ads.google.com", "facebook.net", "ads.twitter.com", "adnxs.com", "criteo.com", "taboola.com",
        "outbrain.com", "amazon-adsystem.com", "popads.net", "adcolony.com", "scorecardresearch.com",
        "quantserve.com", "adsrvr.org", "openx.net", "rubiconproject.com"
    ];

    private static readonly string[] BettingProcesses =
    ["bet365", "betfair", "pinnacle", "dafabet", "betway", "1xbet", "casino", "poker", "slots"];

    private readonly Timer _reminderTimer;
    private readonly Timer _appWatchdog;
    private readonly AynSalatService _salat = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private UdpClient? _listener;
    private CancellationTokenSource? _dnsCts;
    private readonly SemaphoreSlim _dnsSlots = new(32);
    private readonly Dictionary<string, (long Ticks, int Count)> _dnsRate = new();
    private readonly object _rateLock = new();
    private TimeSpan _activeWork;
    private TimeSpan _activeBreak;
    private bool _onBreak;
    private readonly HashSet<string> _firedToday = [];
    private DateOnly _firedDate = DateOnly.FromDateTime(DateTime.Today);

    public static AynExtrasService Instance { get; } = new();

    public AynExtrasConfig Config { get; private set; } = AynExtrasConfig.Load();

    public bool DnsRunning { get; private set; }
    public long DnsBlocked { get; private set; }
    public long DnsAllowed { get; private set; }
    public List<string> RecentBlocks { get; } = [];



    public TimeSpan ActiveWork => _activeWork;
    public TimeSpan ActiveBreak => _activeBreak;
    public bool OnBreak => _onBreak;

    public void StartDnsPublic() => StartDns();

    private AynExtrasService()
    {
        _reminderTimer = new Timer(Tick, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));
        _appWatchdog = new Timer(CheckApps, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        if (Config.BlockerEnabled) StartDns();
    }

    public void Save()
    {
        Config.Save();
        if (Config.BlockerEnabled && !DnsRunning) StartDns();
        if (!Config.BlockerEnabled && DnsRunning) StopDns();
    }

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

    private void StartDns()
    {
        if (DnsRunning) return;
        try
        {
            _dnsCts = new CancellationTokenSource();
            _listener = new UdpClient(new IPEndPoint(IPAddress.Any, Config.DnsPort));
            DnsRunning = true;
            if (Config.BlockApps) _appWatchdog.Change(5000, 5000);
            _ = Task.Run(() => ListenAsync(_dnsCts.Token));
        }
        catch (Exception ex)
        {
            CapsuleWindow.ShowToast("Network Guard could not start", ex.Message);
        }
    }

    public void StopDns()
    {
        _dnsCts?.Cancel();
        _listener?.Dispose();
        _dnsCts = null;
        _listener = null;
        _appWatchdog.Change(Timeout.Infinite, Timeout.Infinite);
        DnsRunning = false;
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var result = await _listener.ReceiveAsync(token);
                if (!IsValidDnsQuery(result.Buffer)) continue;
                if (!await _dnsSlots.WaitAsync(0, token)) continue;
                if (!AllowDnsClient(result.RemoteEndPoint.Address)) { _dnsSlots.Release(); continue; }
                _ = Task.Run(() =>
                {
                    try { HandleDns(result.Buffer, result.RemoteEndPoint); }
                    finally { _dnsSlots.Release(); }
                });
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { }
            catch { }
        }
    }

    private static bool IsValidDnsQuery(byte[] p)
    {
        if (p.Length < 12 || p.Length > 4096) return false;
        if ((p[2] & 0x80) != 0) return false;
        if (((p[2] & 0x78) >> 3) != 0) return false;
        var qd = (p[4] << 8) | p[5];
        return qd == 1;
    }

    private bool AllowDnsClient(IPAddress address)
    {
        var key = address.ToString();
        var now = DateTime.UtcNow.Ticks;
        lock (_rateLock)
        {
            if (_dnsRate.TryGetValue(key, out var entry) && now - entry.Ticks < TimeSpan.FromSeconds(1).Ticks)
            {
                if (entry.Count >= 40) return false;
                _dnsRate[key] = (entry.Ticks, entry.Count + 1);
                return true;
            }
            _dnsRate[key] = (now, 1);
            if (_dnsRate.Count > 512) _dnsRate.Clear();
            return true;
        }
    }

    private void HandleDns(byte[] query, IPEndPoint client)
    {
        var domain = ExtractDomain(query);
        var category = Categorize(domain);
        if (category is not null)
        {
            DnsBlocked++;
            lock (RecentBlocks)
            {
                RecentBlocks.Insert(0, $"{DateTime.Now:HH:mm:ss}  {domain}  [{category}]");
                if (RecentBlocks.Count > 80) RecentBlocks.RemoveAt(RecentBlocks.Count - 1);
            }
            try
            {
                var blocked = BlockedResponse(query);
                _listener?.Send(blocked, blocked.Length, client);
            }
            catch { }
        }
        else
        {
            DnsAllowed++;
            Forward(query, client);
        }
    }

    private string? Categorize(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;
        var lower = domain.ToLowerInvariant();
        bool Match(string d) => lower.Equals(d, StringComparison.OrdinalIgnoreCase) ||
                                lower.EndsWith("." + d, StringComparison.OrdinalIgnoreCase);
        if (Config.BlockPorn && PornDomains.Any(Match)) return "Adult";
        if (Config.BlockBetting && BettingDomains.Any(Match)) return "Betting";
        if (Config.BlockAds && AdDomains.Any(Match)) return "Ads";
        return null;
    }

    private void Forward(byte[] query, IPEndPoint client)
    {
        try
        {
            if (!IPAddress.TryParse(Config.UpstreamDns, out var upstream)) upstream = IPAddress.Parse("1.1.1.1");
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            udp.Client.ReceiveTimeout = 3000;
            var upstreamEp = new IPEndPoint(upstream, 53);
            udp.Send(query, query.Length, upstreamEp);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var response = udp.Receive(ref remote);
            if (!remote.Address.Equals(upstream) || remote.Port != 53) return;
            if (response.Length < query.Length || response.Length > 4096) return;
            if (response[0] != query[0] || response[1] != query[1]) return;
            for (var i = 12; i < query.Length; i++)
                if (response[i] != query[i]) return;
            _listener?.Send(response, response.Length, client);
        }
        catch { }
    }

    private static string ExtractDomain(byte[] packet)
    {
        try
        {
            if (packet.Length < 13) return "";
            var sb = new StringBuilder();
            var offset = 12;
            while (offset < packet.Length && packet[offset] != 0)
            {
                var len = packet[offset++];
                if (len == 0 || offset + len > packet.Length) break;
                if (sb.Length > 0) sb.Append('.');
                sb.Append(Encoding.ASCII.GetString(packet, offset, len));
                offset += len;
            }
            return sb.ToString();
        }
        catch { return ""; }
    }

    private static byte[] BlockedResponse(byte[] query)
    {
        var response = new byte[query.Length + 16];
        Array.Copy(query, response, query.Length);
        response[2] |= 0x80;
        response[3] = 0x80;
        response[6] = 0;
        response[7] = 1;
        var o = query.Length;
        response[o++] = 0xC0; response[o++] = 0x0C;
        response[o++] = 0; response[o++] = 1;
        response[o++] = 0; response[o++] = 1;
        response[o++] = 0; response[o++] = 0; response[o++] = 0; response[o++] = 60;
        response[o++] = 0; response[o++] = 4;
        response[o++] = 0; response[o++] = 0; response[o++] = 0; response[o] = 0;
        return response;
    }

    private void CheckApps(object? _)
    {
        if (!Config.BlockApps) return;
        var self = Environment.ProcessId;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == self) continue;
                var name = process.ProcessName.Trim();
                if (BettingProcesses.Any(p => name.Equals(p, StringComparison.OrdinalIgnoreCase)))
                    process.Kill();
            }
            catch { }
        }
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
        StopDns();
        _appWatchdog.Dispose();
        _http.Dispose();
    }
}
