using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services.CoreAudio;

public sealed class CoreAudioService : IAudioEndpointVolumeCallback, IMMNotificationClient, IDisposable
{
	private static readonly Lazy<CoreAudioService> _instance = new Lazy<CoreAudioService>(() => new CoreAudioService());

	public static readonly Guid BuddyVolumeContext = new Guid("4A91B872-5C13-4E89-9F31-187532A183D5");

	private readonly object _syncLock = new object();

	private IMMDeviceEnumerator _deviceEnumerator;

	private IMMDevice _currentDevice;

	private IAudioEndpointVolume _endpointVolume;

	private IAudioMeterInformation _audioMeter;

	private string _currentDeviceFriendlyName = "Speakers";

	private string _currentDeviceId;

	private bool _isInitialized;

	private int _endpointChangePending;

	private bool _currentDeviceIsBluetooth;

	private int _initInProgress;

	internal static Action SlowInitForQa;

	public static CoreAudioService Instance => _instance.Value;

	public string CurrentDeviceName
	{
		get
		{
			if (_initInProgress != 0)
			{
				return _currentDeviceFriendlyName;
			}
			lock (_syncLock)
			{
				return _currentDeviceFriendlyName;
			}
		}
	}

	public bool CurrentDeviceIsBluetooth
	{
		get
		{
			if (_initInProgress != 0)
			{
				return false;
			}
			lock (_syncLock)
			{
				return _currentDeviceIsBluetooth;
			}
		}
	}

	public string CurrentDeviceId
	{
		get
		{
			if (_initInProgress != 0)
			{
				return null;
			}
			lock (_syncLock)
			{
				return _currentDeviceId;
			}
		}
	}

	public bool HasEndpoint
	{
		get
		{
			lock (_syncLock)
			{
				return _endpointVolume != null;
			}
		}
	}

	public event EventHandler<AudioVolumeChangedEventArgs> VolumeChanged;

	public event EventHandler<AudioEndpointChangedEventArgs> DefaultEndpointChanged;

	internal static event Action InitialBindReady;

