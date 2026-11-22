using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Lifecycle;
using WindowsBuddy.Tools.Notes;

namespace WindowsBuddy.Tools.FileShelf;

public class FileShelfService : IShutdownParticipant
{
	private sealed class Work
	{
		public OperationReceipt Receipt;

		public Action<Work> Apply;

		public Action<Stream> Copy;

		public TaskCompletionSource<string> Completion;

		public string Stage;

		public string Path;

		public string Result;

		public string Prefix;

		public bool Replaceable;

		public Action CheckSource;

		public bool Applied;

		public bool Failed;

		public bool CleanupOnly;

		public bool Running;

		public bool WaitingSource;

		public bool RecoveryPersisted;

		public bool Ready;

		public bool Image;

		public string ShelfId;

		public long Epoch;

		public ShelfCopyRecovery Recovery;
	}

	private sealed class Superseded
	{
		public string Shelf;

		public string Path;

		public long Epoch;
	}

	internal sealed class ViewItem
	{
		internal string ShelfId;

		internal ParkedFile File;
	}

	internal sealed class ViewShelf
	{
		internal string Id;

		internal string Name;

		internal int Count;
	}

	internal sealed class ViewChange : EventArgs
	{
		internal string ActiveId;

		internal ShelfSort Sort;

		internal ViewShelf[] Shelves;

		internal ViewItem[] Items;

		internal bool Reset;
	}

	private sealed class ItemSlot
	{
		internal string ShelfId;

		internal ParkedFile File;

		internal ItemSlot Original;

		internal bool NeedsProbe;
	}

	public sealed class CacheLease
	{
		internal OperationReceipt Receipt;

		internal volatile bool Active = true;

		internal readonly TaskCompletionSource<bool> Settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		public bool IsActive => Active;

		public Task<bool> Completion => Settled.Task;
	}

	public const int MaxShelves = 12;

	public const int MaxNameLength = 40;

	public const string DefaultShelfName = "Desk";

	private const int StorageVersion = 2;

	private const string FailedCopyPrefix = "failed-";

	private const int RetryEvidenceSuffix = 53;

	private static FileShelfService _instance;

	private static readonly object InstanceLock = new object();

	private readonly List<ShelfCopyRecovery> _recoveries = new List<ShelfCopyRecovery>();

	private readonly List<Work> _queue = new List<Work>();

	private readonly HashSet<string> _retired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _eligible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _deleting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, long> _epochs = new Dictionary<string, long>();

	private readonly Dictionary<string, long> _deletedEpochs = new Dictionary<string, long>();

	private readonly Dictionary<List<RemovedItem>, Tuple<string, long>> _clearUndo = new Dictionary<List<RemovedItem>, Tuple<string, long>>();

	private readonly List<Superseded> _superseded = new List<Superseded>();

	private readonly HashSet<string> _reclaimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private Task _drain = Task.FromResult(0);

	private volatile bool _writing;

	private bool _recoveryRestart;

	private bool _initializing = true;

	private readonly TaskCompletionSource<bool> _initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

	private Func<string, string, bool> _persist;

	private Action<string> _delete = File.Delete;

	private Func<ParkedFile, bool> _probe;

	private bool _statusEnabled;

