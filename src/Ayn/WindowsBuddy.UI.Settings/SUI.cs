using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal static class SUI
{
	internal static Window LastDiagnosticsReview;

	public static FrameworkElement PageHeader(string title, string subtitle, FrameworkElement tile = null)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(SettingsTheme.Text(title, 28.0, SettingsTheme.TextPrimary, AynType.Semibold));
		if (!string.IsNullOrEmpty(subtitle))
		{
			TextBlock textBlock = SettingsTheme.Text(subtitle, 14.0, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock);
		}
		return stackPanel2;
	}

	public static FrameworkElement SectionLabel(string text)
	{
		TextBlock textBlock = SettingsTheme.Text(text.ToUpperInvariant(), 12.0, SettingsTheme.TextLabel, AynType.Medium);
		textBlock.Margin = new Thickness(0.0, 32.0, 0.0, 12.0);
		return textBlock;
	}

	public static Border Group(params UIElement[] rows)
	{
		SCard sCard = new SCard();
		foreach (UIElement row in rows)
		{
			AddToGroup(sCard.Stack, row);
		}
		return sCard;
	}

	public static void AddToGroup(Panel groupStack, UIElement row)
	{
		if (row != null)
		{
			groupStack.Children.Add(row);
		}
	}

	public static StackPanel GroupStack(Border group)
	{
		if (!(group is SCard sCard))
		{
			return (StackPanel)group.Child;
		}
		return sCard.Stack;
	}

	public static Grid Row(string title, string description, UIElement trailing, UIElement leading = null, bool isRightAlignedAction = false, string settingId = null)
	{
		Grid g = new Grid
		{
			Margin = new Thickness(0.0, 20.0, 0.0, 20.0),
			Tag = settingId,
			Background = Brushes.Transparent,
			MaxWidth = 616.0
		};
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(272.0)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(24.0)
		});
		g.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		AutomationProperties.SetName(g, title);
		if (settingId != null)
		{
			AutomationProperties.SetAutomationId(g, settingId);
		}
		StackPanel left = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		if (leading != null)
		{
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			if (leading is FrameworkElement frameworkElement)
			{
				frameworkElement.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			}
			grid.Children.Add(leading);
			StackPanel stackPanel = new StackPanel();
			stackPanel.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel2 = stackPanel;
			TextBlock textBlock = SettingsTheme.Text(title, 15.0, SettingsTheme.TextPrimary, AynType.Medium);
			textBlock.TextWrapping = TextWrapping.Wrap;
			textBlock.TextTrimming = TextTrimming.None;
			stackPanel2.Children.Add(textBlock);
			if (!string.IsNullOrEmpty(description))
			{
				TextBlock textBlock2 = SettingsTheme.Text(description, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
				textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
				textBlock2.TextWrapping = TextWrapping.Wrap;
				textBlock2.TextTrimming = TextTrimming.None;
				textBlock2.LineHeight = 20.0;
				stackPanel2.Children.Add(textBlock2);
			}
			Grid.SetColumn(stackPanel2, 1);
			grid.Children.Add(stackPanel2);
			left.Children.Add(grid);
		}
		else
		{
			TextBlock textBlock3 = SettingsTheme.Text(title, 15.0, SettingsTheme.TextPrimary, AynType.Medium);
			textBlock3.TextWrapping = TextWrapping.Wrap;
			textBlock3.TextTrimming = TextTrimming.None;
			left.Children.Add(textBlock3);
			if (!string.IsNullOrEmpty(description))
			{
				TextBlock textBlock4 = SettingsTheme.Text(description, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
				textBlock4.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
				textBlock4.TextWrapping = TextWrapping.Wrap;
				textBlock4.TextTrimming = TextTrimming.None;
				textBlock4.LineHeight = 20.0;
				left.Children.Add(textBlock4);
			}
		}
		Grid.SetColumn(left, 0);
		g.Children.Add(left);
		if (trailing != null)
		{
			if (trailing is FrameworkElement frameworkElement2)
			{
				frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
				if (isRightAlignedAction || frameworkElement2.HorizontalAlignment == HorizontalAlignment.Right)
				{
					frameworkElement2.HorizontalAlignment = HorizontalAlignment.Right;
				}
				else
				{
					frameworkElement2.HorizontalAlignment = HorizontalAlignment.Left;
					if (double.IsNaN(frameworkElement2.MaxWidth) || frameworkElement2.MaxWidth > 320.0)
					{
						frameworkElement2.MaxWidth = 320.0;
					}
				}
				if (string.IsNullOrEmpty(AutomationProperties.GetName(frameworkElement2)))
				{
					AutomationProperties.SetName(frameworkElement2, title);
				}
			}
			Grid.SetColumn(trailing, 2);
			g.Children.Add(trailing);
		}
		g.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		g.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		g.SizeChanged += delegate
		{
			FrameworkElement frameworkElement3 = trailing as FrameworkElement;
			bool flag = g.ActualWidth < 560.0;
			g.ColumnDefinitions[0].Width = (flag ? new GridLength(1.0, GridUnitType.Star) : new GridLength(272.0));
			g.ColumnDefinitions[1].Width = new GridLength(flag ? 0.0 : 24.0);
			g.ColumnDefinitions[2].Width = (flag ? new GridLength(0.0) : new GridLength(1.0, GridUnitType.Star));
			Grid.SetColumnSpan(left, (!flag) ? 1 : 3);
			if (frameworkElement3 != null)
			{
				Grid.SetRow(frameworkElement3, flag ? 1 : 0);
				Grid.SetColumn(frameworkElement3, (!flag) ? 2 : 0);
				Grid.SetColumnSpan(frameworkElement3, (!flag) ? 1 : 3);
				frameworkElement3.Margin = new Thickness(0.0, flag ? 8 : 0, 0.0, 0.0);
			}
		};
		return g;
	}

	public static Grid ActionRow(string title, string description, string actionText, Action onClick, UIElement leading = null, string settingId = null)
	{
		SButton sButton = new SButton(actionText);
		sButton.Click += delegate
		{
			onClick();
		};
		return Row(title, description, sButton, leading, isRightAlignedAction: true, settingId);
	}

	public static Grid ValueRow(string title, string description, string valueText, UIElement leading = null, string settingId = null)
	{
		TextBlock textBlock = SettingsTheme.Text(valueText, 15.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		return Row(title, description, textBlock, leading, isRightAlignedAction: true, settingId);
	}

	public static Grid ToggleRow(string title, string description, bool value, Action<bool> onChange, UIElement leading = null, string settingId = null, Func<bool> read = null)
	{
		SToggle t = new SToggle(value, title);
		t.Toggled += delegate(bool v)
		{
			onChange(v);
			if (read != null)
			{
				t.IsOn = read();
			}
		};
		if (read != null)
		{
			SSettingBinding.Attach(t, delegate
			{
				t.IsOn = read();
			});
		}
		return Row(title, description, t, leading, isRightAlignedAction: false, settingId);
	}

	public static Grid SegmentedRow(string title, string description, string[] labels, string[] keys, string selected, Action<string> onChange, string settingId = null, Func<string> read = null)
	{
		SSegmented seg = new SSegmented(labels, keys, selected, title);
		seg.Changed += delegate(string k)
		{
			onChange(k);
			if (read != null)
			{
				seg.Selected = read();
			}
		};
		if (read != null)
		{
			SSettingBinding.Attach(seg, delegate
			{
				seg.Selected = read();
			});
		}
		return Row(title, description, seg, null, isRightAlignedAction: false, settingId);
	}

	public static Grid DropdownRow(string title, string description, IList<KeyValuePair<string, string>> items, string selected, Action<string> onChange, double width = 240.0, string settingId = null, Func<string> read = null)
	{
		SDropdown dd = new SDropdown(items, selected, width, title);
		dd.Changed += delegate(string k)
		{
			onChange(k);
			if (read != null)
			{
				string text2 = read();
				if (text2 != null)
				{
					dd.Selected = text2;
				}
			}
		};
		if (read != null)
		{
			SSettingBinding.Attach(dd, delegate
			{
				string text = read();
				if (text != null)
				{
					dd.Selected = text;
				}
			});
		}
		return Row(title, description, dd, null, isRightAlignedAction: false, settingId);
	}

	public static Grid SliderRow(string title, string description, double min, double max, double step, double value, Func<double, string> format, Action<double> onChange, double width = 180.0, string settingId = null, Func<double> read = null)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		StackPanel stackPanel2 = stackPanel;
		TextBlock val = SettingsTheme.Text(format(value), 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		val.TextWrapping = TextWrapping.NoWrap;
		val.MinWidth = 52.0;
		val.TextAlignment = TextAlignment.Right;
		val.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		stackPanel2.Children.Add(val);
		SSlider sl = new SSlider(min, max, step, value, width, title);
		sl.ValueUpdated += delegate(double v)
		{
			val.Text = format(v);
		};
		sl.ValueChanged += delegate(double v)
		{
			onChange(v);
			if (read != null)
			{
				sl.Value = read();
			}
		};
		if (read != null)
		{
			SSettingBinding.Attach(sl, delegate
			{
				sl.Value = read();
			});
		}
		stackPanel2.Children.Add(sl);
		return Row(title, description, stackPanel2, null, isRightAlignedAction: false, settingId);
	}

	public static List<KeyValuePair<string, string>> Items(params string[] keyLabelPairs)
	{
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		for (int i = 0; i + 1 < keyLabelPairs.Length; i += 2)
		{
			list.Add(new KeyValuePair<string, string>(keyLabelPairs[i], keyLabelPairs[i + 1]));
		}
		return list;
	}

	public static void SetRowEnabled(Grid row, bool enabled)
	{
		row.IsEnabled = enabled;
		row.Opacity = 1.0;
		foreach (StackPanel item in from p in row.Children.OfType<StackPanel>()
			where Grid.GetColumn(p) == 0 && Grid.GetRow(p) == 0
			select p)
		{
			item.Opacity = (enabled ? 1.0 : 0.4);
		}
	}

	public static Border Badge(string text)
	{
		TextBlock textBlock = SettingsTheme.Text(text, 12.0, SettingsTheme.TextSecondary, AynType.Medium);
		textBlock.TextWrapping = TextWrapping.Wrap;
		Border border = new Border();
		border.Background = SettingsTheme.ControlBg;
		border.CornerRadius = new CornerRadius(4.0);
		border.Padding = new Thickness(8.0, 4.0, 8.0, 4.0);
		border.Child = textBlock;
		border.VerticalAlignment = VerticalAlignment.Center;
		return border;
	}

	public static TextBlock Note(string text)
	{
		TextBlock textBlock = SettingsTheme.Text(text, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		return textBlock;
	}

	public static WrapPanel ButtonBar(params UIElement[] buttons)
	{
		WrapPanel wrapPanel = new WrapPanel();
		wrapPanel.Margin = new Thickness(16.0, 12.0, 16.0, 12.0);
		WrapPanel wrapPanel2 = wrapPanel;
		foreach (UIElement uIElement in buttons)
		{
			if (uIElement is FrameworkElement frameworkElement)
			{
				frameworkElement.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			}
			wrapPanel2.Children.Add(uIElement);
		}
		return wrapPanel2;
	}

	public static bool Confirm(Window owner, string title, string message, string confirmText, bool danger)
	{
		Window dlg = new Window
		{
			Owner = owner,
			WindowStyle = WindowStyle.None,
			ResizeMode = ResizeMode.NoResize,
			AllowsTransparency = true,
			Background = Brushes.Transparent,
			ShowInTaskbar = false,
			SizeToContent = SizeToContent.WidthAndHeight,
			WindowStartupLocation = ((owner == null) ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner),
			Title = title
		};
		SettingsTheme.PrepareRoot(dlg);
		bool result = false;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = 380.0;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(SettingsTheme.Text(title, 15.0, SettingsTheme.TextPrimary, AynType.Medium));
		TextBlock textBlock = SettingsTheme.Text(message, 15.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 20.0);
		stackPanel2.Children.Add(textBlock);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.HorizontalAlignment = HorizontalAlignment.Right;
		StackPanel stackPanel4 = stackPanel3;
		SButton cancel = new SButton("Cancel");
		cancel.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		cancel.Click += delegate
		{
			dlg.Close();
		};
		SButton sButton = new SButton(confirmText, (!danger) ? SButtonKind.Primary : SButtonKind.Danger);
		sButton.Click += delegate
		{
			result = true;
			dlg.Close();
		};
		stackPanel4.Children.Add(cancel);
		stackPanel4.Children.Add(sButton);
		stackPanel2.Children.Add(stackPanel4);
		dlg.Content = new Border
		{
			Background = SettingsTheme.PopupBg,
			BorderBrush = SettingsTheme.Hairline,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(24.0, 20.0, 24.0, 20.0),
			Margin = new Thickness(16.0),
			Child = stackPanel2,
			Effect = SettingsTheme.SoftShadow(24.0, 6.0, 0.5)
		};
		dlg.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				dlg.Close();
				e.Handled = true;
			}
		};
		dlg.Loaded += delegate
		{
			cancel.Focus();
		};
		dlg.ShowDialog();
		return result;
	}

	public static bool ReviewDiagnostics(Window owner, string report)
	{
		bool result = false;
		Window window = BuildDiagnosticsReview(owner, report, delegate(bool send)
		{
			result = send;
		});
		window.ShowDialog();
		return result;
	}

	internal static Window BuildDiagnosticsReview(Window owner, string report, Action<bool> done)
	{
		Window dlg = new Window
		{
			Owner = owner,
			WindowStyle = WindowStyle.None,
			ResizeMode = ResizeMode.NoResize,
			AllowsTransparency = true,
			Background = Brushes.Transparent,
			ShowInTaskbar = false,
			SizeToContent = SizeToContent.WidthAndHeight,
			WindowStartupLocation = ((owner == null) ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner),
			Title = "Send diagnostics"
		};
		SettingsTheme.PrepareRoot(dlg);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = 460.0;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(SettingsTheme.Text("Send diagnostics", 15.0, SettingsTheme.TextPrimary, AynType.Medium));
		TextBlock textBlock = SettingsTheme.Text("This is the full report. It has no files, names or content. Send opens your email app with it; nothing is sent until you send that email.", 15.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 12.0);
		stackPanel2.Children.Add(textBlock);
		TextBox textBox = new TextBox();
		textBox.Text = report ?? "";
		textBox.IsReadOnly = true;
		textBox.IsReadOnlyCaretVisible = false;
		textBox.TextWrapping = TextWrapping.Wrap;
		textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		textBox.Height = 220.0;
		textBox.FontFamily = new FontFamily("Consolas");
		textBox.FontSize = 11.5;
		textBox.Foreground = SettingsTheme.TextPrimary;
		textBox.Background = SettingsTheme.Hairline;
		textBox.BorderThickness = new Thickness(0.0);
		textBox.Padding = new Thickness(8.0);
		TextBox element = textBox;
		AutomationProperties.SetName(element, "Diagnostics report");
		stackPanel2.Children.Add(element);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel3.Margin = new Thickness(0.0, 16.0, 0.0, 0.0);
		StackPanel stackPanel4 = stackPanel3;
		SButton cancel = new SButton("Cancel");
		cancel.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		cancel.Click += delegate
		{
			done(obj: false);
			dlg.Close();
		};
		SButton sButton = new SButton("Send", SButtonKind.Primary);
		sButton.Click += delegate
		{
			done(obj: true);
			dlg.Close();
		};
		stackPanel4.Children.Add(cancel);
		stackPanel4.Children.Add(sButton);
		stackPanel2.Children.Add(stackPanel4);
		dlg.Content = new Border
		{
			Background = SettingsTheme.PopupBg,
			BorderBrush = SettingsTheme.Hairline,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(24.0, 20.0, 24.0, 20.0),
			Margin = new Thickness(16.0),
			Child = stackPanel2,
			Effect = SettingsTheme.SoftShadow(24.0, 6.0, 0.5)
		};
		dlg.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				done(obj: false);
				dlg.Close();
				e.Handled = true;
			}
		};
		dlg.Loaded += delegate
		{
			cancel.Focus();
		};
		LastDiagnosticsReview = dlg;
		return dlg;
	}
}
