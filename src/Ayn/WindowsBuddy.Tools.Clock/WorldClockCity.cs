namespace WindowsBuddy.Tools.Clock;

public class WorldClockCity
{
	public string Id { get; set; }

	public string CityName { get; set; }

	public string TimeZoneId { get; set; }

	public bool IsLocal { get; set; }

	public WorldClockCity()
	{
	}

	public WorldClockCity(string id, string cityName, string timeZoneId, bool isLocal = false)
	{
		Id = id;
		CityName = cityName;
		TimeZoneId = timeZoneId;
		IsLocal = isLocal;
	}
}
