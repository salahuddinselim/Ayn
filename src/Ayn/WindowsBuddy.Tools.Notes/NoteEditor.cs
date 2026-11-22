using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Notes;

public sealed class NoteEditor : Grid
{
	private sealed class MarkerView : Grid
	{
		private readonly NoteEditor _owner;

		private readonly Border _dot;

		private readonly Border _circle;

		private readonly ContentControl _check;

		private NoteBlockKind _kind;

		public Paragraph Paragraph;

		public MarkerView(NoteEditor owner)
		{
			_owner = owner;
			base.Width = 20.0;
			base.Height = 20.0;
			base.Background = Brushes.Transparent;
			_dot = new Border
			{
				Width = 6.0,
				Height = 6.0,
				CornerRadius = new CornerRadius(3.0),
				Background = BuddyTheme.Text92,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false
			};
			base.Children.Add(_dot);
			_check = new ContentControl
			{
				Width = 14.0,
				Height = 14.0,
				Focusable = false,
				IsTabStop = false,
				IsHitTestVisible = false,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Content = FluentIcons.Create("checkmark", 14.0, BuddyTheme.Ink, filled: false)
			};
			_circle = new Border
			{
				Width = 20.0,
				Height = 20.0,
				CornerRadius = new CornerRadius(10.0),
				BorderThickness = new Thickness(1.5),
				Child = _check
			};
			base.Children.Add(_circle);
			MouseEventHandler value = delegate
			{
				if (_kind == NoteBlockKind.Check)
				{
					_circle.BorderBrush = BuddyTheme.Text92;
				}
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				Restyle();
			};
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				if (_kind != NoteBlockKind.Bullet && Paragraph != null)
				{
					e.Handled = true;
					_owner.ToggleCheck(Paragraph);
				}
			};
		}

		public void Bind(Paragraph p, NoteBlockKind kind)
		{
			Paragraph = p;
			_kind = kind;
			bool flag = kind == NoteBlockKind.Bullet;
			_dot.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			_circle.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			base.Cursor = (flag ? null : Cursors.Hand);
			base.IsHitTestVisible = !flag;
			Restyle();
		}

