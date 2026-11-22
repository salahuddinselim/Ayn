using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Media;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Theme;

public class NotchConfig
{
	public const double NotchGeometryIslandGap = 5.0;

	private static volatile NotchConfig _instance;

	private static readonly object InstanceLock = new object();

	private NotchConfigModel _model;

	private readonly string _filePath;

	public static readonly KeyValuePair<string, Color>[] MusicPresets = new KeyValuePair<string, Color>[7]
	{
		new KeyValuePair<string, Color>("Red", Color.FromRgb(byte.MaxValue, 69, 58)),
		new KeyValuePair<string, Color>("Pink", Color.FromRgb(byte.MaxValue, 55, 95)),
		new KeyValuePair<string, Color>("Purple", Color.FromRgb(191, 90, 242)),
		new KeyValuePair<string, Color>("Blue", Color.FromRgb(10, 132, byte.MaxValue)),
		new KeyValuePair<string, Color>("Teal", Color.FromRgb(100, 210, byte.MaxValue)),
		new KeyValuePair<string, Color>("Green", Color.FromRgb(48, 209, 88)),
		new KeyValuePair<string, Color>("Yellow", Color.FromRgb(byte.MaxValue, 214, 10))
	};

	private readonly object _saveLock = new object();

	private Timer _saveDebounceTimer;

	private bool _isDirty;

	private string _pendingJson;

