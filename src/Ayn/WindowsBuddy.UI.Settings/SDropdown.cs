using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal class SDropdown : SClickable
{
	private readonly List<KeyValuePair<string, string>> _items;

	private string _selected;

	private readonly TextBlock _label;

	private Popup _popup;

	private StackPanel _list;

	private int _highlight = -1;

	internal bool IsExpanded
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

	public string Selected
	{
		get
		{
			return _selected;
		}
		set
		{
			_selected = value;
			_label.Text = LabelOf(value);
		}
	}

	public event Action<string> Changed;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsDropdownPeer(this);
	}

	internal void SetExpanded(bool value)
	{
		if (value != IsExpanded)
		{
			Toggle();
		}
	}

	public SDropdown(IList<KeyValuePair<string, string>> items, string selected, double customWidth = 240.0, string name = null)
		: base(8.0, name)
	{
		_items = new List<KeyValuePair<string, string>>(items);
		_selected = selected;
		base.Height = 36.0;
		base.Width = ((customWidth > 0.0) ? customWidth : 240.0);
		base.Padding = new Thickness(16.0, 0.0, 12.0, 0.0);
		base.HorizontalAlignment = HorizontalAlignment.Left;
		base.VerticalAlignment = VerticalAlignment.Center;
		base.BorderBrush = SettingsTheme.ControlBorder;
		base.BorderThickness = new Thickness(1.0);
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
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
		_label = SettingsTheme.Text(LabelOf(selected), 13.5, SettingsTheme.TextPrimary, FontWeights.Normal);
		_label.TextWrapping = TextWrapping.NoWrap;
		_label.TextTrimming = TextTrimming.CharacterEllipsis;
		grid.Children.Add(_label);
		FrameworkElement frameworkElement = Glyphs.Create("chevron_down", 10.0, SettingsTheme.TextSecondary);
		frameworkElement.Margin = new Thickness(12.0, 4.0, 0.0, 0.0);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(frameworkElement, 1);
		grid.Children.Add(frameworkElement);
		Child = grid;
		base.Click += delegate
		{
			Toggle();
		};
		base.PreviewKeyDown += OnKey;
		OnVisual();
		base.IsEnabledChanged += delegate
		{
			if (!base.IsEnabled && _popup != null)
			{
				_popup.IsOpen = false;
			}
		};
		base.Unloaded += delegate
		{
			if (_popup != null)
			{
				_popup.IsOpen = false;
			}
		};
	}

	private string LabelOf(string key)
	{
		foreach (KeyValuePair<string, string> item in _items)
		{
			if (item.Key == key)
			{
				return item.Value;
			}
		}
		return key ?? "";
	}

	protected override void OnVisual()
	{
		base.OnVisual();
	}

	private void Commit(string key)
	{
		if (!(key == _selected))
		{
			Selected = key;
			this.Changed?.Invoke(key);
		}
	}

	private void OnKey(object sender, KeyEventArgs e)
	{
		if (!base.IsEnabled)
		{
			return;
		}
		bool flag = _popup != null && _popup.IsOpen;
		int num = _items.FindIndex((KeyValuePair<string, string> kv) => kv.Key == _selected);
		if (!flag)
		{
			if (e.Key != Key.Down && e.Key != Key.Up)
			{
				return;
			}
			if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
			{
				Toggle();
				e.Handled = true;
				return;
			}
			int num2 = num + ((e.Key == Key.Down) ? 1 : (-1));
			if (num2 >= 0 && num2 < _items.Count)
			{
				Commit(_items[num2].Key);
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Escape)
		{
			_popup.IsOpen = false;
			Focus();
			e.Handled = true;
		}
		else if (e.Key == Key.Down)
		{
			SetHighlight(Math.Min(_items.Count - 1, _highlight + 1));
			e.Handled = true;
		}
		else if (e.Key == Key.Up)
		{
			SetHighlight(Math.Max(0, _highlight - 1));
			e.Handled = true;
		}
		else if (e.Key == Key.Return || e.Key == Key.Space)
		{
			if (_highlight >= 0)
			{
				Commit(_items[_highlight].Key);
			}
			_popup.IsOpen = false;
			e.Handled = true;
		}
		else if (e.Key == Key.Tab)
		{
			_popup.IsOpen = false;
			Focus();
		}
	}

	private void Toggle()
	{
		if (_popup != null && _popup.IsOpen)
		{
			_popup.IsOpen = false;
			return;
		}
		BuildPopup();
		_popup.IsOpen = true;
	}

	private void BuildPopup()
	{
		_list = new StackPanel();
		for (int i = 0; i < _items.Count; i++)
		{
			int index = i;
			KeyValuePair<string, string> kv2 = _items[i];
			Grid grid = new Grid();
			grid.MinHeight = 36.0;
			grid.Background = Brushes.Transparent;
			grid.Cursor = Cursors.Hand;
			Grid grid2 = grid;
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(22.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			if (kv2.Key == _selected)
			{
				FrameworkElement frameworkElement = Glyphs.Create("check", 12.0, SettingsTheme.TextPrimary);
				frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				grid2.Children.Add(frameworkElement);
			}
			TextBlock textBlock = SettingsTheme.Text(kv2.Value, 13.5, SettingsTheme.TextPrimary, FontWeights.Normal);
			textBlock.TextWrapping = TextWrapping.Wrap;
			textBlock.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
			Grid.SetColumn(textBlock, 1);
			grid2.Children.Add(textBlock);
			SClickable sClickable = new SClickable(7.0, kv2.Value);
			sClickable.Child = grid2;
			sClickable.VisualRole = SVisualRole.Navigation;
			sClickable.IsSelected = kv2.Key == _selected;
			SClickable sClickable2 = sClickable;
			sClickable2.MouseEnter += delegate
			{
				SetHighlight(index);
			};
			sClickable2.Click += delegate
			{
				Commit(kv2.Key);
				_popup.IsOpen = false;
				Focus();
			};
			sClickable2.PreviewKeyDown += OnKey;
			_list.Children.Add(sClickable2);
		}
		BuddyScrollViewer buddyScrollViewer = new BuddyScrollViewer();
		buddyScrollViewer.Content = _list;
		buddyScrollViewer.MaxHeight = 320.0;
		buddyScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		BuddyScrollViewer child = buddyScrollViewer;
		Border border = new Border();
		border.Background = SettingsTheme.PopupBg;
		border.BorderBrush = SettingsTheme.Hairline;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(8.0);
		border.Padding = new Thickness(4.0);
		border.MinWidth = Math.Max(base.ActualWidth, 160.0);
		border.MaxWidth = Math.Max(160.0, Math.Min(400.0, SystemParameters.WorkArea.Width - 16.0));
		border.Child = child;
		border.Margin = new Thickness(0.0, 0.0, 8.0, 8.0);
		border.Effect = SettingsTheme.SoftShadow(16.0, 4.0, 0.45);
		Border border2 = border;
		SettingsTheme.PrepareRoot(border2);
		_popup = new Popup
		{
			Child = border2,
			PlacementTarget = this,
			Placement = PlacementMode.Bottom,
			VerticalOffset = 4.0,
			StaysOpen = false,
			AllowsTransparency = true,
			PopupAnimation = PopupAnimation.None
		};
		TranslateTransform entry = new TranslateTransform(0.0, -4.0);
		border2.RenderTransform = entry;
		_popup.Opened += delegate
		{
			entry.BeginAnimation(TranslateTransform.YProperty, null);
			entry.Y = -4.0;
			SMotion.To(entry, TranslateTransform.YProperty, 0.0, animate: true);
		};
		_popup.Closed += delegate
		{
			_highlight = -1;
			SMotion.Finish(entry);
		};
		SetHighlight(_items.FindIndex((KeyValuePair<string, string> kv) => kv.Key == _selected));
	}

	private void SetHighlight(int i)
	{
		_highlight = i;
		if (_list != null)
		{
			for (int j = 0; j < _list.Children.Count; j++)
			{
				((SClickable)_list.Children[j]).IsActive = j == i;
			}
			if (i >= 0 && i < _list.Children.Count)
			{
				((FrameworkElement)_list.Children[i]).BringIntoView();
			}
		}
	}
}
