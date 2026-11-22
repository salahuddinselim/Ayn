using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar;

public sealed class IcsCalendar
{
	public string Name;

	public List<IcsEvent> Events = new List<IcsEvent>();
}
