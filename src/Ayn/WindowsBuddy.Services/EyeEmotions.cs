namespace WindowsBuddy.Services;

public static class EyeEmotions
{
    public const string Neutral = "neutral";
    public const string Happy = "happy";
    public const string Sad = "sad";
    public const string Cry = "cry";
    public const string Ego = "ego";
    public const string Surprised = "surprised";
    public const string Sleepy = "sleepy";

    public static readonly string[] All =
    [
        Neutral, Happy, Sad, Cry, Ego, Surprised, Sleepy
    ];

    public static string Normalize(string id)
    {
        id = (id ?? "").Trim().ToLowerInvariant();
        foreach (var item in All)
            if (item == id) return item;
        return Neutral;
    }

    public static string Title(string id) => Normalize(id) switch
    {
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
        Happy => "Warm smile, soft squint",
        Sad => "Drooping lids and a quiet gaze",
        Cry => "Teary eyes and a low gaze",
        Ego => "Sharp brows, narrowed stare",
        Surprised => "Wide open, fully dilated",
        Sleepy => "Heavy lids, slow blink",
        _ => "Just being itself"
    };
}
