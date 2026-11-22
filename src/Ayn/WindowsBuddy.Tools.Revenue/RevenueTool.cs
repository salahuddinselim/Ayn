using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Tools.Analytics;

namespace WindowsBuddy.Tools.Revenue;

public class RevenueTool : IBuddyTool, IEscapeAware
{
	private enum Mode
	{
		Picker,
		Form,
		Dashboard
	}

	public const string GumroadSettingsUrl = "https://gumroad.com/settings/advanced";

	internal static readonly Color StripeBrand = Color.FromRgb(99, 91, byte.MaxValue);

	internal static readonly Color GumroadBrand = Color.FromRgb(byte.MaxValue, 144, 232);

	private readonly RevenueService _svc;

	private Grid _root;

	private Mode _mode;

	private RevenueSnapshot _snap;

	private Exception _lastError;

	private bool _loading;

	private DispatcherTimer _timer;

	private StackPanel _headerActions;

	private BuddySlidingSegmented _rangeSeg;

	private string _formProvider = "stripe";

	private ConnectorKit.SoftField _fKey;

	private ConnectorKit.Notice _fNotice;

	private ConnectorKit.BusyButton _fConnect;

	private int _connectGen;

	private Grid _dash;

	private BuddyLabel _label;

	private TextBlock _updated;

	private TextBlock _hero;

	private TextBlock _heroSub;

	private TextBlock _message;

	private TextBlock _accountText;

	private Border _refresh;

	private AnalyticsTool.VisitorsChart _chart;

	private Grid _axis;

	private Grid _tiles;

	private StackPanel _loadingRow;

	private ConnectorKit.Notice _dashNotice;

	private ConnectorKit.ConfirmAction _disconnect;

	private TextBlock _disconnectError;

	public string Id => "revenue";

	public string DisplayName => "Revenue";

	public ToolCategory Category => ToolCategory.Utility;

	public bool IsEnabled { get; set; }

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge
	{
		get
		{
			if (_svc.IsConnected && _snap != null)
			{
				return _snap.Gross.HasValue;
			}
			return false;
		}
	}

	public bool RequiresNetwork => true;

	public ToolState State { get; private set; }

	public bool IsInitialized { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	private TimeSpan StaleAfter
	{
		get
		{
			if (_svc.Config.Range != 0)
			{
				return TimeSpan.FromMinutes(10.0);
			}
			return TimeSpan.FromMinutes(2.0);
		}
	}

	internal string ModeName => _mode.ToString();

	internal ConnectorKit.SoftField FormKey => _fKey;

	internal ConnectorKit.Notice FormNotice => _fNotice;

	internal ConnectorKit.BusyButton FormConnect => _fConnect;

	internal ConnectorKit.Notice DashNotice => _dashNotice;

	internal ConnectorKit.ConfirmAction DisconnectAction => _disconnect;

	internal Grid Tiles => _tiles;

	internal TextBlock HeroText => _hero;

	internal TextBlock HeroSub => _heroSub;

	internal TextBlock UpdatedText => _updated;

	internal TextBlock MessageText => _message;

	public RevenueTool()
	{
		IsEnabled = false;
		OrderIndex = 25;
		State = ToolState.Idle;
		_svc = RevenueService.Instance;
		_mode = (_svc.IsConnected ? Mode.Dashboard : Mode.Picker);
		if (_svc.IsConnected)
		{
			_snap = _svc.Cached(_svc.Config.Range);
		}
	}

	public void Initialize()
	{
		if (!IsInitialized)
		{
			IsInitialized = true;
			_timer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(60.0)
			};
			_timer.Tick += delegate
			{
				UpdateAgo();
				RefreshIfStale(force: false);
			};
		}
	}

	public void Shutdown()
	{
		if (_timer != null)
		{
			_timer.Stop();
		}
		IsInitialized = false;
	}

