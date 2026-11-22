using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.CoreAudio;

namespace WindowsBuddy.Tools;

public class SystemTool : IBuddyTool
{
	private sealed class VolumeSlider : Grid
	{
		private const double TrackH = 6.0;

		private const double Thumb = 18.0;

		private readonly Border _fill;

		private readonly Ellipse _thumb;

		private readonly TranslateTransform _thumbX = new TranslateTransform();

		private double _value;

		public bool IsDragging { get; private set; }

		public event Action<double> ValueChanging;

		public event Action<double> ValueCommitted;

		public VolumeSlider()
		{
			base.Height = 22.0;
			base.Background = Brushes.Transparent;
			base.Cursor = Cursors.Hand;
			base.Focusable = true;
			base.FocusVisualStyle = null;
			BuddyTheme.Crisp(this);
			base.Children.Add(new Border
			{
				Height = 6.0,
				CornerRadius = new CornerRadius(3.0),
				Background = BuddyTheme.Track,
				VerticalAlignment = VerticalAlignment.Center
			});
			_fill = new Border
			{
				Height = 6.0,
				CornerRadius = new CornerRadius(3.0),
				Background = BuddyTheme.Mark,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 0.0
			};
			base.Children.Add(_fill);
			_thumb = new Ellipse
			{
				Width = 18.0,
				Height = 18.0,
				Fill = BuddyTheme.Text100,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				RenderTransform = _thumbX
			};
			base.Children.Add(_thumb);
			SizeChangedEventHandler value = delegate
			{
				Layout();
			};
			base.SizeChanged += value;
			base.GotKeyboardFocus += delegate
			{
				_thumb.Stroke = BuddyTheme.FocusRing;
			};
			base.LostKeyboardFocus += delegate
			{
				_thumb.Stroke = null;
			};
		}

		public void SetValueSilently(double v)
		{
			_value = Math.Max(0.0, Math.Min(1.0, v));
			Layout();
		}

		public void SetMuted(bool muted)
		{
			_fill.Background = (muted ? BuddyTheme.Text50 : BuddyTheme.Mark);
		}

		public void CancelDrag()
		{
			if (IsDragging)
			{
				IsDragging = false;
				if (base.IsMouseCaptured)
				{
					ReleaseMouseCapture();
				}
			}
		}

		private void Layout()
		{
			double actualWidth = base.ActualWidth;
			if (!(actualWidth <= 18.0))
			{
				double num = Math.Round(_value * (actualWidth - 18.0));
				_thumbX.X = num;
				_fill.Width = ((_value <= 0.0) ? 0.0 : Math.Max(6.0, num + 9.0));
			}
		}

		private double ValueAt(double x)
		{
			double actualWidth = base.ActualWidth;
			if (actualWidth <= 18.0)
			{
				return _value;
			}
			return Math.Max(0.0, Math.Min(1.0, (x - 9.0) / (actualWidth - 18.0)));
		}

		private void Change(double v, bool commit)
		{
			v = Math.Max(0.0, Math.Min(1.0, v));
			bool flag = Math.Abs(v - _value) > 0.0005;
			_value = v;
			Layout();
			if (commit)
			{
				this.ValueCommitted?.Invoke(v);
			}
			else if (flag)
			{
				this.ValueChanging?.Invoke(v);
			}
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);
			if (base.IsEnabled)
			{
				e.Handled = true;
				IsDragging = true;
				CaptureMouse();
				Change(ValueAt(e.GetPosition(this).X), commit: false);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (IsDragging)
			{
				Change(ValueAt(e.GetPosition(this).X), commit: false);
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			if (IsDragging)
			{
				e.Handled = true;
				IsDragging = false;
				ReleaseMouseCapture();
				Change(ValueAt(e.GetPosition(this).X), commit: true);
			}
		}

		protected override void OnLostMouseCapture(MouseEventArgs e)
		{
			base.OnLostMouseCapture(e);
			if (IsDragging)
			{
				IsDragging = false;
				Change(_value, commit: true);
			}
		}

		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			base.OnMouseWheel(e);
			if (base.IsEnabled)
			{
				e.Handled = true;
				Change(_value + ((e.Delta > 0) ? 0.02 : (-0.02)), commit: true);
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			double num = 0.0;
			switch (e.Key)
			{
			default:
				return;
			case Key.Left:
			case Key.Down:
				num = -0.02;
				break;
			case Key.Up:
			case Key.Right:
				num = 0.02;
				break;
			case Key.Next:
				num = -0.1;
				break;
			case Key.Prior:
				num = 0.1;
				break;
			case Key.Home:
				num = -1.0;
				break;
			case Key.End:
				num = 1.0;
				break;
			}
			e.Handled = true;
			Change(_value + num, commit: true);
		}
	}

