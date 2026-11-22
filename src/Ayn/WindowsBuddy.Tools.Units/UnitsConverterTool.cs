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
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Units;

public class UnitsConverterTool : IBuddyTool, IEscapeAware
{
	private enum Side
	{
		From,
		To
	}

	private sealed class CategoryRow : Grid
	{
		private readonly Rectangle _bar;

		private readonly TextBlock _name;

		private readonly ContentControl _glyph;

		private readonly string _icon;

		private bool _selected;

		public event Action Click;

		public CategoryRow(string icon, string name)
		{
			_icon = icon;
			base.Height = 30.0;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			BuddyTheme.Crisp(this);
			_bar = new Rectangle
			{
				Width = 2.0,
				RadiusX = 1.0,
				RadiusY = 1.0,
				Fill = BuddyTheme.Mark,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(0.0, 7.0, 0.0, 7.0),
				Visibility = Visibility.Collapsed
			};
			base.Children.Add(_bar);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			_glyph = new ContentControl
			{
				Width = 16.0,
				Height = 16.0,
				IsHitTestVisible = false,
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(_glyph);
			_name = Txt(name, 13.0, BuddyTheme.Text92, FontWeights.Medium);
			_name.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
			_name.VerticalAlignment = VerticalAlignment.Center;
			stackPanel.Children.Add(_name);
			base.Children.Add(stackPanel);
			Paint();
			MouseEventHandler value = delegate
			{
				Paint();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Paint();
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				this.Click?.Invoke();
			};
		}

		public void SetSelected(bool on)
		{
			_selected = on;
			Paint();
		}

		private void Paint()
		{
			_bar.Visibility = ((!_selected) ? Visibility.Collapsed : Visibility.Visible);
			_name.Foreground = ((_selected || base.IsMouseOver) ? BuddyTheme.Text100 : BuddyTheme.Text92);
			_glyph.Content = FluentIcons.Create(_icon, 16.0, _selected ? BuddyTheme.Text100 : BuddyTheme.Text50, filled: true);
		}
	}

	private sealed class UnitSelector : Border
	{
		private readonly TextBlock _name;

		private readonly TextBlock _symbol;

		public UnitSelector(Action open)
		{
			UnitSelector unitSelector = this;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			_name = Txt("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
			_name.VerticalAlignment = VerticalAlignment.Center;
			stackPanel.Children.Add(_name);
			_symbol = Txt("", 13.0, BuddyTheme.Text100, AynType.Medium);
			_symbol.VerticalAlignment = VerticalAlignment.Center;
			_symbol.Margin = new Thickness(8.0, 0.0, 6.0, 0.0);
			stackPanel.Children.Add(_symbol);
			FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 12.0, BuddyTheme.Text50, filled: false);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			stackPanel.Children.Add(frameworkElement);
			Child = stackPanel;
			base.ToolTip = BuddyTooltip.Create("Change unit");
			MouseEventHandler value = delegate
			{
				_name.Foreground = BuddyTheme.Text100;
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				_name.Foreground = BuddyTheme.Text92;
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				open();
			};
		}

		public void Set(Unit u)
		{
			_name.Text = u.Name;
			_symbol.Text = u.Symbol;
		}
	}

	private sealed class CommonRow : Grid
	{
		private readonly TextBlock _name;

		private readonly TextBlock _value;

		private readonly TextBlock _symbol;

		public Unit Unit { get; private set; }

		public event Action Click;

		public CommonRow()
		{
			base.Height = 24.0;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			base.Children.Add(new Rectangle
			{
				Height = 1.0,
				Fill = BuddyTheme.Hairline,
				VerticalAlignment = VerticalAlignment.Top,
				IsHitTestVisible = false
			});
			_name = Txt("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
			_name.VerticalAlignment = VerticalAlignment.Center;
			base.Children.Add(_name);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			};
			_value = Txt("", 13.0, BuddyTheme.Text100, FontWeights.Medium);
			Typography.SetNumeralAlignment(_value, FontNumeralAlignment.Tabular);
			stackPanel.Children.Add(_value);
			_symbol = Txt("", 13.0, BuddyTheme.Text30, FontWeights.Medium);
			_symbol.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
			stackPanel.Children.Add(_symbol);
			base.Children.Add(stackPanel);
			base.ToolTip = BuddyTooltip.Create("Convert to this unit");
			MouseEventHandler value = delegate
			{
				_name.Foreground = BuddyTheme.Text100;
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				_name.Foreground = BuddyTheme.Text92;
			};
			base.MouseLeftButtonUp += delegate
			{
				this.Click?.Invoke();
			};
		}

