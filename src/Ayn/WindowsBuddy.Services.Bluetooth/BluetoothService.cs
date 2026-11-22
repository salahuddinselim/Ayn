using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services.CoreAudio;

namespace WindowsBuddy.Services.Bluetooth;

public sealed class BluetoothService : IDisposable
{
	private static readonly Lazy<BluetoothService> _instance;

	private string _previousAudioEndpoint;

	private bool _previousIsBluetooth;

	private bool _isInitialized;

	private static readonly Brush BluetoothCyanAccent;

	private static readonly Brush NeutralDisconnectAccent;

	public static BluetoothService Instance => _instance.Value;

	public bool IsExternalAudioConnected
	{
		get
		{
			try
			{
				return CoreAudioService.Instance.CurrentDeviceIsBluetooth;
			}
			catch
			{
				return false;
			}
		}
	}

	static BluetoothService()
	{
		_instance = new Lazy<BluetoothService>(() => new BluetoothService());
		BluetoothCyanAccent = new SolidColorBrush(Color.FromRgb(56, 189, 248));
		NeutralDisconnectAccent = new SolidColorBrush(Color.FromRgb(148, 163, 184));
		BluetoothCyanAccent.Freeze();
		NeutralDisconnectAccent.Freeze();
	}

	private BluetoothService()
	{
		Initialize();
	}

	public void Initialize()
	{
		if (_isInitialized)
		{
			return;
		}
		try
		{
			CoreAudioService instance = CoreAudioService.Instance;
			_previousAudioEndpoint = instance.CurrentDeviceName;
			_previousIsBluetooth = instance.CurrentDeviceIsBluetooth;
			instance.DefaultEndpointChanged += OnDefaultEndpointChanged;
			_isInitialized = true;
		}
		catch
		{
		}
	}

	private void OnDefaultEndpointChanged(object sender, AudioEndpointChangedEventArgs e)
	{
		string previousAudioEndpoint = _previousAudioEndpoint;
		bool previousIsBluetooth = _previousIsBluetooth;
		string endpointName = e.EndpointName;
		bool flag = false;
		try
		{
			flag = CoreAudioService.Instance.CurrentDeviceIsBluetooth;
		}
		catch
		{
		}
		_previousAudioEndpoint = endpointName;
		_previousIsBluetooth = flag;
		if (!string.IsNullOrWhiteSpace(endpointName) && (!string.Equals(previousAudioEndpoint, endpointName, StringComparison.OrdinalIgnoreCase) || previousIsBluetooth != flag))
		{
			if (flag)
			{
				PublishDeviceEvent(endpointName, connected: true);
			}
			else if (previousIsBluetooth && !string.IsNullOrWhiteSpace(previousAudioEndpoint))
			{
				PublishDeviceEvent(previousAudioEndpoint, connected: false);
			}
		}
	}

	public void PublishDeviceEvent(string rawDeviceName, bool connected)
	{
		string text = SanitizeDeviceName(rawDeviceName);
		Func<double, Brush, UIElement> iconFactory = ClassifyDevice(text) switch
		{
			DeviceCategory.Earbuds => (double sz, Brush br) => VectorIcons.CreateEarbudsIcon(sz, br), 
			DeviceCategory.Headphones => (double sz, Brush br) => VectorIcons.CreateHeadphonesIcon(sz, br), 
			DeviceCategory.Speaker => (double sz, Brush br) => VectorIcons.CreateVolumeIcon(sz, br, isMuted: false, 80), 
			DeviceCategory.Keyboard => (double sz, Brush br) => VectorIcons.CreateKeyboardIcon(sz, br), 
			DeviceCategory.Mouse => (double sz, Brush br) => VectorIcons.CreateMouseIcon(sz, br), 
			DeviceCategory.Controller => (double sz, Brush br) => VectorIcons.CreateControllerIcon(sz, br), 
			_ => (double sz, Brush br) => VectorIcons.CreateBluetoothIcon(sz, br), 
		};
		NotchActivity notchActivity = new NotchActivity();
		notchActivity.Id = "bt_" + text.ToLowerInvariant();
		notchActivity.Type = (connected ? ActivityType.BluetoothConnected : ActivityType.BluetoothDisconnected);
		notchActivity.Priority = ActivityPriority.P3_Transient;
		notchActivity.Mode = ActivityMode.Temporary;
		notchActivity.Duration = TimeSpan.FromSeconds(3.0);
		notchActivity.Title = text;
		notchActivity.Subtitle = (connected ? "Connected" : "Disconnected");
		notchActivity.IconFactory = iconFactory;
		notchActivity.AccentColor = (connected ? BuddyTokens.TextPrimaryBrush : BuddyTokens.DangerRedBrush);
		notchActivity.NavigateToToolId = "volume";
		NotchActivity activity = notchActivity;
		DynamicActivityService.Instance.PublishActivity(activity);
	}

