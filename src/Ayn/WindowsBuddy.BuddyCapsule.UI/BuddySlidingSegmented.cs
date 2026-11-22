using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddySlidingSegmented : Border
{
	public static readonly Duration SlideDuration = new Duration(TimeSpan.FromMilliseconds(180.0));

	private static readonly DropShadowEffect ThumbShadow = CreateShadow();

	private readonly Grid _row = new Grid();

	private readonly Border _thumb;

	private readonly TranslateTransform _x = new TranslateTransform();

	private readonly List<TextBlock> _labels = new List<TextBlock>();

	private readonly double _segWidth;

	private int _sel = -1;

	public int Selected
	{
		get
		{
			return _sel;
		}
		set
		{
			Select(value, animate: true);
		}
	}

	public int Count => _labels.Count;

	public double SegmentWidth => _segWidth;

	public double ThumbTarget => (double)_sel * _segWidth;

	public event Action<int> SelectionChanged;

	public BuddySlidingSegmented(string[] labels, int selected, double height = 28.0, double fontSize = 11.5, double minSegment = 0.0)
	{
		base.Height = height;
		base.CornerRadius = new CornerRadius(height / 2.0);
		base.Padding = new Thickness(2.0);
		base.Focusable = true;
		base.FocusVisualStyle = null;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		base.VerticalAlignment = VerticalAlignment.Center;
		double num = minSegment;
		for (int i = 0; i < labels.Length; i++)
		{
			TextBlock textBlock = new TextBlock
			{
				Text = labels[i],
				FontSize = fontSize,
				FontWeight = AynType.Medium,
				FontFamily = AynType.Family(TypeSurface.Capsule, fontSize),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			TextOptions.SetTextFormattingMode(textBlock, TextFormattingMode.Display);
			textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			num = Math.Max(num, Math.Ceiling(textBlock.DesiredSize.Width) + 24.0);
			_labels.Add(textBlock);
		}
		_segWidth = num;
		_thumb = new Border
		{
			Width = _segWidth,
			CornerRadius = new CornerRadius((height - 4.0) / 2.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			RenderTransform = _x,
			IsHitTestVisible = false
		};
		Grid.SetColumnSpan(_thumb, Math.Max(1, labels.Length));
		_row.Children.Add(_thumb);
		for (int j = 0; j < labels.Length; j++)
		{
			int idx = j;
			_row.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(_segWidth)
			});
			Border border = new Border
			{
				Background = Brushes.Transparent,
				Cursor = Cursors.Hand,
				Child = _labels[j]
			};
			bool pressed = false;
			border.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				pressed = true;
				e.Handled = true;
			};
			border.MouseLeave += delegate
			{
				pressed = false;
				if (idx != _sel)
				{
					_labels[idx].Foreground = ThemeTokens.Text2;
				}
			};
			border.MouseEnter += delegate
			{
				if (idx != _sel)
				{
					_labels[idx].Foreground = ThemeTokens.Text;
				}
			};
			border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				if (pressed)
				{
					pressed = false;
					e.Handled = true;
					Choose(idx);
				}
			};
			Grid.SetColumn(border, j);
			_row.Children.Add(border);
		}
		Child = _row;
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			int sel = _sel;
			if (e.Key == Key.Left || e.Key == Key.Up)
			{
				sel = Math.Max(0, _sel - 1);
			}
			else if (e.Key == Key.Right || e.Key == Key.Down)
			{
				sel = Math.Min(_labels.Count - 1, _sel + 1);
			}
			else if (e.Key == Key.Home)
			{
				sel = 0;
			}
			else
			{
				if (e.Key != Key.End)
				{
					return;
				}
				sel = _labels.Count - 1;
			}
			e.Handled = true;
			if (sel != _sel)
			{
				Choose(sel);
			}
		};
		base.GotKeyboardFocus += delegate
		{
			Paint();
		};
		base.LostKeyboardFocus += delegate
		{
			Paint();
		};
		ApplyTheme(this);
		ThemeTokens.Subscribe(this, ApplyTheme);
		Select(selected, animate: false);
	}

	public void Choose(int idx)
	{
		if (idx != _sel && idx >= 0 && idx < _labels.Count)
		{
			Select(idx, animate: true);
			this.SelectionChanged?.Invoke(idx);
		}
	}

	private void Select(int idx, bool animate)
	{
		if (idx >= 0 && idx < _labels.Count)
		{
			bool flag = idx != _sel;
			_sel = idx;
			double num = (double)idx * _segWidth;
			if (flag && animate && _x.X != num && CapsuleMotion.AnimationsEnabled && base.IsLoaded)
			{
				_x.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(num, SlideDuration)
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
			else
			{
				_x.BeginAnimation(TranslateTransform.XProperty, null);
				_x.X = num;
			}
			Paint();
		}
	}

	private void Paint()
	{
		for (int i = 0; i < _labels.Count; i++)
		{
			_labels[i].Foreground = ((i == _sel) ? ThemeTokens.Text : ThemeTokens.Text2);
		}
		base.Background = BuddyTint.Ink((byte)(base.IsKeyboardFocused ? 26 : 15));
	}

	private static void ApplyTheme(BuddySlidingSegmented c)
	{
		bool isLight = ThemeTokens.IsLight;
		c._thumb.Background = (isLight ? ThemeTokens.Surface : BuddyTint.Ink(46));
		c._thumb.Effect = (isLight ? ThumbShadow : null);
	}

	private static DropShadowEffect CreateShadow()
	{
		DropShadowEffect dropShadowEffect = new DropShadowEffect();
		dropShadowEffect.Color = Colors.Black;
		dropShadowEffect.Direction = 270.0;
		dropShadowEffect.ShadowDepth = 1.0;
		dropShadowEffect.BlurRadius = 6.0;
		dropShadowEffect.Opacity = 0.14;
		dropShadowEffect.RenderingBias = RenderingBias.Performance;
		DropShadowEffect dropShadowEffect2 = dropShadowEffect;
		dropShadowEffect2.Freeze();
		return dropShadowEffect2;
	}
}
