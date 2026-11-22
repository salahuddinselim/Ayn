using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Sounds;

public class SoundMixerService : IDisposable
{
	private static SoundMixerService _instance;

	private readonly List<SoundChannel> _channels = new List<SoundChannel>();

	private readonly string _soundsDir;

	private readonly string _stateFilePath;

	private double _masterVolume = 0.8;

	public static SoundMixerService Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new SoundMixerService();
			}
			return _instance;
		}
	}

	public IReadOnlyList<SoundChannel> Channels => _channels;

	public double MasterVolume
	{
		get
		{
			return _masterVolume;
		}
		set
		{
			_masterVolume = Math.Max(0.0, Math.Min(1.0, value));
			UpdateAllVolumes();
			SaveState();
			NotifyStateChanged();
		}
	}

	public event EventHandler StateChanged;

	public SoundMixerService()
	{
		string localAppData = AppPaths.LocalAppData;
		_soundsDir = Path.Combine(localAppData, AppPaths.FolderName, "sounds");
		_stateFilePath = Path.Combine(_soundsDir, "mixer_state.json");
		if (!Directory.Exists(_soundsDir))
		{
			try
			{
				Directory.CreateDirectory(_soundsDir);
			}
			catch
			{
			}
		}
		InitializeChannels();
		LoadState();
	}

	private void InitializeChannels()
	{
		var array = new[]
		{
			new
			{
				Id = "rain",
				Name = "Rain",
				Icon = "\ud83c\udf27"
			},
			new
			{
				Id = "ocean",
				Name = "Ocean",
				Icon = "\ud83c\udf0a"
			},
			new
			{
				Id = "forest",
				Name = "Forest",
				Icon = "\ud83c\udf3f"
			},
			new
			{
				Id = "fire",
				Name = "Fireplace",
				Icon = "\ud83d\udd25"
			},
			new
			{
				Id = "cafe",
				Name = "Coffee Shop",
				Icon = "☕"
			},
			new
			{
				Id = "white_noise",
				Name = "White Noise",
				Icon = "〰"
			}
		};
		var array2 = array;
		foreach (var anon in array2)
		{
			string text = Path.Combine(_soundsDir, anon.Id + ".wav");
			EnsureSoundFileGenerated(anon.Id, text);
			SoundChannel channel = new SoundChannel(anon.Id, anon.Name, anon.Icon, text);
			try
			{
				if (File.Exists(text))
				{
					channel.Player.Open(new Uri(text, UriKind.Absolute));
					channel.Player.MediaEnded += delegate
					{
						try
						{
							channel.Player.Position = TimeSpan.Zero;
							channel.Player.Play();
						}
						catch
						{
						}
					};
				}
			}
			catch
			{
			}
			_channels.Add(channel);
		}
	}

	public void SetChannelPlaying(string id, bool play)
	{
		SoundChannel soundChannel = _channels.Find((SoundChannel c) => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
		if (soundChannel == null || soundChannel.IsPlaying == play)
		{
			return;
		}
		soundChannel.IsPlaying = play;
		try
		{
			if (play)
			{
				soundChannel.Player.Volume = soundChannel.Volume * _masterVolume;
				soundChannel.Player.Position = TimeSpan.Zero;
				soundChannel.Player.Play();
			}
			else
			{
				soundChannel.Player.Stop();
			}
		}
		catch
		{
		}
		SaveState();
		NotifyStateChanged();
	}

	public void SetChannelVolume(string id, double vol)
	{
		SoundChannel soundChannel = _channels.Find((SoundChannel c) => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
		if (soundChannel != null)
		{
			soundChannel.Volume = Math.Max(0.0, Math.Min(1.0, vol));
			try
			{
				soundChannel.Player.Volume = soundChannel.Volume * _masterVolume;
			}
			catch
			{
			}
			SaveState();
			NotifyStateChanged();
		}
	}

	public void StopAll()
	{
		foreach (SoundChannel channel in _channels)
		{
			if (channel.IsPlaying)
			{
				channel.IsPlaying = false;
				try
				{
					channel.Player.Stop();
				}
				catch
				{
				}
			}
		}
		SaveState();
		NotifyStateChanged();
	}

	public void Dispose()
	{
		StopAll();
		foreach (SoundChannel channel in _channels)
		{
			if (channel.Player != null)
			{
				try
				{
					channel.Player.Stop();
					channel.Player.Close();
				}
				catch
				{
				}
			}
		}
	}

	private void UpdateAllVolumes()
	{
		foreach (SoundChannel channel in _channels)
		{
			try
			{
				channel.Player.Volume = channel.Volume * _masterVolume;
			}
			catch
			{
			}
		}
	}

	private void NotifyStateChanged()
	{
		if (this.StateChanged != null)
		{
			try
			{
				this.StateChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	private void EnsureSoundFileGenerated(string soundType, string filePath)
	{
		if (File.Exists(filePath))
		{
			try
			{
				if (new FileInfo(filePath).Length > 1000)
				{
					return;
				}
			}
			catch
			{
			}
		}
		try
		{
			int num = 22050;
			int num2 = 4;
			int num3 = num * num2;
			short[] array = new short[num3];
			Random random = new Random(soundType.GetHashCode());
			double num4 = 0.0;
			double num5 = 0.0;
			for (int i = 0; i < num3; i++)
			{
				double num6 = (double)i / (double)num;
				double num7 = 0.0;
				switch (soundType)
				{
				case "rain":
				{
					double num15 = random.NextDouble() * 2.0 - 1.0;
					num4 = num4 * 0.92 + num15 * 0.08;
					double num16 = ((random.NextDouble() < 0.003) ? (random.NextDouble() * 0.5) : 0.0);
					num7 = num4 * 0.7 + num16;
					break;
				}
				case "ocean":
				{
					double num11 = random.NextDouble() * 2.0 - 1.0;
					num5 = num5 * 0.985 + num11 * 0.015;
					double num12 = 0.4 + 0.6 * Math.Sin(Math.PI * 2.0 * (num6 / 3.5));
					if (num12 < 0.0)
					{
						num12 = 0.0;
					}
					num7 = num5 * num12 * 1.5;
					break;
				}
				case "forest":
				{
					double num13 = random.NextDouble() * 2.0 - 1.0;
					num4 = num4 * 0.95 + num13 * 0.05;
					double num14 = 0.3 + 0.3 * Math.Sin(Math.PI * 2.0 * (num6 / 2.8));
					num7 = num4 * num14 * 0.8;
					break;
				}
				case "fire":
				{
					double num17 = random.NextDouble() * 2.0 - 1.0;
					num4 = num4 * 0.96 + num17 * 0.04;
					double num18 = 0.0;
					if (random.NextDouble() < 0.008)
					{
						num18 = (random.NextDouble() * 2.0 - 1.0) * 0.7;
					}
					num7 = num4 * 0.5 + num18;
					break;
				}
				case "cafe":
				{
					double num9 = random.NextDouble() * 2.0 - 1.0;
					num4 = num4 * 0.94 + num9 * 0.06;
					double num10 = ((random.NextDouble() < 0.001) ? (Math.Sin(Math.PI * 3600.0 * num6) * 0.25) : 0.0);
					num7 = num4 * 0.6 + num10;
					break;
				}
				default:
				{
					double num8 = random.NextDouble() * 2.0 - 1.0;
					num4 = num4 * 0.88 + num8 * 0.12;
					num7 = num4 * 0.5;
					break;
				}
				}
				int num19 = num / 10;
				if (i < num19)
				{
					num7 *= (double)i / (double)num19;
				}
				else if (i > num3 - num19)
				{
					num7 *= (double)(num3 - i) / (double)num19;
				}
				if (num7 > 1.0)
				{
					num7 = 1.0;
				}
				if (num7 < -1.0)
				{
					num7 = -1.0;
				}
				array[i] = (short)(num7 * 30000.0);
			}
			WriteWavFile(filePath, array, num);
		}
		catch
		{
		}
	}

	private void WriteWavFile(string path, short[] samples, int sampleRate)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
		{
			int num = samples.Length * 2;
			int value = 36 + num;
			binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
			binaryWriter.Write(value);
			binaryWriter.Write(Encoding.ASCII.GetBytes("WAVE"));
			binaryWriter.Write(Encoding.ASCII.GetBytes("fmt "));
			binaryWriter.Write(16);
			binaryWriter.Write((short)1);
			binaryWriter.Write((short)1);
			binaryWriter.Write(sampleRate);
			binaryWriter.Write(sampleRate * 2);
			binaryWriter.Write((short)2);
			binaryWriter.Write((short)16);
			binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
			binaryWriter.Write(num);
			for (int i = 0; i < samples.Length; i++)
			{
				binaryWriter.Write(samples[i]);
			}
		}
		PersistenceHelper.WriteCacheBytes(path, memoryStream.ToArray());
	}

	private void LoadState()
	{
		try
		{
			string text = PersistenceHelper.SafeReadAllText(_stateFilePath);
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			SoundMixerStateModel soundMixerStateModel = javaScriptSerializer.Deserialize<SoundMixerStateModel>(text);
			if (soundMixerStateModel == null)
			{
				return;
			}
			if (soundMixerStateModel.MasterVolume > 0.0)
			{
				_masterVolume = soundMixerStateModel.MasterVolume;
			}
			if (soundMixerStateModel.Tracks == null)
			{
				return;
			}
			foreach (SoundTrackConfig trk in soundMixerStateModel.Tracks)
			{
				List<SoundChannel> channels = _channels;
				Predicate<SoundChannel> match = (SoundChannel c) => c.Id.Equals(trk.Id, StringComparison.OrdinalIgnoreCase);
				SoundChannel soundChannel = channels.Find(match);
				if (soundChannel != null)
				{
					soundChannel.Volume = trk.Volume;
					if (trk.IsPlaying)
					{
						SetChannelPlaying(soundChannel.Id, play: true);
					}
				}
			}
		}
		catch
		{
		}
	}

	private void SaveState()
	{
		try
		{
			SoundMixerStateModel soundMixerStateModel = new SoundMixerStateModel();
			soundMixerStateModel.MasterVolume = _masterVolume;
			soundMixerStateModel.Tracks = new List<SoundTrackConfig>();
			SoundMixerStateModel soundMixerStateModel2 = soundMixerStateModel;
			foreach (SoundChannel channel in _channels)
			{
				soundMixerStateModel2.Tracks.Add(new SoundTrackConfig
				{
					Id = channel.Id,
					IsPlaying = channel.IsPlaying,
					Volume = channel.Volume
				});
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			string content = javaScriptSerializer.Serialize(soundMixerStateModel2);
			PersistenceHelper.SafeWriteAllText(_stateFilePath, content);
		}
		catch
		{
		}
	}
}
