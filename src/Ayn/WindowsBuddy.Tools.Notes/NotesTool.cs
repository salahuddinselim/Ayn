using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Tools.Clipboard;

namespace WindowsBuddy.Tools.Notes;

public class NotesTool : IBuddyTool, IEscapeAware
{
	private sealed class ToolButton : Border
	{
		public readonly ContentControl Glyph;

		private bool _active;

		public bool IsActive
		{
			get
			{
				return _active;
			}
			set
			{
				if (_active != value)
				{
					_active = value;
					base.Background = (value ? BuddyTheme.RowHover : Brushes.Transparent);
					Glyph.Opacity = ((value || base.IsMouseOver) ? 1.0 : 0.92);
				}
			}
		}

		public ToolButton(string icon, string tip, Action click, double size)
		{
			ToolButton toolButton = this;
			Glyph = new ContentControl
			{
				Width = 18.0,
				Height = 18.0,
				Focusable = false,
				IsTabStop = false,
				IsHitTestVisible = false,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Opacity = 0.92,
				Content = FluentIcons.Create(icon, 18.0, BuddyTheme.Text100, filled: false)
			};
			base.Width = size;
			base.Height = size;
			base.CornerRadius = new CornerRadius(6.0);
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			base.Focusable = false;
			Child = Glyph;
			base.ToolTip = BuddyTooltip.Create(tip);
			MouseEventHandler value = delegate
			{
				Glyph.Opacity = 1.0;
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Glyph.Opacity = (_active ? 1.0 : 0.92);
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
		}
	}

	private sealed class NoteRow : BuddyRow
	{
		private readonly NotesTool _owner;

		private readonly ContentControl _icon;

		private readonly InlineUIContainer _pin;

		private string _iconKey;

		public NoteRow(NotesTool owner)
			: base(18.0, 12.0, 0.0, 12.0, flat: true)
		{
			_owner = owner;
			TitleText.FontWeight = FontWeights.Medium;
			TitleText.FontSize = 13.5;
			_icon = new ContentControl
			{
				Width = 18.0,
				Height = 18.0,
				Focusable = false,
				IsTabStop = false,
				IsHitTestVisible = false,
				VerticalAlignment = VerticalAlignment.Center
			};
			Leading.Children.Add(_icon);
			FrameworkElement frameworkElement = FluentIcons.Create("pin", 12.0, BuddyTheme.Text50, filled: true);
			frameworkElement.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
			_pin = new InlineUIContainer(frameworkElement)
			{
				BaselineAlignment = BaselineAlignment.Center
			};
		}

		protected override void OnBind(object o)
		{
			NoteItem noteItem = (NoteItem)o;
			string text = _owner.TitleFor(noteItem);
			TitleMuted = text == null;
			TitleText.Text = text ?? "New note";
			string text2 = _owner.PreviewFor(noteItem);
			MetaText.Inlines.Clear();
			if (noteItem.IsPinned)
			{
				MetaText.Inlines.Add(_pin);
			}
			MetaText.Inlines.Add(new Run(DateLabel(noteItem) + ((text2.Length > 0) ? (" · " + text2) : string.Empty)));
			string text3 = (_owner.IsChecklist(noteItem) ? "checkbox_checked" : "note");
			if (text3 != _iconKey)
			{
				_iconKey = text3;
				_icon.Content = FluentIcons.Create(text3, 18.0, BuddyTheme.Text50, filled: false);
			}
		}
	}

	private const double GridW = 728.0;

	private const double GridH = 242.0;

	private const double GridTop = 0.0;

	private const double LeftW = 232.0;

	private const double LeftInset = 12.0;

	private const double LeftGap = 12.0;

	private const double EditorLeft = 18.0;

	private const double EditorRight = 14.0;

	private const double TopRowH = 32.0;

	private const double ListTop = 40.0;

	private const double RowH = 46.0;

	private const double HeaderH = 30.0;

	private const double FirstHeaderH = 22.0;

	private const double ToolSize = 24.0;

	private const double ToolGlyph = 18.0;

	private const double RowTitleSize = 13.5;

	private const double TitleInset = 3.0;

	private Grid _root;

	private readonly List<FrameworkElement> _columns = new List<FrameworkElement>();

	private bool _attached;

	private string _query = string.Empty;

	private NoteItem _current;

	private List<NoteItem> _visible = new List<NoteItem>();

	private string _listSignature;

	private bool _loading;

	private bool _liveReady;

	private string _livePlain;

	private string _createdEmptyId;

	private DispatcherTimer _autosave;

	private DispatcherTimer _ageTimer;

	private EventHandler _notesChanged;

	private EventHandler<string> _noteEdited;

	private Grid _leftBody;

	private Grid _topRow;

	private BuddyInlineSearch _search;

	private BuddyVirtualList _list;

	private StackPanel _listEmpty;

	private TextBlock _listEmptyText;

	private TextBlock _recoverHint;

	private Border _recoverButton;

