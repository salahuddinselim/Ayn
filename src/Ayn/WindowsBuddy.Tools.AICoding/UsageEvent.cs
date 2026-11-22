namespace WindowsBuddy.Tools.AICoding;

internal sealed class UsageEvent
{
	public string Key;

	public AICodingToolType Agent;

	public long Utc;

	public string Model;

	public string Session;

	public string Cwd;

	public long In;

	public long Out;

	public long W5;

	public long W1h;

	public long Read;

	public bool Fast;

	public int Messages;

	public int Tools;

	public int Active;

	public long Tokens => In + Out + W5 + W1h + Read;
}