	public void OnActivated()
	{
		State = ToolState.Active;
		if (!IsInitialized)
		{
			Initialize();
		}
		if (!_svc.IsConnected && _mode == Mode.Dashboard)
		{
			_mode = Mode.Picker;
		}
		Render(animate: false);
		if (_mode == Mode.Dashboard)
		{
			RefreshIfStale(force: false);
			_timer.Start();
		}
	}

	public void OnDeactivated()
	{
		State = ToolState.Idle;
		if (_timer != null)
		{
			_timer.Stop();
		}
	}

	public bool HandleEscape()
	{
		if (_mode == Mode.Form)
		{
			_connectGen++;
			_mode = (_svc.IsConnected ? Mode.Dashboard : Mode.Picker);
			Render();
			return true;
		}
		return false;
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		if (!HasCollapsedBadge)
		{
			return null;
		}
		return new CollapsedBadgeData(RevenueMath.Format(_snap.Gross.Value, _snap.Currency), "#30A46C");
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return VectorIcons.CreateRevenueIcon(size, stroke);
	}

	private static int RangeIndex(RevenueRange r)
	{
		return r switch
		{
			RevenueRange.Week => 1, 
			RevenueRange.Today => 0, 
			_ => 2, 
		};
	}

	public UIElement GetHeaderActions()
	{
		if (_headerActions != null)
		{
			return _headerActions;
		}
		_headerActions = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		_rangeSeg = new BuddySlidingSegmented(new string[3] { "Today", "7d", "30d" }, RangeIndex(_svc.Config.Range), 28.0, 11.5, 52.0);
		_rangeSeg.SelectionChanged += delegate(int i)
		{
			SetRange(i switch
			{
				1 => RevenueRange.Week, 
				0 => RevenueRange.Today, 
				_ => RevenueRange.Month, 
			});
		};
		ConnectorKit.AutomationName(_rangeSeg, "Period");
		_headerActions.Children.Add(_rangeSeg);
		UpdateHeader();
		return _headerActions;
	}

	private void UpdateHeader()
	{
		if (_headerActions != null)
		{
			_headerActions.Visibility = ((_mode != Mode.Dashboard) ? Visibility.Collapsed : Visibility.Visible);
			if (_rangeSeg.Selected != RangeIndex(_svc.Config.Range))
			{
				_rangeSeg.Selected = RangeIndex(_svc.Config.Range);
			}
		}
	}

	private void SetRange(RevenueRange r)
	{
		if (_svc.Config.Range != r)
		{
			_svc.Config.Range = r;
			_svc.Save();
			_lastError = null;
			_snap = _svc.Cached(r);
			RenderSnapshot();
			RefreshIfStale(force: false);
		}
	}

	public UIElement CreateView()
	{
		_root = BuddyTheme.Crisp(new Grid
		{
			Background = BuddyTheme.Canvas,
			Tag = "NoOuterScroll",
			ClipToBounds = true
		});
		Render(animate: false);
		return _root;
	}

	private void Render(bool animate = true)
	{
		if (_root != null)
		{
			_root.Children.Clear();
			_dash = null;
			switch (_mode)
			{
			case Mode.Picker:
				_root.Children.Add(BuildPicker(animate));
				break;
			case Mode.Form:
				_root.Children.Add(BuildForm(animate));
				break;
			default:
				_root.Children.Add(BuildDashboard());
				RenderSnapshot();
				break;
			}
			UpdateHeader();
		}
	}

