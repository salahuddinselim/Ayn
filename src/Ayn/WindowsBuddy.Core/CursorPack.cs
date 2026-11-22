using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsBuddy.Core;

public class CursorPack
{
	public CursorPackManifest Manifest { get; set; }

	public string DirectoryPath { get; set; }

	public string Id
	{
		get
		{
			if (Manifest == null)
			{
				return string.Empty;
			}
			return Manifest.Id;
		}
	}

	public string Name
	{
		get
		{
			if (Manifest == null)
			{
				return string.Empty;
			}
			return Manifest.Name;
		}
	}

	public string GetStateFilePath(string stateKey)
	{
		if (Manifest == null || !Manifest.States.ContainsKey(stateKey))
		{
			return null;
		}
		string file = Manifest.States[stateKey].File;
		return Path.Combine(DirectoryPath, file);
	}

	public Dictionary<string, string> ResolveRegistryMapping()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (Manifest == null || Manifest.States == null)
		{
			return dictionary;
		}
		foreach (KeyValuePair<string, CursorStateMetadata> state in Manifest.States)
		{
			CursorStateMetadata value = state.Value;
			string fullPath = Path.GetFullPath(Path.Combine(DirectoryPath, value.File));
			dictionary[value.RegistryKey] = fullPath;
		}
		return dictionary;
	}
}
