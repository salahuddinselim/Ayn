using System.Collections.Generic;

namespace WindowsBuddy.Tools.FileShelf;

public class ShelfStorageDto
{
	public int Version { get; set; }

	public List<ShelfCopyRecovery> Copies { get; set; }

	public int ActiveIndex { get; set; }

	public string ActiveId { get; set; }

	public string Sort { get; set; }

	public List<ShelfTabDto> Shelves { get; set; }
}
