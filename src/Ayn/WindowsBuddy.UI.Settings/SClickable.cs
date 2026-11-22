using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.UI.Settings;

internal class SClickable : Border
{
	private bool _pressed;

	private bool _armed;

	private bool _selected;

	private bool _active;

	private Key _pressedKey;

	private SVisualRole _role;

	private FrameworkElement _motionChild;

	private ScaleTransform _motion;

	private TranslateTransform _motionOffset;

	internal bool IsPressed
	{
		get
		{
			if (base.IsEnabled && _pressed)
			{
				return _armed;
			}
			return false;
		}
	}

	internal SVisualRole VisualRole
	{
		get
		{
			return _role;
		}
		set
		{
			_role = value;
			RefreshVisual();
		}
	}

	public bool IsSelected
	{
		get
		{
			return _selected;
		}
		set
		{
			if (_selected != value)
			{
				_selected = value;
				AutomationProperties.SetItemStatus(this, value ? "Selected" : "");
				RefreshVisual();
			}
		}
	}

	internal bool IsActive
	{
		get
		{
			return _active;
		}
		set
		{
			_active = value;
			RefreshVisual();
		}
	}

	public event EventHandler Click;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsInvokePeer(this);
	}

	public SClickable(double radius, string name)
	{
		base.CornerRadius = new CornerRadius(radius);
		base.Cursor = Cursors.Hand;
		base.SnapsToDevicePixels = true;
		base.MinHeight = 32.0;
		base.MinWidth = 32.0;
		SettingsTheme.MakeFocusable(this, radius, name);
		MouseButtonEventHandler value = delegate(object s, MouseButtonEventArgs e)
		{
			if (base.IsEnabled && !e.Handled)
			{
				Focus();
				_pressed = (_armed = true);
				CaptureMouse();
				RefreshVisual();
				e.Handled = true;
			}
		};
		base.MouseLeftButtonDown += value;
		base.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (_pressed && _pressedKey == Key.None)
			{
				_armed = Contains(e.GetPosition(this));
				RefreshVisual();
			}
		};
		base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_pressed && _pressedKey == Key.None)
			{
				bool flag2 = base.IsEnabled && Contains(e.GetPosition(this));
				CancelPress();
				if (flag2)
				{
					RaiseClick();
				}
				e.Handled = true;
			}
		};
		base.LostMouseCapture += delegate
		{
			if (_pressedKey == Key.None)
			{
				CancelPress();
			}
		};
		base.MouseEnter += delegate
		{
			RefreshVisual();
		};
		base.MouseLeave += delegate
		{
			if (_pressedKey == Key.None)
			{
				_armed = false;
			}
			RefreshVisual();
		};
		base.GotKeyboardFocus += delegate
		{
			RefreshVisual();
		};
		base.LostKeyboardFocus += delegate
		{
			CancelPress();
		};
		base.IsEnabledChanged += delegate
		{
			if (!base.IsEnabled)
			{
				CancelPress();
			}
			RefreshVisual();
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (base.IsEnabled && e.OriginalSource == this && (e.Key == Key.Return || e.Key == Key.Space))
			{
				if (!e.IsRepeat)
				{
					_pressedKey = e.Key;
					_pressed = (_armed = true);
					RefreshVisual();
				}
				e.Handled = true;
			}
		};
		base.KeyUp += delegate(object s, KeyEventArgs e)
		{
			if (_pressedKey != 0 && e.Key == _pressedKey)
			{
				bool flag = base.IsEnabled && base.IsKeyboardFocused;
				CancelPress();
				if (flag)
				{
					RaiseClick();
				}
				e.Handled = true;
			}
		};
		base.Loaded += delegate
		{
			RefreshVisual();
		};
		base.IsVisibleChanged += delegate
		{
			if (!base.IsVisible)
			{
				CancelPress();
				SMotion.Finish(_motion);
				SMotion.Finish(_motionOffset);
			}
		};
		base.Unloaded += delegate
		{
			CancelPress();
			SMotion.Finish(_motion);
			SMotion.Finish(_motionOffset);
		};
		ThemeTokens.Subscribe(this, delegate(SClickable c)
		{
			c.RefreshVisual();
		});
	}

	private bool Contains(Point p)
	{
		if (p.X >= 0.0 && p.Y >= 0.0 && p.X <= base.ActualWidth)
		{
			return p.Y <= base.ActualHeight;
		}
		return false;
	}

	private void CancelPress()
	{
		_pressed = (_armed = false);
		_pressedKey = Key.None;
		if (base.IsMouseCaptured)
		{
			ReleaseMouseCapture();
		}
		RefreshVisual();
	}

	internal void RefreshVisual()
	{
		OnVisual();
	}

	protected virtual void OnVisual()
	{
		bool flag = base.IsEnabled && (base.IsMouseOver || _active);
		bool isPressed = IsPressed;
		base.Opacity = (base.IsEnabled ? 1.0 : 0.4);
		base.Cursor = (base.IsEnabled ? Cursors.Hand : Cursors.Arrow);
		Brush brush = SettingsTheme.ControlBg;
		Brush brush2 = SettingsTheme.ControlPressed;
		Brush brush3 = SettingsTheme.ControlHover;
		Brush brush4 = SettingsTheme.ControlPressed;
		switch (_role)
		{
		case SVisualRole.Ghost:
		case SVisualRole.Segment:
		case SVisualRole.Swatch:
			brush = Brushes.Transparent;
			brush2 = ((_role == SVisualRole.Segment) ? Brushes.Transparent : SettingsTheme.ControlBg);
			brush3 = ThemeTokens.Surface2;
			brush4 = ThemeTokens.Surface3;
			break;
		case SVisualRole.Navigation:
			brush = Brushes.Transparent;
			brush2 = ThemeTokens.NavSelectedFill;
			brush3 = (_selected ? ThemeTokens.NavSelectedFill : ThemeTokens.NavHoverFill);
			brush4 = ThemeTokens.NavSelectedFill;
			break;
		case SVisualRole.Primary:
			brush = (brush2 = (brush3 = (brush4 = SettingsTheme.PrimaryButtonBg)));
			break;
		case SVisualRole.Danger:
			brush = (brush2 = ThemeTokens.DangerSoft);
			brush3 = (brush4 = ThemeTokens.DangerSoft);
			break;
		case SVisualRole.Caption:
		case SVisualRole.CloseCaption:
			brush = (brush2 = Brushes.Transparent);
			brush3 = (brush4 = ((_role == SVisualRole.CloseCaption) ? SettingsTheme.Solid(196, 43, 28) : SettingsTheme.ControlHover));
			break;
		case SVisualRole.Bare:
			brush = (brush2 = (brush3 = (brush4 = Brushes.Transparent)));
			break;
		case SVisualRole.Card:
			brush = (brush2 = (ThemeTokens.IsLight ? ThemeTokens.Surface : ThemeTokens.Surface2));
			brush3 = (_selected ? brush : ThemeTokens.CardHoverFill);
			brush4 = (_selected ? brush : ThemeTokens.CardPressedFill);
			break;
		}
		base.Background = ((!base.IsEnabled) ? brush : (isPressed ? brush4 : (flag ? brush3 : (_selected ? brush2 : brush))));
		if (_role == SVisualRole.Segment && _selected)
		{
			base.Background = Brushes.Transparent;
		}
		if (_role == SVisualRole.Card || _role == SVisualRole.Surface || _role == SVisualRole.Ghost)
		{
			base.BorderBrush = ((_selected && _role != SVisualRole.Card) ? SettingsTheme.SelectedOutline : ((base.IsEnabled && base.IsKeyboardFocusWithin && _role != SVisualRole.Card) ? SettingsTheme.FocusRing : Brushes.Transparent));
		}
		if (_role == SVisualRole.Primary)
		{
			base.BorderBrush = ((base.IsEnabled && (flag || isPressed || base.IsKeyboardFocusWithin)) ? SettingsTheme.PrimaryButtonText : Brushes.Transparent);
		}
		if (Child is FrameworkElement frameworkElement)
		{
			if (_motionChild != frameworkElement)
			{
				SMotion.Finish(_motion);
				SMotion.Finish(_motionOffset);
				_motionChild = frameworkElement;
				_motion = SMotion.AddScale(frameworkElement);
				_motionOffset = new TranslateTransform();
				((TransformGroup)frameworkElement.RenderTransform).Children.Add(_motionOffset);
			}
			double value = ((!base.IsEnabled) ? 1.0 : ((!isPressed) ? ((!flag) ? 1.0 : ((_role == SVisualRole.Swatch) ? 1.12 : 1.015)) : ((_role == SVisualRole.Swatch) ? 0.9 : 0.97)));
			if (_role == SVisualRole.Navigation)
			{
				value = 1.0;
			}
			SMotion.Scale(_motion, value, base.IsVisible && base.IsEnabled);
			SMotion.To(_motionOffset, TranslateTransform.YProperty, (_role == SVisualRole.Navigation && isPressed) ? 1 : 0, base.IsVisible && base.IsEnabled);
		}
	}

	public void RaiseClick()
	{
		if (base.IsEnabled)
		{
			this.Click?.Invoke(this, EventArgs.Empty);
		}
	}
}
