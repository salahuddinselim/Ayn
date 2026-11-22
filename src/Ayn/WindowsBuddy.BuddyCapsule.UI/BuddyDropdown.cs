using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyDropdown
{
	private static Popup _popup;

	private static DateTime _closedUtc;

	private static FrameworkElement _closedAnchor;

	public static bool IsOpen
	{
		get
		{
			if (_popup != null)
			{
				return _popup.IsOpen;
			}
			return false;
		}
	}

	public static FrameworkElement Anchor
	{
		get
		{
			if (!IsOpen)
			{
				return null;
			}
			return _popup.PlacementTarget as FrameworkElement;
		}
	}

	public static event Action Closed;

	public static bool JustClosed(FrameworkElement anchor)
	{
		if (anchor != null && anchor == _closedAnchor)
		{
			return (DateTime.UtcNow - _closedUtc).TotalMilliseconds < 300.0;
		}
		return false;
	}

	public static void Close()
	{
		if (_popup != null)
		{
			_popup.IsOpen = false;
		}
	}

	public static void Show(FrameworkElement anchor, IList<BuddyMenuItem> items, string emptyText = null, double minWidth = 180.0)
	{
		if (anchor == null)
		{
			return;
		}
		bool flag = false;
		foreach (BuddyMenuItem item in items)
		{
			if (item.IsChecked || item.Icon != null)
			{
				flag = true;
				break;
			}
		}
		StackPanel stackPanel = new StackPanel();
		if (items.Count == 0 && emptyText != null)
		{
			TextBlock textBlock = BuddyTheme.SecondaryText(emptyText);
			textBlock.Margin = new Thickness(10.0, 8.0, 10.0, 8.0);
			stackPanel.Children.Add(textBlock);
		}
		foreach (BuddyMenuItem item2 in items)
		{
			BuddyMenuItem it = item2;
			Grid grid = new Grid();
			grid.Height = 32.0;
			grid.Background = Brushes.Transparent;
			grid.Cursor = Cursors.Hand;
			Grid grid2 = grid;
			Border border = new Border
			{
				CornerRadius = new CornerRadius(8.0),
				Child = grid2
			};
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(flag ? 28 : 10)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			if (it.IsChecked || it.Icon != null)
			{
				FrameworkElement frameworkElement = FluentIcons.Create(it.IsChecked ? "checkmark" : it.Icon, 16.0, it.IsChecked ? BuddyTheme.Text100 : BuddyTheme.Text92, filled: false);
				frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				grid2.Children.Add(frameworkElement);
			}
			TextBlock textBlock2 = BuddyTheme.BodyText(it.Label);
			textBlock2.MaxWidth = 240.0;
			textBlock2.VerticalAlignment = VerticalAlignment.Center;
			textBlock2.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
			Grid.SetColumn(textBlock2, 1);
			grid2.Children.Add(textBlock2);
			border.MouseEnter += delegate
			{
				border.Background = BuddyTheme.RowHover;
			};
			border.MouseLeave += delegate
			{
				border.Background = Brushes.Transparent;
			};
			border.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				Close();
				if (it.Invoke != null)
				{
					it.Invoke();
				}
			};
			stackPanel.Children.Add(border);
		}
		Open(anchor, stackPanel, minWidth, 4.0);
	}

	public static void ShowContent(FrameworkElement anchor, FrameworkElement content, double padding)
	{
		if (anchor != null && content != null)
		{
			Open(anchor, content, 0.0, padding);
		}
	}

	private static void Open(FrameworkElement anchor, FrameworkElement content, double minWidth, double padding)
	{
		Border chrome = BuddyTheme.Crisp(new Border
		{
			Background = BuddyTheme.CardFill,
			BorderBrush = BuddyTheme.CardBorderHover,
			BorderThickness = new Thickness(0.0),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(padding),
			MinWidth = minWidth,
			Child = new BuddyScrollViewer
			{
				Content = content,
				MaxHeight = Math.Max(120.0, SystemParameters.WorkArea.Height - 80.0)
			}
		});
		BuddyTheme.ApplyTextMode(chrome, 13.0);
		chrome.Focusable = true;
		chrome.FocusVisualStyle = null;
		chrome.Loaded += delegate
		{
			chrome.Focus();
		};
		chrome.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
				anchor.Focus();
				e.Handled = true;
			}
		};
		if (_popup == null)
		{
			Popup popup = new Popup();
			popup.AllowsTransparency = true;
			popup.StaysOpen = false;
			popup.Placement = PlacementMode.Custom;
			popup.PopupAnimation = PopupAnimation.None;
			_popup = popup;
			_popup.CustomPopupPlacementCallback = (Size popupSize, Size targetSize, Point offset) => new CustomPopupPlacement[1]
			{
				new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width, targetSize.Height + 8.0), PopupPrimaryAxis.Horizontal)
			};
			_popup.Closed += delegate
			{
				_closedUtc = DateTime.UtcNow;
				_closedAnchor = _popup.PlacementTarget as FrameworkElement;
				BuddyDropdown.Closed?.Invoke();
			};
		}
		_popup.IsOpen = false;
		_popup.PlacementTarget = anchor;
		_popup.Child = chrome;
		chrome.Opacity = 0.0;
		_popup.IsOpen = true;
		BuddyTheme.Animate(chrome, UIElement.OpacityProperty, 1.0, BuddyTheme.EntryFade);
	}
}
