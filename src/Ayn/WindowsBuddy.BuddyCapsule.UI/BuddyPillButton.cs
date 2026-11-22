using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyPillButton : Border
{
	public static readonly Brush InactiveBgBrush = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(18, 18, 20)));

	public static readonly Brush ActiveBgBrush = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(24, 24, 28)));

	public static readonly Brush InactiveBorderBrush = ThemeTokens.Border;

	public static readonly Brush HoverBorderBrush = ThemeTokens.BorderStrong;

	public static readonly Brush ActiveBorderBrush = ThemeTokens.Text;

	public static readonly Brush MutedTextBrush = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(154, 154, 158)));

	private bool _isSelected;

	private readonly TextBlock _label;

	private readonly ContentControl _iconHost;

	private Func<Brush, UIElement> _iconFactory;

	public object TagValue { get; set; }

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (_isSelected != value)
			{
				_isSelected = value;
				UpdateVisualState();
			}
		}
	}

	public event Action<BuddyPillButton> Clicked;

	public BuddyPillButton(string text, Func<Brush, UIElement> iconFactory = null, object tagValue = null, double cornerRadius = 8.0, bool isVertical = false)
	{
		TagValue = tagValue ?? text;
		_iconFactory = iconFactory;
		base.CornerRadius = new CornerRadius(cornerRadius);
		base.Cursor = Cursors.Hand;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		base.Padding = new Thickness(6.0, 4.0, 6.0, 4.0);
		_label = new TextBlock
		{
			Text = text,
			FontSize = 10.0,
			FontFamily = BuddyTokens.FontText,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			TextAlignment = TextAlignment.Center
		};
		_iconHost = new ContentControl
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		if (isVertical)
		{
			base.Padding = new Thickness(2.0, 3.0, 2.0, 3.0);
			_label.FontSize = 10.0;
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Vertical,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			if (_iconFactory != null)
			{
				_iconHost.Margin = new Thickness(0.0, 0.0, 0.0, 3.0);
				stackPanel.Children.Add(_iconHost);
			}
			stackPanel.Children.Add(_label);
			Child = new Viewbox
			{
				Child = stackPanel,
				Stretch = Stretch.Uniform,
				StretchDirection = StretchDirection.DownOnly,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
		}
		else
		{
			base.Padding = new Thickness(2.0, 2.0, 2.0, 2.0);
			_label.FontSize = 10.0;
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			if (_iconFactory != null)
			{
				_iconHost.Margin = new Thickness(0.0, 0.0, 2.0, 0.0);
				stackPanel2.Children.Add(_iconHost);
			}
			stackPanel2.Children.Add(_label);
			Child = new Viewbox
			{
				Child = stackPanel2,
				Stretch = Stretch.Uniform,
				StretchDirection = StretchDirection.DownOnly,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
		}
		UpdateVisualState();
		base.MouseEnter += delegate
		{
			if (!_isSelected)
			{
				base.BorderBrush = HoverBorderBrush;
				_label.Foreground = ThemeTokens.Text;
				if (_iconFactory != null)
				{
					_iconHost.Content = _iconFactory(ThemeTokens.Text);
				}
			}
		};
		base.MouseLeave += delegate
		{
			if (!_isSelected)
			{
				UpdateVisualState();
			}
		};
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (this.Clicked != null)
			{
				this.Clicked(this);
			}
		};
	}

	private void UpdateVisualState()
	{
		if (_isSelected)
		{
			base.Background = ActiveBgBrush;
			base.BorderBrush = ActiveBorderBrush;
			base.BorderThickness = new Thickness(1.5);
			_label.Foreground = ThemeTokens.Text;
			_label.FontWeight = AynType.Medium;
			if (_iconFactory != null)
			{
				_iconHost.Content = _iconFactory(ThemeTokens.Text);
			}
		}
		else
		{
			base.Background = InactiveBgBrush;
			base.BorderBrush = InactiveBorderBrush;
			base.BorderThickness = new Thickness(1.0);
			_label.Foreground = MutedTextBrush;
			_label.FontWeight = FontWeights.Normal;
			if (_iconFactory != null)
			{
				_iconHost.Content = _iconFactory(MutedTextBrush);
			}
		}
	}
}
