using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal sealed class MusicSwatches : SSwatchPalette
{
	private readonly NotchConfig _cfg;

	public MusicSwatches(NotchConfig cfg)
	{
		_cfg = cfg;
		SSettingBinding.Attach(this, Refresh);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0)
		};
		linearGradientBrush.GradientStops.Add(new GradientStop(SettingsTheme.Hex("#FF453A"), 0.0));
		linearGradientBrush.GradientStops.Add(new GradientStop(SettingsTheme.Hex("#BF5AF2"), 0.5));
		linearGradientBrush.GradientStops.Add(new GradientStop(SettingsTheme.Hex("#64D2FF"), 1.0));
		Add("Adaptive", linearGradientBrush, "Adaptive: the album art's main color");
		Add("White", Brushes.White, "White");
		KeyValuePair<string, Color>[] musicPresets = NotchConfig.MusicPresets;
		for (int i = 0; i < musicPresets.Length; i++)
		{
			KeyValuePair<string, Color> keyValuePair = musicPresets[i];
			Add(keyValuePair.Key, new SolidColorBrush(keyValuePair.Value), keyValuePair.Key);
		}
		CustomButton.Click += delegate
		{
			OpenPicker(CustomButton);
		};
		Refresh();
	}

	private void Add(string key, Brush fill, string tip)
	{
		AddPreset(key, fill, tip, delegate
		{
			_cfg.MusicColor = key;
			Refresh();
		});
	}

	private void Refresh()
	{
		string musicColor = _cfg.MusicColor;
		ShowSelection(musicColor, _cfg.MusicCustomColor);
	}

	private void OpenPicker(UIElement anchor)
	{
		string initialKey = _cfg.MusicColor;
		Color initialColor;
		try
		{
			initialColor = (Color)ColorConverter.ConvertFromString(_cfg.MusicCustomColor);
		}
		catch
		{
			initialColor = Colors.White;
		}
		BuddyCustomColorPickerWindow buddyCustomColorPickerWindow = new BuddyCustomColorPickerWindow(initialColor, delegate(Color color, string hex)
		{
			_cfg.SetCustomMusicColor(hex);
			Refresh();
		}, delegate
		{
		}, delegate
		{
			_cfg.MusicColor = initialKey;
			Refresh();
		});
		buddyCustomColorPickerWindow.Owner = Window.GetWindow(this);
		buddyCustomColorPickerWindow.PositionBelow(anchor);
		buddyCustomColorPickerWindow.ShowDialog();
	}
}
