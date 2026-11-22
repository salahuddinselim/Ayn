using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyButton : Button
{
	private Border _border;

	private ScaleTransform _scale;

	private BuddyButtonVariant _variant;

	public bool IsPrimary
	{
		get
		{
			return _variant == BuddyButtonVariant.Primary;
		}
		set
		{
			_variant = ((!value) ? BuddyButtonVariant.Secondary : BuddyButtonVariant.Primary);
			UpdateVariantVisuals();
		}
	}

	public BuddyButtonVariant Variant
	{
		get
		{
			return _variant;
		}
		set
		{
			_variant = value;
			UpdateVariantVisuals();
		}
	}

	public BuddyButton()
		: this("", BuddyButtonVariant.Secondary, 32.0)
	{
	}

	public BuddyButton(string text, bool isPrimary = false)
		: this(text, (!isPrimary) ? BuddyButtonVariant.Secondary : BuddyButtonVariant.Primary, 32.0)
	{
	}

	public BuddyButton(string text, BuddyButtonVariant variant, double height = 30.0)
	{
		base.Content = text;
		_variant = variant;
		base.Height = height;
		base.Cursor = Cursors.Hand;
		base.Focusable = true;
		base.FocusVisualStyle = null;
		base.FontFamily = BuddyTypography.FontText;
		base.FontSize = 13.0;
		base.FontWeight = AynType.Medium;
		base.Padding = new Thickness(14.0, 0.0, 14.0, 0.0);
		base.VerticalContentAlignment = VerticalAlignment.Center;
		base.HorizontalContentAlignment = HorizontalAlignment.Center;
		_scale = new ScaleTransform(1.0, 1.0);
		base.RenderTransformOrigin = new Point(0.5, 0.5);
		base.RenderTransform = _scale;
		ApplyCustomTemplate();
		UpdateVariantVisuals();
		SizeChangedEventHandler value = delegate(object s, SizeChangedEventArgs e)
		{
			if (_border != null && e.NewSize.Height > 0.0)
			{
				_border.CornerRadius = new CornerRadius(e.NewSize.Height / 2.0);
			}
		};
		base.SizeChanged += value;
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
		base.PreviewMouseLeftButtonDown += delegate
		{
			AnimatePress();
		};
		base.PreviewMouseLeftButtonUp += delegate
		{
			AnimateRelease();
		};
		base.IsEnabledChanged += delegate
		{
			UpdateEnabledState();
		};
	}

	public static BuddyButton Create(string text, BuddyButtonVariant variant = BuddyButtonVariant.Secondary)
	{
		return new BuddyButton(text, variant);
	}

	private void ApplyCustomTemplate()
	{
		double num = ((base.Height > 0.0 && !double.IsNaN(base.Height)) ? base.Height : 32.0);
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.Name = "BtnBorder";
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(num / 2.0));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		frameworkElementFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
		frameworkElementFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		base.Template = controlTemplate;
		base.Loaded += delegate
		{
			_border = base.Template.FindName("BtnBorder", this) as Border;
			if (_border != null)
			{
				double num2 = ((base.ActualHeight > 0.0) ? base.ActualHeight : ((base.Height > 0.0) ? base.Height : 30.0));
				_border.CornerRadius = new CornerRadius(num2 / 2.0);
			}
			UpdateVariantVisuals();
		};
		base.MouseEnter += delegate
		{
			if (base.IsEnabled)
			{
				ApplyHoverVisuals(isHover: true);
			}
		};
		base.MouseLeave += delegate
		{
			AnimateRelease();
			if (base.IsEnabled)
			{
				ApplyHoverVisuals(isHover: false);
			}
		};
		base.GotFocus += delegate
		{
			if (base.IsEnabled)
			{
				base.BorderBrush = BuddyTokens.FocusRingBrush;
				base.BorderThickness = new Thickness(1.0);
			}
		};
		base.LostFocus += delegate
		{
			if (base.IsEnabled)
			{
				base.BorderThickness = new Thickness(0.0);
				ApplyHoverVisuals(base.IsMouseOver);
			}
		};
	}

	private void UpdateVariantVisuals()
	{
		base.BorderThickness = new Thickness(0.0);
		if (_variant == BuddyButtonVariant.Primary)
		{
			base.Background = ThemeTokens.AccentStrong;
			base.BorderBrush = Brushes.Transparent;
			base.Foreground = ThemeTokens.AccentFg;
			base.FontWeight = AynType.Medium;
		}
		else if (_variant == BuddyButtonVariant.Secondary)
		{
			base.Background = BuddyTokens.ButtonSecondarySurfaceBrush;
			base.BorderBrush = Brushes.Transparent;
			base.Foreground = BuddyTokens.TextPrimaryBrush;
			base.FontWeight = FontWeights.Medium;
		}
		else if (_variant == BuddyButtonVariant.Ghost)
		{
			base.Background = Brushes.Transparent;
			base.BorderBrush = Brushes.Transparent;
			base.Foreground = BuddyTokens.TextSecondaryBrush;
			base.FontWeight = FontWeights.Medium;
		}
		else if (_variant == BuddyButtonVariant.Danger)
		{
			base.Background = ThemeTokens.DangerSoft;
			base.BorderBrush = Brushes.Transparent;
			base.Foreground = BuddyTokens.DangerRedBrush;
			base.FontWeight = AynType.Medium;
		}
	}

	private void ApplyHoverVisuals(bool isHover)
	{
		if (base.IsFocused)
		{
			base.BorderBrush = BuddyTokens.FocusRingBrush;
			base.BorderThickness = new Thickness(1.0);
		}
		else
		{
			base.BorderThickness = new Thickness(0.0);
		}
		if (_variant == BuddyButtonVariant.Primary)
		{
			base.Background = ThemeTokens.AccentStrong;
			base.Opacity = (isHover ? 0.9 : 1.0);
		}
		else if (_variant == BuddyButtonVariant.Secondary)
		{
			base.Background = (isHover ? ThemeTokens.Surface3 : BuddyTokens.ButtonSecondarySurfaceBrush);
		}
		else if (_variant == BuddyButtonVariant.Ghost)
		{
			base.Background = (isHover ? BuddyTokens.HoverSurfaceBrush : Brushes.Transparent);
			base.Foreground = (isHover ? BuddyTokens.TextPrimaryBrush : BuddyTokens.TextSecondaryBrush);
		}
		else if (_variant == BuddyButtonVariant.Danger)
		{
			base.Background = ThemeTokens.DangerSoft;
			base.Opacity = (isHover ? 0.85 : 1.0);
		}
	}

	private void AnimatePress()
	{
		if (base.IsEnabled)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.96, TimeSpan.FromMilliseconds(70.0));
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
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100.0));
		doubleAnimation.EasingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation animation = doubleAnimation;
		_scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		_scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
	}

	private void UpdateEnabledState()
	{
		base.Opacity = (base.IsEnabled ? 1.0 : 0.38);
		base.Cursor = (base.IsEnabled ? Cursors.Hand : Cursors.Arrow);
		if (!base.IsEnabled)
		{
			_scale.ScaleX = 1.0;
			_scale.ScaleY = 1.0;
		}
	}
}
