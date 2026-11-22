namespace WindowsBuddy.Tools.Weather;

public class HourlyForecast
{
	public string Time { get; set; }

	public double Temp { get; set; }

	public int WeatherCode { get; set; }

	public bool IsDay { get; set; }

	public double UvIndex { get; set; }
}
