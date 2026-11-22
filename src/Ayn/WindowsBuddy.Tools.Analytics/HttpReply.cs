using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Analytics;

public sealed class HttpReply
{
	public int Status;

	public string Body;

	public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
