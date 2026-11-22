using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyIconButton : Button
{
	private Border _container;

	private ScaleTransform _scale;

	private bool _isActive;

	public bool IsActive
	{
		get
		{
			return _isActive;
		}
		set
		{
			_isActive = value;
			UpdateActiveVisual();
		}
	}

	public BuddyIconButton(UIElement icon, string tooltip = "", double size = 32.0, bool isCircular = true)
	{
		base.Width = size;
		base.Height = size;
		base.Cursor = Cursors.Hand;
		base.Focusable = true;
		base.FocusVisualStyle = null;
		base.Background = Brushes.Transparent;
		base.BorderThickness = new Thickness(0.0);
		if (!string.IsNullOrEmpty(tooltip))
		{
			base.ToolTip = BuddyTooltip.Create(tooltip);
		}
		_scale = new ScaleTransform(1.0, 1.0);
		base.RenderTransformOrigin = new Point(0.5, 0.5);
		base.RenderTransform = _scale;
		_container = new Border
		{
			Width = size,
			Height = size,
			CornerRadius = new CornerRadius(size / 2.0),
			Background = Brushes.Transparent,
			BorderBrush = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Child = icon
		};
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory visualTree = new FrameworkElementFactory(typeof(ContentPresenter));
		controlTemplate.VisualTree = visualTree;
		base.Template = controlTemplate;
		base.Content = _container;
		base.MouseEnter += delegate
		{
			if (base.IsEnabled && !_isActive)
			{
				_container.Background = BuddyTokens.IconButtonHoverBrush;
			}
		};
		base.MouseLeave += delegate
		{
			AnimateRelease();
			if (base.IsEnabled && !_isActive)
			{
				_container.Background = Brushes.Transparent;
			}
		};
		base.PreviewMouseLeftButtonDown += delegate
		{
			AnimatePress();
		};
		base.PreviewMouseLeftButtonUp += delegate
		{
			AnimateRelease();
		};
		base.GotFocus += delegate
		{
			if (base.IsEnabled)
			{
				_container.BorderBrush = BuddyTokens.FocusRingBrush;
				_container.BorderThickness = new Thickness(1.0);
			}
		};
		base.LostFocus += delegate
		{
			if (base.IsEnabled)
			{
				_container.BorderBrush = Brushes.Transparent;
				_container.BorderThickness = new Thickness(0.0);
			}
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				AnimatePress();
				OnClick();
				e.Handled = true;
			}
		};
		base.KeyUp += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				AnimateRelease();
				e.Handled = true;
			}
		};
		base.IsEnabledChanged += delegate
		{
			base.Opacity = (base.IsEnabled ? 1.0 : 0.35);
			base.Cursor = (base.IsEnabled ? Cursors.Hand : Cursors.Arrow);
		};
	}

	public BuddyIconButton(UIElement icon, double size, Action onClick)
		: this(icon, "", size)
	{
		if (onClick != null)
		{
			base.Click += delegate
			{
				onClick();
			};
		}
	}

	public void SetIcon(UIElement icon)
	{
		if (_container != null)
		{
			_container.Child = icon;
		}
	}

	private void UpdateActiveVisual()
	{
		if (_container != null)
		{
			if (_isActive)
			{
				_container.Background = ThemeTokens.Text;
				_container.BorderThickness = new Thickness(0.0);
			}
			else
			{
				_container.Background = (base.IsMouseOver ? BuddyTokens.IconButtonHoverBrush : Brushes.Transparent);
				_container.BorderThickness = new Thickness(0.0);
			}
		}
	}

	private void AnimatePress()
	{
		if (base.IsEnabled)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(60.0));
			doubleAnimation.EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation animation = doubleAnimation;
			_scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			_scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}
	}

	private void AnimateRelease()
	{
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(90.0));
		doubleAnimation.EasingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation animation = doubleAnimation;
		_scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		_scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
	}
}
