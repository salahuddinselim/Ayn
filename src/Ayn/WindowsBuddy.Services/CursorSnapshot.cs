using System;
using System.Collections.Generic;

namespace WindowsBuddy.Services;

public class CursorSnapshot
{
	public string Timestamp { get; set; }

	public string SchemeName { get; set; }

	public int SchemeSource { get; set; }

	public Dictionary<string, string> CursorMap { get; set; }

	public CursorSnapshot()
	{
		CursorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	}
}
