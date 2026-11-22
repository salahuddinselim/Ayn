namespace WindowsBuddy.Tools.AICoding;

public class UsageRow
{
	public string Name { get; set; }

	public AICodingToolType Agent { get; set; }

	public AgentStatus Status { get; set; }

	public long Tokens { get; set; }

	public double Cost { get; set; }

	public bool HasUnpriced { get; set; }

	public long Input { get; set; }

	public long Output { get; set; }

	public long CacheWrite { get; set; }

	public long CacheRead { get; set; }
}
