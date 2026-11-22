using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public static class BuddyToolIcons
{
	private static readonly Dictionary<string, BuddyToolDefinition> _definitions;

	static BuddyToolIcons()
	{
		_definitions = new Dictionary<string, BuddyToolDefinition>(StringComparer.OrdinalIgnoreCase);
		Register(new BuddyToolDefinition("home", "Home", "home", ToolCategory.Core, isPrimary: true));
		Register(new BuddyToolDefinition("system", "System", "system", ToolCategory.System, isPrimary: true));
		Register(new BuddyToolDefinition("clipboard", "Clipboard", "clipboard", ToolCategory.Productivity, isPrimary: true));
		Register(new BuddyToolDefinition("notes", "Notes", "notes", ToolCategory.Productivity, isPrimary: true));
		Register(new BuddyToolDefinition("timer", "Timers", "timer", ToolCategory.Utility, isPrimary: true));
		Register(new BuddyToolDefinition("shelf", "File Shelf", "shelf", ToolCategory.Productivity, isPrimary: true));
		Register(new BuddyToolDefinition("drop", "Ayn Drop", "drop", ToolCategory.Productivity, isPrimary: true));
		Register(new BuddyToolDefinition("calendar", "Calendar", "calendar", ToolCategory.Productivity, isPrimary: true));
		Register(new BuddyToolDefinition("clock", "Clock", "clock", ToolCategory.Core));
		Register(new BuddyToolDefinition("weather", "Weather", "weather", ToolCategory.Utility));
		Register(new BuddyToolDefinition("files", "File Converter", "files", ToolCategory.Productivity));
		Register(new BuddyToolDefinition("links", "Links", "links", ToolCategory.Productivity));
		Register(new BuddyToolDefinition("units", "Units", "units", ToolCategory.Utility));
		Register(new BuddyToolDefinition("message", "Message", "message", ToolCategory.Productivity));
		Register(new BuddyToolDefinition("screentime", "Screen Time", "screentime", ToolCategory.Utility));
		Register(new BuddyToolDefinition("analytics", "Analytics", "analytics", ToolCategory.System));
		Register(new BuddyToolDefinition("revenue", "Revenue", "revenue", ToolCategory.Utility));
		Register(new BuddyToolDefinition("aicoding", "AI Coding", "aicoding", ToolCategory.Productivity));
		Register(new BuddyToolDefinition("settings", "Settings", "settings", ToolCategory.Core));
		Register(new BuddyToolDefinition("notch_customization", "Notch Contour", "notch_customization", ToolCategory.Core));
		Register(new BuddyToolDefinition("emoji", "Emoji", "emoji", ToolCategory.Utility));
		Register(new BuddyToolDefinition("breaks", "Breaks & Meals", "breaks", ToolCategory.Utility));
		Register(new BuddyToolDefinition("salat", "Salat Times", "salat", ToolCategory.Utility));
		Register(new BuddyToolDefinition("guard", "Network Guard", "guard", ToolCategory.System));
		Register(new BuddyToolDefinition("qr", "QR & Wi-Fi Share", "qr", ToolCategory.Utility));
		Register(new BuddyToolDefinition("docs", "Doc Converter", "docs", ToolCategory.Productivity));
		Register(new BuddyToolDefinition("todo", "To-Do List", "todo", ToolCategory.Productivity));
	}

	private static void Register(BuddyToolDefinition def)
	{
		if (def != null && !string.IsNullOrEmpty(def.Id))
		{
			_definitions[def.Id] = def;
		}
	}

	public static BuddyToolDefinition GetDefinition(string toolId)
	{
		if (string.IsNullOrEmpty(toolId))
		{
			return null;
		}
		if (_definitions.TryGetValue(toolId, out var value))
		{
			return value;
		}
		return null;
	}

	public static IEnumerable<BuddyToolDefinition> GetAllDefinitions()
	{
		return _definitions.Values;
	}

	public static string FluentName(string id)
	{
		return (id ?? string.Empty).ToLowerInvariant() switch
		{
			"home" => "home", 
			"system" => "developer_board", 
			"clipboard" => "clipboard_text_ltr", 
			"notes" => "note", 
			"timer" => "timer", 
			"shelf" => "folder", 
			"drop" => "phone_arrow_up", 
			"calendar" => "calendar_ltr", 
			"clock" => "clock", 
			"weather" => "weather_partly_cloudy_day", 
			"files" => "arrow_swap", 
			"network" => "wifi_1", 
			"links" => "link", 
			"units" => "ruler", 
			"message" => "slide_text", 
			"screentime" => "data_bar_vertical", 
			"analytics" => "data_trending", 
			"revenue" => "money", 
			"aicoding" => "bot", 
			"breaks" => "timer", 
			"salat" => "calendar_ltr", 
			"guard" => "wifi_1", 
			"qr" => "developer_board", 
			"docs" => "folder", 
			"todo" => "clipboard_text_ltr", 
			"settings" => "settings", 
			"notch_customization" => "rectangle_landscape", 
			_ => null, 
		};
	}

	public static FrameworkElement CreateLineGlyph(string id, double size, Brush ink)
	{
		Geometry geometry = FluentIcons.GetGeometry(FluentName(id), filled: false);
		if (geometry == null)
		{
			return (FrameworkElement)Resolve(id, size, ink);
		}
		Path path = new Path();
		path.Data = geometry;
		path.Fill = ink;
		path.Stroke = ink;
		path.StrokeThickness = 0.5;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		Path path2 = path;
		if (Math.Abs(size - 20.0) > 0.0001)
		{
			ScaleTransform scaleTransform = new ScaleTransform(size / 20.0, size / 20.0);
			scaleTransform.Freeze();
			path2.RenderTransform = scaleTransform;
		}
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		canvas.IsHitTestVisible = false;
		canvas.SnapsToDevicePixels = true;
		Canvas canvas2 = canvas;
		canvas2.Children.Add(path2);
		return canvas2;
	}

	public static UIElement Resolve(string toolIdOrIconKey, double size, Brush stroke)
	{
		Brush stroke2 = stroke ?? BuddyTokens.Text60Brush;
		Canvas element;
		switch ((toolIdOrIconKey ?? string.Empty).Trim().ToLowerInvariant())
		{
		case "home":
			element = VectorIcons.CreateHomeOutlined(size, stroke2);
			break;
		case "clock":
		case "time":
			element = VectorIcons.CreateClockIcon(size, stroke2);
			break;
		case "battery":
			element = VectorIcons.CreateBatteryIcon(size, stroke2);
			break;
		case "wifi":
		case "network":
			element = VectorIcons.CreateWiFiIcon(size, stroke2);
			break;
		case "volume":
		case "audio":
			element = VectorIcons.CreateVolumeIcon(size, stroke2);
			break;
		case "settings":
			element = VectorIcons.CreateSettingsIcon(size, stroke2);
			break;
		case "clipboard":
			element = VectorIcons.CreateClipboardIcon(size, stroke2);
			break;
		case "notes":
		case "note":
			element = VectorIcons.CreateNotesIcon(size, stroke2);
			break;
		case "scratchpad":
		case "edit":
			element = VectorIcons.CreateScratchpadIcon(size, stroke2);
			break;
		case "calendar":
			element = VectorIcons.CreateCalendarIcon(size, stroke2);
			break;
		case "weather":
			element = VectorIcons.CreateWeatherIcon(size, stroke2);
			break;
		case "timer":
		case "timers":
		case "stopwatch":
			element = VectorIcons.CreateTimerIcon(size, stroke2);
			break;
		case "stats":
			element = VectorIcons.CreateStatsIcon(size, stroke2);
			break;
		case "screentime":
			element = VectorIcons.CreateScreenTimeIcon(size, stroke2);
			break;
		case "files":
			element = VectorIcons.CreateFilesIcon(size, stroke2);
			break;
		case "shelf":
		case "fileshelf":
			element = VectorIcons.CreateFileShelfIcon(size, stroke2);
			break;
		case "links":
			element = VectorIcons.CreateLinksIcon(size, stroke2);
			break;
		case "message":
			element = VectorIcons.CreateMessageIcon(size, stroke2);
			break;
		case "sounds":
			element = VectorIcons.CreateSoundsIcon(size, stroke2);
			break;
		case "emoji":
			element = VectorIcons.CreateEmojiIcon(size, stroke2);
			break;
		case "units":
			element = VectorIcons.CreateUnitsIcon(size, stroke2);
			break;
		case "analytics":
			element = VectorIcons.CreateAnalyticsIcon(size, stroke2);
			break;
		case "revenue":
			element = VectorIcons.CreateRevenueIcon(size, stroke2);
			break;
		case "aicoding":
			element = VectorIcons.CreateCodeIcon(size, stroke2);
			break;
		case "claude":
			element = VectorIcons.CreateClaudeIcon(size, stroke2);
			break;
		case "notch_customization":
		case "contour":
			element = VectorIcons.CreateStyleContourIcon(size, stroke2);
			break;
		case "system":
			element = VectorIcons.CreateSlidersIcon(size, stroke2);
			break;
		case "focus":
			element = VectorIcons.CreateFocusIcon(size, stroke2);
			break;
		case "more":
		case "plus":
			element = VectorIcons.CreatePlusOutlined(size, stroke2);
			break;
		case "close":
			element = VectorIcons.CreateCloseIcon(size, stroke2);
			break;
		default:
			element = VectorIcons.CreateFileGenericIcon(size, stroke2);
			break;
		}
		Grid grid = new Grid();
		grid.Width = size;
		grid.Height = size;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.IsHitTestVisible = false;
		Grid grid2 = grid;
		grid2.Children.Add(element);
		return grid2;
	}
}
