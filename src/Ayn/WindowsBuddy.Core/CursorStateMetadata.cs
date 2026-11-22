using System.Collections.Generic;

namespace WindowsBuddy.Core;

public class CursorStateMetadata
{
	public string RegistryKey { get; set; }

	public string DisplayName { get; set; }

	public string File { get; set; }

	public Dictionary<string, int[]> Hotspots { get; set; }

	public string OpticalAnchor { get; set; }

	public CursorStateMetadata()
	{
		Hotspots = new Dictionary<string, int[]>();
	}
}
