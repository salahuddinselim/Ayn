using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Lifecycle;
using WindowsBuddy.Tools.Notes;

namespace WindowsBuddy.Services;

public sealed class AppPreferences : IShutdownParticipant
{
	private sealed class Pending
	{
		public OperationReceipt Receipt;

		public AppPreferencesModel Model;

		public bool Failed;
	}

	private static AppPreferences _instance;

	private static readonly object _lock = new object();

	private readonly object _state = new object();

	private readonly string _path;

	private AppPreferencesModel _m;

	private readonly List<Pending> _pending = new List<Pending>();

	private Task _drain = Task.FromResult(0);

	private readonly TaskCompletionSource<bool> _initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

	private bool _writing;

	private bool _repair;

	private volatile bool _sealed;

	private Exception _loadFailure;

	private Exception _lastError;

	private Func<string, string, bool> _persist = PersistenceHelper.WriteOwnedText;

	private Func<AppPreferencesModel, string> _serialize = (AppPreferencesModel m) => new JavaScriptSerializer().Serialize(m);

	private readonly ManualResetEventSlim _saveNow = new ManualResetEventSlim(initialState: false);

	public static AppPreferences Instance
	{
		get
		{
			lock (_lock)
			{
				return _instance ?? (_instance = new AppPreferences(standalone: false));
			}
		}
	}

	public OperationGate Operations { get; private set; }

	public string Id => "preferences";

	public PersistenceClass Classification => PersistenceClass.CriticalUserData;

	public string FilePath => _path;

	public bool IsSealed => _sealed;

	public Exception LastSaveError
	{
		get
		{
			lock (_state)
			{
				return _lastError ?? _loadFailure;
			}
		}
	}

	public int HotkeyModifiers => Read((AppPreferencesModel m) => m.HotkeyModifiers);

	public string DropSaveFolder
	{
		get
		{
			return Read((AppPreferencesModel m) => m.DropSaveFolder);
		}
		set
		{
			Mutate("DropSaveFolder", delegate(AppPreferencesModel m)
			{
				if (m.DropSaveFolder == value)
				{
					return false;
				}
				m.DropSaveFolder = value;
				return true;
			});
		}
	}

	public bool DropFirewallExplained
	{
		get
		{
			return Read((AppPreferencesModel m) => m.DropFirewallExplained);
		}
		set
		{
			Mutate("DropFirewallExplained", delegate(AppPreferencesModel m)
			{
				if (m.DropFirewallExplained == value)
				{
					return false;
				}
				m.DropFirewallExplained = value;
				return true;
			});
		}
	}

	public bool DropRequirePin
	{
		get
		{
			return Read((AppPreferencesModel m) => m.DropRequirePin);
		}
		set
		{
			Mutate("DropRequirePin", delegate(AppPreferencesModel m)
			{
				if (m.DropRequirePin == value)
				{
					return false;
				}
				m.DropRequirePin = value;
				return true;
			});
		}
	}

	public string DropFirewallExplainedPath
	{
		get
		{
			return Read((AppPreferencesModel m) => m.DropFirewallExplainedPath);
		}
		set
		{
			Mutate("DropFirewallExplainedPath", delegate(AppPreferencesModel m)
			{
				if (m.DropFirewallExplainedPath == value)
				{
					return false;
				}
				m.DropFirewallExplainedPath = value;
				return true;
			});
		}
	}

	public string HomeWidgetOrder
	{
		get
		{
			return Read((AppPreferencesModel m) => m.HomeWidgetOrder);
		}
		set
		{
			Mutate("HomeWidgetOrder", delegate(AppPreferencesModel m)
			{
				if (m.HomeWidgetOrder == value)
				{
					return false;
				}
				m.HomeWidgetOrder = value;
				return true;
			});
		}
	}

	public string HomeWidgetSlots
	{
		get
		{
			return Read((AppPreferencesModel m) => m.HomeWidgetSlots);
		}
		set
		{
			Mutate("HomeWidgetSlots", delegate(AppPreferencesModel m)
			{
				if (m.HomeWidgetSlots == value)
				{
					return false;
				}
				m.HomeWidgetSlots = value;
				return true;
			});
		}
	}

