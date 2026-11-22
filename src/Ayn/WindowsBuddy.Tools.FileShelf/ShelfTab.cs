using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.FileShelf;

public class ShelfTab
{
	public string Id { get; set; }

	public string Name { get; set; }

	public List<ParkedFile> Files { get; set; }

	public ShelfTab()
		: this(Guid.NewGuid().ToString("N"), "Desk")
	{
	}

	public ShelfTab(string id, string name)
	{
		Id = (string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id);
		Name = name;
		Files = new List<ParkedFile>();
	}
}
