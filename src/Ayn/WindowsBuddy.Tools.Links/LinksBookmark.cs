using System;

namespace WindowsBuddy.Tools.Links;

[Serializable]
public class LinksBookmark
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Url { get; set; }

	public bool Pinned { get; set; }

	public bool FromClipboard { get; set; }

	public long AddedTicks { get; set; }

	public LinksBookmark()
	{
		Id = Guid.NewGuid().ToString("N");
		AddedTicks = DateTime.UtcNow.Ticks;
	}

	public LinksBookmark(string name, string url)
		: this()
	{
		Name = name;
		Url = url;
	}
}