		public void Bind(Unit u, string value)
		{
			Unit = u;
			base.Visibility = ((u == null) ? Visibility.Hidden : Visibility.Visible);
			if (u != null)
			{
				_name.Text = u.Name;
				_value.Text = value;
				_value.Foreground = ((value == "—") ? BuddyTheme.Text30 : BuddyTheme.Text100);
				_symbol.Text = u.Symbol;
			}
		}
	}

	private sealed class UnitPicker
	{
		private const double RowHeight = 28.0;

		private readonly Popup _popup;

		private readonly TextBox _search;

		private readonly StackPanel _list;

		private readonly ScrollViewer _scroll;

		private IList<Unit> _units;

		private Unit _current;

		private Action<Unit> _pick;

		private readonly List<Unit> _shown = new List<Unit>();

		private int _hi;

		public bool IsOpen => _popup.IsOpen;

		public UnitPicker()
		{
			_search = new TextBox
			{
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				BorderBrush = BuddyTheme.Hairline,
				Foreground = BuddyTheme.Text100,
				CaretBrush = BuddyTheme.Text100,
				SelectionBrush = BuddyTheme.Text50,
				FontFamily = BuddyTheme.Font,
				FontSize = 13.0,
				FontWeight = FontWeights.Medium,
				Padding = new Thickness(2.0, 4.0, 2.0, 6.0),
				FocusVisualStyle = null
			};
			BuddyTheme.ApplyTextMode(_search, 13.0);
			TextBox search = _search;
			TextChangedEventHandler value = delegate
			{
				Fill();
			};
			search.TextChanged += value;
			_search.PreviewKeyDown += OnKey;
			_list = new StackPanel();
			_scroll = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				MaxHeight = 168.0,
				Focusable = false,
				CanContentScroll = false,
				Content = _list,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
			};
			StackPanel child = new StackPanel
			{
				Children = 
				{
					(UIElement)_search,
					(UIElement)_scroll
				}
			};
			Border border = new Border
			{
				Background = BuddyTheme.Control,
				CornerRadius = new CornerRadius(12.0),
				Padding = new Thickness(6.0),
				Width = 220.0,
				Child = child
			};
			RenderOptions.SetClearTypeHint(border, ClearTypeHint.Enabled);
			TextOptions.SetTextFormattingMode(border, TextFormattingMode.Display);
			_popup = new Popup
			{
				Child = border,
				AllowsTransparency = true,
				StaysOpen = false,
				Placement = PlacementMode.Bottom,
				VerticalOffset = 6.0,
				PopupAnimation = PopupAnimation.Fade
			};
			_popup.Opened += delegate
			{
				_search.Focus();
				Keyboard.Focus(_search);
			};
		}

		public void Open(FrameworkElement anchor, IList<Unit> units, Unit current, Action<Unit> pick)
		{
			_units = units;
			_current = current;
			_pick = pick;
			_popup.PlacementTarget = anchor;
			_popup.HorizontalOffset = 0.0;
			_search.Text = string.Empty;
			Fill();
			_popup.IsOpen = true;
			_popup.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			_popup.HorizontalOffset = anchor.ActualWidth - _popup.Child.DesiredSize.Width;
		}

		public void Close()
		{
			_popup.IsOpen = false;
		}

		private void Fill()
		{
			if (_units == null)
			{
				return;
			}
			string text = _search.Text.Trim();
			_shown.Clear();
			foreach (Unit unit in _units)
			{
				if (text.Length == 0 || unit.Symbol.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || unit.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					_shown.Add(unit);
				}
			}
			_hi = Math.Max(0, _shown.IndexOf(_current));
			if (text.Length > 0)
			{
				_hi = 0;
			}
			Render();
			if (_shown.Count > 0)
			{
				BringIntoView(_hi);
			}
		}

