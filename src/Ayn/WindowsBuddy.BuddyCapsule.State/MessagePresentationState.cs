namespace WindowsBuddy.BuddyCapsule.State;

public class MessagePresentationState
{
	public string Text { get; set; }

	public string AnimationMode { get; set; }

	public double Speed { get; set; }

	public double Spacing { get; set; }

	public string ColorHex { get; set; }

	public double Brightness { get; set; }

	public bool IsPinned { get; set; }

	public bool IsFlash { get; set; }

	public MessagePresentationState()
	{
		Text = string.Empty;
		AnimationMode = "Scroll";
		Speed = 0.5;
		Spacing = 0.4;
		ColorHex = "#FFFFFF";
		Brightness = 0.9;
		IsPinned = false;
		IsFlash = false;
	}
}
