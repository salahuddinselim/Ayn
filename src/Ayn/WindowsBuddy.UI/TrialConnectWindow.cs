using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.UI;

public sealed class AynConnectWindow : Window
{
	public const string Headline = "Connect to the internet to start your 3-day Ayn";

	public const string ActivateHeadline = "Activate Ayn";

	private static AynConnectWindow _current;

	private readonly EventHandler _licenseChanged;

	private readonly DispatcherTimer _poll;

	private readonly StackPanel _body = new StackPanel();

	private AccessState? _shown;

	private bool _keyEntry;

	private bool _busy;

	private TextBox _keyBox;

	private TextBlock _message;

	internal static int ActivationCeilingMs = 20000;

	public static bool IsOpen => _current != null;

	internal static AynConnectWindow Current => _current;

	internal AccessState? ShownState => _shown;

	internal TextBox KeyBoxForQa => _keyBox;

	internal string MessageText
	{
		get
		{
			if (_message == null)
			{
				return null;
			}
			return _message.Text;
		}
	}

	public static void ShowOrActivate()
	{
		ShowOrActivate(keyEntry: false);
	}

	public static void ShowOrActivate(bool keyEntry)
	{
		try
		{
			if (_current == null)
			{
				_current = new AynConnectWindow();
				_current.Show();
			}
			if (!_current.IsVisible)
			{
				_current.Show();
			}
			if (_current.WindowState == WindowState.Minimized)
			{
				_current.WindowState = WindowState.Normal;
			}
			if (keyEntry)
			{
				_current.ShowKeyEntry();
			}
			_current.Activate();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Could not open the activation window: " + ex.GetType().Name);
			try
			{
				if (_current != null && !_current.IsVisible)
				{
					_current = null;
				}
			}
			catch
			{
				_current = null;
			}
		}
	}

