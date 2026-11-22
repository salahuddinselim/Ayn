using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Animation;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyListRow : Border
{
	private ScaleTransform _pressScale;

	private Border _actionsHost;

	private TextBlock _metaBlock;

	public event EventHandler Clicked;

	public BuddyListRow(string title, string description = null, UIElement leadingIcon = null, UIElement trailingControl = null, bool isClickable = false)
	{
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		base.Height = 40.0;
		base.Background = BuddyTokens.RowBrush;
		base.BorderThickness = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(12.0);
		base.Padding = new Thickness(12.0, 0.0, 12.0, 0.0);
		base.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
		base.VerticalAlignment = VerticalAlignment.Center;
		if (isClickable)
		{
			base.Cursor = Cursors.Hand;
		}
		Grid grid = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		if (leadingIcon != null)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(24.0)
			});
		}
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		if (trailingControl != null)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
		}
		int num = 0;
		if (leadingIcon != null)
		{
			Border element = new Border
			{
				Width = 16.0,
				Height = 16.0,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Child = leadingIcon
			};
			Grid.SetColumn(element, num++);
			grid.Children.Add(element);
		}
		TextBlock element2 = new TextBlock
		{
			Text = (title ?? string.Empty),
			FontSize = 13.0,
			FontWeight = FontWeights.Normal,
			Foreground = BuddyTokens.Text100Brush,
			FontFamily = BuddyTypography.FontText,
			TextTrimming = TextTrimming.CharacterEllipsis,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(element2, num++);
		grid.Children.Add(element2);
		if (!string.IsNullOrEmpty(description))
		{
			_metaBlock = new TextBlock
			{
				Text = description,
				FontSize = 11.5,
				FontWeight = FontWeights.Normal,
				Foreground = BuddyTokens.Text38Brush,
				FontFamily = BuddyTypography.FontText,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 0.0, (trailingControl != null) ? 8 : 0, 0.0)
			};
			BuddyTypography.SetTabularFigures(_metaBlock);
			Grid.SetColumn(_metaBlock, num++);
			grid.Children.Add(_metaBlock);
		}
		if (trailingControl != null)
		{
			_actionsHost = new Border
			{
				VerticalAlignment = VerticalAlignment.Center,
				Opacity = 0.0,
				Child = trailingControl
			};
			Grid.SetColumn(_actionsHost, num++);
			grid.Children.Add(_actionsHost);
		}
		Child = grid;
		base.MouseEnter += delegate
		{
			base.Background = BuddyTokens.CardHoverBrush;
			if (_actionsHost != null)
			{
				if (CapsuleMotion.AnimationsEnabled)
				{
					DoubleAnimation animation2 = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120.0));
					_actionsHost.BeginAnimation(UIElement.OpacityProperty, animation2);
				}
				else
				{
					_actionsHost.Opacity = 1.0;
				}
			}
		};
		base.MouseLeave += delegate
		{
			base.Background = BuddyTokens.RowBrush;
			if (_actionsHost != null)
			{
				if (CapsuleMotion.AnimationsEnabled)
				{
					DoubleAnimation animation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120.0));
					_actionsHost.BeginAnimation(UIElement.OpacityProperty, animation);
				}
				else
				{
					_actionsHost.Opacity = 0.0;
				}
			}
		};
		if (isClickable)
		{
			_pressScale = new ScaleTransform(1.0, 1.0);
			base.RenderTransformOrigin = new Point(0.5, 0.5);
			base.RenderTransform = _pressScale;
			base.MouseLeftButtonDown += delegate
			{
				AnimatePress();
			};
			base.MouseLeftButtonUp += delegate
			{
				AnimateRelease();
				this.Clicked?.Invoke(this, EventArgs.Empty);
			};
		}
	}

	public static BuddyListRow Create(string title, string description = null, UIElement icon = null, UIElement trailing = null, bool clickable = false)
	{
		return new BuddyListRow(title, description, icon, trailing, clickable);
	}

	private void AnimatePress()
	{
		if (_pressScale != null)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(70.0));
			doubleAnimation.EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation animation = doubleAnimation;
			_pressScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			_pressScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}
	}

	private void AnimateRelease()
	{
		if (_pressScale != null)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100.0));
			doubleAnimation.EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation animation = doubleAnimation;
			_pressScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			_pressScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}
	}
}
