using System;

namespace WindowsBuddy.Tools.Revenue;

public interface IRevenueProvider
{
	string Id { get; }

	string Name { get; }

	string CheckKey(string key);

	RevenueTestResult Test(string key);

	RevenueSnapshot Fetch(string key, RevenueRange range, DateTime nowLocal);
}
