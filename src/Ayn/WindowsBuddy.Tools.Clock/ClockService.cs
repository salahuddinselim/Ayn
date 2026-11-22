using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Clock;

public class ClockService
{
	private static ClockService _instance;

	private static readonly object _syncLock = new object();

	private DispatcherTimer _tickTimer;

	private readonly object _timerLock = new object();

	private int _tickFailures;

	private static readonly CuratedCityEntry[] CuratedCities = new CuratedCityEntry[103]
	{
		new CuratedCityEntry("New York", "Eastern Standard Time", "United States"),
		new CuratedCityEntry("Los Angeles", "Pacific Standard Time", "United States"),
		new CuratedCityEntry("Chicago", "Central Standard Time", "United States"),
		new CuratedCityEntry("Houston", "Central Standard Time", "United States"),
		new CuratedCityEntry("Phoenix", "US Mountain Standard Time", "United States"),
		new CuratedCityEntry("Philadelphia", "Eastern Standard Time", "United States"),
		new CuratedCityEntry("San Antonio", "Central Standard Time", "United States"),
		new CuratedCityEntry("San Diego", "Pacific Standard Time", "United States"),
		new CuratedCityEntry("Dallas", "Central Standard Time", "United States"),
		new CuratedCityEntry("Austin", "Central Standard Time", "United States"),
		new CuratedCityEntry("San Francisco", "Pacific Standard Time", "United States"),
		new CuratedCityEntry("Seattle", "Pacific Standard Time", "United States"),
		new CuratedCityEntry("Denver", "Mountain Standard Time", "United States"),
		new CuratedCityEntry("Boston", "Eastern Standard Time", "United States"),
		new CuratedCityEntry("Miami", "Eastern Standard Time", "United States"),
		new CuratedCityEntry("Atlanta", "Eastern Standard Time", "United States"),
		new CuratedCityEntry("Washington", "Eastern Standard Time", "United States"),
		new CuratedCityEntry("Las Vegas", "Pacific Standard Time", "United States"),
		new CuratedCityEntry("Honolulu", "Hawaiian Standard Time", "United States"),
		new CuratedCityEntry("Anchorage", "Alaskan Standard Time", "United States"),
		new CuratedCityEntry("Toronto", "Eastern Standard Time", "Canada"),
		new CuratedCityEntry("Montreal", "Eastern Standard Time", "Canada"),
		new CuratedCityEntry("Vancouver", "Pacific Standard Time", "Canada"),
		new CuratedCityEntry("Calgary", "Mountain Standard Time", "Canada"),
		new CuratedCityEntry("Mexico City", "Central Standard Time (Mexico)", "Mexico"),
		new CuratedCityEntry("London", "GMT Standard Time", "United Kingdom"),
		new CuratedCityEntry("Dublin", "GMT Standard Time", "Ireland"),
		new CuratedCityEntry("Edinburgh", "GMT Standard Time", "United Kingdom"),
		new CuratedCityEntry("Paris", "W. Europe Standard Time", "France"),
		new CuratedCityEntry("Berlin", "W. Europe Standard Time", "Germany"),
		new CuratedCityEntry("Amsterdam", "W. Europe Standard Time", "Netherlands"),
		new CuratedCityEntry("Rome", "W. Europe Standard Time", "Italy"),
		new CuratedCityEntry("Madrid", "W. Europe Standard Time", "Spain"),
		new CuratedCityEntry("Barcelona", "W. Europe Standard Time", "Spain"),
		new CuratedCityEntry("Zurich", "W. Europe Standard Time", "Switzerland"),
		new CuratedCityEntry("Geneva", "W. Europe Standard Time", "Switzerland"),
		new CuratedCityEntry("Vienna", "W. Europe Standard Time", "Austria"),
		new CuratedCityEntry("Brussels", "W. Europe Standard Time", "Belgium"),
		new CuratedCityEntry("Stockholm", "W. Europe Standard Time", "Sweden"),
		new CuratedCityEntry("Oslo", "W. Europe Standard Time", "Norway"),
		new CuratedCityEntry("Copenhagen", "W. Europe Standard Time", "Denmark"),
		new CuratedCityEntry("Helsinki", "FLE Standard Time", "Finland"),
		new CuratedCityEntry("Lisbon", "GMT Standard Time", "Portugal"),
		new CuratedCityEntry("Athens", "GTB Standard Time", "Greece"),
		new CuratedCityEntry("Warsaw", "Central European Standard Time", "Poland"),
		new CuratedCityEntry("Prague", "Central European Standard Time", "Czech Republic"),
		new CuratedCityEntry("Budapest", "Central European Standard Time", "Hungary"),
		new CuratedCityEntry("Istanbul", "Turkey Standard Time", "Turkey"),
		new CuratedCityEntry("Moscow", "Russian Standard Time", "Russia"),
		new CuratedCityEntry("Dubai", "Arabian Standard Time", "United Arab Emirates"),
		new CuratedCityEntry("Abu Dhabi", "Arabian Standard Time", "United Arab Emirates"),
		new CuratedCityEntry("Doha", "Arabian Standard Time", "Qatar"),
		new CuratedCityEntry("Riyadh", "Arab Standard Time", "Saudi Arabia"),
		new CuratedCityEntry("Kuwait City", "Arab Standard Time", "Kuwait"),
		new CuratedCityEntry("Tel Aviv", "Israel Standard Time", "Israel"),
		new CuratedCityEntry("Jerusalem", "Israel Standard Time", "Israel"),
		new CuratedCityEntry("Cairo", "Egypt Standard Time", "Egypt"),
		new CuratedCityEntry("Johannesburg", "South Africa Standard Time", "South Africa"),
		new CuratedCityEntry("Cape Town", "South Africa Standard Time", "South Africa"),
		new CuratedCityEntry("Nairobi", "E. Africa Standard Time", "Kenya"),
		new CuratedCityEntry("Mumbai", "India Standard Time", "India"),
		new CuratedCityEntry("New Delhi", "India Standard Time", "India"),
		new CuratedCityEntry("Delhi", "India Standard Time", "India"),
		new CuratedCityEntry("Bengaluru", "India Standard Time", "India"),
		new CuratedCityEntry("Bangalore", "India Standard Time", "India"),
		new CuratedCityEntry("Kolkata", "India Standard Time", "India"),
		new CuratedCityEntry("Chennai", "India Standard Time", "India"),
		new CuratedCityEntry("Hyderabad", "India Standard Time", "India"),
		new CuratedCityEntry("Karachi", "Pakistan Standard Time", "Pakistan"),
		new CuratedCityEntry("Lahore", "Pakistan Standard Time", "Pakistan"),
		new CuratedCityEntry("Islamabad", "Pakistan Standard Time", "Pakistan"),
		new CuratedCityEntry("Dhaka", "Bangladesh Standard Time", "Bangladesh"),
		new CuratedCityEntry("Colombo", "Sri Lanka Standard Time", "Sri Lanka"),
		new CuratedCityEntry("Kathmandu", "Nepal Standard Time", "Nepal"),
		new CuratedCityEntry("Bangkok", "SE Asia Standard Time", "Thailand"),
		new CuratedCityEntry("Singapore", "Singapore Standard Time", "Singapore"),
		new CuratedCityEntry("Kuala Lumpur", "Singapore Standard Time", "Malaysia"),
		new CuratedCityEntry("Jakarta", "SE Asia Standard Time", "Indonesia"),
		new CuratedCityEntry("Manila", "Singapore Standard Time", "Philippines"),
		new CuratedCityEntry("Hanoi", "SE Asia Standard Time", "Vietnam"),
		new CuratedCityEntry("Ho Chi Minh City", "SE Asia Standard Time", "Vietnam"),
		new CuratedCityEntry("Hong Kong", "China Standard Time", "China"),
		new CuratedCityEntry("Beijing", "China Standard Time", "China"),
		new CuratedCityEntry("Shanghai", "China Standard Time", "China"),
		new CuratedCityEntry("Shenzhen", "China Standard Time", "China"),
		new CuratedCityEntry("Guangzhou", "China Standard Time", "China"),
		new CuratedCityEntry("Taipei", "Taipei Standard Time", "Taiwan"),
		new CuratedCityEntry("Seoul", "Korea Standard Time", "South Korea"),
		new CuratedCityEntry("Tokyo", "Tokyo Standard Time", "Japan"),
		new CuratedCityEntry("Osaka", "Tokyo Standard Time", "Japan"),
		new CuratedCityEntry("Sydney", "AUS Eastern Standard Time", "Australia"),
		new CuratedCityEntry("Melbourne", "AUS Eastern Standard Time", "Australia"),
		new CuratedCityEntry("Brisbane", "E. Australia Standard Time", "Australia"),
		new CuratedCityEntry("Perth", "W. Australia Standard Time", "Australia"),
		new CuratedCityEntry("Adelaide", "AUS Central Standard Time", "Australia"),
		new CuratedCityEntry("Auckland", "New Zealand Standard Time", "New Zealand"),
		new CuratedCityEntry("Wellington", "New Zealand Standard Time", "New Zealand"),
		new CuratedCityEntry("Sao Paulo", "E. South America Standard Time", "Brazil"),
		new CuratedCityEntry("Rio de Janeiro", "E. South America Standard Time", "Brazil"),
		new CuratedCityEntry("Buenos Aires", "Argentina Standard Time", "Argentina"),
		new CuratedCityEntry("Santiago", "Pacific SA Standard Time", "Chile"),
		new CuratedCityEntry("Bogota", "SA Pacific Standard Time", "Colombia"),
		new CuratedCityEntry("Lima", "SA Pacific Standard Time", "Peru")
	};

