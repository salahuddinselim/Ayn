using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace WindowsBuddy.Tools.Analytics;

internal static class J
{
	public static object Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			javaScriptSerializer.MaxJsonLength = int.MaxValue;
			return javaScriptSerializer.DeserializeObject(json);
		}
		catch
		{
			return null;
		}
	}

	public static string Write(object o)
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		return javaScriptSerializer.Serialize(o);
	}

	public static object Get(object o, string key)
	{
		if (!(o is Dictionary<string, object> dictionary) || !dictionary.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public static object Get(object o, params string[] path)
	{
		foreach (string key in path)
		{
			o = Get(o, key);
		}
		return o;
	}

	public static object[] Arr(object o)
	{
		if (o is object[] result)
		{
			return result;
		}
		if (!(o is ArrayList arrayList))
		{
			return new object[0];
		}
		return arrayList.ToArray();
	}

	public static double? Num(object o)
	{
		if (o == null)
		{
			return null;
		}
		if (o is int)
		{
			return (int)o;
		}
		if (o is long)
		{
			return (long)o;
		}
		if (o is decimal)
		{
			return (double)(decimal)o;
		}
		if (o is double)
		{
			return (double)o;
		}
		if (o is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return null;
	}

	public static string Str(object o)
	{
		if (o != null)
		{
			return Convert.ToString(o, CultureInfo.InvariantCulture);
		}
		return null;
	}
}
