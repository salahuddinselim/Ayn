using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Lifecycle;
using WindowsBuddy.Tools.Notes;

namespace WindowsBuddy.Tools.Clipboard;

public class ClipboardService : IShutdownParticipant
{
	private sealed class Command
	{
		public OperationReceipt Receipt;

		public Action Apply;

		public Action Published;

		public Action Failure;

		public bool Applied;

		public bool Failed;

		public Func<bool> RequiresPersistence;
	}

	private const int WM_CLIPBOARDUPDATE = 797;

	private const uint PROCESS_QUERY_LIMITED_INFORMATION = 4096u;

	public const int MaxTextBytes = 1048576;

	private const int MaxRichSourceBytes = 65536;

	public const long MaxImageBytes = 5242880L;

	public const long MaxTotalStorageBytes = 209715200L;

	private const long MaxImagePixels = 50000000L;

	public const int ThumbPixels = 80;

	private const int MaxThumbCacheSize = 120;

	private const string ProtectedHistoryPrefix = "VAYLI-CLIP-DPAPI1:";

	private static ClipboardService _instance;

	private static readonly object InstanceLock = new object();

	private static readonly TimeSpan RepeatWindow = TimeSpan.FromMilliseconds(500.0);

	private static readonly TimeSpan SelfCopyWindow = TimeSpan.FromSeconds(2.0);

	private readonly object _operationLock = new object();

	private readonly List<Command> _commands = new List<Command>();

	private readonly List<ClipboardItem> _deletions = new List<ClipboardItem>();

	private readonly Dictionary<string, ClipboardItem> _undoRetained = new Dictionary<string, ClipboardItem>();

	private Task _drain = Task.FromResult(0);

	private bool _writing;

	private bool _nativeEnabled;

	private readonly ManualResetEventSlim _saveNow = new ManualResetEventSlim(initialState: false);

	private volatile bool _paused;

	private volatile bool _disposed;

	private Exception _loadFailure;

	private Func<string, string, bool> _persist;

	private Action<BitmapSource, string> _imageWrite;

	public static Action PrepareEditor;

