using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Files;

public class FilesConverterTool : IBuddyTool, IEscapeAware, IFileDropTarget
{
	private sealed class FileRow : Grid
	{
		private readonly Border _thumb;

		private readonly TextBlock _name;

		private readonly TextBlock _meta;

		private readonly TextBlock _percent;

		private readonly Grid _progress;

		private readonly Rectangle _fill;

		private readonly Border _remove;

		private readonly Border _retry;

		private readonly ContentControl _check;

		private readonly Action _reveal;

		private string _fullName;

		private string _tip;

		private bool _clickable;

		public FileRow(Action remove, Action retry, Action reveal)
		{
			_reveal = reveal;
			base.Height = 44.0;
			base.Background = Brushes.Transparent;
			BuddyTheme.Crisp(this);
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(32.0)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(10.0)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			_thumb = new Border
			{
				Width = 32.0,
				Height = 32.0,
				CornerRadius = new CornerRadius(6.0),
				Background = BuddyTheme.Control,
				VerticalAlignment = VerticalAlignment.Center
			};
			base.Children.Add(_thumb);
			StackPanel stackPanel = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			_name = Txt("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
			_meta = Txt("", 11.5, BuddyTheme.Text30, FontWeights.Medium);
			_meta.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
			_meta.TextTrimming = TextTrimming.CharacterEllipsis;
			Typography.SetNumeralAlignment(_meta, FontNumeralAlignment.Tabular);
			stackPanel.Children.Add(_name);
			stackPanel.Children.Add(_meta);
			Grid.SetColumn(stackPanel, 2);
			base.Children.Add(stackPanel);
			SizeChangedEventHandler value = delegate(object s, SizeChangedEventArgs e)
			{
				if (e.WidthChanged)
				{
					TrimName();
				}
			};
			stackPanel.SizeChanged += value;
			_progress = new Grid
			{
				Height = 2.0,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0.0, 0.0, 0.0, 2.0),
				Visibility = Visibility.Collapsed,
				IsHitTestVisible = false
			};
			_progress.Children.Add(new Rectangle
			{
				Fill = BuddyTheme.Track,
				RadiusX = 1.0,
				RadiusY = 1.0
			});
			_fill = new Rectangle
			{
				Fill = BuddyTheme.Mark,
				RadiusX = 1.0,
				RadiusY = 1.0,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				Width = 0.0
			};
			_progress.Children.Add(_fill);
			Grid.SetColumn(_progress, 2);
			Grid.SetColumnSpan(_progress, 2);
			base.Children.Add(_progress);
			Grid grid = new Grid
			{
				MinWidth = 24.0,
				Margin = new Thickness(12.0, 0.0, -4.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			_remove = BuddyKit.GlyphButton("dismiss", 14.0, "Remove", remove, out var glyphHost);
			_retry = BuddyKit.GlyphButton("arrow_reset", 14.0, "Retry", retry, out glyphHost);
			_percent = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
			_percent.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			_percent.VerticalAlignment = VerticalAlignment.Center;
			_percent.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
			Typography.SetNumeralAlignment(_percent, FontNumeralAlignment.Tabular);
			_check = new ContentControl
			{
				Width = 16.0,
				Height = 16.0,
				IsHitTestVisible = false,
				Margin = new Thickness(4.0),
				Content = FluentIcons.Create("checkmark", 16.0, BuddyTheme.Text100, filled: false)
			};
			FrameworkElement[] array = new FrameworkElement[4] { _remove, _retry, _percent, _check };
			foreach (FrameworkElement frameworkElement in array)
			{
				frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
				grid.Children.Add(frameworkElement);
			}
			Grid.SetColumn(grid, 3);
			base.Children.Add(grid);
			base.MouseEnter += delegate
			{
				_name.Foreground = BuddyTheme.Text100;
			};
			base.MouseLeave += delegate
			{
				_name.Foreground = BuddyTheme.Text92;
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				if (_clickable)
				{
					e.Handled = true;
					_reveal();
				}
			};
		}

		public void Update(FileItem item, bool running, OutFormat format)
		{
			if (_fullName != item.Name)
			{
				_fullName = item.Name;
				TrimName();
			}
			string text = item.Name;
			if (item.State == ItemState.Done && item.Outputs.Count > 0)
			{
				string text2 = text;
				text = text2 + "\n→ " + System.IO.Path.GetFileName(item.Outputs[0]) + ((item.Outputs.Count > 1) ? (" (+" + (item.Outputs.Count - 1) + " more pages)") : "") + "\nClick to show in folder";
			}
			else if (item.State == ItemState.Failed && !string.IsNullOrEmpty(item.Error))
			{
				text = text + "\n" + item.Error;
			}
			if (_tip != text)
			{
				_tip = text;
				base.ToolTip = BuddyTooltip.Create(text);
			}
			if (item.Thumb != null && !(_thumb.Background is ImageBrush))
			{
				_thumb.Background = new ImageBrush(item.Thumb)
				{
					Stretch = Stretch.UniformToFill
				};
				_thumb.Child = null;
			}
			else if (item.Thumb == null && item.Probed && _thumb.Child == null)
			{
				_thumb.Child = new ContentControl
				{
					Width = 16.0,
					Height = 16.0,
					HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					Content = FluentIcons.Create(item.IsPdf ? "document_text" : "image", 16.0, BuddyTheme.Text50, filled: false)
				};
			}
			string typeLabel = item.TypeLabel;
			string text3 = ((item.IsPdf && item.PageCount > 1) ? (" · " + item.PageCount + " pages") : "");
			string text4 = FileItem.FormatBytes(item.SizeBytes);
			Border remove = _remove;
			Border retry = _retry;
			TextBlock percent = _percent;
			Visibility visibility2 = (_check.Visibility = Visibility.Collapsed);
			Visibility visibility4 = (percent.Visibility = visibility2);
			Visibility visibility6 = (retry.Visibility = visibility4);
			remove.Visibility = visibility6;
			_progress.Visibility = Visibility.Collapsed;
			_meta.Foreground = BuddyTheme.Text30;
			_clickable = false;
			switch (item.State)
			{
			case ItemState.Converting:
			{
				_meta.Text = typeLabel + " → " + FileItem.FormatName(format) + text3 + " · " + text4;
				_percent.Text = Math.Round(item.Progress * 100.0) + "%";
				_percent.Visibility = Visibility.Visible;
				_progress.Visibility = Visibility.Visible;
				double num = Math.Max(0.0, _progress.ActualWidth);
				if (num <= 0.0)
				{
					_progress.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
					{
						_fill.Width = _progress.ActualWidth * item.Progress;
					});
				}
				_fill.Width = num * item.Progress;
				break;
			}
			case ItemState.Done:
				_meta.Text = item.Note ?? (typeLabel + " → " + FileItem.FormatName(item.DoneFormat) + text3 + " · " + text4 + " → " + FileItem.FormatBytes(item.OutputBytes));
				_check.Visibility = Visibility.Visible;
				_clickable = true;
				base.Cursor = System.Windows.Input.Cursors.Hand;
				break;
			case ItemState.Failed:
				_meta.Text = "Failed · " + (item.Error ?? "Couldn't convert");
				_meta.Foreground = BuddyTheme.Text50;
				if (item.InputError == null && !running)
				{
					_retry.Visibility = Visibility.Visible;
				}
				else if (item.InputError != null && !running)
				{
					_remove.Visibility = Visibility.Visible;
				}
				break;
			default:
				_meta.Text = typeLabel + text3 + " · " + text4;
				if (!running)
				{
					_remove.Visibility = Visibility.Visible;
				}
				break;
			}
			if (!_clickable)
			{
				base.Cursor = null;
			}
		}

		private void TrimName()
		{
			if (_fullName != null)
			{
				double actualWidth = ((FrameworkElement)_name.Parent).ActualWidth;
				_name.Text = ((actualWidth > 0.0) ? MiddleTrim(_fullName, actualWidth, _name) : _fullName);
			}
		}
	}

	private sealed class Segmented : Grid
	{
		private readonly Border _thumb;

		private readonly TranslateTransform _move = new TranslateTransform();

		private readonly TextBlock[] _labels;

		private int _index = -1;

		public event Action<int> Selected;

