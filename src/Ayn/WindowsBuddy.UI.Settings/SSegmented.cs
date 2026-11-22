using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal class SSegmented : Border
{
	private readonly string[] _keys;

	private readonly List<Border> _segs = new List<Border>();

	private readonly List<TextBlock> _labels = new List<TextBlock>();

	private readonly WrapPanel _items = new WrapPanel();

	private readonly Border _indicator = new Border
	{
		Width = 100.0,
		Height = 32.0,
		CornerRadius = new CornerRadius(6.0),
		Background = SettingsTheme.SegmentSelectedBg,
		IsHitTestVisible = false
	};

	private readonly ScaleTransform _indicatorScale = new ScaleTransform();

	private readonly TranslateTransform _indicatorMove = new TranslateTransform();

	private string _selected;

	internal IList<Border> Segments => _segs;

	public string Selected
	{
		get
		{
			return _selected;
		}
		set
		{
			Select(value, raise: false);
		}
	}

	public event Action<string> Changed;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsSegmentsPeer(this);
	}

	public SSegmented(string[] labels, string[] keys, string selected, string name = null, double customWidth = 0.0)
	{
		_keys = keys;
		_selected = selected;
		base.Background = SettingsTheme.ControlBg;
		base.BorderBrush = Brushes.Transparent;
		base.BorderThickness = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(8.0);
		base.Padding = new Thickness(4.0);
		ThemeTokens.Elevate(_indicator);
		base.MinHeight = 38.0;
		base.VerticalAlignment = VerticalAlignment.Center;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		SettingsTheme.MakeFocusable(this, 8.0, name);
		Grid grid = new Grid();
		Canvas canvas = new Canvas
		{
			IsHitTestVisible = false
		};
		TransformGroup renderTransform = new TransformGroup
		{
			Children = 
			{
				(Transform)_indicatorScale,
				(Transform)_indicatorMove
			}
		};
		_indicator.RenderTransform = renderTransform;
		canvas.Children.Add(_indicator);
		grid.Children.Add(canvas);
		grid.Children.Add(_items);
		for (int i = 0; i < labels.Length; i++)
		{
			string key = keys[i];
			TextBlock textBlock = SettingsTheme.Text(labels[i], 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock.TextWrapping = TextWrapping.NoWrap;
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			SettingsSegment settingsSegment = new SettingsSegment(this, key, labels[i])
			{
				CornerRadius = new CornerRadius(6.0),
				Child = textBlock,
				MinHeight = 32.0,
				Padding = new Thickness(12.0, 0.0, 12.0, 0.0),
				Cursor = Cursors.Hand,
				Background = Brushes.Transparent
			};
			Grid.SetColumn(settingsSegment, i);
			settingsSegment.Click += delegate
			{
				if (base.IsEnabled)
				{
					Focus();
					Select(key, raise: true);
				}
			};
			settingsSegment.MouseEnter += delegate
			{
				Refresh();
			};
			settingsSegment.MouseLeave += delegate
			{
				Refresh();
			};
			_segs.Add(settingsSegment);
			_labels.Add(textBlock);
			_items.Children.Add(settingsSegment);
		}
		Child = grid;
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (base.IsEnabled)
			{
				int num = Array.IndexOf(_keys, _selected);
				if (e.Key == Key.Left && num > 0)
				{
					Select(_keys[num - 1], raise: true);
					e.Handled = true;
				}
				else if (e.Key == Key.Right && num < _keys.Length - 1)
				{
					Select(_keys[num + 1], raise: true);
					e.Handled = true;
				}
				else if (e.Key == Key.Home)
				{
					Select(_keys[0], raise: true);
					e.Handled = true;
				}
				else if (e.Key == Key.End)
				{
					Select(_keys[_keys.Length - 1], raise: true);
					e.Handled = true;
				}
			}
		};
		base.IsEnabledChanged += delegate
		{
			Refresh();
			if (!base.IsEnabled)
			{
				SMotion.Finish(_indicatorMove);
				SMotion.Finish(_indicatorScale);
			}
		};
		base.SizeChanged += delegate
		{
			MoveIndicator(animate: false);
		};
		_items.SizeChanged += delegate
		{
			MoveIndicator(animate: false);
		};
		base.IsVisibleChanged += delegate
		{
			if (!base.IsVisible)
			{
				SMotion.Finish(_indicatorMove);
				SMotion.Finish(_indicatorScale);
			}
			else
			{
				MoveIndicator(animate: false);
			}
		};
		base.Unloaded += delegate
		{
			SMotion.Finish(_indicatorMove);
			SMotion.Finish(_indicatorScale);
		};
		Refresh();
	}

	protected override Size MeasureOverride(Size constraint)
	{
		double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		for (int i = 0; i < _labels.Count; i++)
		{
			TextBlock textBlock = _labels[i];
			double num = 0.0;
			FontWeight[] array = new FontWeight[2]
			{
				FontWeights.Normal,
				AynType.Medium
			};
			foreach (FontWeight weight in array)
			{
				Typeface typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, weight, textBlock.FontStretch);
				FormattedText formattedText = new FormattedText(textBlock.Text, CultureInfo.CurrentUICulture, base.FlowDirection, typeface, textBlock.FontSize, textBlock.Foreground, pixelsPerDip);
				num = Math.Max(num, formattedText.WidthIncludingTrailingWhitespace);
			}
			_segs[i].MinWidth = Math.Ceiling(num) + 24.0;
			textBlock.MinWidth = Math.Ceiling(num);
		}
		return base.MeasureOverride(constraint);
	}

	public void Select(string key, bool raise)
	{
		if (_selected == key)
		{
			return;
		}
		string selected = _selected;
		_selected = key;
		Refresh();
		MoveIndicator(base.IsVisible && base.IsEnabled);
		for (int i = 0; i < _segs.Count; i++)
		{
			AutomationPeer automationPeer = UIElementAutomationPeer.FromElement(_segs[i]);
			if (automationPeer != null && (!(selected != _keys[i]) || !(key != _keys[i])))
			{
				automationPeer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, selected == _keys[i], key == _keys[i]);
				if (key == _keys[i])
				{
					automationPeer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
				}
			}
		}
		if (raise)
		{
			this.Changed?.Invoke(key);
		}
	}

	private void Refresh()
	{
		for (int i = 0; i < _segs.Count; i++)
		{
			bool flag = _keys[i] == _selected;
			((SClickable)_segs[i]).IsSelected = flag;
			((SClickable)_segs[i]).RefreshVisual();
			_labels[i].Foreground = (flag ? SettingsTheme.SegmentSelectedText : ((base.IsEnabled && _segs[i].IsMouseOver) ? SettingsTheme.TextPrimary : SettingsTheme.TextSecondary));
			_labels[i].FontWeight = AynType.Medium;
		}
		AutomationProperties.SetItemStatus(this, _selected ?? "");
		_indicator.Opacity = (base.IsEnabled ? 1.0 : 0.4);
	}

	private void MoveIndicator(bool animate)
	{
		int num = Array.IndexOf(_keys, _selected);
		if (num < 0 || _segs[num].ActualWidth <= 0.0)
		{
			_indicator.Visibility = Visibility.Hidden;
			return;
		}
		Border border = _segs[num];
		Point point = border.TranslatePoint(default(Point), _items);
		_indicator.Visibility = Visibility.Visible;
		SMotion.To(_indicatorMove, TranslateTransform.XProperty, point.X, animate);
		SMotion.To(_indicatorMove, TranslateTransform.YProperty, point.Y, animate);
		SMotion.To(_indicatorScale, ScaleTransform.ScaleXProperty, border.ActualWidth / 100.0, animate);
		SMotion.To(_indicatorScale, ScaleTransform.ScaleYProperty, border.ActualHeight / 32.0, animate);
	}
}
