using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddyDockSlot : Border
{
	public const double Size = 36.0;

	private const double Glyph = 18.0;

	private readonly Func<Brush, UIElement> _icon;

	private readonly ContentControl _glyph;

	private readonly SolidColorBrush _ink = new SolidColorBrush(BuddyTheme.ColorOf(BuddyTheme.DockIcon));

	private readonly SolidColorBrush _fill = new SolidColorBrush(Colors.Transparent);

	private bool _active;

	private bool _hover;

	private bool _focused;

	public string Id { get; private set; }

	public double ActiveWidth => 36.0;

	public bool IsActive => _active;

	public BuddyDockSlot(string id, string label, Func<Brush, UIElement> icon)
	{
		Id = id;
		_icon = icon;
		base.Width = 36.0;
		base.Height = 36.0;
		base.CornerRadius = new CornerRadius(18.0);
		base.Background = _fill;
		BuddyTheme.Crisp(this);
		_glyph = new ContentControl
		{
			Width = 18.0,
			Height = 18.0,
			Focusable = false,
			IsTabStop = false,
			IsHitTestVisible = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Child = _glyph;
		BuddyTheme.Pressable(this);
		SetInk();
		Apply(animate: false);
		ThemeTokens.Subscribe(this, delegate(BuddyDockSlot s)
		{
			s.Apply(animate: true);
		});
		base.MouseEnter += delegate
		{
			_hover = true;
			Apply(animate: true);
		};
		base.MouseLeave += delegate
		{
			_hover = false;
			Apply(animate: true);
		};
	}

	public void ClearTransientState()
	{
		_hover = (_focused = false);
		Apply(animate: false);
	}

	public void SetActive(bool active, Duration? duration)
	{
		if (_active != active || !duration.HasValue)
		{
			_active = active;
			Apply(duration.HasValue);
		}
	}

	public void SetFocused(bool focused)
	{
		_focused = focused;
		Apply(animate: true);
	}

	public void SetInk()
	{
		_glyph.Content = _icon(_ink);
	}

	private void Apply(bool animate)
	{
		bool flag = (_hover || _focused) && !_active;
		Color color = BuddyTheme.ColorOf(ThemeTokens.Text);
		Color color2 = (_active ? BuddyTheme.ColorOf(ThemeTokens.Inverse) : (flag ? color : Color.FromArgb(140, color.R, color.G, color.B)));
		Color color3 = BuddyTheme.ColorOf(ThemeTokens.Selection);
		Color color4 = Color.FromArgb(0, color3.R, color3.G, color3.B);
		if (!animate || !CapsuleMotion.AnimationsEnabled)
		{
			_ink.BeginAnimation(SolidColorBrush.ColorProperty, null);
			_fill.BeginAnimation(SolidColorBrush.ColorProperty, null);
			_ink.Color = color2;
			_fill.Color = color4;
		}
		else
		{
			_ink.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color2, BuddyTheme.Hover));
			_fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color4, BuddyTheme.Hover));
		}
	}
}
