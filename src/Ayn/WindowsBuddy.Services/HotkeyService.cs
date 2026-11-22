using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public sealed class HotkeyService
{
	private const int WM_HOTKEY = 786;

	private const int HotkeyId = 23057;

	private const uint MOD_NOREPEAT = 16384u;

	private const int CtrlBit = 2;

	private const int AltBit = 1;

	private const int ShiftBit = 4;

	private const int WinBit = 8;

	private static HotkeyService _instance;

	private HwndSource _source;

	private bool _registered;

	private bool _disposed;

	private int _activeId = 23057;

	private int _mods;

	private int _key;

	private bool _suspended;

	public static HotkeyService Instance => _instance ?? (_instance = new HotkeyService());

	public int LastNativeError { get; private set; }

	public string LastError { get; private set; }

	public bool IsRegistered => _registered;

	public event EventHandler Pressed;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	public void Initialize()
	{
		_disposed = false;
		AppPreferences instance = AppPreferences.Instance;
		if (instance.HotkeyKey != 0)
		{
			Apply(instance.HotkeyModifiers, instance.HotkeyKey);
		}
	}

	private void EnsureSource()
	{
		if (_source == null)
		{
			HwndSourceParameters hwndSourceParameters = new HwndSourceParameters("AynHotkey");
			hwndSourceParameters.ParentWindow = new IntPtr(-3);
			hwndSourceParameters.Width = 0;
			hwndSourceParameters.Height = 0;
			hwndSourceParameters.WindowStyle = 0;
			HwndSourceParameters parameters = hwndSourceParameters;
			_source = new HwndSource(parameters);
			_source.AddHook(WndProc);
		}
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (_registered && msg == 786 && wParam.ToInt32() == _activeId)
		{
			handled = true;
			EventHandler pressed = this.Pressed;
			if (pressed != null)
			{
				try
				{
					pressed(this, EventArgs.Empty);
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Hotkey handler: " + ex.Message);
				}
			}
		}
		return IntPtr.Zero;
	}

	public bool Apply(int modifiers, int key)
	{
		LastError = null;
		LastNativeError = 0;
		if (key != 0)
		{
			string text2 = (LastError = Validate(modifiers, key));
			if (text2 != null)
			{
				return false;
			}
		}
		try
		{
			EnsureSource();
			_suspended = false;
			if (key == 0)
			{
				Release();
				_mods = (_key = 0);
				return true;
			}
			if (_registered && _mods == modifiers && _key == key)
			{
				return true;
			}
			int num = ((_activeId == 23057) ? 23058 : 23057);
			uint vk = (uint)KeyInterop.VirtualKeyFromKey((Key)key);
			bool flag = RegisterHotKey(_source.Handle, num, (uint)modifiers | 0x4000u, vk);
			LastNativeError = ((!flag) ? Marshal.GetLastWin32Error() : 0);
			TransactionLogger.Info("RegisterHotKey result=" + flag + " error=" + LastNativeError + " shortcut=" + Describe(modifiers, key));
			if (!flag)
			{
				LastError = ((LastNativeError == 1409) ? "Already used by another app. Try a different shortcut" : ("Windows could not register this shortcut (error " + LastNativeError + "). Try a different shortcut."));
				return false;
			}
			Release();
			_activeId = num;
			_mods = modifiers;
			_key = key;
			_registered = true;
			return true;
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			return false;
		}
	}

	private void Release()
	{
		if (_registered && _source != null)
		{
			UnregisterHotKey(_source.Handle, _activeId);
		}
		_registered = false;
	}

	public void Recover()
	{
		if (!_suspended && !_disposed)
		{
			Release();
			Initialize();
		}
	}

	public void Dispose()
	{
		if (_source != null && !_source.Dispatcher.CheckAccess())
		{
			_source.Dispatcher.Invoke(Dispose);
			return;
		}
		_disposed = true;
		Release();
		if (_source != null)
		{
			_source.RemoveHook(WndProc);
			_source.Dispose();
			_source = null;
		}
		_suspended = false;
	}

	public void Suspend()
	{
		try
		{
			if (!_suspended)
			{
				_suspended = true;
				Release();
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Hotkey suspend: " + ex.Message);
		}
	}

	public void Resume()
	{
		if (_suspended)
		{
			_suspended = false;
			AppPreferences instance = AppPreferences.Instance;
			if (instance.HotkeyKey != 0 && !_registered)
			{
				Apply(instance.HotkeyModifiers, instance.HotkeyKey);
			}
		}
	}

	public static string Validate(int modifiers, int key)
	{
		string text = Describe(modifiers, key);
		bool flag = (modifiers & 2) != 0;
		bool flag2 = (modifiers & 1) != 0;
		bool flag3 = (modifiers & 4) != 0;
		bool flag4 = (modifiers & 8) != 0;
		if (!flag && !flag2 && !flag3 && !flag4)
		{
			return "Add Ctrl, Alt, Shift or Win to the key.";
		}
		if (((uint)modifiers & 0xFFFFFFF0u) != 0 || key == 118 || key == 119 || key == 120 || key == 121 || key == 116 || key == 117 || key == 70 || key == 71)
		{
			return "Add a normal key to the modifier.";
		}
		int num;
		try
		{
			num = KeyInterop.VirtualKeyFromKey((Key)key);
		}
		catch
		{
			num = 0;
		}
		if (num == 0)
		{
			return "That key can't be used in a shortcut. Try a letter, number or Space.";
		}
		if (key == 101)
		{
			return "Windows reserves F12 for debuggers. Try another key.";
		}
		bool flag5 = (key >= 44 && key <= 69) || (key >= 34 && key <= 43) || (key >= 74 && key <= 83);
		bool flag6 = key >= 90 && key <= 113;
		if (flag2 && !flag && !flag4 && (key == 3 || key == 13 || key == 93))
		{
			return text + " is used by Windows to switch and close windows.";
		}
		if (flag && flag2 && !flag4 && (key == 3 || key == 32 || key == 21))
		{
			return text + " is reserved by Windows.";
		}
		if (flag && !flag2 && !flag4 && key == 13)
		{
			return text + " opens Start or Task Manager.";
		}
		if ((flag2 && key == 30) || (flag && key == 30))
		{
			return text + " is used by Windows for screenshots.";
		}
		if (flag4 && !flag6)
		{
			return "Windows uses Win + " + KeyName((Key)key) + " shortcuts. Try Ctrl + Alt + a key instead.";
		}
		if (flag4 && key == 90)
		{
			return text + " opens Windows help.";
		}
		if (flag && !flag2 && !flag4 && key == 18)
		{
			return text + " switches input languages. Try Alt + Space.";
		}
		if (flag && !flag2 && !flag4 && !flag3 && (flag5 || key == 3 || key == 6 || key == 2 || key == 32 || key == 31 || key == 22 || key == 21 || key == 19 || key == 20 || key == 23 || key == 25 || key == 24 || key == 26 || key == 141 || key == 143 || key == 93))
		{
			return text + " is used by apps everywhere (copy, save, tabs…). Add Alt or Shift.";
		}
		if (flag2 && !flag && !flag4 && !flag3 && flag5)
		{
			return text + " opens app menus. Add Ctrl or Shift.";
		}
		if (flag2 && !flag && !flag4 && (key == 23 || key == 25 || key == 6))
		{
			return text + " is used by apps for back, forward and full screen.";
		}
		if (flag && flag2 && flag3 && !flag4 && (key == 47 || key == 49))
		{
			return text + " is already used by Ayn.";
		}
		return null;
	}

	public static string Describe(int modifiers, int key)
	{
		if (key == 0)
		{
			return "Not set";
		}
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
		list.Add(KeyName((Key)key));
		return string.Join(" + ", list);
	}

	public static string KeyName(Key k)
	{
		if (k >= Key.D0 && k <= Key.D9)
		{
			return ((int)(k - 34)).ToString();
		}
		if (k >= Key.NumPad0 && k <= Key.NumPad9)
		{
			return "Num " + (int)(k - 74);
		}
		return k switch
		{
			Key.Space => "Space", 
			Key.Return => "Enter", 
			Key.OemPlus => "=", 
			Key.OemMinus => "-", 
			Key.OemComma => ",", 
			Key.OemPeriod => ".", 
			Key.Oem1 => ";", 
			Key.Oem2 => "/", 
			Key.Oem3 => "`", 
			Key.Oem4 => "[", 
			Key.Oem5 => "\\", 
			Key.Oem6 => "]", 
			Key.Oem7 => "'", 
			Key.Back => "Backspace", 
			_ => k.ToString(), 
		};
	}
}
