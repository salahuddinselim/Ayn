using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Extras;

namespace WindowsBuddy.Tools.Personal;

public sealed class GlassHeroPanel : UserControl
{
    private readonly Border _frame;
    private readonly Border _card;
    private readonly Border _orb1;
    private readonly Border _orb2;
    private readonly LinearGradientBrush _background;
    private readonly SolidColorBrush _orb1Fill;
    private readonly SolidColorBrush _orb2Fill;
    private readonly SolidColorBrush _frameFill;
    private readonly SolidColorBrush _frameEdge;
    private readonly SolidColorBrush _cardFill;
    private readonly SolidColorBrush _cardEdge;
    private readonly SolidColorBrush _modeFill;
    private readonly SolidColorBrush _modeEdge;
    private readonly SolidColorBrush _primaryText;
    private readonly SolidColorBrush _secondaryText;
    private readonly BlurEffect _orb1Blur;
    private readonly BlurEffect _orb2Blur;
    private readonly DropShadowEffect _cardShadow;
    private TextBlock _metric;
    private readonly Slider _minutes;
    private readonly Border _mode;
    private int _modeIndex;
    private static readonly string[] Modes = ["Deep work", "Study", "Pomodoro"];
    private readonly EventHandler _themeHandler;

    public GlassHeroPanel()
    {
        Height = 340;
        MinWidth = 620;
        ClipToBounds = true;

        _background = new LinearGradientBrush(Colors.Black, Colors.Black, 35);
        _orb1Fill = new SolidColorBrush(Colors.Transparent);
        _orb2Fill = new SolidColorBrush(Colors.Transparent);
        _frameFill = new SolidColorBrush(Colors.Transparent);
        _frameEdge = new SolidColorBrush(Colors.Transparent);
        _cardFill = new SolidColorBrush(Colors.Transparent);
        _cardEdge = new SolidColorBrush(Colors.Transparent);
        _modeFill = new SolidColorBrush(Colors.Transparent);
        _modeEdge = new SolidColorBrush(Colors.Transparent);
        _primaryText = new SolidColorBrush(Colors.White);
        _secondaryText = new SolidColorBrush(Colors.White);
        _orb1Blur = new BlurEffect { Radius = 16, RenderingBias = RenderingBias.Performance };
        _orb2Blur = new BlurEffect { Radius = 14, RenderingBias = RenderingBias.Performance };
        _cardShadow = new DropShadowEffect
        {
            BlurRadius = 16,
            ShadowDepth = 6,
            Direction = 270,
            Color = Colors.Black,
            Opacity = 0.25,
            RenderingBias = RenderingBias.Performance
        };

        var root = new Grid { Background = _background };

        _orb1 = new Border { Width = 260, Height = 260, CornerRadius = new CornerRadius(130), Background = _orb1Fill, Effect = _orb1Blur };
        _orb2 = new Border { Width = 220, Height = 220, CornerRadius = new CornerRadius(110), Background = _orb2Fill, Effect = _orb2Blur };
        Canvas.SetLeft(_orb1, -60);
        Canvas.SetTop(_orb1, -80);
        Canvas.SetRight(_orb2, 40);
        Canvas.SetBottom(_orb2, -60);
        var canvas = new Canvas();
        canvas.Children.Add(_orb1);
        canvas.Children.Add(_orb2);
        root.Children.Add(canvas);

        _frame = new Border
        {
            Margin = new Thickness(18),
            CornerRadius = new CornerRadius(36),
            Padding = new Thickness(26, 20, 26, 20),
            Background = _frameFill,
            BorderBrush = _frameEdge,
            BorderThickness = new Thickness(1)
        };
        var frameGrid = new Grid();
        frameGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        frameGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var nav = new Grid();
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(new TextBlock { Text = "Ayn", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = _primaryText });
        brand.Children.Add(new TextBlock
        {
            Text = "  العين",
            FontSize = 14,
            Foreground = _secondaryText,
            Margin = new Thickness(6, 4, 0, 0)
        });
        nav.Children.Add(brand);
        var links = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        foreach (var label in new[] { "Features", "Themes", "About" })
            links.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = _secondaryText, Margin = new Thickness(10, 0, 10, 0) });
        Grid.SetColumn(links, 1);
        nav.Children.Add(links);
        var cta = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16, 7, 16, 7),
            VerticalAlignment = VerticalAlignment.Center
        };
        cta.Child = new TextBlock
        {
            Text = "Get Ayn",
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 24))
        };
        Grid.SetColumn(cta, 2);
        nav.Children.Add(cta);
        frameGrid.Children.Add(nav);

        var hero = new Grid();
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock
        {
            Text = "The Notch,\nReimagined.",
            FontSize = 42,
            LineHeight = 46,
            Foreground = _primaryText,
            FontWeight = FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap
        });
        left.Children.Add(new TextBlock
        {
            Text = "A calm, glassy command center for your day.\nBreaks, prayer times, tasks and focus — all in one place.",
            FontSize = 13,
            Foreground = _secondaryText,
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 330
        });
        left.Children.Add(new TextBlock
        {
            Text = "★ 4.9  ·  Free & open source",
            FontSize = 12,
            Foreground = _secondaryText,
            Margin = new Thickness(0, 18, 0, 0)
        });
        hero.Children.Add(left);

        _card = new Border
        {
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(18),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 244,
            Background = _cardFill,
            BorderBrush = _cardEdge,
            BorderThickness = new Thickness(1),
            Effect = _cardShadow
        };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = "Focus session",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = _primaryText
        });
        _mode = new Border
        {
            Height = 32,
            Margin = new Thickness(0, 12, 0, 0),
            CornerRadius = new CornerRadius(8),
            Background = _modeFill,
            BorderBrush = _modeEdge,
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var modeText = new TextBlock
        {
            Text = Modes[0],
            Foreground = _primaryText,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        _mode.Child = modeText;
        _mode.MouseLeftButtonUp += (_, _) =>
        {
            _modeIndex = (_modeIndex + 1) % Modes.Length;
            modeText.Text = Modes[_modeIndex];
        };
        cardStack.Children.Add(_mode);
        _minutes = new Slider { Minimum = 5, Maximum = 120, Value = 25, Margin = new Thickness(0, 14, 0, 0) };
        cardStack.Children.Add(_minutes);
        _metric = new TextBlock { Text = "25 minutes", FontSize = 22, Foreground = _primaryText, Margin = new Thickness(0, 8, 0, 12) };
        cardStack.Children.Add(_metric);
        _minutes.ValueChanged += (_, e) => _metric.Text = $"{e.NewValue:0} minutes";
        var button = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            Height = 38,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Child = new TextBlock
        {
            Text = "Start focus",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        };
        button.MouseLeftButtonUp += (_, _) =>
        {
            AynExtrasService.Instance.Config.WorkMinutes = (int)_minutes.Value;
            AynExtrasService.Instance.Config.Save();
            AynExtrasService.Instance.StartBreak();
            _metric.Text = "Break started";
        };
        cardStack.Children.Add(button);
        _card.Child = cardStack;
        Grid.SetColumn(_card, 1);
        hero.Children.Add(_card);
        frameGrid.Children.Add(hero);
        Grid.SetRow(hero, 1);
        _frame.Child = frameGrid;
        root.Children.Add(_frame);
        Content = root;

        _themeHandler = (_, _) => Dispatcher.BeginInvoke(new Action(ApplyGlass));
        ThemeTokens.ThemeChanged += _themeHandler;
        Unloaded += (_, _) => ThemeTokens.ThemeChanged -= _themeHandler;
        ApplyGlass();
    }

    public void ApplyGlass()
    {
        var cfg = AynExtrasService.Instance.Config;
        var bg = ThemeTokens.Target(ThemeTokens.GlassFill);
        var deep = ThemeTokens.Target(ThemeTokens.Bg);
        _background.GradientStops[0].Color = Mix(deep, Colors.Black, 0.35);
        _background.GradientStops[1].Color = Mix(bg, ThemeTokens.Target(ThemeTokens.Accent), 0.12);

        var accent = ThemeTokens.Target(ThemeTokens.Accent);
        var chart = ThemeTokens.Target(ThemeTokens.Chart2);
        _orb1Fill.Color = Color.FromArgb(130, accent.R, accent.G, accent.B);
        _orb2Fill.Color = Color.FromArgb(110, chart.R, chart.G, chart.B);
        _orb1Blur.Radius = Math.Min(18, cfg.GlassBlur * 0.55);
        _orb2Blur.Radius = Math.Min(16, cfg.GlassBlur * 0.5);

        var edge = ThemeTokens.Target(ThemeTokens.GlassEdge);
        var sheen = ThemeTokens.Target(ThemeTokens.GlassSheen);
        _frameFill.Color = Color.FromArgb((byte)(bg.A * cfg.GlassOuterOpacity / 0.08 * 0.55), bg.R, bg.G, bg.B);
        _frameEdge.Color = Color.FromArgb((byte)Math.Min(255, edge.A * cfg.GlassBorderOpacity / 0.2), edge.R, edge.G, edge.B);
        _cardFill.Color = Color.FromArgb((byte)(cfg.GlassCardOpacity * 255), deep.R, deep.G, deep.B);
        _cardEdge.Color = _frameEdge.Color;
        _modeFill.Color = Color.FromArgb((byte)Math.Min(200, bg.A + 40), bg.R, bg.G, bg.B);
        _modeEdge.Color = sheen;
        _cardShadow.BlurRadius = Math.Min(18, cfg.GlassBlur);
        GlassShell.RefreshAll();
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
