using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Behavior;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Tools.AICoding;

public class AICodingService
{
	private sealed class Acc
	{
		public long Tokens;

		public long Unpriced;

		public long MonthTokens;

		public long MonthUnpriced;

		public long In;

		public long Out;

		public long Write;

		public long Read;

		public double Cost;

		public double MonthCost;

		public int Messages;

		public int Tools;

		public readonly HashSet<string> Sessions = new HashSet<string>(StringComparer.Ordinal);

		public readonly HashSet<string> UnpricedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public readonly long[] Hours = new long[24];

		public readonly HashSet<DateTime> Days = new HashSet<DateTime>();

		public long LatestUtc;

		public string LatestCwd;

		public readonly Dictionary<string, UsageRow> Models = new Dictionary<string, UsageRow>(StringComparer.OrdinalIgnoreCase);
	}

	private const string CacheVersion = "v3";

	internal const int RetentionDays = 120;

	private static readonly Lazy<AICodingService> _instance = new Lazy<AICodingService>(() => new AICodingService());

	public static readonly ProducerFence Fence = new ProducerFence();

	private long _passToken = -1L;

	private readonly object _gate = new object();

	private readonly Dictionary<string, UsageEvent> _events = new Dictionary<string, UsageEvent>(StringComparer.Ordinal);

	private Dictionary<string, LogFileState> _files = new Dictionary<string, LogFileState>(StringComparer.OrdinalIgnoreCase);

	private Dictionary<AICodingToolType, AICodingSnapshot> _snapshots = new Dictionary<AICodingToolType, AICodingSnapshot>();

	private List<AICodingToolType> _detected = new List<AICodingToolType>();

	private readonly PricingTable _prices;

	private readonly AICodingBudgetConfig _budgets;

	private readonly string _cacheDir;

	private bool _loaded;

	private bool _scanning;

	private bool _pending;

	private System.Threading.Timer _timer;

	private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();

	private System.Threading.Timer _debounce;

	private readonly Dictionary<string, string> _intern = new Dictionary<string, string>(StringComparer.Ordinal);

	public static readonly AICodingToolType[] Agents = new AICodingToolType[3]
	{
		AICodingToolType.ClaudeCode,
		AICodingToolType.Codex,
		AICodingToolType.OpenCode
	};

	private static readonly Regex RxAssistant = new Regex("\"type\"\\s*:\\s*\"assistant\"", RegexOptions.Compiled);

	private static readonly Regex RxUser = new Regex("\"type\"\\s*:\\s*\"user\"", RegexOptions.Compiled);

	private static readonly Regex RxNotPrompt = new Regex("\"type\"\\s*:\\s*\"tool_result\"|\"(?:isMeta|isCompactSummary|isSidechain)\"\\s*:\\s*true", RegexOptions.Compiled);

	private static readonly Regex RxUuid = new Regex("\"uuid\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

	private static readonly Regex RxTs = new Regex("\"timestamp\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

	private static readonly Regex RxSession = new Regex("\"sessionId\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

	private static readonly Regex RxCwd = new Regex("\"cwd\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

	private int _tsvLines;

	public static AICodingService Instance => _instance.Value;

	public PricingTable Prices => _prices;

	public List<AICodingToolType> DetectedAgents
	{
		get
		{
			lock (_gate)
			{
				return new List<AICodingToolType>(_detected);
			}
		}
	}

	private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

	public static List<string> ClaudeRoots
	{
		get
		{
			List<string> list = new List<string>();
			string environmentVariable = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
			if (!string.IsNullOrEmpty(environmentVariable))
			{
				string[] array = environmentVariable.Split(',');
				foreach (string text in array)
				{
					if (text.Trim().Length > 0)
					{
						list.Add(text.Trim());
					}
				}
			}
			else
			{
				list.Add(Path.Combine(Home, ".claude"));
				string environmentVariable2 = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
				list.Add(Path.Combine((!string.IsNullOrEmpty(environmentVariable2)) ? environmentVariable2 : Path.Combine(Home, ".config"), "claude"));
			}
			return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}
	}

	public static IEnumerable<string> ClaudeProjectDirs => ClaudeRoots.Select((string r) => Path.Combine(r, "projects"));

	public static string CodexRoot
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("CODEX_HOME");
			if (string.IsNullOrEmpty(environmentVariable))
			{
				return Path.Combine(Home, ".codex");
			}
			return environmentVariable;
		}
	}

	public static string OpenCodeDatabase
	{
		get
		{
			var local = Path.Combine(Home, ".local", "share", "opencode", "opencode.db");
			if (File.Exists(local)) return local;
			return Path.Combine(Home, ".opencode", "opencode.db");
		}
	}

	private void ScanOpenCode(List<UsageEvent> changed)
	{
		var db = OpenCodeDatabase;
		if (!File.Exists(db)) return;
		try
		{
			using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + db + ";Mode=ReadOnly");
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, time_created, tokens_input, tokens_output, tokens_reasoning, tokens_cache_read, tokens_cache_write FROM session WHERE time_created IS NOT NULL";
			using var reader = command.ExecuteReader();
			while (reader.Read())
			{
				var id = reader.GetValue(0)?.ToString() ?? "";
				if (string.IsNullOrEmpty(id)) continue;
				var ms = Convert.ToInt64(reader.GetValue(1));
				if (ms <= 0) continue;
				var utc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.Ticks;
				if (utc < CutoffTicks) continue;
				long input = Convert.ToInt64(reader.GetValue(2));
				long output = Convert.ToInt64(reader.GetValue(3));
				long reasoning = reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4));
				long cacheRead = reader.IsDBNull(5) ? 0 : Convert.ToInt64(reader.GetValue(5));
				long cacheWrite = reader.IsDBNull(6) ? 0 : Convert.ToInt64(reader.GetValue(6));
				if (input + output + reasoning + cacheRead + cacheWrite <= 0) continue;
				Add(new UsageEvent
				{
					Key = "oc:m:" + id,
					Agent = AICodingToolType.OpenCode,
					Utc = utc,
					Session = Intern(id),
					In = input,
					Out = output + reasoning,
					W5 = cacheWrite,
					Read = cacheRead,
					Messages = 0,
					Active = 2
				}, changed);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("OpenCode usage scan failed: " + ex.Message);
		}
	}