	private const double GridW = 728.0;

	private const double GridH = 242.0;

	private const double GridTop = 0.0;

	private const double RowH = 116.0;

	private const double ContentH = 88.0;

	private const double Bleed = 6.0;

	private const double HeroTop = 20.0;

	private const double ColumnGap = 12.0;

	private const double RateRowH = 18.0;

	private Grid _root;

	private readonly List<FrameworkElement> _cards = new List<FrameworkElement>();

	private bool _attached;

	private int _samplePending;

	private ContentControl _batIcon;

	private BuddyLabel _batLabel;

	private TextBlock _batHero;

	private TextBlock _batSub;

	private TextBlock _batHealth;

	private FrameworkElement _batBolt;

	private Grid _batBar;

	private Border _batFill;

	private double _batFraction = -1.0;

	private string _batIconName;

	private ContentControl _netIcon;

	private TextBlock _netHero;

	private TextBlock _netSub;

	private TextBlock _netDown;

	private TextBlock _netUp;

	private BuddySparkline _netDownSpark;

	private BuddySparkline _netUpSpark;

	private string _netIconName;

	private bool _netConnected;

	private TextBlock _cpuVal;

	private TextBlock _ramVal;

	private TextBlock _diskVal;

	private BuddySparkline _cpuSpark;

	private BuddySparkline _ramSpark;

	private BuddySparkline _diskSpark;

	private TextBlock _volHero;

	private TextBlock _volDevice;

	private TextBlock _volEmpty;

	private FrameworkElement _volDeviceButton;

	private ContentControl _muteIcon;

	private Border _muteButton;

	private VolumeSlider _slider;

	private bool _muted;

	private DateTime _lastVolumeSet = DateTime.MinValue;

	private EventHandler<SystemSample> _sampleHandler;

	private EventHandler<SystemTelemetryData> _powerHandler;

	private EventHandler<NetworkSnapshot> _networkHandler;

	private EventHandler<AudioVolumeChangedEventArgs> _volumeHandler;

	private EventHandler<AudioEndpointChangedEventArgs> _endpointHandler;

	public string Id => "system";

	public string DisplayName => "System";

	public ToolCategory Category => ToolCategory.Core;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => true;

