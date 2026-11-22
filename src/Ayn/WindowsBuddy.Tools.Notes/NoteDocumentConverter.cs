using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Notes;

public sealed class NoteDocumentConverter : JavaScriptConverter
{
	public override IEnumerable<Type> SupportedTypes => new Type[2]
	{
		typeof(NoteBlock),
		typeof(NoteRun)
	};

	public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		if (obj is NoteBlock noteBlock)
		{
			dictionary["type"] = NoteDocument.TypeKey(noteBlock.Kind);
			if (noteBlock.Kind == NoteBlockKind.Checked)
			{
				dictionary["checked"] = true;
			}
			dictionary["runs"] = noteBlock.Runs;
			return dictionary;
		}
		if (obj is NoteRun noteRun)
		{
			dictionary["text"] = noteRun.Text ?? string.Empty;
			if (noteRun.Bold)
			{
				dictionary["b"] = true;
			}
			if (noteRun.Italic)
			{
				dictionary["i"] = true;
			}
			if (noteRun.Underline)
			{
				dictionary["u"] = true;
			}
			if (noteRun.Strike)
			{
				dictionary["s"] = true;
			}
			if (!string.IsNullOrEmpty(noteRun.Color))
			{
				dictionary["color"] = noteRun.Color;
			}
		}
		return dictionary;
	}

	public override object Deserialize(IDictionary<string, object> d, Type type, JavaScriptSerializer serializer)
	{
		if (type == typeof(NoteRun))
		{
			return ReadRun(d);
		}
		NoteBlock noteBlock = new NoteBlock();
		noteBlock.Kind = NoteDocument.KindOf(Str(d, "type"), Flag(d, "checked"));
		NoteBlock noteBlock2 = noteBlock;
		object value;
		IEnumerable enumerable = (d.TryGetValue("runs", out value) ? (value as IEnumerable) : null);
		if (enumerable != null)
		{
			foreach (object item in enumerable)
			{
				if (item is IDictionary<string, object> d2)
				{
					noteBlock2.Runs.Add(ReadRun(d2));
				}
				else if (item is NoteRun)
				{
					noteBlock2.Runs.Add((NoteRun)item);
				}
			}
		}
		return noteBlock2;
	}

	private static NoteRun ReadRun(IDictionary<string, object> d)
	{
		string text = Str(d, "color");
		NoteRun noteRun = new NoteRun(Str(d, "text"));
		noteRun.Bold = Flag(d, "b");
		noteRun.Italic = Flag(d, "i");
		noteRun.Underline = Flag(d, "u");
		noteRun.Strike = Flag(d, "s");
		noteRun.Color = (NotePalette.IsKnown(text) ? text : null);
		return noteRun;
	}

	private static string Str(IDictionary<string, object> d, string key)
	{
		if (!d.TryGetValue(key, out var value) || value == null)
		{
			return null;
		}
		return Convert.ToString(value, CultureInfo.InvariantCulture);
	}

	private static bool Flag(IDictionary<string, object> d, string key)
	{
		if (!d.TryGetValue(key, out var value) || value == null)
		{
			return false;
		}
		if (value is bool)
		{
			return (bool)value;
		}
		try
		{
			return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
		}
		catch
		{
			return false;
		}
	}
}
