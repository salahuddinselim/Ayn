using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.UI.Settings;

internal class SSlider : Grid
{
	private readonly double _min;

	private readonly double _max;

	private readonly double _step;

	private double _value;

	private readonly Border _fill;

	private readonly Ellipse _thumb;

	private readonly TranslateTransform _thumbTx = new TranslateTransform();

	private readonly ScaleTransform _thumbScale = new ScaleTransform(1.0, 1.0, 7.0, 7.0);

	private bool _dragging;

	internal double Minimum => _min;

	internal double Maximum => _max;

	internal double Step => _step;

	public double Value
	{
		get
		{
			return _value;
		}
		set
		{
			SetValue(value, raise: false);
		}
	}

	public event Action<double> ValueChanged;

	public event Action<double> ValueUpdated;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsSliderPeer(this);
	}

	internal void SetUserValue(double value)
	{
		SetValue(value, raise: true);
	}

	public SSlider(double min, double max, double step, double value, double width = 200.0, string name = null)
	{
		_min = min;
		_max = max;
		_step = step;
		_value = Clamp(value);
		base.Width = width;
		base.Height = 32.0;
		base.Background = Brushes.Transparent;
		base.Cursor = Cursors.Hand;
		base.VerticalAlignment = VerticalAlignment.Center;
		SettingsTheme.MakeFocusable(this, 6.0, name);
		double num = 7.0;
		base.Children.Add(new Border
		{
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = SettingsTheme.ToggleOffTrack,
			Margin = new Thickness(num, 0.0, num, 0.0)
		});
		_fill = new Border
		{
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = SettingsTheme.SliderFill,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(num, 0.0, 0.0, 0.0)
		};
		base.Children.Add(_fill);
		TransformGroup renderTransform = new TransformGroup
		{
			Children = 
			{
				(Transform)_thumbScale,
				(Transform)_thumbTx
			}
		};
		_thumb = new Ellipse
		{
			Width = 14.0,
			Height = 14.0,
			Fill = ThemeTokens.Text,
			HorizontalAlignment = HorizontalAlignment.Left,
			RenderTransform = renderTransform,
			Effect = SettingsTheme.SoftShadow(3.0, 1.0, 0.3)
		};
		base.Children.Add(_thumb);
		SizeChangedEventHandler value2 = delegate
		{
			Layout();
		};
		base.SizeChanged += value2;
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (base.IsEnabled)
			{
				Focus();
				_dragging = true;
				CaptureMouse();
				SetFromX(e.GetPosition(this).X);
				UpdateVisual();
				e.Handled = true;
			}
		};
		base.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (_dragging)
			{
				SetFromX(e.GetPosition(this).X);
			}
		};
		base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_dragging)
			{
				_dragging = false;
				ReleaseMouseCapture();
				UpdateVisual();
				e.Handled = true;
			}
		};
		base.LostMouseCapture += delegate
		{
			_dragging = false;
			UpdateVisual();
		};
		base.MouseEnter += delegate
		{
			UpdateVisual();
		};
		base.MouseLeave += delegate
		{
			UpdateVisual();
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (base.IsEnabled)
			{
				double value3 = _value;
				if (e.Key == Key.Left || e.Key == Key.Down)
				{
					value3 -= _step;
				}
				else if (e.Key == Key.Right || e.Key == Key.Up)
				{
					value3 += _step;
				}
				else if (e.Key == Key.Next)
				{
					value3 -= _step * 5.0;
				}
				else if (e.Key == Key.Prior)
				{
					value3 += _step * 5.0;
				}
				else if (e.Key == Key.Home)
				{
					value3 = _min;
				}
				else
				{
					if (e.Key != Key.End)
					{
						return;
					}
					value3 = _max;
				}
				e.Handled = true;
				SetValue(value3, raise: true);
			}
		};
		base.IsEnabledChanged += delegate
		{
			if (!base.IsEnabled)
			{
				_dragging = false;
				ReleaseMouseCapture();
			}
			UpdateVisual();
		};
		base.IsVisibleChanged += delegate
		{
			if (!base.IsVisible)
			{
				_dragging = false;
				ReleaseMouseCapture();
				SMotion.Finish(_thumbScale);
			}
		};
		base.Unloaded += delegate
		{
			_dragging = false;
			ReleaseMouseCapture();
			SMotion.Finish(_thumbScale);
		};
	}

	private double Clamp(double v)
	{
		if (_step > 0.0)
		{
			v = _min + Math.Round((v - _min) / _step) * _step;
		}
		return Math.Max(_min, Math.Min(_max, v));
	}

	private void SetFromX(double x)
	{
		double num = Math.Max(1.0, base.ActualWidth - 14.0);
		SetValue(_min + (x - 7.0) / num * (_max - _min), raise: true);
	}

	private void SetValue(double v, bool raise)
	{
		v = Clamp(v);
		if (!(Math.Abs(v - _value) < 1E-09))
		{
			double value = _value;
			_value = v;
			Layout();
			AutomationProperties.SetItemStatus(this, _value.ToString("0.##"));
			UIElementAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, value, _value);
			this.ValueUpdated?.Invoke(_value);
			if (raise)
			{
				this.ValueChanged?.Invoke(_value);
			}
		}
	}

	private void Layout()
	{
		double num = Math.Max(0.0, base.ActualWidth - 14.0);
		double num2 = ((_max > _min) ? ((_value - _min) / (_max - _min)) : 0.0);
		_thumbTx.X = num * num2;
		_fill.Width = Math.Max(0.0, num * num2);
	}

	private void UpdateVisual()
	{
		base.Opacity = (base.IsEnabled ? 1.0 : 0.4);
		base.Cursor = (base.IsEnabled ? Cursors.Hand : Cursors.Arrow);
		SMotion.Scale(_thumbScale, (!base.IsEnabled) ? 1.0 : (_dragging ? 1.16 : (base.IsMouseOver ? 1.08 : 1.0)), base.IsVisible && base.IsEnabled);
	}
}