		private void Render()
		{
			_list.Children.Clear();
			for (int i = 0; i < _shown.Count; i++)
			{
				Unit u = _shown[i];
				int index = i;
				bool flag = u == _current;
				Border border = new Border();
				border.Height = 28.0;
				border.CornerRadius = new CornerRadius(8.0);
				border.Padding = new Thickness(8.0, 0.0, 8.0, 0.0);
				border.Cursor = Cursors.Hand;
				border.Background = ((i == _hi) ? BuddyTheme.ControlActive : Brushes.Transparent);
				Border border2 = border;
				Grid grid = new Grid();
				TextBlock textBlock = Txt(u.Name, 13.0, flag ? BuddyTheme.Text100 : BuddyTheme.Text92, FontWeights.Medium);
				textBlock.VerticalAlignment = VerticalAlignment.Center;
				textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
				textBlock.Margin = new Thickness(0.0, 0.0, 56.0, 0.0);
				grid.Children.Add(textBlock);
				StackPanel stackPanel = new StackPanel();
				stackPanel.Orientation = Orientation.Horizontal;
				stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
				stackPanel.VerticalAlignment = VerticalAlignment.Center;
				StackPanel stackPanel2 = stackPanel;
				stackPanel2.Children.Add(Txt(u.Symbol, 13.0, BuddyTheme.Text30, FontWeights.Medium));
				FrameworkElement frameworkElement = FluentIcons.Create("checkmark", 12.0, BuddyTheme.Text100, filled: false);
				frameworkElement.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				frameworkElement.Visibility = ((!flag) ? Visibility.Hidden : Visibility.Visible);
				stackPanel2.Children.Add(frameworkElement);
				grid.Children.Add(stackPanel2);
				border2.Child = grid;
				border2.MouseEnter += delegate
				{
					_hi = index;
					Highlight();
				};
				border2.MouseLeftButtonUp += delegate
				{
					Choose(u);
				};
				_list.Children.Add(border2);
			}
		}

		private void Highlight()
		{
			for (int i = 0; i < _list.Children.Count; i++)
			{
				((Border)_list.Children[i]).Background = ((i == _hi) ? BuddyTheme.ControlActive : Brushes.Transparent);
			}
		}

		private void BringIntoView(int i)
		{
			double num = (double)i * 28.0;
			double maxHeight = _scroll.MaxHeight;
			if (num < _scroll.VerticalOffset)
			{
				_scroll.ScrollToVerticalOffset(num);
			}
			else if (num + 28.0 > _scroll.VerticalOffset + maxHeight)
			{
				_scroll.ScrollToVerticalOffset(num + 28.0 - maxHeight);
			}
		}

		private void OnKey(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
				e.Handled = true;
			}
			else if (e.Key == Key.Return)
			{
				if (_shown.Count > 0)
				{
					Choose(_shown[Math.Min(_hi, _shown.Count - 1)]);
				}
				e.Handled = true;
			}
			else if ((e.Key == Key.Down || e.Key == Key.Up) && _shown.Count != 0)
			{
				_hi = Math.Max(0, Math.Min(_shown.Count - 1, _hi + ((e.Key == Key.Down) ? 1 : (-1))));
				Highlight();
				BringIntoView(_hi);
				e.Handled = true;
			}
		}