	public List<AudioOutputDevice> GetOutputDevices()
	{
		List<AudioOutputDevice> list = new List<AudioOutputDevice>();
		if (_initInProgress != 0)
		{
			return list;
		}
		lock (_syncLock)
		{
			if (_deviceEnumerator == null)
			{
				return list;
			}
			IMMDeviceCollection ppDevices = null;
			try
			{
				if (_deviceEnumerator.EnumAudioEndpoints(EDataFlow.eRender, 1, out ppDevices) != 0 || ppDevices == null)
				{
					return list;
				}
				ppDevices.GetCount(out var pcDevices);
				for (uint num = 0u; num < pcDevices; num++)
				{
					if (ppDevices.Item(num, out var ppDevice) != 0 || ppDevice == null)
					{
						continue;
					}
					try
					{
						string text = QueryDeviceId(ppDevice);
						if (!string.IsNullOrEmpty(text))
						{
							list.Add(new AudioOutputDevice
							{
								Id = text,
								Name = QueryDeviceFriendlyName(ppDevice),
								IsDefault = string.Equals(text, _currentDeviceId, StringComparison.OrdinalIgnoreCase)
							});
						}
					}
					finally
					{
						Marshal.ReleaseComObject(ppDevice);
					}
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[CoreAudioService] GetOutputDevices: " + ex.Message);
			}
			finally
			{
				if (ppDevices != null)
				{
					Marshal.ReleaseComObject(ppDevices);
				}
			}
		}
		return list;
	}

	public bool SetDefaultOutputDevice(string deviceId)
	{
		if (string.IsNullOrEmpty(deviceId))
		{
			return false;
		}
		object obj = null;
		try
		{
			obj = new PolicyConfigClientComObject();
			if (!(obj is IPolicyConfig policyConfig))
			{
				return false;
			}
			int num = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
			if (num == 0)
			{
				num = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
			}
			if (num == 0)
			{
				num = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
			}
			if (num != 0)
			{
				TransactionLogger.Warn($"[CoreAudioService] SetDefaultEndpoint hr=0x{num:X8}");
			}
			return num == 0;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[CoreAudioService] SetDefaultOutputDevice: " + ex.Message);
			return false;
		}
		finally
		{
			if (obj != null)
			{
				try
				{
					Marshal.ReleaseComObject(obj);
				}
				catch
				{
				}
			}
		}
	}

	private CoreAudioService()
	{
		_initInProgress = 1;
		ThreadStart start = delegate
		{
			IDisposable disposable = BootTimeline.Service("audio (Core Audio)");
			try
			{
				Initialize();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[CoreAudioService] start-up initialisation failed: " + ex.Message);
			}
			finally
			{
				disposable.Dispose();
				Interlocked.Exchange(ref _initInProgress, 0);
				Action initialBindReady = CoreAudioService.InitialBindReady;
				if (initialBindReady != null)
				{
					try
					{
						initialBindReady();
					}
					catch
					{
					}
				}
			}
		};
		Thread thread = new Thread(start)
		{
			IsBackground = true,
			Name = "Core Audio (start-up)"
		};
		thread.SetApartmentState(ApartmentState.MTA);
		thread.Start();
	}

	public void Initialize()
	{
		SlowInitForQa?.Invoke();
		lock (_syncLock)
		{
			if (_isInitialized)
			{
				return;
			}
			try
			{
				TransactionLogger.Info("[CoreAudioService] Initializing MMDeviceEnumerator COM co-class...");
				_deviceEnumerator = new MMDeviceEnumeratorComObject() as IMMDeviceEnumerator;
				if (_deviceEnumerator != null)
				{
					int num = _deviceEnumerator.RegisterEndpointNotificationCallback(this);
					TransactionLogger.Info($"[CoreAudioService] Registered IMMNotificationClient: hr=0x{num:X8}");
					BindDefaultEndpointInternal();
					_isInitialized = true;
				}
				else
				{
					TransactionLogger.Warn("[CoreAudioService] Failed to create MMDeviceEnumerator COM object.");
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[CoreAudioService] Initialize exception: " + ex.Message);
			}
		}
	}

	private void BindDefaultEndpointInternal()
	{
		ReleaseCurrentEndpointInternal();
		if (_deviceEnumerator == null)
		{
			_currentDeviceFriendlyName = "No audio device";
			TransactionLogger.Warn("[CoreAudioService] BindDefaultEndpointInternal aborted: _deviceEnumerator is null.");
			return;
		}
		try
		{
			IMMDevice ppEndpoint = null;
			int num = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out ppEndpoint);
			if (num != 0 || ppEndpoint == null)
			{
				TransactionLogger.Warn($"[CoreAudioService] GetDefaultAudioEndpoint(eMultimedia) hr=0x{num:X8}. Trying eConsole fallback...");
				num = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out ppEndpoint);
			}
			if (num != 0 || ppEndpoint == null)
			{
				Thread.Sleep(50);
				num = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out ppEndpoint);
				if (num != 0 || ppEndpoint == null)
				{
					num = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out ppEndpoint);
				}
			}
			if (num != 0 || ppEndpoint == null)
			{
				TransactionLogger.Warn($"[CoreAudioService] GetDefaultAudioEndpoint failed (hr=0x{num:X8}). Checking EnumAudioEndpoints for active devices...");
				IMMDeviceCollection ppDevices = null;
				if (_deviceEnumerator.EnumAudioEndpoints(EDataFlow.eRender, 1, out ppDevices) == 0 && ppDevices != null)
				{
					uint pcDevices = 0u;
					ppDevices.GetCount(out pcDevices);
					TransactionLogger.Info($"[CoreAudioService] Active render endpoint count: {pcDevices}");
					if (pcDevices != 0)
					{
						num = ppDevices.Item(0u, out ppEndpoint);
						TransactionLogger.Info($"[CoreAudioService] Fallback to active render device 0: hr=0x{num:X8}, dev={ppEndpoint != null}");
					}
					Marshal.ReleaseComObject(ppDevices);
				}
			}
			if (num == 0 && ppEndpoint != null)
			{
				_currentDevice = ppEndpoint;
				_currentDeviceFriendlyName = QueryDeviceFriendlyName(ppEndpoint);
				_currentDeviceId = QueryDeviceId(ppEndpoint);
				_currentDeviceIsBluetooth = QueryIsBluetooth(ppEndpoint, _currentDeviceFriendlyName);
				Guid iid = typeof(IAudioEndpointVolume).GUID;
				IntPtr ppInterface = IntPtr.Zero;
				num = ppEndpoint.Activate(ref iid, 23, IntPtr.Zero, out ppInterface);
				if (num == 0 && ppInterface != IntPtr.Zero)
				{
					object objectForIUnknown = Marshal.GetObjectForIUnknown(ppInterface);
					Marshal.Release(ppInterface);
					_endpointVolume = objectForIUnknown as IAudioEndpointVolume;
					if (_endpointVolume != null)
					{
						int num2 = _endpointVolume.RegisterControlChangeNotify(this);
						TransactionLogger.Info($"[CoreAudioService] Bound IAudioEndpointVolume (bluetooth={_currentDeviceIsBluetooth}, hrNotify=0x{num2:X8})");
					}
				}
				else
				{
					TransactionLogger.Warn($"[CoreAudioService] Failed to activate IAudioEndpointVolume: hr=0x{num:X8}");
				}
				Guid iid2 = typeof(IAudioMeterInformation).GUID;
				IntPtr ppInterface2 = IntPtr.Zero;
				if (ppEndpoint.Activate(ref iid2, 23, IntPtr.Zero, out ppInterface2) == 0 && ppInterface2 != IntPtr.Zero)
				{
					object objectForIUnknown2 = Marshal.GetObjectForIUnknown(ppInterface2);
					Marshal.Release(ppInterface2);
					_audioMeter = objectForIUnknown2 as IAudioMeterInformation;
				}
			}
			else
			{
				_currentDevice = null;
				_endpointVolume = null;
				_audioMeter = null;
				_currentDeviceFriendlyName = "No audio device";
				TransactionLogger.Warn($"[CoreAudioService] No active render device available. Declared 'No audio device' state (final hr=0x{num:X8}).");
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("[CoreAudioService] BindDefaultEndpointInternal error: " + ex.Message);
			_currentDevice = null;
			_endpointVolume = null;
			_audioMeter = null;
			_currentDeviceFriendlyName = "No audio device";
		}
	}

	private void ReleaseCurrentEndpointInternal()
	{
		if (_endpointVolume != null)
		{
			try
			{
				_endpointVolume.UnregisterControlChangeNotify(this);
			}
			catch
			{
			}
			try
			{
				Marshal.ReleaseComObject(_endpointVolume);
			}
			catch
			{
			}
			_endpointVolume = null;
		}
		if (_audioMeter != null)
		{
			try
			{
				Marshal.ReleaseComObject(_audioMeter);
			}
			catch
			{
			}
			_audioMeter = null;
		}
		if (_currentDevice != null)
		{
			try
			{
				Marshal.ReleaseComObject(_currentDevice);
			}
			catch
			{
			}
			_currentDevice = null;
		}
		_currentDeviceId = null;
	}

	[DllImport("ole32.dll")]
	private static extern int PropVariantClear(ref PROPVARIANT pvar);

	private static string QueryDeviceId(IMMDevice dev)
	{
		if (dev == null)
		{
			return null;
		}
		IntPtr ppstrId = IntPtr.Zero;
		try
		{
			dev.GetId(out ppstrId);
			return (ppstrId != IntPtr.Zero) ? Marshal.PtrToStringUni(ppstrId) : null;
		}
		catch
		{
			return null;
		}
		finally
		{
			if (ppstrId != IntPtr.Zero)
			{
				Marshal.FreeCoTaskMem(ppstrId);
			}
		}
	}

	private static bool QueryIsBluetooth(IMMDevice dev, string friendlyName)
	{
		if (dev == null)
		{
			return false;
		}
		IPropertyStore ppProperties = null;
		try
		{
			if (dev.OpenPropertyStore(0, out ppProperties) == 0 && ppProperties != null)
			{
				PROPERTYKEY pROPERTYKEY = default(PROPERTYKEY);
				pROPERTYKEY.fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0");
				pROPERTYKEY.pid = 24u;
				PROPERTYKEY key = pROPERTYKEY;
				if (ppProperties.GetValue(ref key, out var pv) == 0)
				{
					try
					{
						string text = ((pv.pwszVal != IntPtr.Zero) ? Marshal.PtrToStringUni(pv.pwszVal) : null);
						if (!string.IsNullOrEmpty(text))
						{
							return text.StartsWith("BTH", StringComparison.OrdinalIgnoreCase);
						}
					}
					finally
					{
						PropVariantClear(ref pv);
					}
				}
			}
		}
		catch
		{
		}
		finally
		{
			if (ppProperties != null)
			{
				try
				{
					Marshal.ReleaseComObject(ppProperties);
				}
				catch
				{
				}
			}
		}
		if (friendlyName != null)
		{
			return friendlyName.IndexOf("Hands-Free", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	private string QueryDeviceFriendlyName(IMMDevice dev)
	{
		if (dev == null)
		{
			return "No audio device";
		}
		IPropertyStore ppProperties = null;
		try
		{
			if (dev.OpenPropertyStore(0, out ppProperties) == 0 && ppProperties != null)
			{
				PROPERTYKEY pROPERTYKEY = default(PROPERTYKEY);
				pROPERTYKEY.fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0");
				pROPERTYKEY.pid = 14u;
				PROPERTYKEY key = pROPERTYKEY;
				if (ppProperties.GetValue(ref key, out var pv) == 0)
				{
					try
					{
						if (pv.pwszVal != IntPtr.Zero)
						{
							string text = Marshal.PtrToStringUni(pv.pwszVal);
							if (!string.IsNullOrEmpty(text))
							{
								return text.Trim();
							}
						}
					}
					finally
					{
						PropVariantClear(ref pv);
					}
				}
			}
		}
		catch
		{
		}
		finally
		{
			if (ppProperties != null)
			{
				try
				{
					Marshal.ReleaseComObject(ppProperties);
				}
				catch
				{
				}
			}
		}
		return "Default output";
	}

	public float GetMasterVolumeScalar()
	{
		if (_initInProgress != 0)
		{
			return 0f;
		}
		lock (_syncLock)
		{
			if (_endpointVolume == null)
			{
				return 0f;
			}
			try
			{
				float pfLevel;
				switch (_endpointVolume.GetMasterVolumeLevelScalar(out pfLevel))
				{
				case 0:
					return Math.Max(0f, Math.Min(1f, pfLevel));
				case -2004287484:
					TransactionLogger.Warn("[CoreAudioService] GetMasterVolumeScalar: Device invalidated, rebinding...");
					BindDefaultEndpointInternal();
					if (_endpointVolume != null && _endpointVolume.GetMasterVolumeLevelScalar(out pfLevel) == 0)
					{
						return Math.Max(0f, Math.Min(1f, pfLevel));
					}
					break;
				}
			}
			catch
			{
			}
			return 0f;
		}
	}

	public int GetMasterVolumePercent()
	{
		if (_initInProgress != 0)
		{
			return -1;
		}
		lock (_syncLock)
		{
			if (_endpointVolume == null)
			{
				return -1;
			}
			try
			{
				float pfLevel = 0f;
				int masterVolumeLevelScalar = _endpointVolume.GetMasterVolumeLevelScalar(out pfLevel);
				switch (masterVolumeLevelScalar)
				{
				case 0:
					return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, pfLevel)) * 100.0);
				case -2004287484:
					TransactionLogger.Warn("[CoreAudioService] GetMasterVolumePercent: Device invalidated, rebinding...");
					BindDefaultEndpointInternal();
					if (_endpointVolume != null && _endpointVolume.GetMasterVolumeLevelScalar(out pfLevel) == 0)
					{
						return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, pfLevel)) * 100.0);
					}
					break;
				}
				TransactionLogger.Warn($"[CoreAudioService] GetMasterVolumePercent error: hr=0x{masterVolumeLevelScalar:X8}");
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[CoreAudioService] GetMasterVolumePercent exception: " + ex.Message);
			}
			return -1;
		}
	}

	public float GetMasterPeakValue()
	{
		if (_initInProgress != 0)
		{
			return 0f;
		}
		lock (_syncLock)
		{
			if (_audioMeter == null)
			{
				return 0f;
			}
			try
			{
				float pfPeak = 0f;
				if (_audioMeter.GetPeakValue(out pfPeak) == 0)
				{
					if (pfPeak < 0f)
					{
						return 0f;
					}
					if (pfPeak > 1f)
					{
						return 1f;
					}
					return pfPeak;
				}
			}
			catch
			{
			}
			return 0f;
		}
	}

	public bool GetMute()
	{
		if (_initInProgress != 0)
		{
			return false;
		}
		lock (_syncLock)
		{
			if (_endpointVolume != null)
			{
				try
				{
					bool pbMute;
					switch (_endpointVolume.GetMute(out pbMute))
					{
					case 0:
						return pbMute;
					case -2004287484:
						BindDefaultEndpointInternal();
						if (_endpointVolume != null && _endpointVolume.GetMute(out pbMute) == 0)
						{
							return pbMute;
						}
						break;
					}
				}
				catch
				{
				}
			}
			return false;
		}
	}

	public bool SetMasterVolumeScalar(float level)
	{
		lock (_syncLock)
		{
			if (_endpointVolume == null)
			{
				BindDefaultEndpointInternal();
				if (_endpointVolume == null)
				{
					return false;
				}
			}
			try
			{
				level = Math.Max(0f, Math.Min(1f, level));
				Guid pguidEventContext = BuddyVolumeContext;
				int num = _endpointVolume.SetMasterVolumeLevelScalar(level, ref pguidEventContext);
				if (num == -2004287484)
				{
					TransactionLogger.Warn("[CoreAudioService] SetMasterVolumeScalar: Device invalidated, rebinding...");
					BindDefaultEndpointInternal();
					if (_endpointVolume != null)
					{
						num = _endpointVolume.SetMasterVolumeLevelScalar(level, ref pguidEventContext);
					}
				}
				if (level > 0.001f && GetMute())
				{
					SetMute(mute: false);
				}
				return num == 0;
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[CoreAudioService] SetMasterVolumeScalar exception: " + ex.Message);
				return false;
			}
		}
	}

	public bool SetMasterVolumePercent(int percent)
	{
		float masterVolumeScalar = Math.Max(0f, Math.Min(100f, percent)) / 100f;
		return SetMasterVolumeScalar(masterVolumeScalar);
	}

	public bool SetMute(bool mute)
	{
		lock (_syncLock)
		{
			if (_endpointVolume == null)
			{
				BindDefaultEndpointInternal();
				if (_endpointVolume == null)
				{
					return false;
				}
			}
			try
			{
				Guid pguidEventContext = BuddyVolumeContext;
				int num = _endpointVolume.SetMute(mute, ref pguidEventContext);
				if (num == -2004287484)
				{
					BindDefaultEndpointInternal();
					if (_endpointVolume != null)
					{
						num = _endpointVolume.SetMute(mute, ref pguidEventContext);
					}
				}
				return num == 0;
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("[CoreAudioService] SetMute exception: " + ex.Message);
				return false;
			}
		}
	}

	public void ToggleMute()
	{
		bool mute = GetMute();
		SetMute(!mute);
	}

	int IAudioEndpointVolumeCallback.OnNotify(IntPtr pNotify)
	{
		if (pNotify == IntPtr.Zero)
		{
			return 0;
		}
		try
		{
			AUDIO_VOLUME_NOTIFICATION_DATA aUDIO_VOLUME_NOTIFICATION_DATA = (AUDIO_VOLUME_NOTIFICATION_DATA)Marshal.PtrToStructure(pNotify, typeof(AUDIO_VOLUME_NOTIFICATION_DATA));
			float scalar = aUDIO_VOLUME_NOTIFICATION_DATA.fMasterVolume;
			bool muted = aUDIO_VOLUME_NOTIFICATION_DATA.bMuted;
			Guid context = aUDIO_VOLUME_NOTIFICATION_DATA.guidEventContext;
			ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					this.VolumeChanged?.Invoke(this, new AudioVolumeChangedEventArgs(scalar, muted, context));
				}
				catch
				{
				}
			});
		}
		catch
		{
		}
		return 0;
	}

	private void HandleEndpointChangedAsync()
	{
		if (Interlocked.Exchange(ref _endpointChangePending, 1) != 0)
		{
			return;
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(40);
			while (Interlocked.Exchange(ref _endpointChangePending, 0) == 1)
			{
				Thread.Sleep(30);
			}
			string currentDeviceFriendlyName;
			int masterVolumePercent;
			bool mute;
			lock (_syncLock)
			{
				BindDefaultEndpointInternal();
				currentDeviceFriendlyName = _currentDeviceFriendlyName;
				masterVolumePercent = GetMasterVolumePercent();
				mute = GetMute();
			}
			TransactionLogger.Info(string.Format("[CoreAudioService] Endpoint change handled (device={0}, vol={1}%, mute={2})", (masterVolumePercent >= 0) ? "yes" : "none", masterVolumePercent, mute));
			try
			{
				this.DefaultEndpointChanged?.Invoke(this, new AudioEndpointChangedEventArgs(currentDeviceFriendlyName, masterVolumePercent, mute));
			}
			catch
			{
			}
			try
			{
				this.VolumeChanged?.Invoke(this, new AudioVolumeChangedEventArgs((masterVolumePercent >= 0) ? ((float)masterVolumePercent / 100f) : 0f, mute, Guid.Empty));
			}
			catch
			{
			}
		});
	}

	void IMMNotificationClient.OnDefaultDeviceChanged(EDataFlow flow, ERole role, string pwstrDefaultDeviceId)
	{
		if (flow == EDataFlow.eRender && (role == ERole.eMultimedia || role == ERole.eConsole))
		{
			TransactionLogger.Info($"[CoreAudioService] IMMNotificationClient.OnDefaultDeviceChanged(flow={flow}, role={role}, id={pwstrDefaultDeviceId})");
			HandleEndpointChangedAsync();
		}
	}

	void IMMNotificationClient.OnDeviceStateChanged(string pwstrDeviceId, int dwNewState)
	{
		TransactionLogger.Info($"[CoreAudioService] IMMNotificationClient.OnDeviceStateChanged(id={pwstrDeviceId}, state={dwNewState})");
		HandleEndpointChangedAsync();
	}

	void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId)
	{
		TransactionLogger.Info($"[CoreAudioService] IMMNotificationClient.OnDeviceAdded(id={pwstrDeviceId})");
		HandleEndpointChangedAsync();
	}

	void IMMNotificationClient.OnDeviceRemoved(string pwstrDeviceId)
	{
		TransactionLogger.Info($"[CoreAudioService] IMMNotificationClient.OnDeviceRemoved(id={pwstrDeviceId})");
		HandleEndpointChangedAsync();
	}

	void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PROPERTYKEY key)
	{
		PROPERTYKEY pROPERTYKEY = default(PROPERTYKEY);
		pROPERTYKEY.fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0");
		pROPERTYKEY.pid = 14u;
		PROPERTYKEY pROPERTYKEY2 = pROPERTYKEY;
		if (key.fmtid == pROPERTYKEY2.fmtid && key.pid == pROPERTYKEY2.pid)
		{
			TransactionLogger.Info($"[CoreAudioService] Friendly name property changed for id={pwstrDeviceId}");
			HandleEndpointChangedAsync();
		}
	}

	public void Dispose()
	{
		lock (_syncLock)
		{
			if (_deviceEnumerator != null)
			{
				try
				{
					_deviceEnumerator.UnregisterEndpointNotificationCallback(this);
				}
				catch
				{
				}
			}
			ReleaseCurrentEndpointInternal();
			if (_deviceEnumerator != null)
			{
				try
				{
					Marshal.ReleaseComObject(_deviceEnumerator);
				}
				catch
				{
				}
				_deviceEnumerator = null;
			}
			_isInitialized = false;
		}
	}
}
