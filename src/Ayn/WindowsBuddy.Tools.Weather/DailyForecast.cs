namespace WindowsBuddy.Tools.Weather;

public class DailyForecast
{
	public string Date { get; set; }

	public string DayName { get; set; }

	public double MaxTemp { get; set; }

	public double MinTemp { get; set; }

	public string Condition { get; set; }

	public int WeatherCode { get; set; }

	public string Sunrise { get; set; }

	public string Sunset { get; set; }
}