	private static bool IsInternalSpeaker(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return true;
		}
		string text = name.ToLowerInvariant();
		if (!text.Contains("realtek") && !text.Contains("internal") && !text.Contains("speakers (high definition") && !text.Contains("default audio"))
		{
			return text.Contains("built-in");
		}
		return true;
	}

	public static string SanitizeDeviceName(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return "Bluetooth Device";
		}
		string input = raw;
		input = Regex.Replace(input, "\\s*\\(.*?\\)", "");
		input = Regex.Replace(input, "\\s*Hands-Free.*", "", RegexOptions.IgnoreCase);
		input = Regex.Replace(input, "\\s*Stereo.*", "", RegexOptions.IgnoreCase);
		input = Regex.Replace(input, "\\s*AG Audio.*", "", RegexOptions.IgnoreCase);
		input = Regex.Replace(input, "\\s*Bluetooth\\s+Audio.*", "", RegexOptions.IgnoreCase);
		if (input.Trim().Length > 9 && input.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) > 0)
		{
			input = Regex.Replace(input, "\\s*Bluetooth.*", "", RegexOptions.IgnoreCase);
		}
		input = input.Trim();
		if (input.Length > 24)
		{
			input = input.Substring(0, 22).TrimEnd() + "…";
		}
		if (!string.IsNullOrWhiteSpace(input))
		{
			return input;
		}
		return "Audio Device";
	}

	public static DeviceCategory ClassifyDevice(string name)
	{
		string text = name.ToLowerInvariant();
		if (text.Contains("airpod") || text.Contains("buds") || text.Contains("earbud") || text.Contains("earphone"))
		{
			return DeviceCategory.Earbuds;
		}
		if (text.Contains("wh-1000") || text.Contains("headphone") || text.Contains("headset") || text.Contains("bose") || text.Contains("beats") || text.Contains("qc35") || text.Contains("qc45") || text.Contains("sony"))
		{
			return DeviceCategory.Headphones;
		}
		if (text.Contains("speaker") || text.Contains("soundbar") || text.Contains("echo") || text.Contains("jbl") || text.Contains("flip") || text.Contains("charge") || text.Contains("soundlink") || text.Contains("harman"))
		{
			return DeviceCategory.Speaker;
		}
		if (text.Contains("keyboard") || text.Contains("keychron") || text.Contains("mx keys") || text.Contains("logi k"))
		{
			return DeviceCategory.Keyboard;
		}
		if (text.Contains("mouse") || text.Contains("trackpad") || text.Contains("mx master") || text.Contains("anywhere"))
		{
			return DeviceCategory.Mouse;
		}
		if (text.Contains("controller") || text.Contains("xbox") || text.Contains("dualsense") || text.Contains("playstation") || text.Contains("gamepad"))
		{
			return DeviceCategory.Controller;
		}
		return DeviceCategory.GenericBluetooth;
	}

	public void Dispose()
	{
		try
		{
			CoreAudioService.Instance.DefaultEndpointChanged -= OnDefaultEndpointChanged;
		}
		catch
		{
		}
	}
}
