using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddyUndoToast : Border
{
	private readonly TextBlock _message;

	private readonly DispatcherTimer _timer;

	private readonly Border _undoHit;

	private Action _undo;

	private Action _commit;

	public bool IsPending => _commit != null;

	public string MessageText => _message.Text;

	public BuddyUndoToast()
		: this(4.0)
	{
	}

	public BuddyUndoToast(double seconds)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		_message = BuddyTheme.BodyText(string.Empty);
		_message.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Children.Add(_message);
		TextBlock textBlock = BuddyTheme.Text("Undo", 13.0, BuddyTheme.Text100, AynType.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Border border = (_undoHit = new Border
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Padding = new Thickness(12.0, 8.0, 4.0, 8.0),
			Child = textBlock,
			ToolTip = BuddyTooltip.Create("Undo (Ctrl+Z)")
		});
		border.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			Undo();
		};
		stackPanel.Children.Add(border);
		BuddyTheme.Crisp(this);
		base.Background = BuddyTheme.CardFill;
		base.BorderBrush = BuddyTheme.CardBorderHover;
		base.BorderThickness = new Thickness(1.0);
		base.CornerRadius = new CornerRadius(10.0);
		base.Padding = new Thickness(14.0, 0.0, 8.0, 0.0);
		base.Height = 36.0;
		base.HorizontalAlignment = HorizontalAlignment.Center;
		base.VerticalAlignment = VerticalAlignment.Bottom;
		base.Margin = new Thickness(0.0, 0.0, 0.0, 2.0);
		Child = stackPanel;
		base.Visibility = Visibility.Collapsed;
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(seconds)
		};
		_timer.Tick += delegate
		{
			Commit();
		};
	}

	public void Show(string message, Action undo, Action commit)
	{
		Commit();
		_message.Text = message;
		_undoHit.Visibility = Visibility.Visible;
		_undo = undo;
		_commit = commit;
		base.Visibility = Visibility.Visible;
		base.Opacity = 0.0;
		BuddyTheme.Animate(this, UIElement.OpacityProperty, 1.0, BuddyTheme.EntryFade);
		_timer.Stop();
		_timer.Start();
	}

	public void ShowNotice(string message)
	{
		Commit();
		_message.Text = message;
		_undoHit.Visibility = Visibility.Collapsed;
		base.Visibility = Visibility.Visible;
		base.Opacity = 0.0;
		BuddyTheme.Animate(this, UIElement.OpacityProperty, 1.0, BuddyTheme.EntryFade);
		_timer.Stop();
		_timer.Start();
	}

	public void Undo()
	{
		Action undo = _undo;
		_undo = null;
		_commit = null;
		Hide();
		undo?.Invoke();
	}

	public void Commit()
	{
		Action commit = _commit;
		_undo = null;
		_commit = null;
		Hide();
		commit?.Invoke();
	}

	private void Hide()
	{
		_timer.Stop();
		base.Visibility = Visibility.Collapsed;
	}
}
