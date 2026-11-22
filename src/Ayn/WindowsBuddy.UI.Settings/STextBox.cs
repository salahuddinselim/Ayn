using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal class STextBox : Border
{
	public readonly TextBox Box;

	private readonly TextBlock _placeholder;

	private bool _press;

	public string Text
	{
		get
		{
			return Box.Text;
		}
		set
		{
			Box.Text = value ?? "";
		}
	}

	public STextBox(string text, string placeholder, double width, string name = null, string leadingGlyph = null, string trailingHint = null)
	{
		base.Height = 36.0;
		if (width > 0.0)
		{
			base.Width = width;
		}
		base.Background = SettingsTheme.InputBg;
		base.BorderBrush = SettingsTheme.InputBorder;
		base.BorderThickness = new Thickness(1.0);
		base.CornerRadius = new CornerRadius(8.0);
		base.VerticalAlignment = VerticalAlignment.Center;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		if (leadingGlyph != null)
		{
			FrameworkElement frameworkElement = Glyphs.Create(leadingGlyph, 14.0, SettingsTheme.TextSecondary);
			frameworkElement.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			grid.Children.Add(frameworkElement);
		}
		Box = new TextBox
		{
			Text = (text ?? ""),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Foreground = SettingsTheme.TextPrimary,
			CaretBrush = SettingsTheme.TextPrimary,
			SelectionBrush = SettingsTheme.PrimaryButtonBg,
			SelectionOpacity = 1.0,
			SelectionTextBrush = SettingsTheme.PrimaryButtonText,
			FontFamily = SettingsTheme.Font,
			FontSize = 13.5,
			VerticalContentAlignment = VerticalAlignment.Center,
			Padding = new Thickness((leadingGlyph != null) ? 8 : 12, 0.0, 8.0, 0.0),
			FocusVisualStyle = SettingsTheme.FocusStyle(8.0)
		};
		if (!string.IsNullOrEmpty(name))
		{
			AutomationProperties.SetName(Box, name);
		}
		Grid.SetColumn(Box, 1);
		grid.Children.Add(Box);
		_placeholder = SettingsTheme.Text(placeholder ?? "", 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		_placeholder.TextWrapping = TextWrapping.NoWrap;
		_placeholder.IsHitTestVisible = false;
		_placeholder.Margin = new Thickness((leadingGlyph != null) ? 8 : 12, 0.0, 0.0, 0.0);
		Grid.SetColumn(_placeholder, 1);
		grid.Children.Add(_placeholder);
		if (!string.IsNullOrEmpty(trailingHint))
		{
			TextBlock textBlock = SettingsTheme.Text(trailingHint, 12.0, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock.TextWrapping = TextWrapping.Wrap;
			Border element = new Border
			{
				Background = ThemeTokens.Surface3,
				CornerRadius = new CornerRadius(4.0),
				Padding = new Thickness(8.0, 4.0, 8.0, 4.0),
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				Child = textBlock,
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false
			};
			Grid.SetColumn(element, 2);
			grid.Children.Add(element);
		}
		Child = grid;
		Box.TextChanged += delegate
		{
			UpdatePlaceholder();
		};
		AynType.ApplyTextMode(Box, 13.5);
		AynType.Tabular(Box);
		Box.GotKeyboardFocus += delegate
		{
			RefreshState();
		};
		Box.LostKeyboardFocus += delegate
		{
			_press = false;
			RefreshState();
		};
		base.MouseEnter += delegate
		{
			RefreshState();
		};
		base.MouseLeave += delegate
		{
			RefreshState();
		};
		base.PreviewMouseLeftButtonDown += delegate
		{
			_press = base.IsEnabled;
			RefreshState();
		};
		base.PreviewMouseLeftButtonUp += delegate
		{
			_press = false;
			RefreshState();
		};
		base.LostMouseCapture += delegate
		{
			_press = false;
			RefreshState();
		};
		base.IsEnabledChanged += delegate
		{
			_press = false;
			RefreshState();
		};
		ThemeTokens.Subscribe(this, delegate(STextBox input)
		{
			input.RefreshState();
		});
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (!Box.IsKeyboardFocusWithin)
			{
				Box.Focus();
				e.Handled = true;
			}
		};
		UpdatePlaceholder();
	}

	private void RefreshState()
	{
		base.Opacity = (base.IsEnabled ? 1.0 : 0.4);
		base.Background = ((!base.IsEnabled) ? SettingsTheme.InputBg : (_press ? SettingsTheme.ControlPressed : (base.IsMouseOver ? SettingsTheme.ControlHover : SettingsTheme.InputBg)));
		base.BorderBrush = ((base.IsEnabled && Box.IsKeyboardFocusWithin) ? SettingsTheme.InputBorderFocus : SettingsTheme.InputBorder);
	}

	private void UpdatePlaceholder()
	{
		_placeholder.Visibility = ((!string.IsNullOrEmpty(Box.Text)) ? Visibility.Collapsed : Visibility.Visible);
	}
}
