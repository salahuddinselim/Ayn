using System;
using System.Globalization;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Calendar;

public sealed class UserReminder
{
	public string Id { get; set; }

	public string Title { get; set; }

	public string Date { get; set; }

	public string Time { get; set; }

	public CalRepeat Repeat { get; set; }

	public bool Completed { get; set; }

	public long CompletedUtc { get; set; }

	public long CreatedUtc { get; set; }

	[ScriptIgnore]
	public DateTime DueDate => Cal.ParseDate(Date) ?? DateTime.Today;

	[ScriptIgnore]
	public TimeSpan? DueTime => Cal.ParseTime(Time);

	[ScriptIgnore]
	public DateTime OverdueAt
	{
		get
		{
			TimeSpan? dueTime = DueTime;
			if (!dueTime.HasValue)
			{
				return DueDate.AddDays(1.0);
			}
			return DueDate + dueTime.Value;
		}
	}

	public UserReminder()
	{
		Id = Guid.NewGuid().ToString("N");
		Title = "";
		Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		CreatedUtc = DateTime.UtcNow.Ticks;
	}

	public bool IsOverdue(DateTime now)
	{
		if (!Completed)
		{
			return now >= OverdueAt;
		}
		return false;
	}

	public UserReminder Clone()
	{
		return (UserReminder)MemberwiseClone();
	}
}
