using System.Collections.Generic;
using System.Text;

namespace WindowsBuddy.Tools.Notes;

public sealed class NoteBlock
{
	public NoteBlockKind Kind;

	public List<NoteRun> Runs = new List<NoteRun>();

	public string Text
	{
		get
		{
			if (Runs.Count == 1)
			{
				return Runs[0].Text ?? string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (NoteRun run in Runs)
			{
				stringBuilder.Append(run.Text);
			}
			return stringBuilder.ToString();
		}
	}

	public bool IsList
	{
		get
		{
			if (Kind != NoteBlockKind.Bullet && Kind != NoteBlockKind.Check)
			{
				return Kind == NoteBlockKind.Checked;
			}
			return true;
		}
	}

	public NoteBlock()
	{
	}

	public NoteBlock(NoteBlockKind kind, string text)
	{
		Kind = kind;
		if (!string.IsNullOrEmpty(text))
		{
			Runs.Add(new NoteRun(text));
		}
	}
}
