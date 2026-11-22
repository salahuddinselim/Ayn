using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyTypography
{
	public const double DisplaySize = 28.0;

	public const double HeroSize = 28.0;

	public const double TitleSize = 13.5;

	public const double SectionSize = 11.0;

	public const double BodySize = 13.0;

	public const double SubSize = 11.5;

	public const double SecondarySize = 11.5;

	public const double CaptionSize = 11.0;

	public const double LabelSize = 11.0;

	public const double MicroSize = 10.0;

	public const double DisplayLineHeight = 34.0;

	public const double HeroLineHeight = 34.0;

	public const double TitleLineHeight = 18.0;

	public const double SectionLineHeight = 14.0;

	public const double BodyLineHeight = 17.0;

	public const double SubLineHeight = 15.0;

	public const double SecondaryLineHeight = 15.0;

	public const double CaptionLineHeight = 14.0;

	public const double LabelLineHeight = 14.0;

	public const double MicroLineHeight = 13.0;

	public const double ToolDisplaySize = 20.0;

	public const double ToolTitleSize = 13.5;

	public const double ToolSectionSize = 11.0;

	public const double ToolBodySize = 13.0;

	public const double ToolSecondarySize = 11.5;

	public const double ToolCaptionSize = 10.0;

	public const double ToolDisplayLineHeight = 26.0;

	public const double ToolTitleLineHeight = 18.0;

	public const double ToolSectionLineHeight = 14.0;

	public const double ToolBodyLineHeight = 17.0;

	public const double ToolSecondaryLineHeight = 15.0;

	public const double ToolCaptionLineHeight = 13.0;

	public static readonly Brush BrushHero = BuddyTokens.Text100Brush;

	public static readonly Brush BrushTitle = BuddyTokens.Text100Brush;

	public static readonly Brush BrushSection = BuddyTokens.Text100Brush;

	public static readonly Brush BrushBody = BuddyTokens.Text100Brush;

	public static readonly Brush BrushSub = BuddyTokens.Text38Brush;

	public static readonly Brush BrushSecondary = BuddyTokens.Text60Brush;

	public static readonly Brush BrushCaption = BuddyTokens.Text38Brush;

	public static FontFamily FontDisplay => AynType.Family(TypeSurface.Capsule, 28.0);

	public static FontFamily FontText => AynType.Family(TypeSurface.Capsule, 13.0);

	public static FontFamily FontEmoji => AynType.Family(TypeSurface.Capsule, 13.0);

	public static TextBlock CreateHero(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontDisplay;
		textBlock.FontSize = 28.0;
		textBlock.FontWeight = AynType.Semibold;
		textBlock.Foreground = foreground ?? BrushHero;
		textBlock.LineHeight = 34.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		SetTabularFigures(textBlock2);
		AynType.ApplyTextMode(textBlock2, textBlock2.FontSize);
		return textBlock2;
	}

	public static TextBlock CreateDisplay(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontDisplay;
		textBlock.FontSize = 28.0;
		textBlock.FontWeight = AynType.Semibold;
		textBlock.Foreground = foreground ?? BrushTitle;
		textBlock.LineHeight = 34.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		SetTabularFigures(textBlock2);
		AynType.ApplyTextMode(textBlock2, textBlock2.FontSize);
		return textBlock2;
	}

	public static TextBlock CreateTitle(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 13.5;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = ((foreground != null) ? foreground : BrushTitle);
		textBlock.LineHeight = 18.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateSection(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 11.0;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = ((foreground != null) ? foreground : BrushSection);
		textBlock.LineHeight = 14.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateBody(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 13.0;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.Foreground = ((foreground != null) ? foreground : BrushBody);
		textBlock.LineHeight = 17.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateSecondary(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 11.5;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.Foreground = ((foreground != null) ? foreground : BrushSecondary);
		textBlock.LineHeight = 15.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateCaption(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 11.0;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.Foreground = ((foreground != null) ? foreground : BrushCaption);
		textBlock.LineHeight = 14.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateMicro(string text, Brush foreground = null)
	{
		return CreateMicroLabel(text, foreground);
	}

	public static TextBlock CreateMicroLabel(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text ?? "";
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 11.0;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = ((foreground != null) ? foreground : BrushCaption);
		textBlock.LineHeight = 14.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static void SetTabularFigures(DependencyObject obj)
	{
		if (obj != null)
		{
			Typography.SetNumeralAlignment(obj, FontNumeralAlignment.Tabular);
		}
	}

	public static TextBlock CreateToolDisplay(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontDisplay;
		textBlock.FontSize = 20.0;
		textBlock.FontWeight = AynType.Semibold;
		textBlock.Foreground = ((foreground != null) ? foreground : BuddyTokens.TextDisplayBrush);
		textBlock.LineHeight = 26.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		SetTabularFigures(textBlock2);
		AynType.ApplyTextMode(textBlock2, textBlock2.FontSize);
		return textBlock2;
	}

	public static TextBlock CreateToolTitle(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 13.5;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = ((foreground != null) ? foreground : BuddyTokens.TextPrimaryBrush);
		textBlock.LineHeight = 18.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateToolSection(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 11.0;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = ((foreground != null) ? foreground : BuddyTokens.TextSecondaryBrush);
		textBlock.LineHeight = 14.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateToolBody(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 13.0;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.Foreground = ((foreground != null) ? foreground : BuddyTokens.TextPrimaryBrush);
		textBlock.LineHeight = 17.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateToolSecondary(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 11.5;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.Foreground = ((foreground != null) ? foreground : BuddyTokens.TextSecondaryBrush);
		textBlock.LineHeight = 15.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateToolCaption(string text, Brush foreground = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = FontText;
		textBlock.FontSize = 10.0;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.Foreground = ((foreground != null) ? foreground : BuddyTokens.TextTertiaryBrush);
		textBlock.LineHeight = 13.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		return textBlock;
	}

	public static TextBlock CreateNumeric(string text, double size, Brush foreground = null, FontWeight? weight = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontFamily = ((size >= 18.0) ? FontDisplay : FontText);
		textBlock.FontSize = size;
		textBlock.FontWeight = AynType.Calm(weight.HasValue ? weight.Value : FontWeights.SemiBold, size);
		textBlock.Foreground = ((foreground != null) ? foreground : BuddyTokens.TextPrimaryBrush);
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		SetTabularFigures(textBlock2);
		AynType.ApplyTextMode(textBlock2, textBlock2.FontSize);
		return textBlock2;
	}

	private static Brush FreezeBrush(Brush b)
	{
		return ThemeTokens.FreezeSafe(b);
	}
}
