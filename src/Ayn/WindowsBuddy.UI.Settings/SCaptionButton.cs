using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowsBuddy.UI.Settings;

internal sealed class SCaptionButton : SClickable
{
	private string _glyph;

	private Brush _ink;

	internal SCaptionButton(string glyph, string name, bool close)
		: base(0.0, name)
	{
		_glyph = glyph;
		base.Width = 46.0;
		base.Height = 32.0;
		base.ToolTip = name;
		base.VisualRole = (close ? SVisualRole.CloseCaption : SVisualRole.Caption);
		RefreshVisual();
	}

	internal void SetMaximized(bool maximized)
	{
		_glyph = (maximized ? "restore" : "max");
		base.ToolTip = (maximized ? "Restore" : "Maximize");
		AutomationProperties.SetName(this, (string)base.ToolTip);
		_ink = null;
		RefreshVisual();
	}

	protected override void OnVisual()
	{
		base.OnVisual();
		if (_glyph != null)
		{
			Brush brush = ((base.VisualRole == SVisualRole.CloseCaption && base.IsEnabled && (base.IsMouseOver || base.IsPressed)) ? Brushes.White : SettingsTheme.TextPrimary);
			if (_ink != brush)
			{
				_ink = brush;
				Child = new Border
				{
					Child = Glyphs.Create(_glyph, 12.0, brush),
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				base.OnVisual();
			}
		}
	}
}