	private AynConnectWindow()
	{
		base.Title = "Ayn";
		base.Width = 440.0;
		base.SizeToContent = SizeToContent.Height;
		base.ResizeMode = ResizeMode.NoResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.Topmost = true;
		base.ShowInTaskbar = true;
		base.Content = new Border
		{
			Background = BuddyTheme.CardFill,
			BorderBrush = BuddyTheme.Hairline,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(18.0),
			Padding = new Thickness(26.0, 24.0, 26.0, 22.0),
			Child = _body
		};
		MouseButtonEventHandler value = delegate(object s, MouseButtonEventArgs e)
		{
			if (e.Handled || e.OriginalSource is TextBox)
			{
				return;
			}
			try
			{
				DragMove();
			}
			catch
			{
			}
		};
		base.MouseLeftButtonDown += value;
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				Close();
			}
		};
		_licenseChanged = delegate
		{
			base.Dispatcher.BeginInvoke(new Action(Refresh));
		};
		LicenseStub.Changed += _licenseChanged;
		_poll = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_poll.Tick += delegate
		{
			Refresh();
		};
		_poll.Start();
		base.Closed += delegate
		{
			_poll.Stop();
			try
			{
				LicenseStub.Changed -= _licenseChanged;
			}
			catch
			{
			}
			if (object.ReferenceEquals(_current, this))
			{
				_current = null;
			}
		};
		_shown = SafeAccess();
		Build(_shown.Value);
	}

	private static AccessState SafeAccess()
	{
		try
		{
			AccessState access = LicenseStub.Access;
			bool flag;
			try
			{
				flag = EntitlementGate.Probe();
			}
			catch
			{
				flag = false;
			}
			if (flag)
			{
				return (access != 0) ? AccessState.Ayn : AccessState.Ayn;
			}
			return (access == AccessState.Ayn || access == AccessState.Ayn) ? AccessState.NeedsActivation : access;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("License state unavailable (" + ex.GetType().Name + ").");
			return AccessState.NeedsActivation;
		}
	}

	internal void Refresh()
	{
		AccessState accessState = SafeAccess();
		if (accessState == AccessState.Ayn || accessState == AccessState.Ayn)
		{
			Close();
		}
		else if (_shown != accessState && !_busy)
		{
			_shown = accessState;
			Build(accessState);
		}
	}

	private void ShowKeyEntry()
	{
		_keyEntry = true;
		if (_shown.HasValue)
		{
			Build(_shown.Value);
		}
		if (_keyBox != null)
		{
			_keyBox.Dispatcher.BeginInvoke((Action)delegate
			{
				_keyBox.Focus();
				Keyboard.Focus(_keyBox);
			}, DispatcherPriority.Input);
		}
	}

	private void Build(AccessState state)
	{
		_body.Children.Clear();
		bool flag = state == AccessState.Offline;
		bool flag2 = state == AccessState.Checking;
		bool flag3 = !flag && !flag2;
		string text = (flag ? "Connect to the internet to start your 3-day Ayn" : (flag2 ? "Getting Ayn ready" : "Activate Ayn"));
		string text2 = (flag ? "Ayn starts your trial with a quick check online. It keeps trying quietly and begins on its own as soon as you are connected." : (flag2 ? "Checking your Ayn with Ayn's server. This takes a few seconds." : ((SafeAynStarted() || !LicenseStub.AwaitingFirstServerContact) ? "The Ayn on this PC has been used. Enter your license key to keep using Ayn — your notes, clipboard history and files are safe." : "Enter your license key to use Ayn on this PC.")));
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
		StackPanel stackPanel2 = stackPanel;
		AynRobotMark aynRobotMark = new AynRobotMark(34.0);
		aynRobotMark.VerticalAlignment = VerticalAlignment.Center;
		aynRobotMark.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		AynRobotMark aynRobotMark2 = aynRobotMark;
		stackPanel2.Children.Add(new Border
		{
			Width = 44.0,
			Height = 44.0,
			CornerRadius = new CornerRadius(12.0),
			Background = Brushes.Black,
			Child = aynRobotMark2,
			Margin = new Thickness(0.0, 0.0, 14.0, 0.0)
		});
		aynRobotMark2.Margin = new Thickness(0.0);
		aynRobotMark2.HorizontalAlignment = HorizontalAlignment.Center;
		TextBlock textBlock = BuddyTheme.Text(text, 17.0, BuddyTheme.Text100, FontWeights.SemiBold);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.MaxWidth = 320.0;
		stackPanel2.Children.Add(textBlock);
		_body.Children.Add(stackPanel2);
		TextBlock textBlock2 = BuddyTheme.Text(text2, 13.0, BuddyTheme.Text50, FontWeights.Normal);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
		_body.Children.Add(textBlock2);
		if (flag || flag2)
		{
			TextBlock textBlock3 = BuddyTheme.Text(flag ? "Waiting for a connection…" : "Checking…", 12.0, BuddyTheme.Text50, FontWeights.Medium);
			textBlock3.Margin = new Thickness(0.0, 0.0, 0.0, 18.0);
			_body.Children.Add(textBlock3);
		}
		if (state == AccessState.ServerUnreachable)
		{
			TextBlock textBlock4 = BuddyTheme.Text("Ayn couldn't reach its server just now. It keeps trying; you can activate a key meanwhile.", 12.0, BuddyTheme.Text50, FontWeights.Medium);
			textBlock4.TextWrapping = TextWrapping.Wrap;
			textBlock4.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
			_body.Children.Add(textBlock4);
		}
		if (flag3 || _keyEntry)
		{
			_body.Children.Add(KeyEntry());
		}
		WrapPanel wrapPanel = new WrapPanel();
		wrapPanel.Margin = new Thickness(0.0, (flag3 || _keyEntry) ? 14 : 0, 0.0, 0.0);
		WrapPanel wrapPanel2 = wrapPanel;
		if (!flag3 && !_keyEntry)
		{
			wrapPanel2.Children.Add(MakeButton("Enter license key", primary: false, ShowKeyEntry));
		}
		if (state == AccessState.ServerUnreachable || flag)
		{
			wrapPanel2.Children.Add(MakeButton("Retry", primary: false, delegate
			{
				try
				{
					LicenseStub.CheckAynNow();
				}
				catch
				{
				}
				_shown = null;
			}));
		}
		if (flag3)
		{
			wrapPanel2.Children.Add(MakeButton("Ayn", primary: false, delegate
			{
				AppLinks.Open("https://github.com/salahuddinselim/Ayn");
			}));
		}
		wrapPanel2.Children.Add(MakeButton("Restore purchase / Help", primary: false, delegate
		{
			AppLinks.Open("mailto:contact@modistudio.online?subject=Ayn%20license");
		}));
		Border border = MakeButton("Close", primary: false, base.Close);
		border.VerticalAlignment = VerticalAlignment.Top;
		border.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		DockPanel dockPanel = new DockPanel();
		dockPanel.LastChildFill = false;
		DockPanel dockPanel2 = dockPanel;
		DockPanel.SetDock(border, Dock.Right);
		dockPanel2.Children.Add(border);
		dockPanel2.Children.Add(wrapPanel2);
		_body.Children.Add(dockPanel2);
	}

	private static bool SafeAynStarted()
	{
		try
		{
			return LicenseStub.AynStarted;
		}
		catch
		{
			return false;
		}
	}

	private FrameworkElement KeyEntry()
	{
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = BuddyTheme.Text("License key", 12.0, BuddyTheme.Text50, FontWeights.Medium);
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		stackPanel.Children.Add(textBlock);
		DockPanel dockPanel = new DockPanel();
		string text = ((_keyBox != null) ? _keyBox.Text : "");
		_keyBox = new TextBox
		{
			Text = text,
			FontSize = 14.0,
			Padding = new Thickness(10.0, 8.0, 10.0, 8.0),
			MinHeight = 38.0,
			Background = BuddyTheme.Control,
			Foreground = BuddyTheme.Text100,
			BorderBrush = BuddyTheme.Hairline,
			BorderThickness = new Thickness(1.0),
			CaretBrush = BuddyTheme.Text100,
			VerticalContentAlignment = VerticalAlignment.Center,
			FontFamily = new FontFamily("Consolas, Segoe UI")
		};
		AutomationProperties.SetName(_keyBox, "License key");
		_keyBox.KeyDown += async delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				await ActivateAsync();
			}
		};
		Border border = MakeButton("Activate", primary: true, async delegate
		{
			await ActivateAsync();
		});
		border.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		DockPanel.SetDock(border, Dock.Right);
		dockPanel.Children.Add(border);
		dockPanel.Children.Add(_keyBox);
		stackPanel.Children.Add(dockPanel);
		_message = BuddyTheme.Text("", 12.0, BuddyTheme.Text50, FontWeights.Medium);
		_message.TextWrapping = TextWrapping.Wrap;
		_message.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		stackPanel.Children.Add(_message);
		if (base.IsLoaded)
		{
			_keyBox.Dispatcher.BeginInvoke((Action)delegate
			{
				_keyBox.Focus();
			}, DispatcherPriority.Input);
		}
		else
		{
			base.Loaded += delegate
			{
				_keyBox.Focus();
			};
		}
		return stackPanel;
	}

	internal async Task ActivateAsync()
	{
		if (_busy || _keyBox == null)
		{
			return;
		}
		string key = LicenseStub.CleanKey(_keyBox.Text);
		if (key == null)
		{
			Say("Paste the license key from your purchase email.", error: true);
			return;
		}
		_busy = true;
		_keyBox.IsEnabled = false;
		Say("Checking your key…", error: false);
		ActivationResult result;
		try
		{
			Task<ActivationResult> pending = LicenseStub.ActivateAsync(key);
			result = ((await Task.WhenAny(pending, Task.Delay(ActivationCeilingMs)) == pending) ? (await pending) : new ActivationResult
			{
				Success = false,
				Status = ActivationStatus.NetworkError,
				Message = "The license server took too long to answer. Check your connection and try again."
			});
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Activation from the activation window failed (" + ex.GetType().Name + ").");
			result = new ActivationResult
			{
				Success = false,
				Status = ActivationStatus.NetworkError,
				Message = "Couldn't reach the license server. Check your connection and try again."
			};
		}
		_busy = false;
		if (base.IsLoaded)
		{
			_keyBox.IsEnabled = true;
			if (result.Success)
			{
				Say("Ayn is activated. Enjoy!", error: false);
				Refresh();
			}
			else
			{
				Say(PlainMessage(result), error: true);
				_keyBox.Focus();
				_keyBox.SelectAll();
			}
		}
	}

	internal static string PlainMessage(ActivationResult r)
	{
		switch (r.Status)
		{
		case ActivationStatus.InvalidKey:
			if (!(r.Message == "Please enter a license key."))
			{
				return "That key wasn't recognised. Check that you pasted the whole key from your purchase email.";
			}
			return "Paste the license key from your purchase email.";
		case ActivationStatus.DeviceLimitReached:
			return "Activation limit reached: this key is already active on another PC. Deactivate it there (Settings > License), or email contact@modistudio.online.";
		case ActivationStatus.NetworkError:
			if (!string.IsNullOrEmpty(r.Message))
			{
				return r.Message;
			}
			return "Network error: couldn't reach the license server. Check your connection and try again.";
		default:
			if (!string.IsNullOrEmpty(r.Message))
			{
				return r.Message;
			}
			return "Activation didn't complete. Please try again.";
		}
	}

	private void Say(string text, bool error)
	{
		if (_message != null)
		{
			_message.Text = text;
			_message.Foreground = (error ? new SolidColorBrush(Color.FromRgb(byte.MaxValue, 107, 97)) : BuddyTheme.Text50);
		}
	}

	private Border MakeButton(string label, bool primary, Action click)
	{
		TextBlock textBlock = BuddyTheme.Text(label, 13.0, primary ? BuddyTheme.PrimaryInk : BuddyTheme.Text100, FontWeights.SemiBold);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		Border border = new Border();
		border.Background = (primary ? BuddyTheme.PrimaryFill : BuddyTheme.Control);
		border.CornerRadius = new CornerRadius(10.0);
		border.Padding = new Thickness(14.0, 8.0, 14.0, 8.0);
		border.Margin = new Thickness(0.0, 0.0, 8.0, 6.0);
		border.Cursor = Cursors.Hand;
		border.Focusable = true;
		border.Child = textBlock;
		Border border2 = border;
		AutomationProperties.SetName(border2, label);
		bool pressed = false;
		border2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			pressed = true;
			e.Handled = true;
		};
		border2.MouseLeave += delegate
		{
			pressed = false;
		};
		border2.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (pressed)
			{
				pressed = false;
				e.Handled = true;
				Run(click);
			}
		};
		border2.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Return || e.Key == Key.Space)
			{
				e.Handled = true;
				Run(click);
			}
		};
		return border2;
	}

	private static void Run(Action click)
	{
		try
		{
			click();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Activation window action failed (" + ex.GetType().Name + ").");
		}
	}
}
