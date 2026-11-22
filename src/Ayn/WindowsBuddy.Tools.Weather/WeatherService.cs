using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.Tools.Weather;

public class WeatherService
{
	private const int MaxRecent = 5;

	public const string CoarseProblem = "Couldn't find your exact town. Search for it once.";

	public const string DeniedProblem = "Turn on location for exact weather";

	public const double PreciseAccuracyMeters = 5000.0;

	private static WeatherService _instance;

	private readonly string _cachePath;

	private readonly string _prefsPath;

	private WeatherData _current;

	private WeatherPrefs _prefs;

	private readonly Dispatcher _ui;

	private readonly DispatcherTimer _refreshTimer;

	private SafeWebClient _activeWeatherClient;

	private readonly object _weatherLock = new object();

	private int _fetchGen;

	private bool _isFetching;

	internal static bool LocationDeniedForQa;

	internal static Func<bool, CancellationToken, Task<WeatherFix>> LocationResolver = WindowsWeatherLocation.ResolveAsync;

	private CancellationTokenSource _locationCancel;

	private int _locationGeneration;

	private bool _locating;

	private DateTime _lastLocationRequest;

	private DateTime _lastOsmSearch = DateTime.MinValue;

	private readonly CallbackLifetime _callbacks = new CallbackLifetime();

	public static WeatherService Existing => _instance;

