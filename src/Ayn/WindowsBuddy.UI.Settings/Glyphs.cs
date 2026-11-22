using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Renderer;

namespace WindowsBuddy.UI.Settings;

internal static class Glyphs
{
	private static readonly Dictionary<string, string> Strokes = new Dictionary<string, string>
	{
		{ "grid", "M3.5,3.5 H8.5 V8.5 H3.5 Z M11.5,3.5 H16.5 V8.5 H11.5 Z M3.5,11.5 H8.5 V16.5 H3.5 Z M11.5,11.5 H16.5 V16.5 H11.5 Z" },
		{ "shield", "M10,2.6 L16,5 V9.6 C16,13.4 13.5,16.3 10,17.4 C6.5,16.3 4,13.4 4,9.6 V5 Z" },
		{ "key", "M7,7 A3.2,3.2 0 1 1 7,13.4 A3.2,3.2 0 1 1 7,7 Z M10.2,10.2 H17 M14.4,10.2 V12.8 M16.4,10.2 V12.2" },
		{ "info", "M10,2.7 A7.3,7.3 0 1 1 10,17.3 A7.3,7.3 0 1 1 10,2.7 Z M10,9 V14" },
		{ "bell", "M10,3 C7.3,3 5.6,5 5.6,7.8 V11 L4.2,13.6 H15.8 L14.4,11 V7.8 C14.4,5 12.7,3 10,3 Z M8.3,15.4 C8.6,16.4 9.2,17 10,17 C10.8,17 11.4,16.4 11.7,15.4" },
		{ "notch", "M2.5,5 H17.5 V15 H2.5 Z" },
		{ "contrast", "M10,2.7 A7.3,7.3 0 1 1 10,17.3 A7.3,7.3 0 1 1 10,2.7 Z" },
		{ "sync", "M15.6,7.6 A6,6 0 0 0 4.6,8 M4.4,12.4 A6,6 0 0 0 15.4,12 M15.8,4.6 V7.8 H12.6 M4.2,15.4 V12.2 H7.4" },
		{ "chevron_right", "M7.5,4.5 L13,10 L7.5,15.5" },
		{ "chevron_left", "M12.5,4.5 L7,10 L12.5,15.5" },
		{ "chevron_down", "M5,7.5 L10,12.5 L15,7.5" },
		{ "min", "M4.5,10 H15.5" },
		{ "max", "M5,5 H15 V15 H5 Z" },
		{ "restore", "M5,7.5 H12.5 V15 H5 Z M7.5,5 H15 V12.5" },
		{ "close", "M5,5 L15,15 M15,5 L5,15" },
		{ "search", "M8.6,3.4 A5.2,5.2 0 1 1 8.6,13.8 A5.2,5.2 0 1 1 8.6,3.4 Z M12.4,12.4 L16.6,16.6" },
		{ "check", "M4.5,10.5 L8.3,14.2 L15.5,6.2" },
		{ "external", "M11.5,3.5 H16.5 V8.5 M16.5,3.5 L9.5,10.5 M14.5,11.5 V16.5 H3.5 V5.5 H8.5" },
		{ "folder_open", "M2.8,5 H7.6 L9.2,6.6 H17.2 V15.4 H2.8 Z" },
		{ "trash", "M4,5.5 H16 M8,5.5 V3.8 H12 V5.5 M5.6,5.5 L6.4,16.2 H13.6 L14.4,5.5" },
		{ "keyboard", "M2.6,5.5 H17.4 V14.5 H2.6 Z M5.5,8.5 H6 M8.5,8.5 H9 M11,8.5 H11.5 M14,8.5 H14.5 M6.5,11.5 H13.5" },
		{ "location", "M10,17.2 C10,17.2 4.6,11.8 4.6,8.2 A5.4,5.4 0 0 1 15.4,8.2 C15.4,11.8 10,17.2 10,17.2 Z M10,6.3 A1.9,1.9 0 1 1 10,10.1 A1.9,1.9 0 1 1 10,6.3 Z" },
		{ "globe2", "M10,2.7 A7.3,7.3 0 1 1 10,17.3 A7.3,7.3 0 1 1 10,2.7 Z M2.7,10 H17.3 M10,2.7 C7.6,5 7.6,15 10,17.3 M10,2.7 C12.4,5 12.4,15 10,17.3" },
		{ "mail", "M2.8,5 H17.2 V15 H2.8 Z M2.8,5.4 L10,10.8 L17.2,5.4" },
		{ "doc", "M5,2.8 H11.6 L15,6.2 V17.2 H5 Z M11.4,2.8 V6.4 H15 M7.6,10 H12.4 M7.6,13 H12.4" },
		{ "store", "M3.5,7 H16.5 V16.5 H3.5 Z M7,7 V5.5 A3,3 0 0 1 13,5.5 V7" },
		{ "help", "M10,2.7 A7.3,7.3 0 1 1 10,17.3 A7.3,7.3 0 1 1 10,2.7 Z M7.8,7.8 C7.8,6.6 8.8,5.6 10,5.6 C11.2,5.6 12.2,6.6 12.2,7.8 C12.2,8.8 11.2,9.4 10.5,10 C10,10.4 10,10.8 10,11.5 M10,14.2 H10.01" },
		{ "chat", "M3.5,4.5 H16.5 C17.1,4.5 17.5,4.9 17.5,5.5 V13.5 C17.5,14.1 17.1,14.5 16.5,14.5 H7.5 L3.5,17.5 V5.5 C3.5,4.9 3.9,4.5 4.5,4.5" }
	};

