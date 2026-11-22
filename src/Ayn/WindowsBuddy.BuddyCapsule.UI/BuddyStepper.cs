using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public sealed class BuddyStepper : Border
{
	private readonly Func<int> _min;

	private readonly Func<int> _max;

	private readonly Action<int> _changed;

	private readonly TextBlock _valueText;

	private readonly TextBox _editor;

	private readonly string _label;

	private readonly string _suffix;

	private int _value;

	public int StepSize { get; set; }

	public int Value
	{
		get
		{
			return _value;
		}
		set
		{
			_value = value;
			Paint();
		}
	}

	public bool IsEditable
	{
		get
		{
			return base.IsEnabled;
		}
		set
		{
			base.IsEnabled = value;
		}
	}

	public BuddyStepper(string label, int min, int max, string suffix, Action<int> changed)
		: this(label, () => min, () => max, suffix, changed)
	{
	}

	public BuddyStepper(string label, Func<int> min, Func<int> max, string suffix, Action<int> changed)
	{
		_label = label;
		_suffix = suffix;
		_min = min;
		_max = max;
		_changed = changed;
		StepSize = 1;
		base.Height = 28.0;
		base.Width = 148.0;
		base.CornerRadius = new CornerRadius(14.0);
		base.Background = ThemeTokens.Surface2;
		base.BorderThickness = new Thickness(0.0);
		base.Focusable = true;
		base.FocusVisualStyle = null;
		DependencyPropertyChangedEventHandler value = delegate
		{
			base.Opacity = (base.IsEnabled ? 1.0 : 0.45);
		};
		base.IsEnabledChanged += value;
		AutomationProperties.SetName(this, label);
		Grid grid = (Grid)(Child = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(26.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = new GridLength(26.0)
				}
			}
		});
		RepeatButton element = StepButton("subtract", "Decrease " + label, -1);
		grid.Children.Add(element);
		RepeatButton element2 = StepButton("add", "Increase " + label, 1);
		Grid.SetColumn(element2, 2);
		grid.Children.Add(element2);
		_valueText = BuddyTheme.Text("", 11.0, ThemeTokens.Text, AynType.Medium);
		_valueText.HorizontalAlignment = HorizontalAlignment.Center;
		_valueText.VerticalAlignment = VerticalAlignment.Center;
		Typography.SetNumeralAlignment(_valueText, FontNumeralAlignment.Tabular);
		Grid.SetColumn(_valueText, 1);
		grid.Children.Add(_valueText);
		_editor = new TextBox
		{
			Visibility = Visibility.Collapsed,
			BorderThickness = new Thickness(0.0),
			Background = Brushes.Transparent,
			Foreground = ThemeTokens.Text,
			CaretBrush = ThemeTokens.Text,
			FontFamily = BuddyTheme.Font,
			FontSize = 11.0,
			VerticalContentAlignment = VerticalAlignment.Center,
			HorizontalContentAlignment = HorizontalAlignment.Center,
			MaxLength = 5
		};
		Grid.SetColumn(_editor, 1);
		grid.Children.Add(_editor);
		_valueText.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			_editor.Text = _value.ToString(CultureInfo.InvariantCulture);
			_editor.Visibility = Visibility.Visible;
			_valueText.Visibility = Visibility.Hidden;
			_editor.Focus();
			_editor.SelectAll();
		};
		_editor.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Escape)
			{
				e.Handled = true;
				EndEdit(e.Key == Key.Return);
				Focus();
			}
		};
		_editor.LostKeyboardFocus += delegate
		{
			EndEdit(commit: true);
		};
		base.MouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			e.Handled = true;
			Step((e.Delta > 0) ? 1 : (-1));
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (!(e.OriginalSource is TextBox) && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Add || e.Key == Key.Subtract || e.Key == Key.OemPlus || e.Key == Key.OemMinus))
			{
				e.Handled = true;
				Step((e.Key == Key.Up || e.Key == Key.Right || e.Key == Key.Add || e.Key == Key.OemPlus) ? 1 : (-1));
			}
		};
		base.GotKeyboardFocus += delegate
		{
			base.Background = ThemeTokens.Selection;
		};
		base.LostKeyboardFocus += delegate
		{
			base.Background = ThemeTokens.Surface2;
		};
	}

	private RepeatButton StepButton(string icon, string name, int direction)
	{
		RepeatButton b = new RepeatButton
		{
			Content = FluentIcons.Create(icon, 12.0, ThemeTokens.Text, filled: false),
			Background = Brushes.Transparent,
			Width = 26.0,
			Height = 26.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			BorderThickness = new Thickness(0.0),
			Cursor = Cursors.Hand,
			Focusable = false,
			IsTabStop = false,
			Padding = new Thickness(0.0),
			ToolTip = BuddyTooltip.Create(name),
			Delay = 400,
			Interval = 80,
			ClickMode = ClickMode.Press
		};
		ControlTemplate controlTemplate = new ControlTemplate(typeof(RepeatButton));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.Name = "HitSurface";
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(13.0));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(UIElement.IsHitTestVisibleProperty, false);
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		Trigger trigger = new Trigger();
		trigger.Property = UIElement.IsMouseOverProperty;
		trigger.Value = true;
		Trigger trigger2 = trigger;
		trigger2.Setters.Add(new Setter(Border.BackgroundProperty, ThemeTokens.Ref(ThemeTokens.Selection), "HitSurface"));
		controlTemplate.Triggers.Add(trigger2);
		Trigger trigger3 = new Trigger();
		trigger3.Property = ButtonBase.IsPressedProperty;
		trigger3.Value = true;
		Trigger trigger4 = trigger3;
		trigger4.Setters.Add(new Setter(Border.BackgroundProperty, ThemeTokens.Ref(ThemeTokens.ControlFillHover), "HitSurface"));
		controlTemplate.Triggers.Add(trigger4);
		b.Template = controlTemplate;
		b.PreviewMouseLeftButtonDown += delegate
		{
			Focus();
		};
		b.Unloaded += delegate
		{
			if (b.IsMouseCaptured)
			{
				b.ReleaseMouseCapture();
			}
		};
		b.IsVisibleChanged += delegate
		{
			if (!b.IsVisible && b.IsMouseCaptured)
			{
				b.ReleaseMouseCapture();
			}
		};
		AutomationProperties.SetName(b, name);
		b.Click += delegate(object s, RoutedEventArgs e)
		{
			e.Handled = true;
			Step(direction);
		};
		return b;
	}

	private void EndEdit(bool commit)
	{
		if (_editor.Visibility == Visibility.Visible)
		{
			_editor.Visibility = Visibility.Collapsed;
			_valueText.Visibility = Visibility.Visible;
			if (commit && int.TryParse(_editor.Text, out var result))
			{
				Apply(result);
			}
		}
	}

	public void Step(int direction)
	{
		if (base.IsEnabled)
		{
			Apply(_value + Math.Sign(direction) * StepSize);
		}
	}

	private void Apply(int n)
	{
		n = Math.Max(_min(), Math.Min(_max(), n));
		if (n != _value)
		{
			Value = n;
			if (_changed != null)
			{
				_changed(n);
			}
		}
	}

	private void Paint()
	{
		_valueText.Text = ((_label.Length == 0) ? "" : (_label + " ")) + _value.ToString(CultureInfo.InvariantCulture) + _suffix;
	}
}
