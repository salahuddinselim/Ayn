using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Tools.Drop;

namespace WindowsBuddy.Tools.FileShelf;

public class FileShelfTool : IBuddyTool, IFileDropTarget, IEscapeAware
{
	private sealed class ShelfRow
	{
		public string Id;

		public Border Root;

		public Grid Body;

		public TextBlock Name;

		public TextBlock Count;

		public Border More;

		public ContentControl Icon;

		public bool Active;

		public bool Target;

		public bool MenuOpen;
	}

	private sealed class Tile
	{
		public ParkedFile Item;

		public volatile bool Alive = true;

		public Grid Root;

		public readonly TranslateTransform Position = new TranslateTransform();

		public Border Surface;

		public Grid Thumb;

		public TextBlock Name;

		public TextBlock Meta;

		public StackPanel Actions;

		public FrameworkElement LocateAction;

		public FrameworkElement OpenAction;

		public FrameworkElement FolderAction;

		public Border Preview;

		public Border Play;

		public Image Icon;

		public ImageBrush PreviewBrush;

		public volatile int Generation;

		public bool Hover;

		public bool DeferredThumbnail;
	}

	private sealed class Viewport : Grid
	{
		public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register("Offset", typeof(double), typeof(Viewport), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((Viewport)d)._owner.OnOffsetChanged();
		}));

		private readonly FileShelfTool _owner;

		public double Target;

		public double Offset
		{
			get
			{
				return (double)GetValue(OffsetProperty);
			}
			set
			{
				SetValue(OffsetProperty, value);
			}
		}

		public Viewport(FileShelfTool owner)
		{
			_owner = owner;
		}
	}

	private sealed class ScrollAnimation : DoubleAnimationBase
	{
		private static readonly DependencyProperty OwnerProperty = DependencyProperty.Register("Owner", typeof(FileShelfTool), typeof(ScrollAnimation), new PropertyMetadata(null));

		internal FileShelfTool Owner
		{
			get
			{
				return (FileShelfTool)GetValue(OwnerProperty);
			}
			set
			{
				SetValue(OwnerProperty, value);
			}
		}

		protected override Freezable CreateInstanceCore()
		{
			return new ScrollAnimation();
		}

		protected override double GetCurrentValueCore(double origin, double destination, AnimationClock clock)
		{
			return Owner?.CurrentScrollY(clock) ?? origin;
		}
	}

	private sealed class TextLink : Border
	{
		private readonly TextBlock _text;

		private readonly ContentControl _chevron;

		private readonly Brush _rest;

		private string _label;

		private bool _enabled = true;

		private DispatcherTimer _timer;

		public bool ShowingFeedback { get; private set; }

		public string Label
		{
			get
			{
				return _label;
			}
			set
			{
				_label = value;
				if (!ShowingFeedback)
				{
					_text.Text = value;
				}
			}
		}

		public bool Enabled
		{
			get
			{
				return _enabled;
			}
			set
			{
				if (_enabled != value)
				{
					_enabled = value;
					Paint();
				}
			}
		}

		public event Action Click;

		public TextLink(string label, bool chevron, Brush rest = null)
		{
			_rest = rest ?? BuddyTheme.Text50;
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			_text = BuddyTheme.Text(label, 13.0, _rest, FontWeights.Medium);
			_text.VerticalAlignment = VerticalAlignment.Center;
			Typography.SetNumeralAlignment(_text, FontNumeralAlignment.Tabular);
			stackPanel.Children.Add(_text);
			if (chevron)
			{
				_chevron = new ContentControl
				{
					Width = 12.0,
					Height = 12.0,
					Margin = new Thickness(4.0, 1.0, 0.0, 0.0),
					Focusable = false,
					IsTabStop = false,
					VerticalAlignment = VerticalAlignment.Center
				};
				stackPanel.Children.Add(_chevron);
			}
			_label = label;
			Child = stackPanel;
			base.Background = Brushes.Transparent;
			base.Padding = new Thickness(0.0, 4.0, 0.0, 4.0);
			base.Cursor = Cursors.Hand;
			base.MouseEnter += delegate
			{
				Paint();
			};
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
				Action click = this.Click;
				if (_enabled)
				{
					click?.Invoke();
				}
			};
			Paint();
		}

		public void ShowFeedback(string text)
		{
			ShowingFeedback = true;
			_text.Text = text;
			Paint();
			if (_timer == null)
			{
				_timer = new DispatcherTimer
				{
					Interval = BuddyTheme.FeedbackHold
				};
				_timer.Tick += delegate
				{
					_timer.Stop();
					ShowingFeedback = false;
					_text.Text = _label;
					Paint();
				};
			}
			_timer.Stop();
			_timer.Start();
		}

		private void Paint()
		{
			Brush brush = ((!_enabled) ? BuddyTheme.Text30 : ((base.IsMouseOver || ShowingFeedback) ? BuddyTheme.Text100 : _rest));
			_text.Foreground = brush;
			base.Cursor = (_enabled ? Cursors.Hand : Cursors.Arrow);
			if (_chevron != null)
			{
				_chevron.Content = FluentIcons.Create("chevron_down", 12.0, brush, filled: false);
			}
		}
	}

	private const double SidebarWidth = 172.0;

	private const double SidebarGap = 12.0;

	private const double ContentInset = 16.0;

	private const double ShelfRowH = 32.0;

	private const double HeaderH = 24.0;

	private const double DropZoneH = 30.0;

	private const double SectionGap = 8.0;

	private const int Columns = 5;

	private const double ThumbSize = 64.0;

	private const double TileH = 112.0;

	private const double RowPitch = 116.0;

	private const double FadeEdge = 22.0;

	public const string InternalDragFormat = "WindowsBuddy.ShelfItems";

	private static readonly Brush HoverFill = BuddyTheme.CardFill;

	private static readonly Brush ActiveRow = BuddyTheme.Row;

	private static readonly Brush TargetRow = BuddyTheme.RowHover;

	private static readonly Brush Ring = BuddyTheme.Text100;

	private static readonly Brush DashIdle = Frozen(Color.FromRgb(58, 58, 60));

	private static readonly Brush DropFill = ThemeTokens.AccentSoft;

	private static readonly Brush BandFill = ThemeTokens.AccentSoft;

	private static readonly Brush BandStroke = ThemeTokens.Accent;

	private static readonly Brush PlayDisc = Frozen(Color.FromArgb(150, 0, 0, 0));

	private readonly FileShelfService _svc = FileShelfService.Instance;

	private EventHandler<FileShelfService.ViewChange> _viewHandler;

	private bool _viewReady;

	private string _activeViewId;

	private ShelfSort _viewSort;

	private FileShelfService.ViewShelf[] _shelfHeaders = new FileShelfService.ViewShelf[0];

	private readonly Dictionary<string, Dictionary<string, ParkedFile>> _shelfFiles = new Dictionary<string, Dictionary<string, ParkedFile>>();

	private readonly Dictionary<string, int> _itemIndices = new Dictionary<string, int>();

	private readonly Dictionary<string, string> _fileShelves = new Dictionary<string, string>();

	private long _totalBytes;

	private int _missingCount;

	private readonly HashSet<string> _itemIds = new HashSet<string>();

	private readonly Stack<Tile> _tilePool = new Stack<Tile>();

	private readonly List<string> _tileRemovals = new List<string>(40);

	private int _realizedFirst = -1;

	private int _realizedLast = -1;

	private bool _realizeDirty = true;

	private AnimationClock _scrollAnimation;

	private readonly EventHandler _scrollTickHandler;

	private double _scrollFrom;

	private double _scrollTo;

	private long _scrollStarted;

	private long _lastScrollTick = -1L;

	private bool _scrollAnimating;

	private bool _fastScroll;

	private int _scrollDirection = 1;

	private Grid _root;

	private Grid _dropRow;

	private StackPanel _shelfList;

	private ScrollViewer _shelfScroll;

	private Border _newShelfRow;

	private readonly Dictionary<string, ShelfRow> _rows = new Dictionary<string, ShelfRow>();

	private TextBlock _title;

	private TextBlock _meta;

	private TextLink _sortBtn;

	private TextLink _selectBtn;

	private TextLink _copyBtn;

	private TextLink _clearBtn;

	private Viewport _viewport;

	private Canvas _canvas;

	private TranslateTransform _canvasShift;

	private Border _scrollThumb;

	private Grid _dropZone;

	private Rectangle _dropDash;

	private TextBlock _dropText;

	private FrameworkElement _dropIcon;

	private FrameworkElement _dropDivider;

	private TextLink _chooseBtn;

	private TextLink _recoveryBtn;

	private BuddyUndoToast _toast;

	private Rectangle _band;

	private string _shelfId;

	private List<ParkedFile> _items = new List<ParkedFile>();

	private readonly Dictionary<string, Tile> _tiles = new Dictionary<string, Tile>();

	private readonly HashSet<string> _selected = new HashSet<string>();

	private string _anchorId;

	private bool _selectMode;

	private double _colWidth = 105.0;

	private bool _fadeTop;

	private bool _fadeBottom;

	private bool _fadeInit;

	private DispatcherTimer _thumbFade;

	private DispatcherTimer _dragLeaveTimer;

	private string _renamingId;

	private bool _sidebarDirty;

	private string _sidebarSig;

	private Point _pressPoint;

	private Tile _pressTile;

	private bool _pressDeferSelect;

	private bool _banding;

	private Point _bandOrigin;

	private HashSet<string> _bandBase;

	private bool _dragActive;

	private string _dragTargetShelf;

	private bool _dragInternal;

	private DateTime _lastDragOver;

	private TextBox _renameBox;

	public string Id => "shelf";

	public string DisplayName => "File Shelf";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	private double ContentHeight => Math.Ceiling((double)_items.Count / 5.0) * 116.0;

	private double MaxOffset => Math.Max(0.0, ContentHeight - ((_viewport != null) ? _viewport.ActualHeight : 0.0));

	private double TileWidth => Math.Max(80.0, _colWidth - 4.0);

	private static Brush Frozen(Color c)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(c);
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public FileShelfTool()
	{
		_scrollTickHandler = OnScrollTick;
		IsEnabled = true;
		OrderIndex = 9;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		FileShelfService.PrepareEditor = delegate
		{
			CommitRename();
			if (_toast != null)
			{
				_toast.Commit();
			}
			EndBand();
		};
		_viewHandler = delegate(object s, FileShelfService.ViewChange e)
		{
			if (IsInitialized && _viewport != null)
			{
				ApplyView(e);
			}
		};
		_svc.ViewChanged += _viewHandler;
	}

	public void Shutdown()
	{
		if (_viewHandler != null)
		{
			_svc.ViewChanged -= _viewHandler;
		}
		if (_thumbFade != null)
		{
			_thumbFade.Stop();
		}
		StopScroll();
		foreach (Tile value in _tiles.Values)
		{
			value.Alive = false;
			value.Generation++;
		}
		_viewReady = false;
		if (_dragLeaveTimer != null)
		{
			_dragLeaveTimer.Stop();
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		_svc.RefreshStatus();
		RefreshContent();
		RefreshSidebar();
		if (_root == null)
		{
			return;
		}
		_root.Dispatcher.BeginInvoke((Action)delegate
		{
			if (IsInitialized && _svc.Operations.AdmissionOpen && _root.IsVisible)
			{
				_root.Focus();
			}
		}, DispatcherPriority.Input);
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		StopScroll();
		CommitRename();
		EndBand();
		if (_toast != null)
		{
			_toast.Commit();
		}
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateFileShelfIcon(size, stroke);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return new CollapsedBadgeData($"Shelf: {_svc.ActiveFileCount}", "#F4F4F5");
	}

	public void OnFileDragOver(bool over)
	{
		if (over)
		{
			BeginDropFeedback(null, isInternal: false);
		}
		else
		{
			EndDropFeedback();
		}
	}

	public async void OnFilesDropped(string[] paths)
	{
		EndDropFeedback();
		if (paths != null)
		{
			await AddPaths(CurrentShelfId(), paths);
		}
	}

	private async Task<int> AddPaths(string shelfId, string[] paths)
	{
		if (paths == null || paths.Length == 0)
		{
			return 0;
		}
		List<string> present = new List<string>();
		int gone = 0;
		Dispatcher dispatcher = ((_root != null) ? _root.Dispatcher : Dispatcher.CurrentDispatcher);
		try
		{
			string[] input = (string[])paths.Clone();
			await Task.Run(delegate
			{
				string[] array = input;
				foreach (string text in array)
				{
					if (!string.IsNullOrWhiteSpace(text))
					{
						if (File.Exists(text) || Directory.Exists(text))
						{
							present.Add(text);
						}
						else
						{
							gone++;
						}
					}
				}
			});
			return await dispatcher.InvokeAsync(() => CompleteAddPaths(shelfId, present, gone), DispatcherPriority.Background).Task;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: add failed: " + ex.Message);
			ReportAddFailure(dispatcher);
			return 0;
		}
	}

	private void ReportAddFailure(Dispatcher dispatcher)
	{
		if (dispatcher.HasShutdownStarted)
		{
			return;
		}
		dispatcher.BeginInvoke((Action)delegate
		{
			if (IsInitialized && _toast != null)
			{
				_toast.ShowNotice("Couldn't add the files · try again");
			}
		});
	}

	private int CompleteAddPaths(string shelfId, List<string> present, int gone)
	{
		if (!IsInitialized || !_svc.Operations.AdmissionOpen)
		{
			return 0;
		}
		shelfId = _svc.ResolveShelfId(shelfId);
		int num = ((present.Count > 0) ? _svc.AddFilesTo(shelfId, present, owned: false) : 0);
		int num2 = present.Count - num;
		string s = _svc.ResolveShelfName(shelfId);
		string text = null;
		if (num == 0)
		{
			text = ((gone > 0 && num2 == 0) ? ("Nothing added · " + ((gone == 1) ? "file not found" : "files not found")) : ((gone == 0) ? ("Already on " + Ellipsize(s, 24)) : "Nothing added · already here or not found"));
		}
		else if (num2 + gone > 0)
		{
			text = "Added " + num + " · " + (num2 + gone) + " skipped (already here or not found)";
		}
		else if (shelfId != _shelfId)
		{
			text = "Added " + num + " to " + Ellipsize(s, 24);
		}
		if (text != null && _toast != null)
		{
			_toast.ShowNotice(text);
		}
		return num;
	}

	public bool HandleEscape()
	{
		if (_renamingId != null)
		{
			CancelRename();
			return true;
		}
		if (_banding)
		{
			EndBand();
			return true;
		}
		if (_selectMode || _selected.Count > 0)
		{
			_selectMode = false;
			ClearSelection();
			return true;
		}
		return false;
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			return _root;
		}
		_root = BuddyTheme.Crisp(new Grid
		{
			Background = BuddyTheme.Canvas,
			AllowDrop = true,
			Focusable = true,
			FocusVisualStyle = null,
			Tag = "NoOuterScroll",
			ClipToBounds = true
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(172.0)
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(12.0)
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0)
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(16.0)
		});
		_root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement element = BuildSidebar();
		Grid.SetColumn(element, 0);
		_root.Children.Add(element);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.IsHitTestVisible = false;
		Rectangle element2 = rectangle;
		Grid.SetColumn(element2, 2);
		_root.Children.Add(element2);
		Grid element3 = BuildContent();
		Grid.SetColumn(element3, 4);
		_root.Children.Add(element3);
		_root.PreviewKeyDown += OnPreviewKeyDown;
		_root.Unloaded += delegate
		{
			StopScroll();
		};
		_root.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (_renameBox == null || !IsInside(e.OriginalSource as DependencyObject, _renameBox))
			{
				_root.Focus();
			}
		};
		_root.PreviewDragEnter += OnPreviewDragOver;
		_root.PreviewDragOver += OnPreviewDragOver;
		_root.PreviewDragLeave += OnPreviewDragLeave;
		_root.PreviewDrop += OnPreviewDrop;
		RefreshSidebar();
		RefreshContent();
		return _root;
	}

	private FrameworkElement BuildSidebar()
	{
		Grid grid = new Grid();
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
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		BuddyLabel buddyLabel = BuddyTheme.LabelText("SHELVES");
		buddyLabel.Margin = new Thickness(10.0, 4.0, 0.0, 8.0);
		buddyLabel.HorizontalAlignment = HorizontalAlignment.Left;
		grid.Children.Add(buddyLabel);
		_shelfList = new StackPanel();
		_shelfScroll = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Focusable = false,
			Content = _shelfList
		};
		Grid.SetRow(_shelfScroll, 1);
		grid.Children.Add(_shelfScroll);
		Rectangle rectangle = new Rectangle();
		rectangle.Height = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.Margin = new Thickness(10.0, 6.0, 10.0, 6.0);
		rectangle.IsHitTestVisible = false;
		Rectangle element = rectangle;
		Grid.SetRow(element, 2);
		grid.Children.Add(element);
		_newShelfRow = BuildNewShelfRow();
		Grid.SetRow(_newShelfRow, 3);
		grid.Children.Add(_newShelfRow);
		return grid;
	}

	private Border BuildNewShelfRow()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		ContentControl icon = new ContentControl
		{
			Width = 16.0,
			Height = 16.0,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			Focusable = false,
			IsTabStop = false
		};
		TextBlock text = BuddyTheme.Text("New shelf", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		text.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(icon);
		stackPanel2.Children.Add(text);
		Border row = new Border
		{
			Height = 32.0,
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = stackPanel2
		};
		Action paint = delegate
		{
			bool canCreateShelf = _svc.CanCreateShelf;
			bool flag = canCreateShelf && row.IsMouseOver;
			Brush brush = ((!canCreateShelf) ? BuddyTheme.Text30 : (flag ? BuddyTheme.Text92 : BuddyTheme.Text50));
			icon.Content = FluentIcons.Create("add", 16.0, brush, filled: false);
			text.Foreground = brush;
			row.Background = (flag ? HoverFill : Brushes.Transparent);
			row.Cursor = (canCreateShelf ? Cursors.Hand : Cursors.Arrow);
			row.ToolTip = (canCreateShelf ? null : BuddyTooltip.Create($"You can have up to {12} shelves"));
		};
		row.Tag = paint;
		row.MouseEnter += delegate
		{
			paint();
		};
		row.MouseLeave += delegate
		{
			paint();
		};
		row.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		row.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (_svc.CanCreateShelf)
			{
				CommitRename();
				ShelfTab shelfTab = _svc.CreateShelf();
				if (shelfTab != null)
				{
					BeginRename(shelfTab.Id);
				}
			}
		};
		paint();
		return row;
	}

	private void RefreshSidebar()
	{
		if (_shelfList == null)
		{
			return;
		}
		if (!_viewReady)
		{
			RefreshContent();
		}
		if (_renamingId != null)
		{
			_sidebarDirty = true;
			UpdateRowCounts();
			return;
		}
		_sidebarDirty = false;
		FileShelfService.ViewShelf[] shelfHeaders = _shelfHeaders;
		string activeViewId = _activeViewId;
		if (_newShelfRow.Tag is Action action)
		{
			action();
		}
		string text = activeViewId + "\n" + string.Join("\n", shelfHeaders.Select((FileShelfService.ViewShelf x) => x.Id + "\t" + x.Name));
		if (text == _sidebarSig)
		{
			UpdateRowCounts();
			return;
		}
		_sidebarSig = text;
		_shelfList.Children.Clear();
		_rows.Clear();
		FileShelfService.ViewShelf[] array = shelfHeaders;
		foreach (FileShelfService.ViewShelf viewShelf in array)
		{
			ShelfRow shelfRow = BuildShelfRow(viewShelf, viewShelf.Id == activeViewId);
			_rows[viewShelf.Id] = shelfRow;
			_shelfList.Children.Add(shelfRow.Root);
		}
		if (_shelfId != activeViewId)
		{
			RefreshContent();
		}
		else
		{
			UpdateHeader();
		}
		if (_rows.TryGetValue(activeViewId, out var active))
		{
			active.Root.Dispatcher.BeginInvoke((Action)delegate
			{
				active.Root.BringIntoView();
			}, DispatcherPriority.Loaded);
		}
	}

	private void UpdateRowCounts()
	{
		FileShelfService.ViewShelf[] shelfHeaders = _shelfHeaders;
		foreach (FileShelfService.ViewShelf viewShelf in shelfHeaders)
		{
			if (_rows.TryGetValue(viewShelf.Id, out var value))
			{
				value.Count.Text = viewShelf.Count.ToString();
			}
		}
	}

	private ShelfRow BuildShelfRow(FileShelfService.ViewShelf shelf, bool active)
	{
		ShelfRow r = new ShelfRow
		{
			Id = shelf.Id,
			Active = active
		};
		Grid grid = new Grid();
		grid.Background = Brushes.Transparent;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(26.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		r.Icon = new ContentControl
		{
			Width = 16.0,
			Height = 16.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			Focusable = false,
			IsTabStop = false
		};
		grid2.Children.Add(r.Icon);
		r.Name = BuddyTheme.Text(shelf.Name, 13.0, BuddyTheme.Text92, FontWeights.Medium);
		r.Name.VerticalAlignment = VerticalAlignment.Center;
		r.Name.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		Grid.SetColumn(r.Name, 1);
		grid2.Children.Add(r.Name);
		r.Count = BuddyTheme.Text(shelf.Count.ToString(), 13.0, BuddyTheme.Text50, FontWeights.Medium);
		Typography.SetNumeralAlignment(r.Count, FontNumeralAlignment.Tabular);
		r.Count.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(r.Count, 2);
		grid2.Children.Add(r.Count);
		r.More = new Border
		{
			Width = 22.0,
			Height = 22.0,
			Margin = new Thickness(6.0, 0.0, -4.0, 0.0),
			CornerRadius = new CornerRadius(6.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Visibility = Visibility.Collapsed,
			Child = Centered(FluentIcons.Create("more_horizontal", 16.0, BuddyTheme.Text92, filled: false)),
			ToolTip = BuddyTooltip.Create("Shelf options")
		};
		r.More.MouseEnter += delegate
		{
			r.More.Background = BuddyTheme.ControlActive;
		};
		r.More.MouseLeave += delegate
		{
			r.More.Background = Brushes.Transparent;
		};
		r.More.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		r.More.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (!BuddyDropdown.JustClosed(r.More))
			{
				ShowShelfMenu(r);
			}
		};
		Grid.SetColumn(r.More, 3);
		grid2.Children.Add(r.More);
		r.Body = grid2;
		r.Root = new Border
		{
			Height = 32.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 2.0),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			Cursor = Cursors.Hand,
			Child = grid2,
			ToolTip = ((shelf.Name.Length > 18) ? BuddyTooltip.Create(shelf.Name) : null)
		};
		r.Root.MouseEnter += delegate
		{
			PaintRow(r);
		};
		r.Root.MouseLeave += delegate
		{
			PaintRow(r);
		};
		r.Root.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (!(_renamingId == r.Id))
			{
				e.Handled = true;
				if (e.ClickCount >= 2)
				{
					BeginRename(r.Id);
				}
				else
				{
					CommitRename();
					if (!r.Active)
					{
						_svc.SwitchShelf(r.Id);
					}
				}
			}
		};
		r.Root.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			ShowShelfMenu(r);
		};
		PaintRow(r);
		return r;
	}

	private void PaintRow(ShelfRow r)
	{
		bool flag = r.Root.IsMouseOver || r.MenuOpen;
		r.Root.Background = (r.Target ? TargetRow : (r.Active ? ActiveRow : (flag ? HoverFill : Brushes.Transparent)));
		r.Name.Foreground = ((r.Active || r.Target) ? BuddyTheme.Text100 : BuddyTheme.Text92);
		r.Icon.Content = FluentIcons.Create("folder", 16.0, (r.Active || r.Target) ? BuddyTheme.Text100 : BuddyTheme.Text50, r.Active);
		r.More.Visibility = ((!flag || !(_renamingId != r.Id)) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void ShowShelfMenu(ShelfRow r)
	{
		ShelfTab shelfTab = _svc.FindShelf(r.Id);
		if (shelfTab != null)
		{
			bool flag = _svc.Shelves.Count <= 1;
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(MenuRow("Rename", "note_edit", enabled: true, null, delegate
			{
				BeginRename(r.Id);
			}));
			stackPanel.Children.Add(MenuRow("Clear", "dismiss_circle", shelfTab.Files.Count > 0, "This shelf is already empty", delegate
			{
				ClearShelfWithUndo(r.Id);
			}));
			stackPanel.Children.Add(MenuRow("Delete", "delete", !flag, "The last shelf can't be deleted", delegate
			{
				DeleteShelfWithUndo(r.Id);
			}));
			r.MenuOpen = true;
			PaintRow(r);
			Action closed = null;
			closed = delegate
			{
				BuddyDropdown.Closed -= closed;
				r.MenuOpen = false;
				PaintRow(r);
			};
			BuddyDropdown.Closed += closed;
			r.More.Visibility = Visibility.Visible;
			r.More.UpdateLayout();
			BuddyDropdown.ShowContent(r.More, stackPanel, 4.0);
		}
	}

	private FrameworkElement MenuRow(string label, string icon, bool enabled, string disabledReason, Action invoke)
	{
		Grid grid = new Grid();
		grid.Height = 32.0;
		grid.MinWidth = 168.0;
		grid.Background = Brushes.Transparent;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(28.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Brush brush = (enabled ? BuddyTheme.Text92 : BuddyTheme.Text30);
		FrameworkElement frameworkElement = FluentIcons.Create(icon, 16.0, brush, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(frameworkElement);
		TextBlock textBlock = BuddyTheme.Text(label, 13.0, brush, FontWeights.Normal);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		Grid.SetColumn(textBlock, 1);
		grid2.Children.Add(textBlock);
		Border b = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			Child = grid2,
			Background = Brushes.Transparent,
			Cursor = (enabled ? Cursors.Hand : Cursors.Arrow)
		};
		if (!enabled)
		{
			if (disabledReason != null)
			{
				b.ToolTip = BuddyTooltip.Create(disabledReason);
			}
			b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			return b;
		}
		b.MouseEnter += delegate
		{
			b.Background = BuddyTheme.RowHover;
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
			BuddyDropdown.Close();
			b.Dispatcher.BeginInvoke((Action)delegate
			{
				if (IsInitialized && _svc.Operations.AdmissionOpen)
				{
					invoke();
				}
			}, DispatcherPriority.Input);
		};
		return b;
	}

	private void BeginRename(string shelfId)
	{
		if (_renamingId == shelfId)
		{
			return;
		}
		CommitRename();
		if (_sidebarDirty || !_rows.ContainsKey(shelfId))
		{
			RefreshSidebar();
		}
		if (!_rows.TryGetValue(shelfId, out var r))
		{
			return;
		}
		ShelfTab shelfTab = _svc.FindShelf(shelfId);
		if (shelfTab == null)
		{
			return;
		}
		_renamingId = shelfId;
		TextBox box = new TextBox
		{
			Text = shelfTab.Name,
			FontFamily = BuddyTheme.Font,
			FontSize = 13.0,
			FontWeight = FontWeights.Medium,
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.FocusRing,
			Background = BuddyTheme.Canvas,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(4.0, 0.0, 4.0, 0.0),
			Margin = new Thickness(-4.0, 0.0, 8.0, 0.0),
			VerticalContentAlignment = VerticalAlignment.Center,
			Height = 24.0,
			MaxLength = 40
		};
		BuddyTheme.ApplyTextMode(box, 13.0);
		Grid.SetColumn(box, 1);
		r.Name.Visibility = Visibility.Collapsed;
		r.Body.Children.Add(box);
		r.More.Visibility = Visibility.Collapsed;
		_renameBox = box;
		box.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				CommitRename();
				_root.Focus();
			}
			else if (e.Key == Key.Escape)
			{
				e.Handled = true;
				CancelRename();
			}
		};
		box.LostKeyboardFocus += delegate
		{
			CommitRename();
		};
		box.Dispatcher.BeginInvoke((Action)delegate
		{
			if (_renameBox == box)
			{
				box.Focus();
				box.SelectAll();
				r.Root.BringIntoView();
			}
		}, DispatcherPriority.Input);
	}

	private void CommitRename()
	{
		EndRename(save: true);
	}

	private void CancelRename()
	{
		EndRename(save: false);
		if (_root != null)
		{
			_root.Focus();
		}
	}

	private void EndRename(bool save)
	{
		if (_renamingId != null)
		{
			string renamingId = _renamingId;
			string text = ((_renameBox != null) ? _renameBox.Text : null);
			_renamingId = null;
			_renameBox = null;
			if (save && !string.IsNullOrWhiteSpace(text))
			{
				_svc.RenameShelf(renamingId, text);
			}
			_sidebarSig = null;
			RefreshSidebar();
		}
	}

	private Grid BuildContent()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(24.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(8.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(8.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(30.0)
		});
		grid.Children.Add(BuildHeader());
		_viewport = new Viewport(this)
		{
			ClipToBounds = true,
			Background = Brushes.Transparent,
			Focusable = false
		};
		_canvasShift = new TranslateTransform();
		_canvas = new Canvas
		{
			RenderTransform = _canvasShift,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Top
		};
		_viewport.Children.Add(_canvas);
		_scrollThumb = new Border
		{
			Width = 6.0,
			CornerRadius = new CornerRadius(5.0),
			Background = BuddyTheme.Text100,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 0.0, 1.0, 0.0),
			Opacity = 0.0,
			IsHitTestVisible = false,
			RenderTransform = new TranslateTransform()
		};
		_viewport.Children.Add(_scrollThumb);
		double thumbStart = 0.0;
		double offsetStart = 0.0;
		_scrollThumb.MouseEnter += delegate
		{
			OverlayScroll.PaintThumb(_scrollThumb, hover: true);
		};
		_scrollThumb.MouseLeave += delegate
		{
			OverlayScroll.PaintThumb(_scrollThumb, hover: false);
			if (_thumbFade != null)
			{
				_thumbFade.Stop();
				_thumbFade.Start();
			}
		};
		_scrollThumb.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			thumbStart = e.GetPosition(_viewport).Y;
			offsetStart = _viewport.Offset;
			_scrollThumb.CaptureMouse();
			e.Handled = true;
		};
		_scrollThumb.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (_scrollThumb.IsMouseCaptured)
			{
				SetOffset(offsetStart + (e.GetPosition(_viewport).Y - thumbStart) * MaxOffset / Math.Max(1.0, _viewport.ActualHeight - _scrollThumb.ActualHeight), animate: false);
				e.Handled = true;
			}
		};
		_scrollThumb.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			_scrollThumb.ReleaseMouseCapture();
			ShowScrollThumb();
			e.Handled = true;
		};
		_viewport.MouseEnter += delegate
		{
			ShowScrollThumb();
		};
		_viewport.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (e.GetPosition(_viewport).X >= _viewport.ActualWidth - 14.0)
			{
				ShowScrollThumb();
			}
		};
		_viewport.MouseLeave += delegate
		{
			if (_thumbFade != null)
			{
				_thumbFade.Stop();
				_thumbFade.Start();
			}
		};
		_band = new Rectangle
		{
			Fill = BandFill,
			Stroke = BandStroke,
			StrokeThickness = 1.0,
			RadiusX = 4.0,
			RadiusY = 4.0,
			Visibility = Visibility.Collapsed,
			IsHitTestVisible = false
		};
		_canvas.Children.Add(_band);
		Panel.SetZIndex(_band, 10);
		_viewport.SizeChanged += delegate
		{
			_fadeInit = false;
			LayoutTiles();
			Realize();
			UpdateScrollThumb();
		};
		_viewport.PreviewMouseWheel += OnWheel;
		_viewport.MouseLeftButtonDown += OnViewportMouseDown;
		_viewport.MouseMove += OnViewportMouseMove;
		_viewport.MouseLeftButtonUp += OnViewportMouseUp;
		_viewport.LostMouseCapture += delegate
		{
			EndBand();
		};
		Grid.SetRow(_viewport, 2);
		grid.Children.Add(_viewport);
		_dropZone = BuildDropZone();
		Grid.SetRow(_dropZone, 4);
		grid.Children.Add(_dropZone);
		_toast = new BuddyUndoToast();
		_toast.Margin = new Thickness(0.0, 0.0, 0.0, -3.0);
		Grid.SetRow(_toast, 2);
		Grid.SetRowSpan(_toast, 3);
		Panel.SetZIndex(_toast, 20);
		grid.Children.Add(_toast);
		return grid;
	}

	private FrameworkElement BuildHeader()
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
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		_title = BuddyTheme.Text(string.Empty, 15.0, BuddyTheme.Text100, AynType.Semibold);
		_title.FontFamily = BuddyTheme.DisplayFont;
		_title.MaxWidth = 190.0;
		_title.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(_title);
		_meta = BuddyTheme.Text(string.Empty, 13.0, BuddyTheme.Text50, FontWeights.Medium);
		Typography.SetNumeralAlignment(_meta, FontNumeralAlignment.Tabular);
		_meta.VerticalAlignment = VerticalAlignment.Center;
		_meta.Margin = new Thickness(10.0, 1.0, 0.0, 0.0);
		stackPanel2.Children.Add(_meta);
		grid.Children.Add(stackPanel2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		_recoveryBtn = new TextLink("Recover copies", chevron: false)
		{
			Visibility = Visibility.Collapsed
		};
		_recoveryBtn.ToolTip = BuddyTooltip.Create("Some files were not copied. Select their source again or cancel the failed copy. Cancelling leaves retained copy data untouched.");
		_recoveryBtn.Click += ShowCopyRecovery;
		stackPanel4.Children.Add(_recoveryBtn);
		_sortBtn = new TextLink("Recent", chevron: true);
		_sortBtn.Click += ShowSortMenu;
		_sortBtn.ToolTip = BuddyTooltip.Create("Sort");
		_selectBtn = new TextLink("Select", chevron: false);
		_selectBtn.Click += delegate
		{
			_selectMode = !_selectMode;
			if (!_selectMode)
			{
				ClearSelection();
			}
			else
			{
				UpdateHeader();
			}
		};
		_copyBtn = new TextLink("Copy all", chevron: false);
		_copyBtn.Click += delegate
		{
			CopyToClipboard(ActionTargets());
		};
		_clearBtn = new TextLink("Clear", chevron: false);
		_clearBtn.Click += delegate
		{
			if (_selected.Count > 0)
			{
				RemoveWithUndo(_selected.ToList());
			}
			else
			{
				ClearShelfWithUndo(_shelfId);
			}
		};
		TextLink[] array = new TextLink[4] { _sortBtn, _selectBtn, _copyBtn, _clearBtn };
		foreach (TextLink textLink in array)
		{
			textLink.Margin = new Thickness(16.0, 0.0, 0.0, 0.0);
			stackPanel4.Children.Add(textLink);
		}
		Grid.SetColumn(stackPanel4, 2);
		grid.Children.Add(stackPanel4);
		return grid;
	}

	private Grid BuildDropZone()
	{
		Grid grid = new Grid();
		grid.Background = Brushes.Transparent;
		Grid grid2 = grid;
		_dropDash = new Rectangle
		{
			Stroke = DashIdle,
			StrokeThickness = 1.0,
			StrokeDashArray = new DoubleCollection { 4.0, 4.0 },
			RadiusX = 10.0,
			RadiusY = 10.0,
			Fill = Brushes.Transparent,
			SnapsToDevicePixels = true,
			IsHitTestVisible = false
		};
		grid2.Children.Add(_dropDash);
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(14.0, 0.0, 6.0, 0.0);
		Grid grid4 = (_dropRow = grid3);
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
			Width = GridLength.Auto
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_dropIcon = FluentIcons.Create("document_arrow_right", 14.0, BuddyTheme.Text50, filled: false);
		_dropIcon.VerticalAlignment = VerticalAlignment.Center;
		_dropIcon.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		grid4.Children.Add(_dropIcon);
		_dropText = BuddyTheme.Text("Drop files here · drag out to use anywhere", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_dropText.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_dropText, 1);
		grid4.Children.Add(_dropText);
		_dropDivider = new Rectangle
		{
			Width = 1.0,
			Height = 14.0,
			Fill = BuddyTheme.Hairline,
			Margin = new Thickness(10.0, 0.0, 4.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(_dropDivider, 2);
		grid4.Children.Add(_dropDivider);
		_chooseBtn = new TextLink("Choose files", chevron: false, BuddyTheme.Text92);
		_chooseBtn.Padding = new Thickness(8.0, 6.0, 8.0, 6.0);
		_chooseBtn.Click += ChooseFiles;
		Grid.SetColumn(_chooseBtn, 3);
		grid4.Children.Add(_chooseBtn);
		grid2.Children.Add(grid4);
		return grid2;
	}

	private string CurrentShelfId()
	{
		return _svc.ResolveShelfId(null);
	}

	private void RefreshContent()
	{
		if (_viewport != null && !_viewReady)
		{
			ApplyView(_svc.ReadView());
		}
	}

	private void AddTotals(ParkedFile file, int sign)
	{
		if (file.IsMissing)
		{
			_missingCount += sign;
		}
		else if (!file.IsDirectory)
		{
			_totalBytes += sign * file.FileSizeBytes;
		}
	}

	private int CompareItems(ParkedFile a, ParkedFile b)
	{
		int num;
		switch (_viewSort)
		{
		case ShelfSort.Name:
			num = CompareNames(a.FileName, b.FileName);
			break;
		case ShelfSort.Size:
			num = ((a.IsMissing || a.IsDirectory) ? 1 : 0).CompareTo((b.IsMissing || b.IsDirectory) ? 1 : 0);
			if (num == 0)
			{
				num = b.FileSizeBytes.CompareTo(a.FileSizeBytes);
			}
			break;
		case ShelfSort.Type:
			num = ((!a.IsDirectory) ? 1 : 0).CompareTo((!b.IsDirectory) ? 1 : 0);
			if (num == 0)
			{
				num = string.Compare(a.Extension ?? "", b.Extension ?? "", StringComparison.OrdinalIgnoreCase);
			}
			if (num == 0)
			{
				num = CompareNames(a.FileName, b.FileName);
			}
			break;
		default:
			num = b.AddedUtc.CompareTo(a.AddedUtc);
			break;
		}
		if (num != 0)
		{
			return num;
		}
		if (_viewSort != 0)
		{
			return a.ViewOrder.CompareTo(b.ViewOrder);
		}
		return b.ViewOrder.CompareTo(a.ViewOrder);
	}

	private void ApplyView(FileShelfService.ViewChange change)
	{
		bool flag = !_viewReady || change.Reset || _shelfId != change.ActiveId || _viewSort != change.Sort;
		_viewReady = true;
		if (change.Reset)
		{
			_shelfFiles.Clear();
			_fileShelves.Clear();
		}
		_shelfHeaders = change.Shelves;
		_activeViewId = change.ActiveId;
		_viewSort = change.Sort;
		HashSet<string> affected = new HashSet<string>();
		FileShelfService.ViewItem[] items = change.Items;
		foreach (FileShelfService.ViewItem viewItem in items)
		{
			ParkedFile file = viewItem.File;
			if (_fileShelves.TryGetValue(file.Id, out var value))
			{
				if (_shelfFiles.TryGetValue(value, out var value2) && value2.TryGetValue(file.Id, out var value3))
				{
					if (!flag && value == _shelfId)
					{
						AddTotals(value3, -1);
						affected.Add(file.Id);
					}
					value2.Remove(file.Id);
				}
				_fileShelves.Remove(file.Id);
			}
			if (file.FullPath == null)
			{
				_itemIds.Remove(file.Id);
				continue;
			}
			if (!_shelfFiles.TryGetValue(viewItem.ShelfId, out var value4))
			{
				value4 = (_shelfFiles[viewItem.ShelfId] = new Dictionary<string, ParkedFile>());
			}
			value4[file.Id] = file;
			_fileShelves[file.Id] = viewItem.ShelfId;
			if (!flag && viewItem.ShelfId == _shelfId)
			{
				AddTotals(file, 1);
				affected.Add(file.Id);
			}
		}
		string[] array = _shelfFiles.Keys.Where((string id) => !change.Shelves.Any((FileShelfService.ViewShelf s) => s.Id == id)).ToArray();
		foreach (string key in array)
		{
			_shelfFiles.Remove(key);
		}
		if (!_shelfFiles.TryGetValue(change.ActiveId, out var activeFiles))
		{
			_shelfFiles[change.ActiveId] = (activeFiles = new Dictionary<string, ParkedFile>());
		}
		if (_shelfId != change.ActiveId)
		{
			_shelfId = change.ActiveId;
			_selected.Clear();
			_anchorId = null;
			_selectMode = false;
			SetOffset(0.0, animate: false);
		}
		bool flag2 = flag;
		if (flag)
		{
			_items = activeFiles.Values.ToList();
			_items.Sort(CompareItems);
			_totalBytes = 0L;
			_missingCount = 0;
			_itemIds.Clear();
			foreach (ParkedFile item in _items)
			{
				AddTotals(item, 1);
				_itemIds.Add(item.Id);
			}
		}
		else
		{
			foreach (string item2 in affected)
			{
				ParkedFile value5;
				bool flag3 = activeFiles.TryGetValue(item2, out value5);
				if (!flag3 || !_itemIndices.TryGetValue(item2, out var value6))
				{
					flag2 = true;
				}
				else if (CompareItems(_items[value6], value5) != 0)
				{
					flag2 = true;
				}
				else
				{
					_items[value6] = value5;
				}
				if (flag3)
				{
					_itemIds.Add(item2);
				}
				else
				{
					_itemIds.Remove(item2);
				}
			}
			if (flag2)
			{
				_items.RemoveAll((ParkedFile f) => affected.Contains(f.Id));
				List<ParkedFile> list = (from id in affected.Where(activeFiles.ContainsKey)
					select activeFiles[id]).ToList();
				list.Sort(CompareItems);
				List<ParkedFile> list2 = new List<ParkedFile>(_items.Count + list.Count);
				int num = 0;
				int num2 = 0;
				while (num < _items.Count && num2 < list.Count)
				{
					list2.Add((CompareItems(_items[num], list[num2]) <= 0) ? _items[num++] : list[num2++]);
				}
				while (num < _items.Count)
				{
					list2.Add(_items[num++]);
				}
				while (num2 < list.Count)
				{
					list2.Add(list[num2++]);
				}
				_items = list2;
			}
		}
		if (flag2)
		{
			_itemIndices.Clear();
			for (int k = 0; k < _items.Count; k++)
			{
				_itemIndices[_items[k].Id] = k;
			}
		}
		if (_selected.Count > 0)
		{
			if (flag)
			{
				_selected.RemoveWhere((string id) => !_itemIds.Contains(id));
			}
			else
			{
				foreach (string item3 in affected)
				{
					if (!_itemIds.Contains(item3))
					{
						_selected.Remove(item3);
					}
				}
			}
		}
		UpdateDropZone();
		if (flag || affected.Count > 0)
		{
			_realizeDirty = true;
			LayoutTiles();
			Realize();
		}
		UpdateHeader();
		RefreshSidebar();
	}

	private static List<ParkedFile> SortItems(List<ParkedFile> items, ShelfSort sort)
	{
		var list = items.Select((ParkedFile f, int i) => new { f, i }).ToList();
		switch (sort)
		{
		case ShelfSort.Name:
			list.Sort((a, b) => CompareNames(a.f.FileName, b.f.FileName));
			break;
		case ShelfSort.Size:
			list = (from x in list
				orderby (x.f.IsMissing || x.f.IsDirectory) ? 1 : 0, x.f.FileSizeBytes descending, x.i
				select x).ToList();
			break;
		case ShelfSort.Type:
			list.Sort((a, b) =>
			{
				int num = ((!a.f.IsDirectory) ? 1 : 0).CompareTo((!b.f.IsDirectory) ? 1 : 0);
				if (num == 0)
				{
					num = string.Compare(a.f.Extension ?? "", b.f.Extension ?? "", StringComparison.OrdinalIgnoreCase);
				}
				return (num == 0) ? CompareNames(a.f.FileName, b.f.FileName) : num;
			});
			break;
		default:
			list = (from x in list
				orderby x.f.AddedUtc descending, x.i descending
				select x).ToList();
			break;
		}
		return list.Select(x => x.f).ToList();
	}

	[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
	private static extern int StrCmpLogicalW(string a, string b);

	private static int CompareNames(string a, string b)
	{
		try
		{
			return StrCmpLogicalW(a ?? string.Empty, b ?? string.Empty);
		}
		catch
		{
			return string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
		}
	}

	private void UpdateHeader()
	{
		if (_title == null)
		{
			return;
		}
		int count = _svc.RecoveryItems.Count;
		_recoveryBtn.Label = "Recover (" + count + ")";
		_recoveryBtn.Visibility = ((count == 0) ? Visibility.Collapsed : Visibility.Visible);
		FileShelfService.ViewShelf viewShelf = _shelfHeaders.FirstOrDefault((FileShelfService.ViewShelf s) => s.Id == _shelfId);
		if (viewShelf != null)
		{
			_title.Text = viewShelf.Name;
			_title.ToolTip = ((viewShelf.Name.Length > 22) ? BuddyTooltip.Create(viewShelf.Name) : null);
			int count2 = _items.Count;
			long totalBytes = _totalBytes;
			int missingCount = _missingCount;
			string text = count2 switch
			{
				1 => "1 item", 
				0 => "Empty", 
				_ => count2.ToString("N0") + " items", 
			};
			if (count2 > 0 && totalBytes > 0)
			{
				text = text + " · " + ParkedFile.FormatBytes(totalBytes);
			}
			if (missingCount > 0)
			{
				object obj = text;
				text = string.Concat(obj, " · ", missingCount, " missing");
			}
			if (_selected.Count > 0)
			{
				text = _selected.Count + " of " + count2.ToString("N0") + " selected";
			}
			_meta.Text = text;
			bool flag = count2 > 0;
			int count3 = _selected.Count;
			_sortBtn.Label = SortLabel(_viewSort);
			_sortBtn.Enabled = flag;
			_selectBtn.Label = (_selectMode ? "Done" : "Select");
			_selectBtn.Enabled = flag || _selectMode;
			if (!_copyBtn.ShowingFeedback)
			{
				_copyBtn.Label = ((count3 > 0) ? ("Copy " + count3) : "Copy all");
			}
			_copyBtn.Enabled = flag && ((_selected.Count == 0) ? (count2 > missingCount) : _selected.Any((string id) => _itemIds.Contains(id) && !_items[_itemIndices[id]].IsMissing));
			_clearBtn.Label = ((count3 > 0) ? ("Remove " + count3) : "Clear");
			_clearBtn.Enabled = flag;
		}
	}

	private static string SortLabel(ShelfSort s)
	{
		return s switch
		{
			ShelfSort.Name => "Name", 
			ShelfSort.Size => "Size", 
			ShelfSort.Type => "Type", 
			_ => "Recent", 
		};
	}

	private void ShowSortMenu()
	{
		if (BuddyDropdown.JustClosed(_sortBtn))
		{
			return;
		}
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		ShelfSort[] array = new ShelfSort[4]
		{
			ShelfSort.Recent,
			ShelfSort.Name,
			ShelfSort.Size,
			ShelfSort.Type
		};
		foreach (ShelfSort shelfSort in array)
		{
			ShelfSort sort = shelfSort;
			list.Add(new BuddyMenuItem(SortLabel(sort), null, delegate
			{
				_svc.Sort = sort;
				SetOffset(0.0, animate: true);
			})
			{
				IsChecked = (_svc.Sort == sort)
			});
		}
		BuddyDropdown.Show(_sortBtn, list, null, 140.0);
	}

	private void UpdateDropZone()
	{
		if (_dropZone != null)
		{
			bool flag = _items.Count == 0;
			Grid.SetRow(_dropZone, flag ? 2 : 4);
			Grid.SetRowSpan(_dropZone, (!flag) ? 1 : 3);
			_viewport.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			FileShelfService.ViewShelf viewShelf = _shelfHeaders.FirstOrDefault((FileShelfService.ViewShelf s) => s.Id == (_dragTargetShelf ?? _shelfId));
			if (viewShelf != null)
			{
				bool dragActive = _dragActive;
				string text = (dragActive ? ((_dragInternal ? "Drop to move to " : "Drop to add to ") + viewShelf.Name) : ((!flag) ? "Drop files here · drag out to use anywhere" : ("Nothing on " + viewShelf.Name + " yet · drop files or paste with Ctrl+V")));
				_dropText.Text = text;
				_dropText.Foreground = (dragActive ? BuddyTheme.Text100 : BuddyTheme.Text50);
				_dropRow.HorizontalAlignment = ((flag || dragActive) ? HorizontalAlignment.Center : HorizontalAlignment.Stretch);
				_dropDash.Stroke = (dragActive ? BuddyTheme.Text92 : DashIdle);
				_dropDash.Fill = (dragActive ? DropFill : Brushes.Transparent);
				_dropIcon.Opacity = (dragActive ? 1.0 : 0.85);
				_chooseBtn.Visibility = (dragActive ? Visibility.Collapsed : Visibility.Visible);
				_dropDivider.Visibility = _chooseBtn.Visibility;
			}
		}
	}

	private Rect TileRect(int index)
	{
		int num = index / 5;
		int num2 = index % 5;
		return new Rect((double)num2 * _colWidth + (_colWidth - TileWidth) / 2.0, (double)num * 116.0, TileWidth, 112.0);
	}

	private void LayoutTiles()
	{
		if (_viewport == null)
		{
			return;
		}
		double actualWidth = _viewport.ActualWidth;
		if (actualWidth > 0.0)
		{
			_colWidth = Math.Floor(actualWidth / 5.0);
		}
		_realizeDirty = true;
		if (_viewport.Offset > MaxOffset)
		{
			SetOffset(MaxOffset, animate: false);
		}
		else if (_viewport.Target > MaxOffset)
		{
			SetOffset(MaxOffset, animate: true);
		}
		foreach (KeyValuePair<string, Tile> tile in _tiles)
		{
			if (_itemIndices.TryGetValue(tile.Key, out var value))
			{
				Tile value2 = tile.Value;
				Rect rect = TileRect(value);
				value2.Root.Width = rect.Width;
				value2.Position.X = rect.X;
				value2.Position.Y = rect.Y;
			}
		}
	}

	private void Realize()
	{
		if (_viewport == null)
		{
			return;
		}
		double num = Math.Max(1.0, _viewport.ActualHeight);
		double num2 = Math.Abs(_viewport.Target - _viewport.Offset);
		bool fastScroll = _fastScroll;
		if (_fastScroll ? (num2 < 232.0) : (num2 > 580.0))
		{
			_fastScroll = !_fastScroll;
		}
		if (fastScroll && !_fastScroll)
		{
			foreach (Tile value3 in _tiles.Values)
			{
				if (value3.DeferredThumbnail)
				{
					LoadThumb(value3);
				}
			}
		}
		int num3 = ((!_fastScroll) ? 1 : 0);
		int num4 = Math.Max(0, (int)Math.Floor(_viewport.Offset / 116.0) - ((_scrollDirection < 0) ? num3 : 0));
		int num5 = (int)Math.Floor((_viewport.Offset + num - 0.001) / 116.0) + ((_scrollDirection > 0) ? num3 : 0);
		int num6 = num4 * 5;
		int num7 = Math.Min(_items.Count - 1, (num5 + 1) * 5 - 1);
		if (!_realizeDirty && num6 == _realizedFirst && num7 == _realizedLast)
		{
			UpdateEdges();
			return;
		}
		_realizeDirty = false;
		_realizedFirst = num6;
		_realizedLast = num7;
		_tileRemovals.Clear();
		foreach (KeyValuePair<string, Tile> tile in _tiles)
		{
			if (!_itemIndices.TryGetValue(tile.Key, out var value) || value < num6 || value > num7)
			{
				_tileRemovals.Add(tile.Key);
			}
		}
		foreach (string tileRemoval in _tileRemovals)
		{
			DropTile(tileRemoval);
		}
		for (int i = num6; i <= num7; i++)
		{
			ParkedFile parkedFile = _items[i];
			if (!_tiles.TryGetValue(parkedFile.Id, out var value2))
			{
				if (_tilePool.Count > 0)
				{
					value2 = _tilePool.Pop();
					BindTile(value2, parkedFile);
				}
				else
				{
					value2 = BuildTile(parkedFile);
					_canvas.Children.Add(value2.Root);
				}
				_tiles[parkedFile.Id] = value2;
			}
			else
			{
				if (object.ReferenceEquals(value2.Item, parkedFile))
				{
					continue;
				}
				BindTile(value2, parkedFile);
			}
			Rect rect = TileRect(i);
			value2.Root.Width = rect.Width;
			value2.Position.X = rect.X;
			value2.Position.Y = rect.Y;
			PaintTile(value2);
		}
		foreach (Tile item in _tilePool)
		{
			item.Root.Visibility = Visibility.Collapsed;
		}
		UpdateEdges();
	}

	private void DropTile(string id)
	{
		if (_tiles.TryGetValue(id, out var value))
		{
			value.Alive = false;
			value.Generation++;
			if (_pressTile == value)
			{
				_pressTile = null;
			}
			value.Thumb.BeginAnimation(UIElement.OpacityProperty, null);
			_tiles.Remove(id);
			if (_tilePool.Count < 50)
			{
				_tilePool.Push(value);
			}
			else
			{
				_canvas.Children.Remove(value.Root);
			}
		}
	}

	private void BindTile(Tile t, ParkedFile item)
	{
		ParkedFile item2 = t.Item;
		bool flag = !t.Alive || item2 == null || item2.Id != item.Id || item2.FullPath != item.FullPath || item2.Extension != item.Extension || item2.IsMissing != item.IsMissing || item2.IsDirectory != item.IsDirectory || item2.IsProbed != item.IsProbed || item2.FileSizeBytes != item.FileSizeBytes || item2.LastWriteTicks != item.LastWriteTicks;
		t.Item = item;
		t.Alive = true;
		t.Root.Visibility = Visibility.Visible;
		t.Hover = t.Root.IsMouseOver;
		t.Name.Text = item.FileName;
		t.Name.Foreground = (item.IsMissing ? BuddyTheme.Text50 : BuddyTheme.Text92);
		((ToolTip)t.Name.ToolTip).IsOpen = false;
		((ToolTip)t.Name.ToolTip).Content = (item.IsMissing ? ("Missing · " + item.FullPath) : item.FullPath);
		t.Meta.Text = (item.IsMissing ? "Missing" : (item.IsProbed ? item.FormattedSize : string.Empty));
		t.Meta.Foreground = (item.IsMissing ? BuddyTheme.Text30 : BuddyTheme.Text50);
		t.LocateAction.Visibility = ((!item.IsMissing) ? Visibility.Collapsed : Visibility.Visible);
		FrameworkElement openAction = t.OpenAction;
		Visibility visibility2 = (t.FolderAction.Visibility = (item.IsMissing ? Visibility.Collapsed : Visibility.Visible));
		openAction.Visibility = visibility2;
		if (flag)
		{
			t.Generation++;
			t.Thumb.BeginAnimation(UIElement.OpacityProperty, null);
			t.Thumb.Opacity = 1.0;
			LoadThumb(t);
		}
	}

	private Tile BuildTile(ParkedFile item)
	{
		Tile t = new Tile
		{
			Item = item
		};
		t.Root = new Grid
		{
			Height = 112.0,
			Background = Brushes.Transparent,
			RenderTransform = t.Position
		};
		t.Surface = new Border
		{
			CornerRadius = new CornerRadius(12.0),
			BorderThickness = new Thickness(1.0),
			BorderBrush = Brushes.Transparent,
			Background = Brushes.Transparent,
			IsHitTestVisible = false
		};
		t.Root.Children.Add(t.Surface);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(6.0, 6.0, 6.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		t.Thumb = new Grid
		{
			Width = 64.0,
			Height = 64.0,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		RenderOptions.SetBitmapScalingMode(t.Thumb, BitmapScalingMode.HighQuality);
		t.PreviewBrush = new ImageBrush
		{
			Stretch = Stretch.UniformToFill
		};
		t.Preview = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			Background = t.PreviewBrush,
			Visibility = Visibility.Collapsed
		};
		t.Icon = new Image
		{
			Stretch = Stretch.Uniform,
			Width = 64.0,
			Height = 64.0
		};
		t.Play = new Border
		{
			Width = 24.0,
			Height = 24.0,
			CornerRadius = new CornerRadius(12.0),
			Background = PlayDisc,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Child = Centered(FluentIcons.Create("play", 12.0, BuddyTheme.Text100, filled: true)),
			Visibility = Visibility.Collapsed
		};
		t.Thumb.Children.Add(t.Preview);
		t.Thumb.Children.Add(t.Icon);
		t.Thumb.Children.Add(t.Play);
		stackPanel2.Children.Add(t.Thumb);
		t.Name = BuddyTheme.Text(item.FileName, 13.0, item.IsMissing ? BuddyTheme.Text50 : BuddyTheme.Text92, FontWeights.Medium);
		t.Name.HorizontalAlignment = HorizontalAlignment.Center;
		t.Name.TextAlignment = TextAlignment.Center;
		t.Name.Height = 16.0;
		t.Name.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
		t.Name.ToolTip = BuddyTooltip.Create(item.IsMissing ? ("Missing · " + item.FullPath) : item.FullPath);
		stackPanel2.Children.Add(t.Name);
		Grid grid = new Grid();
		grid.Height = 20.0;
		Grid grid2 = grid;
		string text = (item.IsMissing ? "Missing" : (item.IsProbed ? item.FormattedSize : string.Empty));
		t.Meta = BuddyTheme.Text(text, 11.5, item.IsMissing ? BuddyTheme.Text30 : BuddyTheme.Text50, FontWeights.Medium);
		Typography.SetNumeralAlignment(t.Meta, FontNumeralAlignment.Tabular);
		t.Meta.HorizontalAlignment = HorizontalAlignment.Center;
		t.Meta.VerticalAlignment = VerticalAlignment.Top;
		t.Meta.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
		grid2.Children.Add(t.Meta);
		t.Actions = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		t.LocateAction = TileAction("search", "Locate", delegate
		{
			Locate(t.Item);
		});
		t.OpenAction = TileAction("open", "Open", delegate
		{
			OpenItem(t.Item);
		});
		t.FolderAction = TileAction("folder", "Show in folder", delegate
		{
			ShowInFolder(t.Item);
		});
		t.Actions.Children.Add(t.LocateAction);
		t.Actions.Children.Add(t.OpenAction);
		t.Actions.Children.Add(t.FolderAction);
		t.Actions.Children.Add(TileAction("delete", "Remove from shelf", delegate
		{
			RemoveWithUndo(t.Item.IsMissing ? new List<string> { t.Item.Id } : ActionIdsFor(t.Item));
		}));
		grid2.Children.Add(t.Actions);
		stackPanel2.Children.Add(grid2);
		t.Root.Children.Add(stackPanel2);
		t.Alive = false;
		BindTile(t, item);
		t.Root.MouseEnter += delegate
		{
			t.Hover = true;
			PaintTile(t);
		};
		t.Root.MouseLeave += delegate
		{
			t.Hover = false;
			PaintTile(t);
		};
		t.Root.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			OnTileMouseDown(t, e);
		};
		t.Root.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			OnTileMouseUp(t, e);
		};
		t.Root.PreviewMouseMove += delegate(object s, MouseEventArgs e)
		{
			OnTileMouseMove(t, e);
		};
		t.Root.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			ShowTileMenu(t);
		};
		return t;
	}

	private FrameworkElement TileAction(string icon, string tip, Action act)
	{
		Border b = new Border
		{
			Width = 24.0,
			Height = 20.0,
			Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
			CornerRadius = new CornerRadius(6.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = Centered(FluentIcons.Create(icon, 14.0, BuddyTheme.Text92, filled: false)),
			ToolTip = BuddyTooltip.Create(tip)
		};
		b.MouseEnter += delegate
		{
			b.Background = BuddyTheme.ControlActive;
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
			act();
		};
		return b;
	}

	private void PaintTile(Tile t)
	{
		bool flag = _selected.Contains(t.Item.Id);
		bool flag2 = t.Hover && !_banding && !_dragActive;
		t.Surface.Background = (flag2 ? HoverFill : Brushes.Transparent);
		t.Surface.BorderBrush = (flag ? Ring : Brushes.Transparent);
		t.Actions.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
		t.Meta.Visibility = (flag2 ? Visibility.Hidden : Visibility.Visible);
	}

	private void RepaintTiles()
	{
		foreach (Tile value in _tiles.Values)
		{
			PaintTile(value);
		}
		UpdateHeader();
	}

	private int ThumbPixels()
	{
		double val = 1.0;
		PresentationSource presentationSource = ((_root != null) ? PresentationSource.FromVisual(_root) : null);
		if (presentationSource != null && presentationSource.CompositionTarget != null)
		{
			val = presentationSource.CompositionTarget.TransformToDevice.M11;
		}
		return (int)Math.Ceiling(64.0 * Math.Max(1.0, val));
	}

	private void LoadThumb(Tile t)
	{
		int pixels = ThumbPixels();
		int generation = ++t.Generation;
		t.DeferredThumbnail = _fastScroll && !t.Item.IsMissing && ShelfThumbnails.HasPreview(t.Item.Extension);
		ShelfThumbnails.Result result = ShelfThumbnails.Get(t.Item, pixels, () => t.Alive && t.Generation == generation && IsInitialized && _svc.Operations.AdmissionOpen, delegate(ShelfThumbnails.Result res)
		{
			if (t.Alive && t.Generation == generation && res != null)
			{
				ShowThumb(t, res, fade: true);
			}
		}, _root.Dispatcher, _svc, t.DeferredThumbnail);
		if (result != null)
		{
			ShowThumb(t, result, fade: false);
			return;
		}
		t.Icon.Source = null;
		t.PreviewBrush.ImageSource = null;
		Border preview = t.Preview;
		Visibility visibility2 = (t.Play.Visibility = Visibility.Collapsed);
		preview.Visibility = visibility2;
	}

	private void ShowThumb(Tile t, ShelfThumbnails.Result res, bool fade)
	{
		t.Icon.Source = (res.IsThumbnail ? null : res.Image);
		t.PreviewBrush.ImageSource = (res.IsThumbnail ? res.Image : null);
		t.Preview.Visibility = ((!res.IsThumbnail) ? Visibility.Collapsed : Visibility.Visible);
		t.Play.Visibility = ((!res.IsThumbnail || !ShelfThumbnails.IsVideo(t.Item.Extension)) ? Visibility.Collapsed : Visibility.Visible);
		double num = (t.Item.IsMissing ? 0.35 : 1.0);
		t.Thumb.BeginAnimation(UIElement.OpacityProperty, null);
		t.Thumb.Opacity = num;
		if (fade && !_scrollAnimating)
		{
			t.Thumb.Opacity = 0.0;
			BuddyTheme.Animate(t.Thumb, UIElement.OpacityProperty, num, BuddyTheme.Hover);
		}
	}

	private double CurrentScrollY(AnimationClock clock)
	{
		double num = Math.Max(0.0, Math.Min(1.0, (double)(clock.CurrentTime.GetValueOrDefault().Ticks - _scrollStarted) / 2200000.0));
		double num2 = 1.0 - num;
		return _scrollFrom + (_scrollTo - _scrollFrom) * (1.0 - num2 * num2 * num2);
	}

	private void OnWheel(object sender, MouseWheelEventArgs e)
	{
		e.Handled = true;
		if (!(MaxOffset <= 0.0))
		{
			double num = (_scrollAnimating ? _viewport.Target : _viewport.Offset);
			SetOffset(num - (double)e.Delta * 0.55, animate: true);
		}
	}

	private void SetOffset(double value, bool animate)
	{
		if (_viewport == null)
		{
			return;
		}
		value = Math.Max(0.0, Math.Min(MaxOffset, value));
		if (value != _viewport.Offset)
		{
			_scrollDirection = ((!(value < _viewport.Offset)) ? 1 : (-1));
		}
		if (animate && _scrollAnimating && Math.Abs(value - _viewport.Target) < 0.01)
		{
			ShowScrollThumb();
			return;
		}
		_viewport.Target = value;
		ShowScrollThumb();
		if (!animate || !CapsuleMotion.AnimationsEnabled || Math.Abs(value - _viewport.Offset) < 0.01)
		{
			StopScroll();
			_viewport.Target = value;
			_canvasShift.Y = 0.0 - value;
			_viewport.Offset = value;
			return;
		}
		_scrollFrom = _canvasShift.Y;
		_scrollTo = 0.0 - value;
		_scrollStarted = (_scrollAnimating ? _scrollAnimation.CurrentTime.GetValueOrDefault().Ticks : 0);
		if (!_scrollAnimating)
		{
			ScrollAnimation scrollAnimation = new ScrollAnimation();
			scrollAnimation.Owner = this;
			scrollAnimation.Duration = Duration.Forever;
			ScrollAnimation scrollAnimation2 = scrollAnimation;
			Timeline.SetDesiredFrameRate(scrollAnimation2, 60);
			scrollAnimation2.Freeze();
			_scrollAnimation = (AnimationClock)scrollAnimation2.CreateClock(hasControllableRoot: true);
			_scrollAnimating = true;
			_lastScrollTick = -1L;
			UpdateEdges();
			_canvasShift.ApplyAnimationClock(TranslateTransform.YProperty, _scrollAnimation, HandoffBehavior.SnapshotAndReplace);
			_scrollAnimation.CurrentTimeInvalidated += _scrollTickHandler;
		}
	}

	private void OnScrollTick(object sender, EventArgs e)
	{
		if (!_scrollAnimating || !IsInitialized || !object.ReferenceEquals(sender, _scrollAnimation))
		{
			return;
		}
		long ticks = _scrollAnimation.CurrentTime.GetValueOrDefault().Ticks;
		if (ticks != _lastScrollTick)
		{
			_lastScrollTick = ticks;
			_viewport.Offset = 0.0 - _canvasShift.Y;
			if (_scrollAnimation.CurrentTime.GetValueOrDefault().Ticks - _scrollStarted >= 2200000)
			{
				StopScroll();
			}
		}
	}

	private void StopScroll()
	{
		if (_scrollAnimating)
		{
			double y = _canvasShift.Y;
			AnimationClock scrollAnimation = _scrollAnimation;
			_scrollAnimation = null;
			_scrollAnimating = false;
			scrollAnimation.CurrentTimeInvalidated -= _scrollTickHandler;
			_canvasShift.Y = y;
			scrollAnimation.Controller.Remove();
			_canvasShift.ApplyAnimationClock(TranslateTransform.YProperty, null);
			_viewport.Target = 0.0 - y;
			_viewport.Offset = 0.0 - y;
			UpdateEdges();
		}
	}

	private void ScrollIntoView(int index)
	{
		if (index >= 0 && _viewport != null)
		{
			Rect rect = TileRect(index);
			double target = _viewport.Target;
			if (rect.Top < target)
			{
				SetOffset(rect.Top, animate: true);
			}
			else if (rect.Bottom > target + _viewport.ActualHeight)
			{
				SetOffset(rect.Bottom - _viewport.ActualHeight, animate: true);
			}
		}
	}

	private void OnOffsetChanged()
	{
		Realize();
		UpdateScrollThumb();
	}

	private void UpdateEdges()
	{
		if (_viewport == null)
		{
			return;
		}
		if (_scrollAnimating)
		{
			if (_viewport.OpacityMask != null)
			{
				_viewport.OpacityMask = null;
			}
			_fadeInit = false;
			return;
		}
		double maxOffset = MaxOffset;
		bool flag = _viewport.Offset > 0.5;
		bool flag2 = _viewport.Offset < maxOffset - 0.5;
		if (!_fadeInit || flag != _fadeTop || flag2 != _fadeBottom)
		{
			_fadeInit = true;
			_fadeTop = flag;
			_fadeBottom = flag2;
			if (!flag && !flag2)
			{
				_viewport.OpacityMask = null;
				return;
			}
			double num = Math.Max(1.0, _viewport.ActualHeight);
			double num2 = Math.Min(0.25, 22.0 / num);
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.MappingMode = BrushMappingMode.Absolute;
			linearGradientBrush.StartPoint = new Point(0.0, 0.0);
			linearGradientBrush.EndPoint = new Point(0.0, num);
			LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
			linearGradientBrush2.GradientStops.Add(new GradientStop(flag ? Colors.Transparent : Colors.Black, 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag ? num2 : 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag2 ? (1.0 - num2) : 1.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(flag2 ? Colors.Transparent : Colors.Black, 1.0));
			linearGradientBrush2.Freeze();
			_viewport.OpacityMask = linearGradientBrush2;
		}
	}

	private void UpdateScrollThumb()
	{
		double actualHeight = _viewport.ActualHeight;
		double contentHeight = ContentHeight;
		if (contentHeight <= actualHeight + 0.5 || actualHeight <= 0.0)
		{
			_scrollThumb.Opacity = 0.0;
			_scrollThumb.IsHitTestVisible = false;
		}
		else
		{
			double num = Math.Max(18.0, actualHeight * actualHeight / contentHeight);
			_scrollThumb.Height = num;
			((TranslateTransform)_scrollThumb.RenderTransform).Y = (actualHeight - num) * (_viewport.Offset / Math.Max(1.0, MaxOffset));
		}
	}

	private void ShowScrollThumb()
	{
		UpdateScrollThumb();
		if (MaxOffset <= 0.0)
		{
			return;
		}
		_scrollThumb.BeginAnimation(UIElement.OpacityProperty, null);
		OverlayScroll.PaintThumb(_scrollThumb, _scrollThumb.IsMouseOver);
		_scrollThumb.IsHitTestVisible = true;
		if (_thumbFade == null)
		{
			_thumbFade = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(1000.0)
			};
			_thumbFade.Tick += delegate
			{
				_thumbFade.Stop();
				if (!_scrollThumb.IsMouseOver && !_scrollThumb.IsMouseCaptured)
				{
					_scrollThumb.IsHitTestVisible = false;
					BuddyTheme.Animate(_scrollThumb, UIElement.OpacityProperty, 0.0, BuddyTheme.EntryFade);
				}
			};
		}
		_thumbFade.Stop();
		_thumbFade.Start();
	}

	private int IndexOf(string id)
	{
		if (!_itemIndices.TryGetValue(id, out var value))
		{
			return -1;
		}
		return value;
	}

	private void ClearSelection()
	{
		_selected.Clear();
		_anchorId = null;
		RepaintTiles();
	}

	private void SelectOnly(ParkedFile f)
	{
		_selected.Clear();
		_selected.Add(f.Id);
		_anchorId = f.Id;
		RepaintTiles();
	}

	private List<ParkedFile> ActionTargets()
	{
		IEnumerable<ParkedFile> source = ((_selected.Count > 0) ? _items.Where((ParkedFile f) => _selected.Contains(f.Id)) : _items);
		return source.Where((ParkedFile f) => !f.IsMissing).ToList();
	}

	private List<string> ActionIdsFor(ParkedFile item)
	{
		if (!_selected.Contains(item.Id) || _selected.Count <= 1)
		{
			List<string> list = new List<string>();
			list.Add(item.Id);
			return list;
		}
		return (from f in _items
			where _selected.Contains(f.Id)
			select f.Id).ToList();
	}

	private void OnTileMouseDown(Tile t, MouseButtonEventArgs e)
	{
		e.Handled = true;
		_root.Focus();
		ParkedFile item = t.Item;
		if (e.ClickCount >= 2)
		{
			_pressTile = null;
			if (item.IsMissing)
			{
				Locate(item);
			}
			else
			{
				OpenItem(item);
			}
			return;
		}
		_pressPoint = e.GetPosition(_root);
		_pressTile = t;
		_pressDeferSelect = false;
		bool flag = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
		if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _anchorId != null)
		{
			int num = IndexOf(_anchorId);
			int num2 = IndexOf(item.Id);
			if (num < 0)
			{
				num = num2;
			}
			if (!flag)
			{
				_selected.Clear();
			}
			for (int i = Math.Min(num, num2); i <= Math.Max(num, num2); i++)
			{
				_selected.Add(_items[i].Id);
			}
		}
		else if (flag || _selectMode)
		{
			if (!_selected.Remove(item.Id))
			{
				_selected.Add(item.Id);
			}
			_anchorId = item.Id;
		}
		else if (_selected.Contains(item.Id))
		{
			_pressDeferSelect = true;
		}
		else
		{
			_selected.Clear();
			_selected.Add(item.Id);
			_anchorId = item.Id;
		}
		RepaintTiles();
	}

	private void OnTileMouseUp(Tile t, MouseButtonEventArgs e)
	{
		if (_pressTile == t && _pressDeferSelect)
		{
			SelectOnly(t.Item);
		}
		_pressTile = null;
	}

	private void OnTileMouseMove(Tile t, MouseEventArgs e)
	{
		if (_pressTile == t && e.LeftButton == MouseButtonState.Pressed)
		{
			Vector vector = e.GetPosition(_root) - _pressPoint;
			if (!(Math.Abs(vector.X) < SystemParameters.MinimumHorizontalDragDistance) || !(Math.Abs(vector.Y) < SystemParameters.MinimumVerticalDragDistance))
			{
				_pressTile = null;
				StartDragOut(t);
			}
		}
	}

	private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
	{
		_root.Focus();
		_banding = true;
		_bandOrigin = e.GetPosition(_canvas);
		bool flag = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0 || _selectMode;
		_bandBase = (flag ? new HashSet<string>(_selected) : new HashSet<string>());
		if (!flag && _selected.Count > 0)
		{
			_selected.Clear();
			RepaintTiles();
		}
		_viewport.CaptureMouse();
		e.Handled = true;
	}

	private void OnViewportMouseMove(object sender, MouseEventArgs e)
	{
		if (!_banding)
		{
			return;
		}
		if (e.LeftButton != MouseButtonState.Pressed)
		{
			EndBand();
			return;
		}
		double y = e.GetPosition(_viewport).Y;
		if (y < 0.0)
		{
			SetOffset(_viewport.Offset + Math.Max(-24.0, y), animate: false);
		}
		else if (y > _viewport.ActualHeight)
		{
			SetOffset(_viewport.Offset + Math.Min(24.0, y - _viewport.ActualHeight), animate: false);
		}
		Point position = e.GetPosition(_canvas);
		Rect rect = new Rect(_bandOrigin, position);
		if (rect.Width < 3.0 && rect.Height < 3.0)
		{
			return;
		}
		_band.Visibility = Visibility.Visible;
		Canvas.SetLeft(_band, rect.X);
		Canvas.SetTop(_band, rect.Y);
		_band.Width = rect.Width;
		_band.Height = rect.Height;
		HashSet<string> hashSet = new HashSet<string>(_bandBase);
		for (int i = 0; i < _items.Count; i++)
		{
			if (TileRect(i).IntersectsWith(rect))
			{
				hashSet.Add(_items[i].Id);
			}
		}
		if (!hashSet.SetEquals(_selected))
		{
			_selected.Clear();
			_selected.UnionWith(hashSet);
			RepaintTiles();
		}
	}

	private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_banding)
		{
			EndBand();
		}
	}

	private void EndBand()
	{
		if (_banding)
		{
			_banding = false;
			_band.Visibility = Visibility.Collapsed;
			if (_viewport.IsMouseCaptured)
			{
				_viewport.ReleaseMouseCapture();
			}
			ParkedFile parkedFile = _items.LastOrDefault((ParkedFile f) => _selected.Contains(f.Id));
			if (parkedFile != null && (_anchorId == null || !_selected.Contains(_anchorId)))
			{
				_anchorId = parkedFile.Id;
			}
			RepaintTiles();
		}
	}

	private void ShowTileMenu(Tile t)
	{
		ParkedFile f = t.Item;
		if (!_selected.Contains(f.Id))
		{
			SelectOnly(f);
		}
		List<string> ids = ActionIdsFor(f);
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		if (f.IsMissing)
		{
			list.Add(new BuddyMenuItem("Locate…", "search", delegate
			{
				Locate(f);
			}));
		}
		else
		{
			list.Add(new BuddyMenuItem("Open", "open", delegate
			{
				OpenItem(f);
			}));
			if (DropTool.Available)
			{
				list.Add(new BuddyMenuItem("Send to phone", "phone_arrow_up", delegate
				{
					DropTool.SendFiles((from x in _items
						where ids.Contains(x.Id) && !x.IsMissing
						select x.FullPath).ToArray());
				}));
			}
			list.Add(new BuddyMenuItem("Show in folder", "folder", delegate
			{
				ShowInFolder(f);
			}));
			list.Add(new BuddyMenuItem((ids.Count > 1) ? ("Copy " + ids.Count + " files") : "Copy", "copy", delegate
			{
				CopyToClipboard(_items.Where((ParkedFile x) => ids.Contains(x.Id) && !x.IsMissing).ToList());
			}));
		}
		foreach (ShelfTab item in _svc.Shelves.Where((ShelfTab s) => s.Id != _shelfId).Take(6))
		{
			ShelfTab target = item;
			list.Add(new BuddyMenuItem("Move to " + target.Name, "arrow_swap", delegate
			{
				_svc.MoveItems(_shelfId, target.Id, ids);
			}));
		}
		list.Add(new BuddyMenuItem((ids.Count > 1) ? ("Remove " + ids.Count + " from shelf") : "Remove from shelf", "delete", delegate
		{
			RemoveWithUndo(ids);
		}));
		BuddyDropdown.Show(t.Thumb, list);
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (Keyboard.FocusedElement is TextBox)
		{
			return;
		}
		bool flag = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
		bool extend = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
		if (flag && e.Key == Key.Z && _toast != null && _toast.IsPending)
		{
			_toast.Undo();
			e.Handled = true;
			return;
		}
		if (flag && e.Key == Key.V)
		{
			PasteFromClipboard();
			e.Handled = true;
			return;
		}
		if (flag && e.Key == Key.A)
		{
			_selected.Clear();
			foreach (ParkedFile item in _items)
			{
				_selected.Add(item.Id);
			}
			if (_items.Count > 0)
			{
				_anchorId = _items[0].Id;
			}
			RepaintTiles();
			e.Handled = true;
			return;
		}
		if (flag && e.Key == Key.C)
		{
			CopyToClipboard(ActionTargets());
			e.Handled = true;
			return;
		}
		switch (e.Key)
		{
		case Key.Delete:
			if (_selected.Count > 0)
			{
				RemoveWithUndo(_selected.ToList());
			}
			e.Handled = true;
			break;
		case Key.Return:
		{
			List<ParkedFile> list = ActionTargets();
			if (_selected.Count > 0)
			{
				if (list.Count == 0)
				{
					ParkedFile parkedFile = _items.FirstOrDefault((ParkedFile f) => _selected.Contains(f.Id));
					if (parkedFile != null)
					{
						Locate(parkedFile);
					}
				}
				foreach (ParkedFile item2 in list.Take(10))
				{
					OpenItem(item2);
				}
			}
			e.Handled = true;
			break;
		}
		case Key.Left:
			MoveFocus(-1, extend);
			e.Handled = true;
			break;
		case Key.Right:
			MoveFocus(1, extend);
			e.Handled = true;
			break;
		case Key.Up:
			MoveFocus(-5, extend);
			e.Handled = true;
			break;
		case Key.Down:
			MoveFocus(5, extend);
			e.Handled = true;
			break;
		case Key.Home:
			MoveFocus(-_items.Count, extend);
			e.Handled = true;
			break;
		case Key.End:
			MoveFocus(_items.Count, extend);
			e.Handled = true;
			break;
		}
	}

	private void MoveFocus(int delta, bool extend)
	{
		if (_items.Count != 0)
		{
			int num = ((_anchorId != null) ? IndexOf(_anchorId) : (-1));
			int index = ((num >= 0) ? Math.Max(0, Math.Min(_items.Count - 1, num + delta)) : 0);
			ParkedFile parkedFile = _items[index];
			if (extend && num >= 0)
			{
				_selected.Add(parkedFile.Id);
				_anchorId = parkedFile.Id;
				RepaintTiles();
			}
			else
			{
				SelectOnly(parkedFile);
			}
			ScrollIntoView(index);
		}
	}

	private void OpenItem(ParkedFile f)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(f.FullPath);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: open failed: " + ex.Message);
			_svc.RefreshStatus();
			if (_toast != null)
			{
				_toast.ShowNotice((File.Exists(f.FullPath) || Directory.Exists(f.FullPath)) ? "Couldn't open · no app is set for this file type" : "Couldn't open · the file was moved or deleted");
			}
		}
	}

	private void ShowInFolder(ParkedFile f)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo("explorer.exe", "/select,\"" + f.FullPath + "\"");
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: reveal failed: " + ex.Message);
			if (_toast != null)
			{
				_toast.ShowNotice("Couldn't open the folder");
			}
		}
	}

	private void Locate(ParkedFile f)
	{
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = "Locate " + f.FileName;
			openFileDialog.FileName = f.FileName;
			openFileDialog.CheckFileExists = true;
			openFileDialog.Multiselect = false;
			OpenFileDialog openFileDialog2 = openFileDialog;
			string text = null;
			try
			{
				text = System.IO.Path.GetDirectoryName(f.FullPath);
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(text) && !f.IsNetworkPath && Directory.Exists(text))
			{
				openFileDialog2.InitialDirectory = text;
			}
			Window window = Window.GetWindow(_root);
			if (((window != null) ? openFileDialog2.ShowDialog(window) : openFileDialog2.ShowDialog()) == true)
			{
				_svc.RelocateItem(f.Id, openFileDialog2.FileName);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: locate failed: " + ex.Message);
		}
	}

	private void ShowCopyRecovery()
	{
		if (!IsInitialized || !_svc.Operations.AdmissionOpen)
		{
			return;
		}
		StackPanel stackPanel = new StackPanel();
		foreach (ShelfCopyRecovery recoveryItem in _svc.RecoveryItems)
		{
			ShelfCopyRecovery copy = recoveryItem;
			TextBlock textBlock = BuddyTheme.Text(Ellipsize(copy.Name, 32) + " — copy incomplete", 13.0, BuddyTheme.Text92, FontWeights.Normal);
			textBlock.Margin = new Thickness(8.0, 8.0, 8.0, 4.0);
			stackPanel.Children.Add(textBlock);
			if (copy.State != "Failed")
			{
				stackPanel.Children.Add(MenuRow("Select source again…", "folder", enabled: true, null, delegate
				{
					RecoverCopy(copy.Id, cancel: false);
				}));
			}
			stackPanel.Children.Add(MenuRow("Cancel failed copy", "dismiss_circle", enabled: true, null, delegate
			{
				RecoverCopy(copy.Id, cancel: true);
			}));
		}
		BuddyDropdown.ShowContent(_recoveryBtn, new ScrollViewer
		{
			Content = stackPanel,
			MaxHeight = 320.0,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		}, 4.0);
	}

	private async void RecoverCopy(string id, bool cancel)
	{
		try
		{
			if (!IsInitialized || !_svc.Operations.AdmissionOpen)
			{
				return;
			}
			if (cancel)
			{
				await _svc.CancelFailedCopyAsync(id);
			}
			else
			{
				OpenFileDialog picker = new OpenFileDialog
				{
					Title = "Select the original file again (or export it from its source application)",
					Multiselect = false
				};
				Window owner = Window.GetWindow(_root);
				if (((owner != null) ? picker.ShowDialog(owner) : picker.ShowDialog()) != true || !IsInitialized || !_svc.Operations.AdmissionOpen)
				{
					return;
				}
				await _svc.RetryFailedCopyFromFileAsync(id, picker.FileName);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf copy recovery failed: " + ex.GetType().Name);
			if (IsInitialized && _svc.Operations.AdmissionOpen)
			{
				MessageBox.Show("The copy could not be recovered. Select the original file again or cancel the failed copy. Retained data has not been deleted.", "File Shelf", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}
		if (IsInitialized && _svc.Operations.AdmissionOpen)
		{
			RefreshContent();
		}
	}

	private async void ChooseFiles()
	{
		int num = default(int);
		_ = num;
		_ = 0;
		try
		{
			OpenFileDialog dlg = new OpenFileDialog
			{
				Title = "Add files to " + _svc.ResolveShelfName(CurrentShelfId()),
				Multiselect = true
			};
			Window owner = Window.GetWindow(_root);
			if (((owner != null) ? dlg.ShowDialog(owner) : dlg.ShowDialog()) == true)
			{
				await AddPaths(CurrentShelfId(), dlg.FileNames);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: file picker failed: " + ex.Message);
		}
	}

	private void RemoveWithUndo(List<string> ids)
	{
		string shelfId = _shelfId;
		List<RemovedItem> removed = _svc.RemoveItems(shelfId, ids);
		if (removed.Count != 0)
		{
			_selected.ExceptWith(ids);
			string message = ((removed.Count == 1) ? ("Removed " + Ellipsize(removed[0].File.FileName, 28)) : ("Removed " + removed.Count + " items"));
			_toast.Show(message, delegate
			{
				_svc.RestoreItems(shelfId, removed);
			}, delegate
			{
				_svc.PurgeUnreferencedOwned();
			});
		}
	}

	private void ClearShelfWithUndo(string shelfId)
	{
		ShelfTab shelfTab = _svc.FindShelf(shelfId);
		if (shelfTab == null)
		{
			return;
		}
		List<RemovedItem> removed = _svc.ClearShelf(shelfId);
		if (removed.Count != 0)
		{
			_toast.Show("Cleared " + Ellipsize(shelfTab.Name, 24), delegate
			{
				_svc.RestoreItems(shelfId, removed);
			}, delegate
			{
				_svc.PurgeUnreferencedOwned();
			});
		}
	}

	private void DeleteShelfWithUndo(string shelfId)
	{
		int index;
		bool wasActive;
		ShelfTab removed = _svc.DeleteShelf(shelfId, out index, out wasActive);
		if (removed != null)
		{
			_toast.Show("Deleted " + Ellipsize(removed.Name, 24), delegate
			{
				_svc.RestoreShelf(removed, index, wasActive);
			}, delegate
			{
				_svc.PurgeUnreferencedOwned();
			});
		}
	}

	private static string Ellipsize(string s, int max)
	{
		if (string.IsNullOrEmpty(s) || s.Length <= max)
		{
			return s;
		}
		return s.Substring(0, max - 1) + "…";
	}

	private void CopyToClipboard(List<ParkedFile> files)
	{
		if (!IsInitialized || !_svc.Operations.AdmissionOpen || files == null || files.Count == 0)
		{
			return;
		}
		StringCollection stringCollection = new StringCollection();
		foreach (ParkedFile file in files)
		{
			stringCollection.Add(file.FullPath);
		}
		DataObject dataObject = new DataObject();
		dataObject.SetFileDropList(stringCollection);
		dataObject.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)));
		for (int i = 0; i < 5; i++)
		{
			try
			{
				System.Windows.Clipboard.SetDataObject(dataObject, copy: true);
				_copyBtn.ShowFeedback((files.Count == 1) ? "Copied" : ("Copied " + files.Count));
				return;
			}
			catch (COMException)
			{
				Thread.Sleep(30);
			}
			catch (Exception ex2)
			{
				TransactionLogger.Warn("Shelf: copy failed: " + ex2.Message);
				break;
			}
		}
		if (_toast != null)
		{
			_toast.ShowNotice("Couldn't copy · the clipboard is busy, try again");
		}
	}

	private async void PasteFromClipboard()
	{
		if (!IsInitialized || !_svc.Operations.AdmissionOpen)
		{
			return;
		}
		try
		{
			IDataObject data = System.Windows.Clipboard.GetDataObject();
			if (data != null && await ImportAny(data, CurrentShelfId()) > 0 && IsInitialized && _svc.Operations.AdmissionOpen)
			{
				SetOffset(0.0, animate: true);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: paste failed: " + ex.Message);
			if (_toast != null)
			{
				_toast.ShowNotice("Couldn't paste · the clipboard is busy, try again");
			}
		}
	}

	private void StartDragOut(Tile t)
	{
		List<string> ids = ActionIdsFor(t.Item);
		if (!_selected.Contains(t.Item.Id))
		{
			SelectOnly(t.Item);
		}
		List<ParkedFile> list = _items.Where((ParkedFile f) => ids.Contains(f.Id) && !f.IsMissing).ToList();
		if (list.Count == 0)
		{
			return;
		}
		DataObject dataObject = new DataObject();
		dataObject.SetData(DataFormats.FileDrop, list.Select((ParkedFile f) => f.FullPath).ToArray());
		dataObject.SetData("WindowsBuddy.ShelfItems", _shelfId + "|" + string.Join(",", ids));
		try
		{
			DragDrop.DoDragDrop(t.Root, dataObject, DragDropEffects.Copy | DragDropEffects.Link);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf: drag out failed: " + ex.Message);
		}
		finally
		{
			EndDropFeedback();
		}
	}

	private string ShelfRowAt(DragEventArgs e)
	{
		foreach (ShelfRow value in _rows.Values)
		{
			if (value.Root.IsVisible)
			{
				Point position = e.GetPosition(value.Root);
				if (position.X >= 0.0 && position.Y >= 0.0 && position.X < value.Root.ActualWidth && position.Y < value.Root.ActualHeight)
				{
					return value.Id;
				}
			}
		}
		return null;
	}

	private static string InternalSource(IDataObject data)
	{
		try
		{
			if (!data.GetDataPresent("WindowsBuddy.ShelfItems"))
			{
				return null;
			}
			return data.GetData("WindowsBuddy.ShelfItems") as string;
		}
		catch
		{
			return null;
		}
	}

	private void OnPreviewDragOver(object sender, DragEventArgs e)
	{
		e.Handled = true;
		_lastDragOver = DateTime.UtcNow;
		string text = InternalSource(e.Data);
		string text2 = ((text != null) ? text.Split('|')[0] : null);
		string text3 = ShelfRowAt(e);
		if (text != null)
		{
			if (text3 == null || text3 == text2)
			{
				e.Effects = DragDropEffects.None;
				BeginDropFeedback(null, isInternal: true, lit: false);
			}
			else
			{
				e.Effects = DragDropEffects.Copy & e.AllowedEffects;
				BeginDropFeedback(text3, isInternal: true);
			}
		}
		else if (!ShelfDataImport.CanImport(e.Data))
		{
			e.Effects = DragDropEffects.None;
		}
		else
		{
			e.Effects = (((e.AllowedEffects & DragDropEffects.Copy) != 0) ? DragDropEffects.Copy : (e.AllowedEffects & (DragDropEffects.Move | DragDropEffects.Link)));
			BeginDropFeedback(text3, isInternal: false);
		}
	}

	private void OnPreviewDragLeave(object sender, DragEventArgs e)
	{
		e.Handled = true;
		if (_dragLeaveTimer == null)
		{
			_dragLeaveTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(120.0)
			};
			_dragLeaveTimer.Tick += delegate
			{
				_dragLeaveTimer.Stop();
				if ((DateTime.UtcNow - _lastDragOver).TotalMilliseconds >= 100.0)
				{
					EndDropFeedback();
				}
			};
		}
		_dragLeaveTimer.Stop();
		_dragLeaveTimer.Start();
	}

	private async void OnPreviewDrop(object sender, DragEventArgs e)
	{
		if (!IsInitialized || !_svc.Operations.AdmissionOpen)
		{
			e.Handled = true;
			e.Effects = DragDropEffects.None;
			return;
		}
		e.Handled = true;
		string rowId = ShelfRowAt(e);
		EndDropFeedback();
		string internalData = InternalSource(e.Data);
		if (internalData != null)
		{
			string[] array = internalData.Split('|');
			if (rowId != null && array.Length == 2 && rowId != array[0])
			{
				_svc.MoveItems(array[0], rowId, array[1].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries));
				e.Effects = DragDropEffects.Copy;
			}
			else
			{
				e.Effects = DragDropEffects.None;
			}
			return;
		}
		string target = rowId ?? CurrentShelfId();
		e.Effects = (_svc.Operations.AdmissionOpen ? DragDropEffects.Copy : DragDropEffects.None);
		try
		{
			if (await ImportAny(e.Data, target) > 0 && target == _shelfId && IsInitialized && _svc.Operations.AdmissionOpen)
			{
				SetOffset(0.0, animate: true);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf drop failed: " + ex.Message);
			if (_toast != null)
			{
				_toast.ShowNotice("Couldn't add that · try again");
			}
		}
	}

	private async Task<int> ImportAny(IDataObject data, string shelfId)
	{
		if (data.GetDataPresent("Ayn.ClipboardDrag") && data.GetData("Ayn.ClipboardDrag") as string == "image")
		{
			return await ShelfDataImport.ImportAsync(data, _svc, shelfId);
		}
		string[] paths = null;
		try
		{
			if (data.GetDataPresent(DataFormats.FileDrop))
			{
				paths = data.GetData(DataFormats.FileDrop) as string[];
			}
		}
		catch
		{
		}
		if (paths != null && paths.Length > 0)
		{
			return await AddPaths(shelfId, paths);
		}
		int added = await ShelfDataImport.ImportAsync(data, _svc, shelfId);
		if (added == 0 && _toast != null && IsInitialized)
		{
			_toast.ShowNotice("Nothing to add · drop files or an image");
		}
		return added;
	}

	private void BeginDropFeedback(string rowId, bool isInternal, bool lit = true)
	{
		bool flag = _dragActive != lit || _dragTargetShelf != rowId || _dragInternal != isInternal;
		_dragActive = lit;
		_dragInternal = isInternal;
		if (_dragTargetShelf != rowId)
		{
			string dragTargetShelf = _dragTargetShelf;
			_dragTargetShelf = rowId;
			if (dragTargetShelf != null && _rows.TryGetValue(dragTargetShelf, out var value))
			{
				value.Target = false;
				PaintRow(value);
			}
			if (rowId != null && _rows.TryGetValue(rowId, out value))
			{
				value.Target = true;
				PaintRow(value);
			}
		}
		if (!flag)
		{
			return;
		}
		UpdateDropZone();
		foreach (Tile value2 in _tiles.Values)
		{
			PaintTile(value2);
		}
	}

	private void EndDropFeedback()
	{
		if (_dragLeaveTimer != null)
		{
			_dragLeaveTimer.Stop();
		}
		if (_dragActive || _dragTargetShelf != null)
		{
			BeginDropFeedback(null, isInternal: false, lit: false);
		}
	}

	private static FrameworkElement Centered(FrameworkElement fe)
	{
		fe.HorizontalAlignment = HorizontalAlignment.Center;
		fe.VerticalAlignment = VerticalAlignment.Center;
		return fe;
	}

	private static bool IsInside(DependencyObject child, DependencyObject parent)
	{
		if (child == null || parent == null)
		{
			return false;
		}
		for (DependencyObject dependencyObject = child; dependencyObject != null; dependencyObject = ((dependencyObject is Visual || dependencyObject is Visual3D) ? VisualTreeHelper.GetParent(dependencyObject) : LogicalTreeHelper.GetParent(dependencyObject)))
		{
			if (dependencyObject == parent)
			{
				return true;
			}
		}
		return false;
	}
}