	public static NotchConfig Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (InstanceLock)
				{
					if (_instance == null)
					{
						_instance = new NotchConfig();
					}
				}
			}
			return _instance;
		}
	}

	public string Style
	{
		get
		{
			return _model.Style;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.Style != value)
				{
					_model.Style = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string Size
	{
		get
		{
			return _model.Size;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.Size != value)
				{
					_model.Size = value;
					SaveAndNotify();
				}
			}
		}
	}

	public double SizeScale
	{
		get
		{
			if (_model.SizeScale > 0.0)
			{
				return _model.SizeScale;
			}
			if (_model.Size == "Small")
			{
				return 0.9;
			}
			if (_model.Size == "Large")
			{
				return 1.1;
			}
			return 1.0;
		}
		set
		{
			lock (_saveLock)
			{
				double num = Math.Max(0.9, Math.Min(1.2, Math.Round(value, 2)));
				if (Math.Abs(_model.SizeScale - num) > 0.001)
				{
					_model.SizeScale = num;
					SaveAndNotify();
				}
			}
		}
	}

	public string AppTheme
	{
		get
		{
			return ThemeTokens.NormalizeId(_model.AppTheme);
		}
		set
		{
			lock (_saveLock)
			{
				string text = ThemeTokens.NormalizeId(value);
				if (_model.AppTheme != text)
				{
					_model.AppTheme = text;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowDateInNotch
	{
		get
		{
			return _model.ShowDateInNotch;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowDateInNotch != value)
				{
					_model.ShowDateInNotch = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string StyleFamily
	{
		get
		{
			if (string.Equals(_model.Style, "Line", StringComparison.OrdinalIgnoreCase))
			{
				return "Line";
			}
			if (string.Equals(_model.Style, "Island", StringComparison.OrdinalIgnoreCase))
			{
				return "Island";
			}
			return "Notch";
		}
	}

	public string BackgroundMode
	{
		get
		{
			return _model.BackgroundMode;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.BackgroundMode != value)
				{
					_model.BackgroundMode = value;
					SaveAndNotify();
				}
			}
		}
	}

	public double ClosedNotchOpacity
	{
		get
		{
			return _model.ClosedNotchOpacity;
		}
		set
		{
			lock (_saveLock)
			{
				double num = NotchConfigModel.SafeNotchOpacity(value, 0.4);
				if (_model.ClosedNotchOpacity != num)
				{
					_model.ClosedNotchOpacity = num;
					SaveAndNotify();
				}
			}
		}
	}

	public double Opacity
	{
		get
		{
			return _model.Opacity;
		}
		set
		{
			lock (_saveLock)
			{
				if (Math.Abs(_model.Opacity - value) > 0.001)
				{
					_model.Opacity = Math.Max(0.2, Math.Min(1.0, value));
					SaveAndNotify();
				}
			}
		}
	}

	public string BlurEffect
	{
		get
		{
			return _model.BlurEffect;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.BlurEffect != value)
				{
					_model.BlurEffect = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string ShadowStrength
	{
		get
		{
			return _model.ShadowStrength;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShadowStrength != value)
				{
					_model.ShadowStrength = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string CornerRadiusMode
	{
		get
		{
			return _model.CornerRadiusMode;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.CornerRadiusMode != value)
				{
					_model.CornerRadiusMode = value;
					SaveAndNotify();
				}
			}
		}
	}

	public double CustomCornerRadius
	{
		get
		{
			return _model.CustomCornerRadius;
		}
		set
		{
			lock (_saveLock)
			{
				if (Math.Abs(_model.CustomCornerRadius - value) > 0.01)
				{
					_model.CustomCornerRadius = value;
					SaveAndNotify();
				}
			}
		}
	}

	public double CornerRadius
	{
		get
		{
			if (string.Equals(CornerRadiusMode, "Custom", StringComparison.OrdinalIgnoreCase))
			{
				return CustomCornerRadius;
			}
			return -1.0;
		}
		set
		{
			lock (_saveLock)
			{
				CornerRadiusMode = "Custom";
				CustomCornerRadius = Math.Max(0.0, Math.Min(32.0, value));
			}
		}
	}

	public string ThemeAccent
	{
		get
		{
			return _model.ThemeAccent;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ThemeAccent != value)
				{
					_model.ThemeAccent = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowTime
	{
		get
		{
			return _model.ShowTime;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowTime != value)
				{
					_model.ShowTime = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool NotchAnalogClock
	{
		get
		{
			return _model.NotchAnalogClock;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.NotchAnalogClock != value)
				{
					_model.NotchAnalogClock = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowBattery
	{
		get
		{
			return _model.ShowBattery;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowBattery != value)
				{
					_model.ShowBattery = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowBluetooth
	{
		get
		{
			return _model.ShowBluetooth;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowBluetooth != value)
				{
					_model.ShowBluetooth = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowNowPlaying
	{
		get
		{
			return _model.ShowNowPlaying;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowNowPlaying != value)
				{
					_model.ShowNowPlaying = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowNotifications
	{
		get
		{
			return _model.ShowNotifications;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowNotifications != value)
				{
					_model.ShowNotifications = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowClipboardQuickAccess
	{
		get
		{
			return _model.ShowClipboardQuickAccess;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowClipboardQuickAccess != value)
				{
					_model.ShowClipboardQuickAccess = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowClipboardToast
	{
		get
		{
			return _model.ShowClipboardToast;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowClipboardToast != value)
				{
					_model.ShowClipboardToast = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool MaskClipboardToast
	{
		get
		{
			return _model.MaskClipboardToast;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.MaskClipboardToast != value)
				{
					_model.MaskClipboardToast = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowCalendarInNotch
	{
		get
		{
			return _model.ShowCalendarInNotch;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowCalendarInNotch != value)
				{
					_model.ShowCalendarInNotch = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowBudgetAlertInNotch
	{
		get
		{
			return _model.ShowBudgetAlertInNotch;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowBudgetAlertInNotch != value)
				{
					_model.ShowBudgetAlertInNotch = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowStreakRiskInNotch
	{
		get
		{
			return _model.ShowStreakRiskInNotch;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowStreakRiskInNotch != value)
				{
					_model.ShowStreakRiskInNotch = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool ShowTimersInNotch
	{
		get
		{
			return _model.ShowTimersInNotch;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.ShowTimersInNotch != value || !_model.ShowTimersInNotchChosen)
				{
					_model.ShowTimersInNotch = value;
					_model.ShowTimersInNotchChosen = true;
					SaveAndNotify();
				}
			}
		}
	}

	public bool EdgeLightEnabled
	{
		get
		{
			return _model.EdgeLightEnabled;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.EdgeLightEnabled != value)
				{
					_model.EdgeLightEnabled = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string EdgeLightColor
	{
		get
		{
			return _model.EdgeLightColor ?? "White";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.EdgeLightColor != value)
				{
					_model.EdgeLightColor = value;
					SaveAndNotify(immediate: true);
				}
			}
		}
	}

	public string EdgeLightCustomColor
	{
		get
		{
			return _model.EdgeLightCustomColor ?? "#FFFFFF";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.EdgeLightCustomColor != value)
				{
					_model.EdgeLightCustomColor = value;
					SaveAndNotify(immediate: true);
				}
			}
		}
	}

	public string EdgeLightStyle
	{
		get
		{
			return _model.EdgeLightStyle ?? "Comet";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.EdgeLightStyle != value)
				{
					_model.EdgeLightStyle = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string EdgeLightSpeed
	{
		get
		{
			return _model.EdgeLightSpeed ?? "Normal";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.EdgeLightSpeed != value)
				{
					_model.EdgeLightSpeed = value;
					SaveAndNotify();
				}
			}
		}
	}

	public double EdgeLightIntensity
	{
		get
		{
			return _model.EdgeLightIntensity;
		}
		set
		{
			lock (_saveLock)
			{
				if (!double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(_model.EdgeLightIntensity - Math.Max(0.0, Math.Min(1.0, value))) > 1E-09)
				{
					_model.EdgeLightIntensity = Math.Max(0.0, Math.Min(1.0, value));
					SaveAndNotify();
				}
			}
		}
	}

	public string EdgeLightWhen
	{
		get
		{
			if (!(_model.EdgeLightAnimationMode == "Always"))
			{
				return "Events";
			}
			return "Always";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.EdgeLightAnimationMode != value)
				{
					_model.EdgeLightAnimationMode = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string RestingLayout
	{
		get
		{
			if (!(_model.RestingLayout == "Center"))
			{
				return "Corners";
			}
			return "Center";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.RestingLayout != value)
				{
					_model.RestingLayout = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string MusicColor
	{
		get
		{
			if (!string.IsNullOrEmpty(_model.MusicColor))
			{
				return _model.MusicColor;
			}
			return "Adaptive";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.MusicColor != value)
				{
					_model.MusicColor = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string MusicCustomColor
	{
		get
		{
			if (!string.IsNullOrEmpty(_model.MusicCustomColor))
			{
				return _model.MusicCustomColor;
			}
			return "#FF453A";
		}
	}

	public string Reminders
	{
		get
		{
			if (!(_model.Reminders == "NotchAndWindows"))
			{
				return "Notch";
			}
			return "NotchAndWindows";
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.Reminders != value)
				{
					_model.Reminders = value;
					SaveAndNotify();
				}
			}
		}
	}

	public bool BorderOutlineEnabled
	{
		get
		{
			return _model.BorderOutlineEnabled;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.BorderOutlineEnabled != value)
				{
					_model.BorderOutlineEnabled = value;
					SaveAndNotify();
				}
			}
		}
	}

	public double BorderThickness
	{
		get
		{
			if (!(_model.BorderThickness <= 0.0))
			{
				return _model.BorderThickness;
			}
			return 1.0;
		}
		set
		{
			lock (_saveLock)
			{
				if (Math.Abs(_model.BorderThickness - value) > 0.1)
				{
					_model.BorderThickness = Math.Max(1.0, Math.Min(3.0, value));
					SaveAndNotify();
				}
			}
		}
	}

	public bool AdaptToWallpaper
	{
		get
		{
			return _model.AdaptToWallpaper;
		}
		set
		{
			lock (_saveLock)
			{
				if (_model.AdaptToWallpaper != value)
				{
					_model.AdaptToWallpaper = value;
					SaveAndNotify();
				}
			}
		}
	}

	public string AnimationSpeed => "Fast";

	public bool LaunchWithWindows
	{
		get
		{
			return StartupService.IsAutoStartEnabled();
		}
		set
		{
			if (value)
			{
				StartupService.EnableAutoStart();
			}
			else
			{
				StartupService.DisableAutoStart();
			}
		}
	}

	public string FilePath => _filePath;

	public event EventHandler ConfigChanged;

	public void ResetNotchOpacity()
	{
		lock (_saveLock)
		{
			if (_model.ClosedNotchOpacity != 1.0)
			{
				_model.ClosedNotchOpacity = 1.0;
				SaveAndNotify();
			}
		}
	}

	public void SetCustomEdgeLightColor(string hex)
	{
		lock (_saveLock)
		{
			if (!string.IsNullOrWhiteSpace(hex))
			{
				if (!hex.StartsWith("#"))
				{
					hex = "#" + hex;
				}
				_model.EdgeLightCustomColor = hex;
				_model.EdgeLightColor = "Custom";
				SaveAndNotify(immediate: true);
			}
		}
	}

	public void SetCustomMusicColor(string hex)
	{
		lock (_saveLock)
		{
			if (!string.IsNullOrWhiteSpace(hex))
			{
				if (!hex.StartsWith("#"))
				{
					hex = "#" + hex;
				}
				_model.MusicCustomColor = hex;
				_model.MusicColor = "Custom";
				SaveAndNotify(immediate: true);
			}
		}
	}

	public Color? GetFixedMusicColor()
	{
		string musicColor = MusicColor;
		switch (musicColor)
		{
		case "Adaptive":
			return null;
		case "White":
			return Colors.White;
		case "Custom":
			try
			{
				return (Color)ColorConverter.ConvertFromString(MusicCustomColor);
			}
			catch
			{
				return Colors.White;
			}
		default:
		{
			KeyValuePair<string, Color>[] musicPresets = MusicPresets;
			for (int i = 0; i < musicPresets.Length; i++)
			{
				KeyValuePair<string, Color> keyValuePair = musicPresets[i];
				if (keyValuePair.Key == musicColor)
				{
					return keyValuePair.Value;
				}
			}
			return null;
		}
		}
	}

	public void ResetToDefaults()
	{
		lock (_saveLock)
		{
			_model = new NotchConfigModel();
			_model.MigrateAnimationSpeed();
			SaveAndNotify();
		}
	}

	public bool ExportSettings(string destinationPath)
	{
		lock (_saveLock)
		{
			try
			{
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				string content = javaScriptSerializer.Serialize(_model);
				PersistenceHelper.WriteAllTextOrThrow(destinationPath, content);
				return true;
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("NotchConfig.ExportSettings failed: " + ex.Message);
				return false;
			}
		}
	}

	public bool ImportSettings(string sourcePath)
	{
		lock (_saveLock)
		{
			try
			{
				if (!File.Exists(sourcePath))
				{
					return false;
				}
				string text = PersistenceHelper.ReadShared(sourcePath);
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				NotchConfigModel notchConfigModel = javaScriptSerializer.Deserialize<NotchConfigModel>(text);
				if (notchConfigModel != null)
				{
					notchConfigModel.Validate();
					notchConfigModel.MigrateLegacyOpacity(NotchConfigModel.JsonHasKey(text, "ClosedNotchOpacity"));
					notchConfigModel.MigrateRemovedSettings();
					_model = notchConfigModel;
					SaveAndNotify();
					return true;
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("NotchConfig.ImportSettings failed: " + ex.Message);
			}
			return false;
		}
	}

	public Color GetEdgeLightColor()
	{
		string text = _model.EdgeLightColor ?? "Arctic White";
		if (string.Equals(text, "Adaptive", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Wallpaper", StringComparison.OrdinalIgnoreCase))
		{
			return WallpaperService.GetDominantColor();
		}
		switch (text)
		{
		case "Arctic White":
		case "White":
			return Color.FromRgb(245, 247, 250);
		case "Champagne Gold":
		case "Gold":
			return Color.FromRgb(231, 184, 92);
		case "Amber":
			return Color.FromRgb(245, 158, 11);
		case "Emerald":
			return Color.FromRgb(39, 201, 138);
		case "Teal":
		case "Cyan":
			return Color.FromRgb(32, 184, 176);
		case "Azure":
		case "Blue":
		case "Blue/Slate":
		case "Sapphire":
		case "Slate":
			return Color.FromRgb(59, 158, byte.MaxValue);
		case "Indigo":
			return Color.FromRgb(99, 102, 241);
		case "Violet":
		case "Plum":
		case "Mauve":
			return Color.FromRgb(155, 109, byte.MaxValue);
		case "Rose":
		case "Pink":
		case "Blush":
			return Color.FromRgb(240, 107, 138);
		case "Coral":
			return Color.FromRgb(240, 120, 95);
		case "Copper":
		case "Bronze":
		case "Terracotta":
			return Color.FromRgb(201, 122, 69);
		case "Ice Blue":
		case "Ivory":
		case "Cream":
		case "Sage":
			return Color.FromRgb(105, 199, 232);
		case "Custom":
		case "Multicolor":
			try
			{
				if (!string.IsNullOrEmpty(_model.EdgeLightCustomColor))
				{
					return (Color)ColorConverter.ConvertFromString(_model.EdgeLightCustomColor);
				}
			}
			catch
			{
			}
			return Color.FromRgb(245, 247, 250);
		default:
			if (!string.IsNullOrEmpty(text) && text.StartsWith("#"))
			{
				try
				{
					return (Color)ColorConverter.ConvertFromString(text);
				}
				catch
				{
				}
			}
			return Color.FromRgb(245, 247, 250);
		}
	}

	public NotchConfig()
	{
		if (_instance != null)
		{
			try
			{
				_instance.Flush();
			}
			catch
			{
			}
		}
		string localAppData = AppPaths.LocalAppData;
		string text = Path.Combine(localAppData, AppPaths.FolderName, "settings");
		if (!Directory.Exists(text))
		{
			try
			{
				Directory.CreateDirectory(text);
			}
			catch
			{
			}
		}
		_filePath = Path.Combine(text, "notch_config.json");
		Load();
		PersistenceHelper.RegisterExitFlush("notch", delegate
		{
			if (!FlushForExit())
			{
				throw new IOException("Notch settings are still pending.");
			}
		});
	}

	public void ResetToDefault()
	{
		lock (_saveLock)
		{
			_model = new NotchConfigModel();
			_model.MigrateAnimationSpeed();
			SaveAndNotify();
		}
	}

	public double GetCollapsedWidth()
	{
		double num;
		switch (_model.Style)
		{
		case "Compact":
			num = 210.0;
			break;
		case "Wide":
			num = 320.0;
			break;
		case "Circular":
			num = 255.0;
			break;
		case "Minimal":
		case "SquareMinimal":
			num = 240.0;
			break;
		case "Dynamic":
			num = 270.0;
			break;
		case "Line":
			num = 236.0;
			break;
		case "Island":
			num = 240.0;
			break;
		default:
			num = 228.0;
			break;
		}
		return Math.Round(num * SizeScale);
	}

	public double GetCollapsedHeight()
	{
		double num;
		switch (_model.Style)
		{
		case "Compact":
			num = 30.0;
			break;
		case "Wide":
			num = 36.0;
			break;
		case "Circular":
			num = 35.0;
			break;
		case "Minimal":
		case "SquareMinimal":
			num = 32.0;
			break;
		case "Dynamic":
			num = 35.0;
			break;
		case "Line":
			num = 26.0;
			break;
		case "Island":
			num = 40.0;
			break;
		default:
			num = 35.0;
			break;
		}
		if (string.Equals(_model.Style, "Island", StringComparison.OrdinalIgnoreCase))
		{
			return Math.Round((num - 5.0) * SizeScale) + 5.0;
		}
		return Math.Round(num * SizeScale);
	}

	public Brush GetAccentBrush()
	{
		return _model.ThemeAccent switch
		{
			"Blue" => new SolidColorBrush(Color.FromRgb(56, 189, 248)), 
			"Purple" => new SolidColorBrush(Color.FromRgb(168, 85, 247)), 
			"Green" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), 
			"Orange" => new SolidColorBrush(Color.FromRgb(249, 115, 22)), 
			"Pink" => new SolidColorBrush(Color.FromRgb(236, 72, 153)), 
			"Light" => new SolidColorBrush(Color.FromRgb(228, 228, 231)), 
			"Dark" => new SolidColorBrush(Color.FromRgb(39, 39, 42)), 
			_ => new SolidColorBrush(Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue)), 
		};
	}

	public Brush GetBackgroundBrush()
	{
		byte a = (byte)Math.Max(30, Math.Min(255, (int)Math.Round(_model.Opacity * 255.0)));
		Color color = Color.FromArgb(a, 0, 0, 0);
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public Brush GetBorderBrush()
	{
		if (_model.AdaptToWallpaper)
		{
			Color dominantColor = WallpaperService.GetDominantColor();
			SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb(90, dominantColor.R, dominantColor.G, dominantColor.B));
			solidColorBrush.Freeze();
			return solidColorBrush;
		}
		byte a = 13;
		SolidColorBrush solidColorBrush2 = new SolidColorBrush(Color.FromArgb(a, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		solidColorBrush2.Freeze();
		return solidColorBrush2;
	}

	public double GetShadowStrengthValue()
	{
		return _model.ShadowStrength switch
		{
			"None" => 0.0, 
			"Subtle" => 0.3, 
			"Deep" => 0.85, 
			_ => 0.55, 
		};
	}

	public Brush GetForegroundBrush()
	{
		if (string.Equals(_model.BackgroundMode, "Light", StringComparison.OrdinalIgnoreCase) || string.Equals(_model.ThemeAccent, "Light", StringComparison.OrdinalIgnoreCase))
		{
			return new SolidColorBrush(Color.FromRgb(24, 24, 27));
		}
		return Brushes.White;
	}

	public void SaveAndNotify(bool immediate = false)
	{
		lock (_saveLock)
		{
			_isDirty = true;
			_pendingJson = new JavaScriptSerializer().Serialize(_model);
		}
		if (!immediate)
		{
			lock (_saveLock)
			{
				if (_saveDebounceTimer == null)
				{
					_saveDebounceTimer = new Timer(delegate
					{
						Flush();
					}, null, 500, -1);
				}
				else
				{
					_saveDebounceTimer.Change(500, -1);
				}
			}
		}
		else
		{
			Flush();
		}
		if (this.ConfigChanged != null)
		{
			try
			{
				this.ConfigChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public bool Flush()
	{
		lock (_saveLock)
		{
			if (!_isDirty)
			{
				return true;
			}
			if (_pendingJson == null)
			{
				_pendingJson = new JavaScriptSerializer().Serialize(_model);
			}
			if (!PersistenceHelper.SafeWriteAllText(_filePath, _pendingJson))
			{
				return false;
			}
			_isDirty = false;
			_pendingJson = null;
			return true;
		}
	}

	public bool FlushForExit()
	{
		lock (_saveLock)
		{
			if (_saveDebounceTimer != null)
			{
				_saveDebounceTimer.Change(-1, -1);
			}
			return Flush();
		}
	}

	private void Load()
	{
		string raw = null;
		NotchConfigModel notchConfigModel = PersistenceHelper.ReadWithBackup(_filePath, delegate(string text)
		{
			NotchConfigModel result = new JavaScriptSerializer().Deserialize<NotchConfigModel>(text);
			raw = text;
			return result;
		});
		_model = notchConfigModel ?? NotchConfigModel.FreshInstall();
		_model.Validate();
		if (notchConfigModel != null)
		{
			_model.MigrateLegacyOpacity(NotchConfigModel.JsonHasKey(raw, "ClosedNotchOpacity"));
		}
		bool flag = notchConfigModel != null && _model.MigrateRemovedSettings();
		if (_model.MigrateEdgeLight() || flag)
		{
			SaveAndNotify(immediate: true);
		}
	}
}