	private static long CutoffTicks => DateTime.UtcNow.AddDays(-120.0).Ticks;

	private string EventsPath => Path.Combine(_cacheDir, "events-v3.tsv");

	private string FilesPath => Path.Combine(_cacheDir, "files-v3.json");

	public event EventHandler Updated;

	private AICodingService()
	{
		_prices = PricingTable.Load();
		_budgets = AICodingBudgetConfig.Load();
		_cacheDir = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "aicoding");
		foreach (AICodingToolType value in Enum.GetValues(typeof(AICodingToolType)))
		{
			_snapshots[value] = new AICodingSnapshot(value);
		}
		AICodingToolType[] agents = Agents;
		foreach (AICodingToolType aICodingToolType2 in agents)
		{
			if (DetectStatus(aICodingToolType2) == AgentStatus.Tracked)
			{
				_detected.Add(aICodingToolType2);
			}
		}
		_timer = new System.Threading.Timer(delegate
		{
			RequestRefresh();
		}, null, TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(60.0));
		SetupWatchers();
		RequestRefresh();
	}

	public AICodingSnapshot GetSnapshot(AICodingToolType filter)
	{
		lock (_gate)
		{
			AICodingSnapshot value;
			return _snapshots.TryGetValue(filter, out value) ? value : new AICodingSnapshot(filter);
		}
	}

	public AICodingSnapshot GetCachedSnapshot(AICodingToolType filter)
	{
		return GetSnapshot(filter);
	}

	public double GetBudget(AICodingToolType filter)
	{
		lock (_gate)
		{
			return _budgets.Get(filter);
		}
	}

	public void SetBudget(AICodingToolType filter, double usd)
	{
		lock (_gate)
		{
			_budgets.Set(filter, usd);
		}
		Task.Run(delegate
		{
			Aggregate();
			RaiseUpdated();
		});
	}

	public void RequestRefresh()
	{
		lock (_gate)
		{
			if (_scanning)
			{
				_pending = true;
				return;
			}
			if (!Fence.TryEnter(out var token))
			{
				return;
			}
			_scanning = true;
			_passToken = token;
		}
		Task.Run(delegate
		{
			try
			{
				Pass();
			}
			catch (Exception ex)
			{
				Log("AICoding scan failed: " + ex.Message);
			}
			finally
			{
				bool pending;
				lock (_gate)
				{
					_scanning = false;
					pending = _pending;
					_pending = false;
				}
				Fence.Exit();
				if (pending)
				{
					RequestRefresh();
				}
			}
		});
	}

	public static Action PauseIfCreated()
	{
		Fence.Close();
		if (!_instance.IsValueCreated)
		{
			return delegate
			{
				Fence.Reopen();
			};
		}
		return _instance.Value.PauseForShutdown();
	}

	public Action PauseForShutdown()
	{
		Fence.Close();
		lock (_gate)
		{
			if (_timer != null)
			{
				_timer.Change(-1, -1);
			}
			if (_debounce != null)
			{
				_debounce.Change(-1, -1);
			}
		}
		List<FileSystemWatcher> watching = _watchers.Where((FileSystemWatcher w) => w.EnableRaisingEvents).ToList();
		foreach (FileSystemWatcher item in watching)
		{
			try
			{
				item.EnableRaisingEvents = false;
			}
			catch
			{
			}
		}
		return delegate
		{
			Fence.Reopen();
			foreach (FileSystemWatcher item2 in watching)
			{
				try
				{
					item2.EnableRaisingEvents = true;
				}
				catch
				{
				}
			}
			lock (_gate)
			{
				if (_timer != null)
				{
					_timer.Change(TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(60.0));
				}
			}
			RequestRefresh();
		};
	}

	public void RecomputeStatsSync()
	{
		long token;
		bool flag = Fence.TryEnter(out token);
		lock (_gate)
		{
			while (_scanning)
			{
				Monitor.Wait(_gate, 50);
			}
			_scanning = true;
			_passToken = (flag ? token : (-1));
		}
		try
		{
			Pass();
		}
		finally
		{
			lock (_gate)
			{
				_scanning = false;
			}
			if (flag)
			{
				Fence.Exit();
			}
		}
	}

	public static AgentStatus DetectStatus(AICodingToolType agent)
	{
		switch (agent)
		{
		case AICodingToolType.ClaudeCode:
			if (!ClaudeProjectDirs.Any(Directory.Exists))
			{
				return AgentStatus.NotDetected;
			}
			return AgentStatus.Tracked;
		case AICodingToolType.Codex:
			if (!Directory.Exists(Path.Combine(CodexRoot, "sessions")) && !Directory.Exists(Path.Combine(CodexRoot, "archived_sessions")))
			{
				return AgentStatus.NotDetected;
			}
			return AgentStatus.Tracked;
		case AICodingToolType.OpenCode:
			if (!File.Exists(OpenCodeDatabase))
			{
				return AgentStatus.NotDetected;
			}
			return AgentStatus.Tracked;
		default:
			return AgentStatus.Tracked;
		}
	}

	private void Pass()
	{
		if (!_loaded)
		{
			LoadCache();
			_loaded = true;
		}
		List<UsageEvent> changed = new List<UsageEvent>();
		foreach (string claudeProjectDir in ClaudeProjectDirs)
		{
			ScanRoot(claudeProjectDir, AICodingToolType.ClaudeCode, changed);
		}
		ScanRoot(Path.Combine(CodexRoot, "sessions"), AICodingToolType.Codex, changed);
		ScanRoot(Path.Combine(CodexRoot, "archived_sessions"), AICodingToolType.Codex, changed);
		ScanOpenCode(changed);
		SaveCache(changed);
		Aggregate();
		CheckBudgetAlerts();
		RaiseUpdated();
	}

	private void ScanRoot(string root, AICodingToolType agent, List<UsageEvent> changed)
	{
		if (!Directory.Exists(root))
		{
			return;
		}
		foreach (string item in EnumerateJsonl(root))
		{
			try
			{
				ScanFile(item, agent, changed);
			}
			catch (Exception ex)
			{
				Log("AICoding: skipped " + Path.GetFileName(item) + ": " + ex.Message);
			}
		}
	}

	private static IEnumerable<string> EnumerateJsonl(string root)
	{
		Stack<string> stack = new Stack<string>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			string dir = stack.Pop();
			string[] files = null;
			string[] dirs = null;
			try
			{
				files = Directory.GetFiles(dir, "*.jsonl");
			}
			catch
			{
			}
			try
			{
				dirs = Directory.GetDirectories(dir);
			}
			catch
			{
			}
			if (files != null)
			{
				try
				{
					string[] array = files;
					for (int i = 0; i < array.Length; i++)
					{
						yield return array[i];
					}
				}
				finally
				{
				}
			}
			if (dirs != null)
			{
				string[] array2 = dirs;
				foreach (string item in array2)
				{
					stack.Push(item);
				}
			}
		}
	}

	private void ScanFile(string path, AICodingToolType agent, List<UsageEvent> changed)
	{
		FileInfo fileInfo = new FileInfo(path);
		long length = fileInfo.Length;
		long ticks = fileInfo.LastWriteTimeUtc.Ticks;
		if (!_files.TryGetValue(path, out var value))
		{
			value = new LogFileState();
			_files[path] = value;
		}
		if (value.Length == length && value.WriteTicks == ticks && value.Offset <= length)
		{
			return;
		}
		if (length < value.Offset)
		{
			value.Offset = 0L;
			LogFileState logFileState = value;
			LogFileState logFileState2 = value;
			long num2 = (value.CxOut = 0L);
			long cxIn = (logFileState2.CxCached = num2);
			logFileState.CxIn = cxIn;
		}
		using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			fileStream.Seek(value.Offset, SeekOrigin.Begin);
			byte[] array = new byte[1048576];
			MemoryStream memoryStream = new MemoryStream();
			bool flag = false;
			long num4 = value.Offset;
			long offset = value.Offset;
			int num5;
			while ((num5 = fileStream.Read(array, 0, array.Length)) > 0)
			{
				int num6 = 0;
				for (int i = 0; i < num5; i++)
				{
					if (array[i] == 10)
					{
						if (memoryStream.Length + i - num6 > 8388608)
						{
							flag = true;
						}
						if (!flag)
						{
							memoryStream.Write(array, num6, i - num6);
						}
						string line = (flag ? null : Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length));
						memoryStream.SetLength(0L);
						if (flag)
						{
							Log("AICoding: skipped transcript line exceeding 8 MB");
						}
						else if (agent == AICodingToolType.ClaudeCode)
						{
							ClaudeLine(line, Path.GetFileNameWithoutExtension(path), changed);
						}
						else
						{
							CodexLine(line, path, offset, value, changed);
						}
						flag = false;
						num6 = i + 1;
						offset = num4 + i + 1;
					}
				}
				if (memoryStream.Length + num5 - num6 > 8388608)
				{
					flag = true;
				}
				if (!flag)
				{
					memoryStream.Write(array, num6, num5 - num6);
				}
				num4 += num5;
			}
			value.Offset = offset;
		}
		value.Length = length;
		value.WriteTicks = ticks;
	}

	private void ClaudeLine(string line, string fileSession, List<UsageEvent> changed)
	{
		if (RxAssistant.IsMatch(line))
		{
			Dictionary<string, object> dictionary = Json(line);
			Dictionary<string, object> dictionary2 = ((dictionary != null && Str(dictionary, "type") == "assistant" && dictionary.ContainsKey("message")) ? (dictionary["message"] as Dictionary<string, object>) : null);
			long num = ((dictionary != null) ? ParseUtc(Str(dictionary, "timestamp")) : 0);
			if (dictionary2 != null && num != 0 && ActivityDays.IsReal(promptOrResponse: true, Str(dictionary, "uuid"), Str(dictionary2, "id")))
			{
				ClaudeResponse(dictionary, dictionary2, num, ActivityDays.SessionOf(Str(dictionary, "sessionId"), fileSession), changed);
				return;
			}
		}
		else if (RxUser.IsMatch(line) && !RxNotPrompt.IsMatch(line))
		{
			string text = Match(RxUuid, line);
			long num2 = ParseUtc(Match(RxTs, line));
			if (ActivityDays.IsReal(promptOrResponse: true, text, null) && num2 != 0)
			{
				Add(new UsageEvent
				{
					Key = "cc:u:" + text,
					Agent = AICodingToolType.ClaudeCode,
					Utc = num2,
					Session = Intern(ActivityDays.SessionOf(Match(RxSession, line), fileSession)),
					Cwd = Intern(Unescape(Match(RxCwd, line))),
					Messages = 1,
					Active = 1
				}, changed);
				return;
			}
		}
		string text2 = ActivityDays.SessionOf(Match(RxSession, line), fileSession);
		long num3 = ParseUtc(Match(RxTs, line));
		if (!string.IsNullOrEmpty(text2) && num3 != 0)
		{
			Add(new UsageEvent
			{
				Key = "cc:m:" + text2 + ":" + (num3 / 9000000000L).ToString(CultureInfo.InvariantCulture),
				Agent = AICodingToolType.ClaudeCode,
				Utc = num3,
				Session = Intern(text2),
				Active = 2
			}, changed);
		}
	}

	private void ClaudeResponse(Dictionary<string, object> d, Dictionary<string, object> msg, long utc, string session, List<UsageEvent> changed)
	{
		string text = Str(msg, "model");
		string text2 = Str(msg, "id");
		string text3 = Str(d, "requestId");
		if (text == "<synthetic>")
		{
			Add(new UsageEvent
			{
				Key = "cc:s:" + (string.IsNullOrEmpty(Str(d, "uuid")) ? text2 : Str(d, "uuid")),
				Agent = AICodingToolType.ClaudeCode,
				Utc = utc,
				Session = Intern(session),
				Cwd = Intern(Str(d, "cwd")),
				Active = 1
			}, changed);
			return;
		}
		UsageEvent usageEvent = new UsageEvent();
		usageEvent.Key = "cc:" + (string.IsNullOrEmpty(text2) ? Str(d, "uuid") : (text2 + ":" + text3));
		usageEvent.Agent = AICodingToolType.ClaudeCode;
		usageEvent.Utc = utc;
		usageEvent.Model = Intern(text);
		usageEvent.Session = Intern(session);
		usageEvent.Cwd = Intern(Str(d, "cwd"));
		usageEvent.Messages = 1;
		usageEvent.Active = 1;
		UsageEvent usageEvent2 = usageEvent;
		Dictionary<string, object> dictionary = (msg.ContainsKey("usage") ? (msg["usage"] as Dictionary<string, object>) : null);
		if (dictionary != null)
		{
			usageEvent2.In = Num(dictionary, "input_tokens");
			usageEvent2.Out = Num(dictionary, "output_tokens");
			usageEvent2.Read = Num(dictionary, "cache_read_input_tokens");
			long num = Num(dictionary, "cache_creation_input_tokens");
			Dictionary<string, object> dictionary2 = (dictionary.ContainsKey("cache_creation") ? (dictionary["cache_creation"] as Dictionary<string, object>) : null);
			long val = ((dictionary2 != null) ? Num(dictionary2, "ephemeral_1h_input_tokens") : 0);
			usageEvent2.W1h = Math.Min(val, num);
			usageEvent2.W5 = num - usageEvent2.W1h;
			usageEvent2.Fast = Str(dictionary, "speed") == "fast";
		}
		Add(usageEvent2, changed);
		object[] array = (msg.ContainsKey("content") ? (msg["content"] as object[]) : null);
		if (array == null)
		{
			return;
		}
		object[] array2 = array;
		foreach (object obj in array2)
		{
			if (obj is Dictionary<string, object> d2 && !(Str(d2, "type") != "tool_use"))
			{
				string text4 = Str(d2, "id");
				if (!string.IsNullOrEmpty(text4))
				{
					Add(new UsageEvent
					{
						Key = "cc:tu:" + text4,
						Agent = AICodingToolType.ClaudeCode,
						Utc = utc,
						Model = usageEvent2.Model,
						Session = usageEvent2.Session,
						Cwd = usageEvent2.Cwd,
						Tools = 1,
						Active = 1
					}, changed);
				}
			}
		}
	}

	private void CodexLine(string line, string path, long offset, LogFileState st, List<UsageEvent> changed)
	{
		if (line.Length == 0 || line[0] != '{' || line.IndexOf("\"function_call_output\"", StringComparison.Ordinal) >= 0 || line.IndexOf("\"custom_tool_call_output\"", StringComparison.Ordinal) >= 0)
		{
			return;
		}
		Dictionary<string, object> dictionary = Json(line);
		if (dictionary == null)
		{
			return;
		}
		string text = (string.IsNullOrEmpty(st.CxSession) ? Path.GetFileNameWithoutExtension(path) : st.CxSession);
		string key = "cx:" + text + ":" + offset;
		long num = ParseUtc(Str(dictionary, "timestamp"));
		Dictionary<string, object> dictionary2 = (dictionary.ContainsKey("payload") ? (dictionary["payload"] as Dictionary<string, object>) : null);
		if (dictionary2 == null)
		{
			if (dictionary.ContainsKey("id") && dictionary.ContainsKey("timestamp") && !dictionary.ContainsKey("type"))
			{
				st.CxSession = Str(dictionary, "id");
				st.CxTs = num;
			}
			else if (st.CxTs != 0)
			{
				CodexItem(dictionary, key, st, st.CxTs, changed);
			}
		}
		else
		{
			if (num == 0)
			{
				return;
			}
			if (!string.IsNullOrEmpty(st.CxSession))
			{
				Add(new UsageEvent
				{
					Key = "cx:m:" + st.CxSession + ":" + (num / 9000000000L).ToString(CultureInfo.InvariantCulture),
					Agent = AICodingToolType.Codex,
					Utc = num,
					Session = Intern(st.CxSession),
					Active = 2
				}, changed);
			}
			switch (Str(dictionary, "type"))
			{
			case "session_meta":
				st.CxSession = Str(dictionary2, "id");
				if (!string.IsNullOrEmpty(Str(dictionary2, "cwd")))
				{
					st.CxCwd = Str(dictionary2, "cwd");
				}
				break;
			case "turn_context":
				if (!string.IsNullOrEmpty(Str(dictionary2, "model")))
				{
					st.CxModel = Str(dictionary2, "model");
				}
				if (!string.IsNullOrEmpty(Str(dictionary2, "cwd")))
				{
					st.CxCwd = Str(dictionary2, "cwd");
				}
				break;
			case "response_item":
				CodexItem(dictionary2, key, st, num, changed);
				break;
			case "event_msg":
			{
				string text2 = Str(dictionary2, "type");
				if (text2 == "user_message" || text2 == "agent_message")
				{
					Add(NewCodex(key, st, num, 1, 0), changed);
				}
				else
				{
					if (text2 != "token_count")
					{
						break;
					}
					Dictionary<string, object> dictionary3 = (dictionary2.ContainsKey("info") ? (dictionary2["info"] as Dictionary<string, object>) : null);
					Dictionary<string, object> dictionary4 = ((dictionary3 != null && dictionary3.ContainsKey("total_token_usage")) ? (dictionary3["total_token_usage"] as Dictionary<string, object>) : null);
					if (dictionary4 != null)
					{
						long num2 = Num(dictionary4, "input_tokens");
						long num3 = Num(dictionary4, "cached_input_tokens");
						long num4 = Num(dictionary4, "output_tokens");
						if (num2 < st.CxIn || num4 < st.CxOut)
						{
							long num6 = (st.CxOut = 0L);
							long cxIn = (st.CxCached = num6);
							st.CxIn = cxIn;
						}
						long num8 = num2 - st.CxIn;
						long val = Math.Max(0L, num3 - st.CxCached);
						long num9 = num4 - st.CxOut;
						st.CxIn = num2;
						st.CxCached = num3;
						st.CxOut = num4;
						if (num8 > 0 || num9 > 0)
						{
							UsageEvent usageEvent = NewCodex(key, st, num, 0, 0);
							usageEvent.Read = Math.Min(val, num8);
							usageEvent.In = num8 - usageEvent.Read;
							usageEvent.Out = num9;
							Add(usageEvent, changed);
						}
					}
				}
				break;
			}
			}
		}
	}

	private void CodexItem(Dictionary<string, object> item, string key, LogFileState st, long ts, List<UsageEvent> changed)
	{
		switch (Str(item, "type"))
		{
		case "function_call":
		case "custom_tool_call":
		case "local_shell_call":
		case "web_search_call":
			Add(NewCodex(key, st, ts, 0, 1), changed);
			break;
		case "message":
			if (st.CxTs != 0 && ts == st.CxTs)
			{
				string text = Str(item, "role");
				if ((!(text != "user") || !(text != "assistant")) && (!(text == "user") || !FirstText(item).StartsWith("<", StringComparison.Ordinal)))
				{
					Add(NewCodex(key, st, ts, 1, 0), changed);
				}
			}
			break;
		}
	}

	private UsageEvent NewCodex(string key, LogFileState st, long ts, int messages, int tools)
	{
		UsageEvent usageEvent = new UsageEvent();
		usageEvent.Key = key;
		usageEvent.Agent = AICodingToolType.Codex;
		usageEvent.Utc = ts;
		usageEvent.Model = Intern(string.IsNullOrEmpty(st.CxModel) ? "unknown" : st.CxModel);
		usageEvent.Session = Intern(st.CxSession);
		usageEvent.Cwd = Intern(st.CxCwd);
		usageEvent.Messages = messages;
		usageEvent.Tools = tools;
		usageEvent.Active = 1;
		return usageEvent;
	}

	private static string FirstText(Dictionary<string, object> item)
	{
		object[] array = (item.ContainsKey("content") ? (item["content"] as object[]) : null);
		if (array == null)
		{
			return string.Empty;
		}
		object[] array2 = array;
		foreach (object obj in array2)
		{
			if (obj is Dictionary<string, object> dictionary && dictionary.ContainsKey("text"))
			{
				return (Convert.ToString(dictionary["text"]) ?? string.Empty).TrimStart();
			}
		}
		return string.Empty;
	}

	private void Add(UsageEvent e, List<UsageEvent> changed)
	{
		if (e.Utc < CutoffTicks)
		{
			return;
		}
		lock (_gate)
		{
			if (!_events.TryGetValue(e.Key, out var value))
			{
				_events[e.Key] = e;
				changed?.Add(e);
				return;
			}
			bool flag = e.In > value.In || e.Out > value.Out || e.W5 > value.W5 || e.W1h > value.W1h || e.Read > value.Read;
			value.In = Math.Max(value.In, e.In);
			value.Out = Math.Max(value.Out, e.Out);
			value.W5 = Math.Max(value.W5, e.W5);
			value.W1h = Math.Max(value.W1h, e.W1h);
			value.Read = Math.Max(value.Read, e.Read);
			if (e.Utc < value.Utc)
			{
				value.Utc = e.Utc;
			}
			if (flag)
			{
				changed?.Add(value);
			}
		}
	}

	private string Intern(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return null;
		}
		lock (_intern)
		{
			if (_intern.TryGetValue(s, out var value))
			{
				return value;
			}
			_intern[s] = s;
			return s;
		}
	}

	private double? CostOf(UsageEvent e)
	{
		if (e.Tokens == 0)
		{
			return 0.0;
		}
		ModelPrice modelPrice = _prices.Find(e.Model);
		if (modelPrice == null)
		{
			return null;
		}
		double num = modelPrice.Cost(e.In, e.Out, e.W5, e.W1h, e.Read);
		if (e.Fast)
		{
			string text = PricingTable.Normalize(e.Model);
			if (text != "claude-opus-5" && text != "claude-opus-5-5")
			{
				return null;
			}
			num *= 2.0;
		}
		return num;
	}

	private void Aggregate()
	{
		List<UsageEvent> list;
		lock (_gate)
		{
			list = _events.Values.ToList();
		}
		DateTime now = DateTime.Now;
		DateTime date = now.Date;
		DateTime dateTime = new DateTime(now.Year, now.Month, 1);
		Dictionary<AICodingToolType, Acc> dictionary = new Dictionary<AICodingToolType, Acc>();
		foreach (AICodingToolType value3 in Enum.GetValues(typeof(AICodingToolType)))
		{
			dictionary[value3] = new Acc();
		}
		ActivityDays activityDays = new ActivityDays();
		foreach (UsageEvent item in list)
		{
			activityDays.Add(item.Agent.ToString(), item.Session, new DateTime(item.Utc, DateTimeKind.Utc).ToLocalTime().Date, item.Active == 1);
		}
		foreach (KeyValuePair<AICodingToolType, Acc> item2 in dictionary)
		{
			item2.Value.Days.UnionWith(activityDays.Days((item2.Key == AICodingToolType.All) ? null : item2.Key.ToString()));
		}
		foreach (UsageEvent item3 in list)
		{
			if (item3.Active != 1)
			{
				continue;
			}
			DateTime dateTime2 = new DateTime(item3.Utc, DateTimeKind.Utc).ToLocalTime();
			double? num = CostOf(item3);
			Acc[] array = new Acc[2]
			{
				dictionary[item3.Agent],
				dictionary[AICodingToolType.All]
			};
			foreach (Acc acc in array)
			{
				if (item3.Utc > acc.LatestUtc && !string.IsNullOrEmpty(item3.Cwd))
				{
					acc.LatestUtc = item3.Utc;
					acc.LatestCwd = item3.Cwd;
				}
				if (dateTime2 >= dateTime)
				{
					acc.MonthTokens += item3.Tokens;
					if (num.HasValue)
					{
						acc.MonthCost += num.Value;
					}
					else
					{
						acc.MonthUnpriced += item3.Tokens;
					}
				}
				if (dateTime2.Date != date)
				{
					continue;
				}
				acc.Tokens += item3.Tokens;
				acc.In += item3.In;
				acc.Out += item3.Out;
				acc.Write += item3.W5 + item3.W1h;
				acc.Read += item3.Read;
				acc.Messages += item3.Messages;
				acc.Tools += item3.Tools;
				if (!string.IsNullOrEmpty(item3.Session))
				{
					acc.Sessions.Add(string.Concat(item3.Agent, ":", item3.Session));
				}
				acc.Hours[dateTime2.Hour] += item3.Tokens;
				if (num.HasValue)
				{
					acc.Cost += num.Value;
				}
				else
				{
					acc.Unpriced += item3.Tokens;
					if (!string.IsNullOrEmpty(item3.Model))
					{
						acc.UnpricedModels.Add(item3.Model);
					}
				}
				if (item3.Tokens > 0 && acc == dictionary[item3.Agent])
				{
					string text = ModelLabel(item3.Model);
					if (!acc.Models.TryGetValue(text, out var value))
					{
						UsageRow usageRow = new UsageRow();
						usageRow.Name = text;
						usageRow.Agent = item3.Agent;
						usageRow.Status = AgentStatus.Tracked;
						value = usageRow;
						acc.Models[text] = value;
					}
					value.Tokens += item3.Tokens;
					value.Input += item3.In;
					value.Output += item3.Out;
					value.CacheWrite += item3.W5 + item3.W1h;
					value.CacheRead += item3.Read;
					if (num.HasValue)
					{
						value.Cost += num.Value;
					}
					else
					{
						value.HasUnpriced = true;
					}
				}
			}
		}
		Dictionary<AICodingToolType, AgentStatus> dictionary2 = new Dictionary<AICodingToolType, AgentStatus>();
		List<AICodingToolType> list2 = new List<AICodingToolType>();
		AICodingToolType[] agents = Agents;
		foreach (AICodingToolType aICodingToolType in agents)
		{
			AgentStatus agentStatus = DetectStatus(aICodingToolType);
			if (agentStatus == AgentStatus.NotDetected && dictionary[aICodingToolType].Days.Count > 0)
			{
				agentStatus = AgentStatus.Tracked;
			}
			dictionary2[aICodingToolType] = agentStatus;
			if (agentStatus == AgentStatus.Tracked)
			{
				list2.Add(aICodingToolType);
			}
		}
		Dictionary<AICodingToolType, AICodingSnapshot> dictionary3 = new Dictionary<AICodingToolType, AICodingSnapshot>();
		foreach (KeyValuePair<AICodingToolType, Acc> item4 in dictionary)
		{
			Acc value2 = item4.Value;
			AICodingSnapshot aICodingSnapshot = new AICodingSnapshot(item4.Key);
			aICodingSnapshot.Status = ((item4.Key != 0) ? dictionary2[item4.Key] : (dictionary2.Values.Any((AgentStatus v) => v == AgentStatus.Tracked) ? AgentStatus.Tracked : AgentStatus.NotDetected));
			aICodingSnapshot.TokensToday = value2.Tokens;
			aICodingSnapshot.CostToday = value2.Cost;
			aICodingSnapshot.UnpricedTokensToday = value2.Unpriced;
			aICodingSnapshot.UnpricedModels = value2.UnpricedModels.OrderBy((string x) => x).ToList();
			aICodingSnapshot.MessagesToday = value2.Messages;
			aICodingSnapshot.ToolCallsToday = value2.Tools;
			aICodingSnapshot.SessionsToday = value2.Sessions.Count;
			aICodingSnapshot.HourlyTokens = value2.Hours;
			aICodingSnapshot.CurrentProjectPath = value2.LatestCwd;
			aICodingSnapshot.CurrentProject = ProjectName(value2.LatestCwd);
			aICodingSnapshot.StreakDays = ActivityDays.Streak(value2.Days, date);
			aICodingSnapshot.ActiveDays = value2.Days.Count;
			aICodingSnapshot.CostThisMonth = value2.MonthCost;
			aICodingSnapshot.TokensThisMonth = value2.MonthTokens;
			aICodingSnapshot.UnpricedTokensThisMonth = value2.MonthUnpriced;
			aICodingSnapshot.MonthlyBudget = _budgets.Get(item4.Key);
			aICodingSnapshot.LastUpdated = now;
			AICodingSnapshot aICodingSnapshot2 = aICodingSnapshot;
			if (item4.Key == AICodingToolType.All)
			{
				foreach (AICodingToolType item5 in list2)
				{
					aICodingSnapshot2.Rows.Add(new UsageRow
					{
						Name = AgentName(item5),
						Agent = item5,
						Status = AgentStatus.Tracked,
						Tokens = dictionary[item5].Tokens,
						Cost = dictionary[item5].Cost,
						HasUnpriced = (dictionary[item5].Unpriced > 0),
						Input = dictionary[item5].In,
						Output = dictionary[item5].Out,
						CacheWrite = dictionary[item5].Write,
						CacheRead = dictionary[item5].Read
					});
				}
				aICodingSnapshot2.Rows = aICodingSnapshot2.Rows.OrderByDescending((UsageRow r) => r.Tokens).ToList();
			}
			else
			{
				aICodingSnapshot2.Rows = value2.Models.Values.OrderByDescending((UsageRow r) => r.Tokens).ToList();
			}
			dictionary3[item4.Key] = aICodingSnapshot2;
		}
		lock (_gate)
		{
			_snapshots = dictionary3;
			_detected = list2;
		}
	}

	private static string ProjectName(string cwd)
	{
		if (string.IsNullOrEmpty(cwd))
		{
			return null;
		}
		string text = cwd.TrimEnd('\\', '/');
		int num = Math.Max(text.LastIndexOf('\\'), text.LastIndexOf('/'));
		if (num < 0)
		{
			return text;
		}
		return text.Substring(num + 1);
	}

	public static string AgentName(AICodingToolType t)
	{
		return t switch
		{
			AICodingToolType.ClaudeCode => "Claude Code", 
			AICodingToolType.Codex => "Codex", 
			AICodingToolType.OpenCode => "OpenCode", 
			_ => "All", 
		};
	}

	public static string ModelLabel(string model)
	{
		string text = PricingTable.Normalize(model);
		if (string.IsNullOrEmpty(text) || text == "unknown")
		{
			return "Unknown model";
		}
		Match match = Regex.Match(text, "^claude-(opus|sonnet|haiku|fable|mythos)-(\\d+)(?:-(\\d+))?$");
		if (match.Success)
		{
			return Cap(match.Groups[1].Value) + " " + match.Groups[2].Value + (match.Groups[3].Success ? ("." + match.Groups[3].Value) : "");
		}
		Match match2 = Regex.Match(text, "^claude-(\\d+)(?:-(\\d+))?-(opus|sonnet|haiku)$");
		if (match2.Success)
		{
			return Cap(match2.Groups[3].Value) + " " + match2.Groups[1].Value + (match2.Groups[2].Success ? ("." + match2.Groups[2].Value) : "");
		}
		return text;
	}

	private static string Cap(string s)
	{
		if (s.Length != 0)
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1);
		}
		return s;
	}

	private void CheckBudgetAlerts()
	{
		string month = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		foreach (AICodingToolType value in Enum.GetValues(typeof(AICodingToolType)))
		{
			AICodingSnapshot snapshot = GetSnapshot(value);
			if (snapshot.MonthlyBudget <= 0.0)
			{
				continue;
			}
			double num = snapshot.CostThisMonth / snapshot.MonthlyBudget * 100.0;
			int num2 = ((num >= 100.0) ? 100 : ((num >= 75.0) ? 75 : 0));
			if (num2 == 0)
			{
				continue;
			}
			string item = string.Concat(month, "|", value, "|", num2);
			lock (_gate)
			{
				if (_budgets.AlertsShown.Contains(item))
				{
					continue;
				}
				_budgets.AlertsShown.RemoveAll((string k) => !k.StartsWith(month, StringComparison.Ordinal));
				_budgets.AlertsShown.Add(item);
				if (num2 == 100)
				{
					_budgets.AlertsShown.Add(string.Concat(month, "|", value, "|75"));
				}
				_budgets.Save();
				goto IL_01b7;
			}
			IL_01b7:
			TriggerNotchBudgetAlert((value == AICodingToolType.All) ? "AI Coding" : AgentName(value), snapshot.CostThisMonth, snapshot.MonthlyBudget, (int)Math.Floor(num));
		}
	}

	public void TriggerNotchBudgetAlert(string toolName, double spend, double budget, int pct)
	{
		Action action = delegate
		{
			try
			{
				if (NotchConfig.Instance.ShowBudgetAlertInNotch)
				{
					int num = 5;
					try
					{
						num = Math.Max(1, NotchBehaviorConfig.Instance.EventDurationSeconds);
					}
					catch
					{
					}
					DynamicActivityService.Instance.PublishActivity(new NotchActivity
					{
						Id = "aicoding_budget_alert",
						Type = ActivityType.Custom,
						Priority = ActivityPriority.P2_Timer,
						Mode = ActivityMode.Temporary,
						Duration = TimeSpan.FromSeconds(num),
						Title = string.Format(CultureInfo.CurrentCulture, "{0} at {1}%", toolName, pct),
						Subtitle = string.Format(CultureInfo.CurrentCulture, "≈ ${0:N2} of your ${1} budget", spend, budget.ToString((budget == Math.Floor(budget)) ? "N0" : "N2", CultureInfo.CurrentCulture)),
						NavigateToToolId = "aicoding",
						IconFactory = (double sz, Brush br) => VectorIcons.CreateWarningTriangleIcon(sz, br)
					});
				}
			}
			catch
			{
			}
		};
		Application current = Application.Current;
		if (current != null && current.Dispatcher != null && !current.Dispatcher.CheckAccess())
		{
			current.Dispatcher.BeginInvoke(action);
		}
		else
		{
			action();
		}
	}

	private void LoadCache()
	{
		try
		{
			if (Directory.Exists(_cacheDir))
			{
				string[] files = Directory.GetFiles(_cacheDir, "*-v*.*");
				foreach (string path in files)
				{
					string fileName = Path.GetFileName(path);
					if ((fileName.StartsWith("events-", StringComparison.Ordinal) || fileName.StartsWith("files-", StringComparison.Ordinal)) && fileName != Path.GetFileName(EventsPath) && fileName != Path.GetFileName(FilesPath))
					{
						File.Delete(path);
					}
				}
			}
		}
		catch
		{
		}
		try
		{
			if (File.Exists(FilesPath))
			{
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				javaScriptSerializer.MaxJsonLength = int.MaxValue;
				JavaScriptSerializer javaScriptSerializer2 = javaScriptSerializer;
				Dictionary<string, LogFileState> dictionary = javaScriptSerializer2.Deserialize<Dictionary<string, LogFileState>>(PersistenceHelper.ReadShared(FilesPath));
				if (dictionary != null)
				{
					_files = new Dictionary<string, LogFileState>(dictionary, StringComparer.OrdinalIgnoreCase);
				}
			}
			if (!File.Exists(EventsPath))
			{
				return;
			}
			foreach (string item in File.ReadLines(EventsPath))
			{
				_tsvLines++;
				UsageEvent usageEvent = FromTsv(item);
				if (usageEvent != null)
				{
					Add(usageEvent, null);
				}
			}
		}
		catch (Exception ex)
		{
			Log("AICoding cache reset: " + ex.Message);
			lock (_gate)
			{
				_events.Clear();
			}
			_files = new Dictionary<string, LogFileState>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private void SaveCache(List<UsageEvent> changed)
	{
		long passToken;
		lock (_gate)
		{
			passToken = _passToken;
		}
		if (!Fence.IsCurrent(passToken) || !PersistenceHelper.TryBeginCacheWrite())
		{
			return;
		}
		try
		{
			Directory.CreateDirectory(_cacheDir);
			if (changed.Count > 0)
			{
				using StreamWriter streamWriter = new StreamWriter(EventsPath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				foreach (UsageEvent item in changed)
				{
					streamWriter.WriteLine(ToTsv(item));
				}
			}
			_tsvLines += changed.Count;
			int count;
			lock (_gate)
			{
				count = _events.Count;
			}
			if (_tsvLines > count * 2 + 500)
			{
				CompactEvents();
			}
			PersistenceHelper.WriteCacheText(FilesPath, new JavaScriptSerializer
			{
				MaxJsonLength = int.MaxValue
			}.Serialize(_files));
		}
		catch (Exception ex)
		{
			Log("AICoding cache save failed: " + ex.Message);
		}
		finally
		{
			PersistenceHelper.EndCacheWrite();
		}
	}

	private void CompactEvents()
	{
		long cutoff = CutoffTicks;
		List<UsageEvent> list;
		lock (_gate)
		{
			foreach (string item in (from kv in _events
				where kv.Value.Utc < cutoff
				select kv.Key).ToList())
			{
				_events.Remove(item);
			}
			list = _events.Values.ToList();
		}
		string text = EventsPath + ".tmp";
		using (StreamWriter streamWriter = new StreamWriter(text, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
		{
			foreach (UsageEvent item2 in list)
			{
				streamWriter.WriteLine(ToTsv(item2));
			}
		}
		if (File.Exists(EventsPath))
		{
			File.Replace(text, EventsPath, null);
		}
		else
		{
			File.Move(text, EventsPath);
		}
		Log("AICoding cache compacted: " + _tsvLines + " lines -> " + list.Count);
		_tsvLines = list.Count;
		foreach (string item3 in _files.Keys.Where((string p) => !File.Exists(p)).ToList())
		{
			_files.Remove(item3);
		}
	}

	private static string Clean(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
		}
		return "";
	}

	private static string ToTsv(UsageEvent e)
	{
		string[] array = new string[15]
		{
			Clean(e.Key),
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		int agent = (int)e.Agent;
		array[1] = agent.ToString(CultureInfo.InvariantCulture);
		array[2] = e.Utc.ToString(CultureInfo.InvariantCulture);
		array[3] = Clean(e.Model);
		array[4] = Clean(e.Session);
		array[5] = Clean(e.Cwd);
		array[6] = e.In.ToString(CultureInfo.InvariantCulture);
		array[7] = e.Out.ToString(CultureInfo.InvariantCulture);
		array[8] = e.W5.ToString(CultureInfo.InvariantCulture);
		array[9] = e.W1h.ToString(CultureInfo.InvariantCulture);
		array[10] = e.Read.ToString(CultureInfo.InvariantCulture);
		array[11] = (e.Fast ? "1" : "0");
		array[12] = e.Messages.ToString(CultureInfo.InvariantCulture);
		array[13] = e.Tools.ToString(CultureInfo.InvariantCulture);
		array[14] = e.Active.ToString(CultureInfo.InvariantCulture);
		return string.Join("\t", array);
	}

	private UsageEvent FromTsv(string line)
	{
		string[] f = line.Split('\t');
		if (f.Length < 15)
		{
			return null;
		}
		Func<int, long> func = (int i) => long.Parse(f[i], CultureInfo.InvariantCulture);
		Func<int, string> func2 = (int i) => (f[i].Length != 0) ? Intern(f[i]) : null;
		UsageEvent usageEvent = new UsageEvent();
		usageEvent.Key = f[0];
		usageEvent.Agent = (AICodingToolType)int.Parse(f[1], CultureInfo.InvariantCulture);
		usageEvent.Utc = func(2);
		usageEvent.Model = func2(3);
		usageEvent.Session = func2(4);
		usageEvent.Cwd = func2(5);
		usageEvent.In = func(6);
		usageEvent.Out = func(7);
		usageEvent.W5 = func(8);
		usageEvent.W1h = func(9);
		usageEvent.Read = func(10);
		usageEvent.Fast = f[11] == "1";
		usageEvent.Messages = (int)func(12);
		usageEvent.Tools = (int)func(13);
		usageEvent.Active = (int)func(14);
		return usageEvent;
	}

	private void SetupWatchers()
	{
		foreach (string item in ClaudeProjectDirs.Concat(new string[1] { Path.Combine(CodexRoot, "sessions") }))
		{
			try
			{
				if (Directory.Exists(item))
				{
					FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(item, "*.jsonl");
					fileSystemWatcher.IncludeSubdirectories = true;
					fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite;
					FileSystemWatcher fileSystemWatcher2 = fileSystemWatcher;
					fileSystemWatcher2.Changed += delegate
					{
						Debounce();
					};
					fileSystemWatcher2.Created += delegate
					{
						Debounce();
					};
					fileSystemWatcher2.EnableRaisingEvents = true;
					_watchers.Add(fileSystemWatcher2);
				}
			}
			catch
			{
			}
		}
	}

	private void Debounce()
	{
		lock (_gate)
		{
			if (_debounce == null)
			{
				_debounce = new System.Threading.Timer(delegate
				{
					RequestRefresh();
				}, null, -1, -1);
			}
			_debounce.Change(3000, -1);
		}
	}

	private void RaiseUpdated()
	{
		EventHandler updated = this.Updated;
		if (updated != null)
		{
			try
			{
				updated(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	private static Dictionary<string, object> Json(string line)
	{
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			javaScriptSerializer.MaxJsonLength = int.MaxValue;
			javaScriptSerializer.RecursionLimit = 256;
			return javaScriptSerializer.DeserializeObject(line) as Dictionary<string, object>;
		}
		catch
		{
			return null;
		}
	}

	private static string Str(Dictionary<string, object> d, string key)
	{
		if (d == null || !d.TryGetValue(key, out var value) || value == null)
		{
			return string.Empty;
		}
		return Convert.ToString(value, CultureInfo.InvariantCulture);
	}

	private static long Num(Dictionary<string, object> d, string key)
	{
		if (d == null || !d.TryGetValue(key, out var value) || value == null)
		{
			return 0L;
		}
		try
		{
			return Convert.ToInt64(Convert.ToDecimal(value, CultureInfo.InvariantCulture));
		}
		catch
		{
			return 0L;
		}
	}

	private static long ParseUtc(string iso)
	{
		if (string.IsNullOrEmpty(iso))
		{
			return 0L;
		}
		if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
		{
			return 0L;
		}
		return result.Ticks;
	}

	private static string Match(Regex rx, string s)
	{
		Match match = rx.Match(s);
		if (!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value;
	}

	private static string Unescape(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		try
		{
			return Regex.Unescape(s.Replace("\\/", "/"));
		}
		catch
		{
			return s;
		}
	}

	private static void Log(string msg)
	{
		try
		{
			TransactionLogger.Warn(msg);
		}
		catch
		{
		}
	}
}
