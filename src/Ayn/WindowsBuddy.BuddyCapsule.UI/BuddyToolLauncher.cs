using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyToolLauncher : Border
{
	private const double PreviewScale = 0.75;

	private const double PreviewWidth = 221.33333333333334;

	public static int LauncherInstanceCount;

	private readonly string instance = Guid.NewGuid().ToString("N");

	private readonly TextBox search;

	private readonly StackPanel rows;

	private readonly TextBlock title;

	private readonly TextBlock capacity;

	private readonly BuddyScrollViewer scroll;

	private readonly Border notice;

	private readonly TextBlock noticeText;

	private readonly TranslateTransform noticeRise = new TranslateTransform();

	private readonly TranslateTransform rise = new TranslateTransform();

	private readonly List<FrameworkElement> choices = new List<FrameworkElement>();

	private int selected;

	private bool widgets;

	private readonly Dictionary<string, ImageSource> previews = new Dictionary<string, ImageSource>();

	private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
	{
		{ "home", "Home|Your widgets at a glance|dashboard widgets" },
		{ "clock", "Clock|World clocks, alarms and time zones|alarm time timezone" },
		{ "network", "Network|Live internet speed|network speed bandwidth download upload wifi" },
		{ "timer", "Timers|Pomodoro, countdown, laps and water|pomodoro stopwatch countdown hydration" },
		{ "links", "Links|Bookmarks and quick search|bookmark bookmarks search engines" },
		{ "units", "Units|Convert measurements and currencies|convert converter ruler currency" },
		{ "files", "File Converter|Convert documents, images and media|convert converter file" },
		{ "shelf", "File Shelf|Keep files within reach|files drop shelf" },
		{ "aicoding", "AI Coding|Follow your coding sessions|claude codex ai coding" },
		{ "analytics", "Analytics|Your connected website statistics|traffic visitors" },
		{ "revenue", "Revenue|Your connected sales overview|stripe gumroad sales" },
		{ "system", "System|Live performance at a glance|cpu memory battery" },
		{ "screentime", "Screen Time|Your time across apps|usage productivity" },
		{ "notes", "Notes|Capture a quick thought|notes scratchpad" },
		{ "clipboard", "Clipboard|Recent copied text and images|copy paste history" },
		{ "weather", "Weather|Local conditions and forecast|temperature forecast" }
	};

	public LauncherLifecycleState State { get; private set; }

	public bool IsLauncherOpen
	{
		get
		{
			if (State != LauncherLifecycleState.Open)
			{
				return State == LauncherLifecycleState.Opening;
			}
			return true;
		}
	}

	public string InstanceId => instance;

	public Point LastAddPoint { get; private set; }

	public bool WidgetMode => widgets;

	public bool DockFullNoticeVisible => notice.Visibility == Visibility.Visible;

	public event EventHandler<string> ToolSelected;

	public event EventHandler Opened;

	public event EventHandler Closed;

	public BuddyToolLauncher()
	{
		LauncherInstanceCount++;
		State = LauncherLifecycleState.Closed;
		base.Width = 728.0;
		base.Height = 242.0;
		base.Background = ThemeTokens.Bg;
		base.CornerRadius = new CornerRadius(18.0);
		base.Padding = new Thickness(16.0);
		base.BorderThickness = new Thickness(0.0);
		base.Visibility = Visibility.Collapsed;
		base.RenderTransform = rise;
		Grid grid = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition()
			}
		};
		title = BuddyTheme.Text("Add to dock", 18.0, ThemeTokens.Text, FontWeights.SemiBold);
		title.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		title.HorizontalAlignment = HorizontalAlignment.Left;
		capacity = BuddyTheme.SecondaryText("Remove a widget to make room");
		capacity.HorizontalAlignment = HorizontalAlignment.Right;
		capacity.VerticalAlignment = VerticalAlignment.Center;
		capacity.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		capacity.Visibility = Visibility.Collapsed;
		Grid element = new Grid
		{
			Children = 
			{
				(UIElement)title,
				(UIElement)capacity
			}
		};
		grid.Children.Add(element);
		search = new TextBox
		{
			Height = 32.0,
			FontFamily = BuddyTokens.FontText,
			FontSize = 12.0,
			Background = ThemeTokens.Surface2,
			Foreground = ThemeTokens.Text,
			CaretBrush = ThemeTokens.Text,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(10.0, 5.0, 10.0, 5.0)
		};
		AutomationProperties.SetName(search, "Search tools");
		search.ToolTip = BuddyTooltip.Create("Search tools and keywords");
		Grid grid2 = new Grid();
		search.Background = Brushes.Transparent;
		grid2.Children.Add(search);
		TextBlock hint = BuddyTheme.SecondaryText("Search tools and keywords");
		hint.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		hint.VerticalAlignment = VerticalAlignment.Center;
		hint.IsHitTestVisible = false;
		grid2.Children.Add(hint);
		search.TextChanged += delegate
		{
			hint.Visibility = ((search.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		};
		Border element2 = new Border
		{
			Background = ThemeTokens.Surface2,
			CornerRadius = new CornerRadius(10.0),
			Child = grid2
		};
		Grid.SetRow(element2, 1);
		grid.Children.Add(element2);
		noticeText = BuddyTheme.Text("Your dock is full. Remove a tool to add another.", 12.0, ThemeTokens.Text, FontWeights.Medium);
		noticeText.TextWrapping = TextWrapping.Wrap;
		noticeText.VerticalAlignment = VerticalAlignment.Center;
		Ellipse element3 = new Ellipse
		{
			Width = 6.0,
			Height = 6.0,
			Fill = ThemeTokens.Accent,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		DockPanel dockPanel = new DockPanel();
		DockPanel.SetDock(element3, Dock.Left);
		dockPanel.Children.Add(element3);
		dockPanel.Children.Add(noticeText);
		notice = new Border
		{
			Background = ThemeTokens.Surface2,
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(12.0, 9.0, 12.0, 9.0),
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			Child = dockPanel,
			Visibility = Visibility.Collapsed,
			RenderTransform = noticeRise,
			Tag = "dock-full-notice"
		};
		AutomationProperties.SetName(notice, "Your dock is full. Remove a tool to add another.");
		AutomationProperties.SetLiveSetting(notice, AutomationLiveSetting.Polite);
		Grid.SetRow(notice, 2);
		grid.Children.Add(notice);
		rows = new StackPanel();
		scroll = new BuddyScrollViewer
		{
			Content = rows,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Grid.SetRow(scroll, 3);
		grid.Children.Add(scroll);
		Child = grid;
		TextBox textBox = search;
		TextChangedEventHandler value = delegate
		{
			PopulateTools();
		};
		textBox.TextChanged += value;
		base.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				HandleEscape();
				e.Handled = true;
			}
			else if (e.Key == Key.Down || e.Key == Key.Up || (widgets && (e.Key == Key.Left || e.Key == Key.Right)))
			{
				int num = ((e.Key != Key.Down) ? ((e.Key != Key.Up) ? ((e.Key == Key.Right) ? 1 : (-1)) : (widgets ? (-3) : (-1))) : ((!widgets) ? 1 : 3));
				selected = Math.Max(0, Math.Min(choices.Count - 1, selected + num));
				PaintSelection();
				e.Handled = true;
			}
			else if (e.Key == Key.Return)
			{
				if (choices.Count > 0)
				{
					Add((string)choices[selected].Tag);
				}
				e.Handled = true;
			}
		};
	}

	public new static string Name(string id)
	{
		if (!Aliases.TryGetValue(id, out var value))
		{
			return ToolRegistry.Instance.GetTool(id).DisplayName;
		}
		return value.Split('|')[0];
	}

	public static string Keywords(string id)
	{
		if (!Aliases.TryGetValue(id, out var value))
		{
			return Name(id);
		}
		return value.Replace('|', ' ');
	}

	public void SetWidgetMode(bool value)
	{
		widgets = value;
		title.Text = (value ? "Add widget" : "Add to dock");
		title.Margin = new Thickness(0.0, 0.0, 0.0, value ? 8 : 10);
		scroll.Margin = new Thickness(0.0, value ? 8 : 10, 0.0, 0.0);
		capacity.Text = (value ? "Remove a widget to make room" : DockCount());
		capacity.Visibility = ((!(value ? (!NotchEditing.HasRoom) : ToolRegistry.Instance.DockFull)) ? Visibility.Collapsed : Visibility.Visible);
		HideNotice();
	}

	private static string DockCount()
	{
		return ToolRegistry.Instance.DockTools.Count + " of " + 14 + " · full";
	}

	private void ShowDockFull()
	{
		noticeText.Text = "Your dock is full. Remove a tool to add another.";
		bool flag = notice.Visibility == Visibility.Visible;
		notice.Visibility = Visibility.Visible;
		if (!flag)
		{
			notice.Opacity = 0.0;
			noticeRise.Y = -4.0;
			CapsuleMotion.AnimateDouble(notice, UIElement.OpacityProperty, 1.0, 0.18, CapsuleMotion.EaseOutCubic);
			CapsuleMotion.AnimateDouble(noticeRise, TranslateTransform.YProperty, 0.0, 0.22, CapsuleMotion.Spring);
		}
		else
		{
			notice.BeginAnimation(UIElement.OpacityProperty, null);
			notice.Opacity = 0.45;
			CapsuleMotion.AnimateDouble(notice, UIElement.OpacityProperty, 1.0, 0.24, CapsuleMotion.EaseOutCubic);
		}
	}

	private void HideNotice()
	{
		if (notice != null)
		{
			notice.BeginAnimation(UIElement.OpacityProperty, null);
			noticeRise.BeginAnimation(TranslateTransform.YProperty, null);
			noticeRise.BeginAnimation(TranslateTransform.XProperty, null);
			TranslateTransform translateTransform = noticeRise;
			double x = (noticeRise.Y = 0.0);
			translateTransform.X = x;
			notice.Opacity = 1.0;
			notice.Visibility = Visibility.Collapsed;
		}
	}

	private bool Present(string id)
	{
		if (!widgets)
		{
			return ToolRegistry.Instance.IsInDock(id);
		}
		return NotchEditing.WidgetVisible(id);
	}

	public void PopulateTools()
	{
		if (State == LauncherLifecycleState.Closed)
		{
			rows.Children.Clear();
			choices.Clear();
			previews.Clear();
			return;
		}
		if (widgets)
		{
			PopulateGallery();
			return;
		}
		rows.Children.Clear();
		choices.Clear();
		string query = (search.Text ?? "").Trim();
		bool dockFull = ToolRegistry.Instance.DockFull;
		capacity.Text = DockCount();
		capacity.Visibility = ((!dockFull) ? Visibility.Collapsed : Visibility.Visible);
		if (!dockFull)
		{
			HideNotice();
		}
		List<IBuddyTool> list = (from t in ToolRegistry.Instance.AllTools
			where t.Id != "settings" && t.Id != "notch_customization" && (!widgets || NotchEditing.Widgets.Contains(t.Id))
			where Keywords(t.Id).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
			select t).ToList();
		bool[] array = new bool[2] { true, false };
		foreach (bool present in array)
		{
			Func<IBuddyTool, bool> predicate = (IBuddyTool t) => Present(t.Id) == present;
			List<IBuddyTool> list2 = list.Where(predicate).ToList();
			if (list2.Count == 0)
			{
				continue;
			}
			TextBlock textBlock = BuddyTheme.SecondaryText((!present) ? (widgets ? "More widgets" : (dockFull ? "More tools · click a tool to open it" : "More tools")) : (widgets ? "On Home" : "In your dock"));
			textBlock.Margin = new Thickness(2.0, 10.0, 0.0, 6.0);
			rows.Children.Add(textBlock);
			foreach (IBuddyTool item in list2)
			{
				string id = item.Id;
				Grid row = new Grid
				{
					Height = 52.0,
					Background = Brushes.Transparent,
					Tag = id,
					Focusable = true
				};
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(42.0)
				});
				row.ColumnDefinitions.Add(new ColumnDefinition());
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(64.0)
				});
				Border border = new Border();
				border.Width = 32.0;
				border.Height = 32.0;
				border.CornerRadius = new CornerRadius(9.0);
				border.Background = ThemeTokens.Surface2;
				border.Child = item.CreateIcon(18.0, ThemeTokens.Text);
				border.VerticalAlignment = VerticalAlignment.Center;
				Border element = border;
				row.Children.Add(element);
				StackPanel stackPanel = new StackPanel();
				stackPanel.VerticalAlignment = VerticalAlignment.Center;
				StackPanel stackPanel2 = stackPanel;
				TextBlock textBlock2 = BuddyTheme.BodyText(Name(id));
				Highlight(textBlock2, Name(id), query);
				stackPanel2.Children.Add(textBlock2);
				string value;
				string text = (Aliases.TryGetValue(id, out value) ? value.Split('|')[1] : ("Open " + Name(id)));
				TextBlock textBlock3 = BuddyTheme.SecondaryText(text);
				textBlock3.FontSize = 11.0;
				Highlight(textBlock3, text, query);
				stackPanel2.Children.Add(textBlock3);
				Grid.SetColumn(stackPanel2, 1);
				row.Children.Add(stackPanel2);
				Border border2 = new Border();
				border2.Width = (present ? 62 : 30);
				border2.Height = 30.0;
				border2.CornerRadius = new CornerRadius(15.0);
				border2.Background = (present ? Brushes.Transparent : ThemeTokens.Surface2);
				border2.Opacity = ((!present && dockFull) ? 0.45 : 1.0);
				border2.Cursor = ((present || dockFull) ? Cursors.Arrow : Cursors.Hand);
				border2.Tag = (present ? "in-dock" : (dockFull ? "add-full" : "add"));
				border2.VerticalAlignment = VerticalAlignment.Center;
				border2.HorizontalAlignment = HorizontalAlignment.Right;
				border2.Child = new TextBlock
				{
					Text = ((!present) ? "+" : (widgets ? "On Home" : "In dock")),
					FontFamily = BuddyTokens.FontText,
					FontSize = (present ? 11 : 20),
					Foreground = ((present || dockFull) ? ThemeTokens.Text3 : ThemeTokens.Success),
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				Border border3 = border2;
				AutomationProperties.SetName(border3, (present ? "Already added " : (dockFull ? "Dock full, can’t add " : "Add ")) + Name(id));
				if (!present && dockFull)
				{
					border3.ToolTip = BuddyTooltip.Create("Your dock is full. Remove a tool to add another.");
				}
				if (!present && dockFull)
				{
					row.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
					{
						if (!e.Handled)
						{
							BeginClose();
							ToolRouter.Instance.RouteTo(id);
							e.Handled = true;
						}
					};
				}
				Grid.SetColumn(border3, 2);
				row.Children.Add(border3);
				border3.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
				};
				border3.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
				{
					if (!present)
					{
						Add(id);
					}
					e.Handled = true;
				};
				row.MouseEnter += delegate
				{
					row.Background = ThemeTokens.Surface2;
				};
				row.MouseLeave += delegate
				{
					row.Background = Brushes.Transparent;
				};
				rows.Children.Add(row);
				if (!present)
				{
					choices.Add(row);
				}
			}
		}
		if (list.Count == 0)
		{
			TextBlock textBlock4 = BuddyTheme.SecondaryText("No results");
			textBlock4.Margin = new Thickness(12.0, 24.0, 12.0, 24.0);
			rows.Children.Add(textBlock4);
		}
		selected = Math.Min(selected, Math.Max(0, choices.Count - 1));
	}

	private void PopulateGallery()
	{
		rows.Children.Clear();
		choices.Clear();
		string query = search.Text.Trim();
		string[] array = NotchEditing.Widgets.Where((string id) => HomeWidgetCard.Keywords(id).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
		bool full = !NotchEditing.HasRoom;
		capacity.Visibility = ((!full) ? Visibility.Collapsed : Visibility.Visible);
		bool[] array2 = new bool[2] { false, true };
		foreach (bool present in array2)
		{
			Func<string, bool> predicate = (string id) => NotchEditing.WidgetVisible(id) == present;
			string[] array3 = array.Where(predicate).ToArray();
			if (array3.Length == 0)
			{
				continue;
			}
			BuddyLabel buddyLabel = BuddyTheme.LabelText(present ? "ON HOME" : "SUGGESTED");
			buddyLabel.FontSize = 11.5;
			buddyLabel.Tracking = 0.69;
			buddyLabel.Foreground = ThemeTokens.Text;
			buddyLabel.Opacity = 0.55;
			buddyLabel.Margin = new Thickness(2.0, 2.0, 0.0, 6.0);
			rows.Children.Add(buddyLabel);
			Grid grid = new Grid();
			grid.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
			Grid grid2 = grid;
			for (int j = 0; j < 5; j++)
			{
				grid2.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = ((j % 2 == 1) ? new GridLength(16.0) : new GridLength(1.0, GridUnitType.Star))
				});
			}
			rows.Children.Add(grid2);
			int num = 0;
			string[] array4 = array3;
			foreach (string id2 in array4)
			{
				int num2 = num / 3;
				if (num % 3 == 0)
				{
					grid2.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
				}
				Grid grid3 = new Grid();
				grid3.Tag = id2;
				grid3.Margin = new Thickness(0.0, 0.0, 0.0, (num2 < (array3.Length - 1) / 3) ? 16 : 0);
				grid3.Background = Brushes.Transparent;
				grid3.Cursor = ((present || full) ? Cursors.Arrow : Cursors.Hand);
				grid3.Focusable = true;
				grid3.Opacity = (present ? 0.5 : 1.0);
				Grid grid4 = grid3;
				grid4.RowDefinitions.Add(new RowDefinition
				{
					Height = new GridLength(87.0)
				});
				grid4.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
				grid4.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
				if (previews.TryGetValue(id2, out var value))
				{
					grid4.Children.Add(new Image
					{
						Source = value,
						Stretch = Stretch.Fill,
						Width = 233.33333333333334,
						Height = 99.0,
						Margin = new Thickness(-6.0)
					});
				}
				TextBlock textBlock = BuddyTheme.Text(HomeWidgetCard.Name(id2), 13.0, ThemeTokens.Text, FontWeights.Medium);
				textBlock.TextAlignment = TextAlignment.Center;
				textBlock.Margin = new Thickness(0.0, 7.0, 0.0, 0.0);
				Highlight(textBlock, textBlock.Text, query);
				if (query.Length > 0 && HomeWidgetCard.Name(id2).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
				{
					textBlock.Foreground = ThemeTokens.Accent;
				}
				Grid.SetRow(textBlock, 1);
				grid4.Children.Add(textBlock);
				if (present)
				{
					TextBlock textBlock2 = BuddyTheme.Text("On Home", 11.0, ThemeTokens.Text, FontWeights.Normal);
					textBlock2.Opacity = 0.6;
					textBlock2.TextAlignment = TextAlignment.Center;
					textBlock2.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
					Grid.SetRow(textBlock2, 2);
					grid4.Children.Add(textBlock2);
				}
				AutomationProperties.SetName(grid4, (present ? "On Home " : (full ? "No room for " : "Add widget ")) + HomeWidgetCard.Name(id2));
				TranslateTransform lift = new TranslateTransform();
				grid4.RenderTransform = lift;
				grid4.MouseEnter += delegate
				{
					if (!present && !full)
					{
						CapsuleMotion.AnimateDouble(lift, TranslateTransform.YProperty, -2.0, 0.16, CapsuleMotion.EaseOutCubic);
					}
				};
				grid4.MouseLeave += delegate
				{
					CapsuleMotion.AnimateDouble(lift, TranslateTransform.YProperty, 0.0, 0.16, CapsuleMotion.EaseOutCubic);
				};
				grid4.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
				{
					e.Handled = true;
				};
				grid4.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
				{
					if (!present && !full)
					{
						Add(id2);
					}
					e.Handled = true;
				};
				Grid.SetRow(grid4, num2);
				Grid.SetColumn(grid4, num % 3 * 2);
				num++;
				grid2.Children.Add(grid4);
				if (!present && !full)
				{
					choices.Add(grid4);
				}
			}
		}
		if (array.Length == 0)
		{
			TextBlock textBlock3 = BuddyTheme.SecondaryText("No results");
			textBlock3.Margin = new Thickness(12.0, 24.0, 12.0, 24.0);
			rows.Children.Add(textBlock3);
		}
		selected = Math.Min(selected, Math.Max(0, choices.Count - 1));
	}

	private void Highlight(TextBlock text, string value, string query)
	{
		if (query.Length != 0)
		{
			int num = value.IndexOf(query, StringComparison.OrdinalIgnoreCase);
			if (num >= 0)
			{
				text.Text = "";
				text.Inlines.Add(new Run(value.Substring(0, num)));
				text.Inlines.Add(new Run(value.Substring(num, query.Length))
				{
					Foreground = ThemeTokens.Accent,
					FontWeight = FontWeights.SemiBold
				});
				text.Inlines.Add(new Run(value.Substring(num + query.Length)));
			}
		}
	}

	private void PaintSelection()
	{
		for (int i = 0; i < choices.Count; i++)
		{
			((Grid)choices[i]).Background = ((i == selected) ? ThemeTokens.Selection : Brushes.Transparent);
		}
		if (choices.Count > 0)
		{
			choices[selected].BringIntoView();
		}
	}

	private void Add(string id)
	{
		if (Present(id))
		{
			return;
		}
		FrameworkElement frameworkElement = choices.FirstOrDefault((FrameworkElement g) => object.Equals(g.Tag, id));
		if (frameworkElement == null)
		{
			return;
		}
		LastAddPoint = frameworkElement.PointToScreen(new Point(frameworkElement.ActualWidth / 2.0, frameworkElement.ActualHeight / 2.0));
		if (!widgets && !ToolRegistry.Instance.CanAddToDock(id))
		{
			ShowDockFull();
			return;
		}
		if (widgets)
		{
			if (!NotchEditing.PlaceWidget(id, NotchEditing.AddSlot))
			{
				return;
			}
			BeginClose();
			if (ToolRegistry.Instance.GetTool("home") is HomeTool homeTool)
			{
				homeTool.AnimateArrival(id, LastAddPoint);
			}
		}
		else if (!ToolRegistry.Instance.TryAddToDock(id))
		{
			ShowDockFull();
			return;
		}
		this.ToolSelected?.Invoke(this, id);
		if (!widgets)
		{
			PopulateTools();
			search.Focus();
		}
	}

	public void Toggle()
	{
		if (IsLauncherOpen)
		{
			BeginClose();
		}
		else
		{
			BeginOpen();
		}
	}

	public void BeginOpen()
	{
		if (IsLauncherOpen)
		{
			return;
		}
		State = LauncherLifecycleState.Opening;
		HideNotice();
		previews.Clear();
		if (widgets)
		{
			string[] array = NotchEditing.Widgets;
			foreach (string text in array)
			{
				previews[text] = HomeTool.SnapshotWidget(text, 295.11111111111114, 8.0);
			}
		}
		search.Text = "";
		PopulateTools();
		scroll.ScrollToTop();
		base.Visibility = Visibility.Visible;
		base.IsHitTestVisible = true;
		base.Opacity = 1.0;
		rise.Y = 60.0;
		this.Opened?.Invoke(this, EventArgs.Empty);
		CapsuleMotion.AnimateDouble(rise, TranslateTransform.YProperty, 0.0, 0.22, CapsuleMotion.Spring, delegate
		{
			if (State == LauncherLifecycleState.Opening)
			{
				State = LauncherLifecycleState.Open;
				if (ToolRegistry.Instance.GetTool("home") is HomeTool homeTool)
				{
					homeTool.GalleryPainted();
				}
			}
		});
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			search.Focus();
		});
	}

	public void BeginClose()
	{
		if (State != 0)
		{
			State = LauncherLifecycleState.Closed;
			rows.Children.Clear();
			choices.Clear();
			previews.Clear();
			base.Visibility = Visibility.Collapsed;
			base.IsHitTestVisible = false;
			rise.BeginAnimation(TranslateTransform.YProperty, null);
			rise.Y = 0.0;
			this.Closed?.Invoke(this, EventArgs.Empty);
		}
	}

	public void HandleEscape()
	{
		BeginClose();
	}
}
