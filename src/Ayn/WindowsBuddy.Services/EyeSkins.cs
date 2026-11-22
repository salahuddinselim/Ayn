namespace WindowsBuddy.Services;

public static class EyeSkins
{
	public const string Ayn = "ayn";

	public const string Bubble = "bubble";

	public const string Honey = "honey";

	public const string Jade = "jade";

	public static readonly string[] All = new string[4] { "ayn", "bubble", "honey", "jade" };

	public static string Normalize(string id)
	{
		id = (id ?? "").Trim().ToLowerInvariant();
		string[] all = All;
		foreach (string text in all)
		{
			if (text == id)
			{
				return text;
			}
		}
		return "ayn";
	}

	public static string Title(string id)
	{
		return Normalize(id) switch
		{
			"bubble" => "Bubble", 
			"honey" => "Honey", 
			"jade" => "Jade", 
			_ => "Ayn", 
		};
	}

	public static string Tagline(string id)
	{
		return Normalize(id) switch
		{
			"bubble" => "Big glossy toy eyes", 
			"honey" => "Warm amber iris, pupils widen as you come close", 
			"jade" => "Cat eyes, slit opens round when you're near", 
			_ => "The logo's pill eyes, alive", 
		};
	}
}
