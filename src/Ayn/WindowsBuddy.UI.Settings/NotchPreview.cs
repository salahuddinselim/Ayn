using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.Tools.Clock;

namespace WindowsBuddy.UI.Settings;

internal sealed class NotchPreview : Border
{
	private readonly Border _contentCanvas;

	private readonly FrameworkElement _modeToggle;

	private string _currentMode = "resting";

	private TextBlock _timeText;

	private EdgeLightWpf _edge;

	private DispatcherTimer _tick;

	private readonly PageContext _ctx;

	private bool _alive = true;

	private bool _telemetryPending;

	private int _batteryPercent = 80;

	private bool _charging;

	private DateTime _telemetryAt;

	public NotchPreview(PageContext ctx)
	{
		_ctx = ctx;
		Action a = delegate
		{
			_alive = false;
			if (_edge != null)
			{
				_edge.Dispose();
				_edge = null;
			}
		};
		ctx.OnClose(a);
		base.Height = 180.0;
		base.Background = SettingsPreviewStage.Background;
		base.BorderThickness = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(10.0);
		base.ClipToBounds = true;
		base.Tag = "setting-card-preview";
		base.ToolTip = "Preview";
		base.Margin = new Thickness(0.0, 0.0, 0.0, 16.0);
		Grid grid = new Grid
		{
			Height = 180.0,
			ClipToBounds = true
		};
		grid.Background = SettingsPreviewStage.Background;
		_contentCanvas = new Border
		{
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Height = 180.0
		};
		grid.Children.Add(_contentCanvas);
		_modeToggle = BuildPreviewToggle();
		grid.Children.Add(_modeToggle);
		Child = grid;
		EventHandler onCfg = delegate
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				UpdateContent();
			});
		};
		EventHandler<NotchSettingsChangedEventArgs> onNotch = delegate(object s, NotchSettingsChangedEventArgs e)
		{
			onCfg(s, e);
		};
		NotchSettings.Instance.Changed += onNotch;
		ToolRegistry.Instance.ToolsUpdated += onCfg;
		ClockSettings.Instance.SettingsChanged += onCfg;
		_tick = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_tick.Tick += delegate
		{
			UpdateTime();
			RequestTelemetry();
		};
		base.Loaded += delegate
		{
			if (base.IsVisible)
			{
				if (_contentCanvas.Child == null)
				{
					UpdateContent();
				}
				_tick.Start();
				base.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RequestTelemetry));
			}
		};
		base.Unloaded += delegate
		{
			_tick.Stop();
		};
		ctx.OnLeave(delegate
		{
			_tick.Stop();
			NotchSettings.Instance.Changed -= onNotch;
			ToolRegistry.Instance.ToolsUpdated -= onCfg;
			ClockSettings.Instance.SettingsChanged -= onCfg;
		});
		ctx.OnReturn(delegate
		{
			NotchSettings.Instance.Changed += onNotch;
			ToolRegistry.Instance.ToolsUpdated += onCfg;
			ClockSettings.Instance.SettingsChanged += onCfg;
			_tick.Start();
			UpdateContent();
		});
	}

	private FrameworkElement BuildPreviewToggle()
	{
		Border border = new Border();
		border.Background = ThemeTokens.Surface;
		border.BorderBrush = ThemeTokens.BorderStrong;
		border.BorderThickness = new Thickness(0.0);
		border.CornerRadius = new CornerRadius(16.0);
		border.Padding = new Thickness(4.0);
		border.Height = 40.0;
		border.HorizontalAlignment = HorizontalAlignment.Right;
		border.VerticalAlignment = VerticalAlignment.Top;
		border.Margin = new Thickness(0.0, 12.0, 16.0, 0.0);
		Border border2 = border;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		StackPanel stackPanel2 = stackPanel;
		SClickable restingBtn = new SClickable(13.0, "Resting preview")
		{
			CornerRadius = new CornerRadius(13.0),
			Padding = new Thickness(12.0, 0.0, 12.0, 0.0),
			Cursor = Cursors.Hand,
			Height = 32.0
		};
		TextBlock restingTxt = SettingsTheme.Text("Resting", 12.0, ThemeTokens.Text, FontWeights.Medium);
		restingTxt.VerticalAlignment = VerticalAlignment.Center;
		restingBtn.Child = restingTxt;
		restingBtn.VisualRole = SVisualRole.Ghost;
		SClickable hoverBtn = new SClickable(13.0, "Hover preview")
		{
			CornerRadius = new CornerRadius(13.0),
			Padding = new Thickness(12.0, 0.0, 12.0, 0.0),
			Cursor = Cursors.Hand,
			Height = 32.0
		};
		TextBlock hoverTxt = SettingsTheme.Text("Hover", 12.0, ThemeTokens.Text2, FontWeights.Normal);
		hoverTxt.VerticalAlignment = VerticalAlignment.Center;
		hoverBtn.Child = hoverTxt;
		hoverBtn.VisualRole = SVisualRole.Ghost;
		Action updatePills = delegate
		{
			bool flag = _currentMode != "hover";
			restingBtn.IsSelected = flag;
			restingTxt.Foreground = (flag ? ThemeTokens.Text : ThemeTokens.Text2);
			restingTxt.FontWeight = FontWeights.Medium;
			hoverBtn.IsSelected = !flag;
			hoverTxt.Foreground = ((!flag) ? ThemeTokens.Text : ThemeTokens.Text2);
			hoverTxt.FontWeight = FontWeights.Medium;
		};
		restingBtn.Click += delegate
		{
			if (_currentMode != "resting")
			{
				_currentMode = "resting";
				updatePills();
				UpdateContent();
			}
		};
		hoverBtn.Click += delegate
		{
			if (_currentMode != "hover")
			{
				_currentMode = "hover";
				updatePills();
				UpdateContent();
			}
		};
		updatePills();
		stackPanel2.Children.Add(restingBtn);
		stackPanel2.Children.Add(hoverBtn);
		border2.Child = stackPanel2;
		return border2;
	}

	private void UpdateContent()
	{
		if (_currentMode == "hover")
		{
			RebuildHover();
		}
		else
		{
			RebuildRest();
		}
	}

	private void RequestTelemetry()
	{
		if (!_alive || !_ctx.CanUpdateUi || _telemetryPending || DateTime.UtcNow - _telemetryAt < TimeSpan.FromSeconds(2.0))
		{
			return;
		}
		_telemetryPending = true;
		Task.Run(() => SystemMonitorService.GetLiveTelemetry()).ContinueWith(delegate(Task<SystemTelemetryData> task)
		{
			if (task.IsFaulted)
			{
				_ = task.Exception;
			}
			if (base.Dispatcher.HasShutdownStarted)
			{
				return;
			}
			try
			{
				base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
				{
					_telemetryPending = false;
					_telemetryAt = DateTime.UtcNow;
					if (_alive && task.Status == TaskStatus.RanToCompletion && task.Result.HasBattery)
					{
						int batteryPercent = task.Result.BatteryPercent;
						bool isCharging = task.Result.IsCharging;
						bool flag = batteryPercent != _batteryPercent || isCharging != _charging;
						_batteryPercent = batteryPercent;
						_charging = isCharging;
						if (flag && _ctx.CanUpdateUi)
						{
							UpdateContent();
						}
					}
				});
			}
			catch (InvalidOperationException)
			{
			}
		});
	}

	private string TimeString()
	{
		NotchConfig instance = NotchConfig.Instance;
		DateTime now = DateTime.Now;
		string text = ClockSettings.Instance.FormatTime(now, includeSeconds: false);
		if (instance.ShowDateInNotch && !instance.NotchAnalogClock)
		{
			text = now.ToString("ddd d", CultureInfo.CurrentCulture) + " · " + text;
		}
		return text;
	}

	private void UpdateTime()
	{
		if (_timeText != null)
		{
			_timeText.Text = TimeString();
		}
	}

	private void RebuildRest()
	{
		NotchConfig instance = NotchConfig.Instance;
		double collapsedWidth = instance.GetCollapsedWidth();
		double collapsedHeight = instance.GetCollapsedHeight();
		double top = ((instance.StyleFamily == "Island") ? 5.0 : 0.0);
		Grid grid = new Grid();
		grid.Width = collapsedWidth;
		grid.Height = collapsedHeight;
		grid.Opacity = Math.Max(0.35, instance.Opacity);
		Grid grid2 = grid;
		grid2.Children.Add(new Path
		{
			Data = NotchGeometryBuilder.BuildGeometry(instance.Style, collapsedWidth, collapsedHeight, instance.CornerRadius),
			Fill = Brushes.Black
		});
		if (instance.BorderOutlineEnabled)
		{
			grid2.Children.Add(new Path
			{
				Data = NotchGeometryBuilder.BuildSilhouetteGeometry(instance.Style, collapsedWidth, collapsedHeight, instance.CornerRadius),
				Stroke = instance.GetBorderBrush(),
				StrokeThickness = instance.BorderThickness
			});
		}
		Brush white = Brushes.White;
		UIElement left = null;
		_timeText = null;
		if (instance.ShowTime)
		{
			if (instance.NotchAnalogClock)
			{
				FrameworkElement frameworkElement = Glyphs.Create("clock", CapsuleWindow.RestingGlyphSize, white);
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				left = frameworkElement;
			}
			else
			{
				_timeText = CapsuleWindow.NotchText(TimeString(), CapsuleWindow.PrimaryFontSize, AynType.Medium, white);
				left = _timeText;
			}
		}
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		double restingGlyphSize = CapsuleWindow.RestingGlyphSize;
		if (instance.ShowBattery)
		{
			int batteryPercent = _batteryPercent;
			bool charging = _charging;
			if (charging)
			{
				stackPanel2.Children.Add(Spaced(NotchGlyphs.Create("bolt", Math.Round(restingGlyphSize * 0.9), NotchGlyphs.GreenBrush)));
			}
			stackPanel2.Children.Add(Spaced(NotchGlyphs.CreateBattery(CapsuleWindow.BatteryHeight, batteryPercent, charging, white)));
			stackPanel2.Children.Add(CapsuleWindow.NotchText(batteryPercent.ToString(), CapsuleWindow.NumberFontSize, FontWeights.Medium, charging ? NotchGlyphs.GreenBrush : white));
		}
		Grid grid3 = new Grid();
		grid3.Margin = new Thickness(0.0, top, 0.0, 0.0);
		Grid grid4 = grid3;
		grid4.Children.Add(CapsuleWindow.BuildRestingStyleRow(left, (stackPanel2.Children.Count > 0) ? stackPanel2 : null, new Size(collapsedWidth, collapsedHeight)));
		grid2.Children.Add(grid4);
		if (_edge != null)
		{
			_edge.Dispose();
		}
		_edge = null;
		if (instance.EdgeLightEnabled)
		{
			_edge = new EdgeLightWpf(30, preview: true);
			_edge.SetOutline(EdgeLightOutline.For(instance.Style, collapsedWidth, collapsedHeight, instance.CornerRadius), default(Point), 1.0);
			_edge.SetLook(EdgeLightLook.FromConfig(instance));
			_edge.SetMode(EdgeLightMode.Animated);
			grid2.Children.Insert(0, _edge);
		}
		Viewbox viewbox = new Viewbox();
		viewbox.Child = grid2;
		viewbox.StretchDirection = StretchDirection.DownOnly;
		viewbox.VerticalAlignment = VerticalAlignment.Top;
		viewbox.HorizontalAlignment = HorizontalAlignment.Center;
		viewbox.Margin = new Thickness(12.0, 0.0, 12.0, 0.0);
		Viewbox viewbox2 = viewbox;
		Grid grid5 = new Grid();
		NotchVisibilityMode visibilityMode = NotchSettings.Instance.VisibilityMode;
		Func<string, TextBlock> func = delegate(string text)
		{
			TextBlock textBlock2 = SettingsTheme.Text(text, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
			textBlock2.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock2.TextAlignment = TextAlignment.Center;
			textBlock2.TextWrapping = TextWrapping.Wrap;
			textBlock2.Margin = new Thickness(16.0, 64.0, 16.0, 0.0);
			return textBlock2;
		};
		if (!ToolRegistry.Instance.CapsuleEnabled || visibilityMode == NotchVisibilityMode.Hidden)
		{
			grid5.Children.Add(viewbox2);
			viewbox2.Opacity = 0.2;
			grid5.Children.Add(func((!ToolRegistry.Instance.CapsuleEnabled) ? "Notch turned off" : "Hidden · open from the tray icon or your shortcut"));
		}
		else
		{
			switch (visibilityMode)
			{
			case NotchVisibilityMode.Minimal:
				grid5.Children.Add(CollapsedPreview(mini: false));
				grid5.Children.Add(func("A small arrow; hover it to peek, click to open"));
				break;
			case NotchVisibilityMode.Smart:
			{
				grid5.Children.Add(viewbox2);
				FrameworkElement frameworkElement2 = CollapsedPreview(NotchSettings.Instance.OverMaximizedLook == OverMaximizedLook.MiniNotch);
				frameworkElement2.Margin = new Thickness(0.0, 64.0, 0.0, 0.0);
				grid5.Children.Add(frameworkElement2);
				TextBlock textBlock = func("On the desktop (top) · over maximized apps");
				textBlock.Margin = new Thickness(16.0, 80.0, 16.0, 0.0);
				grid5.Children.Add(textBlock);
				break;
			}
			default:
				grid5.Children.Add(viewbox2);
				break;
			}
		}
		_contentCanvas.Child = grid5;
	}

	private static FrameworkElement CollapsedPreview(bool mini)
	{
		CapsuleWindow.CollapsedLook collapsedLook = CapsuleWindow.ComputeCollapsedLook(mini, eyes: false, NotchSettings.Instance.RestingSize, (NotchConfig.Instance != null) ? NotchConfig.Instance.CornerRadius : 16.0, 1.0);
		Grid grid = new Grid();
		grid.Width = collapsedLook.W;
		grid.Height = collapsedLook.H;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.VerticalAlignment = VerticalAlignment.Top;
		Grid grid2 = grid;
		grid2.Children.Add(new Path
		{
			Data = NotchGeometryBuilder.BuildMiniGeometry(collapsedLook.W, collapsedLook.H, collapsedLook.Scale, collapsedLook.Radius),
			Fill = Brushes.Black
		});
		if (!mini)
		{
			grid2.Children.Add(new Path
			{
				Data = Geometry.Parse("M0,0 L3.5,3.25 L7,0"),
				Stroke = Brushes.White,
				StrokeThickness = 1.35,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round,
				StrokeLineJoin = PenLineJoin.Round,
				Opacity = 0.7,
				Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			});
		}
		Viewbox viewbox = new Viewbox();
		viewbox.Child = grid2;
		viewbox.StretchDirection = StretchDirection.DownOnly;
		viewbox.VerticalAlignment = VerticalAlignment.Top;
		viewbox.HorizontalAlignment = HorizontalAlignment.Center;
		viewbox.Margin = new Thickness(12.0, 0.0, 12.0, 0.0);
		return viewbox;
	}

	private static FrameworkElement Spaced(FrameworkElement e)
	{
		e.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
		e.VerticalAlignment = VerticalAlignment.Center;
		return e;
	}

	private void RebuildHover()
	{
		Grid grid = new Grid();
		grid.Width = 752.0;
		grid.Height = 352.0;
		Grid grid2 = grid;
		grid2.Children.Add(new Path
		{
			Data = NotchGeometryBuilder.BuildExpandedGeometry(752.0, 352.0),
			Fill = ThemeTokens.Bg
		});
		Grid grid3 = new Grid();
		grid3.Height = 30.0;
		grid3.VerticalAlignment = VerticalAlignment.Top;
		grid3.Margin = new Thickness(40.0, 16.0, 40.0, 0.0);
		Grid grid4 = grid3;
		IBuddyTool buddyTool = ModuleCatalog.DockOrder().FirstOrDefault((IBuddyTool t) => t.IsEnabled);
		ModuleInfo moduleInfo = ((buddyTool != null) ? ModuleCatalog.For(buddyTool) : ModuleCatalog.Get("home"));
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(FluentIcons.Create(moduleInfo.Glyph, 22.0, ThemeTokens.Text, filled: true));
		TextBlock textBlock = SettingsTheme.Text(moduleInfo.Name, 20.0, ThemeTokens.Text, AynType.Semibold);
		textBlock.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		grid4.Children.Add(stackPanel2);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 26.0;
		ellipse.Height = 26.0;
		ellipse.Fill = ThemeTokens.Surface2;
		ellipse.HorizontalAlignment = HorizontalAlignment.Right;
		Ellipse element = ellipse;
		grid4.Children.Add(element);
		grid2.Children.Add(grid4);
		Grid grid5 = new Grid();
		grid5.Margin = new Thickness(40.0, 64.0, 40.0, 88.0);
		Grid grid6 = grid5;
		grid6.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.4, GridUnitType.Star)
		});
		grid6.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(16.0)
		});
		grid6.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Border border = new Border();
		border.Background = ThemeTokens.Surface;
		border.BorderBrush = ThemeTokens.Border;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(16.0);
		Border element2 = border;
		Border border2 = new Border();
		border2.Background = ThemeTokens.Surface;
		border2.BorderBrush = ThemeTokens.Border;
		border2.BorderThickness = new Thickness(1.0);
		border2.CornerRadius = new CornerRadius(16.0);
		Border element3 = border2;
		Grid.SetColumn(element3, 2);
		grid6.Children.Add(element2);
		grid6.Children.Add(element3);
		grid2.Children.Add(grid6);
		Border border3 = new Border();
		border3.Background = ThemeTokens.Surface;
		border3.BorderBrush = ThemeTokens.Border;
		border3.BorderThickness = new Thickness(1.0);
		border3.CornerRadius = new CornerRadius(19.0);
		border3.Height = 44.0;
		border3.Padding = new Thickness(8.0, 0.0, 8.0, 0.0);
		border3.VerticalAlignment = VerticalAlignment.Bottom;
		border3.HorizontalAlignment = HorizontalAlignment.Center;
		border3.Margin = new Thickness(0.0, 0.0, 0.0, 24.0);
		Border border4 = border3;
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		bool flag = true;
		foreach (IBuddyTool item in (from x in ModuleCatalog.DockOrder()
			where x.IsEnabled
			select x).Take(18))
		{
			ModuleInfo moduleInfo2 = ModuleCatalog.For(item);
			Brush fill = (flag ? ThemeTokens.AccentFg : ThemeTokens.Icon);
			Grid grid7 = new Grid();
			grid7.Width = 34.0;
			grid7.Height = 34.0;
			grid7.Margin = new Thickness(4.0, 0.0, 4.0, 0.0);
			Grid grid8 = grid7;
			if (flag)
			{
				grid8.Children.Add(new Ellipse
				{
					Fill = ThemeTokens.Accent
				});
			}
			FrameworkElement frameworkElement = FluentIcons.Create(moduleInfo2.Glyph, 20.0, fill, filled: true);
			frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			grid8.Children.Add(frameworkElement);
			stackPanel4.Children.Add(grid8);
			flag = false;
		}
		border4.Child = stackPanel4;
		grid2.Children.Add(border4);
		_contentCanvas.Child = new Viewbox
		{
			Child = grid2,
			Stretch = Stretch.Uniform,
			Margin = new Thickness(16.0, 4.0, 16.0, 4.0),
			VerticalAlignment = VerticalAlignment.Center
		};
	}
}