	public static ClockService Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_syncLock)
				{
					if (_instance == null)
					{
						_instance = new ClockService();
					}
				}
			}
			return _instance;
		}
	}

	public event EventHandler<DateTime> SecondTick;

	public event EventHandler SystemTimeChanged;

	private ClockService()
	{
		try
		{
			SystemEvents.TimeChanged += OnSystemTimeChanged;
			SystemEvents.PowerModeChanged += OnPowerModeChanged;
		}
		catch
		{
		}
		InitializeTickTimer();
	}

	private void InitializeTickTimer()
	{
		try
		{
			Dispatcher dispatcher = ((Application.Current != null) ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);
			if (dispatcher == null)
			{
				return;
			}
			if (dispatcher.CheckAccess())
			{
				CreateTimerInternal(dispatcher);
				return;
			}
			dispatcher.BeginInvoke((Action)delegate
			{
				CreateTimerInternal(dispatcher);
			});
		}
		catch
		{
		}
	}

	private void CreateTimerInternal(Dispatcher dispatcher)
	{
		lock (_timerLock)
		{
			if (_tickTimer == null)
			{
				_tickTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
				_tickTimer.Tick += OnTimerTick;
				ResyncTickTimer();
				_tickTimer.Start();
			}
		}
	}

	private void OnTimerTick(object sender, EventArgs e)
	{
		DateTime now = DateTime.Now;
		int num = 1000 - now.Millisecond;
		if (num < 15)
		{
			num += 1000;
		}
		lock (_timerLock)
		{
			if (_tickTimer != null)
			{
				_tickTimer.Interval = TimeSpan.FromMilliseconds(num);
			}
		}
		RaiseSecondTick(now);
	}

	private void RaiseSecondTick(DateTime now)
	{
		EventHandler<DateTime> secondTick = this.SecondTick;
		if (secondTick == null)
		{
			return;
		}
		Delegate[] invocationList = secondTick.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<DateTime> eventHandler = (EventHandler<DateTime>)invocationList[i];
			try
			{
				eventHandler(this, now);
			}
			catch (Exception ex)
			{
				if (_tickFailures++ < 5)
				{
					TransactionLogger.Warn("Clock tick listener failed: " + ex.GetType().Name + ": " + ex.Message);
				}
			}
		}
	}

	public void ResyncTickTimer()
	{
		lock (_timerLock)
		{
			if (_tickTimer != null)
			{
				int num = 1000 - DateTime.Now.Millisecond;
				if (num < 15)
				{
					num += 1000;
				}
				_tickTimer.Interval = TimeSpan.FromMilliseconds(num);
			}
		}
	}

	private void OnSystemTimeChanged(object sender, EventArgs e)
	{
		try
		{
			TimeZoneInfo.ClearCachedData();
		}
		catch
		{
		}
		SafeResyncAndNotify();
	}

	private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.Resume)
		{
			SafeResyncAndNotify();
		}
	}

	private void SafeResyncAndNotify()
	{
		try
		{
			Dispatcher dispatcher = ((Application.Current != null) ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);
			if (dispatcher != null && !dispatcher.CheckAccess())
			{
				dispatcher.BeginInvoke((Action)delegate
				{
					ResyncTickTimer();
					NotifyTimeChanged();
					RaiseSecondTick(DateTime.Now);
				});
			}
			else
			{
				ResyncTickTimer();
				NotifyTimeChanged();
				RaiseSecondTick(DateTime.Now);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Clock resync failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	public void NotifyTimeChanged()
	{
		if (this.SystemTimeChanged != null)
		{
			try
			{
				this.SystemTimeChanged(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Clock time-change listener failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
	}

	public bool TryGetCityTime(string timeZoneId, out DateTime time)
	{
		time = DateTime.Now;
		if (string.IsNullOrEmpty(timeZoneId))
		{
			return true;
		}
		try
		{
			time = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
			return true;
		}
		catch (TimeZoneNotFoundException)
		{
			return false;
		}
		catch (InvalidTimeZoneException)
		{
			return false;
		}
	}

	public DateTime GetCityTime(string timeZoneId)
	{
		try
		{
			if (string.IsNullOrEmpty(timeZoneId))
			{
				return DateTime.Now;
			}
			TimeZoneInfo destinationTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
			return TimeZoneInfo.ConvertTime(DateTime.UtcNow, destinationTimeZone);
		}
		catch
		{
			return DateTime.Now;
		}
	}

	public string GetTimeZoneAbbreviation(string timeZoneId)
	{
		try
		{
			if (string.IsNullOrEmpty(timeZoneId))
			{
				timeZoneId = TimeZoneInfo.Local.Id;
			}
			TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
			DateTime utcNow = DateTime.UtcNow;
			bool flag = timeZoneInfo.IsDaylightSavingTime(TimeZoneInfo.ConvertTime(utcNow, timeZoneInfo));
			TimeSpan utcOffset = timeZoneInfo.GetUtcOffset(TimeZoneInfo.ConvertTime(utcNow, timeZoneInfo));
			switch (timeZoneInfo.Id)
			{
			case "Eastern Standard Time":
				return flag ? "EDT" : "EST";
			case "Central Standard Time":
				return flag ? "CDT" : "CST";
			case "Mountain Standard Time":
				return flag ? "MDT" : "MST";
			case "Pacific Standard Time":
				return flag ? "PDT" : "PST";
			case "GMT Standard Time":
				return flag ? "BST" : "GMT";
			case "W. Europe Standard Time":
				return flag ? "CEST" : "CET";
			case "Central European Standard Time":
				return flag ? "CEST" : "CET";
			case "India Standard Time":
				return "IST";
			case "Tokyo Standard Time":
				return "JST";
			case "Singapore Standard Time":
				return "SGT";
			case "China Standard Time":
				return "CST";
			case "AUS Eastern Standard Time":
				return flag ? "AEDT" : "AEST";
			case "UTC":
				return "UTC";
			default:
			{
				string arg = ((utcOffset.TotalMinutes >= 0.0) ? "+" : "-");
				int num = Math.Abs(utcOffset.Hours);
				int num2 = Math.Abs(utcOffset.Minutes);
				if (num2 > 0)
				{
					return $"UTC{arg}{num}:{num2:D2}";
				}
				return $"UTC{arg}{num}";
			}
			}
		}
		catch
		{
			return "UTC";
		}
	}

	private static TimeZoneOption CreateOptionFromCity(CuratedCityEntry city)
	{
		TimeSpan baseUtcOffset = TimeSpan.Zero;
		try
		{
			TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZoneId);
			baseUtcOffset = timeZoneInfo.BaseUtcOffset;
		}
		catch
		{
		}
		TimeZoneOption timeZoneOption = new TimeZoneOption();
		timeZoneOption.Id = city.TimeZoneId;
		timeZoneOption.DisplayName = city.CityName + ", " + city.CountryOrRegion;
		timeZoneOption.CitySuggestion = city.CityName;
		timeZoneOption.BaseUtcOffset = baseUtcOffset;
		return timeZoneOption;
	}

	public IReadOnlyList<TimeZoneOption> SearchCitiesAndTimeZones(string filter)
	{
		List<TimeZoneOption> list = new List<TimeZoneOption>();
		string value = (filter ?? "").Trim();
		if (string.IsNullOrEmpty(value))
		{
			string[] array = new string[12]
			{
				"New York", "London", "Tokyo", "Paris", "Dubai", "Mumbai", "Singapore", "Sydney", "San Francisco", "Berlin",
				"Toronto", "Hong Kong"
			};
			foreach (string value2 in array)
			{
				for (int j = 0; j < CuratedCities.Length; j++)
				{
					if (CuratedCities[j].CityName.Equals(value2, StringComparison.OrdinalIgnoreCase))
					{
						list.Add(CreateOptionFromCity(CuratedCities[j]));
						break;
					}
				}
			}
			return list;
		}
		for (int k = 0; k < CuratedCities.Length; k++)
		{
			CuratedCityEntry curatedCityEntry = CuratedCities[k];
			if (curatedCityEntry.CityName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 || curatedCityEntry.CountryOrRegion.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				list.Add(CreateOptionFromCity(curatedCityEntry));
			}
		}
		if (list.Count > 0)
		{
			return list;
		}
		try
		{
			foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
			{
				if (systemTimeZone.DisplayName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 || systemTimeZone.Id.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					string text = systemTimeZone.DisplayName;
					int num = text.IndexOf(')');
					if (num >= 0 && num < text.Length - 1)
					{
						text = text.Substring(num + 1).Trim();
					}
					list.Add(new TimeZoneOption
					{
						Id = systemTimeZone.Id,
						DisplayName = systemTimeZone.DisplayName,
						CitySuggestion = text,
						BaseUtcOffset = systemTimeZone.BaseUtcOffset
					});
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public IReadOnlyList<TimeZoneOption> GetAvailableTimeZones()
	{
		return SearchCitiesAndTimeZones(string.Empty);
	}
}
