using System;
using System.Windows;
using WindowsBuddy.Core;

namespace WindowsBuddy.UI.Settings;

internal static class SSettingBinding
{
	private sealed class Structure
	{
		internal Func<string> Read;

		internal string Value;
	}

	private static readonly DependencyProperty RefreshProperty = DependencyProperty.RegisterAttached("SettingsRefresh", typeof(Action), typeof(SSettingBinding));

	private static readonly DependencyProperty StructureProperty = DependencyProperty.RegisterAttached("SettingsStructure", typeof(Structure), typeof(SSettingBinding));

	internal static void Attach(FrameworkElement element, Action refresh)
	{
		element.SetValue(RefreshProperty, (Action)Delegate.Combine(element.GetValue(RefreshProperty) as Action, refresh));
	}

	internal static void WatchStructure(FrameworkElement element, Func<string> read)
	{
		element.SetValue(StructureProperty, new Structure
		{
			Read = read,
			Value = read()
		});
	}

	internal static bool StructureChanged(DependencyObject root)
	{
		if (root == null)
		{
			return false;
		}
		if (root.GetValue(StructureProperty) is Structure structure && structure.Value != structure.Read())
		{
			return true;
		}
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is DependencyObject && StructureChanged((DependencyObject)child))
			{
				return true;
			}
		}
		return false;
	}

	internal static void RefreshTree(DependencyObject root)
	{
		if (root == null)
		{
			return;
		}
		if (root.GetValue(RefreshProperty) is Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Settings state refresh: " + ex.Message);
			}
		}
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is DependencyObject)
			{
				RefreshTree((DependencyObject)child);
			}
		}
	}
}