		public Segmented(string[] labels)
		{
			base.Height = 30.0;
			BuddyTheme.Crisp(this);
			base.Children.Add(new Border
			{
				Background = BuddyTheme.Control,
				CornerRadius = new CornerRadius(15.0)
			});
			_thumb = new Border
			{
				Background = BuddyTheme.SelectedFill,
				BorderBrush = BuddyTheme.SelectedEdge,
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(13.0),
				Margin = new Thickness(2.0),
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				RenderTransform = _move
			};
			base.Children.Add(_thumb);
			UniformGrid uniformGrid = new UniformGrid
			{
				Rows = 1,
				Columns = labels.Length,
				Margin = new Thickness(2.0)
			};
			_labels = new TextBlock[labels.Length];
			for (int i = 0; i < labels.Length; i++)
			{
				int idx = i;
				TextBlock t = Txt(labels[i], 13.0, BuddyTheme.Text50, AynType.Medium);
				t.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
				t.VerticalAlignment = VerticalAlignment.Center;
				Border border = new Border
				{
					Background = Brushes.Transparent,
					Cursor = System.Windows.Input.Cursors.Hand,
					Child = t
				};
				border.MouseEnter += delegate
				{
					if (idx != _index)
					{
						t.Foreground = BuddyTheme.Text92;
					}
				};
				border.MouseLeave += delegate
				{
					if (idx != _index)
					{
						t.Foreground = BuddyTheme.Text50;
					}
				};
				border.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
				};
				border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
					if (idx != _index)
					{
						Select(idx, animate: true);
						this.Selected?.Invoke(idx);
					}
				};
				_labels[i] = t;
				uniformGrid.Children.Add(border);
			}
			base.Children.Add(uniformGrid);
			base.SizeChanged += delegate
			{
				Place(animate: false);
			};
		}

		public void Select(int index, bool animate)
		{
			if (index < 0)
			{
				index = 0;
			}
			_index = index;
			for (int i = 0; i < _labels.Length; i++)
			{
				_labels[i].Foreground = ((i == index) ? BuddyTheme.SelectedInk : BuddyTheme.Text50);
			}
			Place(animate);
		}

