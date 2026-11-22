using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Tools.Drop;

namespace WindowsBuddy.Tools.Clipboard;

public class ClipboardTool : IBuddyTool, IEscapeAware
{
	private enum Filter
	{
		All,
		Text,
		Images,
		Links
	}

	private sealed class RowView : BuddyRow
	{
		private readonly ClipboardTool _owner;

		private readonly ContentControl _glyph;

		private readonly Border _thumb;

		private readonly Border _badge;

		private readonly Image _badgeImage;

		private readonly ContentControl _pin;

		private readonly Border _more;

		private readonly ContentControl _moreGlyph;

		private string _glyphName;

		private bool _pinned;

		private Point _dragStart;

		private bool _dragPending;

		private int _clickCount;

		protected override FrameworkElement ContextAnchor
		{
			get
			{
				if (_more.Visibility != 0)
				{
					return this;
				}
				return _more;
			}
		}

		public RowView(ClipboardTool owner)
			: base(28.0, 10.0, 24.0, 12.0, flat: true)
		{
			_owner = owner;
			MouseButtonEventHandler value = delegate(object s, MouseButtonEventArgs e)
			{
				DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
				while (dependencyObject != null && dependencyObject != this)
				{
					if (dependencyObject == _more)
					{
						return;
					}
					dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
				}
				if (base.Item != null)
				{
					_dragStart = e.GetPosition(this);
					_clickCount = e.ClickCount;
					_dragPending = true;
					CaptureMouse();
					e.Handled = true;
				}
			};
			base.PreviewMouseLeftButtonDown += value;
			base.PreviewMouseMove += delegate(object s, MouseEventArgs e)
			{
				if (_dragPending && e.LeftButton == MouseButtonState.Pressed && ClipboardDrag.PastThreshold(_dragStart, e.GetPosition(this)))
				{
					ClipboardItem item2 = base.Item as ClipboardItem;
					_dragPending = false;
					ReleaseMouseCapture();
					e.Handled = true;
					ClipboardDrag.Start(this, item2);
				}
			};
			base.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				if (_dragPending)
				{
					_dragPending = false;
					ReleaseMouseCapture();
					e.Handled = true;
					if (base.Item != null && Owner != null)
					{
						Owner.RaiseItemClick(base.Item, _clickCount);
					}
				}
			};
			base.LostMouseCapture += delegate
			{
				_dragPending = false;
			};
			TitleText.FontWeight = FontWeights.Medium;
			MetaText.FontWeight = FontWeights.Medium;
			MetaText.Foreground = BuddyTheme.Text30;
			MetaText.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			Grid grid = new Grid
			{
				Width = 28.0,
				Height = 28.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			_glyph = new ContentControl
			{
				Width = 16.0,
				Height = 16.0,
				Focusable = false,
				IsTabStop = false,
				IsHitTestVisible = false,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			grid.Children.Add(_glyph);
			_thumb = new Border
			{
				CornerRadius = new CornerRadius(5.0),
				Visibility = Visibility.Collapsed
			};
			grid.Children.Add(_thumb);
			_badgeImage = new Image
			{
				Width = 11.0,
				Height = 11.0,
				Stretch = Stretch.Uniform
			};
			RenderOptions.SetBitmapScalingMode(_badgeImage, BitmapScalingMode.HighQuality);
			_badge = new Border
			{
				Width = 13.0,
				Height = 13.0,
				CornerRadius = new CornerRadius(3.0),
				Background = BuddyTheme.Canvas,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0.0, 0.0, -3.0, -3.0),
				Child = _badgeImage,
				Visibility = Visibility.Collapsed
			};
			grid.Children.Add(_badge);
			Leading.Children.Add(grid);
			_pin = new ContentControl
			{
				Width = 12.0,
				Height = 12.0,
				Focusable = false,
				IsTabStop = false,
				IsHitTestVisible = false,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				Content = FluentIcons.Create("pin", 12.0, BuddyTheme.Text30, filled: true)
			};
			Trailing.Children.Add(_pin);
			_more = BuddyKit.GlyphButton("more_horizontal", 16.0, "More", delegate
			{
				if (base.Item is ClipboardItem item)
				{
					_owner.ShowRowMenu(_more, item);
				}
			}, out _moreGlyph);
			_more.HorizontalAlignment = HorizontalAlignment.Right;
			_more.VerticalAlignment = VerticalAlignment.Center;
			_more.Margin = new Thickness(0.0, 0.0, -4.0, 0.0);
			Trailing.Children.Add(_more);
		}

		protected override void UpdateVisuals()
		{
			base.UpdateVisuals();
			bool isMouseOver = base.IsMouseOver;
			_more.Visibility = ((!isMouseOver) ? Visibility.Collapsed : Visibility.Visible);
			_pin.Visibility = ((!_pinned || isMouseOver) ? Visibility.Collapsed : Visibility.Visible);
		}

		protected override void OnBind(object o)
		{
			ClipboardItem clipboardItem = (ClipboardItem)o;
			TitleText.Text = TitleFor(clipboardItem);
			MetaText.Text = MetaFor(clipboardItem);
			_pinned = clipboardItem.IsPinned;
			ClipKind kind = clipboardItem.Kind;
			SetGlyph(IconFor(kind));
			_thumb.Visibility = Visibility.Collapsed;
			_thumb.Background = null;
			switch (kind)
			{
			case ClipKind.Image:
			{
				string id = clipboardItem.Id;
				ImageSource thumbnail = ClipboardService.Instance.GetThumbnail(clipboardItem, delegate(ImageSource img)
				{
					if (IsBoundTo(id))
					{
						ShowThumb(img, favicon: false);
					}
				});
				if (thumbnail != null)
				{
					ShowThumb(thumbnail, favicon: false);
				}
				break;
			}
			case ClipKind.Link:
			{
				LinkMetadata cached = LinkMetadataService.GetCached(clipboardItem.Text);
				if (cached != null && cached.Favicon != null)
				{
					ShowThumb(cached.Favicon, favicon: true);
				}
				break;
			}
			}
			_badge.Visibility = Visibility.Collapsed;
			_badgeImage.Source = null;
			string bid = clipboardItem.Id;
			ImageSource appIcon = AppBadgeHelper.GetAppIcon(clipboardItem, delegate(ImageSource img)
			{
				if (IsBoundTo(bid))
				{
					ShowBadge(img);
				}
			});
			if (appIcon != null)
			{
				ShowBadge(appIcon);
			}
		}

		protected override void OnUnbind()
		{
			_thumb.Background = null;
			_badgeImage.Source = null;
		}

		public override void Refresh()
		{
			if (base.Item is ClipboardItem item)
			{
				MetaText.Text = MetaFor(item);
			}
		}

		private bool IsBoundTo(string id)
		{
			if (base.Item is ClipboardItem clipboardItem)
			{
				return clipboardItem.Id == id;
			}
			return false;
		}

		private void SetGlyph(string name)
		{
			if (!(name == _glyphName))
			{
				_glyphName = name;
				_glyph.Content = FluentIcons.Create(name, 16.0, BuddyTheme.Text50, filled: false);
			}
		}

