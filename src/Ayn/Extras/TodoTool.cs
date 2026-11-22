using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Personal;

public sealed class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public static class TodoStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static string Path => System.IO.Path.Combine(AppPaths.DataDir, "todo.json");

    public static List<TodoItem> Load()
    {
        try
        {
            if (File.Exists(Path)) return JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(Path), Options) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(List<TodoItem> items)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(Path, JsonSerializer.Serialize(items, Options));
        }
        catch { }
    }
}

public sealed class TodoTool : AynPersonalToolBase
{
    private ListBox _list;
    private System.Windows.Controls.TextBox _input;

    public TodoTool() => OrderIndex = 12;

    public override string Id => "todo";
    public override string DisplayName => "To-Do List";

    public override double DesiredWidth => 560.0;
    public override double DesiredHeight => 320.0;

    public override UIElement CreateIcon(double size, Brush stroke) => VectorIcons.CreateClipboardIcon(size, stroke);

    protected override FrameworkElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
        root.Children.Add(BreaksTool.Heading("To-Do List"));

        _list = new ListBox
        {
            Height = 190,
            Margin = new Thickness(0, 12, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ThemeTokens.Text
        };
        root.Children.Add(_list);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        _input = new System.Windows.Controls.TextBox
        {
            Width = 350,
            Height = 30,
            Background = ThemeTokens.Surface2,
            Foreground = ThemeTokens.Text,
            BorderBrush = ThemeTokens.Border
        };
        _input.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Add(); };
        row.Children.Add(_input);
        row.Children.Add(BreaksTool.ActionButton("Add", Add));
        row.Children.Add(BreaksTool.ActionButton("Clear done", ClearDone));
        root.Children.Add(row);

        Render();
        return root;
    }

    private void Render()
    {
        if (_list == null) return;
        _list.Items.Clear();
        foreach (var item in TodoStore.Load().OrderBy(i => i.Done))
        {
            var panel = new DockPanel { LastChildFill = true, Margin = new Thickness(2, 4, 2, 4) };
            var check = new CheckBox
            {
                IsChecked = item.Done,
                Foreground = ThemeTokens.Text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var captured = item;
            check.Click += (_, _) =>
            {
                var items = TodoStore.Load();
                var found = items.FirstOrDefault(i => i.Id == captured.Id);
                if (found != null) found.Done = check.IsChecked == true;
                TodoStore.Save(items);
                Render();
            };
            DockPanel.SetDock(check, Dock.Left);
            panel.Children.Add(check);

            var text = new TextBlock
            {
                Text = item.Text,
                Foreground = item.Done ? ThemeTokens.Text3 : ThemeTokens.Text,
                TextDecorations = item.Done ? TextDecorations.Strikethrough : null,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            panel.Children.Add(text);

            var remove = new Button
            {
                Content = "×",
                Width = 24,
                Height = 24,
                Background = Brushes.Transparent,
                Foreground = ThemeTokens.Text3,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            remove.Click += (_, _) =>
            {
                var items = TodoStore.Load();
                items.RemoveAll(i => i.Id == captured.Id);
                TodoStore.Save(items);
                Render();
            };
            DockPanel.SetDock(remove, Dock.Right);
            panel.Children.Insert(0, remove);
            _list.Items.Add(panel);
        }
    }

    private void Add()
    {
        var text = _input?.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        var items = TodoStore.Load();
        items.Add(new TodoItem { Text = text });
        TodoStore.Save(items);
        _input.Text = "";
        Render();
    }

    private void ClearDone()
    {
        var items = TodoStore.Load();
        items.RemoveAll(i => i.Done);
        TodoStore.Save(items);
        Render();
    }
}
