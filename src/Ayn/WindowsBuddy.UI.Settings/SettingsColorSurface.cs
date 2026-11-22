using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsBuddy.UI.Settings;

internal abstract class SettingsColorSurface : Border
{
	private ScaleTransform _indicatorMotion;

	private bool _pressed;

	protected SettingsColorSurface()
	{
		base.MinHeight = 32.0;
		MouseEventHandler value = delegate
		{
			RefreshIndicator();
		};
		base.MouseEnter += value;
		base.MouseLeave += delegate
		{
			RefreshIndicator();
		};
		base.PreviewMouseLeftButtonDown += delegate
		{
			_pressed = base.IsEnabled;
			RefreshIndicator();
		};
		base.PreviewMouseLeftButtonUp += delegate
		{
			_pressed = false;
			RefreshIndicator();
		};
		base.LostMouseCapture += delegate
		{
			_pressed = false;
			RefreshIndicator();
		};
		base.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
			{
				_pressed = base.IsEnabled;
				RefreshIndicator();
			}
		};
		base.PreviewKeyUp += delegate
		{
			_pressed = false;
			RefreshIndicator();
		};
		base.LostKeyboardFocus += delegate
		{
			_pressed = false;
			RefreshIndicator();
		};
		base.IsEnabledChanged += delegate
		{
			if (!base.IsEnabled)
			{
				_pressed = false;
				ReleaseMouseCapture();
			}
			RefreshIndicator();
		};
		base.IsVisibleChanged += delegate
		{
			if (!base.IsVisible)
			{
				_pressed = false;
				ReleaseMouseCapture();
				SMotion.Finish(_indicatorMotion);
			}
		};
		base.Unloaded += delegate
		{
			_pressed = false;
			ReleaseMouseCapture();
			SMotion.Finish(_indicatorMotion);
		};
	}

	internal void BindIndicator(FrameworkElement indicator)
	{
		_indicatorMotion = SMotion.AddScale(indicator);
		RefreshIndicator();
	}

	private void RefreshIndicator()
	{
		base.Opacity = (base.IsEnabled ? 1.0 : 0.4);
		if (_indicatorMotion != null)
		{
			SMotion.Scale(_indicatorMotion, (!base.IsEnabled) ? 1.0 : (_pressed ? 1.16 : (base.IsMouseOver ? 1.08 : 1.0)), base.IsVisible && base.IsEnabled);
		}
	}
}
