using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyChip : Button
{
	public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register("IsActive", typeof(bool), typeof(BuddyChip), new FrameworkPropertyMetadata(false, OnIsActiveChanged));

	private Border _chipBorder;

	public bool Borderless { get; set; }

	public bool IsActive
	{
		get
		{
			return (bool)GetValue(IsActiveProperty);
		}
		set
		{
			SetValue(IsActiveProperty, value);
		}
	}

	public string Text
	{
		get
		{
			return (base.Content as string) ?? "";
		}
		set
		{
			base.Content = value;
		}
	}

	public BuddyChip()
	{
		base.Cursor = Cursors.Hand;
		base.Focusable = true;
		base.FocusVisualStyle = null;
		base.Height = 24.0;
		base.Padding = new Thickness(10.0, 2.0, 10.0, 2.0);
		base.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		base.FontFamily = BuddyTypography.FontText;
		base.FontSize = 11.0;
		base.FontWeight = FontWeights.Medium;
		BuildTemplate();
		UpdateVisual();
	}

	public BuddyChip(string label, bool isActive = false)
		: this()
	{
		base.Content = label;
		IsActive = isActive;
	}

	private void BuildTemplate()
	{
		ControlTemplate controlTemplate = new ControlTemplate(typeof(BuddyChip));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.Name = "ChipBorder";
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12.0));
		frameworkElementFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		base.Template = controlTemplate;
		base.Loaded += delegate
		{
			_chipBorder = base.Template.FindName("ChipBorder", this) as Border;
			UpdateVisual();
		};
		base.MouseEnter += delegate
		{
			if (!IsActive && _chipBorder != null)
			{
				_chipBorder.Background = BuddyTokens.HairlineLightBrush;
			}
		};
		base.MouseLeave += delegate
		{
			if (!IsActive && _chipBorder != null)
			{
				_chipBorder.Background = BuddyTokens.SurfaceSecondaryBrush;
			}
		};
	}

	private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is BuddyChip buddyChip)
		{
			buddyChip.UpdateVisual();
		}
	}

	public void UpdateVisual()
	{
		if (_chipBorder == null && base.Template != null)
		{
			ApplyTemplate();
			_chipBorder = base.Template.FindName("ChipBorder", this) as Border;
		}
		if (_chipBorder != null)
		{
			if (IsActive)
			{
				_chipBorder.Background = (Borderless ? ThemeTokens.AccentSoft : BuddyTokens.SurfaceActiveBrush);
				_chipBorder.BorderBrush = ThemeTokens.BorderStrong;
				_chipBorder.BorderThickness = new Thickness((!Borderless) ? 1 : 0);
				base.Foreground = (Borderless ? ThemeTokens.AccentStrong : BuddyTokens.TextPrimaryBrush);
				base.FontWeight = AynType.Medium;
			}
			else
			{
				_chipBorder.Background = (Borderless ? ThemeTokens.Selection : BuddyTokens.SurfaceSecondaryBrush);
				_chipBorder.BorderBrush = BuddyTokens.HairlineBrush;
				_chipBorder.BorderThickness = new Thickness((!Borderless) ? 1 : 0);
				base.Foreground = BuddyTokens.TextMutedBrush;
				base.FontWeight = FontWeights.Medium;
			}
		}
	}
}
