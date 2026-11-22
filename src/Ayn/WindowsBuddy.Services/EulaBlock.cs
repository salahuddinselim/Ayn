namespace WindowsBuddy.Services;

public sealed class EulaBlock
{
	public readonly EulaBlockKind Kind;

	public readonly string Text;

	public EulaBlock(EulaBlockKind kind, string text)
	{
		Kind = kind;
		Text = text;
	}
}
