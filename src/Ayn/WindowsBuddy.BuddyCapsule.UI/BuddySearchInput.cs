using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddySearchInput : Grid
{
	private TextBox _textBox;

	private TextBlock _placeholderBlock;

	private Border _border;

	private BuddyIconButton _clearBtn;

	public string Query
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

	public event EventHandler<string> QueryChanged;

	public event EventHandler<string> SearchSubmitted;

	public event EventHandler SearchCleared;

	public BuddySearchInput(string placeholder = "Search...")
	{
		base.Height = 32.0;
		base.VerticalAlignment = VerticalAlignment.Center;
		_border = new Border
		{
			Background = BuddyTokens.ToolSurfaceBrush,
			BorderBrush = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			CornerRadius = new CornerRadius(16.0),
			Padding = new Thickness(8.0, 0.0, 6.0, 0.0)
		};
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(24.0)
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
		ContentControl element = new ContentControl
		{
			Content = VectorIcons.CreateSearchIcon(13.0, BuddyTokens.TextSecondaryBrush),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		Grid.SetColumn(element, 0);
		grid.Children.Add(element);
		Grid grid2 = new Grid();
		_placeholderBlock = new TextBlock
		{
			Text = placeholder,
			FontSize = 13.0,
			Foreground = BuddyTokens.TextMutedBrush,
			FontFamily = BuddyTypography.FontText,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		grid2.Children.Add(_placeholderBlock);
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
			FocusVisualStyle = null
		};
		TextBox textBox = _textBox;
		TextChangedEventHandler value = delegate
		{
			bool flag = !string.IsNullOrEmpty(_textBox.Text);
			_placeholderBlock.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			_clearBtn.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			this.QueryChanged?.Invoke(this, _textBox.Text);
		};
		textBox.TextChanged += value;
		_textBox.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				this.SearchSubmitted?.Invoke(this, _textBox.Text);
				e.Handled = true;
			}
			else if (e.Key == Key.Escape && !string.IsNullOrEmpty(_textBox.Text))
			{
				Clear();
				e.Handled = true;
			}
		};
		grid2.Children.Add(_textBox);
		Grid.SetColumn(grid2, 1);
		grid.Children.Add(grid2);
		_clearBtn = new BuddyIconButton(VectorIcons.CreateCloseIcon(8.0, BuddyTokens.TextMutedBrush), "Clear", 20.0);
		_clearBtn.Visibility = Visibility.Collapsed;
		_clearBtn.Click += delegate
		{
			Clear();
		};
		Grid.SetColumn(_clearBtn, 2);
		grid.Children.Add(_clearBtn);
		_textBox.GotFocus += delegate
		{
			_border.BorderBrush = BuddyTokens.FocusRingBrush;
			_border.BorderThickness = new Thickness(1.0);
		};
		_textBox.LostFocus += delegate
		{
			_border.BorderThickness = new Thickness(0.0);
		};
		_border.Child = grid;
		base.Children.Add(_border);
	}

	public void Clear()
	{
		_textBox.Text = "";
		this.SearchCleared?.Invoke(this, EventArgs.Empty);
		_textBox.Focus();
	}

	public void FocusInput()
	{
		_textBox.Focus();
	}
}
