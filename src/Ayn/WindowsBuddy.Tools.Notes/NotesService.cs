using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Tools.Notes;

public class NotesService : IShutdownParticipant
{
	internal const int MaxUndoHistory = 100;

	private static NotesService _instance;

	private static readonly object InstanceLock = new object();

	public static Action BeforeFlush;

	public static Func<Action> PauseEditor;

	private readonly List<NoteItem> _notes = new List<NoteItem>();

	private readonly string _storagePath;

	private readonly List<NoteItem> _recentlyDeleted = new List<NoteItem>();

	private readonly List<Action> _notifications = new List<Action>();

	private readonly List<OperationReceipt> _queued = new List<OperationReceipt>();

	private readonly List<long> _failed = new List<long>();

	private Task _drain = Task.FromResult(result: true);

	private bool _writing;

	private Exception _loadFailure;

	private Func<string, string, bool> _persist;

	private readonly ManualResetEventSlim _saveNow = new ManualResetEventSlim(initialState: false);

	private Action _resumeEditor;

	public static NotesService Instance
	{
		get
		{
			lock (InstanceLock)
			{
				if (_instance == null)
				{
					NotesService notesService = new NotesService();
					NotesOwnership.Registry.Register(notesService);
					_instance = notesService;
				}
				return _instance;
			}
		}
	}

	public IReadOnlyList<NoteItem> Notes
	{
		get
		{
			NoteItem[] source;
			lock (_notes)
			{
				source = _notes.ToArray();
			}
			return (from n in source
				orderby n.IsPinned descending, n.UpdatedAtTicks descending
				select n.Clone()).ToList();
		}
	}

	public bool HasLoadFailure => _loadFailure != null;

	public string StorageFolder => Path.GetDirectoryName(_storagePath);

	public bool HasDeletedNotes
	{
		get
		{
			lock (_notes)
			{
				return _recentlyDeleted.Count > 0;
			}
		}
	}

	public string Id => "notes";

	public PersistenceClass Classification => PersistenceClass.CriticalUserData;

	public OperationGate Operations { get; private set; }

	public event EventHandler NotesChanged;

	public event EventHandler<string> NoteEdited;

	public NoteItem FindNote(string id)
	{
		NoteItem noteItem;
		lock (_notes)
		{
			noteItem = _notes.FirstOrDefault((NoteItem n) => n.Id == id);
		}
		return noteItem?.Clone();
	}

