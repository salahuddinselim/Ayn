namespace WindowsBuddy.Tools;

public class CollapsedBadgeData
{
	public string Text { get; set; }

	public string AccentColor { get; set; }

	public bool IsHighlighted { get; set; }

	public CollapsedBadgeData(string text, string accentColor = null, bool isHighlighted = false)
	{
		Text = text;
		AccentColor = accentColor;
		IsHighlighted = isHighlighted;
	}
}
