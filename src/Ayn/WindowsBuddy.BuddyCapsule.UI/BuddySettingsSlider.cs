using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddySettingsSlider : Grid
{
	private readonly Border _trackBg;

	private readonly Border _trackFill;

	private readonly Border _thumb;

	private readonly TranslateTransform _thumbTransform;

	private double _value;

	private bool _isDragging;

	private int _stepCount;

	public double Value
	{
		get
		{
			return _value;
		}
		set
		{
			double num = Math.Max(0.0, Math.Min(1.0, value));
			if (_stepCount > 1)
			{
				double num2 = Math.Round(num * (double)(_stepCount - 1));
				num = num2 / (double)(_stepCount - 1);
			}
			if (Math.Abs(_value - num) > 0.001)
			{
				_value = num;
				UpdateLayoutPositions();
				if (this.ValueChanged != null)
				{
					this.ValueChanged(_value);
				}
			}
		}
	}

	public event Action<double> ValueChanged;

	public void SetValueSilently(double val)
	{
		_value = Math.Max(0.0, Math.Min(1.0, val));
		if (_stepCount > 1)
		{
			double num = Math.Round(_value * (double)(_stepCount - 1));
			_value = num / (double)(_stepCount - 1);
		}
		UpdateLayoutPositions();
	}

	public BuddySettingsSlider(double initialValue = 0.5, int stepCount = 0)
	{
		_value = Math.Max(0.0, Math.Min(1.0, initialValue));
		_stepCount = stepCount;
		base.Height = 22.0;
		base.Cursor = Cursors.Hand;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		base.Background = Brushes.Transparent;
		_trackBg = new Border
		{
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(38, 38, 43))),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		base.Children.Add(_trackBg);
		_trackFill = new Border
		{
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = ThemeTokens.Text,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Left,
			Width = 0.0
		};
		base.Children.Add(_trackFill);
		_thumbTransform = new TranslateTransform(0.0, 0.0);
		_thumb = new Border
		{
			Width = 14.0,
			Height = 14.0,
			CornerRadius = new CornerRadius(7.0),
			Background = ThemeTokens.Text,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Left,
			RenderTransform = _thumbTransform,
			IsHitTestVisible = false
		};
		base.Children.Add(_thumb);
		SizeChangedEventHandler value = delegate
		{
			UpdateLayoutPositions();
		};
		base.SizeChanged += value;
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			_isDragging = true;
			CaptureMouse();
			UpdateFromMouse(e.GetPosition(this).X);
			e.Handled = true;
		};
		base.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (_isDragging)
			{
				UpdateFromMouse(e.GetPosition(this).X);
				e.Handled = true;
			}
		};
		base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_isDragging)
			{
				_isDragging = false;
				ReleaseMouseCapture();
				e.Handled = true;
			}
		};
	}

	private void UpdateFromMouse(double mouseX)
	{
		double num = base.ActualWidth - 14.0;
		if (!(num <= 0.0))
		{
			double value = (mouseX - 7.0) / num;
			Value = value;
		}
	}

	private void UpdateLayoutPositions()
	{
		double actualWidth = base.ActualWidth;
		if (!(actualWidth <= 14.0))
		{
			double num = actualWidth - 14.0;
			double num2 = _value * num;
			_thumbTransform.X = num2;
			_trackFill.Width = Math.Max(0.0, num2 + 7.0);
		}
	}
}
