using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Media;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Theme;

public class NotchConfigModel
{
	public string Style { get; set; }

	public string Size { get; set; }

	public double SizeScale { get; set; }

	public bool ShowDateInNotch { get; set; }

	public string BackgroundMode { get; set; }

	public double ClosedNotchOpacity { get; set; }

	public double Opacity { get; set; }

	public string BlurEffect { get; set; }

	public string ShadowStrength { get; set; }

	public string CornerRadiusMode { get; set; }

	public double CustomCornerRadius { get; set; }

	public string ThemeAccent { get; set; }

	public string AppTheme { get; set; }

	public bool ShowAppLauncher { get; set; }

	public bool ShowTime { get; set; }

	public bool NotchAnalogClock { get; set; }

	public bool ShowVolume { get; set; }

	public bool ShowWiFi { get; set; }

	public bool ShowBattery { get; set; }

	public bool ShowBluetooth { get; set; }

	public bool ShowNowPlaying { get; set; }

	public bool ShowNotifications { get; set; }

	public bool ShowClipboardQuickAccess { get; set; }

	public bool ShowClipboardToast { get; set; }

	public bool MaskClipboardToast { get; set; }

	public bool ShowCalendarInNotch { get; set; }

	public bool ShowBudgetAlertInNotch { get; set; }

	public bool ShowStreakRiskInNotch { get; set; }

	public bool ShowTimersInNotch { get; set; }

	public bool ShowTimersInNotchChosen { get; set; }

	public bool EdgeLightEnabled { get; set; }

	public string EdgeLightColor { get; set; }

	public string EdgeLightCustomColor { get; set; }

	public string EdgeLightStyle { get; set; }

	public string EdgeLightSpeed { get; set; }

	public double EdgeLightIntensity { get; set; }

	public string EdgeLightAnimationMode { get; set; }

	public string RestingLayout { get; set; }

	public string MusicColor { get; set; }

	public string MusicCustomColor { get; set; }

	public string Reminders { get; set; }

	public bool BorderOutlineEnabled { get; set; }

	public double BorderThickness { get; set; }

	public bool AdaptToWallpaper { get; set; }

	public string AnimationSpeed { get; set; }

	public static NotchConfigModel FreshInstall()
	{
		NotchConfigModel notchConfigModel = new NotchConfigModel();
		notchConfigModel.Style = "Compact";
		notchConfigModel.SizeScale = 0.9;
		notchConfigModel.RestingLayout = "Corners";
		notchConfigModel.AnimationSpeed = "Fast";
		notchConfigModel.EdgeLightEnabled = true;
		notchConfigModel.EdgeLightStyle = "Twin comets";
		notchConfigModel.EdgeLightAnimationMode = "Always";
		notchConfigModel.EdgeLightColor = "White";
		notchConfigModel.EdgeLightCustomColor = "#FFFFFF";
		notchConfigModel.ShowClipboardToast = true;
		return notchConfigModel;
	}

	public NotchConfigModel()
	{
		Style = "Default";
		Size = "Medium";
		BackgroundMode = "Auto";
		Opacity = 1.0;
		ClosedNotchOpacity = 1.0;
		BlurEffect = "Medium";
		ShadowStrength = "Medium";
		CornerRadiusMode = "Auto";
		CustomCornerRadius = 16.0;
		ThemeAccent = "Auto";
		AppTheme = "PureBlack";
		ShowAppLauncher = false;
		ShowTime = true;
		ShowVolume = false;
		ShowWiFi = true;
		ShowBattery = true;
		ShowBluetooth = false;
		ShowNowPlaying = false;
		ShowNotifications = false;
		ShowClipboardQuickAccess = false;
		ShowClipboardToast = false;
		MaskClipboardToast = true;
		ShowCalendarInNotch = true;
		ShowBudgetAlertInNotch = false;
		ShowStreakRiskInNotch = false;
		ShowTimersInNotch = true;
		EdgeLightEnabled = false;
		EdgeLightColor = "White";
		EdgeLightCustomColor = "#FFFFFF";
		EdgeLightStyle = "Comet";
		EdgeLightSpeed = "Normal";
		EdgeLightIntensity = 0.8;
		EdgeLightAnimationMode = "Events";
		RestingLayout = "Corners";
		MusicColor = "Adaptive";
		MusicCustomColor = "#FF453A";
		Reminders = "Notch";
		BorderOutlineEnabled = false;
		BorderThickness = 1.0;
		AdaptToWallpaper = false;
		AnimationSpeed = "Normal";
	}