		private void Choose(Unit u)
		{
			Close();
			if (_pick != null)
			{
				_pick(u);
			}
		}
	}

	private const double ListW = 200.0;

	private const double SplitPad = 16.0;

	private const double ListInset = 12.0;

	private const double ValueSize = 28.0;

	private const double ValueH = 40.0;

	private const double CommonH = 24.0;

	private const double SwapSize = 28.0;

	private const double SwapX = 96.0;

	private Grid _root;

	private UnitCategory _cat;

	private Unit _fromUnit;

	private Unit _toUnit;

	private Side _source;

	private bool _writing;

	private double _lastResult = double.NaN;

	private UnitsState _state = UnitsState.Load();

	private DispatcherTimer _recentTimer;

	private readonly Dictionary<string, CategoryRow> _rows = new Dictionary<string, CategoryRow>();

	private TextBox _fromBox;

	private TextBox _toBox;

	private UnitSelector _fromSel;

	private UnitSelector _toSel;

	private Border _swap;

	private ContentControl _copyGlyph;

	private DispatcherTimer _copyTimer;

	private TextBlock _ratesLine;

	private readonly CommonRow[] _common = new CommonRow[3];

	private StackPanel _recentHost;

	private UnitPicker _picker;

	private string _recentSig;

	public string Id => "units";

	public string DisplayName => "Units";

	public ToolCategory Category => ToolCategory.Utility;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => false;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	private bool IsTyping
	{
		get
		{
			if (_fromBox != null)
			{
				if (!_fromBox.IsKeyboardFocused)
				{
					return _toBox.IsKeyboardFocused;
				}
				return true;
			}
			return false;
		}
	}

	public UnitsConverterTool()
	{
		IsEnabled = true;
		OrderIndex = 11;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		CurrencyRates.Loaded += delegate
		{
			if (_root != null && _cat != null && _cat.IsCurrency)
			{
				Dispatch(OnRatesLoaded);
			}
		};
		PersistenceHelper.RegisterExitPreparation("units", delegate
		{
			if (IsInitialized && State == ToolState.Active)
			{
				_state.Save();
			}
		});
	}

	public void Shutdown()
	{
		IsInitialized = false;
	}

	public void OnActivated()
	{
		_state = UnitsState.Load();
		if (_root != null)
		{
			SelectCategory(UnitCatalog.Find(_state.Category) ?? UnitCatalog.All[0], _state.Value);
		}
		State = ToolState.Active;
		if (_cat != null && _cat.IsCurrency)
		{
			CurrencyRates.EnsureFresh();
		}
		if (_root == null)
		{
			return;
		}
		_root.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			if (State == ToolState.Active && !IsTyping)
			{
				_root.Focus();
			}
		});
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		if (_picker != null)
		{
			_picker.Close();
		}
		CommitRecent();
		_state.Save();
	}

	public bool HandleEscape()
	{
		if (_picker != null && _picker.IsOpen)
		{
			_picker.Close();
			return true;
		}
		return false;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return FluentIcons.Create("arrow_swap", size, stroke ?? BuddyTheme.Text100, filled: false);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return null;
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			return _root;
		}
		Grid root = BuddyTheme.Crisp(new Grid
		{
			Width = 728.0,
			Height = 242.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Background = Brushes.Transparent,
			Focusable = true,
			FocusVisualStyle = null,
			Tag = "NoOuterScroll"
		});
		root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(200.0)
		});
		root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(16.0)
		});
		root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0)
		});
		root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(16.0)
		});
		root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		root.Children.Add(BuildList());
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.IsHitTestVisible = false;
		Rectangle element = rectangle;
		Grid.SetColumn(element, 2);
		root.Children.Add(element);
		FrameworkElement element2 = BuildDetail();
		Grid.SetColumn(element2, 4);
		root.Children.Add(element2);
		root.PreviewKeyDown += OnPreviewKeyDown;
		root.PreviewTextInput += OnPreviewTextInput;
		root.MouseLeftButtonDown += delegate
		{
			if (!IsTyping)
			{
				root.Focus();
			}
		};
		_root = root;
		_recentTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(2.0)
		};
		_recentTimer.Tick += delegate
		{
			CommitRecent();
		};
		_copyTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_copyTimer.Tick += delegate
		{
			_copyTimer.Stop();
			_copyGlyph.Content = FluentIcons.Create("copy", 14.0, BuddyTheme.Text92, filled: false);
		};
		UnitCategory cat = UnitCatalog.Find(_state.Category) ?? UnitCatalog.All[0];
		SelectCategory(cat, _state.Value);
		return root;
	}

	private FrameworkElement BuildList()
	{
		Grid grid = new Grid();
		grid.Margin = new Thickness(-12.0, 0.0, 0.0, 0.0);
		Grid grid2 = grid;
		for (int i = 0; i < UnitCatalog.All.Length; i++)
		{
			UnitCategory c = UnitCatalog.All[i];
			grid2.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			CategoryRow categoryRow = new CategoryRow(c.Icon, c.Name);
			categoryRow.Click += delegate
			{
				SelectCategory(c, null);
				_root.Focus();
			};
			Grid.SetRow(categoryRow, i);
			grid2.Children.Add(categoryRow);
			_rows[c.Name] = categoryRow;
		}
		return grid2;
	}

	private FrameworkElement BuildDetail()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(20.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_fromBox = ValueBox(Side.From);
		_fromSel = new UnitSelector(delegate
		{
			OpenPicker(Side.From);
		});
		grid.Children.Add(Block("From", _fromBox, null, _fromSel, 0));
		Grid grid2 = new Grid();
		grid2.Children.Add(new Rectangle
		{
			Height = 1.0,
			Fill = BuddyTheme.Hairline,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		});
		_swap = new Border
		{
			Width = 28.0,
			Height = 28.0,
			CornerRadius = new CornerRadius(14.0),
			Background = BuddyTheme.Control,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(82.0, -4.0, 0.0, -4.0),
			Cursor = Cursors.Hand,
			ToolTip = BuddyTooltip.Create("Swap (Ctrl+Shift+S)"),
			Child = new ContentControl
			{
				Width = 14.0,
				Height = 14.0,
				IsHitTestVisible = false,
				Content = FluentIcons.Create("arrow_sort", 14.0, BuddyTheme.Text100, filled: true)
			}
		};
		_swap.MouseEnter += delegate
		{
			_swap.Background = BuddyTheme.ControlActive;
		};
		_swap.MouseLeave += delegate
		{
			_swap.Background = BuddyTheme.Control;
		};
		_swap.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_swap.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			Swap();
		};
		grid2.Children.Add(_swap);
		Grid.SetRow(grid2, 1);
		Panel.SetZIndex(grid2, 1);
		grid.Children.Add(grid2);
		_toBox = ValueBox(Side.To);
		_toSel = new UnitSelector(delegate
		{
			OpenPicker(Side.To);
		});
		Border border = BuddyKit.GlyphButton("copy", 14.0, "Copy result (Ctrl+C)", CopyResult, out _copyGlyph);
		border.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		grid.Children.Add(Block("To", _toBox, border, _toSel, 2));
		_ratesLine = Txt("", 11.0, BuddyTheme.Text30, FontWeights.Medium);
		_ratesLine.Margin = new Thickness(0.0, 0.0, 0.0, 2.0);
		_ratesLine.Visibility = Visibility.Collapsed;
		Grid.SetRow(_ratesLine, 3);
		grid.Children.Add(_ratesLine);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		for (int i = 0; i < 3; i++)
		{
			_common[i] = new CommonRow();
			CommonRow row = _common[i];
			row.Click += delegate
			{
				if (row.Unit != null)
				{
					SetUnits(_fromUnit, row.Unit, Side.From);
				}
			};
			stackPanel2.Children.Add(row);
		}
		Grid.SetRow(stackPanel2, 4);
		grid.Children.Add(stackPanel2);
		_recentHost = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		Grid.SetRow(_recentHost, 6);
		grid.Children.Add(_recentHost);
		_picker = new UnitPicker();
		return grid;
	}

	private static FrameworkElement Block(string label, TextBox box, FrameworkElement after, FrameworkElement selector, int row)
	{
		StackPanel stackPanel = new StackPanel();
		BuddyLabel buddyLabel = BuddyTheme.LabelText(label);
		buddyLabel.Foreground = BuddyTheme.Text30;
		stackPanel.Children.Add(buddyLabel);
		Grid grid = new Grid();
		grid.Height = 40.0;
		grid.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel3 = stackPanel2;
		stackPanel3.Children.Add(box);
		if (after != null)
		{
			stackPanel3.Children.Add(after);
		}
		grid2.Children.Add(stackPanel3);
		selector.VerticalAlignment = VerticalAlignment.Center;
		selector.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		Grid.SetColumn(selector, 1);
		grid2.Children.Add(selector);
		stackPanel.Children.Add(grid2);
		Grid.SetRow(stackPanel, row);
		return stackPanel;
	}

	private TextBox ValueBox(Side side)
	{
		TextBox tb = new TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			FontFamily = BuddyTheme.DisplayFont,
			FontSize = 28.0,
			FontWeight = AynType.Semibold,
			Padding = new Thickness(0.0),
			Height = 40.0,
			MinWidth = 24.0,
			MaxWidth = 300.0,
			MaxLength = 40,
			VerticalContentAlignment = VerticalAlignment.Center,
			FocusVisualStyle = null,
			TabIndex = ((side != 0) ? 1 : 0)
		};
		Typography.SetNumeralAlignment(tb, FontNumeralAlignment.Tabular);
		BuddyTheme.ApplyTextMode(tb, 28.0);
		tb.TextChanged += delegate
		{
			if (!_writing)
			{
				_source = side;
				Convert();
				_recentTimer.Stop();
				_recentTimer.Start();
			}
		};
		tb.GotKeyboardFocus += delegate
		{
			if (tb.Foreground == BuddyTheme.Text30)
			{
				tb.Foreground = BuddyTheme.Text100;
			}
		};
		return tb;
	}

	private void SelectCategory(UnitCategory cat, string value)
	{
		CommitRecent();
		_cat = cat;
		_state.Category = cat.Name;
		foreach (KeyValuePair<string, CategoryRow> row in _rows)
		{
			row.Value.SetSelected(row.Key == cat.Name);
		}
		if (cat.IsCurrency)
		{
			CurrencyRates.EnsureFresh();
		}
		Unit unit = null;
		Unit unit2 = null;
		if (_state.Pairs.TryGetValue(cat.Name, out var value2))
		{
			string[] array = value2.Split('|');
			if (array.Length == 2)
			{
				unit = cat.Find(array[0]);
				unit2 = cat.Find(array[1]);
			}
		}
		if (unit == null || unit2 == null || unit == unit2)
		{
			unit = cat.Find(cat.DefaultFrom);
			unit2 = cat.Find(cat.DefaultTo);
		}
		if (value != null)
		{
			SetText(_fromBox, value, valid: true);
		}
		else if (string.IsNullOrWhiteSpace(_fromBox.Text))
		{
			SetText(_fromBox, "1", valid: true);
		}
		SetUnits(unit, unit2, Side.From);
	}

	private void SetUnits(Unit from, Unit to, Side source)
	{
		if (from == null || to == null)
		{
			return;
		}
		if (from == to)
		{
			if (source == Side.From)
			{
				from = _toUnit ?? from;
			}
			else
			{
				to = _fromUnit ?? to;
			}
		}
		_fromUnit = from;
		_toUnit = to;
		_state.Pairs[_cat.Name] = from.Symbol + "|" + to.Symbol;
		_fromSel.Set(from);
		_toSel.Set(to);
		if (source == Side.From && _source != 0)
		{
			_source = Side.From;
		}
		Convert();
	}

	private void Convert()
	{
		if (_cat == null || _fromUnit == null)
		{
			return;
		}
		bool flag = _source == Side.From;
		TextBox textBox = (flag ? _fromBox : _toBox);
		TextBox tb = (flag ? _toBox : _fromBox);
		Unit a = (flag ? _fromUnit : _toUnit);
		Unit b = (flag ? _toUnit : _fromUnit);
		textBox.Foreground = BuddyTheme.Text100;
		decimal? num = Expr.Evaluate(textBox.Text);
		bool flag2 = _cat.IsCurrency && !CurrencyRates.HasRates;
		double num2 = double.NaN;
		if (num.HasValue && !flag2)
		{
			num2 = UnitCatalog.Convert(num.Value, a, b, _cat);
		}
		SetText(tb, double.IsNaN(num2) ? "—" : Format.Number(num2), !double.IsNaN(num2));
		if (flag)
		{
			_state.Value = textBox.Text;
		}
		else if (!double.IsNaN(num2))
		{
			_state.Value = Format.Number(num2);
		}
		_lastResult = (flag ? num2 : (num.HasValue ? ((double)num.Value) : double.NaN));
		decimal? num3 = (flag ? num : (double.IsNaN(num2) ? null : SafeDecimal(num2)));
		List<Unit> list = (from sym in _cat.Common
			select _cat.Find(sym) into u
			where u != null && u != _fromUnit && u != _toUnit
			select u).Take(3).ToList();
		for (int i = 0; i < 3; i++)
		{
			if (i >= list.Count)
			{
				_common[i].Bind(null, null);
				continue;
			}
			double num4 = ((!num3.HasValue || flag2) ? double.NaN : UnitCatalog.Convert(num3.Value, _fromUnit, list[i], _cat));
			_common[i].Bind(list[i], double.IsNaN(num4) ? "—" : Format.Number(num4));
		}
		UpdateRatesLine();
		RenderRecents();
	}

	private static decimal? SafeDecimal(double d)
	{
		try
		{
			return (decimal)d;
		}
		catch
		{
			return null;
		}
	}

	private void SetText(TextBox tb, string text, bool valid)
	{
		_writing = true;
		if (tb.Text != text)
		{
			tb.Text = text;
			if (tb.IsKeyboardFocused)
			{
				tb.CaretIndex = tb.Text.Length;
			}
		}
		tb.Foreground = (valid ? BuddyTheme.Text100 : BuddyTheme.Text30);
		_writing = false;
	}

	private void UpdateRatesLine()
	{
		if (!_cat.IsCurrency)
		{
			_ratesLine.Visibility = Visibility.Collapsed;
			return;
		}
		_ratesLine.Visibility = Visibility.Visible;
		_ratesLine.Text = (CurrencyRates.HasRates ? ("Rates from " + CurrencyRates.FetchedLocal.ToString("d MMM, h:mm tt", CultureInfo.CurrentCulture)) : (CurrencyRates.IsFetching ? "Fetching rates…" : "Rates unavailable offline"));
	}

	private void OnRatesLoaded()
	{
		_cat.RefreshCurrencyUnits();
		_fromUnit = _cat.Find(_fromUnit.Symbol) ?? _fromUnit;
		_toUnit = _cat.Find(_toUnit.Symbol) ?? _toUnit;
		Convert();
	}

	private void Swap()
	{
		Unit fromUnit = _fromUnit;
		string text = ((_toBox.Foreground == BuddyTheme.Text30) ? _fromBox.Text : _toBox.Text);
		if (CapsuleMotion.AnimationsEnabled)
		{
			DoubleAnimation animation = new DoubleAnimation(0.0, 1.0, BuddyTheme.Hover);
			_fromBox.BeginAnimation(UIElement.OpacityProperty, animation);
			_toBox.BeginAnimation(UIElement.OpacityProperty, animation);
		}
		_source = Side.From;
		SetText(_fromBox, text, valid: true);
		SetUnits(_toUnit, fromUnit, Side.From);
	}

	private void CopyResult()
	{
		if (!double.IsNaN(_lastResult))
		{
			string text = ((_source == Side.From) ? _toBox.Text : _fromBox.Text);
			try
			{
				System.Windows.Clipboard.SetText(text);
			}
			catch
			{
				return;
			}
			_copyGlyph.Content = FluentIcons.Create("checkmark", 14.0, BuddyTheme.Text100, filled: false);
			_copyTimer.Stop();
			_copyTimer.Start();
		}
	}

	private void OpenPicker(Side side)
	{
		UnitSelector anchor = ((side == Side.From) ? _fromSel : _toSel);
		Unit current = ((side == Side.From) ? _fromUnit : _toUnit);
		_picker.Open(anchor, _cat.Units, current, delegate(Unit u)
		{
			if (side == Side.From)
			{
				SetUnits(u, _toUnit, Side.From);
			}
			else
			{
				SetUnits(_fromUnit, u, Side.To);
			}
			_root.Focus();
		});
	}

	private void CommitRecent()
	{
		if (_recentTimer != null)
		{
			_recentTimer.Stop();
		}
		if (_cat == null || _fromUnit == null || double.IsNaN(_lastResult))
		{
			return;
		}
		decimal? num = Expr.Evaluate(_fromBox.Text);
		if (num.HasValue && _toBox.Foreground != BuddyTheme.Text30)
		{
			Recent r = new Recent
			{
				Category = _cat.Name,
				From = _fromUnit.Symbol,
				To = _toUnit.Symbol,
				Value = Format.Number((double)num.Value),
				Result = _toBox.Text
			};
			List<Recent> recents = _state.Recents;
			recents.RemoveAll((Recent x) => x.Category == r.Category && x.From == r.From && x.To == r.To && x.Value == r.Value);
			recents.Insert(0, r);
			if (recents.Count > 10)
			{
				recents.RemoveRange(10, recents.Count - 10);
			}
			RenderRecents();
		}
	}

	private void RenderRecents()
	{
		List<Recent> list = _state.Recents.Take(2).ToList();
		string text = string.Join("|", list.Select((Recent i) => i.Category + i.Value + i.From + i.To));
		if (text == _recentSig)
		{
			return;
		}
		_recentSig = text;
		_recentHost.Children.Clear();
		if (list.Count == 0)
		{
			return;
		}
		TextBlock textBlock = Txt("Recent", 11.0, BuddyTheme.Text30, FontWeights.Medium);
		textBlock.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		_recentHost.Children.Add(textBlock);
		for (int j = 0; j < list.Count; j++)
		{
			Recent r = list[j];
			if (j > 0)
			{
				_recentHost.Children.Add(Txt("  ·  ", 11.0, BuddyTheme.Text30, FontWeights.Medium));
			}
			TextBlock t = Txt(r.Value + " " + r.From + " → " + r.Result + " " + r.To, 11.0, BuddyTheme.Text30, FontWeights.Medium);
			t.Cursor = Cursors.Hand;
			t.Background = Brushes.Transparent;
			Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
			t.MouseEnter += delegate
			{
				t.Foreground = BuddyTheme.Text92;
			};
			t.MouseLeave += delegate
			{
				t.Foreground = BuddyTheme.Text30;
			};
			t.MouseLeftButtonUp += delegate
			{
				RestoreRecent(r);
			};
			_recentHost.Children.Add(t);
		}
	}

	private void RestoreRecent(Recent r)
	{
		UnitCategory unitCategory = UnitCatalog.Find(r.Category);
		if (unitCategory != null)
		{
			_state.Pairs[unitCategory.Name] = r.From + "|" + r.To;
			_source = Side.From;
			SelectCategory(unitCategory, r.Value);
		}
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		ModifierKeys modifiers = Keyboard.Modifiers;
		if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
		{
			Swap();
			e.Handled = true;
		}
		else if (modifiers == ModifierKeys.Control && e.Key == Key.C)
		{
			if (!(Keyboard.FocusedElement is TextBox { SelectionLength: not 0 }))
			{
				CopyResult();
				e.Handled = true;
			}
		}
		else if (!IsTyping && modifiers == ModifierKeys.None && (e.Key == Key.Up || e.Key == Key.Down))
		{
			int num = Array.IndexOf(UnitCatalog.All, _cat) + ((e.Key == Key.Down) ? 1 : (-1));
			if (num >= 0 && num < UnitCatalog.All.Length)
			{
				SelectCategory(UnitCatalog.All[num], null);
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Tab && modifiers == ModifierKeys.None && _fromBox.IsKeyboardFocused)
		{
			_toBox.Focus();
			_toBox.SelectAll();
			e.Handled = true;
		}
	}

	private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		if (!IsTyping && !string.IsNullOrEmpty(e.Text))
		{
			char c = e.Text[0];
			if (char.IsDigit(c) || c == '.' || c == ',' || c == '-' || c == '(')
			{
				e.Handled = true;
				_source = Side.From;
				_fromBox.Focus();
				_fromBox.Text = e.Text;
				_fromBox.CaretIndex = _fromBox.Text.Length;
			}
		}
	}

	private static TextBlock Txt(string text, double size, Brush ink, FontWeight weight)
	{
		TextBlock textBlock = BuddyTheme.Text(text, size, ink, weight);
		textBlock.TextTrimming = TextTrimming.None;
		return textBlock;
	}

	private static void Dispatch(Action a)
	{
		Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
		{
			try
			{
				a();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Units: " + ex.Message);
			}
		});
	}
}
