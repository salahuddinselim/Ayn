namespace WindowsBuddy.Tools.Units;

internal sealed class Unit
{
	public readonly string Symbol;

	public readonly string Name;

	public readonly decimal Factor;

	public Unit(string symbol, string name, decimal factor)
	{
		Symbol = symbol;
		Name = name;
		Factor = factor;
	}
}
