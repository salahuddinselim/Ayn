using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyTextField : Grid
{
	private TextBox _textBox;

	private TextBlock _placeholderBlock;

	private Border _border;

	public string Text
	{
		get
		{
			return _textBox.Text;
		}
		set
		{
			_textBox.Text = value ?? "";
		}
	}

	public TextBox InnerTextBox => _textBox;

	public event TextChangedEventHandler TextChanged;

	public BuddyTextField(string placeholder = "", double height = 30.0)
	{
		base.Height = height;
		base.VerticalAlignment = VerticalAlignment.Center;
		_border = new Border
		{
			Background = BuddyTokens.ToolSurfaceBrush,
			BorderBrush = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0)
		};
		Grid grid = new Grid();
		_placeholderBlock = new TextBlock
		{
			Text = placeholder,
			FontSize = 13.0,
			Foreground = BuddyTokens.TextMutedBrush,
			FontFamily = BuddyTypography.FontText,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			Visibility = Visibility.Visible
		};
		grid.Children.Add(_placeholderBlock);
		_textBox = new TextBox
		{
			FontSize = 13.0,
			Foreground = BuddyTokens.TextPrimaryBrush,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			CaretBrush = ThemeTokens.Text,
			SelectionBrush = ThemeTokens.Accent,
			FontFamily = BuddyTypography.FontText,
			VerticalAlignment = VerticalAlignment.Center,
			Focusable = true,
			FocusVisualStyle = null
		};
		TextBox textBox = _textBox;
		TextChangedEventHandler value = delegate(object s, TextChangedEventArgs e)
		{
			_placeholderBlock.Visibility = ((!string.IsNullOrEmpty(_textBox.Text)) ? Visibility.Collapsed : Visibility.Visible);
			this.TextChanged?.Invoke(this, e);
		};
		textBox.TextChanged += value;
		_textBox.GotFocus += delegate
		{
			_border.BorderBrush = BuddyTokens.FocusRingBrush;
			_border.BorderThickness = new Thickness(1.0);
		};
		_textBox.LostFocus += delegate
		{
			_border.BorderThickness = new Thickness(0.0);
		};
		_border.MouseLeftButtonDown += delegate
		{
			_textBox.Focus();
		};
		_textBox.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape && !string.IsNullOrEmpty(_textBox.Text))
			{
				_textBox.Text = "";
				e.Handled = true;
			}
		};
		grid.Children.Add(_textBox);
		_border.Child = grid;
		base.Children.Add(_border);
	}

	public void FocusInput()
	{
		_textBox.Focus();
	}

	public void SelectAll()
	{
		_textBox.SelectAll();
	}

	public static TextBox Create(string placeholder = "", double fontSize = 13.0)
	{
		TextBox tb = new TextBox
		{
			FontSize = fontSize,
			Foreground = BuddyTokens.TextPrimaryBrush,
			Background = BuddyTokens.ToolSurfaceBrush,
			BorderBrush = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			CaretBrush = ThemeTokens.Text,
			SelectionBrush = ThemeTokens.Accent,
			FontFamily = BuddyTypography.FontText,
			Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
			VerticalAlignment = VerticalAlignment.Center,
			FocusVisualStyle = null,
			Focusable = true
		};
		if (!string.IsNullOrEmpty(placeholder))
		{
			tb.ToolTip = BuddyTooltip.Create(placeholder);
		}
		tb.GotFocus += delegate
		{
			tb.BorderBrush = BuddyTokens.FocusRingBrush;
			tb.BorderThickness = new Thickness(1.0);
		};
		tb.LostFocus += delegate
		{
			tb.BorderThickness = new Thickness(0.0);
		};
		return tb;
	}
}