	private static readonly Dictionary<string, string> Fills = new Dictionary<string, string>
	{
		{ "info", "M10,5.6 A1,1 0 1 1 10,7.6 A1,1 0 1 1 10,5.6 Z" },
		{ "notch", "M7,5 H13 V6.6 A1.6,1.6 0 0 1 11.4,8.2 H8.6 A1.6,1.6 0 0 1 7,6.6 Z" },
		{ "contrast", "M10,2.7 A7.3,7.3 0 0 1 10,17.3 Z" },
		{ "drag", "M7.2,4.2 A1.3,1.3 0 1 1 7.2,6.8 A1.3,1.3 0 1 1 7.2,4.2 Z M12.8,4.2 A1.3,1.3 0 1 1 12.8,6.8 A1.3,1.3 0 1 1 12.8,4.2 Z M7.2,8.7 A1.3,1.3 0 1 1 7.2,11.3 A1.3,1.3 0 1 1 7.2,8.7 Z M12.8,8.7 A1.3,1.3 0 1 1 12.8,11.3 A1.3,1.3 0 1 1 12.8,8.7 Z M7.2,13.2 A1.3,1.3 0 1 1 7.2,15.8 A1.3,1.3 0 1 1 7.2,13.2 Z M12.8,13.2 A1.3,1.3 0 1 1 12.8,15.8 A1.3,1.3 0 1 1 12.8,13.2 Z" }
	};

