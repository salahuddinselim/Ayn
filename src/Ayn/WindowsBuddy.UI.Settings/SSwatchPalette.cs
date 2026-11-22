using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.UI.Settings;

internal class SSwatchPalette : StackPanel
{
	private readonly WrapPanel _presets = new WrapPanel();

	private readonly List<KeyValuePair<string, SSwatch>> _swatches = new List<KeyValuePair<string, SSwatch>>();

	private readonly Border _customDot = new Border
	{
		Width = 12.0,
		Height = 12.0,
		CornerRadius = new CornerRadius(6.0),
		BorderThickness = new Thickness(1.0),
		BorderBrush = ThemeTokens.BorderStrong
	};

	protected readonly SButton CustomButton = new SButton("Custom…");

	protected SSwatchPalette()
	{
		base.MaxWidth = 320.0;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		base.Children.Add(_presets);
		CustomButton.AddAccessory(_customDot);
		CustomButton.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		base.Children.Add(CustomButton);
	}

	protected void AddPreset(string key, Brush fill, string tip, Action select)
	{
		SSwatch sSwatch = new SSwatch(key, fill);
		sSwatch.ToolTip = tip;
		SSwatch sSwatch2 = sSwatch;
		sSwatch2.Click += delegate
		{
			select();
		};
		_swatches.Add(new KeyValuePair<string, SSwatch>(key, sSwatch2));
		_presets.Children.Add(sSwatch2);
	}

	protected void ShowSelection(string key, string customHex)
	{
		foreach (KeyValuePair<string, SSwatch> swatch in _swatches)
		{
			swatch.Value.IsSelected = swatch.Key == key;
		}
		CustomButton.IsSelected = key == "Custom";
		Color color;
		try
		{
			color = (Color)ColorConverter.ConvertFromString(customHex);
		}
		catch
		{
			color = Colors.White;
		}
		_customDot.Background = new SolidColorBrush(color);
		CustomButton.ToolTip = "Custom color " + customHex;
		AutomationProperties.SetHelpText(CustomButton, (CustomButton.IsSelected ? "Selected custom color " : "Choose a custom color. Last custom color ") + customHex);
	}
}
