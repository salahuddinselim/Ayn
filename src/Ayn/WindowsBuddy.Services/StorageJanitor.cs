using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Services;

public static class StorageJanitor
{
	public sealed class Policy
	{
		public string Name;

		public string Folder;

		public string Pattern = "*";

		public bool Recursive;

		public int MaxFiles = int.MaxValue;

		public long MaxBytes = long.MaxValue;

		public int MaxAgeDays;

		public Func<string, bool> Protected;
	}

	public sealed class StoreSize
	{
		public string Name;

		public long Bytes;

		public int Files;

		public string Cap;
	}

	public static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(2.0);

	public static readonly TimeSpan Period = TimeSpan.FromHours(6.0);

	private static Timer _timer;

	private static int _running;

	public static readonly ProducerFence Fence = new ProducerFence();

	private static readonly Regex AtomicLeftover = new Regex("\\.tmp(\\.[0-9a-f]{32})?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static bool Halted => !Fence.IsOpen;

	private static string Data => AppPaths.DataDir;

	public static Action PauseForShutdown()
	{
		Fence.Close();
		return Fence.Reopen;
	}

	public static void Start()
	{
		if (_timer == null)
		{
			_timer = new Timer(delegate
			{
				RunInBackground();
			}, null, FirstRunDelay, Period);
		}
	}

	private static void RunInBackground()
	{
		if (!Fence.TryEnter(out var _))
		{
			return;
		}
		if (Interlocked.Exchange(ref _running, 1) == 1)
		{
			Fence.Exit();
			return;
		}
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				Run();
			}
			catch (Exception ex)
			{
				try
				{
					TransactionLogger.Warn("Storage cleanup failed: " + ex.Message);
				}
				catch
				{
				}
			}
			finally
			{
				Interlocked.Exchange(ref _running, 0);
				Fence.Exit();
			}
		});
		thread.IsBackground = true;
		thread.Priority = ThreadPriority.BelowNormal;
		thread.Name = "Ayn storage janitor";
		Thread thread2 = thread;
		thread2.Start();
	}

	public static List<Policy> Policies()
	{
		string folder = Path.Combine(Data, "logs");
		HashSet<string> keepLogs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "engine.log", "engine.log.1", "notch.log", "notch.log.1", "crash.log", "crash.log.1", "metrics.log", "metrics.log.1" };
		List<Policy> list = new List<Policy>();
		list.Add(new Policy
		{
			Name = "logs (stale)",
			Folder = folder,
			Pattern = "*.log*",
			MaxAgeDays = 14,
			Protected = (string f) => keepLogs.Contains(Path.GetFileName(f))
		});
		list.Add(new Policy
		{
			Name = "logs (oversize)",
			Folder = folder,
			Pattern = "*.log*",
			MaxBytes = 12582912L
		});
		list.Add(new Policy
		{
			Name = "hang reports",
			Folder = folder,
			Pattern = "hang-*.txt",
			MaxFiles = 20,
			MaxAgeDays = 30
		});
		list.Add(new Policy
		{
			Name = "hang dumps",
			Folder = folder,
			Pattern = "hang-*.dmp",
			MaxFiles = 3,
			MaxAgeDays = 7
		});
		list.Add(new Policy
		{
			Name = "emoji cache",
			Folder = Path.Combine(Data, "EmojiCache"),
			Pattern = "*.png",
			MaxFiles = 3000,
			MaxBytes = 31457280L
		});
		list.Add(new Policy
		{
			Name = "favicons",
			Folder = Path.Combine(Data, "links", "favicons"),
			MaxFiles = 500,
			MaxBytes = 10485760L
		});
		list.Add(new Policy
		{
			Name = "screen time icons v1",
			Folder = Path.Combine(Data, "screentime", "icons"),
			MaxFiles = 0
		});
		list.Add(new Policy
		{
			Name = "screen time icons",
			Folder = Path.Combine(Data, "screentime", "icons-v2"),
			MaxFiles = 600,
			MaxBytes = 15728640L,
			MaxAgeDays = 180
		});
		list.Add(new Policy
		{
			Name = "analytics cache",
			Folder = Path.Combine(Data, "analytics", "cache"),
			Recursive = true,
			MaxBytes = 10485760L,
			MaxAgeDays = 60
		});
		list.Add(new Policy
		{
			Name = "cursor snapshots",
			Folder = Path.Combine(Data, "snapshots"),
			Pattern = "cursor_snapshot_*.json",
			MaxFiles = 10
		});
		list.Add(new Policy
		{
			Name = "backups",
			Folder = Path.Combine(Data, "backups"),
			Pattern = "*.json",
			MaxFiles = 10
		});
		return list;
	}

	public static List<string> Run()
	{
		List<string> list = new List<string>();
		int deleted = 0;
		long freed = 0L;
		foreach (Policy item in Policies())
		{
			if (!Halted)
			{
				Enforce(item, ref deleted, ref freed);
				continue;
			}
			break;
		}
		RemoveStaleFontFolders(ref deleted, ref freed);
		RemoveStaleTempFiles(ref deleted, ref freed);
		string text = LegacyFolderCleanup.Run();
		foreach (StoreSize item2 in Measure())
		{
			list.Add(string.Format(CultureInfo.InvariantCulture, "{0,-22} {1,9:N0} KB {2,6} files  cap: {3}", item2.Name, (double)item2.Bytes / 1024.0, item2.Files, item2.Cap));
		}
		TransactionLogger.Info(string.Format(CultureInfo.InvariantCulture, "Storage cleanup: {0} files ({1:N0} KB) removed. {2}\r\n  {3}", deleted, (double)freed / 1024.0, text, string.Join("\r\n  ", list)));
		return list;
	}

	internal static void Enforce(Policy p, ref int deleted, ref long freed)
	{
		if (!Directory.Exists(p.Folder))
		{
			return;
		}
		List<FileInfo> list;
		try
		{
			list = (from f in new DirectoryInfo(p.Folder).GetFiles(p.Pattern, p.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
				where p.Protected == null || !p.Protected(f.FullName)
				orderby f.LastWriteTimeUtc descending
				select f).ToList();
		}
		catch
		{
			return;
		}
		DateTime dateTime = ((p.MaxAgeDays > 0) ? DateTime.UtcNow.AddDays(-p.MaxAgeDays) : DateTime.MinValue);
		long num = 0L;
		int num2 = 0;
		foreach (FileInfo item in list)
		{
			if (Halted)
			{
				break;
			}
			if (!(item.LastWriteTimeUtc < dateTime) && num2 < p.MaxFiles && num + item.Length <= p.MaxBytes)
			{
				num2++;
				num += item.Length;
				continue;
			}
			long length = item.Length;
			if (TryDelete(item.FullName))
			{
				deleted++;
				freed += length;
			}
		}
	}

	private static void RemoveStaleFontFolders(ref int deleted, ref long freed)
	{
		string path = Path.Combine(Data, "fonts");
		if (!Directory.Exists(path))
		{
			return;
		}
		string text = "Inter-v15";
		DirectoryInfo[] directories = new DirectoryInfo(path).GetDirectories("Inter-v*");
		foreach (DirectoryInfo directoryInfo in directories)
		{
			if (Halted)
			{
				break;
			}
			if (!directoryInfo.Name.Equals(text, StringComparison.OrdinalIgnoreCase) && !directoryInfo.Name.StartsWith(text + "-", StringComparison.OrdinalIgnoreCase))
			{
				long num = SizeOf(directoryInfo.FullName);
				try
				{
					directoryInfo.Delete(recursive: true);
					deleted++;
					freed += num;
				}
				catch
				{
				}
			}
		}
	}

	private static void RemoveStaleTempFiles(ref int deleted, ref long freed)
	{
		if (!Directory.Exists(Data))
		{
			return;
		}
		DateTime dateTime = DateTime.UtcNow.AddDays(-1.0);
		foreach (string item in SafeFiles(Data, "*.tmp*"))
		{
			if (Halted)
			{
				break;
			}
			try
			{
				FileInfo fileInfo = new FileInfo(item);
				if (!(fileInfo.LastWriteTimeUtc > dateTime) && IsAtomicWriteLeftover(fileInfo.Name) && !IsUserContent(fileInfo.FullName))
				{
					long length = fileInfo.Length;
					if (TryDelete(item))
					{
						deleted++;
						freed += length;
					}
				}
			}
			catch
			{
			}
		}
	}

	public static List<StoreSize> Measure()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		dictionary.Add("logs", "1 MB + 1 rollover per log; stale logs 14 d; hang reports 20 / 30 d; dumps 3 / 7 d");
		dictionary.Add("clipboard", "500 unpinned items (Settings) + retention 7/30 d; pinned kept; orphan images removed");
		dictionary.Add("screentime", "7 days of history; icons 600 / 15 MB / 180 d");
		dictionary.Add("aicoding", "120 days of events; TSV compacted");
		dictionary.Add("calendar", "your events; feeds max 8 MB each, window -45/+60 d; completed reminders removed");
		dictionary.Add("weather", "one forecast cache + 5 recent places");
		dictionary.Add("EmojiCache", "3000 files / 30 MB");
		dictionary.Add("emoji", "recent list (fixed size)");
		dictionary.Add("links", "your bookmarks; favicons 500 / 10 MB");
		dictionary.Add("analytics", "cache 10 MB / 60 d");
		dictionary.Add("snapshots", "10 newest");
		dictionary.Add("backups", "10 newest + legacy archive (kept)");
		dictionary.Add("fonts", "current version only");
		dictionary.Add("themes", "installed cursor packs (user action)");
		dictionary.Add("notes", "your notes (never trimmed)");
		Dictionary<string, string> dictionary2 = dictionary;
		List<StoreSize> list = new List<StoreSize>();
		if (Directory.Exists(Data))
		{
			DirectoryInfo[] directories = new DirectoryInfo(Data).GetDirectories();
			foreach (DirectoryInfo directoryInfo in directories)
			{
				list.Add(new StoreSize
				{
					Name = directoryInfo.Name,
					Bytes = SizeOf(directoryInfo.FullName),
					Files = SafeFiles(directoryInfo.FullName, "*").Count(),
					Cap = (dictionary2.TryGetValue(directoryInfo.Name, out var value) ? value : "small settings/state")
				});
			}
		}
		string legacyLocal = LegacyFolderCleanup.LegacyLocal;
		if (Directory.Exists(legacyLocal))
		{
			list.Add(new StoreSize
			{
				Name = "legacy WindowsBuddy",
				Bytes = SizeOf(legacyLocal),
				Files = SafeFiles(legacyLocal, "*").Count(),
				Cap = "retired 3 d after migration"
			});
		}
		return list.OrderByDescending((StoreSize s) => s.Bytes).ToList();
	}

	internal static bool IsAtomicWriteLeftover(string name)
	{
		return AtomicLeftover.IsMatch(name);
	}

	internal static bool IsUserContent(string fullPath)
	{
		string[] array = new string[2]
		{
			Path.Combine(Data, "shelf", "stored"),
			Path.Combine(Data, "backups", "legacy-windowsbuddy")
		};
		foreach (string path in array)
		{
			string value = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
			if (Path.GetFullPath(fullPath).StartsWith(value, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	internal static IEnumerable<string> SafeFiles(string dir, string pattern)
	{
		try
		{
			return Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).ToList();
		}
		catch
		{
			return new string[0];
		}
	}

	internal static long SizeOf(string dir)
	{
		long num = 0L;
		foreach (string item in SafeFiles(dir, "*"))
		{
			try
			{
				num += new FileInfo(item).Length;
			}
			catch
			{
			}
		}
		return num;
	}

	internal static bool TryDelete(string path)
	{
		try
		{
			File.SetAttributes(path, FileAttributes.Normal);
			File.Delete(path);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
