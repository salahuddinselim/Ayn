using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.UI.Settings;

internal static class SettingsPageLayout
{
	public const double PageWidth = 616.0;

	public const double LeftColWidth = 272.0;

	public const double ColGap = 24.0;

	public const double RightColWidth = 320.0;

	public static StackPanel Create(string key, string title = null, string subtitle = null)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.MaxWidth = 616.0;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
		stackPanel.Margin = new Thickness(0.0, 0.0, 0.0, 40.0);
		StackPanel stackPanel2 = stackPanel;
		if (title != null)
		{
			stackPanel2.Children.Add(SUI.PageHeader(title, subtitle));
		}
		else
		{
			PageInfo pageInfo = PageInfo.Get(key);
			if (pageInfo != null)
			{
				stackPanel2.Children.Add(SUI.PageHeader(pageInfo.Title, pageInfo.Subtitle));
			}
		}
		return stackPanel2;
	}
}
