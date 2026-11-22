namespace WindowsBuddy.Services;

public static class NotchCompanions
{
    public const string Eyes = "eyes";

    public static readonly string[] All =
    [
        Eyes, "ditto", "cat", "husky", "pikachu", "doraemon", "dots", "grok", "robot"
    ];

    public static string Normalize(string id)
    {
        id = (id ?? "").Trim().ToLowerInvariant();
        foreach (var item in All)
            if (item == id) return item;
        return Eyes;
    }

    public static bool IsPet(string id) => Normalize(id) != Eyes;

    public static string Title(string id) => Normalize(id) switch
    {
        "ditto" => "Ditto",
        "cat" => "Cat",
        "husky" => "Husky",
        "pikachu" => "Pikachu",
        "doraemon" => "Doraemon",
        "dots" => "Dots",
        "grok" => "Grok",
        "robot" => "Robot",
        _ => "Eyes"
    };
}
