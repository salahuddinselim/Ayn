using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class Toggle : Border
{
	private readonly Ellipse _knob;

	private readonly TranslateTransform _x = new TranslateTransform();

	private readonly Action<bool> _changed;

	private bool _on;

	public bool IsOn
	{
		get
		{
			return _on;
		}
		set
		{
			if (_on != value)
			{
				_on = value;
				Paint(this);
				double num = (value ? 14 : 0);
				if (CalendarTool.CapsuleMotionOn && base.IsLoaded)
				{
					_x.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(num, TimeSpan.FromMilliseconds(140.0))
					{
						EasingFunction = BuddyTheme.EaseOut
					});
				}
				else
				{
					_x.BeginAnimation(TranslateTransform.XProperty, null);
					_x.X = num;
				}
			}
		}
	}

	public Toggle(bool on, Action<bool> changed)
	{
		_changed = changed;
		base.Width = 34.0;
		base.Height = 20.0;
		base.CornerRadius = new CornerRadius(10.0);
		base.Cursor = Cursors.Hand;
		base.Focusable = true;
		base.FocusVisualStyle = null;
		_knob = new Ellipse
		{
			Width = 16.0,
			Height = 16.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(2.0, 0.0, 0.0, 0.0),
			RenderTransform = _x
		};
		Child = _knob;
		CalendarTool.AttachClick(this, Flip, null);
		KeyEventHandler value = delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Space || e.Key == Key.Return)
			{
				e.Handled = true;
				Flip();
			}
		};
		base.KeyDown += value;
		base.GotKeyboardFocus += delegate
		{
			base.Opacity = 0.85;
		};
		base.LostKeyboardFocus += delegate
		{
			base.Opacity = 1.0;
		};
		_on = !on;
		IsOn = on;
		ThemeTokens.Subscribe(this, Paint);
	}

	private static void Paint(Toggle t)
	{
		t.Background = (t._on ? ThemeTokens.Accent : BuddyTint.Ink(38));
		t._knob.Fill = (t._on ? ThemeTokens.AccentFg : (ThemeTokens.IsLight ? ThemeTokens.Surface : ThemeTokens.Text2));
	}

	private void Flip()
	{
		IsOn = !_on;
		if (_changed != null)
		{
			_changed(_on);
		}
	}
}
