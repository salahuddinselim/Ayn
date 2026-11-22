using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Message;

public class MessageBannerTool : IBuddyTool
{
	private sealed class Pill : Border
	{
		private readonly TextBlock _t;

		private readonly bool _filled;

		private bool _on;

		public Action Clicked;

		public Pill(string text, bool filled, Action click)
		{
			_filled = filled;
			Clicked = click;
			base.Height = 24.0;
			base.CornerRadius = new CornerRadius(12.0);
			base.BorderThickness = new Thickness(1.0);
			base.Cursor = Cursors.Hand;
			_t = BuddyTheme.Text(text, 13.0, BuddyTheme.Text92, FontWeights.Medium);
			_t.HorizontalAlignment = HorizontalAlignment.Center;
			_t.VerticalAlignment = VerticalAlignment.Center;
			_t.TextTrimming = TextTrimming.None;
			Child = _t;
			base.Focusable = true;
			base.FocusVisualStyle = null;
			MouseEventHandler value = delegate
			{
				if (!_on)
				{
					base.Background = BuddyTheme.ControlActive;
				}
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Paint();
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				Invoke();
			};
			base.GotKeyboardFocus += delegate
			{
				Paint();
			};
			base.LostKeyboardFocus += delegate
			{
				Paint();
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.Space)
				{
					e.Handled = true;
					Invoke();
				}
			};
			Paint();
		}

		private void Invoke()
		{
			Clicked?.Invoke();
		}

		public void SetActive(bool on)
		{
			_on = on;
			Paint();
		}

