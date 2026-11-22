namespace WindowsBuddy.Tools.Revenue;

public class RevenueConfigModel
{
	public int Version { get; set; }

	public bool IsConfigured { get; set; }

	public string Provider { get; set; }

	public RevenueRange Range { get; set; }

	public string AccountLabel { get; set; }

	public bool TestMode { get; set; }

	public RevenueConfigModel()
	{
		Version = 2;
		Provider = "stripe";
		Range = RevenueRange.Week;
	}
}
