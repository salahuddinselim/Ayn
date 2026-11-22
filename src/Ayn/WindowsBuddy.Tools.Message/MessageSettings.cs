using System;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Message;

public class MessageSettings
{
	public string Text { get; set; }

	public MessageAnimationMode Animation { get; set; }

	public double Speed { get; set; }

	public string ColorHex { get; set; }

	public string CustomColorHex { get; set; }

	public MessageFinish Finish { get; set; }

	public MessageDurationMode Duration { get; set; }

	public int CustomDurationSeconds { get; set; }

	public bool RepeatEnabled { get; set; }

	[ScriptIgnore]
	public TimeSpan DurationSpan => Duration switch
	{
		MessageDurationMode.Sec3 => TimeSpan.FromSeconds(3.0), 
		MessageDurationMode.Sec10 => TimeSpan.FromSeconds(10.0), 
		MessageDurationMode.Custom => TimeSpan.FromSeconds(MessageText.ClampSeconds(CustomDurationSeconds)), 
		_ => TimeSpan.FromSeconds(5.0), 
	};

	public MessageSettings()
	{
		Text = string.Empty;
		Animation = MessageAnimationMode.Slide;
		Speed = 50.0;
		ColorHex = "#FFFFFF";
		Finish = MessageFinish.None;
		Duration = MessageDurationMode.Sec5;
		CustomDurationSeconds = 30;
	}

	public MessageSettings Clone()
	{
		return (MessageSettings)MemberwiseClone();
	}

	internal void Sanitize()
	{
		Text = MessageText.Limit(Text);
		if (!Enum.IsDefined(typeof(MessageAnimationMode), Animation))
		{
			Animation = MessageAnimationMode.Slide;
		}
		if (!Enum.IsDefined(typeof(MessageFinish), Finish))
		{
			Finish = MessageFinish.None;
		}
		if (Duration == (MessageDurationMode)4)
		{
			RepeatEnabled = true;
			Duration = MessageDurationMode.Sec5;
		}
		else if (!Enum.IsDefined(typeof(MessageDurationMode), Duration))
		{
			Duration = MessageDurationMode.Sec5;
		}
		if (double.IsNaN(Speed) || double.IsInfinity(Speed))
		{
			Speed = 50.0;
		}
		Speed = Math.Max(0.0, Math.Min(100.0, Speed));
		CustomDurationSeconds = MessageText.ClampSeconds(CustomDurationSeconds);
		if (!MessageText.ParseColor(ColorHex).HasValue)
		{
			ColorHex = "#FFFFFF";
		}
		if (!MessageText.ParseColor(CustomColorHex).HasValue)
		{
			CustomColorHex = null;
		}
	}
}
