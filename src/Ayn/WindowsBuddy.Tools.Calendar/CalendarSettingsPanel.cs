using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class CalendarSettingsPanel : Grid
{
	private readonly CalendarTool _tool;

	private Grid _holidayPane;

	private Grid _pickerPane;

	private TextBlock _countryValue;

	private TextBlock _holidayStatus;

	private TextBlock _addStatus;

	private TextBlock _addHint;

	private Toggle _holidays;

	private Toggle _observances;

	private TextBox _search;

	private TextBox _addBox;

	private CalList _countries;

	private StackPanel _subs;

	private Border _subsCard;

	private bool _adding;

	private CalendarService Svc => CalendarService.Instance;

	internal TextBox AddBox => _addBox;

	internal TextBlock AddStatus => _addStatus;

	internal bool AddStatusIsError => _addStatus.Foreground == ThemeTokens.Danger;

	internal StackPanel SubscriptionRows => _subs;

	internal TextBlock HolidayStatus => _holidayStatus;

	internal bool IsAdding => _adding;

	public CalendarSettingsPanel(CalendarTool tool)
	{
		_tool = tool;
		base.Background = BuddyTheme.Canvas;
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(300.0)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(22.0)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		BuildHolidays();
		BuildPicker();
		BuildSubscriptions();
	}

	internal static Border Group(UIElement content)
	{
		Border border = new Border();
		border.Background = BuddyTheme.CardFill;
		border.CornerRadius = new CornerRadius(14.0);
		border.Padding = new Thickness(14.0, 4.0, 12.0, 4.0);
		border.Child = content;
		Border border2 = border;
		Action<Border> action = delegate(Border x)
		{
			x.Effect = (ThemeTokens.IsLight ? ThemeTokens.CardElevation : null);
		};
		action(border2);
		ThemeTokens.Subscribe(border2, action);
		return border2;
	}

	private static FrameworkElement SettingRow(string label, UIElement right)
	{
		Grid grid = new Grid();
		grid.Height = 40.0;
		Grid grid2 = grid;
		TextBlock textBlock = BuddyTheme.Text(label, 13.0, BuddyTheme.Text92, AynType.Regular);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(textBlock);
		FrameworkElement frameworkElement = (FrameworkElement)right;
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Right;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(frameworkElement);
		return grid2;
	}

	private void BuildHolidays()
	{
		_holidayPane = new Grid
		{
			Margin = new Thickness(2.0, 0.0, 0.0, 0.0)
		};
		StackPanel stackPanel = new StackPanel();
		BuddyLabel buddyLabel = BuddyTheme.LabelText("Holidays & festivals");
		buddyLabel.Margin = new Thickness(2.0, 2.0, 0.0, 8.0);
		stackPanel.Children.Add(buddyLabel);
		StackPanel stackPanel2 = new StackPanel();
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.Background = Brushes.Transparent;
		stackPanel3.Cursor = Cursors.Hand;
		StackPanel stackPanel4 = stackPanel3;
		_countryValue = BuddyTheme.Text("", 13.0, BuddyTheme.Text50, AynType.Regular);
		_countryValue.VerticalAlignment = VerticalAlignment.Center;
		stackPanel4.Children.Add(_countryValue);
		FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 10.0, BuddyTheme.Text50, filled: false);
		frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
		frameworkElement.RenderTransform = new RotateTransform(-90.0);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		stackPanel4.Children.Add(frameworkElement);
		FrameworkElement frameworkElement2 = SettingRow("Country", stackPanel4);
		((Grid)frameworkElement2).Background = Brushes.Transparent;
		((Grid)frameworkElement2).Cursor = Cursors.Hand;
		CalendarTool.AttachClick(frameworkElement2, OpenPicker, null);
		stackPanel2.Children.Add(frameworkElement2);
		_holidays = new Toggle(on: true, delegate(bool on)
		{
			Svc.SetHolidays(on, _observances.IsOn);
		});
		stackPanel2.Children.Add(SettingRow("Public holidays", _holidays));
		_observances = new Toggle(on: true, delegate(bool on)
		{
			Svc.SetHolidays(_holidays.IsOn, on);
		});
		stackPanel2.Children.Add(SettingRow("Festivals & observances", _observances));
		stackPanel.Children.Add(Group(stackPanel2));
		_holidayStatus = BuddyTheme.Text("", 11.5, BuddyTheme.Text50, FontWeights.Normal);
		_holidayStatus.TextWrapping = TextWrapping.Wrap;
		_holidayStatus.Margin = new Thickness(2.0, 10.0, 0.0, 0.0);
		stackPanel.Children.Add(_holidayStatus);
		TextBlock textBlock = BuddyTheme.Text("From Google's public holiday calendars · refreshed weekly · saved for offline use", 11.0, BuddyTheme.Text30, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(2.0, 3.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock);
		_holidayPane.Children.Add(stackPanel);
		base.Children.Add(_holidayPane);
	}

	private void BuildPicker()
	{
		_pickerPane = new Grid
		{
			Margin = new Thickness(2.0, 0.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		_pickerPane.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_pickerPane.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Grid grid = new Grid();
		grid.Height = 30.0;
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Border border = CalendarTool.QuietIcon(-90.0, "Back", ClosePicker);
		border.Margin = new Thickness(-4.0, 0.0, 6.0, 0.0);
		grid2.Children.Add(border);
		Border border2 = new Border();
		border2.Background = CalendarTool.QuietFill;
		border2.CornerRadius = new CornerRadius(14.0);
		border2.Padding = new Thickness(12.0, 0.0, 12.0, 0.0);
		border2.Height = 28.0;
		Border border3 = border2;
		Grid grid3 = new Grid();
		TextBlock ph = BuddyTheme.Text("Search countries", 13.0, BuddyTheme.Text30, FontWeights.Normal);
		ph.VerticalAlignment = VerticalAlignment.Center;
		ph.IsHitTestVisible = false;
		grid3.Children.Add(ph);
		_search = CalendarTool.CleanBox(13.0);
		_search.TextChanged += delegate
		{
			ph.Visibility = ((_search.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
			FillCountries();
		};
		_search.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				ClosePicker();
			}
			else if (e.Key == Key.Return)
			{
				e.Handled = true;
				string[] array = Filtered().FirstOrDefault();
				if (array != null)
				{
					Pick(array[0]);
				}
			}
		};
		grid3.Children.Add(_search);
		border3.Child = grid3;
		Grid.SetColumn(border3, 1);
		grid2.Children.Add(border3);
		_pickerPane.Children.Add(grid2);
		_countries = new CalList((int k) => new CountryRow(this), delegate(FrameworkElement el, CalList.Entry e)
		{
			((CountryRow)el).Bind((string[])e.Data, ((string[])e.Data)[0] == Svc.Settings.Country);
		});
		Grid.SetRow(_countries, 1);
		_pickerPane.Children.Add(_countries);
		base.Children.Add(_pickerPane);
	}

	private void BuildSubscriptions()
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
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
		grid2.Margin = new Thickness(0.0, 2.0, 0.0, 4.0);
		Grid grid3 = grid2;
		BuddyLabel buddyLabel = BuddyTheme.LabelText("Calendars by link");
		buddyLabel.VerticalAlignment = VerticalAlignment.Center;
		grid3.Children.Add(buddyLabel);
		Border border = CalendarTool.SoftPill("Done", _tool.CloseSettings, 24.0);
		border.HorizontalAlignment = HorizontalAlignment.Right;
		border.Margin = new Thickness(0.0, -4.0, 0.0, -4.0);
		grid3.Children.Add(border);
		grid.Children.Add(grid3);
		TextBlock textBlock = BuddyTheme.Text("Add a Google Calendar, Outlook or iCloud link (https://, http:// or webcal://). Read-only · refreshes every 30 minutes · works offline from the last sync.", 11.5, BuddyTheme.Text50, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		Grid.SetRow(textBlock, 1);
		grid.Children.Add(textBlock);
		Grid grid4 = new Grid();
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Border boxHost = new Border
		{
			Background = CalendarTool.QuietFill,
			CornerRadius = new CornerRadius(15.0),
			Padding = new Thickness(14.0, 0.0, 12.0, 0.0),
			Height = 30.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		Grid grid5 = new Grid();
		_addHint = BuddyTheme.Text("Paste a calendar link (ICS / iCal / webcal)", 13.0, BuddyTheme.Text30, FontWeights.Normal);
		_addHint.VerticalAlignment = VerticalAlignment.Center;
		_addHint.IsHitTestVisible = false;
		_addHint.TextTrimming = TextTrimming.CharacterEllipsis;
		grid5.Children.Add(_addHint);
		_addBox = CalendarTool.CleanBox(13.0);
		_addBox.TextChanged += delegate
		{
			_addHint.Visibility = ((_addBox.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
			if (!_adding && _addStatus.Foreground != BuddyTheme.Text50)
			{
				_addStatus.Text = "";
			}
		};
		_addBox.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				Add();
			}
		};
		_addBox.GotKeyboardFocus += delegate
		{
			boxHost.Background = CalendarTool.QuietFillHover;
		};
		_addBox.LostKeyboardFocus += delegate
		{
			boxHost.Background = CalendarTool.QuietFill;
		};
		grid5.Children.Add(_addBox);
		boxHost.Child = grid5;
		grid4.Children.Add(boxHost);
		Border element = CalendarTool.Pill("Add", Add);
		Grid.SetColumn(element, 1);
		grid4.Children.Add(element);
		Grid.SetRow(grid4, 2);
		grid.Children.Add(grid4);
		_addStatus = BuddyTheme.Text("", 11.5, BuddyTheme.Text50, FontWeights.Normal);
		_addStatus.Margin = new Thickness(14.0, 6.0, 0.0, 0.0);
		_addStatus.TextTrimming = TextTrimming.CharacterEllipsis;
		_addStatus.MinHeight = 15.0;
		Grid.SetRow(_addStatus, 3);
		grid.Children.Add(_addStatus);
		_subs = new StackPanel();
		BuddyScrollViewer buddyScrollViewer = new BuddyScrollViewer();
		buddyScrollViewer.Content = _subs;
		BuddyScrollViewer content = buddyScrollViewer;
		_subsCard = Group(content);
		_subsCard.Padding = new Thickness(12.0, 4.0, 6.0, 4.0);
		_subsCard.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		_subsCard.VerticalAlignment = VerticalAlignment.Top;
		Grid.SetRow(_subsCard, 4);
		grid.Children.Add(_subsCard);
		Grid.SetColumn(grid, 2);
		base.Children.Add(grid);
	}

	public void Refresh()
	{
		CalendarSettings settings = Svc.Settings;
		_countryValue.Text = HolidayCountries.NameOf(settings.Country);
		_holidays.IsOn = settings.Holidays;
		_observances.IsOn = settings.Observances;
		string holidayKey = Svc.HolidayKey;
		CalendarFeeds instance = CalendarFeeds.Instance;
		FeedData feedData = instance.Get(holidayKey);
		string text = instance.StatusOf(holidayKey);
		if (!settings.Holidays && !settings.Observances)
		{
			_holidayStatus.Text = "Holidays are hidden";
		}
		else if (instance.IsBusy(holidayKey))
		{
			_holidayStatus.Text = ((feedData == null) ? "Downloading holidays…" : "Checking for updates…");
		}
		else if (feedData != null)
		{
			int count = feedData.Events.Count;
			DateTime local = instance.CachedAtUtc(holidayKey).ToLocalTime();
			_holidayStatus.Text = count + " holidays · updated " + Ago(local) + ((text != null) ? (" · " + text.ToLowerInvariant()) : "");
		}
		else
		{
			_holidayStatus.Text = text ?? "Not downloaded yet";
		}
		_holidayStatus.Foreground = ((feedData == null && text != null) ? CalendarTool.OverdueInk : BuddyTheme.Text50);
		_subs.Children.Clear();
		foreach (CalendarSubscription subscription in settings.Subscriptions)
		{
			SubscriptionRow subscriptionRow = new SubscriptionRow(this);
			subscriptionRow.Bind(subscription);
			_subs.Children.Add(subscriptionRow);
		}
		_subsCard.Visibility = ((settings.Subscriptions.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		if (_pickerPane.Visibility == Visibility.Visible)
		{
			FillCountries();
		}
	}

	internal static string Ago(DateTime local)
	{
		TimeSpan timeSpan = DateTime.Now - local;
		if (timeSpan.TotalMinutes < 1.0)
		{
			return "just now";
		}
		if (timeSpan.TotalMinutes < 60.0)
		{
			return (int)timeSpan.TotalMinutes + " min ago";
		}
		if (timeSpan.TotalHours < 24.0 && local.Date == DateTime.Today)
		{
			return (int)timeSpan.TotalHours + " h ago";
		}
		if (local.Date == DateTime.Today.AddDays(-1.0))
		{
			return "yesterday";
		}
		return local.ToString((local.Year == DateTime.Today.Year) ? "d MMM" : "d MMM yyyy", Cal.Inv);
	}

	public bool HandleEscape()
	{
		if (_pickerPane.Visibility == Visibility.Visible)
		{
			ClosePicker();
			return true;
		}
		return false;
	}

	public void Reset()
	{
		ClosePicker();
		if (!_adding)
		{
			_addStatus.Text = "";
		}
	}

	private void OpenPicker()
	{
		_holidayPane.Visibility = Visibility.Collapsed;
		_pickerPane.Visibility = Visibility.Visible;
		_search.Text = "";
		FillCountries();
		int num = Array.FindIndex(HolidayCountries.All, (string[] c) => c[0] == Svc.Settings.Country);
		if (num > 3)
		{
			_countries.ScrollBy((num - 3) * 30);
		}
		CalendarTool.FocusLater(_search);
	}

	private void ClosePicker()
	{
		_pickerPane.Visibility = Visibility.Collapsed;
		_holidayPane.Visibility = Visibility.Visible;
	}

	private IEnumerable<string[]> Filtered()
	{
		string q = (_search.Text ?? "").Trim();
		return HolidayCountries.All.Where((string[] c) => q.Length == 0 || c[1].IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(c[0], q, StringComparison.OrdinalIgnoreCase));
	}

	private void FillCountries()
	{
		List<CalList.Entry> entries = Filtered().Select(delegate(string[] c)
		{
			CalList.Entry result = default(CalList.Entry);
			result.Kind = 0;
			result.Height = 30.0;
			result.Data = c;
			return result;
		}).ToList();
		_countries.SetEntries(entries, resetOffset: true);
	}

	internal void Pick(string code)
	{
		Svc.SetCountry(code);
		ClosePicker();
		Refresh();
	}

	private void Add()
	{
		if (_adding)
		{
			return;
		}
		string text = _addBox.Text.Trim();
		if (text.Length == 0)
		{
			SetStatus("Paste the calendar's link first", error: true);
			return;
		}
		_adding = true;
		SetStatus("Checking the link…", error: false);
		Svc.AddSubscription(text, delegate(bool ok, string err)
		{
			_adding = false;
			if (ok)
			{
				_addBox.Text = "";
				CalendarSubscription calendarSubscription = Svc.Settings.Subscriptions.LastOrDefault();
				SetStatus("Added" + ((calendarSubscription != null) ? (" “" + calendarSubscription.Name + "”") : ""), error: false);
			}
			else
			{
				SetStatus(err ?? "Couldn't add that calendar", error: true);
			}
			Refresh();
		});
		Refresh();
	}

	private void SetStatus(string text, bool error)
	{
		_addStatus.Text = text;
		_addStatus.Foreground = (error ? ThemeTokens.Danger : BuddyTheme.Text50);
	}

	internal void Remove(CalendarSubscription s)
	{
		Svc.RemoveSubscription(s.Id);
		Refresh();
		_tool.Toast.Show("Removed “" + s.Name + "”", delegate
		{
			Svc.RestoreSubscription(s);
			Refresh();
		}, delegate
		{
		});
	}

	internal void RefreshNow(CalendarSubscription s)
	{
		Svc.RefreshSubscription(s.Id, delegate
		{
			Refresh();
		});
		Refresh();
	}

	internal void CycleColor(CalendarSubscription s)
	{
		Svc.SetSubscription(s.Id, null, (CalColor)((int)(s.Color + 1) % 4));
		Refresh();
	}

	internal void InvokeAdd()
	{
		Add();
	}
}
