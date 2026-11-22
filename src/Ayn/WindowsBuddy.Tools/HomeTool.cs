using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Media;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.Tools.Clock;
using WindowsBuddy.Tools.Notes;
using WindowsBuddy.Tools.ScreenTime;
using WindowsBuddy.Tools.Timer;
using WindowsBuddy.Tools.Weather;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.Tools;

public class HomeTool : IBuddyTool
{
	private sealed class RingGauge : Grid
	{
		private const double MinArc = 0.06;

		private static readonly DependencyProperty FractionProperty = DependencyProperty.Register("Fraction", typeof(double), typeof(RingGauge), new PropertyMetadata(0.0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((RingGauge)d).Draw((double)e.NewValue);
		}));

		private readonly double _size;

		private readonly double _stroke;

		private readonly Path _arc;

		private readonly TextBlock _value;

		private int _lastKey = -1;

		public RingGauge(double size, double stroke, bool showValue, Brush mark = null, Brush track = null)
		{
			_size = size;
			_stroke = stroke;
			base.Width = size;
			base.Height = size;
			BuddyTheme.Crisp(this);
			base.Children.Add(new Ellipse
			{
				Stroke = (track ?? BuddyTheme.Track),
				StrokeThickness = stroke
			});
			_arc = new Path
			{
				Stroke = (mark ?? BuddyTheme.Mark),
				StrokeThickness = stroke,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round
			};
			base.Children.Add(_arc);
			if (showValue)
			{
				_value = BuddyTheme.Text("—", 13.0, BuddyTheme.Text100, AynType.Medium);
				_value.FontWeight = AynType.Semibold;
				_value.HorizontalAlignment = HorizontalAlignment.Center;
				_value.VerticalAlignment = VerticalAlignment.Center;
				Typography.SetNumeralAlignment(_value, FontNumeralAlignment.Tabular);
				base.Children.Add(_value);
			}
		}

		public void SetPercent(double pct, string tooltip)
		{
			if (double.IsNaN(pct) || double.IsInfinity(pct))
			{
				if (_value != null)
				{
					_value.Text = "—";
				}
				base.ToolTip = tooltip;
				SetFraction(0.0);
				return;
			}
			pct = Math.Max(0.0, Math.Min(100.0, pct));
			int num = (int)Math.Round(pct);
			if (_value != null)
			{
				_value.Text = num + "%";
			}
			if (!string.IsNullOrEmpty(tooltip))
			{
				base.ToolTip = tooltip;
			}
			SetFraction((double)num / 100.0);
		}

		public void SetFraction(double f)
		{
			f = Math.Max(0.0, Math.Min(1.0, f));
			int num = (int)Math.Round(f * 1000.0);
			if (num != _lastKey)
			{
				bool flag = _lastKey < 0;
				_lastKey = num;
				if (flag || !CapsuleMotion.AnimationsEnabled)
				{
					BeginAnimation(FractionProperty, null);
					SetValue(FractionProperty, f);
				}
				else
				{
					BeginAnimation(FractionProperty, new DoubleAnimation(f, BuddyTheme.Value)
					{
						EasingFunction = BuddyTheme.EaseOut
					});
				}
			}
		}

