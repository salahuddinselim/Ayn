using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar;

public sealed class QuickAddResult
{
	public string Title;

	public DateTime Date;

	public bool HasDate;

	public TimeSpan? Start;

	public TimeSpan? End;

	public CalRepeat Repeat;

	public bool AllDay => !Start.HasValue;

	public string Describe(DateTime today)
	{
		List<string> list = new List<string>();
		list.Add(Cal.DayName(Date, today));
		List<string> list2 = list;
		if (Start.HasValue)
		{
			list2.Add(End.HasValue ? Cal.Range(Date + Start.Value, Date + End.Value) : Cal.Time(Date + Start.Value));
		}
		else
		{
			list2.Add("All day");
		}
		if (Repeat != 0)
		{
			list2.Add((Repeat == CalRepeat.Weekly) ? ("Every " + Date.ToString("dddd", Cal.Inv)) : Cal.RepeatLabel(Repeat));
		}
		return string.Join(" · ", list2);
	}
}
