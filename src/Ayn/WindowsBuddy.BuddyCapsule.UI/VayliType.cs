using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class AynType
{
	public const double NotchPrimary = 12.5;

	public const double NotchSecondary = 11.5;

	public const double NotchNumber = 12.5;

	public const double NotchTitleMax = 13.0;

	public const double Label = 11.0;

	public const double Body = 13.0;

	public const double RowTitle = 13.5;

	public const double Secondary = 11.5;

	public const double Caption = 11.0;

	public const double Micro = 10.0;

	public const double Button = 13.0;

	public const double Tooltip = 12.0;

	public const double PanelTitle = 15.0;

	public const double ValueSmall = 20.0;

	public const double Value = 24.0;

	public const double ValueLarge = 28.0;

	public const double PageTitle = 20.0;

	public const double GroupLabel = 12.0;

	public const double SettingsRow = 15.0;

	public const double Description = 13.5;

	public const double SettingsPageTitle = 28.0;

	public const double SettingsSubtitle = 14.0;

	public const double SettingsControl = 13.5;

	public const double SettingsMeta = 12.0;

	public const double SettingsBrand = 15.0;

	public const double DocBody = 15.0;

	public const double DocH2 = 18.0;

	public const double DocH1 = 24.0;

	public const double ValueTrackingEm = -0.02;

	public const double IdealThreshold = 15.0;

	public static readonly FontWeight Regular = FontWeights.Normal;

	public static readonly FontWeight Medium = FontWeights.Medium;

	public static readonly FontWeight Semibold = FontWeights.SemiBold;

	public static readonly Brush NotchGrey = Freeze(new SolidColorBrush(Color.FromRgb(142, 142, 147)));

	public static readonly DependencyProperty SurfaceProperty = DependencyProperty.RegisterAttached("Surface", typeof(TypeSurface), typeof(AynType), new FrameworkPropertyMetadata(TypeSurface.Capsule, FrameworkPropertyMetadataOptions.Inherits));

	public static double ValueTracking(double size)
	{
		return Math.Round(-0.02 * size, 2);
	}

	public static FontFamily Family(TypeSurface surface, double size)
	{
		return BuddyFonts.Geist;
	}

	public static void SetClearType(DependencyObject root, bool opaque)
	{
		if (root == null)
		{
			return;
		}
		Stack<DependencyObject> stack = new Stack<DependencyObject>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			DependencyObject dependencyObject = stack.Pop();
			if (dependencyObject is UIElement)
			{
				SetHint((UIElement)dependencyObject, opaque);
			}
			int num = ((dependencyObject is Visual) ? VisualTreeHelper.GetChildrenCount(dependencyObject) : 0);
			for (int i = 0; i < num; i++)
			{
				stack.Push(VisualTreeHelper.GetChild(dependencyObject, i));
			}
		}
	}

	public static void SetHint(UIElement e, bool opaque)
	{
		if (opaque)
		{
			RenderOptions.SetClearTypeHint(e, ClearTypeHint.Enabled);
		}
		else
		{
			e.ClearValue(RenderOptions.ClearTypeHintProperty);
		}
	}

	public static void SetSurface(DependencyObject d, TypeSurface s)
	{
		d.SetValue(SurfaceProperty, s);
	}

	public static TypeSurface GetSurface(DependencyObject d)
	{
		return (TypeSurface)d.GetValue(SurfaceProperty);
	}

	public static void ApplyTextMode(DependencyObject element, double size)
	{
		if (element != null)
		{
			TextOptions.SetTextFormattingMode(element, (!(size >= 15.0)) ? TextFormattingMode.Display : TextFormattingMode.Ideal);
			TextOptions.SetTextRenderingMode(element, TextRenderingMode.ClearType);
			TextOptions.SetTextHintingMode(element, TextHintingMode.Fixed);
		}
	}

	public static void PrepareRoot(FrameworkElement root, TypeSurface surface)
	{
		if (root != null)
		{
			root.UseLayoutRounding = true;
			root.SnapsToDevicePixels = true;
			SetSurface(root, surface);
			TextOptions.SetTextHintingMode(root, TextHintingMode.Fixed);
			TextOptions.SetTextRenderingMode(root, TextRenderingMode.ClearType);
			TextOptions.SetTextFormattingMode(root, TextFormattingMode.Display);
		}
	}

	public static void Tabular(DependencyObject d)
	{
		if (d != null)
		{
			Typography.SetNumeralAlignment(d, FontNumeralAlignment.Tabular);
		}
	}

	public static TextBlock Text(string text, double size, FontWeight weight, Brush ink, TypeSurface surface = TypeSurface.Capsule)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text ?? string.Empty;
		textBlock.FontFamily = Family(surface, size);
		textBlock.FontSize = size;
		textBlock.FontWeight = weight;
		textBlock.Foreground = ink;
		textBlock.TextWrapping = TextWrapping.NoWrap;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		ApplyTextMode(textBlock2, size);
		return textBlock2;
	}

	public static TextBlock Number(string text, double size, FontWeight weight, Brush ink, TypeSurface surface = TypeSurface.Capsule)
	{
		TextBlock textBlock = Text(text, size, weight, ink, surface);
		Tabular(textBlock);
		return textBlock;
	}

	public static void Style(DependencyObject d, double size, FontWeight weight, TypeSurface surface = TypeSurface.Capsule)
	{
		if (d != null)
		{
			d.SetValue(TextElement.FontFamilyProperty, Family(surface, size));
			d.SetValue(TextElement.FontSizeProperty, size);
			d.SetValue(TextElement.FontWeightProperty, weight);
			ApplyTextMode(d, size);
		}
	}

	public static FontWeight Calm(FontWeight w, double size)
	{
		if (w.ToOpenTypeWeight() >= 600 && size < 20.0 && size != 20.0 && size != 15.0 && size != 28.0)
		{
			return Medium;
		}
		if (w.ToOpenTypeWeight() > 600)
		{
			return Semibold;
		}
		return w;
	}

	private static Brush Freeze(Brush b)
	{
		b.Freeze();
		return b;
	}
}
