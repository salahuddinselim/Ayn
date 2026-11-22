using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Notes;

public class NoteItem
{
	public string Id { get; set; }

	public string Title { get; set; }

	public string Content { get; set; }

	public long CreatedAtTicks { get; set; }

	public string CreatedAtFormatted { get; set; }

	public long UpdatedAtTicks { get; set; }

	public string UpdatedAtFormatted { get; set; }

	public bool IsPinned { get; set; }

	public List<NoteBlock> Blocks { get; set; }

	[ScriptIgnore]
	public string Plain { get; private set; }

	[ScriptIgnore]
	public int ChecklistDone { get; private set; }

	[ScriptIgnore]
	public int ChecklistTotal { get; private set; }

	public NoteItem()
	{
		Id = Guid.NewGuid().ToString("N");
		Title = string.Empty;
		Content = string.Empty;
		Blocks = new List<NoteBlock>();
		Plain = string.Empty;
		CreatedAtTicks = DateTime.UtcNow.Ticks;
		CreatedAtFormatted = DateTime.Now.ToString("MMM d, yyyy");
		UpdatedAtTicks = DateTime.UtcNow.Ticks;
		UpdatedAtFormatted = DateTime.Now.ToString("MMM d, h:mm tt");
		IsPinned = false;
	}

	public NoteItem(string title, string content)
		: this()
	{
		Title = title ?? string.Empty;
		SetBlocks(NoteDocument.FromLegacy(content));
	}

	public void SetBlocks(List<NoteBlock> blocks)
	{
		Blocks = blocks ?? new List<NoteBlock>();
		Project();
	}

	public void Project()
	{
		if (Blocks == null)
		{
			Blocks = new List<NoteBlock>();
		}
		Content = NoteDocument.ToMarkdown(Blocks);
		Plain = NoteDocument.ToPlain(Blocks);
		NoteDocument.ChecklistStats(Blocks, out var done, out var total);
		ChecklistDone = done;
		ChecklistTotal = total;
	}

	public NoteItem Clone()
	{
		NoteItem noteItem = new NoteItem();
		noteItem.Id = Id;
		noteItem.Title = Title;
		noteItem.Content = Content;
		noteItem.CreatedAtTicks = CreatedAtTicks;
		noteItem.CreatedAtFormatted = CreatedAtFormatted;
		noteItem.UpdatedAtTicks = UpdatedAtTicks;
		noteItem.UpdatedAtFormatted = UpdatedAtFormatted;
		noteItem.IsPinned = IsPinned;
		noteItem.Blocks = NoteDocument.Clone(Blocks);
		NoteItem noteItem2 = noteItem;
		noteItem2.Plain = Plain;
		noteItem2.ChecklistDone = ChecklistDone;
		noteItem2.ChecklistTotal = ChecklistTotal;
		return noteItem2;
	}

	public string GetFormattedTimestamp()
	{
		try
		{
			DateTime dateTime = ((UpdatedAtTicks > 0) ? new DateTime(UpdatedAtTicks, DateTimeKind.Utc).ToLocalTime() : DateTime.Now);
			DateTime now = DateTime.Now;
			if (dateTime.Date == now.Date)
			{
				return dateTime.ToString("h:mm tt");
			}
			if (dateTime.Year == now.Year)
			{
				return dateTime.ToString("MMM d");
			}
			return dateTime.ToString("MMM d, yyyy");
		}
		catch
		{
			return UpdatedAtFormatted ?? "Recently";
		}
	}
}
