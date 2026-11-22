using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsBuddy.Tools.Notes;

public static class NoteDocument
{
	public const string NewLine = "\r\n";

	public static string TypeKey(NoteBlockKind kind)
	{
		switch (kind)
		{
		case NoteBlockKind.Heading1:
			return "h1";
		case NoteBlockKind.Heading2:
			return "h2";
		case NoteBlockKind.Bullet:
			return "bullet";
		case NoteBlockKind.Check:
		case NoteBlockKind.Checked:
			return "check";
		default:
			return "p";
		}
	}

	public static NoteBlockKind KindOf(string type, bool isChecked)
	{
		switch (type)
		{
		case "h1":
			return NoteBlockKind.Heading1;
		case "h2":
			return NoteBlockKind.Heading2;
		case "bullet":
			return NoteBlockKind.Bullet;
		case "check":
			if (!isChecked)
			{
				return NoteBlockKind.Check;
			}
			return NoteBlockKind.Checked;
		default:
			return NoteBlockKind.Paragraph;
		}
	}

	public static List<NoteBlock> FromLegacy(string content)
	{
		List<NoteBlock> list = new List<NoteBlock>();
		if (string.IsNullOrEmpty(content))
		{
			return list;
		}
		string[] array = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		foreach (string line in array)
		{
			NoteBlockKind kind;
			string text = StripPrefix(line, out kind);
			NoteBlock noteBlock = new NoteBlock();
			noteBlock.Kind = kind;
			NoteBlock noteBlock2 = noteBlock;
			foreach (KeyValuePair<string, bool> item in BoldSegments(text))
			{
				if (item.Key.Length > 0)
				{
					noteBlock2.Runs.Add(new NoteRun(item.Key)
					{
						Bold = item.Value
					});
				}
			}
			list.Add(noteBlock2);
		}
		Trim(list);
		return list;
	}

	public static string StripPrefix(string line, out NoteBlockKind kind)
	{
		kind = NoteBlockKind.Paragraph;
		if (line == null)
		{
			return string.Empty;
		}
		if (line.StartsWith("- [ ] "))
		{
			kind = NoteBlockKind.Check;
			return line.Substring(6);
		}
		if (line.StartsWith("- [x] ") || line.StartsWith("- [X] "))
		{
			kind = NoteBlockKind.Checked;
			return line.Substring(6);
		}
		switch (line)
		{
		case "- [ ]":
			kind = NoteBlockKind.Check;
			return string.Empty;
		case "- [x]":
		case "- [X]":
			kind = NoteBlockKind.Checked;
			return string.Empty;
		default:
			if (line.StartsWith("[ ] "))
			{
				kind = NoteBlockKind.Check;
				return line.Substring(4);
			}
			if (line.StartsWith("[] "))
			{
				kind = NoteBlockKind.Check;
				return line.Substring(3);
			}
			if (line.StartsWith("[x] ") || line.StartsWith("[X] "))
			{
				kind = NoteBlockKind.Checked;
				return line.Substring(4);
			}
			if (line.StartsWith("## "))
			{
				kind = NoteBlockKind.Heading2;
				return line.Substring(3);
			}
			if (line.StartsWith("# "))
			{
				kind = NoteBlockKind.Heading1;
				return line.Substring(2);
			}
			if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• "))
			{
				kind = NoteBlockKind.Bullet;
				return line.Substring(2);
			}
			return line;
		}
	}

	private static List<KeyValuePair<string, bool>> BoldSegments(string text)
	{
		List<KeyValuePair<string, bool>> list = new List<KeyValuePair<string, bool>>();
		if (string.IsNullOrEmpty(text))
		{
			return list;
		}
		bool flag = false;
		int num = 0;
		while (num < text.Length)
		{
			int num2 = text.IndexOf("**", num, StringComparison.Ordinal);
			if (num2 < 0 || (!flag && text.IndexOf("**", num2 + 2, StringComparison.Ordinal) < 0))
			{
				list.Add(new KeyValuePair<string, bool>(text.Substring(num), flag));
				break;
			}
			if (num2 > num)
			{
				list.Add(new KeyValuePair<string, bool>(text.Substring(num, num2 - num), flag));
			}
			flag = !flag;
			num = num2 + 2;
		}
		return list;
	}

