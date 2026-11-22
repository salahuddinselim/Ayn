using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.UI.Settings;

internal sealed class SSwatch : SClickable
{
	private readonly Border _dot;

	internal SSwatch(string name, Brush color)
		: base(8.0, name)
	{
		base.Width = (base.Height = 32.0);
		base.Margin = new Thickness(0.0, 0.0, 4.0, 4.0);
		_dot = new Border
		{
			Width = 16.0,
			Height = 16.0,
			CornerRadius = new CornerRadius(8.0),
			Background = color,
			BorderThickness = new Thickness(2.0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Child = _dot;
		base.VisualRole = SVisualRole.Swatch;
	}

	protected override void OnVisual()
	{
		base.OnVisual();
		if (_dot != null)
		{
			_dot.BorderBrush = (base.IsSelected ? SettingsTheme.SelectedOutline : ((base.IsEnabled && base.IsKeyboardFocusWithin) ? SettingsTheme.FocusRing : ((base.IsEnabled && base.IsMouseOver) ? ThemeTokens.BorderStrong : SettingsTheme.ControlBorder)));
		}
	}
}
