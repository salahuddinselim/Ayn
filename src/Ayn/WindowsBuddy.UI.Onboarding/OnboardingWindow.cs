using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.State;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class OnboardingWindow : Window
{
	private sealed class Page
	{
		public UIElement View;

		public Action Show;

		public Action Hide;
	}

	private const int StepCount = 4;

	private const double HeaderTop = 40.0;

	private const double HeaderHeight = 138.0;

	private readonly Action _onFinished;

	private CapsuleWindow _notch;

	private readonly Func<CapsuleWindow> _startNotch;

	private readonly Action _onDeclined;

	private readonly bool _replay;

	private readonly Grid _stepHost = new Grid();

	private readonly StackPanel _dots = new StackPanel
	{
		Orientation = Orientation.Horizontal,
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly SButton _back;

	private readonly SButton _primary;

	private readonly SButton _skip;

	private readonly SButton _decline;

	private readonly SClickable _close;

	private bool _introPlayed;

	private StackPanel _barActions;

	private Page _page;

	private int _step = -1;

	private bool _finished;

	private bool _skipAsked;

	private bool _launchAtLogin;

	private bool _agreementPending;

	private bool _declined;

	private SCheckBox _agreeBox;

	private DispatcherTimer _autoAdvance;

	private Action _settle;

	public int Step => _step;

	public bool IsReplay => _replay;

	public bool IsAgreementShowing => _agreementPending;

	public bool Declined => _declined;

	internal SCheckBox AgreementBox
	{
		get
		{
			if (!_agreementPending)
			{
				return null;
			}
			return _agreeBox;
		}
	}

	internal CapsuleWindow Notch => _notch;

	internal UIElement PageView
	{
		get
		{
			if (_page == null)
			{
				return null;
			}
			return _page.View;
		}
	}

	private static bool Animate
	{
		get
		{
			if (CapsuleMotion.AnimationsEnabled)
			{
				return !PowerSaver.IsBatterySaverOn;
			}
			return false;
		}
	}

	public OnboardingWindow(Action onFinished, CapsuleWindow notch, bool replay = false)
		: this(onFinished, notch, null, null, agreementFirst: false, replay)
	{
	}

	public OnboardingWindow(Action onFinished, Func<CapsuleWindow> startNotch, Action onDeclined)
		: this(onFinished, null, startNotch, onDeclined, agreementFirst: true, replay: false)
	{
	}

	private OnboardingWindow(Action onFinished, CapsuleWindow notch, Func<CapsuleWindow> startNotch, Action onDeclined, bool agreementFirst, bool replay)
	{
		_onFinished = onFinished;
		_notch = notch;
		_startNotch = startNotch;
		_onDeclined = onDeclined;
		_replay = replay;
		_agreementPending = agreementFirst && !replay;
		base.Title = "Ayn";
		base.Width = 760.0;
		base.Height = 540.0;
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ResizeMode = ResizeMode.NoResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		base.Foreground = SettingsTheme.TextPrimary;
		base.FontFamily = SettingsTheme.Font;
		base.FontSize = 15.0;
		base.ShowInTaskbar = true;
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		AynType.PrepareRoot(this, TypeSurface.Settings);
		try
		{
			base.Icon = AppIcon();
		}
		catch
		{
		}
		Border shell = new Border
		{
			CornerRadius = new CornerRadius(14.0),
			Background = ThemeTokens.Bg,
			BorderBrush = ThemeTokens.BorderStrong,
			BorderThickness = new Thickness(1.0)
		};
		Grid grid = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = new GridLength(1.0, GridUnitType.Star)
				},
				new RowDefinition
				{
					Height = new GridLength(72.0)
				}
			},
			Children = { (UIElement)_stepHost }
		};
		Grid grid2 = new Grid
		{
			Margin = new Thickness(32.0, 0.0, 32.0, 0.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_dots.HorizontalAlignment = HorizontalAlignment.Left;
		grid2.Children.Add(_dots);
		StackPanel stackPanel = (_barActions = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		});
		_decline = new SButton("Decline and exit", SButtonKind.Ghost)
		{
			Height = 36.0,
			Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		AutomationProperties.SetHelpText(_decline, "Closes Ayn without changing anything");
		_decline.Click += delegate
		{
			Decline();
		};
		stackPanel.Children.Add(_decline);
		_skip = new SButton("Skip", SButtonKind.Ghost)
		{
			Height = 36.0,
			Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		_skip.Click += delegate
		{
			RequestSkip();
		};
		stackPanel.Children.Add(_skip);
		_back = new SButton("Back", SButtonKind.Ghost)
		{
			Height = 36.0,
			Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		_back.Click += delegate
		{
			Go(_step - 1);
		};
		stackPanel.Children.Add(_back);
		_primary = new SButton("Continue", SButtonKind.Primary)
		{
			Height = 36.0,
			Padding = new Thickness(24.0, 0.0, 24.0, 0.0),
			MinWidth = 112.0,
			CornerRadius = new CornerRadius(18.0)
		};
		((FrameworkElement)_primary.Child).HorizontalAlignment = HorizontalAlignment.Center;
		_primary.Click += delegate
		{
			OnPrimary();
		};
		stackPanel.Children.Add(_primary);
		Grid.SetColumn(stackPanel, 1);
		grid2.Children.Add(stackPanel);
		Grid.SetRow(grid2, 1);
		grid.Children.Add(grid2);
		SClickable close = (_close = new SClickable(8.0, "Skip setup")
		{
			Width = 32.0,
			Height = 32.0,
			Background = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 14.0, 14.0, 0.0),
			ToolTip = "Skip setup"
		});
		FrameworkElement frameworkElement = Glyphs.Create("close", 12.0, SettingsTheme.TextSecondary);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		close.Child = frameworkElement;
		close.MouseEnter += delegate
		{
			close.Background = ThemeTokens.AccentSoft;
		};
		close.MouseLeave += delegate
		{
			close.Background = Brushes.Transparent;
		};
		close.Click += delegate
		{
			RequestSkip();
		};
		Grid.SetRowSpan(close, 2);
		grid.Children.Add(close);
		shell.Child = grid;
		base.Content = shell;
		AgreementPanel.FitWindow(this, shell, 760.0, 540.0);
		shell.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (e.GetPosition(shell).Y < 64.0 && e.ButtonState == MouseButtonState.Pressed)
			{
				try
				{
					DragMove();
				}
				catch
				{
				}
			}
		};
		base.PreviewKeyDown += OnKey;
		base.Closing += delegate(object s, CancelEventArgs e)
		{
			if (!_finished)
			{
				if (_agreementPending)
				{
					MarkDeclined("window closed");
				}
				else
				{
					e.Cancel = true;
					RequestSkip();
				}
			}
		};
		base.Closed += delegate
		{
			if (SettingsLauncher.Redirect == new Action(BringToFront))
			{
				SettingsLauncher.Redirect = null;
			}
			if (_autoAdvance != null)
			{
				_autoAdvance.Stop();
			}
			if (_declined && _onDeclined != null)
			{
				try
				{
					_onDeclined();
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Exit after declining the agreement failed: " + ex.Message);
				}
			}
		};
		SettingsLauncher.Redirect = BringToFront;
		if (_agreementPending)
		{
			ShowAgreement();
		}
		else
		{
			Go(0);
		}
	}

	public static void ShowReplay()
	{
		try
		{
			OnboardingWindow onboardingWindow = new OnboardingWindow(null, CapsuleWindow.Instance, replay: true);
			onboardingWindow.Show();
			onboardingWindow.Activate();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Welcome tour could not open: " + ex.Message);
		}
	}

	public void Go(int step)
	{
		if (_agreementPending || step < 0 || step >= 4 || step == _step)
		{
			return;
		}
		int num = ((step > _step) ? 1 : (-1));
		if (_autoAdvance != null)
		{
			_autoAdvance.Stop();
			_autoAdvance = null;
		}
		Page page = _page;
		UIElement uIElement = page?.View;
		if (page != null && page.Hide != null)
		{
			page.Hide();
		}
		_step = step;
		_page = step switch
		{
			2 => LookPage(), 
			1 => TryPage(), 
			0 => WelcomePage(), 
			_ => ReadyPage(), 
		};
		UIElement view = _page.View;
		_stepHost.Children.Add(view);
		if (Animate && uIElement != null)
		{
			TranslateTransform translateTransform = (TranslateTransform)(view.RenderTransform = new TranslateTransform(24 * num, 0.0));
			view.Opacity = 0.0;
			CubicEase cubicEase = new CubicEase();
			cubicEase.EasingMode = EasingMode.EaseOut;
			CubicEase easingFunction = cubicEase;
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(220.0);
			translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(24 * num, 0.0, timeSpan)
			{
				EasingFunction = easingFunction
			});
			view.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, timeSpan)
			{
				EasingFunction = easingFunction
			});
			uIElement.IsHitTestVisible = false;
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(120.0));
			UIElement keep = uIElement;
			doubleAnimation.Completed += delegate
			{
				_stepHost.Children.Remove(keep);
			};
			uIElement.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
		else if (uIElement != null)
		{
			_stepHost.Children.Remove(uIElement);
		}
		_back.Visibility = ((step <= 0) ? Visibility.Collapsed : Visibility.Visible);
		_skip.Visibility = ((step != 0) ? Visibility.Collapsed : Visibility.Visible);
		_primary.Text = step switch
		{
			3 => "Start using Ayn", 
			0 => "Get started", 
			_ => "Continue", 
		};
		RenderDots();
		if (_page.Show != null)
		{
			_page.Show();
		}
		base.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			_primary.Focus();
		});
	}

	private void OnPrimary()
	{
		if (_agreementPending)
		{
			AcceptAgreement();
		}
		else if (_step < 3)
		{
			Go(_step + 1);
		}
		else
		{
			Finish();
		}
	}

	private void OnKey(object sender, KeyEventArgs e)
	{
		if (_agreementPending)
		{
			if (e.Key == Key.Escape)
			{
				Decline();
				e.Handled = true;
			}
			else if (e.Key == Key.Return || e.Key == Key.Return)
			{
				IInputElement focusedElement = Keyboard.FocusedElement;
				if (!(focusedElement is SLink) && focusedElement != _decline && focusedElement != _close)
				{
					AcceptAgreement();
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Left || e.Key == Key.Right)
			{
				e.Handled = true;
			}
			return;
		}
		DependencyObject dependencyObject = Keyboard.FocusedElement as DependencyObject;
		bool flag = false;
		for (DependencyObject dependencyObject2 = dependencyObject; dependencyObject2 != null; dependencyObject2 = VisualTreeHelper.GetParent(dependencyObject2) ?? LogicalTreeHelper.GetParent(dependencyObject2))
		{
			if (dependencyObject2 is ChoiceCards || dependencyObject2 is STextBox)
			{
				flag = true;
				break;
			}
		}
		if (e.Key == Key.Escape)
		{
			RequestSkip();
			e.Handled = true;
		}
		else if (e.Key == Key.Return || e.Key == Key.Return)
		{
			if (!(dependencyObject is SButton sButton) || sButton == _primary)
			{
				OnPrimary();
				e.Handled = true;
			}
		}
		else if (e.Key == Key.Right && !flag)
		{
			if (_step < 3)
			{
				Go(_step + 1);
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Left && !flag)
		{
			if (_step > 0)
			{
				Go(_step - 1);
			}
			e.Handled = true;
		}
	}

	private void RenderDots()
	{
		_dots.Children.Clear();
		for (int i = 0; i < 4; i++)
		{
			bool flag = i == _step;
			_dots.Children.Add(new Border
			{
				Width = (flag ? 18 : 6),
				Height = 6.0,
				CornerRadius = new CornerRadius(3.0),
				Background = (flag ? ThemeTokens.Text : ThemeTokens.Rule),
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
			});
		}
		AutomationProperties.SetName(_dots, $"Step {_step + 1} of {4}");
	}

	public void RequestSkip()
	{
		if (_finished)
		{
			return;
		}
		if (_agreementPending)
		{
			Decline();
			return;
		}
		if (!_skipAsked)
		{
			if (!SUI.Confirm(this, "Skip setup?", "Ayn keeps its default look. Change it any time in Settings", "Skip", danger: false))
			{
				return;
			}
			_skipAsked = true;
		}
		Finish();
	}

	public void Finish()
	{
		if (_finished)
		{
			return;
		}
		if (_agreementPending)
		{
			TransactionLogger.Warn("Welcome tour: finish ignored, the agreement is not accepted.");
			return;
		}
		_finished = true;
		if (_page != null && _page.Hide != null)
		{
			_page.Hide();
		}
		if (!_replay)
		{
			if (LicenseStub.Status != LicenseStatus.Active)
			{
				LicenseStub.StartAyn();
			}
			ApplyLaunchAtLogin(_launchAtLogin);
			FirstRunManager.MarkCompleted();
		}
		if (SettingsLauncher.Redirect == new Action(BringToFront))
		{
			SettingsLauncher.Redirect = null;
		}
		TransactionLogger.Info("Onboarding " + (_replay ? "replay " : "") + "finished (theme " + NotchConfig.Instance.AppTheme + ", edge light " + NotchConfig.Instance.EdgeLightEnabled + ", launch at login " + _launchAtLogin + ", " + LicenseStub.StatusLine + ").");
		if (_onFinished != null && !_replay)
		{
			try
			{
				_onFinished();
			}
			catch (Exception ex)
			{
				TransactionLogger.Error("Starting Ayn after onboarding failed: " + ex.Message, ex);
			}
		}
		if (!_replay)
		{
			WelcomeOpen(_notch);
		}
		if (Animate)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(140.0));
			doubleAnimation.Completed += delegate
			{
				Close();
			};
			BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
		else
		{
			Close();
		}
	}

	private static void WelcomeOpen(CapsuleWindow notch)
	{
		if (notch == null)
		{
			return;
		}
		try
		{
			notch.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				notch.RequestExpand("Welcome");
				DispatcherTimer t = new DispatcherTimer
				{
					Interval = TimeSpan.FromSeconds(3.2)
				};
				t.Tick += delegate
				{
					t.Stop();
					notch.RequestCollapse("Welcome");
				};
				t.Start();
			});
		}
		catch
		{
		}
	}

	private static void ApplyLaunchAtLogin(bool on)
	{
		if (AppPaths.IsSandboxed)
		{
			TransactionLogger.Info("QA sandbox: launch at login (" + on + ") not written to Windows.");
			return;
		}
		try
		{
			bool flag = StartupService.IsAutoStartEnabled();
			if (on && !flag)
			{
				StartupService.EnableAutoStart();
			}
			else if (!on && flag)
			{
				StartupService.DisableAutoStart();
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Launch at login: " + ex.Message);
		}
	}

	private static UIElement Headline(string text, double size, double tracking)
	{
		TrackedText trackedText = new TrackedText(text, size, tracking);
		trackedText.HorizontalAlignment = HorizontalAlignment.Center;
		return trackedText;
	}

	private static Grid Frame(string title, string subtitle, UIElement content, UIElement under = null)
	{
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(138.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 40.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(Headline(title, 30.0, -0.02));
		stackPanel2.Children.Add(Centered(SettingsTheme.Text(subtitle, 15.0, SettingsTheme.TextSecondary, FontWeights.Normal), 8.0));
		Grid grid2 = new Grid();
		grid2.Height = 32.0;
		grid2.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		Grid grid3 = grid2;
		if (under != null)
		{
			grid3.Children.Add(under);
		}
		stackPanel2.Children.Add(grid3);
		grid.Children.Add(stackPanel2);
		if (content is FrameworkElement frameworkElement)
		{
			frameworkElement.VerticalAlignment = VerticalAlignment.Top;
			frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
			frameworkElement.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		}
		Grid.SetRow(content, 1);
		grid.Children.Add(content);
		return grid;
	}

	private static void Reveal(UIElement el, double delayMs, double ms, double rise = 0.0, double fromScale = 1.0)
	{
		ScaleTransform scaleTransform = new ScaleTransform(fromScale, fromScale);
		TranslateTransform translateTransform = new TranslateTransform(0.0, rise);
		TransformGroup transformGroup = new TransformGroup();
		transformGroup.Children.Add(scaleTransform);
		transformGroup.Children.Add(translateTransform);
		el.RenderTransformOrigin = new Point(0.5, 0.5);
		el.RenderTransform = transformGroup;
		el.Opacity = 0.0;
		CubicEase cubicEase = new CubicEase();
		cubicEase.EasingMode = EasingMode.EaseOut;
		CubicEase easingFunction = cubicEase;
		TimeSpan value = TimeSpan.FromMilliseconds(delayMs);
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(ms);
		el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, timeSpan)
		{
			BeginTime = value,
			EasingFunction = easingFunction
		});
		if (rise != 0.0)
		{
			translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(rise, 0.0, timeSpan)
			{
				BeginTime = value,
				EasingFunction = easingFunction
			});
		}
		if (fromScale != 1.0)
		{
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromScale, 1.0, timeSpan)
			{
				BeginTime = value,
				EasingFunction = easingFunction
			});
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromScale, 1.0, timeSpan)
			{
				BeginTime = value,
				EasingFunction = easingFunction
			});
		}
	}

	private void RevealBar(double delayMs)
	{
		UIElement[] array = new UIElement[2] { _barActions, _dots };
		foreach (UIElement uIElement in array)
		{
			uIElement.Opacity = 0.0;
			uIElement.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320.0))
			{
				BeginTime = TimeSpan.FromMilliseconds(delayMs),
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
		}
	}

	private void SettleBar()
	{
		UIElement[] array = new UIElement[2] { _barActions, _dots };
		foreach (UIElement uIElement in array)
		{
			uIElement.BeginAnimation(UIElement.OpacityProperty, null);
			uIElement.Opacity = 1.0;
		}
	}

	private static void Final(UIElement el)
	{
		el.BeginAnimation(UIElement.OpacityProperty, null);
		el.Opacity = 1.0;
		if (!(el.RenderTransform is TransformGroup transformGroup))
		{
			return;
		}
		foreach (Transform child in transformGroup.Children)
		{
			ScaleTransform scaleTransform = child as ScaleTransform;
			TranslateTransform translateTransform = child as TranslateTransform;
			if (scaleTransform != null)
			{
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				double scaleX = (scaleTransform.ScaleY = 1.0);
				scaleTransform.ScaleX = scaleX;
			}
			if (translateTransform != null)
			{
				translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
				translateTransform.Y = 0.0;
			}
		}
	}

	public void SettleIntro()
	{
		if (_settle != null)
		{
			_settle();
		}
	}

	private void ShowAgreement()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement frameworkElement = BrandAssets.CreateLogo(48.0);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		AutomationProperties.SetName(frameworkElement, "Ayn");
		stackPanel2.Children.Add(frameworkElement);
		UIElement uIElement = Headline("Before you start", 30.0, -0.02);
		((FrameworkElement)uIElement).Margin = new Thickness(0.0, 18.0, 0.0, 0.0);
		AutomationProperties.SetName(uIElement, "Before you start");
		stackPanel2.Children.Add(uIElement);
		FrameworkElement frameworkElement2 = Centered(SettingsTheme.Text("To use Ayn, please read and accept its End User License Agreement.", 15.0, SettingsTheme.TextSecondary, FontWeights.Normal), 8.0);
		frameworkElement2.MaxWidth = 520.0;
		stackPanel2.Children.Add(frameworkElement2);
		SCheckBox box;
		StackPanel stackPanel3 = AgreementPanel.Content(() => this, 520.0, out box);
		stackPanel3.Margin = new Thickness(0.0, 28.0, 0.0, 0.0);
		stackPanel2.Children.Add(stackPanel3);
		_agreeBox = box;
		box.Changed += delegate(bool v)
		{
			if (_agreementPending)
			{
				_primary.IsEnabled = v;
			}
		};
		_page = new Page
		{
			View = stackPanel2
		};
		_stepHost.Children.Add(stackPanel2);
		_dots.Visibility = Visibility.Hidden;
		AutomationProperties.SetName(_dots, "");
		_skip.Visibility = Visibility.Collapsed;
		_back.Visibility = Visibility.Collapsed;
		_decline.Visibility = Visibility.Visible;
		_primary.Text = "Accept and continue";
		_primary.IsEnabled = false;
		AutomationProperties.SetHelpText(_primary, "Available after you tick the agreement box");
		_close.ToolTip = "Decline and exit";
		AutomationProperties.SetName(_close, "Close (decline and exit)");
		base.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			if (_agreeBox != null)
			{
				_agreeBox.Focus();
			}
		});
	}

	public void AcceptAgreement()
	{
		if (!_agreementPending || _agreeBox == null || !_agreeBox.IsChecked)
		{
			return;
		}
		EulaAgreement.RecordAcceptance();
		_agreementPending = false;
		if (_notch == null && _startNotch != null)
		{
			try
			{
				_notch = _startNotch();
			}
			catch (Exception ex)
			{
				TransactionLogger.Error("Starting the notch after the agreement failed: " + ex.Message, ex);
			}
		}
		_decline.Visibility = Visibility.Collapsed;
		_dots.Visibility = Visibility.Visible;
		_primary.IsEnabled = true;
		AutomationProperties.SetHelpText(_primary, "");
		_close.ToolTip = "Skip setup";
		AutomationProperties.SetName(_close, "Skip setup");
		Go(0);
		try
		{
			Activate();
		}
		catch
		{
		}
	}

	public void Decline()
	{
		if (!_finished && _agreementPending)
		{
			MarkDeclined("Decline and exit");
			Close();
		}
	}

	private void MarkDeclined(string how)
	{
		_finished = true;
		_declined = true;
		TransactionLogger.Info("End User License Agreement declined in the welcome tour (" + how + "); Ayn exits without changes.");
	}

	private Page WelcomePage()
	{
		Grid grid = new Grid();
		Grid grid2 = new Grid();
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 40.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		FrameworkElement logo = BrandAssets.CreateLogo(64.0);
		logo.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel2.Children.Add(logo);
		LetterReveal title = new LetterReveal("Welcome to Ayn", 40.0, -0.02)
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		stackPanel2.Children.Add(title);
		FrameworkElement line = Centered(SettingsTheme.Text("The notch, reimagined for Windows.", 15.0, SettingsTheme.TextSecondary, FontWeights.Normal), 10.0);
		stackPanel2.Children.Add(line);
		grid2.Children.Add(stackPanel2);
		LiveNotchTile tile = new LiveNotchTile(_notch)
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		};
		AutomationProperties.SetName(tile, "The live notch");
		Grid.SetRow(tile, 1);
		grid2.Children.Add(tile);
		grid.Children.Add(grid2);
		WelcomeDemo demo = new WelcomeDemo(_notch);
		DispatcherTimer demoStart = null;
		bool firstShow = _step <= 0 && !_introPlayed;
		Action settle = delegate
		{
			title.Settle();
			Final(logo);
			Final(line);
			Final(tile);
			SettleBar();
		};
		_settle = settle;
		Page page = new Page();
		page.View = grid;
		page.Show = delegate
		{
			if (!Animate || RenderProfile.LightMode || !firstShow)
			{
				settle();
				if (Animate && !RenderProfile.LightMode)
				{
					demo.Start();
				}
			}
			else
			{
				_introPlayed = true;
				Reveal(logo, 0.0, 300.0, 6.0);
				title.Clock = -1.0;
				title.Start(220.0);
				Reveal(line, 1150.0, 380.0, 6.0);
				RevealBar(1350.0);
				Reveal(tile, 1550.0, 400.0, 0.0, 0.98);
				demoStart = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(2400.0)
				};
				demoStart.Tick += delegate
				{
					demoStart.Stop();
					demo.Start();
				};
				demoStart.Start();
			}
		};
		page.Hide = delegate
		{
			if (demoStart != null)
			{
				demoStart.Stop();
			}
			demo.Stop();
			title.Settle();
			SettleBar();
			_settle = null;
		};
		return page;
	}

	private Page TryPage()
	{
		string subtitle = (ToolRegistry.Instance.OpensOnClick ? "Click the notch at the top of your screen" : "Move your pointer to the top of your screen");
		SButton skip = new SButton("Skip", SButtonKind.Ghost)
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Height = 30.0,
			Padding = new Thickness(12.0, 0.0, 12.0, 0.0)
		};
		skip.LabelSize = 13.5;
		skip.Click += delegate
		{
			Go(2);
		};
		NotchConfig instance = NotchConfig.Instance;
		double num = instance.GetCollapsedWidth() * 1.15;
		double num2 = instance.GetCollapsedHeight() * 1.15;
		Canvas canvas = new Canvas();
		canvas.Width = 360.0;
		canvas.Height = 200.0;
		canvas.ClipToBounds = true;
		Canvas canvas2 = canvas;
		canvas2.Children.Add(new Border
		{
			Width = 360.0,
			Height = 200.0,
			CornerRadius = new CornerRadius(14.0),
			Background = ThemeTokens.Surface
		});
		Border border = new Border();
		border.Width = 150.0;
		border.Height = 78.0;
		border.CornerRadius = new CornerRadius(8.0);
		border.Background = ThemeTokens.Surface2;
		Border element = border;
		Canvas.SetLeft(element, 36.0);
		Canvas.SetTop(element, 100.0);
		Border border2 = new Border();
		border2.Width = 120.0;
		border2.Height = 60.0;
		border2.CornerRadius = new CornerRadius(8.0);
		border2.Background = ThemeTokens.Surface2;
		Border element2 = border2;
		Canvas.SetLeft(element2, 206.0);
		Canvas.SetTop(element2, 118.0);
		canvas2.Children.Add(element);
		canvas2.Children.Add(element2);
		double length = (360.0 - num) / 2.0;
		PathGeometry data = NotchGeometryBuilder.BuildGeometry(instance.Style, num, num2, instance.CornerRadius);
		System.Windows.Shapes.Path glow = new System.Windows.Shapes.Path
		{
			Data = data,
			Fill = ThemeTokens.Accent,
			Opacity = 0.12,
			IsHitTestVisible = false
		};
		if (!RenderProfile.LightMode)
		{
			glow.Effect = new BlurEffect
			{
				Radius = 16.0,
				RenderingBias = RenderingBias.Performance
			};
		}
		else
		{
			glow.Fill = null;
			glow.Stroke = ThemeTokens.Accent;
			glow.StrokeThickness = 2.0;
		}
		Canvas.SetLeft(glow, length);
		Canvas.SetTop(glow, 0.0);
		canvas2.Children.Add(glow);
		System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
		path.Data = data;
		path.Fill = new SolidColorBrush(ThemeTokens.NotchBlack);
		System.Windows.Shapes.Path element3 = path;
		Canvas.SetLeft(element3, length);
		Canvas.SetTop(element3, 0.0);
		canvas2.Children.Add(element3);
		TranslateTransform arrowMove = new TranslateTransform();
		System.Windows.Shapes.Path path2 = new System.Windows.Shapes.Path();
		path2.Data = Geometry.Parse("M14,34 L14,4 M3,15 L14,4 L25,15");
		path2.Stroke = ThemeTokens.Text;
		path2.StrokeThickness = 2.6;
		path2.StrokeStartLineCap = PenLineCap.Round;
		path2.StrokeEndLineCap = PenLineCap.Round;
		path2.StrokeLineJoin = PenLineJoin.Round;
		path2.RenderTransform = arrowMove;
		System.Windows.Shapes.Path element4 = path2;
		Canvas.SetLeft(element4, 166.0);
		Canvas.SetTop(element4, num2 + 14.0);
		canvas2.Children.Add(element4);
		Border border3 = new Border();
		border3.Width = 360.0;
		border3.Height = 200.0;
		border3.CornerRadius = new CornerRadius(14.0);
		border3.BorderBrush = ThemeTokens.BorderStrong;
		border3.BorderThickness = new Thickness(1.0);
		border3.IsHitTestVisible = false;
		Border element5 = border3;
		canvas2.Children.Add(element5);
		AutomationProperties.SetName(canvas2, "A screen with the notch at its top");
		StackPanel done = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0),
			Opacity = 0.0
		};
		Grid grid = new Grid();
		grid.Width = 26.0;
		grid.Height = 26.0;
		Grid grid2 = grid;
		grid2.Children.Add(new Ellipse
		{
			Fill = ThemeTokens.Success
		});
		grid2.Children.Add(new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse("M7.5,13.5 L11.4,17.2 L18.6,9.2"),
			Stroke = Brushes.White,
			StrokeThickness = 2.3,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round
		});
		done.Children.Add(grid2);
		TextBlock textBlock = SettingsTheme.Text("That's it", 18.0, SettingsTheme.TextPrimary, AynType.Semibold);
		textBlock.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		done.Children.Add(textBlock);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = 360.0;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(canvas2);
		stackPanel2.Children.Add(done);
		Grid view = Frame("Open the notch", subtitle, stackPanel2, skip);
		DispatcherTimer watch = null;
		bool opened = false;
		Page page = new Page();
		page.View = view;
		page.Show = delegate
		{
			if (Animate && !RenderProfile.LightMode)
			{
				SineEase easingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseInOut
				};
				arrowMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, -7.0, TimeSpan.FromMilliseconds(750.0))
				{
					AutoReverse = true,
					RepeatBehavior = RepeatBehavior.Forever,
					EasingFunction = easingFunction
				});
				glow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.1, 0.5, TimeSpan.FromMilliseconds(1300.0))
				{
					AutoReverse = true,
					RepeatBehavior = RepeatBehavior.Forever,
					EasingFunction = easingFunction
				});
			}
			else
			{
				glow.Opacity = 0.35;
			}
			if (_notch != null)
			{
				watch = new DispatcherTimer(DispatcherPriority.Background)
				{
					Interval = TimeSpan.FromMilliseconds(80.0)
				};
				watch.Tick += delegate
				{
					NotchPhase notchPhase = _notch.NotchPhase;
					if (!opened && (notchPhase == NotchPhase.Peeking || notchPhase == NotchPhase.Opening || notchPhase == NotchPhase.Open))
					{
						opened = true;
						watch.Stop();
						arrowMove.BeginAnimation(TranslateTransform.YProperty, null);
						if (Animate)
						{
							done.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220.0)));
						}
						else
						{
							done.Opacity = 1.0;
						}
						skip.Visibility = Visibility.Hidden;
						_autoAdvance = new DispatcherTimer
						{
							Interval = TimeSpan.FromSeconds(1.0)
						};
						_autoAdvance.Tick += delegate
						{
							_autoAdvance.Stop();
							_autoAdvance = null;
							if (_step == 1)
							{
								Go(2);
							}
						};
						_autoAdvance.Start();
					}
				};
				watch.Start();
			}
		};
		page.Hide = delegate
		{
			if (watch != null)
			{
				watch.Stop();
			}
			arrowMove.BeginAnimation(TranslateTransform.YProperty, null);
			glow.BeginAnimation(UIElement.OpacityProperty, null);
		};
		return page;
	}

	private Page LookPage()
	{
		NotchConfig cfg = NotchConfig.Instance;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = 680.0;
		StackPanel stackPanel2 = stackPanel;
		ChoiceCards choiceCards = new ChoiceCards(new ChoiceCards.Option[4]
		{
			new ChoiceCards.Option("PureBlack", "Pure Black", "", SettingsPages.ThemePreview("PureBlack")),
			new ChoiceCards.Option("Vivid", "Vivid", "", SettingsPages.ThemePreview("Vivid")),
			new ChoiceCards.Option("EmberDark", "Ember Dark", "", SettingsPages.ThemePreview("EmberDark")),
			new ChoiceCards.Option("Silver", "Silver", "", SettingsPages.ThemePreview("Silver"))
		}, cfg.AppTheme, "Theme", 4);
		choiceCards.Changed += delegate(string k)
		{
			cfg.AppTheme = k;
		};
		AutomationProperties.SetName(choiceCards, "Theme");
		stackPanel2.Children.Add(choiceCards);
		DispatcherTimer previewTimer = null;
		SButton sButton = new SButton("Preview on notch");
		sButton.HorizontalAlignment = HorizontalAlignment.Right;
		sButton.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		sButton.Height = 28.0;
		SButton sButton2 = sButton;
		sButton2.IsEnabled = _notch != null;
		sButton2.Click += delegate
		{
			if (_notch != null)
			{
				_notch.RequestExpand("Onboarding preview");
				if (previewTimer != null)
				{
					previewTimer.Stop();
				}
				previewTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromSeconds(3.0)
				};
				previewTimer.Tick += delegate
				{
					previewTimer.Stop();
					_notch.RequestCollapse("Onboarding preview");
				};
				previewTimer.Start();
			}
		};
		stackPanel2.Children.Add(sButton2);
		Grid styleRow = null;
		Grid grid = SUI.ToggleRow("Edge Light", "A light that runs along the notch outline", cfg.EdgeLightEnabled, delegate(bool v)
		{
			cfg.EdgeLightEnabled = v;
			if (styleRow != null)
			{
				SUI.SetRowEnabled(styleRow, v);
			}
		});
		string[] array = new string[3] { "Comet", "Twin comets", "Breathe" };
		string selected = ((Array.IndexOf(array, cfg.EdgeLightStyle) >= 0) ? cfg.EdgeLightStyle : "Twin comets");
		SSegmented sSegmented = new SSegmented(array, array, selected, "Edge Light style", 330.0);
		sSegmented.Changed += delegate(string k)
		{
			cfg.EdgeLightStyle = k;
		};
		styleRow = SUI.Row("Style", null, sSegmented);
		SUI.SetRowEnabled(styleRow, cfg.EdgeLightEnabled);
		Border border = SUI.Group(grid, styleRow);
		border.Margin = new Thickness(0.0, -4.0, 0.0, 0.0);
		stackPanel2.Children.Add(border);
		Page page = new Page();
		page.View = Frame("Pick a look", "It applies everywhere, right away", stackPanel2);
		page.Hide = delegate
		{
			if (previewTimer != null)
			{
				previewTimer.Stop();
				if (_notch != null)
				{
					_notch.RequestCollapse("Onboarding preview");
				}
			}
		};
		return page;
	}

	private Page ReadyPage()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = 520.0;
		StackPanel stackPanel2 = stackPanel;
		SToggle sToggle = new SToggle(_launchAtLogin, "Launch at login");
		sToggle.Toggled += delegate(bool v)
		{
			_launchAtLogin = v;
			ApplyLaunchAtLogin(v);
		};
		stackPanel2.Children.Add(ReadyRow("general", "Launch at login", "Start Ayn when you sign in to Windows", sToggle));
		stackPanel2.Children.Add(ReadyRow("privacy", "Everything stays on this PC", "Notes, clipboard and settings never leave it", null));
		stackPanel2.Children.Add(ReadyRow("license", LicenseLine(), LicenseDetail(), null));
		Page page = new Page();
		page.View = Frame("You're all set", ReadyLine(), stackPanel2);
		return page;
	}

	private static string ReadyLine()
	{
		if (LicenseStub.Status == LicenseStatus.Ayn && LicenseStub.AynStarted)
		{
			return $"Your {3}-day Ayn has started";
		}
		return "Ayn is ready at the top of your screen";
	}

	private static string LicenseLine()
	{
		LicenseStatus status = LicenseStub.Status;
		if (status == LicenseStatus.Active)
		{
			return "Ayn";
		}
		if (LicenseStub.AynStarted && status != 0)
		{
			return "Ayn";
		}
		int num = (LicenseStub.AynStarted ? LicenseStub.AynDaysLeft : 3);
		return string.Format("Ayn · {0} day{1} left", num, (num == 1) ? "" : "s");
	}

	private static string LicenseDetail()
	{
		if (LicenseStub.Status != LicenseStatus.Active)
		{
			return "Every tool is unlocked. No account needed";
		}
		return "Every tool is unlocked on this PC";
	}

	private static UIElement ReadyRow(string pageKey, string title, string detail, UIElement trailing)
	{
		Grid grid = new Grid();
		grid.MinHeight = 44.0;
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
			Width = GridLength.Auto
		});
		PageInfo pageInfo = PageInfo.Get(pageKey);
		object obj;
		if (pageInfo == null)
		{
			Border border = new Border();
			border.Width = 32.0;
			border.Height = 32.0;
			obj = border;
		}
		else
		{
			obj = pageInfo.Tile(32.0);
		}
		FrameworkElement frameworkElement = (FrameworkElement)obj;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(frameworkElement);
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(14.0, 0.0, 14.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(SettingsTheme.Text(title, 15.0, SettingsTheme.TextPrimary, AynType.Medium));
		TextBlock textBlock = SettingsTheme.Text(detail, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock);
		Grid.SetColumn(stackPanel2, 1);
		grid2.Children.Add(stackPanel2);
		if (trailing != null)
		{
			((FrameworkElement)trailing).VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(trailing, 2);
			grid2.Children.Add(trailing);
		}
		Border border2 = new Border();
		border2.Background = ThemeTokens.Surface;
		border2.BorderBrush = ThemeTokens.Rule;
		border2.BorderThickness = new Thickness(1.0);
		border2.CornerRadius = new CornerRadius(10.0);
		border2.Padding = new Thickness(14.0, 12.0, 16.0, 12.0);
		border2.Child = grid2;
		border2.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		return border2;
	}

	private static FrameworkElement Centered(TextBlock t, double top)
	{
		t.HorizontalAlignment = HorizontalAlignment.Center;
		t.TextAlignment = TextAlignment.Center;
		t.TextWrapping = TextWrapping.Wrap;
		t.Margin = new Thickness(0.0, top, 0.0, 0.0);
		return t;
	}

	private void BringToFront()
	{
		if (base.WindowState == WindowState.Minimized)
		{
			base.WindowState = WindowState.Normal;
		}
		Activate();
		try
		{
			SetForegroundWindow(new WindowInteropHelper(this).Handle);
		}
		catch
		{
		}
	}

	internal static BitmapSource AppIcon()
	{
		using Stream stream = typeof(OnboardingWindow).Assembly.GetManifestResourceStream("Ayn.Brand.mark-128.png");
		if (stream == null)
		{
			return null;
		}
		BitmapFrame bitmapFrame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
		bitmapFrame.Freeze();
		return bitmapFrame;
	}

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);
}
