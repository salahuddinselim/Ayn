using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsBuddy.Tools.Units;

internal static class UnitCatalog
{
	public static readonly Dictionary<string, string> CurrencyNames;

	public static readonly UnitCategory[] All;

	private static Unit U(string s, string n, decimal f)
	{
		return new Unit(s, n, f);
	}

	static UnitCatalog()
	{
		CurrencyNames = new Dictionary<string, string>
		{
			{ "USD", "US Dollar" },
			{ "INR", "Indian Rupee" },
			{ "EUR", "Euro" },
			{ "GBP", "British Pound" },
			{ "JPY", "Japanese Yen" },
			{ "AED", "UAE Dirham" },
			{ "CAD", "Canadian Dollar" },
			{ "AUD", "Australian Dollar" },
			{ "SGD", "Singapore Dollar" },
			{ "CNY", "Chinese Yuan" }
		};
		All = new UnitCategory[8]
		{
			new UnitCategory("Length", "ruler", "m", "ft", new string[8] { "in", "cm", "yd", "mm", "km", "mi", "ft", "m" }, new List<Unit>
			{
				U("mm", "Millimeters", 0.001m),
				U("cm", "Centimeters", 0.01m),
				U("m", "Meters", 1m),
				U("km", "Kilometers", 1000m),
				U("in", "Inches", 0.0254m),
				U("ft", "Feet", 0.3048m),
				U("yd", "Yards", 0.9144m),
				U("mi", "Miles", 1609.344m),
				U("nmi", "Nautical miles", 1852m),
				U("µm", "Micrometers", 0.000001m)
			}),
			new UnitCategory("Mass", "weight", "kg", "lb", new string[6] { "g", "oz", "st", "lb", "kg", "t" }, new List<Unit>
			{
				U("mg", "Milligrams", 0.001m),
				U("g", "Grams", 1m),
				U("kg", "Kilograms", 1000m),
				U("t", "Tonnes", 1000000m),
				U("oz", "Ounces", 28.349523125m),
				U("lb", "Pounds", 453.59237m),
				U("st", "Stones", 6350.29318m)
			}),
			new UnitCategory("Temperature", "temperature", "°C", "°F", new string[3] { "K", "°F", "°C" }, new List<Unit>
			{
				U("°C", "Celsius", 1m),
				U("°F", "Fahrenheit", 1m),
				U("K", "Kelvin", 1m)
			}),
			new UnitCategory("Data", "database", "GB", "MB", new string[6] { "KB", "TB", "Mb", "B", "MB", "GB" }, new List<Unit>
			{
				U("B", "Bytes", 1m),
				U("KB", "Kilobytes", 1024m),
				U("MB", "Megabytes", 1048576m),
				U("GB", "Gigabytes", 1073741824m),
				U("TB", "Terabytes", 1099511627776m),
				U("bit", "Bits", 0.125m),
				U("Kb", "Kilobits", 128m),
				U("Mb", "Megabits", 131072m),
				U("Gb", "Gigabits", 134217728m)
			}),
			new UnitCategory("Volume", "beaker", "l", "gal", new string[5] { "ml", "cup", "fl oz", "l", "gal" }, new List<Unit>
			{
				U("ml", "Milliliters", 1m),
				U("l", "Liters", 1000m),
				U("m³", "Cubic meters", 1000000m),
				U("tsp", "Teaspoons", 4.92892159375m),
				U("tbsp", "Tablespoons", 14.78676478125m),
				U("cup", "Cups", 236.5882365m),
				U("fl oz", "Fluid ounces", 29.5735295625m),
				U("gal", "Gallons (US)", 3785.411784m)
			}),
			new UnitCategory("Speed", "gauge", "km/h", "mph", new string[5] { "m/s", "kn", "ft/s", "mph", "km/h" }, new List<Unit>
			{
				U("m/s", "Meters per second", 1m),
				U("km/h", "Kilometers per hour", 0.2777777777777777777777777778m),
				U("mph", "Miles per hour", 0.44704m),
				U("kn", "Knots", 0.5144444444444444444444444444m),
				U("ft/s", "Feet per second", 0.3048m)
			}),
			new UnitCategory("Time", "clock", "h", "min", new string[6] { "s", "day", "week", "ms", "min", "h" }, new List<Unit>
			{
				U("ms", "Milliseconds", 0.001m),
				U("s", "Seconds", 1m),
				U("min", "Minutes", 60m),
				U("h", "Hours", 3600m),
				U("day", "Days", 86400m),
				U("week", "Weeks", 604800m),
				U("month", "Months", 2630016m),
				U("year", "Years", 31557600m)
			}),
			new UnitCategory("Currency", "money", "USD", "INR", new string[6] { "EUR", "GBP", "JPY", "AED", "USD", "INR" }, new List<Unit>())
		};
		All[7].RefreshCurrencyUnits();
	}

	public static UnitCategory Find(string name)
	{
		return All.FirstOrDefault((UnitCategory c) => string.Equals(c.Name, name, StringComparison.Ordinal));
	}

	public static double Convert(decimal v, Unit a, Unit b, UnitCategory cat)
	{
		try
		{
			if (cat.IsTemperature)
			{
				double num = (double)v;
				double num2 = a.Symbol switch
				{
					"°F" => (num - 32.0) * 5.0 / 9.0, 
					"K" => num - 273.15, 
					_ => num, 
				};
				return b.Symbol switch
				{
					"°F" => num2 * 9.0 / 5.0 + 32.0, 
					"K" => num2 + 273.15, 
					_ => num2, 
				};
			}
			if (cat.IsCurrency)
			{
				if (!CurrencyRates.TryRate(a.Symbol, out var rate) || !CurrencyRates.TryRate(b.Symbol, out var rate2) || rate == 0m)
				{
					return double.NaN;
				}
				return (double)(v / rate * rate2);
			}
			return (double)(v * a.Factor / b.Factor);
		}
		catch (OverflowException)
		{
			if (cat.IsCurrency || cat.IsTemperature)
			{
				return double.NaN;
			}
			return (double)v * (double)a.Factor / (double)b.Factor;
		}
	}
}