	private static readonly byte[] PngSignature = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };

	private static readonly byte[] PngEnd = new byte[12]
	{
		0, 0, 0, 0, 73, 69, 78, 68, 174, 66,
		96, 130
	};

	private readonly List<ClipboardItem> _items = new List<ClipboardItem>();

	private readonly string _clipDir;

	private readonly string _imageDir;

	private readonly string _storagePath;

	private IntPtr _listenerHwnd = IntPtr.Zero;

	private HwndSource _hwndSource;

	private bool _isListenerRegistered;

	private bool _isMonitoring = true;

	private int _maxHistoryCount = 500;

	private System.Threading.Timer _maintenanceTimer;

	private long _version;

	private uint _lastSequence;

	private string _lastHash;

	private DateTime _lastHashUtc = DateTime.MinValue;

	private volatile string _selfCopyHash;

	private DateTime _selfCopyUntil = DateTime.MinValue;

	private static readonly Dictionary<string, ImageSource> _thumbCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

	private static readonly LinkedList<string> _thumbOrder = new LinkedList<string>();

	private static readonly object _thumbLock = new object();

	private readonly HashSet<string> _thumbsPending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private Dispatcher _worker;

	private DispatcherTimer _debounce;

	private readonly object _saveLock = new object();

	private Func<uint> _sequence = GetClipboardSequenceNumber;

	private static readonly string[] CodeStarts = new string[44]
	{
		"{", "[", "<", "#include", "#!", "//", "/*", "import ", "from ", "export ",
		"package ", "using ", "namespace ", "public ", "private ", "protected ", "internal ", "static ", "class ", "struct ",
		"interface ", "enum ", "function ", "func ", "fn ", "def ", "const ", "let ", "var ", "async ",
		"return ", "if (", "for (", "while (", "SELECT ", "INSERT ", "UPDATE ", "DELETE ", "CREATE ", "@echo",
		"$ ", "git ", "npm ", "pip "
	};

	private static readonly string[] CodeTokens = new string[16]
	{
		"=>", "();", "){", ") {", "};", "</", "/>", "!=", "==", "&&",
		"||", "::", "->", "+=", "${", "#{"
	};

	private static readonly HashSet<string> _commonTlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"com", "org", "net", "edu", "gov", "mil", "int", "io", "dev", "app",
		"co", "ai", "sh", "me", "so", "xyz", "info", "biz", "tech", "site",
		"online", "cloud", "store", "uk", "ca", "de", "jp", "fr", "au", "in",
		"cn", "ru", "nl", "br", "eu", "ch", "se", "no", "es", "it",
		"nz", "kr", "sg", "hk", "tv", "cc", "fm", "gg", "design", "page",
		"live", "link", "pro", "space", "ly", "to", "us"
	};

	private Action<ClipboardItem> _restoreNative;

	[ThreadStatic]
	private static HashSet<string> _loadVerifiedDirs;

	public static ClipboardService Instance
	{
		get
		{
			lock (InstanceLock)
			{
				if (_instance == null)
				{
					ClipboardService clipboardService = new ClipboardService();
					NotesOwnership.Registry.Register(clipboardService);
					_instance = clipboardService;
				}
				return _instance;
			}
		}
	}

	public OperationGate Operations { get; private set; }

	public string Id => "clipboard";

	public PersistenceClass Classification => PersistenceClass.CriticalUserData;

	public long Version => Interlocked.Read(ref _version);

	public bool IsMonitoring
	{
		get
		{
			return _isMonitoring;
		}
		set
		{
			if (_disposed)
			{
				throw new InvalidOperationException("Clipboard admission is closed.");
			}
			if (_isMonitoring == value)
			{
				return;
			}
			AppPreferences.Instance.ClipboardRecording = value;
			_isMonitoring = value;
			if (!_paused && Operations.AdmissionOpen)
			{
				if (_isMonitoring && _nativeEnabled)
				{
					StartMonitoring();
				}
				else
				{
					StopMonitoring();
				}
			}
			EventHandler monitoringStateChanged = this.MonitoringStateChanged;
			if (monitoringStateChanged == null)
			{
				return;
			}
			try
			{
				monitoringStateChanged(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	public int MaxHistoryCount
	{
		get
		{
			return _maxHistoryCount;
		}
		set
		{
			if (_disposed)
			{
				throw new InvalidOperationException("Clipboard admission is closed.");
			}
			int num = Math.Max(1, value);
			AppPreferences.Instance.ClipboardHistoryLimit = num;
			if (num != _maxHistoryCount)
			{
				_maxHistoryCount = num;
				TriggerMaintenance();
			}
		}
	}

	public int RetentionDays
	{
		get
		{
			return AppPreferences.Instance.ClipboardRetentionDays;
		}
		set
		{
			if (_disposed)
			{
				throw new InvalidOperationException("Clipboard admission is closed.");
			}
			AppPreferences.Instance.ClipboardRetentionDays = value;
			TriggerMaintenance();
		}
	}

	public IReadOnlyList<ClipboardItem> Items
	{
		get
		{
			lock (_items)
			{
				return (from i in _items
					orderby i.IsPinned descending, i.TimestampTicks descending
					select i.CloneForPersistence()).ToList();
			}
		}
	}

	private static Dispatcher UiDispatcher
	{
		get
		{
			if (Application.Current == null)
			{
				return Dispatcher.CurrentDispatcher;
			}
			return Application.Current.Dispatcher;
		}
	}

	private Dispatcher Worker
	{
		get
		{
			if (_worker != null)
			{
				return _worker;
			}
			lock (_saveLock)
			{
				if (_worker != null)
				{
					return _worker;
				}
				ManualResetEventSlim ready = new ManualResetEventSlim(initialState: false);
				Dispatcher d = null;
				Thread thread = new Thread((ThreadStart)delegate
				{
					d = Dispatcher.CurrentDispatcher;
					ready.Set();
					Dispatcher.Run();
				});
				thread.Name = "WindowsBuddy.Clipboard";
				thread.IsBackground = true;
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				ready.Wait(2000);
				_worker = d;
				return _worker;
			}
		}
	}

	public event EventHandler ItemsChanged;

	public event Action<string> TextCopied;

	public event EventHandler MonitoringStateChanged;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool AddClipboardFormatListener(IntPtr hwnd);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

	[DllImport("user32.dll")]
	private static extern uint GetClipboardSequenceNumber();

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr GetClipboardOwner();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr GetOpenClipboardWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder exeName, ref int size);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr handle);

	private void Submit(Action apply, Action published = null, Action failure = null, Func<bool> requiresPersistence = null)
	{
		lock (_operationLock)
		{
			if (_disposed)
			{
				throw new InvalidOperationException("Clipboard owner is disposed.");
			}
			if (_loadFailure != null)
			{
				throw new InvalidDataException("Clipboard history requires recovery.", _loadFailure);
			}
			OperationReceipt operationReceipt = Operations.TryAccept();
			if (operationReceipt == null)
			{
				throw new InvalidOperationException("Clipboard admission is closed.");
			}
			_commands.Add(new Command
			{
				Receipt = operationReceipt,
				Apply = apply,
				Published = published,
				Failure = failure,
				RequiresPersistence = requiresPersistence
			});
			StartWriterLocked();
		}
	}

	private void StartWriterLocked()
	{
		if (!_writing && _commands.Count != 0)
		{
			_writing = true;
			_drain = Task.Run((Action)WriteLoop);
		}
	}

	private void WriteLoop()
	{
		while (true)
		{
			Command[] array;
			lock (_operationLock)
			{
				if (_commands.Count == 0)
				{
					_writing = false;
					break;
				}
				array = _commands.ToArray();
				Command[] array2 = array;
				foreach (Command command2 in array2)
				{
					if (command2.Failed)
					{
						command2.Receipt = Operations.Retry(command2.Receipt.Revision);
						command2.Failed = false;
					}
				}
			}
			try
			{
				Command[] array3 = array;
				foreach (Command command3 in array3)
				{
					command3.Receipt.AssertWriteAllowed();
					if (!command3.Applied)
					{
						command3.Apply();
						command3.Applied = true;
					}
				}
				NotifyChanged();
				_saveNow.Wait(500);
				_saveNow.Reset();
				lock (_operationLock)
				{
					array = _commands.ToArray();
				}
				Command[] array4 = array;
				foreach (Command command4 in array4)
				{
					command4.Receipt.AssertWriteAllowed();
					if (!command4.Applied)
					{
						command4.Apply();
						command4.Applied = true;
					}
				}
				if (array.Any((Command command) => command.RequiresPersistence == null || command.RequiresPersistence()))
				{
					string arg = SerializeSnapshot();
					Command[] array5 = array;
					foreach (Command command5 in array5)
					{
						command5.Receipt.AssertWriteAllowed();
					}
					if (!_persist(_storagePath, arg))
					{
						throw new IOException("Clipboard history write failed.");
					}
					ReclaimRetiredFiles();
				}
				Command[] array6 = array;
				foreach (Command command6 in array6)
				{
					command6.Receipt.CompleteDurably();
				}
				lock (_operationLock)
				{
					_commands.RemoveRange(0, array.Length);
				}
				NotifyChanged();
				Command[] array7 = array;
				foreach (Command command7 in array7)
				{
					if (command7.Published != null)
					{
						DispatchWhileActive(command7.Published);
					}
				}
			}
			catch (Exception error)
			{
				lock (_operationLock)
				{
					foreach (Command command9 in _commands)
					{
						if (!command9.Failed)
						{
							command9.Receipt.Fail();
							command9.Failed = true;
						}
					}
				}
				PersistenceHelper.ReportSaveFailure(_storagePath, error);
				Command[] array8 = array;
				foreach (Command command8 in array8)
				{
					if (command8.Failure != null)
					{
						DispatchWhileActive(command8.Failure);
					}
				}
				lock (_operationLock)
				{
					foreach (Command command10 in _commands)
					{
						if (!command10.Failed)
						{
							command10.Receipt.Fail();
							command10.Failed = true;
						}
					}
					_writing = false;
					break;
				}
			}
		}
	}

	private void ValidateStoredImage(ClipboardItem item)
	{
		ValidateStoredImage(item, deep: true);
	}

	private void ValidateStoredImage(ClipboardItem item, bool deep)
	{
		if (!ValidateItemPaths(item))
		{
			throw new IOException("Unsafe clipboard sidecar.");
		}
		if (item.Kind == ClipKind.Image)
		{
			if (string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath))
			{
				throw new IOException("Clipboard image is not complete.");
			}
			if (!string.IsNullOrEmpty(item.ThumbPath) && !File.Exists(item.ThumbPath))
			{
				throw new IOException("Clipboard thumbnail is not complete.");
			}
			if (new FileInfo(item.ImagePath).Length == 0 || !IsCompletePng(item.ImagePath) || (deep && Decode(item.ImagePath, 1) == null))
			{
				throw new IOException("Clipboard image is unreadable.");
			}
			if (!string.IsNullOrEmpty(item.ThumbPath) && (new FileInfo(item.ThumbPath).Length == 0 || !IsCompletePng(item.ThumbPath) || (deep && Decode(item.ThumbPath, 1) == null)))
			{
				throw new IOException("Clipboard thumbnail is unreadable.");
			}
		}
	}

	private static bool IsCompletePng(string path)
	{
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (fileStream.Length < PngSignature.Length + PngEnd.Length)
			{
				return false;
			}
			byte[] array = new byte[PngSignature.Length];
			byte[] array2 = new byte[PngEnd.Length];
			if (fileStream.Read(array, 0, array.Length) != array.Length)
			{
				return false;
			}
			fileStream.Seek(-array2.Length, SeekOrigin.End);
			if (fileStream.Read(array2, 0, array2.Length) != array2.Length)
			{
				return false;
			}
			return array.SequenceEqual(PngSignature) && array2.SequenceEqual(PngEnd);
		}
		catch
		{
			return false;
		}
	}

	private void ReclaimRetiredFiles()
	{
		if (_deletions.Count == 0 || Directory.GetFiles(_clipDir, "history.json*.recovery.*").Length != 0)
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] array = new string[2]
		{
			_storagePath,
			_storagePath + ".bak"
		};
		foreach (string path in array)
		{
			if (!File.Exists(path))
			{
				continue;
			}
			List<ClipboardItem> list = ParseHistory(PersistenceHelper.ReadShared(path));
			if (list == null)
			{
				throw new InvalidDataException("Cannot verify backup sidecar references.");
			}
			foreach (ClipboardItem item in list)
			{
				if (item != null)
				{
					if (!string.IsNullOrEmpty(item.ImagePath))
					{
						hashSet.Add(Path.GetFullPath(item.ImagePath));
					}
					if (!string.IsNullOrEmpty(item.ThumbPath))
					{
						hashSet.Add(Path.GetFullPath(item.ThumbPath));
					}
				}
			}
		}
		ClipboardItem[] array2 = _deletions.ToArray();
		foreach (ClipboardItem clipboardItem in array2)
		{
			bool flag = false;
			string[] array3 = new string[2] { clipboardItem.ImagePath, clipboardItem.ThumbPath };
			foreach (string text in array3)
			{
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}
				if (!TryOwnedPath(text, out var canonical))
				{
					throw new IOException("Unsafe retired sidecar.");
				}
				if (hashSet.Contains(canonical))
				{
					flag = true;
					continue;
				}
				if (File.Exists(canonical))
				{
					File.Delete(canonical);
				}
				ForgetThumb(canonical);
			}
			if (!flag)
			{
				_deletions.Remove(clipboardItem);
			}
		}
	}

	private void DispatchWhileActive(Action callback)
	{
		if (_paused || _disposed || Application.Current == null)
		{
			return;
		}
		Dispatcher dispatcher = Application.Current.Dispatcher;
		try
		{
			dispatcher.BeginInvoke((Action)delegate
			{
				if (_paused || _disposed)
				{
					return;
				}
				try
				{
					callback();
				}
				catch (Exception ex2)
				{
					TransactionLogger.Warn("Clipboard callback failed: " + ex2.Message);
				}
			});
		}
		catch (InvalidOperationException ex)
		{
			TransactionLogger.Warn("Clipboard callback delivery stopped: " + ex.Message);
		}
	}

	private void BeginCapture()
	{
		if (_paused || !_isMonitoring || !_nativeEnabled || _loadFailure != null)
		{
			return;
		}
		try
		{
			SubmitCapture(ReadNativeAsync);
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void SubmitCapture(Func<Task<ClipboardItem>> read)
	{
		Task<ClipboardItem> result = null;
		ClipboardItem captured = null;
		Submit(delegate
		{
			if (result == null)
			{
				result = read();
			}
			if (captured == null)
			{
				ClipboardItem result2;
				try
				{
					result2 = result.GetAwaiter().GetResult();
				}
				catch (ThreadAbortException)
				{
					throw;
				}
				catch (Exception ex2)
				{
					TransactionLogger.Warn((ex2 is ExternalException error && IsClipboardBusy(error)) ? "Clipboard busy; capture skipped after native retries." : ("Clipboard read failed (" + ex2.GetType().Name + " 0x" + ex2.HResult.ToString("X8") + "); capture skipped."));
					return;
				}
				if (result2 != null)
				{
					captured = result2.CloneForPersistence();
				}
			}
			if (captured != null)
			{
				AddCore(captured);
			}
		}, delegate
		{
			if (captured != null)
			{
				PillActivityArbiter.Instance.PublishClipboardCopied((captured.Kind == ClipKind.Image) ? "Image" : captured.Text);
				Action<string> textCopied = this.TextCopied;
				if (textCopied != null && captured.Kind != ClipKind.Image && captured.Kind != ClipKind.Files && !string.IsNullOrEmpty(captured.Text))
				{
					textCopied(captured.Text);
				}
			}
		}, null, () => captured != null);
	}

	private Task<ClipboardItem> ReadNativeAsync()
	{
		TaskCompletionSource<ClipboardItem> result = new TaskCompletionSource<ClipboardItem>();
		try
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					Dispatcher worker = Worker;
					if (worker == null)
					{
						throw new IOException("Clipboard worker unavailable.");
					}
					worker.BeginInvoke((Action)delegate
					{
						try
						{
							result.SetResult(CaptureOnWorker());
						}
						catch (Exception exception3)
						{
							result.SetException(exception3);
						}
					});
				}
				catch (Exception exception2)
				{
					result.SetException(exception2);
				}
			});
		}
		catch (Exception exception)
		{
			result.SetException(exception);
		}
		return result.Task;
	}

	public void CaptureSynthetic(Func<ClipboardItem> capture)
	{
		if (_nativeEnabled || !AppPaths.IsSandboxed)
		{
			throw new InvalidOperationException("Synthetic owner required.");
		}
		SubmitCapture(() => Task.Run(capture));
	}

	public Task PrepareAsync(CancellationToken cancellation)
	{
		cancellation.ThrowIfCancellationRequested();
		if (PrepareEditor != null)
		{
			PrepareEditor();
		}
		return Task.FromResult(0);
	}

	public Task QuiesceAsync(CancellationToken cancellation)
	{
		_paused = true;
		StopMonitoring();
		if (_debounce != null)
		{
			_debounce.Stop();
		}
		return Task.FromResult(0);
	}

	public async Task VerifyAsync(CancellationToken cancellation)
	{
		while (true)
		{
			cancellation.ThrowIfCancellationRequested();
			_saveNow.Set();
			Task drain;
			lock (_operationLock)
			{
				StartWriterLocked();
				drain = _drain;
			}
			await drain.ConfigureAwait(continueOnCapturedContext: false);
			cancellation.ThrowIfCancellationRequested();
			lock (_operationLock)
			{
				if (_writing || !object.ReferenceEquals(drain, _drain))
				{
					continue;
				}
				AssertDurable();
				break;
			}
		}
	}

	private void AssertDurable()
	{
		OperationSnapshot operationSnapshot = Operations.Snapshot();
		if (_loadFailure != null || operationSnapshot.Pending != 0 || operationSnapshot.Failed != 0 || operationSnapshot.Violations != 0 || operationSnapshot.Accepted != operationSnapshot.Durable)
		{
			throw new IOException("Clipboard durability barrier has not been reached.", _loadFailure);
		}
	}

	public Task ResumeAsync()
	{
		_paused = false;
		NotifyChanged();
		if (_isMonitoring && _nativeEnabled)
		{
			StartMonitoringAfterReopen();
		}
		return Task.FromResult(0);
	}

	private void StartMonitoringAfterReopen()
	{
		EnsureListenerWindow();
		if (_listenerHwnd == IntPtr.Zero)
		{
			throw new IOException("Clipboard listener window restoration failed.");
		}
		if (_listenerHwnd != IntPtr.Zero && !_isListenerRegistered)
		{
			_isListenerRegistered = AddClipboardFormatListener(_listenerHwnd);
			if (!_isListenerRegistered)
			{
				throw new IOException("Clipboard listener restoration failed.");
			}
		}
	}

	public Task DisposeAsync()
	{
		AssertDurable();
		_disposed = true;
		_paused = true;
		StopMonitoring();
		if (_maintenanceTimer != null)
		{
			_maintenanceTimer.Dispose();
		}
		if (_debounce != null)
		{
			_debounce.Stop();
		}
		if (_hwndSource != null)
		{
			_hwndSource.RemoveHook(WndProc);
			_hwndSource.Dispose();
			_hwndSource = null;
		}
		if (_worker != null)
		{
			_worker.BeginInvokeShutdown(DispatcherPriority.Background);
		}
		return Task.FromResult(0);
	}

	public Action PauseForShutdown()
	{
		Operations.CloseAdmission();
		QuiesceAsync(CancellationToken.None).GetAwaiter().GetResult();
		return delegate
		{
			ResumeAsync().GetAwaiter().GetResult();
			Operations.Reopen();
		};
	}

	public void SealForDisposal()
	{
		AssertDurable();
		Operations.Seal(requireDurability: true);
		Operations.CommitSeal();
	}

	public long GetTotalSizeBytes()
	{
		long num = 0L;
		try
		{
			if (Directory.Exists(_clipDir))
			{
				foreach (string item in OwnedStorageFiles())
				{
					try
					{
						num += new FileInfo(item).Length;
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		return num;
	}

	public string GetTotalSizeFormatted()
	{
		return SettingsData.FormatBytes(GetTotalSizeBytes());
	}

	public void TriggerMaintenance()
	{
		RunMaintenanceBackground();
	}

	public void RunMaintenanceBackground()
	{
		if (_paused || !Operations.AdmissionOpen)
		{
			return;
		}
		try
		{
			Submit(delegate
			{
				int num = Math.Max(1, AppPreferences.Instance.ClipboardHistoryLimit);
				int clipboardRetentionDays = AppPreferences.Instance.ClipboardRetentionDays;
				long cutoff = ((clipboardRetentionDays > 0) ? DateTime.UtcNow.AddDays(-clipboardRetentionDays).Ticks : 0);
				List<ClipboardItem> source;
				lock (_items)
				{
					source = _items.ToList();
				}
				long num2 = source.Sum((ClipboardItem i) => EstimateItemBytes(i));
				List<ClipboardItem> removed = source.Where((ClipboardItem i) => !i.IsPinned && cutoff > 0 && i.TimestampTicks < cutoff).ToList();
				List<ClipboardItem> list = (from i in source
					where !i.IsPinned && !removed.Contains(i)
					orderby i.TimestampTicks
					select i).ToList();
				removed.AddRange(list.Take(Math.Max(0, list.Count - num)));
				foreach (ClipboardItem item in from i in source
					where !i.IsPinned
					orderby i.TimestampTicks
					select i)
				{
					if (num2 <= 209715200)
					{
						break;
					}
					if (!removed.Contains(item))
					{
						removed.Add(item);
					}
					num2 -= EstimateItemBytes(item);
				}
				lock (_items)
				{
					foreach (ClipboardItem item2 in removed)
					{
						_items.Remove(item2);
					}
				}
				foreach (ClipboardItem item3 in removed)
				{
					QueueDeletion(item3);
				}
			});
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Clipboard maintenance rejected: " + ex.Message);
		}
	}

	private long EstimateItemBytes(ClipboardItem it)
	{
		if (it == null)
		{
			return 0L;
		}
		if (!ValidateItemPaths(it))
		{
			return 0L;
		}
		long num = it.FileSizeBytes;
		if (num <= 0 && !string.IsNullOrEmpty(it.ImagePath))
		{
			try
			{
				if (File.Exists(it.ImagePath))
				{
					num = new FileInfo(it.ImagePath).Length;
				}
			}
			catch
			{
			}
		}
		if (num <= 0 && !string.IsNullOrEmpty(it.Text))
		{
			num = it.Text.Length * 2;
		}
		return Math.Max(128L, num);
	}

	private void QueueDeletion(ClipboardItem item)
	{
		if (item != null)
		{
			_deletions.Add(item.CloneForPersistence());
		}
	}

	public ClipboardService()
		: this(enableNative: true)
	{
	}

	public ClipboardService(bool enableNative)
	{
		if (!enableNative && !AppPaths.IsSandboxed)
		{
			throw new InvalidOperationException("Synthetic profile required.");
		}
		_nativeEnabled = enableNative;
		Operations = new OperationGate();
		_persist = PersistenceHelper.WriteOwnedText;
		_imageWrite = SavePng;
		string localAppData = AppPaths.LocalAppData;
		_clipDir = Path.Combine(localAppData, AppPaths.FolderName, "clipboard");
		_imageDir = Path.Combine(_clipDir, "images");
		try
		{
			Directory.CreateDirectory(_imageDir);
		}
		catch
		{
		}
		_storagePath = Path.Combine(_clipDir, "history.json");
		_maxHistoryCount = Math.Max(1, AppPreferences.Instance.ClipboardHistoryLimit);
		_isMonitoring = AppPreferences.Instance.ClipboardRecording;
		LoadHistory();
		PersistenceHelper.RegisterExitPreparation("clipboard-editor", delegate
		{
			if (PrepareEditor != null)
			{
				PrepareEditor();
			}
		});
		PersistenceHelper.RegisterExitFlush("clipboard", Flush);
		if (_isMonitoring && _nativeEnabled)
		{
			StartMonitoring();
		}
		if (_nativeEnabled)
		{
			_maintenanceTimer = new System.Threading.Timer(delegate
			{
				RunMaintenanceBackground();
			}, null, TimeSpan.FromSeconds(3.0), TimeSpan.FromHours(1.0));
		}
	}

	public void AttachWindow(IntPtr hwnd)
	{
		if (hwnd == IntPtr.Zero || _listenerHwnd == hwnd)
		{
			return;
		}
		bool isMonitoring = _isMonitoring;
		StopMonitoring();
		if (_hwndSource != null)
		{
			try
			{
				_hwndSource.Dispose();
			}
			catch
			{
			}
			_hwndSource = null;
		}
		_listenerHwnd = hwnd;
		HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
		if (isMonitoring)
		{
			StartMonitoring();
		}
	}

	public void StartMonitoring()
	{
		if (_paused || !_nativeEnabled || _loadFailure != null || !Operations.AdmissionOpen)
		{
			return;
		}
		EnsureListenerWindow();
		if (!(_listenerHwnd != IntPtr.Zero) || _isListenerRegistered)
		{
			return;
		}
		try
		{
			_isListenerRegistered = AddClipboardFormatListener(_listenerHwnd);
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Failed to register AddClipboardFormatListener: " + ex.Message, ex);
		}
	}

	public void StopMonitoring()
	{
		if (_listenerHwnd != IntPtr.Zero && _isListenerRegistered)
		{
			try
			{
				RemoveClipboardFormatListener(_listenerHwnd);
			}
			catch
			{
			}
			_isListenerRegistered = false;
		}
	}

	private void EnsureListenerWindow()
	{
		if (_listenerHwnd != IntPtr.Zero || _hwndSource != null)
		{
			return;
		}
		try
		{
			HwndSourceParameters hwndSourceParameters = new HwndSourceParameters("WindowsBuddy_ClipboardListener");
			hwndSourceParameters.WindowStyle = 0;
			hwndSourceParameters.ExtendedWindowStyle = 0;
			hwndSourceParameters.ParentWindow = new IntPtr(-3);
			HwndSourceParameters parameters = hwndSourceParameters;
			_hwndSource = new HwndSource(parameters);
			_hwndSource.AddHook(WndProc);
			_listenerHwnd = _hwndSource.Handle;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Failed to create message-only HwndSource for clipboard: " + ex.Message, ex);
		}
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg == 797)
		{
			RequestCapture();
			handled = true;
		}
		return IntPtr.Zero;
	}

	private void RequestCapture()
	{
		if (!_isMonitoring || !_nativeEnabled || _paused || _loadFailure != null || !Operations.AdmissionOpen)
		{
			return;
		}
		Dispatcher uiDispatcher = UiDispatcher;
		if (uiDispatcher == null)
		{
			return;
		}
		if (!uiDispatcher.CheckAccess())
		{
			uiDispatcher.BeginInvoke(new Action(RequestCapture));
			return;
		}
		if (_debounce == null)
		{
			_debounce = new DispatcherTimer(DispatcherPriority.Normal)
			{
				Interval = TimeSpan.FromMilliseconds(150.0)
			};
			_debounce.Tick += delegate
			{
				_debounce.Stop();
				BeginCapture();
			};
		}
		_debounce.Stop();
		_debounce.Start();
	}

	private ClipboardItem CaptureOnWorker()
	{
		if (!_nativeEnabled)
		{
			throw new InvalidOperationException("Native clipboard disabled for synthetic owner.");
		}
		try
		{
			uint num = _sequence();
			if (num != 0 && num == _lastSequence)
			{
				return null;
			}
			_lastSequence = num;
			IDataObject dataObject = Retry(() => System.Windows.Clipboard.GetDataObject());
			if (dataObject == null)
			{
				return null;
			}
			GetOwnerProcess(out var friendlyName, out var exeName, out var exePath);
			ClipboardItem clipboardItem = ReadDataObject(dataObject, num);
			if (clipboardItem == null)
			{
				return null;
			}
			DateTime utcNow = DateTime.UtcNow;
			if (clipboardItem.ContentHash != null)
			{
				if (clipboardItem.ContentHash == _selfCopyHash && utcNow < _selfCopyUntil)
				{
					return null;
				}
				if (clipboardItem.ContentHash == _lastHash && utcNow - _lastHashUtc < RepeatWindow)
				{
					return null;
				}
				_lastHash = clipboardItem.ContentHash;
				_lastHashUtc = utcNow;
			}
			clipboardItem.SourceApp = friendlyName;
			clipboardItem.SourceAppExe = exeName;
			clipboardItem.SourceAppPath = exePath;
			return clipboardItem.CloneForPersistence();
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Clipboard capture failed: " + ex.GetType().Name);
			throw;
		}
	}

	private ClipboardItem ReadDataObject(IDataObject data, uint sequence)
	{
		if (IsExcluded(data))
		{
			return null;
		}
		ClipboardItem clipboardItem = null;
		if (Retry(() => data.GetDataPresent(DataFormats.FileDrop)))
		{
			clipboardItem = ReadFiles(data);
		}
		if (clipboardItem == null && Retry(() => data.GetDataPresent(DataFormats.UnicodeText)))
		{
			clipboardItem = ReadText(data);
		}
		if (clipboardItem == null)
		{
			clipboardItem = ReadImage(data);
		}
		if (clipboardItem != null && sequence != 0 && _sequence() != sequence)
		{
			TransactionLogger.Info("Clipboard changed while it was being read; capture dropped.");
			return null;
		}
		return clipboardItem;
	}

	private static bool IsClipboardBusy(ExternalException error)
	{
		return error.HResult == -2147221040;
	}

	private static T Retry<T>(Func<T> read)
	{
		int num = 0;
		while (true)
		{
			try
			{
				return read();
			}
			catch (Exception ex)
			{
				if (num >= 5 || (!(ex is COMException) && !(ex is ExternalException)))
				{
					throw;
				}
				Thread.Sleep(30 + num * 20);
			}
			num++;
		}
	}

	private static bool IsExcluded(IDataObject data)
	{
		try
		{
			if (data.GetDataPresent("Clipboard Viewer Ignore"))
			{
				return true;
			}
			if (data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"))
			{
				return true;
			}
			if (data.GetDataPresent("CanIncludeInClipboardHistory"))
			{
				object data2 = data.GetData("CanIncludeInClipboardHistory");
				if (data2 is int && (int)data2 == 0)
				{
					return true;
				}
				if (data2 is uint && (uint)data2 == 0)
				{
					return true;
				}
				if (data2 is Stream stream)
				{
					using (stream)
					{
						byte[] array = new byte[4];
						int num = stream.Read(array, 0, 4);
						if (num > 0 && array.Take(num).All((byte b) => b == 0))
						{
							return true;
						}
					}
				}
			}
		}
		catch
		{
			return true;
		}
		return false;
	}

	private ClipboardItem ReadText(IDataObject data)
	{
		string text = Retry(() => data.GetData(DataFormats.UnicodeText) as string);
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		if (Encoding.UTF8.GetByteCount(text) > 1048576)
		{
			return null;
		}
		string type = Classify(text);
		bool flag = Encoding.UTF8.GetByteCount(text) <= 65536;
		ClipboardItem clipboardItem = new ClipboardItem(text, type);
		clipboardItem.LineCount = CountLines(text);
		clipboardItem.Html = (flag ? ReadRich(data, DataFormats.Html) : null);
		clipboardItem.Rtf = (flag ? ReadRich(data, DataFormats.Rtf) : null);
		clipboardItem.ContentHash = HashText(text);
		return clipboardItem;
	}

	private static string ReadRich(IDataObject data, string format)
	{
		try
		{
			string text = (data.GetDataPresent(format) ? (data.GetData(format) as string) : null);
			return (text != null && Encoding.UTF8.GetByteCount(text) <= 1048576) ? text : null;
		}
		catch
		{
			return null;
		}
	}

	private static ClipboardItem ReadFiles(IDataObject data)
	{
		string[] array = Retry(() => data.GetData(DataFormats.FileDrop) as string[]);
		if (array == null || array.Length == 0)
		{
			return null;
		}
		List<string> list = new List<string>(array);
		long num = 0L;
		foreach (string item in list)
		{
			try
			{
				if (File.Exists(item))
				{
					num += new FileInfo(item).Length;
				}
			}
			catch
			{
			}
		}
		string text = ((list.Count == 1) ? Path.GetFileName(list[0]) : $"{list.Count} files");
		ClipboardItem clipboardItem = new ClipboardItem(text, "files");
		clipboardItem.FilePaths = list;
		clipboardItem.FileSizeBytes = num;
		clipboardItem.ContentHash = HashText("files\n" + string.Join("\n", list));
		return clipboardItem;
	}

	private static byte[] CopyBgra(BitmapSource frame, out int w, out int h)
	{
		w = frame.PixelWidth;
		h = frame.PixelHeight;
		if (w <= 0 || h <= 0 || (long)w * (long)h > 50000000)
		{
			return null;
		}
		BitmapSource bitmapSource = ((frame.Format == PixelFormats.Bgra32) ? frame : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0.0));
		byte[] array = new byte[w * 4 * h];
		bitmapSource.CopyPixels(array, w * 4, 0);
		return array;
	}

	private ClipboardItem ReadImage(IDataObject data)
	{
		byte[] array = null;
		int w = 0;
		int h = 0;
		if (Retry(() => data.GetDataPresent("PNG")))
		{
			try
			{
				Stream stream = Retry(() => data.GetData("PNG") as Stream);
				if (stream != null)
				{
					using (stream)
					{
						BitmapDecoder bitmapDecoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
						if (bitmapDecoder.Frames.Count > 0)
						{
							BitmapFrame bitmapFrame = bitmapDecoder.Frames[0];
							if ((long)bitmapFrame.PixelWidth * (long)bitmapFrame.PixelHeight > 50000000)
							{
								return null;
							}
							array = CopyBgra(bitmapFrame, out w, out h);
						}
					}
				}
			}
			catch (OutOfMemoryException)
			{
				throw;
			}
			catch
			{
				array = null;
			}
		}
		if (array == null && Retry(() => System.Windows.Clipboard.ContainsImage()))
		{
			BitmapSource bitmapSource = Retry(() => System.Windows.Clipboard.GetImage());
			if (bitmapSource != null)
			{
				array = CopyBgra(bitmapSource, out w, out h);
			}
		}
		if (array == null)
		{
			return null;
		}
		int stride = w * 4;
		bool flag = true;
		for (int i = 3; i < array.Length; i += 4)
		{
			if (array[i] != 0)
			{
				flag = false;
				break;
			}
		}
		if (flag)
		{
			for (int j = 3; j < array.Length; j += 4)
			{
				array[j] = byte.MaxValue;
			}
		}
		string text = HashPixels(array, w, h);
		BitmapSource bitmapSource2 = BitmapSource.Create(w, h, 96.0, 96.0, PixelFormats.Bgra32, null, array, stride);
		bitmapSource2.Freeze();
		array = null;
		ClipboardItem clipboardItem = FindByHash(text);
		if (clipboardItem != null)
		{
			ClipboardItem clipboardItem2 = new ClipboardItem("Image", "image");
			clipboardItem2.ContentHash = text;
			clipboardItem2.ImageWidth = w;
			clipboardItem2.ImageHeight = h;
			return clipboardItem2;
		}
		ClipboardItem clipboardItem3 = new ClipboardItem("Image", "image", $"{w} × {h}");
		clipboardItem3.ImageWidth = w;
		clipboardItem3.ImageHeight = h;
		clipboardItem3.ContentHash = text;
		clipboardItem3.PendingImage = bitmapSource2;
		return clipboardItem3;
	}

	private static BitmapSource MakeThumbnail(BitmapSource image)
	{
		double num = 80.0 / (double)Math.Min(image.PixelWidth, image.PixelHeight);
		if (num >= 1.0)
		{
			return image;
		}
		TransformedBitmap transformedBitmap = new TransformedBitmap(image, new ScaleTransform(num, num));
		transformedBitmap.Freeze();
		return transformedBitmap;
	}

	private static void SavePng(BitmapSource image, string path)
	{
		if (!TryOwnedPath(path, out var canonical))
		{
			throw new IOException("Unsafe clipboard image destination");
		}
		path = canonical;
		using MemoryStream memoryStream = new MemoryStream();
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(image));
		pngBitmapEncoder.Save(memoryStream);
		PersistenceHelper.WriteOwnedBytes(path, memoryStream.ToArray());
	}

	private void GetOwnerProcess(out string friendlyName, out string exeName, out string exePath)
	{
		friendlyName = null;
		exeName = null;
		exePath = null;
		try
		{
			IntPtr intPtr = GetClipboardOwner();
			if (intPtr == IntPtr.Zero)
			{
				intPtr = GetOpenClipboardWindow();
			}
			if (intPtr == IntPtr.Zero)
			{
				intPtr = GetForegroundWindow();
			}
			if (intPtr == IntPtr.Zero)
			{
				return;
			}
			GetWindowThreadProcessId(intPtr, out var lpdwProcessId);
			if (lpdwProcessId != 0 && lpdwProcessId != (uint)Process.GetCurrentProcess().Id)
			{
				exePath = QueryImagePath(lpdwProcessId);
				if (!string.IsNullOrEmpty(exePath))
				{
					exeName = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
					friendlyName = FriendlyAppName(exeName, exePath);
				}
			}
		}
		catch
		{
			friendlyName = (exeName = (exePath = null));
		}
	}

	private static string QueryImagePath(uint pid)
	{
		IntPtr intPtr = OpenProcess(4096u, inherit: false, pid);
		if (intPtr == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(1024);
			int size = stringBuilder.Capacity;
			return QueryFullProcessImageName(intPtr, 0, stringBuilder, ref size) ? stringBuilder.ToString(0, size) : null;
		}
		finally
		{
			CloseHandle(intPtr);
		}
	}

	private static string FriendlyAppName(string exe, string path)
	{
		switch (exe)
		{
		case "chrome":
			return "Google Chrome";
		case "msedge":
			return "Microsoft Edge";
		case "code":
			return "Visual Studio Code";
		case "screenclippinghost":
		case "snippingtool":
			return "Snipping Tool";
		case "explorer":
			return "File Explorer";
		default:
			try
			{
				string fileDescription = FileVersionInfo.GetVersionInfo(path).FileDescription;
				if (!string.IsNullOrWhiteSpace(fileDescription))
				{
					return fileDescription.Trim();
				}
			}
			catch
			{
			}
			return exe;
		}
	}

	public static string Classify(string text)
	{
		if (IsUrlCandidate(text))
		{
			return "url";
		}
		if (DetectIsCode(text))
		{
			return "code";
		}
		return "text";
	}

	public static bool DetectIsCode(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string text2 = text.Trim();
		string[] array = text2.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
		int num = 0;
		string[] codeStarts = CodeStarts;
		foreach (string value in codeStarts)
		{
			if (text2.StartsWith(value, StringComparison.Ordinal))
			{
				num += 2;
				break;
			}
		}
		string[] codeTokens = CodeTokens;
		foreach (string value2 in codeTokens)
		{
			if (text2.IndexOf(value2, StringComparison.Ordinal) >= 0)
			{
				num++;
			}
		}
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		string[] array2 = array;
		foreach (string text3 in array2)
		{
			string text4 = text3.TrimEnd('\r');
			string text5 = text4.Trim();
			if (text5.EndsWith(";"))
			{
				num2++;
			}
			if (text5.EndsWith("{") || text5 == "}" || text5.StartsWith("}"))
			{
				num3++;
			}
			if (text4.StartsWith("    ") || text4.StartsWith("\t"))
			{
				num4++;
			}
		}
		if (num2 > 0)
		{
			num += ((array.Length == 1) ? 1 : 2);
		}
		if (num3 > 0)
		{
			num += 2;
		}
		if (array.Length > 2 && num4 * 3 >= array.Length)
		{
			num++;
		}
		return num >= 3;
	}

	public static bool IsUrlCandidate(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string text2 = text.Trim();
		if (text2.IndexOfAny(new char[4] { ' ', '\t', '\r', '\n' }) >= 0)
		{
			return false;
		}
		if (text2.IndexOf('@') > 0 && text2.IndexOf('/') < 0)
		{
			return false;
		}
		Uri result;
		if (text2.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text2.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			if (Uri.TryCreate(text2, UriKind.Absolute, out result))
			{
				return !string.IsNullOrEmpty(result.Host);
			}
			return false;
		}
		if (text2.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
		{
			return Uri.TryCreate("https://" + text2, UriKind.Absolute, out result);
		}
		if (text2.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (Uri.TryCreate("http://" + text2, UriKind.Absolute, out result))
		{
			string host = result.Host;
			int num = host.LastIndexOf('.');
			if (num > 0 && num < host.Length - 1 && _commonTlds.Contains(host.Substring(num + 1)))
			{
				string source = host.Substring(0, num);
				return source.Any(char.IsLetter);
			}
		}
		return false;
	}

	private static int CountLines(string text)
	{
		int num = 1;
		foreach (char c in text)
		{
			if (c == '\n')
			{
				num++;
			}
		}
		return num;
	}

	public static string HashText(string text)
	{
		using SHA256 sHA = SHA256.Create();
		return ToHex(sHA.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty)));
	}

	private static string HashPixels(byte[] bgra, int w, int h)
	{
		using SHA256 sHA = SHA256.Create();
		byte[] bytes = Encoding.ASCII.GetBytes($"img{w}x{h}");
		sHA.TransformBlock(bytes, 0, bytes.Length, null, 0);
		byte[] array = new byte[w * 3];
		int num = w * 4;
		for (int i = 0; i < h; i++)
		{
			int num2 = i * num;
			int num3 = 0;
			int num4 = 0;
			while (num3 < w)
			{
				array[num4++] = bgra[num2];
				array[num4++] = bgra[num2 + 1];
				array[num4++] = bgra[num2 + 2];
				num3++;
				num2 += 4;
			}
			sHA.TransformBlock(array, 0, array.Length, null, 0);
		}
		sHA.TransformFinalBlock(new byte[0], 0, 0);
		return ToHex(sHA.Hash);
	}

	private static string ToHex(byte[] bytes)
	{
		StringBuilder stringBuilder = new StringBuilder(bytes.Length * 2);
		foreach (byte b in bytes)
		{
			stringBuilder.Append(b.ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	private ClipboardItem FindByHash(string hash)
	{
		if (hash == null)
		{
			return null;
		}
		lock (_items)
		{
			return _items.FirstOrDefault((ClipboardItem i) => i.ContentHash == hash);
		}
	}

	public ClipboardItem AddItem(ClipboardItem item)
	{
		if (item == null)
		{
			return null;
		}
		ClipboardItem owned = item.CloneForPersistence();
		ClipboardItem result = owned.CloneForPersistence();
		Submit(delegate
		{
			AddCore(owned);
		});
		return result;
	}

	private void AddCore(ClipboardItem item)
	{
		if (item.ContentHash == null && item.ItemType != "image")
		{
			item.ContentHash = HashText(item.Text);
		}
		ClipboardItem clipboardItem;
		lock (_items)
		{
			clipboardItem = _items.FirstOrDefault((ClipboardItem i) => i.ContentHash != null && i.ContentHash == item.ContentHash);
		}
		if (clipboardItem != null)
		{
			lock (_items)
			{
				clipboardItem.TimestampTicks = DateTime.UtcNow.Ticks;
				clipboardItem.TimestampFormatted = DateTime.Now.ToString("h:mm tt");
				clipboardItem.Html = item.Html;
				clipboardItem.Rtf = item.Rtf;
				if (!string.IsNullOrEmpty(item.SourceAppPath))
				{
					clipboardItem.SourceApp = item.SourceApp;
					clipboardItem.SourceAppExe = item.SourceAppExe;
					clipboardItem.SourceAppPath = item.SourceAppPath;
				}
			}
			if (!string.Equals(clipboardItem.ImagePath, item.ImagePath, StringComparison.OrdinalIgnoreCase))
			{
				QueueDeletion(item);
			}
			return;
		}
		if (item.PendingImage != null)
		{
			item.ImagePath = Path.Combine(_imageDir, item.Id + ".png");
			item.ThumbPath = Path.Combine(_imageDir, item.Id + "_thumb.png");
			_imageWrite(item.PendingImage, item.ImagePath);
			item.FileSizeBytes = new FileInfo(item.ImagePath).Length;
			if (item.FileSizeBytes > 5242880)
			{
				QueueDeletion(item);
				item.PendingImage = null;
				TransactionLogger.Warn("Clipboard capture rejected: image exceeds the storage limit.");
				return;
			}
			_imageWrite(MakeThumbnail(item.PendingImage), item.ThumbPath);
			item.PendingImage = null;
		}
		ValidateStoredImage(item);
		List<ClipboardItem> list2;
		lock (_items)
		{
			_items.Add(item);
			List<ClipboardItem> list = (from i in _items
				where !i.IsPinned
				orderby i.TimestampTicks
				select i).ToList();
			list2 = list.Take(Math.Max(0, list.Count - _maxHistoryCount)).ToList();
			foreach (ClipboardItem item2 in list2)
			{
				_items.Remove(item2);
			}
		}
		foreach (ClipboardItem item3 in list2)
		{
			QueueDeletion(item3);
		}
	}

	public void AddEntry(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			AddItem(new ClipboardItem(text, Classify(text.Trim())));
		}
	}

	public void TogglePin(string id)
	{
		Submit(delegate
		{
			lock (_items)
			{
				ClipboardItem clipboardItem = _items.FirstOrDefault((ClipboardItem i) => i.Id == id);
				if (clipboardItem != null)
				{
					clipboardItem.IsPinned = !clipboardItem.IsPinned;
				}
			}
		});
	}

	public void DeleteEntry(string id)
	{
		Submit(delegate
		{
			ClipboardItem clipboardItem;
			lock (_items)
			{
				clipboardItem = _items.FirstOrDefault((ClipboardItem i) => i.Id == id);
				if (clipboardItem != null)
				{
					_items.Remove(clipboardItem);
				}
			}
			QueueDeletion(clipboardItem);
		});
	}

	public ClipboardItem RemoveForUndo(string id)
	{
		ClipboardItem clipboardItem;
		lock (_items)
		{
			clipboardItem = _items.FirstOrDefault((ClipboardItem i) => i.Id == id)?.CloneForPersistence();
		}
		if (clipboardItem != null)
		{
			Submit(delegate
			{
				lock (_items)
				{
					ClipboardItem clipboardItem2 = _items.FirstOrDefault((ClipboardItem i) => i.Id == id);
					if (clipboardItem2 != null)
					{
						_undoRetained[id] = clipboardItem2.CloneForPersistence();
						_items.Remove(clipboardItem2);
					}
				}
			});
		}
		return clipboardItem;
	}

	public void Reinsert(ClipboardItem item)
	{
		if (item == null)
		{
			return;
		}
		ClipboardItem owned = item.CloneForPersistence();
		Submit(delegate
		{
			if (_undoRetained.TryGetValue(owned.Id, out var value))
			{
				owned = value.CloneForPersistence();
			}
			ValidateStoredImage(owned);
			lock (_items)
			{
				if (!_items.Any((ClipboardItem i) => i.Id == owned.Id || (i.ContentHash != null && i.ContentHash == owned.ContentHash)))
				{
					_items.Add(owned);
				}
			}
			_undoRetained.Remove(owned.Id);
		});
	}

	public void DiscardFiles(ClipboardItem item)
	{
		if (item != null)
		{
			ClipboardItem owned = item.CloneForPersistence();
			Submit(delegate
			{
				_undoRetained.Remove(owned.Id);
				QueueDeletion(owned);
			});
		}
	}

	public void ClearHistory(bool includePinned = false)
	{
		Submit(delegate
		{
			List<ClipboardItem> list;
			lock (_items)
			{
				list = _items.Where((ClipboardItem i) => includePinned || !i.IsPinned).ToList();
				foreach (ClipboardItem item in list)
				{
					_items.Remove(item);
				}
			}
			foreach (ClipboardItem item2 in list)
			{
				QueueDeletion(item2);
			}
		});
	}

	public void ClearAllData()
	{
		ClearHistory(includePinned: true);
	}

	public static void RangeTicks(ClipboardClearRange range, DateTime nowLocal, out long fromUtc, out long toUtc)
	{
		DateTime date = nowLocal.Date;
		toUtc = long.MaxValue;
		switch (range)
		{
		case ClipboardClearRange.Today:
			fromUtc = date.ToUniversalTime().Ticks;
			break;
		case ClipboardClearRange.Yesterday:
			fromUtc = date.AddDays(-1.0).ToUniversalTime().Ticks;
			toUtc = date.ToUniversalTime().Ticks;
			break;
		case ClipboardClearRange.Last7Days:
			fromUtc = date.AddDays(-6.0).ToUniversalTime().Ticks;
			break;
		default:
			fromUtc = long.MinValue;
			break;
		}
	}

	public static bool InClearRange(ClipboardItem item, ClipboardClearRange range, DateTime nowLocal)
	{
		if (item == null || item.IsPinned)
		{
			return false;
		}
		RangeTicks(range, nowLocal, out var fromUtc, out var toUtc);
		if (item.TimestampTicks >= fromUtc)
		{
			return item.TimestampTicks < toUtc;
		}
		return false;
	}

	public int CountForClear(ClipboardClearRange range, DateTime nowLocal)
	{
		lock (_items)
		{
			return _items.Count((ClipboardItem i) => InClearRange(i, range, nowLocal));
		}
	}

	public IList<ClipboardItem> RemoveRangeForUndo(ClipboardClearRange range, DateTime nowLocal)
	{
		List<ClipboardItem> list;
		lock (_items)
		{
			list = (from i in _items
				where InClearRange(i, range, nowLocal)
				select i.CloneForPersistence()).ToList();
		}
		if (list.Count == 0)
		{
			return list;
		}
		HashSet<string> ids = new HashSet<string>(list.Select((ClipboardItem c) => c.Id));
		Submit(delegate
		{
			lock (_items)
			{
				foreach (ClipboardItem item in _items.Where((ClipboardItem i) => ids.Contains(i.Id) && !i.IsPinned).ToList())
				{
					_undoRetained[item.Id] = item.CloneForPersistence();
					_items.Remove(item);
				}
			}
		});
		return list;
	}

	public void ReinsertRange(IList<ClipboardItem> items)
	{
		if (items == null || items.Count == 0)
		{
			return;
		}
		List<ClipboardItem> owned = items.Select((ClipboardItem i) => i.CloneForPersistence()).ToList();
		Submit(delegate
		{
			foreach (ClipboardItem item2 in owned)
			{
				ClipboardItem item = item2;
				if (_undoRetained.TryGetValue(item2.Id, out var value))
				{
					item = value.CloneForPersistence();
				}
				try
				{
					ValidateStoredImage(item);
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Clipboard undo skipped an item whose image is gone: " + ex.Message);
					_undoRetained.Remove(item2.Id);
					continue;
				}
				lock (_items)
				{
					if (!_items.Any((ClipboardItem i) => i.Id == item.Id || (i.ContentHash != null && i.ContentHash == item.ContentHash)))
					{
						_items.Add(item);
					}
				}
				_undoRetained.Remove(item2.Id);
			}
		});
	}

	public void DiscardRange(IList<ClipboardItem> items)
	{
		if (items == null || items.Count == 0)
		{
			return;
		}
		List<ClipboardItem> owned = items.Select((ClipboardItem i) => i.CloneForPersistence()).ToList();
		Submit(delegate
		{
			foreach (ClipboardItem item in owned)
			{
				if (_undoRetained.TryGetValue(item.Id, out var value))
				{
					_undoRetained.Remove(item.Id);
					QueueDeletion(value);
				}
				else
				{
					QueueDeletion(item);
				}
			}
		});
	}

	public void SuppressNextClipboardUpdate(string signature)
	{
		_selfCopyHash = signature;
		_selfCopyUntil = DateTime.UtcNow + SelfCopyWindow;
	}

	public IEnumerable<ClipboardItem> Search(string query)
	{
		IReadOnlyList<ClipboardItem> items = Items;
		if (string.IsNullOrWhiteSpace(query))
		{
			return items;
		}
		string q = query.Trim();
		return items.Where((ClipboardItem i) => Matches(i, q)).ToList();
	}

	public static bool Matches(ClipboardItem i, string q)
	{
		if (i == null)
		{
			return false;
		}
		if (string.IsNullOrEmpty(q))
		{
			return true;
		}
		if (!string.IsNullOrEmpty(i.Text) && i.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		if (!string.IsNullOrEmpty(i.SourceApp) && i.SourceApp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		if (i.FilePaths != null)
		{
			foreach (string filePath in i.FilePaths)
			{
				if (!string.IsNullOrEmpty(filePath) && filePath.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool CopyToClipboard(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		return RestoreItem(new ClipboardItem(text, Classify(text))
		{
			ContentHash = HashText(text)
		}, moveToTop: false);
	}

	public bool RestoreItem(ClipboardItem item)
	{
		return RestoreItem(item, moveToTop: true);
	}

	private IEnumerable<string> OwnedStorageFiles()
	{
		Stack<string> pending = new Stack<string>();
		pending.Push(_clipDir);
		while (pending.Count > 0)
		{
			string dir = pending.Pop();
			if (!TryOwnedPath(Path.Combine(dir, ".ownership-check"), out var canonical))
			{
				continue;
			}
			try
			{
				string[] files = Directory.GetFiles(dir);
				foreach (string file in files)
				{
					if (TryOwnedPath(file, out canonical))
					{
						yield return canonical;
					}
				}
			}
			finally
			{
			}
			string[] directories = Directory.GetDirectories(dir);
			foreach (string item in directories)
			{
				pending.Push(item);
			}
		}
	}

	public bool RestoreItem(ClipboardItem item, Action<bool> completed)
	{
		return RestoreItem(item, moveToTop: true, completed);
	}

	private bool RestoreItem(ClipboardItem item, bool moveToTop, Action<bool> completed = null)
	{
		if (item == null)
		{
			return false;
		}
		ClipboardItem owned = item.CloneForPersistence();
		bool restored = false;
		try
		{
			Submit(delegate
			{
				ValidateStoredImage(owned);
				try
				{
					if (_restoreNative != null)
					{
						_restoreNative(owned);
					}
					else
					{
						RestoreNative(owned);
					}
				}
				catch (ExternalException error)
				{
					if (!IsClipboardBusy(error))
					{
						throw;
					}
					TransactionLogger.Warn("Clipboard busy; restore skipped after native retries.");
					return;
				}
				restored = true;
				if (moveToTop)
				{
					lock (_items)
					{
						ClipboardItem clipboardItem = _items.FirstOrDefault((ClipboardItem i) => i.Id == owned.Id);
						if (clipboardItem != null)
						{
							clipboardItem.TimestampTicks = DateTime.UtcNow.Ticks;
							clipboardItem.TimestampFormatted = DateTime.Now.ToString("h:mm tt");
						}
					}
				}
			}, delegate
			{
				if (!restored)
				{
					if (completed != null)
					{
						completed(obj: false);
					}
				}
				else
				{
					PillActivityArbiter.Instance.PublishClipboardCopied((owned.Kind == ClipKind.Image) ? "Image" : owned.Text);
					if (completed != null)
					{
						completed(obj: true);
					}
				}
			}, delegate
			{
				if (completed != null)
				{
					completed(obj: false);
				}
			}, () => restored && moveToTop);
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void RestoreNative(ClipboardItem item)
	{
		if (!_nativeEnabled)
		{
			throw new InvalidOperationException("Native clipboard disabled for synthetic owner.");
		}
		SuppressNextClipboardUpdate(item.ContentHash);
		TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
		Dispatcher worker = Worker;
		if (worker == null)
		{
			throw new IOException("Clipboard worker unavailable.");
		}
		worker.BeginInvoke((Action)delegate
		{
			try
			{
				if (item.Kind == ClipKind.Image)
				{
					BitmapSource image = Decode(item.ImagePath, 0);
					if (image == null)
					{
						throw new IOException("Cannot decode clipboard restore image.");
					}
					Retry(delegate
					{
						System.Windows.Clipboard.SetImage(image);
						return true;
					});
				}
				else if (item.Kind == ClipKind.Files && item.FilePaths != null && item.FilePaths.Count > 0)
				{
					StringCollection paths = new StringCollection();
					paths.AddRange(item.FilePaths.ToArray());
					Retry(delegate
					{
						System.Windows.Clipboard.SetFileDropList(paths);
						return true;
					});
				}
				else
				{
					Retry(delegate
					{
						System.Windows.Clipboard.SetText(item.Text);
						return true;
					});
				}
				done.SetResult(result: true);
			}
			catch (Exception exception)
			{
				done.SetException(exception);
			}
		});
		done.Task.GetAwaiter().GetResult();
	}

	public ImageSource GetThumbnail(ClipboardItem item, Action<ImageSource> onLoaded)
	{
		if (item == null || string.IsNullOrEmpty(item.ImagePath) || _paused || !Operations.AdmissionOpen)
		{
			return null;
		}
		ClipboardItem owned = item.CloneForPersistence();
		string key = ((!string.IsNullOrEmpty(owned.ThumbPath)) ? owned.ThumbPath : owned.ImagePath);
		ImageSource imageSource = CachedThumb(key);
		if (imageSource != null)
		{
			return imageSource;
		}
		lock (_thumbsPending)
		{
			if (!_thumbsPending.Add(key))
			{
				return null;
			}
		}
		try
		{
			Submit(delegate
			{
				try
				{
					ClipboardItem clipboardItem;
					lock (_items)
					{
						clipboardItem = _items.FirstOrDefault((ClipboardItem i) => i.Id == owned.Id);
					}
					if (clipboardItem != null)
					{
						ValidateStoredImage(clipboardItem);
						ImageSource image;
						if (!string.IsNullOrEmpty(clipboardItem.ThumbPath))
						{
							image = Decode(clipboardItem.ThumbPath, 0);
						}
						else
						{
							BitmapSource bitmapSource = Decode(clipboardItem.ImagePath, 80);
							if (bitmapSource == null)
							{
								throw new IOException("Cannot decode clipboard image.");
							}
							string text = Path.Combine(_imageDir, clipboardItem.Id + "_thumb.png");
							_imageWrite(bitmapSource, text);
							lock (_items)
							{
								clipboardItem.ThumbPath = text;
							}
							image = bitmapSource;
						}
						if (image == null)
						{
							throw new IOException("Cannot decode clipboard thumbnail.");
						}
						StoreThumb(key, image);
						DispatchWhileActive(delegate
						{
							if (onLoaded != null)
							{
								onLoaded(image);
							}
						});
					}
				}
				finally
				{
					lock (_thumbsPending)
					{
						_thumbsPending.Remove(key);
					}
				}
			});
		}
		catch
		{
			lock (_thumbsPending)
			{
				_thumbsPending.Remove(key);
			}
			throw;
		}
		return null;
	}

	public ImageSource GetThumbnail(string imagePath, Action<ImageSource> onLoaded = null)
	{
		if (string.IsNullOrEmpty(imagePath))
		{
			return null;
		}
		ClipboardItem clipboardItem;
		lock (_items)
		{
			clipboardItem = _items.FirstOrDefault((ClipboardItem i) => string.Equals(i.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase));
		}
		return GetThumbnail(clipboardItem ?? new ClipboardItem("Image", "image")
		{
			ImagePath = imagePath
		}, onLoaded);
	}

	public static BitmapSource Decode(string path, int decodeWidth)
	{
		if (!TryOwnedPath(path, out var canonical))
		{
			return null;
		}
		path = canonical;
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return null;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(path);
			if (decodeWidth > 0)
			{
				bitmapImage.DecodePixelWidth = decodeWidth;
			}
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static ImageSource CachedThumb(string key)
	{
		lock (_thumbLock)
		{
			if (!_thumbCache.TryGetValue(key, out var value))
			{
				return null;
			}
			_thumbOrder.Remove(key);
			_thumbOrder.AddLast(key);
			return value;
		}
	}

	public static void ReleaseThumbnailCache()
	{
		lock (_thumbLock)
		{
			_thumbCache.Clear();
			_thumbOrder.Clear();
		}
	}

	private static void StoreThumb(string key, ImageSource img)
	{
		lock (_thumbLock)
		{
			if (!_thumbCache.ContainsKey(key))
			{
				_thumbCache[key] = img;
				_thumbOrder.AddLast(key);
				while (_thumbOrder.Count > 120)
				{
					_thumbCache.Remove(_thumbOrder.First.Value);
					_thumbOrder.RemoveFirst();
				}
			}
		}
	}

	private static void ForgetThumb(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		lock (_thumbLock)
		{
			if (_thumbCache.Remove(key))
			{
				_thumbOrder.Remove(key);
			}
		}
	}

	private static void SafeDelete(string path)
	{
		if (string.IsNullOrEmpty(path) || !TryOwnedPath(path, out var canonical))
		{
			return;
		}
		path = canonical;
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static bool TryOwnedPath(string path, out string canonical)
	{
		canonical = null;
		try
		{
			string text = Path.GetFullPath(Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "clipboard")).TrimEnd(Path.DirectorySeparatorChar);
			string fullPath = Path.GetFullPath(path);
			if (!Path.IsPathRooted(path) || !fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException("Path is outside clipboard storage");
			}
			if (fullPath.Substring(Path.GetPathRoot(fullPath).Length).Contains(":"))
			{
				throw new IOException("Alternate stream");
			}
			HashSet<string> loadVerifiedDirs = _loadVerifiedDirs;
			string text2 = fullPath;
			while (text2 != null && (loadVerifiedDirs == null || !(text2 != fullPath) || !loadVerifiedDirs.Contains(text2)))
			{
				if ((File.Exists(text2) || Directory.Exists(text2)) && (File.GetAttributes(text2) & FileAttributes.ReparsePoint) != 0)
				{
					throw new IOException("Reparse point");
				}
				if (loadVerifiedDirs != null && text2 != fullPath)
				{
					loadVerifiedDirs.Add(text2);
				}
				text2 = Path.GetDirectoryName(text2);
			}
			canonical = fullPath;
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Skipped unsafe clipboard path: " + ex.GetType().Name);
			return false;
		}
	}

	public static bool ValidateItemPaths(ClipboardItem item)
	{
		if (item == null)
		{
			return false;
		}
		if (string.IsNullOrEmpty(item.ImagePath) && string.IsNullOrEmpty(item.ThumbPath))
		{
			return true;
		}
		if (!Guid.TryParseExact(item.Id, "N", out var _))
		{
			TransactionLogger.Warn("Skipped clipboard image with invalid item ID.");
			return false;
		}
		string canonical;
		if (!string.IsNullOrEmpty(item.ImagePath))
		{
			if (!TryOwnedPath(item.ImagePath, out canonical))
			{
				return false;
			}
			item.ImagePath = canonical;
		}
		if (!string.IsNullOrEmpty(item.ThumbPath))
		{
			if (!TryOwnedPath(item.ThumbPath, out canonical))
			{
				return false;
			}
			item.ThumbPath = canonical;
		}
		return true;
	}

	private void NotifyChanged()
	{
		Interlocked.Increment(ref _version);
		EventHandler h = this.ItemsChanged;
		if (h != null)
		{
			DispatchWhileActive(delegate
			{
				h(this, EventArgs.Empty);
			});
		}
	}

	public void SaveHistory()
	{
		SaveHistory(synchronous: false);
	}

	private void SaveHistory(bool synchronous)
	{
		Submit(delegate
		{
		});
		if (synchronous)
		{
			Flush();
		}
	}

	private string SerializeSnapshot()
	{
		List<ClipboardItem> list;
		lock (_items)
		{
			list = _items.Select((ClipboardItem i) => i.CloneForPersistence()).ToList();
		}
		foreach (ClipboardItem item in list)
		{
			ValidateStoredImage(item, deep: false);
		}
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		return ProtectHistory(javaScriptSerializer.Serialize(list));
	}

	private static string ProtectHistory(string json)
	{
		return "VAYLI-CLIP-DPAPI1:" + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));
	}

	private static List<ClipboardItem> ParseHistory(string text)
	{
		if (text.StartsWith("VAYLI-CLIP-DPAPI1:", StringComparison.Ordinal))
		{
			text = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(text.Substring("VAYLI-CLIP-DPAPI1:".Length)), null, DataProtectionScope.CurrentUser));
		}
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		return javaScriptSerializer.Deserialize<List<ClipboardItem>>(text);
	}

	public void Flush()
	{
		Task task = VerifyAsync(CancellationToken.None);
		if (!task.Wait(2000))
		{
			task.ContinueWith(delegate(Task completed)
			{
				_ = completed.Exception;
			}, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
			throw new TimeoutException("Clipboard durability is still pending.");
		}
	}

	private void LoadHistory()
	{
		try
		{
			bool recovery = false;
			List<ClipboardItem> list = PersistenceHelper.ReadWithBackup(_storagePath, ParseHistory, delegate
			{
				recovery = true;
			});
			if (list == null)
			{
				return;
			}
			if (File.Exists(_storagePath + ".bak") && ParseHistory(PersistenceHelper.ReadShared(_storagePath + ".bak")) == null)
			{
				throw new InvalidDataException("Null backup history.");
			}
			_loadVerifiedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				foreach (ClipboardItem item in list)
				{
					if (item == null || string.IsNullOrEmpty(item.Id))
					{
						throw new InvalidDataException("Invalid history record.");
					}
					ValidateStoredImage(item, deep: false);
					if (item.FilePaths == null)
					{
						item.FilePaths = new List<string>();
					}
				}
			}
			finally
			{
				_loadVerifiedDirs = null;
			}
			lock (_items)
			{
				_items.AddRange(list.Select((ClipboardItem i) => i.CloneForPersistence()));
			}
			bool plainPrimary = !recovery && File.Exists(_storagePath) && !PersistenceHelper.ReadShared(_storagePath).StartsWith("VAYLI-CLIP-DPAPI1:", StringComparison.Ordinal);
			bool flag = recovery || plainPrimary;
			bool plainBackup = File.Exists(_storagePath + ".bak") && !PersistenceHelper.ReadShared(_storagePath + ".bak").StartsWith("VAYLI-CLIP-DPAPI1:", StringComparison.Ordinal);
			if (flag || plainBackup)
			{
				Submit(delegate
				{
					if (recovery)
					{
						string[] array = new string[2]
						{
							_storagePath,
							_storagePath + ".bak"
						};
						foreach (string text in array)
						{
							if (File.Exists(text))
							{
								File.Copy(text, text + ".recovery." + Guid.NewGuid().ToString("N"));
							}
						}
					}
					if (recovery)
					{
						string s = ProtectHistory(new JavaScriptSerializer
						{
							MaxJsonLength = int.MaxValue
						}.Serialize(list));
						PersistenceHelper.WriteOwnedBytes(_storagePath, Encoding.UTF8.GetBytes(s));
					}
					else if (plainPrimary)
					{
						string text2 = PersistenceHelper.ReadShared(_storagePath);
						if (!text2.StartsWith("VAYLI-CLIP-DPAPI1:", StringComparison.Ordinal))
						{
							PersistenceHelper.WriteOwnedBytes(_storagePath, Encoding.UTF8.GetBytes(ProtectHistory(text2)));
						}
					}
					if (plainBackup)
					{
						string path = _storagePath + ".bak";
						string text3 = PersistenceHelper.ReadShared(path);
						if (!text3.StartsWith("VAYLI-CLIP-DPAPI1:", StringComparison.Ordinal))
						{
							string s2 = ProtectHistory(text3);
							PersistenceHelper.WriteOwnedBytes(path, Encoding.UTF8.GetBytes(s2));
						}
					}
				});
			}
			Interlocked.Increment(ref _version);
		}
		catch (Exception loadFailure)
		{
			TransactionLogger.Warn("Clipboard history preserved; recovery required: " + (_loadFailure = loadFailure).Message);
		}
	}
}
