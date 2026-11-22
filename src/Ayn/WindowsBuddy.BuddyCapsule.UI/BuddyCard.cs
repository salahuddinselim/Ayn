using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsBuddy.BuddyCapsule.Animation;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyCard : Border
{
	private readonly TranslateTransform _translate;

	private BuddyCardHeader _header;

	private UIElement _content;

	private StackPanel _layout;

	public BuddyCardHeader Header
	{
		get
		{
			return _header;
		}
		set
		{
			_header = value;
			RebuildLayout();
		}
	}

	public UIElement ContentSlot
	{
		get
		{
			return _content;
		}
		set
		{
			_content = value;
			RebuildLayout();
		}
	}

	public event EventHandler Click;

	public BuddyCard()
		: this(null, null)
	{
	}

	public BuddyCard(UIElement content)
		: this(null, content)
	{
	}

	public BuddyCard(BuddyCardHeader header, UIElement content)
	{
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		base.Background = BuddyTokens.CardBrush;
		base.BorderBrush = BuddyTokens.CardBorderBrush;
		base.BorderThickness = new Thickness(1.0);
		base.CornerRadius = new CornerRadius(16.0);
		base.Padding = new Thickness(16.0);
		base.Focusable = true;
		KeyboardNavigation.SetIsTabStop(this, isTabStop: true);
		base.FocusVisualStyle = null;
		base.Cursor = Cursors.Hand;
		_translate = new TranslateTransform(0.0, 0.0);
		base.RenderTransform = _translate;
		_layout = new StackPanel
		{
			Orientation = Orientation.Vertical
		};
		Child = _layout;
		_header = header;
		_content = content;
		RebuildLayout();
		MouseEventHandler value = delegate
		{
			base.Background = BuddyTokens.CardHoverBrush;
			if (!base.IsFocused)
			{
				base.BorderBrush = BuddyTokens.CardBorderHoverBrush;
			}
			if (CapsuleMotion.AnimationsEnabled)
			{
				DoubleAnimation animation2 = new DoubleAnimation(-1.0, TimeSpan.FromMilliseconds(180.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				_translate.BeginAnimation(TranslateTransform.YProperty, animation2);
			}
			else
			{
				_translate.Y = -1.0;
			}
		};
		base.MouseEnter += value;
		base.MouseLeave += delegate
		{
			base.Background = BuddyTokens.CardBrush;
			if (!base.IsFocused)
			{
				base.BorderBrush = BuddyTokens.CardBorderBrush;
			}
			if (CapsuleMotion.AnimationsEnabled)
			{
				DoubleAnimation animation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				_translate.BeginAnimation(TranslateTransform.YProperty, animation);
			}
			else
			{
				_translate.Y = 0.0;
			}
		};
		base.GotFocus += delegate
		{
			base.BorderBrush = BuddyTokens.FocusRingBrush;
		};
		base.LostFocus += delegate
		{
			base.BorderBrush = (base.IsMouseOver ? BuddyTokens.CardBorderHoverBrush : BuddyTokens.CardBorderBrush);
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				if (this.Click != null)
				{
					this.Click(this, EventArgs.Empty);
				}
			}
		};
		base.MouseLeftButtonUp += delegate
		{
			if (this.Click != null)
			{
				this.Click(this, EventArgs.Empty);
			}
		};
	}

	private void RebuildLayout()
	{
		_layout.Children.Clear();
		if (_header != null)
		{
			_layout.Children.Add(_header);
		}
		if (_content != null)
		{
			_layout.Children.Add(_content);
		}
	}
}
