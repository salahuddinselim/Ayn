using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.AICoding;

public class AICodingBudgetConfig
{
	public Dictionary<string, double> Budgets { get; set; }

	public List<string> AlertsShown { get; set; }

	public AICodingBudgetConfig()
	{
		Budgets = new Dictionary<string, double>();
		AlertsShown = new List<string>();
	}

	public double Get(AICodingToolType filter)
	{
		if (Budgets == null || !Budgets.TryGetValue(filter.ToString(), out var value) || !(value > 0.0))
		{
			return 0.0;
		}
		return value;
	}

	public void Set(AICodingToolType filter, double usd)
	{
		if (Budgets == null)
		{
			Budgets = new Dictionary<string, double>();
		}
		if (usd > 0.0)
		{
			Budgets[filter.ToString()] = Math.Round(usd, 2);
		}
		else
		{
			Budgets.Remove(filter.ToString());
		}
		Save();
	}

	private static string PathFor()
	{
		string text = Path.Combine(AppPaths.RoamingAppData, AppPaths.FolderName);
		try
		{
			Directory.CreateDirectory(text);
		}
		catch
		{
		}
		return Path.Combine(text, "aicoding_budget_v2.json");
	}

	public static AICodingBudgetConfig Load()
	{
		try
		{
			string path = PathFor();
			if (File.Exists(path))
			{
				AICodingBudgetConfig aICodingBudgetConfig = new JavaScriptSerializer().Deserialize<AICodingBudgetConfig>(PersistenceHelper.ReadShared(path));
				if (aICodingBudgetConfig != null)
				{
					if (aICodingBudgetConfig.Budgets == null)
					{
						aICodingBudgetConfig.Budgets = new Dictionary<string, double>();
					}
					if (aICodingBudgetConfig.AlertsShown == null)
					{
						aICodingBudgetConfig.AlertsShown = new List<string>();
					}
					return aICodingBudgetConfig;
				}
			}
		}
		catch
		{
		}
		return new AICodingBudgetConfig();
	}

	public void Save()
	{
		try
		{
			PersistenceHelper.SafeWriteAllText(PathFor(), new JavaScriptSerializer().Serialize(this));
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(PathFor(), error);
		}
	}
}
