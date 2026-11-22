using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class OverlayScroll
{
	public const double ThumbWidth = 6.0;

	public const double HoverWidth = 10.0;

	public const double InkOpacity = 0.25;

	public const double HoverOpacity = 0.45;

	public const int HideMilliseconds = 1000;

	private static readonly ConditionalWeakTable<ScrollViewer, OverlayScroll> Owners = new ConditionalWeakTable<ScrollViewer, OverlayScroll>();

	private static bool installed;

	private readonly ScrollViewer viewer;

	private readonly Border vertical;

	private readonly Border horizontal;

	private readonly DispatcherTimer hide;

	private bool dragging;

	private bool horizontalDrag;

	private bool moving;

	private double dragStart;

	private double offsetStart;

	private double target;

	private double origin;

	private DateTime started;

	public static void PaintThumb(Border thumb, bool hover, bool horizontal = false)
	{
		thumb.Background = ThemeTokens.Text;
		thumb.CornerRadius = new CornerRadius(5.0);
		if (horizontal)
		{
			thumb.Height = (hover ? 10.0 : 6.0);
		}
		else
		{
			thumb.Width = (hover ? 10.0 : 6.0);
		}
		thumb.Opacity = (hover ? 0.45 : 0.25);
	}

	public static void Attach(ScrollViewer viewer)
	{
		Owners.GetValue(viewer, (ScrollViewer x) => new OverlayScroll(x));
	}

	public static void Install()
	{
		if (installed || Application.Current == null)
		{
			return;
		}
		installed = true;
		Style style = new Style(typeof(ScrollViewer), Application.Current.TryFindResource(typeof(ScrollViewer)) as Style);
		style.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent, (RoutedEventHandler)delegate(object s, RoutedEventArgs e)
		{
			ScrollViewer key2 = (ScrollViewer)s;
			Owners.GetValue(key2, (ScrollViewer x) => new OverlayScroll(x));
		}));
		Application.Current.Resources[typeof(ScrollViewer)] = style;
		Application.Current.Resources[typeof(BuddyScrollViewer)] = style;
		EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent, (RoutedEventHandler)delegate(object s, RoutedEventArgs e)
		{
			ScrollViewer key = (ScrollViewer)s;
			Owners.GetValue(key, (ScrollViewer x) => new OverlayScroll(x));
		}, handledEventsToo: true);
	}

	public OverlayScroll(ScrollViewer v)
	{
		OverlayScroll overlayScroll = this;
		viewer = v;
		ControlTemplate controlTemplate = new ControlTemplate(typeof(ScrollViewer));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Grid));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ScrollContentPresenter), "PART_ScrollContentPresenter");
		frameworkElementFactory2.SetValue(ScrollContentPresenter.CanContentScrollProperty, new TemplateBindingExtension(ScrollViewer.CanContentScrollProperty));
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		frameworkElementFactory.AppendChild(Thumb("OverlayVertical", horizontal: false));
		frameworkElementFactory.AppendChild(Thumb("OverlayHorizontal", horizontal: true));
		controlTemplate.VisualTree = frameworkElementFactory;
		v.Template = controlTemplate;
		v.ApplyTemplate();
		vertical = (Border)controlTemplate.FindName("OverlayVertical", v);
		horizontal = (Border)controlTemplate.FindName("OverlayHorizontal", v);
		hide = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		DispatcherTimer dispatcherTimer = hide;
		EventHandler value = delegate
		{
			hide.Stop();
			if (!dragging && !vertical.IsMouseOver && !horizontal.IsMouseOver)
			{
				SetVisible(value: false);
			}
		};
		dispatcherTimer.Tick += value;
		Border[] array = new Border[2] { vertical, horizontal };
		foreach (Border t in array)
		{
			Border border = t;
			MouseEventHandler value2 = delegate
			{
				PaintThumb(t, hover: true, t == overlayScroll.horizontal);
			};
			border.MouseEnter += value2;
			t.MouseLeave += delegate
			{
				PaintThumb(t, hover: false, t == overlayScroll.horizontal);
				overlayScroll.hide.Stop();
				overlayScroll.hide.Start();
			};
			t.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				overlayScroll.StopMotion();
				overlayScroll.dragging = true;
				overlayScroll.horizontalDrag = t == overlayScroll.horizontal;
				overlayScroll.dragStart = overlayScroll.Coordinate(e);
				overlayScroll.offsetStart = (overlayScroll.horizontalDrag ? v.HorizontalOffset : v.VerticalOffset);
				t.CaptureMouse();
				e.Handled = true;
			};
			t.MouseMove += delegate(object s, MouseEventArgs e)
			{
				if (overlayScroll.dragging && Mouse.Captured == t)
				{
					double num = (overlayScroll.horizontalDrag ? v.ScrollableWidth : v.ScrollableHeight);
					double val = (overlayScroll.horizontalDrag ? (v.ActualWidth - overlayScroll.horizontal.ActualWidth - 8.0) : (v.ActualHeight - overlayScroll.vertical.ActualHeight - 8.0));
					double offset = overlayScroll.offsetStart + (overlayScroll.Coordinate(e) - overlayScroll.dragStart) * num / Math.Max(1.0, val);
					if (overlayScroll.horizontalDrag)
					{
						v.ScrollToHorizontalOffset(offset);
					}
					else
					{
						v.ScrollToVerticalOffset(offset);
					}
					e.Handled = true;
				}
			};
			t.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				overlayScroll.dragging = false;
				t.ReleaseMouseCapture();
				overlayScroll.Show();
				e.Handled = true;
			};
			t.LostMouseCapture += delegate
			{
				dragging = false;
			};
		}
		v.ScrollChanged += delegate(object s, ScrollChangedEventArgs e)
		{
			Geometry();
			if (e.VerticalChange != 0.0 || e.HorizontalChange != 0.0)
			{
				Show();
			}
		};
		v.SizeChanged += delegate
		{
			Geometry();
		};
		v.MouseEnter += delegate
		{
			Show();
		};
		v.MouseLeave += delegate
		{
			hide.Stop();
			hide.Start();
		};
		v.MouseMove += delegate(object s, MouseEventArgs e)
		{
			Point position = e.GetPosition(v);
			if ((v.ScrollableHeight > 0.0 && position.X >= v.ActualWidth - 14.0) || (v.ScrollableWidth > 0.0 && position.Y >= v.ActualHeight - 14.0))
			{
				overlayScroll.Show();
			}
		};
		v.PreviewMouseWheel += Wheel;
		v.Unloaded += delegate
		{
			StopMotion();
			hide.Stop();
			dragging = false;
			Border[] array2 = new Border[2] { vertical, horizontal };
			foreach (Border border2 in array2)
			{
				if (border2.IsMouseCaptured)
				{
					border2.ReleaseMouseCapture();
				}
			}
		};
		ThemeTokens.Subscribe(v, delegate
		{
			vertical.Background = ThemeTokens.Text;
			horizontal.Background = ThemeTokens.Text;
		});
		vertical.Background = (horizontal.Background = ThemeTokens.Text);
		Geometry();
	}

	private static FrameworkElementFactory Thumb(string name, bool horizontal)
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border), name);
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5.0));
		frameworkElementFactory.SetValue(horizontal ? FrameworkElement.HeightProperty : FrameworkElement.WidthProperty, 6.0);
		frameworkElementFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, (!horizontal) ? HorizontalAlignment.Right : HorizontalAlignment.Left);
		frameworkElementFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, horizontal ? VerticalAlignment.Bottom : VerticalAlignment.Top);
		frameworkElementFactory.SetValue(UIElement.OpacityProperty, 0.0);
		frameworkElementFactory.SetValue(UIElement.IsHitTestVisibleProperty, false);
		return frameworkElementFactory;
	}

	private double Coordinate(MouseEventArgs e)
	{
		Point position = e.GetPosition(viewer);
		if (!horizontalDrag)
		{
			return position.Y;
		}
		return position.X;
	}

	private void SetVisible(bool value)
	{
		Border[] array = new Border[2] { vertical, horizontal };
		foreach (Border border in array)
		{
			if (value)
			{
				PaintThumb(border, border.IsMouseOver, border == horizontal);
			}
			else
			{
				border.Opacity = 0.0;
			}
			border.IsHitTestVisible = value;
		}
	}

	private void Show()
	{
		SetVisible(value: true);
		hide.Stop();
		hide.Start();
	}

	private void Geometry()
	{
		Position(vertical, h: false);
		Position(horizontal, h: true);
	}

	private void Position(Border t, bool h)
	{
		double num = (h ? viewer.ViewportWidth : viewer.ViewportHeight);
		double num2 = (h ? viewer.ExtentWidth : viewer.ExtentHeight);
		double num3 = (h ? viewer.HorizontalOffset : viewer.VerticalOffset);
		double num4 = Math.Max(0.0, (h ? viewer.ActualWidth : viewer.ActualHeight) - 8.0);
		bool flag = (h ? viewer.HorizontalScrollBarVisibility : viewer.VerticalScrollBarVisibility) != ScrollBarVisibility.Disabled;
		t.Visibility = ((!flag || !(num2 > num) || !(num > 0.0)) ? Visibility.Collapsed : Visibility.Visible);
		if (t.Visibility == Visibility.Visible)
		{
			double num5 = Math.Min(num4, Math.Max(24.0, num4 * num / num2));
			double num6 = 4.0 + Math.Max(0.0, Math.Min(1.0, num3 / (num2 - num))) * (num4 - num5);
			if (h)
			{
				t.Width = num5;
				t.Margin = new Thickness(num6, 0.0, 0.0, 3.0);
			}
			else
			{
				t.Height = num5;
				t.Margin = new Thickness(0.0, num6, 3.0, 0.0);
			}
		}
	}

	private void Wheel(object sender, MouseWheelEventArgs e)
	{
		try
		{
			if (ToolRouterNotch() != null)
			{
				ToolRouterNotch().RecheckPointer("scroll");
			}
		}
		catch
		{
		}
		if (e.Handled || viewer.ScrollableHeight <= 0.0)
		{
			return;
		}
		DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
		while (dependencyObject != null && dependencyObject != viewer)
		{
			if (dependencyObject is ScrollViewer { ScrollableHeight: >0.0 })
			{
				return;
			}
			try
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			catch
			{
				return;
			}
		}
		if (!viewer.CanContentScroll)
		{
			origin = viewer.VerticalOffset;
			target = Math.Max(0.0, Math.Min(viewer.ScrollableHeight, (moving ? target : origin) - (double)e.Delta / 3.0));
			started = DateTime.UtcNow;
			e.Handled = true;
			Show();
			if (!CapsuleMotion.AnimationsEnabled)
			{
				StopMotion();
				viewer.ScrollToVerticalOffset(target);
			}
			else if (!moving)
			{
				moving = true;
				CompositionTarget.Rendering += Frame;
			}
		}
	}

	private void Frame(object s, EventArgs e)
	{
		double num = Math.Min(1.0, (DateTime.UtcNow - started).TotalMilliseconds / 160.0);
		viewer.ScrollToVerticalOffset(origin + (target - origin) * (1.0 - Math.Pow(1.0 - num, 3.0)));
		if (num >= 1.0)
		{
			StopMotion();
		}
	}

	private void StopMotion()
	{
		if (moving)
		{
			CompositionTarget.Rendering -= Frame;
		}
		moving = false;
	}

	private static NotchStateMachine ToolRouterNotch()
	{
		return ToolRouter.Instance.Notch;
	}
}