	internal static List<ConnectorKit.ProviderOption> ProviderOptions()
	{
		List<ConnectorKit.ProviderOption> list = new List<ConnectorKit.ProviderOption>();
		list.Add(new ConnectorKit.ProviderOption
		{
			Id = "stripe",
			Name = "Stripe",
			Letters = "S",
			Brand = StripeBrand,
			WhatYouSee = "Sales, net after fees, refunds and your balance",
			Sample = () => SampleBody("$1,248.00", "18 sales · last 7 days", new double[7] { 120.0, 90.0, 210.0, 160.0, 240.0, 180.0, 248.0 }, "Net after fees", "$1,201.40", "Available to pay out", "$3,420.00")
		});
		list.Add(new ConnectorKit.ProviderOption
		{
			Id = "gumroad",
			Name = "Gumroad",
			Letters = "G",
			Brand = GumroadBrand,
			WhatYouSee = "Sales, net after Gumroad fees and refunds",
			Sample = () => SampleBody("$864.00", "24 sales · last 7 days", new double[7] { 60.0, 140.0, 90.0, 120.0, 180.0, 110.0, 164.0 }, "Net after fees", "$777.60", "Refunds", "$36.00")
		});
		return list;
	}

	private static FrameworkElement SampleBody(string gross, string sub, double[] line, string l1, string v1, string l2, string v2)
	{
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = ConnectorKit.Text(gross, 24.0, ThemeTokens.Text, AynType.Semibold);
		textBlock.FontFamily = BuddyTheme.DisplayFont;
		stackPanel.Children.Add(textBlock);
		stackPanel.Children.Add(ConnectorKit.Text(sub, 11.5, ThemeTokens.Text2, AynType.Regular));
		FrameworkElement frameworkElement = ConnectorKit.SampleLine(line, 286.0, 38.0);
		frameworkElement.Margin = new Thickness(0.0, 8.0, 0.0, 8.0);
		stackPanel.Children.Add(frameworkElement);
		stackPanel.Children.Add(ConnectorKit.SampleRow(l1, v1));
		stackPanel.Children.Add(ConnectorKit.SampleRow(l2, v2));
		return stackPanel;
	}

	private FrameworkElement BuildPicker(bool animate = true)
	{
		FrameworkElement frameworkElement = ConnectorKit.Picker("Connect your sales", "See today's sales and your balance right in the notch. Read-only — your key is encrypted on this PC and only sent to the provider you pick.", ProviderOptions(), delegate(string id)
		{
			_formProvider = id;
			_mode = Mode.Form;
			Render();
		}, null);
		if (animate)
		{
			ConnectorKit.Reveal(frameworkElement, 6.0);
		}
		return frameworkElement;
	}

