using System.Collections.Generic;

namespace WindowsBuddy.Tools.Weather;

public class WeatherPrefs
{
	public bool UseFahrenheit { get; set; }

	public string WindUnit { get; set; }

	public bool FollowLocation { get; set; }

	public bool UseIpLocation { get; set; }

	public string LocationSource { get; set; }

	public List<RecentCity> Recent { get; set; }

	public WeatherPrefs()
	{
		Recent = new List<RecentCity>();
	}
}
