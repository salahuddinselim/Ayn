using System;

namespace WindowsBuddy.UI.Settings;

internal static class FluentExtensions
{
	public static T With<T>(this T obj, Action<T> a)
	{
		a(obj);
		return obj;
	}
}
