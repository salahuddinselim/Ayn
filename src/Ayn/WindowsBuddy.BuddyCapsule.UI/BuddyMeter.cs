using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Animation;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyMeter : StackPanel
{
	private TextBlock _labelBlock;

	private TextBlock _valueBlock;

	private Border _track;

	private Border _fill;

	private double _percent;

	private string _valueFormat = "{0:0}%";

	public double Value
	{
		get
		{
			return _percent;
		}
		set
		{
			SetValue(value);
		}
	}

	public BuddyMeter(string label, double initialPercent = 0.0, string customValueText = null)
	{
		base.Orientation = Orientation.Vertical;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_labelBlock = new TextBlock
		{
			Text = (label ?? string.Empty),
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.5,
			FontWeight = FontWeights.Normal,
			Foreground = BuddyTokens.Text60Brush,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(_labelBlock, 0);
		grid.Children.Add(_labelBlock);
		_valueBlock = new TextBlock
		{
			FontFamily = BuddyTokens.FontDisplay,
			FontSize = 11.5,
			FontWeight = AynType.Medium,
			Foreground = BuddyTokens.Text100Brush,
			VerticalAlignment = VerticalAlignment.Center
		};
		Typography.SetNumeralAlignment(_valueBlock, FontNumeralAlignment.Tabular);
		Grid.SetColumn(_valueBlock, 1);
		grid.Children.Add(_valueBlock);
		base.Children.Add(grid);
		_track = new Border
		{
			Height = 6.0,
			Background = BuddyTokens.MeterTrackBrush,
			CornerRadius = new CornerRadius(3.0),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			ClipToBounds = true
		};
		_fill = new Border
		{
			Height = 6.0,
			Background = BuddyTokens.Text100Brush,
			CornerRadius = new CornerRadius(3.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			Width = 6.0
		};
		_track.Child = _fill;
		base.Children.Add(_track);
		_track.SizeChanged += delegate
		{
			UpdateFillVisual(animate: false);
		};
		SetValue(initialPercent, customValueText);
	}

	public void SetValue(double percent, string customValueText = null)
	{
		_percent = Math.Max(0.0, Math.Min(100.0, percent));
		_valueBlock.Text = customValueText ?? string.Format(_valueFormat, _percent);
		if (_percent > 90.0)
		{
			_fill.Background = BuddyTokens.AlertBrush;
			_valueBlock.Foreground = BuddyTokens.AlertBrush;
		}
		else
		{
			_fill.Background = BuddyTokens.Text100Brush;
			_valueBlock.Foreground = BuddyTokens.Text100Brush;
		}
		UpdateFillVisual(animate: true);
	}

	private void UpdateFillVisual(bool animate)
	{
		double actualWidth = _track.ActualWidth;
		if (!(actualWidth <= 0.0))
		{
			double val = Math.Round(_percent / 100.0 * actualWidth);
			double num = Math.Max(6.0, val);
			if (animate && CapsuleMotion.AnimationsEnabled)
			{
				DoubleAnimation doubleAnimation = new DoubleAnimation(num, TimeSpan.FromMilliseconds(200.0));
				doubleAnimation.EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				};
				DoubleAnimation animation = doubleAnimation;
				_fill.BeginAnimation(FrameworkElement.WidthProperty, animation);
			}
			else
			{
				_fill.BeginAnimation(FrameworkElement.WidthProperty, null);
				_fill.Width = num;
			}
		}
	}
}