		private void ShowThumb(ImageSource img, bool favicon)
		{
			if (img != null)
			{
				ImageBrush imageBrush2;
				if (favicon)
				{
					ImageBrush imageBrush = new ImageBrush(img);
					imageBrush.Stretch = Stretch.Uniform;
					imageBrush.Viewport = new Rect(0.21, 0.21, 0.58, 0.58);
					imageBrush2 = imageBrush;
				}
				else
				{
					ImageBrush imageBrush3 = new ImageBrush(img);
					imageBrush3.Stretch = Stretch.UniformToFill;
					imageBrush2 = imageBrush3;
					RenderOptions.SetBitmapScalingMode(imageBrush2, BitmapScalingMode.HighQuality);
				}
				imageBrush2.Freeze();
				_thumb.Background = imageBrush2;
				_thumb.Visibility = Visibility.Visible;
				_glyph.Content = null;
				_glyphName = null;
			}
		}

		private void ShowBadge(ImageSource img)
		{
			_badgeImage.Source = img;
			_badge.Visibility = Visibility.Visible;
		}
	}

	private const double GridW = 728.0;

	private const double GridH = 242.0;

	private const double SplitPad = 16.0;

	private const double LeftW = 319.0;

	private const double ListInset = 12.0;

	private const double TrailOut = 10.0;

	private const double TabsH = 28.0;

	private const double ListTop = 28.0;

	private const double RowH = 40.0;

	private const double HeaderH = 33.0;

	private const double Thumb = 28.0;

	private const double Glyph = 16.0;

	private const double MetaRowH = 28.0;

	private const double FooterH = 30.0;

	private const double PreviewFont = 13.0;

	private const double PreviewLine = 19.0;

	private Grid _root;

	private readonly List<FrameworkElement> _cards = new List<FrameworkElement>();

	private Filter _filter;

	private string _query = string.Empty;

	private ClipboardItem _selected;

	private List<ClipboardItem> _visible = new List<ClipboardItem>();

	private string _listSignature;

	private EventHandler _itemsChangedHandler;

	private bool _attached;

	private DispatcherTimer _ageTimer;

	private Grid _leftBody;

	private Grid _tabsLayer;

	private readonly Dictionary<Filter, FrameworkElement> _tabs = new Dictionary<Filter, FrameworkElement>();

	private readonly Dictionary<Filter, TextBlock> _tabLabels = new Dictionary<Filter, TextBlock>();

	private readonly Dictionary<Filter, TextBlock> _tabCounts = new Dictionary<Filter, TextBlock>();

	private Border _underline;

	private TranslateTransform _underlineX;

	private BuddyInlineSearch _search;

	private BuddyVirtualList _list;

	private StackPanel _listEmpty;

	private TextBlock _listEmptyText;

	private TextBlock _listEmptyHint;

	private BuddyUndoToast _toast;

	private BuddyLabel _metaLabel;

	private FrameworkElement _metaActions;

	private Border _pinButton;

	private Border _deleteButton;

	private ContentControl _pinIcon;

	private Grid _contentHost;

	private ScrollViewer _textScroll;

	private TextBlock _textBody;

	private Grid _imageHost;

	private Border _imageFrame;

	private StackPanel _linkPanel;

	private TextBlock _linkHost;

	private TextBlock _linkUrl;

	private StackPanel _filesPanel;

	private TextBlock _previewEmpty;

	private Grid _footer;

	private TextBlock _footerMeta;

	private BuddyPrimaryPill _copyPill;

	private Border _actionButton;

	private ContentControl _actionIcon;

	private string _previewId;

	private long _previewTicks;

	private bool _previewPinned;

	private Border _clearButton;

	internal static Func<DateTime> ClearClock = () => DateTime.Now;

	internal static readonly ClipboardClearRange[] ClearRanges = new ClipboardClearRange[4]
	{
		ClipboardClearRange.Today,
		ClipboardClearRange.Yesterday,
		ClipboardClearRange.Last7Days,
		ClipboardClearRange.All
	};

	private static FontFamily _codeFont;

	private int _fadeState = -1;

	private double _fadeH;

	public string Id => "clipboard";

	public string DisplayName => "Clipboard";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	internal BuddyUndoToast ToastForQa => _toast;

	private static FontFamily CodeFont => _codeFont ?? (_codeFont = new FontFamily("Cascadia Mono, Consolas, Courier New"));

	public ClipboardTool()
	{
		IsEnabled = true;
		OrderIndex = 5;
		State = ToolState.Idle;
	}

	public static string FormatItemCount(int count)
	{
		return count.ToString(CultureInfo.CurrentCulture) + ((count == 1) ? " item" : " items");
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		ClipboardService.PrepareEditor = delegate
		{
			if (_toast != null)
			{
				_toast.Commit();
			}
		};
		_itemsChangedHandler = delegate
		{
			SafeBeginInvoke(OnItemsChanged);
		};
	}

