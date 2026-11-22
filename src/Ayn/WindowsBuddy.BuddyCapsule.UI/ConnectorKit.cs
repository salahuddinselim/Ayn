using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI;

public static class ConnectorKit
{
	public sealed class Step
	{
		public string Text;

		public string ActionText;

		public string Url;

		public Step(string text, string actionText = null, string url = null)
		{
			Text = text;
			ActionText = actionText;
			Url = url;
		}
	}

	public sealed class Spinner : FrameworkElement
	{
		private readonly RotateTransform _rot = new RotateTransform();

		private readonly Brush _ink;

		public Spinner(double size, Brush ink)
		{
			base.Width = size;
			base.Height = size;
			_ink = ink;
			base.IsHitTestVisible = false;
			base.RenderTransformOrigin = new Point(0.5, 0.5);
			base.RenderTransform = _rot;
			DependencyPropertyChangedEventHandler value = delegate
			{
				Run(base.IsVisible);
			};
			base.IsVisibleChanged += value;
		}

		private void Run(bool on)
		{
			if (on && CapsuleMotion.AnimationsEnabled)
			{
				_rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0.0, 360.0, new Duration(TimeSpan.FromMilliseconds(800.0)))
				{
					RepeatBehavior = RepeatBehavior.Forever
				});
			}
			else
			{
				_rot.BeginAnimation(RotateTransform.AngleProperty, null);
			}
		}

		protected override void OnRender(DrawingContext dc)
		{
			double num = Math.Min(base.ActualWidth, base.ActualHeight) / 2.0 - 1.0;
			Point center = new Point(base.ActualWidth / 2.0, base.ActualHeight / 2.0);
			Pen pen = new Pen(_ink, 1.8);
			pen.StartLineCap = PenLineCap.Round;
			pen.EndLineCap = PenLineCap.Round;
			Pen pen2 = pen;
			Pen pen3 = new Pen(_ink, 1.8);
			dc.PushOpacity(0.22);
			dc.DrawEllipse(null, pen3, center, num, num);
			dc.Pop();
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(new Point(center.X, center.Y - num), isFilled: false, isClosed: false);
				streamGeometryContext.ArcTo(new Point(center.X + num, center.Y), new Size(num, num), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
			}
			dc.DrawGeometry(null, pen2, streamGeometry);
		}
	}

	public sealed class BusyButton : Border
	{
		private readonly TextBlock _t;

		private readonly Spinner _spin;

		private readonly string _label;

		private bool _busy;

		private bool _enabled = true;

		public string Label => _t.Text;

		public bool IsBusy => _busy;

		public bool Enabled
		{
			get
			{
				return _enabled;
			}
			set
			{
				_enabled = value;
				Paint();
			}
		}

		public event Action Click;

		public BusyButton(string label, double height = 34.0)
		{
			_label = label;
			base.Height = height;
			base.MinWidth = 96.0;
			base.Padding = new Thickness(18.0, 0.0, 18.0, 0.0);
			base.CornerRadius = new CornerRadius(height / 2.0);
			base.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			_spin = new Spinner(13.0, BuddyTheme.PrimaryInk)
			{
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				Visibility = Visibility.Collapsed,
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(_spin);
			_t = Text(label, 13.0, BuddyTheme.PrimaryInk, AynType.Semibold);
			_t.VerticalAlignment = VerticalAlignment.Center;
			stackPanel.Children.Add(_t);
			Child = stackPanel;
			base.Focusable = true;
			base.FocusVisualStyle = null;
			KeyboardNavigation.SetIsTabStop(this, isTabStop: true);
			MouseEventHandler value = delegate
			{
				Paint();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Paint();
			};
			base.GotKeyboardFocus += delegate
			{
				Paint();
			};
			base.LostKeyboardFocus += delegate
			{
				Paint();
			};
			AttachClick(this, Fire);
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.Space)
				{
					e.Handled = true;
					Fire();
				}
			};
			Paint();
		}

		public void ShowResult(string text)
		{
			_t.Text = text;
		}

		public void SetBusy(bool busy, string busyLabel = null)
		{
			_busy = busy;
			_t.Text = ((busy && busyLabel != null) ? busyLabel : _label);
			_spin.Visibility = ((!busy) ? Visibility.Collapsed : Visibility.Visible);
			Paint();
		}

		public void Fire()
		{
			if (!_busy && _enabled)
			{
				this.Click?.Invoke();
			}
		}

		private void Paint()
		{
			bool flag = _enabled && !_busy;
			base.Background = (_enabled ? BuddyTheme.PrimaryFill : ThemeTokens.Surface3);
			_t.Foreground = (_enabled ? BuddyTheme.PrimaryInk : ThemeTokens.TextDisabled);
			base.Opacity = ((flag && (base.IsMouseOver || base.IsKeyboardFocused)) ? 0.88 : 1.0);
			base.Cursor = (flag ? Cursors.Hand : Cursors.Arrow);
		}
	}

	public sealed class SoftField : StackPanel
	{
		private readonly Border _box;

		private readonly TextBox _text;

		private readonly PasswordBox _pass;

		private readonly TextBlock _placeholder;

		private readonly TextBlock _below;

		private readonly string _hint;

		private readonly bool _secret;

		private bool _revealed;

		private bool _syncing;

		private bool _error;

		private readonly ContentControl _eyeHost;

		public static Func<string> ClipboardReader = delegate
		{
			try
			{
				return Clipboard.ContainsText() ? Clipboard.GetText() : null;
			}
			catch
			{
				return (string)null;
			}
		};

		public string Value
		{
			get
			{
				return (_text.Text ?? "").Trim();
			}
			set
			{
				_text.Text = value ?? "";
				_placeholder.Visibility = ((!string.IsNullOrEmpty(_text.Text)) ? Visibility.Collapsed : Visibility.Visible);
			}
		}

		public bool Revealed => _revealed;

		public bool HasError => _error;

		public string BelowText => _below.Text;

		public string Placeholder => _placeholder.Text;

		public bool PlaceholderVisible => _placeholder.Visibility == Visibility.Visible;

		public event Action Changed;

		public event Action Submitted;

		public SoftField(string label, string placeholder, string hint = null, bool secret = false, bool paste = false)
		{
			_hint = hint;
			_secret = secret;
			TextBlock textBlock = Text(label, 11.5, ThemeTokens.Text2, AynType.Medium);
			textBlock.Margin = new Thickness(2.0, 0.0, 0.0, 6.0);
			base.Children.Add(textBlock);
			Grid grid = new Grid
			{
				ColumnDefinitions = 
				{
					new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					},
					new ColumnDefinition
					{
						Width = GridLength.Auto
					}
				}
			};
			Grid grid2 = new Grid
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			_placeholder = Text(placeholder, 13.0, ThemeTokens.Text3, AynType.Regular);
			_placeholder.VerticalAlignment = VerticalAlignment.Center;
			_placeholder.IsHitTestVisible = false;
			grid2.Children.Add(_placeholder);
			_text = CleanTextBox();
			grid2.Children.Add(_text);
			if (secret)
			{
				_pass = CleanPasswordBox();
				grid2.Children.Add(_pass);
				_text.Visibility = Visibility.Collapsed;
				_pass.PasswordChanged += delegate
				{
					if (!_syncing)
					{
						_syncing = true;
						_text.Text = _pass.Password;
						_syncing = false;
						OnChanged();
					}
				};
				_pass.GotKeyboardFocus += delegate
				{
					Paint();
				};
				_pass.LostKeyboardFocus += delegate
				{
					Paint();
				};
				_pass.KeyDown += OnKey;
			}
			_text.TextChanged += delegate
			{
				if (!_syncing)
				{
					if (_pass != null)
					{
						_syncing = true;
						_pass.Password = _text.Text;
						_syncing = false;
					}
					OnChanged();
				}
			};
			_text.GotKeyboardFocus += delegate
			{
				Paint();
			};
			_text.LostKeyboardFocus += delegate
			{
				Paint();
			};
			_text.KeyDown += OnKey;
			grid.Children.Add(grid2);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(6.0, 0.0, -6.0, 0.0)
			};
			if (paste)
			{
				Border border = SoftButton("Paste", DoPaste, 24.0, "clipboard");
				border.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
				border.ToolTip = BuddyTooltip.Create("Paste from the clipboard");
				stackPanel.Children.Add(border);
			}
			if (secret)
			{
				_eyeHost = new ContentControl
				{
					Focusable = false,
					IsHitTestVisible = false,
					Content = Eye(open: true, 15.0, ThemeTokens.Text2)
				};
				Border element = IconButton(_eyeHost, "Show key", Toggle);
				stackPanel.Children.Add(element);
			}
			Grid.SetColumn(stackPanel, 1);
			grid.Children.Add(stackPanel);
			_box = new Border
			{
				Height = 38.0,
				CornerRadius = new CornerRadius(11.0),
				Padding = new Thickness(12.0, 0.0, 10.0, 0.0),
				Child = grid,
				Cursor = Cursors.IBeam
			};
			_box.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				if (!IsInsideButton(e.OriginalSource as DependencyObject))
				{
					FocusInput();
					e.Handled = true;
				}
			};
			base.Children.Add(_box);
			_below = Wrap(hint ?? "", 11.5, ThemeTokens.Text3, AynType.Regular);
			_below.Margin = new Thickness(2.0, 6.0, 0.0, 0.0);
			_below.Visibility = (string.IsNullOrEmpty(hint) ? Visibility.Collapsed : Visibility.Visible);
			base.Children.Add(_below);
			Paint();
		}

		private bool IsInsideButton(DependencyObject d)
		{
			while (d != null && d != _box)
			{
				if (d is Border border && border.Cursor == Cursors.Hand)
				{
					return true;
				}
				d = ((d is Visual) ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d));
			}
			return false;
		}

		private void OnKey(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				this.Submitted?.Invoke();
			}
		}

		private void OnChanged()
		{
			_placeholder.Visibility = ((!string.IsNullOrEmpty(_text.Text)) ? Visibility.Collapsed : Visibility.Visible);
			if (_error)
			{
				SetError(null);
			}
			this.Changed?.Invoke();
		}

		public void FocusInput()
		{
			UIElement uIElement = ((_secret && !_revealed) ? ((UIElement)_pass) : ((UIElement)_text));
			uIElement.Focus();
			System.Windows.Input.Keyboard.Focus(uIElement);
		}

		public void Toggle()
		{
			if (_secret)
			{
				_revealed = !_revealed;
				_text.Visibility = ((!_revealed) ? Visibility.Collapsed : Visibility.Visible);
				_pass.Visibility = (_revealed ? Visibility.Collapsed : Visibility.Visible);
				_eyeHost.Content = Eye(!_revealed, 15.0, ThemeTokens.Text2);
				if (_eyeHost.Parent is Border border)
				{
					border.ToolTip = BuddyTooltip.Create(_revealed ? "Hide key" : "Show key");
				}
			}
		}

		public void DoPaste()
		{
			string text = ClipboardReader();
			if (!string.IsNullOrEmpty(text))
			{
				Value = text.Trim();
				FocusInput();
			}
		}

		public void SetError(string message)
		{
			_error = !string.IsNullOrEmpty(message);
			_below.Text = (_error ? message : (_hint ?? ""));
			_below.Foreground = (_error ? ThemeTokens.Danger : ThemeTokens.Text3);
			_below.Visibility = (string.IsNullOrEmpty(_below.Text) ? Visibility.Collapsed : Visibility.Visible);
			Paint();
		}

		private void Paint()
		{
			bool flag = _text.IsKeyboardFocusWithin || (_pass != null && _pass.IsKeyboardFocusWithin);
			_box.Background = (_error ? ThemeTokens.DangerSoft : (flag ? FieldFocus : FieldFill));
		}
	}

	public enum NoticeKind
	{
		Error,
		Info,
		Offline
	}

	public sealed class Notice : Border
	{
		private readonly TextBlock _title;

		private readonly TextBlock _body;

		private readonly ContentControl _glyph;

		private readonly Border _action;

		private readonly TextBlock _actionText;

		private Action _onAction;

		public string Title => _title.Text;

		public string Body => _body.Text;

		public string ActionText
		{
			get
			{
				if (_action.Visibility != 0)
				{
					return null;
				}
				return _actionText.Text;
			}
		}

		public NoticeKind Kind { get; private set; }

		public event Action<bool> Toggled;

		public Notice()
		{
			base.CornerRadius = new CornerRadius(12.0);
			base.Padding = new Thickness(12.0, 10.0, 12.0, 10.0);
			Grid grid = new Grid
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
						Width = GridLength.Auto
					}
				}
			};
			_glyph = new ContentControl
			{
				Focusable = false,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
				HorizontalAlignment = HorizontalAlignment.Left
			};
			grid.Children.Add(_glyph);
			StackPanel stackPanel = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			_title = Wrap("", 13.0, ThemeTokens.Text, AynType.Semibold);
			_body = Wrap("", 11.5, ThemeTokens.Text2, AynType.Regular);
			_body.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			stackPanel.Children.Add(_title);
			stackPanel.Children.Add(_body);
			Grid.SetColumn(stackPanel, 1);
			grid.Children.Add(stackPanel);
			_actionText = Text("", 11.5, ThemeTokens.Text, AynType.Medium);
			_actionText.VerticalAlignment = VerticalAlignment.Center;
			_action = new Border
			{
				Height = 26.0,
				Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
				CornerRadius = new CornerRadius(13.0),
				Background = QuietHover,
				Cursor = Cursors.Hand,
				Child = _actionText,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
				Visibility = Visibility.Collapsed
			};
			Border action = _action;
			Action click = delegate
			{
				if (_onAction != null)
				{
					_onAction();
				}
			};
			AttachClick(action, click);
			Keyboard(_action, delegate
			{
				if (_onAction != null)
				{
					_onAction();
				}
			}, () => QuietHover, () => BuddyTint.Ink(42));
			stackPanel.Children.Add(_action);
			Child = grid;
			base.Visibility = Visibility.Collapsed;
		}

		public void Show(NoticeKind kind, string title, string body, string actionText = null, Action action = null)
		{
			Kind = kind;
			_title.Text = title ?? "";
			_body.Text = body ?? "";
			_body.Visibility = (string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible);
			base.Background = ((kind == NoticeKind.Error) ? ThemeTokens.DangerSoft : Quiet);
			_glyph.Content = FluentIcons.Create(kind switch
			{
				NoticeKind.Offline => "wifi_off", 
				NoticeKind.Error => "dismiss_circle", 
				_ => "checkmark_circle", 
			}, 15.0, (kind == NoticeKind.Error) ? ThemeTokens.Danger : ThemeTokens.Text2, filled: false);
			_onAction = action;
			_actionText.Text = actionText ?? "";
			_action.Visibility = ((actionText == null || action == null) ? Visibility.Collapsed : Visibility.Visible);
			base.Visibility = Visibility.Visible;
			this.Toggled?.Invoke(obj: true);
		}

		public void Hide()
		{
			if (base.Visibility != Visibility.Collapsed)
			{
				base.Visibility = Visibility.Collapsed;
				this.Toggled?.Invoke(obj: false);
			}
		}

		public void Act()
		{
			if (_onAction != null)
			{
				_onAction();
			}
		}
	}

	public sealed class StatTile : Border
	{
		private readonly TextBlock _label;

		private readonly TextBlock _value;

		private readonly TextBlock _sub;

		public string Value => _value.Text;

		public string Sub => _sub.Text;

		public StatTile(string label, double valueSize = 20.0)
		{
			base.Background = BuddyTint.Ink(10);
			base.CornerRadius = new CornerRadius(14.0);
			base.Padding = new Thickness(14.0, 11.0, 14.0, 11.0);
			StackPanel stackPanel = new StackPanel();
			_label = Text(label, 11.5, ThemeTokens.Text2, AynType.Medium);
			stackPanel.Children.Add(_label);
			_value = Text("", valueSize, ThemeTokens.Text, AynType.Semibold);
			_value.FontFamily = BuddyTheme.DisplayFont;
			_value.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
			_value.TextTrimming = TextTrimming.CharacterEllipsis;
			Typography.SetNumeralAlignment(_value, FontNumeralAlignment.Tabular);
			stackPanel.Children.Add(_value);
			_sub = Text("", 11.5, ThemeTokens.Text3, AynType.Regular);
			_sub.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
			_sub.Visibility = Visibility.Collapsed;
			Typography.SetNumeralAlignment(_sub, FontNumeralAlignment.Tabular);
			stackPanel.Children.Add(_sub);
			Child = stackPanel;
			Action<StatTile> action = delegate(StatTile x)
			{
				x.Background = (ThemeTokens.IsLight ? ThemeTokens.Surface : BuddyTint.Ink(10));
				x.Effect = (ThemeTokens.IsLight ? ThemeTokens.CardElevation : null);
			};
			action(this);
			ThemeTokens.Subscribe(this, action);
		}

		public void Set(string value, string sub = null, string tip = null)
		{
			_value.Text = value ?? "";
			_sub.Text = sub ?? "";
			_sub.Visibility = (string.IsNullOrEmpty(sub) ? Visibility.Collapsed : Visibility.Visible);
			base.ToolTip = (string.IsNullOrEmpty(tip) ? null : BuddyTooltip.Create(tip));
		}

		public void SetLoading()
		{
			_value.Text = "—";
			_sub.Visibility = Visibility.Collapsed;
		}
	}

	public sealed class ConfirmAction : StackPanel
	{
		private readonly Border _start;

		private readonly StackPanel _ask;

		public bool Armed => _ask.Visibility == Visibility.Visible;

		public ConfirmAction(string text, string question, string confirm, Action act)
		{
			ConfirmAction confirmAction = this;
			base.Orientation = Orientation.Horizontal;
			base.VerticalAlignment = VerticalAlignment.Center;
			_start = Link(text, Arm);
			base.Children.Add(_start);
			_ask = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Visibility = Visibility.Collapsed,
				VerticalAlignment = VerticalAlignment.Center
			};
			TextBlock textBlock = Text(question, 13.0, ThemeTokens.Text, AynType.Medium);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			textBlock.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
			_ask.Children.Add(textBlock);
			Border border = SoftButton("Cancel", Disarm, 26.0);
			border.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
			_ask.Children.Add(border);
			TextBlock textBlock2 = Text(confirm, 11.5, Brushes.White, AynType.Semibold);
			textBlock2.VerticalAlignment = VerticalAlignment.Center;
			Border d = new Border
			{
				Height = 26.0,
				Padding = new Thickness(12.0, 0.0, 12.0, 0.0),
				CornerRadius = new CornerRadius(13.0),
				Background = ThemeTokens.Danger,
				Cursor = Cursors.Hand,
				Child = textBlock2,
				VerticalAlignment = VerticalAlignment.Center
			};
			d.MouseEnter += delegate
			{
				d.Opacity = 0.88;
			};
			d.MouseLeave += delegate
			{
				d.Opacity = 1.0;
			};
			Border el = d;
			Action click = delegate
			{
				confirmAction.Disarm();
				act();
			};
			AttachClick(el, click);
			Keyboard(d, delegate
			{
				confirmAction.Disarm();
				act();
			}, () => ThemeTokens.Danger, () => ThemeTokens.Danger);
			_ask.Children.Add(d);
			base.Children.Add(_ask);
		}

		public void Arm()
		{
			_start.Visibility = Visibility.Collapsed;
			_ask.Visibility = Visibility.Visible;
		}

		public void Disarm()
		{
			_start.Visibility = Visibility.Visible;
			_ask.Visibility = Visibility.Collapsed;
		}
	}

	public sealed class ProviderOption
	{
		public string Id;

		public string Name;

		public string Letters;

		public Color Brand;

		public string WhatYouSee;

		public Func<FrameworkElement> Sample;
	}

	public const double Body = 13.0;

	public const double Meta = 11.5;

	public static Action<string> Browser = OpenUrl;

	public static Brush FieldFill => BuddyTint.Ink(16);

	public static Brush FieldFocus => BuddyTint.Ink(26);

	public static Brush Quiet => BuddyTint.Ink(18);

	public static Brush QuietHover => BuddyTint.Ink(30);

	public static Brush Hover => BuddyTint.Ink(12);

	public static TextBlock Text(string text, double size, Brush ink, FontWeight weight)
	{
		return BuddyTheme.Text(text, size, ink, weight);
	}

	public static TextBlock Wrap(string text, double size, Brush ink, FontWeight weight)
	{
		TextBlock textBlock = BuddyTheme.Text(text, size, ink, weight);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		return textBlock;
	}

	public static Border Card(UIElement child, Thickness padding)
	{
		Border border = new Border();
		border.Background = BuddyTheme.CardFill;
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = padding;
		border.Child = child;
		Border border2 = border;
		Action<Border> action = delegate(Border x)
		{
			x.Effect = (ThemeTokens.IsLight ? ThemeTokens.CardElevation : null);
		};
		action(border2);
		ThemeTokens.Subscribe(border2, action);
		return border2;
	}

	public static Border Monogram(string letters, Color brand, double size)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(brand);
		solidColorBrush.Freeze();
		double num = (0.299 * (double)(int)brand.R + 0.587 * (double)(int)brand.G + 0.114 * (double)(int)brand.B) / 255.0;
		SolidColorBrush ink = ((num > 0.66) ? Brushes.Black : Brushes.White);
		TextBlock textBlock = BuddyTheme.Text(letters, Math.Round(size * ((letters.Length > 1) ? 0.36 : 0.46)), ink, AynType.Semibold);
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.TextTrimming = TextTrimming.None;
		Border border = new Border();
		border.Width = size;
		border.Height = size;
		border.CornerRadius = new CornerRadius(size * 0.3);
		border.Background = solidColorBrush;
		border.Child = textBlock;
		border.VerticalAlignment = VerticalAlignment.Center;
		return border;
	}

	public static Border SampleChip()
	{
		TextBlock textBlock = Text("Sample", 10.0, ThemeTokens.Text2, AynType.Semibold);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Border border = new Border();
		border.Background = Quiet;
		border.CornerRadius = new CornerRadius(8.0);
		border.Padding = new Thickness(7.0, 2.0, 7.0, 2.0);
		border.Child = textBlock;
		border.VerticalAlignment = VerticalAlignment.Center;
		return border;
	}

	public static void AttachClick(FrameworkElement el, Action click)
	{
		bool pressed = false;
		el.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			pressed = true;
			e.Handled = true;
		};
		el.MouseLeave += delegate
		{
			pressed = false;
		};
		el.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (pressed)
			{
				pressed = false;
				e.Handled = true;
				if (click != null)
				{
					click();
				}
			}
		};
	}

	public static void Keyboard(Border b, Action click, Func<Brush> rest, Func<Brush> focused)
	{
		b.Focusable = true;
		b.FocusVisualStyle = null;
		KeyboardNavigation.SetIsTabStop(b, isTabStop: true);
		b.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				if (click != null)
				{
					click();
				}
			}
		};
		b.GotKeyboardFocus += delegate
		{
			b.Background = focused();
		};
		b.LostKeyboardFocus += delegate
		{
			b.Background = rest();
		};
	}

	public static Border SoftButton(string text, Action click, double height = 30.0, string icon = null)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		if (icon != null)
		{
			FrameworkElement frameworkElement = FluentIcons.Create(icon, 13.0, ThemeTokens.Text, filled: false);
			frameworkElement.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			stackPanel2.Children.Add(frameworkElement);
		}
		TextBlock textBlock = Text(text, (height <= 26.0) ? 11.5 : 13.0, ThemeTokens.Text, AynType.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(textBlock);
		Border b = new Border
		{
			Height = height,
			Padding = new Thickness((height <= 26.0) ? 10 : 14, 0.0, (height <= 26.0) ? 10 : 14, 0.0),
			CornerRadius = new CornerRadius(height / 2.0),
			Background = Quiet,
			Cursor = Cursors.Hand,
			Child = stackPanel2,
			VerticalAlignment = VerticalAlignment.Center
		};
		b.MouseEnter += delegate
		{
			b.Background = QuietHover;
		};
		b.MouseLeave += delegate
		{
			if (!b.IsKeyboardFocused)
			{
				b.Background = Quiet;
			}
		};
		AttachClick(b, click);
		Keyboard(b, click, () => Quiet, () => QuietHover);
		return b;
	}

	public static Border Link(string text, Action click, bool external = false, Brush ink = null)
	{
		Brush rest = ink ?? ThemeTokens.Text2;
		TextBlock t = Text(text + (external ? " ↗" : ""), 13.0, rest, AynType.Medium);
		t.VerticalAlignment = VerticalAlignment.Center;
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Padding = new Thickness(2.0, 4.0, 2.0, 4.0);
		border.Child = t;
		border.VerticalAlignment = VerticalAlignment.Center;
		Border border2 = border;
		border2.MouseEnter += delegate
		{
			t.Foreground = ThemeTokens.Text;
		};
		border2.MouseLeave += delegate
		{
			t.Foreground = rest;
		};
		AttachClick(border2, click);
		Keyboard(border2, click, () => Brushes.Transparent, () => Hover);
		return border2;
	}

	public static void OpenUrl(string url)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(url);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch
		{
		}
	}

	public static FrameworkElement Steps(params Step[] steps)
	{
		StackPanel stackPanel = new StackPanel();
		for (int i = 0; i < steps.Length; i++)
		{
			Step step = steps[i];
			Grid grid = new Grid();
			grid.Margin = new Thickness(0.0, (i != 0) ? 10 : 0, 0.0, 0.0);
			grid.MinHeight = 24.0;
			Grid grid2 = grid;
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(30.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			TextBlock textBlock = Text((i + 1).ToString(CultureInfo.InvariantCulture), 11.5, ThemeTokens.Text, AynType.Semibold);
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			Border border = new Border();
			border.Width = 22.0;
			border.Height = 22.0;
			border.CornerRadius = new CornerRadius(11.0);
			border.Background = Quiet;
			border.Child = textBlock;
			border.HorizontalAlignment = HorizontalAlignment.Left;
			border.VerticalAlignment = VerticalAlignment.Center;
			Border element = border;
			grid2.Children.Add(element);
			TextBlock textBlock2 = Wrap(step.Text, 13.0, ThemeTokens.Text, AynType.Regular);
			textBlock2.VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(textBlock2, 1);
			grid2.Children.Add(textBlock2);
			if (step.ActionText != null && step.Url != null)
			{
				string url = step.Url;
				Border border2 = SoftButton(step.ActionText + " ↗", delegate
				{
					Browser(url);
				}, 26.0);
				border2.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
				border2.ToolTip = BuddyTooltip.Create(url);
				Grid.SetColumn(border2, 2);
				grid2.Children.Add(border2);
			}
			stackPanel.Children.Add(grid2);
		}
		return stackPanel;
	}

	private static ControlTemplate BareTemplate(Type t)
	{
		ControlTemplate controlTemplate = new ControlTemplate(t);
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ScrollViewer));
		frameworkElementFactory2.Name = "PART_ContentHost";
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory2.SetValue(UIElement.FocusableProperty, false);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		return controlTemplate;
	}

	public static TextBox CleanTextBox()
	{
		TextBox textBox = new TextBox();
		textBox.FontSize = 13.0;
		textBox.FontFamily = BuddyTheme.Font;
		textBox.Foreground = ThemeTokens.Text;
		textBox.Background = Brushes.Transparent;
		textBox.BorderThickness = new Thickness(0.0);
		textBox.CaretBrush = ThemeTokens.Text;
		textBox.SelectionBrush = ThemeTokens.Accent;
		textBox.VerticalAlignment = VerticalAlignment.Center;
		textBox.VerticalContentAlignment = VerticalAlignment.Center;
		textBox.Padding = new Thickness(0.0);
		textBox.FocusVisualStyle = null;
		TextBox textBox2 = textBox;
		textBox2.Template = BareTemplate(typeof(TextBox));
		BuddyTheme.ApplyTextMode(textBox2, 13.0);
		return textBox2;
	}

	public static PasswordBox CleanPasswordBox()
	{
		PasswordBox passwordBox = new PasswordBox();
		passwordBox.FontSize = 13.0;
		passwordBox.FontFamily = BuddyTheme.Font;
		passwordBox.Foreground = ThemeTokens.Text;
		passwordBox.Background = Brushes.Transparent;
		passwordBox.BorderThickness = new Thickness(0.0);
		passwordBox.CaretBrush = ThemeTokens.Text;
		passwordBox.SelectionBrush = ThemeTokens.Accent;
		passwordBox.VerticalAlignment = VerticalAlignment.Center;
		passwordBox.VerticalContentAlignment = VerticalAlignment.Center;
		passwordBox.Padding = new Thickness(0.0);
		passwordBox.FocusVisualStyle = null;
		PasswordBox passwordBox2 = passwordBox;
		passwordBox2.Template = BareTemplate(typeof(PasswordBox));
		BuddyTheme.ApplyTextMode(passwordBox2, 13.0);
		return passwordBox2;
	}

	public static FrameworkElement Eye(bool open, double size, Brush ink)
	{
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		canvas.IsHitTestVisible = false;
		Canvas canvas2 = canvas;
		double num = size / 16.0;
		Path path = new Path();
		path.Data = Geometry.Parse("M1.5,8 C3.2,4.6 5.4,3 8,3 C10.6,3 12.8,4.6 14.5,8 C12.8,11.4 10.6,13 8,13 C5.4,13 3.2,11.4 1.5,8 Z");
		path.Stroke = ink;
		path.StrokeThickness = 1.3;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.RenderTransform = new ScaleTransform(num, num);
		Path element = path;
		canvas2.Children.Add(element);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 4.6 * num;
		ellipse.Height = 4.6 * num;
		ellipse.Stroke = ink;
		ellipse.StrokeThickness = 1.3;
		Ellipse element2 = ellipse;
		Canvas.SetLeft(element2, 5.7 * num);
		Canvas.SetTop(element2, 5.7 * num);
		canvas2.Children.Add(element2);
		if (!open)
		{
			canvas2.Children.Add(new Line
			{
				X1 = 2.5 * num,
				Y1 = 13.5 * num,
				X2 = 13.5 * num,
				Y2 = 2.5 * num,
				Stroke = ink,
				StrokeThickness = 1.4,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round
			});
		}
		return canvas2;
	}

	public static Border IconButton(FrameworkElement glyph, string tip, Action click)
	{
		glyph.HorizontalAlignment = HorizontalAlignment.Center;
		glyph.VerticalAlignment = VerticalAlignment.Center;
		Border b = new Border
		{
			Width = 26.0,
			Height = 26.0,
			CornerRadius = new CornerRadius(13.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = glyph,
			VerticalAlignment = VerticalAlignment.Center,
			ToolTip = BuddyTooltip.Create(tip)
		};
		b.MouseEnter += delegate
		{
			b.Background = QuietHover;
		};
		b.MouseLeave += delegate
		{
			b.Background = Brushes.Transparent;
		};
		AttachClick(b, click);
		Keyboard(b, click, () => Brushes.Transparent, () => QuietHover);
		return b;
	}

	public static FrameworkElement Disclosure(string title, UIElement content, bool open = false)
	{
		StackPanel stackPanel = new StackPanel();
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		stackPanel2.Background = Brushes.Transparent;
		stackPanel2.Cursor = Cursors.Hand;
		stackPanel2.HorizontalAlignment = HorizontalAlignment.Left;
		StackPanel stackPanel3 = stackPanel2;
		FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 12.0, ThemeTokens.Text2, filled: false);
		frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
		RotateTransform rot = new RotateTransform((!open) ? (-90) : 0);
		frameworkElement.RenderTransform = rot;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		stackPanel3.Children.Add(frameworkElement);
		TextBlock textBlock = Text(title, 11.5, ThemeTokens.Text2, AynType.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		stackPanel3.Children.Add(textBlock);
		stackPanel.Children.Add(stackPanel3);
		ContentControl body = new ContentControl
		{
			Content = content,
			Focusable = false,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			Visibility = ((!open) ? Visibility.Collapsed : Visibility.Visible)
		};
		stackPanel.Children.Add(body);
		Action action = delegate
		{
			bool flag = body.Visibility != Visibility.Visible;
			body.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			rot.Angle = ((!flag) ? (-90) : 0);
		};
		AttachClick(stackPanel3, action);
		stackPanel.Tag = action;
		return stackPanel;
	}

	public static FrameworkElement Picker(string title, string subtitle, IList<ProviderOption> options, Action<string> choose, Action cancel)
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(24.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(318.0)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		Grid grid2 = new Grid();
		grid2.Children.Add(Text(title, 13.5, ThemeTokens.Text, AynType.Semibold));
		if (cancel != null)
		{
			Border border = Link("Cancel", cancel);
			border.HorizontalAlignment = HorizontalAlignment.Right;
			grid2.Children.Add(border);
		}
		stackPanel2.Children.Add(grid2);
		TextBlock textBlock = Wrap(subtitle, 11.5, ThemeTokens.Text2, AynType.Regular);
		textBlock.Margin = new Thickness(0.0, 4.0, 0.0, 12.0);
		stackPanel2.Children.Add(textBlock);
		ContentControl preview = new ContentControl
		{
			Focusable = false
		};
		Action<ProviderOption> show = delegate(ProviderOption o)
		{
			if (preview.Tag != o)
			{
				preview.Tag = o;
				preview.Content = SampleCard(o);
			}
		};
		foreach (ProviderOption option in options)
		{
			ProviderOption opt = option;
			Grid grid3 = new Grid();
			grid3.Height = 52.0;
			Grid grid4 = grid3;
			grid4.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(46.0)
			});
			grid4.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid4.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			Border border2 = Monogram(option.Letters, option.Brand, 34.0);
			border2.HorizontalAlignment = HorizontalAlignment.Left;
			grid4.Children.Add(border2);
			StackPanel stackPanel3 = new StackPanel();
			stackPanel3.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel4 = stackPanel3;
			stackPanel4.Children.Add(Text(option.Name, 13.0, ThemeTokens.Text, AynType.Semibold));
			TextBlock textBlock2 = Text(option.WhatYouSee, 11.5, ThemeTokens.Text2, AynType.Regular);
			textBlock2.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			stackPanel4.Children.Add(textBlock2);
			Grid.SetColumn(stackPanel4, 1);
			grid4.Children.Add(stackPanel4);
			TextBlock go = Text("Connect", 11.5, ThemeTokens.Text2, AynType.Medium);
			go.VerticalAlignment = VerticalAlignment.Center;
			FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 12.0, ThemeTokens.Text2, filled: false);
			frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
			frameworkElement.RenderTransform = new RotateTransform(-90.0);
			frameworkElement.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel5 = new StackPanel();
			stackPanel5.Orientation = Orientation.Horizontal;
			stackPanel5.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel6 = stackPanel5;
			stackPanel6.Children.Add(go);
			stackPanel6.Children.Add(frameworkElement);
			Grid.SetColumn(stackPanel6, 2);
			grid4.Children.Add(stackPanel6);
			Border b = new Border
			{
				Child = grid4,
				Padding = new Thickness(8.0, 0.0, 12.0, 0.0),
				Margin = new Thickness(-8.0, 0.0, -12.0, 4.0),
				CornerRadius = new CornerRadius(14.0),
				Background = Brushes.Transparent,
				Cursor = Cursors.Hand,
				Tag = option.Id
			};
			b.MouseEnter += delegate
			{
				b.Background = Hover;
				go.Foreground = ThemeTokens.Text;
				show(opt);
			};
			b.MouseLeave += delegate
			{
				if (!b.IsKeyboardFocused)
				{
					b.Background = Brushes.Transparent;
				}
				go.Foreground = ThemeTokens.Text2;
			};
			b.GotKeyboardFocus += delegate
			{
				show(opt);
			};
			Action click = delegate
			{
				choose(opt.Id);
			};
			AttachClick(b, click);
			Keyboard(b, click, () => Brushes.Transparent, () => Hover);
			AutomationName(b, "Connect " + option.Name);
			stackPanel2.Children.Add(b);
		}
		grid.Children.Add(stackPanel2);
		if (options.Count > 0)
		{
			show(options[0]);
		}
		Grid.SetColumn(preview, 2);
		preview.VerticalAlignment = VerticalAlignment.Center;
		grid.Children.Add(preview);
		return grid;
	}

	public static void AutomationName(DependencyObject d, string name)
	{
		AutomationProperties.SetName(d, name);
	}

	public static FrameworkElement SampleCard(ProviderOption o)
	{
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		Grid grid2 = grid;
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		StackPanel stackPanel3 = stackPanel2;
		Border border = Monogram(o.Letters, o.Brand, 20.0);
		border.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		stackPanel3.Children.Add(border);
		TextBlock textBlock = Text("What you'll see", 11.5, ThemeTokens.Text2, AynType.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		stackPanel3.Children.Add(textBlock);
		grid2.Children.Add(stackPanel3);
		Border border2 = SampleChip();
		border2.HorizontalAlignment = HorizontalAlignment.Right;
		grid2.Children.Add(border2);
		stackPanel.Children.Add(grid2);
		if (o.Sample != null)
		{
			stackPanel.Children.Add(o.Sample());
		}
		Border border3 = Card(stackPanel, new Thickness(16.0, 14.0, 16.0, 14.0));
		AutomationName(border3, o.Name + " sample preview");
		return border3;
	}

	public static FrameworkElement SampleLine(double[] values, double width, double height)
	{
		Canvas canvas = new Canvas();
		canvas.Width = width;
		canvas.Height = height;
		canvas.IsHitTestVisible = false;
		Canvas canvas2 = canvas;
		double num = 0.0;
		foreach (double val in values)
		{
			num = Math.Max(num, val);
		}
		if (num <= 0.0 || values.Length < 2)
		{
			return canvas2;
		}
		PointCollection pointCollection = new PointCollection();
		for (int j = 0; j < values.Length; j++)
		{
			pointCollection.Add(new Point((double)j * width / (double)(values.Length - 1), height - 2.0 - (height - 6.0) * values[j] / num));
		}
		PointCollection pointCollection2 = new PointCollection(pointCollection);
		pointCollection2.Add(new Point(width, height));
		pointCollection2.Add(new Point(0.0, height));
		PointCollection points = pointCollection2;
		canvas2.Children.Add(new Polygon
		{
			Points = points,
			Fill = ThemeTokens.AccentSoft
		});
		canvas2.Children.Add(new Polyline
		{
			Points = pointCollection,
			Stroke = ThemeTokens.Text,
			StrokeThickness = 1.5,
			StrokeLineJoin = PenLineJoin.Round
		});
		return canvas2;
	}

	public static FrameworkElement SampleRow(string label, string value)
	{
		Grid grid = new Grid();
		grid.Height = 22.0;
		Grid grid2 = grid;
		TextBlock textBlock = Text(label, 11.5, ThemeTokens.Text2, AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(textBlock);
		TextBlock textBlock2 = Text(value, 11.5, ThemeTokens.Text, AynType.Medium);
		textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		Typography.SetNumeralAlignment(textBlock2, FontNumeralAlignment.Tabular);
		grid2.Children.Add(textBlock2);
		return grid2;
	}

	public static Grid ConnectLayout(ProviderOption o, string reassurance, FrameworkElement steps, FrameworkElement leftExtra, FrameworkElement right, Notice notice, Action back)
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(28.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(360.0)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		StackPanel stackPanel4 = stackPanel3;
		FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 13.0, ThemeTokens.Text, filled: false);
		frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
		frameworkElement.RenderTransform = new RotateTransform(90.0);
		Border border = IconButton(frameworkElement, "Back", back);
		border.Margin = new Thickness(-6.0, 0.0, 4.0, 0.0);
		AutomationName(border, "Back");
		stackPanel4.Children.Add(border);
		Border border2 = Monogram(o.Letters, o.Brand, 24.0);
		border2.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		stackPanel4.Children.Add(border2);
		TextBlock textBlock = Text("Connect " + o.Name, 13.5, ThemeTokens.Text, AynType.Semibold);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		stackPanel4.Children.Add(textBlock);
		stackPanel2.Children.Add(stackPanel4);
		TextBlock textBlock2 = Wrap(reassurance, 11.5, ThemeTokens.Text2, AynType.Regular);
		textBlock2.Margin = new Thickness(0.0, 6.0, 0.0, 14.0);
		stackPanel2.Children.Add(textBlock2);
		stackPanel2.Children.Add(steps);
		if (leftExtra != null)
		{
			leftExtra.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
			stackPanel2.Children.Add(leftExtra);
		}
		if (notice != null)
		{
			notice.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
			stackPanel2.Children.Insert(stackPanel2.Children.IndexOf(steps), notice);
			notice.Toggled += delegate(bool on)
			{
				steps.Visibility = (on ? Visibility.Collapsed : Visibility.Visible);
			};
		}
		grid.Children.Add(stackPanel2);
		Grid.SetColumn(right, 2);
		right.VerticalAlignment = VerticalAlignment.Top;
		right.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		grid.Children.Add(right);
		return grid;
	}

	public static void Reveal(FrameworkElement e, double rise = 10.0)
	{
		if (e != null)
		{
			if (!CapsuleMotion.AnimationsEnabled)
			{
				e.Opacity = 1.0;
				e.RenderTransform = null;
				return;
			}
			TranslateTransform translateTransform = (TranslateTransform)(e.RenderTransform = new TranslateTransform(0.0, rise));
			e.Opacity = 0.0;
			CubicEase cubicEase = new CubicEase();
			cubicEase.EasingMode = EasingMode.EaseOut;
			CubicEase easingFunction = cubicEase;
			Duration duration = new Duration(TimeSpan.FromMilliseconds(220.0));
			e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, duration)
			{
				EasingFunction = easingFunction
			});
			translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(rise, 0.0, duration)
			{
				EasingFunction = easingFunction
			});
		}
	}

	public static string Ago(DateTime utc)
	{
		TimeSpan timeSpan = DateTime.UtcNow - utc;
		DateTime dateTime = utc.ToLocalTime();
		if (timeSpan.TotalMinutes < 1.0)
		{
			return "just now";
		}
		if (timeSpan.TotalMinutes < 60.0)
		{
			return (int)timeSpan.TotalMinutes + " min ago";
		}
		if (timeSpan.TotalHours < 24.0 && dateTime.Date == DateTime.Today)
		{
			return (int)timeSpan.TotalHours + " h ago";
		}
		if (dateTime.Date == DateTime.Today.AddDays(-1.0))
		{
			return "yesterday";
		}
		return dateTime.ToString((dateTime.Year == DateTime.Today.Year) ? "d MMM" : "d MMM yyyy", CultureInfo.CurrentCulture);
	}
}
