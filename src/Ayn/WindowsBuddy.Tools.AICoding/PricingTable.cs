using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.AICoding;

public class PricingTable
{
	private Dictionary<string, ModelPrice> _index;

	private static readonly Regex DateSuffix = new Regex("(-\\d{8}|-\\d{4}-\\d{2}-\\d{2}|@\\d{8}|-latest)$", RegexOptions.Compiled);

	public List<ModelPrice> Models { get; set; }

	public PricingTable()
	{
		Models = new List<ModelPrice>();
	}

	public static PricingTable CreateDefault()
	{
		PricingTable pricingTable = new PricingTable();
		List<ModelPrice> models = pricingTable.Models;
		models.Add(new ModelPrice("claude-fable-5-1", 10.0, 50.0, 0.25));
		models.Add(new ModelPrice("claude-mythos-5-1", 10.0, 50.0, 0.25));
		models.Add(new ModelPrice("claude-fable-5", 10.0, 50.0, 1.0));
		models.Add(new ModelPrice("claude-mythos-5", 10.0, 50.0, 1.0));
		models.Add(new ModelPrice("claude-opus-5-5", 4.0, 20.0, 0.2));
		models.Add(new ModelPrice("claude-opus-5", 5.0, 25.0, 0.5));
		models.Add(new ModelPrice("claude-opus-4-8", 5.0, 25.0, 0.5));
		models.Add(new ModelPrice("claude-opus-4-7", 5.0, 25.0, 0.5));
		models.Add(new ModelPrice("claude-opus-4-6", 5.0, 25.0, 0.5));
		models.Add(new ModelPrice("claude-opus-4-5", 5.0, 25.0, 0.5));
		models.Add(new ModelPrice("claude-opus-4-1", 15.0, 75.0, 1.5));
		models.Add(new ModelPrice("claude-opus-4", 15.0, 75.0, 1.5));
		models.Add(new ModelPrice("claude-opus-4-0", 15.0, 75.0, 1.5));
		models.Add(new ModelPrice("claude-3-opus", 15.0, 75.0, 1.5));
		models.Add(new ModelPrice("claude-sonnet-5-5", 2.0, 10.0, 0.2));
		models.Add(new ModelPrice("claude-sonnet-5", 2.0, 10.0, 0.2));
		models.Add(new ModelPrice("claude-sonnet-4-6", 3.0, 15.0, 0.3));
		models.Add(new ModelPrice("claude-sonnet-4-5", 3.0, 15.0, 0.3));
		models.Add(new ModelPrice("claude-sonnet-4", 3.0, 15.0, 0.3));
		models.Add(new ModelPrice("claude-sonnet-4-0", 3.0, 15.0, 0.3));
		models.Add(new ModelPrice("claude-3-7-sonnet", 3.0, 15.0, 0.3));
		models.Add(new ModelPrice("claude-3-5-sonnet", 3.0, 15.0, 0.3));
		models.Add(new ModelPrice("claude-haiku-4-5", 1.0, 5.0, 0.1));
		models.Add(new ModelPrice("claude-3-5-haiku", 0.8, 4.0, 0.08));
		models.Add(new ModelPrice("claude-3-haiku", 0.25, 1.25, 0.03, 0.3, 0.5));
		models.Add(new ModelPrice("gpt-5", 1.25, 10.0, 0.125, 0.0, 0.0));
		models.Add(new ModelPrice("gpt-5-codex", 1.25, 10.0, 0.125, 0.0, 0.0));
		models.Add(new ModelPrice("gpt-5-mini", 0.25, 2.0, 0.025, 0.0, 0.0));
		models.Add(new ModelPrice("gpt-5-nano", 0.05, 0.4, 0.005, 0.0, 0.0));
		models.Add(new ModelPrice("codex-mini", 1.5, 6.0, 0.375, 0.0, 0.0));
		models.Add(new ModelPrice("o3", 2.0, 8.0, 0.5, 0.0, 0.0));
		models.Add(new ModelPrice("o4-mini", 1.1, 4.4, 0.275, 0.0, 0.0));
		models.Add(new ModelPrice("gpt-4.1", 2.0, 8.0, 0.5, 0.0, 0.0));
		models.Add(new ModelPrice("gpt-4o", 2.5, 10.0, 1.25, 0.0, 0.0));
		return pricingTable;
	}

	public static string Normalize(string model)
	{
		if (string.IsNullOrEmpty(model))
		{
			return string.Empty;
		}
		string text = model.Trim().ToLowerInvariant();
		int num = text.LastIndexOf('/');
		if (num >= 0)
		{
			text = text.Substring(num + 1);
		}
		if (text.StartsWith("anthropic."))
		{
			text = text.Substring("anthropic.".Length);
		}
		if (text.EndsWith("[1m]"))
		{
			text = text.Substring(0, text.Length - 4);
		}
		return DateSuffix.Replace(text, "");
	}

	public ModelPrice Find(string model)
	{
		if (_index == null)
		{
			Dictionary<string, ModelPrice> dictionary = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase);
			foreach (ModelPrice model2 in Models)
			{
				if (model2 != null && !string.IsNullOrEmpty(model2.Model))
				{
					dictionary[Normalize(model2.Model)] = model2;
				}
			}
			_index = dictionary;
		}
		if (!_index.TryGetValue(Normalize(model), out var value))
		{
			return null;
		}
		return value;
	}

	public static string OverridePath()
	{
		string path = Path.Combine(AppPaths.RoamingAppData, AppPaths.FolderName);
		return Path.Combine(path, "aicoding_prices.json");
	}

	public static PricingTable Load()
	{
		PricingTable pricingTable = CreateDefault();
		try
		{
			string path = OverridePath();
			if (File.Exists(path))
			{
				PricingTable pricingTable2 = new JavaScriptSerializer().Deserialize<PricingTable>(PersistenceHelper.ReadShared(path));
				if (pricingTable2 != null && pricingTable2.Models != null)
				{
					foreach (ModelPrice p in pricingTable2.Models)
					{
						if (p != null && !string.IsNullOrEmpty(p.Model))
						{
							pricingTable.Models.RemoveAll((ModelPrice x) => Normalize(x.Model) == Normalize(p.Model));
							pricingTable.Models.Add(p);
						}
					}
				}
			}
		}
		catch
		{
		}
		return pricingTable;
	}
}
