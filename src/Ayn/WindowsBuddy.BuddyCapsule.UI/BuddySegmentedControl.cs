using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddySegmentedControl : Border
{
	private readonly List<string> _keys = new List<string>();

	private readonly List<Border> _segmentBorders = new List<Border>();

	private readonly List<TextBlock> _segmentLabels = new List<TextBlock>();

	private int _selectedIndex;

	public string SelectedKey
	{
		get
		{
			if (_selectedIndex < 0 || _selectedIndex >= _keys.Count)
			{
				return string.Empty;
			}
			return _keys[_selectedIndex];
		}
	}

	public int SelectedIndex => _selectedIndex;

	public event EventHandler<string> SelectionChanged;

	public BuddySegmentedControl(Tuple<string, string>[] items, int initialIndex = 0)
	{
		base.Background = BuddyTokens.SegmentedSurfaceBrush;
		base.BorderThickness = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(16.0);
		base.Padding = new Thickness(2.0);
		base.Height = 30.0;
		base.HorizontalAlignment = HorizontalAlignment.Center;
		base.VerticalAlignment = VerticalAlignment.Center;
		base.Focusable = true;
		base.FocusVisualStyle = null;
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		for (int i = 0; i < items.Length; i++)
		{
			int idx = i;
			string item = items[i].Item1;
			string item2 = items[i].Item2;
			_keys.Add(item);
			Border border = new Border
			{
				CornerRadius = new CornerRadius(12.0),
				Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
				Height = 26.0,
				Cursor = Cursors.Hand,
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0.0)
			};
			TextBlock tb = new TextBlock
			{
				Text = item2,
				FontSize = 11.5,
				FontWeight = FontWeights.Medium,
				FontFamily = BuddyTypography.FontText,
				Foreground = BuddyTokens.TextMutedBrush,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			border.Child = tb;
			border.MouseLeftButtonUp += delegate
			{
				SelectIndex(idx);
			};
			border.MouseEnter += delegate
			{
				if (idx != _selectedIndex)
				{
					tb.Foreground = BuddyTokens.TextSecondaryBrush;
				}
			};
			border.MouseLeave += delegate
			{
				if (idx != _selectedIndex)
				{
					tb.Foreground = BuddyTokens.TextMutedBrush;
				}
			};
			_segmentBorders.Add(border);
			_segmentLabels.Add(tb);
			stackPanel.Children.Add(border);
		}
		Child = stackPanel;
		SelectIndex(initialIndex, fireEvent: false);
		base.GotFocus += delegate
		{
			base.BorderBrush = BuddyTokens.FocusRingBrush;
			base.BorderThickness = new Thickness(1.0);
		};
		base.LostFocus += delegate
		{
			base.BorderThickness = new Thickness(0.0);
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Left || e.Key == Key.Up)
			{
				if (_selectedIndex > 0)
				{
					SelectIndex(_selectedIndex - 1);
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Right || e.Key == Key.Down)
			{
				if (_selectedIndex < _keys.Count - 1)
				{
					SelectIndex(_selectedIndex + 1);
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Home)
			{
				SelectIndex(0);
				e.Handled = true;
			}
			else if (e.Key == Key.End)
			{
				SelectIndex(_keys.Count - 1);
				e.Handled = true;
			}
		};
	}

	public void SelectIndex(int index, bool fireEvent = true)
	{
		if (index >= -1 && index < _keys.Count)
		{
			_selectedIndex = index;
			for (int i = 0; i < _segmentBorders.Count; i++)
			{
				bool flag = i == index;
				_segmentBorders[i].Background = (flag ? BuddyTokens.ElevatedSurfaceBrush : Brushes.Transparent);
				_segmentLabels[i].Foreground = (flag ? BuddyTokens.TextPrimaryBrush : BuddyTokens.TextMutedBrush);
				_segmentLabels[i].FontWeight = (flag ? AynType.Medium : AynType.Regular);
			}
			if (fireEvent && _selectedIndex >= 0 && _selectedIndex < _keys.Count)
			{
				this.SelectionChanged?.Invoke(this, _keys[_selectedIndex]);
			}
		}
	}
}