	public bool MigrateEdgeLight()
	{
		string edgeLightStyle = EdgeLightStyle;
		string edgeLightSpeed = EdgeLightSpeed;
		string edgeLightAnimationMode = EdgeLightAnimationMode;
		string obj = EdgeLightStyle ?? "";
		string key = obj;
		if (obj != null)
		{
			if (_003CPrivateImplementationDetails_003E_007BA4927130_002DF5F1_002D4136_002D8795_002D663817BD8143_007D._0024_0024method0x6000b8d_002D1 == null)
			{
				_003CPrivateImplementationDetails_003E_007BA4927130_002DF5F1_002D4136_002D8795_002D663817BD8143_007D._0024_0024method0x6000b8d_002D1 = new Dictionary<string, int>(6)
				{
					{ "Dual Sweep", 0 },
					{ "Twin comets", 1 },
					{ "Pulse", 2 },
					{ "Breathing", 3 },
					{ "Continuous Flow", 4 },
					{ "Breathe", 5 }
				};
			}
			if (_003CPrivateImplementationDetails_003E_007BA4927130_002DF5F1_002D4136_002D8795_002D663817BD8143_007D._0024_0024method0x6000b8d_002D1.TryGetValue(key, out var value))
			{
				switch (value)
				{
				case 0:
				case 1:
					break;
				case 2:
				case 3:
				case 4:
				case 5:
					goto IL_00c8;
				default:
					goto IL_00d5;
				}
				EdgeLightStyle = "Twin comets";
				goto IL_00e0;
			}
		}
		goto IL_00d5;
		IL_00e0:
		switch (EdgeLightSpeed ?? "")
		{
		case "Very Slow":
		case "Slow":
			EdgeLightSpeed = "Slow";
			break;
		case "Fast":
		case "Very Fast":
			EdgeLightSpeed = "Fast";
			break;
		default:
			EdgeLightSpeed = "Normal";
			break;
		}
		EdgeLightAnimationMode = ((EdgeLightAnimationMode == "Always" || EdgeLightAnimationMode == "Always On") ? "Always" : "Events");
		if (double.IsNaN(EdgeLightIntensity) || EdgeLightIntensity < 0.0 || EdgeLightIntensity > 1.0)
		{
			EdgeLightIntensity = 0.8;
		}
		if (!(edgeLightStyle != EdgeLightStyle) && !(edgeLightSpeed != EdgeLightSpeed))
		{
			return edgeLightAnimationMode != EdgeLightAnimationMode;
		}
		return true;
		IL_00c8:
		EdgeLightStyle = "Breathe";
		goto IL_00e0;
		IL_00d5:
		EdgeLightStyle = "Comet";
		goto IL_00e0;
	}

	public bool MigrateAnimationSpeed()
	{
		bool result = AnimationSpeed != "Fast";
		AnimationSpeed = "Fast";
		return result;
	}

	public bool MigrateTimersInNotch()
	{
		if (ShowTimersInNotch || ShowTimersInNotchChosen)
		{
			return false;
		}
		ShowTimersInNotch = true;
		return true;
	}

	public bool MigrateRemovedSettings()
	{
		bool flag = MigrateAnimationSpeed();
		bool result = MigrateTimersInNotch();
		if (!flag)
		{
			return result;
		}
		return true;
	}

