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
    private TextBlock _metric;
    private readonly Slider _minutes;
    private readonly Border _mode;
    private int _modeIndex;
    private static readonly string[] Modes = ["Deep work", "Study", "Pomodoro"];

    public GlassHeroPanel()
    {
        Height = 340;
        MinWidth = 620;
        ClipToBounds = true;

        var root = new Grid();
        root.Background = new LinearGradientBrush(
            Color.FromRgb(58, 46, 38),
            Color.FromRgb(122, 96, 74),
            35);

        _orb1 = new Border { Width = 260, Height = 260, CornerRadius = new CornerRadius(130), Background = new SolidColorBrush(Color.FromArgb(140, 214, 166, 120)), Effect = new BlurEffect { Radius = 40 } };
        _orb2 = new Border { Width = 220, Height = 220, CornerRadius = new CornerRadius(110), Background = new SolidColorBrush(Color.FromArgb(120, 130, 160, 190)), Effect = new BlurEffect { Radius = 36 } };
        Canvas.SetLeft(_orb1, -60); Canvas.SetTop(_orb1, -80);
        Canvas.SetRight(_orb2, 40); Canvas.SetBottom(_orb2, -60);
        var canvas = new Canvas();
        canvas.Children.Add(_orb1); canvas.Children.Add(_orb2);
        root.Children.Add(canvas);

        _frame = new Border
        {
            Margin = new Thickness(18),
            CornerRadius = new CornerRadius(36),
            Padding = new Thickness(26, 20, 26, 20)
        };
        var frameGrid = new Grid();
        frameGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        frameGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var nav = new Grid();
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(new TextBlock { Text = "Ayn", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        brand.Children.Add(new TextBlock { Text = "  العين", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), Margin = new Thickness(6, 4, 0, 0) });
        nav.Children.Add(brand);
        var links = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
        foreach (var label in new[] { "Features", "Themes", "About" })
            links.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), Margin = new Thickness(10, 0, 10, 0) });
        nav.Children.Add(links);
        Grid.SetColumn(links, 1);
        var cta = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 7, 16, 7), VerticalAlignment = VerticalAlignment.Center };
        cta.Child = new TextBlock { Text = "Get Ayn", FontSize = 12, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 24)) };
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
            Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            FontWeight = FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Color = Colors.Black, Opacity = 0.35 }
        });
        left.Children.Add(new TextBlock
        {
            Text = "A calm, glassy command center for your day.\nBreaks, prayer times, tasks and focus — all in one place.",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromArgb(205, 255, 255, 255)),
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 330,
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Color = Colors.Black, Opacity = 0.3 }
        });
        left.Children.Add(new TextBlock
        {
            Text = "★ 4.9  ·  Free & open source",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            Margin = new Thickness(0, 18, 0, 0)
        });
        hero.Children.Add(left);

        _card = new Border { CornerRadius = new CornerRadius(24), Padding = new Thickness(18), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, Width = 244 };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock { Text = "Focus session", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        _mode = new Border
        {
            Height = 32,
            Margin = new Thickness(0, 12, 0, 0),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var modeText = new TextBlock
        {
            Text = Modes[0],
            Foreground = Brushes.White,
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
        _metric = new TextBlock { Text = "25 minutes", FontSize = 22, Foreground = Brushes.White, Margin = new Thickness(0, 8, 0, 12) };
        cardStack.Children.Add(_metric);
        _minutes.ValueChanged += (_, e) => _metric.Text = $"{e.NewValue:0} minutes";
        var button = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Height = 38, Cursor = System.Windows.Input.Cursors.Hand };
        button.Child = new TextBlock { Text = "Start focus", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 24)), FontWeight = FontWeights.SemiBold, FontSize = 13 };
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

        ApplyGlass();
        ThemeTokens.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyGlass));
    }

    public void ApplyGlass()
    {
        var cfg = AynExtrasService.Instance.Config;
        _frame.Background = new SolidColorBrush(Color.FromArgb((byte)(cfg.GlassOuterOpacity * 255), 255, 255, 255));
        _frame.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(cfg.GlassBorderOpacity * 255), 255, 255, 255));
        _frame.BorderThickness = new Thickness(1);
        _card.Background = new SolidColorBrush(Color.FromArgb((byte)(cfg.GlassCardOpacity * 255), 15, 20, 28));
        _card.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(cfg.GlassBorderOpacity * 255), 255, 255, 255));
        _card.BorderThickness = new Thickness(1);
        _card.Effect = new DropShadowEffect { BlurRadius = cfg.GlassBlur, ShadowDepth = 8, Color = Colors.Black, Opacity = 0.45 };
        _orb1.Effect = new BlurEffect { Radius = cfg.GlassBlur };
        _orb2.Effect = new BlurEffect { Radius = cfg.GlassBlur };
        GlassShell.RefreshAll();
    }
}
