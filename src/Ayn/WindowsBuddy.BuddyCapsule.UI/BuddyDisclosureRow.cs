using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyDisclosureRow : Border
{
	private TextBlock _valueLabel;

	public string CurrentValue
	{
		get
		{
			if (_valueLabel == null)
			{
				return string.Empty;
			}
			return _valueLabel.Text;
		}
		set
		{
			if (_valueLabel != null)
			{
				_valueLabel.Text = value ?? string.Empty;
			}
		}
	}

	public event EventHandler Clicked;

	public BuddyDisclosureRow(string title, string description = null, string currentValue = null, UIElement trailingControl = null, bool showChevron = true)
	{
		base.Background = Brushes.Transparent;
		base.BorderThickness = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(8.0);
		base.Cursor = Cursors.Hand;
		base.Height = (string.IsNullOrEmpty(description) ? 40.0 : 48.0);
		Grid grid = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock element = new TextBlock
		{
			Text = title,
			FontSize = 13.0,
			FontWeight = FontWeights.Medium,
			Foreground = BuddyTokens.TextPrimaryBrush,
			FontFamily = BuddyTypography.FontText,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		stackPanel.Children.Add(element);
		if (!string.IsNullOrEmpty(description))
		{
			TextBlock element2 = new TextBlock
			{
				Text = description,
				FontSize = 10.0,
				FontWeight = FontWeights.Normal,
				Foreground = BuddyTokens.TextMutedBrush,
				FontFamily = BuddyTypography.FontText,
				Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
				TextTrimming = TextTrimming.CharacterEllipsis
			};
			stackPanel.Children.Add(element2);
		}
		Grid.SetColumn(stackPanel, 0);
		grid.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		if (trailingControl != null)
		{
			((FrameworkElement)trailingControl).VerticalAlignment = VerticalAlignment.Center;
			stackPanel2.Children.Add(trailingControl);
		}
		else
		{
			if (!string.IsNullOrEmpty(currentValue))
			{
				_valueLabel = new TextBlock
				{
					Text = currentValue,
					FontSize = 11.5,
					FontWeight = FontWeights.Normal,
					Foreground = BuddyTokens.TextSecondaryBrush,
					FontFamily = BuddyTypography.FontText,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
				};
				stackPanel2.Children.Add(_valueLabel);
			}
			if (showChevron)
			{
				ContentControl element3 = new ContentControl
				{
					Content = VectorIcons.CreateChevronRight(10.0, BuddyTokens.TextMutedBrush),
					VerticalAlignment = VerticalAlignment.Center
				};
				stackPanel2.Children.Add(element3);
			}
		}
		Grid.SetColumn(stackPanel2, 1);
		grid.Children.Add(stackPanel2);
		Child = grid;
		base.MouseEnter += delegate
		{
			base.Background = BuddyTokens.ListRowHoverBrush;
		};
		base.MouseLeave += delegate
		{
			base.Background = Brushes.Transparent;
		};
		base.MouseLeftButtonUp += delegate
		{
			this.Clicked?.Invoke(this, EventArgs.Empty);
		};
	}
}