	private FrameworkElement BuildForm(bool animate = true)
	{
		ConnectorKit.ProviderOption providerOption = ProviderOptions().First((ConnectorKit.ProviderOption o) => o.Id == _formProvider);
		bool flag = _formProvider == "stripe";
		FrameworkElement steps = (flag ? ConnectorKit.Steps(new ConnectorKit.Step("Open API keys in your Stripe Dashboard", "Open Stripe", "https://dashboard.stripe.com/apikeys"), new ConnectorKit.Step("Create restricted key → set Balance to Read, leave the rest None"), new ConnectorKit.Step("Copy the key (rk_live_…) and paste it here")) : ConnectorKit.Steps(new ConnectorKit.Step("Open Settings → Advanced in Gumroad", "Open Gumroad", "https://gumroad.com/settings/advanced"), new ConnectorKit.Step("Create an application (any name), then Generate access token"), new ConnectorKit.Step("Copy the token and paste it here")));
		_fKey = (flag ? new ConnectorKit.SoftField("Restricted key", "rk_live_…", "Read-only. Never paste a secret key (sk_…).", secret: true, paste: true) : new ConnectorKit.SoftField("Access token", "Paste your Gumroad access token", "Gumroad tokens can't be limited to read-only — Ayn only reads your sales.", secret: true, paste: true));
		_fKey.Submitted += Connect;
		_fNotice = new ConnectorKit.Notice
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		_fKey.Changed += delegate
		{
			_fNotice.Hide();
		};
		_fConnect = new ConnectorKit.BusyButton("Connect")
		{
			HorizontalAlignment = HorizontalAlignment.Right
		};
		_fConnect.Click += Connect;
		ConnectorKit.AutomationName(_fConnect, "Connect " + providerOption.Name);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(_fKey);
		Grid grid = new Grid();
		grid.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		Grid grid2 = grid;
		grid2.Children.Add(_fConnect);
		stackPanel.Children.Add(grid2);
		Grid grid3 = ConnectorKit.ConnectLayout(providerOption, "Read-only. Your key is encrypted on this PC and only sent to " + providerOption.Name + ".", steps, null, stackPanel, _fNotice, delegate
		{
			_connectGen++;
			_mode = (_svc.IsConnected ? Mode.Dashboard : Mode.Picker);
			Render();
		});
		if (animate)
		{
			ConnectorKit.Reveal(grid3, 6.0);
		}
		_fKey.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			if (_mode == Mode.Form && _fKey != null)
			{
				_fKey.FocusInput();
			}
		});
		return grid3;
	}

	private void Connect()
	{
		if (_fConnect == null || _fConnect.IsBusy)
		{
			return;
		}
		_fNotice.Hide();
		if (_fKey.HasError)
		{
			_fKey.SetError(null);
		}
		IRevenueProvider revenueProvider = RevenueService.For(_formProvider);
		string key = _fKey.Value;
		string text = revenueProvider.CheckKey(key);
		if (text != null)
		{
			_fKey.SetError(text);
			_fKey.FocusInput();
			return;
		}
		_fConnect.SetBusy(busy: true, "Connecting…");
		int gen = ++_connectGen;
		string providerId = _formProvider;
		Dispatcher ui = _root.Dispatcher;
		Task.Factory.StartNew(delegate
		{
			Exception err = null;
			try
			{
				_svc.Connect(providerId, key);
			}
			catch (Exception ex)
			{
				err = ex;
			}
			ui.BeginInvoke((Action)delegate
			{
				if (gen == _connectGen && _mode == Mode.Form)
				{
					_fConnect.SetBusy(busy: false);
					if (err != null)
					{
						ShowConnectError(err);
					}
					else
					{
						_snap = null;
						_lastError = null;
						_mode = Mode.Dashboard;
						Render();
						ConnectorKit.Reveal(_dash, 12.0);
						RefreshIfStale(force: true);
						if (State == ToolState.Active && _timer != null)
						{
							_timer.Start();
						}
					}
				}
			});
		});
	}

	private void ShowConnectError(Exception ex)
	{
		RevenueService.Describe(ex, out var title, out var advice);
		RevenueException ex2 = ex as RevenueException;
		string actionText = null;
		Action action = null;
		if (ex2 != null && (ex2.Kind == RevenueErrorKind.Offline || ex2.Kind == RevenueErrorKind.Server))
		{
			actionText = "Try again";
			action = Connect;
		}
		else if (ex2 != null && (ex2.Kind == RevenueErrorKind.WrongKey || ex2.Kind == RevenueErrorKind.Revoked || ex2.Kind == RevenueErrorKind.NoPermission))
		{
			actionText = ((_formProvider == "stripe") ? "Open Stripe" : "Open Gumroad");
			string url = ((_formProvider == "stripe") ? "https://dashboard.stripe.com/apikeys" : "https://gumroad.com/settings/advanced");
			action = delegate
			{
				ConnectorKit.Browser(url);
			};
		}
		if (ex2 != null && ex2.Kind == RevenueErrorKind.WrongKeyType)
		{
			_fKey.SetError(title);
			return;
		}
		_fNotice.Show(ConnectorKit.NoticeKind.Error, title, advice, actionText, action);
		if (ex2 != null && (ex2.Kind == RevenueErrorKind.WrongKey || ex2.Kind == RevenueErrorKind.Revoked))
		{
			_fKey.SetError("Check this key");
		}
	}

	private Grid BuildDashboard()
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(18.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(286.0)
		});
		Grid grid2 = new Grid();
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(16.0)
		});
		Grid grid3 = new Grid();
		_label = BuddyTheme.LabelText("SALES");
		_label.HorizontalAlignment = HorizontalAlignment.Left;
		grid3.Children.Add(_label);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		_updated = ConnectorKit.Text("", 11.5, ThemeTokens.Text3, AynType.Medium);
		_updated.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(_updated);
		_refresh = ConnectorKit.Link("Refresh", delegate
		{
			_lastError = null;
			RefreshIfStale(force: true);
		});
		_refresh.Margin = new Thickness(8.0, -4.0, -2.0, -4.0);
		((TextBlock)_refresh.Child).FontSize = 11.5;
		ConnectorKit.AutomationName(_refresh, "Refresh");
		stackPanel2.Children.Add(_refresh);
		grid3.Children.Add(stackPanel2);
		grid2.Children.Add(grid3);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		StackPanel stackPanel4 = stackPanel3;
		_hero = BuddyTheme.Text("", 28.0, ThemeTokens.Text, AynType.Semibold);
		_hero.FontFamily = BuddyTheme.DisplayFont;
		_hero.TextTrimming = TextTrimming.None;
		Typography.SetNumeralAlignment(_hero, FontNumeralAlignment.Tabular);
		stackPanel4.Children.Add(_hero);
		_heroSub = ConnectorKit.Text("", 13.0, ThemeTokens.Text2, AynType.Regular);
		_heroSub.VerticalAlignment = VerticalAlignment.Center;
		_heroSub.Margin = new Thickness(16.0, 6.0, 0.0, 0.0);
		Typography.SetNumeralAlignment(_heroSub, FontNumeralAlignment.Tabular);
		stackPanel4.Children.Add(_heroSub);
		Grid.SetRow(stackPanel4, 1);
		grid2.Children.Add(stackPanel4);
		Grid grid4 = new Grid();
		grid4.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		Grid grid5 = grid4;
		_chart = new AnalyticsTool.VisitorsChart();
		grid5.Children.Add(_chart);
		_loadingRow = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		_loadingRow.Children.Add(new ConnectorKit.Spinner(14.0, ThemeTokens.Text2)
		{
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		_loadingRow.Children.Add(ConnectorKit.Text("Loading your sales…", 13.0, ThemeTokens.Text2, AynType.Regular));
		grid5.Children.Add(_loadingRow);
		_message = ConnectorKit.Wrap("", 13.0, ThemeTokens.Text2, AynType.Regular);
		_message.HorizontalAlignment = HorizontalAlignment.Center;
		_message.VerticalAlignment = VerticalAlignment.Center;
		_message.TextAlignment = TextAlignment.Center;
		_message.MaxWidth = 320.0;
		grid5.Children.Add(_message);
		_dashNotice = new ConnectorKit.Notice
		{
			VerticalAlignment = VerticalAlignment.Center,
			MaxWidth = 400.0
		};
		grid5.Children.Add(_dashNotice);
		Grid.SetRow(grid5, 2);
		grid2.Children.Add(grid5);
		_axis = new Grid
		{
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		Grid.SetRow(_axis, 3);
		grid2.Children.Add(_axis);
		grid.Children.Add(grid2);
		Grid grid6 = new Grid();
		grid6.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid6.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		_tiles = new Grid();
		grid6.Children.Add(_tiles);
		Grid grid7 = new Grid();
		grid7.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		Grid grid8 = grid7;
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid8.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		ConnectorKit.ProviderOption providerOption = ProviderOptions().First((ConnectorKit.ProviderOption o) => o.Id == _svc.Config.Provider);
		Border border = ConnectorKit.Monogram(providerOption.Letters, providerOption.Brand, 22.0);
		border.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		grid8.Children.Add(border);
		_accountText = ConnectorKit.Text("", 11.5, ThemeTokens.Text2, AynType.Medium);
		_accountText.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_accountText, 1);
		grid8.Children.Add(_accountText);
		_disconnect = new ConnectorKit.ConfirmAction("Disconnect", "Remove key?", "Disconnect", DoDisconnect);
		((TextBlock)((Border)_disconnect.Children[0]).Child).FontSize = 11.5;
		Grid.SetColumn(_disconnect, 2);
		grid8.Children.Add(_disconnect);
		_disconnectError = ConnectorKit.Wrap("", 11.5, ThemeTokens.Danger, AynType.Regular);
		_disconnectError.Visibility = Visibility.Collapsed;
		StackPanel stackPanel5 = new StackPanel();
		stackPanel5.Children.Add(_disconnectError);
		stackPanel5.Children.Add(grid8);
		Grid.SetRow(stackPanel5, 1);
		grid6.Children.Add(stackPanel5);
		Border element = ConnectorKit.Card(grid6, new Thickness(12.0, 12.0, 12.0, 10.0));
		Grid.SetColumn(element, 2);
		grid.Children.Add(element);
		_dash = grid;
		return grid;
	}

	private void DoDisconnect()
	{
		if (!_svc.Disconnect())
		{
			_disconnectError.Text = "Ayn couldn't remove the key from this PC. Please try again.";
			_disconnectError.Visibility = Visibility.Visible;
			return;
		}
		_snap = null;
		_lastError = null;
		if (_timer != null)
		{
			_timer.Stop();
		}
		_mode = Mode.Picker;
		Render();
	}

	private void RefreshIfStale(bool force)
	{
		if (_mode == Mode.Dashboard && _root != null && _svc.IsConnected && (force || State == ToolState.Active))
		{
			RevenueRange range = _svc.Config.Range;
			if ((_snap == null || _snap.Range != range || DateTime.UtcNow - _snap.FetchedUtc > StaleAfter || force) && _svc.Fetch(range, force, _root.Dispatcher, delegate(RevenueSnapshot snap, Exception err)
			{
				_loading = false;
				if (_svc.Config.Range == range && _svc.IsConnected)
				{
					if (snap != null)
					{
						_snap = snap;
						_lastError = null;
					}
					else
					{
						_lastError = err;
					}
					RenderSnapshot();
				}
			}))
			{
				_loading = true;
				RenderSnapshot();
			}
		}
	}

	private void UpdateAgo()
	{
		if (_updated == null)
		{
			return;
		}
		if (_snap == null)
		{
			_updated.Text = "";
			_updated.ToolTip = null;
			return;
		}
		string text = ConnectorKit.Ago(_snap.FetchedUtc);
		if (_lastError == null)
		{
			_updated.Text = (_loading ? "Refreshing…" : ("Updated " + text));
			_updated.Foreground = ThemeTokens.Text3;
			_updated.ToolTip = null;
		}
		else
		{
			RevenueException ex = _lastError as RevenueException;
			_updated.Text = ((ex != null && ex.Kind == RevenueErrorKind.Offline) ? "Offline · " : "Couldn't refresh · ") + "showing " + text;
			_updated.Foreground = ThemeTokens.Text2;
			RevenueService.Describe(_lastError, out var title, out var advice);
			_updated.ToolTip = BuddyTooltip.Create(title + ". " + advice);
		}
	}

	private static bool NeedsReconnect(Exception ex)
	{
		if (ex is RevenueException ex2)
		{
			if (ex2.Kind != RevenueErrorKind.WrongKey && ex2.Kind != RevenueErrorKind.Revoked && ex2.Kind != RevenueErrorKind.NoPermission)
			{
				return ex2.Kind == RevenueErrorKind.NotConfigured;
			}
			return true;
		}
		return false;
	}

	private static string RangeLabel(RevenueRange r)
	{
		return r switch
		{
			RevenueRange.Week => "LAST 7 DAYS", 
			RevenueRange.Today => "TODAY", 
			_ => "LAST 30 DAYS", 
		};
	}

	private void RenderSnapshot()
	{
		if (_dash == null)
		{
			return;
		}
		RevenueSnapshot snap = _snap;
		RevenueRange range = _svc.Config.Range;
		_label.Text = "SALES · " + RangeLabel(range);
		string text = ((_svc.Provider != null) ? _svc.Provider.Name : "");
		_accountText.Text = text + (string.IsNullOrEmpty(_svc.Config.AccountLabel) ? "" : (" · " + _svc.Config.AccountLabel));
		UpdateAgo();
		if (_lastError != null && (snap == null || NeedsReconnect(_lastError)))
		{
			RevenueService.Describe(_lastError, out var title, out var advice);
			RevenueException ex = _lastError as RevenueException;
			if (NeedsReconnect(_lastError))
			{
				_dashNotice.Show(ConnectorKit.NoticeKind.Error, title, advice, "Reconnect", Reconnect);
			}
			else
			{
				_dashNotice.Show((ex != null && ex.Kind == RevenueErrorKind.Offline) ? ConnectorKit.NoticeKind.Offline : ConnectorKit.NoticeKind.Error, title, advice, "Try again", delegate
				{
					_lastError = null;
					RefreshIfStale(force: true);
				});
			}
		}
		else
		{
			_dashNotice.Hide();
		}
		_loadingRow.Visibility = ((snap != null || !_loading || _lastError != null) ? Visibility.Collapsed : Visibility.Visible);
		if (snap == null)
		{
			_hero.Text = ((_loading || _lastError != null) ? "—" : "");
			_hero.Foreground = ThemeTokens.Text3;
			_heroSub.Text = "";
			_message.Visibility = Visibility.Collapsed;
			_chart.SetPoints(null, AnalyticsRange.Week);
			_axis.Children.Clear();
			BuildTiles(null);
			return;
		}
		_hero.Foreground = ThemeTokens.Text;
		_hero.Text = RevenueMath.Format(snap.Gross ?? 0, snap.Currency);
		int num = snap.Sales ?? 0;
		_heroSub.Text = num.ToString("N0", CultureInfo.CurrentCulture) + ((num == 1) ? " sale" : " sales") + (snap.Partial ? (" (first " + num.ToString("N0", CultureInfo.CurrentCulture) + ")") : "");
		string text2 = "Gross sales in " + snap.Currency + " · " + ((snap.RangeStart == snap.RangeEnd) ? snap.RangeStart : (snap.RangeStart + " – " + snap.RangeEnd)) + " (this PC's time zone)";
		if (snap.OtherCurrencies.Count > 0)
		{
			text2 = text2 + "\nAlso sales in " + string.Join(", ", snap.OtherCurrencies) + " — not added in.";
		}
		if (snap.Partial)
		{
			text2 += "\nThere were more sales than Ayn reads at once; the totals are a lower bound.";
		}
		_hero.ToolTip = BuddyTooltip.Create(text2);
		bool flag = num == 0;
		_message.Text = ((!flag) ? "" : ((range == RevenueRange.Today) ? "No sales yet today." : "No sales in this period."));
		_message.Visibility = ((!flag || _dashNotice.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible);
		AnalyticsRange range2 = range switch
		{
			RevenueRange.Week => AnalyticsRange.Week, 
			RevenueRange.Today => AnalyticsRange.Today, 
			_ => AnalyticsRange.Month, 
		};
		bool flag2 = _dashNotice.Visibility == Visibility.Visible;
		_chart.SetPoints((flag || flag2) ? null : snap.Series, range2);
		_axis.Children.Clear();
		if (!flag && !flag2 && snap.Series != null && snap.Series.Count > 0)
		{
			object obj;
			if (snap.Series.Count != 1)
			{
				obj = new int[3]
				{
					0,
					(snap.Series.Count - 1) / 2,
					snap.Series.Count - 1
				};
			}
			else
			{
				int[] array = new int[1];
				obj = array;
			}
			int[] array2 = (int[])obj;
			for (int i = 0; i < array2.Length; i++)
			{
				TextBlock textBlock = ConnectorKit.Text(AnalyticsTool.AxisLabel(snap.Series[array2[i]].Time, range2), 11.5, ThemeTokens.Text2, AynType.Medium);
				textBlock.HorizontalAlignment = ((i != 0) ? ((i != array2.Length - 1) ? HorizontalAlignment.Center : HorizontalAlignment.Right) : HorizontalAlignment.Left);
				_axis.Children.Add(textBlock);
			}
		}
		BuildTiles(snap);
	}

	private void BuildTiles(RevenueSnapshot s)
	{
		_tiles.Children.Clear();
		_tiles.RowDefinitions.Clear();
		_tiles.ColumnDefinitions.Clear();
		List<Tuple<string, string, string>> list = new List<Tuple<string, string, string>>();
		if (s == null)
		{
			if (!_loading)
			{
				return;
			}
			string[] array = new string[4] { "Net after fees", "Refunds", "Available", "Pending" };
			foreach (string item in array)
			{
				list.Add(Tuple.Create<string, string, string>(item, null, null));
			}
		}
		else
		{
			list.Add(Tuple.Create("Net after fees", RevenueMath.Format(s.Net ?? 0, s.Currency), (s.Provider == "stripe") ? "After Stripe fees and refunds" : "After Gumroad fees, refunds excluded"));
			list.Add(Tuple.Create<string, string, string>("Refunds", RevenueMath.Format(s.Refunds ?? 0, s.Currency), null));
			if (s.Available.HasValue)
			{
				list.Add(Tuple.Create("Available", RevenueMath.Format(s.Available.Value, s.Currency), "Ready to pay out (Stripe balance)"));
			}
			if (s.Pending.HasValue)
			{
				list.Add(Tuple.Create("Pending", RevenueMath.Format(s.Pending.Value, s.Currency), "On its way to your balance"));
			}
			if (list.Count < 4)
			{
				list.Add(Tuple.Create<string, string, string>("Sales", (s.Sales ?? 0).ToString("N0", CultureInfo.CurrentCulture), null));
			}
			if (list.Count < 4 && s.OtherCurrencies.Count > 0)
			{
				list.Add(Tuple.Create("Other currencies", string.Join(", ", s.OtherCurrencies), "Sales in these currencies aren't added into the totals"));
			}
		}
		_tiles.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		_tiles.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(8.0)
		});
		_tiles.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		for (int j = 0; j < list.Count; j++)
		{
			int num = j / 2;
			while (_tiles.RowDefinitions.Count <= num * 2)
			{
				if (_tiles.RowDefinitions.Count > 0)
				{
					_tiles.RowDefinitions.Add(new RowDefinition
					{
						Height = new GridLength(8.0)
					});
				}
				_tiles.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
			}
			ConnectorKit.StatTile statTile = new ConnectorKit.StatTile(list[j].Item1, 17.0);
			statTile.Padding = new Thickness(12.0, 9.0, 12.0, 9.0);
			ConnectorKit.StatTile statTile2 = statTile;
			if (list[j].Item2 == null)
			{
				statTile2.SetLoading();
			}
			else
			{
				statTile2.Set(list[j].Item2, null, list[j].Item3);
			}
			Grid.SetRow(statTile2, num * 2);
			Grid.SetColumn(statTile2, j % 2 * 2);
			_tiles.Children.Add(statTile2);
		}
	}

	private void Reconnect()
	{
		_formProvider = _svc.Config.Provider;
		_mode = Mode.Form;
		Render();
	}

	internal void OpenForm(string provider)
	{
		_formProvider = provider;
		_mode = Mode.Form;
		Render();
	}

	internal void OpenPicker()
	{
		_mode = Mode.Picker;
		Render();
	}

	internal void ShowDashboardForQa(RevenueSnapshot s, Exception err, bool loading)
	{
		_mode = Mode.Dashboard;
		_snap = s;
		_lastError = err;
		_loading = loading;
		Render();
	}
}
