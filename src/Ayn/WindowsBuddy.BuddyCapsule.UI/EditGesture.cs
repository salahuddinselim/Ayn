using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class EditGesture
{
	private static readonly ConditionalWeakTable<FrameworkElement, EditGesture> Owners = new ConditionalWeakTable<FrameworkElement, EditGesture>();

	private readonly FrameworkElement element;

	private readonly Panel panel;

	private readonly string id;

	private readonly Func<List<string>> order;

	private readonly Action<List<string>> commit;

	private readonly RotateTransform rotation = new RotateTransform();

	private readonly ScaleTransform scale = new ScaleTransform(1.0, 1.0);

	private readonly TranslateTransform shift = new TranslateTransform();

	private readonly TransformGroup transforms = new TransformGroup();

	private readonly bool swap;

	private Point start;

	private bool down;

	private bool dragging;

	private bool wiggling;

	private List<string> original;

	private List<string> preview;

	private readonly Action remove;

	private readonly Dictionary<string, Rect> slots = new Dictionary<string, Rect>();

	public static void Attach(FrameworkElement view, Panel parent, string key, Func<List<string>> getOrder, Action<List<string>> save, Action delete, bool swap = false)
	{
		EditGesture value = Owners.GetValue(view, (FrameworkElement _) => new EditGesture(view, parent, key, getOrder, save, delete, swap));
		value.Update();
	}

	public static Border Badge(string label, Action remove)
	{
		Border border = new Border();
		border.Width = 16.0;
		border.Height = 16.0;
		border.CornerRadius = new CornerRadius(8.0);
		border.Background = ThemeTokens.Danger;
		border.HorizontalAlignment = HorizontalAlignment.Left;
		border.VerticalAlignment = VerticalAlignment.Top;
		border.Cursor = Cursors.Hand;
		border.Focusable = true;
		border.Child = new TextBlock
		{
			Text = "−",
			Foreground = Brushes.White,
			FontFamily = BuddyTokens.FontText,
			FontSize = 13.0,
			TextAlignment = TextAlignment.Center,
			IsHitTestVisible = false
		};
		Border border2 = border;
		AutomationProperties.SetName(border2, "Remove " + label);
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			remove();
		};
		border2.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				remove();
				e.Handled = true;
			}
		};
		return border2;
	}

	public EditGesture(FrameworkElement view, Panel parent, string key, Func<List<string>> getOrder, Action<List<string>> save, Action delete, bool swap = false)
	{
		EditGesture editGesture = this;
		element = view;
		panel = parent;
		id = key;
		order = getOrder;
		commit = save;
		remove = delete;
		this.swap = swap;
		transforms.Children.Add(rotation);
		transforms.Children.Add(scale);
		transforms.Children.Add(shift);
		element.RenderTransform = transforms;
		element.RenderTransformOrigin = new Point(0.5, 0.5);
		element.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Down), handledEventsToo: true);
		element.AddHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler(Move), handledEventsToo: true);
		element.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Up), handledEventsToo: true);
		FrameworkElement frameworkElement = element;
		MouseEventHandler value = delegate
		{
			if (down)
			{
				Cancel();
			}
		};
		frameworkElement.LostMouseCapture += value;
		element.Unloaded += delegate
		{
			Stop();
		};
		element.IsVisibleChanged += delegate
		{
			if (!element.IsVisible)
			{
				Stop();
			}
			else
			{
				element.BeginAnimation(UIElement.OpacityProperty, null);
				element.Opacity = 1.0;
				element.RenderTransform = transforms;
				Update();
			}
		};
		element.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (NotchEditing.IsEditing)
			{
				if (e.Key == Key.Delete && editGesture.remove != null)
				{
					editGesture.remove();
					e.Handled = true;
				}
				else if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
				{
					List<string> list = editGesture.order();
					int num = list.IndexOf(editGesture.id);
					int index = Math.Max((list.Contains("home") && editGesture.id != "home") ? 1 : 0, Math.Min(list.Count - 1, num + ((!swap || (e.Key != Key.Up && e.Key != Key.Down)) ? ((e.Key != Key.Left && e.Key != Key.Up) ? 1 : (-1)) : ((e.Key == Key.Up) ? (-3) : 3))));
					if (editGesture.id != "home" && num >= 0)
					{
						if (swap)
						{
							list[num] = list[index];
							list[index] = editGesture.id;
						}
						else
						{
							list.Remove(editGesture.id);
							list.Insert(index, editGesture.id);
						}
						editGesture.commit(list);
					}
					e.Handled = true;
				}
			}
		};
		Update();
	}

	private void Update()
	{
		rotation.BeginAnimation(RotateTransform.AngleProperty, null);
		rotation.Angle = 0.0;
		wiggling = false;
	}

	private void Down(object sender, MouseButtonEventArgs e)
	{
		if (!NotchEditing.IsEditing)
		{
			return;
		}
		DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
		while (dependencyObject != null && dependencyObject != element)
		{
			if (AutomationProperties.GetName(dependencyObject).StartsWith("Remove "))
			{
				return;
			}
			try
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			catch
			{
				break;
			}
		}
		element.Focus();
		start = e.GetPosition(panel);
		down = true;
		dragging = false;
		original = order();
		preview = new List<string>(original);
		slots.Clear();
		foreach (FrameworkElement item in from x in panel.Children.OfType<FrameworkElement>()
			where x.Tag is string
			select x)
		{
			slots[(string)item.Tag] = LayoutInformation.GetLayoutSlot(item);
		}
		element.CaptureMouse();
		e.Handled = true;
	}

	private void Move(object sender, MouseEventArgs e)
	{
		if (!down || e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}
		Point position = e.GetPosition(panel);
		if (!dragging && (position - start).Length > 5.0)
		{
			dragging = true;
			element.CaptureMouse();
			if (CapsuleMotion.AnimationsEnabled)
			{
				CapsuleMotion.AnimateDouble(scale, ScaleTransform.ScaleXProperty, 1.03, 0.18, CapsuleMotion.Spring);
				CapsuleMotion.AnimateDouble(scale, ScaleTransform.ScaleYProperty, 1.03, 0.18, CapsuleMotion.Spring);
				element.Effect = new DropShadowEffect
				{
					BlurRadius = 12.0,
					ShadowDepth = 3.0,
					Opacity = 0.15
				};
			}
			Panel.SetZIndex(element, 20);
		}
		if (!dragging)
		{
			return;
		}
		shift.X = position.X - start.X;
		shift.Y = position.Y - start.Y;
		List<FrameworkElement> list = (from x in panel.Children.OfType<FrameworkElement>()
			where x != element && x.Tag is string && preview.Contains((string)x.Tag)
			select x).ToList();
		foreach (FrameworkElement item in list)
		{
			string text = (string)item.Tag;
			if (!slots.TryGetValue(text, out var value) || !value.Contains(position))
			{
				continue;
			}
			if (!(id == "home"))
			{
				int index = (swap ? original.IndexOf(id) : preview.IndexOf(id));
				int num = ((text == "home") ? 1 : original.IndexOf(text));
				if (swap)
				{
					preview = new List<string>(original);
					preview[index] = original[num];
					preview[num] = id;
				}
				else
				{
					preview.RemoveAt(index);
					preview.Insert(Math.Min(num, preview.Count), id);
				}
				Preview(preview);
			}
			break;
		}
		e.Handled = true;
	}

	private void Preview(List<string> ids)
	{
		foreach (FrameworkElement item in from x in panel.Children.OfType<FrameworkElement>()
			where x != element && x.Tag is string
			select x)
		{
			int num = original.IndexOf((string)item.Tag);
			int num2 = ids.IndexOf((string)item.Tag);
			if (num >= 0 && num2 >= 0 && slots.ContainsKey(original[num2]) && slots.ContainsKey((string)item.Tag))
			{
				Rect rect = slots[(string)item.Tag];
				Rect rect2 = slots[original[num2]];
				if (item.RenderTransform is TransformGroup transformGroup)
				{
					TranslateTransform translateTransform = transformGroup.Children.OfType<TranslateTransform>().Last();
					CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.XProperty, rect2.X - rect.X, 0.18, CapsuleMotion.Spring);
					CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.YProperty, rect2.Y - rect.Y, 0.18, CapsuleMotion.Spring);
				}
			}
		}
	}

	private void Up(object sender, MouseButtonEventArgs e)
	{
		if (down)
		{
			down = false;
			Point position = e.GetPosition(panel);
			bool flag = dragging && new Rect(panel.RenderSize).Contains(position);
			if (element.IsMouseCaptured)
			{
				element.ReleaseMouseCapture();
			}
			Reset();
			if (flag)
			{
				commit(preview);
			}
			Update();
			e.Handled = true;
		}
	}

	private void Cancel()
	{
		down = false;
		Reset();
	}

	private void Reset()
	{
		dragging = false;
		scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		ScaleTransform scaleTransform = scale;
		double scaleX = (scale.ScaleY = 1.0);
		scaleTransform.ScaleX = scaleX;
		TranslateTransform translateTransform = shift;
		double x = (shift.Y = 0.0);
		translateTransform.X = x;
		element.Effect = null;
		Panel.SetZIndex(element, 0);
		foreach (FrameworkElement item in panel.Children.OfType<FrameworkElement>())
		{
			if (!(item.RenderTransform is TransformGroup transformGroup))
			{
				continue;
			}
			foreach (TranslateTransform item2 in transformGroup.Children.OfType<TranslateTransform>())
			{
				item2.BeginAnimation(TranslateTransform.XProperty, null);
				item2.BeginAnimation(TranslateTransform.YProperty, null);
				double x2 = (item2.Y = 0.0);
				item2.X = x2;
			}
		}
	}

	private void Stop()
	{
		Cancel();
		wiggling = false;
		rotation.BeginAnimation(RotateTransform.AngleProperty, null);
		rotation.Angle = 0.0;
	}

	public static void Remove(FrameworkElement view, Action finish)
	{
		ScaleTransform scaleTransform = (ScaleTransform)(view.RenderTransform = new ScaleTransform(1.0, 1.0));
		view.RenderTransformOrigin = new Point(0.5, 0.5);
		CapsuleMotion.AnimateDouble(scaleTransform, ScaleTransform.ScaleXProperty, 0.5, 0.18, CapsuleMotion.Spring);
		CapsuleMotion.AnimateDouble(scaleTransform, ScaleTransform.ScaleYProperty, 0.5, 0.18, CapsuleMotion.Spring);
		CapsuleMotion.AnimateDouble(view, UIElement.OpacityProperty, 0.0, 0.18, CapsuleMotion.EaseOutCubic, finish);
	}
}
