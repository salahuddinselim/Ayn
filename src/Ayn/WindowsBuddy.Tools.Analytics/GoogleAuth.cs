using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WindowsBuddy.Tools.Analytics;

public static class GoogleAuth
{
	public const string Scope = "https://www.googleapis.com/auth/analytics.readonly";

	public const string TokenUrl = "https://oauth2.googleapis.com/token";

	public const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";

	public static Func<string> ClientId = () => (string)null;

	public static Func<string> ClientSecret = () => (string)null;

	public static Action<string> OpenBrowser = delegate(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	};

	private static readonly object TokenLock = new object();

	private static readonly Dictionary<string, KeyValuePair<string, DateTime>> Tokens = new Dictionary<string, KeyValuePair<string, DateTime>>();

	public static void Forget(AnalyticsSite site)
	{
		lock (TokenLock)
		{
			Tokens.Remove(site.Key);
		}
	}

	public static string AccessToken(AnalyticsSite site, string secret)
	{
		lock (TokenLock)
		{
			if (Tokens.TryGetValue(site.Key, out var value) && value.Value > DateTime.UtcNow.AddMinutes(1.0))
			{
				return value.Key;
			}
		}
		if (string.IsNullOrEmpty(secret))
		{
			throw new AnalyticsException(AnalyticsErrorKind.NotConfigured, "Sign in to Google again");
		}
		string url = "https://oauth2.googleapis.com/token";
		Dictionary<string, string> form;
		if (site.GoogleAuth == "service_account")
		{
			ServiceAccount serviceAccount = ServiceAccount.Parse(secret);
			url = serviceAccount.TokenUri;
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			dictionary.Add("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
			dictionary.Add("assertion", serviceAccount.CreateAssertion("https://www.googleapis.com/auth/analytics.readonly", SiteClock.UtcNow()));
			form = dictionary;
		}
		else
		{
			if (string.IsNullOrWhiteSpace(ClientId()))
			{
				throw new AnalyticsException(AnalyticsErrorKind.NotConfigured, "Add your Google OAuth client ID in Manage");
			}
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
			dictionary2.Add("grant_type", "refresh_token");
			dictionary2.Add("refresh_token", secret);
			dictionary2.Add("client_id", ClientId());
			dictionary2.Add("client_secret", ClientSecret() ?? "");
			form = dictionary2;
		}
		object o = TokenRequest(url, form);
		string text = J.Str(J.Get(o, "access_token"));
		double value2 = J.Num(J.Get(o, "expires_in")) ?? 3600.0;
		lock (TokenLock)
		{
			Tokens[site.Key] = new KeyValuePair<string, DateTime>(text, DateTime.UtcNow.AddSeconds(value2));
			return text;
		}
	}

	private static object TokenRequest(string url, Dictionary<string, string> form)
	{
		HttpCall httpCall = new HttpCall();
		httpCall.Method = "POST";
		httpCall.Url = url;
		httpCall.ContentType = "application/x-www-form-urlencoded";
		httpCall.Body = string.Join("&", form.Select((KeyValuePair<string, string> kv) => kv.Key + "=" + Uri.EscapeDataString(kv.Value ?? "")));
		HttpCall call = httpCall;
		HttpReply httpReply = AnalyticsHttp.Send(call);
		object obj = J.Parse(httpReply.Body);
		if (httpReply.Status >= 200 && httpReply.Status < 300 && J.Str(J.Get(obj, "access_token")) != null)
		{
			return obj;
		}
		string text = J.Str(J.Get(obj, "error")) ?? "";
		switch (text)
		{
		case "invalid_grant":
			throw new AnalyticsException(AnalyticsErrorKind.WrongKey, "Google sign-in expired or was revoked — sign in again");
		case "invalid_client":
		case "unauthorized_client":
			throw new AnalyticsException(AnalyticsErrorKind.WrongKey, "Google rejected the OAuth client ID / secret");
		default:
			throw new AnalyticsException((httpReply.Status < 500) ? AnalyticsErrorKind.WrongKey : AnalyticsErrorKind.Server, "Google sign-in failed (" + ((text.Length > 0) ? text : httpReply.Status.ToString()) + ")");
		}
	}

	public static string SignIn(CancellationToken cancel)
	{
		string text = ClientId();
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new AnalyticsException(AnalyticsErrorKind.NotConfigured, "Add your Google OAuth client ID first (Advanced)");
		}
		TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
		tcpListener.Start();
		try
		{
			int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
			string text2 = "http://127.0.0.1:" + port + "/";
			string text3 = RandomUrlSafe(48);
			string text4 = RandomUrlSafe(16);
			string text5;
			using (SHA256 sHA = SHA256.Create())
			{
				text5 = B64Url(sHA.ComputeHash(Encoding.ASCII.GetBytes(text3)));
			}
			string obj = "https://accounts.google.com/o/oauth2/v2/auth?client_id=" + Uri.EscapeDataString(text) + "&redirect_uri=" + Uri.EscapeDataString(text2) + "&response_type=code&scope=" + Uri.EscapeDataString("https://www.googleapis.com/auth/analytics.readonly") + "&code_challenge=" + text5 + "&code_challenge_method=S256&state=" + text4 + "&access_type=offline&prompt=consent";
			OpenBrowser(obj);
			DateTime dateTime = DateTime.UtcNow.AddMinutes(5.0);
			while (true)
			{
				cancel.ThrowIfCancellationRequested();
				if (DateTime.UtcNow > dateTime)
				{
					break;
				}
				if (!tcpListener.Pending())
				{
					Thread.Sleep(150);
					continue;
				}
				using TcpClient tcpClient = tcpListener.AcceptTcpClient();
				using NetworkStream networkStream = tcpClient.GetStream();
				networkStream.ReadTimeout = 5000;
				StreamReader streamReader = new StreamReader(networkStream, Encoding.ASCII);
				string requestLine = streamReader.ReadLine() ?? "";
				Dictionary<string, string> dictionary = ParseQuery(requestLine);
				bool flag = dictionary.ContainsKey("code") && dictionary.ContainsKey("state") && dictionary["state"] == text4;
				string text6 = (flag ? "You're signed in. You can close this tab and return to Ayn." : "Sign-in was cancelled. You can close this tab.");
				byte[] bytes = Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><body style=\"font:16px system-ui;background:#000;color:#fff;display:grid;place-items:center;height:90vh\">" + text6);
				byte[] bytes2 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
				networkStream.Write(bytes2, 0, bytes2.Length);
				networkStream.Write(bytes, 0, bytes.Length);
				if (dictionary.ContainsKey("error"))
				{
					throw new AnalyticsException(AnalyticsErrorKind.NoAccess, "Google sign-in was cancelled");
				}
				if (!flag)
				{
					continue;
				}
				object o = TokenRequest("https://oauth2.googleapis.com/token", new Dictionary<string, string>
				{
					{ "grant_type", "authorization_code" },
					{
						"code",
						dictionary["code"]
					},
					{ "redirect_uri", text2 },
					{ "client_id", text },
					{
						"client_secret",
						ClientSecret() ?? ""
					},
					{ "code_verifier", text3 }
				});
				string text7 = J.Str(J.Get(o, "refresh_token"));
				if (string.IsNullOrEmpty(text7))
				{
					throw new AnalyticsException(AnalyticsErrorKind.WrongKey, "Google didn't return a refresh token — try again");
				}
				return text7;
			}
			throw new AnalyticsException(AnalyticsErrorKind.NotConfigured, "Sign-in timed out");
		}
		finally
		{
			tcpListener.Stop();
		}
	}

	public static List<GaProperty> ListProperties(string refreshToken)
	{
		AnalyticsSite analyticsSite = new AnalyticsSite();
		analyticsSite.Key = "signin-" + Guid.NewGuid().ToString("N");
		analyticsSite.GoogleAuth = "oauth";
		AnalyticsSite site = analyticsSite;
		string text = AccessToken(site, refreshToken);
		Forget(site);
		List<GaProperty> list = new List<GaProperty>();
		string text2 = null;
		do
		{
			HttpCall httpCall = new HttpCall();
			httpCall.Method = "GET";
			httpCall.Url = "https://analyticsadmin.googleapis.com/v1beta/accountSummaries?pageSize=200" + ((text2 != null) ? ("&pageToken=" + Uri.EscapeDataString(text2)) : "");
			HttpCall httpCall2 = httpCall;
			httpCall2.Headers["Authorization"] = "Bearer " + text;
			HttpReply httpReply = AnalyticsHttp.Send(httpCall2);
			object obj = J.Parse(httpReply.Body);
			if (httpReply.Status < 200 || httpReply.Status >= 300)
			{
				string text3 = J.Str(J.Get(obj, "error", "message")) ?? "";
				if (httpReply.Status == 403 && text3.IndexOf("has not been used", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					throw new AnalyticsException(AnalyticsErrorKind.NoAccess, "Enable the “Google Analytics Admin API” in your Google Cloud project, or enter the Property ID");
				}
				throw GoogleProvider.MapError(httpReply.Status, obj, site);
			}
			object[] array = J.Arr(J.Get(obj, "accountSummaries"));
			foreach (object o in array)
			{
				object[] array2 = J.Arr(J.Get(o, "propertySummaries"));
				foreach (object o2 in array2)
				{
					list.Add(new GaProperty
					{
						Id = GoogleProvider.PropertyId(J.Str(J.Get(o2, "property"))),
						Name = J.Str(J.Get(o2, "displayName")),
						Account = J.Str(J.Get(o, "displayName"))
					});
				}
			}
			text2 = J.Str(J.Get(obj, "nextPageToken"));
		}
		while (!string.IsNullOrEmpty(text2));
		return list;
	}

	private static Dictionary<string, string> ParseQuery(string requestLine)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array = requestLine.Split(' ');
		if (array.Length < 2)
		{
			return dictionary;
		}
		int num = array[1].IndexOf('?');
		if (num < 0)
		{
			return dictionary;
		}
		string[] array2 = array[1].Substring(num + 1).Split('&');
		foreach (string text in array2)
		{
			int num2 = text.IndexOf('=');
			if (num2 > 0)
			{
				dictionary[Uri.UnescapeDataString(text.Substring(0, num2))] = Uri.UnescapeDataString(text.Substring(num2 + 1).Replace('+', ' '));
			}
		}
		return dictionary;
	}

	internal static string B64Url(byte[] b)
	{
		return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}

	private static string RandomUrlSafe(int bytes)
	{
		byte[] array = new byte[bytes];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		return B64Url(array);
	}
}
