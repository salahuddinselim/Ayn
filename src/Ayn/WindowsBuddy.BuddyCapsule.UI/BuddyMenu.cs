using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyMenu : Popup
{
	private readonly StackPanel _itemsPanel;

	private readonly Border _card;

	public BuddyMenu()
	{
		base.AllowsTransparency = true;
		base.Placement = PlacementMode.Custom;
		base.StaysOpen = false;
		base.Focusable = true;
		_card = new Border
		{
			Background = ThemeTokens.Surface,
			BorderBrush = ThemeTokens.BorderStrong,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(0.0, 8.0, 0.0, 8.0),
			MinWidth = 164.0,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true
		};
		_itemsPanel = new StackPanel
		{
			Orientation = Orientation.Vertical
		};
		_card.Child = _itemsPanel;
		base.Child = _card;
		KeyEventHandler value = delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				base.IsOpen = false;
				e.Handled = true;
			}
		};
		base.PreviewKeyDown += value;
	}

	public void AddItem(string header, Action onClick)
	{
		Border itemRow = new Border
		{
			Height = 32.0,
			Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand
		};
		TextBlock textBlock = new TextBlock();
		textBlock.Text = header;
		textBlock.FontSize = 13.0;
		textBlock.Foreground = BuddyTokens.Text100Brush;
		textBlock.FontFamily = BuddyTokens.FontText;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock child = textBlock;
		itemRow.Child = child;
		itemRow.MouseEnter += delegate
		{
			itemRow.Background = ThemeTokens.Surface2;
		};
		itemRow.MouseLeave += delegate
		{
			itemRow.Background = Brushes.Transparent;
		};
		itemRow.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			base.IsOpen = false;
			if (onClick != null)
			{
				onClick();
			}
		};
		_itemsPanel.Children.Add(itemRow);
	}

	public void Show(FrameworkElement anchor, bool preferAbove = false)
	{
		if (anchor != null)
		{
			base.PlacementTarget = anchor;
			base.CustomPopupPlacementCallback = delegate(Size popupSize, Size targetSize, Point offset)
			{
				double x = targetSize.Width - popupSize.Width;
				double y = ((!preferAbove) ? (targetSize.Height + 6.0) : (0.0 - popupSize.Height - 6.0));
				Point point = new Point(x, y);
				Point point2 = (preferAbove ? new Point(x, targetSize.Height + 6.0) : new Point(x, 0.0 - popupSize.Height - 6.0));
				return new CustomPopupPlacement[2]
				{
					new CustomPopupPlacement(point, PopupPrimaryAxis.Horizontal),
					new CustomPopupPlacement(point2, PopupPrimaryAxis.Horizontal)
				};
			};
			base.IsOpen = true;
		}
	}
}
