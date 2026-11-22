using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;

namespace WindowsBuddy.Tools.Weather;

public static class WindowsWeatherLocation
{
	public static async Task<WeatherFix> ResolveAsync(bool requestAccess, CancellationToken token)
	{
		if (requestAccess && await Await(Geolocator.RequestAccessAsync(), token) != GeolocationAccessStatus.Allowed)
		{
			throw new UnauthorizedAccessException();
		}
		Geolocator locator = new Geolocator
		{
			DesiredAccuracy = PositionAccuracy.High,
			DesiredAccuracyInMeters = 50u
		};
		Geocoordinate coordinate = (await Await(locator.GetGeopositionAsync(TimeSpan.FromMinutes(1.0), TimeSpan.FromSeconds(20.0)), token)).Coordinate;
		string source = coordinate.PositionSource switch
		{
			PositionSource.Satellite => "GPS", 
			PositionSource.WiFi => "Wi-Fi", 
			PositionSource.IPAddress => "IP", 
			PositionSource.Cellular => "Cellular", 
			_ => "Windows (unknown)", 
		};
		return new WeatherFix
		{
			Latitude = coordinate.Point.Position.Latitude,
			Longitude = coordinate.Point.Position.Longitude,
			Accuracy = coordinate.Accuracy,
			Source = source
		};
	}

	private static Task<T> Await<T>(IAsyncOperation<T> operation, CancellationToken token)
	{
		TaskCompletionSource<T> completion = new TaskCompletionSource<T>();
		CancellationTokenRegistration registration = token.Register(delegate
		{
			try
			{
				operation.Cancel();
			}
			catch
			{
			}
			completion.TrySetCanceled();
		});
		operation.Completed = delegate(IAsyncOperation<T> result, AsyncStatus status)
		{
			try
			{
				if (status == AsyncStatus.Canceled)
				{
					completion.TrySetCanceled();
				}
				else
				{
					completion.TrySetResult(result.GetResults());
				}
			}
			catch (Exception exception)
			{
				completion.TrySetException(exception);
			}
			finally
			{
				registration.Dispose();
			}
		};
		return completion.Task;
	}
}