	private Border _listEmptyButton;

	private BuddyUndoToast _toast;

	private Grid _editorBody;

	private TextBlock _edited;

	private ContentControl _pinGlyph;

	private ContentControl _copyGlyph;

	private Border _pinButton;

	private DispatcherTimer _copyFeedback;

	private Border _colorButton;

	private Ellipse _colorDot;

	private TextBox _title;

	private TextBlock _titleHint;

	private NoteEditor _editor;

	private TextBlock _words;

	private TextBlock _editorEmpty;

	private readonly Dictionary<string, ToolButton> _tools = new Dictionary<string, ToolButton>();

	private static NotesTool _instance;

	private static string _pendingOpenId;

	private static bool _pendingNew;

	private bool _lifecyclePaused;

	public string Id => "notes";

	public string DisplayName => "Notes";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public NotesTool()
	{
		IsEnabled = true;
		OrderIndex = 6;
		State = ToolState.Idle;
		_instance = this;
	}

	public static string FormatNoteCount(int count)
	{
		return count.ToString(CultureInfo.CurrentCulture) + ((count == 1) ? " note" : " notes");
	}

	public static void RequestNewNote()
	{
		_pendingNew = true;
		_pendingOpenId = null;
		ApplyPendingSoon();
	}

	public static void RequestOpen(string id)
	{
		_pendingOpenId = id;
		_pendingNew = false;
		ApplyPendingSoon();
	}

	private static void ApplyPendingSoon()
	{
		NotesTool instance = _instance;
		if (instance != null && instance.State == ToolState.Active && instance._root != null)
		{
			instance._root.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(instance.ApplyPending));
		}
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_notesChanged = delegate
		{
			SafeBeginInvoke(OnNotesChanged);
		};
		_noteEdited = delegate(object s, string id)
		{
			SafeBeginInvoke(delegate
			{
				OnNoteEdited(id);
			});
		};
		_autosave = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		_autosave.Tick += delegate
		{
			_autosave.Stop();
			CommitSave();
		};
		NotesService.BeforeFlush = CommitSave;
		NotesService.PauseEditor = delegate
		{
			bool enabled = _autosave != null && _autosave.IsEnabled;
			_lifecyclePaused = true;
			if (_autosave != null)
			{
				_autosave.Stop();
			}
			return delegate
			{
				_lifecyclePaused = false;
				if (enabled && _autosave != null)
				{
					_autosave.Start();
				}
			};
		};
	}

