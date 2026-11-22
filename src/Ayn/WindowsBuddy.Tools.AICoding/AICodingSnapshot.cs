using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.AICoding;

public class AICodingSnapshot
{
	public AICodingToolType Tool { get; set; }

	public AgentStatus Status { get; set; }

	public DateTime LastUpdated { get; set; }

	public long TokensToday { get; set; }

	public double CostToday { get; set; }

	public long UnpricedTokensToday { get; set; }

	public List<string> UnpricedModels { get; set; }

	public int MessagesToday { get; set; }

	public int ToolCallsToday { get; set; }

	public int SessionsToday { get; set; }

	public long[] HourlyTokens { get; set; }

	public string CurrentProject { get; set; }

	public string CurrentProjectPath { get; set; }

	public int StreakDays { get; set; }

	public int ActiveDays { get; set; }

	public double CostThisMonth { get; set; }

	public long TokensThisMonth { get; set; }

	public long UnpricedTokensThisMonth { get; set; }

	public double MonthlyBudget { get; set; }

	public List<UsageRow> Rows { get; set; }

	public bool HasActivityToday
	{
		get
		{
			if (TokensToday <= 0)
			{
				return MessagesToday > 0;
			}
			return true;
		}
	}

	public string PeakWindow
	{
		get
		{
			if (HourlyTokens == null || HourlyTokens.Length < 24)
			{
				return null;
			}
			long num = 0L;
			int num2 = -1;
			for (int i = 0; i < 24; i++)
			{
				if (HourlyTokens[i] > 0)
				{
					long num3 = HourlyTokens[i] + ((i < 23) ? HourlyTokens[i + 1] : 0);
					if (num3 > num)
					{
						num = num3;
						num2 = i;
					}
				}
			}
			if (num2 < 0)
			{
				return null;
			}
			int end = ((num2 < 23 && HourlyTokens[num2 + 1] > 0) ? (num2 + 2) : (num2 + 1));
			return "Peak " + ClockFormat.Range(num2, end);
		}
	}

	public AICodingSnapshot()
	{
		HourlyTokens = new long[24];
		UnpricedModels = new List<string>();
		Rows = new List<UsageRow>();
		LastUpdated = DateTime.Now;
	}

	public AICodingSnapshot(AICodingToolType tool)
		: this()
	{
		Tool = tool;
	}
}
