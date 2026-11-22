using System;

namespace WindowsBuddy.Tools.Calendar;

public struct IcsOccurrence
{
	public IcsEvent Event;

	public DateTime Start;

	public DateTime End;

	public bool AllDay;
}
