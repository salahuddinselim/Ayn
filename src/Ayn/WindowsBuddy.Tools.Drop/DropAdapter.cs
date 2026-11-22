namespace WindowsBuddy.Tools.Drop;

public sealed class DropAdapter
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Address { get; set; }

	public bool Private { get; set; }

	public bool Gateway { get; set; }

	public bool DefaultRoute { get; set; }

	public override string ToString()
	{
		return Name + " · " + Address;
	}
}
