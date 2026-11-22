using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyTheme
{
	public const double SparkStroke = 1.5;

	public const double HeaderTitle = 15.0;

	public const double Hero = 28.0;

	public const double HeroLarge = 28.0;

	public const double HeroTracking = -0.56;

	public const double PageTitle = 20.0;

	public const double Metric = 24.0;

	public const double Title = 13.5;

	public const double Body = 13.0;

	public const double Battery = 11.5;

	public const double RingValue = 13.0;

	public const double DockLabel = 12.0;

	public const double Secondary = 11.5;

	public const double Label = 11.0;

	public const double Caption = 11.0;

	public const double LabelTracking = 0.0;

	public const double IdealTextThreshold = 15.0;

	public const double PanelWidth = 752.0;

	public const double PanelHeight = 352.0;

	public const double PanelPadding = 12.0;

	public const double PanelTop = 10.0;

	public const double HeaderHeight = 30.0;

	public const double HeaderToContent = 10.0;

	public const double ContentToDock = 6.0;

	public const double DockBottom = 6.0;

	public const double ContentWidth = 728.0;

	public const double ContentHeight = 242.0;

	public const double CardPadding = 14.0;

	public const double CardPaddingBottom = 12.0;

	public const double Gap = 10.0;

	public const double HeaderGap = 6.0;

	public const double RowGap = 8.0;

	public const double Inline = 10.0;

	public const double RadiusPanel = 28.0;

	public const double RadiusCard = 16.0;

	public const double RadiusRow = 10.0;

	public const double IconHeader = 12.0;

	public const double Icon = 18.0;

	public const double IconHero = 26.0;

	public const double ControlSize = 28.0;

	public const double ControlIcon = 13.0;

	public const double HitTarget = 28.0;

	public const double RowHeight = 32.0;

	public const double DockHeight = 48.0;

	public const double DockPadding = 5.0;

	public const double DockButton = 36.0;

	public const double DockActive = 36.0;

	public const double RingSize = 52.0;

	public const double RingStroke = 5.0;

	public const double BarWidth = 3.0;

	public const double HoverLift = 0.0;

	public const double EntryOffset = 8.0;

	public const double WidgetHeaderHeight = 22.0;

	public const double WidgetLabel = 11.5;

	public const double WidgetTracking = 0.69;

	public const double WidgetLabelOpacity = 0.55;

	public const double PressScale = 0.98;

	public static readonly Brush Canvas = ThemeTokens.Bg;

	public static readonly Brush CardFill = ThemeTokens.Surface;

	public static readonly Brush CardFillHover = ThemeTokens.Surface2;

	public static readonly Brush CardBorder = Brushes.Transparent;

	public static readonly Brush CardBorderHover = Brushes.Transparent;

	public static readonly Brush Control = ThemeTokens.Surface2;

	public static readonly Brush ControlActive = ThemeTokens.Surface3;

	public static readonly Brush ControlHover = ControlActive;

	public static readonly Brush Row = Control;

	public static readonly Brush RowHover = ControlActive;

	public static readonly Brush Hairline = ThemeTokens.Border;

	public static readonly Brush DockSurface = ThemeTokens.Surface;

	public static readonly Brush DockSeparator = ThemeTokens.BorderStrong;

	public static readonly Brush FocusRing = ThemeTokens.Focus;

	public static readonly Brush Text100 = ThemeTokens.Text;

	public static readonly Brush Text92 = ThemeTokens.Text;

	public static readonly Brush Text50 = ThemeTokens.Text2;

	public static readonly Brush Text30 = ThemeTokens.Text3;

	public static readonly Brush DockIcon = ThemeTokens.IconMuted;

	public static readonly Brush WeatherIconInk = ThemeTokens.Icon;

	public static readonly Brush Placeholder = Text30;

	public static readonly Brush Ink = ThemeTokens.Inverse;

	public static readonly Brush Track = ThemeTokens.Track;

	public static readonly Brush BarPast = ThemeTokens.Bar;

	public static readonly Brush BarEmpty = ThemeTokens.Bar;

	public static readonly Brush Mark = ThemeTokens.Accent;

	public static readonly Brush SelectedFill = ThemeTokens.AccentSoft;

	public static readonly Brush SelectedInk = ThemeTokens.Text;

	public static readonly Brush SelectedEdge = ThemeTokens.Accent;

	public static readonly Brush PrimaryFill = ThemeTokens.AccentStrong;

	public static readonly Brush PrimaryInk = ThemeTokens.AccentFg;

	public static readonly Brush Alert = ThemeTokens.Danger;

	public static readonly Brush NoteGrey = NoteInk(10592678, 7237235);

	public static readonly Brush NoteRed = NoteInk(15760499, 13186860);

	public static readonly Brush NoteOrange = NoteInk(15901022, 11753984);

	public static readonly Brush NoteYellow = NoteInk(15585130, 8414720);

	public static readonly Brush NoteGreen = NoteInk(7653775, 1998661);

	public static readonly Brush NoteBlue = NoteInk(6990066, 681686);

	public static readonly Brush NotePurple = NoteInk(11703276, 8013257);

	public static readonly Brush SparkLine = ThemeTokens.Chart1;

	public static readonly Brush SparkFill = ThemeTokens.AccentSoft;

	public static readonly Duration Hover = new Duration(TimeSpan.FromMilliseconds(120.0));

	public static readonly Duration Slide = new Duration(TimeSpan.FromMilliseconds(180.0));

	public static readonly Duration Value = new Duration(TimeSpan.FromMilliseconds(300.0));

	public static readonly Duration CrossFade = new Duration(TimeSpan.FromMilliseconds(200.0));

	public static readonly Duration EntryMove = new Duration(TimeSpan.FromMilliseconds(220.0));

	public static readonly Duration EntryFade = new Duration(TimeSpan.FromMilliseconds(180.0));

	public static readonly TimeSpan EntryStagger = TimeSpan.FromMilliseconds(35.0);

	public static readonly TimeSpan FeedbackHold = TimeSpan.FromMilliseconds(1200.0);

	private static readonly IEasingFunction _easeOut = CreateEaseOut();

	public static readonly Duration Press = new Duration(TimeSpan.FromMilliseconds(130.0));

	private static Dictionary<Brush, Color[]> _noteVariants;

	public static FontFamily Font => AynType.Family(TypeSurface.Capsule, 13.0);

	public static FontFamily DisplayFont => AynType.Family(TypeSurface.Capsule, 28.0);

	public static IEasingFunction EaseOut => _easeOut;

	public static FontFamily FontFor(double size)
	{
		return AynType.Family(TypeSurface.Capsule, size);
	}

	public static T Crisp<T>(T element) where T : FrameworkElement
	{
		element.UseLayoutRounding = true;
		element.SnapsToDevicePixels = true;
		return element;
	}

	public static void ApplyTextMode(DependencyObject element, double fontSize)
	{
		AynType.ApplyTextMode(element, fontSize);
	}

	public static TextBlock Text(string text, double size, Brush ink, FontWeight weight)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text ?? string.Empty;
		textBlock.FontFamily = FontFor(size);
		textBlock.FontSize = size;
		textBlock.FontWeight = AynType.Calm(weight, size);
		textBlock.Foreground = ink;
		textBlock.TextWrapping = TextWrapping.NoWrap;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		ApplyTextMode(textBlock2, size);
		return textBlock2;
	}

	public static TextBlock HeroText(string text)
	{
		return HeroText(text, 28.0);
	}

	public static TextBlock HeroText(string text, double size)
	{
		TextBlock textBlock = Text(text, size, Text100, FontWeights.SemiBold);
		textBlock.LineHeight = size;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.TextTrimming = TextTrimming.None;
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		return textBlock;
	}

	public static BuddyLabel TrackedHero(string text)
	{
		BuddyLabel buddyLabel = new BuddyLabel(string.Empty, Text100, 28.0, AynType.Semibold);
		buddyLabel.FontFamily = DisplayFont;
		buddyLabel.Uppercase = false;
		buddyLabel.TabularDigits = true;
		buddyLabel.Tracking = -0.56;
		buddyLabel.Text = text;
		ApplyTextMode(buddyLabel, 28.0);
		return buddyLabel;
	}

	public static TextBlock BodyText(string text)
	{
		return Text(text, 13.0, Text92, FontWeights.Normal);
	}

	public static TextBlock SecondaryText(string text)
	{
		return Text(text, 11.5, Text50, FontWeights.Normal);
	}

	public static BuddyLabel LabelText(string text)
	{
		BuddyLabel buddyLabel = new BuddyLabel(string.Empty, Text50, 11.0, AynType.Medium);
		buddyLabel.FontFamily = Font;
		buddyLabel.Uppercase = false;
		buddyLabel.Tracking = 0.0;
		buddyLabel.Text = text;
		ApplyTextMode(buddyLabel, 11.0);
		return buddyLabel;
	}

	public static FrameworkElement CardHeader(string fluentIcon, string label)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.Height = 12.0;
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement frameworkElement = FluentIcons.Create(fluentIcon, 12.0, Text50, filled: true);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		stackPanel2.Children.Add(frameworkElement);
		BuddyLabel buddyLabel = LabelText(label);
		buddyLabel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(buddyLabel);
		return stackPanel2;
	}

	public static FrameworkElement WidgetHeader(string fluentIcon, string label, string widget)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.Height = 22.0;
		StackPanel stackPanel2 = stackPanel;
		Border border = new Border();
		border.Width = 22.0;
		border.Height = 22.0;
		border.CornerRadius = new CornerRadius(6.0);
		border.Background = ThemeTokens.Widget(widget);
		border.VerticalAlignment = VerticalAlignment.Center;
		border.Margin = new Thickness(0.0, 0.0, 9.0, 0.0);
		Border border2 = border;
		FrameworkElement frameworkElement = FluentIcons.Create(fluentIcon, 13.0, (widget == "notes") ? ThemeTokens.TileGlyphOnLight : ThemeTokens.TileGlyph, filled: true);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		border2.Child = frameworkElement;
		stackPanel2.Children.Add(border2);
		FrameworkElement frameworkElement2 = FluentIcons.Create(fluentIcon, 14.0, ThemeTokens.Text, filled: false);
		frameworkElement2.Opacity = 0.55;
		frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement2.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		stackPanel2.Children.Add(frameworkElement2);
		BuddyLabel buddyLabel = LabelText(label.ToUpperInvariant());
		buddyLabel.FontSize = 11.5;
		buddyLabel.Foreground = ThemeTokens.Text;
		buddyLabel.Opacity = 0.55;
		buddyLabel.Tracking = 0.69;
		buddyLabel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(buddyLabel);
		ApplyWidgetHeader(stackPanel2);
		ThemeTokens.Subscribe(stackPanel2, ApplyWidgetHeader);
		return stackPanel2;
	}

	private static void ApplyWidgetHeader(StackPanel sp)
	{
		bool useIconTiles = ThemeTokens.UseIconTiles;
		sp.Children[0].Visibility = ((!useIconTiles) ? Visibility.Collapsed : Visibility.Visible);
		sp.Children[1].Visibility = (useIconTiles ? Visibility.Collapsed : Visibility.Visible);
	}

	public static void Animate(IAnimatable target, DependencyProperty property, double to, Duration duration)
	{
		if (!CapsuleMotion.AnimationsEnabled)
		{
			target.BeginAnimation(property, null);
			if (target is DependencyObject dependencyObject)
			{
				dependencyObject.SetValue(property, to);
			}
		}
		else
		{
			target.BeginAnimation(property, new DoubleAnimation(to, duration)
			{
				EasingFunction = EaseOut
			});
		}
	}

	public static T Pressable<T>(T el) where T : UIElement
	{
		if (el == null)
		{
			return null;
		}
		if (el.RenderTransform != null && !el.RenderTransform.Value.IsIdentity)
		{
			return el;
		}
		ScaleTransform st = new ScaleTransform(1.0, 1.0);
		el.RenderTransformOrigin = new Point(0.5, 0.5);
		el.RenderTransform = st;
		el.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, (MouseButtonEventHandler)delegate
		{
			PressTo(st, 0.98);
		}, handledEventsToo: true);
		el.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, (MouseButtonEventHandler)delegate
		{
			PressTo(st, 1.0);
		}, handledEventsToo: true);
		el.MouseLeave += delegate
		{
			PressTo(st, 1.0);
		};
		return el;
	}

	public static Border SoftHover(Border surface, UIElement hoverSource = null)
	{
		if (surface == null)
		{
			return null;
		}
		SolidColorBrush fill = new SolidColorBrush(Colors.Transparent);
		surface.Background = fill;
		UIElement uIElement = hoverSource ?? surface;
		Action<bool> to = delegate(bool on)
		{
			Color color = ColorOf(ThemeTokens.Selection);
			Color color2 = (on ? color : Color.FromArgb(0, color.R, color.G, color.B));
			if (!CapsuleMotion.AnimationsEnabled)
			{
				fill.BeginAnimation(SolidColorBrush.ColorProperty, null);
				fill.Color = color2;
			}
			else
			{
				fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color2, Press)
				{
					EasingFunction = EaseOut
				});
			}
		};
		uIElement.MouseEnter += delegate
		{
			to(obj: true);
		};
		uIElement.MouseLeave += delegate
		{
			to(obj: false);
		};
		return surface;
	}

	private static void PressTo(ScaleTransform st, double to)
	{
		if (!CapsuleMotion.AnimationsEnabled)
		{
			st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			double scaleX = (st.ScaleY = 1.0);
			st.ScaleX = scaleX;
		}
		else if (!(Math.Abs(st.ScaleX - to) < 0.0001) || st.HasAnimatedProperties)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(to, Press);
			doubleAnimation.EasingFunction = EaseOut;
			DoubleAnimation animation = doubleAnimation;
			st.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			st.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}
	}

	public static Color White(byte alpha)
	{
		return Color.FromArgb(alpha, byte.MaxValue, byte.MaxValue, byte.MaxValue);
	}

	public static bool NoteInkMatches(Brush ink, Color c)
	{
		if (ink != null && _noteVariants != null && _noteVariants.TryGetValue(ink, out var value))
		{
			if (!(value[0] == c))
			{
				return value[1] == c;
			}
			return true;
		}
		return false;
	}

	private static Brush NoteInk(int dark, int light)
	{
		Func<int, Color> c = (int v) => Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
		SolidColorBrush b = new SolidColorBrush(ThemeTokens.IsLight ? c(light) : c(dark));
		if (_noteVariants == null)
		{
			_noteVariants = new Dictionary<Brush, Color[]>();
		}
		_noteVariants[b] = new Color[2]
		{
			c(dark),
			c(light)
		};
		ThemeTokens.ThemeChanged += delegate
		{
			b.Color = (ThemeTokens.IsLight ? c(light) : c(dark));
		};
		return b;
	}

	private static Brush Solid(Color c)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(c);
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static Brush Hex(int rgb)
	{
		return Solid(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
	}

	public static Color ColorOf(Brush b)
	{
		if (ThemeTokens.IsToken(b))
		{
			return ThemeTokens.Target(b);
		}
		if (!(b is SolidColorBrush solidColorBrush))
		{
			return Colors.Transparent;
		}
		return solidColorBrush.Color;
	}

	private static IEasingFunction CreateEaseOut()
	{
		CubicEase cubicEase = new CubicEase();
		cubicEase.EasingMode = EasingMode.EaseOut;
		CubicEase cubicEase2 = cubicEase;
		cubicEase2.Freeze();
		return cubicEase2;
	}
}
