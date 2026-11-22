using System.Windows;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.UI.Settings;

internal sealed class EdgeSwatches : SSwatchPalette
{
	private static readonly string[][] Colors_ = new string[12][]
	{
		new string[2] { "Arctic White", "#F5F7FA" },
		new string[2] { "Champagne Gold", "#E7B85C" },
		new string[2] { "Amber", "#F59E0B" },
		new string[2] { "Emerald", "#27C98A" },
		new string[2] { "Teal", "#20B8B0" },
		new string[2] { "Azure", "#3B9EFF" },
		new string[2] { "Indigo", "#6366F1" },
		new string[2] { "Violet", "#9B6DFF" },
		new string[2] { "Rose", "#F06B8A" },
		new string[2] { "Coral", "#F0785F" },
		new string[2] { "Copper", "#C97A45" },
		new string[2] { "Ice Blue", "#69C7E8" }
	};

	private readonly NotchConfig _cfg;

	public EdgeSwatches(NotchConfig cfg)
	{
		_cfg = cfg;
		SSettingBinding.Attach(this, Refresh);
		string[][] colors_ = Colors_;
		foreach (string[] array in colors_)
		{
			string key = array[0];
			AddPreset(key, new SolidColorBrush(SettingsTheme.Hex(array[1])), key, delegate
			{
				_cfg.EdgeLightColor = key;
				Refresh();
			});
		}
		CustomButton.Click += delegate
		{
			OpenPicker(CustomButton);
		};
		Refresh();
	}

	private void Refresh()
	{
		string text = _cfg.EdgeLightColor;
		if (text == "White")
		{
			text = "Arctic White";
		}
		if (text == "Gold")
		{
			text = "Champagne Gold";
		}
		ShowSelection(text, _cfg.EdgeLightCustomColor);
	}

	private void OpenPicker(UIElement anchor)
	{
		string initialKey = _cfg.EdgeLightColor;
		string initialHex = _cfg.EdgeLightCustomColor;
		BuddyCustomColorPickerWindow buddyCustomColorPickerWindow = new BuddyCustomColorPickerWindow(_cfg.GetEdgeLightColor(), delegate(Color color, string hex)
		{
			_cfg.SetCustomEdgeLightColor(hex);
			Refresh();
		}, delegate(Color color, string hex)
		{
			CapsuleWindow.Instance?.SetTemporaryEdgeLightColor(color);
		}, delegate
		{
			_cfg.EdgeLightCustomColor = initialHex;
			_cfg.EdgeLightColor = initialKey;
			CapsuleWindow.Instance?.ApplyNotchConfig();
			Refresh();
		});
		buddyCustomColorPickerWindow.Owner = Window.GetWindow(this);
		buddyCustomColorPickerWindow.PositionBelow(anchor);
		buddyCustomColorPickerWindow.ShowDialog();
	}
}
