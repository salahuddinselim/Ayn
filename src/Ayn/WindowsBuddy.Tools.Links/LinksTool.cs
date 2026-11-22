using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools.Clipboard;

namespace WindowsBuddy.Tools.Links;

public class LinksTool : IBuddyTool, IEscapeAware
{
	private const int CardsPerPage = 4;

	private const int MaxClipboardLinks = 100;

	private const int WM_MOUSEHWHEEL = 526;

	private const int WM_MOUSEWHEEL = 522;

	private const string GitHubMark = "M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0 0 16 8c0-4.42-3.58-8-8-8z";

	private readonly string _bookmarksPath;

	private readonly string _prefsPath;

	private bool _bookmarksLoadFailed;

	private List<LinksBookmark> _bookmarks = new List<LinksBookmark>();

	private LinksPreferences _prefs = new LinksPreferences();

	private List<SearchEngineItem> _engines = new List<SearchEngineItem>();

	private bool _clipboardSubscribed;

	private Grid _rootGrid;

	private TextBox _searchBox;

	private ScrollViewer _engineScrollViewer;

	private StackPanel _chipsPanel;

	private readonly Dictionary<string, Border> _engineChips = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, Border> _engineGlyphHosts = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);

	private UniformGrid _cardsRow;

	private TextBlock _emptyState;

	private TextBlock _countLabel;

	private Border _prevBtn;

	private Border _nextBtn;

	private int _pageStart;

	private bool _addRowVisible;

	private string _editingId;

	private Border _addRowContainer;

	private TextBox _addUrlBox;

	private TextBox _addNameBox;

	private TextBlock _validationHint;

	private HwndSource _hwndSource;

	private Point _chipDragStartPoint;

	private bool _isChipDragging;

	private bool _suppressChipClick;

	private SearchEngineItem _dragEngine;

	private readonly Dictionary<string, DateTime> _copiedUntil = new Dictionary<string, DateTime>();

	internal static Action<Uri> LaunchBrowser = delegate(Uri uri)
	{
		Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
		{
			UseShellExecute = true
		});
	};

	public string Id => "links";

	public string DisplayName => "Links";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => false;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 720.0;

	public double DesiredHeight => 352.0;

	public IReadOnlyList<LinksBookmark> WidgetLinks
	{
		get
		{
			if (!IsInitialized)
			{
				Initialize();
			}
			return _bookmarks.ToArray();
		}
	}

	public event EventHandler BookmarksChanged;

	public LinksTool()
	{
		IsEnabled = true;
		OrderIndex = 18;
		State = ToolState.Idle;
		string localAppData = AppPaths.LocalAppData;
		string text = System.IO.Path.Combine(localAppData, AppPaths.FolderName, "links");
		try
		{
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
		}
		catch
		{
		}
		_bookmarksPath = System.IO.Path.Combine(text, "bookmarks.json");
		_prefsPath = System.IO.Path.Combine(text, "links_prefs.json");
		try
		{
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(SubscribeClipboard));
		}
		catch
		{
		}
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		LoadAll();
		SubscribeClipboard();
		PersistenceHelper.RegisterExitPreparation("links", delegate
		{
			if (IsInitialized)
			{
				SaveAll();
			}
		});
	}

	public void Shutdown()
	{
		UnsubscribeClipboard();
		IsInitialized = false;
	}

	public void OnActivated()
	{
		Dispatcher ui = Application.Current.Dispatcher;
		ui.BeginInvoke((Action)delegate
		{
			RefreshLinksGrid();
			ui.BeginInvoke((Action)delegate
			{
				if (_searchBox != null)
				{
					_searchBox.Focus();
					_searchBox.SelectAll();
				}
			}, DispatcherPriority.ApplicationIdle);
		}, DispatcherPriority.Background);
	}

	public void OnDeactivated()
	{
		if (_addRowVisible)
		{
			HideAddRow();
		}
	}

	public bool HandleEscape()
	{
		if (!_addRowVisible)
		{
			return false;
		}
		HideAddRow();
		return true;
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
		return VectorIcons.CreateLinksIcon(size, stroke);
	}

	public UIElement CreateView()
	{
		_rootGrid = new Grid
		{
			Width = 728.0,
			Height = 242.0,
			Tag = "SelfScroller",
			VerticalAlignment = VerticalAlignment.Top,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true
		};
		TextOptions.SetTextFormattingMode(_rootGrid, TextFormattingMode.Display);
		TextOptions.SetTextRenderingMode(_rootGrid, TextRenderingMode.ClearType);
		TextOptions.SetTextHintingMode(_rootGrid, TextHintingMode.Fixed);
		_rootGrid.Loaded += delegate
		{
			AttachHwndHook();
		};
		_rootGrid.Unloaded += delegate
		{
			DetachHwndHook();
		};
		_rootGrid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_rootGrid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(6.0, GridUnitType.Pixel)
		});
		_rootGrid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Grid element = WrapWithSheen(BuildSearchCard());
		Grid.SetRow(element, 0);
		_rootGrid.Children.Add(element);
		Grid element2 = WrapWithSheen(BuildLinksCard());
		Grid.SetRow(element2, 2);
		_rootGrid.Children.Add(element2);
		RefreshLinksGrid();
		_rootGrid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AttachHwndHook));
		return _rootGrid;
	}

	private Border BuildSearchCard()
	{
		Border border = new Border();
		border.Background = BuddyTokens.CardBrush;
		border.BorderBrush = BuddyTokens.CardBorderBrush;
		border.BorderThickness = new Thickness(0.0);
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(14.0, 6.0, 14.0, 6.0);
		border.SnapsToDevicePixels = true;
		border.UseLayoutRounding = true;
		border.Cursor = Cursors.Arrow;
		border.ClipToBounds = true;
		Border border2 = border;
		ThemeTokens.Card(border2);
		StackPanel stackPanel = new StackPanel();
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
		StackPanel stackPanel3 = stackPanel2;
		stackPanel3.Children.Add(new Border
		{
			Width = 12.0,
			Height = 12.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Child = CreateSearchIcon(12.0, BuddyTokens.Text38Brush)
		});
		stackPanel3.Children.Add(new TextBlock
		{
			Text = "Search",
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.0,
			FontWeight = AynType.Medium,
			Foreground = BuddyTokens.Text60Brush,
			VerticalAlignment = VerticalAlignment.Center
		});
		stackPanel.Children.Add(stackPanel3);
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 5.0);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_searchBox = CreateRoundedTextBox("Search or paste a link", 13.0, 30.0, 15.0, 512);
		_searchBox.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				PerformSearch();
				e.Handled = true;
			}
		};
		Grid.SetColumn(_searchBox, 0);
		grid2.Children.Add(_searchBox);
		Border border3 = MakePillButton("Search ↗", 28.0, 14.0, 12.0, primary: true);
		border3.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		border3.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			PerformSearch();
			e.Handled = true;
		};
		Grid.SetColumn(border3, 1);
		grid2.Children.Add(border3);
		stackPanel.Children.Add(grid2);
		_chipsPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
			Background = Brushes.Transparent
		};
		_engineScrollViewer = new ScrollViewer
		{
			Height = 28.0,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
			HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
			VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Focusable = false,
			ClipToBounds = true,
			Background = Brushes.Transparent,
			Content = _chipsPanel,
			IsManipulationEnabled = true
		};
		_engineScrollViewer.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(HandleChipStripWheel), handledEventsToo: true);
		SetupChipDragScroll();
		RebuildEngineChips();
		stackPanel.Children.Add(_engineScrollViewer);
		border2.Child = stackPanel;
		return border2;
	}

	private void RebuildEngineChips()
	{
		if (_chipsPanel == null)
		{
			return;
		}
		_chipsPanel.Children.Clear();
		_engineChips.Clear();
		_engineGlyphHosts.Clear();
		for (int i = 0; i < _engines.Count; i++)
		{
			SearchEngineItem searchEngineItem = _engines[i];
			if (!_engineChips.ContainsKey(searchEngineItem.Name))
			{
				Border border = BuildEngineChip(searchEngineItem, i);
				_engineChips[searchEngineItem.Name] = border;
				_chipsPanel.Children.Add(border);
			}
		}
		Border add = MakePillButton("+", 24.0, 12.0, 10.0, primary: false);
		AutomationProperties.SetName(add, "Pin search engine");
		add.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			ShowEnginePicker(add);
			e.Handled = true;
		};
		_chipsPanel.Children.Add(add);
		ApplyChipSelection(_prefs.SelectedEngine);
	}

	private Border BuildEngineChip(SearchEngineItem engine, int index)
	{
		bool isSearchEng = engine.Type == QuickLinkType.SearchEngine;
		Border chip = new Border
		{
			Height = 24.0,
			CornerRadius = new CornerRadius(12.0),
			Background = BuddyTokens.CardBrush,
			BorderBrush = BuddyTokens.HairlineBrush,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(8.0, 0.0, 9.0, 0.0),
			Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
			Cursor = Cursors.Hand,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true
		};
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		Border border = new Border();
		border.Width = 12.0;
		border.Height = 12.0;
		border.Margin = new Thickness(0.0, 0.0, 5.0, 0.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		Border border2 = border;
		_engineGlyphHosts[engine.Name] = border2;
		stackPanel2.Children.Add(border2);
		stackPanel2.Children.Add(new TextBlock
		{
			Text = engine.Name,
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.5,
			FontWeight = FontWeights.Medium,
			Foreground = BuddyTokens.Text100Brush,
			VerticalAlignment = VerticalAlignment.Center
		});
		chip.Child = stackPanel2;
		string engName = engine.Name;
		chip.MouseEnter += delegate
		{
			if (!isSearchEng || !IsEngineSelected(engName))
			{
				chip.Background = BuddyTokens.CardHoverBrush;
			}
		};
		chip.MouseLeave += delegate
		{
			if (!isSearchEng || !IsEngineSelected(engName))
			{
				chip.Background = BuddyTokens.CardBrush;
			}
		};
		chip.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (_suppressChipClick)
			{
				_suppressChipClick = false;
			}
			else
			{
				SearchEngineItem searchEngineItem = FindEngine(engName);
				if (searchEngineItem != null)
				{
					if (isSearchEng)
					{
						SelectEngine(engName);
						string textBoxValue = GetTextBoxValue(_searchBox);
						if (!string.IsNullOrWhiteSpace(textBoxValue))
						{
							RunQuery(textBoxValue, searchEngineItem);
						}
						if (_searchBox != null)
						{
							_searchBox.Focus();
							_searchBox.CaretIndex = _searchBox.Text.Length;
						}
					}
					else
					{
						OpenUrl(searchEngineItem.SearchUrl);
					}
				}
			}
		};
		AutomationProperties.SetName(chip, "Engine " + engName);
		chip.Tag = engine;
		chip.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			ShowEngineMenu(chip, engine);
			e.Handled = true;
		};
		return chip;
	}

	private void ShowEngineMenu(FrameworkElement chip, SearchEngineItem engine)
	{
		BuddyDropdown.Show(chip, new BuddyMenuItem[1]
		{
			new BuddyMenuItem("Unpin", null, delegate
			{
				_engines.Remove(engine);
				if (IsEngineSelected(engine.Name))
				{
					_prefs.SelectedEngine = FirstSearchEngineName();
				}
				PersistEngines();
			})
		});
	}

	private void PersistEngines()
	{
		_prefs.CustomEngines = new List<SearchEngineItem>(_engines);
		SavePrefs();
		RebuildEngineChips();
	}

	private void PinEngine(SearchEngineItem item)
	{
		if (FindEngine(item.Name) == null)
		{
			_engines.Add(item);
			PersistEngines();
		}
	}

	private void ShowEnginePicker(FrameworkElement anchor)
	{
		List<SearchEngineItem> list = SearchEngines.CreateDefaults();
		list.AddRange(new SearchEngineItem[8]
		{
			new SearchEngineItem("Bing", "https://www.bing.com/search?q=%s", "bing.com"),
			new SearchEngineItem("DuckDuckGo", "https://duckduckgo.com/?q=%s", "duckduckgo.com"),
			new SearchEngineItem("Wikipedia", "https://en.wikipedia.org/w/index.php?search=%s", "wikipedia.org"),
			new SearchEngineItem("Amazon", "https://www.amazon.com/s?k=%s", "amazon.com"),
			new SearchEngineItem("Reddit", "https://www.reddit.com/search/?q=%s", "reddit.com"),
			new SearchEngineItem("X", "https://x.com/search?q=%s", "x.com"),
			new SearchEngineItem("Maps", "https://www.google.com/maps/search/%s", "google.com"),
			new SearchEngineItem("Translate", "https://translate.google.com/?sl=auto&tl=en&text=%s", "translate.google.com")
		});
		StackPanel stackPanel = new StackPanel();
		foreach (SearchEngineItem item in list.Where((SearchEngineItem x) => FindEngine(x.Name) == null))
		{
			SearchEngineItem engine = item;
			Border border = MakePillButton(item.Name, 30.0, 12.0, 12.0, primary: false);
			border.Margin = new Thickness(0.0, 2.0, 0.0, 2.0);
			AutomationProperties.SetName(border, "Pin " + item.Name);
			border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				BuddyDropdown.Close();
				PinEngine(engine);
				e.Handled = true;
			};
			stackPanel.Children.Add(border);
		}
		Border border2 = MakePillButton("Custom...", 30.0, 12.0, 12.0, primary: false);
		AutomationProperties.SetName(border2, "Custom engine");
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			ShowCustomEngine(anchor);
			e.Handled = true;
		};
		stackPanel.Children.Add(border2);
		BuddyDropdown.ShowContent(anchor, new BuddyScrollViewer
		{
			Content = stackPanel,
			MaxHeight = 300.0,
			MinWidth = 180.0
		}, 6.0);
	}

	private void ShowCustomEngine(FrameworkElement anchor)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = 300.0;
		StackPanel stackPanel2 = stackPanel;
		TextBox name = CreateRoundedTextBox("Name", 12.0, 30.0, 12.0, 60);
		TextBox url = CreateRoundedTextBox("URL template with %s", 12.0, 30.0, 12.0, 2048);
		AutomationProperties.SetName(name, "Engine name");
		AutomationProperties.SetName(url, "Engine URL template");
		TextBlock hint = BuddyTheme.SecondaryText("Use %s for the search words.");
		Border border = MakePillButton("Pin engine", 30.0, 15.0, 14.0, primary: true);
		stackPanel2.Children.Add(name);
		stackPanel2.Children.Add(url);
		stackPanel2.Children.Add(hint);
		stackPanel2.Children.Add(border);
		border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			string text = GetTextBoxValue(name).Trim();
			string text2 = GetTextBoxValue(url).Trim();
			if (text.Length == 0 || FindEngine(text) != null || !text2.Contains("%s") || !LinkUrl.TryNormalize(text2.Replace("%s", "q"), out var uri))
			{
				hint.Text = "Enter a unique name and http(s) URL with %s.";
				e.Handled = true;
			}
			else
			{
				PinEngine(new SearchEngineItem(text, text2, uri.Host));
				BuddyDropdown.Close();
				e.Handled = true;
			}
		};
		BuddyDropdown.ShowContent(anchor, stackPanel2, 12.0);
	}

	private void MoveEngine(int index, int delta)
	{
		int num = index + delta;
		if (index >= 0 && index < _engines.Count && num >= 0 && num < _engines.Count)
		{
			SearchEngineItem item = _engines[index];
			_engines.RemoveAt(index);
			_engines.Insert(num, item);
			_prefs.CustomEngines = new List<SearchEngineItem>(_engines);
			SavePrefs();
			RebuildEngineChips();
		}
	}

	private SearchEngineItem FindEngine(string name)
	{
		return _engines.FirstOrDefault((SearchEngineItem x) => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	private string FirstSearchEngineName()
	{
		SearchEngineItem searchEngineItem = _engines.FirstOrDefault((SearchEngineItem x) => x.Type == QuickLinkType.SearchEngine);
		if (searchEngineItem == null)
		{
			return "Google";
		}
		return searchEngineItem.Name;
	}

	private bool IsEngineSelected(string engine)
	{
		return string.Equals(engine, _prefs.SelectedEngine, StringComparison.OrdinalIgnoreCase);
	}

	private void SelectEngine(string engine)
	{
		if (!IsEngineSelected(engine))
		{
			_prefs.SelectedEngine = engine;
			SavePrefs();
			ApplyChipSelection(engine);
		}
	}

	private void ApplyChipSelection(string sel)
	{
		foreach (SearchEngineItem engine in _engines)
		{
			if (!_engineChips.TryGetValue(engine.Name, out var value))
			{
				continue;
			}
			bool flag = engine.Type == QuickLinkType.SearchEngine && string.Equals(engine.Name, sel, StringComparison.OrdinalIgnoreCase);
			Brush foreground = (flag ? BuddyTheme.SelectedInk : BuddyTokens.Text100Brush);
			value.Background = (flag ? BuddyTheme.SelectedFill : BuddyTokens.CardBrush);
			value.BorderThickness = new Thickness(0.0);
			value.BorderBrush = (flag ? BuddyTheme.SelectedEdge : BuddyTokens.HairlineBrush);
			if (value.Child is StackPanel stackPanel)
			{
				foreach (UIElement child in stackPanel.Children)
				{
					if (child is TextBlock textBlock)
					{
						textBlock.Foreground = foreground;
					}
				}
			}
			if (_engineGlyphHosts.TryGetValue(engine.Name, out var value2))
			{
				value2.Child = CreateEngineGlyph(engine, flag ? BuddyTheme.SelectedInk : BuddyTokens.Text60Brush);
			}
		}
	}

	private void AttachHwndHook()
	{
		if (_hwndSource != null || _rootGrid == null)
		{
			return;
		}
		try
		{
			_hwndSource = PresentationSource.FromVisual(_rootGrid) as HwndSource;
			if (_hwndSource != null)
			{
				_hwndSource.AddHook(WndProc);
			}
		}
		catch
		{
		}
	}

	private void DetachHwndHook()
	{
		try
		{
			if (_hwndSource != null)
			{
				_hwndSource.RemoveHook(WndProc);
				_hwndSource = null;
			}
		}
		catch
		{
		}
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (_engineScrollViewer == null || !_engineScrollViewer.IsVisible)
		{
			return IntPtr.Zero;
		}
		if (msg == 526 && IsMouseOver(_engineScrollViewer))
		{
			short num = (short)((long)wParam >> 16);
			double num2 = ((Math.Abs(num) >= 120) ? ((double)num / 120.0) : ((double)Math.Sign(num))) * 60.0;
			_engineScrollViewer.ScrollToHorizontalOffset(_engineScrollViewer.HorizontalOffset + num2);
			handled = true;
		}
		else if (msg == 522 && IsMouseOver(_engineScrollViewer))
		{
			short num3 = (short)((long)wParam >> 16);
			_engineScrollViewer.ScrollToHorizontalOffset(_engineScrollViewer.HorizontalOffset + ((num3 < 0) ? 75.0 : (-75.0)));
			handled = true;
		}
		return IntPtr.Zero;
	}

	private static bool IsMouseOver(FrameworkElement el)
	{
		if (el == null || !el.IsVisible)
		{
			return false;
		}
		try
		{
			Point position = Mouse.GetPosition(el);
			return position.X >= -10.0 && position.X <= el.ActualWidth + 10.0 && position.Y >= -8.0 && position.Y <= el.ActualHeight + 8.0;
		}
		catch
		{
			return false;
		}
	}

	private void HandleChipStripWheel(object sender, MouseWheelEventArgs e)
	{
		if (_engineScrollViewer != null)
		{
			_engineScrollViewer.ScrollToHorizontalOffset(_engineScrollViewer.HorizontalOffset + ((e.Delta < 0) ? 75.0 : (-75.0)));
			e.Handled = true;
		}
	}

	private void SetupChipDragScroll()
	{
		_engineScrollViewer.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			_chipDragStartPoint = e.GetPosition(_engineScrollViewer);
			_isChipDragging = false;
			_suppressChipClick = false;
			_dragEngine = (from x in _engineChips
				where x.Value.IsMouseOver
				select FindEngine(x.Key)).FirstOrDefault();
		};
		_engineScrollViewer.PreviewMouseMove += delegate(object s, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed && _dragEngine != null)
			{
				double value2 = e.GetPosition(_engineScrollViewer).X - _chipDragStartPoint.X;
				if (!_isChipDragging && Math.Abs(value2) > SystemParameters.MinimumHorizontalDragDistance)
				{
					_isChipDragging = true;
					_suppressChipClick = true;
					_engineScrollViewer.CaptureMouse();
				}
				if (_isChipDragging)
				{
					Point position2 = e.GetPosition(_engineScrollViewer);
					if (position2.X < 24.0)
					{
						_engineScrollViewer.ScrollToHorizontalOffset(_engineScrollViewer.HorizontalOffset - 12.0);
					}
					else if (position2.X > _engineScrollViewer.ActualWidth - 24.0)
					{
						_engineScrollViewer.ScrollToHorizontalOffset(_engineScrollViewer.HorizontalOffset + 12.0);
					}
					e.Handled = true;
				}
			}
		};
		_engineScrollViewer.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_isChipDragging)
			{
				Point position = e.GetPosition(_chipsPanel);
				int val = _engines.Count - 1;
				for (int i = 0; i < _engines.Count; i++)
				{
					if (_engineChips.TryGetValue(_engines[i].Name, out var value))
					{
						Rect rect = value.TransformToAncestor(_chipsPanel).TransformBounds(new Rect(value.RenderSize));
						if (position.X < rect.Right)
						{
							val = i;
							break;
						}
					}
				}
				_engineScrollViewer.ReleaseMouseCapture();
				_isChipDragging = false;
				_engines.Remove(_dragEngine);
				_engines.Insert(Math.Min(val, _engines.Count), _dragEngine);
				_dragEngine = null;
				PersistEngines();
				e.Handled = true;
			}
		};
		_engineScrollViewer.LostMouseCapture += delegate
		{
			_isChipDragging = false;
		};
	}

	private Border BuildLinksCard()
	{
		Border border = new Border();
		border.Background = BuddyTokens.CardBrush;
		border.BorderBrush = BuddyTokens.CardBorderBrush;
		border.BorderThickness = new Thickness(0.0);
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(14.0, 6.0, 14.0, 8.0);
		border.SnapsToDevicePixels = true;
		border.UseLayoutRounding = true;
		border.Cursor = Cursors.Arrow;
		border.ClipToBounds = true;
		Border border2 = border;
		ThemeTokens.Card(border2);
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(24.0, GridUnitType.Pixel)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(6.0, GridUnitType.Pixel)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
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
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(new Border
		{
			Width = 12.0,
			Height = 12.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Child = CreateBookmarkIcon(12.0, BuddyTokens.Text38Brush)
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "Saved links",
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.0,
			FontWeight = AynType.Medium,
			Foreground = BuddyTokens.Text60Brush,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		});
		_countLabel = new TextBlock
		{
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.0,
			Foreground = BuddyTokens.Text38Brush,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(_countLabel);
		Grid.SetColumn(stackPanel2, 0);
		grid2.Children.Add(stackPanel2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		_prevBtn = MakeArrowButton(left: true);
		_prevBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			Page(-1);
			e.Handled = true;
		};
		stackPanel4.Children.Add(_prevBtn);
		_nextBtn = MakeArrowButton(left: false);
		_nextBtn.Margin = new Thickness(2.0, 0.0, 8.0, 0.0);
		_nextBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			Page(1);
			e.Handled = true;
		};
		stackPanel4.Children.Add(_nextBtn);
		Border border3 = MakePillButton("+ Add Link", 24.0, 12.0, 12.0, primary: true);
		border3.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_addRowVisible)
			{
				HideAddRow();
			}
			else
			{
				ShowAddRow(null);
			}
			e.Handled = true;
		};
		stackPanel4.Children.Add(border3);
		Grid.SetColumn(stackPanel4, 2);
		grid2.Children.Add(stackPanel4);
		Grid.SetRow(grid2, 0);
		grid.Children.Add(grid2);
		_addRowContainer = BuildAddLinkRow();
		_addRowContainer.Visibility = Visibility.Collapsed;
		Grid.SetRow(_addRowContainer, 2);
		grid.Children.Add(_addRowContainer);
		Grid grid3 = new Grid();
		grid3.ClipToBounds = true;
		Grid grid4 = grid3;
		_cardsRow = new UniformGrid
		{
			Rows = 1,
			Columns = 4,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(-4.0, 0.0, -4.0, 0.0)
		};
		_cardsRow.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			if (_bookmarks.Count > 4)
			{
				Page((e.Delta < 0) ? 1 : (-1));
				e.Handled = true;
			}
		};
		grid4.Children.Add(_cardsRow);
		_emptyState = new TextBlock
		{
			Text = "No saved links yet. Copied links appear here.",
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.0,
			Foreground = BuddyTokens.Text38Brush,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Visibility = Visibility.Collapsed
		};
		grid4.Children.Add(_emptyState);
		Grid.SetRow(grid4, 3);
		grid.Children.Add(grid4);
		border2.Child = grid;
		return border2;
	}

	private void Page(int dir)
	{
		int val = Math.Max(0, _bookmarks.Count - 4);
		int num = Math.Max(0, Math.Min(val, _pageStart + dir * 4));
		if (num != _pageStart)
		{
			_pageStart = num;
			RefreshLinksGrid();
		}
	}

	private void RefreshLinksGrid()
	{
		if (_cardsRow == null)
		{
			return;
		}
		int count = _bookmarks.Count;
		if (_countLabel != null)
		{
			_countLabel.Text = count + " link" + ((count == 1) ? "" : "s");
		}
		_pageStart = Math.Max(0, Math.Min(_pageStart, Math.Max(0, count - 4)));
		_cardsRow.Children.Clear();
		for (int i = _pageStart; i < Math.Min(count, _pageStart + 4); i++)
		{
			_cardsRow.Children.Add(BuildLinkCard(_bookmarks.OrderByDescending((LinksBookmark x) => x.Pinned).ElementAt(i)));
		}
		if (_emptyState != null)
		{
			_emptyState.Visibility = ((count != 0) ? Visibility.Collapsed : Visibility.Visible);
		}
		bool visible = count > 4;
		SetArrowState(_prevBtn, visible, _pageStart > 0);
		SetArrowState(_nextBtn, visible, _pageStart + 4 < count);
	}

	private Border BuildLinkCard(LinksBookmark bm)
	{
		string bmId = bm.Id;
		string url = bm.Url;
		string text = LinkUrl.Host(url);
		string text2 = ((!string.IsNullOrWhiteSpace(bm.Name)) ? bm.Name.Trim() : text);
		Border card = new Border
		{
			Height = 54.0,
			Margin = new Thickness(4.0, 0.0, 4.0, 0.0),
			CornerRadius = new CornerRadius(12.0),
			Background = Brushes.Transparent,
			BorderBrush = BuddyTokens.HairlineBrush,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(10.0, 0.0, 4.0, 0.0),
			Cursor = Cursors.Hand,
			ClipToBounds = true,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true,
			ToolTip = url
		};
		ToolTipService.SetInitialShowDelay(card, 700);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(26.0, GridUnitType.Pixel)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(22.0, GridUnitType.Pixel)
		});
		Border avatar = new Border
		{
			Width = 24.0,
			Height = 24.0,
			CornerRadius = new CornerRadius(6.0),
			Background = DomainFill(text),
			VerticalAlignment = VerticalAlignment.Center,
			Child = new TextBlock
			{
				Text = AvatarLetter(text, text),
				FontFamily = BuddyTokens.FontText,
				FontSize = 11.0,
				FontWeight = AynType.Medium,
				Foreground = BuddyTokens.Text80Brush,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		avatar.Clip = new RectangleGeometry(new Rect(0.0, 0.0, 24.0, 24.0), 6.0, 6.0);
		UIElement fallback = avatar.Child;
		Action loadIcon = async delegate
		{
			if (avatar.IsVisible)
			{
				if (!AppPreferences.Instance.ShowWebsiteIconsInLinks)
				{
					avatar.Child = fallback;
				}
				else
				{
					ImageSource image = await WebsiteIcons.GetAsync(url);
					if (avatar.IsVisible && AppPreferences.Instance.ShowWebsiteIconsInLinks && image != null)
					{
						avatar.Child = new Image
						{
							Source = image,
							Width = 24.0,
							Height = 24.0,
							Stretch = Stretch.Uniform
						};
					}
				}
			}
		};
		avatar.IsVisibleChanged += delegate
		{
			if (avatar.IsVisible)
			{
				loadIcon();
			}
		};
		EventHandler<string> privacy = delegate(object s, string key)
		{
			if (key == "ShowWebsiteIconsInLinks")
			{
				avatar.Child = fallback;
				if (avatar.IsVisible)
				{
					loadIcon();
				}
			}
		};
		avatar.Loaded += delegate
		{
			AppPreferences.Instance.Changed += privacy;
			loadIcon();
		};
		avatar.Unloaded += delegate
		{
			AppPreferences.Instance.Changed -= privacy;
		};
		grid.Children.Add(avatar);
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(8.0, 0.0, 2.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		Grid grid2 = new Grid();
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		if (bm.Pinned)
		{
			FrameworkElement frameworkElement = FluentIcons.Create("pin", 10.0, BuddyTheme.Text50, filled: false);
			frameworkElement.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			grid2.Children.Add(frameworkElement);
		}
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text2;
		textBlock.FontFamily = BuddyTokens.FontText;
		textBlock.FontSize = 13.5;
		textBlock.FontWeight = AynType.Medium;
		textBlock.Foreground = BuddyTokens.Text100Brush;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		textBlock.TextWrapping = TextWrapping.NoWrap;
		TextBlock element = textBlock;
		Grid.SetColumn(element, 1);
		grid2.Children.Add(element);
		stackPanel2.Children.Add(grid2);
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		Grid grid4 = grid3;
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid4.Children.Add(new TextBlock
		{
			Text = text,
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.0,
			Foreground = BuddyTokens.Text38Brush,
			VerticalAlignment = VerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextWrapping = TextWrapping.NoWrap
		});
		if (!_copiedUntil.TryGetValue(LinkUrl.Key(url), out var value))
		{
			value = (bm.FromClipboard ? new DateTime(bm.AddedTicks, DateTimeKind.Utc).AddMilliseconds(1500.0) : DateTime.MinValue);
		}
		if (value > DateTime.UtcNow)
		{
			Border tag = new Border
			{
				CornerRadius = new CornerRadius(4.0),
				Background = BuddyTokens.RowHoverBrush,
				Padding = new Thickness(4.0, 0.0, 4.0, 1.0),
				Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				Child = new TextBlock
				{
					Text = "Copied",
					FontFamily = BuddyTokens.FontText,
					FontSize = 10.0,
					Foreground = BuddyTokens.Text60Brush
				}
			};
			Grid.SetColumn(tag, 1);
			grid4.Children.Add(tag);
			DispatcherTimer expiry = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(Math.Max(1.0, (value - DateTime.UtcNow).TotalMilliseconds))
			};
			expiry.Tick += delegate
			{
				expiry.Stop();
				tag.Visibility = Visibility.Collapsed;
			};
			expiry.Start();
			tag.Unloaded += delegate
			{
				expiry.Stop();
			};
		}
		stackPanel2.Children.Add(grid4);
		Grid.SetColumn(stackPanel2, 1);
		grid.Children.Add(stackPanel2);
		Border menuBtn = new Border
		{
			Width = 20.0,
			Height = 24.0,
			CornerRadius = new CornerRadius(6.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right,
			Opacity = 0.4,
			Child = CreateKebabIcon(BuddyTokens.Text80Brush)
		};
		Grid.SetColumn(menuBtn, 2);
		grid.Children.Add(menuBtn);
		card.Child = grid;
		Action showMenu = delegate
		{
			BuddyDropdown.Show(menuBtn, new BuddyMenuItem[5]
			{
				new BuddyMenuItem("Open", null, delegate
				{
					OpenUrl(url);
				}),
				new BuddyMenuItem("Copy link", null, delegate
				{
					CopyLink(url);
				}),
				new BuddyMenuItem("Rename", null, delegate
				{
					ShowAddRow(bmId);
				}),
				new BuddyMenuItem(bm.Pinned ? "Unpin" : "Pin to top", null, delegate
				{
					bm.Pinned = !bm.Pinned;
					SaveBookmarks();
					_pageStart = 0;
					RefreshLinksGrid();
				}),
				new BuddyMenuItem("Delete", null, delegate
				{
					DeleteBookmark(bmId);
				})
			});
		};
		AutomationProperties.SetName(card, "Saved link " + text);
		AutomationProperties.SetName(menuBtn, "Link menu " + text);
		card.MouseEnter += delegate
		{
			card.Background = BuddyTheme.RowHover;
			menuBtn.Opacity = 0.85;
		};
		card.MouseLeave += delegate
		{
			card.Background = Brushes.Transparent;
			menuBtn.Opacity = 0.4;
		};
		menuBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			showMenu();
			e.Handled = true;
		};
		card.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			showMenu();
			e.Handled = true;
		};
		card.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (!e.Handled)
			{
				e.Handled = true;
				OpenUrl(url);
			}
		};
		return card;
	}

	internal static Brush DomainFill(string host)
	{
		uint num = 2166136261u;
		string text = host.ToLowerInvariant();
		foreach (char c in text)
		{
			num = (num ^ c) * 16777619;
		}
		Color[] array = new Color[5]
		{
			Color.FromRgb(70, 135, 235),
			Color.FromRgb(80, 175, 160),
			Color.FromRgb(145, 115, 220),
			Color.FromRgb(210, 115, 170),
			Color.FromRgb(90, 165, 205)
		};
		Color color = array[num % array.Length];
		return new SolidColorBrush(Color.FromArgb(48, color.R, color.G, color.B));
	}

	private static string AvatarLetter(string title, string host)
	{
		string[] array = new string[2] { title, host };
		foreach (string text in array)
		{
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			string text2 = text;
			foreach (char c in text2)
			{
				if (char.IsLetterOrDigit(c))
				{
					return char.ToUpperInvariant(c).ToString();
				}
			}
		}
		return "#";
	}

	private LinksBookmark FindBookmark(string id)
	{
		if (id != null)
		{
			return _bookmarks.FirstOrDefault((LinksBookmark b) => b.Id == id);
		}
		return null;
	}

	private LinksBookmark FindByKey(string url, string exceptId)
	{
		string key = LinkUrl.Key(url);
		return _bookmarks.FirstOrDefault((LinksBookmark b) => b.Id != exceptId && LinkUrl.Key(b.Url) == key);
	}

	private void DeleteBookmark(string id)
	{
		LinksBookmark linksBookmark = FindBookmark(id);
		if (linksBookmark != null)
		{
			_bookmarks.Remove(linksBookmark);
			if (_editingId == id)
			{
				HideAddRow();
			}
			SaveBookmarks();
			RefreshLinksGrid();
		}
	}

	private void CopyLink(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return;
		}
		bool flag = false;
		try
		{
			flag = ClipboardService.Instance.CopyToClipboard(url);
		}
		catch
		{
		}
		if (!flag)
		{
			try
			{
				System.Windows.Clipboard.SetText(url);
			}
			catch
			{
			}
		}
		_copiedUntil[LinkUrl.Key(url)] = DateTime.UtcNow.AddMilliseconds(1500.0);
		RefreshLinksGrid();
	}

	private Border BuildAddLinkRow()
	{
		Border border = new Border();
		border.Background = BuddyTokens.CardBrush;
		border.BorderBrush = BuddyTokens.HairlineBrush;
		border.BorderThickness = new Thickness(0.0);
		border.CornerRadius = new CornerRadius(12.0);
		border.Padding = new Thickness(8.0, 6.0, 8.0, 6.0);
		border.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		Border border2 = border;
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid();
		grid.Height = 28.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(8.0, GridUnitType.Pixel)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(150.0, GridUnitType.Pixel)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(8.0, GridUnitType.Pixel)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(6.0, GridUnitType.Pixel)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		KeyEventHandler value = delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				CommitAddLink();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				HideAddRow();
				e.Handled = true;
			}
		};
		_addUrlBox = CreateRoundedTextBox("Link (required)", 13.0, 28.0, 14.0, 2048);
		_addUrlBox.PreviewKeyDown += value;
		_addUrlBox.TextChanged += delegate
		{
			if (_validationHint != null && _validationHint.Visibility == Visibility.Visible && _addUrlBox.IsFocused)
			{
				ClearValidation();
			}
		};
		Grid.SetColumn(_addUrlBox, 0);
		grid2.Children.Add(_addUrlBox);
		_addNameBox = CreateRoundedTextBox("Title (optional)", 13.0, 28.0, 14.0, 80);
		_addNameBox.PreviewKeyDown += value;
		Grid.SetColumn(_addNameBox, 2);
		grid2.Children.Add(_addNameBox);
		Border border3 = MakePillButton("Save", 26.0, 13.0, 14.0, primary: true);
		border3.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			CommitAddLink();
			e.Handled = true;
		};
		Grid.SetColumn(border3, 4);
		grid2.Children.Add(border3);
		Border border4 = MakePillButton("Cancel", 26.0, 13.0, 14.0, primary: false);
		border4.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			HideAddRow();
			e.Handled = true;
		};
		Grid.SetColumn(border4, 6);
		grid2.Children.Add(border4);
		stackPanel.Children.Add(grid2);
		_validationHint = new TextBlock
		{
			FontFamily = BuddyTokens.FontText,
			FontSize = 11.0,
			Foreground = BuddyTokens.AlertBrush,
			Margin = new Thickness(6.0, 4.0, 0.0, 0.0),
			TextTrimming = TextTrimming.CharacterEllipsis,
			Visibility = Visibility.Collapsed
		};
		stackPanel.Children.Add(_validationHint);
		border2.Child = stackPanel;
		return border2;
	}

	private void ShowAddRow(string editId)
	{
		LinksBookmark linksBookmark = FindBookmark(editId);
		_addRowVisible = true;
		_editingId = linksBookmark?.Id;
		ClearValidation();
		if (_addRowContainer != null)
		{
			_addRowContainer.Visibility = Visibility.Visible;
		}
		SetTextBoxValue(_addUrlBox, (linksBookmark != null) ? linksBookmark.Url : "");
		SetTextBoxValue(_addNameBox, (linksBookmark != null) ? (linksBookmark.Name ?? "") : "");
		TextBox focus = ((linksBookmark != null) ? _addNameBox : _addUrlBox);
		if (focus == null)
		{
			return;
		}
		focus.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			focus.Focus();
			if (GetTextBoxValue(focus).Length > 0)
			{
				focus.SelectAll();
			}
		});
	}

	private void HideAddRow()
	{
		_addRowVisible = false;
		_editingId = null;
		ClearValidation();
		if (_addRowContainer != null)
		{
			_addRowContainer.Visibility = Visibility.Collapsed;
		}
		SetTextBoxValue(_addUrlBox, "");
		SetTextBoxValue(_addNameBox, "");
	}

	private void ClearValidation()
	{
		if (_validationHint != null)
		{
			_validationHint.Text = "";
			_validationHint.Visibility = Visibility.Collapsed;
		}
		if (_addUrlBox != null)
		{
			_addUrlBox.BorderBrush = BuddyTokens.HairlineBrush;
		}
	}

	private void ShowValidation(string message)
	{
		if (_validationHint != null)
		{
			_validationHint.Text = message;
			_validationHint.Visibility = Visibility.Visible;
		}
		if (_addUrlBox != null)
		{
			_addUrlBox.BorderBrush = BuddyTokens.AlertBrush;
			_addUrlBox.Focus();
		}
	}

	private void CommitAddLink()
	{
		if (_addUrlBox == null || _addNameBox == null)
		{
			return;
		}
		string textBoxValue = GetTextBoxValue(_addUrlBox);
		if (string.IsNullOrWhiteSpace(textBoxValue))
		{
			ShowValidation("Enter a link.");
			return;
		}
		if (!LinkUrl.TryNormalize(textBoxValue, out var uri))
		{
			ShowValidation("Enter a valid http(s) link, e.g. example.com");
			return;
		}
		string absoluteUri = uri.AbsoluteUri;
		LinksBookmark linksBookmark = FindByKey(absoluteUri, _editingId);
		if (linksBookmark != null)
		{
			ShowValidation("Already saved as “" + (string.IsNullOrWhiteSpace(linksBookmark.Name) ? LinkUrl.Host(linksBookmark.Url) : linksBookmark.Name) + "”.");
			return;
		}
		string textBoxValue2 = GetTextBoxValue(_addNameBox);
		LinksBookmark linksBookmark2 = FindBookmark(_editingId);
		if (linksBookmark2 != null)
		{
			linksBookmark2.Name = (string.IsNullOrWhiteSpace(textBoxValue2) ? null : textBoxValue2);
			linksBookmark2.Url = absoluteUri;
		}
		else
		{
			_bookmarks.Insert(0, new LinksBookmark(string.IsNullOrWhiteSpace(textBoxValue2) ? null : textBoxValue2, absoluteUri));
			_pageStart = 0;
		}
		SaveBookmarks();
		HideAddRow();
		RefreshLinksGrid();
	}

	private void SubscribeClipboard()
	{
		if (_clipboardSubscribed)
		{
			return;
		}
		try
		{
			ClipboardService.Instance.TextCopied -= OnTextCopied;
			ClipboardService.Instance.TextCopied += OnTextCopied;
			_clipboardSubscribed = true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Links: clipboard subscribe failed: " + ex.Message);
		}
	}

	private void UnsubscribeClipboard()
	{
		if (_clipboardSubscribed)
		{
			try
			{
				ClipboardService.Instance.TextCopied -= OnTextCopied;
			}
			catch
			{
			}
			_clipboardSubscribed = false;
		}
	}

	private void OnTextCopied(string text)
	{
		if (string.IsNullOrWhiteSpace(text) || text.Length > 2048)
		{
			return;
		}
		string text2 = text.Trim();
		if ((!text2.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !text2.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !text2.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) || !ClipboardService.IsUrlCandidate(text2) || !LinkUrl.TryNormalize(text2, out var uri))
		{
			return;
		}
		if (!IsInitialized)
		{
			Initialize();
		}
		string absoluteUri = uri.AbsoluteUri;
		if (FindByKey(absoluteUri, null) == null)
		{
			_bookmarks.Insert(0, new LinksBookmark(null, absoluteUri)
			{
				FromClipboard = true
			});
			List<LinksBookmark> list = _bookmarks.Where((LinksBookmark b) => b.FromClipboard).ToList();
			for (int i = 100; i < list.Count; i++)
			{
				_bookmarks.Remove(list[i]);
			}
			_pageStart = 0;
			SaveBookmarks();
			RefreshLinksGrid();
		}
	}

	private void PerformSearch()
	{
		if (_searchBox == null)
		{
			return;
		}
		string textBoxValue = GetTextBoxValue(_searchBox);
		SearchEngineItem searchEngineItem = FindEngine(_prefs.SelectedEngine);
		if (searchEngineItem == null || searchEngineItem.Type != 0)
		{
			searchEngineItem = _engines.FirstOrDefault((SearchEngineItem e) => e.Type == QuickLinkType.SearchEngine) ?? SearchEngines.CreateDefaults()[0];
		}
		if (string.IsNullOrWhiteSpace(textBoxValue))
		{
			OpenUrl(searchEngineItem.GetHomeUrl());
		}
		else
		{
			RunQuery(textBoxValue, searchEngineItem);
		}
	}

	private static void RunQuery(string query, SearchEngineItem engine)
	{
		if (ClipboardService.IsUrlCandidate(query) && LinkUrl.TryNormalize(query, out var uri))
		{
			OpenUrl(uri.AbsoluteUri);
		}
		else
		{
			OpenUrl(engine.BuildSearchUrl(query.Trim()));
		}
	}

	private void LoadAll()
	{
		List<LinksBookmark> list = null;
		try
		{
			list = PersistenceHelper.ReadWithBackup(_bookmarksPath, (string text) => new JavaScriptSerializer().Deserialize<List<LinksBookmark>>(text));
		}
		catch (Exception ex)
		{
			_bookmarksLoadFailed = true;
			TransactionLogger.Warn("Links: bookmarks unreadable; original files preserved and saving disabled: " + ex.GetType().Name);
		}
		List<LinksBookmark> list2 = new List<LinksBookmark>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (list != null)
		{
			foreach (LinksBookmark item in list)
			{
				if (item == null || !LinkUrl.TryNormalize(item.Url, out var uri))
				{
					continue;
				}
				item.Url = uri.AbsoluteUri;
				if (hashSet.Add(LinkUrl.Key(item.Url)))
				{
					if (string.IsNullOrEmpty(item.Id))
					{
						item.Id = Guid.NewGuid().ToString("N");
					}
					if (item.Name != null && item.Name.Length > 200)
					{
						item.Name = item.Name.Substring(0, 200);
					}
					list2.Add(item);
				}
			}
		}
		_bookmarks = list2;
		try
		{
			LinksPreferences linksPreferences = PersistenceHelper.SafeReadJson<LinksPreferences>(_prefsPath);
			if (linksPreferences != null)
			{
				_prefs = linksPreferences;
			}
		}
		catch (Exception ex2)
		{
			TransactionLogger.Warn("Links: prefs unreadable, using defaults: " + ex2.Message);
		}
		List<SearchEngineItem> list3 = null;
		if (_prefs.CustomEngines != null)
		{
			list3 = _prefs.CustomEngines.Where((SearchEngineItem e) => e != null && !string.IsNullOrWhiteSpace(e.Name) && IsSafeEngine(e)).ToList();
		}
		_engines = list3 ?? SearchEngines.CreateDefaults();
		SearchEngineItem searchEngineItem = FindEngine(_prefs.SelectedEngine);
		if (searchEngineItem == null || searchEngineItem.Type != 0)
		{
			_prefs.SelectedEngine = FirstSearchEngineName();
		}
	}

	private static bool IsSafeEngine(SearchEngineItem e)
	{
		string text = ((e.Type == QuickLinkType.SearchEngine) ? (e.SearchUrl ?? "").Replace("{0}", "q").Replace("%s", "q") : e.SearchUrl);
		Uri uri;
		if (!string.IsNullOrEmpty(text) && (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
		{
			return LinkUrl.TryNormalize(text, out uri);
		}
		return false;
	}

	private void SaveAll()
	{
		SaveBookmarks();
		SavePrefs();
	}

	private void SaveBookmarks()
	{
		this.BookmarksChanged?.Invoke(this, EventArgs.Empty);
		if (_bookmarksLoadFailed)
		{
			return;
		}
		try
		{
			PersistenceHelper.SafeWriteJson(_bookmarksPath, _bookmarks);
		}
		catch
		{
		}
	}

	private void SavePrefs()
	{
		try
		{
			PersistenceHelper.SafeWriteJson(_prefsPath, _prefs);
		}
		catch
		{
		}
	}

	public static bool OpenUrl(string rawUrl)
	{
		if (!LinkUrl.TryNormalize(rawUrl, out var uri))
		{
			return false;
		}
		try
		{
			LaunchBrowser(uri);
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Links: failed to open URL: " + ex.Message);
			return false;
		}
	}

	private static TextBox CreateRoundedTextBox(string placeholder, double fontSize, double height, double cornerRadius, int maxLength)
	{
		TextBox tb = new TextBox
		{
			Height = height,
			FontFamily = BuddyTokens.FontText,
			FontSize = fontSize,
			Foreground = BuddyTokens.Text38Brush,
			Background = BuddyTokens.CardBrush,
			BorderBrush = BuddyTokens.HairlineBrush,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(Math.Min(cornerRadius, 14.0), 0.0, Math.Min(cornerRadius, 14.0), 0.0),
			CaretBrush = BuddyTokens.Text100Brush,
			SelectionBrush = BuddyTokens.CardHoverBrush,
			VerticalContentAlignment = VerticalAlignment.Center,
			Tag = placeholder
		};
		if (maxLength > 0)
		{
			tb.MaxLength = maxLength;
		}
		ControlTemplate controlTemplate = new ControlTemplate(typeof(TextBox));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		frameworkElementFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
		frameworkElementFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ScrollViewer));
		frameworkElementFactory2.Name = "PART_ContentHost";
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		tb.Template = controlTemplate;
		tb.Text = placeholder;
		tb.GotFocus += delegate
		{
			if ((string)tb.Tag == tb.Text && tb.Foreground == BuddyTokens.Text38Brush)
			{
				tb.Text = "";
				tb.Foreground = BuddyTokens.Text100Brush;
			}
		};
		tb.LostFocus += delegate
		{
			if (string.IsNullOrEmpty(tb.Text))
			{
				tb.Text = (string)tb.Tag;
				tb.Foreground = BuddyTokens.Text38Brush;
			}
		};
		return tb;
	}

	private static string GetTextBoxValue(TextBox tb)
	{
		if (tb == null)
		{
			return "";
		}
		if (tb.Foreground == BuddyTokens.Text38Brush && tb.Text == tb.Tag as string)
		{
			return "";
		}
		return (tb.Text ?? "").Trim();
	}

	private static void SetTextBoxValue(TextBox tb, string value)
	{
		if (tb != null)
		{
			if (!string.IsNullOrEmpty(value))
			{
				tb.Text = value;
				tb.Foreground = BuddyTokens.Text100Brush;
			}
			else if (tb.IsKeyboardFocused)
			{
				tb.Text = "";
				tb.Foreground = BuddyTokens.Text100Brush;
			}
			else
			{
				tb.Text = (tb.Tag as string) ?? "";
				tb.Foreground = BuddyTokens.Text38Brush;
			}
		}
	}

	private static Border MakePillButton(string text, double height, double radius, double padH, bool primary)
	{
		Border b = new Border
		{
			Height = height,
			CornerRadius = new CornerRadius(radius),
			Background = (primary ? BuddyTheme.PrimaryFill : BuddyTokens.CardBrush),
			BorderBrush = (primary ? null : BuddyTokens.HairlineBrush),
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(padH, 0.0, padH, 0.0),
			Cursor = Cursors.Hand,
			VerticalAlignment = VerticalAlignment.Center,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true
		};
		TextBlock label = new TextBlock
		{
			Text = text,
			FontFamily = BuddyTokens.FontText,
			FontSize = 13.0,
			FontWeight = AynType.Medium,
			Foreground = (primary ? BuddyTheme.PrimaryInk : BuddyTokens.Text60Brush),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		b.Child = label;
		if (primary)
		{
			b.MouseEnter += delegate
			{
				b.Opacity = 0.9;
			};
			b.MouseLeave += delegate
			{
				b.Opacity = 1.0;
			};
		}
		else
		{
			b.MouseEnter += delegate
			{
				b.Background = BuddyTokens.CardHoverBrush;
				label.Foreground = BuddyTokens.Text100Brush;
			};
			b.MouseLeave += delegate
			{
				b.Background = BuddyTokens.CardBrush;
				label.Foreground = BuddyTokens.Text60Brush;
			};
		}
		return b;
	}

	private static Border MakeArrowButton(bool left)
	{
		System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
		path.Data = Geometry.Parse(left ? "M 6,1.5 L 2.5,5 L 6,8.5" : "M 3,1.5 L 6.5,5 L 3,8.5");
		path.Stroke = BuddyTokens.Text80Brush;
		path.StrokeThickness = 1.4;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.Width = 9.0;
		path.Height = 10.0;
		path.HorizontalAlignment = HorizontalAlignment.Center;
		path.VerticalAlignment = VerticalAlignment.Center;
		System.Windows.Shapes.Path child = path;
		Border b = new Border
		{
			Width = 22.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(11.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			VerticalAlignment = VerticalAlignment.Center,
			Child = child,
			Visibility = Visibility.Collapsed
		};
		b.MouseEnter += delegate
		{
			if (b.IsEnabled)
			{
				b.Background = BuddyTokens.CardHoverBrush;
			}
		};
		b.MouseLeave += delegate
		{
			b.Background = Brushes.Transparent;
		};
		return b;
	}

	private static void SetArrowState(Border b, bool visible, bool enabled)
	{
		if (b != null)
		{
			b.Visibility = ((!visible) ? Visibility.Collapsed : Visibility.Visible);
			b.IsEnabled = enabled;
			b.Opacity = (enabled ? 0.9 : 0.25);
			b.Cursor = (enabled ? Cursors.Hand : Cursors.Arrow);
			if (!enabled)
			{
				b.Background = Brushes.Transparent;
			}
		}
	}

	private static Grid WrapWithSheen(FrameworkElement card)
	{
		Grid grid = new Grid();
		grid.Children.Add(card);
		grid.Children.Add(new Border
		{
			Height = 44.0,
			CornerRadius = new CornerRadius(14.0, 14.0, 0.0, 0.0),
			Background = BuddyTokens.CardSheenBrush,
			BorderThickness = new Thickness(0.0),
			VerticalAlignment = VerticalAlignment.Top,
			IsHitTestVisible = false,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true,
			Margin = new Thickness(1.0, 1.0, 1.0, 0.0)
		});
		return grid;
	}

	private static FrameworkElement CreateEngineGlyph(SearchEngineItem engine, Brush brush)
	{
		string text = null;
		bool flag = false;
		switch ((engine.Name ?? "").ToLowerInvariant())
		{
		case "google":
			text = "M13.6,8 H8.4 M13.6,8 A5.6,5.6 0 1 1 11.96,4.04";
			break;
		case "youtube":
			text = "F0 M1.5,5 C1.5,3.6 2.6,2.8 4,2.8 L12,2.8 C13.4,2.8 14.5,3.6 14.5,5 L14.5,11 C14.5,12.4 13.4,13.2 12,13.2 L4,13.2 C2.6,13.2 1.5,12.4 1.5,11 Z M6.5,5.4 L10.6,8 L6.5,10.6 Z";
			flag = true;
			break;
		case "github":
			text = "M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0 0 16 8c0-4.42-3.58-8-8-8z";
			flag = true;
			break;
		case "stack overflow":
			text = "M3,9.5 V14 H12.5 V9.5 M5,12 H10.5 M5.2,9.6 L10.6,10.4 M5.9,7.2 L11,8.9 M7.3,4.9 L11.8,7.7 M9.3,2.8 L12.6,6.8";
			break;
		case "chatgpt":
			text = "M8,1.6 L13.5,4.8 L13.5,11.2 L8,14.4 L2.5,11.2 L2.5,4.8 Z M8,5.2 L10.4,6.6 L10.4,9.4 L8,10.8 L5.6,9.4 L5.6,6.6 Z";
			break;
		case "perplexity":
			text = "M8,1.5 V14.5 M2.6,4.6 L13.4,11.4 M13.4,4.6 L2.6,11.4";
			break;
		}
		if (text == null)
		{
			return CreateGlobeIcon(12.0, brush);
		}
		try
		{
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
			path.Data = Geometry.Parse(text);
			path.Stretch = Stretch.Uniform;
			path.Width = 12.0;
			path.Height = 12.0;
			path.StrokeStartLineCap = PenLineCap.Round;
			path.StrokeEndLineCap = PenLineCap.Round;
			path.StrokeLineJoin = PenLineJoin.Round;
			path.SnapsToDevicePixels = true;
			System.Windows.Shapes.Path path2 = path;
			if (flag)
			{
				path2.Fill = brush;
			}
			else
			{
				path2.Stroke = brush;
				path2.StrokeThickness = 1.3;
			}
			return path2;
		}
		catch
		{
			return CreateGlobeIcon(12.0, brush);
		}
	}

	private static FrameworkElement CreateKebabIcon(Brush fill)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		for (int i = 0; i < 3; i++)
		{
			stackPanel2.Children.Add(new Ellipse
			{
				Width = 3.0,
				Height = 3.0,
				Fill = fill,
				Margin = new Thickness(0.0, (i != 0) ? 2 : 0, 0.0, 0.0)
			});
		}
		return stackPanel2;
	}

	private static UIElement CreateSearchIcon(double size, Brush stroke)
	{
		double num = size / 16.0;
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 9.0 * num;
		ellipse.Height = 9.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = 1.4 * num;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 1.5 * num);
		Canvas.SetTop(element, 1.5 * num);
		canvas2.Children.Add(element);
		canvas2.Children.Add(new Line
		{
			X1 = 9.3 * num,
			Y1 = 9.3 * num,
			X2 = 14.0 * num,
			Y2 = 14.0 * num,
			Stroke = stroke,
			StrokeThickness = 1.4 * num,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		});
		return canvas2;
	}

	private static UIElement CreateBookmarkIcon(double size, Brush fill)
	{
		double num = size / 16.0;
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		canvas2.Children.Add(new System.Windows.Shapes.Path
		{
			Fill = fill,
			Data = Geometry.Parse(string.Format(CultureInfo.InvariantCulture, "M {0},{1} L {2},{1} L {2},{3} L {4},{5} L {0},{3} Z", 2.0 * num, 1.0 * num, 14.0 * num, 12.0 * num, 8.0 * num, 15.0 * num))
		});
		return canvas2;
	}

	private static FrameworkElement CreateGlobeIcon(double size, Brush stroke)
	{
		double num = size / 16.0;
		double strokeThickness = Math.Max(1.0, 1.2 * num);
		Canvas canvas = new Canvas();
		canvas.Width = size;
		canvas.Height = size;
		Canvas canvas2 = canvas;
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 13.0 * num;
		ellipse.Height = 13.0 * num;
		ellipse.Stroke = stroke;
		ellipse.StrokeThickness = strokeThickness;
		Ellipse element = ellipse;
		Canvas.SetLeft(element, 1.5 * num);
		Canvas.SetTop(element, 1.5 * num);
		canvas2.Children.Add(element);
		canvas2.Children.Add(new Line
		{
			X1 = 1.5 * num,
			Y1 = 8.0 * num,
			X2 = 14.5 * num,
			Y2 = 8.0 * num,
			Stroke = stroke,
			StrokeThickness = strokeThickness
		});
		Ellipse ellipse2 = new Ellipse();
		ellipse2.Width = 6.0 * num;
		ellipse2.Height = 13.0 * num;
		ellipse2.Stroke = stroke;
		ellipse2.StrokeThickness = strokeThickness;
		Ellipse element2 = ellipse2;
		Canvas.SetLeft(element2, 5.0 * num);
		Canvas.SetTop(element2, 1.5 * num);
		canvas2.Children.Add(element2);
		return canvas2;
	}
}