		private void Paint()
		{
			base.Background = (_on ? BuddyTheme.SelectedFill : (_filled ? BuddyTheme.Control : Brushes.Transparent));
			base.BorderBrush = (base.IsKeyboardFocused ? BuddyTheme.FocusRing : (_on ? BuddyTheme.SelectedEdge : Brushes.Transparent));
			_t.Foreground = (_on ? BuddyTheme.SelectedInk : (_filled ? BuddyTheme.Text92 : BuddyTheme.Text50));
			_t.FontWeight = (_on ? FontWeights.SemiBold : AynType.Regular);
		}
	}

	private sealed class Swatch : Grid
	{
		private readonly Ellipse _ring;

		private readonly Ellipse _fill;

		private bool _on;

		public Action Clicked;

		public Swatch(double size, double fill, Brush brush, string tip, Action click)
		{
			base.Width = size;
			base.Height = size;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			Clicked = click;
			_ring = new Ellipse
			{
				Width = size,
				Height = size,
				Stroke = BuddyTheme.Text100,
				StrokeThickness = 1.5,
				Opacity = 0.0
			};
			_fill = new Ellipse
			{
				Width = fill,
				Height = fill,
				Fill = brush,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			base.Children.Add(_ring);
			base.Children.Add(_fill);
			if (tip != null)
			{
				base.ToolTip = BuddyTooltip.Create(tip);
			}
			base.Focusable = true;
			base.FocusVisualStyle = null;
			base.MouseEnter += delegate
			{
				if (!_on && !base.IsKeyboardFocused)
				{
					_ring.Opacity = 0.35;
				}
			};
			base.MouseLeave += delegate
			{
				PaintRing();
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				Invoke();
			};
			base.GotKeyboardFocus += delegate
			{
				PaintRing();
			};
			base.LostKeyboardFocus += delegate
			{
				PaintRing();
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.Space)
				{
					e.Handled = true;
					Invoke();
				}
			};
		}

		private void Invoke()
		{
			Clicked?.Invoke();
		}

		private void PaintRing()
		{
			bool isKeyboardFocused = base.IsKeyboardFocused;
			_ring.Stroke = (isKeyboardFocused ? BuddyTheme.FocusRing : BuddyTheme.Text100);
			_ring.Opacity = ((isKeyboardFocused || _on) ? 1 : 0);
		}

		public void SetSelected(bool on)
		{
			_on = on;
			PaintRing();
		}

		public void SetFill(Brush b)
		{
			_fill.Fill = b;
		}

		public void SetStroke(Brush b)
		{
			_fill.Stroke = b;
			_fill.StrokeThickness = 1.0;
		}
	}

	private sealed class Strip : Grid
	{
		private readonly Ellipse _thumb;

		private readonly TranslateTransform _pos = new TranslateTransform();

		private readonly Action<double> _changed;

		private double _value;

		public Strip(Brush fill, double value, Action<double> changed)
		{
			_changed = changed;
			base.Height = 16.0;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			base.Children.Add(new Border
			{
				Height = 10.0,
				CornerRadius = new CornerRadius(5.0),
				Background = fill,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(7.0, 0.0, 7.0, 0.0)
			});
			_thumb = new Ellipse
			{
				Width = 14.0,
				Height = 14.0,
				Fill = NotchInk.Primary,
				Stroke = NotchInk.Surface,
				StrokeThickness = 1.0,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				RenderTransform = _pos,
				IsHitTestVisible = false
			};
			base.Children.Add(_thumb);
			_value = value;
			SizeChangedEventHandler value2 = delegate
			{
				Place();
			};
			base.SizeChanged += value2;
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				CaptureMouse();
				Drag(e.GetPosition(this).X);
			};
			base.MouseMove += delegate(object s, MouseEventArgs e)
			{
				if (base.IsMouseCaptured)
				{
					Drag(e.GetPosition(this).X);
				}
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				ReleaseMouseCapture();
			};
		}

		public void SetPosition(double v)
		{
			_value = Math.Max(0.0, Math.Min(1.0, v));
			Place();
		}

		private void Drag(double x)
		{
			double num = base.ActualWidth - 14.0;
			if (!(num <= 0.0))
			{
				_value = Math.Max(0.0, Math.Min(1.0, (x - 7.0) / num));
				Place();
				_changed(_value);
			}
		}

		private void Place()
		{
			_pos.X = Math.Max(0.0, base.ActualWidth - 14.0) * _value;
		}
	}

	private const double PreviewH = 56.0;

	private const double ComposeH = 34.0;

	private const double FlashW = 104.0;

	private const double SecondaryW = 92.0;

	private const double PreviewScale = 0.82;

	private const double PreviewMaxW = 420.0;

	private static readonly string[] Swatches = new string[7] { "#FFFFFF", "#F6DFC4", "#F5A524", "#E8735A", "#F0436A", "#A35BF0", "#5AC8FA" };

	private static readonly MessageFinish[] Finishes = new MessageFinish[4]
	{
		MessageFinish.None,
		MessageFinish.Silver,
		MessageFinish.Ocean,
		MessageFinish.Aurora
	};

	private static readonly MessageAnimationMode[] Animations = new MessageAnimationMode[6]
	{
		MessageAnimationMode.Slide,
		MessageAnimationMode.Fade,
		MessageAnimationMode.TypeOn,
		MessageAnimationMode.Scroll,
		MessageAnimationMode.Blur,
		MessageAnimationMode.Pulse
	};

	private Grid _root;

	private bool _active;

	private EventHandler _stateHandler;

	private EventHandler _emojiHandler;

	private int _emojiRefreshQueued;

	private Grid _stage;

	private TextBlock _stateText;

	private Ellipse _liveDot;

	private DispatcherTimer _previewDebounce;

	private long _previewGeneration;

	private long _scheduledPreviewGeneration;

	private TextBox _input;

	private Canvas _inputOverlay;

	private TextBlock _placeholder;

	private TextBlock _counter;

	private TextBlock _secondaryText;

	private BuddyPrimaryPill _flash;

	private Border _secondary;

	private bool _syncingText;

	private readonly Dictionary<MessageAnimationMode, Pill> _animPills = new Dictionary<MessageAnimationMode, Pill>();

	private Slider _speed;

	private TextBlock _speedValue;

	private readonly List<Swatch> _swatches = new List<Swatch>();

	private Swatch _customSwatch;

	private FrameworkElement _customPlus;

	private readonly Dictionary<MessageFinish, Swatch> _finishSwatches = new Dictionary<MessageFinish, Swatch>();

	private readonly Dictionary<MessageFinish, TextBlock> _finishNames = new Dictionary<MessageFinish, TextBlock>();

	private readonly Dictionary<MessageDurationMode, Pill> _durPills = new Dictionary<MessageDurationMode, Pill>();

	private FrameworkElement _durHost;

	private BuddyToggle _repeat;

	private Popup _popup;

	private AnimationClock _dotClock;

	private bool _overlayQueued;

	public string Id => "message";

	public string DisplayName => "Message";

	public ToolCategory Category => ToolCategory.Utility;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => false;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	private static MessageService Svc => MessageService.Instance;

	private static MessageSettings S => MessageService.Instance.Settings;

	public MessageBannerTool()
	{
		IsEnabled = true;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_stateHandler = delegate
		{
			if (_root != null)
			{
				SyncLive();
			}
		};
		Svc.StateChanged += _stateHandler;
		_emojiHandler = delegate
		{
			if (!_active || _root == null || Interlocked.Exchange(ref _emojiRefreshQueued, 1) == 1)
			{
				return;
			}
			long generation = _previewGeneration;
			try
			{
				_root.Dispatcher.BeginInvoke((Action)delegate
				{
					Interlocked.Exchange(ref _emojiRefreshQueued, 0);
					if (_active && generation == _previewGeneration)
					{
						SchedulePreview();
						QueueOverlay();
					}
				});
			}
			catch
			{
				Interlocked.Exchange(ref _emojiRefreshQueued, 0);
			}
		};
		ColorEmoji.Rendered += _emojiHandler;
	}

	public void Shutdown()
	{
		if (_stateHandler != null)
		{
			Svc.StateChanged -= _stateHandler;
		}
		if (_emojiHandler != null)
		{
			ColorEmoji.Rendered -= _emojiHandler;
		}
		_stateHandler = null;
		_emojiHandler = null;
		OnDeactivated();
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		_active = true;
		if (_root != null)
		{
			SyncAll();
			QueueOverlay();
			SchedulePreview(immediate: true);
		}
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		_active = false;
		_previewGeneration++;
		if (_previewDebounce != null)
		{
			_previewDebounce.Stop();
		}
		if (_stage != null)
		{
			_stage.Children.Clear();
		}
		SetDotPulse(on: false);
		ClosePopup();
		Svc.Flush();
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return null;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateMessageIcon(size, stroke);
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			return _root;
		}
		if (!IsInitialized)
		{
			Initialize();
		}
		Grid grid = new Grid();
		grid.Width = 728.0;
		grid.Height = 242.0;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		grid.Background = Brushes.Transparent;
		grid.Tag = "NoOuterScroll";
		Grid grid2 = BuddyTheme.Crisp(grid);
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(56.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(10.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(34.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(10.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.Children.Add(BuildPreview());
		FrameworkElement element = BuildCompose();
		Grid.SetRow(element, 2);
		grid2.Children.Add(element);
		Grid grid3 = new Grid();
		for (int i = 0; i < 5; i++)
		{
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = ((i % 2 == 1) ? new GridLength(10.0) : new GridLength(1.0, GridUnitType.Star))
			});
		}
		FrameworkElement element2 = BuildAnimationCard();
		FrameworkElement element3 = BuildColourCard();
		FrameworkElement element4 = BuildDurationCard();
		Grid.SetColumn(element3, 2);
		Grid.SetColumn(element4, 4);
		grid3.Children.Add(element2);
		grid3.Children.Add(element3);
		grid3.Children.Add(element4);
		Grid.SetRow(grid3, 4);
		grid2.Children.Add(grid3);
		_root = grid2;
		SyncAll();
		if (_active)
		{
			SchedulePreview(immediate: true);
		}
		return grid2;
	}

	private static Border Card(UIElement child)
	{
		Border border = new Border();
		border.Background = BuddyTheme.CardFill;
		border.BorderBrush = BuddyTheme.Hairline;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(13.0, 13.0, 13.0, 11.0);
		border.Child = child;
		Border border2 = BuddyTheme.Crisp(border);
		RenderOptions.SetClearTypeHint(border2, ClearTypeHint.Enabled);
		return ThemeTokens.Card(border2);
	}

	private static TextBlock Txt(string text, double size, Brush ink, FontWeight weight)
	{
		TextBlock textBlock = BuddyTheme.Text(text, size, ink, weight);
		textBlock.TextTrimming = TextTrimming.None;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		return textBlock;
	}

	private static Border Divider()
	{
		Border border = new Border();
		border.Height = 1.0;
		border.Background = BuddyTheme.Hairline;
		border.Margin = new Thickness(0.0, 8.0, 0.0, 8.0);
		return border;
	}

	private FrameworkElement BuildPreview()
	{
		Grid grid = new Grid();
		grid.ClipToBounds = true;
		Grid grid2 = grid;
		_stage = new Grid
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			IsHitTestVisible = false
		};
		grid2.Children.Add(_stage);
		FrameworkElement frameworkElement = BuddyTheme.CardHeader("rectangle_landscape", "Live preview");
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Left;
		frameworkElement.Margin = new Thickness(13.0, 13.0, 0.0, 0.0);
		grid2.Children.Add(frameworkElement);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.Margin = new Thickness(0.0, 11.0, 13.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		_liveDot = new Ellipse
		{
			Width = 6.0,
			Height = 6.0,
			Fill = BuddyTheme.Text100,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		stackPanel2.Children.Add(_liveDot);
		_stateText = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		stackPanel2.Children.Add(_stateText);
		grid2.Children.Add(stackPanel2);
		Border border = Card(grid2);
		border.Padding = new Thickness(0.0);
		border.ClipToBounds = true;
		return border;
	}

	private void RebuildPreview()
	{
		if (_stage == null || !_active)
		{
			return;
		}
		using (new MessageTiming("preview construction"))
		{
			if (_previewDebounce != null)
			{
				_previewDebounce.Stop();
			}
			MessageSettings s = S;
			string text = MessageText.Clean(s.Text);
			NotchConfig instance = NotchConfig.Instance;
			Size size = ((text.Length == 0) ? new Size(instance?.GetCollapsedWidth() ?? 228.0, 35.0) : MessageRenderer.MeasureDimensions(text, s));
			double num = Math.Min(0.82, Math.Min(52.0 / size.Height, 420.0 / size.Width));
			Grid grid = new Grid();
			grid.Width = size.Width;
			grid.Height = size.Height;
			grid.HorizontalAlignment = HorizontalAlignment.Left;
			grid.VerticalAlignment = VerticalAlignment.Top;
			Grid grid2 = grid;
			grid2.Children.Add(new Path
			{
				Data = NotchGeometryBuilder.BuildGeometry((instance != null) ? instance.Style : "Default", size.Width, size.Height, instance?.CornerRadius ?? (-1.0)),
				Fill = NotchInk.Black
			});
			if (text.Length > 0)
			{
				grid2.Children.Add(MessageRenderer.CreateRealCapsuleVisual(text, s));
			}
			using (new MessageTiming("preview RenderTransform"))
			{
				grid2.RenderTransform = new ScaleTransform(num, num);
				Canvas canvas = new Canvas();
				canvas.Width = size.Width * num;
				canvas.Height = size.Height * num;
				canvas.Margin = new Thickness(0.0, -1.0, 0.0, 0.0);
				Canvas canvas2 = canvas;
				canvas2.Children.Add(grid2);
				_stage.Children.Clear();
				_stage.Children.Add(canvas2);
				_stage.InvalidateMeasure();
				_stage.InvalidateVisual();
			}
		}
	}

	private void QueuePreview(long generation)
	{
		ToolRouter.AfterNextFrame(delegate
		{
			if (!_active || generation != _previewGeneration)
			{
				return;
			}
			try
			{
				RebuildPreview();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Message preview failed: " + ex.GetType().Name);
			}
		});
	}

	private void SchedulePreview(bool immediate = false)
	{
		if (!_active)
		{
			return;
		}
		long num = ++_previewGeneration;
		if (_previewDebounce != null)
		{
			_previewDebounce.Stop();
		}
		if (immediate)
		{
			QueuePreview(num);
			return;
		}
		if (_previewDebounce == null)
		{
			_previewDebounce = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(150.0)
			};
			_previewDebounce.Tick += delegate
			{
				_previewDebounce.Stop();
				try
				{
					QueuePreview(_scheduledPreviewGeneration);
				}
				catch (Exception ex)
				{
					_previewDebounce.Stop();
					try
					{
						TransactionLogger.Warn("Message preview failed: " + ex.GetType().Name + ": " + ex.Message);
					}
					catch
					{
					}
				}
			};
		}
		_previewDebounce.Stop();
		_scheduledPreviewGeneration = num;
		_previewDebounce.Start();
	}

	internal void RefreshPresentation()
	{
		if (_active && _stage != null)
		{
			SchedulePreview(immediate: true);
			QueueOverlay();
		}
	}

	private void SetDotPulse(bool on)
	{
		if (_liveDot == null)
		{
			return;
		}
		if (_dotClock != null)
		{
			if (_dotClock.Controller != null)
			{
				_dotClock.Controller.Stop();
			}
			_liveDot.ApplyAnimationClock(UIElement.OpacityProperty, null);
			_dotClock = null;
			LoopGuard.End(_liveDot);
		}
		if (on)
		{
			LoopGuard.Begin(_liveDot, "message live dot");
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.25, TimeSpan.FromSeconds(0.9));
			doubleAnimation.AutoReverse = true;
			doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			doubleAnimation.EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			};
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			doubleAnimation2.Freeze();
			_dotClock = doubleAnimation2.CreateClock();
			_liveDot.ApplyAnimationClock(UIElement.OpacityProperty, _dotClock);
		}
	}

	private FrameworkElement BuildCompose()
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(112.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(100.0)
		});
		Border border = new Border();
		border.Height = 34.0;
		border.CornerRadius = new CornerRadius(17.0);
		border.Background = BuddyTheme.CardFill;
		border.BorderBrush = BuddyTheme.Hairline;
		border.BorderThickness = new Thickness(1.0);
		border.Padding = new Thickness(13.0, 0.0, 13.0, 0.0);
		border.Cursor = Cursors.IBeam;
		Border border2 = border;
		Grid grid2 = new Grid();
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		FrameworkElement frameworkElement = FluentIcons.Create("slide_text", 14.0, BuddyTheme.Text50, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		grid2.Children.Add(frameworkElement);
		_placeholder = Txt("Type a message", 13.5, BuddyTheme.Placeholder, FontWeights.Medium);
		_placeholder.IsHitTestVisible = false;
		_placeholder.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		Grid.SetColumn(_placeholder, 1);
		grid2.Children.Add(_placeholder);
		_input = PlainTextBox(13.5);
		_input.FontWeight = FontWeights.Medium;
		_input.Text = S.Text;
		_input.TextChanged += delegate
		{
			OnTextChanged();
		};
		_input.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Return)
			{
				e.Handled = true;
				DoFlash();
			}
		};
		bool scrollHooked = false;
		_input.Loaded += delegate
		{
			if (!scrollHooked)
			{
				_input.ApplyTemplate();
				if (_input.Template.FindName("PART_ContentHost", _input) is ScrollViewer scrollViewer)
				{
					scrollViewer.ScrollChanged += delegate
					{
						QueueOverlay();
					};
					scrollHooked = true;
				}
			}
			QueueOverlay();
		};
		_input.SizeChanged += delegate
		{
			QueueOverlay();
		};
		Grid grid3 = new Grid();
		grid3.ClipToBounds = true;
		Grid grid4 = grid3;
		grid4.Children.Add(_input);
		_inputOverlay = new Canvas
		{
			IsHitTestVisible = false,
			ClipToBounds = true
		};
		grid4.Children.Add(_inputOverlay);
		Grid.SetColumn(grid4, 1);
		grid2.Children.Add(grid4);
		_counter = Txt("", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_counter.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		Typography.SetNumeralAlignment(_counter, FontNumeralAlignment.Tabular);
		Grid.SetColumn(_counter, 2);
		grid2.Children.Add(_counter);
		border2.Child = grid2;
		border2.MouseLeftButtonDown += delegate
		{
			if (!_input.IsKeyboardFocusWithin)
			{
				_input.Focus();
				_input.CaretIndex = _input.Text.Length;
			}
		};
		grid.Children.Add(border2);
		_flash = new BuddyPrimaryPill("flash", "Flash", 34.0)
		{
			Width = 104.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			ToolTip = BuddyTooltip.Create("Show on the notch (Enter)")
		};
		_flash.Click += DoFlash;
		Grid.SetColumn(_flash, 1);
		grid.Children.Add(_flash);
		_secondaryText = Txt("Clear", 13.0, BuddyTheme.Text92, AynType.Medium);
		_secondaryText.HorizontalAlignment = HorizontalAlignment.Center;
		_secondary = new Border
		{
			Width = 92.0,
			Height = 34.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			CornerRadius = new CornerRadius(17.0),
			Background = BuddyTheme.Control,
			BorderThickness = new Thickness(1.0),
			BorderBrush = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Focusable = true,
			FocusVisualStyle = null,
			Child = _secondaryText
		};
		_secondary.MouseEnter += delegate
		{
			_secondary.Background = BuddyTheme.ControlActive;
		};
		_secondary.MouseLeave += delegate
		{
			_secondary.Background = BuddyTheme.Control;
		};
		_secondary.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_secondary.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			DoSecondary();
		};
		_secondary.GotKeyboardFocus += delegate
		{
			_secondary.BorderBrush = BuddyTheme.FocusRing;
		};
		_secondary.LostKeyboardFocus += delegate
		{
			_secondary.BorderBrush = Brushes.Transparent;
		};
		_secondary.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				DoSecondary();
			}
		};
		Grid.SetColumn(_secondary, 2);
		grid.Children.Add(_secondary);
		return grid;
	}

	private static TextBox PlainTextBox(double size)
	{
		TextBox textBox = new TextBox();
		textBox.Background = Brushes.Transparent;
		textBox.BorderThickness = new Thickness(0.0);
		textBox.Foreground = BuddyTheme.Text92;
		textBox.CaretBrush = BuddyTheme.Text100;
		textBox.SelectionBrush = BuddyTheme.Text50;
		textBox.SelectionOpacity = 0.45;
		textBox.FontFamily = BuddyTheme.Font;
		textBox.FontSize = size;
		textBox.Padding = new Thickness(0.0);
		textBox.VerticalAlignment = VerticalAlignment.Center;
		textBox.VerticalContentAlignment = VerticalAlignment.Center;
		textBox.AcceptsReturn = false;
		textBox.AcceptsTab = false;
		textBox.FocusVisualStyle = null;
		TextBox textBox2 = textBox;
		ControlTemplate controlTemplate = new ControlTemplate(typeof(TextBox));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(ScrollViewer));
		frameworkElementFactory.Name = "PART_ContentHost";
		frameworkElementFactory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
		frameworkElementFactory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
		frameworkElementFactory.SetValue(Control.BackgroundProperty, Brushes.Transparent);
		controlTemplate.VisualTree = frameworkElementFactory;
		textBox2.Template = controlTemplate;
		BuddyTheme.ApplyTextMode(textBox2, size);
		return textBox2;
	}

	private void OnTextChanged()
	{
		if (!_syncingText)
		{
			string text = _input.Text ?? string.Empty;
			string text2 = MessageText.Limit(text.Replace("\r", "").Replace("\n", " "));
			if (text2 != text)
			{
				int caretIndex = Math.Min(_input.CaretIndex, text2.Length);
				_syncingText = true;
				_input.Text = text2;
				_input.CaretIndex = caretIndex;
				_syncingText = false;
			}
			S.Text = text2;
			Svc.SaveSoon();
			SyncCompose();
			SyncLive();
			SchedulePreview();
			QueueOverlay();
		}
	}

	private void QueueOverlay()
	{
		if (_overlayQueued || _input == null || !_active)
		{
			return;
		}
		_overlayQueued = true;
		long generation = _previewGeneration;
		ToolRouter.AfterNextFrame(delegate
		{
			_overlayQueued = false;
			if (_active)
			{
				if (generation == _previewGeneration)
				{
					try
					{
						UpdateOverlay();
						return;
					}
					catch (Exception ex)
					{
						TransactionLogger.Warn("Message input overlay failed: " + ex.GetType().Name);
						return;
					}
				}
				QueueOverlay();
			}
		});
	}

	private void UpdateOverlay()
	{
		_overlayQueued = false;
		_inputOverlay.Children.Clear();
		string s = _input.Text ?? string.Empty;
		if (!EmojiText.HasEmoji(s) || !_input.IsLoaded || ColorEmoji.Get("\ud83d\ude00") == null)
		{
			_input.Foreground = BuddyTheme.Text92;
			return;
		}
		_input.Foreground = Brushes.Transparent;
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		int start = 0;
		foreach (string item in EmojiText.Clusters(s))
		{
			Image image = (EmojiText.IsEmoji(item) ? ColorEmoji.CreateImage(item, _input.FontSize) : null);
			if (image == null)
			{
				stringBuilder.Append(item);
				num += item.Length;
				continue;
			}
			PlaceOverlayText(stringBuilder, start);
			Rect rect = CharRect(num);
			Rect rect2 = CharRect(num + item.Length);
			Border border = new Border();
			border.Width = Math.Max(1.0, rect2.Left - rect.Left);
			border.Height = rect.Height;
			border.Child = image;
			Border element = border;
			Canvas.SetLeft(element, rect.Left);
			Canvas.SetTop(element, rect.Top);
			_inputOverlay.Children.Add(element);
			num += item.Length;
			start = num;
		}
		PlaceOverlayText(stringBuilder, start);
	}

	private void PlaceOverlayText(StringBuilder run, int start)
	{
		if (run.Length != 0)
		{
			Rect rect = CharRect(start);
			TextBlock textBlock = new TextBlock();
			textBlock.Text = run.ToString();
			textBlock.FontFamily = _input.FontFamily;
			textBlock.FontSize = _input.FontSize;
			textBlock.FontWeight = _input.FontWeight;
			textBlock.Foreground = BuddyTheme.Text92;
			TextBlock element = textBlock;
			BuddyTheme.ApplyTextMode(element, _input.FontSize);
			Canvas.SetLeft(element, rect.Left);
			Canvas.SetTop(element, rect.Top);
			_inputOverlay.Children.Add(element);
			run.Length = 0;
		}
	}

	private Rect CharRect(int index)
	{
		Rect rectFromCharacterIndex = _input.GetRectFromCharacterIndex(Math.Min(index, _input.Text.Length));
		if (rectFromCharacterIndex.IsEmpty)
		{
			return new Rect(0.0, 0.0, 0.0, _input.ActualHeight);
		}
		Point point = _input.TranslatePoint(rectFromCharacterIndex.TopLeft, _inputOverlay);
		return new Rect(point.X, point.Y, 0.0, rectFromCharacterIndex.Height);
	}

	internal string GetText()
	{
		return _input.Text;
	}

	internal void SetText(string text)
	{
		_input.Text = text ?? string.Empty;
	}

	private void DoFlash()
	{
		if (MessageText.Clean(S.Text).Length != 0)
		{
			if (!Svc.Flash())
			{
				ShowHint(Svc.LastError ?? "Couldn't flash. Try again");
				return;
			}
			Keyboard.ClearFocus();
			CapsuleWindow.Instance?.RequestCollapse("Message flashed");
		}
	}

	private void ShowHint(string text)
	{
		_liveDot.Visibility = Visibility.Collapsed;
		SetDotPulse(on: false);
		_stateText.Text = text;
		_stateText.Foreground = BuddyTheme.Text100;
	}

	private void DoSecondary()
	{
		if (Svc.IsLive)
		{
			Svc.TakeDown();
		}
		else if (_input.Text.Length != 0)
		{
			_input.Text = string.Empty;
			_input.Focus();
		}
	}

	private FrameworkElement BuildAnimationCard()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(BuddyTheme.CardHeader("play", "Animation"));
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		Grid grid2 = grid;
		for (int i = 0; i < 5; i++)
		{
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = ((i % 2 == 1) ? new GridLength(6.0) : new GridLength(1.0, GridUnitType.Star))
			});
		}
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(24.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(6.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(24.0)
		});
		for (int j = 0; j < Animations.Length; j++)
		{
			MessageAnimationMode mode = Animations[j];
			Pill pill = new Pill(AnimationName(mode), filled: true, delegate
			{
				SetAnimation(mode);
			});
			Grid.SetColumn(pill, j % 3 * 2);
			Grid.SetRow(pill, j / 3 * 2);
			grid2.Children.Add(pill);
			_animPills[mode] = pill;
		}
		stackPanel.Children.Add(grid2);
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		grid3.Height = 20.0;
		Grid grid4 = grid3;
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(46.0)
		});
		grid4.Children.Add(Txt("Speed", 11.5, BuddyTheme.Text50, FontWeights.Medium));
		_speed = BuddySlider.Create(0.0, 100.0, S.Speed, delegate(double v)
		{
			S.Speed = Math.Round(v);
			Svc.SaveSoon();
			SyncSpeedLabel();
			SchedulePreview();
		});
		_speed.Margin = new Thickness(10.0, 0.0, 8.0, 0.0);
		_speed.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_speed, 1);
		grid4.Children.Add(_speed);
		_speedValue = Txt("", 11.5, BuddyTheme.Text92, FontWeights.Medium);
		_speedValue.HorizontalAlignment = HorizontalAlignment.Right;
		Grid.SetColumn(_speedValue, 2);
		grid4.Children.Add(_speedValue);
		stackPanel.Children.Add(grid4);
		return Card(stackPanel);
	}

	private static string AnimationName(MessageAnimationMode m)
	{
		if (m == MessageAnimationMode.TypeOn)
		{
			return "Type-on";
		}
		return m.ToString();
	}

	private void SetAnimation(MessageAnimationMode m)
	{
		S.Animation = m;
		Svc.SaveSoon();
		SyncCards();
		SchedulePreview(immediate: true);
	}

	private void SyncSpeedLabel()
	{
		double speed = S.Speed;
		_speedValue.Text = ((speed < 34.0) ? "Slow" : ((speed > 66.0) ? "Fast" : "Normal"));
	}

	private FrameworkElement BuildColourCard()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(BuddyTheme.CardHeader("paint_brush", "Colour"));
		UniformGrid uniformGrid = new UniformGrid();
		uniformGrid.Columns = Swatches.Length + 1;
		uniformGrid.Rows = 1;
		uniformGrid.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		uniformGrid.Height = 22.0;
		UniformGrid uniformGrid2 = uniformGrid;
		string[] swatches = Swatches;
		foreach (string text in swatches)
		{
			string h = text;
			Swatch swatch = new Swatch(22.0, 16.0, new SolidColorBrush(MessageText.ParseColor(h).Value), null, delegate
			{
				SetColour(h);
			});
			swatch.Tag = h;
			Swatch swatch2 = swatch;
			_swatches.Add(swatch2);
			uniformGrid2.Children.Add(swatch2);
		}
		_customSwatch = new Swatch(22.0, 16.0, BuddyTheme.Control, "Custom colour", null);
		_customPlus = FluentIcons.Create("add", 11.0, BuddyTheme.Text50, filled: false);
		_customPlus.IsHitTestVisible = false;
		_customPlus.HorizontalAlignment = HorizontalAlignment.Center;
		_customPlus.VerticalAlignment = VerticalAlignment.Center;
		_customSwatch.Children.Add(_customPlus);
		_customSwatch.Clicked = delegate
		{
			ShowColourPopup(_customSwatch);
		};
		uniformGrid2.Children.Add(_customSwatch);
		stackPanel.Children.Add(uniformGrid2);
		stackPanel.Children.Add(Divider());
		Grid grid = new Grid();
		grid.Height = 39.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(44.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		TextBlock textBlock = Txt("Finish", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Top;
		textBlock.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
		grid2.Children.Add(textBlock);
		UniformGrid uniformGrid3 = new UniformGrid();
		uniformGrid3.Columns = Finishes.Length;
		uniformGrid3.Rows = 1;
		UniformGrid uniformGrid4 = uniformGrid3;
		MessageFinish[] finishes = Finishes;
		foreach (MessageFinish messageFinish in finishes)
		{
			MessageFinish fin = messageFinish;
			StackPanel stackPanel2 = new StackPanel();
			Swatch swatch3 = new Swatch(24.0, 18.0, MessageRenderer.CreateFinishSwatchBrush(fin), FinishName(fin), delegate
			{
				SetFinish(fin);
			});
			swatch3.HorizontalAlignment = HorizontalAlignment.Center;
			if (fin == MessageFinish.None)
			{
				swatch3.SetStroke(BuddyTheme.Track);
			}
			stackPanel2.Children.Add(swatch3);
			TextBlock textBlock2 = Txt(FinishName(fin), 11.0, BuddyTheme.Text92, FontWeights.Medium);
			textBlock2.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock2.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			textBlock2.Visibility = Visibility.Hidden;
			stackPanel2.Children.Add(textBlock2);
			_finishSwatches[fin] = swatch3;
			_finishNames[fin] = textBlock2;
			uniformGrid4.Children.Add(stackPanel2);
		}
		Grid.SetColumn(uniformGrid4, 1);
		grid2.Children.Add(uniformGrid4);
		stackPanel.Children.Add(grid2);
		return Card(stackPanel);
	}

	private static string FinishName(MessageFinish f)
	{
		if (f != 0)
		{
			return f.ToString();
		}
		return "Solid";
	}

	private void SetColour(string hex)
	{
		S.ColorHex = hex;
		S.Finish = MessageFinish.None;
		Svc.SaveSoon();
		SyncCards();
		SchedulePreview(immediate: true);
	}

	private void SetFinish(MessageFinish f)
	{
		S.Finish = f;
		Svc.SaveSoon();
		SyncCards();
		SchedulePreview(immediate: true);
	}

	private FrameworkElement BuildDurationCard()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(BuddyTheme.CardHeader("clock", "Duration"));
		Border border = new Border();
		border.Height = 28.0;
		border.CornerRadius = new CornerRadius(14.0);
		border.Background = BuddyTheme.Control;
		border.Padding = new Thickness(2.0);
		border.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		Border border2 = border;
		UniformGrid uniformGrid = new UniformGrid();
		uniformGrid.Columns = 4;
		uniformGrid.Rows = 1;
		UniformGrid uniformGrid2 = uniformGrid;
		MessageDurationMode[] array = new MessageDurationMode[4]
		{
			MessageDurationMode.Sec3,
			MessageDurationMode.Sec5,
			MessageDurationMode.Sec10,
			MessageDurationMode.Custom
		};
		foreach (MessageDurationMode messageDurationMode in array)
		{
			MessageDurationMode mode = messageDurationMode;
			Pill pill = new Pill(messageDurationMode switch
			{
				MessageDurationMode.Sec10 => "10s", 
				MessageDurationMode.Sec5 => "5s", 
				MessageDurationMode.Sec3 => "3s", 
				_ => "Custom", 
			}, filled: false, null)
			{
				Height = 24.0
			};
			pill.Clicked = delegate
			{
				if (mode == MessageDurationMode.Custom)
				{
					ShowDurationPopup(pill);
				}
				else
				{
					SetDuration(mode, S.CustomDurationSeconds);
				}
			};
			_durPills[messageDurationMode] = pill;
			uniformGrid2.Children.Add(pill);
		}
		border2.Child = uniformGrid2;
		_durHost = border2;
		stackPanel.Children.Add(border2);
		Border border3 = Divider();
		border3.Margin = new Thickness(0.0, 12.0, 0.0, 12.0);
		stackPanel.Children.Add(border3);
		Grid grid = new Grid();
		grid.Height = 22.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.Children.Add(Txt("Repeat until taken down", 13.0, BuddyTheme.Text92, FontWeights.Medium));
		_repeat = new BuddyToggle
		{
			VerticalAlignment = VerticalAlignment.Center,
			ToolTip = BuddyTooltip.Create("Keep the banner on the notch until you take it down")
		};
		_repeat.SetCheckedSilently(S.RepeatEnabled);
		_repeat.Toggled += delegate(object s, bool on)
		{
			S.RepeatEnabled = on;
			Svc.SaveSoon();
			SyncCards();
			SyncLive();
		};
		Grid.SetColumn(_repeat, 1);
		grid2.Children.Add(_repeat);
		stackPanel.Children.Add(grid2);
		return Card(stackPanel);
	}

	private void SetDuration(MessageDurationMode d, int customSeconds)
	{
		S.Duration = d;
		S.CustomDurationSeconds = MessageText.ClampSeconds(customSeconds);
		Svc.SaveSoon();
		SyncCards();
		SyncLive();
	}

	private Border PopupSurface(double width, UIElement child)
	{
		Border border = new Border();
		border.Width = width;
		border.Background = BuddyTheme.Control;
		border.BorderBrush = BuddyTheme.ControlActive;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(14.0);
		border.Padding = new Thickness(12.0);
		border.Child = child;
		return BuddyTheme.Crisp(border);
	}

	private void OpenPopup(FrameworkElement target, Border surface)
	{
		ClosePopup();
		_popup = new Popup
		{
			PlacementTarget = target,
			Placement = PlacementMode.Bottom,
			VerticalOffset = 6.0,
			StaysOpen = false,
			AllowsTransparency = true,
			PopupAnimation = PopupAnimation.None,
			Child = surface
		};
		_popup.Closed += delegate
		{
			if (_root != null)
			{
				SyncCards();
			}
		};
		_popup.IsOpen = true;
	}

	private void ClosePopup()
	{
		if (_popup == null)
		{
			return;
		}
		Popup popup = _popup;
		_popup = null;
		try
		{
			popup.IsOpen = false;
		}
		catch
		{
		}
	}

	private void ShowColourPopup(FrameworkElement target)
	{
		Color color = MessageText.ParseColor(S.CustomColorHex) ?? MessageText.ParseColor(S.ColorHex) ?? Colors.White;
		ToHueLight(color, out var hue, out var light);
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = Txt("Custom colour", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		stackPanel.Children.Add(textBlock);
		GradientStopCollection gradientStopCollection = new GradientStopCollection();
		for (int i = 0; i <= 6; i++)
		{
			gradientStopCollection.Add(new GradientStop(FromHueLight(i * 60, 0.5), (double)i / 6.0));
		}
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(gradientStopCollection, new Point(0.0, 0.0), new Point(1.0, 0.0));
		linearGradientBrush.Freeze();
		LinearGradientBrush shadeBrush = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 0.0)
		};
		Ellipse chip = new Ellipse
		{
			Width = 22.0,
			Height = 22.0,
			Stroke = BuddyTheme.Track,
			StrokeThickness = 1.0
		};
		TextBox hex = PlainTextBox(13.0);
		hex.MaxLength = 7;
		hex.Width = 70.0;
		Strip hueStrip = null;
		Strip shadeStrip = null;
		bool syncing = false;
		Action<Color, bool> apply = delegate(Color c, bool fromHex)
		{
			shadeBrush.GradientStops.Clear();
			shadeBrush.GradientStops.Add(new GradientStop(Colors.Black, 0.0));
			shadeBrush.GradientStops.Add(new GradientStop(FromHueLight(hue, 0.5), 0.5));
			shadeBrush.GradientStops.Add(new GradientStop(Colors.White, 1.0));
			chip.Fill = new SolidColorBrush(c);
			string text = MessageText.ToHex(c);
			if (!fromHex)
			{
				syncing = true;
				hex.Text = text;
				syncing = false;
			}
			S.CustomColorHex = text;
			S.ColorHex = text;
			S.Finish = MessageFinish.None;
			Svc.SaveSoon();
			SyncCards();
			SchedulePreview();
		};
		hueStrip = new Strip(linearGradientBrush, hue / 360.0, delegate(double v)
		{
			hue = v * 360.0;
			apply(FromHueLight(hue, light), arg2: false);
		});
		shadeStrip = new Strip(shadeBrush, light, delegate(double v)
		{
			light = v;
			apply(FromHueLight(hue, light), arg2: false);
		});
		stackPanel.Children.Add(hueStrip);
		shadeStrip.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		stackPanel.Children.Add(shadeStrip);
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		grid.Height = 26.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		chip.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(chip);
		Border border = new Border();
		border.Height = 26.0;
		border.CornerRadius = new CornerRadius(13.0);
		border.Background = BuddyTheme.CardFill;
		border.Padding = new Thickness(10.0, 0.0, 10.0, 0.0);
		border.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		border.Child = hex;
		Border element = border;
		Grid.SetColumn(element, 1);
		grid2.Children.Add(element);
		Pill pill = new Pill("Done", filled: true, ClosePopup);
		pill.Width = 56.0;
		pill.Height = 26.0;
		Pill pill2 = pill;
		pill2.SetActive(on: true);
		Grid.SetColumn(pill2, 2);
		grid2.Children.Add(pill2);
		stackPanel.Children.Add(grid2);
		hex.TextChanged += delegate
		{
			if (!syncing)
			{
				Color? color2 = MessageText.ParseColor(hex.Text);
				if (color2.HasValue)
				{
					ToHueLight(color2.Value, out hue, out light);
					hueStrip.SetPosition(hue / 360.0);
					shadeStrip.SetPosition(light);
					apply(color2.Value, arg2: true);
				}
			}
		};
		hex.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Return || e.Key == Key.Escape)
			{
				e.Handled = true;
				ClosePopup();
			}
		};
		apply(color, arg2: false);
		OpenPopup(target, PopupSurface(212.0, stackPanel));
	}

	private void ShowDurationPopup(FrameworkElement target)
	{
		int num = MessageText.ClampSeconds(S.CustomDurationSeconds);
		bool minutes = num >= 60 && num % 60 == 0;
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = Txt("Custom duration", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		stackPanel.Children.Add(textBlock);
		Grid grid = new Grid();
		grid.Height = 26.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		TextBox box = PlainTextBox(13.0);
		box.MaxLength = 4;
		box.Text = (minutes ? (num / 60) : num).ToString(CultureInfo.InvariantCulture);
		box.PreviewTextInput += delegate(object s, TextCompositionEventArgs e)
		{
			string text2 = e.Text ?? string.Empty;
			foreach (char c in text2)
			{
				if (c < '0' || c > '9')
				{
					e.Handled = true;
					break;
				}
			}
		};
		DataObject.AddPastingHandler(box, delegate(object s, DataObjectPastingEventArgs e)
		{
			string text = (e.DataObject.GetDataPresent(DataFormats.UnicodeText) ? (e.DataObject.GetData(DataFormats.UnicodeText) as string) : null);
			if (text == null || !int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var _))
			{
				e.CancelCommand();
			}
		});
		Border border = new Border();
		border.Height = 26.0;
		border.CornerRadius = new CornerRadius(13.0);
		border.Background = BuddyTheme.CardFill;
		border.Padding = new Thickness(10.0, 0.0, 10.0, 0.0);
		border.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		border.Child = box;
		Border element = border;
		grid2.Children.Add(element);
		Border border2 = new Border();
		border2.Height = 26.0;
		border2.CornerRadius = new CornerRadius(13.0);
		border2.Background = BuddyTheme.CardFill;
		border2.Padding = new Thickness(2.0);
		Border border3 = border2;
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		StackPanel stackPanel3 = stackPanel2;
		Pill sec = null;
		Pill min = null;
		sec = new Pill("sec", filled: false, delegate
		{
			minutes = false;
			sec.SetActive(on: true);
			min.SetActive(on: false);
		})
		{
			Width = 40.0,
			Height = 22.0
		};
		min = new Pill("min", filled: false, delegate
		{
			minutes = true;
			sec.SetActive(on: false);
			min.SetActive(on: true);
		})
		{
			Width = 40.0,
			Height = 22.0
		};
		sec.SetActive(!minutes);
		min.SetActive(minutes);
		stackPanel3.Children.Add(sec);
		stackPanel3.Children.Add(min);
		border3.Child = stackPanel3;
		Grid.SetColumn(border3, 1);
		grid2.Children.Add(border3);
		stackPanel.Children.Add(grid2);
		Action commit = delegate
		{
			if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0)
			{
				SetDuration(MessageDurationMode.Custom, minutes ? (result * 60) : result);
			}
			ClosePopup();
		};
		Pill pill = new Pill("Set", filled: true, delegate
		{
			commit();
		});
		pill.Height = 26.0;
		pill.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		Pill pill2 = pill;
		pill2.SetActive(on: true);
		stackPanel.Children.Add(pill2);
		box.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Return)
			{
				e.Handled = true;
				commit();
			}
			else if (e.Key == Key.Escape)
			{
				e.Handled = true;
				ClosePopup();
			}
		};
		OpenPopup(target, PopupSurface(196.0, stackPanel));
		box.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			box.Focus();
			box.SelectAll();
		});
	}

	private void SyncAll()
	{
		if (_input.Text != S.Text)
		{
			_syncingText = true;
			_input.Text = S.Text;
			_syncingText = false;
		}
		if (Math.Abs(_speed.Value - S.Speed) > 0.5)
		{
			_speed.Value = S.Speed;
		}
		_repeat.SetCheckedSilently(S.RepeatEnabled);
		SyncCompose();
		SyncCards();
		SyncLive();
	}

	private void SyncCompose()
	{
		int num = MessageText.Count(_input.Text);
		_counter.Text = num + "/" + 60;
		_counter.Foreground = ((num >= 60) ? BuddyTheme.Text92 : BuddyTheme.Text30);
		_placeholder.Visibility = ((num != 0) ? Visibility.Collapsed : Visibility.Visible);
		bool flag = MessageText.Clean(_input.Text).Length > 0;
		_flash.Opacity = (flag ? 1.0 : 0.35);
		_flash.IsHitTestVisible = flag;
		_flash.IsEnabled = flag;
		SyncSecondary();
	}

	private void SyncSecondary()
	{
		bool isLive = Svc.IsLive;
		_secondaryText.Text = (isLive ? "Take down" : "Clear");
		bool flag = isLive || _input.Text.Length > 0;
		_secondary.Opacity = (flag ? 1.0 : 0.35);
		_secondary.IsHitTestVisible = flag;
		_secondary.IsEnabled = flag;
		_secondary.ToolTip = BuddyTooltip.Create(isLive ? "Remove the banner from the notch now" : "Empty the message");
	}

	private void SyncCards()
	{
		MessageSettings s = S;
		foreach (KeyValuePair<MessageAnimationMode, Pill> animPill in _animPills)
		{
			animPill.Value.SetActive(animPill.Key == s.Animation);
		}
		SyncSpeedLabel();
		bool flag = s.Finish == MessageFinish.None;
		bool flag2 = false;
		foreach (Swatch swatch in _swatches)
		{
			bool flag3 = flag && string.Equals((string)swatch.Tag, s.ColorHex, StringComparison.OrdinalIgnoreCase);
			flag2 = flag2 || flag3;
			swatch.SetSelected(flag3);
		}
		bool flag4 = flag && !flag2;
		Color? color = (flag4 ? MessageText.ParseColor(s.ColorHex) : MessageText.ParseColor(s.CustomColorHex));
		_customSwatch.SetFill(color.HasValue ? new SolidColorBrush(color.Value) : BuddyTheme.Control);
		_customPlus.Visibility = (color.HasValue ? Visibility.Collapsed : Visibility.Visible);
		_customSwatch.SetSelected(flag4);
		MessageFinish[] finishes = Finishes;
		foreach (MessageFinish messageFinish in finishes)
		{
			_finishSwatches[messageFinish].SetSelected(messageFinish == s.Finish);
			_finishNames[messageFinish].Visibility = ((messageFinish != s.Finish) ? Visibility.Hidden : Visibility.Visible);
		}
		foreach (KeyValuePair<MessageDurationMode, Pill> durPill in _durPills)
		{
			durPill.Value.SetActive(durPill.Key == s.Duration);
		}
		_durPills[MessageDurationMode.Custom].ToolTip = BuddyTooltip.Create((s.Duration == MessageDurationMode.Custom) ? ("Custom: " + MessageText.FormatSeconds(s.CustomDurationSeconds) + " · click to change") : "Set your own duration");
		_durHost.Opacity = (s.RepeatEnabled ? 0.35 : 1.0);
		_durHost.IsHitTestVisible = !s.RepeatEnabled;
		_durHost.IsEnabled = !s.RepeatEnabled;
		if (s.RepeatEnabled && _popup != null && _popup.PlacementTarget == _durPills[MessageDurationMode.Custom])
		{
			ClosePopup();
		}
	}

	private void SyncLive()
	{
		bool isLive = Svc.IsLive;
		_liveDot.Visibility = ((!isLive) ? Visibility.Collapsed : Visibility.Visible);
		SetDotPulse(isLive && _active);
		_stateText.Text = (isLive ? "Live on notch" : (S.RepeatEnabled ? "Stays until taken down" : ("Shows for " + MessageText.FormatSeconds((int)S.DurationSpan.TotalSeconds))));
		_stateText.Foreground = (isLive ? BuddyTheme.Text92 : BuddyTheme.Text50);
		SyncSecondary();
	}

	private static void ToHueLight(Color c, out double hue, out double light)
	{
		double num = (double)(int)c.R / 255.0;
		double num2 = (double)(int)c.G / 255.0;
		double num3 = (double)(int)c.B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = num4 - num5;
		light = (num4 + num5) / 2.0;
		if (num6 < 1E-06)
		{
			hue = 0.0;
			return;
		}
		if (num4 == num)
		{
			hue = 60.0 * ((num2 - num3) / num6 % 6.0);
		}
		else if (num4 == num2)
		{
			hue = 60.0 * ((num3 - num) / num6 + 2.0);
		}
		else
		{
			hue = 60.0 * ((num - num2) / num6 + 4.0);
		}
		if (hue < 0.0)
		{
			hue += 360.0;
		}
	}

	private static Color FromHueLight(double hue, double light)
	{
		light = Math.Max(0.0, Math.Min(1.0, light));
		double num = 1.0 - Math.Abs(2.0 * light - 1.0);
		double num2 = num * (1.0 - Math.Abs(hue / 60.0 % 2.0 - 1.0));
		double num3 = light - num / 2.0;
		double num4;
		double num5;
		double num6;
		if (hue < 60.0)
		{
			num4 = num;
			num5 = num2;
			num6 = 0.0;
		}
		else if (hue < 120.0)
		{
			num4 = num2;
			num5 = num;
			num6 = 0.0;
		}
		else if (hue < 180.0)
		{
			num4 = 0.0;
			num5 = num;
			num6 = num2;
		}
		else if (hue < 240.0)
		{
			num4 = 0.0;
			num5 = num2;
			num6 = num;
		}
		else if (hue < 300.0)
		{
			num4 = num2;
			num5 = 0.0;
			num6 = num;
		}
		else
		{
			num4 = num;
			num5 = 0.0;
			num6 = num2;
		}
		return Color.FromRgb((byte)Math.Round((num4 + num3) * 255.0), (byte)Math.Round((num5 + num3) * 255.0), (byte)Math.Round((num6 + num3) * 255.0));
	}
}
