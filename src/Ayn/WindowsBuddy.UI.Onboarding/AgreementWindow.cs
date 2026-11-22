using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class AgreementWindow : Window
{
	public const double W = 640.0;

	public const double H = 460.0;

	private readonly SCheckBox _box;

	private readonly SButton _accept;

	private readonly SButton _decline;

	private readonly SClickable _close;

	private bool _decided;

	public bool Accepted { get; private set; }

	internal SCheckBox CheckBox => _box;

	internal SButton AcceptButton => _accept;

	internal SButton DeclineButton => _decline;

	public AgreementWindow()
	{
		base.Title = "Ayn – End User License Agreement";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ResizeMode = ResizeMode.NoResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		base.ShowInTaskbar = true;
		base.Topmost = false;
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		base.Foreground = SettingsTheme.TextPrimary;
		base.FontFamily = SettingsTheme.Font;
		base.FontSize = 15.0;
		AynType.PrepareRoot(this, TypeSurface.Settings);
		AutomationProperties.SetName(this, base.Title);
		try
		{
			base.Icon = OnboardingWindow.AppIcon();
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
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 36.0, 0.0, 0.0)
		};
		FrameworkElement frameworkElement = BrandAssets.CreateLogo(40.0);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		AutomationProperties.SetName(frameworkElement, "Ayn");
		stackPanel.Children.Add(frameworkElement);
		TrackedText element = new TrackedText("Before you continue", 28.0, -0.02)
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		AutomationProperties.SetName(element, "Before you continue");
		stackPanel.Children.Add(element);
		TextBlock textBlock = SettingsTheme.Text(AgreementPanel.ExistingUserSentence(), 15.0, SettingsTheme.TextSecondary, FontWeights.Normal);
		textBlock.TextAlignment = TextAlignment.Center;
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.MaxWidth = 480.0;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock);
		SCheckBox box;
		StackPanel stackPanel2 = AgreementPanel.Content(() => this, 540.0, out box);
		stackPanel2.Margin = new Thickness(0.0, 24.0, 0.0, 0.0);
		stackPanel.Children.Add(stackPanel2);
		_box = box;
		grid.Children.Add(stackPanel);
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(32.0, 0.0, 32.0, 0.0)
		};
		_decline = new SButton("Decline and exit", SButtonKind.Ghost)
		{
			Height = 36.0,
			Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		AutomationProperties.SetHelpText(_decline, "Closes Ayn without changing anything");
		_decline.Click += delegate
		{
			Decide(accept: false);
		};
		stackPanel3.Children.Add(_decline);
		_accept = new SButton("Accept and continue", SButtonKind.Primary)
		{
			Height = 36.0,
			Padding = new Thickness(24.0, 0.0, 24.0, 0.0),
			MinWidth = 112.0,
			CornerRadius = new CornerRadius(18.0)
		};
		((FrameworkElement)_accept.Child).HorizontalAlignment = HorizontalAlignment.Center;
		AutomationProperties.SetHelpText(_accept, "Available after you tick the agreement box");
		_accept.IsEnabled = false;
		_accept.Click += delegate
		{
			Decide(accept: true);
		};
		stackPanel3.Children.Add(_accept);
		Grid.SetRow(stackPanel3, 1);
		grid.Children.Add(stackPanel3);
		_box.Changed += delegate(bool v)
		{
			_accept.IsEnabled = v;
		};
		_close = new SClickable(8.0, "Close (decline and exit)")
		{
			Width = 32.0,
			Height = 32.0,
			Background = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 14.0, 14.0, 0.0),
			ToolTip = "Decline and exit"
		};
		FrameworkElement frameworkElement2 = Glyphs.Create("close", 12.0, SettingsTheme.TextSecondary);
		frameworkElement2.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
		_close.Child = frameworkElement2;
		_close.MouseEnter += delegate
		{
			_close.Background = ThemeTokens.AccentSoft;
		};
		_close.MouseLeave += delegate
		{
			_close.Background = Brushes.Transparent;
		};
		_close.Click += delegate
		{
			Decide(accept: false);
		};
		Grid.SetRowSpan(_close, 2);
		grid.Children.Add(_close);
		shell.Child = grid;
		base.Content = shell;
		AgreementPanel.FitWindow(this, shell, 640.0, 460.0);
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
		base.Closing += delegate
		{
			if (!_decided)
			{
				_decided = true;
				Accepted = false;
				TransactionLogger.Info("End User License Agreement declined (window closed); Ayn exits without changes.");
			}
		};
		base.Loaded += delegate
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
			{
				Activate();
				_box.Focus();
			});
		};
	}

	private void OnKey(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			Decide(accept: false);
			e.Handled = true;
		}
		else
		{
			if (e.Key != Key.Return && e.Key != Key.Return)
			{
				return;
			}
			IInputElement focusedElement = Keyboard.FocusedElement;
			if (!(focusedElement is SLink) && (!(focusedElement is SButton) || focusedElement == _accept) && focusedElement != _close)
			{
				if (_box.IsChecked)
				{
					Decide(accept: true);
				}
				e.Handled = true;
			}
		}
	}

	public void Decide(bool accept)
	{
		if (!_decided && (!accept || _box.IsChecked))
		{
			_decided = true;
			Accepted = accept;
			if (accept)
			{
				EulaAgreement.RecordAcceptance();
			}
			else
			{
				TransactionLogger.Info("End User License Agreement declined; Ayn exits without changes.");
			}
			Close();
		}
	}

	public static bool AskOnce()
	{
		try
		{
			AgreementWindow agreementWindow = new AgreementWindow();
			agreementWindow.ShowDialog();
			return agreementWindow.Accepted;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Agreement screen failed: " + ex.Message, ex);
			return false;
		}
	}
}
