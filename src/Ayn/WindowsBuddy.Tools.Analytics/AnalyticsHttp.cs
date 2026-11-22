using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace WindowsBuddy.Tools.Analytics;

public static class AnalyticsHttp
{
	public static Func<HttpCall, HttpReply> Transport;

	private static readonly object GateLock;

	private static readonly Dictionary<string, DateTime> PausedUntil;

	static AnalyticsHttp()
	{
		Transport = DefaultTransport;
		GateLock = new object();
		PausedUntil = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
	}

	public static void ResetRateGates()
	{
		lock (GateLock)
		{
			PausedUntil.Clear();
		}
	}

	public static HttpReply Send(HttpCall call)
	{
		string key = HostOf(call.Url);
		lock (GateLock)
		{
			if (PausedUntil.TryGetValue(key, out var value) && value > DateTime.UtcNow)
			{
				throw new AnalyticsException(AnalyticsErrorKind.RateLimited, "Rate limited — pausing requests", value - DateTime.UtcNow);
			}
		}
		HttpReply httpReply = Transport(call);
		if (httpReply == null || httpReply.Status == 0)
		{
			throw new AnalyticsException(AnalyticsErrorKind.Offline, "You're offline or the server can't be reached");
		}
		TimeSpan? retryAfter = null;
		string value2;
		int result;
		if (httpReply.Status == 429)
		{
			retryAfter = RetryAfter(httpReply) ?? TimeSpan.FromSeconds(60.0);
		}
		else if (httpReply.Headers.TryGetValue("X-RateLimit-Remaining", out value2) && int.TryParse(value2, out result) && result <= 1)
		{
			retryAfter = ResetIn(httpReply) ?? TimeSpan.FromSeconds(60.0);
		}
		if (retryAfter.HasValue)
		{
			lock (GateLock)
			{
				PausedUntil[key] = DateTime.UtcNow + retryAfter.Value;
			}
		}
		if (httpReply.Status == 429)
		{
			throw new AnalyticsException(AnalyticsErrorKind.RateLimited, "Rate limited by the provider", retryAfter);
		}
		return httpReply;
	}

	private static TimeSpan? RetryAfter(HttpReply r)
	{
		if (r.Headers.TryGetValue("Retry-After", out var value) && int.TryParse(value, out var result))
		{
			return TimeSpan.FromSeconds(Math.Max(1, result));
		}
		return ResetIn(r);
	}

	private static TimeSpan? ResetIn(HttpReply r)
	{
		if (!r.Headers.TryGetValue("X-RateLimit-Reset", out var value) || !long.TryParse(value, out var result))
		{
			return null;
		}
		if (result > 1000000000)
		{
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(result);
			TimeSpan timeSpan = dateTime - DateTime.UtcNow;
			return (timeSpan > TimeSpan.Zero) ? timeSpan : TimeSpan.FromSeconds(1.0);
		}
		return TimeSpan.FromSeconds(Math.Max(1L, result));
	}

	private static string HostOf(string url)
	{
		try
		{
			return new Uri(url).Host;
		}
		catch
		{
			return url;
		}
	}

	internal static HttpReply DefaultTransport(HttpCall call)
	{
		HttpReply httpReply = new HttpReply();
		try
		{
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(call.Url);
			httpWebRequest.Method = call.Method ?? "GET";
			httpWebRequest.Timeout = 20000;
			httpWebRequest.ReadWriteTimeout = 20000;
			httpWebRequest.UserAgent = "Ayn-Analytics/2.0";
			httpWebRequest.Accept = "application/json";
			httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			foreach (KeyValuePair<string, string> header in call.Headers)
			{
				httpWebRequest.Headers[header.Key] = header.Value;
			}
			if (call.Body != null)
			{
				httpWebRequest.ContentType = call.ContentType ?? "application/json";
				byte[] bytes = Encoding.UTF8.GetBytes(call.Body);
				httpWebRequest.ContentLength = bytes.Length;
				using Stream stream = httpWebRequest.GetRequestStream();
				stream.Write(bytes, 0, bytes.Length);
			}
			using HttpWebResponse resp = (HttpWebResponse)httpWebRequest.GetResponse();
			Fill(httpReply, resp);
		}
		catch (WebException ex)
		{
			if (!(ex.Response is HttpWebResponse httpWebResponse))
			{
				httpReply.Status = 0;
				return httpReply;
			}
			using (httpWebResponse)
			{
				Fill(httpReply, httpWebResponse);
			}
		}
		catch (Exception)
		{
			httpReply.Status = 0;
		}
		return httpReply;
	}

	private static void Fill(HttpReply reply, HttpWebResponse resp)
	{
		reply.Status = (int)resp.StatusCode;
		string[] allKeys = resp.Headers.AllKeys;
		foreach (string text in allKeys)
		{
			reply.Headers[text] = resp.Headers[text];
		}
		using StreamReader streamReader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8);
		reply.Body = streamReader.ReadToEnd();
	}
}