	public string HiddenHomeWidgets
	{
		get
		{
			return Read((AppPreferencesModel m) => m.HiddenHomeWidgets ?? "");
		}
		set
		{
			Mutate("HiddenHomeWidgets", delegate(AppPreferencesModel m)
			{
				if (m.HiddenHomeWidgets == value)
				{
					return false;
				}
				m.HiddenHomeWidgets = value;
				return true;
			});
		}
	}

	public string UpdateNoticeDay
	{
		get
		{
			return Read((AppPreferencesModel m) => m.UpdateNoticeDay);
		}
		set
		{
			Mutate("UpdateNoticeDay", delegate(AppPreferencesModel m)
			{
				if (m.UpdateNoticeDay == value)
				{
					return false;
				}
				m.UpdateNoticeDay = value;
				return true;
			});
		}
	}

	public int HotkeyKey => Read((AppPreferencesModel m) => m.HotkeyKey);

	public string Language
	{
		get
		{
			return Read((AppPreferencesModel m) => m.Language ?? "en");
		}
		set
		{
			Mutate("Language", delegate(AppPreferencesModel m)
			{
				if (m.Language == value)
				{
					return false;
				}
				m.Language = value;
				return true;
			});
		}
	}

	public string Theme
	{
		get
		{
			return Read((AppPreferencesModel m) => (!(m.Theme == "Light")) ? "Dark" : "Light");
		}
		set
		{
			string v = ((value == "Light") ? "Light" : "Dark");
			Mutate("Theme", delegate(AppPreferencesModel m)
			{
				if (m.Theme == v)
				{
					return false;
				}
				m.Theme = v;
				return true;
			});
		}
	}

	public string ColorStyle
	{
		get
		{
			return Read((AppPreferencesModel m) => (!(m.ColorStyle == "Color") && !(m.ColorStyle == "Tinted")) ? "Mono" : m.ColorStyle);
		}
		set
		{
			string v = ((value == "Color" || value == "Tinted") ? value : "Mono");
			Mutate("ColorStyle", delegate(AppPreferencesModel m)
			{
				if (m.ColorStyle == v)
				{
					return false;
				}
				m.ColorStyle = v;
				return true;
			});
		}
	}

	public string Accent
	{
		get
		{
			return Read((AppPreferencesModel m) => (!string.IsNullOrEmpty(m.Accent)) ? m.Accent : "#F59E0B");
		}
		set
		{
			Mutate("Accent", delegate(AppPreferencesModel m)
			{
				if (string.IsNullOrEmpty(value) || m.Accent == value)
				{
					return false;
				}
				m.Accent = value;
				return true;
			});
		}
	}

	public int ClipboardHistoryLimit
	{
		get
		{
			return Read((AppPreferencesModel m) => m.ClipboardHistoryLimit);
		}
		set
		{
			int v = Math.Max(1, value);
			Mutate("ClipboardHistoryLimit", delegate(AppPreferencesModel m)
			{
				if (m.ClipboardHistoryLimit == v)
				{
					return false;
				}
				m.ClipboardHistoryLimit = v;
				return true;
			});
		}
	}

	public bool ShowWebsiteIconsInLinks
	{
		get
		{
			return Read((AppPreferencesModel m) => m.ShowWebsiteIconsInLinks);
		}
		set
		{
			Mutate("ShowWebsiteIconsInLinks", delegate(AppPreferencesModel m)
			{
				if (m.ShowWebsiteIconsInLinks == value)
				{
					return false;
				}
				m.ShowWebsiteIconsInLinks = value;
				return true;
			});
		}
	}

	public bool ClipboardRecording
	{
		get
		{
			return Read((AppPreferencesModel m) => m.ClipboardRecording);
		}
		set
		{
			Mutate("ClipboardRecording", delegate(AppPreferencesModel m)
			{
				if (m.ClipboardRecording == value)
				{
					return false;
				}
				m.ClipboardRecording = value;
				return true;
			});
		}
	}

	public bool BackupIncludeClipboardImages
	{
		get
		{
			return Read((AppPreferencesModel m) => m.BackupIncludeClipboardImages);
		}
		set
		{
			Mutate("BackupIncludeClipboardImages", delegate(AppPreferencesModel m)
			{
				if (m.BackupIncludeClipboardImages == value)
				{
					return false;
				}
				m.BackupIncludeClipboardImages = value;
				return true;
			});
		}
	}