	private Action<BitmapSource, Stream> _encode = delegate(BitmapSource image, Stream output)
	{
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(image));
		pngBitmapEncoder.Save(output);
	};

	private Exception _loadFailure;

	private volatile bool _paused;

	private volatile bool _disposed;

	private readonly object _cacheLock = new object();

	private readonly OperationGate _cacheGate = new OperationGate();

	private readonly HashSet<CacheLease> _cache = new HashSet<CacheLease>();

	public static Action PrepareEditor;

	private readonly ManualResetEventSlim _saveNow = new ManualResetEventSlim(initialState: false);

	private readonly List<ShelfTab> _shelves = new List<ShelfTab>();

	private readonly Dictionary<string, ItemSlot> _itemIndex = new Dictionary<string, ItemSlot>();

	private readonly HashSet<string> _unprobedIds = new HashSet<string>();

	private readonly Dictionary<string, ViewItem> _viewPending = new Dictionary<string, ViewItem>();

	private int _notificationPending;

	private int _activeShelfIndex;

	private ShelfSort _sort;

	private readonly string _storageDir;

	private readonly string _shelvesPath;

	private readonly string _legacyPath;

	private readonly string _ownedDir;

	private readonly Dispatcher _dispatcher;

	private bool _probeRunning;

	private bool _probeAgain;

	private bool _probeAgainFull;

	public static FileShelfService Instance
	{
		get
		{
			lock (InstanceLock)
			{
				if (_instance == null)
				{
					FileShelfService instance = new FileShelfService();
					_instance = instance;
				}
				return _instance;
			}
		}
	}

	public OperationGate Operations { get; private set; }

	public string Id => "shelf";

	public PersistenceClass Classification => PersistenceClass.CriticalUserData;

	public OperationSnapshot CacheSnapshot => _cacheGate.Snapshot();

	internal int ActiveFileCount
	{
		get
		{
			lock (_shelves)
			{
				return _shelves[_activeShelfIndex].Files.Count;
			}
		}
	}

	public string StorageDirectory => _storageDir;

	public string OwnedDirectory => _ownedDir;

	public IReadOnlyList<ShelfTab> Shelves
	{
		get
		{
			lock (_shelves)
			{
				return _shelves.Select(CloneShelf).ToList();
			}
		}
	}

	public bool CanCreateShelf
	{
		get
		{
			lock (_shelves)
			{
				return _shelves.Count < 12;
			}
		}
	}

	public int ActiveShelfIndex
	{
		get
		{
			lock (_shelves)
			{
				return _activeShelfIndex;
			}
		}
		set
		{
			SwitchShelf(value);
		}
	}

	public ShelfTab ActiveShelf
	{
		get
		{
			lock (_shelves)
			{
				EnsureOneShelf();
				return CloneShelf(_shelves[_activeShelfIndex]);
			}
		}
	}

	public IReadOnlyList<ParkedFile> ParkedFiles
	{
		get
		{
			lock (_shelves)
			{
				return ActiveShelf.Files.ToList();
			}
		}
	}

	public ShelfSort Sort
	{
		get
		{
			return _sort;
		}
		set
		{
			Perform(delegate
			{
				_sort = value;
				return 0;
			});
		}
	}

	public IReadOnlyList<ShelfCopyRecovery> RecoveryItems
	{
		get
		{
			lock (_shelves)
			{
				return (from w in _queue
					where w.WaitingSource
					select w.Recovery.Clone()).Concat(from w in _queue.Where(IsFailedOwnedCopy)
					select new ShelfCopyRecovery
					{
						Id = "failed-" + w.Receipt.Revision,
						OriginalRevision = w.Receipt.Revision,
						ShelfId = w.ShelfId,
						Name = Path.GetFileName(w.Path),
						Path = w.Path,
						State = "Failed"
					}).ToList();
			}
		}
	}

	internal event EventHandler<ViewChange> ViewChanged;

	public event EventHandler FilesChanged;

	public event EventHandler ShelvesChanged;

	private static ShelfTab CloneShelf(ShelfTab tab)
	{
		ShelfTab shelfTab = new ShelfTab(tab.Id, tab.Name);
		shelfTab.Files = tab.Files.Select((ParkedFile f) => f.Clone()).ToList();
		return shelfTab;
	}

	private static List<RemovedItem> CloneRemoved(List<RemovedItem> items)
	{
		return items.Select((RemovedItem i) => new RemovedItem
		{
			Index = i.Index,
			File = i.File.Clone()
		}).ToList();
	}

	private T Perform<T>(Func<T> edit, Action validate = null, bool cleanupOnly = false)
	{
		T result;
		lock (_shelves)
		{
			CheckAdmission();
			validate?.Invoke();
			OperationReceipt operationReceipt = Operations.TryAccept();
			if (operationReceipt == null)
			{
				throw new InvalidOperationException("Shelf admission closed.");
			}
			try
			{
				result = edit();
				_queue.Add(new Work
				{
					Receipt = operationReceipt,
					Applied = true,
					CleanupOnly = cleanupOnly
				});
				StartWriterLocked();
			}
			catch
			{
				operationReceipt.Fail();
				throw;
			}
		}
		PostChanged();
		RefreshStatus(unprobedOnly: true);
		return result;
	}

	private void CheckAdmission()
	{
		if (_disposed || !Operations.AdmissionOpen)
		{
			throw new InvalidOperationException("Shelf admission closed.");
		}
		if (_loadFailure != null)
		{
			throw new InvalidDataException("Shelf recovery is required.", _loadFailure);
		}
	}

	private void SubmitLocked(Work work)
	{
		CheckAdmission();
		work.Receipt = Operations.TryAccept();
		if (work.Receipt == null)
		{
			throw new InvalidOperationException("Shelf admission closed.");
		}
		_queue.Add(work);
		StartWriterLocked();
	}

	private void StartWriterLocked()
	{
		if (!_initializing && !_writing && _queue.Count != 0)
		{
			_writing = true;
			_drain = Task.Run((Action)WriteLoop);
		}
	}

	private void WriteLoop()
	{
		_saveNow.Wait(500);
		_saveNow.Reset();
		HashSet<Work> failedThisRun = new HashSet<Work>();
		while (true)
		{
			Work[] array;
			lock (_shelves)
			{
				array = _queue.Where((Work w) => (!failedThisRun.Contains(w) || w.Applied) && (!w.WaitingSource || !w.RecoveryPersisted)).ToArray();
				_recoveryRestart = false;
				if (array.Length == 0)
				{
					_writing = false;
					break;
				}
				Work[] array2 = array;
				foreach (Work work2 in array2)
				{
					if (work2.Failed)
					{
						work2.Receipt = Operations.Retry(work2.Receipt.Revision);
						work2.Failed = false;
					}
					work2.Running = true;
				}
			}
			try
			{
				Work[] array3 = array;
				foreach (Work work3 in array3)
				{
					work3.Receipt.AssertWriteAllowed();
					if (work3.WaitingSource)
					{
						PersistRecoveryFailure(work3, new IOException("Fresh source or explicit cancellation required."));
					}
					else
					{
						if (work3.Applied)
						{
							continue;
						}
						try
						{
							if (work3.Apply != null)
							{
								work3.Apply(work3);
							}
							work3.Applied = true;
						}
						catch (Exception ex)
						{
							if (work3.Replaceable && !work3.Ready)
							{
								lock (_shelves)
								{
									WaitForFreshSource(work3, ex);
								}
								PersistRecoveryFailure(work3, ex);
								continue;
							}
							if (work3.Stage == null || work3.Ready || work3.Applied || ex is InvalidOperationException)
							{
								throw;
							}
							FailOwnedCopy(work3, ex);
							failedThisRun.Add(work3);
						}
					}
				}
				Work[] array4 = array.Where((Work w) => !w.Failed).ToArray();
				if (array4.Length != 0)
				{
					string arg = SerializeSnapshot();
					Work[] array5 = array4;
					foreach (Work work4 in array5)
					{
						work4.Receipt.AssertWriteAllowed();
					}
					if (array4.Any((Work work) => !work.CleanupOnly) && !_persist(_shelvesPath, arg))
					{
						throw new IOException("Shelf metadata write failed.");
					}
					ReclaimRetired();
					Work[] array6 = array4;
					foreach (Work work5 in array6)
					{
						work5.Receipt.CompleteDurably();
					}
					lock (_shelves)
					{
						Work[] array7 = array4;
						foreach (Work work6 in array7)
						{
							_queue.Remove(work6);
							work6.Running = false;
						}
					}
					Work[] array8 = array4;
					foreach (Work work7 in array8)
					{
						if (work7.Completion != null)
						{
							work7.Completion.TrySetResult(work7.Result);
						}
					}
				}
				PostChanged();
			}
			catch (Exception ex2)
			{
				TaskCompletionSource<string>[] array10;
				lock (_shelves)
				{
					Work[] array9 = _queue.Where((Work w) => !w.Failed).ToArray();
					array10 = (from w in array9
						select w.Completion into c
						where c != null
						select c).ToArray();
					Work[] array11 = array9;
					foreach (Work work8 in array11)
					{
						if (work8.Replaceable && !work8.Ready && !work8.Applied)
						{
							WaitForFreshSource(work8, ex2);
						}
						work8.Receipt.Fail();
						work8.Failed = true;
						work8.Running = false;
					}
					Work[] array12 = array;
					foreach (Work work9 in array12)
					{
						work9.Running = false;
					}
				}
				TaskCompletionSource<string>[] array13 = array10;
				foreach (TaskCompletionSource<string> taskCompletionSource in array13)
				{
					taskCompletionSource.TrySetException(ex2);
				}
				PersistenceHelper.ReportSaveFailure(_shelvesPath, ex2);
				lock (_shelves)
				{
					Work[] array9 = _queue.Where((Work w) => !w.Failed).ToArray();
					array10 = (from w in array9
						select w.Completion into c
						where c != null
						select c).ToArray();
					Work[] array14 = array9;
					foreach (Work work10 in array14)
					{
						if (work10.Replaceable && !work10.Ready && !work10.Applied)
						{
							WaitForFreshSource(work10, ex2);
						}
						work10.Receipt.Fail();
						work10.Failed = true;
						work10.Running = false;
					}
					_writing = false;
					if (_recoveryRestart)
					{
						StartWriterLocked();
					}
				}
				TaskCompletionSource<string>[] array15 = array10;
				foreach (TaskCompletionSource<string> taskCompletionSource2 in array15)
				{
					taskCompletionSource2.TrySetException(ex2);
				}
				PostChanged();
				break;
			}
		}
	}

	private static void WaitForFreshSource(Work work, Exception error)
	{
		work.WaitingSource = true;
		work.RecoveryPersisted = false;
		work.Copy = null;
		work.CheckSource = null;
		work.Recovery.State = "NeedsSource";
		work.Recovery.Error = error.GetType().Name;
	}

	private void FailOwnedCopy(Work work, Exception error)
	{
		TaskCompletionSource<string> completion;
		lock (_shelves)
		{
			completion = work.Completion;
			work.Receipt.Fail();
			work.Failed = true;
			work.Running = false;
		}
		completion?.TrySetException(error);
		PersistenceHelper.ReportSaveFailure(_shelvesPath, error);
	}

	private void PersistRecoveryFailure(Work work, Exception error)
	{
		work.Receipt.AssertWriteAllowed();
		if (work.Prefix == null && File.Exists(work.Stage))
		{
			work.Prefix = work.Stage;
		}
		lock (_shelves)
		{
			work.Recovery.Prefix = work.Prefix;
		}
		if (!_persist(_shelvesPath, SerializeSnapshot()))
		{
			throw new IOException("Shelf recovery record write failed.", error);
		}
		TaskCompletionSource<string> completion;
		lock (_shelves)
		{
			completion = work.Completion;
			work.RecoveryPersisted = true;
			work.Receipt.Fail();
			work.Failed = true;
			work.Running = false;
		}
		completion?.TrySetException(error);
	}

	private void ReclaimRetired()
	{
		string[] array;
		lock (_shelves)
		{
			array = _eligible.ToArray();
		}
		if (array.Length == 0 || Directory.GetFiles(_storageDir, "parked_shelves.json*.recovery.*").Length != 0)
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] array2 = new string[2]
		{
			_shelvesPath,
			_shelvesPath + ".bak"
		};
		foreach (string path2 in array2)
		{
			if (!File.Exists(path2))
			{
				continue;
			}
			ShelfStorageDto shelfStorageDto = Parse(PersistenceHelper.ReadShared(path2));
			foreach (ShelfTabDto shelf in shelfStorageDto.Shelves)
			{
				if (shelf.Items != null)
				{
					foreach (ShelfItemDto item in shelf.Items)
					{
						hashSet.Add(ComparisonPath(item.Path));
					}
				}
				if (shelf.Paths == null)
				{
					continue;
				}
				foreach (string path3 in shelf.Paths)
				{
					hashSet.Add(ComparisonPath(path3));
				}
			}
		}
		string[] array3 = array;
		foreach (string path in array3)
		{
			if (hashSet.Contains(ComparisonPath(path)))
			{
				continue;
			}
			ValidateOwnedPath(path);
			lock (_shelves)
			{
				if (_shelves.Any((ShelfTab s) => s.Files.Any((ParkedFile f) => string.Equals(ComparisonPath(f.FullPath), ComparisonPath(path), StringComparison.OrdinalIgnoreCase))))
				{
					continue;
				}
				_deleting.Add(path);
				goto IL_0220;
			}
			IL_0220:
			try
			{
				_delete(path);
				lock (_shelves)
				{
					_eligible.Remove(path);
					_retired.Remove(path);
					_reclaimed.Add(path);
				}
			}
			finally
			{
				lock (_shelves)
				{
					_deleting.Remove(path);
				}
			}
		}
	}

	private void CheckRestore(IEnumerable<ParkedFile> files)
	{
		foreach (ParkedFile file in files)
		{
			if (file == null || (file.IsOwned && !IsInside(file.FullPath, _ownedDir)))
			{
				throw new InvalidDataException("Invalid owned restore path.");
			}
			if (_reclaimed.Contains(file.FullPath))
			{
				throw new InvalidOperationException("Owned copy was reclaimed; recover the file before undo.");
			}
			if (_deleting.Contains(file.FullPath))
			{
				throw new InvalidOperationException("Owned deletion has begun; undo window expired.");
			}
		}
	}

	public void SwitchShelf(int index)
	{
		Perform(delegate
		{
			SwitchShelfCore(index);
			return 0;
		});
	}

	public void SwitchShelf(string id)
	{
		Perform(delegate
		{
			SwitchShelfCore(id);
			return 0;
		});
	}

	public ShelfTab CreateShelf(string name = null)
	{
		return Perform(delegate
		{
			ShelfTab shelfTab = CreateShelfCore(name);
			return (shelfTab != null) ? CloneShelf(shelfTab) : null;
		});
	}

	public void RenameShelf(int index, string name)
	{
		Perform(delegate
		{
			RenameShelfCore(index, name);
			return 0;
		});
	}

	public void RenameShelf(string id, string name)
	{
		Perform(delegate
		{
			RenameShelfCore(id, name);
			return 0;
		});
	}

	public ShelfTab DeleteShelf(string id, out int index, out bool active)
	{
		int i = 0;
		bool a = false;
		ShelfTab result = Perform(delegate
		{
			ShelfTab shelfTab = DeleteShelfCore(id, out i, out a);
			return (shelfTab != null) ? CloneShelf(shelfTab) : null;
		});
		index = i;
		active = a;
		return result;
	}

	public void RemoveShelf(int index)
	{
		Perform(delegate
		{
			RemoveShelfCore(index);
			return 0;
		});
	}

	public void RestoreShelf(ShelfTab tab, int index, bool active)
	{
		if (tab == null)
		{
			return;
		}
		ShelfTab clone = CloneShelf(tab);
		Perform(delegate
		{
			RestoreShelfCore(clone, index, active);
			if (_deletedEpochs.TryGetValue(clone.Id, out var value) && Epoch(clone.Id) == value + 1)
			{
				_epochs[clone.Id] = value;
				_deletedEpochs.Remove(clone.Id);
				RestoreSuperseded(clone.Id, value);
			}
			return 0;
		}, delegate
		{
			CheckRestore(clone.Files);
		});
	}

	public void MoveShelf(string id, int index)
	{
		Perform(delegate
		{
			MoveShelfCore(id, index);
			return 0;
		});
	}

	public void AddFiles(string[] paths)
	{
		AddFilesTo(ActiveShelf.Id, paths, owned: false);
	}

	public int AddFilesTo(string id, IEnumerable<string> paths, bool owned)
	{
		string[] copy = (paths ?? Enumerable.Empty<string>()).ToArray();
		if (owned && copy.Any((string p) => !IsInside(NormalizePath(p), _ownedDir)))
		{
			throw new InvalidDataException("External files cannot be owned.");
		}
		return Perform(() => AddFilesToCore(id, copy, owned), delegate
		{
			if (copy.Any((string p) => _deleting.Any((string d) => ComparisonPath(d) == ComparisonPath(p))))
			{
				throw new InvalidOperationException("Owned deletion is already in progress.");
			}
		});
	}

	public void RemoveFile(string id)
	{
		RemoveItems(ActiveShelf.Id, new string[1] { id });
	}

	public List<RemovedItem> RemoveItems(string id, IEnumerable<string> ids)
	{
		string[] copy = (ids ?? Enumerable.Empty<string>()).ToArray();
		return Perform(() => CloneRemoved(RemoveItemsCore(id, copy)));
	}

	public void RestoreItems(string id, List<RemovedItem> items)
	{
		if (items == null)
		{
			return;
		}
		List<RemovedItem> copy = CloneRemoved(items);
		Perform(delegate
		{
			RestoreItemsCore(id, copy);
			if (_clearUndo.TryGetValue(items, out var value) && value.Item1 == id && Epoch(id) == value.Item2 + 1)
			{
				_epochs[id] = value.Item2;
				_clearUndo.Remove(items);
				RestoreSuperseded(id, value.Item2);
			}
			return 0;
		}, delegate
		{
			CheckRestore(copy.Select((RemovedItem i) => i.File));
		});
	}

	public void ClearShelf(int index = -1)
	{
		Perform(delegate
		{
			ClearShelfCore(index);
			return 0;
		});
	}

	public List<RemovedItem> ClearShelf(string id)
	{
		return Perform(delegate
		{
			long item = Epoch(id);
			List<RemovedItem> list = CloneRemoved(ClearShelfCore(id));
			_clearUndo[list] = Tuple.Create(id, item);
			return list;
		});
	}

	public int MoveItems(string from, string to, IEnumerable<string> ids)
	{
		string[] copy = (ids ?? Enumerable.Empty<string>()).ToArray();
		return Perform(() => MoveItemsCore(from, to, copy));
	}

	public void RelocateItem(string id, string path)
	{
		Perform(delegate
		{
			RelocateItemCore(id, path);
			return 0;
		});
	}

	public Task PrepareAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		if (PrepareEditor != null)
		{
			PrepareEditor();
		}
		return Task.FromResult(0);
	}

	public Task QuiesceAsync(CancellationToken token)
	{
		_paused = true;
		CacheLease[] array;
		lock (_cacheLock)
		{
			_cacheGate.CloseAdmission();
			array = _cache.ToArray();
			CacheLease[] array2 = array;
			foreach (CacheLease cacheLease in array2)
			{
				cacheLease.Active = false;
				cacheLease.Receipt.Fail();
			}
			_cache.Clear();
		}
		CacheLease[] array3 = array;
		foreach (CacheLease cacheLease2 in array3)
		{
			cacheLease2.Settled.TrySetResult(result: false);
		}
		return Task.FromResult(0);
	}

	public async Task VerifyAsync(CancellationToken token)
	{
		await _initialized.Task.ConfigureAwait(continueOnCapturedContext: false);
		_saveNow.Set();
		Task task;
		lock (_shelves)
		{
			StartWriterLocked();
			task = _drain;
		}
		await task.ConfigureAwait(continueOnCapturedContext: false);
		token.ThrowIfCancellationRequested();
		await Task.Run(delegate
		{
			AssertDurable();
			SerializeSnapshot();
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void AssertDurable()
	{
		OperationSnapshot operationSnapshot = Operations.Snapshot();
		if (_loadFailure != null || operationSnapshot.Pending != 0 || operationSnapshot.Failed != 0 || operationSnapshot.Violations != 0 || operationSnapshot.Accepted != operationSnapshot.Durable)
		{
			throw new IOException("Shelf durability incomplete.", _loadFailure);
		}
	}

	public Task ResumeAsync()
	{
		lock (_cacheLock)
		{
			_cacheGate.Reopen();
		}
		_paused = false;
		PostChanged();
		_dispatcher.BeginInvoke((Action)delegate
		{
			if (!_paused && !_disposed && Operations.AdmissionOpen)
			{
				RefreshStatus();
			}
		}, DispatcherPriority.Background);
		return Task.FromResult(0);
	}

	public Task DisposeAsync()
	{
		AssertDurable();
		_disposed = true;
		_paused = true;
		lock (_cacheLock)
		{
			_cacheGate.CloseAdmission();
			_cacheGate.Seal(requireDurability: false);
			_cacheGate.CommitSeal();
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
			RefreshStatus();
		};
	}

	public void SealForDisposal()
	{
		AssertDurable();
		Operations.Seal(requireDurability: true);
		Operations.CommitSeal();
	}

	private void OnDeferredReopen()
	{
		try
		{
			_dispatcher.BeginInvoke((Action)delegate
			{
				if (!_paused && !_disposed && Operations.AdmissionOpen)
				{
					PostChanged();
					RefreshStatus();
				}
			}, DispatcherPriority.Background);
		}
		catch (InvalidOperationException ex)
		{
			TransactionLogger.Warn("Shelf dispatcher stopped: " + ex.Message);
		}
	}

	private void TrackItem(string shelfId, ParkedFile file)
	{
		_itemIndex[file.Id] = new ItemSlot
		{
			ShelfId = shelfId,
			File = file
		};
		_unprobedIds.Add(file.Id);
		PublishItem(shelfId, file);
	}

	private void PublishItem(string shelfId, ParkedFile file)
	{
		_viewPending[file.Id] = new ViewItem
		{
			ShelfId = shelfId,
			File = file.Clone()
		};
	}

	private long OrderForInsert(ShelfTab shelf, int index)
	{
		if (index == shelf.Files.Count)
		{
			if (index != 0)
			{
				return shelf.Files[index - 1].ViewOrder + 1024;
			}
			return 1024L;
		}
		long num = ((index == 0) ? 0 : shelf.Files[index - 1].ViewOrder);
		long viewOrder = shelf.Files[index].ViewOrder;
		if (viewOrder - num <= 1)
		{
			for (int i = 0; i < shelf.Files.Count; i++)
			{
				shelf.Files[i].ViewOrder = ((long)i + 1L) * 1024;
				PublishItem(shelf.Id, shelf.Files[i]);
			}
			num = ((index == 0) ? 0 : shelf.Files[index - 1].ViewOrder);
			viewOrder = shelf.Files[index].ViewOrder;
		}
		return num + (viewOrder - num) / 2;
	}

	private void UntrackItem(string shelfId, ParkedFile file)
	{
		_itemIndex.Remove(file.Id);
		_unprobedIds.Remove(file.Id);
		_viewPending[file.Id] = new ViewItem
		{
			ShelfId = shelfId,
			File = new ParkedFile
			{
				Id = file.Id
			}
		};
	}

	private ViewChange CaptureView(bool reset)
	{
		lock (_shelves)
		{
			ViewChange viewChange = new ViewChange();
			viewChange.ActiveId = _shelves[_activeShelfIndex].Id;
			viewChange.Sort = _sort;
			viewChange.Reset = reset;
			viewChange.Shelves = _shelves.Select((ShelfTab s) => new ViewShelf
			{
				Id = s.Id,
				Name = s.Name,
				Count = s.Files.Count
			}).ToArray();
			viewChange.Items = (reset ? _shelves.SelectMany((ShelfTab s) => s.Files.Select((ParkedFile f) => new ViewItem
			{
				ShelfId = s.Id,
				File = f.Clone()
			})).ToArray() : _viewPending.Values.ToArray());
			ViewChange result = viewChange;
			if (!reset)
			{
				_viewPending.Clear();
			}
			return result;
		}
	}

	internal ViewChange ReadView()
	{
		return CaptureView(reset: true);
	}

	internal string ResolveShelfId(string preferred)
	{
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == preferred);
			return (shelfTab ?? _shelves[_activeShelfIndex]).Id;
		}
	}

	internal string ResolveShelfName(string preferred)
	{
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == preferred);
			return (shelfTab ?? _shelves[_activeShelfIndex]).Name;
		}
	}

	public FileShelfService()
		: this(refresh: true, register: true)
	{
	}

	public FileShelfService(bool refresh)
		: this(refresh, register: false)
	{
	}

	private FileShelfService(bool refresh, bool register)
	{
		if (!register && !AppPaths.IsSandboxed)
		{
			throw new InvalidOperationException("Synthetic profile required.");
		}
		_statusEnabled = refresh;
		_dispatcher = ((Application.Current != null) ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);
		_storageDir = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "shelf");
		_ownedDir = Path.Combine(_storageDir, "stored");
		_shelvesPath = Path.Combine(_storageDir, "parked_shelves.json");
		_legacyPath = Path.Combine(_storageDir, "parked_files.json");
		Operations = new OperationGate();
		Operations.DeferredReopenCompleted += OnDeferredReopen;
		_persist = PersistenceHelper.WriteOwnedText;
		_probe = (ParkedFile f) => f.Probe();
		LoadShelves();
		lock (_shelves)
		{
			foreach (ShelfTab shelf in _shelves)
			{
				for (int i = 0; i < shelf.Files.Count; i++)
				{
					shelf.Files[i].ViewOrder = ((long)i + 1L) * 1024;
					TrackItem(shelf.Id, shelf.Files[i]);
				}
			}
		}
		if (register)
		{
			NotesOwnership.Registry.Register(this);
		}
		try
		{
			Directory.CreateDirectory(_storageDir);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Shelf directory unavailable: " + ex.Message);
		}
		lock (_shelves)
		{
			_initializing = false;
			StartWriterLocked();
		}
		_initialized.TrySetResult(result: true);
		PersistenceHelper.RegisterExitPreparation("shelf-editor", delegate
		{
			if (PrepareEditor != null)
			{
				PrepareEditor();
			}
		});
		PersistenceHelper.RegisterExitFlush("shelf", Flush);
		RefreshStatus();
	}

	public ShelfTab FindShelf(string shelfId)
	{
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == shelfId);
			return (shelfTab == null) ? null : CloneShelf(shelfTab);
		}
	}

	public int IndexOfShelf(string shelfId)
	{
		lock (_shelves)
		{
			return _shelves.FindIndex((ShelfTab s) => s.Id == shelfId);
		}
	}

	private void SwitchShelfCore(int index)
	{
		lock (_shelves)
		{
			if (index < 0 || index >= _shelves.Count || index == _activeShelfIndex)
			{
				return;
			}
			_activeShelfIndex = index;
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
	}

	private void SwitchShelfCore(string shelfId)
	{
		SwitchShelfCore(IndexOfShelf(shelfId));
	}

	public string AllocateNextShelfName()
	{
		lock (_shelves)
		{
			HashSet<string> hashSet = new HashSet<string>(_shelves.Select((ShelfTab s) => (s.Name ?? string.Empty).Trim()), StringComparer.OrdinalIgnoreCase);
			int i;
			for (i = _shelves.Count + 1; hashSet.Contains("Shelf " + i); i++)
			{
			}
			return "Shelf " + i;
		}
	}

	private ShelfTab CreateShelfCore(string name = null)
	{
		ShelfTab shelfTab;
		lock (_shelves)
		{
			if (_shelves.Count >= 12)
			{
				return null;
			}
			name = CleanName(name);
			if (string.IsNullOrEmpty(name))
			{
				name = AllocateNextShelfName();
			}
			shelfTab = new ShelfTab(Guid.NewGuid().ToString("N"), name);
			_shelves.Add(shelfTab);
			_activeShelfIndex = _shelves.Count - 1;
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
		return shelfTab;
	}

	private void RenameShelfCore(int index, string newName)
	{
		newName = CleanName(newName);
		if (string.IsNullOrEmpty(newName))
		{
			return;
		}
		lock (_shelves)
		{
			if (index < 0 || index >= _shelves.Count || _shelves[index].Name == newName)
			{
				return;
			}
			_shelves[index].Name = newName;
		}
		SaveShelves();
		NotifyShelvesChanged();
	}

	private void RenameShelfCore(string shelfId, string newName)
	{
		RenameShelfCore(IndexOfShelf(shelfId), newName);
	}

	private ShelfTab DeleteShelfCore(string shelfId, out int index, out bool wasActive)
	{
		ShelfTab shelfTab = null;
		lock (_shelves)
		{
			index = _shelves.FindIndex((ShelfTab s) => s.Id == shelfId);
			wasActive = index == _activeShelfIndex;
			if (_shelves.Count <= 1 || index < 0)
			{
				return null;
			}
			string activeId = _shelves[_activeShelfIndex].Id;
			shelfTab = _shelves[index];
			foreach (ParkedFile file in shelfTab.Files)
			{
				Retire(file);
				UntrackItem(shelfId, file);
			}
			_deletedEpochs[shelfId] = Epoch(shelfId);
			AdvanceEpoch(shelfId);
			_shelves.RemoveAt(index);
			_activeShelfIndex = (wasActive ? Math.Min(index, _shelves.Count - 1) : Math.Max(0, _shelves.FindIndex((ShelfTab s) => s.Id == activeId)));
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
		return shelfTab;
	}

	private void RemoveShelfCore(int index)
	{
		ShelfTab shelfTab;
		lock (_shelves)
		{
			shelfTab = ((index >= 0 && index < _shelves.Count) ? _shelves[index] : null);
		}
		if (shelfTab != null)
		{
			DeleteShelfCore(shelfTab.Id, out var _, out var _);
		}
	}

	private void RestoreShelfCore(ShelfTab shelf, int index, bool makeActive)
	{
		if (shelf == null)
		{
			return;
		}
		lock (_shelves)
		{
			if (_shelves.Any((ShelfTab s) => s.Id == shelf.Id))
			{
				return;
			}
			index = Math.Max(0, Math.Min(index, _shelves.Count));
			string activeId = ((_shelves.Count > 0) ? _shelves[_activeShelfIndex].Id : null);
			_shelves.Insert(index, shelf);
			for (int i = 0; i < shelf.Files.Count; i++)
			{
				shelf.Files[i].ViewOrder = ((long)i + 1L) * 1024;
				TrackItem(shelf.Id, shelf.Files[i]);
			}
			_activeShelfIndex = (makeActive ? index : Math.Max(0, _shelves.FindIndex((ShelfTab s) => s.Id == activeId)));
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
	}

	private void MoveShelfCore(string shelfId, int newIndex)
	{
		lock (_shelves)
		{
			int num = _shelves.FindIndex((ShelfTab s) => s.Id == shelfId);
			if (num < 0)
			{
				return;
			}
			newIndex = Math.Max(0, Math.Min(newIndex, _shelves.Count - 1));
			if (num == newIndex)
			{
				return;
			}
			string activeId = _shelves[_activeShelfIndex].Id;
			ShelfTab item = _shelves[num];
			_shelves.RemoveAt(num);
			_shelves.Insert(newIndex, item);
			_activeShelfIndex = _shelves.FindIndex((ShelfTab s) => s.Id == activeId);
		}
		SaveShelves();
		NotifyShelvesChanged();
	}

	private void AddFilesCore(string[] paths)
	{
		AddFilesToCore(ActiveShelf.Id, paths, owned: false);
	}

	private int AddFilesToCore(string shelfId, IEnumerable<string> paths, bool owned)
	{
		if (paths == null)
		{
			return 0;
		}
		int num = 0;
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == shelfId);
			if (shelfTab == null)
			{
				return 0;
			}
			HashSet<string> hashSet = new HashSet<string>(shelfTab.Files.Select((ParkedFile f) => f.FullPath), StringComparer.OrdinalIgnoreCase);
			foreach (string path in paths)
			{
				string text = NormalizePath(path);
				if (text != null && !hashSet.Contains(text))
				{
					ParkedFile parkedFile = new ParkedFile(text);
					parkedFile.IsOwned = owned;
					ParkedFile parkedFile2 = parkedFile;
					parkedFile2.ViewOrder = OrderForInsert(shelfTab, shelfTab.Files.Count);
					shelfTab.Files.Add(parkedFile2);
					TrackItem(shelfId, parkedFile2);
					hashSet.Add(text);
					num++;
				}
			}
		}
		if (num == 0)
		{
			return 0;
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
		return num;
	}

	private void RemoveFileCore(string id)
	{
		RemoveItemsCore(ActiveShelf.Id, new string[1] { id });
	}

	private List<RemovedItem> RemoveItemsCore(string shelfId, IEnumerable<string> ids)
	{
		List<RemovedItem> list = new List<RemovedItem>();
		HashSet<string> set = new HashSet<string>(ids ?? Enumerable.Empty<string>());
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == shelfId);
			if (shelfTab == null || set.Count == 0)
			{
				return list;
			}
			for (int i = 0; i < shelfTab.Files.Count; i++)
			{
				if (set.Contains(shelfTab.Files[i].Id))
				{
					list.Add(new RemovedItem
					{
						Index = i,
						File = shelfTab.Files[i]
					});
				}
			}
			foreach (RemovedItem item in list)
			{
				Retire(item.File);
				UntrackItem(shelfId, item.File);
			}
			shelfTab.Files.RemoveAll((ParkedFile f) => set.Contains(f.Id));
		}
		if (list.Count == 0)
		{
			return list;
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
		return list;
	}

	private void RestoreItemsCore(string shelfId, List<RemovedItem> removed)
	{
		if (removed == null || removed.Count == 0)
		{
			return;
		}
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == shelfId);
			if (shelfTab == null)
			{
				return;
			}
			HashSet<string> hashSet = new HashSet<string>(shelfTab.Files.Select((ParkedFile f) => f.FullPath), StringComparer.OrdinalIgnoreCase);
			foreach (RemovedItem item in removed.OrderBy((RemovedItem r) => r.Index))
			{
				if (!hashSet.Contains(item.File.FullPath))
				{
					int index = Math.Min(item.Index, shelfTab.Files.Count);
					item.File.ViewOrder = OrderForInsert(shelfTab, index);
					shelfTab.Files.Insert(index, item.File);
					TrackItem(shelfId, item.File);
					hashSet.Add(item.File.FullPath);
				}
			}
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
	}

	private void ClearShelfCore(int index = -1)
	{
		ShelfTab shelfTab;
		lock (_shelves)
		{
			int num = ((index >= 0 && index < _shelves.Count) ? index : _activeShelfIndex);
			shelfTab = ((num >= 0 && num < _shelves.Count) ? _shelves[num] : null);
		}
		if (shelfTab != null)
		{
			ClearShelfCore(shelfTab.Id);
		}
	}

	private List<RemovedItem> ClearShelfCore(string shelfId)
	{
		List<string> ids;
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == shelfId);
			if (shelfTab == null)
			{
				return new List<RemovedItem>();
			}
			ids = shelfTab.Files.Select((ParkedFile f) => f.Id).ToList();
		}
		AdvanceEpoch(shelfId);
		return RemoveItemsCore(shelfId, ids);
	}

	private int MoveItemsCore(string fromShelfId, string toShelfId, IEnumerable<string> ids)
	{
		if (fromShelfId == toShelfId)
		{
			return 0;
		}
		int num = 0;
		HashSet<string> set = new HashSet<string>(ids ?? Enumerable.Empty<string>());
		lock (_shelves)
		{
			ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == fromShelfId);
			ShelfTab shelfTab2 = _shelves.FirstOrDefault((ShelfTab s) => s.Id == toShelfId);
			if (shelfTab == null || shelfTab2 == null)
			{
				return 0;
			}
			HashSet<string> hashSet = new HashSet<string>(shelfTab2.Files.Select((ParkedFile f) => f.FullPath), StringComparer.OrdinalIgnoreCase);
			foreach (ParkedFile item in shelfTab.Files.Where((ParkedFile f) => set.Contains(f.Id)).ToList())
			{
				shelfTab.Files.Remove(item);
				UntrackItem(fromShelfId, item);
				if (!hashSet.Contains(item.FullPath))
				{
					item.ViewOrder = OrderForInsert(shelfTab2, shelfTab2.Files.Count);
					shelfTab2.Files.Add(item);
					TrackItem(toShelfId, item);
					hashSet.Add(item.FullPath);
				}
				num++;
			}
		}
		if (num == 0)
		{
			return 0;
		}
		SaveShelves();
		NotifyShelvesChanged();
		NotifyChanged();
		return num;
	}

	private void RelocateItemCore(string itemId, string newPath)
	{
		newPath = NormalizePath(newPath);
		if (newPath == null)
		{
			return;
		}
		ParkedFile item = null;
		lock (_shelves)
		{
			foreach (ShelfTab shelf in _shelves)
			{
				item = shelf.Files.FirstOrDefault((ParkedFile f) => f.Id == itemId);
				if (item != null)
				{
					if (shelf.Files.Any((ParkedFile f) => f != item && string.Equals(f.FullPath, newPath, StringComparison.OrdinalIgnoreCase)))
					{
						shelf.Files.Remove(item);
						UntrackItem(shelf.Id, item);
						break;
					}
					Retire(item);
					item.SetPath(newPath);
					item.IsOwned = false;
					TrackItem(shelf.Id, item);
					break;
				}
			}
		}
		if (item != null)
		{
			SaveShelves();
			NotifyShelvesChanged();
			NotifyChanged();
		}
	}

	public void PurgeUnreferencedOwned()
	{
		Perform(delegate
		{
			foreach (string item in _retired)
			{
				_eligible.Add(item);
			}
			return 0;
		}, null, cleanupOnly: true);
	}

	private void Retire(ParkedFile file)
	{
		if (file != null && file.IsOwned)
		{
			_retired.Add(file.FullPath);
		}
	}

	private void RestoreSuperseded(string id, long epoch)
	{
		foreach (Work item in _queue.Where((Work w) => w.ShelfId == id && w.Epoch == epoch && w.Recovery != null))
		{
			item.Recovery.Suppressed = false;
		}
		Superseded[] array = _superseded.Where((Superseded x) => x.Shelf == id && x.Epoch == epoch).ToArray();
		foreach (Superseded superseded in array)
		{
			AddFilesToCore(id, new string[1] { superseded.Path }, owned: true);
			_superseded.Remove(superseded);
		}
	}

	private void AdvanceEpoch(string id)
	{
		_epochs.TryGetValue(id, out var value);
		_epochs[id] = value + 1;
		foreach (Work item in _queue.Where((Work w) => w.ShelfId == id && w.Recovery != null))
		{
			item.Recovery.Suppressed = true;
		}
	}

	private long Epoch(string id)
	{
		if (!_epochs.TryGetValue(id, out var value))
		{
			return 0L;
		}
		return value;
	}

	public Task<string> ImportStreamAsync(string shelfId, string name, Stream content)
	{
		if (content == null)
		{
			throw new ArgumentNullException("content");
		}
		int attempted = 0;
		return OwnedAsync(shelfId, name, delegate(Stream output)
		{
			Interlocked.Exchange(ref attempted, 1);
			content.CopyTo(output);
		}, replaceable: true, image: false, delegate
		{
			if (Volatile.Read(ref attempted) != 0)
			{
				throw new InvalidOperationException("Source already consumed; provide a replacement stream to RetryOwnedCopy.");
			}
		});
	}

	public Task<string> ImportImageAsync(string shelfId, BitmapSource image, string name = null)
	{
		if (image == null)
		{
			throw new ArgumentNullException("image");
		}
		BitmapSource frozen = (image.IsFrozen ? image : image.CloneCurrentValue());
		if (!frozen.IsFrozen)
		{
			frozen.Freeze();
		}
		return OwnedAsync(shelfId, (name ?? ("Clipboard image " + DateTime.Now.ToString("yyyy-MM-dd HHmmss"))) + ".png", delegate(Stream output)
		{
			_encode(frozen, output);
		}, replaceable: false, image: true);
	}

	public Task<string> CopyFileAsync(string shelfId, string source)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			throw new ArgumentException("Source required.");
		}
		return OwnedAsync(shelfId, Path.GetFileName(source), delegate(Stream output)
		{
			using FileStream fileStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
			fileStream.CopyTo(output);
		});
	}

	public string StoreStream(string name, Stream content)
	{
		if (_dispatcher.CheckAccess())
		{
			throw new InvalidOperationException("Use ImportStreamAsync on the dispatcher.");
		}
		return ImportStreamAsync(null, name, content).GetAwaiter().GetResult();
	}

	public string StoreImage(BitmapSource image, string name = null)
	{
		if (_dispatcher.CheckAccess())
		{
			throw new InvalidOperationException("Use ImportImageAsync on the dispatcher.");
		}
		return ImportImageAsync(null, image, name).GetAwaiter().GetResult();
	}

	private Task<string> OwnedAsync(string shelfId, string name, Action<Stream> copy, bool replaceable = false, bool image = false, Action checkSource = null)
	{
		string text = Path.Combine(_ownedDir, Guid.NewGuid().ToString("N"), SanitizeFileName(string.IsNullOrWhiteSpace(name) ? "Dropped file" : name, MaxOwnedNameLength()));
		Work work = new Work();
		work.Completion = NewCompletion();
		work.Copy = copy;
		work.Stage = text + ".partial";
		work.Path = text;
		work.ShelfId = shelfId;
		work.Replaceable = replaceable;
		work.Image = image;
		work.CheckSource = checkSource;
		work.Apply = ApplyOwned;
		Work work2 = work;
		lock (_shelves)
		{
			CheckAdmission();
			work2.Epoch = Epoch(shelfId ?? string.Empty);
			if (replaceable)
			{
				work2.Recovery = new ShelfCopyRecovery
				{
					Id = Guid.NewGuid().ToString("N"),
					ShelfId = shelfId,
					Name = Path.GetFileName(text),
					Path = text,
					State = "Pending"
				};
			}
			work2.Receipt = Operations.TryAccept();
			if (work2.Receipt == null)
			{
				throw new InvalidOperationException("Shelf admission closed.");
			}
			if (work2.Recovery != null)
			{
				work2.Recovery.OriginalRevision = work2.Receipt.Revision;
				_recoveries.Add(work2.Recovery);
			}
			_queue.Add(work2);
			StartWriterLocked();
		}
		return work2.Completion.Task;
	}

	private static TaskCompletionSource<string> NewCompletion()
	{
		return new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	private void ApplyOwned(Work work)
	{
		ValidateOwnedPath(work.Path);
		if (!work.Ready)
		{
			if (work.CheckSource != null)
			{
				work.CheckSource();
			}
			if (work.Recovery != null)
			{
				work.Receipt.AssertWriteAllowed();
				if (!_persist(_shelvesPath, SerializeSnapshot()))
				{
					throw new IOException("Shelf copy intent write failed.");
				}
			}
			Directory.CreateDirectory(Path.GetDirectoryName(work.Path));
			if (work.Recovery != null && work.Recovery.EvidenceFrom != null && !ExistsStrict(work.Prefix) && ExistsStrict(work.Recovery.EvidenceFrom))
			{
				work.Prefix = work.Recovery.EvidenceFrom;
			}
			string[] array = new string[2] { work.Stage, work.Path };
			foreach (string text in array)
			{
				if (!File.Exists(text))
				{
					continue;
				}
				string text2 = work.Stage + ".interrupted." + Guid.NewGuid().ToString("N");
				if (work.Prefix == null || work.Prefix == text)
				{
					work.Prefix = text2;
					lock (_shelves)
					{
						if (work.Recovery != null)
						{
							work.Recovery.Prefix = text2;
							work.Recovery.EvidenceFrom = text;
						}
					}
					if (work.Recovery != null && !_persist(_shelvesPath, SerializeSnapshot()))
					{
						throw new IOException("Shelf evidence relocation intent failed.");
					}
				}
				File.Move(text, text2);
				lock (_shelves)
				{
					if (work.Recovery != null)
					{
						work.Recovery.EvidenceFrom = null;
					}
				}
			}
			lock (_shelves)
			{
				if (work.Recovery != null)
				{
					work.Recovery.Prefix = work.Prefix;
				}
			}
			if (work.Recovery != null && !_persist(_shelvesPath, SerializeSnapshot()))
			{
				throw new IOException("Shelf recovery intent write failed.");
			}
			using (FileStream fileStream = new FileStream(work.Stage, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				work.Copy(fileStream);
				fileStream.Flush(flushToDisk: true);
			}
			if (work.Image)
			{
				using FileStream bitmapStream = new FileStream(work.Stage, FileMode.Open, FileAccess.Read, FileShare.Read);
				BitmapDecoder bitmapDecoder = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
				if (bitmapDecoder.Frames.Count == 0 || bitmapDecoder.Frames[0].PixelWidth <= 0)
				{
					throw new IOException("Encoded shelf image is incomplete.");
				}
			}
			File.Move(work.Stage, work.Path);
			work.Ready = true;
		}
		if (!File.Exists(work.Path))
		{
			throw new IOException("Owned copy is missing.");
		}
		work.Result = work.Path;
		lock (_shelves)
		{
			if (work.ShelfId != null)
			{
				ShelfTab shelfTab = _shelves.FirstOrDefault((ShelfTab s) => s.Id == work.ShelfId);
				if (shelfTab == null || Epoch(work.ShelfId) != work.Epoch || (work.Recovery != null && work.Recovery.Suppressed))
				{
					work.Result = null;
					_superseded.Add(new Superseded
					{
						Shelf = work.ShelfId,
						Path = work.Path,
						Epoch = work.Epoch
					});
				}
				else
				{
					AddFilesToCore(work.ShelfId, new string[1] { work.Path }, owned: true);
				}
			}
			if (work.Recovery != null)
			{
				work.Recovery.State = ((work.Result == null) ? "Superseded" : "Completed");
			}
		}
	}

	private static bool IsFailedOwnedCopy(Work w)
	{
		if (w.Recovery == null && w.Stage != null && w.Failed && !w.Running)
		{
			return !w.Applied;
		}
		return false;
	}

	private Work FindRecoveryLocked(string id)
	{
		CheckAdmission();
		Work work = _queue.FirstOrDefault((Work w) => w.Recovery != null && w.Recovery.Id == id);
		if (work == null || !work.WaitingSource || !work.Failed || work.Running)
		{
			throw new InvalidOperationException("Copy is not awaiting recovery.");
		}
		return work;
	}

	public Task<string> CancelFailedCopyAsync(string id)
	{
		Task<string> task;
		if (id != null && id.StartsWith("failed-", StringComparison.Ordinal))
		{
			if (!long.TryParse(id.Substring("failed-".Length), out var revision))
			{
				throw new InvalidOperationException("Copy is not awaiting recovery.");
			}
			string stage;
			lock (_shelves)
			{
				CheckAdmission();
				Work work = _queue.FirstOrDefault((Work w) => IsFailedOwnedCopy(w) && w.Receipt.Revision == revision);
				if (work == null)
				{
					throw new InvalidOperationException("Copy is not awaiting recovery.");
				}
				work.Applied = true;
				work.Result = null;
				work.Copy = null;
				work.CheckSource = null;
				stage = work.Stage;
				work.Completion = NewCompletion();
				task = work.Completion.Task;
			}
			try
			{
				if (File.Exists(stage))
				{
					ValidateOwnedPath(stage);
					_delete(stage);
				}
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Shelf: discarded copy staging not removed: " + ex.GetType().Name);
			}
			lock (_shelves)
			{
				_recoveryRestart = _writing;
				StartWriterLocked();
			}
			PostChanged();
			return task;
		}
		lock (_shelves)
		{
			Work work2 = FindRecoveryLocked(id);
			work2.Recovery.State = "Cancelled";
			work2.WaitingSource = false;
			work2.Applied = true;
			work2.Result = null;
			work2.Copy = null;
			work2.CheckSource = null;
			work2.Completion = NewCompletion();
			task = work2.Completion.Task;
			_recoveryRestart = _writing;
			StartWriterLocked();
		}
		PostChanged();
		return task;
	}

	public Task<string> RetryFailedCopyFromFileAsync(string id, string source)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			throw new ArgumentException("Select the source again.");
		}
		lock (_shelves)
		{
			return ReplaceRecoveryLocked(FindRecoveryLocked(id), delegate(Stream output)
			{
				using FileStream replacement = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
				CopyReplacement(id, replacement, output);
			});
		}
	}

	private Task<string> ReplaceRecoveryLocked(Work work, Action<Stream> copy)
	{
		work.Copy = copy;
		work.CheckSource = null;
		work.WaitingSource = false;
		work.RecoveryPersisted = false;
		work.Recovery.State = "Pending";
		work.Completion = NewCompletion();
		_recoveryRestart = _writing;
		StartWriterLocked();
		return work.Completion.Task;
	}

	private void CopyReplacement(string id, Stream replacement, Stream output)
	{
		Work work;
		lock (_shelves)
		{
			work = _queue.Single((Work w) => w.Recovery != null && w.Recovery.Id == id);
		}
		if (work.Prefix != null)
		{
			ValidateOwnedPath(work.Prefix);
			using FileStream fileStream = new FileStream(work.Prefix, FileMode.Open, FileAccess.Read, FileShare.Read);
			byte[] array = new byte[81920];
			byte[] array2 = new byte[81920];
			int num;
			while ((num = fileStream.Read(array, 0, array.Length)) > 0)
			{
				int num2;
				for (int i = 0; i < num; i += num2)
				{
					num2 = replacement.Read(array2, i, num - i);
					if (num2 == 0)
					{
						throw new InvalidDataException("Replacement is shorter than retained prefix.");
					}
				}
				for (int j = 0; j < num; j++)
				{
					if (array2[j] != array[j])
					{
						throw new InvalidDataException("Replacement differs from retained copy prefix.");
					}
				}
				output.Write(array2, 0, num);
			}
		}
		replacement.CopyTo(output);
	}

	public void RetryOwnedCopy(long revision, Stream replacement)
	{
		if (replacement == null)
		{
			throw new ArgumentNullException("replacement");
		}
		lock (_shelves)
		{
			Work work = _queue.FirstOrDefault((Work w) => w.Receipt.Revision == revision && w.Recovery != null);
			if (work == null)
			{
				throw new InvalidOperationException("No failed copy with that revision.");
			}
			Work work2 = FindRecoveryLocked(work.Recovery.Id);
			string id = work2.Recovery.Id;
			ReplaceRecoveryLocked(work2, delegate(Stream output)
			{
				CopyReplacement(id, replacement, output);
			});
		}
	}

	public CacheLease BeginCacheOperation()
	{
		lock (_cacheLock)
		{
			if (_paused || _disposed || !Operations.AdmissionOpen)
			{
				return null;
			}
			OperationReceipt operationReceipt = _cacheGate.TryAccept();
			if (operationReceipt == null)
			{
				return null;
			}
			CacheLease cacheLease = new CacheLease();
			cacheLease.Receipt = operationReceipt;
			CacheLease cacheLease2 = cacheLease;
			_cache.Add(cacheLease2);
			return cacheLease2;
		}
	}

	public void FinishCacheOperation(CacheLease lease, bool success)
	{
		if (lease == null)
		{
			return;
		}
		lock (_cacheLock)
		{
			if (!_cache.Remove(lease))
			{
				return;
			}
			lease.Active = false;
			if (success)
			{
				lease.Receipt.CompleteDurably();
			}
			else
			{
				lease.Receipt.Fail();
			}
		}
		lease.Settled.TrySetResult(success);
	}

	public void RefreshStatus()
	{
		RefreshStatus(unprobedOnly: false);
	}

	private void RefreshStatus(bool unprobedOnly)
	{
		if (!_statusEnabled)
		{
			return;
		}
		lock (_cacheLock)
		{
			if (_paused || _disposed || !Operations.AdmissionOpen)
			{
				return;
			}
			if (unprobedOnly)
			{
				lock (_shelves)
				{
					if (_unprobedIds.Count == 0)
					{
						return;
					}
				}
			}
			if (_probeRunning)
			{
				_probeAgain = true;
				_probeAgainFull |= !unprobedOnly;
				return;
			}
			_probeRunning = true;
		}
		CacheLease lease = BeginCacheOperation();
		if (lease == null)
		{
			lock (_cacheLock)
			{
				_probeRunning = false;
				return;
			}
		}
		Task.Run(delegate
		{
			bool success = false;
			bool flag = false;
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				KeyValuePair<string, ItemSlot>[] source2;
				lock (_shelves)
				{
					IEnumerable<KeyValuePair<string, ItemSlot>> source = (unprobedOnly ? _unprobedIds.Select((string id) => new KeyValuePair<string, ItemSlot>(id, _itemIndex[id])) : _itemIndex);
					source2 = source.Select((KeyValuePair<string, ItemSlot> p) => new KeyValuePair<string, ItemSlot>(p.Key, new ItemSlot
					{
						ShelfId = p.Value.ShelfId,
						File = p.Value.File.Clone(),
						Original = p.Value,
						NeedsProbe = _unprobedIds.Contains(p.Key)
					})).ToArray();
				}
				foreach (KeyValuePair<string, ItemSlot> item in source2.OrderBy((KeyValuePair<string, ItemSlot> p) => p.Value.File.IsNetworkPath ? 1 : 0))
				{
					ParkedFile file = item.Value.File;
					if (flag && (stopwatch.ElapsedMilliseconds >= 150 || file.IsNetworkPath))
					{
						PostChanged();
						flag = false;
						stopwatch.Restart();
					}
					if (!lease.IsActive)
					{
						return;
					}
					bool flag2 = _probe(file);
					if (flag2 || item.Value.NeedsProbe)
					{
						lock (_cacheLock)
						{
							if (!lease.IsActive || _paused)
							{
								return;
							}
							lock (_shelves)
							{
								if (_itemIndex.TryGetValue(file.Id, out var value) && object.ReferenceEquals(value, item.Value.Original) && value.File.FullPath == file.FullPath)
								{
									if (file.IsProbed)
									{
										_unprobedIds.Remove(file.Id);
									}
									if (flag2)
									{
										value.File.ApplyProbeStatus(file);
										file.ViewOrder = value.File.ViewOrder;
										_viewPending[file.Id] = new ViewItem
										{
											ShelfId = value.ShelfId,
											File = file
										};
										flag = true;
									}
								}
							}
						}
					}
				}
				success = true;
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Shelf status probe failed: " + ex.GetType().Name);
			}
			finally
			{
				if (flag && lease.IsActive)
				{
					PostChanged();
				}
				FinishCacheOperation(lease, success);
				bool probeAgain;
				bool probeAgainFull;
				lock (_cacheLock)
				{
					_probeRunning = false;
					probeAgain = _probeAgain;
					probeAgainFull = _probeAgainFull;
					_probeAgain = (_probeAgainFull = false);
				}
				if (probeAgain && !_paused && !_disposed)
				{
					RefreshStatus(!probeAgainFull);
				}
			}
		});
	}

	private void PostChanged()
	{
		if (_paused || _disposed || Interlocked.Exchange(ref _notificationPending, 1) != 0)
		{
			return;
		}
		try
		{
			_dispatcher.BeginInvoke((Action)delegate
			{
				Interlocked.Exchange(ref _notificationPending, 0);
				if (!_paused && !_disposed)
				{
					ViewChange e = CaptureView(reset: false);
					EventHandler<ViewChange> viewChanged = this.ViewChanged;
					try
					{
						viewChanged?.Invoke(this, e);
					}
					catch (Exception ex2)
					{
						TransactionLogger.Warn("Shelf view observer failed: " + ex2.Message);
					}
					EventHandler filesChanged = this.FilesChanged;
					EventHandler shelvesChanged = this.ShelvesChanged;
					try
					{
						filesChanged?.Invoke(this, EventArgs.Empty);
						shelvesChanged?.Invoke(this, EventArgs.Empty);
					}
					catch (Exception ex3)
					{
						TransactionLogger.Warn("Shelf observer failed: " + ex3.Message);
					}
				}
			}, DispatcherPriority.Background);
		}
		catch (InvalidOperationException ex)
		{
			Interlocked.Exchange(ref _notificationPending, 0);
			TransactionLogger.Warn("Shelf dispatcher stopped: " + ex.Message);
		}
	}

	private void NotifyChanged()
	{
	}

	private void NotifyShelvesChanged()
	{
	}

	public void Flush()
	{
		Task task = VerifyAsync(CancellationToken.None);
		if (!task.Wait(250))
		{
			throw new TimeoutException("Shelf writes pending.");
		}
	}

	private void SaveShelves()
	{
	}

	private string SerializeSnapshot()
	{
		ShelfStorageDto shelfStorageDto2;
		lock (_shelves)
		{
			ShelfStorageDto shelfStorageDto = new ShelfStorageDto();
			shelfStorageDto.Copies = _recoveries.Select((ShelfCopyRecovery r) => r.Clone()).ToList();
			shelfStorageDto.Version = 2;
			shelfStorageDto.Sort = _sort.ToString();
			shelfStorageDto.ActiveIndex = _activeShelfIndex;
			shelfStorageDto.ActiveId = _shelves[_activeShelfIndex].Id;
			shelfStorageDto.Shelves = _shelves.Select((ShelfTab s) => new ShelfTabDto
			{
				Id = s.Id,
				Name = s.Name,
				Items = s.Files.Select((ParkedFile f) => new ShelfItemDto
				{
					Id = f.Id,
					Path = f.FullPath,
					Added = f.AddedUtc.Ticks,
					Owned = f.IsOwned
				}).ToList()
			}).ToList();
			shelfStorageDto2 = shelfStorageDto;
		}
		foreach (ShelfItemDto item in shelfStorageDto2.Shelves.SelectMany((ShelfTabDto s) => s.Items))
		{
			if (item.Owned)
			{
				ValidateOwnedPath(item.Path);
				if (!File.Exists(item.Path))
				{
					throw new IOException("Owned shelf file missing.");
				}
			}
		}
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		return javaScriptSerializer.Serialize(shelfStorageDto2);
	}

	private static ShelfStorageDto Parse(string text)
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		ShelfStorageDto shelfStorageDto = javaScriptSerializer.Deserialize<ShelfStorageDto>(text);
		if (shelfStorageDto == null || shelfStorageDto.Shelves == null || shelfStorageDto.Shelves.Count == 0 || shelfStorageDto.Version > 2)
		{
			throw new InvalidDataException("Invalid shelf metadata.");
		}
		if (shelfStorageDto.Shelves.Any((ShelfTabDto s) => s == null || (s.Items == null && s.Paths == null)))
		{
			throw new InvalidDataException("Invalid shelf items.");
		}
		return shelfStorageDto;
	}

	private static bool ExistsStrict(string path)
	{
		try
		{
			FileAttributes attributes = File.GetAttributes(path);
			if ((attributes & FileAttributes.Directory) != 0)
			{
				throw new InvalidDataException("Shelf metadata is a directory.");
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

	private void LoadShelves()
	{
		try
		{
			bool recovery = false;
			ShelfStorageDto shelfStorageDto = PersistenceHelper.ReadWithBackup(_shelvesPath, Parse, delegate
			{
				recovery = true;
			});
			if (shelfStorageDto != null)
			{
				if (ExistsStrict(_shelvesPath + ".bak"))
				{
					Parse(PersistenceHelper.ReadShared(_shelvesPath + ".bak"));
				}
				LoadFromDto(shelfStorageDto);
				LoadRecovery(shelfStorageDto);
				if (!recovery && shelfStorageDto.Version >= 2)
				{
					return;
				}
				lock (_shelves)
				{
					SubmitLocked(new Work
					{
						Apply = delegate
						{
							if (recovery)
							{
								string[] array = new string[2]
								{
									_shelvesPath,
									_shelvesPath + ".bak"
								};
								foreach (string text in array)
								{
									if (File.Exists(text))
									{
										File.Copy(text, text + ".recovery." + Guid.NewGuid().ToString("N"));
									}
								}
								PersistenceHelper.WriteOwnedBytes(_shelvesPath, Encoding.UTF8.GetBytes(SerializeSnapshot()));
							}
						}
					});
					return;
				}
			}
			if (ExistsStrict(_legacyPath))
			{
				List<string> list = new JavaScriptSerializer().Deserialize<List<string>>(PersistenceHelper.ReadShared(_legacyPath));
				if (list == null)
				{
					throw new InvalidDataException("Invalid legacy shelf.");
				}
				ShelfTab shelfTab = new ShelfTab(null, "Desk");
				AddLoaded(shelfTab, list.Select((string path) => new ShelfItemDto
				{
					Path = path
				}), legacy: true);
				_shelves.Add(shelfTab);
				lock (_shelves)
				{
					SubmitLocked(new Work());
					return;
				}
			}
		}
		catch (Exception loadFailure)
		{
			TransactionLogger.Warn("Shelf metadata retained; recovery required: " + (_loadFailure = loadFailure).GetType().Name);
		}
		lock (_shelves)
		{
			EnsureOneShelf();
		}
	}

	private void LoadRecovery(ShelfStorageDto dto)
	{
		foreach (ShelfCopyRecovery record in dto.Copies ?? new List<ShelfCopyRecovery>())
		{
			if (record == null || string.IsNullOrEmpty(record.Id) || _recoveries.Any((ShelfCopyRecovery r) => r.Id == record.Id) || !new string[5] { "Pending", "NeedsSource", "Cancelled", "Completed", "Superseded" }.Contains(record.State))
			{
				throw new InvalidDataException("Invalid shelf recovery record.");
			}
			ValidateOwnedPath(record.Path);
			string directoryName = Path.GetDirectoryName(record.Path);
			if (!Regex.IsMatch(Path.GetFileName(directoryName), "^[0-9a-f]{32}$") || !string.Equals(ComparisonPath(Path.GetDirectoryName(directoryName)), ComparisonPath(_ownedDir), StringComparison.OrdinalIgnoreCase) || _recoveries.Any((ShelfCopyRecovery r) => string.Equals(ComparisonPath(r.Path), ComparisonPath(record.Path), StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidDataException("Invalid copy recovery destination.");
			}
			if (record.Prefix != null)
			{
				ValidateOwnedPath(record.Prefix);
				if (record.Prefix != record.Path && record.Prefix != record.Path + ".partial" && !record.Prefix.StartsWith(record.Path + ".partial.interrupted.", StringComparison.Ordinal))
				{
					throw new InvalidDataException("Invalid copy evidence destination.");
				}
			}
			if (record.EvidenceFrom != null && record.EvidenceFrom != record.Path && record.EvidenceFrom != record.Path + ".partial")
			{
				throw new InvalidDataException("Invalid evidence relocation source.");
			}
			_recoveries.Add(record);
			if (record.State == "Cancelled" || record.State == "Completed" || record.State == "Superseded")
			{
				continue;
			}
			if (_shelves.Any((ShelfTab t) => t.Files.Any((ParkedFile f) => string.Equals(ComparisonPath(f.FullPath), ComparisonPath(record.Path), StringComparison.OrdinalIgnoreCase))))
			{
				throw new InvalidDataException("Unresolved copy destination is already referenced.");
			}
			record.State = "NeedsSource";
			record.Error = "InterruptedCopy";
			Work work = new Work();
			work.Recovery = record;
			work.Path = record.Path;
			work.Stage = record.Path + ".partial";
			work.Prefix = record.Prefix;
			work.ShelfId = record.ShelfId;
			work.Epoch = Epoch(record.ShelfId ?? string.Empty);
			work.Replaceable = true;
			work.WaitingSource = true;
			work.Failed = true;
			work.RecoveryPersisted = true;
			work.Apply = ApplyOwned;
			work.Completion = NewCompletion();
			Work work2 = work;
			if (work2.Prefix == null)
			{
				if (ExistsStrict(work2.Stage))
				{
					work2.Prefix = work2.Stage;
				}
				else if (ExistsStrict(work2.Path))
				{
					work2.Prefix = work2.Path;
				}
			}
			else if (!ExistsStrict(work2.Prefix) && record.EvidenceFrom != null && ExistsStrict(record.EvidenceFrom))
			{
				work2.Prefix = record.EvidenceFrom;
			}
			record.Prefix = work2.Prefix;
			work2.Receipt = Operations.TryAccept();
			work2.Receipt.Fail();
			_queue.Add(work2);
		}
	}

	private void LoadFromDto(ShelfStorageDto dto)
	{
		if (!string.IsNullOrEmpty(dto.Sort) && Enum.TryParse<ShelfSort>(dto.Sort, ignoreCase: true, out var result))
		{
			_sort = result;
		}
		lock (_shelves)
		{
			_shelves.Clear();
			foreach (ShelfTabDto shelf in dto.Shelves)
			{
				if (shelf == null)
				{
					continue;
				}
				ShelfTab tab = new ShelfTab(shelf.Id, CleanName(shelf.Name) ?? string.Empty);
				if (_shelves.Any((ShelfTab s) => s.Id == tab.Id))
				{
					tab.Id = Guid.NewGuid().ToString("N");
				}
				if (shelf.Items != null)
				{
					AddLoaded(tab, shelf.Items, dto.Version < 2);
				}
				else if (shelf.Paths != null)
				{
					AddLoaded(tab, shelf.Paths.Select((string p) => new ShelfItemDto
					{
						Path = p
					}), legacy: true);
				}
				_shelves.Add(tab);
			}
			int num = (string.IsNullOrEmpty(dto.ActiveId) ? (-1) : _shelves.FindIndex((ShelfTab s) => s.Id == dto.ActiveId));
			_activeShelfIndex = ((num >= 0) ? num : Math.Max(0, Math.Min(dto.ActiveIndex, _shelves.Count - 1)));
			EnsureOneShelf();
		}
	}

	private void AddLoaded(ShelfTab tab, IEnumerable<ShelfItemDto> items, bool legacy = false)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		long ticks = DateTime.UtcNow.Ticks;
		int num = 0;
		foreach (ShelfItemDto it in items)
		{
			string text = ((it == null) ? null : NormalizePath(it.Path));
			if (text == null)
			{
				throw new InvalidDataException("Invalid shelf path.");
			}
			if (hashSet.Add(text))
			{
				ParkedFile parkedFile = new ParkedFile(text);
				if (!string.IsNullOrEmpty(it.Id) && tab.Files.All((ParkedFile x) => x.Id != it.Id))
				{
					parkedFile.Id = it.Id;
				}
				parkedFile.AddedUtc = new DateTime((it.Added > 0) ? it.Added : (ticks - 864000000000L + num), DateTimeKind.Utc);
				if (it.Owned && !IsInside(text, _ownedDir))
				{
					throw new InvalidDataException("Owned shelf entry is outside storage.");
				}
				parkedFile.IsOwned = it.Owned || (legacy && IsInside(text, _ownedDir));
				tab.Files.Add(parkedFile);
				num++;
			}
		}
	}

	private void EnsureOneShelf()
	{
		if (_shelves.Count == 0)
		{
			_shelves.Add(new ShelfTab(null, "Desk"));
		}
		if (_activeShelfIndex < 0 || _activeShelfIndex >= _shelves.Count)
		{
			_activeShelfIndex = 0;
		}
	}

	public static string CleanName(string name)
	{
		if (name == null)
		{
			return null;
		}
		name = Regex.Replace(name, "\\s+", " ").Trim();
		if (name.Length > 40)
		{
			name = name.Substring(0, 40).TrimEnd();
		}
		return name;
	}

	private static string NormalizePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		path = path.Trim().Trim('"');
		if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				path = new Uri(path).LocalPath;
			}
			catch
			{
				return null;
			}
		}
		if (path.Length > 3)
		{
			path = path.TrimEnd('\\', '/');
		}
		if (path.Length != 0)
		{
			return path;
		}
		return null;
	}

	private static string ComparisonPath(string path)
	{
		try
		{
			return Path.GetFullPath(NormalizePath(path));
		}
		catch
		{
			return path;
		}
	}

	private static bool IsInside(string path, string dir)
	{
		try
		{
			return Path.IsPathRooted(path) && Path.GetFullPath(path).StartsWith(Path.GetFullPath(dir).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private void ValidateOwnedPath(string path)
	{
		if (!IsInside(path, _ownedDir))
		{
			throw new IOException("Not a shelf-owned destination.");
		}
		string fullPath = Path.GetFullPath(path);
		if (fullPath.Substring(Path.GetPathRoot(fullPath).Length).Contains(":"))
		{
			throw new IOException("Alternate stream rejected.");
		}
		for (string text = fullPath; text != null; text = Path.GetDirectoryName(text))
		{
			if ((File.Exists(text) || Directory.Exists(text)) && (File.GetAttributes(text) & FileAttributes.ReparsePoint) != 0)
			{
				throw new IOException("Reparse point rejected.");
			}
		}
	}

	private int MaxOwnedNameLength()
	{
		int val = 259 - (_ownedDir.Length + 1 + 32 + 1 + 53);
		return Math.Max(16, Math.Min(120, val));
	}

	private static string SanitizeFileName(string name, int maxLength = 120)
	{
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			name = name.Replace(oldChar, '_');
		}
		name = name.Trim().TrimEnd('.');
		if (name.Length > maxLength)
		{
			string text = Path.GetExtension(name);
			if (text.Length > 16 || text.Length >= maxLength)
			{
				text = string.Empty;
			}
			name = name.Substring(0, maxLength - text.Length).TrimEnd('.', ' ') + text;
		}
		if (name.Length != 0)
		{
			return name;
		}
		return "file";
	}
}
