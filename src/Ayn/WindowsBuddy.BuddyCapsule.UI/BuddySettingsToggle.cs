using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddySettingsToggle : Border
{
	private readonly Border _thumb;

	private readonly TranslateTransform _thumbTransform;

	private bool _isChecked;

	private static readonly Brush TrackOffBrush = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(42, 42, 45)));

	private static readonly Brush TrackOnBrush = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(80, 80, 88)));

	private static readonly Brush ThumbOffBrush = BuddyTokens.FreezeBrush(new SolidColorBrush(Color.FromRgb(126, 126, 132)));

	private static readonly Brush ThumbOnBrush = ThemeTokens.Text;

	public bool IsChecked
	{
		get
		{
			return _isChecked;
		}
		set
		{
			if (_isChecked != value)
			{
				_isChecked = value;
				UpdateVisualState(animate: true);
				if (this.Toggled != null)
				{
					this.Toggled(_isChecked);
				}
			}
		}
	}

	public event Action<bool> Toggled;

	public void SetCheckedSilently(bool isChecked)
	{
		_isChecked = isChecked;
		UpdateVisualState(animate: false);
	}

	public BuddySettingsToggle(bool initialChecked = false)
	{
		base.Width = 42.0;
		base.Height = 24.0;
		base.CornerRadius = new CornerRadius(12.0);
		base.Cursor = Cursors.Hand;
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		_thumbTransform = new TranslateTransform(0.0, 0.0);
		_thumb = new Border
		{
			Width = 18.0,
			Height = 18.0,
			CornerRadius = new CornerRadius(9.0),
			Background = (initialChecked ? ThumbOnBrush : ThumbOffBrush),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(3.0, 0.0, 0.0, 0.0),
			RenderTransform = _thumbTransform
		};
		Child = _thumb;
		_isChecked = initialChecked;
		UpdateVisualState(animate: false);
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			IsChecked = !IsChecked;
		};
	}

	private void UpdateVisualState(bool animate)
	{
		base.Background = (_isChecked ? TrackOnBrush : TrackOffBrush);
		_thumb.Background = (_isChecked ? ThumbOnBrush : ThumbOffBrush);
		double num = (_isChecked ? 18.0 : 0.0);
		if (animate)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation();
			doubleAnimation.To = num;
			doubleAnimation.Duration = TimeSpan.FromMilliseconds(160.0);
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation animation = doubleAnimation;
			_thumbTransform.BeginAnimation(TranslateTransform.XProperty, animation);
		}
		else
		{
			_thumbTransform.BeginAnimation(TranslateTransform.XProperty, null);
			_thumbTransform.X = num;
		}
	}
}
