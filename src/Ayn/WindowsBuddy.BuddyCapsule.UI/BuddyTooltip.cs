using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyTooltip
{
	public static ToolTip Create(string text, double fontSize = 12.0)
	{
		return Create(text, fontSize, pill: false);
	}

	public static ToolTip Create(string text, double fontSize, bool pill)
	{
		ToolTip tt = new ToolTip
		{
			Content = text,
			Background = ThemeTokens.Surface2,
			Foreground = BuddyTheme.Text92,
			BorderBrush = ThemeTokens.BorderStrong,
			BorderThickness = new Thickness(1.0),
			FontFamily = BuddyTokens.FontText,
			FontSize = fontSize,
			FontWeight = AynType.Regular,
			Padding = new Thickness(8.0, 4.0, 8.0, 4.0),
			Placement = PlacementMode.Top,
			VerticalOffset = -6.0,
			HasDropShadow = false
		};
		TextOptions.SetTextFormattingMode(tt, TextFormattingMode.Display);
		tt.Opened += delegate
		{
			if (CapsuleMotion.AnimationsEnabled)
			{
				tt.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, BuddyTheme.Hover));
			}
		};
		ControlTemplate controlTemplate = new ControlTemplate(typeof(ToolTip));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(pill ? 12 : 6));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		frameworkElementFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
		frameworkElementFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		tt.Template = controlTemplate;
		ToolTipService.SetInitialShowDelay(tt, 200);
		ToolTipService.SetBetweenShowDelay(tt, 40);
		ToolTipService.SetShowDuration(tt, 4000);
		return tt;
	}
}
