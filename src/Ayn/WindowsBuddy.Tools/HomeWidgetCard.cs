using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Media;
using WindowsBuddy.Tools.AICoding;
using WindowsBuddy.Tools.Analytics;
using WindowsBuddy.Tools.Calendar;
using WindowsBuddy.Tools.Clock;
using WindowsBuddy.Tools.FileShelf;
using WindowsBuddy.Tools.Files;
using WindowsBuddy.Tools.Links;
using WindowsBuddy.Tools.Message;
using WindowsBuddy.Tools.Revenue;
using WindowsBuddy.Tools.Timer;
using WindowsBuddy.Tools.Units;

namespace WindowsBuddy.Tools;

public sealed class HomeWidgetCard : WidgetCard
{
	public readonly string WidgetId;

	private readonly TextBlock hero;

	private readonly TextBlock secondary;

	private readonly StackPanel accessories;

	private readonly Button action;

	private readonly StackPanel transport;

	private bool active;

	private int generation;

	private int lastMinute = -1;

	private EventHandler changed;

	private EventHandler<DateTime> clock;

	private Action cacheChanged;

	private EventHandler<string> privacyChanged;

	private string[] dropped;

	public new static string Name(string id)
	{
		return id switch
		{
			"timer" => "Focus", 
			"music" => "Now Playing", 
			"water" => "Water", 
			"stopwatch" => "Stopwatch", 
			_ => BuddyToolLauncher.Name(id), 
		};
	}

	public static string Route(string id)
	{
		switch (id)
		{
		default:
			return id;
		case "music":
			return "home";
		case "water":
		case "stopwatch":
			return "timer";
		}
	}

	public static string Keywords(string id)
	{
		return id switch
		{
			"stopwatch" => "Stopwatch laps elapsed", 
			"music" => "Now Playing music song spotify audio", 
			"clock" => "Clock time world timezone", 
			"aicoding" => "AI Coding ai coding claude codex", 
			"revenue" => "Revenue money sales stripe gumroad", 
			"analytics" => "Analytics visitors website traffic", 
			"water" => "Water drink hydration", 
			"timer" => "Focus pomodoro focus timer", 
			"units" => "Units convert units conversion", 
			"files" => "File Converter convert units files", 
			"links" => "Links bookmark link", 
			"network" => "Network speed internet download upload wifi bandwidth", 
			"shelf" => "File Shelf files shelf", 
			_ => BuddyToolLauncher.Keywords(id), 
		};
	}

