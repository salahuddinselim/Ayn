using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Units;

public class UnitsState
{
	public string Category { get; set; }

	public string Value { get; set; }

	public Dictionary<string, string> Pairs { get; set; }

	public List<Recent> Recents { get; set; }

	private static string FilePath => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "units", "units_state.json");

	public static event Action Changed;

	public UnitsState()
	{
		Category = "Length";
		Value = "1";
		Pairs = new Dictionary<string, string>();
		Recents = new List<Recent>();
	}

	public static UnitsState Load()
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(FilePath);
			if (!string.IsNullOrEmpty(text))
			{
				UnitsState unitsState = new JavaScriptSerializer().Deserialize<UnitsState>(text);
				if (unitsState != null)
				{
					if (unitsState.Pairs == null)
					{
						unitsState.Pairs = new Dictionary<string, string>();
					}
					if (unitsState.Recents == null)
					{
						unitsState.Recents = new List<Recent>();
					}
					if (string.IsNullOrEmpty(unitsState.Value))
					{
						unitsState.Value = "1";
					}
					return unitsState;
				}
			}
		}
		catch
		{
		}
		return new UnitsState();
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			PersistenceHelper.SafeWriteAllText(FilePath, new JavaScriptSerializer().Serialize(this));
			UnitsState.Changed?.Invoke();
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(FilePath, error);
		}
	}
}
