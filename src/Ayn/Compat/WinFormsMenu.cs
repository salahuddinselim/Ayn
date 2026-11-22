using System.Collections.Generic;

namespace System.Windows.Forms;

public class MenuItem
{
    public MenuItem() { }

    public MenuItem(string text) => Text = text;

    public MenuItem(string text, EventHandler onClick)
    {
        Text = text;
        Click += onClick;
    }

    public string Text { get; set; } = "";

    public bool Checked { get; set; }

    public bool Enabled { get; set; } = true;

    public MenuItemCollection MenuItems { get; } = new();

    public event EventHandler? Click;

    public void PerformClick() => Click?.Invoke(this, EventArgs.Empty);
}

public sealed class MenuItemCollection : List<MenuItem>
{
    public void Add(string text) => Add(new MenuItem(text));

    public void Add(string text, EventHandler handler) => Add(new MenuItem(text, handler));
}

public sealed class ContextMenu
{
    public MenuItemCollection MenuItems { get; } = new();

    public event EventHandler? Popup;

    public void PerformPopup() => Popup?.Invoke(this, EventArgs.Empty);
}
