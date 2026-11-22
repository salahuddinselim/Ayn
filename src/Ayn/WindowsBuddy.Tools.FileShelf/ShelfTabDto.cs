using System.Collections.Generic;

namespace WindowsBuddy.Tools.FileShelf;

public class ShelfTabDto
{
	public string Id { get; set; }

	public string Name { get; set; }

	public List<string> Paths { get; set; }

	public List<ShelfItemDto> Items { get; set; }
}
