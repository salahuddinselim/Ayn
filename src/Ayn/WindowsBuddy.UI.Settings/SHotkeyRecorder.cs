using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.Services;

namespace WindowsBuddy.UI.Settings;

internal class SHotkeyRecorder : Border
{
	private readonly Border _field;

	private readonly TextBlock _textBlock;

	private readonly SButton _changeBtn;

	private readonly SButton _clearBtn;

	private bool _recording;

	private int _mods;

	private int _key;

	public Func<int, int, string> Validate;

	public bool IsRecording => _recording;

	public event Action<int, int> Recorded;

	public event Action<string> Message;

	public event Action<bool> RecordingChanged;

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SettingsHotkeyPeer(this);
	}

	internal void ToggleRecording()
	{
		if (base.IsEnabled)
		{
			SetRecording(!_recording);
		}
	}

	public SHotkeyRecorder(int mods, int key)
	{
		_mods = mods;
		_key = key;
		base.Background = Brushes.Transparent;
		base.HorizontalAlignment = HorizontalAlignment.Left;
		base.VerticalAlignment = VerticalAlignment.Center;
		base.ToolTip = "Click Change, then press a shortcut. Backspace clears, Esc cancels.";
		AutomationProperties.SetHelpText(this, "Press Change, then the new shortcut. Backspace clears it, Escape cancels.");
		SettingsTheme.MakeFocusable(this, 8.0, "Keyboard shortcut");
		WrapPanel wrapPanel = new WrapPanel
		{
			Orientation = Orientation.Horizontal
		};
		_textBlock = SettingsTheme.Text(FormatShortcut(mods, key), 13.5, SettingsTheme.TextPrimary, FontWeights.Normal);
		_textBlock.VerticalAlignment = VerticalAlignment.Center;
		_field = new SClickable(8.0, "Change keyboard shortcut")
		{
			Width = 180.0,
			Height = 36.0,
			Background = SettingsTheme.ControlBg,
			BorderBrush = SettingsTheme.ControlBorder,
			BorderThickness = new Thickness(2.0),
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(16.0, 0.0, 16.0, 0.0),
			Child = _textBlock,
			Cursor = Cursors.Hand
		};
		SClickable obj = (SClickable)_field;
		EventHandler value = delegate
		{
			SetRecording(!_recording);
		};
		obj.Click += value;
		_changeBtn = new SButton("Change");
		_changeBtn.Padding = new Thickness(12.0, 0.0, 12.0, 0.0);
		_changeBtn.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		_changeBtn.Click += delegate
		{
			SetRecording(!_recording);
		};
		wrapPanel.Children.Add(_field);
		wrapPanel.Children.Add(_changeBtn);
		_clearBtn = new SButton("Clear", SButtonKind.Ghost)
		{
			Padding = new Thickness(8.0, 0.0, 8.0, 0.0)
		};
		_clearBtn.Click += delegate
		{
			ClearShortcut();
		};
		wrapPanel.Children.Add(_clearBtn);
		Child = wrapPanel;
		base.PreviewKeyDown += OnKey;
		base.IsEnabledChanged += delegate
		{
			if (!base.IsEnabled)
			{
				SetRecording(on: false);
			}
		};
		base.Unloaded += delegate
		{
			SetRecording(on: false);
		};
		base.PreviewKeyUp += delegate
		{
			if (_recording)
			{
				ShowHeld();
			}
		};
		base.LostKeyboardFocus += delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				if (_recording && !base.IsKeyboardFocusWithin)
				{
					SetRecording(on: false);
				}
			});
		};
		Show();
	}

	public void SetValue(int mods, int key)
	{
		_mods = mods;
		_key = key;
		if (_recording)
		{
			SetRecording(on: false);
		}
		else
		{
			Show();
		}
	}

	private void SetRecording(bool on)
	{
		if (_recording != on)
		{
			_recording = on;
			if (on)
			{
				Focus();
			}
			Show();
			this.RecordingChanged?.Invoke(on);
		}
	}

	public static string[] Parts(int modifiers, int key)
	{
		List<string> list = new List<string>();
		if (((uint)modifiers & 2u) != 0)
		{
			list.Add("Ctrl");
		}
		if (((uint)modifiers & (true ? 1u : 0u)) != 0)
		{
			list.Add("Alt");
		}
		if (((uint)modifiers & 4u) != 0)
		{
			list.Add("Shift");
		}
		if (((uint)modifiers & 8u) != 0)
		{
			list.Add("Win");
		}
		if (key != 0)
		{
			list.Add(HotkeyService.KeyName((Key)key));
		}
		return list.ToArray();
	}

	private static string FormatShortcut(int modifiers, int key)
	{
		if (key == 0)
		{
			return "Not set";
		}
		return string.Join(" + ", Parts(modifiers, key));
	}

	private void Show()
	{
		if (_recording)
		{
			ShowHeld();
			return;
		}
		((SClickable)_field).IsSelected = false;
		((SClickable)_field).RefreshVisual();
		_textBlock.Text = FormatShortcut(_mods, _key);
		_textBlock.Foreground = ((_key == 0) ? SettingsTheme.TextSecondary : SettingsTheme.TextPrimary);
		if (_key == 0)
		{
			_field.Child = _textBlock;
		}
		else
		{
			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = Orientation.Horizontal;
			stackPanel.VerticalAlignment = VerticalAlignment.Center;
			StackPanel stackPanel2 = stackPanel;
			string[] array = Parts(_mods, _key);
			foreach (string text in array)
			{
				if (stackPanel2.Children.Count > 0)
				{
					stackPanel2.Children.Add(SettingsTheme.Text(" + ", 11.0, SettingsTheme.TextSecondary, FontWeights.Normal));
				}
				stackPanel2.Children.Add(Keycap.Create(text, dim: false));
			}
			_field.Child = new Viewbox
			{
				Stretch = Stretch.Uniform,
				StretchDirection = StretchDirection.DownOnly,
				Child = stackPanel2
			};
		}
		_clearBtn.Visibility = ((_key == 0) ? Visibility.Collapsed : Visibility.Visible);
		_changeBtn.Text = "Change";
	}

	private void ShowHeld()
	{
		((SClickable)_field).IsSelected = true;
		((SClickable)_field).RefreshVisual();
		_field.BorderBrush = SettingsTheme.FocusRing;
		_field.Child = _textBlock;
		_clearBtn.Visibility = Visibility.Collapsed;
		string[] array = Parts(CurrentMods(), 0);
		if (array.Length > 0)
		{
			_textBlock.Text = string.Join(" + ", array) + " + …";
		}
		else
		{
			_textBlock.Text = "Press a shortcut...";
		}
		_textBlock.Foreground = SettingsTheme.TextSecondary;
		_changeBtn.Text = "Cancel";
	}

	private static int CurrentMods()
	{
		int num = (int)Keyboard.Modifiers;
		if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
		{
			num |= 8;
		}
		return num;
	}

	private void Say(string msg)
	{
		this.Message?.Invoke(msg);
	}

	private void ClearShortcut()
	{
		_mods = 0;
		_key = 0;
		if (_recording)
		{
			SetRecording(on: false);
		}
		else
		{
			Show();
		}
		Say(null);
		this.Recorded?.Invoke(0, 0);
	}

	private void OnKey(object sender, KeyEventArgs e)
	{
		if (!_recording)
		{
			if (e.OriginalSource == this && (e.Key == Key.Return || e.Key == Key.Space))
			{
				ToggleRecording();
				e.Handled = true;
			}
		}
		else
		{
			e.Handled = true;
			Key k = ((e.Key == Key.System) ? e.SystemKey : e.Key);
			RecordKey(k, CurrentMods());
		}
	}

	internal void RecordKey(Key k, int mods)
	{
		if (!_recording)
		{
			return;
		}
		if (k == Key.Escape && mods == 0)
		{
			SetRecording(on: false);
			Say(null);
			return;
		}
		if ((k == Key.Back || k == Key.Delete) && mods == 0)
		{
			ClearShortcut();
			return;
		}
		if (k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt || k == Key.LeftShift || k == Key.RightShift || k == Key.LWin || k == Key.RWin || k == Key.ImeProcessed || k == Key.DeadCharProcessed || k == Key.None)
		{
			ShowHeld();
			return;
		}
		string text = ((Validate != null) ? Validate(mods, (int)k) : null);
		if (text != null)
		{
			Say(text);
			ShowHeld();
			return;
		}
		_mods = mods;
		_key = (int)k;
		SetRecording(on: false);
		Say(null);
		this.Recorded?.Invoke(_mods, _key);
	}
}
