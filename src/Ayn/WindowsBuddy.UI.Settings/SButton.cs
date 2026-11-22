using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace WindowsBuddy.UI.Settings;

internal class SButton : SClickable
{
	private readonly SButtonKind _kind;

	private readonly TextBlock _label;

	private readonly StackPanel _contents;

	public double LabelSize
	{
		get
		{
			return _label.FontSize;
		}
		set
		{
			_label.FontSize = value;
		}
	}

	public string Text
	{
		get
		{
			return _label.Text;
		}
		set
		{
			_label.Text = value;
			AutomationProperties.SetName(this, value);
		}
	}

	public SButton(string text, SButtonKind kind = SButtonKind.Secondary, string glyph = null)
		: base(8.0, text)
	{
		_kind = kind;
		base.Height = 36.0;
		base.Padding = new Thickness((glyph != null) ? 12 : 16, 0.0, 16.0, 0.0);
		base.VerticalAlignment = VerticalAlignment.Center;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		base.BorderThickness = new Thickness((kind != SButtonKind.Ghost) ? 1 : 0);
		_contents = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		if (glyph != null)
		{
			FrameworkElement frameworkElement = Glyphs.Create(glyph, 14.0, kind switch
			{
				SButtonKind.Danger => SettingsTheme.Danger, 
				SButtonKind.Primary => SettingsTheme.PrimaryButtonText, 
				_ => SettingsTheme.TextPrimary, 
			});
			frameworkElement.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
			_contents.Children.Add(frameworkElement);
		}
		_label = SettingsTheme.Text(text, 13.5, SettingsTheme.TextPrimary, FontWeights.Medium);
		_label.TextWrapping = TextWrapping.NoWrap;
		_contents.Children.Add(_label);
		Child = _contents;
		base.VisualRole = kind switch
		{
			SButtonKind.Ghost => SVisualRole.Ghost, 
			SButtonKind.Danger => SVisualRole.Danger, 
			SButtonKind.Primary => SVisualRole.Primary, 
			_ => SVisualRole.Surface, 
		};
		RefreshVisual();
	}

	internal void AddAccessory(FrameworkElement accessory)
	{
		accessory.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		accessory.VerticalAlignment = VerticalAlignment.Center;
		_contents.Children.Insert(0, accessory);
	}

	protected override void OnVisual()
	{
		base.OnVisual();
		if (_label != null)
		{
			_label.Foreground = ((_kind == SButtonKind.Primary) ? SettingsTheme.PrimaryButtonText : ((_kind == SButtonKind.Danger) ? SettingsTheme.Danger : ((_kind == SButtonKind.Ghost && (!base.IsEnabled || (!base.IsSelected && !base.IsMouseOver))) ? SettingsTheme.TextSecondary : SettingsTheme.TextPrimary)));
		}
	}
}