	public static string ToMarkdown(IList<NoteBlock> blocks)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < blocks.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append("\r\n");
			}
			NoteBlock noteBlock = blocks[i];
			switch (noteBlock.Kind)
			{
			case NoteBlockKind.Heading1:
				stringBuilder.Append("# ");
				break;
			case NoteBlockKind.Heading2:
				stringBuilder.Append("## ");
				break;
			case NoteBlockKind.Bullet:
				stringBuilder.Append("- ");
				break;
			case NoteBlockKind.Check:
				stringBuilder.Append("- [ ] ");
				break;
			case NoteBlockKind.Checked:
				stringBuilder.Append("- [x] ");
				break;
			}
			foreach (NoteRun run in noteBlock.Runs)
			{
				string text = (run.Text ?? string.Empty).Replace("**", string.Empty);
				if (run.Bold && text.Trim().Length > 0)
				{
					stringBuilder.Append("**").Append(text).Append("**");
				}
				else
				{
					stringBuilder.Append(text);
				}
			}
		}
		return stringBuilder.ToString();
	}

	public static string ToPlain(IList<NoteBlock> blocks)
	{
		if (blocks == null || blocks.Count == 0)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < blocks.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append('\n');
			}
			foreach (NoteRun run in blocks[i].Runs)
			{
				stringBuilder.Append(run.Text);
			}
		}
		return stringBuilder.ToString();
	}

	public static string ToCopyText(IList<NoteBlock> blocks)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < blocks.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append("\r\n");
			}
			switch (blocks[i].Kind)
			{
			case NoteBlockKind.Bullet:
				stringBuilder.Append("• ");
				break;
			case NoteBlockKind.Check:
				stringBuilder.Append("☐ ");
				break;
			case NoteBlockKind.Checked:
				stringBuilder.Append("☑ ");
				break;
			}
			stringBuilder.Append(blocks[i].Text);
		}
		return stringBuilder.ToString().TrimEnd();
	}

	public static void ChecklistStats(IList<NoteBlock> blocks, out int done, out int total)
	{
		done = 0;
		total = 0;
		if (blocks == null)
		{
			return;
		}
		foreach (NoteBlock block in blocks)
		{
			if (block.Kind == NoteBlockKind.Check)
			{
				total++;
			}
			else if (block.Kind == NoteBlockKind.Checked)
			{
				total++;
				done++;
			}
		}
	}

	public static string FirstLine(string plain, int skip)
	{
		if (string.IsNullOrEmpty(plain))
		{
			return null;
		}
		int num = 0;
		int num2 = 0;
		while (num < plain.Length)
		{
			int num3 = plain.IndexOf('\n', num);
			if (num3 < 0)
			{
				num3 = plain.Length;
			}
			string text = plain.Substring(num, num3 - num).Trim();
			if (text.Length > 0)
			{
				if (num2 == skip)
				{
					if (text.Length <= 200)
					{
						return text;
					}
					return text.Substring(0, 200);
				}
				num2++;
			}
			num = num3 + 1;
		}
		return null;
	}

	public static int WordCount(string plain)
	{
		if (string.IsNullOrEmpty(plain))
		{
			return 0;
		}
		int num = 0;
		bool flag = false;
		foreach (char c in plain)
		{
			if (char.IsWhiteSpace(c))
			{
				flag = false;
			}
			else if (!flag)
			{
				flag = true;
				num++;
			}
		}
		return num;
	}

	public static string Signature(IList<NoteBlock> blocks)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (blocks == null)
		{
			return string.Empty;
		}
		foreach (NoteBlock block in blocks)
		{
			stringBuilder.Append((int)block.Kind).Append('\u0001');
			foreach (NoteRun run in block.Runs)
			{
				stringBuilder.Append(run.Bold ? 'b' : '-').Append(run.Italic ? 'i' : '-').Append(run.Underline ? 'u' : '-')
					.Append(run.Strike ? 's' : '-')
					.Append(run.Color)
					.Append('\u0002')
					.Append(run.Text)
					.Append('\u0003');
			}
			stringBuilder.Append('\u0004');
		}
		return stringBuilder.ToString();
	}

	public static List<NoteBlock> Normalize(List<NoteBlock> blocks)
	{
		foreach (NoteBlock block in blocks)
		{
			List<NoteRun> list = new List<NoteRun>(block.Runs.Count);
			foreach (NoteRun run in block.Runs)
			{
				if (!string.IsNullOrEmpty(run.Text))
				{
					NoteRun noteRun = ((list.Count > 0) ? list[list.Count - 1] : null);
					if (noteRun != null && noteRun.SameStyle(run))
					{
						noteRun.Text += run.Text;
					}
					else
					{
						list.Add(run);
					}
				}
			}
			block.Runs = list;
		}
		Trim(blocks);
		return blocks;
	}

	private static void Trim(List<NoteBlock> blocks)
	{
		while (blocks.Count > 0)
		{
			NoteBlock noteBlock = blocks[blocks.Count - 1];
			if (noteBlock.Kind != 0 || noteBlock.Text.Trim().Length > 0)
			{
				break;
			}
			blocks.RemoveAt(blocks.Count - 1);
		}
	}

	public static List<NoteBlock> Clone(IList<NoteBlock> blocks)
	{
		List<NoteBlock> list = new List<NoteBlock>(blocks?.Count ?? 0);
		if (blocks == null)
		{
			return list;
		}
		foreach (NoteBlock block in blocks)
		{
			NoteBlock noteBlock = new NoteBlock();
			noteBlock.Kind = block.Kind;
			NoteBlock noteBlock2 = noteBlock;
			foreach (NoteRun run in block.Runs)
			{
				noteBlock2.Runs.Add(new NoteRun(run.Text)
				{
					Bold = run.Bold,
					Italic = run.Italic,
					Underline = run.Underline,
					Strike = run.Strike,
					Color = run.Color
				});
			}
			list.Add(noteBlock2);
		}
		return list;
	}
}
