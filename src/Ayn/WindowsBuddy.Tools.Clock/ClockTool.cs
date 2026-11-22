using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Clock;

public class ClockTool : IBuddyTool
{
	private sealed class CityRow
	{
		public WorldClockCity City;

		public TextBlock Time;

		public TextBlock Sub;
	}

	private sealed class Seg : Border
	{
		private readonly List<Border> _items = new List<Border>();

		private readonly List<TextBlock> _labels = new List<TextBlock>();

		private readonly Action<int> _picked;

		private int _index = -1;

		public Seg(string[] labels, Action<int> picked)
		{
			_picked = picked;
			base.Height = 24.0;
			base.CornerRadius = new CornerRadius(12.0);
			base.Background = BuddyTheme.Control;
			base.Padding = new Thickness(2.0);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			for (int i = 0; i < labels.Length; i++)
			{
				int idx = i;
				TextBlock textBlock = Txt(labels[i], 11.5, BuddyTheme.Text50, AynType.Regular);
				textBlock.HorizontalAlignment = HorizontalAlignment.Center;
				Border border = new Border
				{
					Height = 20.0,
					MinWidth = 52.0,
					Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
					CornerRadius = new CornerRadius(10.0),
					BorderThickness = new Thickness(1.0),
					Cursor = Cursors.Hand,
					Focusable = true,
					FocusVisualStyle = null,
					Child = textBlock
				};
				border.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
				};
				border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
					Pick(idx);
				};
				border.MouseEnter += delegate
				{
					Paint();
				};
				border.MouseLeave += delegate
				{
					Paint();
				};
				border.GotKeyboardFocus += delegate
				{
					Paint();
				};
				border.LostKeyboardFocus += delegate
				{
					Paint();
				};
				border.KeyDown += delegate(object s, KeyEventArgs e)
				{
					if (e.Key == Key.Return || e.Key == Key.Space)
					{
						e.Handled = true;
						Pick(idx);
					}
				};
				_items.Add(border);
				_labels.Add(textBlock);
				stackPanel.Children.Add(border);
			}
			Child = stackPanel;
			Paint();
		}

		private void Pick(int i)
		{
			if (i != _index)
			{
				Select(i);
				_picked?.Invoke(i);
			}
		}

		public void Select(int i)
		{
			_index = i;
			Paint();
		}

		private void Paint()
		{
			for (int i = 0; i < _items.Count; i++)
			{
				bool flag = i == _index;
				bool isKeyboardFocused = _items[i].IsKeyboardFocused;
				_items[i].Background = (flag ? BuddyTheme.SelectedFill : (_items[i].IsMouseOver ? BuddyTheme.ControlActive : Brushes.Transparent));
				_items[i].BorderBrush = (isKeyboardFocused ? BuddyTheme.FocusRing : (flag ? BuddyTheme.SelectedEdge : Brushes.Transparent));
				_labels[i].Foreground = (flag ? BuddyTheme.SelectedInk : BuddyTheme.Text50);
				_labels[i].FontWeight = (flag ? FontWeights.SemiBold : AynType.Regular);
			}
		}
	}

	private sealed class LinkButton : Border
	{
		private readonly TextBlock _t;

		public LinkButton(string text, double size, string tip, Action click)
		{
			LinkButton linkButton = this;
			_t = Txt(text, size, BuddyTheme.Text50, AynType.Medium);
			Child = _t;
			base.Padding = new Thickness(4.0, 1.0, 4.0, 1.0);
			base.CornerRadius = new CornerRadius(6.0);
			base.BorderThickness = new Thickness(1.0);
			base.BorderBrush = Brushes.Transparent;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			base.Focusable = true;
			base.FocusVisualStyle = null;
			base.VerticalAlignment = VerticalAlignment.Center;
			if (tip != null)
			{
				base.ToolTip = BuddyTooltip.Create(tip);
			}
			base.MouseEnter += delegate
			{
				Paint();
			};
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
			base.IsEnabledChanged += delegate
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
				if (click != null)
				{
					click();
				}
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
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
			ToolTipService.SetShowOnDisabled(this, value: true);
		}

		private void Paint()
		{
			_t.Foreground = ((!base.IsEnabled) ? BuddyTheme.Text30 : ((base.IsMouseOver || base.IsKeyboardFocused) ? BuddyTheme.Text100 : BuddyTheme.Text50));
			base.BorderBrush = (base.IsKeyboardFocused ? BuddyTheme.FocusRing : Brushes.Transparent);
		}
	}

	private sealed class IconButton : Border
	{
		public IconButton(string icon, double size, string tip, Action click)
		{
			IconButton iconButton = this;
			base.Width = 20.0;
			base.Height = 20.0;
			base.CornerRadius = new CornerRadius(10.0);
			base.BorderThickness = new Thickness(1.0);
			base.BorderBrush = Brushes.Transparent;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			base.Focusable = true;
			base.FocusVisualStyle = null;
			base.VerticalAlignment = VerticalAlignment.Center;
			FrameworkElement frameworkElement = FluentIcons.Create(icon, size, BuddyTheme.Text50, filled: false);
			frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			Child = frameworkElement;
			base.ToolTip = BuddyTooltip.Create(tip);
			MouseEventHandler value = delegate
			{
				base.Background = BuddyTheme.ControlActive;
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				base.Background = Brushes.Transparent;
			};
			base.GotKeyboardFocus += delegate
			{
				base.BorderBrush = BuddyTheme.FocusRing;
			};
			base.LostKeyboardFocus += delegate
			{
				base.BorderBrush = Brushes.Transparent;
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				click();
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.Space)
				{
					e.Handled = true;
					click();
				}
			};
		}
	}

	private const double HeaderH = 24.0;

	private const double BodyTop = 36.0;

	private const double RowH = 24.0;

	private const double RowGap = 6.0;

	private const double DialSize = 160.0;

	private const double CityRowH = 34.0;

	private ClockSettings _settings;

	private EventHandler<DateTime> _secondTickHandler;

	private EventHandler _systemTimeChangedHandler;

	private EventHandler _settingsChangedHandler;

	private bool _active;

	private Grid _root;

	private Seg _modeSeg;

	private FrameworkElement _digitalPanel;

	private FrameworkElement _analogPanel;

	private TextBlock _heroTime;

	private TextBlock _heroDate;

	private TextBlock _heroZone;

	private TextBlock _formatValue;

	private BuddyToggle _secondsToggle;

	private BuddyToggle _dateToggle;

	private Seg _dateSeg;

	private Canvas _dial;

	private RotateTransform _hourRot;

	private RotateTransform _minuteRot;

	private RotateTransform _secondRot;

	private Line _secondHand;

	private readonly List<UIElement> _dialMarks = new List<UIElement>();

	private BuddyToggle _secondHandToggle;

	private BuddyToggle _numeralsToggle;

	private BuddyToggle _ticksToggle;

	private StackPanel _cityList;

	private TextBlock _cityHint;

	private LinkButton _addCity;

	private readonly List<CityRow> _cityRows = new List<CityRow>();

	private Popup _addCityPopup;

	private TextBox _citySearchBox;

	private TextBlock _citySearchPlaceholder;

	private StackPanel _cityResultsPanel;

	private List<TimeZoneOption> _filteredTimezones = new List<TimeZoneOption>();

	private int _selectedResultIndex = -1;

	public string Id => "clock";

	public string DisplayName => "Clock";

	public ToolCategory Category => ToolCategory.Core;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public ClockTool()
	{
		IsEnabled = true;
		OrderIndex = 1;
		State = ToolState.Idle;
		_settings = ClockSettings.Instance;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_settings = ClockSettings.Instance;
		_settingsChangedHandler = delegate
		{
			OnUi(OnSettingsChanged);
		};
		_settings.SettingsChanged += _settingsChangedHandler;
		_secondTickHandler = delegate(object s, DateTime now)
		{
			if (_active)
			{
				OnUi(delegate
				{
					UpdateTick(now);
				});
			}
		};
		ClockService.Instance.SecondTick += _secondTickHandler;
		_systemTimeChangedHandler = delegate
		{
			OnUi(delegate
			{
				if (_root != null)
				{
					UpdateZoneLabel();
					RebuildCityRows();
					UpdateTick(DateTime.Now);
				}
			});
		};
		ClockService.Instance.SystemTimeChanged += _systemTimeChangedHandler;
		if (_settings.ShowInNotch)
		{
			SyncNotchClockActivity();
		}
	}

	public void Shutdown()
	{
		if (IsInitialized)
		{
			if (_secondTickHandler != null)
			{
				ClockService.Instance.SecondTick -= _secondTickHandler;
				_secondTickHandler = null;
			}
			if (_systemTimeChangedHandler != null)
			{
				ClockService.Instance.SystemTimeChanged -= _systemTimeChangedHandler;
				_systemTimeChangedHandler = null;
			}
			if (_settingsChangedHandler != null && _settings != null)
			{
				_settings.SettingsChanged -= _settingsChangedHandler;
				_settingsChangedHandler = null;
			}
			CloseAddCity();
			_active = false;
			IsInitialized = false;
		}
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		_active = true;
		if (_root != null)
		{
			SyncControls();
			UpdateTick(DateTime.Now);
		}
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		_active = false;
		CloseAddCity();
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateClockIcon(size, stroke);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return new CollapsedBadgeData(_settings.FormatTime(DateTime.Now, _settings.ShowSeconds));
	}

	private static void OnUi(Action a)
	{
		Action action = delegate
		{
			try
			{
				a();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Clock tab update failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		};
		Application current = Application.Current;
		if (current != null && !current.Dispatcher.CheckAccess())
		{
			current.Dispatcher.BeginInvoke(action);
		}
		else
		{
			action();
		}
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
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(3.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(2.0, GridUnitType.Star)
		});
		grid2.Children.Add(BuildClockCard());
		FrameworkElement element = BuildWorldCard();
		Grid.SetColumn(element, 2);
		grid2.Children.Add(element);
		_root = grid2;
		SyncControls();
		UpdateTick(DateTime.Now);
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
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		return textBlock;
	}

	private static T At<T>(T el, double top) where T : FrameworkElement
	{
		el.VerticalAlignment = VerticalAlignment.Top;
		el.Margin = new Thickness(el.Margin.Left, top, el.Margin.Right, 0.0);
		return el;
	}

	private FrameworkElement BuildClockCard()
	{
		Grid grid = new Grid();
		Grid grid2 = new Grid();
		grid2.Height = 24.0;
		grid2.VerticalAlignment = VerticalAlignment.Top;
		Grid grid3 = grid2;
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		FrameworkElement frameworkElement = BuddyTheme.CardHeader("clock", "Clock");
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid3.Children.Add(frameworkElement);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(Txt("This view only ·", 11.0, BuddyTheme.Text30, AynType.Regular));
		LinkButton linkButton = new LinkButton("Notch style", 11.0, "Change how the notch shows the time", delegate
		{
			OpenSettings("notch");
		});
		linkButton.Margin = new Thickness(3.0, 0.0, 0.0, 0.0);
		stackPanel2.Children.Add(linkButton);
		Grid.SetColumn(stackPanel2, 1);
		grid3.Children.Add(stackPanel2);
		_modeSeg = new Seg(new string[2] { "Digital", "Analog" }, delegate(int i)
		{
			_settings.DisplayMode = ((i == 1) ? ClockDisplayMode.Analog : ClockDisplayMode.Digital);
			SyncNotchClockActivity();
		});
		_modeSeg.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_modeSeg, 2);
		grid3.Children.Add(_modeSeg);
		grid.Children.Add(grid3);
		_digitalPanel = BuildDigitalPanel();
		grid.Children.Add(_digitalPanel);
		_analogPanel = BuildAnalogPanel();
		grid.Children.Add(_analogPanel);
		return Card(grid);
	}

	private FrameworkElement BuildDigitalPanel()
	{
		Grid grid = new Grid();
		_heroTime = At(BuddyTheme.HeroText("--:--", 28.0), 36.0);
		_heroTime.HorizontalAlignment = HorizontalAlignment.Left;
		grid.Children.Add(_heroTime);
		_heroDate = At(Txt("", 11.5, BuddyTheme.Text50, AynType.Regular), 68.0);
		_heroDate.TextTrimming = TextTrimming.CharacterEllipsis;
		grid.Children.Add(_heroDate);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		StackPanel stackPanel2 = At(stackPanel, 86.0);
		Canvas canvas = VectorIcons.CreateLocationPinIcon(11.0, BuddyTheme.Text30);
		canvas.VerticalAlignment = VerticalAlignment.Center;
		canvas.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
		stackPanel2.Children.Add(canvas);
		_heroZone = Txt("", 11.0, BuddyTheme.Text30, AynType.Regular);
		_heroZone.TextTrimming = TextTrimming.CharacterEllipsis;
		_heroZone.MaxWidth = 340.0;
		stackPanel2.Children.Add(_heroZone);
		grid.Children.Add(stackPanel2);
		UpdateZoneLabel();
		grid.Children.Add(At(new Border
		{
			Height = 1.0,
			Background = BuddyTheme.Hairline
		}, 110.0));
		double num = 120.0;
		Grid grid2 = Row(num, "Time format");
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		_formatValue = Txt("", 13.0, BuddyTheme.Text92, AynType.Medium);
		stackPanel4.Children.Add(_formatValue);
		LinkButton linkButton = new LinkButton("Change", 11.5, "Settings › General › Time format (used everywhere)", delegate
		{
			OpenSettings("general");
		});
		linkButton.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		stackPanel4.Children.Add(linkButton);
		Grid.SetColumn(stackPanel4, 1);
		grid2.Children.Add(stackPanel4);
		grid.Children.Add(grid2);
		num += 30.0;
		Grid grid3 = new Grid();
		grid3.Height = 24.0;
		Grid grid4 = At(grid3, num);
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(24.0)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_secondsToggle = new BuddyToggle
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_secondsToggle.Toggled += delegate(object s, bool on)
		{
			_settings.ShowSeconds = on;
			UpdateTick(DateTime.Now);
		};
		grid4.Children.Add(ToggleCell("Show seconds", _secondsToggle));
		_dateToggle = new BuddyToggle
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_dateToggle.Toggled += delegate(object s, bool on)
		{
			_settings.ShowDate = on;
			SyncControls();
		};
		Grid element = ToggleCell("Show date", _dateToggle);
		Grid.SetColumn(element, 2);
		grid4.Children.Add(element);
		grid.Children.Add(grid4);
		num += 30.0;
		Grid grid5 = Row(num, "Date style");
		_dateSeg = new Seg(new string[3] { "Full", "Short", "Compact" }, delegate(int i)
		{
			_settings.DateFormat = i switch
			{
				2 => ClockDateFormat.Compact, 
				1 => ClockDateFormat.Standard, 
				_ => ClockDateFormat.Full, 
			};
			UpdateTick(DateTime.Now);
		});
		_dateSeg.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_dateSeg, 1);
		grid5.Children.Add(_dateSeg);
		grid.Children.Add(grid5);
		return grid;
	}

	private static Grid Row(double top, string label)
	{
		Grid grid = new Grid();
		grid.Height = 24.0;
		Grid grid2 = At(grid, top);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.Children.Add(Txt(label, 11.5, BuddyTheme.Text50, AynType.Medium));
		return grid2;
	}

	private static Grid ToggleCell(string label, BuddyToggle toggle)
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.Children.Add(Txt(label, 11.5, BuddyTheme.Text50, AynType.Medium));
		Grid.SetColumn(toggle, 1);
		grid.Children.Add(toggle);
		return grid;
	}

	private void UpdateZoneLabel()
	{
		if (_heroZone != null)
		{
			string text = TimeZoneInfo.Local.DisplayName ?? string.Empty;
			int num = text.IndexOf(')');
			string text2 = ((num > 0) ? text.Substring(0, num + 1) : string.Empty);
			_heroZone.Text = ClockService.Instance.GetTimeZoneAbbreviation(TimeZoneInfo.Local.Id) + ((text2.Length > 0) ? (" " + text2) : string.Empty);
			_heroZone.ToolTip = BuddyTooltip.Create(text);
		}
	}

	private static void OpenSettings(string page)
	{
		CapsuleWindow.Instance?.OpenSettings(page);
	}

	private FrameworkElement BuildAnalogPanel()
	{
		Grid grid = new Grid();
		Border border = new Border();
		border.Width = 160.0;
		border.Height = 160.0;
		border.CornerRadius = new CornerRadius(80.0);
		border.Background = BuddyTheme.Control;
		border.BorderBrush = BuddyTheme.Hairline;
		border.BorderThickness = new Thickness(1.0);
		border.HorizontalAlignment = HorizontalAlignment.Left;
		Border border2 = At(border, 46.0);
		_dial = new Canvas
		{
			Width = 158.0,
			Height = 158.0
		};
		border2.Child = _dial;
		BuildDial();
		grid.Children.Add(border2);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(184.0, 80.0, 0.0, 0.0);
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		StackPanel stackPanel2 = stackPanel;
		_secondHandToggle = new BuddyToggle
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_secondHandToggle.Toggled += delegate(object s, bool on)
		{
			_settings.AnalogShowSecondHand = on;
		};
		_numeralsToggle = new BuddyToggle
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_numeralsToggle.Toggled += delegate(object s, bool on)
		{
			_settings.AnalogShowNumerals = on;
		};
		_ticksToggle = new BuddyToggle
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_ticksToggle.Toggled += delegate(object s, bool on)
		{
			_settings.AnalogShowMinuteTicks = on;
		};
		Tuple<string, BuddyToggle>[] array = new Tuple<string, BuddyToggle>[3]
		{
			Tuple.Create("Second hand", _secondHandToggle),
			Tuple.Create("Numerals", _numeralsToggle),
			Tuple.Create("Minute ticks", _ticksToggle)
		};
		foreach (Tuple<string, BuddyToggle> tuple in array)
		{
			Grid grid2 = ToggleCell(tuple.Item1, tuple.Item2);
			grid2.Height = 24.0;
			if (stackPanel2.Children.Count > 0)
			{
				grid2.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
			}
			stackPanel2.Children.Add(grid2);
		}
		grid.Children.Add(stackPanel2);
		return grid;
	}

	private void BuildDial()
	{
		_dial.Children.Clear();
		_dialMarks.Clear();
		double num = _dial.Width / 2.0;
		BuildDialMarks();
		_hourRot = new RotateTransform();
		_minuteRot = new RotateTransform();
		_secondRot = new RotateTransform();
		_dial.Children.Add(Hand(2.0, -36.0, 2.4, BuddyTheme.Text100, _hourRot, num));
		_dial.Children.Add(Hand(3.0, -54.0, 1.5, BuddyTheme.Text100, _minuteRot, num));
		_secondHand = Hand(10.0, -62.0, 1.0, BuddyTheme.Mark, _secondRot, num);
		_dial.Children.Add(_secondHand);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 4.0;
		ellipse.Height = 4.0;
		ellipse.Fill = BuddyTheme.Text100;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, num - 2.0);
		Canvas.SetTop(element, num - 2.0);
		_dial.Children.Add(element);
	}

	private static Line Hand(double tail, double tip, double width, Brush ink, RotateTransform rot, double c)
	{
		Line line = new Line();
		line.X1 = 0.0;
		line.Y1 = tail;
		line.X2 = 0.0;
		line.Y2 = tip;
		line.Stroke = ink;
		line.StrokeThickness = width;
		line.StrokeStartLineCap = PenLineCap.Round;
		line.StrokeEndLineCap = PenLineCap.Round;
		line.RenderTransform = rot;
		Line line2 = line;
		Canvas.SetLeft(line2, c);
		Canvas.SetTop(line2, c);
		return line2;
	}

	private void BuildDialMarks()
	{
		foreach (UIElement dialMark in _dialMarks)
		{
			_dial.Children.Remove(dialMark);
		}
		_dialMarks.Clear();
		double num = _dial.Width / 2.0;
		double num2 = num - 6.0;
		bool analogShowMinuteTicks = _settings.AnalogShowMinuteTicks;
		bool analogShowNumerals = _settings.AnalogShowNumerals;
		int num3 = 0;
		for (int i = 0; i < 60; i++)
		{
			bool flag = i % 5 == 0;
			if (flag || analogShowMinuteTicks)
			{
				double num4 = (double)i * 6.0 * Math.PI / 180.0;
				double num5 = (flag ? 6.0 : 3.0);
				Line line = new Line();
				line.X1 = num + num2 * Math.Sin(num4);
				line.Y1 = num - num2 * Math.Cos(num4);
				line.X2 = num + (num2 - num5) * Math.Sin(num4);
				line.Y2 = num - (num2 - num5) * Math.Cos(num4);
				line.Stroke = (flag ? BuddyTheme.Text92 : BuddyTheme.Text30);
				line.StrokeThickness = (flag ? 1.4 : 0.8);
				Line line2 = line;
				_dial.Children.Insert(num3++, line2);
				_dialMarks.Add(line2);
			}
		}
		if (analogShowNumerals)
		{
			double num6 = num2 - 17.0;
			for (int j = 1; j <= 12; j++)
			{
				double num7 = (double)j * 30.0 * Math.PI / 180.0;
				TextBlock textBlock = Txt(j.ToString(CultureInfo.InvariantCulture), 10.0, BuddyTheme.Text50, AynType.Regular);
				textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				Canvas.SetLeft(textBlock, num + num6 * Math.Sin(num7) - textBlock.DesiredSize.Width / 2.0);
				Canvas.SetTop(textBlock, num - num6 * Math.Cos(num7) - textBlock.DesiredSize.Height / 2.0);
				_dial.Children.Insert(num3++, textBlock);
				_dialMarks.Add(textBlock);
			}
		}
	}

	private FrameworkElement BuildWorldCard()
	{
		Grid grid = new Grid();
		Grid grid2 = new Grid();
		grid2.Height = 24.0;
		grid2.VerticalAlignment = VerticalAlignment.Top;
		Grid grid3 = grid2;
		FrameworkElement frameworkElement = BuddyTheme.CardHeader("globe", "World clocks");
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid3.Children.Add(frameworkElement);
		_addCity = new LinkButton("+ Add city", 11.5, null, delegate
		{
			OpenAddCity(_addCity);
		});
		_addCity.HorizontalAlignment = HorizontalAlignment.Right;
		grid3.Children.Add(_addCity);
		grid.Children.Add(grid3);
		ScrollViewer scrollViewer = new ScrollViewer();
		scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		scrollViewer.Margin = new Thickness(0.0, 32.0, 0.0, 0.0);
		scrollViewer.Focusable = false;
		ScrollViewer scrollViewer2 = scrollViewer;
		_cityList = new StackPanel();
		scrollViewer2.Content = _cityList;
		grid.Children.Add(scrollViewer2);
		_cityHint = Txt("Add a city to compare times", 11.5, BuddyTheme.Text50, AynType.Regular);
		_cityHint.VerticalAlignment = VerticalAlignment.Top;
		_cityHint.Margin = new Thickness(0.0, 76.0, 0.0, 0.0);
		grid.Children.Add(_cityHint);
		RebuildCityRows();
		return Card(grid);
	}

	private void RebuildCityRows()
	{
		if (_cityList == null)
		{
			return;
		}
		_cityList.Children.Clear();
		_cityRows.Clear();
		IReadOnlyList<WorldClockCity> worldClocks = _settings.WorldClocks;
		for (int i = 0; i < worldClocks.Count; i++)
		{
			if (i > 0)
			{
				_cityList.Children.Add(new Border
				{
					Height = 1.0,
					Background = BuddyTheme.Hairline
				});
			}
			_cityList.Children.Add(BuildCityRow(worldClocks[i]));
		}
		_cityHint.Visibility = ((worldClocks.Count > 1) ? Visibility.Collapsed : Visibility.Visible);
		bool canAddWorldClock = _settings.CanAddWorldClock;
		_addCity.IsEnabled = canAddWorldClock;
		_addCity.ToolTip = BuddyTooltip.Create(canAddWorldClock ? "Add a city" : ("Up to " + 5 + " cities. Remove one to add another"));
		UpdateCityTimes();
	}

	private FrameworkElement BuildCityRow(WorldClockCity city)
	{
		Grid row = new Grid
		{
			Height = 34.0,
			Background = Brushes.Transparent
		};
		row.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		row.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		row.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(22.0)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		TextBlock textBlock = Txt(city.IsLocal ? "Local" : city.CityName, 13.0, BuddyTheme.Text92, AynType.Medium);
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel2.Children.Add(textBlock);
		TextBlock textBlock2 = Txt("", 11.0, BuddyTheme.Text30, AynType.Regular);
		textBlock2.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel2.Children.Add(textBlock2);
		row.Children.Add(stackPanel2);
		TextBlock textBlock3 = Txt("", 13.0, BuddyTheme.Text92, AynType.Medium);
		Typography.SetNumeralAlignment(textBlock3, FontNumeralAlignment.Tabular);
		textBlock3.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		Grid.SetColumn(textBlock3, 1);
		row.Children.Add(textBlock3);
		_cityRows.Add(new CityRow
		{
			City = city,
			Time = textBlock3,
			Sub = textBlock2
		});
		if (!city.IsLocal)
		{
			string id = city.Id;
			IconButton del = new IconButton("dismiss", 10.0, "Remove " + city.CityName, delegate
			{
				_settings.RemoveWorldClock(id);
			});
			del.HorizontalAlignment = HorizontalAlignment.Right;
			del.Opacity = 0.0;
			Grid.SetColumn(del, 2);
			row.Children.Add(del);
			Action paint = delegate
			{
				del.Opacity = ((row.IsMouseOver || del.IsKeyboardFocused) ? 1 : 0);
			};
			row.MouseEnter += delegate
			{
				paint();
			};
			row.MouseLeave += delegate
			{
				paint();
			};
			del.GotKeyboardFocus += delegate
			{
				paint();
			};
			del.LostKeyboardFocus += delegate
			{
				paint();
			};
		}
		return row;
	}

	private void UpdateCityTimes()
	{
		DateTime now = DateTime.Now;
		foreach (CityRow cityRow in _cityRows)
		{
			string text = (cityRow.City.IsLocal ? TimeZoneInfo.Local.Id : cityRow.City.TimeZoneId);
			string text2;
			string text3;
			if (!ClockService.Instance.TryGetCityTime(cityRow.City.IsLocal ? null : text, out var time))
			{
				text2 = "--";
				text3 = "Time zone not on this PC";
			}
			else
			{
				text2 = _settings.FormatTime(time, includeSeconds: false);
				text3 = ClockService.Instance.GetTimeZoneAbbreviation(text) + (cityRow.City.IsLocal ? string.Empty : (" · " + Offset(time, now)));
			}
			if (cityRow.Time.Text != text2)
			{
				cityRow.Time.Text = text2;
			}
			if (cityRow.Sub.Text != text3)
			{
				cityRow.Sub.Text = text3;
			}
		}
	}

	private static string Offset(DateTime city, DateTime local)
	{
		int num = (int)Math.Round((city - local).TotalMinutes / 15.0) * 15;
		string text;
		if (num == 0)
		{
			text = "Same time";
		}
		else
		{
			int num2 = Math.Abs(num);
			text = ((num > 0) ? "+" : "−") + num2 / 60 + "h" + ((num2 % 60 != 0) ? (" " + num2 % 60 + "m") : string.Empty);
		}
		int days = (city.Date - local.Date).Days;
		if (days > 0)
		{
			text += ", tomorrow";
		}
		else if (days < 0)
		{
			text += ", yesterday";
		}
		return text;
	}

	private void OpenAddCity(FrameworkElement target)
	{
		if (!_settings.CanAddWorldClock)
		{
			return;
		}
		if (_addCityPopup == null)
		{
			_addCityPopup = new Popup
			{
				Placement = PlacementMode.Bottom,
				PlacementTarget = target,
				VerticalOffset = 6.0,
				StaysOpen = false,
				AllowsTransparency = true,
				PopupAnimation = PopupAnimation.None
			};
			Grid grid = new Grid();
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			Border border = new Border();
			border.Height = 30.0;
			border.CornerRadius = new CornerRadius(15.0);
			border.Background = BuddyTheme.CardFill;
			border.Padding = new Thickness(12.0, 0.0, 12.0, 0.0);
			border.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
			Border border2 = border;
			Grid grid2 = new Grid();
			_citySearchPlaceholder = Txt("Search city or time zone", 13.0, BuddyTheme.Placeholder, AynType.Regular);
			_citySearchPlaceholder.IsHitTestVisible = false;
			grid2.Children.Add(_citySearchPlaceholder);
			_citySearchBox = new TextBox
			{
				FontSize = 13.0,
				FontFamily = BuddyTheme.Font,
				Foreground = BuddyTheme.Text92,
				CaretBrush = BuddyTheme.Text100,
				SelectionBrush = BuddyTheme.Text50,
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0.0),
				VerticalAlignment = VerticalAlignment.Center,
				MaxLength = 60,
				FocusVisualStyle = null
			};
			_citySearchBox.TextChanged += delegate
			{
				_citySearchPlaceholder.Visibility = ((!string.IsNullOrEmpty(_citySearchBox.Text)) ? Visibility.Collapsed : Visibility.Visible);
				PopulateCityResults(_citySearchBox.Text);
			};
			_citySearchBox.PreviewKeyDown += OnSearchKeyDown;
			grid2.Children.Add(_citySearchBox);
			border2.Child = grid2;
			grid.Children.Add(border2);
			ScrollViewer scrollViewer = new ScrollViewer();
			scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
			scrollViewer.Focusable = false;
			ScrollViewer scrollViewer2 = scrollViewer;
			_cityResultsPanel = new StackPanel();
			scrollViewer2.Content = _cityResultsPanel;
			Grid.SetRow(scrollViewer2, 1);
			grid.Children.Add(scrollViewer2);
			_addCityPopup.Child = BuddyTheme.Crisp(new Border
			{
				Width = 280.0,
				Height = 260.0,
				Background = BuddyTheme.Control,
				BorderBrush = BuddyTheme.ControlActive,
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(14.0),
				Padding = new Thickness(10.0),
				Child = grid
			});
		}
		_citySearchBox.Text = string.Empty;
		PopulateCityResults(string.Empty);
		_addCityPopup.IsOpen = true;
		_citySearchBox.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			_citySearchBox.Focus();
		});
	}

	private void CloseAddCity()
	{
		if (_addCityPopup != null && _addCityPopup.IsOpen)
		{
			_addCityPopup.IsOpen = false;
		}
	}

	private void OnSearchKeyDown(object sender, KeyEventArgs e)
	{
		int count = _filteredTimezones.Count;
		if (e.Key == Key.Escape)
		{
			CloseAddCity();
			e.Handled = true;
		}
		else if (e.Key == Key.Down)
		{
			if (count > 0)
			{
				_selectedResultIndex = Math.Min(_selectedResultIndex + 1, count - 1);
				HighlightResult();
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Up)
		{
			if (count > 0)
			{
				_selectedResultIndex = Math.Max(_selectedResultIndex - 1, 0);
				HighlightResult();
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Return || e.Key == Key.Return)
		{
			if (_selectedResultIndex >= 0 && _selectedResultIndex < count)
			{
				SelectCity(_filteredTimezones[_selectedResultIndex]);
			}
			e.Handled = true;
		}
	}

	private void PopulateCityResults(string filter)
	{
		if (_cityResultsPanel == null)
		{
			return;
		}
		_cityResultsPanel.Children.Clear();
		_filteredTimezones = ClockService.Instance.SearchCitiesAndTimeZones(filter).Take(25).ToList();
		HashSet<string> hashSet = new HashSet<string>(from c in _settings.WorldClocks
			where !c.IsLocal
			select c.CityName + "|" + c.TimeZoneId, StringComparer.OrdinalIgnoreCase);
		_selectedResultIndex = -1;
		if (_filteredTimezones.Count == 0)
		{
			TextBlock textBlock = Txt("No matching city or time zone", 11.5, BuddyTheme.Text50, AynType.Regular);
			textBlock.Margin = new Thickness(8.0, 8.0, 0.0, 0.0);
			_cityResultsPanel.Children.Add(textBlock);
			return;
		}
		for (int i = 0; i < _filteredTimezones.Count; i++)
		{
			TimeZoneOption timeZoneOption = _filteredTimezones[i];
			bool added = hashSet.Contains(timeZoneOption.CitySuggestion + "|" + timeZoneOption.Id);
			if (!added && _selectedResultIndex < 0)
			{
				_selectedResultIndex = i;
			}
			Border border = new Border();
			border.Height = 30.0;
			border.CornerRadius = new CornerRadius(8.0);
			border.Padding = new Thickness(8.0, 0.0, 8.0, 0.0);
			border.Background = Brushes.Transparent;
			border.Cursor = (added ? Cursors.Arrow : Cursors.Hand);
			border.Tag = added;
			Border border2 = border;
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = Orientation.Horizontal;
			stackPanel.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel2 = stackPanel;
			TextBlock textBlock2 = Txt(timeZoneOption.CitySuggestion, 13.0, added ? BuddyTheme.Text30 : BuddyTheme.Text92, AynType.Medium);
			textBlock2.TextTrimming = TextTrimming.CharacterEllipsis;
			textBlock2.MaxWidth = 170.0;
			stackPanel2.Children.Add(textBlock2);
			TextBlock textBlock3 = Txt(ClockService.Instance.GetTimeZoneAbbreviation(timeZoneOption.Id), 11.0, BuddyTheme.Text30, AynType.Regular);
			textBlock3.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock3);
			grid.Children.Add(stackPanel2);
			if (added)
			{
				TextBlock element = Txt("Added", 11.0, BuddyTheme.Text30, AynType.Medium);
				Grid.SetColumn(element, 1);
				grid.Children.Add(element);
			}
			border2.Child = grid;
			int idx = i;
			TimeZoneOption option = timeZoneOption;
			border2.MouseEnter += delegate
			{
				if (!added)
				{
					_selectedResultIndex = idx;
					HighlightResult();
				}
			};
			border2.MouseLeftButtonUp += delegate
			{
				if (!added)
				{
					SelectCity(option);
				}
			};
			_cityResultsPanel.Children.Add(border2);
		}
		HighlightResult();
	}

	private void HighlightResult()
	{
		if (_cityResultsPanel == null)
		{
			return;
		}
		for (int i = 0; i < _cityResultsPanel.Children.Count; i++)
		{
			if (_cityResultsPanel.Children[i] is Border border)
			{
				border.Background = ((i == _selectedResultIndex && (!(border.Tag is bool) || !(bool)border.Tag)) ? BuddyTheme.ControlActive : Brushes.Transparent);
			}
		}
	}

	private void SelectCity(TimeZoneOption tz)
	{
		if (tz != null)
		{
			_settings.AddWorldClock(tz.CitySuggestion, tz.Id);
			CloseAddCity();
		}
	}

	private void OnSettingsChanged()
	{
		if (_root == null)
		{
			SyncNotchClockActivity();
			return;
		}
		SyncControls();
		BuildDialMarks();
		RebuildCityRows();
		UpdateTick(DateTime.Now);
		SyncNotchClockActivity();
	}

	private void SyncControls()
	{
		if (_root != null)
		{
			bool flag = _settings.DisplayMode == ClockDisplayMode.Analog;
			_modeSeg.Select(flag ? 1 : 0);
			_digitalPanel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			_analogPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			_formatValue.Text = ((_settings.Format == ClockFormat.TwentyFourHour) ? "24-hour" : "12-hour") + (_settings.FollowSystemFormat ? " (Windows)" : string.Empty);
			_secondsToggle.SetCheckedSilently(_settings.ShowSeconds);
			_dateToggle.SetCheckedSilently(_settings.ShowDate);
			_heroDate.Visibility = ((!_settings.ShowDate) ? Visibility.Hidden : Visibility.Visible);
			_dateSeg.Select((_settings.DateFormat == ClockDateFormat.Standard) ? 1 : ((_settings.DateFormat == ClockDateFormat.Compact) ? 2 : 0));
			_dateSeg.IsEnabled = _settings.ShowDate;
			_dateSeg.Opacity = (_settings.ShowDate ? 1.0 : 0.4);
			_secondHandToggle.SetCheckedSilently(_settings.AnalogShowSecondHand);
			_numeralsToggle.SetCheckedSilently(_settings.AnalogShowNumerals);
			_ticksToggle.SetCheckedSilently(_settings.AnalogShowMinuteTicks);
			if (_secondHand != null)
			{
				_secondHand.Visibility = ((!_settings.AnalogShowSecondHand) ? Visibility.Collapsed : Visibility.Visible);
			}
		}
	}

	private void UpdateTick(DateTime now)
	{
		if (_root == null)
		{
			return;
		}
		if (_settings.DisplayMode == ClockDisplayMode.Analog)
		{
			if (_secondRot != null && _settings.AnalogShowSecondHand)
			{
				_secondRot.Angle = (double)now.Second * 6.0;
			}
			if (_minuteRot != null)
			{
				_minuteRot.Angle = ((double)now.Minute + (double)now.Second / 60.0) * 6.0;
			}
			if (_hourRot != null)
			{
				_hourRot.Angle = ((double)(now.Hour % 12) + (double)now.Minute / 60.0) * 30.0;
			}
		}
		else
		{
			string text = _settings.FormatTime(now, _settings.ShowSeconds);
			if (_heroTime.Text != text)
			{
				_heroTime.Text = text;
			}
			if (_settings.ShowDate)
			{
				string text2 = _settings.FormatDate(now);
				if (_heroDate.Text != text2)
				{
					_heroDate.Text = text2;
				}
			}
		}
		UpdateCityTimes();
	}

	public static void SyncNotchClockActivity()
	{
		ClockSettings instance = ClockSettings.Instance;
		if (!instance.ShowInNotch)
		{
			DynamicActivityService.Instance.ClearClockActivity();
			return;
		}
		NotchActivity notchActivity = new NotchActivity();
		notchActivity.Id = "persistent_clock_activity";
		notchActivity.Type = ActivityType.Clock;
		notchActivity.Priority = ActivityPriority.P4_Ambient;
		notchActivity.Mode = ActivityMode.Persistent;
		notchActivity.Profile = CollapsedActivityProfile.Clock;
		notchActivity.Payload = "digital";
		notchActivity.Title = instance.GetCurrentFormattedTime();
		notchActivity.Subtitle = "Clock";
		notchActivity.NavigateToToolId = "clock";
		NotchActivity activity = notchActivity;
		DynamicActivityService.Instance.PublishClockActivity(activity);
	}
}