	public bool RequiresNetwork => false;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	public SystemTool()
	{
		State = ToolState.Idle;
	}

	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}
		IsInitialized = true;
		_sampleHandler = delegate
		{
			if (Interlocked.Exchange(ref _samplePending, 1) != 1)
			{
				SafeBeginInvoke(delegate
				{
					Interlocked.Exchange(ref _samplePending, 0);
					ApplySample(SystemSampler.Latest);
				});
			}
		};
		_powerHandler = delegate(object s, SystemTelemetryData d)
		{
			SafeBeginInvoke(delegate
			{
				ApplyBattery(d);
			});
		};
		_networkHandler = delegate(object s, NetworkSnapshot n)
		{
			SafeBeginInvoke(delegate
			{
				ApplyNetwork(n);
			});
		};
		_volumeHandler = delegate(object s, AudioVolumeChangedEventArgs e)
		{
			SafeBeginInvoke(delegate
			{
				ApplyVolume(e.VolumePercent, e.IsMuted);
			});
		};
		_endpointHandler = delegate(object s, AudioEndpointChangedEventArgs e)
		{
			SafeBeginInvoke(delegate
			{
				ApplyDevice(e.EndpointName, e.VolumePercent >= 0);
				ApplyVolume(e.VolumePercent, e.IsMuted);
			});
		};
	}

	public void Shutdown()
	{
		Detach();
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		Attach();
		RefreshAll();
		NetworkService.Instance.RefreshAsync();
		SystemMonitorService.RefreshAsync();
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		Detach();
		BuddyDropdown.Close();
		if (_slider != null)
		{
			_slider.CancelDrag();
		}
	}

	private void Attach()
	{
		if (!_attached)
		{
			_attached = true;
			SystemSampler.Sampled += _sampleHandler;
			SystemMonitorService.PowerChanged += _powerHandler;
			NetworkService.Instance.NetworkChanged += _networkHandler;
			CoreAudioService.Instance.VolumeChanged += _volumeHandler;
			CoreAudioService.Instance.DefaultEndpointChanged += _endpointHandler;
		}
	}

	private void Detach()
	{
		if (_attached)
		{
			_attached = false;
			SystemSampler.Sampled -= _sampleHandler;
			SystemMonitorService.PowerChanged -= _powerHandler;
			NetworkService.Instance.NetworkChanged -= _networkHandler;
			CoreAudioService.Instance.VolumeChanged -= _volumeHandler;
			CoreAudioService.Instance.DefaultEndpointChanged -= _endpointHandler;
		}
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return FluentIcons.Create("developer_board", size, stroke ?? BuddyTheme.Text100, filled: false);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		try
		{
			SystemTelemetryData liveTelemetry = SystemMonitorService.GetLiveTelemetry();
			if (liveTelemetry.HasBattery)
			{
				string accentColor = (liveTelemetry.IsCharging ? null : ((liveTelemetry.BatteryPercent >= 0 && liveTelemetry.BatteryPercent <= 15) ? "#EF4444" : null));
				return new CollapsedBadgeData((liveTelemetry.BatteryPercent >= 0) ? $"{liveTelemetry.BatteryPercent}%" : "—", accentColor, liveTelemetry.IsCharging);
			}
		}
		catch
		{
		}
		return new CollapsedBadgeData("SYS");
	}

	public UIElement CreateView()
	{
		if (_root != null)
		{
			RefreshAll();
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
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(116.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(10.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(116.0)
		});
		Place(grid2, BuildBatteryCard(), 0, 0);
		Place(grid2, BuildNetworkCard(), 0, 2);
		Place(grid2, BuildPerformanceCard(), 2, 0);
		Place(grid2, BuildVolumeCard(), 2, 2);
		_root = grid2;
		RefreshAll();
		return grid2;
	}

	private void Place(Grid root, FrameworkElement card, int row, int col)
	{
		Grid.SetRow(card, row);
		Grid.SetColumn(card, col);
		root.Children.Add(card);
		_cards.Add(card);
	}

	private static Grid NewCard(out BuddyThemeCard card)
	{
		card = new BuddyThemeCard
		{
			Cursor = Cursors.Arrow,
			Focusable = false
		};
		KeyboardNavigation.SetIsTabStop(card, isTabStop: false);
		Grid grid = new Grid();
		grid.Height = 88.0;
		grid.ClipToBounds = false;
		Grid grid2 = grid;
		card.Child = grid2;
		return grid2;
	}

	private static StackPanel Header(string icon, string label, out ContentControl iconHost, out BuddyLabel labelText)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
		stackPanel.Height = 12.0;
		StackPanel stackPanel2 = stackPanel;
		iconHost = new ContentControl
		{
			Width = 12.0,
			Height = 12.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Focusable = false,
			IsTabStop = false,
			Content = FluentIcons.Create(icon, 12.0, BuddyTheme.Text50, filled: true)
		};
		stackPanel2.Children.Add(iconHost);
		labelText = BuddyTheme.LabelText(label);
		labelText.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(labelText);
		return stackPanel2;
	}

	private static T At<T>(T el, double top) where T : FrameworkElement
	{
		el.VerticalAlignment = VerticalAlignment.Top;
		el.Margin = new Thickness(el.Margin.Left, top, el.Margin.Right, el.Margin.Bottom);
		return el;
	}

	private static TextBlock Tabular(TextBlock tb)
	{
		Typography.SetNumeralAlignment(tb, FontNumeralAlignment.Tabular);
		return tb;
	}

	private static Rectangle VerticalHairline(double top, double bottom)
	{
		Rectangle rectangle = new Rectangle();
		rectangle.Width = 1.0;
		rectangle.Fill = BuddyTheme.Hairline;
		rectangle.HorizontalAlignment = HorizontalAlignment.Center;
		rectangle.VerticalAlignment = VerticalAlignment.Stretch;
		rectangle.Margin = new Thickness(0.0, top, 0.0, 0.0 - bottom);
		rectangle.SnapsToDevicePixels = true;
		return rectangle;
	}

	private FrameworkElement BuildBatteryCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard(out card);
		grid.Children.Add(Header("battery_10", "Battery", out _batIcon, out _batLabel));
		_batHealth = BuddyTheme.SecondaryText(string.Empty);
		_batHealth.HorizontalAlignment = HorizontalAlignment.Right;
		_batHealth.VerticalAlignment = VerticalAlignment.Top;
		_batHealth.Margin = new Thickness(0.0, -1.0, 0.0, 0.0);
		grid.Children.Add(Tabular(_batHealth));
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
		StackPanel stackPanel2 = At(stackPanel, 20.0);
		_batHero = Tabular(BuddyTheme.HeroText("--", 28.0));
		stackPanel2.Children.Add(_batHero);
		_batBolt = FluentIcons.Create("flash", 26.0, BuddyTheme.Text100, filled: true);
		_batBolt.VerticalAlignment = VerticalAlignment.Center;
		_batBolt.Margin = new Thickness(12.0, 2.0, 0.0, 0.0);
		_batBolt.Visibility = Visibility.Collapsed;
		stackPanel2.Children.Add(_batBolt);
		grid.Children.Add(stackPanel2);
		_batSub = BuddyTheme.SecondaryText(string.Empty);
		_batSub.VerticalAlignment = VerticalAlignment.Center;
		_batSub.Margin = new Thickness(12.0, 6.0, 0.0, 0.0);
		stackPanel2.Children.Add(_batSub);
		_batBar = new Grid
		{
			Height = 10.0,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		};
		_batBar.Children.Add(new Border
		{
			Background = BuddyTheme.Track,
			CornerRadius = new CornerRadius(5.0)
		});
		_batFill = new Border
		{
			Background = BuddyTheme.Mark,
			CornerRadius = new CornerRadius(5.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			Width = 0.0
		};
		_batBar.Children.Add(_batFill);
		_batBar.SizeChanged += delegate
		{
			LayoutBatteryFill();
		};
		grid.Children.Add(_batBar);
		return card;
	}

	private void ApplyBattery(SystemTelemetryData d)
	{
		if (_root == null || d == null)
		{
			return;
		}
		if (!d.HasBattery)
		{
			SetIcon(_batIcon, ref _batIconName, "plug_connected");
			_batLabel.Text = "Power";
			_batHero.Text = "AC power";
			_batBolt.Visibility = Visibility.Collapsed;
			_batSub.Text = "No battery";
			_batSub.Visibility = Visibility.Visible;
			_batHealth.Visibility = Visibility.Collapsed;
			_batBar.Visibility = Visibility.Collapsed;
			return;
		}
		int num = Math.Max(0, Math.Min(100, d.BatteryPercent));
		SetIcon(_batIcon, ref _batIconName, FluentIcons.BatteryName(num, charging: false));
		_batLabel.Text = "Battery";
		_batHero.Text = ((d.BatteryPercent >= 0) ? (num.ToString(CultureInfo.CurrentCulture) + "%") : "—");
		_batBolt.Visibility = ((!d.IsCharging) ? Visibility.Collapsed : Visibility.Visible);
		string text = (d.IsCharging ? "Charging" : ((d.IsFullyCharged || (d.IsACConnected && num >= 100)) ? "Fully charged" : (d.IsACConnected ? "Plugged in" : ((!d.BatteryLifeTimeAvailable) ? null : FormatTimeLeft(d.BatteryLifeTimeSeconds)))));
		_batSub.Text = text ?? string.Empty;
		_batSub.Visibility = ((text == null) ? Visibility.Collapsed : Visibility.Visible);
		int batteryHealthPercent = d.BatteryHealthPercent;
		bool flag = batteryHealthPercent > 0 && d.BatteryDesignMWh > 0 && d.BatteryFullChargeMWh > 0;
		if (flag)
		{
			CultureInfo currentCulture = CultureInfo.CurrentCulture;
			_batHealth.Text = "Health " + Math.Min(100, batteryHealthPercent).ToString(currentCulture) + "%";
			_batHealth.ToolTip = "Full charge " + d.BatteryFullChargeMWh.ToString("N0", currentCulture) + " mWh · Design " + d.BatteryDesignMWh.ToString("N0", currentCulture) + " mWh (" + batteryHealthPercent.ToString(currentCulture) + "%)";
		}
		else
		{
			_batHealth.Text = string.Empty;
			_batHealth.ToolTip = null;
		}
		_batHealth.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_batBar.Visibility = ((d.BatteryPercent < 0) ? Visibility.Collapsed : Visibility.Visible);
		bool flag2 = num <= 15 && !d.IsACConnected;
		_batFill.Background = (flag2 ? BuddyTheme.Alert : BuddyTheme.Mark);
		_batFraction = (double)num / 100.0;
		LayoutBatteryFill();
	}

	private void LayoutBatteryFill()
	{
		if (_batBar != null && !(_batFraction < 0.0))
		{
			double actualWidth = _batBar.ActualWidth;
			if (!(actualWidth <= 0.0))
			{
				_batFill.Width = ((_batFraction <= 0.0) ? 0.0 : Math.Max(_batBar.Height, Math.Round(actualWidth * _batFraction)));
			}
		}
	}

	private static string FormatTimeLeft(int seconds)
	{
		int num = Math.Max(1, seconds / 60);
		int num2 = num / 60;
		int num3 = num % 60;
		if (num2 == 0)
		{
			return num3 + "m left";
		}
		if (num3 != 0)
		{
			return num2 + "h " + num3 + "m left";
		}
		return num2 + "h left";
	}

	private FrameworkElement BuildNetworkCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard(out card);
		grid.Children.Add(Header("wifi_1", "Network", out _netIcon, out var _));
		_netHero = At(BuddyTheme.HeroText("--"), 20.0);
		_netHero.TextTrimming = TextTrimming.CharacterEllipsis;
		grid.Children.Add(_netHero);
		_netSub = At(BuddyTheme.SecondaryText(string.Empty), 51.0);
		grid.Children.Add(_netSub);
		Grid grid2 = new Grid();
		grid2.Height = 18.0;
		grid2.VerticalAlignment = VerticalAlignment.Bottom;
		Grid grid3 = grid2;
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(25.0)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Grid element = BuildRateColumn("arrow_down", "Download", out _netDown, out _netDownSpark);
		Grid.SetColumn(element, 0);
		grid3.Children.Add(element);
		Rectangle element2 = VerticalHairline(0.0, 0.0);
		Grid.SetColumn(element2, 1);
		grid3.Children.Add(element2);
		Grid element3 = BuildRateColumn("arrow_up", "Upload", out _netUp, out _netUpSpark);
		Grid.SetColumn(element3, 2);
		grid3.Children.Add(element3);
		grid.Children.Add(grid3);
		return card;
	}

	private static Grid BuildRateColumn(string arrow, string label, out TextBlock value, out BuddySparkline spark)
	{
		Grid grid = new Grid();
		grid.Background = Brushes.Transparent;
		grid.ToolTip = BuddyTooltip.Create(label);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(4.0, GridUnitType.Star),
			MaxWidth = 72.0
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement frameworkElement = FluentIcons.Create(arrow, 14.0, BuddyTheme.Text100, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		stackPanel2.Children.Add(frameworkElement);
		value = Tabular(BuddyTheme.Text("--", 13.5, BuddyTheme.Text100, AynType.Medium));
		value.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(value);
		grid2.Children.Add(stackPanel2);
		spark = new BuddySparkline(60, 0.0, 16384.0, 1.0)
		{
			Height = 18.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(spark, 2);
		grid2.Children.Add(spark);
		return grid2;
	}

	private void ApplyNetwork(NetworkSnapshot n)
	{
		if (_root == null || n == null)
		{
			return;
		}
		_netConnected = n.IsConnected;
		if (!n.IsConnected)
		{
			SetIcon(_netIcon, ref _netIconName, "wifi_off");
			_netHero.Text = "Not connected";
			_netSub.Text = string.Empty;
			_netSub.Visibility = Visibility.Collapsed;
			ApplyRates(null);
			return;
		}
		string text = ((n.LinkSpeedMbps > 0) ? NetworkService.FormatLinkSpeed(n.LinkSpeedMbps) : null);
		string text3;
		if (n.IsWiFi)
		{
			SetIcon(_netIcon, ref _netIconName, "wifi_1");
			_netHero.Text = (string.IsNullOrEmpty(n.SSID) ? "Wi-Fi" : n.SSID);
			string text2 = (string.IsNullOrEmpty(n.WiFiStandard) ? "Wi-Fi" : n.WiFiStandard);
			text3 = ((text != null) ? (text2 + " · " + text) : text2);
		}
		else
		{
			SetIcon(_netIcon, ref _netIconName, "globe");
			_netHero.Text = "Ethernet";
			text3 = text;
		}
		if (!n.InternetAvailable)
		{
			text3 = (string.IsNullOrEmpty(text3) ? "No internet" : (text3 + " · No internet"));
		}
		_netSub.Text = text3 ?? string.Empty;
		_netSub.Visibility = (string.IsNullOrEmpty(text3) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void ApplyRates(SystemSample s)
	{
		if (!_netConnected || s == null || !s.NetConnected)
		{
			_netDown.Text = NetworkService.FormatRate(0.0);
			_netUp.Text = NetworkService.FormatRate(0.0);
			_netDownSpark.SetValues(null);
			_netUpSpark.SetValues(null);
		}
		else
		{
			_netDown.Text = NetworkService.FormatRate(s.Down);
			_netUp.Text = NetworkService.FormatRate(s.Up);
			_netDownSpark.SetValues(SystemSampler.History(PerfMetric.NetDown));
			_netUpSpark.SetValues(SystemSampler.History(PerfMetric.NetUp));
		}
	}

	private FrameworkElement BuildPerformanceCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard(out card);
		grid.Children.Add(Header("developer_board", "Performance", out var _, out var _));
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(0.0, 0.0, 0.0, -4.0);
		Grid grid3 = At(grid2, 24.0);
		grid3.VerticalAlignment = VerticalAlignment.Stretch;
		for (int i = 0; i < 3; i++)
		{
			if (i > 0)
			{
				grid3.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(25.0)
				});
			}
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		AddMetricColumn(grid3, 0, "CPU", out _cpuVal, out _cpuSpark);
		AddMetricColumn(grid3, 2, "RAM", out _ramVal, out _ramSpark);
		AddMetricColumn(grid3, 4, "Disk", out _diskVal, out _diskSpark);
		for (int j = 1; j <= 3; j += 2)
		{
			Rectangle element = VerticalHairline(0.0, 0.0);
			Grid.SetColumn(element, j);
			grid3.Children.Add(element);
		}
		grid.Children.Add(grid3);
		return card;
	}

	private static void AddMetricColumn(Grid cols, int column, string label, out TextBlock value, out BuddySparkline spark)
	{
		Grid grid = new Grid();
		value = Tabular(BuddyTheme.HeroText("—", 24.0));
		value.VerticalAlignment = VerticalAlignment.Top;
		grid.Children.Add(value);
		grid.Children.Add(At(BuddyTheme.LabelText(label), 28.0));
		spark = new BuddySparkline(60, 100.0, 0.0, 1.0)
		{
			Height = 22.0,
			VerticalAlignment = VerticalAlignment.Bottom
		};
		grid.Children.Add(spark);
		Grid.SetColumn(grid, column);
		cols.Children.Add(grid);
	}

	private void ApplySample(SystemSample s)
	{
		if (_root != null && s != null)
		{
			_cpuVal.Text = (s.IsFresh(PerfMetric.Cpu) ? Pct(s.Cpu) : "—");
			_ramVal.Text = (s.IsFresh(PerfMetric.Ram) ? Pct(s.Ram) : "—");
			_diskVal.Text = (s.IsFresh(PerfMetric.Disk) ? Pct(s.Disk) : "—");
			_cpuSpark.SetValues(SystemSampler.History(PerfMetric.Cpu));
			_ramSpark.SetValues(SystemSampler.History(PerfMetric.Ram));
			_diskSpark.SetValues(SystemSampler.History(PerfMetric.Disk));
			if (s.TotalPhys != 0)
			{
				_ramVal.ToolTip = BuddyTooltip.Create(string.Format(CultureInfo.CurrentCulture, "{0:0.0} GB of {1:0.0} GB in use", (double)(s.TotalPhys - s.AvailPhys) / 1073741824.0, (double)s.TotalPhys / 1073741824.0));
			}
			ApplyRates(s);
		}
	}

	private static string Pct(double v)
	{
		return Math.Round(v).ToString(CultureInfo.CurrentCulture) + "%";
	}

	private FrameworkElement BuildVolumeCard()
	{
		BuddyThemeCard card;
		Grid grid = NewCard(out card);
		grid.Children.Add(Header("speaker_2", "Volume", out var _, out var _));
		StackPanel deviceRow = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Height = 12.0,
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			Focusable = true,
			FocusVisualStyle = null,
			Margin = new Thickness(0.0, -1.0, 0.0, 0.0)
		};
		_volDevice = BuddyTheme.SecondaryText("Output");
		_volDevice.MaxWidth = 170.0;
		_volDevice.VerticalAlignment = VerticalAlignment.Center;
		deviceRow.Children.Add(_volDevice);
		FrameworkElement frameworkElement = FluentIcons.Create("chevron_down", 14.0, BuddyTheme.Text50, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.Margin = new Thickness(6.0, 1.0, 0.0, 0.0);
		deviceRow.Children.Add(frameworkElement);
		deviceRow.MouseEnter += delegate
		{
			_volDevice.Foreground = BuddyTheme.Text92;
		};
		deviceRow.MouseLeave += delegate
		{
			if (!deviceRow.IsKeyboardFocused)
			{
				_volDevice.Foreground = BuddyTheme.Text50;
			}
		};
		deviceRow.GotKeyboardFocus += delegate
		{
			_volDevice.Foreground = BuddyTheme.Text92;
		};
		deviceRow.LostKeyboardFocus += delegate
		{
			if (!deviceRow.IsMouseOver)
			{
				_volDevice.Foreground = BuddyTheme.Text50;
			}
		};
		deviceRow.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			OpenDevicePicker();
		};
		deviceRow.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space || e.Key == Key.Down)
			{
				e.Handled = true;
				OpenDevicePicker();
			}
		};
		_volDeviceButton = deviceRow;
		grid.Children.Add(deviceRow);
		_volHero = At(Tabular(BuddyTheme.HeroText("--", 28.0)), 26.0);
		_volHero.HorizontalAlignment = HorizontalAlignment.Left;
		grid.Children.Add(_volHero);
		_volEmpty = BuddyTheme.SecondaryText("Connect speakers or headphones");
		_volEmpty.VerticalAlignment = VerticalAlignment.Bottom;
		_volEmpty.Margin = new Thickness(0.0, 0.0, 0.0, 3.0);
		_volEmpty.Visibility = Visibility.Collapsed;
		grid.Children.Add(_volEmpty);
		_muteIcon = new ContentControl
		{
			Focusable = false,
			IsTabStop = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_muteButton = new Border
		{
			Width = 44.0,
			Height = 44.0,
			CornerRadius = new CornerRadius(22.0),
			Background = BuddyTheme.Control,
			BorderThickness = new Thickness(2.0),
			BorderBrush = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0),
			Cursor = Cursors.Hand,
			Focusable = true,
			FocusVisualStyle = null,
			Child = _muteIcon
		};
		_muteButton.MouseEnter += delegate
		{
			_muteButton.Background = BuddyTheme.ControlHover;
		};
		_muteButton.MouseLeave += delegate
		{
			_muteButton.Background = BuddyTheme.Control;
		};
		_muteButton.GotKeyboardFocus += delegate
		{
			_muteButton.BorderBrush = BuddyTheme.FocusRing;
		};
		_muteButton.LostKeyboardFocus += delegate
		{
			_muteButton.BorderBrush = Brushes.Transparent;
		};
		_muteButton.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			ToggleMute();
		};
		_muteButton.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				ToggleMute();
			}
		};
		grid.Children.Add(_muteButton);
		_slider = new VolumeSlider
		{
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(0.0, 0.0, 0.0, 0.0)
		};
		_slider.ValueChanging += delegate(double v)
		{
			SetVolume(v, commit: false);
		};
		_slider.ValueCommitted += delegate(double v)
		{
			SetVolume(v, commit: true);
		};
		grid.Children.Add(_slider);
		return card;
	}

	private void SetVolume(double fraction, bool commit)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (!commit && (utcNow - _lastVolumeSet).TotalMilliseconds < 25.0)
		{
			ShowVolumePercent(fraction);
			return;
		}
		_lastVolumeSet = utcNow;
		ShowVolumePercent(fraction);
		try
		{
			CoreAudioService.Instance.SetMasterVolumeScalar((float)fraction);
		}
		catch
		{
		}
		if (_muted && fraction > 0.001)
		{
			SetMutedVisual(muted: false);
		}
	}

	private void ShowVolumePercent(double fraction)
	{
		_volHero.Text = ((int)Math.Round(fraction * 100.0)).ToString(CultureInfo.CurrentCulture) + "%";
	}

	private void ToggleMute()
	{
		CoreAudioService instance = CoreAudioService.Instance;
		if (instance.HasEndpoint)
		{
			bool flag = !_muted;
			if (instance.SetMute(flag))
			{
				SetMutedVisual(flag);
			}
		}
	}

	private void ApplyVolume(int percent, bool muted)
	{
		if (_root == null)
		{
			return;
		}
		bool flag = percent >= 0;
		_slider.IsEnabled = flag;
		_muteButton.IsEnabled = flag;
		_slider.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_muteButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_volEmpty.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		if (!flag)
		{
			if (_slider.IsDragging)
			{
				_slider.CancelDrag();
			}
			_slider.SetValueSilently(0.0);
			SetMutedVisual(muted: false);
			_volHero.Text = "No output";
			_volHero.Foreground = BuddyTheme.Text50;
		}
		else if (!_slider.IsDragging)
		{
			_slider.SetValueSilently((double)Math.Max(0, Math.Min(100, percent)) / 100.0);
			ShowVolumePercent((double)Math.Max(0, Math.Min(100, percent)) / 100.0);
			SetMutedVisual(muted);
		}
	}

	private void SetMutedVisual(bool muted)
	{
		_muted = muted;
		_muteIcon.Content = FluentIcons.Create(muted ? "speaker_mute" : "speaker_2", 18.0, BuddyTheme.Text100, filled: false);
		_volHero.Foreground = (muted ? BuddyTheme.Text50 : BuddyTheme.Text100);
		_slider.SetMuted(muted);
		_muteButton.ToolTip = BuddyTooltip.Create(muted ? "Unmute" : "Mute");
	}

	private void ApplyDevice(string name, bool has)
	{
		if (_root != null)
		{
			_volDevice.Text = ((has && !string.IsNullOrEmpty(name)) ? ShortDeviceName(name) : "Output");
			_volDeviceButton.ToolTip = ((has && !string.IsNullOrEmpty(name)) ? BuddyTooltip.Create(name) : null);
		}
	}

	private static string ShortDeviceName(string friendlyName)
	{
		Match match = Regex.Match(friendlyName ?? string.Empty, "^.+? \\((.+)\\)$");
		if (!match.Success)
		{
			return friendlyName;
		}
		return match.Groups[1].Value;
	}

	private void OpenDevicePicker()
	{
		if (BuddyDropdown.IsOpen)
		{
			BuddyDropdown.Close();
			return;
		}
		FrameworkElement anchor = _volDeviceButton;
		ThreadPool.QueueUserWorkItem(delegate
		{
			List<AudioOutputDevice> devices;
			try
			{
				devices = CoreAudioService.Instance.GetOutputDevices();
			}
			catch
			{
				devices = new List<AudioOutputDevice>();
			}
			SafeBeginInvoke(delegate
			{
				ShowDevicePicker(anchor, devices);
			});
		});
	}

	private void ShowDevicePicker(FrameworkElement anchor, List<AudioOutputDevice> devices)
	{
		if (_root == null || State != ToolState.Active || anchor == null)
		{
			return;
		}
		List<BuddyMenuItem> list = new List<BuddyMenuItem>();
		foreach (AudioOutputDevice device in devices)
		{
			AudioOutputDevice d = device;
			list.Add(new BuddyMenuItem(d.Name, null, delegate
			{
				if (!d.IsDefault)
				{
					_volDevice.Text = ShortDeviceName(d.Name);
					ThreadPool.QueueUserWorkItem(delegate
					{
						CoreAudioService.Instance.SetDefaultOutputDevice(d.Id);
					});
				}
			})
			{
				IsChecked = d.IsDefault
			});
		}
		BuddyDropdown.Show(anchor, list, "No output devices");
	}

	private void RefreshAll()
	{
		if (_root != null)
		{
			Guarded("battery", delegate
			{
				ApplyBattery(SystemMonitorService.GetLiveTelemetry());
			});
			Guarded("network", delegate
			{
				ApplyNetwork(NetworkService.Instance.GetSnapshot());
			});
			Guarded("sample", delegate
			{
				ApplySample(SystemSampler.Latest);
			});
			Guarded("volume", delegate
			{
				CoreAudioService instance = CoreAudioService.Instance;
				ApplyDevice(instance.CurrentDeviceName, instance.HasEndpoint);
				ApplyVolume(instance.GetMasterVolumePercent(), instance.GetMute());
			});
		}
	}

	private static void Guarded(string what, Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[System] " + what + " update failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	internal void ShowQaState(string state)
	{
		if (_root == null)
		{
			return;
		}
		Detach();
		RefreshAll();
		double[] values = HistoryOf((int i) => 40.0 + 25.0 * Math.Sin((double)i / 4.0) + (double)((i % 7 == 0) ? 30 : 0));
		if (state == "desktop")
		{
			ApplyBattery(new SystemTelemetryData
			{
				HasBattery = false,
				IsACConnected = true
			});
		}
		if (state == "ethernet")
		{
			ApplyNetwork(new NetworkSnapshot
			{
				IsConnected = true,
				IsWiFi = false,
				LinkSpeedMbps = 1000L,
				InternetAvailable = true
			});
		}
		if (state == "offline")
		{
			ApplyNetwork(new NetworkSnapshot
			{
				IsConnected = false
			});
		}
		if (state == "noaudio")
		{
			ApplyDevice(null, has: false);
			ApplyVolume(-1, muted: false);
		}
		_cpuVal.Text = "38%";
		_ramVal.Text = "62%";
		_diskVal.Text = "2%";
		_cpuSpark.SetValues(values);
		_ramSpark.SetValues(HistoryOf((int i) => 62.0));
		_diskSpark.SetValues(HistoryOf((int i) => (i % 11 != 0) ? 1 : 35));
		if (_netConnected)
		{
			_netDown.Text = "1.2 MB/s";
			_netUp.Text = "85 KB/s";
			_netDownSpark.SetValues(HistoryOf((int i) => (i % 9 == 0) ? 1200000 : (20000 + 1000 * i)));
			_netUpSpark.SetValues(HistoryOf((int i) => 30000.0 + 25000.0 * Math.Sin((double)i / 3.0)));
		}
	}

	internal void RefreshQa()
	{
		RefreshAll();
		if (State == ToolState.Active)
		{
			Attach();
		}
	}

	private static double[] HistoryOf(Func<int, double> f)
	{
		double[] array = new double[60];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Math.Max(0.0, f(i));
		}
		return array;
	}

	private static void SetIcon(ContentControl host, ref string current, string name)
	{
		if (host != null && !(current == name))
		{
			current = name;
			host.Content = FluentIcons.Create(name, 12.0, BuddyTheme.Text50, filled: true);
		}
	}

	private static void SafeBeginInvoke(Action action)
	{
		try
		{
			Application current = Application.Current;
			if (current != null && !current.Dispatcher.HasShutdownStarted)
			{
				current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
				{
					Guarded("event", action);
				});
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[System] dispatch failed: " + ex.GetType().Name);
		}
	}
}