	public bool BackupIncludeShelfFiles
	{
		get
		{
			return Read((AppPreferencesModel m) => m.BackupIncludeShelfFiles);
		}
		set
		{
			Mutate("BackupIncludeShelfFiles", delegate(AppPreferencesModel m)
			{
				if (m.BackupIncludeShelfFiles == value)
				{
					return false;
				}
				m.BackupIncludeShelfFiles = value;
				return true;
			});
		}
	}

	public bool AutomaticWeeklyBackup
	{
		get
		{
			return Read((AppPreferencesModel m) => m.AutomaticWeeklyBackup);
		}
		set
		{
			Mutate("AutomaticWeeklyBackup", delegate(AppPreferencesModel m)
			{
				if (m.AutomaticWeeklyBackup == value)
				{
					return false;
				}
				m.AutomaticWeeklyBackup = value;
				return true;
			});
		}
	}

	public int ClipboardRetentionDays
	{
		get
		{
			return Read((AppPreferencesModel m) => m.ClipboardRetentionDays);
		}
		set
		{
			Mutate("ClipboardRetentionDays", delegate(AppPreferencesModel m)
			{
				if (m.ClipboardRetentionDays == value)
				{
					return false;
				}
				m.ClipboardRetentionDays = value;
				return true;
			});
		}
	}

	public string EulaAcceptedVersion => Read((AppPreferencesModel m) => m.EulaAcceptedVersion);

	public string EulaAcceptedUtc => Read((AppPreferencesModel m) => m.EulaAcceptedUtc);

	public string EulaAcceptedAppVersion => Read((AppPreferencesModel m) => m.EulaAcceptedAppVersion);

	public AppPreferencesModel WindowState => Read((AppPreferencesModel m) => m.Copy());

	internal AppPreferencesModel Raw => WindowState;

	public event EventHandler<string> Changed;

	public AppPreferences(bool standalone)
	{
		if (standalone && !AppPaths.IsSandboxed)
		{
			throw new InvalidOperationException("Synthetic profile required.");
		}
		_path = Path.Combine(AppPaths.DataDir, "settings", "app_preferences.json");
		Operations = new OperationGate();
		try
		{
			_m = PersistenceHelper.ReadWithBackup(_path, Parse, delegate
			{
				_repair = true;
			});
			if (Exists(_path + ".bak"))
			{
				try
				{
					Parse(PersistenceHelper.ReadShared(_path + ".bak"));
				}
				catch
				{
					if (_m == null)
					{
						throw;
					}
					_repair = true;
				}
			}
		}
		catch (Exception loadFailure)
		{
			_loadFailure = loadFailure;
		}
		if (_m == null)
		{
			_m = new AppPreferencesModel();
		}
		if (_m.ClipboardHistoryLimit <= 0)
		{
			_m.ClipboardHistoryLimit = 500;
		}
		if (_m.ClipboardRetentionDays < 0)
		{
			_m.ClipboardRetentionDays = 30;
		}
		if (_repair && _loadFailure == null)
		{
			lock (_state)
			{
				EnqueueLocked(_m);
			}
		}
		if (!standalone)
		{
			NotesOwnership.Registry.Register(this);
		}
		lock (_state)
		{
			StartLocked();
		}
		_initialized.TrySetResult(result: true);
		if (standalone)
		{
			return;
		}
		PersistenceHelper.RegisterExitFlush("preferences", delegate
		{
			if (!Save())
			{
				throw new IOException("App preferences are pending or failed.");
			}
		});
	}

	private static bool Exists(string path)
	{
		try
		{
			if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
			{
				throw new InvalidDataException("Preferences path is a directory.");
			}
			return true;
		}
		catch (FileNotFoundException)
		{
			return false;
		}
		catch (DirectoryNotFoundException)
		{
			return false;
		}
	}