	public static WeatherService Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new WeatherService();
			}
			return _instance;
		}
	}

	public WeatherData Current => _current;

	public bool IsConfigured
	{
		get
		{
			if (_current != null)
			{
				return _current.IsConfigured;
			}
			return false;
		}
	}

	public WeatherFetchState FetchState { get; private set; }

	public bool IsFetching => _isFetching;

	public bool UseFahrenheit
	{
		get
		{
			return _prefs.UseFahrenheit;
		}
		set
		{
			if (_prefs.UseFahrenheit != value)
			{
				_prefs.UseFahrenheit = value;
				SavePrefs();
				NotifyUpdated();
			}
		}
	}

	public bool FollowLocation => _prefs.FollowLocation;

	public IList<RecentCity> RecentCities => _prefs.Recent.AsReadOnly();

	public string UnitSymbol
	{
		get
		{
			if (!_prefs.UseFahrenheit)
			{
				return "°C";
			}
			return "°F";
		}
	}

	public bool WindInMph
	{
		get
		{
			if (string.Equals(_prefs.WindUnit, "mph", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (string.Equals(_prefs.WindUnit, "kmh", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			return _prefs.UseFahrenheit;
		}
		set
		{
			string text = (value ? "mph" : "kmh");
			if (!(_prefs.WindUnit == text))
			{
				_prefs.WindUnit = text;
				SavePrefs();
				NotifyUpdated();
			}
		}
	}

	public DateTime PlaceNow
	{
		get
		{
			if (_current == null || _current.LastUpdatedTicks == 0)
			{
				return DateTime.Now;
			}
			return DateTime.UtcNow.AddSeconds(_current.UtcOffsetSeconds);
		}
	}

	private static bool Uses24h => ClockSettings.Uses24Hour;

	public bool LocationNeedsAttention { get; private set; }

	public string LocationSource => _prefs.LocationSource ?? "manual";

	public string LocationProblem { get; private set; }

	public bool LocationIsCoarse
	{
		get
		{
			if (LocationNeedsAttention)
			{
				return LocationProblem == "Couldn't find your exact town. Search for it once.";
			}
			return false;
		}
	}

	public bool IsLocating => _locating;

	private static string AppVersionForUserAgent
	{
		get
		{
			try
			{
				return typeof(WeatherService).Assembly.GetName().Version.ToString(3);
			}
			catch
			{
				return "1";
			}
		}
	}

	public event EventHandler WeatherUpdated;

	public WeatherService()
	{
		_ui = ((Application.Current != null) ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);
		string localAppData = AppPaths.LocalAppData;
		string text = Path.Combine(localAppData, AppPaths.FolderName, "weather");
		if (!Directory.Exists(text))
		{
			try
			{
				Directory.CreateDirectory(text);
			}
			catch
			{
			}
		}
		_cachePath = Path.Combine(text, "cache.json");
		_prefsPath = Path.Combine(text, "prefs.json");
		LoadCache();
		if (_current == null)
		{
			_current = new WeatherData();
		}
		if (_current.Forecast == null)
		{
			_current.Forecast = new List<DailyForecast>();
		}
		if (_current.Hourly == null)
		{
			_current.Hourly = new List<HourlyForecast>();
		}
		LoadPrefs();
		PersistenceHelper.RegisterExitFlush("weather preferences", SavePrefs);
		_refreshTimer = new DispatcherTimer(DispatcherPriority.Background, _ui)
		{
			Interval = TimeSpan.FromMinutes(30.0)
		};
		_refreshTimer.Tick += delegate
		{
			Refresh();
		};
		_refreshTimer.Start();
		NetworkChange.NetworkAddressChanged += NetworkChanged;
		if (_current.IsConfigured)
		{
			FetchWeatherAsync();
		}
		try
		{
			NetworkChange.NetworkAvailabilityChanged += delegate(object s, NetworkAvailabilityEventArgs e)
			{
				if (e.IsAvailable)
				{
					OnUi(delegate
					{
						if (FetchState == WeatherFetchState.Offline || FetchState == WeatherFetchState.Error)
						{
							Refresh();
						}
					});
				}
			};
		}
		catch
		{
		}
		if (_prefs.FollowLocation || !_current.IsConfigured)
		{
			TryResolveLocationAsync();
		}
	}

	public int Temp(double celsius)
	{
		return (int)Math.Round(_prefs.UseFahrenheit ? (celsius * 9.0 / 5.0 + 32.0) : celsius, MidpointRounding.AwayFromZero);
	}

	public string Wind(double kmh)
	{
		if (!WindInMph)
		{
			return Math.Round(kmh).ToString(CultureInfo.CurrentCulture) + " km/h";
		}
		return Math.Round(kmh * 0.621371).ToString(CultureInfo.CurrentCulture) + " mph";
	}

	public static string UvLevel(double uv)
	{
		if (uv < 3.0)
		{
			return "Low";
		}
		if (uv < 6.0)
		{
			return "Moderate";
		}
		if (uv < 8.0)
		{
			return "High";
		}
		if (uv < 11.0)
		{
			return "Very high";
		}
		return "Extreme";
	}

	public static DateTime? ParseLocal(string iso)
	{
		if (!string.IsNullOrEmpty(iso) && DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return result;
		}
		return null;
	}

	public bool IsDayNow()
	{
		if (_current == null)
		{
			return true;
		}
		List<DailyForecast> list = UpcomingDays(1);
		if (list.Count > 0)
		{
			DateTime? dateTime = ParseLocal(list[0].Sunrise);
			DateTime? dateTime2 = ParseLocal(list[0].Sunset);
			DateTime? dateTime3 = ParseLocal(list[0].Date);
			if (dateTime.HasValue && dateTime2.HasValue && dateTime3.HasValue && dateTime3.Value.Date == PlaceNow.Date)
			{
				DateTime placeNow = PlaceNow;
				if (placeNow >= dateTime.Value)
				{
					return placeNow < dateTime2.Value;
				}
				return false;
			}
		}
		return _current.IsDay;
	}

	public static string Clock(DateTime t)
	{
		return t.ToString(Uses24h ? "HH:mm" : "h:mm", CultureInfo.CurrentCulture);
	}

	public static string HourLabel(DateTime t)
	{
		if (Uses24h)
		{
			return t.ToString("HH:00", CultureInfo.CurrentCulture);
		}
		string text = t.ToString("h tt", CultureInfo.CurrentCulture).Trim();
		if (text.Length <= 0)
		{
			return t.ToString("HH:00", CultureInfo.CurrentCulture);
		}
		return text;
	}

	public List<HourlyForecast> UpcomingHours(int count)
	{
		List<HourlyForecast> list = new List<HourlyForecast>();
		if (_current == null || _current.Hourly == null)
		{
			return list;
		}
		DateTime placeNow = PlaceNow;
		DateTime dateTime = new DateTime(placeNow.Year, placeNow.Month, placeNow.Day, placeNow.Hour, 0, 0);
		foreach (HourlyForecast item in _current.Hourly)
		{
			DateTime? dateTime2 = ParseLocal(item.Time);
			if (dateTime2.HasValue && !(dateTime2.Value < dateTime))
			{
				list.Add(item);
				if (list.Count == count)
				{
					break;
				}
			}
		}
		return list;
	}

	public List<DailyForecast> UpcomingDays(int count)
	{
		List<DailyForecast> list = new List<DailyForecast>();
		if (_current == null || _current.Forecast == null)
		{
			return list;
		}
		DateTime date = PlaceNow.Date;
		foreach (DailyForecast item in _current.Forecast)
		{
			DateTime? dateTime = ParseLocal(item.Date);
			if (!dateTime.HasValue || !(dateTime.Value.Date < date))
			{
				list.Add(item);
				if (list.Count == count)
				{
					break;
				}
			}
		}
		return list;
	}

	public string DayLabel(DailyForecast d)
	{
		DateTime? dateTime = ParseLocal(d.Date);
		if (!dateTime.HasValue)
		{
			return d.DayName ?? string.Empty;
		}
		if (dateTime.Value.Date == PlaceNow.Date)
		{
			return "Today";
		}
		return dateTime.Value.ToString("ddd", CultureInfo.CurrentCulture);
	}

	private void CancelLocation()
	{
		_locationGeneration++;
		_locating = false;
		if (_locationCancel != null)
		{
			_locationCancel.Cancel();
			_locationCancel.Dispose();
			_locationCancel = null;
		}
	}

	public void TryResolveLocationAsync()
	{
		ResolveWindows(requestAccess: false);
	}

	public void UseCurrentLocation()
	{
		_prefs.FollowLocation = true;
		_prefs.UseIpLocation = false;
		SavePrefs();
		ResolveWindows(requestAccess: true);
	}

	private async void ResolveWindows(bool requestAccess)
	{
		if (!_ui.CheckAccess())
		{
			OnUi(delegate
			{
				ResolveWindows(requestAccess);
			});
		}
		else
		{
			if (!requestAccess && (_locating || !_prefs.FollowLocation))
			{
				return;
			}
			CancelLocation();
			int generation = _locationGeneration;
			_locationCancel = new CancellationTokenSource(TimeSpan.FromSeconds(25.0));
			CancellationToken token = _locationCancel.Token;
			_locating = true;
			LocationNeedsAttention = false;
			_lastLocationRequest = DateTime.UtcNow;
			NotifyUpdated();
			bool naming = false;
			try
			{
				int num = default(int);
				_ = num;
				_ = 0;
				try
				{
					if (LocationDeniedForQa)
					{
						throw new UnauthorizedAccessException();
					}
					WeatherFix fix = await LocationResolver(requestAccess, token);
					if (generation != _locationGeneration || !_prefs.FollowLocation || token.IsCancellationRequested)
					{
						return;
					}
					TransactionLogger.Info("Weather location source: " + fix.Source);
					if (!IsPreciseFix(fix))
					{
						TransactionLogger.Info("Weather location: Windows fix too coarse to use (" + (double.IsNaN(fix.Accuracy) ? "unknown" : (((int)Math.Round(fix.Accuracy / 1000.0)).ToString(CultureInfo.InvariantCulture) + " km")) + " accuracy)");
						if (_current != null && _current.IsConfigured)
						{
							_prefs.FollowLocation = false;
							_prefs.LocationSource = "manual";
							SavePrefs();
						}
						LocationFailed("Couldn't find your exact town. Search for it once.");
					}
					else
					{
						naming = true;
						ReverseGeocode(fix.Latitude, fix.Longitude, fix.Source, generation);
					}
				}
				catch (UnauthorizedAccessException)
				{
					if (generation == _locationGeneration)
					{
						LocationFailed("Turn on location for exact weather");
					}
				}
				catch (Exception)
				{
					if (generation == _locationGeneration)
					{
						LocationFailed("Turn on location for exact weather");
					}
				}
			}
			finally
			{
				if (generation == _locationGeneration && !naming)
				{
					_locating = false;
					NotifyUpdated();
				}
			}
		}
	}

	internal static bool IsPreciseFix(WeatherFix fix)
	{
		if (fix != null && fix.Source != "IP" && !double.IsNaN(fix.Accuracy) && fix.Accuracy >= 0.0 && fix.Accuracy <= 5000.0 && !double.IsNaN(fix.Latitude) && !double.IsNaN(fix.Longitude) && Math.Abs(fix.Latitude) <= 90.0)
		{
			return Math.Abs(fix.Longitude) <= 180.0;
		}
		return false;
	}

	private void LocationFailed(string detail)
	{
		LocationNeedsAttention = true;
		LocationProblem = detail;
		_current.StatusMessage = detail;
		FetchState = WeatherFetchState.Error;
		TransactionLogger.Info("Weather location source: unavailable; no automatic IP fallback");
		NotifyUpdated();
	}

	private void ReverseGeocode(double lat, double lon, string source, int generation)
	{
		string url = string.Format(CultureInfo.InvariantCulture, "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={0:F6}&longitude={1:F6}&localityLanguage=en", lat, lon);
		Download(url, 7000, delegate(Dictionary<string, object> json, string err)
		{
			if (generation == _locationGeneration && _prefs.FollowLocation)
			{
				_locating = false;
				string text = ((json == null) ? null : PlaceName(json));
				_prefs.LocationSource = source;
				LocationNeedsAttention = false;
				LocationProblem = null;
				SetLocationInternal(string.IsNullOrEmpty(text) ? "Current location" : text, lat, lon, picked: false);
			}
		});
	}

	public void UseApproximateLocation()
	{
		CancelLocation();
		_prefs.FollowLocation = false;
		_prefs.UseIpLocation = true;
		SavePrefs();
		ResolveByIp();
	}

	private void ResolveByIp()
	{
		if (!_prefs.UseIpLocation || _locating)
		{
			return;
		}
		int generation = _locationGeneration;
		_locating = true;
		NotifyUpdated();
		Download("https://api.bigdatacloud.net/data/reverse-geocode-client?localityLanguage=en", 7000, delegate(Dictionary<string, object> json, string err)
		{
			if (generation == _locationGeneration && _prefs.UseIpLocation)
			{
				_locating = false;
				try
				{
					if (json != null && json.ContainsKey("latitude") && json.ContainsKey("longitude"))
					{
						_prefs.LocationSource = "IP";
						LocationNeedsAttention = false;
						TransactionLogger.Info("Weather location source: IP (user-selected Approximate)");
						SetLocationInternal((PlaceName(json) ?? "Network location") + " · Approximate", Convert.ToDouble(json["latitude"], CultureInfo.InvariantCulture), Convert.ToDouble(json["longitude"], CultureInfo.InvariantCulture), picked: false);
						return;
					}
				}
				catch
				{
				}
				LocationFailed("Approximate location unavailable. Search for a city instead.");
			}
		});
	}

	private static string PlaceName(Dictionary<string, object> json)
	{
		string text = Str(json, "locality");
		if (string.IsNullOrEmpty(text))
		{
			text = Str(json, "city");
		}
		string text2 = Str(json, "principalSubdivision");
		if (string.IsNullOrEmpty(text2))
		{
			text2 = Str(json, "countryName");
		}
		if (string.IsNullOrEmpty(text))
		{
			return text2;
		}
		if (!string.IsNullOrEmpty(text2) && !(text2 == text))
		{
			return text + ", " + text2;
		}
		return text;
	}

	private static string Str(Dictionary<string, object> json, string key)
	{
		if (json == null || !json.TryGetValue(key, out var value) || value == null)
		{
			return string.Empty;
		}
		return value.ToString();
	}

	public void SetCity(GeocodedCity city)
	{
		if (city != null)
		{
			string detail = (string.IsNullOrEmpty(city.Admin1) ? city.Country : (string.IsNullOrEmpty(city.Country) ? city.Admin1 : (city.Admin1 + ", " + city.Country)));
			SetRecentCity(new RecentCity
			{
				Name = city.ShortName,
				Detail = detail,
				Latitude = city.Latitude,
				Longitude = city.Longitude
			});
			LocationNeedsAttention = false;
			LocationProblem = null;
		}
	}

	public void SetRecentCity(RecentCity c)
	{
		if (c != null)
		{
			_prefs.Recent.RemoveAll((RecentCity r) => string.Equals(r.Name, c.Name, StringComparison.OrdinalIgnoreCase) || (Math.Abs(r.Latitude - c.Latitude) < 0.01 && Math.Abs(r.Longitude - c.Longitude) < 0.01));
			_prefs.Recent.Insert(0, c);
			while (_prefs.Recent.Count > 5)
			{
				_prefs.Recent.RemoveAt(_prefs.Recent.Count - 1);
			}
			SetLocationInternal(c.Name, c.Latitude, c.Longitude, picked: true);
		}
	}

	public void RemoveRecentCity(RecentCity c)
	{
		if (c != null)
		{
			_prefs.Recent.RemoveAll((RecentCity r) => r.Name == c.Name && r.Latitude == c.Latitude && r.Longitude == c.Longitude);
			SavePrefs();
			NotifyUpdated();
		}
	}

	public void SetLocation(string city, double lat, double lon)
	{
		SetLocationInternal(city, lat, lon, picked: true);
	}

	private void SetLocationInternal(string city, double lat, double lon, bool picked)
	{
		OnUi(delegate
		{
			if (picked)
			{
				_prefs.FollowLocation = false;
				_prefs.UseIpLocation = false;
				_prefs.LocationSource = "manual";
				CancelLocation();
				LocationNeedsAttention = false;
				TransactionLogger.Info("Weather location source: manual");
			}
			if (!_current.IsConfigured || !(Math.Abs(_current.Latitude - lat) < 0.01) || !(Math.Abs(_current.Longitude - lon) < 0.01))
			{
				WeatherData current = new WeatherData();
				_current = current;
			}
			_current.City = city;
			_current.Latitude = lat;
			_current.Longitude = lon;
			_current.IsConfigured = true;
			SavePrefs();
			SaveCache();
			CancelFetch();
			FetchWeatherAsync();
		});
	}

	public void Refresh()
	{
		if (_prefs.FollowLocation)
		{
			TryResolveLocationAsync();
		}
		else if (_prefs.UseIpLocation)
		{
			ResolveByIp();
		}
		else if (_current != null && _current.IsConfigured)
		{
			FetchWeatherAsync();
		}
	}

	private void NetworkChanged(object sender, EventArgs args)
	{
		OnUi(delegate
		{
			if (DateTime.UtcNow - _lastLocationRequest > TimeSpan.FromSeconds(30.0))
			{
				Refresh();
			}
		});
	}

	public void FetchWeatherAsync(Action<bool, string> onComplete = null)
	{
		if (!_current.IsConfigured)
		{
			onComplete?.Invoke(arg1: false, "Location not configured");
		}
		else if (!_isFetching)
		{
			_isFetching = true;
			FetchState = WeatherFetchState.Loading;
			int gen = ++_fetchGen;
			NotifyUpdated();
			string url = string.Format(CultureInfo.InvariantCulture, "https://api.open-meteo.com/v1/forecast?latitude={0:F4}&longitude={1:F4}&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day,uv_index&hourly=temperature_2m,weather_code,is_day,uv_index&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset&timezone=auto&forecast_days=7", _current.Latitude, _current.Longitude);
			ExecuteFetchWithRetry(url, 0, gen, onComplete);
		}
	}

	private void CancelFetch()
	{
		_fetchGen++;
		_isFetching = false;
		lock (_weatherLock)
		{
			if (_activeWeatherClient != null)
			{
				try
				{
					_activeWeatherClient.CancelAsync();
					_activeWeatherClient.Dispose();
				}
				catch
				{
				}
				_activeWeatherClient = null;
			}
		}
	}

	private void ExecuteFetchWithRetry(string url, int retryCount, int gen, Action<bool, string> onComplete)
	{
		SafeWebClient safeWebClient;
		lock (_weatherLock)
		{
			if (gen != _fetchGen)
			{
				return;
			}
			if (_activeWeatherClient != null)
			{
				try
				{
					_activeWeatherClient.CancelAsync();
					_activeWeatherClient.Dispose();
				}
				catch
				{
				}
			}
			safeWebClient = (_activeWeatherClient = new SafeWebClient());
		}
		safeWebClient.DownloadStringCompleted += delegate(object s, DownloadStringCompletedEventArgs e)
		{
			OnUi(delegate
			{
				if (gen == _fetchGen && !e.Cancelled)
				{
					if (e.Error == null)
					{
						try
						{
							Parse(e.Result);
							_isFetching = false;
							FetchState = WeatherFetchState.Idle;
							SaveCache();
							NotifyUpdated();
							if (onComplete != null)
							{
								onComplete(arg1: true, "Updated successfully");
							}
							return;
						}
						catch (Exception ex2)
						{
							TransactionLogger.Error("Weather parse error: " + ex2.Message, ex2);
							Fail(offline: false, "Couldn't read weather data", onComplete);
							return;
						}
					}
					TransactionLogger.Warn($"Weather fetch attempt {retryCount + 1} error: {e.Error.Message}");
					bool flag = IsOfflineError(e.Error);
					if (retryCount < 2 && !flag)
					{
						DispatcherTimer delay = new DispatcherTimer(DispatcherPriority.Background, _ui)
						{
							Interval = TimeSpan.FromSeconds(retryCount + 1)
						};
						delay.Tick += delegate
						{
							delay.Stop();
							ExecuteFetchWithRetry(url, retryCount + 1, gen, onComplete);
						};
						delay.Start();
					}
					else
					{
						Fail(flag, flag ? "Offline" : "Couldn't update weather", onComplete);
					}
				}
			});
		};
		StartOffUi(safeWebClient, url, delegate(Exception ex)
		{
			OnUi(delegate
			{
				if (gen == _fetchGen)
				{
					Fail(offline: false, ex.Message, onComplete);
				}
			});
		});
	}

	private static void StartOffUi(SafeWebClient client, string url, Action<Exception> failedToStart)
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				client.DownloadStringAsync(new Uri(url));
			}
			catch (Exception obj)
			{
				failedToStart(obj);
			}
		});
	}

	private void Fail(bool offline, string message, Action<bool, string> onComplete)
	{
		_isFetching = false;
		FetchState = (offline ? WeatherFetchState.Offline : WeatherFetchState.Error);
		_current.IsOffline = offline;
		_current.StatusMessage = message;
		NotifyUpdated();
		onComplete?.Invoke(arg1: false, message);
	}

	private static bool IsOfflineError(Exception ex)
	{
		try
		{
			if (!NetworkInterface.GetIsNetworkAvailable())
			{
				return true;
			}
		}
		catch
		{
		}
		if (!(ex is WebException { Status: var status }))
		{
			return false;
		}
		switch (status)
		{
		case WebExceptionStatus.NameResolutionFailure:
		case WebExceptionStatus.ConnectFailure:
		case WebExceptionStatus.ProxyNameResolutionFailure:
			return true;
		default:
			return false;
		}
	}

	private void Parse(string body)
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		JavaScriptSerializer javaScriptSerializer2 = javaScriptSerializer;
		if (!(javaScriptSerializer2.DeserializeObject(body) is Dictionary<string, object> dictionary))
		{
			throw new FormatException("Empty response");
		}
		if (dictionary.ContainsKey("error") && Convert.ToBoolean(dictionary["error"]))
		{
			throw new FormatException(Str(dictionary, "reason"));
		}
		Dictionary<string, object> dictionary2 = (Dictionary<string, object>)dictionary["current"];
		WeatherData current = _current;
		current.UtcOffsetSeconds = (dictionary.ContainsKey("utc_offset_seconds") ? Convert.ToInt32(dictionary["utc_offset_seconds"]) : 0);
		current.Temperature = Num(dictionary2, "temperature_2m");
		current.FeelsLike = Num(dictionary2, "apparent_temperature");
		current.Humidity = (int)Math.Round(Num(dictionary2, "relative_humidity_2m"));
		current.WindSpeed = Num(dictionary2, "wind_speed_10m");
		current.WeatherCode = (int)Num(dictionary2, "weather_code");
		current.IsDay = !dictionary2.ContainsKey("is_day") || Num(dictionary2, "is_day") > 0.0;
		current.Condition = WeatherCodeToString(current.WeatherCode);
		string text = Str(dictionary2, "time");
		List<HourlyForecast> list = new List<HourlyForecast>();
		Dictionary<string, object> dictionary3 = (dictionary.ContainsKey("hourly") ? (dictionary["hourly"] as Dictionary<string, object>) : null);
		if (dictionary3 != null)
		{
			object[] array = Arr(dictionary3, "time");
			object[] array2 = Arr(dictionary3, "temperature_2m");
			object[] array3 = Arr(dictionary3, "weather_code");
			object[] array4 = Arr(dictionary3, "is_day");
			object[] array5 = Arr(dictionary3, "uv_index");
			int num = 0;
			while (array != null && num < array.Length)
			{
				if (array2 != null && num < array2.Length && array2[num] != null)
				{
					list.Add(new HourlyForecast
					{
						Time = array[num].ToString(),
						Temp = ToD(array2[num]),
						WeatherCode = ((array3 != null && num < array3.Length && array3[num] != null) ? ((int)ToD(array3[num])) : 0),
						IsDay = (array4 == null || num >= array4.Length || array4[num] == null || ToD(array4[num]) > 0.0),
						UvIndex = ((array5 != null && num < array5.Length && array5[num] != null) ? ToD(array5[num]) : 0.0)
					});
				}
				num++;
			}
		}
		current.Hourly = list;
		if (dictionary2.ContainsKey("uv_index") && dictionary2["uv_index"] != null)
		{
			current.UvIndex = Num(dictionary2, "uv_index");
		}
		else
		{
			string hourKey = ((text.Length >= 13) ? text.Substring(0, 13) : text);
			current.UvIndex = list.Find((HourlyForecast x) => x.Time != null && x.Time.StartsWith(hourKey, StringComparison.Ordinal))?.UvIndex ?? 0.0;
		}
		List<DailyForecast> list2 = new List<DailyForecast>();
		Dictionary<string, object> dictionary4 = (dictionary.ContainsKey("daily") ? (dictionary["daily"] as Dictionary<string, object>) : null);
		if (dictionary4 != null)
		{
			object[] array6 = Arr(dictionary4, "time");
			object[] array7 = Arr(dictionary4, "weather_code");
			object[] array8 = Arr(dictionary4, "temperature_2m_max");
			object[] array9 = Arr(dictionary4, "temperature_2m_min");
			object[] array10 = Arr(dictionary4, "sunrise");
			object[] array11 = Arr(dictionary4, "sunset");
			int num2 = 0;
			while (array6 != null && num2 < array6.Length)
			{
				if (array8 != null && array9 != null && num2 < array8.Length && num2 < array9.Length && array8[num2] != null && array9[num2] != null)
				{
					int num3 = ((array7 != null && num2 < array7.Length && array7[num2] != null) ? ((int)ToD(array7[num2])) : 0);
					DateTime? dateTime = ParseLocal(array6[num2].ToString());
					list2.Add(new DailyForecast
					{
						Date = array6[num2].ToString(),
						DayName = (dateTime.HasValue ? dateTime.Value.ToString("ddd", CultureInfo.CurrentCulture) : array6[num2].ToString()),
						WeatherCode = num3,
						Condition = WeatherCodeToString(num3),
						MaxTemp = ToD(array8[num2]),
						MinTemp = ToD(array9[num2]),
						Sunrise = ((array10 != null && num2 < array10.Length && array10[num2] != null) ? array10[num2].ToString() : null),
						Sunset = ((array11 != null && num2 < array11.Length && array11[num2] != null) ? array11[num2].ToString() : null)
					});
				}
				num2++;
			}
		}
		current.Forecast = list2;
		if (list2.Count > 0)
		{
			current.Sunrise = list2[0].Sunrise;
			current.Sunset = list2[0].Sunset;
		}
		current.LastUpdatedTicks = DateTime.UtcNow.Ticks;
		current.LastUpdatedFormatted = Clock(DateTime.Now);
		current.IsOffline = false;
		current.StatusMessage = string.Empty;
	}

	private static object[] Arr(Dictionary<string, object> o, string key)
	{
		if (!o.TryGetValue(key, out var value))
		{
			return null;
		}
		return value as object[];
	}

	private static double Num(Dictionary<string, object> o, string key)
	{
		if (!o.TryGetValue(key, out var value) || value == null)
		{
			return 0.0;
		}
		return ToD(value);
	}

	private static double ToD(object v)
	{
		return Convert.ToDouble(v, CultureInfo.InvariantCulture);
	}

	public void SearchCitiesAsync(string query, Action<List<GeocodedCity>, string> callback)
	{
		if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
		{
			if (callback != null)
			{
				callback(new List<GeocodedCity>(), null);
			}
			return;
		}
		string q = query.Trim();
		string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(q)}&count=10&language=en&format=json";
		Download(url, 5000, delegate(Dictionary<string, object> json, string err)
		{
			List<GeocodedCity> arg = new List<GeocodedCity>();
			if (json == null)
			{
				if (callback != null)
				{
					callback(arg, err ?? "Search failed");
				}
			}
			else
			{
				try
				{
					arg = ParseOpenMeteo(json);
					if (arg.Count > 0)
					{
						if (callback != null)
						{
							callback(RankTowns(arg, q), null);
						}
						return;
					}
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Town search answer unreadable (" + ex.GetType().Name + ").");
				}
				SearchOpenStreetMap(q, callback);
			}
		});
	}

	internal static List<GeocodedCity> ParseOpenMeteo(Dictionary<string, object> json)
	{
		List<GeocodedCity> list = new List<GeocodedCity>();
		object[] array = (json.ContainsKey("results") ? (json["results"] as object[]) : null);
		if (array == null)
		{
			return list;
		}
		object[] array2 = array;
		foreach (object obj in array2)
		{
			if (!(obj is Dictionary<string, object> dictionary) || !dictionary.ContainsKey("latitude") || !dictionary.ContainsKey("longitude"))
			{
				continue;
			}
			long population = 0L;
			if (dictionary.TryGetValue("population", out var value) && value != null)
			{
				try
				{
					population = Convert.ToInt64(value, CultureInfo.InvariantCulture);
				}
				catch
				{
				}
			}
			list.Add(new GeocodedCity
			{
				Name = Str(dictionary, "name"),
				Country = Str(dictionary, "country"),
				CountryCode = Str(dictionary, "country_code"),
				Admin1 = Str(dictionary, "admin1"),
				Admin2 = Str(dictionary, "admin2"),
				Population = population,
				Latitude = ToD(dictionary["latitude"]),
				Longitude = ToD(dictionary["longitude"])
			});
		}
		return list;
	}

	internal static List<GeocodedCity> RankTowns(List<GeocodedCity> list, string query)
	{
		string home = HomeCountryCode();
		string fold = Fold(query);
		return (from c in list
			group c by Fold(c.Name) + "|" + (c.Admin1 ?? "") + "|" + (c.Admin2 ?? "") + "|" + (c.CountryCode ?? "") into g
			select g.OrderByDescending((GeocodedCity c) => c.Population).First() into c
			orderby !string.IsNullOrEmpty(home) && string.Equals(c.CountryCode, home, StringComparison.OrdinalIgnoreCase) descending, Fold(c.Name) == fold descending, c.Population descending
			select c).ToList();
	}

	internal static string HomeCountryCode()
	{
		try
		{
			return new RegionInfo(CultureInfo.CurrentCulture.Name).TwoLetterISORegionName;
		}
		catch
		{
			return null;
		}
	}

	internal static string Fold(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		string text = s.Normalize(NormalizationForm.FormD);
		foreach (char c in text)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	private void SearchOpenStreetMap(string q, Action<List<GeocodedCity>, string> callback)
	{
		if (DateTime.UtcNow - _lastOsmSearch < TimeSpan.FromSeconds(1.0))
		{
			if (callback != null)
			{
				callback(new List<GeocodedCity>(), null);
			}
			return;
		}
		_lastOsmSearch = DateTime.UtcNow;
		string url = "https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&limit=8&featureType=settlement&accept-language=en&q=" + Uri.EscapeDataString(q);
		DownloadArray(url, 6000, delegate(object[] items, string err)
		{
			List<GeocodedCity> list = new List<GeocodedCity>();
			if (items == null)
			{
				if (callback != null)
				{
					callback(list, err);
				}
			}
			else
			{
				try
				{
					list = ParseNominatim(items);
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Town search (OpenStreetMap) unreadable (" + ex.GetType().Name + ").");
				}
				if (callback != null)
				{
					callback(RankTowns(list, q), null);
				}
			}
		});
	}

	internal static List<GeocodedCity> ParseNominatim(object[] items)
	{
		List<GeocodedCity> list = new List<GeocodedCity>();
		foreach (object obj in items)
		{
			if (!(obj is Dictionary<string, object> dictionary))
			{
				continue;
			}
			Dictionary<string, object> dictionary2 = (dictionary.ContainsKey("address") ? (dictionary["address"] as Dictionary<string, object>) : null);
			string text = Str(dictionary, "name");
			if (!string.IsNullOrEmpty(text) && dictionary2 != null)
			{
				string text2 = Str(dictionary2, "state_district");
				if (string.IsNullOrEmpty(text2))
				{
					text2 = Str(dictionary2, "county");
				}
				if (double.TryParse(Str(dictionary, "lat"), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.TryParse(Str(dictionary, "lon"), NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					list.Add(new GeocodedCity
					{
						Name = (text.EndsWith(" Tahsil", StringComparison.Ordinal) ? text.Substring(0, text.Length - 7) : text),
						Country = Str(dictionary2, "country"),
						CountryCode = Str(dictionary2, "country_code").ToUpperInvariant(),
						Admin1 = Str(dictionary2, "state"),
						Admin2 = text2,
						Latitude = result,
						Longitude = result2
					});
				}
			}
		}
		return list;
	}

	private void DownloadArray(string url, int timeoutMs, Action<object[], string> done)
	{
		int lifetime = _callbacks.Capture();
		SafeWebClient client = new SafeWebClient(timeoutMs);
		client.Headers[HttpRequestHeader.UserAgent] = "Ayn/" + AppVersionForUserAgent + " (contact@modistudio.online)";
		client.Encoding = Encoding.UTF8;
		client.DownloadStringCompleted += delegate(object s, DownloadStringCompletedEventArgs e)
		{
			object[] items = null;
			string err = null;
			if (e.Cancelled)
			{
				err = "Cancelled";
			}
			else if (e.Error != null)
			{
				err = (IsOfflineError(e.Error) ? "Offline" : e.Error.Message);
			}
			else
			{
				try
				{
					items = new JavaScriptSerializer().DeserializeObject(e.Result) as object[];
				}
				catch (Exception ex2)
				{
					err = ex2.Message;
				}
			}
			try
			{
				client.Dispose();
			}
			catch
			{
			}
			OnUi(delegate
			{
				if (_callbacks.Accepts(lifetime))
				{
					done(items, err);
				}
			});
		};
		StartOffUi(client, url, delegate(Exception ex)
		{
			try
			{
				client.Dispose();
			}
			catch
			{
			}
			OnUi(delegate
			{
				if (_callbacks.Accepts(lifetime))
				{
					done(null, ex.Message);
				}
			});
		});
	}

	private void Download(string url, int timeoutMs, Action<Dictionary<string, object>, string> done)
	{
		int lifetime = _callbacks.Capture();
		SafeWebClient client = new SafeWebClient(timeoutMs);
		client.DownloadStringCompleted += delegate(object s, DownloadStringCompletedEventArgs e)
		{
			Dictionary<string, object> json = null;
			string err = null;
			if (e.Cancelled)
			{
				err = "Cancelled";
			}
			else if (e.Error != null)
			{
				err = (IsOfflineError(e.Error) ? "Offline" : e.Error.Message);
			}
			else
			{
				try
				{
					json = new JavaScriptSerializer().DeserializeObject(e.Result) as Dictionary<string, object>;
				}
				catch (Exception ex2)
				{
					err = ex2.Message;
				}
			}
			try
			{
				client.Dispose();
			}
			catch
			{
			}
			OnUi(delegate
			{
				if (_callbacks.Accepts(lifetime))
				{
					done(json, err);
				}
			});
		};
		StartOffUi(client, url, delegate(Exception ex)
		{
			try
			{
				client.Dispose();
			}
			catch
			{
			}
			OnUi(delegate
			{
				if (_callbacks.Accepts(lifetime))
				{
					done(null, ex.Message);
				}
			});
		});
	}

	public Action PauseForShutdown()
	{
		bool enabled = _refreshTimer.IsEnabled;
		_callbacks.Pause();
		CancelLocation();
		CancelFetch();
		_refreshTimer.Stop();
		return delegate
		{
			_callbacks.Resume();
			if (enabled)
			{
				_refreshTimer.Start();
			}
			Refresh();
		};
	}

	public void Shutdown()
	{
		_callbacks.Stop();
		NetworkChange.NetworkAddressChanged -= NetworkChanged;
		_refreshTimer.Stop();
		try
		{
			CancelLocation();
			CancelFetch();
		}
		catch
		{
		}
	}

	public static string WeatherCodeToString(int code)
	{
		switch (code)
		{
		case 0:
			return "Clear Sky";
		case 1:
			return "Mainly Clear";
		case 2:
			return "Partly Cloudy";
		case 3:
			return "Overcast";
		case 45:
			return "Fog";
		case 48:
			return "Freezing Fog";
		case 51:
			return "Light Drizzle";
		case 53:
			return "Drizzle";
		case 55:
			return "Heavy Drizzle";
		case 56:
		case 57:
			return "Freezing Drizzle";
		case 61:
			return "Light Rain";
		case 63:
			return "Rain";
		case 65:
			return "Heavy Rain";
		case 66:
		case 67:
			return "Freezing Rain";
		case 71:
			return "Light Snow";
		case 73:
			return "Snow";
		case 75:
			return "Heavy Snow";
		case 77:
			return "Snow Grains";
		case 80:
			return "Light Showers";
		case 81:
			return "Rain Showers";
		case 82:
			return "Heavy Showers";
		case 85:
		case 86:
			return "Snow Showers";
		case 95:
			return "Thunderstorm";
		case 96:
		case 99:
			return "Thunderstorm, Hail";
		default:
			return "Cloudy";
		}
	}

	private void OnUi(Action a)
	{
		int token = _callbacks.Capture();
		Action action = delegate
		{
			if (!_callbacks.Accepts(token))
			{
				return;
			}
			try
			{
				a();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Weather: " + ex.Message);
			}
		};
		if (_ui == null || _ui.CheckAccess())
		{
			action();
			return;
		}
		try
		{
			_ui.BeginInvoke(action);
		}
		catch
		{
		}
	}

	private void NotifyUpdated()
	{
		OnUi(delegate
		{
			EventHandler weatherUpdated = this.WeatherUpdated;
			if (weatherUpdated != null)
			{
				try
				{
					weatherUpdated(this, EventArgs.Empty);
				}
				catch
				{
				}
			}
		});
	}

	private void SaveCache()
	{
		try
		{
			PersistenceHelper.WriteCacheText(_cachePath, new JavaScriptSerializer
			{
				MaxJsonLength = int.MaxValue
			}.Serialize(_current));
		}
		catch
		{
		}
	}

	private void LoadCache()
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_cachePath);
			if (!string.IsNullOrEmpty(text))
			{
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				javaScriptSerializer.MaxJsonLength = int.MaxValue;
				WeatherData weatherData = javaScriptSerializer.Deserialize<WeatherData>(text);
				if (weatherData != null && ((weatherData.Forecast != null && weatherData.Forecast.Exists((DailyForecast d) => d == null)) || (weatherData.Hourly != null && weatherData.Hourly.Exists((HourlyForecast h) => h == null)) || weatherData.UtcOffsetSeconds < -86400 || weatherData.UtcOffsetSeconds > 86400 || weatherData.LastUpdatedTicks < 0 || weatherData.LastUpdatedTicks > DateTime.MaxValue.Ticks))
				{
					throw new FormatException("Invalid weather cache");
				}
				_current = weatherData;
			}
		}
		catch
		{
		}
	}

	private void SavePrefs()
	{
		try
		{
			PersistenceHelper.SafeWriteAllText(_prefsPath, new JavaScriptSerializer().Serialize(_prefs));
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(_prefsPath, error);
		}
	}

	private void LoadPrefs()
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_prefsPath);
			if (!string.IsNullOrEmpty(text))
			{
				_prefs = new JavaScriptSerializer().Deserialize<WeatherPrefs>(text);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Weather prefs unreadable, using defaults: " + ex.Message);
			_prefs = null;
		}
		if (_prefs != null && _prefs.Recent == null)
		{
			_prefs.Recent = new List<RecentCity>();
		}
		if (_prefs == null)
		{
			_prefs = new WeatherPrefs();
			string text2 = ((_current != null) ? (_current.City ?? string.Empty) : string.Empty);
			_prefs.FollowLocation = _current == null || !_current.IsConfigured || text2.Split(',').Length < 3;
			SavePrefs();
		}
		if (_prefs.Recent == null)
		{
			_prefs.Recent = new List<RecentCity>();
		}
		if (_prefs.UseIpLocation || string.Equals(_prefs.LocationSource, "IP", StringComparison.Ordinal))
		{
			_prefs.UseIpLocation = false;
			_prefs.FollowLocation = false;
			_prefs.LocationSource = "manual";
			LocationNeedsAttention = true;
			LocationProblem = "Couldn't find your exact town. Search for it once.";
			if (_current != null && _current.City != null && _current.City.EndsWith(" · Approximate", StringComparison.Ordinal))
			{
				_current.City = _current.City.Substring(0, _current.City.Length - " · Approximate".Length);
			}
			SavePrefs();
			TransactionLogger.Info("Weather location: IP-approximate place replaced by a one-time town search");
		}
	}
}