	public HomeWidgetCard(string id)
		: base((id == "music") ? "play" : ((id == "water") ? "drop" : ((id == "stopwatch") ? "timer" : (BuddyToolIcons.FluentName(id) ?? "home"))), Name(id), id)
	{
		HomeWidgetCard homeWidgetCard = this;
		WidgetId = id;
		base.Focusable = false;
		base.Cursor = Cursors.Arrow;
		KeyboardNavigation.SetIsTabStop(this, isTabStop: false);
		Grid body = Body;
		hero = WidgetCard.Value("");
		hero.Margin = new Thickness(0.0, 18.0, 0.0, 0.0);
		hero.VerticalAlignment = VerticalAlignment.Top;
		body.Children.Add(hero);
		secondary = WidgetCard.Secondary("");
		secondary.VerticalAlignment = VerticalAlignment.Top;
		body.Children.Add(secondary);
		accessories = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Bottom
		};
		body.Children.Add(accessories);
		action = new Button
		{
			Width = 28.0,
			Height = 28.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Background = ThemeTokens.Selection,
			Foreground = ThemeTokens.Text,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Cursor = Cursors.Hand
		};
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14.0));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		action.Template = controlTemplate;
		action.Click += delegate(object s, RoutedEventArgs e)
		{
			if (!NotchEditing.IsEditing)
			{
				DoAction();
				e.Handled = true;
			}
		};
		action.PreviewMouseDown += delegate
		{
			if (!NotchEditing.IsEditing)
			{
				action.Opacity = 0.65;
			}
		};
		action.MouseLeave += delegate
		{
			action.Opacity = 1.0;
		};
		action.PreviewMouseUp += delegate
		{
			action.Opacity = 1.0;
		};
		AutomationProperties.SetName(action, "Widget action " + Name(id));
		body.Children.Add(action);
		transport = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Visibility = Visibility.Collapsed
		};
		if (id == "music")
		{
			transport.Children.Add(Transport("Previous", "previous", delegate
			{
				MediaService.Instance.SkipPrevious();
			}));
			transport.Children.Add(Transport("Play or pause", "play", delegate
			{
				MediaService.Instance.TogglePlayPause();
			}));
			transport.Children.Add(Transport("Next", "next", delegate
			{
				MediaService.Instance.SkipNext();
			}));
			body.Children.Add(transport);
		}
		base.IsVisibleChanged += delegate
		{
			if (!base.IsVisible)
			{
				SetActive(value: false);
			}
		};
		base.Unloaded += delegate
		{
			SetActive(value: false);
		};
		if (id == "shelf" || id == "files")
		{
			base.AllowDrop = true;
			base.DragOver += delegate(object s, DragEventArgs e)
			{
				if (!NotchEditing.IsEditing && e.Data.GetDataPresent(DataFormats.FileDrop))
				{
					e.Effects = DragDropEffects.Copy;
					e.Handled = true;
				}
			};
			base.Drop += delegate(object s, DragEventArgs e)
			{
				if (!NotchEditing.IsEditing && e.Data.GetData(DataFormats.FileDrop) is string[] array)
				{
					if (id == "shelf")
					{
						FileShelfService.Instance.AddFiles(array);
					}
					else
					{
						homeWidgetCard.dropped = array;
						homeWidgetCard.hero.Text = Path.GetFileName(array[0]);
						homeWidgetCard.secondary.Text = "Choose a format";
						homeWidgetCard.ShowFormats();
					}
					e.Handled = true;
				}
			};
		}
		Set(Name(id), "Add to Home for a quick glance", null);
	}

	private Button Transport(string name, string icon, Action run)
	{
		FrameworkElement frameworkElement = FluentIcons.Create((icon == "previous") ? "next" : icon, 12.0, ThemeTokens.Text, filled: false);
		if (icon == "previous")
		{
			frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
			frameworkElement.RenderTransform = new ScaleTransform(-1.0, 1.0);
		}
		BuddyButton buddyButton = new BuddyButton("", BuddyButtonVariant.Secondary, 24.0);
		buddyButton.Width = 28.0;
		buddyButton.Padding = new Thickness(0.0);
		buddyButton.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
		buddyButton.Content = frameworkElement;
		BuddyButton buddyButton2 = buddyButton;
		AutomationProperties.SetName(buddyButton2, "Now Playing " + name);
		buddyButton2.ToolTip = BuddyTooltip.Create(name);
		buddyButton2.Click += delegate(object s, RoutedEventArgs e)
		{
			if (!NotchEditing.IsEditing)
			{
				run();
			}
			e.Handled = true;
		};
		return buddyButton2;
	}

	private void Set(string value, string sub, string button, bool empty = false)
	{
		hero.Text = value;
		WidgetCard.ValueStyle(hero, empty);
		secondary.Text = sub;
		secondary.Opacity = (empty ? 0.55 : 0.6);
		secondary.Margin = new Thickness(0.0, empty ? 48.0 : 52.0, (button != null) ? 32 : 0, 0.0);
		action.Width = 28.0;
		action.Content = button;
		action.Visibility = ((button == null) ? Visibility.Collapsed : Visibility.Visible);
	}

	public void SetActive(bool value)
	{
		if (active == value)
		{
			return;
		}
		active = value;
		generation++;
		if (value)
		{
			changed = delegate
			{
				Queue();
			};
			clock = delegate(object s, DateTime e)
			{
				if (WidgetId == "network")
				{
					Queue();
					return;
				}
				if (lastMinute != e.Minute)
				{
					lastMinute = e.Minute;
					Queue();
				}
			};
			cacheChanged = delegate
			{
				Queue();
			};
			Bind(add: true);
			Refresh();
		}
		else
		{
			Bind(add: false);
		}
	}

	private void Queue()
	{
		if (!active)
		{
			return;
		}
		int version = generation;
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (active && version == generation && base.IsVisible)
			{
				Refresh();
			}
		});
	}

	private void Bind(bool add)
	{
		switch (WidgetId)
		{
		case "clock":
		case "calendar":
		case "network":
			if (add)
			{
				ClockService.Instance.SecondTick += clock;
				ClockService.Instance.SystemTimeChanged += changed;
				ClockSettings.Instance.SettingsChanged += changed;
			}
			else
			{
				ClockService.Instance.SecondTick -= clock;
				ClockService.Instance.SystemTimeChanged -= changed;
				ClockSettings.Instance.SettingsChanged -= changed;
			}
			if (WidgetId == "calendar")
			{
				if (add)
				{
					CalendarService.Instance.Changed += changed;
				}
				else
				{
					CalendarService.Instance.Changed -= changed;
				}
			}
			break;
		case "music":
			if (add)
			{
				MediaService.Instance.MediaStateChanged += changed;
			}
			else
			{
				MediaService.Instance.MediaStateChanged -= changed;
			}
			break;
		case "water":
			if (add)
			{
				TimerService.Instance.HydrationChanged += changed;
			}
			else
			{
				TimerService.Instance.HydrationChanged -= changed;
			}
			break;
		case "stopwatch":
			if (add)
			{
				TimerService.Instance.Tick += changed;
			}
			else
			{
				TimerService.Instance.Tick -= changed;
			}
			break;
		case "shelf":
			if (add)
			{
				FileShelfService.Instance.FilesChanged += changed;
				FileShelfService.Instance.ShelvesChanged += changed;
			}
			else
			{
				FileShelfService.Instance.FilesChanged -= changed;
				FileShelfService.Instance.ShelvesChanged -= changed;
			}
			break;
		case "message":
			if (add)
			{
				MessageService.Instance.StateChanged += changed;
			}
			else
			{
				MessageService.Instance.StateChanged -= changed;
			}
			break;
		case "aicoding":
			if (add)
			{
				AICodingService.Instance.Updated += changed;
			}
			else
			{
				AICodingService.Instance.Updated -= changed;
			}
			break;
		case "links":
			if (ToolRegistry.Instance.GetTool("links") is LinksTool linksTool)
			{
				if (add)
				{
					linksTool.BookmarksChanged += changed;
				}
				else
				{
					linksTool.BookmarksChanged -= changed;
				}
			}
			if (add)
			{
				privacyChanged = delegate(object s, string key)
				{
					if (key == "ShowWebsiteIconsInLinks")
					{
						Queue();
					}
				};
				AppPreferences.Instance.Changed += privacyChanged;
			}
			else
			{
				AppPreferences.Instance.Changed -= privacyChanged;
			}
			break;
		case "units":
			if (add)
			{
				UnitsState.Changed += cacheChanged;
			}
			else
			{
				UnitsState.Changed -= cacheChanged;
			}
			break;
		case "analytics":
			if (add)
			{
				AnalyticsService.Instance.SettingsChanged += cacheChanged;
			}
			else
			{
				AnalyticsService.Instance.SettingsChanged -= cacheChanged;
			}
			if (add)
			{
				AnalyticsService.Instance.CacheChanged += cacheChanged;
			}
			else
			{
				AnalyticsService.Instance.CacheChanged -= cacheChanged;
			}
			break;
		case "revenue":
			if (add)
			{
				RevenueService.Instance.Changed += cacheChanged;
			}
			else
			{
				RevenueService.Instance.Changed -= cacheChanged;
			}
			break;
		}
	}

	public void Refresh()
	{
		accessories.Children.Clear();
		accessories.Margin = new Thickness(0.0);
		accessories.VerticalAlignment = VerticalAlignment.Bottom;
		try
		{
			switch (WidgetId)
			{
			case "clock":
			{
				ClockSettings instance3 = ClockSettings.Instance;
				WorldClockCity worldClockCity = instance3.WorldClocks.FirstOrDefault((WorldClockCity c) => !c.IsLocal);
				string sub = "Add a world clock";
				if (worldClockCity != null)
				{
					try
					{
						sub = worldClockCity.CityName + "  " + instance3.FormatTime(TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById(worldClockCity.TimeZoneId)), includeSeconds: false);
					}
					catch
					{
					}
				}
				Set(instance3.FormatTime(DateTime.Now, includeSeconds: false), sub, null);
				break;
			}
			case "network":
			{
				var sample = SystemSampler.Latest;
				var snapshot = NetworkService.Instance.GetSnapshot();
				var down = NetworkService.FormatRate(sample?.Down ?? 0.0);
				var up = NetworkService.FormatRate(sample?.Up ?? 0.0);
				var sub = snapshot == null ? "↓ " + down + "   ↑ " + up
					: string.Format("{0}  ·  ↓ {1}  ↑ {2}", snapshot.IsConnected ? snapshot.ConnectionType : "Offline", down, up);
				Set(down, sub, null);
				break;
			}
			case "calendar":
			{
				CalendarService instance = CalendarService.Instance;
				CalendarService.UpcomingItem upcomingItem = instance.UpcomingAfter(DateTime.Today.AddDays(-1.0), 20).FirstOrDefault((CalendarService.UpcomingItem item) => item.At >= DateTime.Now || (item.AllDay && item.At.Date == DateTime.Today));
				Set(DateTime.Today.ToString("ddd d", CultureInfo.CurrentCulture), (upcomingItem != null) ? upcomingItem.Title : "Nothing planned", null);
				break;
			}
			case "music":
			{
				MediaViewState view = MediaService.Instance.View;
				Set(view.Present ? view.Title : "Nothing playing", view.Present ? view.Artist : "Play music in any app", null, empty: true);
				transport.Visibility = ((!view.Present) ? Visibility.Collapsed : Visibility.Visible);
				if (view.Present)
				{
					hero.Opacity = 1.0;
					hero.Margin = new Thickness(48.0, 24.0, 0.0, 0.0);
					secondary.Margin = new Thickness(48.0, 45.0, 0.0, 0.0);
					Button button = (Button)transport.Children[1];
					button.Content = FluentIcons.Create(view.Playing ? "pause" : "play", 12.0, ThemeTokens.Text, filled: false);
					Border border = new Border();
					border.Width = 40.0;
					border.Height = 40.0;
					border.CornerRadius = new CornerRadius(7.0);
					border.Background = ((view.Artwork == null) ? ((Brush)ThemeTokens.Selection) : ((Brush)new ImageBrush(view.Artwork)
					{
						Stretch = Stretch.UniformToFill
					}));
					Border element = border;
					accessories.VerticalAlignment = VerticalAlignment.Top;
					accessories.Margin = new Thickness(0.0, 26.0, 0.0, 0.0);
					accessories.Children.Add(element);
				}
				break;
			}
			case "water":
				Set(TimerService.Instance.HydrationGlassesConsumed + " / " + TimerService.Instance.HydrationDailyTarget, "Glasses today", "+1");
				break;
			case "stopwatch":
			{
				TimerService instance5 = TimerService.Instance;
				Set(instance5.StopwatchElapsed.ToString("hh\\:mm\\:ss"), instance5.StopwatchLaps.Count + " laps", instance5.StopwatchIsRunning ? "Lap" : "▶");
				break;
			}
			case "message":
			{
				MessageService instance6 = MessageService.Instance;
				bool flag = !string.IsNullOrWhiteSpace(instance6.Settings.Text);
				Set(flag ? instance6.Settings.Text : "Your words here", flag ? "Last banner" : "Write a message to show", flag ? "Show" : null, empty: true);
				action.Width = (flag ? 40 : 28);
				break;
			}
			case "units":
				RefreshUnits();
				break;
			case "links":
				RefreshLinks();
				break;
			case "shelf":
				RefreshShelf();
				break;
			case "files":
				if (dropped == null)
				{
					Set("Drop a file here", "Pick a format to convert", null, empty: true);
				}
				else
				{
					ShowFormats();
				}
				break;
			case "analytics":
			{
				AnalyticsService instance2 = AnalyticsService.Instance;
				AnalyticsSite activeSite = instance2.ActiveSite;
				AnalyticsSnapshot analyticsSnapshot = ((activeSite == null) ? null : instance2.Cached(activeSite.Key, AnalyticsRange.Today));
				Set((activeSite == null) ? "Connect a site" : ((analyticsSnapshot == null) ? "Waiting for data" : (analyticsSnapshot.Visitors.HasValue ? analyticsSnapshot.Visitors.Value.ToString("N0") : "No data")), (activeSite == null) ? "See visitors at a glance" : ("Visitors today · " + activeSite.DisplayName), null, analyticsSnapshot == null || !analyticsSnapshot.Visitors.HasValue);
				if (analyticsSnapshot != null)
				{
					accessories.Children.Add(new Border
					{
						Width = 5.0,
						Height = 5.0,
						CornerRadius = new CornerRadius(3.0),
						Background = ThemeTokens.Success
					});
				}
				break;
			}
			case "revenue":
			{
				RevenueService instance4 = RevenueService.Instance;
				RevenueSnapshot revenueSnapshot = (instance4.IsConnected ? instance4.Cached(RevenueRange.Today) : null);
				Set((!instance4.IsConnected) ? "Connect a store" : ((revenueSnapshot == null) ? "Waiting for data" : (revenueSnapshot.Sales ?? 0).ToString("N0")), (!instance4.IsConnected) ? "Connect Stripe or Gumroad" : "Sales today", null, revenueSnapshot == null);
				break;
			}
			case "aicoding":
			{
				AICodingSnapshot cachedSnapshot = AICodingService.Instance.GetCachedSnapshot(AICodingToolType.All);
				Set(cachedSnapshot.HasActivityToday ? cachedSnapshot.TokensToday.ToString("N0") : "No sessions today", cachedSnapshot.HasActivityToday ? ("Tokens · $" + cachedSnapshot.CostToday.ToString("0.00", CultureInfo.InvariantCulture)) : "Use Claude Code or Codex", null, !cachedSnapshot.HasActivityToday);
				break;
			}
			}
		}
		catch
		{
			Set("Not available", "Open " + Name(WidgetId) + " to get started", null, empty: true);
		}
	}

	private void RefreshUnits()
	{
		UnitsState unitsState = UnitsState.Load();
		UnitCategory unitCategory = UnitCatalog.Find(unitsState.Category) ?? UnitCatalog.All[0];
		string value;
		string[] array = (unitsState.Pairs.TryGetValue(unitCategory.Name, out value) ? value.Split('|') : new string[2] { unitCategory.DefaultFrom, unitCategory.DefaultTo });
		Unit unit = unitCategory.Find(array[0]) ?? unitCategory.Find(unitCategory.DefaultFrom);
		Unit unit2 = unitCategory.Find((array.Length > 1) ? array[1] : unitCategory.DefaultTo) ?? unitCategory.Find(unitCategory.DefaultTo);
		decimal v = Expr.Evaluate(unitsState.Value) ?? 0m;
		Set(Format.Number(UnitCatalog.Convert(v, unit, unit2, unitCategory)) + " " + unit2.Symbol, unitsState.Value + " " + unit.Symbol + " → " + unit2.Symbol, "⇄");
	}

	private static Image SiteIcon(ImageSource source)
	{
		Image image = new Image();
		image.Source = source;
		image.Width = 28.0;
		image.Height = 28.0;
		image.HorizontalAlignment = HorizontalAlignment.Center;
		image.VerticalAlignment = VerticalAlignment.Center;
		image.Clip = new RectangleGeometry(new Rect(0.0, 0.0, 28.0, 28.0), 7.0, 7.0);
		return image;
	}

	private void RefreshLinks()
	{
		LinksBookmark[] source = ((!(ToolRegistry.Instance.GetTool("links") is LinksTool linksTool)) ? new LinksBookmark[0] : linksTool.WidgetLinks.ToArray());
		LinksBookmark[] array = source.Where((LinksBookmark x) => x.Pinned).ToArray();
		LinksBookmark[] array2 = ((array.Length > 0) ? array : source.OrderByDescending((LinksBookmark x) => x.AddedTicks).ToArray()).Take(3).ToArray();
		Set((array2.Length == 0) ? "No pinned links" : "", (array2.Length == 0) ? "Pin links in the Links tool" : "", (array2.Length == 0) ? "Open" : null, empty: true);
		action.Width = 56.0;
		action.ToolTip = BuddyTooltip.Create("Open Links");
		accessories.VerticalAlignment = VerticalAlignment.Top;
		accessories.Margin = new Thickness(0.0, 28.0, 0.0, 0.0);
		LinksBookmark[] array3 = array2;
		foreach (LinksBookmark linksBookmark in array3)
		{
			LinksBookmark link = linksBookmark;
			Uri uri;
			string host = (LinkUrl.TryNormalize(link.Url, out uri) ? uri.Host : "link");
			TextBlock textBlock = BuddyTheme.BodyText(string.IsNullOrEmpty(link.Name) ? "L" : link.Name.Substring(0, 1).ToUpperInvariant());
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			Border tile = new Border
			{
				Width = 28.0,
				Height = 28.0,
				CornerRadius = new CornerRadius(7.0),
				Background = LinksTool.DomainFill(host),
				Cursor = Cursors.Hand,
				Child = textBlock,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			ImageSource cached = WebsiteIcons.GetCached(link.Url);
			if (cached != null)
			{
				tile.Child = SiteIcon(cached);
			}
			StackPanel stackPanel = new StackPanel();
			stackPanel.Width = 60.0;
			stackPanel.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			stackPanel.Background = Brushes.Transparent;
			stackPanel.Cursor = Cursors.Hand;
			StackPanel stackPanel2 = stackPanel;
			stackPanel2.Children.Add(tile);
			TextBlock textBlock2 = WidgetCard.Secondary(link.Name);
			textBlock2.TextAlignment = TextAlignment.Center;
			textBlock2.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock2);
			stackPanel2.ToolTip = BuddyTooltip.Create(link.Name);
			AutomationProperties.SetName(stackPanel2, "Open pinned " + link.Name);
			stackPanel2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
			{
				if (!NotchEditing.IsEditing)
				{
					LinksTool.OpenUrl(link.Url);
				}
				e.Handled = true;
			};
			accessories.Children.Add(stackPanel2);
			if (!active)
			{
				continue;
			}
			int version = generation;
			WebsiteIcons.GetAsync(link.Url).ContinueWith(delegate(Task<ImageSource> task)
			{
				if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
				{
					base.Dispatcher.BeginInvoke((Action)delegate
					{
						if (active && version == generation && tile.IsVisible && AppPreferences.Instance.ShowWebsiteIconsInLinks)
						{
							tile.Child = SiteIcon(task.Result);
						}
					});
				}
			});
		}
	}

	private void RefreshShelf()
	{
		ParkedFile[] array = FileShelfService.Instance.ParkedFiles.OrderByDescending((ParkedFile f) => f.AddedUtc).Take(3).ToArray();
		int count = FileShelfService.Instance.ParkedFiles.Count;
		Set(count + ((count == 1) ? " item" : " items"), (count == 0) ? "Drop files to keep them handy" : "Drop files to add more", null);
		ParkedFile[] array2 = array;
		foreach (ParkedFile parkedFile in array2)
		{
			string text = parkedFile.Extension ?? "FILE";
			TextBlock textBlock = BuddyTheme.Text(text.Substring(0, Math.Min(3, text.Length)), 8.0, ThemeTokens.Text2, FontWeights.Medium);
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			Border thumb = new Border
			{
				Width = 20.0,
				Height = 20.0,
				CornerRadius = new CornerRadius(5.0),
				Background = ThemeTokens.Selection,
				Child = textBlock,
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
				ToolTip = parkedFile.FileName
			};
			accessories.Children.Add(thumb);
			Action<ShelfThumbnails.Result> action = delegate(ShelfThumbnails.Result result)
			{
				if (result != null && result.Image != null)
				{
					thumb.Child = new Image
					{
						Source = result.Image,
						Stretch = Stretch.UniformToFill,
						Clip = new RectangleGeometry(new Rect(0.0, 0.0, 20.0, 20.0), 5.0, 5.0)
					};
				}
			};
			action(ShelfThumbnails.GetCached(parkedFile, 48));
			if (active)
			{
				int version = generation;
				action(ShelfThumbnails.Get(parkedFile, 48, () => active && version == generation && thumb.IsVisible, action, base.Dispatcher));
			}
		}
	}

	private void ShowFormats()
	{
		accessories.Children.Clear();
		action.Visibility = Visibility.Collapsed;
		string[] array = new string[3] { "PNG", "JPG", "PDF" };
		foreach (string text in array)
		{
			string format = text;
			BuddyButton buddyButton = new BuddyButton(text, BuddyButtonVariant.Secondary, 24.0);
			buddyButton.FontSize = 10.0;
			buddyButton.Padding = new Thickness(8.0, 2.0, 8.0, 2.0);
			buddyButton.BorderThickness = new Thickness(0.0);
			buddyButton.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
			BuddyButton buddyButton2 = buddyButton;
			AutomationProperties.SetName(buddyButton2, "Widget convert " + text);
			buddyButton2.Click += delegate(object s, RoutedEventArgs e)
			{
				if (!NotchEditing.IsEditing && EntitlementGate.IsEntitled)
				{
					if (ToolRegistry.Instance.GetTool("files") is FilesConverterTool filesConverterTool)
					{
						ToolRegistry.Instance.SetToolEnabled("files", enabled: true);
						if (ToolRouter.Instance.RouteTo("files"))
						{
							filesConverterTool.AddFiles(dropped);
							filesConverterTool.SelectWidgetFormat(format);
						}
					}
					e.Handled = true;
				}
			};
			accessories.Children.Add(buddyButton2);
		}
	}

	public void DoAction()
	{
		switch (WidgetId)
		{
		case "links":
			ToolRouter.Instance.RouteTo("links");
			break;
		case "water":
			TimerService.Instance.AddHydrationGlass();
			break;
		case "stopwatch":
			if (TimerService.Instance.StopwatchIsRunning)
			{
				TimerService.Instance.AddStopwatchLap();
			}
			else
			{
				TimerService.Instance.StartStopwatch();
			}
			break;
		case "music":
			MediaService.Instance.TogglePlayPause();
			break;
		case "message":
			MessageService.Instance.Flash();
			break;
		case "units":
		{
			UnitsState unitsState = UnitsState.Load();
			UnitCategory unitCategory = UnitCatalog.Find(unitsState.Category) ?? UnitCatalog.All[0];
			string value;
			string[] array = (unitsState.Pairs.TryGetValue(unitCategory.Name, out value) ? value.Split('|') : new string[2] { unitCategory.DefaultFrom, unitCategory.DefaultTo });
			if (array.Length == 2)
			{
				unitsState.Pairs[unitCategory.Name] = array[1] + "|" + array[0];
				unitsState.Save();
			}
			break;
		}
		}
		Refresh();
	}
}