	private static readonly Dictionary<string, string[]> Solid = new Dictionary<string, string[]>
	{
		{
			"settings_f",
			new string[1] { "F0 M8,2 H12 L12.5,4 L14,4.8 L16,4.2 L18,7.6 L16.5,9 V11 L18,12.4 L16,15.8 L14,15.2 L12.5,16 L12,18 H8 L7.5,16 L6,15.2 L4,15.8 L2,12.4 L3.5,11 V9 L2,7.6 L4,4.2 L6,4.8 L7.5,4 Z M10,6.5 A3.5,3.5 0 1 1 10,13.5 A3.5,3.5 0 1 1 10,6.5 Z" }
		},
		{
			"notch_f",
			new string[1] { "F0 M5,4 H15 A2.5,2.5 0 0 1 17.5,6.5 V13.5 A2.5,2.5 0 0 1 15,16 H5 A2.5,2.5 0 0 1 2.5,13.5 V6.5 A2.5,2.5 0 0 1 5,4 Z M5.5,8 H14.5 A1,1 0 0 1 15.5,9 V13 A1,1 0 0 1 14.5,14 H5.5 A1,1 0 0 1 4.5,13 V9 A1,1 0 0 1 5.5,8 Z" }
		},
		{
			"contrast_f",
			new string[1] { "F0 M10,2.5 A7.5,7.5 0 1 1 10,17.5 A7.5,7.5 0 1 1 10,2.5 Z M10,4.2 A5.8,5.8 0 0 1 10,15.8 Z" }
		},
		{
			"grid_f",
			new string[1] { "F1 M4.5,3 H7.5 A1.5,1.5 0 0 1 9,4.5 V7.5 A1.5,1.5 0 0 1 7.5,9 H4.5 A1.5,1.5 0 0 1 3,7.5 V4.5 A1.5,1.5 0 0 1 4.5,3 Z M12.5,3 H15.5 A1.5,1.5 0 0 1 17,4.5 V7.5 A1.5,1.5 0 0 1 15.5,9 H12.5 A1.5,1.5 0 0 1 11,7.5 V4.5 A1.5,1.5 0 0 1 12.5,3 Z M4.5,11 H7.5 A1.5,1.5 0 0 1 9,12.5 V15.5 A1.5,1.5 0 0 1 7.5,17 H4.5 A1.5,1.5 0 0 1 3,15.5 V12.5 A1.5,1.5 0 0 1 4.5,11 Z M12.5,11 H15.5 A1.5,1.5 0 0 1 17,12.5 V15.5 A1.5,1.5 0 0 1 15.5,17 H12.5 A1.5,1.5 0 0 1 11,15.5 V12.5 A1.5,1.5 0 0 1 12.5,11 Z" }
		},
		{
			"bell_f",
			new string[1] { "F1 M10,2.5 C7,2.5 5,4.8 5,7.8 V11 L3.6,13.6 C3.4,14 3.7,14.5 4.1,14.5 H15.9 C16.3,14.5 16.6,14 16.4,13.6 L15,11 V7.8 C15,4.8 13,2.5 10,2.5 Z M7.8,15.6 C8.1,16.8 9,17.5 10,17.5 C11,17.5 11.9,16.8 12.2,15.6 Z" }
		},
		{
			"hand_f",
			new string[1] { "F1 M5.4,7 A1.05,1.05 0 0 1 7.5,7 V11 H5.4 Z M7.85,4.5 A1.05,1.05 0 0 1 9.95,4.5 V11 H7.85 Z M10.3,4 A1.05,1.05 0 0 1 12.4,4 V11 H10.3 Z M12.75,5.5 A1.05,1.05 0 0 1 14.85,5.5 V11.5 H12.75 Z M5.4,10 H14.85 V13.5 C14.85,15.9 13,17.5 10.6,17.5 H9.6 C7.3,17.5 5.4,15.8 5.4,13.5 Z M14.6,12.2 L16.1,9.8 A1.05,1.05 0 0 1 17.9,10.9 L15.6,15 Z" }
		},
		{
			"key_f",
			new string[2] { "F0 M6.5,6 A4,4 0 1 1 6.5,14 A4,4 0 1 1 6.5,6 Z M6.5,8.4 A1.6,1.6 0 1 0 6.5,11.6 A1.6,1.6 0 1 0 6.5,8.4 Z", "F1 M10.2,8.9 H17.5 V11.1 H16.7 V13.3 H14.7 V11.1 H13.7 V12.7 H11.9 V11.1 H10.2 Z" }
		},
		{
			"info_f",
			new string[1] { "F0 M10,2.5 A7.5,7.5 0 1 1 10,17.5 A7.5,7.5 0 1 1 10,2.5 Z M9.1,8.8 H10.9 V14.5 H9.1 Z M10,5.4 A1.1,1.1 0 1 1 10,7.6 A1.1,1.1 0 1 1 10,5.4 Z" }
		}
	};

	private static readonly Dictionary<string, Geometry> _cache = new Dictionary<string, Geometry>();

	private static Geometry Parse(string key, string data)
	{
		if (_cache.TryGetValue(key, out var value))
		{
			return value;
		}
		value = Geometry.Parse(data);
		value.Freeze();
		_cache[key] = value;
		return value;
	}

	public static bool Has(string name)
	{
		if (!Strokes.ContainsKey(name) && !Fills.ContainsKey(name))
		{
			return Solid.ContainsKey(name);
		}
		return true;
	}

	public static FrameworkElement Create(string name, double size, Brush brush)
	{
		if (!Has(name))
		{
			bool flag = name?.EndsWith("_f") ?? false;
			return FluentIcons.Create(flag ? name.Substring(0, name.Length - 2) : name, size, brush, flag);
		}
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		canvas.IsHitTestVisible = false;
		canvas.SnapsToDevicePixels = true;
		Canvas canvas2 = canvas;
		double num = size / 20.0;
		ScaleTransform scaleTransform = new ScaleTransform(num, num);
		scaleTransform.Freeze();
		if (Solid.TryGetValue(name, out var value))
		{
			for (int i = 0; i < value.Length; i++)
			{
				canvas2.Children.Add(new Path
				{
					Data = Parse("x:" + name + i, value[i]),
					Fill = brush,
					RenderTransform = scaleTransform
				});
			}
			return canvas2;
		}
		if (Strokes.TryGetValue(name, out var value2))
		{
			canvas2.Children.Add(new Path
			{
				Data = Parse("s:" + name, value2),
				Stroke = brush,
				StrokeThickness = 1.45,
				StrokeLineJoin = PenLineJoin.Round,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round,
				RenderTransform = scaleTransform
			});
		}
		if (Fills.TryGetValue(name, out value2))
		{
			canvas2.Children.Add(new Path
			{
				Data = Parse("f:" + name, value2),
				Fill = brush,
				RenderTransform = scaleTransform
			});
		}
		return canvas2;
	}
}
