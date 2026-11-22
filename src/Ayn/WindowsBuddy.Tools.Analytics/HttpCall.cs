using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Analytics;

public sealed class HttpCall
{
	public string Method;

	public string Url;

	public string Body;

	public string ContentType;

	public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