	public void Shutdown()
	{
		_lifecyclePaused = true;
		Detach();
		if (_autosave != null)
		{
			_autosave.Stop();
		}
		if (_ageTimer != null)
		{
			_ageTimer.Stop();
		}
		if (_copyFeedback != null)
		{
			_copyFeedback.Stop();
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
		_instance = this;
		Attach();
		_listSignature = null;
		if (_root != null)
		{
			_root.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Refresh));
		}
		if (_ageTimer == null)
		{
			_ageTimer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(30.0)
			};
			_ageTimer.Tick += delegate
			{
				if (_list != null)
				{
					_list.RefreshVisible();
				}
				UpdateEditedLabel();
			};
		}
		_ageTimer.Start();
		if (_root != null)
		{
			_root.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ApplyPending));
		}
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		CommitSave();
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
	}

	private void Attach()
	{
		if (!_attached && _notesChanged != null)
		{
			_attached = true;
			NotesService.Instance.NotesChanged += _notesChanged;
			NotesService.Instance.NoteEdited += _noteEdited;
		}
	}

	private void Detach()
	{
		if (_attached)
		{
			_attached = false;
			NotesService.Instance.NotesChanged -= _notesChanged;
			NotesService.Instance.NoteEdited -= _noteEdited;
		}
	}

	private void ApplyPending()
	{
		if (State != ToolState.Active || _root == null)
		{
			return;
		}
		if (_pendingNew)
		{
			_pendingNew = false;
			NewNote();
			return;
		}
		string id = _pendingOpenId;
		_pendingOpenId = null;
		if (id != null)
		{
			if (_search != null && _search.IsOpen)
			{
				_search.Close();
			}
			NoteItem noteItem = NotesService.Instance.Notes.FirstOrDefault((NoteItem n) => n.Id == id);
			if (noteItem != null)
			{
				Select(noteItem, scroll: true);
			}
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
		if (_root != null && (_title.IsKeyboardFocusWithin || _editor.HasFocus))
		{
			CommitSave();
			FocusList();
			return true;
		}
		return false;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateNotesIcon(size, stroke);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return new CollapsedBadgeData(string.Format(CultureInfo.CurrentCulture, "Notes: {0}", NotesService.Instance.Notes.Count), "#FFFFFF");
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
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		grid.Tag = "NoOuterScroll";
		Grid grid2 = BuddyTheme.Crisp(grid);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(232.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement frameworkElement = BuildList();
		Grid.SetColumn(frameworkElement, 0);
		grid2.Children.Add(frameworkElement);
		_columns.Add(frameworkElement);
		Rectangle rectangle = new Rectangle();
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.Width = 1.0;
		rectangle.IsHitTestVisible = false;
		Rectangle element = rectangle;
		Grid.SetColumn(element, 1);
		grid2.Children.Add(element);
		FrameworkElement frameworkElement2 = BuildEditor();
		Grid.SetColumn(frameworkElement2, 2);
		grid2.Children.Add(frameworkElement2);
		_columns.Add(frameworkElement2);
		grid2.PreviewKeyDown += OnPreviewKeyDown;
		_root = grid2;
		Refresh();
		return grid2;
	}

	private FrameworkElement BuildList()
	{
		Grid grid = new Grid();
		grid.Focusable = true;
		grid.FocusVisualStyle = null;
		grid.Background = Brushes.Transparent;
		Grid grid2 = (_leftBody = grid);
		_topRow = new Grid
		{
			Height = 32.0,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(12.0, 0.0, 12.0, 0.0)
		};
		TextBlock textBlock = BuddyTheme.Text("All notes", 13.5, BuddyTheme.Text50, FontWeights.Normal);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		_topRow.Children.Add(textBlock);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 0.0, -5.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(new ToolButton("search", "Search (Ctrl+F)", OpenSearch, 28.0));
		stackPanel2.Children.Add(new ToolButton("add", "New note (Ctrl+N)", NewNote, 28.0)
		{
			Margin = new Thickness(4.0, 0.0, 0.0, 0.0)
		});
		_topRow.Children.Add(stackPanel2);
		grid2.Children.Add(_topRow);
		_search = new BuddyInlineSearch(_topRow, "Search notes", 32.0)
		{
			Margin = new Thickness(4.0, 0.0, 12.0, 0.0)
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
		_list = new BuddyVirtualList(() => new NoteRow(this), (object o) => ((NoteItem)o).Id, 46.0, 30.0, 22.0, 12.0)
		{
			Margin = new Thickness(0.0, 40.0, 12.0, 0.0),
			VerticalAlignment = VerticalAlignment.Stretch
		};
		_list.ItemClick += delegate(object o, int clicks)
		{
			NoteItem note = (NoteItem)o;
			Select(note, scroll: true);
			if (clicks >= 2)
			{
				_editor.FocusEnd();
			}
			else
			{
				FocusList();
			}
		};
		_list.ItemContext += delegate(object o, FrameworkElement anchor)
		{
			ShowRowMenu(anchor, (NoteItem)o);
		};
		grid2.Children.Add(_list);
		_listEmpty = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 40.0, 12.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		_listEmptyText = BuddyTheme.SecondaryText("No notes yet");
		_listEmptyText.HorizontalAlignment = HorizontalAlignment.Center;
		_listEmpty.Children.Add(_listEmptyText);
		_listEmptyButton = BuddyKit.TextButton("New note", NewNote);
		_listEmptyButton.HorizontalAlignment = HorizontalAlignment.Center;
		_listEmptyButton.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		_listEmpty.Children.Add(_listEmptyButton);
		_recoverHint = BuddyTheme.Text("Your saved file was kept untouched. Restore it from a backup, then reopen Ayn.", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_recoverHint.TextWrapping = TextWrapping.Wrap;
		_recoverHint.TextAlignment = TextAlignment.Center;
		_recoverHint.MaxWidth = 190.0;
		_recoverHint.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		_recoverHint.HorizontalAlignment = HorizontalAlignment.Center;
		_recoverHint.Visibility = Visibility.Collapsed;
		_listEmpty.Children.Add(_recoverHint);
		_recoverButton = BuddyKit.TextButton("Open folder", OpenNotesFolder);
		_recoverButton.HorizontalAlignment = HorizontalAlignment.Center;
		_recoverButton.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		_recoverButton.Visibility = Visibility.Collapsed;
		_listEmpty.Children.Add(_recoverButton);
		grid2.Children.Add(_listEmpty);
		_toast = new BuddyUndoToast
		{
			Margin = new Thickness(0.0, 0.0, 12.0, 2.0)
		};
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

	private FrameworkElement BuildEditor()
	{
		Grid grid = new Grid();
		grid.Margin = new Thickness(18.0, 0.0, 14.0, 0.0);
		Grid grid2 = grid;
		_editorBody = new Grid();
		_editorBody.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(32.0)
		});
		_editorBody.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_editorBody.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_editorBody.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		_editorBody.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid2.Children.Add(_editorBody);
		_editorBody.Children.Add(BuildToolbar());
		Rectangle rectangle = new Rectangle();
		rectangle.Height = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.Margin = new Thickness(0.0, 7.0, 0.0, 0.0);
		rectangle.IsHitTestVisible = false;
		Rectangle element = rectangle;
		Grid.SetRow(element, 1);
		_editorBody.Children.Add(element);
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(3.0, 12.0, 0.0, 0.0);
		Grid grid4 = grid3;
		_title = new TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			FontFamily = BuddyTheme.DisplayFont,
			FontSize = 20.0,
			FontWeight = AynType.Semibold,
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = false,
			MaxLines = 2,
			FocusVisualStyle = null
		};
		BuddyTheme.ApplyTextMode(_title, 20.0);
		_title.TextChanged += delegate
		{
			_titleHint.Visibility = ((_title.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
			if (!_loading && _current != null)
			{
				TriggerAutosave();
				_list.RefreshItem(_current);
			}
		};
		_title.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || (e.Key == Key.Down && _title.CaretIndex == _title.Text.Length))
			{
				e.Handled = true;
				_editor.FocusStart();
			}
		};
		grid4.Children.Add(_title);
		_titleHint = BuddyTheme.Text("Title", 20.0, BuddyTheme.Placeholder, AynType.Semibold);
		_titleHint.IsHitTestVisible = false;
		_titleHint.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		_titleHint.VerticalAlignment = VerticalAlignment.Top;
		BuddyTheme.ApplyTextMode(_titleHint, 20.0);
		grid4.Children.Add(_titleHint);
		Grid.SetRow(grid4, 2);
		_editorBody.Children.Add(grid4);
		_editor = new NoteEditor
		{
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		_editor.Changed += delegate
		{
			if (!_loading)
			{
				TriggerAutosave();
			}
		};
		_editor.CheckToggled += CommitSave;
		_editor.Updated += OnEditorUpdated;
		_editor.UpAtTop += delegate
		{
			_title.Focus();
			_title.CaretIndex = _title.Text.Length;
		};
		Grid.SetRow(_editor, 3);
		_editorBody.Children.Add(_editor);
		_words = BuddyTheme.Text(string.Empty, 11.5, BuddyTheme.Placeholder, FontWeights.Normal);
		_words.HorizontalAlignment = HorizontalAlignment.Right;
		_words.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		Typography.SetNumeralAlignment(_words, FontNumeralAlignment.Tabular);
		Grid.SetRow(_words, 4);
		_editorBody.Children.Add(_words);
		_editorEmpty = BuddyTheme.SecondaryText("No note selected");
		_editorEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_editorEmpty.VerticalAlignment = VerticalAlignment.Center;
		_editorEmpty.Visibility = Visibility.Collapsed;
		grid2.Children.Add(_editorEmpty);
		return grid2;
	}

	private FrameworkElement BuildToolbar()
	{
		Grid grid = new Grid();
		grid.Height = 32.0;
		grid.VerticalAlignment = VerticalAlignment.Top;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(-3.0, 0.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(Tool("bold", "text_bold", "Bold (Ctrl+B)", delegate
		{
			_editor.ToggleInline(NoteInline.Bold);
		}));
		stackPanel2.Children.Add(Tool("italic", "text_italic", "Italic (Ctrl+I)", delegate
		{
			_editor.ToggleInline(NoteInline.Italic);
		}));
		stackPanel2.Children.Add(Tool("underline", "text_underline", "Underline (Ctrl+U)", delegate
		{
			_editor.ToggleInline(NoteInline.Underline);
		}));
		stackPanel2.Children.Add(Tool("strike", "text_strikethrough", "Strikethrough (Ctrl+Shift+X)", delegate
		{
			_editor.ToggleInline(NoteInline.Strike);
		}));
		stackPanel2.Children.Add(ToolDivider());
		stackPanel2.Children.Add(Tool("h1", "text_header_1", "Heading 1 (# space)", delegate
		{
			_editor.ToggleKind(NoteBlockKind.Heading1);
		}));
		stackPanel2.Children.Add(Tool("h2", "text_header_2", "Heading 2 (## space)", delegate
		{
			_editor.ToggleKind(NoteBlockKind.Heading2);
		}));
		stackPanel2.Children.Add(Tool("text", "text_t", "Text", delegate
		{
			_editor.SetText();
		}));
		stackPanel2.Children.Add(ToolDivider());
		stackPanel2.Children.Add(Tool("bullet", "text_bullet_list", "Bullets (- space)", delegate
		{
			_editor.ToggleKind(NoteBlockKind.Bullet);
		}));
		stackPanel2.Children.Add(Tool("check", "checkbox_checked", "Checklist ([] space)", delegate
		{
			_editor.ToggleKind(NoteBlockKind.Check);
		}));
		stackPanel2.Children.Add(ToolDivider());
		stackPanel2.Children.Add(BuildColorButton());
		grid2.Children.Add(stackPanel2);
		DockPanel dockPanel = new DockPanel();
		dockPanel.LastChildFill = true;
		dockPanel.HorizontalAlignment = HorizontalAlignment.Right;
		dockPanel.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		DockPanel dockPanel2 = dockPanel;
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		ToolButton toolButton = (ToolButton)(_pinButton = new ToolButton("pin", "Pin", delegate
		{
			if (_current != null)
			{
				TogglePin(_current);
			}
		}, 24.0));
		_pinGlyph = toolButton.Glyph;
		ToolButton toolButton2 = new ToolButton("copy", "Copy as text", CopyCurrent, 24.0);
		_copyGlyph = toolButton2.Glyph;
		ToolButton element = new ToolButton("delete", "Delete", delegate
		{
			if (_current != null)
			{
				DeleteNote(_current);
			}
		}, 24.0);
		stackPanel4.Children.Add(toolButton);
		stackPanel4.Children.Add(toolButton2);
		stackPanel4.Children.Add(element);
		DockPanel.SetDock(stackPanel4, Dock.Left);
		dockPanel2.Children.Add(stackPanel4);
		_edited = BuddyTheme.SecondaryText(string.Empty);
		_edited.VerticalAlignment = VerticalAlignment.Center;
		_edited.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		Typography.SetNumeralAlignment(_edited, FontNumeralAlignment.Tabular);
		dockPanel2.Children.Add(_edited);
		Grid.SetColumn(dockPanel2, 1);
		grid2.Children.Add(dockPanel2);
		return grid2;
	}

	private ToolButton Tool(string key, string icon, string tip, Action apply)
	{
		ToolButton toolButton = new ToolButton(icon, tip, delegate
		{
			if (_current != null)
			{
				if (!_editor.HasFocus)
				{
					_editor.FocusEnd();
				}
				apply();
			}
		}, 24.0);
		_tools[key] = toolButton;
		return toolButton;
	}

	private static FrameworkElement ToolDivider()
	{
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Height = 16.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.Margin = new Thickness(3.0, 0.0, 3.0, 0.0);
		rectangle.VerticalAlignment = VerticalAlignment.Center;
		rectangle.IsHitTestVisible = false;
		return rectangle;
	}

	private FrameworkElement BuildColorButton()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.IsHitTestVisible = false;
		StackPanel stackPanel2 = stackPanel;
		_colorDot = new Ellipse
		{
			Width = 14.0,
			Height = 14.0,
			Fill = BuddyTheme.Text100,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(_colorDot);
		ContentControl chevron = new ContentControl
		{
			Width = 12.0,
			Height = 12.0,
			Focusable = false,
			IsTabStop = false,
			Margin = new Thickness(3.0, 0.0, 0.0, 0.0),
			Opacity = 0.5,
			VerticalAlignment = VerticalAlignment.Center,
			Content = FluentIcons.Create("chevron_down", 12.0, BuddyTheme.Text100, filled: false)
		};
		stackPanel2.Children.Add(chevron);
		_colorButton = new Border
		{
			Height = 24.0,
			Padding = new Thickness(5.0, 0.0, 3.0, 0.0),
			CornerRadius = new CornerRadius(6.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = stackPanel2,
			ToolTip = BuddyTooltip.Create("Text colour")
		};
		_colorButton.MouseEnter += delegate
		{
			chevron.Opacity = 1.0;
		};
		_colorButton.MouseLeave += delegate
		{
			chevron.Opacity = 0.5;
		};
		_colorButton.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_colorButton.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			OpenColors();
		};
		BuddyDropdown.Closed += delegate
		{
			if (_colorButton != null)
			{
				_colorButton.Background = Brushes.Transparent;
			}
		};
		return _colorButton;
	}

	private void OpenColors()
	{
		if (_current == null || BuddyDropdown.JustClosed(_colorButton))
		{
			return;
		}
		if (BuddyDropdown.IsOpen && BuddyDropdown.Anchor == _colorButton)
		{
			BuddyDropdown.Close();
			return;
		}
		_editor.RestoreFocus();
		string currentColor = _editor.CurrentColor;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		StackPanel stackPanel2 = stackPanel;
		for (int i = 0; i < NotePalette.Keys.Length; i++)
		{
			string text = NotePalette.Keys[i];
			FrameworkElement frameworkElement = ColorDot(text, NotePalette.Names[i], text == currentColor);
			if (i > 0)
			{
				frameworkElement.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
			}
			stackPanel2.Children.Add(frameworkElement);
		}
		BuddyDropdown.ShowContent(_colorButton, stackPanel2, 8.0);
		_colorButton.Background = BuddyTheme.RowHover;
	}

	private FrameworkElement ColorDot(string key, string name, bool current)
	{
		Grid grid = new Grid();
		grid.Width = 26.0;
		grid.Height = 26.0;
		grid.Background = Brushes.Transparent;
		grid.Cursor = Cursors.Hand;
		grid.ToolTip = BuddyTooltip.Create(name);
		Grid grid2 = grid;
		Ellipse ring = new Ellipse
		{
			Width = 26.0,
			Height = 26.0,
			StrokeThickness = 1.5,
			Stroke = (current ? BuddyTheme.Text100 : Brushes.Transparent),
			IsHitTestVisible = false
		};
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 18.0;
		ellipse.Height = 18.0;
		ellipse.Fill = NotePalette.BrushOf(key) ?? BuddyTheme.Text100;
		ellipse.IsHitTestVisible = false;
		Ellipse element = ellipse;
		grid2.Children.Add(ring);
		grid2.Children.Add(element);
		grid2.MouseEnter += delegate
		{
			if (!current)
			{
				ring.Stroke = BuddyTheme.Text50;
			}
		};
		grid2.MouseLeave += delegate
		{
			if (!current)
			{
				ring.Stroke = Brushes.Transparent;
			}
		};
		grid2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		grid2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			BuddyDropdown.Close();
			_editor.RestoreFocus();
			_editor.ApplyColor(key);
		};
		return grid2;
	}

	private void OnEditorUpdated()
	{
		_liveReady = true;
		int words = _editor.Words;
		_words.Text = words.ToString(CultureInfo.CurrentCulture) + ((words == 1) ? " word" : " words");
		NoteBlockKind currentKind = _editor.CurrentKind;
		SetTool("bold", _editor.IsActive(NoteInline.Bold));
		SetTool("italic", _editor.IsActive(NoteInline.Italic));
		SetTool("underline", _editor.IsActive(NoteInline.Underline));
		SetTool("strike", _editor.IsActive(NoteInline.Strike));
		SetTool("h1", currentKind == NoteBlockKind.Heading1);
		SetTool("h2", currentKind == NoteBlockKind.Heading2);
		SetTool("text", currentKind == NoteBlockKind.Paragraph);
		SetTool("bullet", currentKind == NoteBlockKind.Bullet);
		SetTool("check", currentKind == NoteBlockKind.Check || currentKind == NoteBlockKind.Checked);
		_colorDot.Fill = NotePalette.BrushOf(_editor.CurrentColor) ?? BuddyTheme.Text100;
		string text = _editor.Plain + "\u0001" + _editor.CheckDone + "/" + _editor.CheckTotal;
		if (text != _livePlain && _current != null && !_loading)
		{
			_livePlain = text;
			_list.RefreshItem(_current);
		}
	}

	private void SetTool(string key, bool active)
	{
		if (_tools.TryGetValue(key, out var value))
		{
			value.IsActive = active;
		}
	}

	private void OnNotesChanged()
	{
		if (State == ToolState.Active)
		{
			ToolRouter.Instance.UpdateHeaderSubtitle(FormatNoteCount(NotesService.Instance.Notes.Count));
		}
		Refresh();
	}

	private void OnNoteEdited(string id)
	{
		if (_list != null)
		{
			NoteItem noteItem = _visible.FirstOrDefault((NoteItem n) => n.Id == id);
			if (noteItem != null)
			{
				_list.RefreshItem(noteItem);
			}
			if (_current != null && _current.Id == id)
			{
				UpdateEditedLabel();
			}
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
			IReadOnlyList<NoteItem> notes = NotesService.Instance.Notes;
			List<NoteItem> list = new List<NoteItem>();
			foreach (NoteItem item in notes)
			{
				if (NotesService.Matches(item, _query))
				{
					list.Add(item);
				}
			}
			string text = string.Join("|", list.Select((NoteItem n) => n.Id + ":" + n.UpdatedAtTicks + ":" + (n.IsPinned ? 1 : 0)));
			bool flag = text != _listSignature;
			_listSignature = text;
			_visible = list;
			NoteItem noteItem = ((_current != null) ? notes.FirstOrDefault((NoteItem n) => n.Id == _current.Id) : null);
			if (noteItem == null && list.Count > 0)
			{
				noteItem = list[0];
			}
			if (noteItem == null || _current == null || noteItem.Id != _current.Id)
			{
				LoadNote(noteItem);
			}
			else
			{
				_current = noteItem;
			}
			if (flag)
			{
				_list.SetItems(list, SectionFor, _current);
			}
			else
			{
				_list.SetSelected(_current);
			}
			bool flag2 = list.Count == 0;
			_listEmpty.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			bool hasLoadFailure = NotesService.Instance.HasLoadFailure;
			_listEmptyText.Text = (hasLoadFailure ? "Your notes couldn't be loaded" : ((notes.Count == 0) ? "No notes yet" : "No matches"));
			_listEmptyButton.Visibility = ((notes.Count != 0 || hasLoadFailure) ? Visibility.Collapsed : Visibility.Visible);
			TextBlock recoverHint = _recoverHint;
			Visibility visibility2 = (_recoverButton.Visibility = ((!hasLoadFailure) ? Visibility.Collapsed : Visibility.Visible));
			recoverHint.Visibility = visibility2;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Notes tab refresh failed: " + ex.Message);
		}
	}

	private static string SectionFor(NoteItem n)
	{
		if (n.IsPinned)
		{
			return "Pinned";
		}
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		DateTime today = DateTime.Today;
		int num = (7 + (today.DayOfWeek - currentCulture.DateTimeFormat.FirstDayOfWeek)) % 7;
		DateTime dateTime = today.AddDays(-num);
		if (!(NotesService.EditedLocal(n).Date >= dateTime))
		{
			return "Earlier";
		}
		return "This week";
	}

	private static string DateLabel(NoteItem n)
	{
		DateTime dateTime = NotesService.EditedLocal(n);
		return dateTime.ToString((dateTime.Year == DateTime.Now.Year) ? "MMM d" : "MMM d, yyyy", CultureInfo.CurrentCulture);
	}

	private bool IsLive(NoteItem n)
	{
		if (_current != null && n.Id == _current.Id && _title != null)
		{
			return !_loading;
		}
		return false;
	}

	private string TitleFor(NoteItem n)
	{
		if (!IsLive(n))
		{
			return NotesService.DisplayTitle(n);
		}
		string text = _title.Text.Trim();
		if (text.Length > 0)
		{
			return text;
		}
		return NoteDocument.FirstLine(_liveReady ? _editor.Plain : n.Plain, 0);
	}

	private string PreviewFor(NoteItem n)
	{
		if (!IsLive(n) || !_liveReady)
		{
			return NotesService.Preview(n);
		}
		return NotesService.Preview(_title.Text.Trim().Length > 0, _editor.Plain, _editor.CheckDone, _editor.CheckTotal);
	}

	private bool IsChecklist(NoteItem n)
	{
		if (!IsLive(n) || !_liveReady)
		{
			return NotesService.HasChecklist(n);
		}
		return _editor.CheckTotal > 0;
	}

	private void Select(NoteItem note, bool scroll)
	{
		if (note != null)
		{
			if (_current == null || note.Id != _current.Id)
			{
				LoadNote(note);
			}
			_list.SetSelected(note);
			if (scroll)
			{
				_list.EnsureVisible(note);
			}
		}
	}

	private void LoadNote(NoteItem note)
	{
		CommitSave();
		NoteItem current = _current;
		if (current != null && current.Id == _createdEmptyId && (note == null || note.Id != current.Id))
		{
			_createdEmptyId = null;
			if (NotesService.IsEmpty(current))
			{
				_current = note;
				NotesService.Instance.Discard(current.Id);
			}
		}
		_current = note;
		_liveReady = false;
		_livePlain = null;
		_loading = true;
		try
		{
			if (_copyFeedback != null)
			{
				_copyFeedback.Stop();
			}
			_copyGlyph.Content = FluentIcons.Create("copy", 18.0, BuddyTheme.Text100, filled: false);
			bool flag = note != null;
			_editorBody.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			_editorEmpty.Text = (NotesService.Instance.HasLoadFailure ? "Notes are unavailable until the file is restored" : ((NotesService.Instance.Notes.Count == 0) ? "Create a note to start writing" : "No note selected"));
			_editorEmpty.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			if (flag)
			{
				_title.Text = NotesService.ExplicitTitle(note);
				_titleHint.Visibility = ((_title.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
				_editor.Load(note.Blocks);
				UpdatePinIcon();
				UpdateEditedLabel();
			}
		}
		finally
		{
			_loading = false;
		}
	}

	private void UpdatePinIcon()
	{
		if (_current != null)
		{
			_pinGlyph.Content = FluentIcons.Create("pin", 18.0, BuddyTheme.Text100, _current.IsPinned);
			_pinButton.ToolTip = BuddyTooltip.Create(_current.IsPinned ? "Unpin" : "Pin");
		}
	}

	private void UpdateEditedLabel()
	{
		if (_current != null && _edited != null)
		{
			DateTime dateTime = NotesService.EditedLocal(_current);
			_edited.Text = "Edited " + ((dateTime.Date == DateTime.Today) ? dateTime.ToString("h:mm tt", CultureInfo.CurrentCulture) : DateLabel(_current));
		}
	}

	private void TriggerAutosave()
	{
		_autosave.Stop();
		_autosave.Start();
	}

	private void CommitSave()
	{
		if (!_lifecyclePaused)
		{
			if (_autosave != null)
			{
				_autosave.Stop();
			}
			if (_current != null && _title != null && !_loading)
			{
				NotesService.Instance.UpdateNote(_current.Id, _title.Text, _editor.GetBlocks());
				_current = NotesService.Instance.FindNote(_current.Id);
			}
		}
	}

	private void NewNote()
	{
		if (_root == null)
		{
			return;
		}
		if (NotesService.Instance.HasLoadFailure)
		{
			_toast.ShowNotice("Can't add notes · your saved notes couldn't be loaded");
			return;
		}
		if (_search != null && _search.IsOpen)
		{
			_search.Close();
		}
		if (_current != null && _current.Id == _createdEmptyId && NotesService.IsEmpty(_current))
		{
			FocusTitle();
			return;
		}
		CommitSave();
		NoteItem noteItem = NotesService.Instance.CreateNote(string.Empty, string.Empty);
		LoadNote(noteItem);
		_createdEmptyId = noteItem.Id;
		_listSignature = null;
		Refresh();
		_list.EnsureVisible(noteItem);
		FocusTitle();
	}

	private void OpenNotesFolder()
	{
		try
		{
			Process.Start("explorer.exe", "\"" + NotesService.Instance.StorageFolder + "\"");
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Notes: open folder failed: " + ex.Message);
			_toast.ShowNotice("Couldn't open the folder");
		}
	}

	private void FocusTitle()
	{
		_root.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			_title.Focus();
			Keyboard.Focus(_title);
			_title.CaretIndex = _title.Text.Length;
		});
	}

	private void TogglePin(NoteItem note)
	{
		CommitSave();
		NotesService.Instance.TogglePin(note.Id);
		UpdatePinIcon();
	}

	private void DeleteNote(NoteItem note)
	{
		if (note == null)
		{
			return;
		}
		_toast.Commit();
		CommitSave();
		int num = _visible.FindIndex((NoteItem n) => n.Id == note.Id);
		NoteItem note2 = null;
		if (num >= 0 && _visible.Count > 1)
		{
			note2 = _visible[(num + 1 < _visible.Count) ? (num + 1) : (num - 1)];
		}
		if (_current != null && _current.Id == note.Id)
		{
			_createdEmptyId = null;
			LoadNote(note2);
		}
		NoteItem removed = NotesService.Instance.RemoveForUndo(note.Id);
		if (removed != null)
		{
			_toast.Show("Note deleted", delegate
			{
				NotesService.Instance.Reinsert(removed);
				LoadNote(removed);
				_listSignature = null;
				Refresh();
				_list.EnsureVisible(removed);
			}, delegate
			{
			});
		}
	}

	private void CopyCurrent()
	{
		if (_current == null)
		{
			return;
		}
		CommitSave();
		string text = NotesService.ExplicitTitle(_current);
		string text2 = NoteDocument.ToCopyText(_current.Blocks);
		string text3 = ((text.Length <= 0) ? text2 : ((text2.Length > 0) ? (text + "\r\n\r\n" + text2) : text));
		if (text3.Length == 0 || !ClipboardService.Instance.CopyToClipboard(text3))
		{
			return;
		}
		_copyGlyph.Content = FluentIcons.Create("checkmark", 18.0, BuddyTheme.Text100, filled: false);
		if (_copyFeedback == null)
		{
			_copyFeedback = new DispatcherTimer
			{
				Interval = BuddyTheme.FeedbackHold
			};
			_copyFeedback.Tick += delegate
			{
				_copyFeedback.Stop();
				_copyGlyph.Content = FluentIcons.Create("copy", 18.0, BuddyTheme.Text100, filled: false);
			};
		}
		_copyFeedback.Stop();
		_copyFeedback.Start();
	}

	private void ShowRowMenu(FrameworkElement anchor, NoteItem note)
	{
		Select(note, scroll: false);
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		list.Add(new BuddyMenuItem(note.IsPinned ? "Unpin" : "Pin", "pin", delegate
		{
			TogglePin(note);
		}));
		list.Add(new BuddyMenuItem("Copy as text", "copy", CopyCurrent));
		list.Add(new BuddyMenuItem("Delete", "delete", delegate
		{
			DeleteNote(note);
		}));
		List<BuddyMenuItem> items = list;
		BuddyDropdown.Show(anchor, items, null, 160.0);
	}

	private void MoveSelection(int delta)
	{
		if (_visible.Count != 0)
		{
			int num = ((_current != null) ? _visible.FindIndex((NoteItem n) => n.Id == _current.Id) : (-1));
			int index = Math.Max(0, Math.Min(_visible.Count - 1, (num >= 0) ? (num + delta) : 0));
			Select(_visible[index], scroll: true);
		}
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		bool flag = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
		bool flag2 = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
		if (flag && !flag2 && e.Key == Key.F)
		{
			OpenSearch();
			e.Handled = true;
			return;
		}
		if (flag && !flag2 && e.Key == Key.N)
		{
			NewNote();
			e.Handled = true;
			return;
		}
		bool flag3 = _title.IsKeyboardFocusWithin || _editor.HasFocus || (_search != null && Keyboard.FocusedElement == _search.Box);
		if (flag && e.Key == Key.Z && !flag3 && _toast.IsPending)
		{
			_toast.Undo();
			e.Handled = true;
		}
		else
		{
			if (flag3)
			{
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
			case Key.Return:
				if (_current != null)
				{
					_editor.FocusEnd();
					e.Handled = true;
				}
				break;
			case Key.Delete:
				if (_current != null)
				{
					DeleteNote(_current);
					e.Handled = true;
				}
				break;
			}
		}
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
			if (!IsInitialized || _lifecyclePaused)
			{
				return;
			}
			try
			{
				a();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("NotesTool: " + ex.Message);
			}
		});
	}
}
