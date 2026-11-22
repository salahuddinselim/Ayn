using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Message;

public static class MessageRenderer
{
	public class MessageLayoutResult
	{
		public double TextWidth { get; set; }

		public double TextHeight { get; set; }

		public int LineCount { get; set; }

		public double RequiredWidth { get; set; }

		public double RequiredHeight { get; set; }

		public double FontSize { get; set; }

		public Size ToSize()
		{
			return new Size(RequiredWidth, RequiredHeight);
		}
	}

	private const double LineFactor = 1.35;

	private const double RestingW = 228.0;

	private const double RestingH = 35.0;

	private const double SidePad = 56.0;

	private const double ScrollNotchW = 320.0;

	private const double ScrollViewW = 280.0;

	private const double ScrollViewH = 28.0;

	private const int WordChunk = 22;

	private static readonly double[] FontSizes = new double[3] { 13.0, 12.5, 11.5 };

	private static string _layoutKey;

	private static MessageLayoutResult _layout;

	private static readonly Dictionary<string, MessageLayoutResult> _layoutCache = new Dictionary<string, MessageLayoutResult>();

	private static FontFamily DefaultFont => AynType.Family(TypeSurface.Notch, 12.5);

	private static double MaxNotchWidth
	{
		get
		{
			double num = ((SystemParameters.PrimaryScreenWidth > 0.0) ? SystemParameters.PrimaryScreenWidth : 1920.0);
			return Math.Min(560.0, num * 0.55);
		}
	}

	private static double MaxTextWidth => Math.Min(380.0, MaxNotchWidth - 56.0);