	public NotesService()
	{
		Operations = new OperationGate();
		string text = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "notes");
		try
		{
			Directory.CreateDirectory(text);
		}
		catch
		{
		}
		_storagePath = Path.Combine(text, "notes.json");
		_persist = PersistenceHelper.WriteOwnedText;
		LoadNotes();
		PersistenceHelper.RegisterExitPreparation("notes editor", delegate
		{
			BeforeFlush?.Invoke();
		});
		PersistenceHelper.RegisterExitFlush("notes", FlushStore);
		if (_notes.Any(IsUntouchedLegacySeed))
		{
			Perform(() => _notes.RemoveAll(IsUntouchedLegacySeed));
		}
	}

	public void ResetToDefaultReferenceNotes()
	{
		Perform(delegate
		{
			ResetToDefaultReferenceNotesCore();
			return 0;
		});
	}

	public NoteItem DuplicateNote(string id)
	{
		return Perform(() => DuplicateNoteCore(id))?.Clone();
	}

	public NoteItem CreateNote(string title = null, string content = null)
	{
		return Perform(() => CreateNoteCore(title, content)).Clone();
	}

	public void UpdateNote(string id, string title, string content)
	{
		UpdateNote(id, title, NoteDocument.FromLegacy(content));
	}

	public void UpdateNote(string id, string title, List<NoteBlock> blocks)
	{
		List<NoteBlock> owned = NoteDocument.Normalize(NoteDocument.Clone(blocks));
		string normalizedTitle = (title ?? string.Empty).Trim();
		string signature = NoteDocument.Signature(owned);
		Perform(delegate
		{
			UpdateNoteCore(id, normalizedTitle, owned);
			return 0;
		}, delegate
		{
			NoteItem noteItem = _notes.FirstOrDefault((NoteItem n) => n.Id == id);
			return noteItem != null && (noteItem.Title != normalizedTitle || NoteDocument.Signature(noteItem.Blocks) != signature);
		});
	}

	public bool TogglePin(string id)
	{
		return Perform(() => TogglePinCore(id));
	}

	public void DeleteNote(string id)
	{
		Perform(delegate
		{
			DeleteNoteCore(id);
			return 0;
		});
	}

	public NoteItem RemoveForUndo(string id)
	{
		return Perform(() => RemoveForUndoCore(id))?.Clone();
	}

	public void Discard(string id)
	{
		Perform(delegate
		{
			DiscardCore(id);
			return 0;
		});
	}

	public void ClearAllNotes()
	{
		Perform(delegate
		{
			ClearAllNotesCore();
			return 0;
		});
	}

	public void Reinsert(NoteItem note)
	{
		NoteItem owned = note?.Clone();
		Perform(delegate
		{
			ReinsertCore(owned);
			return 0;
		});
	}

	public NoteItem UndoDelete()
	{
		return Perform(() => UndoDeleteCore())?.Clone();
	}

	private void ResetToDefaultReferenceNotesCore()
	{
		lock (_notes)
		{
			_notes.Clear();
		}
		NotifyChanged();
	}

	private static bool IsUntouchedLegacySeed(NoteItem note)
	{
		if (note.Title != "Ideas for Side Projects" || !note.IsPinned || note.CreatedAtTicks <= 0 || note.UpdatedAtTicks >= note.CreatedAtTicks || Math.Abs(note.CreatedAtTicks - note.UpdatedAtTicks - TimeSpan.FromMinutes(5.0).Ticks) > TimeSpan.FromSeconds(1.0).Ticks)
		{
			return false;
		}
		NoteItem noteItem = new NoteItem();
		noteItem.SetBlocks(new List<NoteBlock>
		{
			Block(NoteBlockKind.Paragraph, R("Build a "), R("focused desktop app suite", bold: true), R(" that feels calm and delightful.")),
			Block(NoteBlockKind.Paragraph, R("Start with a clipboard manager, then timers, notes, and a few system utilities. Keep the design "), new NoteRun("minimal and consistent")
			{
				Color = "blue"
			}, R(".")),
			Block(NoteBlockKind.Checked, R("Design detailed UI for all tabs")),
			Block(NoteBlockKind.Check, R("Implement clipboard history with search")),
			Block(NoteBlockKind.Check, R("Plan pricing and licensing strategy"))
		});
		JavaScriptSerializer javaScriptSerializer = NewSerializer();
		if (note.Content == noteItem.Content)
		{
			return javaScriptSerializer.Serialize(note.Blocks) == javaScriptSerializer.Serialize(noteItem.Blocks);
		}
		return false;
	}

	private static NoteRun R(string text, bool bold = false)
	{
		NoteRun noteRun = new NoteRun(text);
		noteRun.Bold = bold;
		return noteRun;
	}

	private static NoteBlock Block(NoteBlockKind kind, params NoteRun[] runs)
	{
		NoteBlock noteBlock = new NoteBlock();
		noteBlock.Kind = kind;
		NoteBlock noteBlock2 = noteBlock;
		noteBlock2.Runs.AddRange(runs);
		return noteBlock2;
	}

	public static string ExplicitTitle(NoteItem n)
	{
		string text = ((n != null) ? (n.Title ?? string.Empty).Trim() : string.Empty);
		if (!string.Equals(text, "Untitled", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "Untitled Note", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		return string.Empty;
	}

	public static string DisplayTitle(NoteItem n)
	{
		string text = ExplicitTitle(n);
		if (text.Length > 0)
		{
			return text;
		}
		if (n == null)
		{
			return null;
		}
		return NoteDocument.FirstLine(n.Plain, 0);
	}

	public static string Preview(NoteItem n)
	{
		return Preview(ExplicitTitle(n).Length > 0, n.Plain, n.ChecklistDone, n.ChecklistTotal);
	}

	public static string Preview(bool hasTitle, string plain, int done, int total)
	{
		if (total > 0)
		{
			return string.Format(CultureInfo.CurrentCulture, "{0}/{1} done", done, total);
		}
		return NoteDocument.FirstLine(plain, (!hasTitle) ? 1 : 0) ?? string.Empty;
	}

	public static bool HasChecklist(NoteItem n)
	{
		return n.ChecklistTotal > 0;
	}

	public static bool IsEmpty(NoteItem n)
	{
		if (n != null && ExplicitTitle(n).Length == 0 && string.IsNullOrWhiteSpace(n.Plain))
		{
			return n.ChecklistTotal == 0;
		}
		return false;
	}

	public static DateTime EditedLocal(NoteItem n)
	{
		return new DateTime(Math.Max(1L, n.UpdatedAtTicks), DateTimeKind.Utc).ToLocalTime();
	}

	private NoteItem DuplicateNoteCore(string id)
	{
		NoteItem noteItem;
		lock (_notes)
		{
			noteItem = _notes.FirstOrDefault((NoteItem n) => n.Id == id);
		}
		if (noteItem == null)
		{
			return null;
		}
		string text = ExplicitTitle(noteItem);
		NoteItem noteItem2 = new NoteItem();
		noteItem2.Title = ((text.Length > 0) ? (text + " (Copy)") : string.Empty);
		NoteItem noteItem3 = noteItem2;
		noteItem3.SetBlocks(NoteDocument.Clone(noteItem.Blocks));
		return Insert(noteItem3);
	}

	private NoteItem CreateNoteCore(string title, string content)
	{
		return Insert(new NoteItem(title, content));
	}

	private NoteItem Insert(NoteItem note)
	{
		lock (_notes)
		{
			_notes.Insert(0, note);
		}
		NotifyChanged();
		return note;
	}

	private void UpdateNoteCore(string id, string title, string content)
	{
		UpdateNoteCore(id, title, NoteDocument.FromLegacy(content));
	}

	private void UpdateNoteCore(string id, string title, List<NoteBlock> blocks)
	{
		bool flag = false;
		blocks = NoteDocument.Normalize(blocks ?? new List<NoteBlock>());
		lock (_notes)
		{
			NoteItem noteItem = _notes.FirstOrDefault((NoteItem n) => n.Id == id);
			if (noteItem != null)
			{
				title = (title ?? string.Empty).Trim();
				if (noteItem.Title != title || NoteDocument.Signature(noteItem.Blocks) != NoteDocument.Signature(blocks))
				{
					noteItem = noteItem.Clone();
					noteItem.Title = title;
					noteItem.SetBlocks(blocks);
					noteItem.UpdatedAtTicks = DateTime.UtcNow.Ticks;
					noteItem.UpdatedAtFormatted = DateTime.Now.ToString("MMM d, h:mm tt", CultureInfo.CurrentCulture);
					_notes[_notes.FindIndex((NoteItem n) => n.Id == id)] = noteItem;
					flag = true;
				}
			}
		}
		if (flag)
		{
			_notifications.Add(delegate
			{
				this.NoteEdited?.Invoke(this, id);
			});
		}
	}

	private bool TogglePinCore(string id)
	{
		bool result = false;
		lock (_notes)
		{
			NoteItem noteItem = _notes.FirstOrDefault((NoteItem n) => n.Id == id);
			if (noteItem != null)
			{
				noteItem = noteItem.Clone();
				_notes[_notes.FindIndex((NoteItem n) => n.Id == id)] = noteItem;
				noteItem.IsPinned = !noteItem.IsPinned;
				result = noteItem.IsPinned;
			}
		}
		NotifyChanged();
		return result;
	}

	private void DeleteNoteCore(string id)
	{
		NoteItem noteItem = RemoveForUndoCore(id);
		if (noteItem == null)
		{
			return;
		}
		lock (_notes)
		{
			_recentlyDeleted.Add(noteItem);
			if (_recentlyDeleted.Count > 100)
			{
				_recentlyDeleted.RemoveRange(0, _recentlyDeleted.Count - 100);
			}
		}
	}

	private NoteItem RemoveForUndoCore(string id)
	{
		NoteItem noteItem;
		lock (_notes)
		{
			noteItem = _notes.FirstOrDefault((NoteItem n) => n.Id == id);
			if (noteItem != null)
			{
				_notes.Remove(noteItem);
			}
		}
		if (noteItem == null)
		{
			return null;
		}
		NotifyChanged();
		return noteItem;
	}

	private void DiscardCore(string id)
	{
		RemoveForUndoCore(id);
	}

	private void ClearAllNotesCore()
	{
		lock (_notes)
		{
			_notes.Clear();
			_recentlyDeleted.Clear();
		}
		NotifyChanged();
	}

	private void ReinsertCore(NoteItem note)
	{
		if (note == null)
		{
			return;
		}
		lock (_notes)
		{
			if (_notes.Any((NoteItem n) => n.Id == note.Id))
			{
				return;
			}
			_notes.Add(note);
			_recentlyDeleted.RemoveAll((NoteItem n) => n.Id == note.Id);
		}
		NotifyChanged();
	}

	private NoteItem UndoDeleteCore()
	{
		NoteItem noteItem = null;
		lock (_notes)
		{
			if (_recentlyDeleted.Count > 0)
			{
				noteItem = _recentlyDeleted[_recentlyDeleted.Count - 1];
				_recentlyDeleted.RemoveAt(_recentlyDeleted.Count - 1);
			}
		}
		if (noteItem != null)
		{
			ReinsertCore(noteItem);
		}
		return noteItem;
	}

	public IEnumerable<NoteItem> Search(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return Notes;
		}
		string q = query.Trim();
		return Notes.Where((NoteItem n) => Matches(n, q)).ToList();
	}

	public static bool Matches(NoteItem n, string q)
	{
		if (string.IsNullOrEmpty(q))
		{
			return true;
		}
		if ((n.Title ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		return (n.Plain ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void NotifyChanged()
	{
		_notifications.Add(delegate
		{
			this.NotesChanged?.Invoke(this, EventArgs.Empty);
		});
	}

	private T Perform<T>(Func<T> mutation, Func<bool> changesState = null)
	{
		T result;
		Action[] array;
		lock (_notes)
		{
			if (_loadFailure != null)
			{
				throw new InvalidDataException("Notes store requires recovery before edits.", _loadFailure);
			}
			if (!Operations.AdmissionOpen)
			{
				throw new InvalidOperationException("Notes are paused or sealed; edit was not accepted.");
			}
			if (changesState != null && !changesState())
			{
				return default(T);
			}
			OperationReceipt operationReceipt = Operations.TryAccept();
			if (operationReceipt == null)
			{
				throw new InvalidOperationException("Notes are paused or sealed; edit was not accepted.");
			}
			try
			{
				result = mutation();
			}
			catch
			{
				operationReceipt.Fail();
				_failed.Add(operationReceipt.Revision);
				_notifications.Clear();
				throw;
			}
			_queued.Add(operationReceipt);
			array = _notifications.ToArray();
			_notifications.Clear();
			StartWriter();
		}
		Action[] array2 = array;
		foreach (Action action in array2)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Notes listener: " + ex.Message);
			}
		}
		return result;
	}

	private void StartWriter()
	{
		if (_writing || (_queued.Count == 0 && _failed.Count == 0))
		{
			return;
		}
		_writing = true;
		try
		{
			_drain = Task.Run(delegate
			{
				WriteLoop();
			});
		}
		catch
		{
			_writing = false;
			foreach (OperationReceipt item in _queued)
			{
				item.Fail();
				_failed.Add(item.Revision);
			}
			_queued.Clear();
			throw;
		}
	}

	private void WriteLoop()
	{
		_saveNow.Wait(500);
		_saveNow.Reset();
		while (true)
		{
			List<OperationReceipt> list;
			NoteItem[] obj;
			lock (_notes)
			{
				list = new List<OperationReceipt>(_queued);
				_queued.Clear();
				foreach (long item in _failed)
				{
					list.Add(Operations.Retry(item));
				}
				_failed.Clear();
				if (list.Count == 0)
				{
					_writing = false;
					break;
				}
				obj = _notes.ToArray();
			}
			try
			{
				string arg = NewSerializer().Serialize(obj);
				foreach (OperationReceipt item2 in list)
				{
					item2.AssertWriteAllowed();
				}
				if (!_persist(_storagePath, arg))
				{
					throw new IOException("Notes write failed; accepted edits remain in memory.");
				}
				foreach (OperationReceipt item3 in list)
				{
					item3.CompleteDurably();
				}
			}
			catch (Exception error)
			{
				lock (_notes)
				{
					foreach (OperationReceipt item4 in list)
					{
						item4.Fail();
						_failed.Add(item4.Revision);
					}
				}
				PersistenceHelper.ReportSaveFailure(_storagePath, error);
				lock (_notes)
				{
					_writing = false;
					if (_queued.Count > 0)
					{
						StartWriter();
					}
					break;
				}
			}
		}
	}

	public void Flush()
	{
		if (Operations.AdmissionOpen)
		{
			BeforeFlush?.Invoke();
		}
		FlushStore();
	}

	private void FlushStore()
	{
		_saveNow.Set();
		Task drain;
		lock (_notes)
		{
			StartWriter();
			drain = _drain;
		}
		if (!drain.Wait(2000))
		{
			throw new TimeoutException("Notes persistence is pending.");
		}
		AssertDurable();
	}

	private void AssertDurable()
	{
		OperationSnapshot operationSnapshot = Operations.Snapshot();
		if (_loadFailure != null || operationSnapshot.Pending != 0 || operationSnapshot.Failed != 0 || operationSnapshot.Violations != 0 || operationSnapshot.Accepted != operationSnapshot.Durable)
		{
			throw new IOException("Notes have not reached their durable revision.");
		}
	}

	public Task PrepareAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		BeforeFlush?.Invoke();
		return Task.FromResult(result: true);
	}

	public Action PauseForShutdown()
	{
		Operations.CloseAdmission();
		Func<Action> pauseEditor = PauseEditor;
		if (pauseEditor != null)
		{
			_resumeEditor = pauseEditor();
		}
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

	public Task QuiesceAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		PauseForShutdown();
		return Task.FromResult(result: true);
	}

	public async Task VerifyAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		_saveNow.Set();
		Task pending;
		lock (_notes)
		{
			StartWriter();
			pending = _drain;
		}
		await pending;
		token.ThrowIfCancellationRequested();
		AssertDurable();
	}

	public Task ResumeAsync()
	{
		Action resumeEditor = _resumeEditor;
		_resumeEditor = null;
		resumeEditor?.Invoke();
		return Task.FromResult(result: true);
	}

	public Task DisposeAsync()
	{
		AssertDurable();
		return Task.FromResult(result: true);
	}

	private static JavaScriptSerializer NewSerializer()
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		JavaScriptSerializer javaScriptSerializer2 = javaScriptSerializer;
		javaScriptSerializer2.RegisterConverters(new JavaScriptConverter[1]
		{
			new NoteDocumentConverter()
		});
		return javaScriptSerializer2;
	}

	private void LoadNotes()
	{
		try
		{
			List<NoteItem> list = PersistenceHelper.ReadWithBackup(_storagePath, (string json) => NewSerializer().Deserialize<List<NoteItem>>(json));
			if (list == null)
			{
				return;
			}
			lock (_notes)
			{
				_notes.Clear();
				foreach (NoteItem item in list)
				{
					if (item != null && !string.IsNullOrEmpty(item.Id))
					{
						if (item.Blocks == null || (item.Blocks.Count == 0 && !string.IsNullOrEmpty(item.Content)))
						{
							item.Blocks = NoteDocument.FromLegacy(item.Content);
						}
						item.Project();
						item.Title = ExplicitTitle(item);
						if (!IsEmpty(item))
						{
							_notes.Add(item);
						}
					}
				}
			}
		}
		catch (Exception loadFailure)
		{
			TransactionLogger.Warn("Could not load notes: " + (_loadFailure = loadFailure).Message);
		}
	}
}
