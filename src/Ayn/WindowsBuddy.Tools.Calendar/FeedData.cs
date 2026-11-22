using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar;

public sealed class FeedData
{
	public string Key;

	public string Name;

	public List<IcsEvent> Events;

	public DateTime FetchedUtc;
}
