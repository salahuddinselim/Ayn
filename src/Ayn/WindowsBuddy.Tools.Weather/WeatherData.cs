using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Weather;

public class WeatherData
{
	public bool IsConfigured { get; set; }

	public bool IsOffline { get; set; }

	public string StatusMessage { get; set; }

	public string City { get; set; }

	public double Latitude { get; set; }

	public double Longitude { get; set; }

	public double Temperature { get; set; }

	public double FeelsLike { get; set; }

	public string Condition { get; set; }

	public int WeatherCode { get; set; }

	public bool IsDay { get; set; }

	public int Humidity { get; set; }

	public double WindSpeed { get; set; }

	public double UvIndex { get; set; }

	public int UtcOffsetSeconds { get; set; }

	public string Sunrise { get; set; }

	public string Sunset { get; set; }

	public List<DailyForecast> Forecast { get; set; }

	public List<HourlyForecast> Hourly { get; set; }

	public string LastUpdatedFormatted { get; set; }

	public long LastUpdatedTicks { get; set; }

	[ScriptIgnore]
	public bool HasData
	{
		get
		{
			if (LastUpdatedTicks <= 0)
			{
				if (Forecast != null)
				{
					return Forecast.Count > 0;
				}
				return false;
			}
			return true;
		}
	}

	public WeatherData()
	{
		IsConfigured = false;
		IsOffline = false;
		IsDay = true;
		StatusMessage = string.Empty;
		City = string.Empty;
		Condition = string.Empty;
		Forecast = new List<DailyForecast>();
		Hourly = new List<HourlyForecast>();
	}
}
