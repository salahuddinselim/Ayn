using System;
using System.Net;
using System.Text;

namespace WindowsBuddy.Services;

public class SafeWebClient : WebClient
{
	private readonly int _timeoutMs;

	public SafeWebClient(int timeoutMs = 6000)
	{
		_timeoutMs = timeoutMs;
		base.Encoding = Encoding.UTF8;
	}

	protected override WebRequest GetWebRequest(Uri address)
	{
		WebRequest webRequest = base.GetWebRequest(address);
		if (webRequest != null)
		{
			webRequest.Timeout = _timeoutMs;
			if (webRequest is HttpWebRequest httpWebRequest)
			{
				httpWebRequest.ReadWriteTimeout = _timeoutMs;
			}
		}
		return webRequest;
	}
}
