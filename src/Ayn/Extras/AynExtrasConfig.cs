using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WindowsBuddy.Services;

namespace WindowsBuddy.Extras;

public sealed class AynExtrasConfig
{
    public bool BreakEnabled { get; set; } = true;
    public int WorkMinutes { get; set; } = 60;
    public int BreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 10;

    public bool MealEnabled { get; set; } = true;
    public string LunchTime { get; set; } = "12:30";
    public string DinnerTime { get; set; } = "19:30";

    public bool SalatEnabled { get; set; } = true;
    public string SalatMethod { get; set; } = "Karachi";
    public double Latitude { get; set; } = 23.8103;
    public double Longitude { get; set; } = 90.4125;
    public string TimezoneId { get; set; } = "Asia/Dhaka";

    public string City { get; set; } = "Dhaka";
    public string Country { get; set; } = "Bangladesh";

    public double GlassOuterOpacity { get; set; } = 0.08;
    public double GlassCardOpacity { get; set; } = 0.55;
    public double GlassBlur { get; set; } = 16;
    public double GlassBorderOpacity { get; set; } = 0.20;

    public double SettingsTransparency { get; set; }
    public double NotchTransparency { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static string ConfigPath => Path.Combine(AppPaths.DataDir, "ayn_extras.json");

    public static AynExtrasConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var loaded = JsonSerializer.Deserialize<AynExtrasConfig>(File.ReadAllText(ConfigPath), Options) ?? new();
                loaded.Sanitize();
                return loaded;
            }
        }
        catch { }
        return new();
    }

    public void Sanitize()
    {
        WorkMinutes = Math.Clamp(WorkMinutes, 1, 480);
        BreakMinutes = Math.Clamp(BreakMinutes, 1, 60);
        SettingsTransparency = Math.Clamp(SettingsTransparency, 0, 100);
        NotchTransparency = Math.Clamp(NotchTransparency, 0, 100);
    }

    public void Save()
    {
        Sanitize();
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, Options));
        }
        catch { }
    }
}
