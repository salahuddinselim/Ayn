using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal sealed class ChoiceCards : Grid
{
	internal sealed class Option
	{
		public string Key;

		public string Name;

		public string Description;

		public string Badge;

		public FrameworkElement Preview;

		public Option(string key, string name, string description, FrameworkElement preview, string badge = null)
		{
			Key = key;
			Name = name;
			Description = description;
			Preview = preview;
			Badge = badge;
		}
	}

	private readonly List<KeyValuePair<string, SClickable>> _cards = new List<KeyValuePair<string, SClickable>>();

	private string _selected;

	public string Selected => _selected;

	public event Action<string> Changed;

	public ChoiceCards(Option[] options, string selected, string groupName, int columns = 0)
	{
		_selected = selected;
		int cols = ((columns > 0) ? columns : options.Length);
		int preferredColumns = cols;
		int num = (int)Math.Ceiling((double)options.Length / (double)cols);
		for (int i = 0; i < cols; i++)
		{
			if (i > 0)
			{
				base.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(12.0)
				});
			}
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		for (int j = 0; j < num; j++)
		{
			if (j > 0)
			{
				base.RowDefinitions.Add(new RowDefinition
				{
					Height = new GridLength(12.0)
				});
			}
			base.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
		}
		for (int k = 0; k < options.Length; k++)
		{
			Option option = options[k];
			SClickable card = new SClickable(8.0, groupName + ": " + option.Name)
			{
				Background = ThemeTokens.Surface2,
				BorderBrush = Brushes.Transparent,
				BorderThickness = new Thickness(2.0),
				VisualRole = SVisualRole.Card,
				Padding = new Thickness(12.0, 12.0, 12.0, 12.0)
			};
			StackPanel stackPanel = new StackPanel();
			Viewbox child = ((option.Preview != null) ? new Viewbox
			{
				Child = option.Preview,
				StretchDirection = StretchDirection.DownOnly,
				Margin = new Thickness(4.0)
			} : null);
			Border element = new Border
			{
				Height = 64.0,
				CornerRadius = new CornerRadius(6.0),
				Background = SettingsPreviewStage.Background,
				Child = child,
				Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
				ClipToBounds = true
			};
			if (option.Preview != null)
			{
				option.Preview.HorizontalAlignment = HorizontalAlignment.Center;
				option.Preview.VerticalAlignment = VerticalAlignment.Center;
			}
			stackPanel.Children.Add(element);
			WrapPanel wrapPanel = new WrapPanel();
			TextBlock textBlock = SettingsTheme.Text(option.Name, 15.0, SettingsTheme.TextPrimary, AynType.Medium);
			textBlock.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			wrapPanel.Children.Add(textBlock);
			if (!string.IsNullOrEmpty(option.Badge))
			{
				wrapPanel.Children.Add(SUI.Badge(option.Badge));
			}
			stackPanel.Children.Add(wrapPanel);
			TextBlock textBlock2 = SettingsTheme.Text(option.Description, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
			textBlock2.TextWrapping = TextWrapping.Wrap;
			textBlock2.TextTrimming = TextTrimming.CharacterEllipsis;
			if (!string.IsNullOrEmpty(option.Description))
			{
				stackPanel.Children.Add(textBlock2);
			}
			card.Child = stackPanel;
			string key = option.Key;
			card.Click += delegate
			{
				Select(key, raise: true);
			};
			card.KeyDown += delegate(object s, KeyEventArgs e)
			{
				int num3 = _cards.FindIndex((KeyValuePair<string, SClickable> kv) => kv.Key == key);
				int num4 = -99;
				if (e.Key == Key.Right)
				{
					num4 = num3 + 1;
				}
				else if (e.Key == Key.Left)
				{
					num4 = num3 - 1;
				}
				else if (e.Key == Key.Down)
				{
					num4 = num3 + cols;
				}
				else if (e.Key == Key.Up)
				{
					num4 = num3 - cols;
				}
				if (num4 >= 0 && num4 < _cards.Count)
				{
					Select(_cards[num4].Key, raise: true);
					_cards[num4].Value.Focus();
					e.Handled = true;
				}
			};
			int value = k / cols * 2;
			int value2 = k % cols * 2;
			Border element2 = new Border
			{
				BorderThickness = new Thickness(2.0),
				Padding = new Thickness(0.0),
				Margin = new Thickness(-4.0),
				CornerRadius = new CornerRadius(12.0),
				BorderBrush = Brushes.Transparent,
				Child = card
			};
			ThemeTokens.Elevate(card);
			card.GotKeyboardFocus += delegate
			{
				RefreshRing(card);
			};
			card.LostKeyboardFocus += delegate
			{
				RefreshRing(card);
			};
			Grid.SetRow(element2, value);
			Grid.SetColumn(element2, value2);
			base.Children.Add(element2);
			_cards.Add(new KeyValuePair<string, SClickable>(key, card));
		}
		base.SizeChanged += delegate
		{
			int num2 = Math.Max(1, Math.Min(preferredColumns, (int)((base.ActualWidth + 12.0) / 160.0)));
			if (num2 != cols)
			{
				cols = num2;
				base.ColumnDefinitions.Clear();
				base.RowDefinitions.Clear();
				for (int l = 0; l < cols; l++)
				{
					if (l > 0)
					{
						base.ColumnDefinitions.Add(new ColumnDefinition
						{
							Width = new GridLength(12.0)
						});
					}
					base.ColumnDefinitions.Add(new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					});
				}
				for (int m = 0; m < (int)Math.Ceiling((double)_cards.Count / (double)cols); m++)
				{
					if (m > 0)
					{
						base.RowDefinitions.Add(new RowDefinition
						{
							Height = new GridLength(12.0)
						});
					}
					base.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
				}
				for (int n = 0; n < _cards.Count; n++)
				{
					UIElement element3 = (UIElement)_cards[n].Value.Parent;
					Grid.SetColumn(element3, n % cols * 2);
					Grid.SetRow(element3, n / cols * 2);
				}
			}
		};
		Refresh();
	}

	internal void SetSelection(string key)
	{
		Select(key, raise: false);
	}

	private void Select(string key, bool raise)
	{
		if (!(_selected == key))
		{
			_selected = key;
			Refresh();
			if (raise)
			{
				this.Changed?.Invoke(key);
			}
		}
	}

	private void Refresh()
	{
		foreach (KeyValuePair<string, SClickable> card in _cards)
		{
			bool isSelected = card.Key == _selected;
			card.Value.IsSelected = isSelected;
			RefreshRing(card.Value);
		}
	}

	private static void RefreshRing(SClickable card)
	{
		if (card.Parent is Border border)
		{
			border.BorderBrush = (card.IsSelected ? ThemeTokens.Accent : (card.IsKeyboardFocusWithin ? SettingsTheme.FocusRing : Brushes.Transparent));
		}
	}
}
