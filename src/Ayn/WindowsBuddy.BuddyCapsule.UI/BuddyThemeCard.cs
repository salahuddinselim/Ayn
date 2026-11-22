using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyThemeCard : Border
{
	private readonly SolidColorBrush _fill = new SolidColorBrush(BuddyTheme.ColorOf(BuddyTheme.CardFill));

	private bool _pressed;

	public event EventHandler Click;

	public BuddyThemeCard()
	{
		BuddyTheme.Crisp(this);
		RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
		base.Background = _fill;
		base.BorderBrush = ThemeTokens.CardBorder;
		base.BorderThickness = new Thickness(1.0);
		base.CornerRadius = new CornerRadius(16.0);
		base.Padding = new Thickness(13.0, 13.0, 13.0, 11.0);
		ThemeTokens.Subscribe(this, delegate(BuddyThemeCard c)
		{
			c.SetHover();
		});
		ThemeTokens.Card(this);
		BuddyTheme.Pressable(this);
		base.Cursor = Cursors.Hand;
		base.Focusable = true;
		base.FocusVisualStyle = null;
		KeyboardNavigation.SetIsTabStop(this, isTabStop: true);
		base.MouseEnter += delegate
		{
			SetHover();
		};
		base.MouseLeave += delegate
		{
			_pressed = false;
			SetHover();
		};
		base.GotKeyboardFocus += delegate
		{
			SetHover();
		};
		base.LostKeyboardFocus += delegate
		{
			SetHover();
		};
		base.MouseLeftButtonDown += delegate
		{
			_pressed = true;
		};
		base.MouseLeftButtonUp += delegate
		{
			if (_pressed)
			{
				_pressed = false;
				RaiseClick();
			}
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				RaiseClick();
			}
		};
	}

	private void SetHover()
	{
		Color color = BuddyTheme.ColorOf((base.IsMouseOver || base.IsKeyboardFocused) ? BuddyTheme.CardFillHover : BuddyTheme.CardFill);
		if (!CapsuleMotion.AnimationsEnabled)
		{
			_fill.BeginAnimation(SolidColorBrush.ColorProperty, null);
			_fill.Color = color;
		}
		else
		{
			_fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, BuddyTheme.Hover));
		}
	}

	private void RaiseClick()
	{
		this.Click?.Invoke(this, EventArgs.Empty);
	}
}
