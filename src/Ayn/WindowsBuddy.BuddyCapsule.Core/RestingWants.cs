namespace WindowsBuddy.BuddyCapsule.Core;

internal struct RestingWants
{
	public bool Time;

	public bool Analog;

	public bool Date;

	public bool Bluetooth;

	public bool NowPlaying;

	public bool Battery;

	public bool Charging;

	public string Key => (Time ? "T" : "") + (Analog ? "A" : "") + (Date ? "D" : "") + (Bluetooth ? "B" : "") + (NowPlaying ? "N" : "") + (Battery ? "Y" : "") + (Charging ? "C" : "");

	public static RestingWants Parse(string list)
	{
		RestingWants result = default(RestingWants);
		string[] array = (list ?? "").Split(',');
		foreach (string text in array)
		{
			switch (text.Trim().ToLowerInvariant())
			{
			case "time":
				result.Time = true;
				break;
			case "analog":
				result.Analog = true;
				break;
			case "date":
				result.Date = true;
				break;
			case "bluetooth":
				result.Bluetooth = true;
				break;
			case "nowplaying":
				result.NowPlaying = true;
				break;
			case "battery":
				result.Battery = true;
				break;
			case "charging":
				result.Charging = true;
				break;
			}
		}
		return result;
	}
}
