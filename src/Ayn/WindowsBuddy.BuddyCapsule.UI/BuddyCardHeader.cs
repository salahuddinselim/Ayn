using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyCardHeader : Grid
{
	public BuddyCardHeader(UIElement icon, string labelText, UIElement rightAccessory = null, UIElement trailingLabelElement = null)
	{
		base.SnapsToDevicePixels = true;
		base.UseLayoutRounding = true;
		base.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		base.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		if (rightAccessory != null)
		{
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
		}
		if (icon != null)
		{
			Border element = new Border
			{
				Width = 16.0,
				Height = 16.0,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				Child = icon
			};
			Grid.SetColumn(element, 0);
			base.Children.Add(element);
		}
		if (trailingLabelElement != null)
		{
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			BuddyLabel element2 = new BuddyLabel(labelText, BuddyTokens.Text38Brush)
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(element2);
			stackPanel.Children.Add(trailingLabelElement);
			Grid.SetColumn(stackPanel, (icon != null) ? 1 : 0);
			base.Children.Add(stackPanel);
		}
		else
		{
			BuddyLabel element3 = new BuddyLabel(labelText, BuddyTokens.Text38Brush)
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(element3, (icon != null) ? 1 : 0);
			if (icon == null && rightAccessory == null)
			{
				Grid.SetColumnSpan(element3, 2);
			}
			base.Children.Add(element3);
		}
		if (rightAccessory != null)
		{
			Grid.SetColumn(rightAccessory, 2);
			base.Children.Add(rightAccessory);
		}
	}
}