		private void Place(bool animate)
		{
			double num = (base.ActualWidth - 4.0) / (double)_labels.Length;
			if (!(num <= 0.0))
			{
				_thumb.Width = num;
				double num2 = num * (double)Math.Max(0, _index);
				if (animate)
				{
					_move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(num2, new Duration(TimeSpan.FromMilliseconds(150.0)))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseOut
						}
					});
				}
				else
				{
					_move.BeginAnimation(TranslateTransform.XProperty, null);
					_move.X = num2;
				}
			}
		}
	}

	private sealed class QualitySlider : Grid
	{
		private readonly Rectangle _fill;

		private readonly Ellipse _knob;

		private int _value = 80;

		public int Value
		{
			get
			{
				return _value;
			}
			set
			{
				_value = Math.Max(1, Math.Min(100, value));
				Place();
			}
		}

		public event Action<int> ValueChanged;

		public QualitySlider()
		{
			base.Height = 20.0;
			base.Background = Brushes.Transparent;
			base.Cursor = System.Windows.Input.Cursors.Hand;
			base.Focusable = true;
			base.FocusVisualStyle = null;
			base.Children.Add(new Rectangle
			{
				Height = 4.0,
				RadiusX = 2.0,
				RadiusY = 2.0,
				Fill = BuddyTheme.Track,
				Margin = new Thickness(0.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false
			});
			_fill = new Rectangle
			{
				Height = 4.0,
				RadiusX = 2.0,
				RadiusY = 2.0,
				Fill = BuddyTheme.Mark,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false
			};
			base.Children.Add(_fill);
			_knob = new Ellipse
			{
				Width = 12.0,
				Height = 12.0,
				Fill = BuddyTheme.Text100,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false
			};
			base.Children.Add(_knob);
			SizeChangedEventHandler value = delegate
			{
				Place();
			};
			base.SizeChanged += value;
			base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				Focus();
				CaptureMouse();
				SetFromX(e.GetPosition(this).X);
			};
			base.MouseMove += delegate(object s, System.Windows.Input.MouseEventArgs e)
			{
				if (base.IsMouseCaptured)
				{
					SetFromX(e.GetPosition(this).X);
				}
			};
			base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				e.Handled = true;
				if (base.IsMouseCaptured)
				{
					ReleaseMouseCapture();
				}
			};
			base.MouseWheel += delegate(object s, MouseWheelEventArgs e)
			{
				e.Handled = true;
				Set(_value + ((e.Delta > 0) ? 1 : (-1)));
			};
			base.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
			{
				if (e.Key == Key.Left || e.Key == Key.Down)
				{
					e.Handled = true;
					Set(_value - 1);
				}
				else if (e.Key == Key.Right || e.Key == Key.Up)
				{
					e.Handled = true;
					Set(_value + 1);
				}
			};
		}

		private void SetFromX(double x)
		{
			double num = Math.Max(1.0, base.ActualWidth - 12.0);
			Set((int)Math.Round(1.0 + (x - 6.0) / num * 99.0));
		}

		private void Set(int v)
		{
			v = Math.Max(1, Math.Min(100, v));
			if (v != _value)
			{
				_value = v;
				Place();
				this.ValueChanged?.Invoke(v);
			}
		}

		private void Place()
		{
			double num = Math.Max(0.0, base.ActualWidth - 12.0);
			double num2 = num * (double)(_value - 1) / 99.0;
			_knob.Margin = new Thickness(num2, 0.0, 0.0, 0.0);
			_fill.Width = num2 + 6.0;
		}
	}

	private sealed class ChoiceMenu
	{
		public sealed class Item
		{
			public readonly string Text;

			public readonly bool Current;

			public readonly Action Pick;

			public Item(string text, bool current, Action pick)
			{
				Text = text;
				Current = current;
				Pick = pick;
			}
		}

		private readonly Popup _popup;

		private readonly StackPanel _list;

		public bool IsOpen => _popup.IsOpen;

		public ChoiceMenu()
		{
			_list = new StackPanel();
			Border border = new Border
			{
				Background = BuddyTheme.Control,
				CornerRadius = new CornerRadius(12.0),
				Padding = new Thickness(6.0),
				MinWidth = 180.0,
				Child = _list
			};
			RenderOptions.SetClearTypeHint(border, ClearTypeHint.Enabled);
			TextOptions.SetTextFormattingMode(border, TextFormattingMode.Display);
			_popup = new Popup
			{
				Child = border,
				AllowsTransparency = true,
				StaysOpen = false,
				Placement = PlacementMode.Bottom,
				VerticalOffset = 4.0,
				PopupAnimation = PopupAnimation.Fade
			};
		}

		public void Open(FrameworkElement anchor, IList<Item> items, string caption)
		{
			_list.Children.Clear();
			if (caption != null)
			{
				TextBlock textBlock = Txt(caption, 11.5, BuddyTheme.Text30, FontWeights.Medium);
				textBlock.Margin = new Thickness(8.0, 4.0, 8.0, 4.0);
				_list.Children.Add(textBlock);
			}
			foreach (Item item2 in items)
			{
				Item item = item2;
				Grid grid = new Grid();
				grid.Height = 28.0;
				Grid grid2 = grid;
				Border bg = new Border
				{
					CornerRadius = new CornerRadius(8.0),
					Background = Brushes.Transparent,
					Cursor = System.Windows.Input.Cursors.Hand,
					Padding = new Thickness(8.0, 0.0, 8.0, 0.0),
					Child = grid2
				};
				TextBlock textBlock2 = Txt(item.Text, 13.0, item.Current ? BuddyTheme.Text100 : BuddyTheme.Text92, FontWeights.Medium);
				textBlock2.VerticalAlignment = VerticalAlignment.Center;
				grid2.Children.Add(textBlock2);
				if (item.Current)
				{
					grid2.Children.Add(new ContentControl
					{
						Width = 12.0,
						Height = 12.0,
						HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
						VerticalAlignment = VerticalAlignment.Center,
						Margin = new Thickness(16.0, 0.0, 0.0, 0.0),
						IsHitTestVisible = false,
						Content = FluentIcons.Create("checkmark", 12.0, BuddyTheme.Text100, filled: false)
					});
				}
				bg.MouseEnter += delegate
				{
					bg.Background = BuddyTheme.ControlActive;
				};
				bg.MouseLeave += delegate
				{
					bg.Background = Brushes.Transparent;
				};
				bg.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
					Close();
					item.Pick();
				};
				_list.Children.Add(bg);
			}
			_popup.PlacementTarget = anchor;
			_popup.HorizontalOffset = 0.0;
			_popup.IsOpen = true;
			_popup.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			_popup.HorizontalOffset = anchor.ActualWidth - _popup.Child.DesiredSize.Width;
		}

		public void Close()
		{
			_popup.IsOpen = false;
		}
	}

	private const double ListW = 300.0;

	private const double SplitPad = 16.0;

	private const double RowH = 44.0;

	private const double OptionRowH = 32.0;

	private static readonly OutFormat[] Formats = new OutFormat[7]
	{
		OutFormat.Jpg,
		OutFormat.Png,
		OutFormat.Pdf,
		OutFormat.Ico,
		OutFormat.Bmp,
		OutFormat.Gif,
		OutFormat.Tiff
	};

	private static readonly int[] ResizePresets = new int[3] { 1024, 1920, 2560 };

	private readonly ConvertSettings _settings;

	private readonly List<FileItem> _items = new List<FileItem>();

	private readonly Dictionary<FileItem, FileRow> _rows = new Dictionary<FileItem, FileRow>();

	private CancellationTokenSource _runCts;

	private CancellationTokenSource _estCts;

	private bool _running;

	private bool _showDone;

	private bool _cancelling;

	private string _summary;

	private string _summaryTip;

	private string _summaryFolder;

	private ConvertEngine.Estimate _estimate;

	private bool _estimating;

	private bool _estimateFailed;

	private string _notice;

	private DispatcherTimer _estTimer;

	private DispatcherTimer _noticeTimer;

	private Grid _root;

	private BuddyLabel _countLabel;

	private Border _clearBtn;

	private StackPanel _listPanel;

	private ScrollViewer _scroll;

	private FrameworkElement _emptyState;

	private FrameworkElement _listArea;

	private FrameworkElement _dropHint;

	private FrameworkElement _dropOverlay;

	private Segmented _segment;

	private TextBlock _modeQuality;

	private TextBlock _modeTarget;

	private TextBlock _modeDot;

	private TextBlock _qualityValue;

	private TextBlock _targetHint;

	private TextBlock _helper;

	private TextBlock _kb;

	private TextBlock _mb;

	private FrameworkElement _sizeBody;

	private FrameworkElement _qualityPanel;

	private FrameworkElement _targetPanel;

	private FrameworkElement _modeToggle;

	private QualitySlider _slider;

	private System.Windows.Controls.TextBox _targetBox;

	private System.Windows.Controls.TextBox _customBox;

	private TextBlock _resizeValue;

	private TextBlock _saveValue;

	private TextBlock _estimateText;

	private TextBlock _pillText;

	private TextBlock _openFolder;

	private FrameworkElement _resizeValueHost;

	private StackPanel _customHost;

	private Border _pill;

	private ChoiceMenu _menu;

	private bool _syncing;

	private bool _dragOver;

	private string _openOutput;

	public string Id => "files";

	public string DisplayName => "File Converter";

	public ToolCategory Category => ToolCategory.Utility;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => false;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public FilesConverterTool()
	{
		IsEnabled = true;
		OrderIndex = 17;
		State = ToolState.Idle;
		_settings = ConvertSettings.Load();
	}

	public void Initialize()
	{
		IsInitialized = true;
		PersistenceHelper.RegisterExitPreparation("files converter", delegate
		{
			if (IsInitialized)
			{
				_settings.Save();
			}
		});
	}

	public void Shutdown()
	{
		if (_runCts != null)
		{
			_runCts.Cancel();
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		if (_menu != null)
		{
			_menu.Close();
		}
		OnFileDragOver(over: false);
		_settings.Save();
	}

	public bool HandleEscape()
	{
		if (_menu != null && _menu.IsOpen)
		{
			_menu.Close();
			return true;
		}
		if (_customHost != null && _customHost.Visibility == Visibility.Visible)
		{
			EndCustom(commit: false);
			return true;
		}
		return false;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return FluentIcons.Create("document_arrow_right", size, stroke ?? BuddyTheme.Text100, filled: false);
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
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Background = Brushes.Transparent,
			Focusable = true,
			FocusVisualStyle = null,
			Tag = "NoOuterScroll"
		});
		root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(300.0)
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
		root.Children.Add(BuildFiles());
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.IsHitTestVisible = false;
		Rectangle element = rectangle;
		Grid.SetColumn(element, 2);
		root.Children.Add(element);
		UIElement element2 = BuildOptions();
		Grid.SetColumn(element2, 4);
		root.Children.Add(element2);
		Border border = new Border();
		border.Background = ThemeTokens.Surface2;
		border.CornerRadius = new CornerRadius(12.0);
		border.IsHitTestVisible = false;
		border.Opacity = 0.0;
		border.Child = Txt("Drop to add", 13.0, BuddyTheme.Text92, FontWeights.Medium);
		Border border2 = border;
		((TextBlock)border2.Child).HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		((TextBlock)border2.Child).VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumnSpan(border2, 5);
		root.Children.Add(border2);
		_dropOverlay = border2;
		root.MouseLeftButtonDown += delegate
		{
			if (!(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
			{
				root.Focus();
			}
		};
		_root = root;
		_estTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(400.0)
		};
		_estTimer.Tick += delegate
		{
			_estTimer.Stop();
			RunEstimate();
		};
		_noticeTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(5.0)
		};
		_noticeTimer.Tick += delegate
		{
			_noticeTimer.Stop();
			_notice = null;
			UpdateFooter();
		};
		SyncOptions();
		RefreshList();
		return root;
	}

	private UIElement BuildFiles()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(24.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(4.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Grid grid2 = new Grid();
		_countLabel = BuddyTheme.LabelText("FILES");
		_countLabel.Foreground = BuddyTheme.Text30;
		_countLabel.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(_countLabel);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		stackPanel.Margin = new Thickness(0.0, 0.0, -4.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		ContentControl glyphHost;
		Border element = BuddyKit.GlyphButton("add", 16.0, "Add files", BrowseFiles, out glyphHost);
		_clearBtn = BuddyKit.GlyphButton("delete", 16.0, "Clear all", ClearAll, out glyphHost);
		_clearBtn.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		stackPanel2.Children.Add(element);
		stackPanel2.Children.Add(_clearBtn);
		grid2.Children.Add(stackPanel2);
		grid.Children.Add(grid2);
		_listPanel = new StackPanel();
		_scroll = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			CanContentScroll = false,
			Focusable = false,
			Content = _listPanel
		};
		_scroll.ScrollChanged += delegate
		{
			UpdateFade();
		};
		_dropHint = BuildDropHint();
		_listArea = _scroll;
		Grid.SetRow(_scroll, 2);
		grid.Children.Add(_scroll);
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = System.Windows.Input.Cursors.Hand;
		Border border2 = border;
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		stackPanel3.Margin = new Thickness(0.0, 0.0, 0.0, 28.0);
		StackPanel stackPanel4 = stackPanel3;
		FrameworkElement frameworkElement = UploadIcon(20.0, BuddyTheme.Text50);
		frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel4.Children.Add(frameworkElement);
		TextBlock et = Txt("Drop files or click to add", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		et.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		et.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel4.Children.Add(et);
		border2.Child = stackPanel4;
		border2.MouseEnter += delegate
		{
			et.Foreground = BuddyTheme.Text92;
		};
		border2.MouseLeave += delegate
		{
			et.Foreground = BuddyTheme.Text50;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			BrowseFiles();
		};
		Grid.SetRow(border2, 1);
		Grid.SetRowSpan(border2, 2);
		grid.Children.Add(border2);
		_emptyState = border2;
		return grid;
	}

	private FrameworkElement BuildDropHint()
	{
		Border border = new Border();
		border.Background = Brushes.Transparent;
		border.Cursor = System.Windows.Input.Cursors.Hand;
		border.Padding = new Thickness(0.0, 14.0, 0.0, 6.0);
		Border border2 = border;
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement frameworkElement = UploadIcon(16.0, BuddyTheme.Text30);
		frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel2.Children.Add(frameworkElement);
		TextBlock t = Txt("Drop more files here", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		t.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		t.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel2.Children.Add(t);
		border2.Child = stackPanel2;
		border2.MouseEnter += delegate
		{
			t.Foreground = BuddyTheme.Text50;
		};
		border2.MouseLeave += delegate
		{
			t.Foreground = BuddyTheme.Text30;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			BrowseFiles();
		};
		return border2;
	}

	private void UpdateFade()
	{
		if (_scroll != null)
		{
			if (!(_scroll.ScrollableHeight > 0.5) || !(_scroll.VerticalOffset < _scroll.ScrollableHeight - 0.5))
			{
				_scroll.OpacityMask = null;
				return;
			}
			double num = Math.Max(1.0, _scroll.ActualHeight);
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.StartPoint = new Point(0.0, 0.0);
			linearGradientBrush.EndPoint = new Point(0.0, 1.0);
			LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, Math.Max(0.0, 1.0 - 20.0 / num)));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
			_scroll.OpacityMask = linearGradientBrush2;
		}
	}

	private UIElement BuildOptions()
	{
		DockPanel dockPanel = new DockPanel();
		dockPanel.LastChildFill = true;
		DockPanel dockPanel2 = dockPanel;
		Grid grid = new Grid();
		grid.Height = 30.0;
		grid.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		Grid grid2 = grid;
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
		_estimateText = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_estimateText.TextTrimming = TextTrimming.CharacterEllipsis;
		_estimateText.VerticalAlignment = VerticalAlignment.Center;
		_estimateText.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		Typography.SetNumeralAlignment(_estimateText, FontNumeralAlignment.Tabular);
		grid2.Children.Add(_estimateText);
		_openFolder = Txt("Open folder", 11.5, BuddyTheme.Text92, FontWeights.Medium);
		_openFolder.VerticalAlignment = VerticalAlignment.Center;
		_openFolder.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		_openFolder.Cursor = System.Windows.Input.Cursors.Hand;
		_openFolder.Focusable = true;
		_openFolder.FocusVisualStyle = null;
		_openFolder.Visibility = Visibility.Collapsed;
		_openFolder.MouseEnter += delegate
		{
			_openFolder.TextDecorations = TextDecorations.Underline;
		};
		_openFolder.MouseLeave += delegate
		{
			if (!_openFolder.IsKeyboardFocused)
			{
				_openFolder.TextDecorations = null;
			}
		};
		_openFolder.GotKeyboardFocus += delegate
		{
			_openFolder.TextDecorations = TextDecorations.Underline;
		};
		_openFolder.LostKeyboardFocus += delegate
		{
			_openFolder.TextDecorations = null;
		};
		_openFolder.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			OpenOutputFolder();
		};
		_openFolder.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				OpenOutputFolder();
			}
		};
		Grid.SetColumn(_openFolder, 1);
		grid2.Children.Add(_openFolder);
		_pillText = Txt("Convert", 13.0, BuddyTheme.PrimaryInk, AynType.Medium);
		_pillText.VerticalAlignment = VerticalAlignment.Center;
		_pill = new Border
		{
			Height = 30.0,
			CornerRadius = new CornerRadius(15.0),
			Background = BuddyTheme.PrimaryFill,
			Padding = new Thickness(15.0, 0.0, 15.0, 0.0),
			BorderThickness = new Thickness(1.0),
			BorderBrush = Brushes.Transparent,
			Cursor = System.Windows.Input.Cursors.Hand,
			Focusable = true,
			FocusVisualStyle = null,
			Child = _pillText
		};
		_pill.GotKeyboardFocus += delegate
		{
			_pill.BorderBrush = BuddyTheme.Text100;
		};
		_pill.LostKeyboardFocus += delegate
		{
			_pill.BorderBrush = Brushes.Transparent;
		};
		_pill.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (_pill.IsEnabled && (e.Key == Key.Return || e.Key == Key.Space))
			{
				e.Handled = true;
				OnPill();
			}
		};
		_pill.MouseEnter += delegate
		{
			if (_pill.IsEnabled)
			{
				_pill.Opacity = 0.9;
			}
		};
		_pill.MouseLeave += delegate
		{
			_pill.Opacity = 1.0;
		};
		_pill.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_pill.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			OnPill();
		};
		_pill.IsEnabledChanged += delegate
		{
			_pill.Background = (_pill.IsEnabled ? BuddyTheme.PrimaryFill : ThemeTokens.Surface3);
			_pillText.Foreground = (_pill.IsEnabled ? BuddyTheme.PrimaryInk : ThemeTokens.TextDisabled);
		};
		Grid.SetColumn(_pill, 2);
		grid2.Children.Add(_pill);
		DockPanel.SetDock(grid2, Dock.Bottom);
		dockPanel2.Children.Add(grid2);
		StackPanel stackPanel = new StackPanel();
		dockPanel2.Children.Add(stackPanel);
		stackPanel.Children.Add(Label("FORMAT"));
		_segment = new Segmented(Formats.Select(FileItem.FormatName).ToArray())
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		_segment.Selected += delegate(int i)
		{
			_settings.Format = Formats[i];
			OptionsChanged();
		};
		stackPanel.Children.Add(_segment);
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		grid3.Height = 16.0;
		Grid grid4 = grid3;
		grid4.Children.Add(Label("SIZE"));
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel3 = stackPanel2;
		_modeQuality = ModeText("Quality", SizeMode.Quality);
		_modeDot = Txt("  ·  ", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_modeTarget = ModeText("Target", SizeMode.Target);
		stackPanel3.Children.Add(_modeQuality);
		stackPanel3.Children.Add(_modeDot);
		stackPanel3.Children.Add(_modeTarget);
		grid4.Children.Add(stackPanel3);
		_modeToggle = stackPanel3;
		stackPanel.Children.Add(grid4);
		Grid grid5 = new Grid();
		grid5.Height = 34.0;
		grid5.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		Grid grid6 = grid5;
		Grid grid7 = new Grid();
		grid7.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid7.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(48.0)
		});
		_slider = new QualitySlider
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_slider.ValueChanged += delegate(int v)
		{
			_settings.Quality = v;
			_qualityValue.Text = v + "%";
			OptionsChanged();
		};
		grid7.Children.Add(_slider);
		_qualityValue = Txt("80%", 13.0, BuddyTheme.Text100, AynType.Medium);
		_qualityValue.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		_qualityValue.VerticalAlignment = VerticalAlignment.Center;
		Typography.SetNumeralAlignment(_qualityValue, FontNumeralAlignment.Tabular);
		Grid.SetColumn(_qualityValue, 1);
		grid7.Children.Add(_qualityValue);
		_qualityPanel = grid7;
		grid6.Children.Add(grid7);
		Grid grid8 = new Grid();
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_targetBox = new System.Windows.Controls.TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			FontFamily = BuddyTheme.DisplayFont,
			FontSize = 28.0,
			FontWeight = AynType.Semibold,
			MinWidth = 20.0,
			MaxLength = 7,
			VerticalAlignment = VerticalAlignment.Center,
			FocusVisualStyle = null
		};
		Typography.SetNumeralAlignment(_targetBox, FontNumeralAlignment.Tabular);
		BuddyTheme.ApplyTextMode(_targetBox, 28.0);
		_targetBox.TextChanged += delegate
		{
			OnTargetText();
		};
		_targetBox.PreviewTextInput += delegate(object s, TextCompositionEventArgs e)
		{
			e.Handled = !e.Text.All((char c) => char.IsDigit(c) || c == '.' || c == ',');
		};
		_targetBox.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				_root.Focus();
			}
		};
		_targetBox.LostKeyboardFocus += delegate
		{
			if (!_syncing)
			{
				SyncTargetText();
			}
		};
		_targetBox.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
		{
			e.Handled = true;
			StepTarget((e.Delta > 0) ? 1 : (-1));
		};
		grid8.Children.Add(_targetBox);
		StackPanel stackPanel4 = new StackPanel();
		stackPanel4.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel4.VerticalAlignment = VerticalAlignment.Center;
		stackPanel4.Margin = new Thickness(12.0, 2.0, 0.0, 0.0);
		StackPanel stackPanel5 = stackPanel4;
		stackPanel5.Children.Add(new Rectangle
		{
			Width = 1.0,
			Height = 24.0,
			Fill = BuddyTheme.Hairline,
			Margin = new Thickness(0.0, 0.0, 14.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		_kb = UnitText("KB", mb: false);
		_mb = UnitText("MB", mb: true);
		stackPanel5.Children.Add(_kb);
		stackPanel5.Children.Add(new Rectangle
		{
			Width = 1.0,
			Height = 14.0,
			Fill = BuddyTheme.Hairline,
			Margin = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		stackPanel5.Children.Add(_mb);
		Grid.SetColumn(stackPanel5, 1);
		grid8.Children.Add(stackPanel5);
		_targetHint = Txt("", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_targetHint.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		_targetHint.VerticalAlignment = VerticalAlignment.Center;
		_targetHint.Margin = new Thickness(12.0, 2.0, 0.0, 0.0);
		_targetHint.TextTrimming = TextTrimming.CharacterEllipsis;
		Typography.SetNumeralAlignment(_targetHint, FontNumeralAlignment.Tabular);
		Grid.SetColumn(_targetHint, 2);
		grid8.Children.Add(_targetHint);
		_targetPanel = grid8;
		grid6.Children.Add(grid8);
		_sizeBody = grid6;
		stackPanel.Children.Add(grid6);
		_helper = Txt("", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_helper.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		_helper.Height = 14.0;
		_helper.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel.Children.Add(_helper);
		_resizeValue = Txt("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
		_customBox = new System.Windows.Controls.TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
			BorderBrush = BuddyTheme.Hairline,
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			FontFamily = BuddyTheme.Font,
			FontSize = 13.0,
			FontWeight = AynType.Medium,
			Width = 52.0,
			MaxLength = 5,
			Padding = new Thickness(0.0, 0.0, 0.0, 1.0),
			TextAlignment = TextAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			FocusVisualStyle = null
		};
		BuddyTheme.ApplyTextMode(_customBox, 13.0);
		_customBox.PreviewTextInput += delegate(object s, TextCompositionEventArgs e)
		{
			e.Handled = !e.Text.All(char.IsDigit);
		};
		_customBox.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				EndCustom(commit: true);
			}
			else if (e.Key == Key.Escape)
			{
				e.Handled = true;
				EndCustom(commit: false);
			}
		};
		_customBox.LostKeyboardFocus += delegate
		{
			if (_customHost.Visibility == Visibility.Visible)
			{
				EndCustom(commit: true);
			}
		};
		_customHost = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			Visibility = Visibility.Collapsed,
			VerticalAlignment = VerticalAlignment.Center
		};
		_customHost.Children.Add(_customBox);
		TextBlock textBlock = Txt("px long edge", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		_customHost.Children.Add(textBlock);
		FrameworkElement valueHost;
		FrameworkElement frameworkElement = OptionRow("RESIZE", _resizeValue, ShowResizeMenu, out valueHost);
		_resizeValueHost = valueHost;
		((Grid)frameworkElement).Children.Add(_customHost);
		_customHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		frameworkElement.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		stackPanel.Children.Add(frameworkElement);
		_saveValue = Txt("", 13.0, BuddyTheme.Text92, FontWeights.Medium);
		stackPanel.Children.Add(OptionRow("SAVE TO", _saveValue, ShowSaveMenu, out var _));
		_menu = new ChoiceMenu();
		return dockPanel2;
	}

	private BuddyLabel Label(string text)
	{
		BuddyLabel buddyLabel = BuddyTheme.LabelText(text);
		buddyLabel.VerticalAlignment = VerticalAlignment.Center;
		buddyLabel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		return buddyLabel;
	}

	private TextBlock ModeText(string text, SizeMode mode)
	{
		TextBlock t = Txt(text, 13.0, BuddyTheme.Text50, FontWeights.Medium);
		t.Cursor = System.Windows.Input.Cursors.Hand;
		t.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (_settings.Mode != mode)
			{
				_settings.Mode = mode;
				OptionsChanged();
			}
		};
		t.MouseEnter += delegate
		{
			if (_settings.Mode != mode)
			{
				t.Foreground = BuddyTheme.Text92;
			}
		};
		t.MouseLeave += delegate
		{
			SyncOptions();
		};
		return t;
	}

	private TextBlock UnitText(string text, bool mb)
	{
		TextBlock textBlock = Txt(text, 13.0, BuddyTheme.Text30, AynType.Medium);
		textBlock.Cursor = System.Windows.Input.Cursors.Hand;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (_settings.TargetInMB != mb)
			{
				double num = _settings.TargetBytes;
				_settings.TargetInMB = mb;
				_settings.TargetValue = (mb ? Math.Round(num / 1048576.0, 2) : Math.Round(num / 1024.0));
				if (_settings.TargetValue <= 0.0)
				{
					_settings.TargetValue = (mb ? 0.1 : 1.0);
				}
				SyncTargetText();
				OptionsChanged();
			}
		};
		return textBlock;
	}

	private FrameworkElement OptionRow(string label, TextBlock value, Action<FrameworkElement> open, out FrameworkElement valueHost)
	{
		Grid grid = new Grid();
		grid.Height = 32.0;
		grid.Background = Brushes.Transparent;
		Grid grid2 = grid;
		grid2.Children.Add(new Rectangle
		{
			Height = 1.0,
			Fill = BuddyTheme.Hairline,
			VerticalAlignment = VerticalAlignment.Top,
			IsHitTestVisible = false
		});
		grid2.Children.Add(Label(label));
		StackPanel host = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Stretch,
			Background = Brushes.Transparent,
			Cursor = System.Windows.Input.Cursors.Hand
		};
		value.VerticalAlignment = VerticalAlignment.Center;
		value.MaxWidth = 220.0;
		value.TextTrimming = TextTrimming.CharacterEllipsis;
		host.Children.Add(value);
		ContentControl contentControl = new ContentControl();
		contentControl.Width = 12.0;
		contentControl.Height = 12.0;
		contentControl.Margin = new Thickness(6.0, 1.0, 0.0, 0.0);
		contentControl.IsHitTestVisible = false;
		contentControl.VerticalAlignment = VerticalAlignment.Center;
		contentControl.Content = FluentIcons.Create("chevron_down", 12.0, BuddyTheme.Text50, filled: false);
		ContentControl element = contentControl;
		host.Children.Add(element);
		host.MouseEnter += delegate
		{
			value.Foreground = BuddyTheme.Text100;
		};
		host.MouseLeave += delegate
		{
			value.Foreground = BuddyTheme.Text92;
		};
		host.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		host.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			open(host);
		};
		grid2.Children.Add(host);
		valueHost = host;
		return grid2;
	}

	private void OptionsChanged()
	{
		_showDone = false;
		SyncOptions();
		UpdateRows();
		ScheduleEstimate();
	}

	private void SyncOptions()
	{
		if (_root == null && _segment == null)
		{
			return;
		}
		_syncing = true;
		try
		{
			_segment.Select(Array.IndexOf(Formats, _settings.Format), _segment.IsLoaded);
			bool isLossless = _settings.IsLossless;
			bool flag = _settings.Mode == SizeMode.Target;
			_modeQuality.Foreground = ((!flag) ? BuddyTheme.Text100 : BuddyTheme.Text50);
			_modeTarget.Foreground = (flag ? BuddyTheme.Text100 : BuddyTheme.Text50);
			_modeQuality.FontWeight = ((!flag) ? AynType.Medium : AynType.Regular);
			_modeTarget.FontWeight = (flag ? AynType.Medium : AynType.Regular);
			_qualityPanel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			_targetPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			_sizeBody.Opacity = (isLossless ? 0.4 : 1.0);
			_sizeBody.IsHitTestVisible = !isLossless;
			_modeToggle.Opacity = (isLossless ? 0.4 : 1.0);
			_modeToggle.IsHitTestVisible = !isLossless;
			_slider.Value = _settings.Quality;
			_qualityValue.Text = _settings.Quality + "%";
			_kb.Foreground = (_settings.TargetInMB ? BuddyTheme.Text30 : BuddyTheme.Text100);
			_mb.Foreground = (_settings.TargetInMB ? BuddyTheme.Text100 : BuddyTheme.Text30);
			if (!_targetBox.IsKeyboardFocused)
			{
				SyncTargetText();
			}
			_helper.Text = (isLossless ? "PNG and ICO are lossless — use Resize to reduce size" : ((!flag) ? NamingRule() : ((_settings.Format == OutFormat.Pdf) ? "Target is for the whole PDF; quality adjusts automatically." : "Quality is adjusted automatically to hit the target.")));
			_resizeValue.Text = ((_settings.ResizeLongEdge <= 0) ? "Original" : (_settings.ResizeLongEdge + " px"));
			_saveValue.Text = SaveLabel();
			UpdateTargetHint();
		}
		finally
		{
			_syncing = false;
		}
	}

	private void SyncTargetText()
	{
		_syncing = true;
		_targetBox.Text = _settings.TargetValue.ToString(_settings.TargetInMB ? "0.##" : "0", CultureInfo.InvariantCulture);
		_syncing = false;
	}

	private void OnTargetText()
	{
		if (!_syncing)
		{
			string s = _targetBox.Text.Replace(',', '.');
			if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && result > 0.0 && result < 100000.0)
			{
				_settings.TargetValue = result;
				_showDone = false;
				UpdateTargetHint();
				ScheduleEstimate();
			}
		}
	}

	private void StepTarget(int dir)
	{
		double num = (_settings.TargetInMB ? 0.1 : ((double)((_settings.TargetValue >= 1000.0) ? 100 : ((_settings.TargetValue >= 100.0) ? 50 : 10))));
		double num2 = Math.Max(num, _settings.TargetValue + (double)dir * num);
		_settings.TargetValue = (_settings.TargetInMB ? Math.Round(num2, 2) : Math.Round(num2));
		SyncTargetText();
		OptionsChanged();
	}

	private void UpdateTargetHint()
	{
		if (_targetHint == null)
		{
			return;
		}
		List<FileItem> list = Pending();
		if (list.Count == 0)
		{
			_targetHint.Text = ((_settings.Format == OutFormat.Pdf) ? "whole PDF" : "per file");
			return;
		}
		bool flag = _settings.Format == OutFormat.Pdf;
		long num = (flag ? list.Sum((FileItem i) => i.SizeBytes) : list[0].SizeBytes);
		long targetBytes = _settings.TargetBytes;
		string text = (flag ? "whole PDF" : "per file") + " · from " + FileItem.FormatBytes(num);
		if (num > 0 && targetBytes < num)
		{
			object obj = text;
			text = string.Concat(obj, " (−", Math.Round((1.0 - (double)targetBytes / (double)num) * 100.0), "%)");
		}
		_targetHint.Text = text;
	}

	private string NamingRule()
	{
		string text = ((_settings.SaveTo == SaveTarget.SameFolder) ? "Saved next to each original" : ("Saved in " + SaveLabel()));
		return text + " · never overwrites (adds \"(1)\")";
	}

	private string SaveLabel()
	{
		switch (_settings.SaveTo)
		{
		case SaveTarget.Downloads:
			return "Downloads";
		case SaveTarget.Custom:
		{
			object obj;
			if (!string.IsNullOrEmpty(_settings.CustomFolder))
			{
				obj = System.IO.Path.GetFileName(_settings.CustomFolder.TrimEnd('\\'));
				if (obj == null)
				{
					return _settings.CustomFolder;
				}
			}
			else
			{
				obj = "Folder";
			}
			return (string)obj;
		}
		default:
			return "Same folder";
		}
	}

	private void ShowResizeMenu(FrameworkElement anchor)
	{
		List<ChoiceMenu.Item> list = new List<ChoiceMenu.Item>();
		list.Add(new ChoiceMenu.Item("Original", _settings.ResizeLongEdge <= 0, delegate
		{
			SetResize(0);
		}));
		List<ChoiceMenu.Item> list2 = list;
		bool flag = false;
		int[] resizePresets = ResizePresets;
		foreach (int num in resizePresets)
		{
			int v = num;
			bool flag2 = _settings.ResizeLongEdge == num;
			flag = flag || flag2;
			list2.Add(new ChoiceMenu.Item(num + " px", flag2, delegate
			{
				SetResize(v);
			}));
		}
		bool flag3 = _settings.ResizeLongEdge > 0 && !flag;
		list2.Add(new ChoiceMenu.Item(flag3 ? ("Custom · " + _settings.ResizeLongEdge + " px") : "Custom…", flag3, BeginCustom));
		_menu.Open(anchor, list2, "Long edge · never upscales");
	}

	private void SetResize(int px)
	{
		_settings.ResizeLongEdge = px;
		OptionsChanged();
	}

	private void BeginCustom()
	{
		_customBox.Text = ((_settings.ResizeLongEdge > 0) ? _settings.ResizeLongEdge : _settings.CustomLongEdge).ToString(CultureInfo.InvariantCulture);
		_resizeValueHost.Visibility = Visibility.Collapsed;
		_customHost.Visibility = Visibility.Visible;
		_customBox.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			_customBox.Focus();
			Keyboard.Focus(_customBox);
			_customBox.SelectAll();
		});
	}

	private void EndCustom(bool commit)
	{
		if (_customHost.Visibility == Visibility.Visible)
		{
			_customHost.Visibility = Visibility.Collapsed;
			_resizeValueHost.Visibility = Visibility.Visible;
			if (commit && int.TryParse(_customBox.Text, out var result) && result >= 16)
			{
				result = Math.Min(result, 30000);
				_settings.CustomLongEdge = result;
				_settings.ResizeLongEdge = result;
				OptionsChanged();
			}
			if (_root != null && _customBox.IsKeyboardFocused)
			{
				_root.Focus();
			}
		}
	}

	private void ShowSaveMenu(FrameworkElement anchor)
	{
		List<ChoiceMenu.Item> list = new List<ChoiceMenu.Item>();
		list.Add(new ChoiceMenu.Item("Same folder", _settings.SaveTo == SaveTarget.SameFolder, delegate
		{
			_settings.SaveTo = SaveTarget.SameFolder;
			OptionsChanged();
		}));
		list.Add(new ChoiceMenu.Item("Downloads", _settings.SaveTo == SaveTarget.Downloads, delegate
		{
			_settings.SaveTo = SaveTarget.Downloads;
			OptionsChanged();
		}));
		List<ChoiceMenu.Item> list2 = list;
		if (!string.IsNullOrEmpty(_settings.CustomFolder) && Directory.Exists(_settings.CustomFolder))
		{
			list2.Add(new ChoiceMenu.Item(System.IO.Path.GetFileName(_settings.CustomFolder.TrimEnd('\\')), _settings.SaveTo == SaveTarget.Custom, delegate
			{
				_settings.SaveTo = SaveTarget.Custom;
				OptionsChanged();
			}));
		}
		list2.Add(new ChoiceMenu.Item("Choose folder…", current: false, ChooseFolder));
		_menu.Open(anchor, list2, "Originals are never changed or overwritten");
	}

	private void ChooseFolder()
	{
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "Save converted files to";
		folderBrowserDialog.ShowNewFolderButton = true;
		using FolderBrowserDialog folderBrowserDialog2 = folderBrowserDialog;
		if (!string.IsNullOrEmpty(_settings.CustomFolder) && Directory.Exists(_settings.CustomFolder))
		{
			folderBrowserDialog2.SelectedPath = _settings.CustomFolder;
		}
		if (folderBrowserDialog2.ShowDialog() == DialogResult.OK && Directory.Exists(folderBrowserDialog2.SelectedPath))
		{
			_settings.CustomFolder = folderBrowserDialog2.SelectedPath;
			_settings.SaveTo = SaveTarget.Custom;
			OptionsChanged();
		}
	}

	private void BrowseFiles()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.Multiselect = true;
		openFileDialog.Filter = "Images and PDFs|*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic;*.heif;*.ico;*.pdf|All files|*.*";
		openFileDialog.Title = "Add files to convert";
		Microsoft.Win32.OpenFileDialog openFileDialog2 = openFileDialog;
		if (openFileDialog2.ShowDialog() == true)
		{
			AddFiles(openFileDialog2.FileNames);
		}
	}

	public void OnFileDragOver(bool over)
	{
		if (_dropOverlay != null && over != _dragOver)
		{
			_dragOver = over;
			_dropOverlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(over ? 1.0 : 0.0, BuddyTheme.Hover));
		}
	}

	public void SelectWidgetFormat(string format)
	{
		OutFormat format2 = (OutFormat)Enum.Parse(typeof(OutFormat), format, ignoreCase: true);
		_settings.Format = format2;
		_settings.Save();
		OptionsChanged();
	}

	public void OnFilesDropped(string[] paths)
	{
		AddFiles(paths);
	}

	public void AddFiles(string[] paths)
	{
		if (paths == null)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (string raw in paths)
		{
			if (string.IsNullOrEmpty(raw))
			{
				continue;
			}
			if (!ConvertEngine.IsSupported(raw))
			{
				num++;
			}
			else
			{
				if (_items.Any((FileItem i) => string.Equals(i.Path, raw, StringComparison.OrdinalIgnoreCase)))
				{
					continue;
				}
				if (_items.Count >= 50)
				{
					num2++;
					continue;
				}
				string text = System.IO.Path.GetExtension(raw).ToLowerInvariant();
				FileItem item = new FileItem
				{
					Path = raw,
					Name = System.IO.Path.GetFileName(raw),
					Ext = text,
					IsPdf = (text == ".pdf")
				};
				try
				{
					item.SizeBytes = new FileInfo(raw).Length;
				}
				catch
				{
				}
				_items.Add(item);
				Task.Run(delegate
				{
					ConvertEngine.Probe(item);
				}).ContinueWith(delegate
				{
					Ui(delegate
					{
						if (item.InputError != null)
						{
							item.State = ItemState.Failed;
							item.Error = item.InputError;
						}
						UpdateRow(item);
						UpdateHeader();
						UpdateTargetHint();
						ScheduleEstimate();
					});
				});
			}
		}
		if (num2 > 0)
		{
			Notice("Up to " + 50 + " files at a time");
		}
		else if (num > 0)
		{
			Notice(((num == 1) ? "1 file isn't supported" : (num + " files aren't supported")) + " · try JPG, PNG, HEIC, PDF");
		}
		_showDone = false;
		RefreshList();
		ScheduleEstimate();
	}

	private void Remove(FileItem item)
	{
		if (!_running || item.State != ItemState.Converting)
		{
			_items.Remove(item);
			_rows.Remove(item);
			RefreshList();
			ScheduleEstimate();
		}
	}

	private void ClearAll()
	{
		if (_runCts != null)
		{
			_runCts.Cancel();
		}
		_items.Clear();
		_rows.Clear();
		_showDone = false;
		_estimate = null;
		RefreshList();
	}

	private List<FileItem> Pending()
	{
		return _items.Where((FileItem i) => i.InputError == null && (i.State == ItemState.Queued || i.State == ItemState.Failed)).ToList();
	}

	private void RefreshList()
	{
		if (_listPanel == null)
		{
			return;
		}
		_listPanel.Children.Clear();
		foreach (FileItem item in _items)
		{
			if (!_rows.TryGetValue(item, out var value))
			{
				FileItem it = item;
				value = new FileRow(delegate
				{
					Remove(it);
				}, delegate
				{
					Retry(it);
				}, delegate
				{
					Reveal(it);
				});
				_rows[item] = value;
			}
			_listPanel.Children.Add(value);
			value.Update(item, _running, _settings.Format);
		}
		if (_items.Count > 0)
		{
			_listPanel.Children.Add(_dropHint);
		}
		bool flag = _items.Count == 0;
		_emptyState.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_listArea.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		UpdateHeader();
		UpdateTargetHint();
		UpdateFooter();
		_scroll.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateFade));
	}

	private void UpdateRow(FileItem item)
	{
		if (_rows.TryGetValue(item, out var value))
		{
			value.Update(item, _running, _settings.Format);
		}
	}

	private void UpdateRows()
	{
		foreach (FileItem item in _items)
		{
			UpdateRow(item);
		}
	}

	private void UpdateHeader()
	{
		if (_countLabel != null)
		{
			int count = _items.Count;
			_countLabel.Text = ((count == 0) ? "FILES" : (count + ((count == 1) ? " FILE · " : " FILES · ") + FileItem.FormatBytes(_items.Sum((FileItem i) => i.SizeBytes)).ToUpperInvariant()));
			_clearBtn.Visibility = ((count == 0) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void Retry(FileItem item)
	{
		if (item.InputError == null)
		{
			item.State = ItemState.Queued;
			item.Error = null;
			UpdateRow(item);
			if (!_running)
			{
				StartRun(new List<FileItem> { item });
			}
			else
			{
				UpdateFooter();
			}
		}
	}

	private void Reveal(FileItem item)
	{
		if (item.State != ItemState.Done || item.Outputs.Count == 0)
		{
			return;
		}
		string text = item.Outputs[0];
		try
		{
			if (File.Exists(text))
			{
				Process.Start("explorer.exe", "/select,\"" + text + "\"");
				return;
			}
			item.State = ItemState.Failed;
			item.Error = "Output was moved or deleted";
			UpdateRow(item);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Converter: reveal failed: " + ex.Message);
		}
	}

	private void OnPill()
	{
		if (_running)
		{
			if (_runCts != null)
			{
				_runCts.Cancel();
			}
			_cancelling = true;
			UpdateFooter();
			return;
		}
		List<FileItem> list = Pending();
		if (list.Count == 0)
		{
			list = _items.Where((FileItem i) => i.InputError == null).ToList();
			foreach (FileItem item in list)
			{
				item.State = ItemState.Queued;
				item.Error = null;
				item.Note = null;
			}
		}
		if (list.Count != 0)
		{
			StartRun(list);
		}
	}

	private void StartRun(List<FileItem> batch)
	{
		if (_running || batch.Count == 0)
		{
			return;
		}
		if (_menu != null)
		{
			_menu.Close();
		}
		EndCustom(commit: true);
		_running = true;
		_cancelling = false;
		_showDone = false;
		_summary = (_summaryTip = (_summaryFolder = null));
		_notice = null;
		if (_noticeTimer != null)
		{
			_noticeTimer.Stop();
		}
		CancellationTokenSource cts = new CancellationTokenSource();
		_runCts = cts;
		ConvertSettings s = _settings.Clone();
		foreach (FileItem item in batch)
		{
			item.State = ItemState.Queued;
			item.Progress = 0.0;
			item.Error = null;
			item.Note = null;
		}
		if (s.Format == OutFormat.Pdf)
		{
			foreach (FileItem item2 in batch)
			{
				item2.State = ItemState.Converting;
			}
		}
		UpdateRows();
		UpdateFooter();
		string[] note = new string[1];
		Task.Run(delegate
		{
			string folderProblem = null;
			if (s.SaveTo != 0)
			{
				try
				{
					folderProblem = ConvertEngine.CheckSaveFolder(ConvertEngine.OutputFolder(batch[0], s));
				}
				catch (Exception ex)
				{
					folderProblem = ConvertEngine.Reason(ex, null);
				}
			}
			if (s.Format == OutFormat.Pdf)
			{
				if (folderProblem != null)
				{
					throw new ConvertEngine.SaveFolderException(folderProblem, null);
				}
				string missedNote;
				string result = ConvertEngine.ConvertToPdf(batch, s, delegate(FileItem it, double f)
				{
					Ui(delegate
					{
						it.Progress = f;
						UpdateRow(it);
					});
				}, cts.Token, out missedNote);
				note[0] = missedNote;
				return result;
			}
			SemaphoreSlim gate = new SemaphoreSlim(2);
			try
			{
				Task.WaitAll(batch.Select((FileItem item) => Task.Run(delegate
				{
					RunOne(item, s, gate, cts.Token, folderProblem);
				})).ToArray());
			}
			finally
			{
				if (gate != null)
				{
					((IDisposable)gate).Dispose();
				}
			}
			return (string)null;
		}).ContinueWith(delegate(Task<string> t)
		{
			Ui(delegate
			{
				FinishRun(batch, s, t, note[0], cts);
			});
		});
	}

	private void FinishRun(List<FileItem> batch, ConvertSettings s, Task<string> t, string note, CancellationTokenSource cts)
	{
		bool isCancellationRequested = cts.IsCancellationRequested;
		if (s.Format == OutFormat.Pdf)
		{
			string error = ((t.IsFaulted && !isCancellationRequested) ? ConvertEngine.Reason(t.Exception, ".pdf") : null);
			foreach (FileItem item in batch)
			{
				item.Progress = 0.0;
				if (!t.IsFaulted && !t.IsCanceled && t.Result != null)
				{
					item.State = ItemState.Done;
					item.DoneFormat = OutFormat.Pdf;
					item.Outputs.Clear();
					item.Outputs.Add(t.Result);
					item.Note = note;
				}
				else
				{
					item.State = ((!isCancellationRequested) ? ItemState.Failed : ItemState.Queued);
					item.Error = error;
				}
			}
		}
		_running = false;
		_cancelling = false;
		if (_runCts == cts)
		{
			_runCts = null;
		}
		cts.Dispose();
		BuildSummary(batch, s, isCancellationRequested);
		_showDone = _summary != null;
		UpdateRows();
		UpdateFooter();
		if (!_showDone)
		{
			ScheduleEstimate();
		}
	}

	private void RunOne(FileItem item, ConvertSettings s, SemaphoreSlim gate, CancellationToken ct, string folderProblem)
	{
		bool flag = false;
		try
		{
			gate.Wait(ct);
			flag = true;
			ct.ThrowIfCancellationRequested();
			if (folderProblem != null)
			{
				throw new ConvertEngine.SaveFolderException(folderProblem, null);
			}
			Ui(delegate
			{
				item.State = ItemState.Converting;
				item.Progress = 0.0;
				UpdateRow(item);
				UpdateFooter();
			});
			ConvertEngine.ConvertOne(item, s, delegate(double f)
			{
				Ui(delegate
				{
					if (item.State == ItemState.Converting)
					{
						item.Progress = f;
						UpdateRow(item);
					}
				});
			}, ct);
			Ui(delegate
			{
				item.State = ItemState.Done;
				item.DoneFormat = s.Format;
				item.Progress = 1.0;
				UpdateRow(item);
				UpdateFooter();
			});
		}
		catch (OperationCanceledException)
		{
			Ui(delegate
			{
				item.State = ItemState.Queued;
				item.Progress = 0.0;
				UpdateRow(item);
			});
		}
		catch (Exception ex2)
		{
			string reason = ConvertEngine.Reason(ex2, item.Ext);
			Ui(delegate
			{
				item.State = ItemState.Failed;
				item.Error = reason;
				UpdateRow(item);
				UpdateFooter();
			});
		}
		finally
		{
			if (flag)
			{
				gate.Release();
			}
		}
	}

	private void ScheduleEstimate()
	{
		if (_estTimer != null)
		{
			if (_estCts != null)
			{
				_estCts.Cancel();
			}
			_estimating = true;
			_estTimer.Stop();
			_estTimer.Start();
			UpdateFooter();
		}
	}

	private void RunEstimate()
	{
		_settings.Save();
		List<FileItem> pending = Pending();
		if (pending.Count == 0)
		{
			pending = _items.Where((FileItem i) => i.InputError == null).ToList();
		}
		if (pending.Count == 0 || pending.Any((FileItem i) => !i.Probed))
		{
			_estimating = pending.Count > 0;
			_estimate = null;
			UpdateFooter();
			return;
		}
		if (_estCts != null)
		{
			_estCts.Cancel();
		}
		CancellationTokenSource cts = new CancellationTokenSource();
		_estCts = cts;
		ConvertSettings s = _settings.Clone();
		Task.Run(() => ConvertEngine.EstimateBatch(pending, s, cts.Token), cts.Token).ContinueWith(delegate(Task<ConvertEngine.Estimate> t)
		{
			Ui(delegate
			{
				if (!cts.IsCancellationRequested && _estCts == cts)
				{
					_estimating = false;
					_estimateFailed = t.IsFaulted;
					_estimate = (t.IsFaulted ? null : t.Result);
					if (t.IsFaulted)
					{
						TransactionLogger.Warn("Converter: estimate failed: " + t.Exception.GetBaseException().Message);
					}
					UpdateFooter();
				}
			});
		});
	}

	private void Notice(string text)
	{
		_notice = text;
		if (_noticeTimer != null)
		{
			_noticeTimer.Stop();
			_noticeTimer.Start();
		}
		UpdateFooter();
	}

	private void UpdateFooter()
	{
		if (_pill == null)
		{
			return;
		}
		List<FileItem> list = Pending();
		int num = _items.Count((FileItem i) => i.State == ItemState.Done);
		int num2 = _items.Count((FileItem i) => i.State == ItemState.Failed);
		_openFolder.Visibility = Visibility.Collapsed;
		_estimateText.ToolTip = null;
		_estimateText.Foreground = BuddyTheme.Text50;
		if (_running && _cancelling)
		{
			_pillText.Text = "Cancelling…";
			_pill.IsEnabled = false;
			_estimateText.Text = "Cancelling · finishing the files in progress";
			return;
		}
		if (_running)
		{
			_pillText.Text = "Cancel";
			_pill.IsEnabled = true;
			int num3 = _items.Count((FileItem i) => i.State == ItemState.Converting);
			int val = _items.Count((FileItem i) => i.State == ItemState.Converting || i.State == ItemState.Queued) + num;
			_estimateText.Text = ((_settings.Format == OutFormat.Pdf) ? "Building PDF…" : ("Converting · " + num + " of " + Math.Max(num + num3, val) + " done"));
			return;
		}
		if (list.Count > 0)
		{
			_pillText.Text = "Convert " + list.Count + ((list.Count == 1) ? " file" : " files");
			_pill.IsEnabled = true;
		}
		else if (num > 0)
		{
			_pillText.Text = "Convert again";
			_pill.IsEnabled = true;
		}
		else
		{
			_pillText.Text = "Convert";
			_pill.IsEnabled = false;
		}
		if (_notice != null)
		{
			_estimateText.Text = _notice;
		}
		else if (_showDone && _summary != null)
		{
			_estimateText.Text = _summary;
			_estimateText.Foreground = BuddyTheme.Text92;
			_estimateText.ToolTip = ((_summaryTip == null) ? null : BuddyTooltip.Create(_summaryTip));
			_openFolder.Visibility = ((_summaryFolder == null) ? Visibility.Collapsed : Visibility.Visible);
			if (_summaryFolder != null)
			{
				_openFolder.ToolTip = BuddyTooltip.Create(_summaryFolder);
			}
		}
		else if (_items.Count == 0)
		{
			_estimateText.Text = "Add files first";
		}
		else if (_estimate != null)
		{
			long num4 = _estimate.InputTotal - _estimate.Total;
			_estimateText.Text = "Est. " + FileItem.FormatBytes(_estimate.Total) + " total · " + ((num4 >= 0) ? ("saves " + FileItem.FormatBytes(num4)) : ("+" + FileItem.FormatBytes(-num4)));
			_estimateText.Foreground = (_estimating ? BuddyTheme.Text30 : BuddyTheme.Text50);
		}
		else if (_estimateFailed && !_estimating)
		{
			_estimateText.Text = "Est. unavailable";
			_estimateText.Foreground = BuddyTheme.Text30;
		}
		else if (list.Count == 0 && num == 0)
		{
			_estimateText.Text = ((num2 > 0) ? (num2 + ((num2 == 1) ? " file" : " files") + " can't be converted") : string.Empty);
			_estimateText.Foreground = BuddyTheme.Text30;
		}
		else
		{
			_estimateText.Text = "Est. …";
			_estimateText.Foreground = BuddyTheme.Text30;
		}
	}

	private void BuildSummary(List<FileItem> batch, ConvertSettings s, bool cancelled)
	{
		_summary = (_summaryTip = (_summaryFolder = null));
		if (_items.Count == 0)
		{
			return;
		}
		List<FileItem> list = batch.Where((FileItem i) => i.State == ItemState.Done).ToList();
		List<FileItem> list2 = batch.Where((FileItem i) => i.State == ItemState.Failed).ToList();
		int num = _items.Count((FileItem i) => i.InputError != null && !batch.Contains(i));
		int count = list.Count;
		bool flag = s.Format == OutFormat.Pdf;
		string summary;
		if (cancelled)
		{
			summary = ((count == 0) ? "Cancelled · nothing was converted" : ("Cancelled · " + count + " of " + batch.Count + " converted"));
		}
		else if (count != 0 || list2.Count <= 0)
		{
			summary = ((list2.Count + num > 0) ? (count + " converted · " + (list2.Count + num) + " failed") : ((!flag) ? ((count == 1) ? "1 file converted" : (count + " files converted")) : ("PDF created from " + count + ((count == 1) ? " file" : " files"))));
		}
		else
		{
			string reason = list2[0].Error ?? "";
			bool flag2 = list2.All((FileItem i) => (i.Error ?? "") == reason);
			summary = ((flag2 && reason == "Disk full") ? "Disk full · free some space, then retry" : ((!flag2 || (!reason.StartsWith("Can't write") && !reason.StartsWith("Save folder"))) ? ("Nothing converted · " + list2.Count + " failed") : "Couldn't save · choose another folder"));
		}
		if (count == 0 && !cancelled && list2.Count == 0)
		{
			return;
		}
		_summary = summary;
		List<string> list3 = new List<string>();
		List<string> list4 = list.SelectMany((FileItem i) => i.Outputs).ToList();
		if (list4.Count > 0)
		{
			List<string> list5 = list4.Select((string o) => System.IO.Path.GetDirectoryName(o)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			_summaryFolder = ((list5.Count == 1) ? list5[0] : (list5[0] + " (+" + (list5.Count - 1) + " more)"));
			_openOutput = list4[0];
			list3.Add(((list5.Count == 1) ? ("Saved to " + list5[0]) : ("Saved next to the originals (" + list5.Count + " folders)")) + " · originals are never changed");
			long num2 = list.Sum((FileItem i) => i.SizeBytes);
			long num3 = (flag ? PdfOutputBytes(list) : list.Sum((FileItem i) => i.OutputBytes));
			list3.Add((num3 <= num2) ? ("Saved " + FileItem.FormatBytes(num2 - num3) + " in total") : ("Output is " + FileItem.FormatBytes(num3 - num2) + " larger in total"));
			FileItem fileItem = list.FirstOrDefault((FileItem i) => i.Note != null);
			if (fileItem != null)
			{
				list3.Add(fileItem.Note);
			}
		}
		if (list2.Count + num > 0)
		{
			list3.Add(list2.Count + num + ((list2.Count + num == 1) ? " file" : " files") + " failed · the reason is on each row");
		}
		_summaryTip = string.Join("\n", list3);
	}

	private void OpenOutputFolder()
	{
		string openOutput = _openOutput;
		if (string.IsNullOrEmpty(openOutput))
		{
			return;
		}
		try
		{
			string directoryName = System.IO.Path.GetDirectoryName(openOutput);
			if (File.Exists(openOutput))
			{
				Process.Start("explorer.exe", "/select,\"" + openOutput + "\"");
			}
			else if (Directory.Exists(directoryName))
			{
				Process.Start("explorer.exe", "\"" + directoryName + "\"");
			}
			else
			{
				Notice("The output folder was moved or deleted");
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Converter: open folder failed: " + ex.Message);
			Notice("Couldn't open the folder");
		}
	}

	private static long PdfOutputBytes(List<FileItem> done)
	{
		IEnumerable<string> enumerable = done.SelectMany((FileItem i) => i.Outputs).Distinct(StringComparer.OrdinalIgnoreCase);
		long num = 0L;
		foreach (string item in enumerable)
		{
			try
			{
				num += new FileInfo(item).Length;
			}
			catch
			{
			}
		}
		return num;
	}

	private static TextBlock Txt(string text, double size, Brush ink, FontWeight weight)
	{
		TextBlock textBlock = BuddyTheme.Text(text, size, ink, weight);
		textBlock.TextTrimming = TextTrimming.None;
		return textBlock;
	}

	private static void Ui(Action a)
	{
		System.Windows.Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
		{
			try
			{
				a();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Converter: " + ex.Message);
			}
		});
	}

	private static FrameworkElement UploadIcon(double size, Brush ink)
	{
		System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
		path.Data = Geometry.Parse("M6.5 14.5H5.5C3.57 14.5 2 12.93 2 11C2 9.24 3.3 7.79 5 7.54C5.5 5.24 7.55 3.5 10 3.5C12.45 3.5 14.5 5.24 15 7.54C16.7 7.79 18 9.24 18 11C18 12.93 16.43 14.5 14.5 14.5H13.5M10 9.5V17M7.5 12L10 9.5L12.5 12");
		path.Stroke = ink;
		path.StrokeThickness = 1.25;
		path.StrokeStartLineCap = PenLineCap.Round;
		path.StrokeEndLineCap = PenLineCap.Round;
		path.StrokeLineJoin = PenLineJoin.Round;
		System.Windows.Shapes.Path element = path;
		Canvas canvas = new Canvas();
		canvas.Width = 20.0;
		canvas.Height = 20.0;
		Canvas canvas2 = canvas;
		canvas2.Children.Add(element);
		Viewbox viewbox = new Viewbox();
		viewbox.Width = size;
		viewbox.Height = size;
		viewbox.Child = canvas2;
		viewbox.IsHitTestVisible = false;
		return viewbox;
	}

	internal static string MiddleTrim(string text, double max, TextBlock style)
	{
		if (string.IsNullOrEmpty(text) || max <= 0.0)
		{
			return text;
		}
		Typeface tf = new Typeface(style.FontFamily, style.FontStyle, style.FontWeight, style.FontStretch);
		Func<string, double> func = (string s) => new FormattedText(s, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight, tf, style.FontSize, Brushes.White, 1.0).WidthIncludingTrailingWhitespace;
		if (func(text) <= max)
		{
			return text;
		}
		int num = Math.Min(text.Length / 2, System.IO.Path.GetExtension(text).Length + 4);
		int num2 = 1;
		int num3 = text.Length - num - 1;
		int length = 1;
		while (num2 <= num3)
		{
			int num4 = (num2 + num3) / 2;
			if (func(text.Substring(0, num4) + "…" + text.Substring(text.Length - num)) <= max)
			{
				length = num4;
				num2 = num4 + 1;
			}
			else
			{
				num3 = num4 - 1;
			}
		}
		return text.Substring(0, length).TrimEnd() + "…" + text.Substring(text.Length - num);
	}
}
