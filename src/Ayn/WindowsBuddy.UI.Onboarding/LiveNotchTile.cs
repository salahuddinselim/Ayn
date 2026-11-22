using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class LiveNotchTile : Border
{
	public const double TileW = 460.0;

	public const double TileH = 140.0;

	private readonly CapsuleWindow _notch;

	private readonly Rectangle _view = new Rectangle
	{
		HorizontalAlignment = HorizontalAlignment.Center,
		VerticalAlignment = VerticalAlignment.Top
	};

	private VisualBrush _brush;

	private DispatcherTimer _timer;

	internal Brush Picture => _view.Fill;

	internal double PictureBottom => _view.Height;

	public LiveNotchTile(CapsuleWindow notch)
	{
		_notch = notch;
		base.Width = 460.0;
		base.Height = 140.0;
		base.CornerRadius = new CornerRadius(16.0);
		bool flag = ThemeTokens.Current != null && ThemeTokens.Current.Id == "Silver";
		LinearGradientBrush linearGradientBrush = (flag ? new LinearGradientBrush(Color.FromRgb(242, 242, 244), Color.FromRgb(226, 226, 230), 90.0) : new LinearGradientBrush(Color.FromRgb(26, 26, 28), Color.FromRgb(14, 14, 16), 90.0));
		linearGradientBrush.Freeze();
		RadialGradientBrush radialGradientBrush = new RadialGradientBrush(flag ? Color.FromArgb(224, byte.MaxValue, byte.MaxValue, byte.MaxValue) : Color.FromArgb(28, byte.MaxValue, byte.MaxValue, byte.MaxValue), Colors.Transparent)
		{
			Center = new Point(0.5, 0.0),
			GradientOrigin = new Point(0.5, 0.0),
			RadiusX = 0.6,
			RadiusY = 0.9
		};
		radialGradientBrush.Freeze();
		Grid grid = new Grid
		{
			Background = linearGradientBrush,
			Width = 460.0,
			Height = 140.0,
			Clip = new RectangleGeometry(new Rect(0.0, 0.0, 460.0, 140.0), 16.0, 16.0)
		};
		grid.Children.Add(new Border
		{
			Background = radialGradientBrush
		});
		grid.Children.Add(_view);
		Child = grid;
		base.BorderBrush = ThemeTokens.Rule;
		base.BorderThickness = new Thickness(1.0);
		base.Loaded += delegate
		{
			Refresh();
			_timer = new DispatcherTimer(DispatcherPriority.Render)
			{
				Interval = TimeSpan.FromMilliseconds(RenderProfile.LightMode ? 250 : 50)
			};
			_timer.Tick += delegate
			{
				Refresh();
			};
			_timer.Start();
		};
		base.Unloaded += delegate
		{
			if (_timer != null)
			{
				_timer.Stop();
			}
			_timer = null;
		};
	}

	public void Refresh()
	{
		FrameworkElement frameworkElement = ((_notch != null) ? _notch.NotchCard : null);
		if (frameworkElement == null)
		{
			return;
		}
		double actualWidth = frameworkElement.ActualWidth;
		double actualHeight = frameworkElement.ActualHeight;
		if (!(actualWidth < 2.0) && !(actualHeight < 2.0))
		{
			if (_brush == null)
			{
				_brush = new VisualBrush(frameworkElement)
				{
					Stretch = Stretch.Fill,
					ViewboxUnits = BrushMappingMode.Absolute
				};
				_view.Fill = _brush;
			}
			Vector offset = VisualTreeHelper.GetOffset(frameworkElement);
			double num = 6.0;
			_brush.Viewbox = new Rect(offset.X, offset.Y, actualWidth, actualHeight + num);
			double num2 = Math.Min(1.7, 420.0 / actualWidth);
			_view.Width = actualWidth * num2;
			_view.Height = (actualHeight + num) * num2;
		}
	}
}