	public void Validate()
	{
		NotchConfigModel notchConfigModel = new NotchConfigModel();
		if (SizeScale != 0.0)
		{
			SizeScale = Math.Round(Number("SizeScale", SizeScale, 0.9, 1.2, notchConfigModel.SizeScale), 2);
		}
		ClosedNotchOpacity = SafeNotchOpacity(ClosedNotchOpacity, 0.4);
		Opacity = Number("Opacity", Opacity, 0.2, 1.0, notchConfigModel.Opacity);
		CustomCornerRadius = Number("CustomCornerRadius", CustomCornerRadius, 0.0, 32.0, notchConfigModel.CustomCornerRadius);
		EdgeLightIntensity = Number("EdgeLightIntensity", EdgeLightIntensity, 0.0, 1.0, notchConfigModel.EdgeLightIntensity);
		BorderThickness = Number("BorderThickness", BorderThickness, 1.0, 3.0, notchConfigModel.BorderThickness);
		Style = Choice("Style", Style, notchConfigModel.Style, "Default", "Compact", "Wide", "Circular", "Minimal", "SquareMinimal", "Dynamic", "Line", "Island");
		Size = Choice("Size", Size, notchConfigModel.Size, "Small", "Medium", "Large");
		BackgroundMode = Choice("BackgroundMode", BackgroundMode, notchConfigModel.BackgroundMode, "Auto", "Dark", "Light", "Custom");
		BlurEffect = Choice("BlurEffect", BlurEffect, notchConfigModel.BlurEffect, "None", "Low", "Medium", "High");
		ShadowStrength = Choice("ShadowStrength", ShadowStrength, notchConfigModel.ShadowStrength, "None", "Subtle", "Medium", "Deep");
		CornerRadiusMode = Choice("CornerRadiusMode", CornerRadiusMode, notchConfigModel.CornerRadiusMode, "Auto", "Custom");
		ThemeAccent = Choice("ThemeAccent", ThemeAccent, notchConfigModel.ThemeAccent, "Auto", "Dark", "Light", "Blue", "Purple", "Green", "Orange", "Pink");
		AppTheme = Choice("AppTheme", AppTheme, notchConfigModel.AppTheme, "PureBlack", "EmberDark", "Vivid", "Silver");
		EdgeLightColor = Choice("EdgeLightColor", EdgeLightColor, notchConfigModel.EdgeLightColor, "White", "Gold", "Ivory", "Emerald", "Indigo", "Rose", "Bronze", "Sapphire", "Sage", "Plum", "Copper", "Slate", "Blush", "Custom", "Arctic White", "Champagne Gold", "Amber", "Teal", "Azure", "Violet", "Coral", "Ice Blue", "Adaptive", "Wallpaper", "Auto");
		EdgeLightStyle = Choice("EdgeLightStyle", EdgeLightStyle, notchConfigModel.EdgeLightStyle, "Comet", "Twin comets", "Breathe", "Soft Sweep", "Sharp Sweep", "Dual Sweep", "Dotted Trail", "Pulse", "Breathing", "Continuous Flow");
		EdgeLightSpeed = Choice("EdgeLightSpeed", EdgeLightSpeed, notchConfigModel.EdgeLightSpeed, "Slow", "Normal", "Fast", "Very Slow", "Very Fast");
		EdgeLightAnimationMode = Choice("EdgeLightAnimationMode", EdgeLightAnimationMode, notchConfigModel.EdgeLightAnimationMode, "Always", "Events", "Always On", "On Activity", "Off");
		RestingLayout = Choice("RestingLayout", RestingLayout, notchConfigModel.RestingLayout, "Corners", "Center");
		MusicColor = Choice("MusicColor", MusicColor, notchConfigModel.MusicColor, "Adaptive", "White", "Custom", "Red", "Pink", "Purple", "Blue", "Teal", "Green", "Yellow");
		Reminders = Choice("Reminders", Reminders, notchConfigModel.Reminders, "Notch", "NotchAndWindows");
		AnimationSpeed = Choice("AnimationSpeed", AnimationSpeed, notchConfigModel.AnimationSpeed, "Slow", "Normal", "Fast");
		EdgeLightCustomColor = ValidColor("EdgeLightCustomColor", EdgeLightCustomColor, notchConfigModel.EdgeLightCustomColor);
		MusicCustomColor = ValidColor("MusicCustomColor", MusicCustomColor, notchConfigModel.MusicCustomColor);
		MigrateEdgeLight();
	}

	public bool MigrateLegacyOpacity(bool closedSaved)
	{
		if (closedSaved || double.IsNaN(Opacity) || Opacity >= 0.999)
		{
			return false;
		}
		ClosedNotchOpacity = Math.Max(0.4, Math.Min(1.0, Opacity));
		return true;
	}

	internal static bool JsonHasKey(string json, string key)
	{
		try
		{
			return !(new JavaScriptSerializer().DeserializeObject(json) is Dictionary<string, object> dictionary) || dictionary.ContainsKey(key);
		}
		catch
		{
			return true;
		}
	}

	internal static double SafeNotchOpacity(double value, double minimum)
	{
		if (!double.IsNaN(value) && !double.IsInfinity(value) && !(value < minimum) && !(value > 1.0))
		{
			return value;
		}
		return 1.0;
	}

	private static double Number(string field, double value, double min, double max, double fallback)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			Repair(field, "defaulted non-finite number");
			return fallback;
		}
		double num = Math.Max(min, Math.Min(max, value));
		if (num != value)
		{
			Repair(field, "clamped to supported range");
		}
		return num;
	}

	private static string Choice(string field, string value, string fallback, params string[] allowed)
	{
		foreach (string text in allowed)
		{
			if (string.Equals(text, value, StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
		}
		Repair(field, "defaulted invalid option");
		return fallback;
	}

	private static string ValidColor(string field, string value, string fallback)
	{
		try
		{
			ColorConverter.ConvertFromString(value);
			return value;
		}
		catch
		{
			Repair(field, "defaulted invalid color");
			return fallback;
		}
	}

	private static void Repair(string field, string action)
	{
		TransactionLogger.Warn("Notch settings: " + field + " " + action);
	}
}
