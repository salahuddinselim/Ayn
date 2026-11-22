using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Tools.Drop;

public static class DropFirewall
{
	public static bool HelperBlocked(string helperPath)
	{
		try
		{
			return Blocks(ReadRules(), helperPath);
		}
		catch
		{
			return false;
		}
	}

	public static bool HelperHasRule(string helperPath)
	{
		try
		{
			string text = Normalize(helperPath);
			foreach (DropFirewallRule item in ReadRules())
			{
				if (item.Inbound && item.Enabled && ((uint)item.Profiles & 2u) != 0 && Normalize(item.Application) == text)
				{
					return true;
				}
			}
			return false;
		}
		catch
		{
			return true;
		}
	}

	public static bool Blocks(IEnumerable<DropFirewallRule> rules, string helperPath)
	{
		string text = Normalize(helperPath);
		foreach (DropFirewallRule rule in rules)
		{
			if (rule.Inbound && rule.Block && rule.Enabled && ((uint)rule.Profiles & 2u) != 0 && Normalize(rule.Application) == text)
			{
				return true;
			}
		}
		return false;
	}

	private static string Normalize(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return "";
		}
		try
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)).TrimEnd('\\').ToUpperInvariant();
		}
		catch
		{
			return path.ToUpperInvariant();
		}
	}

	private static object Get(object com, string property)
	{
		return com.GetType().InvokeMember(property, BindingFlags.GetProperty, null, com, null);
	}

	private static List<DropFirewallRule> ReadRules()
	{
		List<DropFirewallRule> list = new List<DropFirewallRule>();
		object obj = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
		object obj2 = null;
		try
		{
			obj2 = Get(obj, "Rules");
			foreach (object item in (IEnumerable)obj2)
			{
				try
				{
					string text = Get(item, "ApplicationName") as string;
					if (!string.IsNullOrEmpty(text) && text.IndexOf("Ayn", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list.Add(new DropFirewallRule
						{
							Application = text,
							Inbound = (Convert.ToInt32(Get(item, "Direction")) == 1),
							Block = (Convert.ToInt32(Get(item, "Action")) == 0),
							Enabled = Convert.ToBoolean(Get(item, "Enabled")),
							Profiles = Convert.ToInt32(Get(item, "Profiles"))
						});
					}
				}
				finally
				{
					if (item != null && Marshal.IsComObject(item))
					{
						Marshal.ReleaseComObject(item);
					}
				}
			}
			return list;
		}
		finally
		{
			if (obj2 != null && Marshal.IsComObject(obj2))
			{
				Marshal.ReleaseComObject(obj2);
			}
			Marshal.ReleaseComObject(obj);
		}
	}
}
