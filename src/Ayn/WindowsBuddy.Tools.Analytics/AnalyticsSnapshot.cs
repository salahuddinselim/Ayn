using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Analytics;

public class AnalyticsSnapshot
{
	public string SiteKey { get; set; }

	public AnalyticsRange Range { get; set; }

	public DateTime FetchedUtc { get; set; }

	public long? Visitors { get; set; }

	public long? PreviousVisitors { get; set; }

	public long? Pageviews { get; set; }

	public double? BounceRate { get; set; }

	public double? AvgDurationSeconds { get; set; }

	public double? Revenue { get; set; }

	public string Currency { get; set; }

	public List<SeriesPoint> Series { get; set; }

	public List<RankedRow> Pages { get; set; }

	public List<RankedRow> Sources { get; set; }

	public List<RankedRow> Countries { get; set; }

	public string RangeStart { get; set; }

	public string RangeEnd { get; set; }

	public string PreviousStart { get; set; }

	public string PreviousEnd { get; set; }

	public string TimeZone { get; set; }

	public double? ChangePercent
	{
		get
		{
			if (!Visitors.HasValue || !PreviousVisitors.HasValue || PreviousVisitors.Value <= 0)
			{
				return null;
			}
			return (double)(Visitors.Value - PreviousVisitors.Value) * 100.0 / (double)PreviousVisitors.Value;
		}
	}

	public AnalyticsSnapshot()
	{
		Series = new List<SeriesPoint>();
		Pages = new List<RankedRow>();
		Sources = new List<RankedRow>();
		Countries = new List<RankedRow>();
	}

	public List<RankedRow> Rows(Breakdown b)
	{
		return b switch
		{
			Breakdown.Sources => Sources, 
			Breakdown.Pages => Pages, 
			_ => Countries, 
		};
	}
}
