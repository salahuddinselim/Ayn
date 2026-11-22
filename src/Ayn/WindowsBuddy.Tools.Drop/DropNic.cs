using System.Net.NetworkInformation;

namespace WindowsBuddy.Tools.Drop;

public sealed class DropNic
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Description { get; set; }

	public NetworkInterfaceType Type { get; set; }

	public OperationalStatus Status { get; set; }

	public int IfIndex { get; set; }

	public string[] Addresses { get; set; }

	public string[] Gateways { get; set; }

	public bool? Hardware { get; set; }

	public bool DefaultRoute { get; set; }
}
