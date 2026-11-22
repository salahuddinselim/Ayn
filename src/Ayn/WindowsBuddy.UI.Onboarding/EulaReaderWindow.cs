using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class EulaReaderWindow : Window
{
	public const double W = 680.0;

	public const double H = 640.0;

	private readonly ScrollViewer _scroll;

	internal ScrollViewer Scroll => _scroll;

	public EulaReaderWindow()
	{
		base.Title = "Ayn End User License Agreement";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ResizeMode = ResizeMode.NoResize;
		base.ShowInTaskbar = false;
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		base.Foreground = SettingsTheme.TextPrimary;
		base.FontFamily = SettingsTheme.Font;
		base.FontSize = 15.0;
		AynType.PrepareRoot(this, TypeSurface.Settings);
		AutomationProperties.SetName(this, base.Title);
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
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = new GridLength(1.0, GridUnitType.Star)
				},
				new RowDefinition
				{
					Height = new GridLength(68.0)
				}
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(32.0, 28.0, 64.0, 16.0)
		};
		TrackedText element = new TrackedText("End User License Agreement", 22.0, -0.015)
		{
			HorizontalAlignment = HorizontalAlignment.Left
		};
		AutomationProperties.SetName(element, "Ayn End User License Agreement");
		stackPanel.Children.Add(element);
		grid.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(32.0, 4.0, 40.0, 24.0),
			MaxWidth = 600.0,
			HorizontalAlignment = HorizontalAlignment.Left
		};
		bool first = true;
		EulaBlock[] blocks = EulaDocument.Blocks;
		foreach (EulaBlock eulaBlock in blocks)
		{
			if (eulaBlock.Kind != 0)
			{
				stackPanel2.Children.Add(Block(eulaBlock, first));
				first = false;
			}
		}
		_scroll = new ScrollViewer
		{
			Content = stackPanel2,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Focusable = true,
			FocusVisualStyle = SettingsTheme.FocusStyle(6.0)
		};
		KeyboardNavigation.SetIsTabStop(_scroll, isTabStop: true);
		AutomationProperties.SetName(_scroll, "Agreement text");
		Border element2 = new Border
		{
			BorderBrush = ThemeTokens.Rule,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 1.0),
			Child = _scroll
		};
		Grid.SetRow(element2, 1);
		grid.Children.Add(element2);
		Grid grid2 = new Grid
		{
			Margin = new Thickness(28.0, 0.0, 28.0, 0.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		SLink sLink = new SLink("Read it on github.com/salahuddinselim/Ayn", "Opens https://github.com/salahuddinselim/Ayn in your browser")
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Left
		};
		sLink.Click += delegate
		{
			AgreementPanel.OpenUrl("https://github.com/salahuddinselim/Ayn");
		};
		grid2.Children.Add(sLink);
		SButton sButton = new SButton("Close")
		{
			Height = 36.0,
			Padding = new Thickness(20.0, 0.0, 20.0, 0.0),
			MinWidth = 96.0
		};
		((FrameworkElement)sButton.Child).HorizontalAlignment = HorizontalAlignment.Center;
		sButton.Click += delegate
		{
			Close();
		};
		Grid.SetColumn(sButton, 1);
		grid2.Children.Add(sButton);
		Grid.SetRow(grid2, 2);
		grid.Children.Add(grid2);
		SClickable close = new SClickable(8.0, "Close")
		{
			Width = 32.0,
			Height = 32.0,
			Background = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 14.0, 14.0, 0.0),
			ToolTip = "Close"
		};
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
			Close();
		};
		Grid.SetRowSpan(close, 3);
		grid.Children.Add(close);
		shell.Child = grid;
		base.Content = shell;
		AgreementPanel.FitWindow(this, shell, 680.0, 640.0);
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
		base.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
				e.Handled = true;
			}
		};
		base.Loaded += delegate
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
			{
				_scroll.Focus();
			});
		};
	}

	private static FrameworkElement Block(EulaBlock b, bool first)
	{
		switch (b.Kind)
		{
		case EulaBlockKind.H2:
		{
			TextBlock textBlock2 = SettingsTheme.Text(b.Text, 15.5, SettingsTheme.TextPrimary, AynType.Semibold);
			textBlock2.Margin = new Thickness(0.0, (!first) ? 22 : 0, 0.0, 6.0);
			AutomationProperties.SetHeadingLevel(textBlock2, AutomationHeadingLevel.Level2);
			return textBlock2;
		}
		case EulaBlockKind.LI:
		{
			Grid grid = new Grid();
			grid.Margin = new Thickness(4.0, 0.0, 0.0, 6.0);
			Grid grid2 = grid;
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(16.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Ellipse ellipse = new Ellipse();
			ellipse.Width = 5.0;
			ellipse.Height = 5.0;
			ellipse.Fill = SettingsTheme.TextSecondary;
			ellipse.VerticalAlignment = VerticalAlignment.Top;
			ellipse.HorizontalAlignment = HorizontalAlignment.Left;
			ellipse.Margin = new Thickness(2.0, 8.0, 0.0, 0.0);
			Ellipse element = ellipse;
			grid2.Children.Add(element);
			TextBlock textBlock3 = Para(b.Text);
			textBlock3.Margin = new Thickness(0.0);
			Grid.SetColumn(textBlock3, 1);
			grid2.Children.Add(textBlock3);
			return grid2;
		}
		default:
		{
			TextBlock textBlock = Para(b.Text);
			if (first)
			{
				textBlock.Foreground = SettingsTheme.TextSecondary;
				textBlock.FontSize = 13.0;
			}
			return textBlock;
		}
		}
	}

	private static TextBlock Para(string text)
	{
		TextBlock textBlock = SettingsTheme.Text(text, 14.0, SettingsTheme.TextPrimary, FontWeights.Normal);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.VerticalAlignment = VerticalAlignment.Top;
		textBlock.LineHeight = 21.0;
		textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		return textBlock;
	}

	public static void ShowFor(Window owner)
	{
		try
		{
			EulaReaderWindow eulaReaderWindow = new EulaReaderWindow();
			if (owner != null && owner.IsVisible)
			{
				eulaReaderWindow.Owner = owner;
				eulaReaderWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			else
			{
				eulaReaderWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}
			eulaReaderWindow.ShowDialog();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Agreement reader could not open: " + ex.Message);
		}
	}
}
