using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.UI.Settings;

internal class SToggle : SClickable
{
	private readonly Ellipse _knob;

	private readonly TranslateTransform _tx = new TranslateTransform();

	private readonly ScaleTransform _knobScale = new ScaleTransform(1.0, 1.0, 10.0, 10.0);

	private readonly SolidColorBrush _track = new SolidColorBrush();

	private bool _on;

	public bool IsOn
	{
		get
		{
			return _on;
		}
		set
		{
			if (_on != value)
			{
				bool on = _on;
				_on = value;
				Apply(base.IsVisible && base.IsEnabled);
				UIElementAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, on ? ToggleState.On : ToggleState.Off, _on ? ToggleState.On : ToggleState.Off);
			}
		}
	}

	public event Action<bool> Toggled;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsTogglePeer(this);
	}

	public SToggle(bool on, string name = null)
		: base(12.0, name)
	{
		base.Width = 44.0;
		base.Height = 32.0;
		base.VerticalAlignment = VerticalAlignment.Center;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		TransformGroup renderTransform = new TransformGroup
		{
			Children = 
			{
				(Transform)_knobScale,
				(Transform)_tx
			}
		};
		_knob = new Ellipse
		{
			Width = 20.0,
			Height = 20.0,
			Fill = SettingsTheme.ToggleOffKnob,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(2.0, 0.0, 0.0, 0.0),
			RenderTransform = renderTransform
		};
		Child = new Border
		{
			Height = 24.0,
			CornerRadius = new CornerRadius(12.0),
			Background = _track,
			Child = _knob,
			VerticalAlignment = VerticalAlignment.Center
		};
		_on = on;
		base.VisualRole = SVisualRole.Bare;
		EventHandler value = delegate
		{
			Flip();
		};
		base.Click += value;
		base.IsVisibleChanged += delegate
		{
			if (!base.IsVisible)
			{
				SMotion.Finish(_tx);
				SMotion.Finish(_knobScale);
			}
		};
		base.Unloaded += delegate
		{
			SMotion.Finish(_tx);
			SMotion.Finish(_knobScale);
		};
		Apply(animate: false);
	}

	public void Flip()
	{
		if (base.IsEnabled)
		{
			IsOn = !_on;
			this.Toggled?.Invoke(_on);
		}
	}

	protected override void OnVisual()
	{
		base.OnVisual();
		if (_knob != null)
		{
			Apply(base.IsVisible && base.IsEnabled);
		}
	}

	private void Apply(bool animate)
	{
		_track.Color = (_on ? ThemeTokens.Target(base.IsPressed ? ThemeTokens.AccentStrong : ThemeTokens.Accent) : ThemeTokens.Target((base.IsEnabled && (base.IsMouseOver || base.IsPressed)) ? ThemeTokens.BorderStrong : ThemeTokens.ControlFillHover));
		_knob.Fill = (_on ? SettingsTheme.ToggleOnKnob : SettingsTheme.ToggleOffKnob);
		AutomationProperties.SetItemStatus(this, _on ? "On" : "Off");
		SMotion.To(_tx, TranslateTransform.XProperty, _on ? 20.0 : 0.0, animate);
		SMotion.Scale(_knobScale, (!base.IsEnabled) ? 1.0 : (base.IsPressed ? 1.08 : (base.IsMouseOver ? 1.03 : 1.0)), animate);
	}
}
