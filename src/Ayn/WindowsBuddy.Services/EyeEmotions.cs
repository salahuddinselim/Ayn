using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsBuddy.Services;

public static class EyeEmotions
{
    public const string Auto = "auto";
    public const string Neutral = "neutral";
    public const string Happy = "happy";
    public const string Sad = "sad";
    public const string Cry = "cry";
    public const string Ego = "ego";
    public const string Surprised = "surprised";
    public const string Sleepy = "sleepy";

    public static readonly string[] All =
    [
        Auto, Neutral, Happy, Sad, Cry, Ego, Surprised, Sleepy
    ];

    private static DateTime _lastEvaluate;
    private static string _current = Happy;

    public static string Normalize(string id)
    {
        id = (id ?? "").Trim().ToLowerInvariant();
        foreach (var item in All)
            if (item == id) return item;
        return Auto;
    }

    public static string Title(string id) => Normalize(id) switch
    {
        Auto => "Automatic",
        Happy => "Happy",
        Sad => "Sad",
        Cry => "Cry",
        Ego => "Ego",
        Surprised => "Surprised",
        Sleepy => "Sleepy",
        _ => "Neutral"
    };

    public static string Tagline(string id) => Normalize(id) switch
    {
        Auto => "The eyes react on their own",
        Happy => "Warm smile, soft squint",
        Sad => "Drooping lids and a quiet gaze",
        Cry => "Teary eyes and a low gaze",
        Ego => "Sharp brows, narrowed stare",
        Surprised => "Wide open, fully dilated",
        Sleepy => "Heavy lids, slow blink",
        _ => "Just being itself"
    };

    public static string Evaluate()
    {
        var now = DateTime.Now;
        if ((now - _lastEvaluate).TotalSeconds < 12) return _current;
        _lastEvaluate = now;

        var hour = now.Hour;
        var battery = 1f;
        try
        {
            var status = SystemInformation.PowerStatus;
            if (status.BatteryLifePercent <= 1f && status.BatteryLifePercent >= 0f)
                battery = status.BatteryLifePercent;
        }
        catch { }

        var idle = IdleSeconds();
        var focused = false;
        try
        {
            var timer = WindowsBuddy.Tools.Timer.TimerService.Instance;
            focused = timer.PomoIsRunning || timer.CountIsRunning;
        }
        catch { }

        if (battery <= 0.08f && (hour >= 21 || hour < 5)) _current = Cry;
        else if (battery <= 0.15f) _current = Sad;
        else if (idle > 480 || hour >= 22 || hour < 6) _current = Sleepy;
        else if (focused) _current = Ego;
        else if (idle > 90) _current = Surprised;
        else if (hour < 11 || battery >= 0.8f) _current = Happy;
        else
        {
            var configured = Normalize(SettingsServiceLikeEmotion());
            _current = configured == Auto ? Happy : configured;
        }

        return _current;
    }

    private static string SettingsServiceLikeEmotion()
    {
        try { return EyesPreferences.Instance.Emotion; }
        catch { return Auto; }
    }

    private static double IdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        return (Environment.TickCount - info.dwTime) / 1000.0;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
}