	public void Shutdown()
	{
		Detach();
		if (_ageTimer != null)
		{
			_ageTimer.Stop();
		}
		if (_search != null)
		{
			_search.StopDebounce();
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		Attach();
		_listSignature = null;
		Refresh();
		if (_ageTimer == null)
		{
			_ageTimer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(30.0)
			};
			_ageTimer.Tick += delegate
			{
				RefreshAges();
			};
		}
		_ageTimer.Start();
		if (_root != null)
		{
			_root.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(FocusList));
		}
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		Detach();
		if (_ageTimer != null)
		{
			_ageTimer.Stop();
		}
		if (_search != null)
		{
			_search.StopDebounce();
		}
		BuddyDropdown.Close();
		if (_toast != null)
		{
			_toast.Commit();
		}
		ResetCopyFeedback();
	}

	private void Attach()
	{
		if (!_attached && _itemsChangedHandler != null)
		{
			_attached = true;
			ClipboardService.Instance.ItemsChanged += _itemsChangedHandler;
		}
	}

	private void Detach()
	{
		if (_attached)
		{
			_attached = false;
			ClipboardService.Instance.ItemsChanged -= _itemsChangedHandler;
		}
	}

	public bool HandleEscape()
	{
		if (BuddyDropdown.IsOpen)
		{
			BuddyDropdown.Close();
			return true;
		}
		if (_search != null && _search.IsOpen)
		{
			_search.Close();
			return true;
		}
		return false;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return FluentIcons.Create("clipboard_text_ltr", size, stroke ?? BuddyTheme.Text100, filled: false);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return new CollapsedBadgeData(string.Format(CultureInfo.CurrentCulture, "Clip: {0}", ClipboardService.Instance.Items.Count), "#FFFFFF");
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			Refresh();
			return _root;
		}
		Grid grid = new Grid();
		grid.Width = 728.0;
		grid.Height = 242.0;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		grid.Tag = "NoOuterScroll";
		Grid grid2 = BuddyTheme.Crisp(grid);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(319.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(16.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(16.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement frameworkElement = BuildListColumn();
		Grid.SetColumn(frameworkElement, 0);
		Grid.SetColumnSpan(frameworkElement, 2);
		grid2.Children.Add(frameworkElement);
		_cards.Add(frameworkElement);
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.IsHitTestVisible = false;
		Rectangle element = rectangle;
		Grid.SetColumn(element, 2);
		grid2.Children.Add(element);
		FrameworkElement frameworkElement2 = BuildDetailColumn();
		Grid.SetColumn(frameworkElement2, 4);
		grid2.Children.Add(frameworkElement2);
		_cards.Add(frameworkElement2);
		grid2.PreviewKeyDown += OnPreviewKeyDown;
		_root = grid2;
		Refresh();
		return grid2;
	}

	private FrameworkElement BuildListColumn()
	{
		Grid grid = new Grid();
		grid.Focusable = true;
		grid.FocusVisualStyle = null;
		grid.Background = Brushes.Transparent;
		grid.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		Grid grid2 = (_leftBody = grid);
		_tabsLayer = new Grid
		{
			Height = 28.0,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
		StackPanel stackPanel2 = stackPanel;
		Filter[] array = new Filter[4]
		{
			Filter.All,
			Filter.Text,
			Filter.Images,
			Filter.Links
		};
		foreach (Filter f in array)
		{
			stackPanel2.Children.Add(BuildTab(f));
		}
		_tabsLayer.Children.Add(stackPanel2);
		_underlineX = new TranslateTransform();
		_underline = new Border
		{
			Height = 2.0,
			Width = 0.0,
			CornerRadius = new CornerRadius(1.0),
			Background = BuddyTheme.Mark,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Bottom,
			RenderTransform = _underlineX,
			IsHitTestVisible = false
		};
		_tabsLayer.Children.Add(_underline);
		stackPanel2.LayoutUpdated += delegate
		{
			if (_underline.Width == 0.0)
			{
				PlaceUnderline(animate: false);
			}
		};
		ContentControl glyphHost;
		Border border = BuddyKit.GlyphButton("search", 16.0, "Search (Ctrl+F)", OpenSearch, out glyphHost);
		border.HorizontalAlignment = HorizontalAlignment.Right;
		border.VerticalAlignment = VerticalAlignment.Center;
		border.Margin = new Thickness(0.0, 0.0, -4.0, 0.0);
		_tabsLayer.Children.Add(border);
		_clearButton = BuddyKit.GlyphButton("delete", 16.0, "Clear history…", delegate
		{
			ShowClearMenu(_clearButton);
		}, out var _);
		_clearButton.HorizontalAlignment = HorizontalAlignment.Right;
		_clearButton.VerticalAlignment = VerticalAlignment.Center;
		_clearButton.Margin = new Thickness(0.0, 0.0, 24.0, 0.0);
		_clearButton.Tag = "clipboard-clear";
		AutomationProperties.SetName(_clearButton, "Clear history");
		_tabsLayer.Children.Add(_clearButton);
		grid2.Children.Add(_tabsLayer);
		_search = new BuddyInlineSearch(_tabsLayer, "Search clipboard", 28.0, flat: true)
		{
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		_search.QueryChanged += delegate(string q)
		{
			_query = q;
			_listSignature = null;
			Refresh();
			if (_list != null)
			{
				_list.ScrollToTop();
			}
		};
		_search.Closed += FocusList;
		grid2.Children.Add(_search);
		_list = new BuddyVirtualList(() => new RowView(this), (object o) => ((ClipboardItem)o).Id, 40.0, 33.0, 33.0, 12.0)
		{
			Margin = new Thickness(-12.0, 28.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Stretch,
			HeaderInk = BuddyTheme.Text30,
			FadeHeight = 16.0
		};
		_list.ItemClick += delegate(object o, int clicks)
		{
			ClipboardItem item = (ClipboardItem)o;
			FocusList();
			if (clicks >= 2)
			{
				CopyAndClose(item);
			}
			else
			{
				Select(item, scrollIntoView: true);
			}
		};
		_list.ItemContext += delegate(object o, FrameworkElement anchor)
		{
			ShowRowMenu(anchor, (ClipboardItem)o);
		};
		grid2.Children.Add(_list);
		_listEmpty = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 28.0, 10.0, 0.0),
			Visibility = Visibility.Collapsed,
			IsHitTestVisible = false
		};
		FrameworkElement frameworkElement = FluentIcons.Create("clipboard", 18.0, BuddyTheme.Text50, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		_listEmpty.Children.Add(frameworkElement);
		_listEmptyText = BuddyTheme.Text("Nothing copied yet", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_listEmptyText.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		_listEmptyText.HorizontalAlignment = HorizontalAlignment.Center;
		_listEmpty.Children.Add(_listEmptyText);
		_listEmptyHint = BuddyTheme.Text("Text, images (up to 5 MB) and files you copy show up here", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_listEmptyHint.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		_listEmptyHint.HorizontalAlignment = HorizontalAlignment.Center;
		_listEmptyHint.TextAlignment = TextAlignment.Center;
		_listEmptyHint.TextWrapping = TextWrapping.Wrap;
		_listEmptyHint.MaxWidth = 250.0;
		_listEmpty.Children.Add(_listEmptyHint);
		grid2.Children.Add(_listEmpty);
		_toast = new BuddyUndoToast();
		grid2.Children.Add(_toast);
		grid2.MouseLeftButtonDown += delegate
		{
			if (!(Keyboard.FocusedElement is TextBox))
			{
				FocusList();
			}
		};
		return grid2;
	}

	private FrameworkElement BuildTab(Filter f)
	{
		Border border = new Border();
		border.Height = 28.0;
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.Margin = new Thickness(0.0, 0.0, 16.0, 0.0);
		Border border2 = border;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		TextBlock label = BuddyTheme.Text(f.ToString(), 13.0, BuddyTheme.Text50, FontWeights.Medium);
		label.VerticalAlignment = VerticalAlignment.Center;
		TextBlock textBlock = BuddyTheme.Text("0", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(5.0, 1.0, 0.0, 0.0);
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		stackPanel2.Children.Add(label);
		stackPanel2.Children.Add(textBlock);
		border2.Child = stackPanel2;
		border2.MouseEnter += delegate
		{
			if (_filter != f)
			{
				label.Foreground = BuddyTheme.Text92;
			}
		};
		border2.MouseLeave += delegate
		{
			if (_filter != f)
			{
				label.Foreground = BuddyTheme.Text50;
			}
		};
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			SetFilter(f);
			FocusList();
		};
		_tabs[f] = border2;
		_tabLabels[f] = label;
		_tabCounts[f] = textBlock;
		return border2;
	}

	private void SetFilter(Filter f)
	{
		if (_filter == f)
		{
			return;
		}
		_filter = f;
		foreach (KeyValuePair<Filter, TextBlock> tabLabel in _tabLabels)
		{
			tabLabel.Value.Foreground = ((tabLabel.Key == f) ? BuddyTheme.Text100 : BuddyTheme.Text50);
		}
		PlaceUnderline(animate: true);
		_listSignature = null;
		Refresh();
		if (_list != null)
		{
			_list.ScrollToTop();
		}
	}

	private void PlaceUnderline(bool animate)
	{
		if (_underline != null && _tabs.TryGetValue(_filter, out var value) && !(value.ActualWidth <= 0.0))
		{
			FrameworkElement frameworkElement = ((Border)value).Child as FrameworkElement;
			double x = value.TranslatePoint(new Point(0.0, 0.0), _tabsLayer).X;
			double num = frameworkElement?.ActualWidth ?? value.ActualWidth;
			Duration duration = new Duration(TimeSpan.FromMilliseconds(150.0));
			if (animate && CapsuleMotion.AnimationsEnabled)
			{
				_underlineX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, duration)
				{
					EasingFunction = BuddyTheme.EaseOut
				});
				_underline.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(num, duration)
				{
					EasingFunction = BuddyTheme.EaseOut
				});
			}
			else
			{
				_underlineX.BeginAnimation(TranslateTransform.XProperty, null);
				_underline.BeginAnimation(FrameworkElement.WidthProperty, null);
				_underlineX.X = x;
				_underline.Width = num;
			}
		}
	}

	private void OpenSearch()
	{
		if (_search != null)
		{
			_search.Open();
		}
	}

	private void FocusList()
	{
		if (_leftBody != null && State == ToolState.Active && (_search == null || !_search.IsOpen || Keyboard.FocusedElement != _search.Box))
		{
			Keyboard.Focus(_leftBody);
		}
	}

	private FrameworkElement BuildDetailColumn()
	{
		Grid grid = new Grid();
		Grid grid2 = new Grid();
		grid2.Height = 28.0;
		grid2.VerticalAlignment = VerticalAlignment.Top;
		Grid grid3 = grid2;
		_metaLabel = BuddyTheme.LabelText(string.Empty);
		_metaLabel.Foreground = BuddyTheme.Text30;
		_metaLabel.VerticalAlignment = VerticalAlignment.Center;
		grid3.Children.Add(_metaLabel);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 0.0, -4.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		_pinButton = BuddyKit.GlyphButton("pin", 16.0, "Pin (Ctrl+P)", delegate
		{
			if (_selected != null)
			{
				TogglePin(_selected);
			}
		}, out _pinIcon);
		_deleteButton = BuddyKit.GlyphButton("delete", 16.0, "Delete (Del)", delegate
		{
			if (_selected != null)
			{
				DeleteItem(_selected);
			}
		}, out var _);
		_deleteButton.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
		stackPanel2.Children.Add(_pinButton);
		stackPanel2.Children.Add(_deleteButton);
		grid3.Children.Add(stackPanel2);
		_metaActions = stackPanel2;
		grid.Children.Add(grid3);
		_contentHost = new Grid
		{
			Margin = new Thickness(0.0, 34.0, 0.0, 40.0),
			ClipToBounds = true
		};
		_textBody = BuddyTheme.Text(string.Empty, 13.0, BuddyTheme.Text92, FontWeights.Normal);
		_textBody.TextWrapping = TextWrapping.Wrap;
		_textBody.TextTrimming = TextTrimming.None;
		_textBody.LineHeight = 19.0;
		_textBody.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		_textScroll = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			CanContentScroll = false,
			Focusable = false,
			Content = _textBody
		};
		_textScroll.ScrollChanged += delegate
		{
			UpdateTextFade();
		};
		_textScroll.SizeChanged += delegate
		{
			UpdateTextFade();
		};
		_contentHost.Children.Add(_textScroll);
		_imageHost = new Grid
		{
			Visibility = Visibility.Collapsed
		};
		_imageFrame = new Border
		{
			CornerRadius = new CornerRadius(10.0),
			Background = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_imageHost.Children.Add(_imageFrame);
		_imageHost.SizeChanged += delegate
		{
			FitImageFrame();
		};
		_contentHost.Children.Add(_imageHost);
		_linkPanel = new StackPanel
		{
			Visibility = Visibility.Collapsed
		};
		_linkHost = BuddyTheme.Text(string.Empty, 15.0, BuddyTheme.Text100, AynType.Medium);
		_linkPanel.Children.Add(_linkHost);
		_linkUrl = BuddyTheme.Text(string.Empty, 13.0, BuddyTheme.Text50, FontWeights.Medium);
		_linkUrl.TextWrapping = TextWrapping.Wrap;
		_linkUrl.TextTrimming = TextTrimming.CharacterEllipsis;
		_linkUrl.LineHeight = 17.0;
		_linkUrl.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		_linkUrl.MaxHeight = 68.0;
		_linkUrl.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		_linkPanel.Children.Add(_linkUrl);
		FrameworkElement frameworkElement = LinkButton("Open", delegate
		{
			if (_selected != null)
			{
				OpenLink(_selected);
			}
		});
		frameworkElement.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		_linkPanel.Children.Add(frameworkElement);
		_contentHost.Children.Add(_linkPanel);
		_filesPanel = new StackPanel
		{
			Visibility = Visibility.Collapsed
		};
		_contentHost.Children.Add(_filesPanel);
		_previewEmpty = BuddyTheme.Text("Nothing to preview", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_previewEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_previewEmpty.VerticalAlignment = VerticalAlignment.Center;
		_previewEmpty.Visibility = Visibility.Collapsed;
		grid.Children.Add(_contentHost);
		grid.Children.Add(_previewEmpty);
		_footer = new Grid
		{
			Height = 30.0,
			VerticalAlignment = VerticalAlignment.Bottom
		};
		_footer.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_footer.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_footer.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_footerMeta = BuddyTheme.Text(string.Empty, 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_footerMeta.VerticalAlignment = VerticalAlignment.Center;
		_footerMeta.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		Typography.SetNumeralAlignment(_footerMeta, FontNumeralAlignment.Tabular);
		_footer.Children.Add(_footerMeta);
		_copyPill = new BuddyPrimaryPill("copy", "Copy", 28.0, 14.0, new Thickness(14.0, 0.0, 14.0, 0.0), 6.0)
		{
			ToolTip = BuddyTooltip.Create("Copy (Enter copies and closes)"),
			VerticalAlignment = VerticalAlignment.Center,
			Hold = TimeSpan.FromSeconds(1.0)
		};
		_copyPill.Click += delegate
		{
			if (_selected != null)
			{
				CopyItem(_selected, closeAfter: false);
			}
		};
		Grid.SetColumn(_copyPill, 1);
		_footer.Children.Add(_copyPill);
		_actionButton = BuddyKit.GlyphButton("save", 16.0, "Save as…", OnActionButton, out _actionIcon);
		_actionButton.VerticalAlignment = VerticalAlignment.Center;
		_actionButton.Margin = new Thickness(8.0, 0.0, -4.0, 0.0);
		Grid.SetColumn(_actionButton, 2);
		_footer.Children.Add(_actionButton);
		grid.Children.Add(_footer);
		return grid;
	}

	private static FrameworkElement LinkButton(string text, Action click)
	{
		TextBlock label = BuddyTheme.Text(text, 13.0, BuddyTheme.Text92, AynType.Medium);
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = Cursors.Hand;
		border.HorizontalAlignment = HorizontalAlignment.Left;
		border.Child = label;
		Border border2 = border;
		border2.MouseEnter += delegate
		{
			label.Foreground = BuddyTheme.Text100;
		};
		border2.MouseLeave += delegate
		{
			label.Foreground = BuddyTheme.Text92;
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

	private void ShowCopyFeedback()
	{
		_copyPill.ShowFeedback("Copied");
	}

	private void ResetCopyFeedback()
	{
		if (_copyPill != null)
		{
			_copyPill.ResetFeedback();
		}
	}

	private void OnItemsChanged()
	{
		if (State == ToolState.Active)
		{
			ToolRouter.Instance.UpdateHeaderSubtitle(FormatItemCount(ClipboardService.Instance.Items.Count));
		}
		Refresh();
	}

	private static bool InFilter(ClipboardItem i, Filter f)
	{
		switch (f)
		{
		case Filter.Text:
		{
			ClipKind kind = i.Kind;
			if (kind != 0 && kind != ClipKind.Code)
			{
				return kind == ClipKind.Email;
			}
			return true;
		}
		case Filter.Images:
			return i.Kind == ClipKind.Image;
		case Filter.Links:
			return i.Kind == ClipKind.Link;
		default:
			return true;
		}
	}

	private void Refresh()
	{
		if (_root == null)
		{
			return;
		}
		try
		{
			IReadOnlyList<ClipboardItem> items = ClipboardService.Instance.Items;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			foreach (ClipboardItem item in items)
			{
				if (InFilter(item, Filter.Text))
				{
					num++;
				}
				else if (item.Kind == ClipKind.Image)
				{
					num2++;
				}
				else if (item.Kind == ClipKind.Link)
				{
					num3++;
				}
			}
			SetCount(Filter.All, items.Count);
			SetCount(Filter.Text, num);
			SetCount(Filter.Images, num2);
			SetCount(Filter.Links, num3);
			foreach (KeyValuePair<Filter, TextBlock> tabLabel in _tabLabels)
			{
				tabLabel.Value.Foreground = ((tabLabel.Key == _filter) ? BuddyTheme.Text100 : BuddyTheme.Text50);
			}
			List<ClipboardItem> list = new List<ClipboardItem>();
			foreach (ClipboardItem item2 in items)
			{
				if (InFilter(item2, _filter) && ClipboardService.Matches(item2, _query))
				{
					list.Add(item2);
				}
			}
			string text = string.Concat(_filter, ":", _query, ":", ClipboardService.Instance.Version, ":", list.Count);
			bool flag = text != _listSignature;
			_listSignature = text;
			_visible = list;
			if (_selected == null || !list.Any((ClipboardItem i) => i.Id == _selected.Id))
			{
				_selected = ((list.Count > 0) ? list[0] : null);
			}
			else
			{
				_selected = list.First((ClipboardItem i) => i.Id == _selected.Id);
			}
			if (flag)
			{
				_list.SetItems(list, SectionFor, _selected);
			}
			else
			{
				_list.SetSelected(_selected);
			}
			if (list.Count == 0)
			{
				_listEmptyText.Text = ((items.Count == 0) ? "Nothing copied yet" : ((_query.Length > 0) ? "No matches" : ((_filter == Filter.Images) ? "No images yet" : ((_filter == Filter.Links) ? "No links yet" : "No text yet"))));
				_listEmptyHint.Visibility = ((items.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
				_listEmpty.Visibility = Visibility.Visible;
			}
			else
			{
				_listEmpty.Visibility = Visibility.Collapsed;
			}
			UpdatePreview(agesOnly: false);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Clipboard tab refresh failed: " + ex.Message);
		}
	}

	private void SetCount(Filter f, int n)
	{
		if (!_tabCounts.TryGetValue(f, out var value))
		{
			return;
		}
		string text = n.ToString(CultureInfo.CurrentCulture);
		if (!(value.Text != text))
		{
			return;
		}
		value.Text = text;
		if (f == _filter)
		{
			value.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
			{
				PlaceUnderline(animate: false);
			});
		}
	}

	private void RefreshAges()
	{
		if (_list != null)
		{
			_list.RefreshVisible();
		}
		UpdatePreview(agesOnly: true);
	}

	private void Select(ClipboardItem item, bool scrollIntoView)
	{
		if (item != null)
		{
			_selected = item;
			_list.SetSelected(item);
			if (scrollIntoView)
			{
				_list.EnsureVisible(item);
			}
			UpdatePreview(agesOnly: false);
		}
	}

	private void MoveSelection(int delta)
	{
		if (_visible.Count != 0)
		{
			int num = ((_selected != null) ? _visible.FindIndex((ClipboardItem i) => i.Id == _selected.Id) : (-1));
			int index = Math.Max(0, Math.Min(_visible.Count - 1, (num >= 0) ? (num + delta) : 0));
			Select(_visible[index], scrollIntoView: true);
		}
	}

	private void CopyAndClose(ClipboardItem item)
	{
		CopyItem(item, closeAfter: true);
	}

	internal void CopyItem(ClipboardItem item, bool closeAfter)
	{
		if (item != null && !ClipboardService.Instance.RestoreItem(item, delegate(bool ok)
		{
			SafeBeginInvoke(delegate
			{
				OnCopyResult(ok, closeAfter);
			});
		}))
		{
			OnCopyResult(ok: false, closeAfter);
		}
	}

	private void OnCopyResult(bool ok, bool closeAfter)
	{
		if (!ok)
		{
			if (_copyPill != null)
			{
				_copyPill.ShowFeedback("Couldn't copy", "dismiss");
			}
			if (_toast != null)
			{
				_toast.ShowNotice("Couldn't copy · clipboard busy, try again");
			}
		}
		else
		{
			bool flag = NotchBehaviorConfig.Instance?.IsPinned ?? false;
			if (closeAfter && !flag && CapsuleWindow.Instance != null)
			{
				CapsuleWindow.Instance.RequestCollapse("Clipboard copy");
			}
			else
			{
				ShowCopyFeedback();
			}
		}
	}

	private void TogglePin(ClipboardItem item)
	{
		if (item != null)
		{
			ClipboardService.Instance.TogglePin(item.Id);
		}
	}

	private void DeleteItem(ClipboardItem item)
	{
		if (item == null)
		{
			return;
		}
		_toast.Commit();
		int num = _visible.FindIndex((ClipboardItem i) => i.Id == item.Id);
		ClipboardItem selected = null;
		if (num >= 0 && _visible.Count > 1)
		{
			selected = _visible[(num + 1 < _visible.Count) ? (num + 1) : (num - 1)];
		}
		if (_selected != null && _selected.Id == item.Id)
		{
			_selected = selected;
		}
		ClipboardItem removed = ClipboardService.Instance.RemoveForUndo(item.Id);
		if (removed != null)
		{
			_toast.Show("Item deleted", delegate
			{
				UndoDelete(removed);
			}, delegate
			{
				ClipboardService.Instance.DiscardFiles(removed);
			});
		}
	}

	internal static string RangeLabel(ClipboardClearRange r)
	{
		return r switch
		{
			ClipboardClearRange.Last7Days => "Last 7 days", 
			ClipboardClearRange.Yesterday => "Yesterday", 
			ClipboardClearRange.Today => "Today", 
			_ => "All", 
		};
	}

	internal static string ConfirmText(ClipboardClearRange r, int n)
	{
		string text = n + ((n == 1) ? " item" : " items");
		return "Remove " + text + r switch
		{
			ClipboardClearRange.Last7Days => " from the last 7 days", 
			ClipboardClearRange.Yesterday => " from yesterday", 
			ClipboardClearRange.Today => " from today", 
			_ => "", 
		} + "?";
	}

	internal IList<BuddyMenuItem> ClearMenuItems(FrameworkElement anchor)
	{
		DateTime nowLocal = ClearClock();
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		ClipboardClearRange[] clearRanges = ClearRanges;
		foreach (ClipboardClearRange clipboardClearRange in clearRanges)
		{
			ClipboardClearRange range = clipboardClearRange;
			int num = ClipboardService.Instance.CountForClear(range, nowLocal);
			list.Add(new BuddyMenuItem(RangeLabel(range) + "  ·  " + num, null, delegate
			{
				AskClear(anchor, range);
			}));
		}
		return list;
	}

	private void ShowClearMenu(FrameworkElement anchor)
	{
		if (anchor != null && !BuddyDropdown.JustClosed(anchor))
		{
			_toast.Commit();
			BuddyDropdown.Show(anchor, ClearMenuItems(anchor));
		}
	}

	internal FrameworkElement BuildClearConfirm(ClipboardClearRange range, int n)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = 236.0;
		stackPanel.Margin = new Thickness(8.0, 6.0, 8.0, 4.0);
		stackPanel.Tag = "clipboard-clear-confirm";
		StackPanel stackPanel2 = stackPanel;
		TextBlock textBlock = BuddyTheme.Text(ConfirmText(range, n), 13.0, BuddyTheme.Text100, AynType.Medium);
		textBlock.TextWrapping = TextWrapping.Wrap;
		stackPanel2.Children.Add(textBlock);
		TextBlock textBlock2 = BuddyTheme.Text("Pinned items stay. You can undo for a few seconds.", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 8.0);
		stackPanel2.Children.Add(textBlock2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel3.Margin = new Thickness(0.0, 0.0, -10.0, 0.0);
		StackPanel stackPanel4 = stackPanel3;
		Border border = BuddyKit.TextButton("Cancel", BuddyDropdown.Close);
		border.Tag = "clipboard-clear-cancel";
		Border border2 = BuddyKit.TextButton("Clear", delegate
		{
			BuddyDropdown.Close();
			ClearRange(range);
		});
		border2.Tag = "clipboard-clear-confirm-button";
		if (border2.Child is TextBlock textBlock3)
		{
			textBlock3.Foreground = BuddyTheme.Alert;
		}
		stackPanel4.Children.Add(border);
		stackPanel4.Children.Add(border2);
		stackPanel2.Children.Add(stackPanel4);
		return stackPanel2;
	}

	private void AskClear(FrameworkElement anchor, ClipboardClearRange range)
	{
		int num = ClipboardService.Instance.CountForClear(range, ClearClock());
		if (num == 0)
		{
			_toast.ShowNotice("Nothing to clear · pinned items are kept");
			return;
		}
		FrameworkElement card = BuildClearConfirm(range, num);
		card.Dispatcher.BeginInvoke((Action)delegate
		{
			BuddyDropdown.ShowContent(anchor, card, 4.0);
		}, DispatcherPriority.Input);
	}

	internal int ClearRange(ClipboardClearRange range)
	{
		_toast.Commit();
		IList<ClipboardItem> removed = ClipboardService.Instance.RemoveRangeForUndo(range, ClearClock());
		if (removed.Count == 0)
		{
			_toast.ShowNotice("Nothing to clear · pinned items are kept");
			return 0;
		}
		if (_selected != null && removed.Any((ClipboardItem i) => i.Id == _selected.Id))
		{
			_selected = null;
		}
		_toast.Show(removed.Count + ((removed.Count == 1) ? " item cleared" : " items cleared"), delegate
		{
			ClipboardService.Instance.ReinsertRange(removed);
			FocusList();
		}, delegate
		{
			ClipboardService.Instance.DiscardRange(removed);
		});
		return removed.Count;
	}

	private void UndoDelete(ClipboardItem item)
	{
		if (item != null)
		{
			_selected = item;
			ClipboardService.Instance.Reinsert(item);
			if (_list != null)
			{
				_list.EnsureVisible(item);
			}
			FocusList();
		}
	}

	private void ShowRowMenu(FrameworkElement anchor, ClipboardItem item)
	{
		if (anchor == null || item == null)
		{
			return;
		}
		Select(item, scrollIntoView: false);
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		list.Add(new BuddyMenuItem("Copy", "copy", delegate
		{
			CopyItem(item, closeAfter: false);
		}));
		list.Add(new BuddyMenuItem(item.IsPinned ? "Unpin" : "Pin", "pin", delegate
		{
			TogglePin(item);
		}));
		List<BuddyMenuItem> list2 = list;
		if (item.Kind == ClipKind.Link)
		{
			list2.Add(new BuddyMenuItem("Open link", "open", delegate
			{
				OpenLink(item);
			}));
		}
		if (item.Kind != ClipKind.Image && item.Kind != ClipKind.Files && DropTool.Available)
		{
			list2.Add(new BuddyMenuItem("Send to phone", "phone_arrow_up", delegate
			{
				DropTool.SendText(item.Text);
			}));
		}
		if (CanSave(item))
		{
			list2.Add(new BuddyMenuItem("Save as…", "save", delegate
			{
				SaveAs(item);
			}));
		}
		list2.Add(new BuddyMenuItem("Delete", "delete", delegate
		{
			DeleteItem(item);
		}));
		BuddyDropdown.Show(anchor, list2, null, 160.0);
	}

	private static bool CanSave(ClipboardItem item)
	{
		ClipKind kind = item.Kind;
		if (kind != ClipKind.Image && kind != 0 && kind != ClipKind.Code)
		{
			return kind == ClipKind.Email;
		}
		return true;
	}

	private void OnActionButton()
	{
		ClipboardItem selected = _selected;
		if (selected != null)
		{
			if (selected.Kind == ClipKind.Link)
			{
				OpenLink(selected);
			}
			else if (CanSave(selected))
			{
				SaveAs(selected);
			}
		}
	}

	private void OpenLink(ClipboardItem item)
	{
		Uri linkUri = item.LinkUri;
		if (linkUri == null)
		{
			_toast.ShowNotice("Couldn't open · this isn't a valid web link");
			return;
		}
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(linkUri.AbsoluteUri);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Open link failed: " + ex.Message);
			_toast.ShowNotice("Couldn't open the link · no app is set for it");
		}
	}

	internal static string SaveFailureText(Exception ex)
	{
		if (ex is UnauthorizedAccessException)
		{
			return "Couldn't save · no permission for that folder";
		}
		if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
		{
			return "Couldn't save · the original file is gone";
		}
		if (ex is PathTooLongException)
		{
			return "Couldn't save · that path is too long";
		}
		if (ex is IOException && ex.HResult == -2147024784)
		{
			return "Couldn't save · the disk is full";
		}
		return "Couldn't save · pick another place";
	}

	private void SaveAs(ClipboardItem item)
	{
		bool flag = item.Kind == ClipKind.Image;
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.FileName = item.GetEffectiveFilename();
		saveFileDialog.Filter = (flag ? "PNG image (*.png)|*.png" : "Text file (*.txt)|*.txt");
		saveFileDialog.DefaultExt = (flag ? ".png" : ".txt");
		saveFileDialog.AddExtension = true;
		saveFileDialog.OverwritePrompt = true;
		SaveFileDialog saveFileDialog2 = saveFileDialog;
		try
		{
			if (saveFileDialog2.ShowDialog() == true)
			{
				if (flag)
				{
					File.Copy(item.ImagePath, saveFileDialog2.FileName, overwrite: true);
				}
				else
				{
					File.WriteAllText(saveFileDialog2.FileName, item.Text ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Clipboard save failed: " + ex.Message);
			_toast.ShowNotice(SaveFailureText(ex));
		}
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		bool flag = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
		bool flag2 = _search != null && Keyboard.FocusedElement == _search.Box;
		if (flag && e.Key == Key.F)
		{
			OpenSearch();
			e.Handled = true;
			return;
		}
		if (flag && e.Key == Key.P)
		{
			if (_selected != null)
			{
				TogglePin(_selected);
			}
			e.Handled = true;
			return;
		}
		if (flag && e.Key == Key.Z && !flag2 && _toast.IsPending)
		{
			_toast.Undo();
			e.Handled = true;
			return;
		}
		switch (e.Key)
		{
		case Key.Down:
			MoveSelection(1);
			e.Handled = true;
			break;
		case Key.Up:
			MoveSelection(-1);
			e.Handled = true;
			break;
		case Key.Next:
			MoveSelection(4);
			e.Handled = true;
			break;
		case Key.Prior:
			MoveSelection(-4);
			e.Handled = true;
			break;
		case Key.Home:
			if (!flag2)
			{
				MoveSelection(-_visible.Count);
				e.Handled = true;
			}
			break;
		case Key.End:
			if (!flag2)
			{
				MoveSelection(_visible.Count);
				e.Handled = true;
			}
			break;
		case Key.Return:
			CopyAndClose(_selected);
			e.Handled = true;
			break;
		case Key.Delete:
			if (!flag2)
			{
				DeleteItem(_selected);
				e.Handled = true;
			}
			break;
		case Key.C:
			if (flag && !flag2 && _selected != null)
			{
				CopyItem(_selected, closeAfter: false);
				e.Handled = true;
			}
			break;
		}
	}

	private void UpdatePreview(bool agesOnly)
	{
		if (_contentHost == null)
		{
			return;
		}
		ClipboardItem selected = _selected;
		if (selected == null)
		{
			_previewId = null;
			_metaLabel.Text = string.Empty;
			_metaActions.Visibility = Visibility.Collapsed;
			_contentHost.Visibility = Visibility.Collapsed;
			_footer.Visibility = Visibility.Collapsed;
			_previewEmpty.Visibility = ((ClipboardService.Instance.Items.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
			return;
		}
		_metaLabel.Text = KindLabel(selected.Kind) + " · " + selected.GetTimeAgo();
		if (agesOnly && selected.Id == _previewId)
		{
			return;
		}
		bool flag = selected.Id == _previewId;
		if (!flag)
		{
			ResetCopyFeedback();
		}
		if (flag && selected.TimestampTicks == _previewTicks && selected.IsPinned == _previewPinned)
		{
			return;
		}
		_previewId = selected.Id;
		_previewTicks = selected.TimestampTicks;
		_previewPinned = selected.IsPinned;
		_metaActions.Visibility = Visibility.Visible;
		_previewEmpty.Visibility = Visibility.Collapsed;
		_contentHost.Visibility = Visibility.Visible;
		_footer.Visibility = Visibility.Visible;
		_pinIcon.Content = FluentIcons.Create("pin", 16.0, BuddyTheme.Text92, selected.IsPinned);
		_pinButton.ToolTip = BuddyTooltip.Create(selected.IsPinned ? "Unpin (Ctrl+P)" : "Pin (Ctrl+P)");
		if (!flag)
		{
			ClipKind kind = selected.Kind;
			_textScroll.Visibility = Visibility.Collapsed;
			_imageHost.Visibility = Visibility.Collapsed;
			_linkPanel.Visibility = Visibility.Collapsed;
			_filesPanel.Visibility = Visibility.Collapsed;
			_imageFrame.Background = Brushes.Transparent;
			switch (kind)
			{
			case ClipKind.Image:
				_imageHost.Visibility = Visibility.Visible;
				FitImageFrame();
				LoadPreviewImage(selected);
				_footerMeta.Text = string.Format(CultureInfo.CurrentCulture, "{0} × {1}", selected.ImageWidth, selected.ImageHeight) + ((selected.FileSizeBytes > 0) ? (" · " + ClipboardItem.FormatFileSize(selected.FileSizeBytes)) : string.Empty);
				break;
			case ClipKind.Link:
				_linkPanel.Visibility = Visibility.Visible;
				_linkHost.Text = selected.LinkHost ?? selected.Text;
				_linkUrl.Text = selected.Text;
				_footerMeta.Text = string.Format(CultureInfo.CurrentCulture, "{0} chars", (selected.Text ?? string.Empty).Length);
				break;
			case ClipKind.Files:
			{
				_filesPanel.Visibility = Visibility.Visible;
				BuildFilesPreview(selected);
				int num = ((selected.FilePaths != null) ? selected.FilePaths.Count : 0);
				_footerMeta.Text = ((num == 1) ? "1 file" : (num.ToString(CultureInfo.CurrentCulture) + " files")) + ((selected.FileSizeBytes > 0) ? (" · " + ClipboardItem.FormatFileSize(selected.FileSizeBytes)) : string.Empty);
				break;
			}
			default:
			{
				_textScroll.Visibility = Visibility.Visible;
				bool flag2 = kind == ClipKind.Code;
				_textBody.FontFamily = (flag2 ? CodeFont : BuddyTheme.Font);
				_textBody.FontSize = (flag2 ? 13.0 : 13.0);
				_textBody.LineHeight = (flag2 ? 18.0 : 19.0);
				_textBody.Text = PreviewText(selected.Text);
				_textScroll.ScrollToTop();
				int wordCount = selected.WordCount;
				int length = (selected.Text ?? string.Empty).Length;
				_footerMeta.Text = (flag2 ? (Compact(Math.Max(1, selected.LineCount)) + " lines · " + Compact(length) + " chars") : (Compact(wordCount) + ((wordCount == 1) ? " word" : " words") + " · " + Compact(length) + " chars"));
				break;
			}
			}
			if (kind != ClipKind.Link && CanSave(selected))
			{
				_actionButton.Visibility = Visibility.Visible;
				_actionIcon.Content = FluentIcons.Create("save", 16.0, BuddyTheme.Text92, filled: false);
				_actionButton.ToolTip = BuddyTooltip.Create((kind == ClipKind.Image) ? "Save as PNG…" : "Save as text…");
			}
			else
			{
				_actionButton.Visibility = Visibility.Collapsed;
			}
		}
	}

	private static string Compact(int n)
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		if (n < 1000)
		{
			return n.ToString(currentCulture);
		}
		if (n < 10000)
		{
			return (Math.Floor((double)n / 100.0) / 10.0).ToString("0.#", currentCulture) + "k";
		}
		return (n / 1000).ToString(currentCulture) + "k";
	}

	private static string PreviewText(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		if (text.Length > 6000)
		{
			return text.Substring(0, 6000) + "…";
		}
		return text;
	}

	private static string KindLabel(ClipKind k)
	{
		return k switch
		{
			ClipKind.Code => "Code", 
			ClipKind.Email => "Email", 
			ClipKind.Link => "Link", 
			ClipKind.Image => "Image", 
			ClipKind.Files => "Files", 
			_ => "Text", 
		};
	}

	private void UpdateTextFade()
	{
		double actualHeight = _textScroll.ActualHeight;
		bool flag = _textScroll.ExtentHeight > _textScroll.ViewportHeight + 1.0;
		bool flag2 = flag && _textScroll.VerticalOffset < _textScroll.ScrollableHeight - 1.0;
		bool flag3 = flag && _textScroll.VerticalOffset > 1.0;
		int num = ((!(actualHeight <= 0.0)) ? ((flag2 ? 1 : 0) | (flag3 ? 2 : 0)) : 0);
		if (num != _fadeState || actualHeight != _fadeH)
		{
			_fadeState = num;
			_fadeH = actualHeight;
			if (num == 0)
			{
				_textScroll.OpacityMask = null;
				return;
			}
			double num2 = Math.Min(0.5, 16.0 / actualHeight);
			GradientStopCollection gradientStopCollection = new GradientStopCollection();
			gradientStopCollection.Add(new GradientStop(flag3 ? Colors.Transparent : Colors.White, 0.0));
			gradientStopCollection.Add(new GradientStop(Colors.White, flag3 ? num2 : 0.0));
			gradientStopCollection.Add(new GradientStop(Colors.White, flag2 ? (1.0 - num2) : 1.0));
			gradientStopCollection.Add(new GradientStop(flag2 ? Colors.Transparent : Colors.White, 1.0));
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.StartPoint = new Point(0.0, 0.0);
			linearGradientBrush.EndPoint = new Point(0.0, 1.0);
			linearGradientBrush.GradientStops = gradientStopCollection;
			LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
			linearGradientBrush2.Freeze();
			_textScroll.OpacityMask = linearGradientBrush2;
		}
	}

	private void FitImageFrame()
	{
		ClipboardItem selected = _selected;
		if (selected != null && selected.Kind == ClipKind.Image)
		{
			double actualWidth = _imageHost.ActualWidth;
			double actualHeight = _imageHost.ActualHeight;
			if (!(actualWidth <= 0.0) && !(actualHeight <= 0.0) && selected.ImageWidth > 0 && selected.ImageHeight > 0)
			{
				double num = Math.Min(actualWidth / (double)selected.ImageWidth, actualHeight / (double)selected.ImageHeight);
				_imageFrame.Width = Math.Max(1.0, Math.Round((double)selected.ImageWidth * num));
				_imageFrame.Height = Math.Max(1.0, Math.Round((double)selected.ImageHeight * num));
			}
		}
	}

	private void LoadPreviewImage(ClipboardItem item)
	{
		if (!ClipboardService.ValidateItemPaths(item))
		{
			return;
		}
		string id = item.Id;
		string path = item.ImagePath;
		double num = 1.0;
		PresentationSource presentationSource = PresentationSource.FromVisual(_imageHost);
		if (presentationSource != null && presentationSource.CompositionTarget != null)
		{
			num = presentationSource.CompositionTarget.TransformToDevice.M11;
		}
		int decodeW = (int)Math.Ceiling(Math.Max(_imageHost.ActualWidth, 266.0) * num);
		if (item.ImageWidth > 0)
		{
			decodeW = Math.Min(decodeW, item.ImageWidth);
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			BitmapSource bmp = ClipboardService.Decode(path, decodeW);
			if (bmp != null)
			{
				SafeBeginInvoke(delegate
				{
					if (_selected != null && !(_selected.Id != id))
					{
						ImageBrush imageBrush = new ImageBrush(bmp)
						{
							Stretch = Stretch.UniformToFill
						};
						RenderOptions.SetBitmapScalingMode(imageBrush, BitmapScalingMode.HighQuality);
						imageBrush.Freeze();
						_imageFrame.Background = imageBrush;
					}
				});
			}
		});
	}

	private void BuildFilesPreview(ClipboardItem item)
	{
		_filesPanel.Children.Clear();
		List<string> list = item.FilePaths ?? new List<string>();
		int num = Math.Min(list.Count, 5);
		for (int i = 0; i < num; i++)
		{
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = Orientation.Horizontal;
			stackPanel.Margin = new Thickness(0.0, (i != 0) ? 10 : 0, 0.0, 0.0);
			StackPanel stackPanel2 = stackPanel;
			FrameworkElement frameworkElement = FluentIcons.Create(Directory.Exists(list[i]) ? "folder" : "document_text", 16.0, BuddyTheme.Text50, filled: false);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			stackPanel2.Children.Add(frameworkElement);
			TextBlock textBlock = BuddyTheme.Text(System.IO.Path.GetFileName(list[i].TrimEnd('\\')), 13.0, BuddyTheme.Text92, FontWeights.Normal);
			textBlock.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
			textBlock.MaxWidth = 230.0;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			stackPanel2.Children.Add(textBlock);
			_filesPanel.Children.Add(stackPanel2);
		}
		if (list.Count > num)
		{
			TextBlock textBlock2 = BuddyTheme.Text(string.Format(CultureInfo.CurrentCulture, "+{0} more", list.Count - num), 11.5, BuddyTheme.Text30, FontWeights.Medium);
			textBlock2.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
			_filesPanel.Children.Add(textBlock2);
		}
	}

	public static string IconFor(ClipKind kind)
	{
		return kind switch
		{
			ClipKind.Code => "code", 
			ClipKind.Email => "mail", 
			ClipKind.Link => "link", 
			ClipKind.Image => "image", 
			ClipKind.Files => "folder", 
			_ => "document_text", 
		};
	}

	public static string TitleFor(ClipboardItem item)
	{
		switch (item.Kind)
		{
		case ClipKind.Image:
			return "Image";
		case ClipKind.Files:
			return item.Text ?? "Files";
		default:
		{
			string text = (item.Text ?? string.Empty).Trim();
			int num = text.IndexOfAny(new char[2] { '\r', '\n' });
			if (num > 0)
			{
				text = text.Substring(0, num).TrimEnd();
			}
			if (text.Length > 300)
			{
				text = text.Substring(0, 300);
			}
			if (text.Length != 0)
			{
				return text;
			}
			return "Empty text";
		}
		}
	}

	public static string MetaFor(ClipboardItem item)
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		string text;
		switch (item.Kind)
		{
		case ClipKind.Image:
			text = ((item.ImageWidth > 0) ? string.Format(currentCulture, "{0} × {1}", item.ImageWidth, item.ImageHeight) : "Image");
			break;
		case ClipKind.Link:
			text = item.LinkHost ?? "Link";
			break;
		case ClipKind.Files:
		{
			int num = ((item.FilePaths != null) ? item.FilePaths.Count : 0);
			text = ((item.FileSizeBytes > 0) ? ClipboardItem.FormatFileSize(item.FileSizeBytes) : ((num == 1) ? "1 file" : (num.ToString(currentCulture) + " files")));
			break;
		}
		case ClipKind.Code:
			text = ((Math.Max(1, item.LineCount) == 1) ? "Code" : string.Format(currentCulture, "{0} lines", item.LineCount));
			break;
		case ClipKind.Email:
			text = "Email";
			break;
		default:
		{
			int wordCount = item.WordCount;
			text = string.Format(currentCulture, "{0} {1}", wordCount, (wordCount == 1) ? "word" : "words");
			break;
		}
		}
		return text + " · " + item.GetTimeAgo();
	}

	private static string SectionFor(ClipboardItem item)
	{
		if (item.IsPinned)
		{
			return "Pinned";
		}
		DateTime date = item.TimestampLocal.Date;
		DateTime today = DateTime.Today;
		if (date >= today)
		{
			return "Today";
		}
		if (date == today.AddDays(-1.0))
		{
			return "Yesterday";
		}
		return "Older";
	}

	private void SafeBeginInvoke(Action a)
	{
		Application current = Application.Current;
		if (current == null || current.Dispatcher == null)
		{
			return;
		}
		current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
		{
			if (!IsInitialized || !ClipboardService.Instance.Operations.AdmissionOpen)
			{
				return;
			}
			try
			{
				a();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("ClipboardTool: " + ex.Message);
			}
		});
	}
}
