using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyToggle : Control
{
	public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(BuddyToggle), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsCheckedChanged));

	public static readonly DependencyProperty ActiveTrackBrushProperty = DependencyProperty.Register("ActiveTrackBrush", typeof(Brush), typeof(BuddyToggle), new PropertyMetadata(null, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((BuddyToggle)d).UpdateVisual(animate: false);
	}));

	private Border _track;

	private Border _thumb;

	private bool _isSilencingEvents;

	private static readonly Brush InactiveTrackBrush = ThemeTokens.Surface3;

	private static readonly Brush InactiveThumbBrush = ThemeTokens.IconMuted;

	public bool IsChecked
	{
		get
		{
			return (bool)GetValue(IsCheckedProperty);
		}
		set
		{
			SetValue(IsCheckedProperty, value);
		}
	}

	public Brush ActiveTrackBrush
	{
		get
		{
			return (Brush)GetValue(ActiveTrackBrushProperty);
		}
		set
		{
			SetValue(ActiveTrackBrushProperty, value);
		}
	}

	protected override int VisualChildrenCount
	{
		get
		{
			if (_track == null)
			{
				return 0;
			}
			return 1;
		}
	}

	public event EventHandler<bool> Toggled;

	public BuddyToggle()
	{
		base.Width = 36.0;
		base.Height = 20.0;
		base.Cursor = Cursors.Hand;
		base.Focusable = true;
		base.FocusVisualStyle = null;
		BuildVisual();
		UpdateVisual(animate: false);
		MouseButtonEventHandler value = delegate(object s, MouseButtonEventArgs e)
		{
			if (base.IsEnabled)
			{
				IsChecked = !IsChecked;
				e.Handled = true;
			}
		};
		base.MouseLeftButtonUp += value;
		base.MouseEnter += delegate
		{
			if (base.IsEnabled && !IsChecked)
			{
				_thumb.Background = BuddyTokens.TextSecondaryBrush;
			}
		};
		base.MouseLeave += delegate
		{
			if (base.IsEnabled)
			{
				UpdateVisual(animate: false);
			}
		};
		base.GotFocus += delegate
		{
			if (base.IsEnabled)
			{
				_track.BorderBrush = BuddyTokens.FocusRingBrush;
				_track.BorderThickness = new Thickness(1.0);
			}
		};
		base.LostFocus += delegate
		{
			if (base.IsEnabled)
			{
				_track.BorderThickness = new Thickness(0.0);
				UpdateVisual(animate: false);
			}
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if ((e.Key == Key.Space || e.Key == Key.Return) && base.IsEnabled)
			{
				IsChecked = !IsChecked;
				e.Handled = true;
			}
		};
		base.IsEnabledChanged += delegate
		{
			base.Opacity = (base.IsEnabled ? 1.0 : 0.35);
			base.Cursor = (base.IsEnabled ? Cursors.Hand : Cursors.Arrow);
		};
	}

	private void BuildVisual()
	{
		_track = new Border
		{
			Width = 36.0,
			Height = 20.0,
			CornerRadius = new CornerRadius(10.0),
			BorderThickness = new Thickness(0.0),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch
		};
		Grid grid = new Grid();
		grid.Width = 36.0;
		grid.Height = 20.0;
		Grid grid2 = grid;
		_thumb = new Border
		{
			Width = 16.0,
			Height = 16.0,
			CornerRadius = new CornerRadius(8.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(2.0, 0.0, 0.0, 0.0)
		};
		grid2.Children.Add(_thumb);
		_track.Child = grid2;
		ControlTemplate controlTemplate = new ControlTemplate(typeof(BuddyToggle));
		FrameworkElementFactory visualTree = new FrameworkElementFactory(typeof(ContentPresenter));
		controlTemplate.VisualTree = visualTree;
		base.Template = controlTemplate;
		AddVisualChild(_track);
	}

	protected override Visual GetVisualChild(int index)
	{
		return _track;
	}

	protected override Size MeasureOverride(Size constraint)
	{
		_track.Measure(new Size(36.0, 20.0));
		return new Size(36.0, 20.0);
	}

	protected override Size ArrangeOverride(Size arrangeBounds)
	{
		_track.Arrange(new Rect(0.0, 0.0, 36.0, 20.0));
		return new Size(36.0, 20.0);
	}

	public void SetCheckedSilently(bool isChecked)
	{
		if (IsChecked != isChecked)
		{
			_isSilencingEvents = true;
			try
			{
				IsChecked = isChecked;
				UpdateVisual(animate: false);
			}
			finally
			{
				_isSilencingEvents = false;
			}
		}
	}

	private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is BuddyToggle buddyToggle)
		{
			buddyToggle.UpdateVisual(animate: true);
			if (!buddyToggle._isSilencingEvents)
			{
				buddyToggle.Toggled?.Invoke(buddyToggle, (bool)e.NewValue);
			}
		}
	}

	private void UpdateVisual(bool animate)
	{
		if (_track != null && _thumb != null)
		{
			bool isChecked = IsChecked;
			Brush brush = ((ActiveTrackBrush != null) ? ActiveTrackBrush : ThemeTokens.Accent);
			Brush background = (isChecked ? brush : InactiveTrackBrush);
			Brush background2 = (isChecked ? ThemeTokens.AccentFg : InactiveThumbBrush);
			double left = (isChecked ? 18.0 : 2.0);
			_track.Background = background;
			_thumb.Background = background2;
			if (animate)
			{
				ThicknessAnimation thicknessAnimation = new ThicknessAnimation();
				thicknessAnimation.To = new Thickness(left, 0.0, 0.0, 0.0);
				thicknessAnimation.Duration = TimeSpan.FromMilliseconds(150.0);
				thicknessAnimation.EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				};
				ThicknessAnimation animation = thicknessAnimation;
				_thumb.BeginAnimation(FrameworkElement.MarginProperty, animation);
			}
			else
			{
				_thumb.BeginAnimation(FrameworkElement.MarginProperty, null);
				_thumb.Margin = new Thickness(left, 0.0, 0.0, 0.0);
			}
		}
	}
}
