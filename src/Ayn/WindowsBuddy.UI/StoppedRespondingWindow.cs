using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI;

public sealed class StoppedRespondingWindow : Window
{
	public event Action<bool> Choice;

	public StoppedRespondingWindow()
	{
		base.Title = "Ayn";
		base.Width = 360.0;
		base.SizeToContent = SizeToContent.Height;
		base.ResizeMode = ResizeMode.NoResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.Topmost = true;
		base.ShowInTaskbar = true;
		TextBlock element = BuddyTheme.Text("Ayn stopped responding.", 16.0, BuddyTheme.Text100, FontWeights.SemiBold);
		TextBlock textBlock = BuddyTheme.Text("It restarted twice in the last 10 minutes. A report was saved in the logs folder.", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(0.0, 6.0, 0.0, 20.0);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		stackPanel.Children.Add(MakeButton("Quit", primary: false, restart: false));
		stackPanel.Children.Add(MakeButton("Restart", primary: true, restart: true));
		StackPanel child = new StackPanel
		{
			Children = 
			{
				(UIElement)element,
				(UIElement)textBlock,
				(UIElement)stackPanel
			}
		};
		base.Content = new Border
		{
			Background = BuddyTheme.CardFill,
			BorderBrush = BuddyTheme.Hairline,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(16.0),
			Padding = new Thickness(24.0),
			Child = child
		};
		MouseButtonEventHandler value = delegate
		{
			try
			{
				DragMove();
			}
			catch
			{
			}
		};
		base.MouseLeftButtonDown += value;
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Raise(restart: false);
			}
		};
	}

	private Border MakeButton(string label, bool primary, bool restart)
	{
		TextBlock child = BuddyTheme.Text(label, 13.0, primary ? BuddyTheme.PrimaryInk : BuddyTheme.Text100, FontWeights.SemiBold);
		Border border = new Border();
		border.Background = (primary ? BuddyTheme.PrimaryFill : BuddyTheme.Control);
		border.CornerRadius = new CornerRadius(10.0);
		border.Padding = new Thickness(16.0, 8.0, 16.0, 8.0);
		border.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		border.Cursor = Cursors.Hand;
		border.Child = child;
		Border border2 = border;
		border2.MouseLeftButtonUp += delegate
		{
			Raise(restart);
		};
		return border2;
	}

	private void Raise(bool restart)
	{
		this.Choice?.Invoke(restart);
	}
}
