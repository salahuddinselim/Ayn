using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyAnimationStyleSelector : Grid
{
	private class StyleOption
	{
		public string Key;

		public string DisplayName;

		public Border Container;

		public FrameworkElement Canvas;

		public TextBlock Label;
	}

	private readonly List<StyleOption> _options = new List<StyleOption>();

	private string _selectedStyle;

	private readonly Action<string> _onStyleChanged;

	private DispatcherTimer _animTimer;

	private double _animTime;

	public string SelectedStyle
	{
		get
		{
			return _selectedStyle;
		}
		set
		{
			_selectedStyle = value;
			UpdateSelectionState();
		}
	}

	public BuddyAnimationStyleSelector(string currentStyle, Action<string> onStyleChanged)
	{
		_selectedStyle = currentStyle ?? "Soft Sweep";
		_onStyleChanged = onStyleChanged;
		base.HorizontalAlignment = HorizontalAlignment.Stretch;
		base.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		var array = new[]
		{
			new
			{
				Key = "Soft Sweep",
				Name = "Soft Sweep"
			},
			new
			{
				Key = "Sharp Sweep",
				Name = "Sharp Sweep"
			},
			new
			{
				Key = "Dotted Trail",
				Name = "Dotted"
			},
			new
			{
				Key = "Dual Sweep",
				Name = "Dual Sweep"
			},
			new
			{
				Key = "Pulse",
				Name = "Pulse"
			},
			new
			{
				Key = "Breathing",
				Name = "Breathing"
			},
			new
			{
				Key = "Continuous",
				Name = "Continuous"
			}
		};
		for (int i = 0; i < array.Length; i++)
		{
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		for (int j = 0; j < array.Length; j++)
		{
			string key = array[j].Key;
			string name = array[j].Name;
			Border btn = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(17, 17, 20)),
				BorderBrush = ThemeTokens.Border,
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(8.0),
				Padding = new Thickness(2.0, 6.0, 2.0, 5.0),
				Margin = new Thickness((j != 0) ? 3 : 0, 0.0, (j != array.Length - 1) ? 3 : 0, 0.0),
				Cursor = Cursors.Hand,
				SnapsToDevicePixels = true,
				UseLayoutRounding = true
			};
			StackPanel stackPanel = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			MiniMotionPreviewCanvas miniMotionPreviewCanvas = new MiniMotionPreviewCanvas(key)
			{
				Width = 36.0,
				Height = 13.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 0.0, 3.0)
			};
			TextBlock textBlock = new TextBlock
			{
				Text = name,
				FontSize = 10.0,
				FontWeight = FontWeights.Medium,
				FontFamily = BuddyTokens.FontText,
				HorizontalAlignment = HorizontalAlignment.Center,
				Foreground = ThemeTokens.Text2
			};
			stackPanel.Children.Add(miniMotionPreviewCanvas);
			stackPanel.Children.Add(textBlock);
			btn.Child = stackPanel;
			StyleOption opt = new StyleOption
			{
				Key = key,
				DisplayName = name,
				Container = btn,
				Canvas = miniMotionPreviewCanvas,
				Label = textBlock
			};
			_options.Add(opt);
			btn.MouseEnter += delegate
			{
				if (!IsOptionSelected(opt.Key))
				{
					btn.BorderBrush = ThemeTokens.BorderStrong;
				}
			};
			btn.MouseLeave += delegate
			{
				if (!IsOptionSelected(opt.Key))
				{
					btn.BorderBrush = ThemeTokens.Border;
				}
			};
			btn.MouseLeftButtonDown += delegate
			{
				_selectedStyle = opt.Key;
				UpdateSelectionState();
				if (_onStyleChanged != null)
				{
					_onStyleChanged(opt.Key);
				}
			};
			Grid.SetColumn(btn, j);
			base.Children.Add(btn);
		}
		UpdateSelectionState();
		_animTimer = new DispatcherTimer(DispatcherPriority.Render)
		{
			Interval = TimeSpan.FromMilliseconds(40.0)
		};
		_animTimer.Tick += delegate
		{
			_animTime += 0.04;
			foreach (StyleOption option in _options)
			{
				if (option.Canvas is MiniMotionPreviewCanvas miniMotionPreviewCanvas2)
				{
					miniMotionPreviewCanvas2.Time = _animTime;
					miniMotionPreviewCanvas2.InvalidateVisual();
				}
			}
		};
		base.Loaded += delegate
		{
			if (base.IsVisible)
			{
				_animTimer.Start();
			}
		};
		base.Unloaded += delegate
		{
			_animTimer.Stop();
		};
		base.IsVisibleChanged += delegate
		{
			if (base.IsVisible)
			{
				_animTimer.Start();
			}
			else
			{
				_animTimer.Stop();
			}
		};
	}

	private bool IsOptionSelected(string key)
	{
		if (string.Equals(_selectedStyle, key, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (string.Equals(key, "Continuous", StringComparison.OrdinalIgnoreCase) && string.Equals(_selectedStyle, "Continuous Flow", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return false;
	}

	private void UpdateSelectionState()
	{
		foreach (StyleOption option in _options)
		{
			bool flag = IsOptionSelected(option.Key);
			if (flag)
			{
				option.Container.Background = new SolidColorBrush(Color.FromRgb(26, 26, 32));
				option.Container.BorderBrush = ThemeTokens.Text;
				option.Label.Foreground = ThemeTokens.Text;
				option.Label.FontWeight = AynType.Medium;
			}
			else
			{
				option.Container.Background = new SolidColorBrush(Color.FromRgb(16, 16, 19));
				option.Container.BorderBrush = ThemeTokens.Border;
				option.Label.Foreground = ThemeTokens.Text2;
				option.Label.FontWeight = FontWeights.Medium;
			}
			if (option.Canvas is MiniMotionPreviewCanvas miniMotionPreviewCanvas)
			{
				miniMotionPreviewCanvas.IsSelected = flag;
			}
		}
	}
}
