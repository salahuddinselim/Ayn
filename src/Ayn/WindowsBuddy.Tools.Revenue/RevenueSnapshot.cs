using System;
using System.Collections.Generic;
using WindowsBuddy.Tools.Analytics;

namespace WindowsBuddy.Tools.Revenue;

public class RevenueSnapshot
{
	public string Provider { get; set; }

	public RevenueRange Range { get; set; }

	public DateTime FetchedUtc { get; set; }

	public string RangeStart { get; set; }

	public string RangeEnd { get; set; }

	public string Currency { get; set; }

	public long? Gross { get; set; }

	public long? Net { get; set; }

	public long? Refunds { get; set; }

	public int? Sales { get; set; }

	public long? Available { get; set; }

	public long? Pending { get; set; }

	public List<string> OtherCurrencies { get; set; }

	public bool Partial { get; set; }

	public List<SeriesPoint> Series { get; set; }

	public RevenueSnapshot()
	{
		OtherCurrencies = new List<string>();
		Series = new List<SeriesPoint>();
	}
}
