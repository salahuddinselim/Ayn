using System;
using System.Collections.Generic;

namespace WindowsBuddy.Core;

public class CursorPackManifest
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Version { get; set; }

	public string Author { get; set; }

	public string VisualStyle { get; set; }

	public string Description { get; set; }

	public int[] Resolutions { get; set; }

	public Dictionary<string, CursorStateMetadata> States { get; set; }

	public CursorPackManifest()
	{
		Resolutions = new int[3] { 32, 48, 64 };
		States = new Dictionary<string, CursorStateMetadata>(StringComparer.OrdinalIgnoreCase);
	}
}
