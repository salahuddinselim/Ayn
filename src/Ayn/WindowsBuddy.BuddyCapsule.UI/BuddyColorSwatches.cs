using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyColorSwatches : StackPanel
{
	public class SwatchItem
	{
		public string Key { get; set; }

		public string Tooltip { get; set; }

		public Brush Fill { get; set; }
	}

	private readonly List<Border> _rings = new List<Border>();

	private Border _customSwatchRing;

	private Border _customWheelBorder;

	private Border _customPip;

	private string _selectedKey;

	public UIElement CustomSwatchElement => _customSwatchRing;

	public string SelectedKey
	{
		get
		{
			return _selectedKey;
		}
		set
		{
			_selectedKey = value;
			UpdateSelection();
		}
	}

	public event Action<string> SelectionChanged;

	public event Action CustomColorPickerRequested;

	public BuddyColorSwatches(string initialKey = "Arctic White")
	{
		base.Orientation = Orientation.Vertical;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		_selectedKey = initialKey;
		List<SwatchItem> list = new List<SwatchItem>
		{
			new SwatchItem
			{
				Key = "Arctic White",
				Tooltip = "01 — Arctic White (#F5F7FA)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(245, 247, 250)))
			},
			new SwatchItem
			{
				Key = "Champagne Gold",
				Tooltip = "02 — Champagne Gold (#E7B85C)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(231, 184, 92)))
			},
			new SwatchItem
			{
				Key = "Amber",
				Tooltip = "03 — Amber (#F59E0B)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(245, 158, 11)))
			},
			new SwatchItem
			{
				Key = "Emerald",
				Tooltip = "04 — Emerald (#27C98A)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(39, 201, 138)))
			},
			new SwatchItem
			{
				Key = "Teal",
				Tooltip = "05 — Teal (#20B8B0)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(32, 184, 176)))
			},
			new SwatchItem
			{
				Key = "Azure",
				Tooltip = "06 — Azure (#3B9EFF)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(59, 158, byte.MaxValue)))
			},
			new SwatchItem
			{
				Key = "Indigo",
				Tooltip = "07 — Indigo (#6366F1)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(99, 102, 241)))
			}
		};
		List<SwatchItem> list2 = new List<SwatchItem>
		{
			new SwatchItem
			{
				Key = "Violet",
				Tooltip = "08 — Violet (#9B6DFF)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(155, 109, byte.MaxValue)))
			},
			new SwatchItem
			{
				Key = "Rose",
				Tooltip = "09 — Rose (#F06B8A)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(240, 107, 138)))
			},
			new SwatchItem
			{
				Key = "Coral",
				Tooltip = "10 — Coral (#F0785F)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(240, 120, 95)))
			},
			new SwatchItem
			{
				Key = "Copper",
				Tooltip = "11 — Copper (#C97A45)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(201, 122, 69)))
			},
			new SwatchItem
			{
				Key = "Ice Blue",
				Tooltip = "12 — Ice Blue (#69C7E8)",
				Fill = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(105, 199, 232)))
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 3.0)
		};
		foreach (SwatchItem item in list)
		{
			stackPanel.Children.Add(CreateSwatch(item.Key, item.Tooltip, item.Fill, isCustom: false));
		}
		base.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		foreach (SwatchItem item2 in list2)
		{
			stackPanel2.Children.Add(CreateSwatch(item2.Key, item2.Tooltip, item2.Fill, isCustom: false));
		}
		_customSwatchRing = CreateSwatch("Custom", "Custom Color Picker...", null, isCustom: true);
		stackPanel2.Children.Add(_customSwatchRing);
		base.Children.Add(stackPanel2);
		UpdateSelection();
	}

	private Border CreateSwatch(string key, string tooltip, Brush fill, bool isCustom)
	{
		Border ring = new Border
		{
			Width = 22.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(11.0),
			BorderThickness = new Thickness(1.5),
			BorderBrush = Brushes.Transparent,
			Background = Brushes.Transparent,
			Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
			Cursor = Cursors.Hand,
			ToolTip = tooltip,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true
		};
		if (isCustom)
		{
			Border border = new Border();
			border.Width = 14.0;
			border.Height = 14.0;
			border.CornerRadius = new CornerRadius(7.0);
			border.BorderThickness = new Thickness(1.5);
			border.Background = new SolidColorBrush(Color.FromRgb(22, 22, 26));
			border.HorizontalAlignment = HorizontalAlignment.Center;
			border.VerticalAlignment = VerticalAlignment.Center;
			border.IsHitTestVisible = false;
			Border border2 = border;
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.StartPoint = new Point(0.0, 0.0);
			linearGradientBrush.EndPoint = new Point(1.0, 1.0);
			linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(240, 107, 138), 0.0));
			linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(231, 184, 92), 0.33));
			linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(39, 201, 138), 0.66));
			linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(59, 158, byte.MaxValue), 1.0));
			linearGradientBrush.Freeze();
			border2.BorderBrush = linearGradientBrush;
			Border border3 = new Border();
			border3.Width = 5.0;
			border3.Height = 5.0;
			border3.CornerRadius = new CornerRadius(2.5);
			border3.Background = ThemeTokens.Text2;
			border3.HorizontalAlignment = HorizontalAlignment.Center;
			border3.VerticalAlignment = VerticalAlignment.Center;
			Border border4 = border3;
			_customWheelBorder = border2;
			_customPip = border4;
			border2.Child = border4;
			ring.Child = border2;
		}
		else
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = 13.0;
			ellipse.Height = 13.0;
			ellipse.Fill = fill;
			ellipse.HorizontalAlignment = HorizontalAlignment.Center;
			ellipse.VerticalAlignment = VerticalAlignment.Center;
			ellipse.IsHitTestVisible = false;
			Ellipse child = ellipse;
			ring.Child = child;
		}
		ring.Tag = key;
		ring.MouseEnter += delegate
		{
			if (ring.BorderBrush != ThemeTokens.Text)
			{
				ring.BorderBrush = ThemeTokens.BorderStrong;
			}
		};
		ring.MouseLeave += delegate
		{
			if (ring.BorderBrush != ThemeTokens.Text)
			{
				ring.BorderBrush = Brushes.Transparent;
			}
		};
		ring.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			_selectedKey = key;
			UpdateSelection();
			if (string.Equals(key, "Custom", StringComparison.OrdinalIgnoreCase))
			{
				if (this.CustomColorPickerRequested != null)
				{
					this.CustomColorPickerRequested();
				}
			}
			else if (this.SelectionChanged != null)
			{
				this.SelectionChanged(key);
			}
		};
		_rings.Add(ring);
		return ring;
	}

	private void UpdateSelection()
	{
		bool flag = string.Equals(_selectedKey, "Custom", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Multicolor", StringComparison.OrdinalIgnoreCase) || (_selectedKey != null && _selectedKey.StartsWith("#"));
		if (_customPip != null)
		{
			if (flag)
			{
				Color edgeLightColor = NotchConfig.Instance.GetEdgeLightColor();
				_customPip.Background = new SolidColorBrush(edgeLightColor);
			}
			else
			{
				_customPip.Background = ThemeTokens.Text2;
			}
		}
		foreach (Border ring in _rings)
		{
			string text = ring.Tag as string;
			bool flag2 = false;
			flag2 = ((!string.Equals(text, "Custom", StringComparison.OrdinalIgnoreCase)) ? (string.Equals(_selectedKey, text, StringComparison.OrdinalIgnoreCase) || (string.Equals(text, "Arctic White", StringComparison.OrdinalIgnoreCase) && string.Equals(_selectedKey, "White", StringComparison.OrdinalIgnoreCase)) || (string.Equals(text, "Champagne Gold", StringComparison.OrdinalIgnoreCase) && string.Equals(_selectedKey, "Gold", StringComparison.OrdinalIgnoreCase)) || (string.Equals(text, "Azure", StringComparison.OrdinalIgnoreCase) && (string.Equals(_selectedKey, "Blue", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Blue/Slate", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Slate", StringComparison.OrdinalIgnoreCase))) || (string.Equals(text, "Violet", StringComparison.OrdinalIgnoreCase) && (string.Equals(_selectedKey, "Mauve", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Plum", StringComparison.OrdinalIgnoreCase))) || (string.Equals(text, "Rose", StringComparison.OrdinalIgnoreCase) && (string.Equals(_selectedKey, "Pink", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Blush", StringComparison.OrdinalIgnoreCase))) || (string.Equals(text, "Copper", StringComparison.OrdinalIgnoreCase) && (string.Equals(_selectedKey, "Bronze", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Terracotta", StringComparison.OrdinalIgnoreCase))) || (string.Equals(text, "Ice Blue", StringComparison.OrdinalIgnoreCase) && (string.Equals(_selectedKey, "Ivory", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Cream", StringComparison.OrdinalIgnoreCase) || string.Equals(_selectedKey, "Sage", StringComparison.OrdinalIgnoreCase)))) : flag);
			ring.BorderBrush = (flag2 ? ThemeTokens.Text : Brushes.Transparent);
		}
	}
}
