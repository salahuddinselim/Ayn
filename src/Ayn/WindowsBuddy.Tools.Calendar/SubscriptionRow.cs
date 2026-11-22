using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Calendar;

internal sealed class SubscriptionRow : Grid
{
	private readonly CalendarSettingsPanel _panel;

	private readonly Ellipse _dot;

	private readonly TextBlock _name;

	private readonly TextBlock _status;

	private CalendarSubscription _s;

	public CalendarSubscription Subscription => _s;

	public string StatusText => _status.Text;

	public string NameText => _name.Text;

	public SubscriptionRow(CalendarSettingsPanel panel)
	{
		_panel = panel;
		base.Height = 48.0;
		base.Background = Brushes.Transparent;
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(24.0)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Border border = new Border
		{
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Width = 22.0,
			Height = 22.0,
			ToolTip = BuddyTooltip.Create("Change colour")
		};
		_dot = new Ellipse
		{
			Width = 10.0,
			Height = 10.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center
		};
		border.Child = _dot;
		Action click = delegate
		{
			if (_s != null)
			{
				_panel.CycleColor(_s);
			}
		};
		CalendarTool.AttachClick(border, click, null);
		base.Children.Add(border);
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_name = BuddyTheme.Text("", 13.0, BuddyTheme.Text92, AynType.Medium);
		_name.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel.Children.Add(_name);
		_status = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, FontWeights.Normal);
		_status.TextTrimming = TextTrimming.CharacterEllipsis;
		_status.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
		stackPanel.Children.Add(_status);
		Grid.SetColumn(stackPanel, 1);
		base.Children.Add(stackPanel);
		Border element = IconButton("arrow_reset", "Refresh now", delegate
		{
			if (_s != null)
			{
				_panel.RefreshNow(_s);
			}
		});
		Grid.SetColumn(element, 2);
		base.Children.Add(element);
		Border element2 = IconButton("dismiss", "Remove calendar", delegate
		{
			if (_s != null)
			{
				_panel.Remove(_s);
			}
		});
		Grid.SetColumn(element2, 3);
		base.Children.Add(element2);
	}

	private static Border IconButton(string icon, string tip, Action click)
	{
		FrameworkElement frameworkElement = FluentIcons.Create(icon, 13.0, BuddyTheme.Text50, filled: false);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		Border b = new Border
		{
			Width = 28.0,
			Height = 28.0,
			CornerRadius = new CornerRadius(14.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Child = frameworkElement,
			ToolTip = BuddyTooltip.Create(tip),
			Margin = new Thickness(2.0, 0.0, 0.0, 0.0)
		};
		b.MouseEnter += delegate
		{
			b.Background = CalendarTool.HoverFill;
		};
		b.MouseLeave += delegate
		{
			b.Background = Brushes.Transparent;
		};
		CalendarTool.AttachClick(b, click, null);
		CalendarTool.MakeKeyboardButton(b, click, Brushes.Transparent, CalendarTool.HoverFill);
		return b;
	}

	public void Bind(CalendarSubscription s)
	{
		_s = s;
		_dot.Fill = CalendarTool.ColorOf(s.Color);
		_name.Text = s.Name;
		CalendarFeeds instance = CalendarFeeds.Instance;
		FeedData feedData = instance.Get(s.Key);
		string text = ((feedData != null) ? (feedData.Events.Count + ((feedData.Events.Count == 1) ? " event" : " events")) : null);
		bool flag = false;
		string text2;
		if (!s.Enabled)
		{
			text2 = "Hidden";
		}
		else if (instance.IsBusy(s.Key))
		{
			text2 = ((text != null) ? (text + " · ") : "") + "Syncing…";
		}
		else if (string.IsNullOrEmpty(s.LastError))
		{
			text2 = ((s.LastSyncUtc <= 0) ? "Waiting to sync" : (((text != null) ? (text + " · ") : "") + "Synced " + CalendarSettingsPanel.Ago(new DateTime(s.LastSyncUtc, DateTimeKind.Utc).ToLocalTime())));
		}
		else
		{
			text2 = s.LastError + ((text != null) ? (" · showing " + text + " from the last sync") : "");
			flag = true;
		}
		_status.Text = text2;
		_status.Foreground = (flag ? ThemeTokens.Danger : BuddyTheme.Text50);
		_status.ToolTip = (flag ? BuddyTooltip.Create(text2) : null);
		base.Opacity = (s.Enabled ? 1.0 : 0.6);
	}
}
