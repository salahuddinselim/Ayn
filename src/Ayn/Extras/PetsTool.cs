using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Extras;

namespace WindowsBuddy.Tools.Personal;

public sealed class PetsTool : AynPersonalToolBase
{
    private CompanionPetCanvas _pet;
    private TextBlock _name;
    private static readonly string[] Kinds = ["ditto", "cat", "husky", "pikachu", "doraemon", "dots", "grok", "robot"];

    public PetsTool() => OrderIndex = 9;

    public override string Id => "pets";
    public override string DisplayName => "Pets";

    public override double DesiredWidth => 480.0;
    public override double DesiredHeight => 300.0;

    public override UIElement CreateIcon(double size, Brush stroke) => VectorIcons.CreateEmojiIcon(size, stroke);

    protected override FrameworkElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
        root.Children.Add(BreaksTool.Heading("Pets"));
        _name = new TextBlock
        {
            Text = Label(AynExtrasService.Instance.Config.PetKind),
            Foreground = ThemeTokens.Text2,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };
        root.Children.Add(_name);
        _pet = new CompanionPetCanvas { Kind = AynExtrasService.Instance.Config.PetKind };
        root.Children.Add(_pet);

        var wrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var kind in Kinds)
        {
            var button = new Button
            {
                Content = Label(kind),
                Margin = new Thickness(3),
                Padding = new Thickness(9, 4, 9, 4),
                FontSize = 11,
                Foreground = ThemeTokens.Text,
                Background = ThemeTokens.Surface2,
                BorderBrush = ThemeTokens.Border,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var chosen = kind;
            button.Click += (_, _) =>
            {
                _pet.Kind = chosen;
                _name.Text = Label(chosen);
                AynExtrasService.Instance.Config.PetKind = chosen;
                AynExtrasService.Instance.Config.Save();
            };
            wrap.Children.Add(button);
        }
        root.Children.Add(wrap);
        return root;
    }

    private static string Label(string kind) => kind switch
    {
        "cat" => "Cat",
        "husky" => "Husky",
        "pikachu" => "Pikachu",
        "doraemon" => "Doraemon",
        "dots" => "Dots",
        "grok" => "Grok",
        "robot" => "Robot",
        _ => "Ditto"
    };
}
