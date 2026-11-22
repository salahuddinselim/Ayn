using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal sealed class SCheckBox : SClickable
{
	private readonly Border _box;

	private readonly Path _tick;

	private readonly TextBlock _label;

	private bool _checked;

	public string Label => _label.Text;

	public bool IsChecked
	{
		get
		{
			return _checked;
		}
		set
		{
			if (_checked != value)
			{
				bool @checked = _checked;
				_checked = value;
				Apply();
				UIElementAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, @checked ? ToggleState.On : ToggleState.Off, value ? ToggleState.On : ToggleState.Off);
				this.Changed?.Invoke(value);
			}
		}
	}

	public event Action<bool> Changed;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new AgreementCheckBoxPeer(this);
	}

	public SCheckBox(string label)
		: base(6.0, label)
	{
		base.VisualRole = SVisualRole.Bare;
		base.Padding = new Thickness(4.0);
		base.HorizontalAlignment = HorizontalAlignment.Left;
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				}
			}
		};
		_tick = new Path
		{
			Data = Geometry.Parse("M4.6,10.4 L8.4,14 L15.4,6.4"),
			StrokeThickness = 2.2,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			Width = 20.0,
			Height = 20.0,
			Stretch = Stretch.None
		};
		_box = new Border
		{
			Width = 20.0,
			Height = 20.0,
			CornerRadius = new CornerRadius(5.0),
			BorderThickness = new Thickness(1.5),
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
			Child = _tick
		};
		grid.Children.Add(_box);
		_label = SettingsTheme.Text(label, 14.5, SettingsTheme.TextPrimary, AynType.Medium);
		_label.TextWrapping = TextWrapping.Wrap;
		_label.VerticalAlignment = VerticalAlignment.Center;
		_label.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		Grid.SetColumn(_label, 1);
		grid.Children.Add(_label);
		Child = grid;
		EventHandler value = delegate
		{
			Toggle();
		};
		base.Click += value;
		Apply();
	}

	public void Toggle()
	{
		if (base.IsEnabled)
		{
			IsChecked = !_checked;
		}
	}

	protected override void OnVisual()
	{
		base.OnVisual();
		if (_box != null)
		{
			Apply();
		}
	}

	private void Apply()
	{
		bool flag = base.IsEnabled && (base.IsMouseOver || base.IsPressed);
		_box.Background = (_checked ? ThemeTokens.AccentStrong : Brushes.Transparent);
		_box.BorderBrush = (_checked ? ThemeTokens.AccentStrong : (flag ? ThemeTokens.Text2 : ThemeTokens.BorderStrong));
		_tick.Stroke = ThemeTokens.AccentFg;
		_tick.Visibility = ((!_checked) ? Visibility.Hidden : Visibility.Visible);
		AutomationProperties.SetItemStatus(this, _checked ? "Checked" : "Not checked");
	}
}
