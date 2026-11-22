using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class BuddyKit
{
	public static Button AccessibleButton(UIElement content, string name, Action click, double radius = 5.0)
	{
		Button button = new Button
		{
			Content = content,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Cursor = Cursors.Hand,
			FocusVisualStyle = null
		};
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Grid));
		frameworkElementFactory.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
		frameworkElementFactory.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory2.SetValue(UIElement.IsHitTestVisibleProperty, false);
		frameworkElementFactory2.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		frameworkElementFactory2.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
		frameworkElementFactory2.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
		});
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		button.Template = new ControlTemplate(typeof(Button))
		{
			VisualTree = frameworkElementFactory
		};
		button.BorderBrush = Brushes.Transparent;
		button.IsKeyboardFocusedChanged += delegate
		{
			button.BorderBrush = (button.IsKeyboardFocused ? BuddyTheme.FocusRing : Brushes.Transparent);
		};
		AutomationProperties.SetName(button, name);
		button.Click += delegate(object s, RoutedEventArgs e)
		{
			e.Handled = true;
			click();
		};
		button.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		BuddyTheme.Pressable(button);
		return button;
	}

	public static void KeyboardFocus(FrameworkElement element, string name)
	{
		element.Focusable = true;
		KeyboardNavigation.SetIsTabStop(element, isTabStop: true);
		AutomationProperties.SetName(element, name);
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3.0));
		frameworkElementFactory.SetBinding(Border.BorderBrushProperty, ThemeTokens.Ref(BuddyTheme.FocusRing));
		Style style = new Style(typeof(Control));
		style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control))
		{
			VisualTree = frameworkElementFactory
		}));
		element.FocusVisualStyle = style;
	}

	public static Border CircleButton(string icon, double size, string tip, Action click)
	{
		ContentControl contentControl = new ContentControl();
		contentControl.Width = 18.0;
		contentControl.Height = 18.0;
		contentControl.Focusable = false;
		contentControl.IsTabStop = false;
		contentControl.HorizontalAlignment = HorizontalAlignment.Center;
		contentControl.VerticalAlignment = VerticalAlignment.Center;
		contentControl.IsHitTestVisible = false;
		contentControl.Content = FluentIcons.Create(icon, 18.0, BuddyTheme.Text92, filled: false);
		ContentControl child = contentControl;
		Border b = new Border
		{
			Width = size,
			Height = size,
			CornerRadius = new CornerRadius(size / 2.0),
			Background = BuddyTheme.Control,
			Cursor = Cursors.Hand,
			Child = child,
			ToolTip = BuddyTooltip.Create(tip)
		};
		b.MouseEnter += delegate
		{
			if (b.Background != Brushes.Transparent)
			{
				b.Background = BuddyTheme.ControlHover;
			}
		};
		b.MouseLeave += delegate
		{
			if (b.Background != Brushes.Transparent)
			{
				b.Background = BuddyTheme.Control;
			}
		};
		b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (click != null)
			{
				click();
			}
		};
		return b;
	}

	public static ContentControl GlyphOf(Border button)
	{
		if (button == null)
		{
			return null;
		}
		return button.Child as ContentControl;
	}

	public static Border IconButton(string icon, string tip, Action click, out ContentControl glyphHost)
	{
		ContentControl glyph = new ContentControl
		{
			Width = 18.0,
			Height = 18.0,
			Focusable = false,
			IsTabStop = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			Opacity = 0.5,
			Content = FluentIcons.Create(icon, 18.0, BuddyTheme.Text100, filled: false)
		};
		glyphHost = glyph;
		Border border = new Border();
		border.Width = 28.0;
		border.Height = 28.0;
		border.CornerRadius = new CornerRadius(14.0);
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Child = glyph;
		border.ToolTip = BuddyTooltip.Create(tip);
		Border border2 = border;
		border2.MouseEnter += delegate
		{
			glyph.Opacity = 1.0;
		};
		border2.MouseLeave += delegate
		{
			glyph.Opacity = 0.5;
		};
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (click != null)
			{
				click();
			}
		};
		return border2;
	}

	public static Border GlyphButton(string icon, double glyph, string tip, Action click, out ContentControl glyphHost)
	{
		ContentControl g = new ContentControl
		{
			Width = glyph,
			Height = glyph,
			Focusable = false,
			IsTabStop = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			Opacity = 0.66,
			Content = FluentIcons.Create(icon, glyph, BuddyTheme.Text92, filled: false)
		};
		glyphHost = g;
		Border border = new Border();
		border.Width = glyph + 8.0;
		border.Height = glyph + 8.0;
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Child = g;
		border.ToolTip = BuddyTooltip.Create(tip);
		Border border2 = border;
		border2.MouseEnter += delegate
		{
			g.Opacity = 1.0;
		};
		border2.MouseLeave += delegate
		{
			g.Opacity = 0.66;
		};
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (click != null)
			{
				click();
			}
		};
		return border2;
	}

	public static Border TextButton(string text, Action click)
	{
		TextBlock child = BuddyTheme.Text(text, 13.0, BuddyTheme.Text100, AynType.Medium);
		Border b = new Border
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
			CornerRadius = new CornerRadius(10.0),
			Child = child
		};
		b.MouseEnter += delegate
		{
			b.Background = BuddyTheme.Row;
		};
		b.MouseLeave += delegate
		{
			b.Background = Brushes.Transparent;
		};
		b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (click != null)
			{
				click();
			}
		};
		return b;
	}
}
