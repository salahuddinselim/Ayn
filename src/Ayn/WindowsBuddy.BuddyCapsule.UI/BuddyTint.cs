using System.Collections.Generic;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyTint
{
	private static readonly Dictionary<byte, SolidColorBrush> _cache = new Dictionary<byte, SolidColorBrush>();

	private static bool _hooked;

	public static SolidColorBrush Ink(byte alpha)
	{
		lock (_cache)
		{
			if (_cache.TryGetValue(alpha, out var value))
			{
				return value;
			}
			value = new SolidColorBrush(Tinted(alpha));
			_cache[alpha] = value;
			if (!_hooked)
			{
				_hooked = true;
				ThemeTokens.ThemeChanged += delegate
				{
					Refresh();
				};
			}
			return value;
		}
	}

	private static Color Tinted(byte a)
	{
		Color color = ThemeTokens.Target(ThemeTokens.Text);
		return Color.FromArgb(a, color.R, color.G, color.B);
	}

	private static void Refresh()
	{
		lock (_cache)
		{
			foreach (KeyValuePair<byte, SolidColorBrush> item in _cache)
			{
				item.Value.Color = Tinted(item.Key);
			}
		}
	}
}
