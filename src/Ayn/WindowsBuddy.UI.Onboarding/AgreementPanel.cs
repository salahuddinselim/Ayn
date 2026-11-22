using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal static class AgreementPanel
{
	public const string TourTitle = "Before you start";

	public const string UpgradeTitle = "Before you continue";

	public const string CheckboxText = "I have read and agree to the Ayn End User License Agreement";

	public const string AcceptText = "Accept and continue";

	public const string DeclineText = "Decline and exit";

	public const string CloseName = "Close (decline and exit)";

	public const string EulaLinkText = "End User License Agreement";

	public const string TermsLinkText = "Terms of Service";

	public const string PrivacyLinkText = "Privacy Policy";

	public const string TourSentence = "To use Ayn, please read and accept its End User License Agreement.";

	public const string UpgradeSentence = "Ayn now has an End User License Agreement, so please read and accept it to keep using Ayn.";

	public const string UpdatedSentence = "The Ayn End User License Agreement has been updated, so please read and accept it to keep using Ayn.";

	internal static Action<string> OpenUrl = AppLinks.Open;

	internal static Action<Window> OpenEula = delegate(Window owner)
	{
		EulaReaderWindow.ShowFor(owner);
	};

	internal static Func<Rect> WorkArea = () => SystemParameters.WorkArea;

	public static string ExistingUserSentence()
	{
		string value = EulaAgreement.ReadAcceptedVersion();
		if (!string.IsNullOrEmpty(value))
		{
			return "The Ayn End User License Agreement has been updated, so please read and accept it to keep using Ayn.";
		}
		return "Ayn now has an End User License Agreement, so please read and accept it to keep using Ayn.";
	}

	public static StackPanel Content(Func<Window> owner, double width, out SCheckBox box)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Width = width;
		StackPanel stackPanel2 = stackPanel;
		box = new SCheckBox("I have read and agree to the Ayn End User License Agreement")
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		AutomationProperties.SetHelpText(box, "Required to continue");
		Border border = new Border();
		border.Background = ThemeTokens.Surface;
		border.BorderBrush = ThemeTokens.Rule;
		border.BorderThickness = new Thickness(1.0);
		border.CornerRadius = new CornerRadius(10.0);
		border.Padding = new Thickness(14.0, 12.0, 16.0, 12.0);
		border.Child = box;
		Border element = border;
		stackPanel2.Children.Add(element);
		WrapPanel wrapPanel = new WrapPanel();
		wrapPanel.HorizontalAlignment = HorizontalAlignment.Center;
		wrapPanel.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		WrapPanel wrapPanel2 = wrapPanel;
		AutomationProperties.SetName(wrapPanel2, "Legal documents");
		SLink sLink = new SLink("End User License Agreement", "Opens the full agreement in Ayn");
		sLink.Click += delegate
		{
			OpenEula(owner());
		};
		SLink sLink2 = new SLink("Terms of Service", "Opens https://github.com/salahuddinselim/Ayn in your browser");
		sLink2.Click += delegate
		{
			OpenUrl("https://github.com/salahuddinselim/Ayn");
		};
		SLink sLink3 = new SLink("Privacy Policy", "Opens https://github.com/salahuddinselim/Ayn in your browser");
		sLink3.Click += delegate
		{
			OpenUrl("https://github.com/salahuddinselim/Ayn");
		};
		wrapPanel2.Children.Add(sLink);
		wrapPanel2.Children.Add(Dot());
		wrapPanel2.Children.Add(sLink2);
		wrapPanel2.Children.Add(Dot());
		wrapPanel2.Children.Add(sLink3);
		stackPanel2.Children.Add(wrapPanel2);
		return stackPanel2;
	}

	private static UIElement Dot()
	{
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 3.0;
		ellipse.Height = 3.0;
		ellipse.Fill = SettingsTheme.TextLabel;
		ellipse.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		ellipse.VerticalAlignment = VerticalAlignment.Center;
		return ellipse;
	}

	public static double FitScale(double width, double height, Rect workArea, double margin = 16.0)
	{
		if (workArea.Width <= 0.0 || workArea.Height <= 0.0)
		{
			return 1.0;
		}
		double num = Math.Min((workArea.Width - margin) / width, (workArea.Height - margin) / height);
		if (!(num >= 1.0))
		{
			return Math.Max(0.6, num);
		}
		return 1.0;
	}

	public static double FitWindow(Window w, FrameworkElement shell, double width, double height)
	{
		double num = FitScale(width, height, WorkArea());
		w.Width = Math.Round(width * num);
		w.Height = Math.Round(height * num);
		shell.Width = width;
		shell.Height = height;
		shell.LayoutTransform = ((num < 1.0) ? new ScaleTransform(num, num) : null);
		return num;
	}
}
