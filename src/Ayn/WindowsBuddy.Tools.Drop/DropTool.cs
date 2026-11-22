using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools.Clipboard;
using WindowsBuddy.Tools.FileShelf;

namespace WindowsBuddy.Tools.Drop;

public sealed class DropTool : IBuddyTool, IFileDropTarget, IEscapeAware
{
	internal enum Stage
	{
		Idle,
		Public,
		Offline,
		Firewall,
		Prepare,
		Starting,
		Waiting,
		Connected,
		Transferring,
		Done
	}

	private sealed class Ring : FrameworkElement
	{
		private double value;

		private double shown;

		public bool Indeterminate;

		public Ring(double size)
		{
			base.Width = (base.Height = size);
			base.IsHitTestVisible = false;
			base.RenderTransformOrigin = new Point(0.5, 0.5);
			RoutedEventHandler routedEventHandler = delegate
			{
				Spin();
			};
			base.Loaded += routedEventHandler;
		}

		private void Spin()
		{
			if (Indeterminate && CapsuleMotion.AnimationsEnabled)
			{
				RotateTransform rotateTransform = (RotateTransform)(base.RenderTransform = new RotateTransform());
				rotateTransform.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0.0, 360.0, TimeSpan.FromMilliseconds(1100.0))
				{
					RepeatBehavior = RepeatBehavior.Forever
				});
			}
		}

		public void Set(double v)
		{
			Set(v, animate: false);
		}

		public void Set(double v, bool animate)
		{
			value = Math.Max(0.0, Math.Min(1.0, v));
			if (!animate || !CapsuleMotion.AnimationsEnabled)
			{
				shown = value;
				InvalidateVisual();
				return;
			}
			double from = shown;
			DateTime start = DateTime.UtcNow;
			EventHandler frame = null;
			frame = delegate
			{
				double num = Math.Min(1.0, (DateTime.UtcNow - start).TotalMilliseconds / BuddyTheme.Value.TimeSpan.TotalMilliseconds);
				shown = from + (value - from) * (1.0 - Math.Pow(1.0 - num, 3.0));
				InvalidateVisual();
				if (num >= 1.0)
				{
					CompositionTarget.Rendering -= frame;
				}
			};
			CompositionTarget.Rendering += frame;
		}

		protected override void OnRender(DrawingContext dc)
		{
			double num = base.Width / 2.0;
			double num2 = num - 4.0;
			dc.PushOpacity(0.12);
			dc.DrawEllipse(null, new Pen(ThemeTokens.Text, 6.0), new Point(num, num), num2, num2);
			dc.Pop();
			Pen pen = new Pen(ThemeTokens.Accent, 6.0);
			pen.StartLineCap = PenLineCap.Round;
			pen.EndLineCap = PenLineCap.Round;
			Pen pen2 = pen;
			if (shown >= 0.99999)
			{
				dc.DrawEllipse(null, pen2, new Point(num, num), num2, num2);
			}
			else if (shown > 0.0001)
			{
				double num3 = shown * 2.0 * Math.PI;
				PathFigure pathFigure = new PathFigure();
				pathFigure.StartPoint = new Point(num, num - num2);
				PathFigure pathFigure2 = pathFigure;
				pathFigure2.Segments.Add(new ArcSegment(new Point(num + num2 * Math.Sin(num3), num - num2 * Math.Cos(num3)), new Size(num2, num2), 0.0, shown > 0.5, SweepDirection.Clockwise, isStroked: true));
				dc.DrawGeometry(null, pen2, new PathGeometry(new PathFigure[1] { pathFigure2 }));
			}
		}
	}

	private sealed class DrawnCheck : Grid
	{
		private const double Length = 10.285714285714286;

		public DrawnCheck(double size)
		{
			base.Width = (base.Height = size);
			base.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			base.IsHitTestVisible = false;
			Ellipse element = new Ellipse
			{
				Fill = ThemeTokens.SuccessSoft
			};
			Ellipse element2 = new Ellipse
			{
				Stroke = ThemeTokens.Success,
				StrokeThickness = 2.0,
				Margin = new Thickness(1.0)
			};
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M 20,33 L 28,41 L 44,24"),
				Stroke = ThemeTokens.Success,
				StrokeThickness = 3.5,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round,
				StrokeLineJoin = PenLineJoin.Round,
				StrokeDashArray = new DoubleCollection { 10.285714285714286, 10.285714285714286 },
				StrokeDashOffset = 0.0,
				Stretch = Stretch.None,
				RenderTransform = new ScaleTransform(size / 64.0, size / 64.0)
			};
			base.Children.Add(element);
			base.Children.Add(element2);
			base.Children.Add(path);
			if (CapsuleMotion.AnimationsEnabled)
			{
				ScaleTransform scaleTransform = new ScaleTransform(0.85, 0.85);
				base.RenderTransformOrigin = new Point(0.5, 0.5);
				base.RenderTransform = scaleTransform;
				DoubleAnimation animation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260.0))
				{
					EasingFunction = new BackEase
					{
						Amplitude = 0.35,
						EasingMode = EasingMode.EaseOut
					}
				};
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
				path.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(10.285714285714286, 0.0, TimeSpan.FromMilliseconds(360.0))
				{
					BeginTime = TimeSpan.FromMilliseconds(140.0),
					EasingFunction = BuddyTheme.EaseOut
				});
			}
		}
	}

	private const double FilesWidth = 318.0;

	private const double MorphMs = 300.0;

	private const double SentHoldMs = 2000.0;

	private const double RecheckMs = 3000.0;

	private bool enabled = true;

	private static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(10.0);

	internal static Func<List<DropAdapter>> AdapterSource = ReadAdapters;

	private static string lastDecision;

	private DropNetworkWatcher watcher;

	internal static Action<DropTool, object> LaunchOverride;

	internal static Func<bool> FirewallSource = () => DropFirewall.HelperBlocked(HelperPath);

	internal static Func<bool> FirewallRuleSource = () => DropFirewall.HelperHasRule(HelperPath);

	private Grid root;

	private Grid cardContent;

	private Grid sideContent;

	private Border card;

	private Border cardFill;

	private Border hoverFill;

	private Border dragFill;

	private Border side;

	private Rectangle dashIdle;

	private Rectangle dashHover;

	private Rectangle dashDrag;

	private TranslateTransform sideShift;

	private TranslateTransform artLift;

	private TextBlock zoneTitle;

	private TextBlock zoneHint;

	private TextBlock countdown;

	private FrameworkElement art;

	private FrameworkElement plane;

	private Image qrImage;

	private Ring ring;

	private TextBlock ringValue;

	private TextBlock progressTitle;

	private TextBlock progressMeta;

	private StackPanel receivedRows;

	private int incomingPercent = -1;

	private Border incomingFill;

	private TextBlock incomingMeta;

	private string incomingName;

	private DispatcherTimer clock;

	private DispatcherTimer recheck;

	private DispatcherTimer sentHold;

	private DispatcherTimer noticeTimer;

	private Stage stage;

	private bool split;

	private bool dragOver;

	private Process host;

	private bool live;

	private bool active;

	private bool starting;

	private bool requestQueued;

	private string[] pendingFiles;

	private string pendingText;

	private string latestReceived;

	private readonly List<string> received = new List<string>();

	private bool receive;

	private bool completed;

	private int generation;

	private string pin = "";

	private DateTime expiresUtc;

	private List<DropAdapter> eligible = new List<DropAdapter>();

	private string selectedAddress;

	private long sendBytes;

	private int lastPercent = -1;

	public string Id => "drop";

	public string DisplayName => "Ayn Drop";

	public ToolCategory Category => ToolCategory.Productivity;

	public bool IsEnabled
	{
		get
		{
			return enabled;
		}
		set
		{
			enabled = value;
			if (!value)
			{
				Stop();
			}
		}
	}

	public int OrderIndex { get; set; }

	public bool HasCollapsedBadge => false;

	public bool RequiresNetwork => false;

	public bool IsInitialized { get; private set; }

	public ToolState State { get; private set; }

	public double DesiredWidth => 752.0;

	public double DesiredHeight => 352.0;

	internal static string HelperPath => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DropHost", "Ayn.Drop.exe");

	public static bool Available => ToolRegistry.Instance.GetTool("drop")?.IsEnabled ?? false;

	public static bool IsLive
	{
		get
		{
			if (ToolRegistry.Instance.GetTool("drop") is DropTool dropTool)
			{
				if (!dropTool.live)
				{
					return dropTool.starting;
				}
				return true;
			}
			return false;
		}
	}

	public static string SaveFolder
	{
		get
		{
			if (!string.IsNullOrEmpty(AppPreferences.Instance.DropSaveFolder))
			{
				return AppPreferences.Instance.DropSaveFolder;
			}
			Guid id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
			if (SHGetKnownFolderPath(ref id, 0u, IntPtr.Zero, out var path) != 0)
			{
				throw new IOException("Downloads folder is unavailable");
			}
			try
			{
				return System.IO.Path.Combine(Marshal.PtrToStringUni(path), "Ayn Drop");
			}
			finally
			{
				Marshal.FreeCoTaskMem(path);
			}
		}
	}

	private static List<DropAdapter> ReadAdapters()
	{
		List<string> list = new List<string>();
		List<DropAdapter> adapters = DropNetwork.GetAdapters(list.Add);
		string text = string.Join("; ", list.ToArray()) + " => " + DropNetwork.Assess(adapters);
		if (text != lastDecision)
		{
			lastDecision = text;
			TransactionLogger.Info(text);
		}
		return adapters;
	}

	public void Initialize()
	{
		IsInitialized = true;
	}

	public void Shutdown()
	{
		Stop();
		IsInitialized = false;
	}

	public void OnActivated()
	{
		active = true;
		State = ToolState.Active;
		if (watcher == null)
		{
			watcher = new DropNetworkWatcher(Dispatcher.CurrentDispatcher, RefreshAdapters);
		}
		if (watcher != null)
		{
			watcher.Start();
		}
		if (requestQueued)
		{
			Begin();
		}
		else
		{
			RefreshAdapters();
		}
	}

	public void OnDeactivated()
	{
		active = false;
		requestQueued = false;
		Stop();
		State = ToolState.Idle;
		if (watcher != null)
		{
			watcher.Stop();
		}
		StopTimer(recheck);
		StopTimer(sentHold);
		dragOver = false;
		if (root != null)
		{
			ShowIdle(animate: false);
		}
	}

	public CollapsedBadgeData GetCollapsedBadge()
	{
		return null;
	}

	public UIElement CreateIcon(double size, Brush stroke)
	{
		return FluentIcons.Create("phone_arrow_up", size, stroke, filled: false);
	}

	public UIElement GetHeaderActions()
	{
		return null;
	}

	public bool HandleEscape()
	{
		if (host != null || starting || live)
		{
			Cancel();
			return true;
		}
		if (stage == Stage.Done || stage == Stage.Prepare)
		{
			ShowIdle(animate: true);
			return true;
		}
		return false;
	}

	public void OnFileDragOver(bool over)
	{
		if (dragOver != over && root != null)
		{
			dragOver = over;
			if (stage == Stage.Idle)
			{
				ApplyZoneState(animate: true);
			}
		}
	}

	public void OnFilesDropped(string[] paths)
	{
		dragOver = false;
		if (paths != null && paths.Length != 0)
		{
			pendingFiles = paths;
			pendingText = null;
			receive = false;
			requestQueued = true;
			Begin();
		}
	}

	public static void SendFiles(string[] paths)
	{
		Open(paths, null);
	}

	public static void SendText(string text)
	{
		Open(null, text);
	}

	private static void Open(string[] paths, string text)
	{
		if (ToolRegistry.Instance.GetTool("drop") is DropTool { IsEnabled: not false } dropTool)
		{
			dropTool.pendingFiles = paths;
			dropTool.pendingText = text;
			dropTool.receive = false;
			dropTool.requestQueued = true;
			CapsuleWindow.Instance?.OpenToolFromSettings("drop");
			if (dropTool.root != null)
			{
				dropTool.root.Dispatcher.BeginInvoke(new Action(dropTool.Begin));
			}
		}
	}

	public UIElement CreateView()
	{
		if (root != null)
		{
			return root;
		}
		root = BuddyTheme.Crisp(new Grid
		{
			Width = 728.0,
			Height = 242.0,
			Background = Brushes.Transparent,
			AllowDrop = true,
			Tag = "NoOuterScroll"
		});
		card = new Border
		{
			Width = 728.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
			CornerRadius = new CornerRadius(16.0),
			ClipToBounds = true,
			Cursor = System.Windows.Input.Cursors.Hand
		};
		Grid grid = new Grid();
		cardFill = new Border
		{
			Background = ThemeTokens.Surface,
			CornerRadius = card.CornerRadius,
			Opacity = 0.0
		};
		hoverFill = new Border
		{
			Background = ThemeTokens.Selection,
			CornerRadius = card.CornerRadius,
			Opacity = 0.0
		};
		dragFill = new Border
		{
			Background = ThemeTokens.AccentSoft,
			CornerRadius = card.CornerRadius,
			Opacity = 0.0
		};
		dashIdle = Dash(ThemeTokens.BorderStrong, 1.0);
		dashHover = Dash(ThemeTokens.Text3, 0.0);
		dashDrag = Dash(ThemeTokens.Accent, 0.0);
		cardContent = new Grid();
		UIElement[] array = new UIElement[7] { cardFill, hoverFill, dragFill, dashIdle, dashHover, dashDrag, cardContent };
		foreach (UIElement element in array)
		{
			grid.Children.Add(element);
		}
		card.Child = grid;
		card.MouseEnter += delegate
		{
			ApplyZoneState(animate: true);
		};
		card.MouseLeave += delegate
		{
			ApplyZoneState(animate: true);
		};
		card.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (stage == Stage.Idle)
			{
				e.Handled = true;
				PickFiles();
			}
		};
		root.Children.Add(card);
		sideShift = new TranslateTransform(12.0, 0.0);
		side = new Border
		{
			Width = 400.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			CornerRadius = new CornerRadius(16.0),
			Background = ThemeTokens.Surface,
			Padding = new Thickness(14.0),
			Opacity = 0.0,
			Visibility = Visibility.Collapsed,
			RenderTransform = sideShift
		};
		sideContent = new Grid();
		side.Child = sideContent;
		root.Children.Add(side);
		root.DragOver += delegate(object s, System.Windows.DragEventArgs e)
		{
			if (ClipboardDrag.CanDrop(e.Data))
			{
				e.Effects = System.Windows.DragDropEffects.Copy;
				OnFileDragOver(over: true);
				e.Handled = true;
			}
		};
		root.DragLeave += delegate(object s, System.Windows.DragEventArgs e)
		{
			Point position = e.GetPosition(root);
			if (position.X <= 0.0 || position.Y <= 0.0 || position.X >= root.ActualWidth || position.Y >= root.ActualHeight)
			{
				OnFileDragOver(over: false);
			}
		};
		root.Drop += delegate(object s, System.Windows.DragEventArgs e)
		{
			if (ClipboardDrag.CanDrop(e.Data))
			{
				ClipboardDrag.ToDrop(e.Data);
				e.Handled = true;
			}
		};
		root.Unloaded += delegate
		{
			Stop();
		};
		ShowIdle(animate: false);
		return root;
	}

	private static Rectangle Dash(Brush stroke, double opacity)
	{
		Rectangle rectangle = new Rectangle();
		rectangle.Stroke = stroke;
		rectangle.StrokeThickness = 1.0;
		rectangle.StrokeDashArray = new DoubleCollection { 4.0, 4.0 };
		rectangle.RadiusX = 16.0;
		rectangle.RadiusY = 16.0;
		rectangle.Opacity = opacity;
		rectangle.SnapsToDevicePixels = true;
		rectangle.IsHitTestVisible = false;
		return rectangle;
	}

	private void ApplyZoneState(bool animate)
	{
		if (card != null)
		{
			bool flag = stage == Stage.Idle;
			bool flag2 = flag && !dragOver && card.IsMouseOver;
			Fade(dashIdle, (flag && !flag2 && !dragOver) ? 1 : 0, animate);
			Fade(dashHover, flag2 ? 1 : 0, animate);
			Fade(dashDrag, (flag && dragOver) ? 1 : 0, animate);
			Fade(hoverFill, flag2 ? 1 : 0, animate);
			Fade(dragFill, (flag && dragOver) ? 1 : 0, animate);
			Fade(cardFill, (!flag) ? 1 : 0, animate);
			card.Cursor = (flag ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow);
			if (zoneTitle != null)
			{
				zoneTitle.Text = (dragOver ? "Release to send to your phone" : "Drop files to send to your phone");
			}
			if (artLift != null)
			{
				Move(artLift, TranslateTransform.YProperty, dragOver ? (-4) : 0, animate ? 220 : 0);
			}
		}
	}

	private void ShowIdle(bool animate)
	{
		StopTimer(clock);
		StopTimer(sentHold);
		SetStage(Stage.Idle);
		StackPanel stackPanel = Centered();
		artLift = new TranslateTransform();
		art = DropArt();
		art.RenderTransform = artLift;
		stackPanel.Children.Add(art);
		zoneTitle = BuddyTheme.Text("Drop files to send to your phone", 15.0, BuddyTheme.Text100, AynType.Medium);
		zoneTitle.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		zoneTitle.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		stackPanel.Children.Add(zoneTitle);
		zoneHint = BuddyTheme.Text("Or click to choose files", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		zoneHint.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		zoneHint.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel.Children.Add(zoneHint);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel2.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel2.Margin = new Thickness(0.0, 18.0, 0.0, 0.0);
		StackPanel stackPanel3 = stackPanel2;
		if (eligible.Count > 1)
		{
			FrameworkElement frameworkElement = AdapterChip();
			frameworkElement.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			stackPanel3.Children.Add(frameworkElement);
		}
		stackPanel3.Children.Add(Pill("arrow_down", "Receive from phone", StartReceive));
		stackPanel.Children.Add(stackPanel3);
		Swap(cardContent, stackPanel, animate);
		SetSplit(on: false, animate);
		ApplyZoneState(animate);
	}

	private void ShowNetworkProblem(bool isPublic, bool animate)
	{
		ShowNetworkProblem(isPublic ? Stage.Public : Stage.Offline, animate);
	}

	private void ShowNetworkProblem(Stage kind, bool animate)
	{
		bool flag = kind == Stage.Public;
		bool flag2 = kind == Stage.Firewall;
		if (stage != kind || cardContent.Children.Count <= 0)
		{
			SetStage(kind);
			Grid grid = new Grid();
			grid.VerticalAlignment = VerticalAlignment.Center;
			grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			grid.MaxWidth = 460.0;
			Grid grid2 = grid;
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Border border = new Border();
			border.Width = 48.0;
			border.Height = 48.0;
			border.CornerRadius = new CornerRadius(24.0);
			border.Background = ThemeTokens.WarningSoft;
			border.VerticalAlignment = VerticalAlignment.Top;
			border.Margin = new Thickness(0.0, 2.0, 16.0, 0.0);
			Border border2 = border;
			FrameworkElement frameworkElement = FluentIcons.Create(flag2 ? "dismiss_circle" : (flag ? "globe" : "wifi_off"), 22.0, ThemeTokens.Warning, filled: false);
			frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			border2.Child = frameworkElement;
			grid2.Children.Add(border2);
			StackPanel stackPanel = new StackPanel();
			Grid.SetColumn(stackPanel, 1);
			grid2.Children.Add(stackPanel);
			stackPanel.Children.Add(BuddyTheme.Text(flag2 ? "Windows Firewall is blocking Ayn Drop" : (flag ? "Your Wi-Fi is set to Public" : "Connect to Wi-Fi or Ethernet"), 15.0, BuddyTheme.Text100, AynType.Medium));
			TextBlock textBlock = BuddyTheme.Text(flag2 ? "Your phone can't connect. In Inbound Rules, delete the \"Ayn.Drop\" Block rules, then try again." : (flag ? "Ayn Drop only works on private networks for your safety." : "Your PC and phone need the same network. VPN-only connections can't be used."), 13.0, BuddyTheme.Text50, FontWeights.Normal);
			textBlock.TextWrapping = TextWrapping.Wrap;
			textBlock.TextTrimming = TextTrimming.None;
			textBlock.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
			stackPanel.Children.Add(textBlock);
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel2.Margin = new Thickness(0.0, 16.0, 0.0, 0.0);
			StackPanel stackPanel3 = stackPanel2;
			BuddyPrimaryPill buddyPrimaryPill = new BuddyPrimaryPill("settings", flag2 ? "Open firewall rules" : (flag ? "Open Wi-Fi settings" : "Open network settings"), 34.0, 15.0, new Thickness(14.0, 0.0, 16.0, 0.0), 8.0);
			if (flag2)
			{
				buddyPrimaryPill.Click += OpenFirewallRules;
			}
			else
			{
				buddyPrimaryPill.Click += OpenNetworkSettings;
			}
			stackPanel3.Children.Add(buddyPrimaryPill);
			StackPanel stackPanel4 = new StackPanel();
			stackPanel4.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel4.VerticalAlignment = VerticalAlignment.Center;
			stackPanel4.Margin = new Thickness(14.0, 0.0, 0.0, 0.0);
			StackPanel stackPanel5 = stackPanel4;
			Ellipse ellipse = new Ellipse();
			ellipse.Width = 6.0;
			ellipse.Height = 6.0;
			ellipse.Fill = ThemeTokens.Text3;
			ellipse.VerticalAlignment = VerticalAlignment.Center;
			ellipse.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
			Ellipse ellipse2 = ellipse;
			if (CapsuleMotion.AnimationsEnabled)
			{
				ellipse2.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(900.0))
				{
					AutoReverse = true,
					RepeatBehavior = RepeatBehavior.Forever,
					EasingFunction = BuddyTheme.EaseOut
				});
			}
			stackPanel5.Children.Add(ellipse2);
			stackPanel5.Children.Add(BuddyTheme.Text("Checking again automatically", 11.5, BuddyTheme.Text50, FontWeights.Normal));
			stackPanel3.Children.Add(stackPanel5);
			stackPanel.Children.Add(stackPanel3);
			Swap(cardContent, grid2, animate);
			SetSplit(on: false, animate);
			ApplyZoneState(animate);
			StartRecheck();
		}
	}

	private void ShowFirewallPrep()
	{
		SetStage(Stage.Prepare);
		Grid grid = new Grid();
		grid.VerticalAlignment = VerticalAlignment.Center;
		grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		grid.MaxWidth = 560.0;
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.Children.Add(FirewallDialogArt());
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(24.0, 0.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		Grid.SetColumn(stackPanel2, 1);
		grid2.Children.Add(stackPanel2);
		TextBlock textBlock = BuddyTheme.Text("Windows will ask to allow Ayn Drop on your network", 15.0, BuddyTheme.Text100, AynType.Medium);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		stackPanel2.Children.Add(textBlock);
		TextBlock textBlock2 = BuddyTheme.Text("Choose Allow so your phone can connect. Ayn Drop only listens on your private network, and only while a code is showing.", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.TextTrimming = TextTrimming.None;
		textBlock2.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel3.Margin = new Thickness(0.0, 16.0, 0.0, 0.0);
		StackPanel stackPanel4 = stackPanel3;
		BuddyPrimaryPill buddyPrimaryPill = new BuddyPrimaryPill("checkmark", "Continue", 34.0, 15.0, new Thickness(14.0, 0.0, 16.0, 0.0), 8.0);
		buddyPrimaryPill.Click += delegate
		{
			AppPreferences.Instance.DropFirewallExplainedPath = HelperPath;
			Begin();
		};
		stackPanel4.Children.Add(buddyPrimaryPill);
		Border border = BuddyKit.TextButton("Not now", delegate
		{
			ShowIdle(animate: true);
		});
		border.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		stackPanel4.Children.Add(border);
		stackPanel2.Children.Add(stackPanel4);
		Swap(cardContent, grid2, animate: true);
		SetSplit(on: false, animate: true);
		ApplyZoneState(animate: true);
	}

	private static FrameworkElement FirewallDialogArt()
	{
		Border border = new Border();
		border.Width = 176.0;
		border.Height = 124.0;
		border.CornerRadius = new CornerRadius(12.0);
		border.Background = ThemeTokens.Surface2;
		border.BorderBrush = ThemeTokens.BorderStrong;
		border.BorderThickness = new Thickness(1.0);
		border.Padding = new Thickness(12.0, 11.0, 12.0, 11.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		border.IsHitTestVisible = false;
		Border border2 = border;
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
		grid.Children.Add(BuddyTheme.Text("Windows Security", 11.0, BuddyTheme.Text50, FontWeights.Normal));
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		Grid.SetRow(stackPanel2, 1);
		FrameworkElement frameworkElement = FluentIcons.Create("phone_arrow_up", 16.0, ThemeTokens.Text, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(frameworkElement);
		TextBlock textBlock = BuddyTheme.Text("Ayn Drop", 13.0, BuddyTheme.Text100, AynType.Medium);
		textBlock.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(textBlock);
		grid.Children.Add(stackPanel2);
		Grid grid2 = new Grid();
		Grid.SetRow(grid2, 2);
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(6.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		Border border3 = new Border();
		border3.Height = 24.0;
		border3.CornerRadius = new CornerRadius(6.0);
		border3.Background = ThemeTokens.AccentStrong;
		border3.BorderBrush = ThemeTokens.Accent;
		border3.BorderThickness = new Thickness(0.0);
		Border border4 = border3;
		TextBlock textBlock2 = BuddyTheme.Text("Allow", 11.0, ThemeTokens.AccentFg, AynType.Medium);
		textBlock2.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		border4.Child = textBlock2;
		Border border5 = new Border();
		border5.CornerRadius = new CornerRadius(9.0);
		border5.Margin = new Thickness(-3.0);
		border5.BorderBrush = ThemeTokens.Accent;
		border5.BorderThickness = new Thickness(1.5);
		border5.Opacity = 0.55;
		Border border6 = border5;
		Grid grid3 = new Grid();
		grid3.Children.Add(border6);
		grid3.Children.Add(border4);
		if (CapsuleMotion.AnimationsEnabled)
		{
			border6.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.85, 0.2, TimeSpan.FromMilliseconds(1100.0))
			{
				AutoReverse = true,
				RepeatBehavior = RepeatBehavior.Forever,
				EasingFunction = BuddyTheme.EaseOut
			});
		}
		grid2.Children.Add(grid3);
		Border border7 = new Border();
		border7.Height = 24.0;
		border7.CornerRadius = new CornerRadius(6.0);
		border7.Background = ThemeTokens.Selection;
		Border border8 = border7;
		TextBlock textBlock3 = BuddyTheme.Text("Cancel", 11.0, BuddyTheme.Text30, FontWeights.Normal);
		textBlock3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock3.VerticalAlignment = VerticalAlignment.Center;
		border8.Child = textBlock3;
		Grid.SetColumn(border8, 2);
		grid2.Children.Add(border8);
		grid.Children.Add(grid2);
		border2.Child = grid;
		return border2;
	}

	private void ShowStarting()
	{
		SetStage(Stage.Starting);
		Swap(cardContent, receive ? ReceivedCard() : FilesCard(), animate: true);
		StackPanel stackPanel = Centered();
		Ring ring = new Ring(56.0);
		ring.Indeterminate = true;
		Ring ring2 = ring;
		ring2.Set(0.25);
		stackPanel.Children.Add(ring2);
		TextBlock textBlock = BuddyTheme.Text("Starting local sharing…", 13.0, BuddyTheme.Text50, FontWeights.Normal);
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock);
		Swap(sideContent, stackPanel, animate: false);
		SetSplit(on: true, animate: true);
		ApplyZoneState(animate: true);
	}

	private void ShowQr(BitmapSource image)
	{
		SetStage(Stage.Waiting);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		qrImage = new Image
		{
			Source = image,
			Width = 172.0,
			Height = 172.0,
			SnapsToDevicePixels = true
		};
		RenderOptions.SetBitmapScalingMode(qrImage, BitmapScalingMode.NearestNeighbor);
		Border border = new Border();
		border.Background = Brushes.White;
		border.CornerRadius = new CornerRadius(14.0);
		border.Padding = new Thickness(0.0);
		border.Child = qrImage;
		border.VerticalAlignment = VerticalAlignment.Center;
		Border border2 = border;
		border2.Clip = new RectangleGeometry(new Rect(0.0, 0.0, 172.0, 172.0), 14.0, 14.0);
		grid.Children.Add(border2);
		Grid grid2 = new Grid();
		grid2.Margin = new Thickness(16.0, 2.0, 0.0, 2.0);
		Grid grid3 = grid2;
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid3.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid.SetColumn(grid3, 1);
		grid.Children.Add(grid3);
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = BuddyTheme.Text(receive ? "Choose files on your phone" : "Scan with your phone camera", 13.5, BuddyTheme.Text100, AynType.Medium);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		stackPanel.Children.Add(textBlock);
		TextBlock textBlock2 = BuddyTheme.Text(receive ? "Scan the code, then pick photos or files to send to this PC." : "Same Wi-Fi as this PC. Nothing leaves your network.", 11.5, BuddyTheme.Text50, FontWeights.Normal);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.TextTrimming = TextTrimming.None;
		textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock2);
		if (pin.Length > 0)
		{
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel2.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
			StackPanel stackPanel3 = stackPanel2;
			TextBlock textBlock3 = BuddyTheme.Text("PIN", 11.0, BuddyTheme.Text50, AynType.Medium);
			textBlock3.VerticalAlignment = VerticalAlignment.Center;
			stackPanel3.Children.Add(textBlock3);
			TextBlock textBlock4 = BuddyTheme.Text(string.Join(" ", pin.Select((char c) => c.ToString()).ToArray()), 20.0, BuddyTheme.Text100, AynType.Semibold);
			AynType.Tabular(textBlock4);
			textBlock4.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
			stackPanel3.Children.Add(textBlock4);
			stackPanel.Children.Add(stackPanel3);
		}
		grid3.Children.Add(stackPanel);
		Grid grid4 = new Grid();
		Grid.SetRow(grid4, 2);
		countdown = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, FontWeights.Normal);
		AynType.Tabular(countdown);
		countdown.VerticalAlignment = VerticalAlignment.Center;
		grid4.Children.Add(countdown);
		Border border3 = BuddyKit.TextButton("Cancel", Cancel);
		border3.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		border3.Margin = new Thickness(0.0, 0.0, -10.0, -6.0);
		grid4.Children.Add(border3);
		grid3.Children.Add(grid4);
		Swap(sideContent, grid, animate: true);
		SetSplit(on: true, animate: true);
		ApplyZoneState(animate: true);
		StartClock();
	}

	private void ShowProgress(bool connectedOnly)
	{
		if (stage != Stage.Transferring || connectedOnly)
		{
			SetStage(connectedOnly ? Stage.Connected : Stage.Transferring);
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Grid grid2 = new Grid();
			grid2.Width = 132.0;
			grid2.Height = 132.0;
			grid2.VerticalAlignment = VerticalAlignment.Center;
			grid2.Margin = new Thickness(14.0, 0.0, 0.0, 0.0);
			Grid grid3 = grid2;
			ring = new Ring(132.0);
			grid3.Children.Add(ring);
			if (connectedOnly)
			{
				FrameworkElement frameworkElement = FluentIcons.Create("phone_arrow_up", 44.0, ThemeTokens.Accent, filled: false);
				frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
				frameworkElement.VerticalAlignment = VerticalAlignment.Center;
				grid3.Children.Add(frameworkElement);
				ringValue = null;
			}
			else
			{
				ringValue = BuddyTheme.Text("0%", 24.0, BuddyTheme.Text100, AynType.Semibold);
				AynType.Tabular(ringValue);
				ringValue.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
				ringValue.VerticalAlignment = VerticalAlignment.Center;
				grid3.Children.Add(ringValue);
			}
			grid.Children.Add(grid3);
			Grid grid4 = new Grid();
			grid4.Margin = new Thickness(22.0, 2.0, 0.0, 2.0);
			Grid grid5 = grid4;
			grid5.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid5.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid5.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid5.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			Grid.SetColumn(grid5, 1);
			grid.Children.Add(grid5);
			StackPanel stackPanel = new StackPanel();
			Grid.SetRow(stackPanel, 1);
			grid5.Children.Add(stackPanel);
			progressTitle = BuddyTheme.Text(connectedOnly ? "Phone connected" : (receive ? "Receiving from phone" : "Sending to phone"), 13.5, BuddyTheme.Text100, AynType.Medium);
			stackPanel.Children.Add(progressTitle);
			progressMeta = BuddyTheme.Text((!connectedOnly) ? "Starting…" : (receive ? "Choose files on your phone" : "Tap Download on your phone"), 11.5, BuddyTheme.Text50, FontWeights.Normal);
			AynType.Tabular(progressMeta);
			progressMeta.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
			stackPanel.Children.Add(progressMeta);
			Border border = BuddyKit.TextButton("Cancel", Cancel);
			border.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			border.Margin = new Thickness(0.0, 0.0, -10.0, -6.0);
			Grid.SetRow(border, 3);
			grid5.Children.Add(border);
			Swap(sideContent, grid, animate: true);
			StopTimer(clock);
		}
	}

	internal void UpdateProgress(int percent, long bytes, long total, long rate, long eta, string name, int file, int files)
	{
		if (stage != Stage.Transferring)
		{
			ShowProgress(connectedOnly: false);
		}
		percent = Math.Max(0, Math.Min(100, percent));
		lastPercent = percent;
		ring.Set((double)percent / 100.0, animate: true);
		if (receive)
		{
			incomingName = name;
			bool flag = incomingPercent < 0;
			incomingPercent = percent;
			if (flag || incomingFill == null)
			{
				RefreshReceived();
			}
			else
			{
				UpdateIncoming();
			}
		}
		ringValue.Text = percent + "%";
		if (progressTitle != null)
		{
			progressTitle.Text = (receive ? "Receiving from phone" : "Sending to phone") + ((files > 1) ? (" Â· " + file + " of " + files) : "");
		}
		progressMeta.Text = ProgressLine(bytes, total, rate, eta);
	}

	internal static string ProgressLine(long bytes, long total, long rate, long eta)
	{
		if (total <= 0)
		{
			return "Transferringâ€¦";
		}
		string text = FormatSize(bytes) + " of " + FormatSize(total);
		if (rate > 0)
		{
			text = text + " Â· " + FormatSize(rate) + "/s";
		}
		if (rate > 0 && eta > 0)
		{
			text = text + " Â· " + Eta(eta);
		}
		return text;
	}

	internal static string Eta(long seconds)
	{
		if (seconds < 60)
		{
			return seconds + " s left";
		}
		if (seconds < 3600)
		{
			return (int)Math.Round((double)seconds / 60.0) + " min left";
		}
		return seconds / 3600 + " h " + (int)Math.Round((double)(seconds % 3600) / 60.0) + " min left";
	}

	private void ShowDone()
	{
		SetStage(Stage.Done);
		StopTimer(clock);
		StackPanel stackPanel = Centered();
		stackPanel.Children.Add(new DrawnCheck(64.0));
		TextBlock textBlock = BuddyTheme.Text(receive ? "Received ✓" : "Sent to phone ✓", 15.0, BuddyTheme.Text100, AynType.Medium);
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock);
		TextBlock textBlock2 = BuddyTheme.Text(receive ? (Count(received.Count) + " saved to " + System.IO.Path.GetFileName(SaveFolder.TrimEnd('\\'))) : Summary(), 13.0, BuddyTheme.Text50, FontWeights.Normal);
		textBlock2.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock2.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock2);
		if (receive && latestReceived != null)
		{
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel2.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			stackPanel2.Margin = new Thickness(0.0, 18.0, 0.0, 0.0);
			StackPanel stackPanel3 = stackPanel2;
			BuddyPrimaryPill buddyPrimaryPill = new BuddyPrimaryPill("open", "Open", 34.0, 15.0, new Thickness(14.0, 0.0, 16.0, 0.0), 8.0);
			buddyPrimaryPill.Click += delegate
			{
				OpenReceived(folder: false);
			};
			stackPanel3.Children.Add(buddyPrimaryPill);
			Border border = Pill("folder", "Show in folder", delegate
			{
				OpenReceived(folder: true);
			});
			border.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
			stackPanel3.Children.Add(border);
			Border border2 = BuddyKit.TextButton("Done", delegate
			{
				ShowIdle(animate: true);
			});
			border2.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
			border2.VerticalAlignment = VerticalAlignment.Center;
			stackPanel3.Children.Add(border2);
			stackPanel.Children.Add(stackPanel3);
		}
		Swap(cardContent, stackPanel, animate: true);
		SetSplit(on: false, animate: true);
		ApplyZoneState(animate: true);
		if (receive)
		{
			return;
		}
		if (sentHold == null)
		{
			sentHold = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(2000.0)
			};
			sentHold.Tick += delegate
			{
				sentHold.Stop();
				if (stage == Stage.Done)
				{
					ShowIdle(animate: true);
				}
			};
		}
		sentHold.Stop();
		sentHold.Start();
	}

	private FrameworkElement FilesCard()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(14.0, 14.0, 14.0, 12.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(CardLabel((pendingText != null) ? "Sending text" : ("Sending " + Summary())));
		if (pendingText != null)
		{
			FrameworkElement element = FileRow("text_t", "Text", pendingText.Length + " characters");
			stackPanel2.Children.Add(element);
			TextBlock textBlock = BuddyTheme.Text(pendingText.Replace('\r', ' ').Replace('\n', ' '), 11.5, BuddyTheme.Text50, FontWeights.Normal);
			textBlock.TextWrapping = TextWrapping.Wrap;
			textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			textBlock.MaxHeight = 64.0;
			textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
			stackPanel2.Children.Add(textBlock);
			return stackPanel2;
		}
		string[] array = pendingFiles ?? new string[0];
		int num = ((array.Length > 4) ? 3 : array.Length);
		for (int i = 0; i < num; i++)
		{
			string text = array[i];
			bool flag = Directory.Exists(text);
			stackPanel2.Children.Add(FileRow(flag ? "folder" : "document_text", System.IO.Path.GetFileName(text.TrimEnd('\\')), flag ? "Folder" : FormatSize(SizeOf(text))));
		}
		if (array.Length > num)
		{
			stackPanel2.Children.Add(FileRow("more_horizontal", "+" + (array.Length - num) + " more", FormatSize(array.Skip(num).Sum((string f) => SizeOf(f)))));
		}
		return stackPanel2;
	}

	private FrameworkElement ReceivedCard()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Margin = new Thickness(14.0, 14.0, 14.0, 12.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(CardLabel("Saving to " + System.IO.Path.GetFileName(SaveFolder.TrimEnd('\\'))));
		receivedRows = new StackPanel();
		stackPanel2.Children.Add(receivedRows);
		RefreshReceived();
		return stackPanel2;
	}

	private void RefreshReceived()
	{
		if (receivedRows == null)
		{
			return;
		}
		receivedRows.Children.Clear();
		incomingFill = null;
		incomingMeta = null;
		if (incomingPercent >= 0)
		{
			Grid grid = (Grid)FileRow("arrow_down", string.IsNullOrEmpty(incomingName) ? "Incoming file" : incomingName, "");
			StackPanel stackPanel = (StackPanel)grid.Children[1];
			stackPanel.Children.RemoveAt(1);
			Grid grid2 = new Grid();
			grid2.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
			Grid grid3 = grid2;
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid3.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(40.0)
			});
			Border border = new Border();
			border.Height = 4.0;
			border.CornerRadius = new CornerRadius(2.0);
			border.Background = ThemeTokens.Track;
			border.VerticalAlignment = VerticalAlignment.Center;
			Border border2 = border;
			incomingFill = new Border
			{
				Height = 4.0,
				CornerRadius = new CornerRadius(2.0),
				Background = ThemeTokens.Accent,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				Width = 0.0
			};
			border2.Child = incomingFill;
			grid3.Children.Add(border2);
			incomingMeta = BuddyTheme.Text("", 11.0, BuddyTheme.Text50, FontWeights.Normal);
			AynType.Tabular(incomingMeta);
			incomingMeta.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			Grid.SetColumn(incomingMeta, 1);
			grid3.Children.Add(incomingMeta);
			stackPanel.Children.Add(grid3);
			receivedRows.Children.Add(grid);
			border2.SizeChanged += delegate
			{
				UpdateIncoming();
			};
			UpdateIncoming();
		}
		if (received.Count == 0 && incomingPercent < 0)
		{
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Margin = new Thickness(0.0, 52.0, 0.0, 0.0);
			StackPanel stackPanel3 = stackPanel2;
			FrameworkElement frameworkElement = FluentIcons.Create("arrow_down", 20.0, BuddyTheme.Text50, filled: false);
			frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			stackPanel3.Children.Add(frameworkElement);
			TextBlock textBlock = BuddyTheme.Text("Files from your phone appear here", 13.0, BuddyTheme.Text50, FontWeights.Normal);
			textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
			stackPanel3.Children.Add(textBlock);
			receivedRows.Children.Add(stackPanel3);
			return;
		}
		List<string> list = received.AsEnumerable().Reverse().Take((incomingPercent >= 0) ? 3 : 4)
			.ToList();
		foreach (string item in list)
		{
			receivedRows.Children.Add(FileRow("checkmark_circle", System.IO.Path.GetFileName(item), FormatSize(SizeOf(item))));
		}
		if (received.Count > 4)
		{
			receivedRows.Children.Add(BuddyTheme.Text("+" + (received.Count - 4) + " earlier", 11.0, BuddyTheme.Text30, FontWeights.Normal));
		}
	}

	private void UpdateIncoming()
	{
		if (incomingFill != null)
		{
			Border border = (Border)incomingFill.Parent;
			incomingFill.Width = Math.Max(4.0, border.ActualWidth * (double)Math.Max(0, Math.Min(100, incomingPercent)) / 100.0);
			incomingMeta.Text = incomingPercent + "%";
		}
	}

	private static FrameworkElement CardLabel(string text)
	{
		TextBlock textBlock = BuddyTheme.Text(text, 11.0, BuddyTheme.Text50, AynType.Medium);
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		return textBlock;
	}

	private static FrameworkElement FileRow(string icon, string name, string meta)
	{
		Grid grid = new Grid();
		grid.Height = 40.0;
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		Grid grid2 = grid;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(40.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Border border = new Border();
		border.Width = 36.0;
		border.Height = 36.0;
		border.CornerRadius = new CornerRadius(10.0);
		border.Background = ThemeTokens.Selection;
		border.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		Border border2 = border;
		FrameworkElement frameworkElement = FluentIcons.Create(icon, 18.0, BuddyTheme.Text92, filled: false);
		frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		border2.Child = frameworkElement;
		grid2.Children.Add(border2);
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		Grid.SetColumn(stackPanel2, 1);
		grid2.Children.Add(stackPanel2);
		stackPanel2.Children.Add(BuddyTheme.Text(name, 13.0, BuddyTheme.Text92, AynType.Medium));
		TextBlock textBlock = BuddyTheme.Text(meta, 11.0, BuddyTheme.Text50, FontWeights.Normal);
		AynType.Tabular(textBlock);
		textBlock.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		return grid2;
	}

	private static StackPanel Centered()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		return stackPanel;
	}

	private static FrameworkElement DropArt()
	{
		Grid grid = new Grid();
		grid.Width = 72.0;
		grid.Height = 72.0;
		grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		grid.IsHitTestVisible = false;
		Grid grid2 = grid;
		grid2.Children.Add(new Ellipse
		{
			Fill = ThemeTokens.AccentSoft
		});
		FrameworkElement frameworkElement = FluentIcons.Create("phone_arrow_up", 40.0, ThemeTokens.Accent, filled: false);
		frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(frameworkElement);
		return grid2;
	}

	private static Border Pill(string icon, string text, Action click)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement frameworkElement = FluentIcons.Create(icon, 15.0, BuddyTheme.Text92, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(frameworkElement);
		TextBlock textBlock = BuddyTheme.Text(text, 13.0, BuddyTheme.Text92, AynType.Medium);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		CornerRadius cornerRadius = new CornerRadius(17.0);
		Border hover = new Border
		{
			Background = ThemeTokens.Selection,
			CornerRadius = cornerRadius,
			Opacity = 0.0
		};
		Grid grid = new Grid();
		grid.Children.Add(hover);
		grid.Children.Add(new Border
		{
			Padding = new Thickness(14.0, 0.0, 16.0, 0.0),
			Child = stackPanel2
		});
		Border border = new Border();
		border.Height = 34.0;
		border.CornerRadius = cornerRadius;
		border.Background = ThemeTokens.Selection;
		border.Child = grid;
		border.Cursor = System.Windows.Input.Cursors.Hand;
		Border border2 = border;
		border2.MouseEnter += delegate
		{
			Fade(hover, 1.0, animate: true);
		};
		border2.MouseLeave += delegate
		{
			Fade(hover, 0.0, animate: true);
		};
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			click();
		};
		AutomationProperties.SetName(border2, text);
		return BuddyTheme.Pressable(border2);
	}

	private FrameworkElement AdapterChip()
	{
		DropAdapter current = eligible.FirstOrDefault((DropAdapter a) => a.Address == selectedAddress) ?? eligible[0];
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement frameworkElement = FluentIcons.Create((current.Name.IndexOf("ethernet", StringComparison.OrdinalIgnoreCase) >= 0) ? "plug_connected" : "wifi_1", 14.0, BuddyTheme.Text50, filled: false);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(frameworkElement);
		TextBlock textBlock = BuddyTheme.Text(current.Name + " · " + current.Address, 11.5, BuddyTheme.Text92, AynType.Medium);
		AynType.Tabular(textBlock);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(6.0, 0.0, 4.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		FrameworkElement frameworkElement2 = FluentIcons.Create("chevron_down", 12.0, BuddyTheme.Text50, filled: false);
		frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(frameworkElement2);
		Border chip = new Border
		{
			Height = 34.0,
			CornerRadius = new CornerRadius(17.0),
			Padding = new Thickness(12.0, 0.0, 10.0, 0.0),
			BorderThickness = new Thickness(1.0),
			BorderBrush = ThemeTokens.BorderStrong,
			Background = Brushes.Transparent,
			Child = stackPanel2,
			Cursor = System.Windows.Input.Cursors.Hand,
			Tag = "adapter-chip"
		};
		chip.MouseEnter += delegate
		{
			chip.Background = ThemeTokens.Selection;
		};
		chip.MouseLeave += delegate
		{
			chip.Background = Brushes.Transparent;
		};
		chip.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		chip.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			e.Handled = true;
			List<BuddyMenuItem> items = eligible.Select((DropAdapter a) => new BuddyMenuItem(a.Name + " · " + a.Address, null, delegate
			{
				selectedAddress = a.Address;
				ShowIdle(animate: false);
			})
			{
				IsChecked = (a.Address == current.Address)
			}).ToList();
			BuddyDropdown.Show(chip, items, null, 220.0);
		};
		return BuddyTheme.Pressable(chip);
	}

	private void SetStage(Stage next)
	{
		stage = next;
		if (next != Stage.Public && next != Stage.Offline && next != Stage.Firewall)
		{
			StopTimer(recheck);
		}
	}

	private void SetSplit(bool on, bool animate)
	{
		animate = animate && CapsuleMotion.AnimationsEnabled;
		if (split == on && card.Width == (on ? 318.0 : 728.0) && !card.HasAnimatedProperties)
		{
			return;
		}
		split = on;
		double num = (on ? 318.0 : 728.0);
		CubicEase cubicEase = new CubicEase();
		cubicEase.EasingMode = EasingMode.EaseInOut;
		CubicEase easingFunction = cubicEase;
		if (animate)
		{
			card.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(num, TimeSpan.FromMilliseconds(300.0))
			{
				EasingFunction = easingFunction,
				FillBehavior = FillBehavior.Stop
			});
		}
		else
		{
			card.BeginAnimation(FrameworkElement.WidthProperty, null);
		}
		card.Width = num;
		if (on)
		{
			side.Visibility = Visibility.Visible;
		}
		if (animate)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(on ? 1 : 0, TimeSpan.FromMilliseconds(on ? 240 : 160));
			doubleAnimation.BeginTime = TimeSpan.FromMilliseconds(on ? 90 : 0);
			doubleAnimation.EasingFunction = BuddyTheme.EaseOut;
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			if (!on)
			{
				doubleAnimation2.Completed += delegate
				{
					if (!split)
					{
						side.Visibility = Visibility.Collapsed;
					}
				};
			}
			side.BeginAnimation(UIElement.OpacityProperty, doubleAnimation2);
			sideShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation((!on) ? 12 : 0, TimeSpan.FromMilliseconds(300.0))
			{
				EasingFunction = easingFunction
			});
		}
		else
		{
			side.BeginAnimation(UIElement.OpacityProperty, null);
			sideShift.BeginAnimation(TranslateTransform.XProperty, null);
			side.Opacity = (on ? 1 : 0);
			sideShift.X = ((!on) ? 12 : 0);
			if (!on)
			{
				side.Visibility = Visibility.Collapsed;
			}
		}
	}

	private static void Swap(Grid host, UIElement next, bool animate)
	{
		animate = animate && CapsuleMotion.AnimationsEnabled;
		foreach (UIElement item in host.Children.Cast<UIElement>().ToList())
		{
			if (!animate)
			{
				host.Children.Remove(item);
				continue;
			}
			item.IsHitTestVisible = false;
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120.0));
			doubleAnimation.EasingFunction = BuddyTheme.EaseOut;
			DoubleAnimation doubleAnimation2 = doubleAnimation;
			UIElement o = item;
			doubleAnimation2.Completed += delegate
			{
				host.Children.Remove(o);
			};
			item.BeginAnimation(UIElement.OpacityProperty, doubleAnimation2);
		}
		host.Children.Add(next);
		if (animate)
		{
			next.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220.0))
			{
				BeginTime = TimeSpan.FromMilliseconds(80.0),
				EasingFunction = BuddyTheme.EaseOut,
				FillBehavior = FillBehavior.Stop
			});
		}
	}

	private static void Fade(UIElement e, double to, bool animate)
	{
		if (e != null)
		{
			if (!animate || !CapsuleMotion.AnimationsEnabled)
			{
				e.BeginAnimation(UIElement.OpacityProperty, null);
				e.Opacity = to;
			}
			else
			{
				e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, BuddyTheme.Hover)
				{
					EasingFunction = BuddyTheme.EaseOut
				});
			}
		}
	}

	private static void Move(TranslateTransform t, DependencyProperty p, double to, double ms)
	{
		if (ms <= 0.0 || !CapsuleMotion.AnimationsEnabled)
		{
			t.BeginAnimation(p, null);
			t.SetValue(p, to);
		}
		else
		{
			t.BeginAnimation(p, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
			{
				EasingFunction = BuddyTheme.EaseOut
			});
		}
	}

	private void StartClock()
	{
		if (clock == null)
		{
			clock = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(250.0)
			};
			clock.Tick += delegate
			{
				Tick();
			};
		}
		Tick();
		clock.Start();
	}

	private void Tick()
	{
		if (countdown != null)
		{
			TimeSpan timeSpan = expiresUtc - DateTime.UtcNow;
			if (timeSpan < TimeSpan.Zero)
			{
				timeSpan = TimeSpan.Zero;
			}
			countdown.Text = "Expires in " + (int)timeSpan.TotalMinutes + ":" + timeSpan.Seconds.ToString("00");
		}
	}

	private void StartRecheck()
	{
		if (recheck == null)
		{
			recheck = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(3000.0)
			};
			recheck.Tick += delegate
			{
				RefreshAdapters();
			};
		}
		if (!recheck.IsEnabled)
		{
			recheck.Start();
		}
	}

	private static void StopTimer(DispatcherTimer t)
	{
		t?.Stop();
	}

	private void Notice(string text)
	{
		if (zoneHint == null || stage != 0)
		{
			return;
		}
		zoneHint.Text = text;
		if (noticeTimer == null)
		{
			noticeTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(5.0)
			};
			noticeTimer.Tick += delegate
			{
				noticeTimer.Stop();
				if (zoneHint != null)
				{
					zoneHint.Text = "Or click to choose files";
				}
			};
		}
		noticeTimer.Stop();
		noticeTimer.Start();
	}

	private void PickFiles()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.Multiselect = true;
		openFileDialog.Title = "Send to phone";
		Microsoft.Win32.OpenFileDialog openFileDialog2 = openFileDialog;
		if (openFileDialog2.ShowDialog() == true)
		{
			OnFilesDropped(openFileDialog2.FileNames);
		}
	}

	private void StartReceive()
	{
		receive = true;
		pendingFiles = null;
		pendingText = null;
		Begin();
	}

	private void Cancel()
	{
		Stop();
		if (root != null)
		{
			ShowIdle(animate: true);
		}
	}

	private void OpenFirewallRules()
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo("wf.msc");
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch
		{
		}
	}

	private void OpenNetworkSettings()
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo("ms-settings:network-status");
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch
		{
		}
	}

	public static void PickSaveFolder()
	{
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "Save files received with Ayn Drop";
		folderBrowserDialog.SelectedPath = SaveFolder;
		using FolderBrowserDialog folderBrowserDialog2 = folderBrowserDialog;
		if (folderBrowserDialog2.ShowDialog() == DialogResult.OK)
		{
			AppPreferences.Instance.DropSaveFolder = folderBrowserDialog2.SelectedPath;
		}
	}

	[DllImport("shell32.dll")]
	private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);

	private async void RefreshAdapters()
	{
		if (root != null && host == null && !starting && !live && (stage == Stage.Idle || stage == Stage.Public || stage == Stage.Offline || stage == Stage.Firewall))
		{
			int epoch = generation;
			List<DropAdapter> list = await Task.Run(() => AdapterSource());
			bool blocked = await Task.Run(() => FirewallSource());
			if (active && generation == epoch && host == null && !starting && !live && (stage == Stage.Idle || stage == Stage.Public || stage == Stage.Offline || stage == Stage.Firewall))
			{
				ApplyAdapters(list, blocked);
			}
		}
	}

	private void ApplyAdapters(List<DropAdapter> list)
	{
		ApplyAdapters(list, firewallBlocked: false);
	}

	private void ApplyAdapters(List<DropAdapter> list, bool firewallBlocked)
	{
		List<DropAdapter> list2 = DropNetwork.OrderAdapters(list.Where((DropAdapter a) => a.Private)).ToList();
		bool flag = list2.Count != eligible.Count || list2.Where((DropAdapter a, int i) => a.Address != eligible[i].Address).Any();
		eligible = list2;
		DropNetworkState dropNetworkState = DropNetwork.Assess(list);
		if (dropNetworkState != 0)
		{
			ShowNetworkProblem(dropNetworkState == DropNetworkState.Public, stage == Stage.Idle);
			return;
		}
		if (firewallBlocked)
		{
			ShowNetworkProblem(Stage.Firewall, stage == Stage.Idle);
			return;
		}
		if (selectedAddress == null || !eligible.Any((DropAdapter a) => a.Address == selectedAddress))
		{
			selectedAddress = eligible[0].Address;
		}
		if (stage != 0 || flag)
		{
			ShowIdle(stage != Stage.Idle);
		}
	}

	private async void Begin()
	{
		if (!enabled || root == null || starting || !active)
		{
			return;
		}
		requestQueued = false;
		Stop();
		int epoch = generation;
		starting = true;
		try
		{
			List<DropAdapter> list = await Task.Run(() => AdapterSource());
			if (generation != epoch || !active)
			{
				return;
			}
			DropAdapter adapter = DropNetwork.ChooseAdapter(list, selectedAddress);
			if (adapter == null)
			{
				starting = false;
				eligible.Clear();
				ShowNetworkProblem(DropNetwork.Assess(list) == DropNetworkState.Public, animate: true);
			}
			else if (!receive && pendingText == null && (pendingFiles == null || pendingFiles.Length == 0))
			{
				starting = false;
			}
			else if (await Task.Run(() => FirewallSource()))
			{
				starting = false;
				ShowNetworkProblem(Stage.Firewall, animate: true);
			}
			else if (AppPreferences.Instance.DropFirewallExplainedPath != HelperPath && !(await Task.Run(() => FirewallRuleSource())))
			{
				starting = false;
				ShowFirewallPrep();
			}
			else
			{
				if (generation != epoch || !active)
				{
					return;
				}
				TransactionLogger.Info("Drop session on " + adapter.Name + " " + adapter.Address + (adapter.DefaultRoute ? " (active default route)" : "") + "; QR and helper bind this address");
				eligible = DropNetwork.OrderAdapters(list.Where((DropAdapter a) => a.Private)).ToList();
				received.Clear();
				latestReceived = null;
				completed = false;
				pin = "";
				lastPercent = -1;
				incomingPercent = -1;
				sendBytes = ((pendingFiles == null) ? ((pendingText != null) ? pendingText.Length : 0) : pendingFiles.Sum((string f) => SizeOf(f)));
				ShowStarting();
				var config = new
				{
					Address = adapter.Address,
					Files = (pendingFiles ?? new string[0]),
					Text = pendingText,
					Receive = receive,
					UsePin = (receive && AppPreferences.Instance.DropRequirePin),
					SaveFolder = SaveFolder
				};
				live = true;
				if (LaunchOverride != null)
				{
					LaunchOverride(this, config);
					return;
				}
				string executable = HelperPath;
				Process process = new Process
				{
					StartInfo = new ProcessStartInfo(executable)
					{
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardInput = true,
						RedirectStandardOutput = true,
						RedirectStandardError = true
					},
					EnableRaisingEvents = true
				};
				process.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
				{
					root.Dispatcher.BeginInvoke((Action)delegate
					{
						if (host == process)
						{
							if (e.Data != null)
							{
								Handle(e.Data);
							}
							else
							{
								host = null;
								live = false;
								RemovePlane();
								if (!completed)
								{
									ShowIdle(animate: true);
									Notice("Session ended. Drop files to start again.");
								}
								process.Dispose();
							}
						}
					});
				};
				process.ErrorDataReceived += delegate
				{
				};
				host = process;
				process.Start();
				HelperProcessJob.Adopt(process);
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
				process.StandardInput.WriteLine(AsciiJson(config));
				process.StandardInput.Flush();
			}
		}
		catch
		{
			Stop();
			ShowIdle(animate: true);
			Notice("Could not start Ayn Drop. Check your private network and try again.");
		}
		finally
		{
			if (generation == epoch)
			{
				starting = false;
			}
		}
	}

	internal static string AsciiJson(object config)
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = 4194304;
		string text = javaScriptSerializer.Serialize(config);
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			if (c < '\u0080')
			{
				stringBuilder.Append(c);
				continue;
			}
			StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
			int num = c;
			stringBuilder2.Append(num.ToString("x4"));
		}
		return stringBuilder.ToString();
	}

	private void Handle(string json)
	{
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			javaScriptSerializer.MaxJsonLength = 4194304;
			Dictionary<string, object> dictionary = javaScriptSerializer.Deserialize<Dictionary<string, object>>(json);
			string text = (string)dictionary["kind"];
			string text2 = (dictionary.ContainsKey("message") ? (dictionary["message"] as string) : null);
			if (text == "ready")
			{
				BitmapImage bitmapImage = new BitmapImage();
				using (MemoryStream streamSource = new MemoryStream(Convert.FromBase64String((string)dictionary["qr"])))
				{
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.StreamSource = streamSource;
					bitmapImage.EndInit();
					bitmapImage.Freeze();
				}
				pin = (dictionary.ContainsKey("pin") ? (((string)dictionary["pin"]) ?? "") : "");
				expiresUtc = DateTime.UtcNow + SessionLength;
				ShowQr(bitmapImage);
				if (PetPreferences.Instance.Enabled)
				{
					CarryPlane();
				}
				return;
			}
			if (text == "status" && text2 == "Connected")
			{
				if (stage == Stage.Waiting)
				{
					ShowProgress(connectedOnly: true);
				}
				return;
			}
			if (text == "status" && text2 != null)
			{
				if (incomingPercent >= 0)
				{
					incomingPercent = -1;
					RefreshReceived();
				}
				if (progressMeta != null)
				{
					progressMeta.Text = text2;
				}
				return;
			}
			switch (text)
			{
			case "progress":
				UpdateProgress(Convert.ToInt32(dictionary["percent"]), Number(dictionary, "bytes"), Number(dictionary, "total"), Number(dictionary, "rate"), Number(dictionary, "eta"), dictionary.ContainsKey("name") ? (dictionary["name"] as string) : null, (int)Number(dictionary, "file"), (int)Number(dictionary, "files"));
				break;
			case "received":
				latestReceived = (string)dictionary["path"];
				received.Add(latestReceived);
				incomingPercent = -1;
				FileShelfService.Instance.AddFiles(new string[1] { latestReceived });
				RefreshReceived();
				if (progressTitle != null)
				{
					progressTitle.Text = "Receiving from phone";
					progressMeta.Text = Count(received.Count) + " received";
				}
				break;
			case "expired":
			case "error":
				Stop();
				ShowIdle(animate: true);
				Notice((text == "expired") ? "Session expired. Drop files to start again." : (text2 ?? "Sharing stopped."));
				break;
			case "sent":
				completed = true;
				live = false;
				RemovePlane();
				ShowDone();
				DynamicActivityService.Instance.PublishActivity(new NotchActivity
				{
					Id = "drop_complete",
					Type = ActivityType.Custom,
					Title = (receive ? "Received ✓" : "Sent to phone ✓"),
					Subtitle = "Ayn Drop",
					NavigateToToolId = "drop",
					Duration = TimeSpan.FromSeconds(5.0),
					PlayPauseAction = ((latestReceived == null) ? null : ((Action)delegate
					{
						OpenReceived(folder: false);
					})),
					NextAction = ((latestReceived == null) ? null : ((Action)delegate
					{
						OpenReceived(folder: true);
					})),
					IconFactory = (double size, Brush ink) => CreateIcon(size, ink)
				});
				break;
			}
		}
		catch
		{
			Stop();
			ShowIdle(animate: true);
			Notice("Sharing stopped. Please try a new session.");
		}
	}

	private static long Number(Dictionary<string, object> data, string key)
	{
		if (!data.TryGetValue(key, out var value) || value == null)
		{
			return 0L;
		}
		return Convert.ToInt64(value, CultureInfo.InvariantCulture);
	}

	private void OpenReceived(bool folder)
	{
		if (latestReceived == null)
		{
			return;
		}
		try
		{
			if (folder)
			{
				Process.Start("explorer.exe", "/select,\"" + latestReceived + "\"");
				return;
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo(latestReceived);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch
		{
		}
	}

	private void CarryPlane()
	{
		RemovePlane();
		Grid grid = new Grid();
		grid.Width = 72.0;
		grid.Height = 42.0;
		grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		grid.VerticalAlignment = VerticalAlignment.Bottom;
		grid.Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
		grid.IsHitTestVisible = false;
		Grid grid2 = grid;
		grid2.Children.Add(new PetView("happy", 40.0));
		grid2.Children.Add(new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse("M 0,0 L 23,8 L 8,13 L 0,0 M 8,13 L 11,7 L 23,8"),
			Stroke = ThemeTokens.Text,
			Fill = ThemeTokens.Text2,
			StrokeThickness = 1.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		});
		TranslateTransform t = (TranslateTransform)(grid2.RenderTransform = new TranslateTransform(-50.0, 0.0));
		Move(t, TranslateTransform.XProperty, 0.0, 600.0);
		cardContent.Children.Add(grid2);
		plane = grid2;
	}

	private void RemovePlane()
	{
		if (plane != null)
		{
			if (plane.Parent is System.Windows.Controls.Panel panel)
			{
				panel.Children.Remove(plane);
			}
			plane = null;
		}
	}

	private void Stop()
	{
		generation++;
		starting = false;
		live = false;
		Process process = host;
		host = null;
		StopTimer(clock);
		qrImage = null;
		RemovePlane();
		if (process == null)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				process.StandardInput.WriteLine("stop");
				process.StandardInput.Close();
				if (!process.WaitForExit(3000))
				{
					process.Kill();
				}
			}
			catch
			{
				try
				{
					if (!process.HasExited)
					{
						process.Kill();
					}
				}
				catch
				{
				}
			}
			finally
			{
				process.Dispose();
			}
		});
	}

	private string Summary()
	{
		if (pendingText != null)
		{
			return "Text";
		}
		string[] array = pendingFiles ?? new string[0];
		return Count(array.Length) + " · " + FormatSize(array.Sum((string f) => SizeOf(f)));
	}

	private static string Count(int n)
	{
		if (n != 1)
		{
			return n + " files";
		}
		return "1 file";
	}

	private static long SizeOf(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				return new FileInfo(path).Length;
			}
			if (Directory.Exists(path))
			{
				return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Take(10000).Sum((FileInfo f) => f.Length);
			}
		}
		catch
		{
		}
		return 0L;
	}

	internal static string FormatSize(long bytes)
	{
		if (bytes < 1024)
		{
			return bytes + " B";
		}
		string[] array = new string[4] { "KB", "MB", "GB", "TB" };
		double num = bytes;
		int num2 = -1;
		do
		{
			num /= 1024.0;
			num2++;
		}
		while (num >= 1024.0 && num2 < array.Length - 1);
		return ((num >= 100.0) ? num.ToString("0") : num.ToString("0.0")) + " " + array[num2];
	}
}
