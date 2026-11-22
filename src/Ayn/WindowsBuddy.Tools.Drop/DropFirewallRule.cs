namespace WindowsBuddy.Tools.Drop;

public sealed class DropFirewallRule
{
	public string Application { get; set; }

	public bool Inbound { get; set; }

	public bool Block { get; set; }

	public bool Enabled { get; set; }

	public int Profiles { get; set; }
}