	private static AppPreferencesModel Parse(string text)
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		if (!(javaScriptSerializer.DeserializeObject(text) is Dictionary<string, object>))
		{
			throw new InvalidDataException("Expected preferences object.");
		}
		AppPreferencesModel appPreferencesModel = javaScriptSerializer.Deserialize<AppPreferencesModel>(text);
		if (appPreferencesModel == null)
		{
			throw new InvalidDataException("Invalid preferences.");
		}
		return appPreferencesModel;
	}

	private void CheckAdmission()
	{
		if (_sealed || !Operations.AdmissionOpen)
		{
			throw new InvalidOperationException("Preferences admission closed.");
		}
		if (_loadFailure != null)
		{
			throw new InvalidDataException("Preferences recovery required; original files retained.", _loadFailure);
		}
	}

	private void EnqueueLocked(AppPreferencesModel model)
	{
		OperationReceipt operationReceipt = Operations.TryAccept();
		if (operationReceipt == null)
		{
			throw new InvalidOperationException("Preferences admission closed.");
		}
		_pending.Add(new Pending
		{
			Receipt = operationReceipt,
			Model = model.Copy()
		});
	}

	private void Mutate(string property, Func<AppPreferencesModel, bool> edit)
	{
		lock (_state)
		{
			CheckAdmission();
			AppPreferencesModel appPreferencesModel = _m.Copy();
			if (!edit(appPreferencesModel))
			{
				return;
			}
			EnqueueLocked(appPreferencesModel);
			_m = appPreferencesModel;
			StartLocked();
		}
		if (property == null)
		{
			return;
		}
		EventHandler<string> changed = this.Changed;
		if (changed == null)
		{
			return;
		}
		Delegate[] invocationList = changed.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<string> eventHandler = (EventHandler<string>)invocationList[i];
			try
			{
				eventHandler(this, property);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Preferences observer failed: " + ex.Message);
			}
		}
	}

	private T Read<T>(Func<AppPreferencesModel, T> read)
	{
		lock (_state)
		{
			return read(_m);
		}
	}

	private void StartLocked()
	{
		if (!_writing && _pending.Count != 0)
		{
			_writing = true;
			_drain = Task.Run((Action)WriteLoop);
		}
	}

	private void WriteLoop()
	{
		_saveNow.Wait(500);
		_saveNow.Reset();
		while (true)
		{
			Pending[] array;
			lock (_state)
			{
				if (_pending.Count == 0)
				{
					_writing = false;
					break;
				}
				array = _pending.ToArray();
				Pending[] array2 = array;
				foreach (Pending pending in array2)
				{
					if (pending.Failed)
					{
						pending.Receipt = Operations.Retry(pending.Receipt.Revision);
						pending.Failed = false;
					}
				}
			}
			try
			{
				string text = _serialize(array[array.Length - 1].Model);
				Pending[] array3 = array;
				foreach (Pending pending2 in array3)
				{
					pending2.Receipt.AssertWriteAllowed();
				}
				if (_repair)
				{
					string[] array4 = new string[2]
					{
						_path,
						_path + ".bak"
					};
					foreach (string text2 in array4)
					{
						if (Exists(text2))
						{
							File.Copy(text2, text2 + ".recovery." + Guid.NewGuid().ToString("N"));
						}
					}
					PersistenceHelper.WriteOwnedBytes(_path, Encoding.UTF8.GetBytes(text));
					_repair = false;
				}
				if (!_persist(_path, text))
				{
					throw new IOException("App preferences write failed.");
				}
				Pending[] array5 = array;
				foreach (Pending pending3 in array5)
				{
					pending3.Receipt.CompleteDurably();
				}
				lock (_state)
				{
					_pending.RemoveRange(0, array.Length);
					_lastError = null;
				}
			}
			catch (Exception ex)
			{
				lock (_state)
				{
					Pending[] array6 = array;
					foreach (Pending pending4 in array6)
					{
						if (!pending4.Failed)
						{
							pending4.Receipt.Fail();
							pending4.Failed = true;
						}
					}
					_lastError = ex;
					_writing = false;
					if (_pending.Count > array.Length)
					{
						StartLocked();
					}
				}
				PersistenceHelper.ReportSaveFailure(_path, ex);
				break;
			}
		}
	}

	public async Task VerifyAsync(CancellationToken token)
	{
		await _initialized.Task.ConfigureAwait(continueOnCapturedContext: false);
		_saveNow.Set();
		Task drain;
		lock (_state)
		{
			StartLocked();
			drain = _drain;
		}
		while (true)
		{
			await drain.ConfigureAwait(continueOnCapturedContext: false);
			lock (_state)
			{
				if (!_writing || _drain == drain)
				{
					break;
				}
				drain = _drain;
				continue;
			}
		}
		token.ThrowIfCancellationRequested();
		_saveNow.Set();
		lock (_state)
		{
			AssertDurable();
		}
	}

	private void AssertDurable()
	{
		OperationSnapshot operationSnapshot = Operations.Snapshot();
		if (_loadFailure != null || operationSnapshot.Pending != 0 || operationSnapshot.Failed != 0 || operationSnapshot.Violations != 0 || operationSnapshot.Accepted != operationSnapshot.Durable)
		{
			throw new IOException("Preferences durability incomplete.", _lastError ?? _loadFailure);
		}
	}

	public bool Save()
	{
		try
		{
			return VerifyAsync(CancellationToken.None).Wait(250);
		}
		catch
		{
			return false;
		}
	}

	public async Task<string> ExportSnapshotAsync()
	{
		AppPreferencesModel snapshot;
		lock (_state)
		{
			if (_loadFailure != null)
			{
				throw new IOException("Preferences recovery required.", _loadFailure);
			}
			snapshot = _m.Copy();
		}
		await VerifyAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		return await Task.Run(() => _serialize(snapshot)).ConfigureAwait(continueOnCapturedContext: false);
	}

	public Task PrepareAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		_saveNow.Set();
		return Task.FromResult(0);
	}

	public Task QuiesceAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		_saveNow.Set();
		return Task.FromResult(0);
	}

	public Task ResumeAsync()
	{
		return Task.FromResult(0);
	}

	public Task DisposeAsync()
	{
		lock (_state)
		{
			AssertDurable();
			_sealed = true;
		}
		return Task.FromResult(0);
	}

	public Action PauseForShutdown()
	{
		lock (_state)
		{
			Operations.CloseAdmission();
		}
		return delegate
		{
			lock (_state)
			{
				Operations.Reopen();
			}
		};
	}

	public void SealForDisposal()
	{
		lock (_state)
		{
			AssertDurable();
			Operations.Seal(requireDurability: true);
			Operations.CommitSeal();
			_sealed = true;
		}
	}

	public void SetHotkey(int modifiers, int key)
	{
		Mutate("Hotkey", delegate(AppPreferencesModel m)
		{
			if (m.HotkeyModifiers == modifiers && m.HotkeyKey == key)
			{
				return false;
			}
			m.HotkeyModifiers = modifiers;
			m.HotkeyKey = key;
			return true;
		});
	}

	public void RecordEulaAcceptance(string version, string utc, string appVersion)
	{
		if (string.IsNullOrEmpty(version))
		{
			throw new ArgumentException("EULA version required.");
		}
		Mutate("EulaAccepted", delegate(AppPreferencesModel m)
		{
			if (m.EulaAcceptedVersion == version && m.EulaAcceptedUtc == utc && m.EulaAcceptedAppVersion == appVersion)
			{
				return false;
			}
			m.EulaAcceptedVersion = version;
			m.EulaAcceptedUtc = utc;
			m.EulaAcceptedAppVersion = appVersion;
			return true;
		});
	}

	public void SaveWindowState(double left, double top, double width, double height, bool maximized, string lastPage)
	{
		if (new double[4] { left, top, width, height }.Any((double v) => double.IsNaN(v) || double.IsInfinity(v)))
		{
			throw new ArgumentException("Window bounds must be finite.");
		}
		Mutate(null, delegate(AppPreferencesModel m)
		{
			if (m.HasWindowBounds && m.WindowLeft == left && m.WindowTop == top && m.WindowWidth == width && m.WindowHeight == height && m.WindowMaximized == maximized && (string.IsNullOrEmpty(lastPage) || m.LastPage == lastPage))
			{
				return false;
			}
			m.WindowLeft = left;
			m.WindowTop = top;
			m.WindowWidth = width;
			m.WindowHeight = height;
			m.WindowMaximized = maximized;
			m.HasWindowBounds = true;
			if (!string.IsNullOrEmpty(lastPage))
			{
				m.LastPage = lastPage;
			}
			return true;
		});
	}
}
