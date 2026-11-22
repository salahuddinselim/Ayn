using System;
using System.Runtime.InteropServices;
using System.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services.CoreAudio;

public sealed class AudioSpectrum
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
	private interface IAudioClient
	{
		[PreserveSig]
		int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);

		[PreserveSig]
		int GetBufferSize(out uint frames);

		[PreserveSig]
		int GetStreamLatency(out long latency);

		[PreserveSig]
		int GetCurrentPadding(out uint padding);

		[PreserveSig]
		int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);

		[PreserveSig]
		int GetMixFormat(out IntPtr format);

		[PreserveSig]
		int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

		[PreserveSig]
		int Start();

		[PreserveSig]
		int Stop();

		[PreserveSig]
		int Reset();

		[PreserveSig]
		int SetEventHandle(IntPtr handle);

		[PreserveSig]
		int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
	}

	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
	private interface IAudioCaptureClient
	{
		[PreserveSig]
		int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);

		[PreserveSig]
		int ReleaseBuffer(uint frames);

		[PreserveSig]
		int GetNextPacketSize(out uint frames);
	}

	private sealed class Capture : IDisposable
	{
		public object Enumerator;

		public object Device;

		public IAudioClient Client;

		public IAudioCaptureClient Reader;

		public int Channels;

		public int Bits;

		public int Rate;

		public bool Float;

		public void Dispose()
		{
			try
			{
				if (Client != null)
				{
					Client.Stop();
				}
			}
			catch
			{
			}
			object[] array = new object[4] { Reader, Client, Device, Enumerator };
			foreach (object obj2 in array)
			{
				if (obj2 != null)
				{
					try
					{
						Marshal.ReleaseComObject(obj2);
					}
					catch
					{
					}
				}
			}
			Reader = null;
			Client = null;
			Device = null;
			Enumerator = null;
		}
	}

	public const int Bands = 28;

	private const int FftSize = 2048;

	private const int Ring = 4096;

	private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 131072u;

	private const uint AUDCLNT_BUFFERFLAGS_SILENT = 2u;

	private const int CLSCTX_ALL = 23;

	public static int FrameMs = 33;

	private static readonly Lazy<AudioSpectrum> _instance = new Lazy<AudioSpectrum>(() => new AudioSpectrum());

	private long _cpu100ns;

	private long _cpuBase;

	private readonly object _lock = new object();

	private readonly AutoResetEvent _wake = new AutoResetEvent(initialState: false);

	private Thread _thread;

	private volatile bool _run;

	private volatile bool _rebind;

	private volatile bool _expectSound;

	private readonly float[] _levels = new float[28];

	private float _glow;

	private bool _live;

	private long _frames;

	private readonly float[] _ring = new float[4096];

	private int _ringPos;

	private readonly float[] _re = new float[2048];

	private readonly float[] _im = new float[2048];

	private readonly float[] _window = new float[2048];

	private readonly float[] _target = new float[28];

	private readonly float[] _smooth = new float[28];

	private float _peakDb = -30f;

	private float _bassAvg;

	private float _glowNow;

	private DateTime _lastSound = DateTime.MinValue;

	private int _respawns;

	private byte[] _packet = new byte[8192];

	public static AudioSpectrum Instance => _instance.Value;

	public TimeSpan CaptureCpu => TimeSpan.FromTicks(Interlocked.Read(ref _cpu100ns));

	public bool ExpectSound
	{
		get
		{
			return _expectSound;
		}
		set
		{
			_expectSound = value;
		}
	}

	public bool IsRunning => _run;

	public bool IsLive
	{
		get
		{
			lock (_lock)
			{
				return _live;
			}
		}
	}

	public long FrameCount
	{
		get
		{
			lock (_lock)
			{
				return _frames;
			}
		}
	}

	public int Binds { get; private set; }

	public string LastError { get; private set; }

	public event Action FrameReady;

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentThread();

	[DllImport("kernel32.dll")]
	private static extern bool GetThreadTimes(IntPtr h, out long create, out long exit, out long kernel, out long user);

	private AudioSpectrum()
	{
		try
		{
			CoreAudioService.Instance.DefaultEndpointChanged += delegate
			{
				_rebind = true;
				_wake.Set();
			};
		}
		catch
		{
		}
	}

	public float Read(float[] into, out bool live)
	{
		lock (_lock)
		{
			Array.Copy(_levels, into, Math.Min(into.Length, 28));
			live = _live;
			return _glow;
		}
	}

	public void Start()
	{
		lock (_lock)
		{
			if (!_run)
			{
				_run = true;
				Thread thread = _thread;
				if (thread != null && thread.IsAlive)
				{
					_wake.Set();
					return;
				}
				_respawns = 0;
				StartThreadLocked();
			}
		}
	}

	public void Stop()
	{
		lock (_lock)
		{
			_run = false;
			Array.Clear(_levels, 0, 28);
			_glow = 0f;
			_live = false;
		}
		_wake.Set();
	}

	public void Rebind()
	{
		_rebind = true;
		_wake.Set();
	}

	private void Loop()
	{
		_cpuBase = Interlocked.Read(ref _cpu100ns);
		for (int i = 0; i < 2048; i++)
		{
			_window[i] = (float)(0.5 - 0.5 * Math.Cos(Math.PI * 2.0 * (double)i / 2047.0));
		}
		Capture capture = null;
		int num = 0;
		DateTime dateTime = DateTime.MinValue;
		try
		{
			while (_run)
			{
				if (_rebind)
				{
					_rebind = false;
					if (capture != null)
					{
						capture.Dispose();
						capture = null;
					}
					num = 0;
					dateTime = DateTime.MinValue;
				}
				if (capture == null && DateTime.UtcNow >= dateTime)
				{
					try
					{
						capture = Open();
						num = 0;
						_lastSound = DateTime.MinValue;
						Binds++;
						LastError = null;
					}
					catch (Exception ex)
					{
						capture = null;
						num++;
						LastError = ex.GetType().Name + ": " + ex.Message;
						dateTime = DateTime.UtcNow.AddMilliseconds((num < 3) ? 500 : 5000);
						if (num == 1 || num == 3)
						{
							TransactionLogger.Warn("[Beat] loopback unavailable, using still line: " + LastError);
						}
					}
				}
				bool flag = false;
				if (capture != null)
				{
					try
					{
						flag = Drain(capture);
					}
					catch (Exception ex2)
					{
						LastError = ex2.GetType().Name + ": " + ex2.Message;
						capture.Dispose();
						capture = null;
						dateTime = DateTime.UtcNow.AddMilliseconds(300.0);
					}
				}
				if (flag)
				{
					_lastSound = DateTime.UtcNow;
				}
				bool flag2 = capture != null && (DateTime.UtcNow - _lastSound).TotalMilliseconds < 200.0;
				if (flag2)
				{
					Analyze(capture.Rate);
				}
				else
				{
					Array.Clear(_smooth, 0, 28);
					_glowNow = 0f;
				}
				Publish(flag2);
				if (GetThreadTimes(GetCurrentThread(), out var _, out var _, out var kernel, out var user))
				{
					Interlocked.Exchange(ref _cpu100ns, _cpuBase + kernel + user);
				}
				_wake.WaitOne(FrameMs);
			}
		}
		catch (Exception ex3)
		{
			TransactionLogger.Warn("[Beat] stopped after error: " + ex3.GetType().Name + ": " + ex3.Message);
		}
		finally
		{
			capture?.Dispose();
			Array.Clear(_ring, 0, 4096);
			Array.Clear(_packet, 0, _packet.Length);
			lock (_lock)
			{
				Array.Clear(_levels, 0, 28);
				_glow = 0f;
				_live = false;
				_thread = null;
				if (_run)
				{
					if (++_respawns < 20)
					{
						StartThreadLocked();
					}
					else
					{
						_run = false;
					}
				}
			}
		}
	}

	private void StartThreadLocked()
	{
		_thread = new Thread(Loop)
		{
			IsBackground = true,
			Name = "Ayn.Beat",
			Priority = ThreadPriority.BelowNormal
		};
		_thread.SetApartmentState(ApartmentState.MTA);
		_thread.Start();
	}

	private Capture Open()
	{
		Capture capture = new Capture();
		try
		{
			capture.Enumerator = new MMDeviceEnumeratorComObject();
			int defaultAudioEndpoint = ((IMMDeviceEnumerator)capture.Enumerator).GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var ppEndpoint);
			if (defaultAudioEndpoint != 0 || ppEndpoint == null)
			{
				throw new InvalidOperationException("no default output (0x" + defaultAudioEndpoint.ToString("X8") + ")");
			}
			capture.Device = ppEndpoint;
			Guid iid = typeof(IAudioClient).GUID;
			defaultAudioEndpoint = ppEndpoint.Activate(ref iid, 23, IntPtr.Zero, out var ppInterface);
			if (defaultAudioEndpoint != 0 || ppInterface == IntPtr.Zero)
			{
				throw new InvalidOperationException("activate 0x" + defaultAudioEndpoint.ToString("X8"));
			}
			capture.Client = (IAudioClient)Marshal.GetObjectForIUnknown(ppInterface);
			Marshal.Release(ppInterface);
			Check(capture.Client.GetMixFormat(out var format), "mix format");
			try
			{
				int num = Marshal.ReadInt16(format, 0) & 0xFFFF;
				capture.Channels = Marshal.ReadInt16(format, 2);
				capture.Rate = Marshal.ReadInt32(format, 4);
				capture.Bits = Marshal.ReadInt16(format, 14);
				capture.Float = num == 3;
				if (num == 65534)
				{
					byte[] array = new byte[16];
					Marshal.Copy(format + 24, array, 0, 16);
					capture.Float = new Guid(array) == new Guid("00000003-0000-0010-8000-00aa00389b71");
				}
				if (capture.Channels < 1 || capture.Rate < 8000 || (capture.Bits != 16 && capture.Bits != 24 && capture.Bits != 32))
				{
					throw new NotSupportedException("format " + num + "/" + capture.Bits);
				}
				Check(capture.Client.Initialize(0, 131072u, 10000000L, 0L, format, IntPtr.Zero), "initialize");
			}
			finally
			{
				Marshal.FreeCoTaskMem(format);
			}
			Guid iid2 = typeof(IAudioCaptureClient).GUID;
			Check(capture.Client.GetService(ref iid2, out var service), "capture service");
			capture.Reader = (IAudioCaptureClient)service;
			Check(capture.Client.Start(), "start");
			return capture;
		}
		catch
		{
			capture.Dispose();
			throw;
		}
	}

	private static void Check(int hr, string what)
	{
		if (hr < 0)
		{
			throw new COMException(what + " 0x" + hr.ToString("X8"), hr);
		}
	}

	private bool Drain(Capture cap)
	{
		bool result = false;
		int num = cap.Bits / 8;
		int num2 = num * cap.Channels;
		for (int i = 0; i < 64; i++)
		{
			Check(cap.Reader.GetNextPacketSize(out var frames), "packet size");
			if (frames == 0)
			{
				break;
			}
			Check(cap.Reader.GetBuffer(out var data, out var frames2, out var flags, out var _, out var _), "get buffer");
			try
			{
				bool flag = (flags & 2u) != 0 || data == IntPtr.Zero;
				int num3 = (int)frames2 * num2;
				if (!flag)
				{
					if (_packet.Length < num3)
					{
						_packet = new byte[num3];
					}
					Marshal.Copy(data, _packet, 0, num3);
				}
				int num4 = Math.Min(2, cap.Channels);
				for (int j = 0; j < frames2; j++)
				{
					float num5 = 0f;
					if (!flag)
					{
						int num6 = j * num2;
						int num7 = 0;
						while (num7 < num4)
						{
							num5 += Sample(_packet, num6, cap.Bits, cap.Float);
							num7++;
							num6 += num;
						}
						num5 /= (float)num4;
						if (num5 > 0.0005f || num5 < -0.0005f)
						{
							result = true;
						}
					}
					_ring[_ringPos] = num5;
					_ringPos = (_ringPos + 1) % 4096;
				}
			}
			finally
			{
				cap.Reader.ReleaseBuffer(frames2);
			}
		}
		return result;
	}

	private static float Sample(byte[] b, int o, int bits, bool isFloat)
	{
		if (isFloat && bits == 32)
		{
			return BitConverter.ToSingle(b, o);
		}
		return bits switch
		{
			16 => (float)BitConverter.ToInt16(b, o) / 32768f, 
			24 => (float)(((b[o] << 8) | (b[o + 1] << 16) | (b[o + 2] << 24)) >> 8) / 8388608f, 
			_ => (float)BitConverter.ToInt32(b, o) / 2.1474836E+09f, 
		};
	}

	private void Analyze(int rate)
	{
		int num = (_ringPos - 2048 + 4096) % 4096;
		for (int i = 0; i < 2048; i++)
		{
			_re[i] = _ring[(num + i) % 4096] * _window[i];
			_im[i] = 0f;
		}
		Fft(_re, _im);
		double num2 = (double)rate / 2048.0;
		double num3 = 40.0;
		double num4 = Math.Min(12000.0, (double)rate / 2.2);
		float num5 = -120f;
		for (int j = 0; j < 28; j++)
		{
			double num6 = num3 * Math.Pow(num4 / num3, (double)j / 28.0);
			double num7 = num3 * Math.Pow(num4 / num3, (double)(j + 1) / 28.0);
			int num8 = Math.Max(1, (int)Math.Floor(num6 / num2));
			int num9 = Math.Max(num8, (int)Math.Ceiling(num7 / num2) - 1);
			double num10 = 0.0;
			int num11 = 0;
			int num12 = num8;
			while (num12 <= num9 && num12 < 1024)
			{
				num10 += (double)(_re[num12] * _re[num12] + _im[num12] * _im[num12]);
				num12++;
				num11++;
			}
			double num13 = Math.Sqrt(num10 / (double)Math.Max(1, num11)) / 512.0;
			double num14 = 20.0 * Math.Log10(num13 + 1E-09) + 3.0 * Math.Log(Math.Max(1.0, (num6 + num7) / 2.0 / 250.0), 2.0);
			num5 = Math.Max(num5, (float)num14);
			_target[j] = (float)num14;
		}
		_peakDb = ((num5 > _peakDb) ? num5 : Math.Max(-40f, _peakDb - 0.12f));
		float num15 = Math.Max(_peakDb, -32f);
		float num16 = 42f;
		for (int k = 0; k < 28; k++)
		{
			float num17 = Math.Max(0f, Math.Min(1f, (_target[k] - (num15 - num16)) / num16));
			num17 = num17 * num17 * (3f - 2f * num17);
			_smooth[k] += (num17 - _smooth[k]) * ((num17 > _smooth[k]) ? 0.6f : 0.2f);
		}
		float num18 = (_smooth[0] + _smooth[1] + _smooth[2] + _smooth[3] + _smooth[4]) / 5f;
		float num19 = Math.Max(0f, num18 - _bassAvg);
		_bassAvg += (num18 - _bassAvg) * 0.08f;
		float num20 = Math.Min(1f, num18 * num18 * 0.8f + num19 * 2.2f);
		_glowNow = ((num20 > _glowNow) ? num20 : (_glowNow * 0.86f));
	}

	private void Publish(bool live)
	{
		lock (_lock)
		{
			if (!_run)
			{
				return;
			}
			for (int i = 0; i < 28; i++)
			{
				float num = _smooth[Math.Max(0, i - 1)];
				float num2 = _smooth[Math.Min(27, i + 1)];
				_levels[i] = 0.2f * num + 0.6f * _smooth[i] + 0.2f * num2;
			}
			_glow = (live ? _glowNow : 0f);
			_live = live;
			_frames++;
		}
		Action frameReady = this.FrameReady;
		if (frameReady != null)
		{
			try
			{
				frameReady();
			}
			catch
			{
			}
		}
	}

	private static void Fft(float[] re, float[] im)
	{
		int num = re.Length;
		int i = 1;
		int num2 = 0;
		for (; i < num; i++)
		{
			int num3 = num >> 1;
			while ((num2 & num3) != 0)
			{
				num2 ^= num3;
				num3 >>= 1;
			}
			num2 ^= num3;
			if (i < num2)
			{
				float num4 = re[i];
				re[i] = re[num2];
				re[num2] = num4;
				num4 = im[i];
				im[i] = im[num2];
				im[num2] = num4;
			}
		}
		for (int num5 = 2; num5 <= num; num5 <<= 1)
		{
			double num6 = Math.PI * -2.0 / (double)num5;
			float num7 = (float)Math.Cos(num6);
			float num8 = (float)Math.Sin(num6);
			for (int j = 0; j < num; j += num5)
			{
				float num9 = 1f;
				float num10 = 0f;
				for (int k = 0; k < num5 / 2; k++)
				{
					int num11 = j + k;
					int num12 = num11 + num5 / 2;
					float num13 = re[num12] * num9 - im[num12] * num10;
					float num14 = re[num12] * num10 + im[num12] * num9;
					re[num12] = re[num11] - num13;
					im[num12] = im[num11] - num14;
					re[num11] += num13;
					im[num11] += num14;
					float num15 = num9 * num7 - num10 * num8;
					num10 = num9 * num8 + num10 * num7;
					num9 = num15;
				}
			}
		}
	}
}
