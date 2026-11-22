using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Extras;
using WindowsBuddy.Tools;

namespace WindowsBuddy.Tools.Personal;

public abstract class AynPersonalToolBase : IBuddyTool
{
    private FrameworkElement _view;

    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public virtual ToolCategory Category => ToolCategory.Utility;

    public bool IsEnabled { get; set; } = true;

    public int OrderIndex { get; set; } = 30;

    public bool HasCollapsedBadge => false;

    public bool IsInitialized { get; private set; }

    public virtual bool RequiresNetwork => false;

    public ToolState State { get; private set; } = ToolState.Idle;

    public virtual double DesiredWidth => 620.0;

    public virtual double DesiredHeight => 330.0;

    public CollapsedBadgeData GetCollapsedBadge() => null;

    public abstract UIElement CreateIcon(double size, Brush stroke);

    protected abstract FrameworkElement BuildView();

    protected virtual void RefreshView() { }

    public UIElement CreateView() => _view ??= BuildView();

    public UIElement GetHeaderActions() => null;

    public void Initialize()
    {
        if (IsInitialized) return;
        IsInitialized = true;
        RefreshView();
    }

    public void Shutdown() => IsInitialized = false;

    public void OnActivated()
    {
        State = ToolState.Active;
        if (!IsInitialized) Initialize();
        RefreshView();
    }

    public void OnDeactivated() => State = ToolState.Idle;
}

public sealed class BreaksTool : AynPersonalToolBase
{
    private TextBlock _status;
    private readonly AynExtrasService _svc = AynExtrasService.Instance;

    public BreaksTool() => OrderIndex = 2;

    public override string Id => "breaks";
    public override string DisplayName => "Breaks & Meals";

    public override UIElement CreateIcon(double size, Brush stroke) => VectorIcons.CreateBellIcon(size, stroke);

    protected override FrameworkElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        root.Children.Add(Heading("Breaks & Meals"));
        _status = new TextBlock
        {
            Foreground = ThemeTokens.Text,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        };
        root.Children.Add(_status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(ActionButton("Start break", () => _svc.StartBreak()));
        buttons.Children.Add(ActionButton("Skip break", () => _svc.SkipBreak()));
        root.Children.Add(buttons);
        return root;
    }

    protected override void RefreshView()
    {
        if (_status == null) return;
        var cfg = _svc.Config;
        var work = Math.Max(0, cfg.WorkMinutes - _svc.ActiveWork.TotalMinutes);
        _status.Text = _svc.OnBreak
            ? $"On break — {Math.Max(0, cfg.BreakMinutes - _svc.ActiveBreak.TotalMinutes):0} min left."
            : $"Break in {work:0} min. Lunch at {cfg.LunchTime}, dinner at {cfg.DinnerTime}.";
    }

    internal static TextBlock Heading(string text) => new TextBlock
    {
        Text = text,
        Foreground = ThemeTokens.Text,
        FontSize = 20,
        FontWeight = FontWeights.SemiBold
    };

    internal static Button ActionButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Height = 32,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 0, 10, 0),
            Foreground = ThemeTokens.Text,
            Background = ThemeTokens.Surface2,
            BorderBrush = ThemeTokens.Border,
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => action();
        return button;
    }
}

public sealed class SalatTool : AynPersonalToolBase
{
    private readonly TextBlock _list = new TextBlock
    {
        Foreground = ThemeTokens.Text,
        FontSize = 13,
        LineHeight = 26
    };

    public SalatTool() => OrderIndex = 3;

    public override string Id => "salat";
    public override string DisplayName => "Salat Times";

    public override UIElement CreateIcon(double size, Brush stroke) => VectorIcons.CreateClockIcon(size, stroke);

    protected override FrameworkElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        root.Children.Add(BreaksTool.Heading("Salat times"));
        _list.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(_list);
        return root;
    }

    protected override void RefreshView()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var prayer in AynExtrasService.Instance.TodayPrayers())
            sb.AppendLine($"{prayer.Name,-10}  {prayer.Time:h:mm tt}");
        _list.Text = sb.ToString();
    }
}

public sealed class GuardTool : AynPersonalToolBase
{
    private TextBlock _status;
    private readonly AynExtrasService _svc = AynExtrasService.Instance;

    public GuardTool() => OrderIndex = 4;

    public override string Id => "guard";
    public override string DisplayName => "Network Guard";

    public override UIElement CreateIcon(double size, Brush stroke) => VectorIcons.CreateWiFiIcon(size, stroke);

    protected override FrameworkElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        root.Children.Add(BreaksTool.Heading("Network Guard"));
        _status = new TextBlock
        {
            Foreground = ThemeTokens.Text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        };
        root.Children.Add(_status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(BreaksTool.ActionButton("Start / Stop", Toggle));
        root.Children.Add(buttons);
        return root;
    }

    private void Toggle()
    {
        if (_svc.DnsRunning) _svc.StopDns();
        else _svc.StartDnsPublic();
        RefreshView();
    }

    protected override void RefreshView()
    {
        if (_status == null) return;
        _status.Text = _svc.DnsRunning
            ? $"Protecting this network on UDP {_svc.Config.DnsPort}.\n{_svc.DnsBlocked:N0} blocked · {_svc.DnsAllowed:N0} allowed."
            : "Guard is off. Point your router DNS at this PC to protect every device.";
    }
}
