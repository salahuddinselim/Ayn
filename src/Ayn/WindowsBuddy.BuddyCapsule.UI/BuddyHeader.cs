using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyHeader : Border
{
	private ContentControl _petLead;

	private TextBlock _titleText;

	private TextBlock _statusText;

	private Border _statusDivider;

	private ContentControl _statusLead;

	private ContentControl _actionHost;

	private StackPanel _batteryGroup;

	private TextBlock _batteryText;

	private ContentControl _batteryIcon;

	private string _batterySig;

	private Border _editButton;

	private Ellipse _updateDot;

	private EventHandler _updateChanged;

	private EventHandler<SystemTelemetryData> _powerChangedHandler;

	private bool _isDisposed;

	internal bool UpdateDotVisible
	{
		get
		{
			if (_updateDot != null)
			{
				return _updateDot.Visibility == Visibility.Visible;
			}
			return false;
		}
	}

	public event EventHandler EditRequested;

	public event EventHandler PinToggled;

	public event EventHandler SettingsRequested;

	public event EventHandler CollapseRequested;

	public void SetPet(FrameworkElement pet)
	{
		if (_petLead != null)
		{
			_petLead.Content = pet;
		}
	}

	public void SetEditing(bool editing)
	{
		_editButton.Width = (editing ? 54 : 30);
		_editButton.Background = (editing ? ThemeTokens.Selection : Brushes.Transparent);
		_editButton.Child = (editing ? new TextBlock
		{
			Text = "Done",
			FontFamily = BuddyTokens.FontText,
			FontSize = 12.0,
			Foreground = ThemeTokens.Text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		} : FluentIcons.Create("note_edit", 18.0, ThemeTokens.Icon, filled: false));
		AutomationProperties.SetName(_editButton, editing ? "Done" : "Edit");
	}

	public BuddyHeader()
	{
		base.Height = 30.0;
		base.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		base.Padding = new Thickness(12.0, 0.0, 12.0, 0.0);
		base.VerticalAlignment = VerticalAlignment.Top;
		base.Background = Brushes.Transparent;
		base.BorderThickness = new Thickness(0.0);
		BuddyTheme.Crisp(this);
		Grid grid = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		_petLead = new ContentControl
		{
			VerticalAlignment = VerticalAlignment.Center,
			Focusable = false,
			IsHitTestVisible = false
		};
		stackPanel.Children.Add(_petLead);
		_titleText = BuddyTheme.Text(string.Empty, 15.0, BuddyTheme.Text100, FontWeights.SemiBold);
		_titleText.FontFamily = BuddyTheme.DisplayFont;
		_titleText.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Children.Add(_titleText);
		_statusLead = new ContentControl
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
			Focusable = false
		};
		stackPanel.Children.Add(_statusLead);
		_statusDivider = new Border
		{
			Width = 1.0,
			Height = 20.0,
			Background = ThemeTokens.Text,
			Opacity = 0.15,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(16.0, 0.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		stackPanel.Children.Add(_statusDivider);
		_statusText = BuddyTheme.Text(string.Empty, 15.0, ThemeTokens.Text, FontWeights.Normal);
		_statusText.Opacity = 0.6;
		_statusText.FontFamily = BuddyTheme.DisplayFont;
		_statusText.VerticalAlignment = VerticalAlignment.Center;
		_statusText.Margin = new Thickness(16.0, 0.0, 0.0, 0.0);
		stackPanel.Children.Add(_statusText);
		grid.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		_actionHost = new ContentControl
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0)
		};
		stackPanel2.Children.Add(_actionHost);
		_batteryGroup = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 14.0, 0.0)
		};
		_batteryText = BuddyTheme.Text(string.Empty, 11.5, BuddyTheme.Text92, FontWeights.Medium);
		_batteryText.VerticalAlignment = VerticalAlignment.Center;
		_batteryText.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		Typography.SetNumeralAlignment(_batteryText, FontNumeralAlignment.Tabular);
		_batteryGroup.Children.Add(_batteryText);
		_batteryIcon = new ContentControl
		{
			Width = 18.0,
			Height = 9.0,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		_batteryGroup.Children.Add(_batteryIcon);
		_batteryGroup.Children.Add(new Border
		{
			Width = 1.5,
			Height = 20.0,
			Background = ThemeTokens.PanelRule,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(16.0, 0.0, 2.0, 0.0)
		});
		stackPanel2.Children.Add(_batteryGroup);
		_editButton = new Border
		{
			Width = 30.0,
			Height = 30.0,
			CornerRadius = new CornerRadius(15.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Focusable = true,
			ToolTip = BuddyTooltip.Create("Edit"),
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		};
		Border editButton = _editButton;
		MouseEventHandler value = delegate
		{
			if (_editButton.Width < 50.0)
			{
				_editButton.Background = ThemeTokens.Surface2;
			}
		};
		editButton.MouseEnter += value;
		_editButton.MouseLeave += delegate
		{
			if (_editButton.Width < 50.0)
			{
				_editButton.Background = Brushes.Transparent;
			}
		};
		BuddyTheme.Pressable(_editButton);
		SetEditing(editing: false);
		_editButton.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		_editButton.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			this.EditRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		};
		_editButton.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				this.EditRequested?.Invoke(this, EventArgs.Empty);
				e.Handled = true;
			}
		};
		stackPanel2.Children.Add(_editButton);
		FrameworkElement element = CircleButton("settings", "Settings", delegate
		{
			this.SettingsRequested?.Invoke(this, EventArgs.Empty);
		});
		_updateDot = new Ellipse
		{
			Width = 7.0,
			Height = 7.0,
			Fill = ThemeTokens.Accent,
			IsHitTestVisible = false,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 3.0, 3.0, 0.0),
			Visibility = ((!StoreUpdateService.UpdateAvailable) ? Visibility.Collapsed : Visibility.Visible)
		};
		AutomationProperties.SetName(_updateDot, "Update available");
		Grid grid2 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		};
		grid2.Children.Add(element);
		grid2.Children.Add(_updateDot);
		stackPanel2.Children.Add(grid2);
		_updateChanged = delegate
		{
			try
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					if (_updateDot != null)
					{
						_updateDot.Visibility = ((!StoreUpdateService.UpdateAvailable) ? Visibility.Collapsed : Visibility.Visible);
					}
				});
			}
			catch
			{
			}
		};
		StoreUpdateService.Changed += _updateChanged;
		stackPanel2.Children.Add(CircleButton("dismiss", "Close (Esc)", delegate
		{
			this.CollapseRequested?.Invoke(this, EventArgs.Empty);
		}));
		Grid.SetColumn(stackPanel2, 1);
		grid.Children.Add(stackPanel2);
		Child = grid;
		_powerChangedHandler = delegate
		{
			try
			{
				base.Dispatcher.BeginInvoke(new Action(UpdateBatteryVisual));
			}
			catch
			{
			}
		};
		try
		{
			SystemMonitorService.PowerChanged += _powerChangedHandler;
		}
		catch
		{
		}
		base.Unloaded += delegate
		{
			Dispose();
		};
		UpdateBatteryVisual();
	}

	private static FrameworkElement CircleButton(string icon, string tooltip, Action onClick)
	{
		double num = 30.0;
		double g = 18.0;
		ContentControl glyph = new ContentControl
		{
			Width = g,
			Height = g,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			Content = FluentIcons.Create(icon, g, ThemeTokens.Icon, filled: false)
		};
		RenderOptions.SetClearTypeHint(glyph, ClearTypeHint.Enabled);
		Border b = new Border
		{
			Width = num,
			Height = num,
			CornerRadius = new CornerRadius(num / 2.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Focusable = true,
			FocusVisualStyle = null,
			Child = glyph,
			ToolTip = BuddyTooltip.Create(tooltip)
		};
		KeyboardNavigation.SetIsTabStop(b, isTabStop: true);
		BuddyTheme.Pressable(b);
		bool pressed = false;
		b.MouseEnter += delegate
		{
			b.Background = ThemeTokens.Surface2;
			glyph.Content = FluentIcons.Create(icon, g, ThemeTokens.Text, filled: false);
		};
		b.MouseLeave += delegate
		{
			pressed = false;
			b.Background = Brushes.Transparent;
			glyph.Content = FluentIcons.Create(icon, g, ThemeTokens.Icon, filled: false);
		};
		b.MouseLeftButtonDown += delegate(object x, MouseButtonEventArgs e)
		{
			pressed = true;
			e.Handled = true;
		};
		b.MouseLeftButtonUp += delegate(object x, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (pressed)
			{
				pressed = false;
				onClick();
			}
		};
		b.KeyDown += delegate(object x, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				onClick();
			}
		};
		b.GotKeyboardFocus += delegate
		{
			b.Background = ThemeTokens.Surface2;
		};
		b.LostKeyboardFocus += delegate
		{
			b.Background = (b.IsMouseOver ? ThemeTokens.Surface2 : Brushes.Transparent);
		};
		return b;
	}

	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}
		_isDisposed = true;
		try
		{
			if (_powerChangedHandler != null)
			{
				SystemMonitorService.PowerChanged -= _powerChangedHandler;
				_powerChangedHandler = null;
			}
			if (_updateChanged != null)
			{
				StoreUpdateService.Changed -= _updateChanged;
				_updateChanged = null;
			}
		}
		catch
		{
		}
	}

	public void UpdateBatteryVisual()
	{
		try
		{
			SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
			if (liveTelemetry == null || !liveTelemetry.HasBattery)
			{
				_batteryGroup.Visibility = Visibility.Collapsed;
				return;
			}
			int num = Math.Max(0, Math.Min(100, liveTelemetry.BatteryPercent));
			bool isCharging = liveTelemetry.IsCharging;
			bool flag = liveTelemetry.IsACConnected && (liveTelemetry.IsFullyCharged || num == 100);
			string text = num + (isCharging ? "c" : "") + (flag ? "f" : "") + (liveTelemetry.IsACConnected ? "a" : "");
			if (text == _batterySig)
			{
				return;
			}
			_batterySig = text;
			if (!isCharging && !flag)
			{
				if (num > 20)
				{
					_ = BuddyTheme.Text92;
				}
				else
				{
					_ = BuddyTheme.Alert;
				}
			}
			else
			{
				new SolidColorBrush(Color.FromRgb(48, 209, 88));
			}
			_batteryGroup.Visibility = Visibility.Visible;
			_batteryText.Text = num + "%";
			_batteryText.Foreground = BuddyTheme.Text92;
			_batteryIcon.Content = NotchGlyphs.CreateBattery(9.0, num, isCharging, BuddyTheme.Text92, flag, liveTelemetry.IsACConnected);
			_batteryGroup.ToolTip = BuddyTooltip.Create(flag ? "Charged" : (isCharging ? "Charging" : (liveTelemetry.IsACConnected ? "Plugged in" : "On battery")));
		}
		catch
		{
		}
	}

	private static FrameworkElement BatteryGlyph(int pct, bool charging, Brush ink)
	{
		return NotchGlyphs.CreateBattery(9.0, pct, charging, ink);
	}

	public void UpdatePinVisual(bool isPinned)
	{
	}

	public void SetContent(UIElement icon, string title, string status, UIElement action = null, bool animate = false)
	{
		_titleText.Text = title ?? string.Empty;
		UpdateStatus(status);
		if (_actionHost.Content != action)
		{
			_actionHost.Content = null;
			if (action != null)
			{
				ToolRouter.DetachFromParent(action);
			}
			_actionHost.Content = action;
		}
		_actionHost.Visibility = ((action == null) ? Visibility.Collapsed : Visibility.Visible);
		UpdateBatteryVisual();
	}

	public void UpdateStatus(string status)
	{
		string text = null;
		int num = status?.IndexOf('\t') ?? (-1);
		if (num >= 0)
		{
			text = status.Substring(num + 1);
			status = status.Substring(0, num);
		}
		_statusText.ToolTip = (string.IsNullOrEmpty(text) ? null : text);
		_statusText.Text = status ?? string.Empty;
		if (_statusDivider != null)
		{
			_statusDivider.Visibility = (string.IsNullOrEmpty(_statusText.Text) ? Visibility.Collapsed : Visibility.Visible);
		}
		_statusText.Visibility = (string.IsNullOrEmpty(status) ? Visibility.Collapsed : Visibility.Visible);
	}

	public void SetStatusLead(UIElement lead)
	{
		if (_statusLead.Content != lead)
		{
			DependencyPropertyDescriptor dependencyPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement));
			if (_statusLead.Content is UIElement component)
			{
				dependencyPropertyDescriptor.RemoveValueChanged(component, OnStatusLeadVisibility);
			}
			_statusLead.Content = null;
			if (lead != null)
			{
				ToolRouter.DetachFromParent(lead);
				dependencyPropertyDescriptor.AddValueChanged(lead, OnStatusLeadVisibility);
			}
			_statusLead.Content = lead;
			SyncStatusLead();
		}
	}

	private void OnStatusLeadVisibility(object sender, EventArgs e)
	{
		SyncStatusLead();
	}

	private void SyncStatusLead()
	{
		bool flag = _statusLead.Content is UIElement uIElement && uIElement.Visibility == Visibility.Visible;
		_statusLead.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_statusText.Margin = new Thickness((!flag) ? 10 : 0, 0.0, 0.0, 0.0);
	}
}