		private void Restyle()
		{
			bool flag = _kind == NoteBlockKind.Checked;
			_circle.Background = (flag ? BuddyTheme.Text100 : Brushes.Transparent);
			_circle.BorderBrush = (flag ? BuddyTheme.Text100 : BuddyTheme.Text50);
			_check.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	public const double BodySize = 15.0;

	public const double BodyLine = 24.0;

	public const double H1Size = 24.0;

	public const double H1Line = 32.0;

	public const double H2Size = 18.0;

	public const double H2Line = 26.0;

	public const double ListIndent = 30.0;

	private const double MarkerSize = 20.0;

	private static readonly TextDecorationCollection NoDeco = Frozen();

	private static readonly TextDecorationCollection UnderDeco = Frozen(TextDecorations.Underline);

	private static readonly TextDecorationCollection StrikeDeco = Frozen(TextDecorations.Strikethrough);

	private static readonly TextDecorationCollection BothDeco = Frozen(TextDecorations.Underline, TextDecorations.Strikethrough);

	private readonly RichTextBox _rtb;

	private readonly FlowDocument _doc;

	private readonly Grid _viewport;

	private readonly Canvas _overlay;

	private readonly TextBlock _placeholder;

	private readonly List<MarkerView> _markers = new List<MarkerView>();

	private bool _loading;

	private bool _deferredQueued;

	private int _fadeState = -1;

	private double _fadeHeight;

	private bool _caretPlaced;

	public RichTextBox Box => _rtb;

	public int Words { get; private set; }

	public string Plain { get; private set; }

	public int CheckDone { get; private set; }

	public int CheckTotal { get; private set; }

	public NoteBlockKind CurrentKind => KindOf(_rtb.CaretPosition.Paragraph);

	public string CurrentColor => NotePalette.KeyOf(_rtb.Selection.GetPropertyValue(TextElement.ForegroundProperty) as Brush);

	public bool HasFocus => _rtb.IsKeyboardFocusWithin;

	public event Action Changed;

	public event Action CheckToggled;

	public event Action Updated;

	public event Action UpAtTop;

	public NoteEditor()
	{
		Plain = string.Empty;
		_doc = new FlowDocument
		{
			PagePadding = new Thickness(0.0),
			FontFamily = BuddyTheme.Font,
			FontSize = 15.0,
			Foreground = BuddyTheme.Text92,
			LineHeight = 24.0,
			LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
			TextAlignment = TextAlignment.Left
		};
		_rtb = new RichTextBox
		{
			Document = _doc,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Foreground = BuddyTheme.Text92,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			FontFamily = BuddyTheme.Font,
			FontSize = 15.0,
			AcceptsReturn = true,
			AcceptsTab = false,
			VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			FocusVisualStyle = null
		};
		SpellCheck.SetIsEnabled(_rtb, value: false);
		BuddyTheme.ApplyTextMode(_rtb, 15.0);
		_overlay = new Canvas
		{
			ClipToBounds = true
		};
		_placeholder = BuddyTheme.Text("Start writing", 15.0, BuddyTheme.Placeholder, FontWeights.Normal);
		_placeholder.VerticalAlignment = VerticalAlignment.Top;
		_placeholder.IsHitTestVisible = false;
		_placeholder.Margin = new Thickness(2.0, 1.0, 0.0, 0.0);
		_viewport = new Grid
		{
			ClipToBounds = true
		};
		_viewport.Children.Add(_rtb);
		_viewport.Children.Add(_placeholder);
		_viewport.Children.Add(_overlay);
		base.Children.Add(_viewport);
		_rtb.PreviewKeyDown += OnPreviewKeyDown;
		_rtb.PreviewTextInput += OnPreviewTextInput;
		RichTextBox rtb = _rtb;
		TextChangedEventHandler value = delegate
		{
			if (!_loading)
			{
				QueueDeferred();
				this.Changed?.Invoke();
			}
		};
		rtb.TextChanged += value;
		_rtb.SelectionChanged += delegate
		{
			if (!_loading)
			{
				QueueDeferred();
			}
		};
		_rtb.GotKeyboardFocus += delegate
		{
			_caretPlaced = true;
		};
		_rtb.AddHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)delegate
		{
			UpdateMarkers();
			UpdateFade();
		});
		base.SizeChanged += delegate
		{
			QueueDeferred();
		};
		DataObject.AddPastingHandler(_rtb, OnPasting);
		Load(null);
	}

	private static TextDecorationCollection Frozen(params TextDecorationCollection[] parts)
	{
		TextDecorationCollection textDecorationCollection = new TextDecorationCollection();
		foreach (TextDecorationCollection textDecorationCollection2 in parts)
		{
			foreach (TextDecoration item in textDecorationCollection2)
			{
				textDecorationCollection.Add(item);
			}
		}
		textDecorationCollection.Freeze();
		return textDecorationCollection;
	}

	public void Load(IList<NoteBlock> blocks)
	{
		_loading = true;
		_caretPlaced = false;
		try
		{
			bool flag = blocks != null && blocks.Count > 200;
			if (flag)
			{
				_rtb.Document = new FlowDocument();
			}
			_doc.Blocks.Clear();
			if (blocks != null)
			{
				List<Block> list = new List<Block>(blocks.Count);
				foreach (NoteBlock block in blocks)
				{
					Paragraph paragraph = new Paragraph();
					foreach (NoteRun run in block.Runs)
					{
						paragraph.Inlines.Add(MakeRun(run));
					}
					ApplyBlockStyle(paragraph, block.Kind);
					list.Add(paragraph);
				}
				_doc.Blocks.AddRange(list);
			}
			if (flag)
			{
				_rtb.Document = _doc;
			}
			if (_doc.Blocks.Count == 0)
			{
				Paragraph paragraph2 = new Paragraph();
				ApplyBlockStyle(paragraph2, NoteBlockKind.Paragraph);
				_doc.Blocks.Add(paragraph2);
			}
			_rtb.CaretPosition = _doc.ContentStart;
			_rtb.ScrollToHome();
			_rtb.IsUndoEnabled = false;
			_rtb.IsUndoEnabled = true;
		}
		finally
		{
			_loading = false;
		}
		QueueDeferred();
	}

	private static Run MakeRun(NoteRun r)
	{
		Run run = new Run(r.Text ?? string.Empty);
		if (r.Bold)
		{
			run.FontWeight = FontWeights.Bold;
		}
		if (r.Italic)
		{
			run.FontStyle = FontStyles.Italic;
		}
		if (r.Underline || r.Strike)
		{
			run.TextDecorations = Deco(r.Underline, r.Strike);
		}
		Brush brush = NotePalette.BrushOf(r.Color);
		if (brush != null)
		{
			run.Foreground = brush;
		}
		return run;
	}

	public List<NoteBlock> GetBlocks()
	{
		List<NoteBlock> list = new List<NoteBlock>();
		foreach (Block block in _doc.Blocks)
		{
			if (!(block is Paragraph paragraph))
			{
				string text = new TextRange(block.ContentStart, block.ContentEnd).Text.Replace("\r", string.Empty);
				string[] array = text.Split('\n');
				foreach (string text2 in array)
				{
					list.Add(new NoteBlock(NoteBlockKind.Paragraph, text2));
				}
			}
			else
			{
				NoteBlock noteBlock = new NoteBlock();
				noteBlock.Kind = KindOf(paragraph);
				NoteBlock noteBlock2 = noteBlock;
				CollectRuns(paragraph.Inlines, paragraph, noteBlock2.Runs);
				list.Add(noteBlock2);
			}
		}
		return NoteDocument.Normalize(list);
	}

	private static void CollectRuns(InlineCollection inlines, Paragraph p, List<NoteRun> into)
	{
		foreach (Inline inline in inlines)
		{
			if (inline is Run run)
			{
				if (!string.IsNullOrEmpty(run.Text))
				{
					ReadDecorations(run, p, out var underline, out var strike);
					into.Add(new NoteRun(run.Text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' '))
					{
						Bold = (run.FontWeight.ToOpenTypeWeight() >= 700),
						Italic = (run.FontStyle != FontStyles.Normal),
						Underline = underline,
						Strike = strike,
						Color = NotePalette.KeyOf(run.Foreground)
					});
				}
			}
			else if (inline is Span span)
			{
				CollectRuns(span.Inlines, p, into);
			}
			else if (inline is LineBreak)
			{
				into.Add(new NoteRun(" "));
			}
		}
	}

	private static void ReadDecorations(Inline inline, Paragraph p, out bool underline, out bool strike)
	{
		underline = (strike = false);
		DependencyObject dependencyObject = inline;
		while (dependencyObject != null && dependencyObject != p)
		{
			if (dependencyObject is Inline { TextDecorations: not null } inline2)
			{
				foreach (TextDecoration textDecoration in inline2.TextDecorations)
				{
					if (textDecoration.Location == TextDecorationLocation.Underline)
					{
						underline = true;
					}
					else if (textDecoration.Location == TextDecorationLocation.Strikethrough)
					{
						strike = true;
					}
				}
			}
			dependencyObject = ((dependencyObject is TextElement textElement) ? textElement.Parent : null);
		}
	}

	private static TextDecorationCollection Deco(bool underline, bool strike)
	{
		if (!underline)
		{
			if (!strike)
			{
				return NoDeco;
			}
			return StrikeDeco;
		}
		if (!strike)
		{
			return UnderDeco;
		}
		return BothDeco;
	}

	public static NoteBlockKind KindOf(Paragraph p)
	{
		if (p == null || !(p.Tag is NoteBlockKind))
		{
			return NoteBlockKind.Paragraph;
		}
		return (NoteBlockKind)p.Tag;
	}

	private static bool IsList(NoteBlockKind k)
	{
		if (k != NoteBlockKind.Bullet && k != NoteBlockKind.Check)
		{
			return k == NoteBlockKind.Checked;
		}
		return true;
	}

	private static bool IsHeading(NoteBlockKind k)
	{
		if (k != NoteBlockKind.Heading1)
		{
			return k == NoteBlockKind.Heading2;
		}
		return true;
	}

	private static void ApplyBlockStyle(Paragraph p, NoteBlockKind kind)
	{
		p.Tag = kind;
		if (IsHeading(kind))
		{
			bool flag = kind == NoteBlockKind.Heading1;
			p.Margin = (flag ? new Thickness(0.0, 8.0, 0.0, 4.0) : new Thickness(0.0, 6.0, 0.0, 2.0));
			p.FontSize = (flag ? 24.0 : 18.0);
			p.LineHeight = (flag ? 32.0 : 26.0);
			p.FontWeight = AynType.Semibold;
			p.Foreground = BuddyTheme.Text100;
			p.ClearValue(Paragraph.TextDecorationsProperty);
			return;
		}
		p.Margin = (IsList(kind) ? new Thickness(30.0, 0.0, 0.0, 2.0) : new Thickness(0.0, 0.0, 0.0, 6.0));
		p.ClearValue(TextElement.FontSizeProperty);
		p.ClearValue(TextElement.FontWeightProperty);
		p.ClearValue(Block.LineHeightProperty);
		if (kind == NoteBlockKind.Checked)
		{
			p.Foreground = BuddyTheme.Text50;
			p.TextDecorations = TextDecorations.Strikethrough;
		}
		else
		{
			p.ClearValue(TextElement.ForegroundProperty);
			p.ClearValue(Paragraph.TextDecorationsProperty);
		}
	}

	private void SetKind(Paragraph p, NoteBlockKind kind)
	{
		if (p != null)
		{
			ApplyBlockStyle(p, kind);
			QueueDeferred();
		}
	}

	public void ToggleKind(NoteBlockKind kind)
	{
		List<Paragraph> list = SelectedParagraphs();
		if (list.Count == 0)
		{
			return;
		}
		bool flag = true;
		foreach (Paragraph item in list)
		{
			NoteBlockKind noteBlockKind = KindOf(item);
			if (noteBlockKind != kind && (kind != NoteBlockKind.Check || noteBlockKind != NoteBlockKind.Checked))
			{
				flag = false;
				break;
			}
		}
		SetKinds(list, (!flag) ? kind : NoteBlockKind.Paragraph);
	}

	public void SetText()
	{
		SetKinds(SelectedParagraphs(), NoteBlockKind.Paragraph);
	}

	private void SetKinds(List<Paragraph> paragraphs, NoteBlockKind kind)
	{
		if (paragraphs.Count == 0)
		{
			return;
		}
		foreach (Paragraph paragraph in paragraphs)
		{
			ApplyBlockStyle(paragraph, kind);
		}
		QueueDeferred();
		RaiseChanged();
	}

	private List<Paragraph> SelectedParagraphs()
	{
		List<Paragraph> list = new List<Paragraph>();
		Paragraph paragraph = _rtb.Selection.Start.Paragraph;
		Paragraph paragraph2 = _rtb.Selection.End.Paragraph;
		if (paragraph == null)
		{
			return list;
		}
		bool flag = false;
		foreach (Block block in _doc.Blocks)
		{
			if (block is Paragraph paragraph3)
			{
				if (paragraph3 == paragraph)
				{
					flag = true;
				}
				if (flag)
				{
					list.Add(paragraph3);
				}
				if (paragraph3 == paragraph2 || (paragraph2 == null && paragraph3 == paragraph))
				{
					break;
				}
			}
		}
		return list;
	}

	public bool IsActive(NoteInline f)
	{
		TextSelection selection = _rtb.Selection;
		switch (f)
		{
		case NoteInline.Bold:
			if (selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fontWeight)
			{
				return fontWeight.ToOpenTypeWeight() >= 700;
			}
			return false;
		case NoteInline.Italic:
		{
			object propertyValue = selection.GetPropertyValue(TextElement.FontStyleProperty);
			if (propertyValue is FontStyle)
			{
				return (FontStyle)propertyValue != FontStyles.Normal;
			}
			return false;
		}
		default:
		{
			SelectionDecorations(out var underline, out var strike);
			if (f != NoteInline.Underline)
			{
				return strike;
			}
			return underline;
		}
		}
	}

	private void SelectionDecorations(out bool underline, out bool strike)
	{
		underline = (strike = false);
		if (!(_rtb.Selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection textDecorationCollection))
		{
			return;
		}
		foreach (TextDecoration item in textDecorationCollection)
		{
			if (item.Location == TextDecorationLocation.Underline)
			{
				underline = true;
			}
			else if (item.Location == TextDecorationLocation.Strikethrough)
			{
				strike = true;
			}
		}
		if (strike && KindOf(_rtb.Selection.Start.Paragraph) == NoteBlockKind.Checked && _rtb.Selection.IsEmpty && _rtb.CaretPosition.Parent is Inline inline)
		{
			ReadDecorations(inline, _rtb.CaretPosition.Paragraph, out var _, out var strike2);
			strike = strike2;
		}
	}

	public void ToggleInline(NoteInline f)
	{
		bool flag = !IsActive(f);
		TextSelection selection = _rtb.Selection;
		switch (f)
		{
		case NoteInline.Bold:
			if (flag)
			{
				selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
			}
			else
			{
				ApplyPerParagraph(TextElement.FontWeightProperty, BaseWeight);
			}
			break;
		case NoteInline.Italic:
			selection.ApplyPropertyValue(TextElement.FontStyleProperty, flag ? FontStyles.Italic : FontStyles.Normal);
			break;
		default:
			ApplyDecoration(f == NoteInline.Underline, flag);
			break;
		}
		QueueDeferred();
		RaiseChanged();
	}

	public void ApplyColor(string key)
	{
		Brush brush = NotePalette.BrushOf(key);
		TextSelection selection = _rtb.Selection;
		if (brush != null)
		{
			selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
		}
		else
		{
			ApplyPerParagraph(TextElement.ForegroundProperty, BaseInk);
			if (!selection.IsEmpty)
			{
				ClearLocal(selection.Start, selection.End, TextElement.ForegroundProperty);
			}
		}
		QueueDeferred();
		RaiseChanged();
	}

	private static object BaseWeight(Paragraph p)
	{
		return IsHeading(KindOf(p)) ? AynType.Semibold : AynType.Regular;
	}

	private static object BaseInk(Paragraph p)
	{
		NoteBlockKind noteBlockKind = KindOf(p);
		if (!IsHeading(noteBlockKind))
		{
			if (noteBlockKind != NoteBlockKind.Checked)
			{
				return BuddyTheme.Text92;
			}
			return BuddyTheme.Text50;
		}
		return BuddyTheme.Text100;
	}

	private void ApplyPerParagraph(DependencyProperty property, Func<Paragraph, object> valueFor)
	{
		TextSelection selection = _rtb.Selection;
		if (selection.IsEmpty)
		{
			selection.ApplyPropertyValue(property, valueFor(_rtb.CaretPosition.Paragraph));
			return;
		}
		TextPointer start = selection.Start;
		TextPointer end = selection.End;
		foreach (Paragraph item in SelectedParagraphs())
		{
			TextPointer textPointer = ((item.ContentStart.CompareTo(start) > 0) ? item.ContentStart : start);
			TextPointer textPointer2 = ((item.ContentEnd.CompareTo(end) < 0) ? item.ContentEnd : end);
			if (textPointer.CompareTo(textPointer2) < 0)
			{
				new TextRange(textPointer, textPointer2).ApplyPropertyValue(property, valueFor(item));
			}
		}
	}

	private void ApplyDecoration(bool underline, bool on)
	{
		TextSelection selection = _rtb.Selection;
		if (selection.IsEmpty)
		{
			SelectionDecorations(out var underline2, out var strike);
			if (underline)
			{
				underline2 = on;
			}
			else
			{
				strike = on;
			}
			selection.ApplyPropertyValue(Inline.TextDecorationsProperty, Deco(underline2, strike));
			return;
		}
		List<KeyValuePair<TextRange, TextDecorationCollection>> list = new List<KeyValuePair<TextRange, TextDecorationCollection>>();
		TextPointer textPointer = selection.Start;
		TextPointer end = selection.End;
		while (textPointer != null && textPointer.CompareTo(end) < 0)
		{
			if (textPointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
			{
				TextPointer textPointer2 = textPointer.GetPositionAtOffset(textPointer.GetTextRunLength(LogicalDirection.Forward));
				if (textPointer2 == null || textPointer2.CompareTo(end) > 0)
				{
					textPointer2 = end;
				}
				bool underline3 = false;
				bool strike2 = false;
				if (textPointer.Parent is Inline inline)
				{
					ReadDecorations(inline, textPointer.Paragraph, out underline3, out strike2);
				}
				if (underline)
				{
					underline3 = on;
				}
				else
				{
					strike2 = on;
				}
				list.Add(new KeyValuePair<TextRange, TextDecorationCollection>(new TextRange(textPointer, textPointer2), Deco(underline3, strike2)));
				textPointer = textPointer2;
			}
			else
			{
				textPointer = textPointer.GetNextContextPosition(LogicalDirection.Forward);
			}
		}
		foreach (KeyValuePair<TextRange, TextDecorationCollection> item in list)
		{
			item.Key.ApplyPropertyValue(Inline.TextDecorationsProperty, item.Value);
		}
	}

	private static void ClearLocal(TextPointer start, TextPointer end, DependencyProperty property)
	{
		List<Inline> list = new List<Inline>();
		TextPointer textPointer = start;
		while (textPointer != null && textPointer.CompareTo(end) < 0)
		{
			if (textPointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart && textPointer.GetAdjacentElement(LogicalDirection.Forward) is Inline inline && inline.ContentEnd.CompareTo(end) <= 0)
			{
				list.Add(inline);
			}
			textPointer = textPointer.GetNextContextPosition(LogicalDirection.Forward);
		}
		if (start.Parent is Inline inline2 && inline2.ContentStart.CompareTo(start) >= 0 && inline2.ContentEnd.CompareTo(end) <= 0)
		{
			list.Add(inline2);
		}
		foreach (Inline item in list)
		{
			item.ClearValue(property);
		}
	}

	private void RaiseChanged()
	{
		if (!_loading)
		{
			this.Changed?.Invoke();
		}
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		ModifierKeys modifiers = Keyboard.Modifiers;
		bool flag = (modifiers & ModifierKeys.Control) != 0;
		bool flag2 = (modifiers & ModifierKeys.Shift) != 0;
		bool flag3 = (modifiers & ModifierKeys.Alt) != 0;
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		if (flag && !flag3)
		{
			if (!flag2 && key == Key.B)
			{
				ToggleInline(NoteInline.Bold);
				e.Handled = true;
				return;
			}
			if (!flag2 && key == Key.I)
			{
				ToggleInline(NoteInline.Italic);
				e.Handled = true;
				return;
			}
			if (!flag2 && key == Key.U)
			{
				ToggleInline(NoteInline.Underline);
				e.Handled = true;
				return;
			}
			if (flag2 && key == Key.X)
			{
				ToggleInline(NoteInline.Strike);
				e.Handled = true;
				return;
			}
			if (flag2 && (key == Key.D7 || key == Key.NumPad7))
			{
				ToggleKind(NoteBlockKind.Bullet);
				e.Handled = true;
				return;
			}
			if (flag2 && (key == Key.D8 || key == Key.NumPad8))
			{
				ToggleKind(NoteBlockKind.Check);
				e.Handled = true;
				return;
			}
			if (flag2 && (key == Key.D9 || key == Key.NumPad9))
			{
				ToggleKind(NoteBlockKind.Heading1);
				e.Handled = true;
				return;
			}
			if (key == Key.E || key == Key.R || key == Key.J || key == Key.L || key == Key.Oem4 || key == Key.Oem6 || key == Key.OemPeriod || key == Key.OemComma || key == Key.OemPlus || key == Key.Add)
			{
				e.Handled = true;
				return;
			}
		}
		if (key == Key.Tab && !flag && !flag3)
		{
			e.Handled = true;
			return;
		}
		Paragraph paragraph = _rtb.CaretPosition.Paragraph;
		if (key == Key.Return && !flag && !flag3)
		{
			e.Handled = true;
			if (paragraph == null)
			{
				return;
			}
			NoteBlockKind noteBlockKind = KindOf(paragraph);
			if (IsList(noteBlockKind) && _rtb.Selection.IsEmpty && IsBlank(paragraph))
			{
				SetKind(paragraph, NoteBlockKind.Paragraph);
				RaiseChanged();
				return;
			}
			if (!_rtb.Selection.IsEmpty)
			{
				_rtb.Selection.Text = string.Empty;
			}
			TextPointer textPointer = _rtb.CaretPosition.InsertParagraphBreak();
			ApplyBlockStyle(paragraph, noteBlockKind);
			Paragraph paragraph2 = textPointer.Paragraph;
			if (paragraph2 != null)
			{
				NoteBlockKind kind = ((!IsHeading(noteBlockKind)) ? ((noteBlockKind == NoteBlockKind.Checked) ? NoteBlockKind.Check : noteBlockKind) : NoteBlockKind.Paragraph);
				ApplyBlockStyle(paragraph2, kind);
			}
			_rtb.CaretPosition = textPointer;
			QueueDeferred();
		}
		else if (key == Key.Back && paragraph != null && _rtb.Selection.IsEmpty && KindOf(paragraph) != 0 && _rtb.CaretPosition.CompareTo(paragraph.ContentStart) <= 0)
		{
			SetKind(paragraph, NoteBlockKind.Paragraph);
			RaiseChanged();
			e.Handled = true;
		}
		else if (key == Key.Up && !flag2 && paragraph != null && paragraph == _doc.Blocks.FirstBlock && IsOnFirstLine())
		{
			Action upAtTop = this.UpAtTop;
			if (upAtTop != null)
			{
				upAtTop();
				e.Handled = true;
			}
		}
	}

	private bool IsOnFirstLine()
	{
		TextPointer lineStartPosition = _rtb.CaretPosition.GetLineStartPosition(0);
		TextPointer lineStartPosition2 = _rtb.CaretPosition.GetLineStartPosition(-1);
		if (lineStartPosition2 != null && lineStartPosition != null)
		{
			return lineStartPosition2.CompareTo(lineStartPosition) == 0;
		}
		return true;
	}

	private static bool IsBlank(Paragraph p)
	{
		return new TextRange(p.ContentStart, p.ContentEnd).Text.Trim().Length == 0;
	}

	private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		Paragraph paragraph = _rtb.CaretPosition.Paragraph;
		if (paragraph != null && _rtb.Selection.IsEmpty && !(e.Text != " ") && KindOf(paragraph) == NoteBlockKind.Paragraph)
		{
			string text = new TextRange(paragraph.ContentStart, _rtb.CaretPosition).Text;
			NoteBlockKind? noteBlockKind = null;
			switch (text)
			{
			case "#":
				noteBlockKind = NoteBlockKind.Heading1;
				break;
			case "##":
				noteBlockKind = NoteBlockKind.Heading2;
				break;
			case "-":
			case "*":
			case "•":
				noteBlockKind = NoteBlockKind.Bullet;
				break;
			case "[]":
			case "[ ]":
			case "-[]":
			case "- [ ]":
			case "- []":
				noteBlockKind = NoteBlockKind.Check;
				break;
			case "[x]":
			case "[X]":
			case "- [x]":
				noteBlockKind = NoteBlockKind.Checked;
				break;
			}
			if (noteBlockKind.HasValue)
			{
				new TextRange(paragraph.ContentStart, _rtb.CaretPosition).Text = string.Empty;
				SetKind(paragraph, noteBlockKind.Value);
				RaiseChanged();
				e.Handled = true;
			}
		}
	}

	private void OnPasting(object sender, DataObjectPastingEventArgs e)
	{
		string text = (e.DataObject.GetDataPresent(DataFormats.UnicodeText) ? (e.DataObject.GetData(DataFormats.UnicodeText) as string) : (e.DataObject.GetDataPresent(DataFormats.Text) ? (e.DataObject.GetData(DataFormats.Text) as string) : null));
		e.CancelCommand();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (!_rtb.Selection.IsEmpty)
		{
			_rtb.Selection.Text = string.Empty;
		}
		string[] array = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		Paragraph paragraph = _rtb.CaretPosition.Paragraph;
		NoteBlockKind noteBlockKind = KindOf(paragraph);
		NoteBlockKind kind = (IsList(noteBlockKind) ? ((noteBlockKind == NoteBlockKind.Checked) ? NoteBlockKind.Check : noteBlockKind) : NoteBlockKind.Paragraph);
		_rtb.BeginChange();
		try
		{
			for (int i = 0; i < array.Length; i++)
			{
				if (i > 0)
				{
					TextPointer textPointer = _rtb.CaretPosition.InsertParagraphBreak();
					if (paragraph != null)
					{
						ApplyBlockStyle(paragraph, KindOf(paragraph));
					}
					_rtb.CaretPosition = textPointer;
					paragraph = textPointer.Paragraph;
					if (paragraph != null)
					{
						ApplyBlockStyle(paragraph, kind);
					}
				}
				string text2 = array[i].Replace('\t', ' ');
				if (text2.Length != 0)
				{
					_rtb.CaretPosition.InsertTextInRun(text2);
					_rtb.CaretPosition = _rtb.CaretPosition.GetPositionAtOffset(text2.Length, LogicalDirection.Forward) ?? _rtb.CaretPosition;
				}
			}
		}
		finally
		{
			_rtb.EndChange();
		}
		QueueDeferred();
		RaiseChanged();
	}

	public void FocusStart()
	{
		_rtb.Focus();
		Keyboard.Focus(_rtb);
		_rtb.CaretPosition = _doc.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
	}

	public void FocusEnd()
	{
		_rtb.Focus();
		Keyboard.Focus(_rtb);
		_rtb.CaretPosition = _doc.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
	}

	public void RestoreFocus()
	{
		if (!HasFocus)
		{
			if (!_caretPlaced)
			{
				FocusEnd();
				return;
			}
			_rtb.Focus();
			Keyboard.Focus(_rtb);
		}
	}

	private void QueueDeferred()
	{
		if (!_deferredQueued)
		{
			_deferredQueued = true;
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RunDeferred));
		}
	}

	private void RunDeferred()
	{
		_deferredQueued = false;
		int num = 0;
		int num2 = 0;
		foreach (Block block in _doc.Blocks)
		{
			if (block is Paragraph paragraph)
			{
				if (!(paragraph.Tag is NoteBlockKind))
				{
					ApplyBlockStyle(paragraph, NoteBlockKind.Paragraph);
				}
				switch (KindOf(paragraph))
				{
				case NoteBlockKind.Check:
					num2++;
					break;
				case NoteBlockKind.Checked:
					num2++;
					num++;
					break;
				}
			}
		}
		CheckDone = num;
		CheckTotal = num2;
		UpdateMarkers();
		UpdateFade();
		string text2 = (Plain = new TextRange(_doc.ContentStart, _doc.ContentEnd).Text.Replace("\r\n", "\n"));
		Words = NoteDocument.WordCount(text2);
		bool flag = _doc.Blocks.Count <= 1 && text2.Trim().Length == 0 && KindOf(_doc.Blocks.FirstBlock as Paragraph) == NoteBlockKind.Paragraph;
		_placeholder.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		this.Updated?.Invoke();
	}

	private void UpdateMarkers()
	{
		int num = 0;
		double actualHeight = _rtb.ActualHeight;
		foreach (Block block in _doc.Blocks)
		{
			Paragraph paragraph = block as Paragraph;
			NoteBlockKind noteBlockKind = KindOf(paragraph);
			if (!IsList(noteBlockKind))
			{
				continue;
			}
			Rect characterRect = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
			if (characterRect.IsEmpty)
			{
				continue;
			}
			double num2 = characterRect.Top + characterRect.Height / 2.0;
			if (!(num2 < -20.0) && !(num2 > actualHeight + 20.0))
			{
				if (num == _markers.Count)
				{
					MarkerView markerView = new MarkerView(this);
					_markers.Add(markerView);
					_overlay.Children.Add(markerView);
				}
				MarkerView markerView2 = _markers[num++];
				markerView2.Bind(paragraph, noteBlockKind);
				Canvas.SetLeft(markerView2, Math.Round(characterRect.Left - 30.0));
				Canvas.SetTop(markerView2, Math.Round(num2 - 10.0));
				markerView2.Visibility = Visibility.Visible;
			}
		}
		for (int i = num; i < _markers.Count; i++)
		{
			_markers[i].Visibility = Visibility.Collapsed;
			_markers[i].Paragraph = null;
		}
	}

	private void ToggleCheck(Paragraph p)
	{
		if (p != null)
		{
			NoteBlockKind noteBlockKind = KindOf(p);
			if (noteBlockKind == NoteBlockKind.Check || noteBlockKind == NoteBlockKind.Checked)
			{
				ApplyBlockStyle(p, (noteBlockKind == NoteBlockKind.Check) ? NoteBlockKind.Checked : NoteBlockKind.Check);
				UpdateMarkers();
				QueueDeferred();
				RaiseChanged();
				this.CheckToggled?.Invoke();
			}
		}
	}

	private void UpdateFade()
	{
		double actualHeight = _viewport.ActualHeight;
		double extentHeight = _rtb.ExtentHeight;
		double viewportHeight = _rtb.ViewportHeight;
		double verticalOffset = _rtb.VerticalOffset;
		bool flag = extentHeight > viewportHeight + 1.0;
		bool flag2 = flag && verticalOffset < extentHeight - viewportHeight - 1.0;
		bool flag3 = flag && verticalOffset > 1.0;
		int num = ((!(actualHeight <= 0.0)) ? ((flag2 ? 1 : 0) | (flag3 ? 2 : 0)) : 0);
		if (num != _fadeState || actualHeight != _fadeHeight)
		{
			_fadeState = num;
			_fadeHeight = actualHeight;
			if (num == 0)
			{
				_viewport.OpacityMask = null;
				return;
			}
			double num2 = Math.Min(0.5, 24.0 / actualHeight);
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
			_viewport.OpacityMask = linearGradientBrush2;
		}
	}
}
