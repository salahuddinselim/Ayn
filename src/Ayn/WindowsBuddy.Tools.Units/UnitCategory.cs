using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsBuddy.Tools.Units;

internal sealed class UnitCategory
{
	public readonly string Name;

	public readonly string Icon;

	public readonly string DefaultFrom;

	public readonly string DefaultTo;

	public readonly string[] Common;

	public List<Unit> Units;

	public bool IsTemperature => Name == "Temperature";

	public bool IsCurrency => Name == "Currency";

	public UnitCategory(string name, string icon, string defFrom, string defTo, string[] common, List<Unit> units)
	{
		Name = name;
		Icon = icon;
		DefaultFrom = defFrom;
		DefaultTo = defTo;
		Common = common;
		Units = units;
	}

	public Unit Find(string symbol)
	{
		return Units.FirstOrDefault((Unit u) => string.Equals(u.Symbol, symbol, StringComparison.Ordinal));
	}

	public void RefreshCurrencyUnits()
	{
		if (!IsCurrency)
		{
			return;
		}
		List<Unit> list = new List<Unit>();
		foreach (KeyValuePair<string, string> currencyName in UnitCatalog.CurrencyNames)
		{
			list.Add(new Unit(currencyName.Key, currencyName.Value, 1m));
		}
		foreach (string code in CurrencyRates.Codes)
		{
			if (!UnitCatalog.CurrencyNames.ContainsKey(code))
			{
				list.Add(new Unit(code, code, 1m));
			}
		}
		Units = list;
	}
}
