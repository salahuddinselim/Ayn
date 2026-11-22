using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.State;

public class CapsuleTextSpan
{
	public string Text { get; set; }

	public Brush Foreground { get; set; }

	public Brush Background { get; set; }

	public bool IsBold { get; set; }

	public CapsuleTextSpan()
	{
	}

	public CapsuleTextSpan(string text)
	{
		Text = text;
	}

	public CapsuleTextSpan(string text, Brush foreground, Brush background, bool isBold)
	{
		Text = text;
		Foreground = foreground;
		Background = background;
		IsBold = isBold;
	}
}
