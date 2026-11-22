using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddyPrimaryPill : Border
{
	private readonly ContentControl _icon;

	private readonly TextBlock _label;

	private readonly string _iconName;

	private readonly string _text;

	private readonly double _glyph;

	private DispatcherTimer _feedback;

	public TimeSpan Hold = BuddyTheme.FeedbackHold;

	public string LabelText => _label.Text;

	public event Action Click;

	public BuddyPrimaryPill(string icon, string text, double height)
		: this(icon, text, height, 18.0, new Thickness(14.0, 0.0, 16.0, 0.0), 8.0)
	{
	}

	public BuddyPrimaryPill(string icon, string text, double height, double glyph, Thickness padding, double gap)
	{
		_iconName = icon;
		_text = text;
		_glyph = glyph;
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_icon = new ContentControl
		{
			Width = glyph,
			Height = glyph,
			Focusable = false,
			IsTabStop = false,
			VerticalAlignment = VerticalAlignment.Center,
			Content = FluentIcons.Create(icon, glyph, BuddyTheme.PrimaryInk, filled: false)
		};
		stackPanel.Children.Add(_icon);
		_label = BuddyTheme.Text(text, 13.0, BuddyTheme.PrimaryInk, AynType.Medium);
		_label.VerticalAlignment = VerticalAlignment.Center;
		_label.Margin = new Thickness(gap, 0.0, 0.0, 0.0);
		stackPanel.Children.Add(_label);
		base.Height = height;
		base.CornerRadius = new CornerRadius(height / 2.0);
		base.Background = BuddyTheme.PrimaryFill;
		base.Padding = padding;
		base.Cursor = Cursors.Hand;
		Child = stackPanel;
		MouseEventHandler value = delegate
		{
			base.Opacity = 0.9;
		};
		base.MouseEnter += value;
		base.MouseLeave += delegate
		{
			base.Opacity = 1.0;
		};
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			this.Click?.Invoke();
		};
	}

	public void ShowFeedback(string text)
	{
		ShowFeedback(text, "checkmark");
	}

	public void ShowFeedback(string text, string icon)
	{
		_icon.Content = FluentIcons.Create(icon, _glyph, BuddyTheme.PrimaryInk, filled: false);
		_label.Text = text;
		if (_feedback == null)
		{
			_feedback = new DispatcherTimer();
			_feedback.Tick += delegate
			{
				ResetFeedback();
			};
		}
		_feedback.Interval = Hold;
		_feedback.Stop();
		_feedback.Start();
	}

	public void ResetFeedback()
	{
		if (_feedback != null)
		{
			_feedback.Stop();
		}
		if (!(_label.Text == _text))
		{
			_icon.Content = FluentIcons.Create(_iconName, _glyph, BuddyTheme.PrimaryInk, filled: false);
			_label.Text = _text;
		}
	}
}
