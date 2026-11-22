namespace WindowsBuddy.Tools.Weather;

public class GeocodedCity
{
	public string Name { get; set; }

	public string Country { get; set; }

	public string Admin1 { get; set; }

	public string Admin2 { get; set; }

	public string CountryCode { get; set; }

	public long Population { get; set; }

	public double Latitude { get; set; }

	public double Longitude { get; set; }

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrEmpty(Admin1) && !string.IsNullOrEmpty(Country))
			{
				return $"{Name}, {Admin1}, {Country}";
			}
			if (!string.IsNullOrEmpty(Country))
			{
				return $"{Name}, {Country}";
			}
			return Name;
		}
	}

	public string ShortName
	{
		get
		{
			string text = ((!string.IsNullOrEmpty(Admin1) && Admin1 != Name) ? Admin1 : Country);
			if (!string.IsNullOrEmpty(text))
			{
				return Name + ", " + text;
			}
			return Name;
		}
	}
}
