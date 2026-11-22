using System.Windows.Media;

namespace WindowsBuddy.Tools.Clipboard;

public class LinkMetadata
{
	public string Url { get; set; }

	public string Title { get; set; }

	public ImageSource Favicon { get; set; }

	public bool IsLoaded { get; set; }
}
