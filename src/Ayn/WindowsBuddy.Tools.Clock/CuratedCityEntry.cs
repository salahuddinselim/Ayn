namespace WindowsBuddy.Tools.Clock;

public class CuratedCityEntry
{
	public string CityName { get; private set; }

	public string TimeZoneId { get; private set; }

	public string CountryOrRegion { get; private set; }

	public CuratedCityEntry(string cityName, string timeZoneId, string countryOrRegion)
	{
		CityName = cityName;
		TimeZoneId = timeZoneId;
		CountryOrRegion = countryOrRegion;
	}
}
