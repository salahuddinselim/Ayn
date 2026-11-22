using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Calendar;

public sealed class UserEvent
{
	public string Id { get; set; }

	public string Title { get; set; }

	public string Date { get; set; }

	public string EndDate { get; set; }

	public string Start { get; set; }

	public string End { get; set; }

	public CalRepeat Repeat { get; set; }

	public CalAlert Alert { get; set; }

	public CalColor Color { get; set; }

	public List<string> Skips { get; set; }

	public long CreatedUtc { get; set; }

	[ScriptIgnore]
	public bool AllDay => string.IsNullOrEmpty(Start);

	[ScriptIgnore]
	public DateTime StartDate => Cal.ParseDate(Date) ?? DateTime.Today;

	[ScriptIgnore]
	public DateTime LastDate
	{
		get
		{
			DateTime dateTime = Cal.ParseDate(EndDate) ?? StartDate;
			if (!(dateTime < StartDate))
			{
				return dateTime;
			}
			return StartDate;
		}
	}

	[ScriptIgnore]
	public DateTime StartAt => StartDate + (Cal.ParseTime(Start) ?? TimeSpan.Zero);

	[ScriptIgnore]
	public DateTime EndAt
	{
		get
		{
			if (AllDay)
			{
				return LastDate.AddDays(1.0);
			}
			DateTime dateTime = LastDate + (Cal.ParseTime(End) ?? Cal.ParseTime(Start) ?? TimeSpan.Zero);
			if (!(dateTime < StartAt))
			{
				return dateTime;
			}
			return StartAt;
		}
	}

	public UserEvent()
	{
		Id = Guid.NewGuid().ToString("N");
		Title = "";
		Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		Skips = new List<string>();
		CreatedUtc = DateTime.UtcNow.Ticks;
	}

	public UserEvent Clone()
	{
		UserEvent userEvent = (UserEvent)MemberwiseClone();
		userEvent.Skips = new List<string>(Skips ?? new List<string>());
		return userEvent;
	}
}