		private void Draw(double f)
		{
			if (f <= 0.0005)
			{
				_arc.Data = null;
				return;
			}
			f = Math.Max(f, 0.06);
			double num = (_size - _stroke) / 2.0;
			double num2 = _size / 2.0;
			if (f >= 0.9995)
			{
				EllipseGeometry ellipseGeometry = new EllipseGeometry(new Point(num2, num2), num, num);
				ellipseGeometry.Freeze();
				_arc.Data = ellipseGeometry;
				return;
			}
			double num3 = f * 2.0 * Math.PI;
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = new Point(num2, num2 - num);
			pathFigure.IsClosed = false;
			PathFigure pathFigure2 = pathFigure;
			pathFigure2.Segments.Add(new ArcSegment(new Point(num2 + num * Math.Sin(num3), num2 - num * Math.Cos(num3)), new Size(num, num), 0.0, f > 0.5, SweepDirection.Clockwise, isStroked: true));
			PathGeometry pathGeometry = new PathGeometry();
			pathGeometry.Figures.Add(pathFigure2);
			pathGeometry.Freeze();
			_arc.Data = pathGeometry;
		}
	}

	private const double GridW = 728.0;

	private const double GridH = 242.0;

	private const double RowH = 116.0;

	private const double ContentH = 90.0;

	private const double HeroTop = 18.0;

	private const double SubTop = 52.0;

	private const double TitleTop = 26.0;

	private const double BarW = 3.0;

	private const double BarGap = 5.0;

	private const double BarMaxH = 16.0;

	private const double BarMinH = 3.0;

	private const double DayBarW = 10.0;

	private const double DayBarGap = 22.0;

	private const double DayBarMaxH = 20.0;

	private const double DayBarMinH = 3.0;

	private const double ClipRowH = 30.0;

	private const double ClipRowGap = 8.0;

	private const double ClipThumb = 22.0;

	private const double ClipTimeW = 30.0;

	private const double ClipCopy = 14.0;

	private const double PlaySize = 44.0;

	private const double PlayIcon = 16.0;

	private const double WxIcon = 36.0;

	private const double AppIcon = 14.0;

	private static readonly TimeSpan CopiedHold = TimeSpan.FromSeconds(1.0);

	private Grid _root;

	private readonly List<FrameworkElement> _cards = new List<FrameworkElement>();

	private readonly List<Grid> _editCards = new List<Grid>();

	private Border _addWidget;

	private BuddyUndoToast _widgetUndo;

	private readonly List<Border> _emptySlots = new List<Border>();

	private bool _snapshot;

	private bool _galleryCovered;

	private bool _attached;

	private DispatcherTimer _minuteTicker;

	private DateTime _lastDate = DateTime.Today;

	private TextBlock _stHero;

	private TextBlock _stSub;

	private Border _stIcon;

	private string _stIconApp;

	private readonly Border[] _bars = new Border[7];

	private readonly int[] _barSec = new int[7];

	private int _stLastTotal = -1;

	private int _barMax = -1;

	private string _stLastTop;

	private RingGauge _cpu;

	private RingGauge _ram;

	private RingGauge _disk;

	private TextBlock _wxTemp;

	private TextBlock _wxSub;

	private TextBlock _wxHiLo;

	private ContentControl _wxIcon;

	private TextBlock _focusHero;

	private TextBlock _focusSub;

	private RingGauge _focusRing;

	private ContentControl _focusGlyph;

	private bool _focusRunning;

	private int _focusRem = -1;

	private int _focusTotal = -1;

	private string _focusState;

	private Grid _clipHost;

	private FrameworkElement _clipEmpty;

	private string _clipSig;

	private DispatcherTimer _copyFeedback;

	private ContentControl _copyFeedbackIcon;

	private readonly Dictionary<string, ContentControl> _clipIcons = new Dictionary<string, ContentControl>();

	private TextBlock _noteTitle;

	private TextBlock _notePreview;

	private TextBlock _noteEdited;

	private FrameworkElement _noteField;

	private NoteItem _note;

	private FrameworkElement _notesCard;

	private FrameworkElement _musicCard;

	private MediaControlsView _musicView;

	private DispatcherTimer _mediaTicker;

	private readonly Dictionary<FrameworkElement, string> _modules = new Dictionary<FrameworkElement, string>();

	private EventHandler _timerHandler;

	private Grid _emptyState;

	private StackPanel _emptyShortcuts;

	public string Id => "home";

	public string DisplayName => "Home";

	public ToolCategory Category => ToolCategory.Core;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	private void LayoutWidgets()
	{
		if (_root == null)
		{
			return;
		}
		string[] slots = NotchEditing.Slots;
		Grid[] array = _editCards.Where((Grid c) => !NotchEditing.LegacyWidgets.Contains((string)c.Tag) && !slots.Contains((string)c.Tag)).ToArray();
		foreach (Grid grid in array)
		{
			foreach (HomeWidgetCard item in grid.Children.OfType<HomeWidgetCard>())
			{
				item.SetActive(value: false);
			}
			_root.Children.Remove(grid);
			_editCards.Remove(grid);
		}
		foreach (string placed in slots.Where((string id) => id != "" && !NotchEditing.LegacyWidgets.Contains(id)))
		{
			if (!_editCards.Any((Grid c) => object.Equals(c.Tag, placed)))
			{
				Grid wrapper = new Grid
				{
					Tag = placed,
					Focusable = true,
					ClipToBounds = false
				};
				wrapper.Children.Add(new HomeWidgetCard(placed));
				wrapper.Children.Add(new Border
				{
					Tag = "edit-highlight",
					CornerRadius = new CornerRadius(16.0),
					Background = ThemeTokens.Selection,
					Opacity = 0.25,
					IsHitTestVisible = false
				});
				Border border = EditGesture.Badge(HomeWidgetCard.Name(placed), delegate
				{
					RemoveWidget(placed, wrapper);
				});
				border.Tag = "edit-badge";
				wrapper.Children.Add(border);
				_root.Children.Add(wrapper);
				_editCards.Add(wrapper);
			}
		}
		Dictionary<string, Rect> dictionary = _editCards.Where((Grid c) => c.IsVisible && c.ActualWidth > 0.0).ToDictionary((Grid c) => (string)c.Tag, (Grid c) => LayoutInformation.GetLayoutSlot(c));
		bool flag = false;
		foreach (KeyValuePair<FrameworkElement, string> module in _modules)
		{
			module.Key.Visibility = ((!NotchEditing.WidgetVisible(module.Value)) ? Visibility.Collapsed : Visibility.Visible);
		}
		foreach (Grid card in _editCards)
		{
			string id2 = (string)card.Tag;
			int num = Array.IndexOf(slots, id2);
			card.Visibility = ((num < 0) ? Visibility.Collapsed : Visibility.Visible);
			if (num >= 0 && !NotchEditing.LegacyWidgets.Contains(id2) && !card.Children.OfType<HomeWidgetCard>().Any())
			{
				card.Children.RemoveAt(0);
				card.Children.Insert(0, new HomeWidgetCard(id2));
			}
			Border border2 = card.Children.OfType<Border>().FirstOrDefault((Border b) => object.Equals(b.Tag, "edit-highlight"));
			if (border2 != null)
			{
				border2.Visibility = ((!NotchEditing.IsEditing) ? Visibility.Collapsed : Visibility.Visible);
			}
			Border border3 = card.Children.OfType<Border>().LastOrDefault((Border b) => object.Equals(b.Tag, "edit-badge"));
			if (border3 != null)
			{
				border3.Visibility = ((!NotchEditing.IsEditing) ? Visibility.Collapsed : Visibility.Visible);
			}
			card.Background = Brushes.Transparent;
			if (num >= 0)
			{
				int num2 = num / 3 * 2;
				int num3 = num % 3 * 2;
				flag |= Grid.GetRow(card) != num2 || Grid.GetColumn(card) != num3;
				Grid.SetRow(card, num2);
				Grid.SetColumn(card, num3);
			}
			EditGesture.Attach(card, _root, id2, () => NotchEditing.Slots.Select((string v, int i) => (!(v == "")) ? v : ("slot:" + i)).ToList(), delegate(List<string> ids)
			{
				NotchEditing.SaveSlots(ids.Select((string v) => (!v.StartsWith("slot:")) ? v : ""));
			}, delegate
			{
				RemoveWidget(id2, card);
			}, swap: true);
			card.Children.OfType<HomeWidgetCard>().FirstOrDefault()?.SetActive(_attached && num >= 0 && _root.IsVisible);
		}
		for (int k = 0; k < 6; k++)
		{
			_emptySlots[k].Visibility = ((!NotchEditing.IsEditing || !(slots[k] == "")) ? Visibility.Collapsed : Visibility.Visible);
		}
		_emptyState.Visibility = ((NotchEditing.IsEditing || !slots.All((string id) => id == "")) ? Visibility.Collapsed : Visibility.Visible);
		if (!flag || dictionary.Count == 0)
		{
			return;
		}
		_root.UpdateLayout();
		foreach (Grid item2 in _editCards.Where((Grid c) => c.IsVisible))
		{
			if (dictionary.TryGetValue((string)item2.Tag, out var value))
			{
				Rect layoutSlot = LayoutInformation.GetLayoutSlot(item2);
				if (!(value.TopLeft == layoutSlot.TopLeft) && item2.RenderTransform is TransformGroup transformGroup)
				{
					TranslateTransform translateTransform = transformGroup.Children.OfType<TranslateTransform>().Last();
					translateTransform.X = value.X - layoutSlot.X;
					translateTransform.Y = value.Y - layoutSlot.Y;
					CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.XProperty, 0.0, 0.18, CapsuleMotion.Spring);
					CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.YProperty, 0.0, 0.18, CapsuleMotion.Spring);
				}
			}
		}
	}

	private void RemoveWidget(string id, FrameworkElement card)
	{
		int slot = Array.IndexOf(NotchEditing.Slots, id);
		EditGesture.Remove(card, delegate
		{
			NotchEditing.SetWidgetVisible(id, value: false);
			_widgetUndo.Show("Removed " + HomeWidgetCard.Name(id), delegate
			{
				NotchEditing.PlaceWidget(id, (NotchEditing.Slots[slot] == "") ? slot : (-1));
			}, delegate
			{
			});
		});
	}

	public FrameworkElement WidgetView(string id)
	{
		return _editCards.FirstOrDefault((Grid c) => object.Equals(c.Tag, id));
	}

	public static BitmapSource SnapshotWidget(string id)
	{
		return SnapshotWidget(id, 236.0, 0.0);
	}

	public static BitmapSource SnapshotWidget(string id, double width, double gutter)
	{
		HomeTool homeTool = new HomeTool();
		homeTool._root = new Grid();
		homeTool._snapshot = true;
		FrameworkElement frameworkElement;
		switch (id)
		{
		case "screentime":
			frameworkElement = homeTool.BuildScreenTimeCard();
			homeTool.RefreshScreenTime();
			break;
		case "system":
			frameworkElement = homeTool.BuildSystemCard();
			homeTool.ApplyPerf(SystemMonitorService.GetLivePerf());
			break;
		case "weather":
			frameworkElement = homeTool.BuildWeatherCard();
			homeTool.RefreshWeather();
			break;
		case "timer":
			frameworkElement = homeTool.BuildFocusCard();
			homeTool.SyncFocus(force: true);
			break;
		case "clipboard":
			frameworkElement = homeTool.BuildClipboardCard();
			homeTool.RefreshClipboard(agesOnly: false);
			break;
		case "notes":
			frameworkElement = homeTool.BuildNotesCard();
			homeTool.RefreshNotes();
			break;
		default:
		{
			HomeWidgetCard homeWidgetCard = new HomeWidgetCard(id);
			homeWidgetCard.Refresh();
			frameworkElement = homeWidgetCard;
			break;
		}
		}
		frameworkElement.Width = width;
		frameworkElement.Height = 116.0;
		Border border = new Border();
		border.Width = width + gutter * 2.0;
		border.Height = 116.0 + gutter * 2.0;
		border.Padding = new Thickness(gutter);
		border.Child = frameworkElement;
		Border border2 = border;
		border2.Measure(new Size(border2.Width, border2.Height));
		border2.Arrange(new Rect(0.0, 0.0, border2.Width, border2.Height));
		border2.UpdateLayout();
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Ceiling(border2.Width * 1.5), (int)Math.Ceiling(border2.Height * 1.5), 144.0, 144.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(border2);
		renderTargetBitmap.Freeze();
		return renderTargetBitmap;
	}

	public void AnimateArrival(string id, Point screen)
	{
		FrameworkElement target = WidgetView(id);
		if (target != null && _root != null && CapsuleMotion.AnimationsEnabled)
		{
			_root.UpdateLayout();
			Point point = target.TranslatePoint(default(Point), _root);
			Point point2 = _root.PointFromScreen(screen);
			Image image = new Image
			{
				Source = SnapshotWidget(id),
				Width = target.ActualWidth,
				Height = target.ActualHeight,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				IsHitTestVisible = false
			};
			TransformGroup transformGroup = new TransformGroup();
			ScaleTransform scaleTransform = new ScaleTransform(0.5, 0.5);
			TranslateTransform translateTransform = new TranslateTransform(point2.X - target.ActualWidth / 4.0, point2.Y - target.ActualHeight / 4.0);
			transformGroup.Children.Add(scaleTransform);
			transformGroup.Children.Add(translateTransform);
			image.RenderTransform = transformGroup;
			Grid.SetRowSpan(image, 3);
			Grid.SetColumnSpan(image, 5);
			Panel.SetZIndex(image, 80);
			_root.Children.Add(image);
			target.Opacity = 0.0;
			IEasingFunction spring = CapsuleMotion.Spring;
			CapsuleMotion.AnimateDouble(scaleTransform, ScaleTransform.ScaleXProperty, 1.0, 0.22, spring);
			CapsuleMotion.AnimateDouble(scaleTransform, ScaleTransform.ScaleYProperty, 1.0, 0.22, spring);
			CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.XProperty, point.X, 0.22, spring);
			CapsuleMotion.AnimateDouble(translateTransform, TranslateTransform.YProperty, point.Y, 0.22, spring, delegate
			{
				target.Opacity = 1.0;
				_root.Children.Remove(image);
			});
		}
	}

	public void GalleryPainted()
	{
		if (_galleryCovered && _root != null)
		{
			_root.Opacity = 0.0;
		}
	}

	public void CoverGallery(bool covered)
	{
		_galleryCovered = covered;
		if (!covered && _root != null)
		{
			_root.Opacity = 1.0;
		}
		if (covered)
		{
			Detach();
			if (_minuteTicker != null)
			{
				_minuteTicker.Stop();
			}
			{
				foreach (HomeWidgetCard item in _editCards.SelectMany((Grid c) => c.Children.OfType<HomeWidgetCard>()))
				{
					item.SetActive(value: false);
				}
				return;
			}
		}
		if (State == ToolState.Active)
		{
			Attach();
			RefreshAll();
			LayoutWidgets();
			if (_minuteTicker != null)
			{
				_minuteTicker.Start();
			}
		}
	}

	public HomeTool()
	{
		IsEnabled = true;
		OrderIndex = 0;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_minuteTicker = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(30.0)
		};
		_minuteTicker.Tick += delegate
		{
			OnMinuteTick();
		};
		_timerHandler = delegate
		{
			if (_attached)
			{
				SyncFocus(force: false);
			}
		};
	}

	public void Shutdown()
	{
		NotchEditing.Changed -= LayoutWidgets;
		Detach();
		foreach (HomeWidgetCard item in _editCards.SelectMany((Grid c) => c.Children.OfType<HomeWidgetCard>()))
		{
			item.SetActive(value: false);
		}
		if (_mediaTicker != null)
		{
			_mediaTicker.Stop();
		}
		if (_minuteTicker != null)
		{
			_minuteTicker.Stop();
			_minuteTicker = null;
		}
		if (_copyFeedback != null)
		{
			_copyFeedback.Stop();
			_copyFeedback = null;
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		if (!_galleryCovered)
		{
			Attach();
			RefreshAll();
			if (_minuteTicker != null)
			{
				_minuteTicker.Start();
			}
		}
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		if (_minuteTicker != null)
		{
			_minuteTicker.Stop();
		}
		Detach();
		foreach (HomeWidgetCard item in _editCards.SelectMany((Grid c) => c.Children.OfType<HomeWidgetCard>()))
		{
			item.SetActive(value: false);
		}
		if (_mediaTicker != null)
		{
			_mediaTicker.Stop();
		}
		ResetCopyFeedback();
	}

	private void ResetCopyFeedback()
	{
		if (_copyFeedback != null)
		{
			_copyFeedback.Stop();
		}
		if (_copyFeedbackIcon != null)
		{
			_copyFeedbackIcon.Content = FluentIcons.Create("copy", 14.0, BuddyTheme.Text50, filled: false);
			_copyFeedbackIcon.ToolTip = null;
			if (_copyFeedbackIcon.Tag is Button button)
			{
				button.ToolTip = BuddyTooltip.Create("Copy again");
				AutomationProperties.SetHelpText(button, "");
			}
			_copyFeedbackIcon = null;
		}
	}

	private void Attach()
	{
		if (!_attached)
		{
			_attached = true;
			SystemMonitorService.PerfChanged += OnPerf;
			ScreenTimeService.Instance.UsageUpdated += OnUsage;
			WeatherService.Instance.WeatherUpdated += OnWeather;
			ClockSettings.Instance.SettingsChanged += OnClockFormat;
			ClipboardService.Instance.ItemsChanged += OnClip;
			NotesService.Instance.NotesChanged += OnNotes;
			NotesService.Instance.NoteEdited += OnNoteEdited;
			MediaService.Instance.MediaStateChanged += OnMedia;
			ToolRegistry.Instance.ToolsUpdated += OnModules;
			TimerService instance = TimerService.Instance;
			instance.Tick += _timerHandler;
			instance.TimerStarted += _timerHandler;
			instance.TimerPaused += _timerHandler;
			instance.TimerResumed += _timerHandler;
			instance.TimerCancelled += _timerHandler;
			instance.TimerCompleted += _timerHandler;
			instance.SettingsChanged += _timerHandler;
		}
	}

	private void Detach()
	{
		if (_attached)
		{
			_attached = false;
			SystemMonitorService.PerfChanged -= OnPerf;
			ScreenTimeService.Instance.UsageUpdated -= OnUsage;
			WeatherService.Instance.WeatherUpdated -= OnWeather;
			ClockSettings.Instance.SettingsChanged -= OnClockFormat;
			ClipboardService.Instance.ItemsChanged -= OnClip;
			NotesService.Instance.NotesChanged -= OnNotes;
			NotesService.Instance.NoteEdited -= OnNoteEdited;
			MediaService.Instance.MediaStateChanged -= OnMedia;
			ToolRegistry.Instance.ToolsUpdated -= OnModules;
			TimerService instance = TimerService.Instance;
			instance.Tick -= _timerHandler;
			instance.TimerStarted -= _timerHandler;
			instance.TimerPaused -= _timerHandler;
			instance.TimerResumed -= _timerHandler;
			instance.TimerCancelled -= _timerHandler;
			instance.TimerCompleted -= _timerHandler;
			instance.SettingsChanged -= _timerHandler;
		}
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return FluentIcons.Create("home", size, stroke ?? BuddyTheme.Text100, filled: false);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return new CollapsedBadgeData("Home");
	}

	public static string HeaderDate(DateTime d)
	{
		return d.ToString("dddd", CultureInfo.CurrentCulture) + ", " + ShortDate(d);
	}

	public static string ShortDate(DateTime d)
	{
		string text = CultureInfo.CurrentCulture.DateTimeFormat.MonthDayPattern ?? "d MMM";
		text = text.Replace("MMMM", "MMM").Replace("dd", "d");
		try
		{
			return d.ToString(text, CultureInfo.CurrentCulture);
		}
		catch (FormatException)
		{
			return d.ToString("d MMM", CultureInfo.CurrentCulture);
		}
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			RefreshAll();
			return _root;
		}
		Grid grid = new Grid();
		grid.Width = 728.0;
		grid.Height = 242.0;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		grid.Tag = "NoOuterScroll";
		Grid grid2 = BuddyTheme.Crisp(grid);
		for (int i = 0; i < 3; i++)
		{
			if (i > 0)
			{
				grid2.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(10.0)
				});
			}
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(116.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(10.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(116.0)
		});
		Place(grid2, BuildScreenTimeCard(), 0, 0);
		Place(grid2, BuildSystemCard(), 0, 2);
		Place(grid2, BuildWeatherCard(), 0, 4);
		Place(grid2, BuildFocusCard(), 2, 0);
		Place(grid2, BuildClipboardCard(), 2, 2);
		_notesCard = BuildNotesCard();
		Place(grid2, _notesCard, 2, 4);
		_emptyState = BuildEmptyState();
		grid2.Children.Add(_emptyState);
		_root = grid2;
		_ = NotchEditing.Slots;
		for (int j = 0; j < _cards.Count; j++)
		{
			FrameworkElement element = _cards[j];
			Grid.GetRow(element);
			Grid.GetColumn(element);
			grid2.Children.Remove(element);
			string id = NotchEditing.Widgets[j];
			Grid editCard = new Grid
			{
				Tag = id,
				Focusable = true,
				ClipToBounds = false
			};
			editCard.Children.Add(element);
			editCard.Children.Add(new Border
			{
				Tag = "edit-highlight",
				CornerRadius = new CornerRadius(16.0),
				Background = ThemeTokens.Selection,
				Opacity = 0.25,
				IsHitTestVisible = false
			});
			Border border = EditGesture.Badge(HomeWidgetCard.Name(id), delegate
			{
				RemoveWidget(id, editCard);
			});
			border.Tag = "edit-badge";
			editCard.Children.Add(border);
			grid2.Children.Add(editCard);
			_editCards.Add(editCard);
		}
		for (int k = 0; k < 6; k++)
		{
			int target = k;
			TextBlock textBlock = BuddyTheme.BodyText("+ Add widget");
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			Border border2 = new Border();
			border2.Tag = "slot:" + k;
			border2.Background = ThemeTokens.Surface2;
			border2.CornerRadius = new CornerRadius(14.0);
			border2.Cursor = Cursors.Hand;
			border2.Child = textBlock;
			Border border3 = border2;
			AutomationProperties.SetName(border3, "Add widget slot " + (k + 1));
			border3.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
			};
			border3.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				NotchEditing.AddWidget(target);
				e.Handled = true;
			};
			Grid.SetRow(border3, k / 3 * 2);
			Grid.SetColumn(border3, k % 3 * 2);
			grid2.Children.Add(border3);
			_emptySlots.Add(border3);
		}
		_addWidget = _emptySlots[0];
		_widgetUndo = new BuddyUndoToast(5.0)
		{
			BorderThickness = new Thickness(0.0)
		};
		Grid.SetRow(_widgetUndo, 2);
		Grid.SetColumnSpan(_widgetUndo, 5);
		Panel.SetZIndex(_widgetUndo, 40);
		grid2.Children.Add(_widgetUndo);
		NotchEditing.Changed += LayoutWidgets;
		RefreshAll();
		LayoutWidgets();
		return grid2;
	}

	private void Place(Grid root, FrameworkElement card, int row, int col)
	{
		Grid.SetRow(card, row);
		Grid.SetColumn(card, col);
		root.Children.Add(card);
		_cards.Add(card);
	}

	private Grid NewCard(string icon, string label, string widget, string routeTo, out BuddyThemeCard card)
	{
		WidgetCard widgetCard = (WidgetCard)(card = new WidgetCard(icon, label, widget));
		_modules[card] = routeTo ?? ((widget == "notes") ? "notes" : widget);
		string target = routeTo;
		if (target != null)
		{
			card.Click += delegate
			{
				ToolRouter.Instance.RouteTo(target);
			};
		}
		return widgetCard.Body;
	}

	private static T At<T>(T el, double top) where T : FrameworkElement
	{
		el.VerticalAlignment = VerticalAlignment.Top;
		el.Margin = new Thickness(el.Margin.Left, top, el.Margin.Right, 0.0);
		return el;
	}

	private static T Bottom<T>(T el) where T : FrameworkElement
	{
		el.VerticalAlignment = VerticalAlignment.Bottom;
		return el;
	}

	private static TextBlock Hero(string text)
	{
		TextBlock textBlock = WidgetCard.Value(text);
		textBlock.HorizontalAlignment = HorizontalAlignment.Left;
		return textBlock;
	}

	private static TextBlock HeroSub(string text)
	{
		return WidgetCard.Secondary(text);
	}

	private static TextBlock Meta(string text)
	{
		TextBlock textBlock = BuddyTheme.Text(text, 11.5, BuddyTheme.Text50, FontWeights.Medium);
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		return textBlock;
	}

	private FrameworkElement BuildScreenTimeCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard("data_bar_vertical", "Screen Time", "screen", "screentime", out card);
		_stHero = At(Hero("0m"), 18.0);
		grid.Children.Add(_stHero);
		Grid grid2 = At(new Grid(), 52.0);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_stIcon = new Border
		{
			Width = 14.0,
			Height = 14.0,
			CornerRadius = new CornerRadius(3.0),
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 1.0, 6.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		grid2.Children.Add(_stIcon);
		_stSub = HeroSub("No activity yet");
		Grid.SetColumn(_stSub, 1);
		grid2.Children.Add(_stSub);
		grid.Children.Add(grid2);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.Height = 20.0;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
		StackPanel stackPanel2 = Bottom(stackPanel);
		for (int i = 0; i < 7; i++)
		{
			_bars[i] = new Border
			{
				Width = 10.0,
				Height = 3.0,
				CornerRadius = new CornerRadius(3.0),
				Background = BuddyTheme.BarPast,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0.0, 0.0, (i < 6) ? 22.0 : 0.0, 0.0)
			};
			_barSec[i] = -1;
			stackPanel2.Children.Add(_bars[i]);
		}
		grid.Children.Add(stackPanel2);
		return card;
	}

	private FrameworkElement BuildSystemCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard("settings", "System", "system", "system", out card);
		Grid grid2 = new Grid();
		grid2.VerticalAlignment = VerticalAlignment.Bottom;
		Grid grid3 = grid2;
		for (int i = 0; i < 3; i++)
		{
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		_cpu = new RingGauge(52.0, 5.0, showValue: true, ThemeTokens.Ring1);
		_ram = new RingGauge(52.0, 5.0, showValue: true, ThemeTokens.Ring2);
		_disk = new RingGauge(52.0, 5.0, showValue: true, ThemeTokens.Ring3);
		AddGauge(grid3, _cpu, "CPU", 0);
		AddGauge(grid3, _ram, "RAM", 1);
		AddGauge(grid3, _disk, "DISK", 2);
		grid.Children.Add(grid3);
		return card;
	}

	private static void AddGauge(Grid row, RingGauge gauge, string label, int col)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(gauge);
		TextBlock textBlock = Meta(label);
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		Grid.SetColumn(stackPanel2, col);
		row.Children.Add(stackPanel2);
	}

	private FrameworkElement BuildWeatherCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard("weather_partly_cloudy_day", "Weather", "weather", "weather", out card);
		_wxTemp = At(Hero("—"), 18.0);
		grid.Children.Add(_wxTemp);
		_wxSub = At(HeroSub("Weather unavailable"), 52.0);
		grid.Children.Add(_wxSub);
		_wxIcon = At(new ContentControl
		{
			Width = 36.0,
			Height = 36.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			IsHitTestVisible = false
		}, 22.0);
		grid.Children.Add(_wxIcon);
		_wxHiLo = Bottom(Meta(""));
		grid.Children.Add(_wxHiLo);
		return card;
	}

	private FrameworkElement BuildFocusCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard("timer", "Focus", "focus", "timer", out card);
		_focusHero = At(Hero("25:00"), 18.0);
		grid.Children.Add(_focusHero);
		_focusSub = At(HeroSub("Ready"), 52.0);
		grid.Children.Add(_focusSub);
		Grid grid2 = new Grid();
		grid2.Width = 44.0;
		grid2.Height = 44.0;
		grid2.HorizontalAlignment = HorizontalAlignment.Right;
		grid2.VerticalAlignment = VerticalAlignment.Bottom;
		grid2.Cursor = Cursors.Hand;
		grid2.Background = Brushes.Transparent;
		Grid grid3 = grid2;
		Ellipse face = new Ellipse
		{
			Fill = ThemeTokens.Selection
		};
		grid3.Children.Add(face);
		_focusRing = new RingGauge(44.0, 2.0, showValue: false, ThemeTokens.PlayInk, Brushes.Transparent)
		{
			Visibility = Visibility.Collapsed
		};
		grid3.Children.Add(_focusRing);
		_focusGlyph = new ContentControl
		{
			Width = 16.0,
			Height = 16.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		grid3.Children.Add(_focusGlyph);
		grid3.MouseEnter += delegate
		{
			face.Fill = ThemeTokens.Surface3;
		};
		grid3.MouseLeave += delegate
		{
			face.Fill = ThemeTokens.Selection;
		};
		Button button = BuddyKit.AccessibleButton(grid3, "Start or pause focus", ToggleFocus, 22.0);
		double width = (button.Height = 44.0);
		button.Width = width;
		button.HorizontalAlignment = HorizontalAlignment.Right;
		button.VerticalAlignment = VerticalAlignment.Bottom;
		button.ToolTip = BuddyTooltip.Create("Start or pause focus");
		grid.Children.Add(button);
		return card;
	}

	private void ToggleFocus()
	{
		TimerService instance = TimerService.Instance;
		if (instance.PomoIsRunning)
		{
			if (instance.PomoIsPaused)
			{
				instance.ResumePomodoro();
			}
			else
			{
				instance.PausePomodoro();
			}
		}
		else if (instance.CountIsRunning)
		{
			if (instance.CountIsPaused)
			{
				instance.ResumeCountdown();
			}
			else
			{
				instance.PauseCountdown();
			}
		}
		else
		{
			instance.StartPomodoro();
		}
		SyncFocus(force: true);
	}

	private FrameworkElement BuildClipboardCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard("document_text", "Clipboard", "clipboard", "clipboard", out card);
		_clipHost = Bottom(new Grid
		{
			Height = 68.0
		});
		_clipHost.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(30.0)
		});
		_clipHost.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(8.0)
		});
		_clipHost.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(30.0)
		});
		TextBlock element = At(WidgetCard.EmptyTitle("Nothing copied yet"), 26.0);
		TextBlock element2 = At(WidgetCard.EmptyHint("Copied items appear here"), 48.0);
		Grid grid2 = new Grid();
		grid2.Visibility = Visibility.Collapsed;
		Grid grid3 = grid2;
		grid3.Children.Add(element);
		grid3.Children.Add(element2);
		_clipEmpty = grid3;
		grid.Children.Add(grid3);
		grid.Children.Add(_clipHost);
		return card;
	}

	private Button MakeClipRow(ClipboardItem item)
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(22.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(30.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(14.0)
		});
		FrameworkElement frameworkElement = ClipLeading(item);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid.Children.Add(frameworkElement);
		TextBlock textBlock = BuddyTheme.Text(ClipDisplay(item), 13.0, BuddyTheme.Text92, FontWeights.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(10.0, 0.0, 8.0, 0.0);
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		Grid.SetColumn(textBlock, 1);
		grid.Children.Add(textBlock);
		TextBlock textBlock2 = BuddyTheme.Text(FmtAge(item.TimestampTicks), 11.5, BuddyTheme.Text30, FontWeights.Normal);
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		textBlock2.Tag = item.TimestampTicks;
		Typography.SetNumeralAlignment(textBlock2, FontNumeralAlignment.Tabular);
		Grid.SetColumn(textBlock2, 2);
		grid.Children.Add(textBlock2);
		ContentControl copy = new ContentControl
		{
			Width = 14.0,
			Height = 14.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			Content = FluentIcons.Create("copy", 14.0, BuddyTheme.Text50, filled: false)
		};
		Grid.SetColumn(copy, 3);
		grid.Children.Add(copy);
		_clipIcons[item.Id] = copy;
		Border border = new Border();
		border.CornerRadius = new CornerRadius(8.0);
		border.Padding = new Thickness(6.0, 0.0, 6.0, 0.0);
		border.Margin = new Thickness(-6.0, 0.0, -6.0, 0.0);
		border.Child = grid;
		Border border2 = border;
		BuddyTheme.SoftHover(border2);
		Button button = BuddyKit.AccessibleButton(border2, "Copy " + ClipDisplay(item), delegate
		{
			ClipboardService.Instance.RestoreItem(item, delegate(bool ok)
			{
				if (_attached)
				{
					RefreshClipboard(agesOnly: false);
					if (_clipIcons.TryGetValue(item.Id, out var value))
					{
						ShowCopied(value, ok);
					}
				}
			});
		});
		copy.Tag = button;
		button.MouseEnter += delegate
		{
			if (copy != _copyFeedbackIcon)
			{
				copy.Content = FluentIcons.Create("copy", 14.0, BuddyTheme.Text92, filled: false);
			}
		};
		button.MouseLeave += delegate
		{
			if (copy != _copyFeedbackIcon)
			{
				copy.Content = FluentIcons.Create("copy", 14.0, BuddyTheme.Text50, filled: false);
			}
		};
		button.ToolTip = BuddyTooltip.Create("Copy again");
		return button;
	}

	private void ShowCopied(ContentControl icon, bool succeeded = true)
	{
		if (_copyFeedbackIcon != null && _copyFeedbackIcon != icon)
		{
			_copyFeedbackIcon.Content = FluentIcons.Create("copy", 14.0, BuddyTheme.Text50, filled: false);
		}
		_copyFeedbackIcon = icon;
		icon.Content = FluentIcons.Create(succeeded ? "checkmark" : "dismiss", 14.0, succeeded ? BuddyTheme.Text100 : BuddyTheme.Text50, filled: false);
		icon.ToolTip = (succeeded ? "Copied" : "Couldn't copy. Try again.");
		if (icon.Tag is Button button)
		{
			button.ToolTip = (succeeded ? "Copy again" : "Couldn't copy. Try again.");
			AutomationProperties.SetHelpText(button, succeeded ? "Copied" : "Copy failed. Try again.");
		}
		if (_copyFeedback == null)
		{
			_copyFeedback = new DispatcherTimer
			{
				Interval = CopiedHold
			};
			_copyFeedback.Tick += delegate
			{
				ResetCopyFeedback();
			};
		}
		_copyFeedback.Stop();
		_copyFeedback.Start();
	}

	private static FrameworkElement ClipLeading(ClipboardItem item)
	{
		Border b = new Border
		{
			Width = 22.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(5.0),
			Background = BuddyTheme.Control
		};
		string text = item.ItemType ?? "text";
		if (text == "image" && !string.IsNullOrEmpty(item.ImagePath))
		{
			ImageSource thumbnail = ClipboardService.Instance.GetThumbnail(item, delegate(ImageSource loaded)
			{
				b.Background = new ImageBrush(loaded)
				{
					Stretch = Stretch.UniformToFill
				};
			});
			if (thumbnail != null)
			{
				b.Background = new ImageBrush(thumbnail)
				{
					Stretch = Stretch.UniformToFill
				};
			}
			return b;
		}
		FrameworkElement frameworkElement = FluentIcons.Create(ClipboardTool.IconFor(item.Kind), 12.0, BuddyTheme.Text50, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		b.Child = frameworkElement;
		return b;
	}

	private FrameworkElement BuildNotesCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard("note", "Notes", "notes", null, out card);
		card.Click += delegate
		{
			if (_note != null)
			{
				NotesTool.RequestOpen(_note.Id);
			}
			ToolRouter.Instance.RouteTo("notes");
		};
		_noteTitle = At(WidgetCard.EmptyTitle(""), 26.0);
		grid.Children.Add(_noteTitle);
		_notePreview = At(WidgetCard.Secondary(""), 48.0);
		_notePreview.TextWrapping = TextWrapping.NoWrap;
		_notePreview.TextTrimming = TextTrimming.CharacterEllipsis;
		_notePreview.LineHeight = 15.0;
		_notePreview.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		_notePreview.MaxHeight = 18.0;
		grid.Children.Add(_notePreview);
		_noteField = BuildNoteField();
		grid.Children.Add(_noteField);
		_noteEdited = Bottom(BuddyTheme.Text("", 11.0, BuddyTheme.Text30, FontWeights.Normal));
		_noteEdited.Margin = new Thickness(0.0, 0.0, 32.0, 0.0);
		Typography.SetNumeralAlignment(_noteEdited, FontNumeralAlignment.Tabular);
		grid.Children.Add(_noteEdited);
		Border border = new Border();
		border.Width = 28.0;
		border.Height = 28.0;
		border.Background = Brushes.Transparent;
		border.HorizontalAlignment = HorizontalAlignment.Right;
		border.VerticalAlignment = VerticalAlignment.Bottom;
		border.Margin = new Thickness(0.0, 0.0, -6.0, -6.0);
		border.Cursor = Cursors.Hand;
		border.ToolTip = BuddyTooltip.Create("New note");
		Border border2 = border;
		ContentControl addIcon = new ContentControl
		{
			Width = 16.0,
			Height = 16.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			Content = FluentIcons.Create("add", 16.0, BuddyTheme.Text92, filled: false)
		};
		border2.Child = addIcon;
		border2.MouseEnter += delegate
		{
			addIcon.Content = FluentIcons.Create("add", 16.0, BuddyTheme.Text100, filled: false);
		};
		border2.MouseLeave += delegate
		{
			addIcon.Content = FluentIcons.Create("add", 16.0, BuddyTheme.Text92, filled: false);
		};
		Button button = BuddyKit.AccessibleButton(border2, "New note", delegate
		{
			NotesTool.RequestNewNote();
			ToolRouter.Instance.RouteTo("notes");
		});
		double width = (button.Height = 28.0);
		button.Width = width;
		button.HorizontalAlignment = HorizontalAlignment.Right;
		button.VerticalAlignment = VerticalAlignment.Bottom;
		grid.Children.Add(button);
		return card;
	}

	private FrameworkElement BuildNoteField()
	{
		TextBox box = new TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Foreground = BuddyTheme.Text100,
			CaretBrush = ThemeTokens.Text,
			SelectionBrush = ThemeTokens.Accent,
			FontFamily = BuddyTheme.FontFor(13.0),
			FontSize = 13.0,
			Padding = new Thickness(0.0),
			VerticalAlignment = VerticalAlignment.Center,
			FocusVisualStyle = null,
			MaxLength = 500
		};
		BuddyTheme.ApplyTextMode(box, 13.0);
		AutomationProperties.SetName(box, "Write a note");
		TextBlock hint = BuddyTheme.Text("Write a note…", 13.0, BuddyTheme.Placeholder, FontWeights.Normal);
		hint.VerticalAlignment = VerticalAlignment.Center;
		hint.IsHitTestVisible = false;
		Grid grid = new Grid();
		grid.Children.Add(hint);
		grid.Children.Add(box);
		Border border = new Border();
		border.Height = 30.0;
		border.CornerRadius = new CornerRadius(8.0);
		border.Background = ThemeTokens.Selection;
		border.Padding = new Thickness(10.0, 0.0, 10.0, 0.0);
		border.Margin = new Thickness(0.0, 0.0, 30.0, 0.0);
		border.Cursor = Cursors.IBeam;
		border.Child = grid;
		border.Visibility = Visibility.Collapsed;
		Border border2 = At(border, 48.0);
		box.TextChanged += delegate
		{
			hint.Visibility = ((box.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		};
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			box.Focus();
			e.Handled = true;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		box.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				e.Handled = true;
			}
			else if (e.Key == Key.Escape && box.Text.Length > 0)
			{
				box.Clear();
				e.Handled = true;
			}
			else if (e.Key == Key.Return)
			{
				e.Handled = true;
				string text = box.Text.Trim();
				if (text.Length != 0)
				{
					try
					{
						NotesService.Instance.CreateNote(null, text);
						box.Clear();
						RefreshNotes();
					}
					catch (Exception ex)
					{
						TransactionLogger.Warn("Home: could not create a note (" + ex.GetType().Name + ")");
					}
				}
			}
		};
		return border2;
	}

	private void OnPerf(object sender, SystemPerfData d)
	{
		SafeBeginInvoke(delegate
		{
			ApplyPerf(d);
		});
	}

	private void OnUsage(object sender, EventArgs e)
	{
		RefreshScreenTime();
	}

	private void OnWeather(object sender, EventArgs e)
	{
		SafeBeginInvoke(RefreshWeather);
	}

	private void OnClockFormat(object sender, EventArgs e)
	{
		SafeBeginInvoke(RefreshAll);
	}

	private void OnClip(object sender, EventArgs e)
	{
		SafeBeginInvoke(delegate
		{
			RefreshClipboard(agesOnly: false);
		});
	}

	private void OnNotes(object sender, EventArgs e)
	{
		SafeBeginInvoke(RefreshNotes);
	}

	private void OnNoteEdited(object sender, string id)
	{
		SafeBeginInvoke(RefreshNotes);
	}

	private void OnMedia(object sender, EventArgs e)
	{
		if (_attached)
		{
			RefreshMedia();
		}
	}

	private bool ModuleEnabled(string id)
	{
		if (_snapshot)
		{
			return true;
		}
		if (AppPreferences.Instance.HomeWidgetSlots != null)
		{
			return NotchEditing.WidgetVisible(id);
		}
		return ToolRegistry.Instance.GetTool(id)?.IsEnabled ?? false;
	}

	private void OnModules(object sender, EventArgs e)
	{
		if (_attached)
		{
			RefreshAll();
		}
	}

	private void RefreshModules()
	{
		foreach (KeyValuePair<FrameworkElement, string> module in _modules)
		{
			module.Key.Visibility = ((!NotchEditing.WidgetVisible(module.Value)) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void RefreshMedia()
	{
		if (_musicCard == null || _notesCard == null)
		{
			return;
		}
		MediaViewState view = MediaService.Instance.View;
		bool flag = view != null && view.Present && (view.Playing || (view.PausedSinceUtc.HasValue && DateTime.UtcNow - view.PausedSinceUtc.Value < TimeSpan.FromMinutes(2.0)));
		if (flag)
		{
			_musicView.Refresh(view);
			_musicCard.Visibility = Visibility.Visible;
			_notesCard.Visibility = Visibility.Collapsed;
		}
		else
		{
			_notesCard.Visibility = ((!NotchEditing.WidgetVisible("notes")) ? Visibility.Collapsed : Visibility.Visible);
			_musicCard.Visibility = Visibility.Collapsed;
		}
		UpdateEmptyState();
		if (_mediaTicker == null)
		{
			_mediaTicker = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(1.0)
			};
			_mediaTicker.Tick += delegate
			{
				if (_attached)
				{
					RefreshMedia();
				}
			};
		}
		if (_attached && flag && (!view.Playing || view.Timeline.DurationSeconds > 0.0))
		{
			_mediaTicker.Interval = (view.Playing ? TimeSpan.FromSeconds(1.0) : TimeSpan.FromMilliseconds(Math.Max(1.0, (view.PausedSinceUtc.Value.AddMinutes(2.0) - DateTime.UtcNow).TotalMilliseconds)));
			_mediaTicker.Start();
		}
		else
		{
			_mediaTicker.Stop();
		}
	}

	private void OnMinuteTick()
	{
		if (DateTime.Today != _lastDate)
		{
			_lastDate = DateTime.Today;
			ToolRouter.Instance.UpdateHeaderSubtitle(HeaderDate(_lastDate));
		}
		RefreshClipboard(agesOnly: true);
		RefreshNotesAge();
		RefreshScreenTime();
		RefreshWeather();
	}

	private void RefreshAll()
	{
		if (_root != null)
		{
			RefreshModules();
			RefreshScreenTime();
			try
			{
				ApplyPerf(SystemMonitorService.GetLivePerf());
			}
			catch
			{
			}
			RefreshWeather();
			SyncFocus(force: true);
			RefreshClipboard(agesOnly: false);
			RefreshNotes();
			RefreshMedia();
			UpdateEmptyState();
		}
	}

	private Grid BuildEmptyState()
	{
		Grid grid = new Grid();
		grid.Visibility = Visibility.Collapsed;
		Grid grid2 = grid;
		Grid.SetRow(grid2, 0);
		Grid.SetRowSpan(grid2, 3);
		Grid.SetColumn(grid2, 0);
		Grid.SetColumnSpan(grid2, 5);
		BuddyThemeCard buddyThemeCard = new BuddyThemeCard();
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		TextBlock textBlock = BuddyTheme.Text("Nothing on Home yet", 15.5, BuddyTheme.Text100, FontWeights.Bold);
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		TextBlock textBlock2 = BuddyTheme.Text("Choose Edit to add a widget to Home.", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		textBlock2.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock2.Margin = new Thickness(0.0, 6.0, 0.0, 16.0);
		_emptyShortcuts = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		stackPanel2.Children.Add(textBlock);
		stackPanel2.Children.Add(textBlock2);
		stackPanel2.Children.Add(_emptyShortcuts);
		buddyThemeCard.Child = stackPanel2;
		grid2.Children.Add(buddyThemeCard);
		return grid2;
	}

	private FrameworkElement ShortcutChip(string text, Action click)
	{
		Border border = new Border();
		border.Background = new SolidColorBrush(Color.FromArgb(34, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		border.CornerRadius = new CornerRadius(14.0);
		border.Padding = new Thickness(14.0, 7.0, 14.0, 7.0);
		border.Margin = new Thickness(4.0, 0.0, 4.0, 0.0);
		border.Cursor = Cursors.Hand;
		border.Child = BuddyTheme.Text(text, 13.0, BuddyTheme.Text100, FontWeights.Medium);
		Border border2 = border;
		border2.MouseLeftButtonUp += delegate
		{
			click();
		};
		return border2;
	}

	private void UpdateEmptyState()
	{
		if (_editCards.Count > 0)
		{
			LayoutWidgets();
		}
		else
		{
			if (_emptyState == null)
			{
				return;
			}
			bool flag = _modules.Keys.Any((FrameworkElement c) => c.Visibility == Visibility.Visible) || (_musicCard != null && _musicCard.Visibility == Visibility.Visible);
			_emptyState.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			if (flag || _emptyShortcuts == null)
			{
				return;
			}
			_emptyShortcuts.Children.Clear();
			foreach (IBuddyTool item in ToolRegistry.Instance.EnabledTools.Where((IBuddyTool t) => t.Id != "home").Take(4))
			{
				string id = item.Id;
				_emptyShortcuts.Children.Add(ShortcutChip(item.DisplayName, delegate
				{
					ToolRouter.Instance.RouteTo(id);
				}));
			}
			_emptyShortcuts.Children.Add(ShortcutChip("Modules…", delegate
			{
				CapsuleWindow instance = CapsuleWindow.Instance;
				if (instance != null)
				{
					instance.OpenSettings("modules");
				}
				else
				{
					SettingsLauncher.Open("modules");
				}
			}));
		}
	}

	private void RefreshScreenTime()
	{
		if (_root == null || !ModuleEnabled("screentime"))
		{
			return;
		}
		try
		{
			ScreenTimeService instance = ScreenTimeService.Instance;
			int todayTotalSeconds = instance.TodayTotalSeconds;
			List<KeyValuePair<string, int>> topAppsToday = instance.GetTopAppsToday(1);
			string text = ((topAppsToday.Count > 0) ? (topAppsToday[0].Key + "|" + topAppsToday[0].Value / 60) : null);
			if (todayTotalSeconds / 60 != _stLastTotal / 60 || _stLastTotal < 0 || text != _stLastTop)
			{
				_stLastTotal = todayTotalSeconds;
				_stLastTop = text;
				_stHero.Text = FmtDuration(todayTotalSeconds);
				bool flag = topAppsToday.Count > 0 && todayTotalSeconds >= 60;
				_stSub.Text = (flag ? (topAppsToday[0].Key + " · " + FmtDuration(topAppsToday[0].Value)) : "No activity yet");
				SetTopAppIcon(flag ? topAppsToday[0].Key : null);
			}
			UpdateBars(instance, todayTotalSeconds);
		}
		catch
		{
		}
	}

	private void SetTopAppIcon(string app)
	{
		if (app == _stIconApp)
		{
			return;
		}
		_stIconApp = app;
		if (app == null)
		{
			_stIcon.Visibility = Visibility.Collapsed;
			return;
		}
		_stIcon.Visibility = Visibility.Visible;
		ImageSource icon = AppIconExtractor.Get(app, delegate(ImageSource loaded)
		{
			if (_stIconApp == app)
			{
				ShowAppIcon(loaded);
			}
		});
		ShowAppIcon(icon);
	}

	private void ShowAppIcon(ImageSource icon)
	{
		if (icon != null)
		{
			_stIcon.Child = null;
			_stIcon.Background = new ImageBrush(icon)
			{
				Stretch = Stretch.UniformToFill
			};
		}
		else
		{
			_stIcon.Background = Brushes.Transparent;
			FrameworkElement child = FluentIcons.Create("rectangle_landscape", 14.0, BuddyTheme.Text50, filled: false);
			_stIcon.Child = child;
		}
	}

	private void UpdateBars(ScreenTimeService svc, int todaySec)
	{
		int[] array = new int[7];
		string[] array2 = new string[7];
		IReadOnlyList<DayScreenTimeRecord> pastDays = svc.PastDays;
		for (int i = 0; i < 7; i++)
		{
			DateTime d = DateTime.Today.AddDays(i - 6);
			array2[i] = d.ToString("ddd", CultureInfo.CurrentCulture) + " " + ShortDate(d);
			if (i == 6)
			{
				array[i] = todaySec;
				continue;
			}
			string text = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			if (pastDays == null)
			{
				continue;
			}
			foreach (DayScreenTimeRecord item in pastDays)
			{
				if (item != null && item.DateString == text)
				{
					array[i] = ScreenTimeService.DayTotal(item);
					break;
				}
			}
		}
		int num = array.Max();
		for (int j = 0; j < 7; j++)
		{
			int num2 = array[j] / 60 * 400 + DateTime.Today.DayOfYear;
			if (_barSec[j] != num2 || _barMax != num)
			{
				_barSec[j] = num2;
				Border border = _bars[j];
				border.Height = ((array[j] == 0) ? 3.0 : Math.Round(4.0 + 16.0 * (double)array[j] / (double)Math.Max(1, num)));
				border.Background = ((j == 6 && array[j] > 0) ? ThemeTokens.Accent : BuddyTheme.BarPast);
				border.ToolTip = $"{array2[j]} · {FmtDuration(array[j])}";
			}
		}
		_barMax = num;
	}

	private void ApplyPerf(SystemPerfData d)
	{
		if (d != null && _cpu != null && ModuleEnabled("system"))
		{
			_cpu.SetPercent(d.CpuPercent, d.CpuTooltip);
			_ram.SetPercent(d.RamPercent, d.RamTooltip);
			_disk.SetPercent(d.DiskPercent, d.DiskTooltip);
		}
	}

	private void RefreshWeather()
	{
		if (_root == null || !ModuleEnabled("weather"))
		{
			return;
		}
		try
		{
			WeatherService instance = WeatherService.Instance;
			WeatherData current = instance.Current;
			if (current == null || !current.IsConfigured || !current.HasData)
			{
				_wxTemp.Text = "No forecast yet";
				WidgetCard.ValueStyle(_wxTemp, empty: true);
				_wxSub.Margin = new Thickness(0.0, 48.0, 0.0, 0.0);
				_wxSub.Opacity = 0.55;
				if (current == null || !current.IsConfigured)
				{
					_wxSub.Text = "Set your city";
				}
				else if (instance.FetchState == WeatherFetchState.Offline)
				{
					_wxSub.Text = "Offline, no forecast yet";
				}
				else if (instance.FetchState == WeatherFetchState.Error)
				{
					_wxSub.Text = "Couldn't load weather";
				}
				else
				{
					_wxSub.Text = "Loading weather";
				}
				_wxSub.ToolTip = null;
				_wxIcon.Content = null;
				_wxHiLo.Visibility = Visibility.Collapsed;
				return;
			}
			_wxTemp.Text = instance.Temp(current.Temperature).ToString(CultureInfo.CurrentCulture) + "°";
			WidgetCard.ValueStyle(_wxTemp, empty: false);
			_wxSub.Margin = new Thickness(0.0, 52.0, 0.0, 0.0);
			_wxSub.Opacity = 0.6;
			string text = SafeCity(current.City);
			string text2 = current.Condition ?? string.Empty;
			_wxSub.Text = ((text2.Length > 0 && text.Length > 0) ? (text2 + " · " + text) : ((text2.Length > 0) ? text2 : text));
			string text3 = current.City ?? string.Empty;
			if (instance.FetchState == WeatherFetchState.Offline)
			{
				text3 = ((text3.Length > 0) ? (text3 + " · ") : "") + "Offline, last saved forecast";
			}
			_wxSub.ToolTip = ((text3.Length == 0) ? null : BuddyTooltip.Create(text3));
			_wxIcon.Content = WeatherGlyphs.Create(current.WeatherCode, instance.IsDayNow(), 36.0, ThemeTokens.SunInk);
			List<DailyForecast> list = instance.UpcomingDays(1);
			if (list.Count > 0)
			{
				_wxHiLo.Text = "H " + instance.Temp(list[0].MaxTemp) + "°   L " + instance.Temp(list[0].MinTemp) + "°";
				_wxHiLo.Visibility = Visibility.Visible;
			}
			else
			{
				_wxHiLo.Visibility = Visibility.Collapsed;
			}
			if (instance.FetchState == WeatherFetchState.Error || instance.FetchState == WeatherFetchState.Offline)
			{
				_wxHiLo.Text = "Updated " + ((DateTime.UtcNow.Ticks - current.LastUpdatedTicks < 600000000) ? "just now" : (FmtAge(current.LastUpdatedTicks) + " ago"));
				_wxHiLo.Visibility = Visibility.Visible;
			}
		}
		catch
		{
		}
	}

	private static string SafeCity(string city)
	{
		if (string.IsNullOrWhiteSpace(city))
		{
			return string.Empty;
		}
		string text = city.Split(',')[0].Trim();
		if (!double.TryParse(text, out var _))
		{
			return text;
		}
		return string.Empty;
	}

	private void SyncFocus(bool force)
	{
		if (_root == null || !ModuleEnabled("timer"))
		{
			return;
		}
		try
		{
			TimerService instance = TimerService.Instance;
			bool flag = false;
			int num;
			int num2;
			string text;
			if (instance.PomoIsRunning)
			{
				flag = !instance.PomoIsPaused;
				num = instance.PomoRemainingSeconds;
				num2 = instance.PomoPhaseTotalSeconds;
				text = ((!flag) ? "Paused" : (instance.PomoIsWork ? "Focusing" : "Break"));
			}
			else if (instance.CountIsRunning)
			{
				flag = !instance.CountIsPaused;
				num = instance.CountRemainingSeconds;
				num2 = instance.CountTotalSeconds;
				text = (flag ? "Focusing" : "Paused");
			}
			else
			{
				num2 = ((instance.PomoPhaseTotalSeconds > 0) ? instance.PomoPhaseTotalSeconds : 1500);
				num = num2;
				text = "Ready";
			}
			if (num2 <= 0)
			{
				num2 = Math.Max(1, num);
			}
			if (force || flag != _focusRunning || num != _focusRem || num2 != _focusTotal || !(text == _focusState))
			{
				bool flag2 = force || flag != _focusRunning;
				_focusRunning = flag;
				_focusRem = num;
				_focusTotal = num2;
				_focusState = text;
				_focusHero.Text = $"{num / 60:D2}:{num % 60:D2}";
				_focusSub.Text = text;
				_focusRing.SetFraction(flag ? (1.0 - (double)num / (double)num2) : 0.0);
				if (flag2)
				{
					_focusGlyph.Content = FluentIcons.Create(flag ? "pause" : "play", 16.0, ThemeTokens.PlayInk, filled: true);
				}
			}
		}
		catch
		{
		}
	}

	private void RefreshClipboard(bool agesOnly)
	{
		try
		{
			if (_clipHost == null || !ModuleEnabled("clipboard"))
			{
				return;
			}
			if (agesOnly)
			{
				foreach (Button item in _clipHost.Children.OfType<Button>())
				{
					if (item.Content is Grid grid)
					{
						foreach (TextBlock item2 in grid.Children.OfType<TextBlock>())
						{
							if (item2.Tag is long)
							{
								item2.Text = FmtAge((long)item2.Tag);
							}
						}
					}
				}
				return;
			}
			List<ClipboardItem> list = ClipboardService.Instance.Items.Take(2).ToList();
			string text = string.Join("|", list.Select((ClipboardItem i) => i.Id + ":" + i.TimestampTicks));
			if (text == _clipSig)
			{
				RefreshClipboard(agesOnly: true);
				return;
			}
			_clipSig = text;
			_clipHost.Children.Clear();
			_clipIcons.Clear();
			_copyFeedbackIcon = null;
			if (_clipEmpty != null)
			{
				_clipEmpty.Visibility = ((list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (list.Count != 0)
			{
				for (int j = 0; j < list.Count; j++)
				{
					Button element = MakeClipRow(list[j]);
					Grid.SetRow(element, j * 2);
					_clipHost.Children.Add(element);
				}
			}
		}
		catch
		{
		}
	}

	private static string ClipDisplay(ClipboardItem item)
	{
		if (item.ItemType == "image")
		{
			if (item.ImageWidth <= 0 || item.ImageHeight <= 0)
			{
				return "Image";
			}
			return $"Image · {item.ImageWidth} × {item.ImageHeight}";
		}
		string text = (item.Text ?? string.Empty).Trim();
		int num = text.IndexOfAny(new char[2] { '\r', '\n' });
		if (num > 0)
		{
			text = text.Substring(0, num).Trim();
		}
		if (text.Length != 0)
		{
			return text;
		}
		return "Empty text";
	}

	private void RefreshNotes()
	{
		if (_root == null || !ModuleEnabled("notes"))
		{
			return;
		}
		try
		{
			NoteItem noteItem = null;
			foreach (NoteItem note in NotesService.Instance.Notes)
			{
				if (noteItem == null)
				{
					noteItem = note;
				}
				else if (note.IsPinned != noteItem.IsPinned)
				{
					if (note.IsPinned)
					{
						noteItem = note;
					}
				}
				else if (note.UpdatedAtTicks > noteItem.UpdatedAtTicks)
				{
					noteItem = note;
				}
			}
			_note = noteItem;
			if (noteItem == null)
			{
				_noteTitle.Text = "No notes yet";
				_noteTitle.Foreground = ThemeTokens.Text;
				_noteTitle.Opacity = 0.85;
				_notePreview.Text = string.Empty;
				_notePreview.Visibility = Visibility.Collapsed;
				_noteField.Visibility = Visibility.Visible;
				_noteEdited.Text = string.Empty;
				return;
			}
			_notePreview.Visibility = Visibility.Visible;
			if (_noteField.IsKeyboardFocusWithin)
			{
				Keyboard.ClearFocus();
			}
			_noteField.Visibility = Visibility.Collapsed;
			string text = NotesService.DisplayTitle(noteItem);
			_noteTitle.Text = text ?? "New note";
			_noteTitle.Opacity = 1.0;
			_noteTitle.Foreground = ((text != null) ? BuddyTheme.Text100 : BuddyTheme.Text50);
			_notePreview.Text = NotePreview(noteItem);
			RefreshNotesAge();
		}
		catch
		{
		}
	}

	private static string NotePreview(NoteItem n)
	{
		if (NotesService.HasChecklist(n))
		{
			return NotesService.Preview(n);
		}
		IEnumerable<string> source = from l in (n.Plain ?? string.Empty).Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			select l.Trim() into l
			where l.Length > 0
			select l;
		if (NotesService.ExplicitTitle(n).Length == 0)
		{
			source = source.Skip(1);
		}
		return string.Join(" ", source.Take(6));
	}

	private void RefreshNotesAge()
	{
		if (_note != null && _noteEdited != null)
		{
			TimeSpan timeSpan = DateTime.UtcNow - new DateTime(_note.UpdatedAtTicks, DateTimeKind.Utc);
			string text = ((timeSpan.TotalMinutes < 1.0) ? "just now" : ((timeSpan.TotalHours < 1.0) ? ((int)timeSpan.TotalMinutes + "m ago") : ((!(timeSpan.TotalDays < 1.0)) ? ShortDate(new DateTime(_note.UpdatedAtTicks, DateTimeKind.Utc).ToLocalTime()) : ((int)timeSpan.TotalHours + "h ago"))));
			_noteEdited.Text = "Edited " + text;
		}
	}

	private static string FmtDuration(int sec)
	{
		return ScreenTimeService.FormatDuration(sec);
	}

	private static string FmtHour(int h)
	{
		if (ClockSettings.Uses24Hour)
		{
			return h.ToString("00", CultureInfo.InvariantCulture) + ":00";
		}
		DateTimeFormatInfo dateTimeFormat = CultureInfo.CurrentCulture.DateTimeFormat;
		bool flag = !string.IsNullOrEmpty(dateTimeFormat.AMDesignator) && !string.IsNullOrEmpty(dateTimeFormat.PMDesignator);
		return new DateTime(2000, 1, 1, h, 0, 0).ToString("h tt", flag ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture);
	}

	private static string FmtAge(long ticks)
	{
		if (ticks <= 0)
		{
			return "now";
		}
		TimeSpan timeSpan = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
		if (timeSpan.TotalMinutes < 1.0)
		{
			return "now";
		}
		if (timeSpan.TotalHours < 1.0)
		{
			return (int)timeSpan.TotalMinutes + "m";
		}
		if (timeSpan.TotalDays < 1.0)
		{
			return (int)timeSpan.TotalHours + "h";
		}
		return (int)timeSpan.TotalDays + "d";
	}

	private static void SafeBeginInvoke(Action action)
	{
		try
		{
			Application current = Application.Current;
			if (current != null && !current.Dispatcher.HasShutdownStarted)
			{
				current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
			}
		}
		catch
		{
		}
	}
}
