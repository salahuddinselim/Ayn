using System.Collections.Generic;
using WindowsBuddy.Liquid;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal static class SettingsTheme
{
	public const double Title = 28.0;

	public const double Subtitle = 14.0;

	public const double Label = 12.0;

	public const double Body = 15.0;

	public const double Caption = 13.5;

	public const double ControlText = 13.5;

	public const double KeycapText = 13.5;

	public const double Meta = 12.0;

	public const double BrandName = 15.0;

	public const double Space4 = 4.0;

	public const double Space8 = 8.0;

	public const double Space12 = 12.0;

	public const double Space16 = 16.0;

	public const double Space20 = 20.0;

	public const double Space24 = 24.0;

	public const double Space32 = 32.0;

	public const double Space40 = 40.0;

	public const double Space48 = 48.0;

	public const double Space64 = 64.0;

	public const double Space80 = 80.0;

	public const double Space88 = 88.0;

	public const double Radius = 10.0;

	public const double ControlRadius = 8.0;

	public const double ControlHeight = 36.0;

	public const double InputHeight = 36.0;

	public const double KeycapHeight = 24.0;

	public const double KeycapRadius = 5.0;

	public const double KeycapGap = 4.0;

	public const double RowHeight = 44.0;

	public const double RowHeightWithDescription = 52.0;

	public const double CardPadding = 16.0;

	public const double GroupGap = 32.0;

	public const double LabelGap = 12.0;

	public const double ContentMaxWidth = 560.0;

	public const double SidebarWidth = 248.0;

	public const double ContentWidth = 712.0;

	public const double NavRowHeight = 36.0;

	public const double NavRowGap = 4.0;

	public const double NavGroupGap = 16.0;

	public const double NavTileSize = 24.0;

	public const double HeaderTileSize = 28.0;

	public const double LogoSize = 40.0;

	public const double UpgradeHeight = 30.0;

	public const double ToggleWidth = 44.0;

	public const double ToggleHeight = 24.0;

	public const double ToggleKnob = 20.0;

	public const double SliderTrack = 4.0;

	public const double SliderKnob = 14.0;

	public const double HoverMs = 100.0;

	public static readonly Brush ContentBg = ThemeTokens.Bg;

	public static readonly Brush SidebarBg = ThemeTokens.Surface;

	public static readonly Brush SidebarEdge = ThemeTokens.Rule;

	public static readonly Brush GroupBg = ThemeTokens.Surface;

	public static readonly Brush Hairline = ThemeTokens.Rule;

	public static readonly Brush CardHighlight = Brushes.Transparent;

	public static readonly Brush Divider = ThemeTokens.Rule;

	public static readonly Brush PopupBg = ThemeTokens.Surface;

	public static readonly Brush TextPrimary = ThemeTokens.Text;

	public static readonly Brush TextSecondary = ThemeTokens.Text2;

	public static readonly Brush TextLabel = ThemeTokens.Text3;

	public static readonly Brush TextDisabled = ThemeTokens.TextDisabled;

	public static readonly Brush Accent = ThemeTokens.Accent;

	public static readonly Brush FocusRing = ThemeTokens.Focus;

	public static readonly Brush RowHover = Brushes.Transparent;

	public static readonly Brush NavHover = ThemeTokens.Surface2;

	public static readonly Brush NavSelected = ThemeTokens.AccentSoft;

	public static readonly Brush Highlight = ThemeTokens.AccentSoft;

	public static readonly Brush SelectedOutline = ThemeTokens.Accent;

	public static readonly Brush SelectionBrush = ThemeTokens.Accent;

	public static readonly Brush ControlBg = ThemeTokens.ControlFill;

	public static readonly Brush ControlHover = ThemeTokens.ControlFillHover;

	public static readonly Brush ControlPressed = ThemeTokens.ControlFillHover;

	public static readonly Brush ControlBorder = Brushes.Transparent;

	public static readonly Brush SegmentSelectedBg = ThemeTokens.SegmentThumb;

	public static readonly Brush SegmentSelectedText = ThemeTokens.Text;

	public static readonly Brush InputBg = ControlBg;

	public static readonly Brush InputBorder = ControlBorder;

	public static readonly Brush InputBorderFocus = FocusRing;

	public static readonly Brush ToggleOnTrack = Accent;

	public static readonly Brush ToggleOffTrack = ThemeTokens.Surface3;

	public static readonly Brush ToggleOnKnob = ThemeTokens.AccentFg;

	public static readonly Brush ToggleOffKnob = ThemeTokens.IconMuted;

	public static readonly Brush SliderFill = TextPrimary;

	public static readonly Brush PrimaryButtonBg = ThemeTokens.AccentStrong;

	public static readonly Brush PrimaryButtonHover = ThemeTokens.AccentStrong;

	public static readonly Brush PrimaryButtonText = ThemeTokens.AccentFg;

	public static readonly Brush Danger = ThemeTokens.Danger;

	public static readonly Brush Success = ThemeTokens.Success;

	public static readonly Brush Warning = ThemeTokens.Warning;

	private static readonly Dictionary<double, Style> _focusStyles = new Dictionary<double, Style>();

	public static Color AccentColor => ThemeTokens.Target(ThemeTokens.Accent);

	public static Color HoverColor => ThemeTokens.Target(ThemeTokens.Surface2);

	public static FontFamily Font => AynType.Family(TypeSurface.Settings, 15.0);

	public static void PrepareRoot(FrameworkElement root)
	{
		AynType.PrepareRoot(root, TypeSurface.Settings);
		root.SetValue(Control.FontFamilyProperty, Font);
		root.SetValue(Control.FontSizeProperty, 13.5);
		Style style = new Style(typeof(ToolTip));
		style.Setters.Add(new Setter(Control.FontFamilyProperty, Font));
		style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
		style.Setters.Add(new Setter(Control.FontWeightProperty, AynType.Regular));
		style.Setters.Add(new Setter(Control.ForegroundProperty, ThemeTokens.Ref(TextPrimary)));
		style.Setters.Add(new Setter(Control.BackgroundProperty, ThemeTokens.Ref(PopupBg)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8.0, 4.0, 8.0, 4.0)));
		style.Setters.Add(new Setter(FrameworkElement.UseLayoutRoundingProperty, true));
		style.Setters.Add(new Setter(UIElement.SnapsToDevicePixelsProperty, true));
		style.Setters.Add(new Setter(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display));
		style.Setters.Add(new Setter(TextOptions.TextRenderingModeProperty, TextRenderingMode.ClearType));
		style.Setters.Add(new Setter(TextOptions.TextHintingModeProperty, TextHintingMode.Fixed));
		root.Resources[typeof(ToolTip)] = style;
	}

	public static Brush Hex8(string argb)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(argb));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public static Brush Solid(byte r, byte g, byte b)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public static Brush Alpha(byte a, byte r, byte g, byte b)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public static Color Hex(string hex)
	{
		try
		{
			return (Color)ColorConverter.ConvertFromString(hex);
		}
		catch
		{
			return Colors.Gray;
		}
	}

	public static TextBlock Text(string text, double size, Brush fg, FontWeight weight)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = size;
		textBlock.Foreground = fg;
		textBlock.FontWeight = AynType.Calm(weight, size);
		textBlock.FontFamily = AynType.Family(TypeSurface.Settings, size);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock textBlock2 = textBlock;
		AynType.ApplyTextMode(textBlock2, size);
		AynType.Tabular(textBlock2);
		return textBlock2;
	}

	public static Style FocusStyle(double radius)
	{
		if (_focusStyles.TryGetValue(radius, out var value))
		{
			return value;
		}
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Rectangle));
		frameworkElementFactory.SetValue(Shape.StrokeProperty, ThemeTokens.Ref(FocusRing));
		frameworkElementFactory.SetValue(Shape.StrokeThicknessProperty, 2.0);
		frameworkElementFactory.SetValue(Rectangle.RadiusXProperty, radius + 2.0);
		frameworkElementFactory.SetValue(Rectangle.RadiusYProperty, radius + 2.0);
		frameworkElementFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(-2.0));
		frameworkElementFactory.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Control));
		controlTemplate.VisualTree = frameworkElementFactory;
		ControlTemplate value2 = controlTemplate;
		value = new Style();
		value.Setters.Add(new Setter(Control.TemplateProperty, value2));
		value.Seal();
		_focusStyles[radius] = value;
		return value;
	}

	public static void MakeFocusable(FrameworkElement e, double radius, string name)
	{
		e.Focusable = true;
		e.FocusVisualStyle = FocusStyle(radius);
		e.GotKeyboardFocus += delegate
		{
			e.BringIntoView();
		};
		KeyboardNavigation.SetIsTabStop(e, isTabStop: true);
		if (!string.IsNullOrEmpty(name))
		{
			AutomationProperties.SetName(e, name);
		}
	}

	public static void Animate(DependencyObject target, DependencyProperty prop, double to, double ms)
	{
		SMotion.To(target as Transform, prop, to, animate: true);
	}

	public static DropShadowEffect SoftShadow(double blur, double depth, double opacity)
	{
		return LiquidGlass.GetSmallShadow();
	}
}
