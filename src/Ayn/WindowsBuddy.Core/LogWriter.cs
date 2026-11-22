using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using WindowsBuddy.Services;

namespace WindowsBuddy.Core;

public static class LogWriter
{
	public const long MaxBytes = 1048576L;

	private static readonly string _dir;

	private static readonly BlockingCollection<KeyValuePair<string, string>> _queue;

	private static readonly Dictionary<string, long> _sizes;

	private static readonly object _fileLock;

	private static readonly ManualResetEventSlim _idle;

	static LogWriter()
	{
		_queue = new BlockingCollection<KeyValuePair<string, string>>(new ConcurrentQueue<KeyValuePair<string, string>>(), 4096);
		_sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		_fileLock = new object();
		_idle = new ManualResetEventSlim(initialState: true);
		try
		{
			_dir = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "logs");
			Directory.CreateDirectory(_dir);
		}
		catch
		{
			_dir = null;
		}
		Thread thread = new Thread(Pump)
		{
			IsBackground = true,
			Name = "WindowsBuddy.LogWriter",
			Priority = ThreadPriority.BelowNormal
		};
		thread.Start();
	}

	public static string PathFor(string fileName)
	{
		if (_dir != null)
		{
			return Path.Combine(_dir, fileName);
		}
		return null;
	}

	public static void Enqueue(string fileName, string line)
	{
		if (_dir != null && !string.IsNullOrEmpty(fileName))
		{
			_idle.Reset();
			if (!_queue.TryAdd(new KeyValuePair<string, string>(fileName, line)) && _queue.Count == 0)
			{
				_idle.Set();
			}
		}
	}

	public static void WriteNow(string fileName, string text)
	{
		if (_dir == null)
		{
			return;
		}
		try
		{
			Append(fileName, LogScrubber.Scrub(text) + Environment.NewLine);
		}
		catch
		{
		}
	}

	public static void Flush(int timeoutMs)
	{
		try
		{
			_idle.Wait(timeoutMs);
		}
		catch
		{
		}
	}

	private static void Pump()
	{
		Dictionary<string, StringBuilder> dictionary = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
		while (true)
		{
			KeyValuePair<string, string> item;
			try
			{
				item = _queue.Take();
			}
			catch
			{
				break;
			}
			dictionary.Clear();
			Add(dictionary, item);
			while (dictionary.Count < 64 && _queue.TryTake(out item))
			{
				Add(dictionary, item);
			}
			foreach (KeyValuePair<string, StringBuilder> item2 in dictionary)
			{
				try
				{
					Append(item2.Key, item2.Value.ToString());
				}
				catch
				{
				}
			}
			if (_queue.Count == 0)
			{
				_idle.Set();
			}
		}
	}

	private static void Add(Dictionary<string, StringBuilder> batch, KeyValuePair<string, string> item)
	{
		if (!batch.TryGetValue(item.Key, out var value))
		{
			value = new StringBuilder();
			batch[item.Key] = value;
		}
		value.Append(LogScrubber.Scrub(item.Value)).Append(Environment.NewLine);
	}

	private static void Append(string fileName, string text)
	{
		lock (_fileLock)
		{
			string text2 = Path.Combine(_dir, fileName);
			if (!_sizes.TryGetValue(fileName, out var value))
			{
				FileInfo fileInfo = new FileInfo(text2);
				value = (fileInfo.Exists ? fileInfo.Length : 0);
			}
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			if (value > 0 && value + bytes.Length > 1048576)
			{
				string text3 = text2 + ".1";
				try
				{
					if (File.Exists(text3))
					{
						File.Delete(text3);
					}
				}
				catch
				{
				}
				try
				{
					File.Move(text2, text3);
				}
				catch
				{
					try
					{
						File.Delete(text2);
					}
					catch
					{
					}
				}
				value = 0L;
			}
			using (FileStream fileStream = new FileStream(text2, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
			{
				if (value == 0 && fileStream.Length == 0)
				{
					byte[] array = new byte[3] { 239, 187, 191 };
					fileStream.Write(array, 0, array.Length);
				}
				fileStream.Write(bytes, 0, bytes.Length);
			}
			_sizes[fileName] = value + bytes.Length;
		}
	}
}
