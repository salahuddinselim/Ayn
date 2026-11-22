using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Tools.Calendar;

public sealed class CalendarFeeds
{
	public const int MaxBytes = 8388608;

	private static CalendarFeeds _instance;

	public static Func<string, string, string, FetchResult> Fetcher = HttpFetch;

	public static readonly ProducerFence Fence = new ProducerFence();

	private readonly object _sync = new object();

	private Dictionary<string, FeedData> _data = new Dictionary<string, FeedData>(StringComparer.Ordinal);

	private readonly HashSet<string> _busy = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, string> _status = new Dictionary<string, string>(StringComparer.Ordinal);

	public static CalendarFeeds Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new CalendarFeeds();
			}
			return _instance;
		}
	}

	public string CacheDir
	{
		get
		{
			string text = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "calendar", "feeds");
			try
			{
				Directory.CreateDirectory(text);
			}
			catch
			{
			}
			return text;
		}
	}

	public event Action<string> Changed;

	public FeedData Get(string key)
	{
		lock (_sync)
		{
			FeedData value;
			return _data.TryGetValue(key, out value) ? value : null;
		}
	}

	public bool IsBusy(string key)
	{
		lock (_sync)
		{
			return _busy.Contains(key);
		}
	}

	public string StatusOf(string key)
	{
		lock (_sync)
		{
			string value;
			return _status.TryGetValue(key, out value) ? value : null;
		}
	}

	public string CachePath(string key)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in key)
		{
			stringBuilder.Append((char.IsLetterOrDigit(c) || c == '_' || c == '-') ? c : '_');
		}
		return Path.Combine(CacheDir, string.Concat(stringBuilder, ".ics"));
	}

	public DateTime CachedAtUtc(string key)
	{
		try
		{
			string path = CachePath(key);
			return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
		}
		catch
		{
			return DateTime.MinValue;
		}
	}

	public void Ensure(string key, string url, TimeSpan maxAge, bool force, string etag, string lastModified, Action<FetchResult, string> done)
	{
		if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(url) || !Fence.TryEnter(out var token))
		{
			return;
		}
		lock (_sync)
		{
			if (_busy.Contains(key))
			{
				Fence.Exit();
				return;
			}
			_busy.Add(key);
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				Work(key, url, maxAge, force, etag, lastModified, done, token);
			}
			finally
			{
				Fence.Exit();
			}
		});
	}

	private void Work(string key, string url, TimeSpan maxAge, bool force, string etag, string lastModified, Action<FetchResult, string> done, long token)
	{
		FetchResult fetchResult = null;
		string arg = null;
		try
		{
			string path = CachePath(key);
			bool flag = File.Exists(path);
			if (flag && Get(key) == null)
			{
				try
				{
					arg = Publish(key, File.ReadAllText(path, Encoding.UTF8), File.GetLastWriteTimeUtc(path));
				}
				catch
				{
				}
			}
			if (!flag || force || DateTime.UtcNow - CachedAtUtc(key) > maxAge)
			{
				lock (_sync)
				{
					_status.Remove(key);
				}
				Raise(key);
				fetchResult = Fetcher(url, flag ? etag : null, flag ? lastModified : null);
				if (fetchResult != null && fetchResult.Ok && !fetchResult.NotModified)
				{
					if (!LooksLikeIcs(fetchResult.Text))
					{
						fetchResult.Ok = false;
						fetchResult.Error = "That link isn't a calendar (ICS) feed";
					}
					else
					{
						if (Fence.IsCurrent(token))
						{
							PersistenceHelper.WriteCacheText(path, fetchResult.Text);
						}
						arg = Publish(key, fetchResult.Text, DateTime.UtcNow);
					}
				}
				else if (fetchResult != null && fetchResult.NotModified && Fence.IsCurrent(token) && PersistenceHelper.TryBeginCacheWrite())
				{
					try
					{
						File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
					}
					catch
					{
					}
					finally
					{
						PersistenceHelper.EndCacheWrite();
					}
				}
				lock (_sync)
				{
					if (fetchResult == null || !fetchResult.Ok)
					{
						_status[key] = ((fetchResult != null && fetchResult.Error != null) ? fetchResult.Error : "Couldn't update");
					}
					else
					{
						_status.Remove(key);
					}
				}
			}
		}
		catch (Exception ex)
		{
			FetchResult fetchResult2 = new FetchResult();
			fetchResult2.Ok = false;
			fetchResult2.Error = ex.Message;
			fetchResult = fetchResult2;
			lock (_sync)
			{
				_status[key] = "Couldn't update";
			}
		}
		finally
		{
			lock (_sync)
			{
				_busy.Remove(key);
			}
		}
		if (done != null)
		{
			try
			{
				done(fetchResult, arg);
			}
			catch
			{
			}
		}
		Raise(key);
	}

	private string Publish(string key, string text, DateTime fetchedUtc)
	{
		IcsCalendar icsCalendar = Ics.Parse(text);
		FeedData feedData = new FeedData();
		feedData.Key = key;
		feedData.Name = icsCalendar.Name;
		feedData.Events = icsCalendar.Events;
		feedData.FetchedUtc = fetchedUtc;
		FeedData value = feedData;
		lock (_sync)
		{
			Dictionary<string, FeedData> dictionary = new Dictionary<string, FeedData>(_data, StringComparer.Ordinal);
			dictionary[key] = value;
			_data = dictionary;
		}
		return icsCalendar.Name;
	}

	public void Forget(string key)
	{
		lock (_sync)
		{
			Dictionary<string, FeedData> dictionary = new Dictionary<string, FeedData>(_data, StringComparer.Ordinal);
			dictionary.Remove(key);
			_data = dictionary;
			_status.Remove(key);
		}
		try
		{
			File.Delete(CachePath(key));
		}
		catch
		{
		}
		Raise(key);
	}

	private void Raise(string key)
	{
		Action<string> changed = this.Changed;
		if (changed != null)
		{
			try
			{
				changed(key);
			}
			catch
			{
			}
		}
	}

	public static bool LooksLikeIcs(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		string text2 = ((text.Length > 512) ? text.Substring(0, 512) : text);
		return text2.TrimStart('\ufeff', ' ', '\r', '\n', '\t').StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase);
	}

	public static string NormalizeUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return null;
		}
		string text = url.Trim();
		if (text.StartsWith("webcals://", StringComparison.OrdinalIgnoreCase))
		{
			text = "https://" + text.Substring(10);
		}
		else if (text.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
		{
			text = "https://" + text.Substring(9);
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out var result))
		{
			return null;
		}
		if (result.Scheme != Uri.UriSchemeHttps && result.Scheme != Uri.UriSchemeHttp)
		{
			return null;
		}
		return result.AbsoluteUri;
	}

	private static FetchResult HttpFetch(string url, string etag, string lastModified)
	{
		try
		{
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
			httpWebRequest.Method = "GET";
			httpWebRequest.Timeout = 20000;
			httpWebRequest.ReadWriteTimeout = 20000;
			httpWebRequest.AllowAutoRedirect = true;
			httpWebRequest.MaximumAutomaticRedirections = 5;
			httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			httpWebRequest.UserAgent = "Ayn-Calendar/1.0";
			httpWebRequest.Accept = "text/calendar, text/plain, */*";
			if (!string.IsNullOrEmpty(etag))
			{
				httpWebRequest.Headers[HttpRequestHeader.IfNoneMatch] = etag;
			}
			if (!string.IsNullOrEmpty(lastModified) && DateTime.TryParse(lastModified, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var result))
			{
				httpWebRequest.IfModifiedSince = result;
			}
			using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			using Stream stream = httpWebResponse.GetResponseStream();
			using MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[32768];
			int count;
			while ((count = stream.Read(array, 0, array.Length)) > 0)
			{
				memoryStream.Write(array, 0, count);
				if (memoryStream.Length > 8388608)
				{
					FetchResult fetchResult = new FetchResult();
					fetchResult.Error = "That calendar is too large (over 8 MB)";
					return fetchResult;
				}
			}
			FetchResult fetchResult2 = new FetchResult();
			fetchResult2.Ok = true;
			fetchResult2.Text = Encoding.UTF8.GetString(memoryStream.ToArray());
			fetchResult2.ETag = httpWebResponse.Headers[HttpResponseHeader.ETag];
			fetchResult2.LastModified = httpWebResponse.Headers[HttpResponseHeader.LastModified];
			return fetchResult2;
		}
		catch (WebException ex)
		{
			HttpWebResponse httpWebResponse2 = ex.Response as HttpWebResponse;
			if (httpWebResponse2 != null && httpWebResponse2.StatusCode == HttpStatusCode.NotModified)
			{
				FetchResult fetchResult3 = new FetchResult();
				fetchResult3.Ok = true;
				fetchResult3.NotModified = true;
				return fetchResult3;
			}
			if (httpWebResponse2 != null && (httpWebResponse2.StatusCode == HttpStatusCode.NotFound || httpWebResponse2.StatusCode == HttpStatusCode.Gone))
			{
				FetchResult fetchResult4 = new FetchResult();
				fetchResult4.Error = "Calendar not found (link removed or private)";
				return fetchResult4;
			}
			if (httpWebResponse2 != null && (httpWebResponse2.StatusCode == HttpStatusCode.Unauthorized || httpWebResponse2.StatusCode == HttpStatusCode.Forbidden))
			{
				FetchResult fetchResult5 = new FetchResult();
				fetchResult5.Error = "Calendar is private — use its secret iCal link";
				return fetchResult5;
			}
			if (ex.Status == WebExceptionStatus.Timeout)
			{
				FetchResult fetchResult6 = new FetchResult();
				fetchResult6.Error = "Timed out — will retry";
				return fetchResult6;
			}
			if (ex.Status == WebExceptionStatus.NameResolutionFailure || ex.Status == WebExceptionStatus.ConnectFailure)
			{
				FetchResult fetchResult7 = new FetchResult();
				fetchResult7.Error = "Offline — showing saved copy";
				return fetchResult7;
			}
			FetchResult fetchResult8 = new FetchResult();
			fetchResult8.Error = "Couldn't download — will retry";
			return fetchResult8;
		}
		catch
		{
			FetchResult fetchResult9 = new FetchResult();
			fetchResult9.Error = "Couldn't download — will retry";
			return fetchResult9;
		}
	}
}
