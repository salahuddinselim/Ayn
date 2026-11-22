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
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.Tools.AICoding;

public class AICodingTool : IBuddyTool, IEscapeAware, IHeaderStatusLead
{
	private sealed class AgentRow : Border
	{
		public AgentRow(UsageRow r, bool all, long top, Action click, double height)
		{
			AgentRow agentRow = this;
			base.Height = height;
			base.Padding = new Thickness(6.0, 0.0, 8.0, 0.0);
			base.CornerRadius = new CornerRadius(10.0);
			base.Background = Brushes.Transparent;
			CultureInfo currentCulture = CultureInfo.CurrentCulture;
			Grid grid = new Grid
			{
				ColumnDefinitions = 
				{
					new ColumnDefinition
					{
						Width = new GridLength(24.0)
					},
					new ColumnDefinition
					{
						Width = new GridLength(10.0)
					},
					new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					},
					new ColumnDefinition
					{
						Width = new GridLength(12.0)
					},
					new ColumnDefinition
					{
						Width = GridLength.Auto
					}
				}
			};
			FrameworkElement frameworkElement = AgentMarks.Create(r.Agent, 24.0);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			grid.Children.Add(frameworkElement);
			StackPanel stackPanel = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			TextBlock element = Txt(r.Name, 13.0, BuddyTheme.Text92, FontWeights.Medium);
			stackPanel.Children.Add(element);
			Grid bar = new Grid
			{
				Height = 3.0,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
			};
			bar.Children.Add(new Border
			{
				CornerRadius = new CornerRadius(1.5),
				Background = BuddyTheme.Track
			});
			Border fill = new Border
			{
				CornerRadius = new CornerRadius(1.5),
				Background = BuddyTheme.Mark,
				HorizontalAlignment = HorizontalAlignment.Left
			};
			double f = Math.Min(1.0, (double)r.Tokens / (double)top);
			bar.SizeChanged += delegate
			{
				fill.Width = ((r.Tokens > 0) ? Math.Max(3.0, Math.Round(bar.ActualWidth * f)) : 0.0);
			};
			bar.Children.Add(fill);
			stackPanel.Children.Add(bar);
			Grid.SetColumn(stackPanel, 2);
			grid.Children.Add(stackPanel);
			StackPanel stackPanel2 = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			TextBlock textBlock = Txt((r.Tokens > 0) ? Compact(r.Tokens) : "—", 13.0, (r.Tokens > 0) ? BuddyTheme.Text100 : BuddyTheme.Text50, AynType.Medium);
			textBlock.HorizontalAlignment = HorizontalAlignment.Right;
			stackPanel2.Children.Add(textBlock);
			TextBlock textBlock2 = Txt((r.Tokens == 0) ? "No usage today" : ((r.HasUnpriced && r.Cost == 0.0) ? "no price" : ("≈ " + Money(r.Cost))), 11.5, BuddyTheme.Text50, FontWeights.Medium);
			textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
			textBlock2.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock2);
			Grid.SetColumn(stackPanel2, 4);
			grid.Children.Add(stackPanel2);
			Child = grid;
			base.ToolTip = BuddyTooltip.Create(r.Name + " · " + r.Tokens.ToString("N0", currentCulture) + " tokens today" + (r.HasUnpriced ? " · part has no known price" : "") + ((click != null) ? ("\nClick to show only " + r.Name) : ""));
			if (click != null)
			{
				base.Cursor = Cursors.Hand;
				base.MouseEnter += delegate
				{
					base.Background = BuddyTheme.Row;
				};
				base.MouseLeave += delegate
				{
					base.Background = Brushes.Transparent;
				};
				base.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
					click();
				};
			}
		}
	}

	private sealed class TokenBreakdown : StackPanel
	{
		public TokenBreakdown(UsageRow r)
		{
			base.Margin = new Thickness(6.0, 4.0, 8.0, 0.0);
			CultureInfo currentCulture = CultureInfo.CurrentCulture;
			long num = Math.Max(1L, r.Tokens);
			Tuple<string, long>[] array = new Tuple<string, long>[4]
			{
				Tuple.Create("Input", r.Input),
				Tuple.Create("Output", r.Output),
				Tuple.Create("Cache write", r.CacheWrite),
				Tuple.Create("Cache read", r.CacheRead)
			};
			for (int i = 0; i < array.Length; i++)
			{
				string item = array[i].Item1;
				long v = array[i].Item2;
				Grid grid = new Grid
				{
					Height = 14.0,
					Margin = new Thickness(0.0, (i != 0) ? 1 : 0, 0.0, 0.0),
					Background = Brushes.Transparent
				};
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(72.0)
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(52.0)
				});
				TextBlock textBlock = Txt(item, 11.5, BuddyTheme.Text50, FontWeights.Medium);
				textBlock.VerticalAlignment = VerticalAlignment.Center;
				grid.Children.Add(textBlock);
				Grid bar = new Grid
				{
					Height = 2.0,
					VerticalAlignment = VerticalAlignment.Center
				};
				bar.Children.Add(new Border
				{
					CornerRadius = new CornerRadius(1.0),
					Background = BuddyTheme.Track
				});
				Border fill = new Border
				{
					CornerRadius = new CornerRadius(1.0),
					Background = BuddyTheme.Mark,
					HorizontalAlignment = HorizontalAlignment.Left
				};
				double f = (double)v / (double)num;
				bar.SizeChanged += delegate
				{
					fill.Width = ((v > 0) ? Math.Max(2.0, Math.Round(bar.ActualWidth * f)) : 0.0);
				};
				bar.Children.Add(fill);
				Grid.SetColumn(bar, 1);
				grid.Children.Add(bar);
				TextBlock textBlock2 = Txt(Compact(v), 11.5, BuddyTheme.Text92, AynType.Medium);
				textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
				textBlock2.VerticalAlignment = VerticalAlignment.Center;
				Grid.SetColumn(textBlock2, 2);
				grid.Children.Add(textBlock2);
				grid.ToolTip = BuddyTooltip.Create(item + " · " + v.ToString("N0", currentCulture) + " tokens · " + (100.0 * (double)v / (double)num).ToString("0.#", currentCulture) + "% of today");
				base.Children.Add(grid);
			}
		}
	}

	private sealed class FadeList : Grid
	{
		private const double Gap = 2.0;

		private const double Fade = 18.0;

		private readonly StackPanel _stack = new StackPanel();

		private readonly TranslateTransform _y = new TranslateTransform();

		private double _offset;

		private double _target;

		private bool _animating;

		private bool _dragging;

		private Point _dragStart;

		private double _dragOrigin;

		private double Max => Math.Max(0.0, _stack.ActualHeight - base.ActualHeight);

		public FadeList()
		{
			base.ClipToBounds = true;
			base.Background = Brushes.Transparent;
			_stack.VerticalAlignment = VerticalAlignment.Top;
			_stack.RenderTransform = _y;
			base.Children.Add(_stack);
			SizeChangedEventHandler value = delegate
			{
				_target = Clamp(_target);
				Jump(_target);
			};
			base.SizeChanged += value;
			_stack.SizeChanged += delegate
			{
				_target = Clamp(_target);
				Jump(_target);
			};
			base.MouseWheel += delegate(object s, MouseWheelEventArgs e)
			{
				if (!(Max <= 0.0))
				{
					e.Handled = true;
					ScrollTo(_target - (double)e.Delta * 0.4);
				}
			};
			base.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				_dragStart = e.GetPosition(this);
				_dragOrigin = _offset;
				_dragging = false;
			};
			base.PreviewMouseMove += delegate(object s, MouseEventArgs e)
			{
				if (e.LeftButton == MouseButtonState.Pressed && !(Max <= 0.0))
				{
					double num = e.GetPosition(this).Y - _dragStart.Y;
					if (_dragging || !(Math.Abs(num) < 4.0))
					{
						if (!_dragging)
						{
							_dragging = true;
							CaptureMouse();
						}
						Jump(_dragOrigin - num);
					}
				}
			};
			base.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				if (_dragging)
				{
					e.Handled = true;
					_dragging = false;
					ReleaseMouseCapture();
				}
			};
			base.IsManipulationEnabled = true;
			base.ManipulationStarting += delegate(object s, ManipulationStartingEventArgs e)
			{
				e.ManipulationContainer = this;
				e.Mode = ManipulationModes.TranslateY;
				e.Handled = true;
			};
			base.ManipulationDelta += delegate(object s, ManipulationDeltaEventArgs e)
			{
				Jump(_offset - e.DeltaManipulation.Translation.Y);
				e.Handled = true;
			};
			base.ManipulationInertiaStarting += delegate(object s, ManipulationInertiaStartingEventArgs e)
			{
				e.TranslationBehavior.DesiredDeceleration = 0.003;
				e.Handled = true;
			};
		}

		private double Clamp(double v)
		{
			return Math.Max(0.0, Math.Min(Max, v));
		}

		public void SetRows(List<FrameworkElement> rows)
		{
			_stack.Children.Clear();
			for (int i = 0; i < rows.Count; i++)
			{
				if (!(rows[i] is TokenBreakdown))
				{
					rows[i].Margin = new Thickness(0.0, (i == 0) ? 0.0 : 2.0, 0.0, 0.0);
				}
				_stack.Children.Add(rows[i]);
			}
		}

		private void ScrollTo(double y)
		{
			_target = Clamp(y);
			if (!CapsuleMotion.AnimationsEnabled)
			{
				Jump(_target);
			}
			else if (!_animating)
			{
				_animating = true;
				CompositionTarget.Rendering += OnFrame;
			}
		}

		private void OnFrame(object sender, EventArgs e)
		{
			double num = _target - _offset;
			if (Math.Abs(num) < 0.5 || !base.IsVisible)
			{
				_animating = false;
				CompositionTarget.Rendering -= OnFrame;
				Apply(_target);
			}
			else
			{
				Apply(_offset + num * 0.22);
			}
		}

		private void Jump(double y)
		{
			_target = Clamp(y);
			Apply(_target);
		}

		private void Apply(double y)
		{
			_offset = y;
			_y.Y = 0.0 - Math.Round(y);
			double actualHeight = base.ActualHeight;
			bool flag = _offset > 1.0;
			bool flag2 = _offset < Max - 1.0;
			if (actualHeight <= 0.0 || (!flag && !flag2))
			{
				base.OpacityMask = null;
				return;
			}
			double num = Math.Min(0.3, 18.0 / actualHeight);
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.MappingMode = BrushMappingMode.Absolute;
			linearGradientBrush.StartPoint = new Point(0.0, 0.0);
			linearGradientBrush.EndPoint = new Point(0.0, actualHeight);
			LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
			linearGradientBrush2.GradientStops.Add(new GradientStop(flag ? Colors.Transparent : Colors.Black, 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag ? num : 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Colors.Black, flag2 ? (1.0 - num) : 1.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(flag2 ? Colors.Transparent : Colors.Black, 1.0));
			linearGradientBrush2.Freeze();
			base.OpacityMask = linearGradientBrush2;
		}
	}

	private const double LeftW = 420.0;

	private const double TodayH = 126.0;

	private const double MonthH = 90.0;

	private const double BarsH = 43.0;

	private const double StatW = 74.0;

	private const double StatsLead = 12.0;

	private const double CurrentHourMin = 10.0;

	private const string Estimated = "Estimated at API prices";

	private const string EstimatedTip = "Cost is estimated from each model's public API list price per million tokens (input, output, cache write and cache read). It is the ≈ API value of what you used: subscription plans such as Claude Pro / Max or ChatGPT plans aren't billed per token.";

	private const string PlanUsageLabel = "View plan usage ↗";

	private const string PlanUsageUrl = "https://claude.ai/settings/usage";

	private static AICodingToolType _filter;

	private Grid _root;

	private Grid _cards;

	private FrameworkElement _emptyState;

	private EventHandler _updatedHandler;

	private Border _headerActions;

	private StackPanel _headerLead;

	private AICodingToolType? _headerLeadAgent;

	private string _segmentsSig;

	private BuddySlidingSegmented _seg;

	private List<AICodingToolType> _segItems;

	private TextBlock _tHero;

	private TextBlock _tCost;

	private TextBlock _tProject;

	private TextBlock _tDot;

	private TextBlock _tStreak;

	private FrameworkElement _tFolder;

	private FrameworkElement _tFlame;

	private readonly TextBlock[] _tStat = new TextBlock[3];

	private TextBlock _hPeak;

	private readonly Border[] _hBars = new Border[24];

	private readonly FrameworkElement[] _hCols = new FrameworkElement[24];

	private readonly TextBlock[] _hAxis = new TextBlock[4];

	private TextBlock _hEmpty;

	private TextBlock _mLine;

	private TextBlock _mNote;

	private TextBlock _mPct;

	private TextBlock _mPlan;

	private Run _mValue;

	private Run _mBudget;

	private Border _mTrack;

	private Border _mFill;

	private Grid _mBarRow;

	private TextBox _mEdit;

	private FrameworkElement _mEditHost;

	private double _mFraction;

	private FrameworkElement _aHeader;

	private Border _aHeaderHost;

	private FadeList _aList;

	private TextBlock _aEmpty;

	public string Id => "aicoding";

	public string DisplayName => "AI Coding";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => false;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public AICodingTool()
	{
		IsEnabled = true;
		OrderIndex = 24;
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_updatedHandler = delegate
		{
			_root?.Dispatcher.BeginInvoke((Action)delegate
			{
				if (State == ToolState.Active)
				{
					Render();
				}
			});
		};
		AICodingService.Instance.Updated += _updatedHandler;
		ClockSettings.Instance.SettingsChanged += _updatedHandler;
	}

	public void Shutdown()
	{
		if (_updatedHandler != null)
		{
			AICodingService.Instance.Updated -= _updatedHandler;
			ClockSettings.Instance.SettingsChanged -= _updatedHandler;
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		AICodingService.Instance.RequestRefresh();
		Application.Current.Dispatcher.BeginInvoke(new Action(Render), DispatcherPriority.Background);
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		EndBudgetEdit(save: false);
	}

	public bool HandleEscape()
	{
		if (_mEditHost != null && _mEditHost.Visibility == Visibility.Visible)
		{
			EndBudgetEdit(save: false);
			return true;
		}
		return false;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return null;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateCodeIcon(size, stroke);
	}

	public UIElement GetHeaderActions()
	{
		List<AICodingToolType> detectedAgents = AICodingService.Instance.DetectedAgents;
		if (detectedAgents.Count < 2)
		{
			return null;
		}
		if (_headerActions == null)
		{
			_headerActions = new Border
			{
				VerticalAlignment = VerticalAlignment.Center
			};
		}
		SyncFilter(detectedAgents);
		return _headerActions;
	}

	public UIElement GetHeaderStatusLead()
	{
		if (_headerLead == null)
		{
			_headerLead = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
		}
		SyncHeaderLead(AICodingService.Instance.DetectedAgents);
		return _headerLead;
	}

	private void SyncHeaderLead(List<AICodingToolType> detected)
	{
		if (_headerLead == null)
		{
			return;
		}
		AICodingToolType? aICodingToolType = ((detected.Count == 1) ? new AICodingToolType?(detected[0]) : null);
		if (aICodingToolType != _headerLeadAgent)
		{
			_headerLeadAgent = aICodingToolType;
			_headerLead.Children.Clear();
			if (aICodingToolType.HasValue)
			{
				FrameworkElement frameworkElement = AgentMarks.Create(aICodingToolType.Value, 14.0);
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				frameworkElement.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
				_headerLead.Children.Add(frameworkElement);
				TextBlock textBlock = BuddyTheme.Text(AICodingService.AgentName(aICodingToolType.Value) + " · ", 15.0, BuddyTheme.Text50, FontWeights.Medium);
				textBlock.FontFamily = BuddyTheme.DisplayFont;
				textBlock.VerticalAlignment = VerticalAlignment.Center;
				textBlock.TextTrimming = TextTrimming.None;
				_headerLead.Children.Add(textBlock);
			}
		}
		_headerLead.Visibility = ((!aICodingToolType.HasValue) ? Visibility.Collapsed : Visibility.Visible);
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			Render();
			return _root;
		}
		Grid grid = new Grid();
		grid.Width = 728.0;
		grid.Height = 242.0;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		grid.Background = Brushes.Transparent;
		grid.Tag = "NoOuterScroll";
		Grid grid2 = BuddyTheme.Crisp(grid);
		Grid grid3 = new Grid();
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(420.0)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid3.Children.Add(Stack2(126.0, BuildToday(), BuildHours()));
		Grid element = Stack2(90.0, BuildMonth(), BuildAgents());
		Grid.SetColumn(element, 2);
		grid3.Children.Add(element);
		grid2.Children.Add(grid3);
		_cards = grid3;
		TextBlock textBlock = Txt("Use Claude Code or Codex to see your usage here.", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.TextTrimming = TextTrimming.None;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Visibility = Visibility.Collapsed;
		grid2.Children.Add(textBlock);
		_emptyState = textBlock;
		_root = grid2;
		Render();
		return grid2;
	}

	private static Grid Stack2(double topH, UIElement top, UIElement bottom)
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(topH)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(10.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.Children.Add(top);
		Grid.SetRow(bottom, 2);
		grid.Children.Add(bottom);
		return grid;
	}

	private static Border Card(UIElement child)
	{
		Border border = new Border();
		border.Background = BuddyTheme.CardFill;
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(14.0, 14.0, 14.0, 12.0);
		border.Child = child;
		Border border2 = BuddyTheme.Crisp(border);
		RenderOptions.SetClearTypeHint(border2, ClearTypeHint.Enabled);
		Action<Border> action = delegate(Border x)
		{
			x.Effect = (ThemeTokens.IsLight ? ThemeTokens.CardElevation : null);
		};
		action(border2);
		ThemeTokens.Subscribe(border2, action);
		return border2;
	}

	private static TextBlock Txt(string text, double size, Brush ink, FontWeight weight)
	{
		TextBlock textBlock = BuddyTheme.Text(text, size, ink, weight);
		textBlock.TextTrimming = TextTrimming.None;
		Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
		return textBlock;
	}

	private static Grid HeaderRow(string icon, string label, out TextBlock right)
	{
		Grid grid = new Grid();
		grid.Children.Add(BuddyTheme.CardHeader(icon, label));
		right = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		right.HorizontalAlignment = HorizontalAlignment.Right;
		right.VerticalAlignment = VerticalAlignment.Center;
		right.Margin = new Thickness(0.0, -3.0, 0.0, -3.0);
		grid.Children.Add(right);
		return grid;
	}

	private static double Measure(string text, double size, FontWeight weight, FontFamily family)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0.0;
		}
		char[] array = text.ToCharArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (char.IsDigit(array[i]))
			{
				array[i] = '4';
			}
		}
		FormattedText formattedText = new FormattedText(new string(array), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal), size, Brushes.White);
		return Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace) + 1.0;
	}

	private static AICodingToolType Effective(List<AICodingToolType> detected)
	{
		if (detected.Count == 1)
		{
			return detected[0];
		}
		if (_filter != 0 && !detected.Contains(_filter))
		{
			return AICodingToolType.All;
		}
		return _filter;
	}

	private void SetFilter(AICodingToolType t)
	{
		if (_filter != t)
		{
			EndBudgetEdit(save: false);
			_filter = t;
			Render();
		}
	}

	private void SyncFilter(List<AICodingToolType> detected)
	{
		if (_headerActions == null)
		{
			return;
		}
		string text = string.Join(",", detected);
		if (text != _segmentsSig)
		{
			_segmentsSig = text;
			_segItems = new List<AICodingToolType> { AICodingToolType.All };
			_segItems.AddRange(detected);
			List<AICodingToolType> items = _segItems;
			_seg = new BuddySlidingSegmented(items.Select((AICodingToolType t) => AICodingService.AgentName(t)).ToArray(), Math.Max(0, items.IndexOf(Effective(detected))), 28.0, 11.5, 52.0);
			_seg.SelectionChanged += delegate(int i)
			{
				if (i >= 0 && i < items.Count)
				{
					SetFilter(items[i]);
				}
			};
			_headerActions.Child = _seg;
		}
		AICodingToolType item = Effective(detected);
		int num = ((_segItems != null) ? _segItems.IndexOf(item) : (-1));
		if (_seg != null && num >= 0 && _seg.Selected != num)
		{
			_seg.Selected = num;
		}
		if (_headerActions.Parent is FrameworkElement frameworkElement)
		{
			frameworkElement.Visibility = ((detected.Count < 2) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private FrameworkElement BuildToday()
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
		grid.Children.Add(BuddyTheme.CardHeader("database", "Today"));
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		Grid grid3 = grid2;
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		_tHero = Txt("", 28.0, BuddyTheme.Text100, AynType.Semibold);
		_tHero.FontFamily = BuddyTheme.DisplayFont;
		_tHero.LineHeight = 34.0;
		_tHero.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		stackPanel2.Children.Add(_tHero);
		_tCost = Txt("", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		_tCost.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel2.Children.Add(_tCost);
		grid3.Children.Add(stackPanel2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		string[] array = new string[3] { "Messages", "Tool calls", "Sessions" };
		for (int i = 0; i < 3; i++)
		{
			stackPanel4.Children.Add(new Border
			{
				Width = 1.0,
				Height = 34.0,
				Margin = new Thickness((i == 0) ? 12.0 : 0.0, 0.0, 0.0, 0.0)
			});
			StackPanel stackPanel5 = new StackPanel();
			stackPanel5.Width = 74.0;
			stackPanel5.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel6 = stackPanel5;
			_tStat[i] = Txt("0", 20.0, BuddyTheme.Text100, AynType.Semibold);
			_tStat[i].FontFamily = BuddyTheme.DisplayFont;
			_tStat[i].HorizontalAlignment = HorizontalAlignment.Center;
			stackPanel6.Children.Add(_tStat[i]);
			BuddyLabel buddyLabel = BuddyTheme.LabelText(array[i]);
			buddyLabel.FontSize = 10.0;
			buddyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			buddyLabel.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
			stackPanel6.Children.Add(buddyLabel);
			stackPanel4.Children.Add(stackPanel6);
		}
		Grid.SetColumn(stackPanel4, 1);
		grid3.Children.Add(stackPanel4);
		Grid.SetRow(grid3, 1);
		grid.Children.Add(grid3);
		StackPanel stackPanel7 = new StackPanel();
		stackPanel7.Children.Add(new Border
		{
			Height = 1.0,
			Margin = new Thickness(0.0, 6.0, 0.0, 7.0)
		});
		StackPanel stackPanel8 = new StackPanel();
		stackPanel8.Orientation = Orientation.Horizontal;
		StackPanel stackPanel9 = stackPanel8;
		_tFolder = FluentIcons.Create("folder", 12.0, BuddyTheme.Text50, filled: false);
		_tFolder.VerticalAlignment = VerticalAlignment.Center;
		_tFolder.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		stackPanel9.Children.Add(_tFolder);
		_tProject = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_tProject.VerticalAlignment = VerticalAlignment.Center;
		stackPanel9.Children.Add(_tProject);
		_tDot = Txt("  ·  ", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_tDot.VerticalAlignment = VerticalAlignment.Center;
		stackPanel9.Children.Add(_tDot);
		_tFlame = Flame(12.0, BuddyTheme.Text50);
		_tFlame.VerticalAlignment = VerticalAlignment.Center;
		_tFlame.Margin = new Thickness(0.0, 0.0, 5.0, 0.0);
		stackPanel9.Children.Add(_tFlame);
		_tStreak = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_tStreak.VerticalAlignment = VerticalAlignment.Center;
		stackPanel9.Children.Add(_tStreak);
		stackPanel7.Children.Add(stackPanel9);
		Grid.SetRow(stackPanel7, 2);
		grid.Children.Add(stackPanel7);
		return Card(grid);
	}

	private static FrameworkElement Flame(double size, Brush ink)
	{
		Path path = new Path();
		path.Data = Geometry.Parse("M8,1.5 C8,1.5 12.8,5.2 12.8,9.6 C12.8,12.4 10.7,14.5 8,14.5 C5.3,14.5 3.2,12.4 3.2,9.6 C3.2,7.6 4.4,6.1 5.2,5.3 C5.3,6.9 6.2,7.9 7.2,8.1 C6.8,5.6 8,3.2 8,1.5 Z");
		path.Width = size;
		path.Height = size;
		path.Stretch = Stretch.Uniform;
		path.Stroke = ink;
		path.StrokeThickness = 1.1;
		path.StrokeLineJoin = PenLineJoin.Round;
		path.IsHitTestVisible = false;
		return path;
	}

	private FrameworkElement BuildHours()
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
		grid.Children.Add(HeaderRow("data_bar_vertical", "Active hours", out _hPeak));
		Grid grid2 = new Grid();
		grid2.Height = 43.0;
		grid2.VerticalAlignment = VerticalAlignment.Bottom;
		grid2.Margin = new Thickness(-2.0, 8.0, -2.0, 0.0);
		Grid grid3 = grid2;
		Grid grid4 = new Grid();
		grid4.Margin = new Thickness(-2.0, 4.0, -2.0, 0.0);
		Grid grid5 = grid4;
		for (int i = 0; i < 24; i++)
		{
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid5.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Grid grid6 = new Grid();
			grid6.Background = Brushes.Transparent;
			Grid grid7 = grid6;
			_hBars[i] = new Border
			{
				Width = 6.0,
				CornerRadius = new CornerRadius(3.0),
				VerticalAlignment = VerticalAlignment.Bottom,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			grid7.Children.Add(_hBars[i]);
			Grid.SetColumn(grid7, i);
			grid3.Children.Add(grid7);
			_hCols[i] = grid7;
			if (i % 6 == 0)
			{
				TextBlock textBlock = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
				textBlock.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
				Grid.SetColumn(textBlock, i);
				Grid.SetColumnSpan(textBlock, 6);
				grid5.Children.Add(textBlock);
				_hAxis[i / 6] = textBlock;
			}
		}
		Grid.SetRow(grid3, 1);
		grid.Children.Add(grid3);
		_hEmpty = Txt("", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_hEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_hEmpty.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetRow(_hEmpty, 1);
		grid.Children.Add(_hEmpty);
		Grid.SetRow(grid5, 2);
		grid.Children.Add(grid5);
		return Card(grid);
	}

	private FrameworkElement BuildMonth()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(HeaderRow("money", "This month", out _mPlan));
		_mPlan.Text = "View plan usage ↗";
		_mPlan.Cursor = Cursors.Hand;
		_mPlan.ToolTip = BuddyTooltip.Create("Open your Claude plan usage in the browser (https://claude.ai/settings/usage)");
		_mPlan.MouseEnter += delegate
		{
			_mPlan.Foreground = BuddyTheme.Text100;
		};
		_mPlan.MouseLeave += delegate
		{
			_mPlan.Foreground = BuddyTheme.Text50;
		};
		_mPlan.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_mPlan.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			try
			{
				Process.Start(new ProcessStartInfo("https://claude.ai/settings/usage")
				{
					UseShellExecute = true
				});
			}
			catch
			{
			}
		};
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = Orientation.Horizontal;
		stackPanel2.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		stackPanel2.Height = 24.0;
		StackPanel stackPanel3 = stackPanel2;
		_mLine = Txt("", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		_mLine.LineHeight = 24.0;
		_mLine.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		_mLine.VerticalAlignment = VerticalAlignment.Bottom;
		_mValue = new Run
		{
			FontFamily = BuddyTheme.DisplayFont,
			FontSize = 20.0,
			FontWeight = AynType.Semibold,
			Foreground = BuddyTheme.Text100
		};
		_mBudget = new Run
		{
			Cursor = Cursors.Hand
		};
		_mBudget.MouseEnter += delegate
		{
			_mBudget.Foreground = BuddyTheme.Text100;
		};
		_mBudget.MouseLeave += delegate
		{
			_mBudget.Foreground = BuddyTheme.Text50;
		};
		_mBudget.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_mBudget.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			BeginBudgetEdit();
		};
		_mLine.Inlines.Add(_mValue);
		_mLine.Inlines.Add(new Run("  "));
		_mLine.Inlines.Add(_mBudget);
		stackPanel3.Children.Add(_mLine);
		_mEditHost = BuildBudgetEditor();
		stackPanel3.Children.Add(_mEditHost);
		stackPanel.Children.Add(stackPanel3);
		_mBarRow = new Grid
		{
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		_mBarRow.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_mBarRow.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(44.0)
		});
		Grid grid = new Grid();
		grid.Height = 4.0;
		grid.VerticalAlignment = VerticalAlignment.Center;
		Grid grid2 = grid;
		_mTrack = new Border
		{
			CornerRadius = new CornerRadius(2.0),
			Background = BuddyTheme.Track
		};
		_mFill = new Border
		{
			CornerRadius = new CornerRadius(2.0),
			Background = BuddyTheme.Mark,
			HorizontalAlignment = HorizontalAlignment.Left
		};
		grid2.Children.Add(_mTrack);
		grid2.Children.Add(_mFill);
		grid2.SizeChanged += delegate
		{
			SizeMonthBar();
		};
		_mBarRow.Children.Add(grid2);
		_mPct = Txt("", 11.5, BuddyTheme.Text50, FontWeights.Medium);
		_mPct.HorizontalAlignment = HorizontalAlignment.Right;
		_mPct.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_mPct, 1);
		_mBarRow.Children.Add(_mPct);
		stackPanel.Children.Add(_mBarRow);
		_mNote = Txt("Estimated at API prices", 11.5, BuddyTheme.Text30, FontWeights.Medium);
		_mNote.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
		_mNote.ToolTip = BuddyTooltip.Create("Cost is estimated from each model's public API list price per million tokens (input, output, cache write and cache read). It is the ≈ API value of what you used: subscription plans such as Claude Pro / Max or ChatGPT plans aren't billed per token.");
		stackPanel.Children.Add(_mNote);
		return Card(stackPanel);
	}

	private void SizeMonthBar()
	{
		double actualWidth = _mTrack.ActualWidth;
		if (!(actualWidth <= 0.0))
		{
			_mFill.Width = ((_mFraction <= 0.0) ? 0.0 : Math.Max(4.0, Math.Round(actualWidth * Math.Min(1.0, _mFraction))));
		}
	}

	private FrameworkElement BuildBudgetEditor()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.Visibility = Visibility.Collapsed;
		stackPanel.VerticalAlignment = VerticalAlignment.Bottom;
		stackPanel.Margin = new Thickness(8.0, 0.0, 0.0, 3.0);
		StackPanel stackPanel2 = stackPanel;
		TextBlock textBlock = Txt("of $", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Bottom;
		stackPanel2.Children.Add(textBlock);
		_mEdit = new TextBox
		{
			Width = 64.0,
			Background = BuddyTint.Ink(20),
			BorderThickness = new Thickness(0.0),
			BorderBrush = Brushes.Transparent,
			Foreground = BuddyTheme.Text100,
			CaretBrush = BuddyTheme.Text100,
			SelectionBrush = BuddyTheme.Text50,
			SelectionOpacity = 0.45,
			FontFamily = BuddyTheme.Font,
			FontSize = 13.0,
			FontWeight = AynType.Medium,
			Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
			MaxLength = 8,
			FocusVisualStyle = null,
			VerticalAlignment = VerticalAlignment.Bottom
		};
		ControlTemplate controlTemplate = new ControlTemplate(typeof(TextBox));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		frameworkElementFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
		frameworkElementFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6.0));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ScrollViewer));
		frameworkElementFactory2.Name = "PART_ContentHost";
		frameworkElementFactory2.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
		frameworkElementFactory2.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		_mEdit.Template = controlTemplate;
		BuddyTheme.ApplyTextMode(_mEdit, 13.0);
		Typography.SetNumeralAlignment(_mEdit, FontNumeralAlignment.Tabular);
		_mEdit.PreviewTextInput += delegate(object s, TextCompositionEventArgs e)
		{
			e.Handled = !e.Text.All((char c) => char.IsDigit(c) || c == '.' || c == ',');
		};
		_mEdit.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				EndBudgetEdit(save: true);
			}
			else if (e.Key == Key.Escape)
			{
				e.Handled = true;
				EndBudgetEdit(save: false);
			}
		};
		_mEdit.LostKeyboardFocus += delegate
		{
			EndBudgetEdit(save: true);
		};
		stackPanel2.Children.Add(_mEdit);
		TextBlock textBlock2 = Txt(" budget", 13.0, BuddyTheme.Text50, FontWeights.Medium);
		textBlock2.VerticalAlignment = VerticalAlignment.Bottom;
		stackPanel2.Children.Add(textBlock2);
		return stackPanel2;
	}

	private void BeginBudgetEdit()
	{
		List<AICodingToolType> detectedAgents = AICodingService.Instance.DetectedAgents;
		if (detectedAgents.Count != 0)
		{
			double budget = AICodingService.Instance.GetBudget(Effective(detectedAgents));
			_mEdit.Text = ((budget > 0.0) ? budget.ToString("0.##", CultureInfo.CurrentCulture) : "");
			_mBudget.Text = string.Empty;
			_mEditHost.Visibility = Visibility.Visible;
			_mEdit.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
			{
				_mEdit.Focus();
				Keyboard.Focus(_mEdit);
				_mEdit.SelectAll();
			});
		}
	}

	private void EndBudgetEdit(bool save)
	{
		if (_mEditHost == null || _mEditHost.Visibility != 0)
		{
			return;
		}
		_mEditHost.Visibility = Visibility.Collapsed;
		if (save)
		{
			AICodingToolType filter = Effective(AICodingService.Instance.DetectedAgents);
			string text = (_mEdit.Text ?? "").Trim();
			double result;
			if (text.Length == 0)
			{
				AICodingService.Instance.SetBudget(filter, 0.0);
			}
			else if (double.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out result) || double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
			{
				AICodingService.Instance.SetBudget(filter, Math.Max(0.0, Math.Min(1000000.0, result)));
			}
		}
		Render();
	}

	private FrameworkElement BuildAgents()
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
		_aHeaderHost = new Border();
		grid.Children.Add(_aHeaderHost);
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(-6.0, 6.0, -6.0, 0.0);
		Grid grid3 = grid2;
		_aList = new FadeList();
		grid3.Children.Add(_aList);
		_aEmpty = Txt("", 13.0, BuddyTheme.Text30, FontWeights.Medium);
		_aEmpty.HorizontalAlignment = HorizontalAlignment.Center;
		_aEmpty.VerticalAlignment = VerticalAlignment.Center;
		grid3.Children.Add(_aEmpty);
		Grid.SetRow(grid3, 1);
		grid.Children.Add(grid3);
		return Card(grid);
	}

	private void Render()
	{
		List<AICodingToolType> detectedAgents = AICodingService.Instance.DetectedAgents;
		SyncFilter(detectedAgents);
		SyncHeaderLead(detectedAgents);
		if (_root == null)
		{
			return;
		}
		bool flag = detectedAgents.Count == 0;
		_cards.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		_emptyState.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		if (flag)
		{
			EndBudgetEdit(save: false);
			return;
		}
		AICodingToolType aICodingToolType = Effective(detectedAgents);
		AICodingSnapshot snapshot = AICodingService.Instance.GetSnapshot(aICodingToolType);
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		SetHero(snapshot.TokensToday);
		_tHero.ToolTip = BuddyTooltip.Create(snapshot.TokensToday.ToString("N0", currentCulture) + " tokens today: input + output + cache write + cache read");
		SetCost(snapshot);
		_tCost.ToolTip = BuddyTooltip.Create(CostTip(snapshot));
		Set(_tStat[0], Count(snapshot.MessagesToday));
		Set(_tStat[1], Count(snapshot.ToolCallsToday));
		Set(_tStat[2], Count(snapshot.SessionsToday));
		SetFooter(string.IsNullOrEmpty(snapshot.CurrentProject) ? "No project yet" : snapshot.CurrentProject, (snapshot.StreakDays == 0) ? "No streak yet" : (snapshot.StreakDays.ToString(currentCulture) + "-day streak"));
		_tProject.ToolTip = (string.IsNullOrEmpty(snapshot.CurrentProjectPath) ? null : BuddyTooltip.Create(snapshot.CurrentProjectPath));
		_tStreak.ToolTip = BuddyTooltip.Create("Consecutive days with at least one prompt or assistant response, up to today (or yesterday, until today has activity) · " + snapshot.ActiveDays.ToString(currentCulture) + " active day" + ((snapshot.ActiveDays == 1) ? "" : "s") + " in total");
		Set(_hPeak, snapshot.PeakWindow ?? "");
		for (int i = 0; i < 4; i++)
		{
			Set(_hAxis[i], ClockFormat.Hour(i * 6));
		}
		bool flag2 = snapshot.HourlyTokens.Any((long v) => v > 0);
		_hEmpty.Visibility = (flag2 ? Visibility.Collapsed : Visibility.Visible);
		Set(_hEmpty, "No activity yet today");
		long num = Math.Max(1L, snapshot.HourlyTokens.Max());
		int hour = DateTime.Now.Hour;
		for (int j = 0; j < 24; j++)
		{
			long num2 = snapshot.HourlyTokens[j];
			double num3 = ((num2 > 0) ? Math.Max(5.0, Math.Round(43.0 * (double)num2 / (double)num)) : 3.0);
			if (j == hour)
			{
				num3 = ((num2 > 0) ? Math.Max(10.0, num3) : 4.0);
			}
			_hBars[j].Height = num3;
			_hBars[j].Background = ((j == hour) ? BuddyTheme.Text100 : ((!flag2) ? BuddyTheme.Track : ((num2 > 0) ? BuddyTheme.BarPast : BuddyTheme.Track)));
			string text = ClockFormat.Range(j, j + 1) + ((j == hour) ? " (now)" : "");
			_hCols[j].ToolTip = BuddyTooltip.Create(text + " · " + ((num2 > 0) ? (num2.ToString("N0", currentCulture) + " tokens") : "no usage"));
		}
		bool flag3 = aICodingToolType switch
		{
			AICodingToolType.All => detectedAgents.Contains(AICodingToolType.ClaudeCode), 
			AICodingToolType.ClaudeCode => true, 
			_ => false, 
		};
		_mPlan.Visibility = ((!flag3) ? Visibility.Collapsed : Visibility.Visible);
		double monthlyBudget = snapshot.MonthlyBudget;
		bool flag4 = _mEditHost.Visibility == Visibility.Visible;
		_mValue.Text = "≈ " + Money(snapshot.CostThisMonth);
		_mLine.ToolTip = BuddyTooltip.Create(snapshot.TokensThisMonth.ToString("N0", currentCulture) + " tokens this month (≈ " + Money(snapshot.CostThisMonth) + " API value)" + ((snapshot.UnpricedTokensThisMonth > 0) ? ("; " + snapshot.UnpricedTokensThisMonth.ToString("N0", currentCulture) + " tokens on unpriced models not included") : ""));
		if (!flag4)
		{
			_mBudget.Text = ((monthlyBudget > 0.0) ? ("of " + Money(monthlyBudget, whole: true) + " budget") : "Set budget");
			_mBudget.Foreground = (_mBudget.IsMouseOver ? BuddyTheme.Text100 : BuddyTheme.Text50);
			_mBudget.ToolTip = BuddyTooltip.Create((monthlyBudget > 0.0) ? "Click to change or remove the monthly budget" : "Set a monthly budget · alerts at 75% and 100%");
		}
		_mFraction = ((monthlyBudget > 0.0) ? (snapshot.CostThisMonth / monthlyBudget) : 0.0);
		_mBarRow.Visibility = ((!(monthlyBudget > 0.0)) ? Visibility.Collapsed : Visibility.Visible);
		Set(_mPct, (monthlyBudget > 0.0) ? (Math.Floor(_mFraction * 100.0).ToString(currentCulture) + "%") : "");
		_mPct.Foreground = ((_mFraction >= 1.0) ? BuddyTheme.Text100 : BuddyTheme.Text50);
		SizeMonthBar();
		_mNote.Visibility = ((monthlyBudget > 0.0) ? Visibility.Collapsed : Visibility.Visible);
		bool flag5 = aICodingToolType == AICodingToolType.All;
		string text2 = (flag5 ? "agent" : "model");
		if (_aHeader == null || (string)_aHeader.Tag != text2)
		{
			_aHeader = (flag5 ? BuddyTheme.CardHeader("bot", "By agent") : BuddyTheme.CardHeader("data_histogram", "By model"));
			_aHeader.Tag = text2;
			_aHeaderHost.Child = _aHeader;
		}
		List<UsageRow> list = snapshot.Rows ?? new List<UsageRow>();
		long top = Math.Max(1L, list.Select((UsageRow r) => r.Tokens).DefaultIfEmpty(0L).Max());
		List<FrameworkElement> list2 = new List<FrameworkElement>();
		bool flag6 = !flag5 && list.Count == 1;
		foreach (UsageRow item in list)
		{
			UsageRow row = item;
			Action click = (flag5 ? ((Action)delegate
			{
				SetFilter(row.Agent);
			}) : null);
			list2.Add(new AgentRow(item, flag5, top, click, flag6 ? 34 : 40));
		}
		if (flag6)
		{
			list2.Add(new TokenBreakdown(list[0]));
		}
		_aList.SetRows(list2);
		_aEmpty.Visibility = ((flag5 || list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		Set(_aEmpty, "No usage today");
	}

	private void SetHero(long tokens)
	{
		string text = Compact(tokens);
		string text2 = " tokens";
		double num = 151.0;
		double fontSize = 28.0;
		double[] array = new double[3] { 28.0, 24.0, 20.0 };
		foreach (double num2 in array)
		{
			fontSize = num2;
			if (Measure(text, num2, AynType.Semibold, BuddyTheme.DisplayFont) + Measure(text2, 13.0, FontWeights.Medium, BuddyTheme.Font) <= num)
			{
				break;
			}
		}
		_tHero.Inlines.Clear();
		_tHero.FontSize = fontSize;
		_tHero.Inlines.Add(new Run(text));
		_tHero.Inlines.Add(new Run(text2)
		{
			FontFamily = BuddyTheme.Font,
			FontSize = 13.0,
			FontWeight = FontWeights.Medium,
			Foreground = BuddyTheme.Text50
		});
	}

	private void SetCost(AICodingSnapshot s)
	{
		double num = 151.0;
		string text = Money(s.CostToday);
		List<string> list = new List<string>();
		if (s.UnpricedTokensToday > 0)
		{
			list.Add(" API value · excl. " + Compact(s.UnpricedTokensToday));
		}
		list.Add(" API value today");
		list.Add(" API value");
		list.Add("");
		string text2 = list[list.Count - 1];
		foreach (string item in list)
		{
			if (Measure("≈ ", 13.0, FontWeights.Medium, BuddyTheme.Font) + Measure(text, 13.0, AynType.Medium, BuddyTheme.Font) + Measure(item, 13.0, FontWeights.Medium, BuddyTheme.Font) <= num)
			{
				text2 = item;
				break;
			}
		}
		_tCost.Inlines.Clear();
		_tCost.Inlines.Add(new Run("≈ "));
		_tCost.Inlines.Add(new Run(text)
		{
			Foreground = BuddyTheme.Text92,
			FontWeight = AynType.Medium
		});
		_tCost.Inlines.Add(new Run(text2));
	}

	private void SetFooter(string project, string streak)
	{
		Set(_tStreak, streak);
		double num = 372.0 - Measure("  ·  ", 11.5, FontWeights.Medium, BuddyTheme.Font) - 17.0 - Measure(streak, 11.5, FontWeights.Medium, BuddyTheme.Font);
		double num2 = 11.5;
		string text = project;
		if (Measure(text, num2, FontWeights.Medium, BuddyTheme.Font) > num)
		{
			num2 = 10.0;
			if (Measure(text, num2, FontWeights.Medium, BuddyTheme.Font) > num)
			{
				int num3 = text.Length;
				while (num3 > 4 && Measure(MiddleShorten(project, num3), num2, FontWeights.Medium, BuddyTheme.Font) > num)
				{
					num3--;
				}
				text = MiddleShorten(project, num3);
			}
		}
		_tProject.FontSize = num2;
		Set(_tProject, text);
	}

	private static string MiddleShorten(string s, int keep)
	{
		if (s.Length <= keep)
		{
			return s;
		}
		int num = (keep + 1) / 2;
		int num2 = keep - num;
		return s.Substring(0, num) + "…" + s.Substring(s.Length - num2);
	}

	private static string CostTip(AICodingSnapshot s)
	{
		string text = "Cost is estimated from each model's public API list price per million tokens (input, output, cache write and cache read). It is the ≈ API value of what you used: subscription plans such as Claude Pro / Max or ChatGPT plans aren't billed per token.";
		if (s.UnpricedTokensToday > 0)
		{
			string text2 = text;
			text = text2 + "\n" + s.UnpricedTokensToday.ToString("N0", CultureInfo.CurrentCulture) + " tokens today are on models without a known price; they are counted in tokens but not in cost: " + string.Join(", ", s.UnpricedModels);
		}
		return text;
	}

	private static void Set(TextBlock tb, string s)
	{
		if (tb.Text != s)
		{
			tb.Text = s;
		}
	}

	private static string Count(int v)
	{
		if (v >= 100000)
		{
			return Compact(v);
		}
		return v.ToString("N0", CultureInfo.CurrentCulture);
	}

	public static string Compact(long v)
	{
		if (v < 10000)
		{
			return v.ToString("N0", CultureInfo.CurrentCulture);
		}
		if (v < 1000000)
		{
			return ((double)v / 1000.0).ToString((v < 100000) ? "0.#" : "0", CultureInfo.CurrentCulture) + "K";
		}
		if (v < 1000000000)
		{
			return ((double)v / 1000000.0).ToString((v < 100000000) ? "0.#" : "0", CultureInfo.CurrentCulture) + "M";
		}
		return ((double)v / 1000000000.0).ToString("0.#", CultureInfo.CurrentCulture) + "B";
	}

	private static string Money(double usd, bool whole = false)
	{
		if (usd > 0.0 && usd < 0.01)
		{
			return "<$0.01";
		}
		if (whole && usd == Math.Floor(usd))
		{
			return "$" + usd.ToString("N0", CultureInfo.CurrentCulture);
		}
		return "$" + usd.ToString((usd >= 1000.0) ? "N0" : "N2", CultureInfo.CurrentCulture);
	}
}
