using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyProgress : Border
{
	private Border _fill;

	private double _progress;

	public double Progress
	{
		get
		{
			return _progress;
		}
		set
		{
			_progress = Math.Max(0.0, Math.Min(1.0, value));
			UpdateFill(_progress);
		}
	}

	public BuddyProgress(double height = 3.0)
	{
		base.Height = height;
		base.CornerRadius = new CornerRadius(height / 2.0);
		base.Background = ThemeTokens.Track;
		base.HorizontalAlignment = HorizontalAlignment.Stretch;
		_fill = new Border
		{
			CornerRadius = new CornerRadius(height / 2.0),
			Background = ThemeTokens.Text,
			HorizontalAlignment = HorizontalAlignment.Left
		};
		Child = _fill;
		SizeChangedEventHandler value = delegate
		{
			UpdateFill(Progress);
		};
		base.SizeChanged += value;
	}

	public void SetFillBrush(Brush brush)
	{
		_fill.Background = brush;
	}

	private void UpdateFill(double progress)
	{
		if (base.ActualWidth > 0.0)
		{
			_fill.Width = base.ActualWidth * progress;
		}
	}
}