	public static string ApplyWordBreakFallback(string text, int chunk = 22)
	{
		if (string.IsNullOrEmpty(text) || MessageText.Count(text) <= chunk)
		{
			return text;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length + 8);
		string[] array = text.Split(' ');
		for (int i = 0; i < array.Length; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(' ');
			}
			List<string> list = EmojiText.Clusters(array[i]);
			for (int j = 0; j < list.Count; j++)
			{
				if (j > 0 && j % chunk == 0)
				{
					stringBuilder.Append(' ');
				}
				stringBuilder.Append(list[j]);
			}
		}
		return stringBuilder.ToString();
	}

	private static TextBlock BuildText(string msg, double size, Brush fg, bool wrap, List<Inline> parts)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.FontFamily = DefaultFont;
		textBlock.FontSize = size;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = fg;
		textBlock.TextAlignment = TextAlignment.Center;
		textBlock.TextWrapping = ((!wrap) ? TextWrapping.NoWrap : TextWrapping.Wrap);
		textBlock.LineHeight = size * 1.35;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock textBlock2 = textBlock;
		if (wrap)
		{
			textBlock2.MaxWidth = MaxTextWidth;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item2 in EmojiText.Clusters(wrap ? ApplyWordBreakFallback(msg) : msg))
		{
			Image image = (EmojiText.IsEmoji(item2) ? ColorEmoji.CreateImage(item2, size) : null);
			if (image == null)
			{
				stringBuilder.Append(item2);
				if (parts != null)
				{
					AddRun(textBlock2, stringBuilder, parts);
				}
			}
			else
			{
				AddRun(textBlock2, stringBuilder, parts);
				InlineUIContainer inlineUIContainer = new InlineUIContainer(image);
				inlineUIContainer.BaselineAlignment = BaselineAlignment.Center;
				InlineUIContainer item = inlineUIContainer;
				textBlock2.Inlines.Add(item);
				parts?.Add(item);
			}
		}
		AddRun(textBlock2, stringBuilder, parts);
		return textBlock2;
	}

	private static void AddRun(TextBlock tb, StringBuilder text, List<Inline> parts)
	{
		if (text.Length != 0)
		{
			Run item = new Run(text.ToString());
			tb.Inlines.Add(item);
			parts?.Add(item);
			text.Length = 0;
		}
	}

	private static Size Measure(TextBlock tb)
	{
		using (new MessageTiming("text measurement"))
		{
			tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			return tb.DesiredSize;
		}
	}

	public static MessageLayoutResult CalculateLayout(string text, MessageSettings settings)
	{
		string text2 = MessageText.Clean(text);
		if (text2.Length == 0)
		{
			MessageLayoutResult messageLayoutResult = new MessageLayoutResult();
			messageLayoutResult.LineCount = 1;
			messageLayoutResult.RequiredWidth = 228.0;
			messageLayoutResult.RequiredHeight = 35.0;
			messageLayoutResult.FontSize = FontSizes[0];
			return messageLayoutResult;
		}
		if (settings != null && settings.Animation == MessageAnimationMode.Scroll)
		{
			MessageLayoutResult messageLayoutResult2 = new MessageLayoutResult();
			messageLayoutResult2.LineCount = 1;
			messageLayoutResult2.RequiredWidth = 320.0;
			messageLayoutResult2.RequiredHeight = 35.0;
			messageLayoutResult2.FontSize = FontSizes[0];
			return messageLayoutResult2;
		}
		string text3 = text2 + "\u0001" + MaxTextWidth.ToString(CultureInfo.InvariantCulture) + "|" + MaxNotchWidth.ToString(CultureInfo.InvariantCulture) + "|" + DefaultFont.Source + "|" + ColorEmoji.Revision;
		if (text3 == _layoutKey)
		{
			return _layout;
		}
		if (_layoutCache.TryGetValue(text3, out var value))
		{
			_layoutKey = text3;
			_layout = value;
			return value;
		}
		MessageLayoutResult messageLayoutResult3 = null;
		double[] fontSizes = FontSizes;
		foreach (double num in fontSizes)
		{
			Size size = Measure(BuildText(text2, num, Brushes.White, wrap: true, null));
			int num2 = Math.Max(1, (int)Math.Round(size.Height / (num * 1.35)));
			MessageLayoutResult messageLayoutResult4 = new MessageLayoutResult();
			messageLayoutResult4.TextWidth = size.Width;
			messageLayoutResult4.TextHeight = size.Height;
			messageLayoutResult4.LineCount = num2;
			messageLayoutResult4.FontSize = num;
			messageLayoutResult4.RequiredWidth = Math.Max(228.0, Math.Min(MaxNotchWidth, size.Width + 56.0));
			messageLayoutResult4.RequiredHeight = num2 switch
			{
				2 => 62.0, 
				1 => 35.0, 
				_ => 84.0, 
			};
			messageLayoutResult3 = messageLayoutResult4;
			if (num2 <= 3)
			{
				break;
			}
		}
		_layoutKey = text3;
		_layout = messageLayoutResult3;
		if (_layoutCache.Count >= 128)
		{
			_layoutCache.Clear();
		}
		_layoutCache[text3] = messageLayoutResult3;
		return messageLayoutResult3;
	}

	public static Size MeasureDimensions(string text, MessageSettings settings)
	{
		return CalculateLayout(text, settings).ToSize();
	}

	private static double SpeedFactor(double speed)
	{
		return 1.6 - Math.Max(0.0, Math.Min(100.0, speed)) / 100.0 * 1.1;
	}

	public static Brush CreateForegroundBrush(MessageSettings settings)
	{
		return CreateForegroundBrush(settings, 0.0);
	}

	public static Brush CreateForegroundBrush(MessageSettings settings, double width)
	{
		LinearGradientBrush linearGradientBrush = null;
		switch (settings?.Finish ?? MessageFinish.None)
		{
		case MessageFinish.Aurora:
			linearGradientBrush = Gradient(Color.FromRgb(123, 241, 168), Color.FromRgb(138, 124, 251), Color.FromRgb(234, 119, 209), diagonal: false);
			break;
		case MessageFinish.Ocean:
			linearGradientBrush = Gradient(Color.FromRgb(33, 147, 176), Color.FromRgb(109, 213, 237), null, diagonal: false);
			break;
		case MessageFinish.Silver:
			linearGradientBrush = Gradient(Color.FromRgb(224, 234, 252), Color.FromRgb(160, 170, 188), Color.FromRgb(224, 234, 252), diagonal: false);
			break;
		}
		Brush brush;
		if (linearGradientBrush != null)
		{
			if (width > 0.0)
			{
				linearGradientBrush.MappingMode = BrushMappingMode.Absolute;
				linearGradientBrush.StartPoint = new Point(0.0, 0.0);
				linearGradientBrush.EndPoint = new Point(width, 0.0);
			}
			brush = linearGradientBrush;
		}
		else
		{
			brush = new SolidColorBrush(ReadableOnNotch(MessageText.ParseColor(settings?.ColorHex) ?? Colors.White));
		}
		brush.Freeze();
		return brush;
	}

	internal static Color ReadableOnNotch(Color c)
	{
		for (int i = 0; i <= 20; i++)
		{
			double num = (double)i / 20.0;
			Color color = Color.FromRgb((byte)Math.Round((double)(int)c.R + (double)(255 - c.R) * num), (byte)Math.Round((double)(int)c.G + (double)(255 - c.G) * num), (byte)Math.Round((double)(int)c.B + (double)(255 - c.B) * num));
			if (Luminance(color) >= 0.1)
			{
				return color;
			}
		}
		return Colors.White;
	}

	private static double Luminance(Color c)
	{
		return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
	}

	private static double Channel(byte v)
	{
		double num = (double)(int)v / 255.0;
		if (!(num <= 0.03928))
		{
			return Math.Pow((num + 0.055) / 1.055, 2.4);
		}
		return num / 12.92;
	}

	public static Brush CreateFinishSwatchBrush(MessageFinish finish)
	{
		Brush brush = finish switch
		{
			MessageFinish.Aurora => Gradient(Color.FromRgb(123, 241, 168), Color.FromRgb(138, 124, 251), Color.FromRgb(234, 119, 209), diagonal: true), 
			MessageFinish.Ocean => Gradient(Color.FromRgb(109, 213, 237), Color.FromRgb(33, 100, 150), null, diagonal: true), 
			MessageFinish.Silver => Gradient(Color.FromRgb(200, 204, 214), Color.FromRgb(90, 92, 100), null, diagonal: true), 
			_ => Gradient(Color.FromRgb(58, 58, 62), Color.FromRgb(14, 14, 16), null, diagonal: true), 
		};
		brush.Freeze();
		return brush;
	}

	private static LinearGradientBrush Gradient(Color a, Color b, Color? c, bool diagonal)
	{
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
		linearGradientBrush.StartPoint = new Point(0.0, 0.0);
		linearGradientBrush.EndPoint = (diagonal ? new Point(1.0, 1.0) : new Point(1.0, 0.0));
		LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
		linearGradientBrush2.GradientStops.Add(new GradientStop(a, 0.0));
		if (c.HasValue)
		{
			linearGradientBrush2.GradientStops.Add(new GradientStop(b, 0.5));
			linearGradientBrush2.GradientStops.Add(new GradientStop(c.Value, 1.0));
		}
		else
		{
			linearGradientBrush2.GradientStops.Add(new GradientStop(b, 1.0));
		}
		return linearGradientBrush2;
	}

	public static FrameworkElement CreateRealCapsuleVisual(string text, MessageSettings settings)
	{
		if (settings == null)
		{
			settings = new MessageSettings();
		}
		string text2 = MessageText.Clean((!string.IsNullOrEmpty(text)) ? text : settings.Text);
		if (text2.Length == 0)
		{
			return new Grid();
		}
		double num = SpeedFactor(settings.Speed);
		if (settings.Animation == MessageAnimationMode.Scroll)
		{
			return BuildScroll(text2, settings);
		}
		MessageLayoutResult messageLayoutResult = CalculateLayout(text2, settings);
		List<Inline> parts = ((settings.Animation == MessageAnimationMode.TypeOn) ? new List<Inline>() : null);
		TextBlock tb = BuildText(text2, messageLayoutResult.FontSize, Brushes.White, wrap: true, parts);
		Brush brush = CreateForegroundBrush(settings, messageLayoutResult.TextWidth);
		tb.Foreground = brush;
		Grid grid = new Grid();
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.Margin = new Thickness(16.0, 4.0, 16.0, 6.0);
		Grid grid2 = grid;
		grid2.Children.Add(tb);
		TranslateTransform slide = null;
		switch (settings.Animation)
		{
		case MessageAnimationMode.Slide:
			slide = new TranslateTransform();
			tb.RenderTransform = slide;
			slide.BeginAnimation(TranslateTransform.XProperty, Ease(40.0, 0.0, 0.35 * num, new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}));
			tb.BeginAnimation(UIElement.OpacityProperty, Ease(0.0, 1.0, 0.25 * num, null));
			break;
		case MessageAnimationMode.Fade:
			tb.BeginAnimation(UIElement.OpacityProperty, Ease(0.0, 1.0, 0.4 * num, new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			}));
			break;
		case MessageAnimationMode.TypeOn:
			TypeOn(parts, brush, num);
			break;
		case MessageAnimationMode.Blur:
		{
			BlurEffect blurEffect = new BlurEffect();
			blurEffect.Radius = 10.0;
			blurEffect.RenderingBias = RenderingBias.Performance;
			BlurEffect blurEffect2 = blurEffect;
			tb.Effect = blurEffect2;
			DoubleAnimation doubleAnimation3 = new DoubleAnimation(10.0, 0.0, TimeSpan.FromSeconds(0.35 * num));
			doubleAnimation3.EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation doubleAnimation4 = doubleAnimation3;
			doubleAnimation4.Completed += delegate
			{
				tb.Effect = null;
			};
			blurEffect2.BeginAnimation(BlurEffect.RadiusProperty, doubleAnimation4);
			tb.BeginAnimation(UIElement.OpacityProperty, Ease(0.0, 1.0, 0.3 * num, null));
			break;
		}
		case MessageAnimationMode.Pulse:
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.35, TimeSpan.FromSeconds(0.75 * num));
			doubleAnimation.AutoReverse = true;
			doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			doubleAnimation.EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			};
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			doubleAnimation2.Freeze();
			LoopWhileVisible(grid2, tb, UIElement.OpacityProperty, doubleAnimation2);
			break;
		}
		}
		grid2.Unloaded += delegate
		{
			tb.BeginAnimation(UIElement.OpacityProperty, null);
			if (slide != null)
			{
				slide.BeginAnimation(TranslateTransform.XProperty, null);
			}
			if (parts != null)
			{
				StopTypeOn(parts);
			}
			tb.Effect = null;
		};
		return grid2;
	}

	private static void LoopWhileVisible(FrameworkElement el, IAnimatable target, DependencyProperty property, AnimationTimeline loop)
	{
		AnimationClock clock = null;
		Action sync = delegate
		{
			bool flag = el.IsLoaded && el.IsVisible;
			if (flag != (clock != null))
			{
				if (flag)
				{
					LoopGuard.Begin(target, "message loop");
					clock = loop.CreateClock();
					target.ApplyAnimationClock(property, clock);
				}
				else
				{
					if (clock.Controller != null)
					{
						clock.Controller.Stop();
					}
					target.ApplyAnimationClock(property, null);
					clock = null;
					LoopGuard.End(target);
				}
			}
		};
		el.Loaded += delegate
		{
			sync();
		};
		el.Unloaded += delegate
		{
			sync();
		};
		el.IsVisibleChanged += delegate
		{
			sync();
		};
	}

	private static DoubleAnimation Ease(double from, double to, double seconds, IEasingFunction ease)
	{
		DoubleAnimation doubleAnimation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds));
		doubleAnimation.EasingFunction = ease;
		DoubleAnimation doubleAnimation2 = doubleAnimation;
		doubleAnimation2.Freeze();
		return doubleAnimation2;
	}

	private static void TypeOn(List<Inline> parts, Brush fg, double f)
	{
		double num = Math.Max(12.0, 28.0 * f);
		for (int i = 0; i < parts.Count; i++)
		{
			KeyTime keyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds((double)(i + 1) * num));
			if (parts[i] is InlineUIContainer inlineUIContainer)
			{
				DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
				doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, keyTime));
				doubleAnimationUsingKeyFrames.Freeze();
				inlineUIContainer.Child.BeginAnimation(UIElement.OpacityProperty, doubleAnimationUsingKeyFrames);
			}
			else
			{
				ObjectAnimationUsingKeyFrames objectAnimationUsingKeyFrames = new ObjectAnimationUsingKeyFrames();
				objectAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteObjectKeyFrame(Brushes.Transparent, KeyTime.FromTimeSpan(TimeSpan.Zero)));
				objectAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteObjectKeyFrame(fg, keyTime));
				objectAnimationUsingKeyFrames.Freeze();
				parts[i].BeginAnimation(TextElement.ForegroundProperty, objectAnimationUsingKeyFrames);
			}
		}
	}

	private static void StopTypeOn(List<Inline> parts)
	{
		foreach (Inline part in parts)
		{
			if (part is InlineUIContainer inlineUIContainer)
			{
				inlineUIContainer.Child.BeginAnimation(UIElement.OpacityProperty, null);
			}
			else
			{
				part.BeginAnimation(TextElement.ForegroundProperty, null);
			}
		}
	}

	private static FrameworkElement BuildScroll(string msg, MessageSettings settings)
	{
		double num = FontSizes[0];
		Canvas canvas = new Canvas();
		canvas.Width = 280.0;
		canvas.Height = 28.0;
		canvas.ClipToBounds = true;
		canvas.HorizontalAlignment = HorizontalAlignment.Center;
		canvas.VerticalAlignment = VerticalAlignment.Center;
		Canvas canvas2 = canvas;
		TextBlock textBlock = BuildText(msg, num, Brushes.White, wrap: false, null);
		double num2 = Math.Max(20.0, Measure(textBlock).Width);
		TranslateTransform move = new TranslateTransform(Math.Max(0.0, (280.0 - num2) / 2.0), 0.0);
		textBlock.Foreground = CreateForegroundBrush(settings, num2);
		textBlock.RenderTransform = move;
		textBlock.HorizontalAlignment = HorizontalAlignment.Left;
		Canvas.SetTop(textBlock, Math.Max(0.0, (28.0 - num * 1.35) / 2.0));
		canvas2.Children.Add(textBlock);
		double num3 = 35.0 + Math.Max(0.0, Math.Min(100.0, settings.Speed)) / 100.0 * 65.0;
		DoubleAnimation doubleAnimation = new DoubleAnimation(280.0, 0.0 - num2, TimeSpan.FromSeconds((280.0 + num2) / num3));
		doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
		DoubleAnimation doubleAnimation2 = doubleAnimation;
		doubleAnimation2.Freeze();
		LoopWhileVisible(canvas2, move, TranslateTransform.XProperty, doubleAnimation2);
		canvas2.Unloaded += delegate
		{
			move.BeginAnimation(TranslateTransform.XProperty, null);
		};
		return canvas2;
	}
}
