using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddyInlineSearch : Grid
{
	private readonly FrameworkElement _covered;

	private readonly TextBlock _hint;

	private readonly DispatcherTimer _debounce;

	private string _query = string.Empty;

	public TextBox Box { get; private set; }

	public string Query => _query;

	public bool IsOpen => base.Visibility == Visibility.Visible;

	public event Action<string> QueryChanged;

	public event Action Closed;

	public BuddyInlineSearch(FrameworkElement covered, string placeholder, double height)
		: this(covered, placeholder, height, flat: false)
	{
	}

	public BuddyInlineSearch(FrameworkElement covered, string placeholder, double height, bool flat)
	{
		_covered = covered;
		base.Height = height;
		base.VerticalAlignment = VerticalAlignment.Top;
		base.Visibility = Visibility.Collapsed;
		double size = (flat ? 16.0 : 18.0);
		Border border = (flat ? new Border
		{
			Background = Brushes.Transparent
		} : new Border
		{
			Background = BuddyTheme.Row,
			BorderBrush = BuddyTheme.Hairline,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(height / 2.0),
			Padding = new Thickness(12.0, 0.0, 4.0, 0.0)
		});
		if (flat)
		{
			base.Children.Add(new Rectangle
			{
				Height = 1.0,
				Fill = BuddyTheme.Hairline,
				VerticalAlignment = VerticalAlignment.Bottom,
				IsHitTestVisible = false
			});
		}
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
		FrameworkElement frameworkElement = FluentIcons.Create("search", size, BuddyTheme.Text50, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid.Children.Add(frameworkElement);
		_hint = BuddyTheme.Text(placeholder, 13.0, BuddyTheme.Text50, flat ? FontWeights.Medium : FontWeights.Normal);
		_hint.VerticalAlignment = VerticalAlignment.Center;
		_hint.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		_hint.IsHitTestVisible = false;
		Grid.SetColumn(_hint, 1);
		grid.Children.Add(_hint);
		Box = new TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			FontFamily = BuddyTheme.Font,
			FontSize = 13.0,
			VerticalAlignment = VerticalAlignment.Center,
			VerticalContentAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8.0, 0.0, 4.0, 0.0),
			Padding = new Thickness(0.0),
			FocusVisualStyle = null
		};
		BuddyTheme.ApplyTextMode(Box, 13.0);
		Grid.SetColumn(Box, 1);
		grid.Children.Add(Box);
		_debounce = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(120.0)
		};
		_debounce.Tick += delegate
		{
			_debounce.Stop();
			string text = Box.Text.Trim();
			if (!(text == _query))
			{
				_query = text;
				this.QueryChanged?.Invoke(text);
			}
		};
		Box.TextChanged += delegate
		{
			_hint.Visibility = ((Box.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
			_debounce.Stop();
			_debounce.Start();
		};
		Border border2;
		if (flat)
		{
			border2 = BuddyKit.GlyphButton("dismiss", 14.0, "Close search (Esc)", Close, out var _);
			border2.Margin = new Thickness(0.0, 0.0, -5.0, 0.0);
		}
		else
		{
			border2 = BuddyKit.CircleButton("dismiss", 28.0, "Close search (Esc)", Close);
			border2.Background = Brushes.Transparent;
		}
		border2.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(border2, 2);
		grid.Children.Add(border2);
		border.Child = grid;
		base.Children.Add(border);
	}

	public void Open()
	{
		if (!IsOpen)
		{
			base.Visibility = Visibility.Visible;
			base.Opacity = 0.0;
			BuddyTheme.Animate(this, UIElement.OpacityProperty, 1.0, BuddyTheme.EntryFade);
			if (_covered != null)
			{
				_covered.IsHitTestVisible = false;
				BuddyTheme.Animate(_covered, UIElement.OpacityProperty, 0.0, BuddyTheme.EntryFade);
			}
		}
		Box.Focus();
		Keyboard.Focus(Box);
		Box.SelectAll();
	}

	public void Close()
	{
		if (IsOpen)
		{
			Box.Text = string.Empty;
			_debounce.Stop();
			bool flag = _query.Length > 0;
			_query = string.Empty;
			base.Visibility = Visibility.Collapsed;
			if (_covered != null)
			{
				_covered.IsHitTestVisible = true;
				BuddyTheme.Animate(_covered, UIElement.OpacityProperty, 1.0, BuddyTheme.EntryFade);
			}
			if (flag)
			{
				this.QueryChanged?.Invoke(string.Empty);
			}
			this.Closed?.Invoke();
		}
	}

	public void StopDebounce()
	{
		_debounce.Stop();
	}
}
