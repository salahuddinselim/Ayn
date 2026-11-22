namespace WindowsBuddy.Tools.Notes;

public sealed class NoteRun
{
	public string Text = string.Empty;

	public bool Bold;

	public bool Italic;

	public bool Underline;

	public bool Strike;

	public string Color;

	public NoteRun()
	{
	}

	public NoteRun(string text)
	{
		Text = text ?? string.Empty;
	}

	public bool SameStyle(NoteRun o)
	{
		if (o != null && Bold == o.Bold && Italic == o.Italic && Underline == o.Underline && Strike == o.Strike)
		{
			return Color == o.Color;
		}
		return false;
	}
}
