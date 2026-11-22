using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Tools.Message;

namespace WindowsBuddy.Services;

public static class PersistenceHelper
{
	private sealed class PendingBytes
	{
		public byte[] Content;

		public bool KeepBackup;
	}

	public sealed class SealViolation
	{
		public readonly DateTime Utc;

		public readonly string Path;

		public readonly string Kind;

		public SealViolation(string path, string kind)
		{
			Utc = DateTime.UtcNow;
			Path = path;
			Kind = kind;
		}
	}

	public const int SessionEndBudgetMs = 1800;

	internal const FileShare Shared = FileShare.ReadWrite | FileShare.Delete;

	private static readonly object _fileLock = new object();

	private static readonly Dictionary<string, string> _failedWrites = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> _saveErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, PendingBytes> _failedBytes = new Dictionary<string, PendingBytes>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, byte[]> _failedCacheJson = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

	private static volatile bool _writesSealed;

	[ThreadStatic]
	private static bool _retryingRecorded;

	private static readonly List<SealViolation> _sealViolations = new List<SealViolation>();

	private static int _fencedCacheWrites;

	private static int _cacheWritesInFlight;

	private static readonly Dictionary<string, Action> _exitFlushes = new Dictionary<string, Action>();

	private static readonly Dictionary<string, Action> _exitPreparations = new Dictionary<string, Action>();

	private static readonly int[] ReplaceBackoffMs = new int[5] { 50, 100, 250, 500, 1000 };

	private static Action<int> ReplaceBackoff = delegate(int ms)
	{
		Thread.Sleep(ms);
	};

	internal static Action<string> BeforeWriteForQa;

	private static int _retryRunning;

	private static readonly Timer RetryTimer = new Timer(delegate
	{
		if (Interlocked.Exchange(ref _retryRunning, 1) != 0)
		{
			return;
		}
		try
		{
			bool flag;
			lock (_fileLock)
			{
				flag = _saveErrors.Count != 0 || _failedWrites.Count != 0 || _failedBytes.Count != 0;
			}
			if (flag)
			{
				FlushForExit();
			}
			lock (_fileLock)
			{
				KeyValuePair<string, byte[]>[] array = _failedCacheJson.ToArray();
				for (int i = 0; i < array.Length; i++)
				{
					KeyValuePair<string, byte[]> keyValuePair = array[i];
					WriteCacheBytes(keyValuePair.Key, keyValuePair.Value);
				}
			}
		}
		catch
		{
		}
		finally
		{
			Interlocked.Exchange(ref _retryRunning, 0);
		}
	}, null, 30000, 30000);

	public static bool WritesSealed => _writesSealed;

	public static SealViolation[] SealViolations
	{
		get
		{
			lock (_sealViolations)
			{
				return _sealViolations.ToArray();
			}
		}
	}

	public static int FencedCacheWrites => Volatile.Read(ref _fencedCacheWrites);

	public static int CacheWritesInFlight => Volatile.Read(ref _cacheWritesInFlight);

	public static bool SessionEnding { get; private set; }

	public static event Action<string, string> SaveFailed;

	public static void ClearSaveFailure(string path)
	{
		lock (_fileLock)
		{
			_saveErrors.Remove(path);
			RefreshSaveActivity();
		}
	}

	private static void RefreshSaveActivity()
	{
		if (_saveErrors.Count == 0 && _failedWrites.Count == 0 && _failedBytes.Count == 0 && _failedCacheJson.Count == 0)
		{
			CrashDiagnostics.StorageRecovered();
		}
	}

	public static void ForgetDeletedStore(string path)
	{
		lock (_fileLock)
		{
			_failedBytes.Remove(path);
			_failedWrites.Remove(path);
			_failedCacheJson.Remove(path);
			_saveErrors.Remove(path);
		}
	}

	public static void SealWritesAfterCommit()
	{
		lock (_fileLock)
		{
			_writesSealed = true;
		}
		TransactionLogger.Info("Persistence sealed at shutdown commit.");
	}

	public static bool TrySealForCommit(out string reason)
	{
		lock (_fileLock)
		{
			if (_failedWrites.Count != 0 || _failedBytes.Count != 0 || _saveErrors.Count != 0)
			{
				reason = "Saves failed after verification: " + string.Join(", ", _failedWrites.Keys.Concat(_failedBytes.Keys).Concat(_saveErrors).Select(PathCategory)
					.Distinct()
					.ToArray());
				return false;
			}
			_writesSealed = true;
		}
		reason = null;
		TransactionLogger.Info("Persistence sealed at shutdown commit gate.");
		return true;
	}

	public static void UnsealAfterCancelledCommit()
	{
		lock (_fileLock)
		{
			_writesSealed = false;
		}
		TransactionLogger.Info("Persistence unsealed: shutdown cancelled before commit.");
	}

	private static bool RejectSealedWrite(string path)
	{
		if (!_writesSealed || _retryingRecorded)
		{
			return false;
		}
		RecordSealViolation(path, "write-after-seal");
		return true;
	}

	public static void RecordSealViolation(string path, string kind)
	{
		lock (_sealViolations)
		{
			if (_sealViolations.Count < 1000)
			{
				_sealViolations.Add(new SealViolation(path, kind));
			}
		}
		TransactionLogger.Error("Shutdown invariant violation (" + kind + "): category=" + PathCategory(path));
	}

	public static bool TryBeginCacheWrite()
	{
		Interlocked.Increment(ref _cacheWritesInFlight);
		if (!_writesSealed)
		{
			return true;
		}
		Interlocked.Decrement(ref _cacheWritesInFlight);
		Interlocked.Increment(ref _fencedCacheWrites);
		return false;
	}

	public static void EndCacheWrite()
	{
		Interlocked.Decrement(ref _cacheWritesInFlight);
	}

	public static bool WriteCacheText(string path, string text)
	{
		return WriteCacheBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text ?? string.Empty));
	}

	public static bool WriteCacheBytes(string path, byte[] content)
	{
		lock (_fileLock)
		{
			if (!TryBeginCacheWrite())
			{
				return false;
			}
			string text = path + ".tmp." + Guid.NewGuid().ToString("N");
			bool flag = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
			bool flag2 = false;
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				if (AppPaths.IsSandboxed && BeforeWriteForQa != null)
				{
					BeforeWriteForQa(path);
				}
				using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
				{
					fileStream.Write(content, 0, content.Length);
					fileStream.Flush(flushToDisk: true);
				}
				RetireTemps(path, text);
				if (File.Exists(path))
				{
					ReplaceWithBoundedRetry(text, path, flag ? (path + ".bak") : null, ignoreMetadataErrors: true);
				}
				else
				{
					File.Move(text, path);
				}
				_failedCacheJson.Remove(path);
				flag2 = true;
				RefreshSaveActivity();
				return true;
			}
			catch (Exception ex)
			{
				if (flag)
				{
					_failedCacheJson[path] = (byte[])content.Clone();
					NotifySaveFailure(path, ex);
				}
				else
				{
					TransactionLogger.Warn("Regenerable cache write failed: category=cache HRESULT=0x" + ex.HResult.ToString("X8"));
				}
				return false;
			}
			finally
			{
				try
				{
					if ((flag2 || !flag) && File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
				EndCacheWrite();
			}
		}
	}

	public static bool WriteOwnedText(string path, string text)
	{
		lock (_fileLock)
		{
			bool flag = SafeWriteAllText(path, text);
			if (!flag)
			{
				_failedWrites.Remove(path);
			}
			return flag;
		}
	}

	public static void WriteOwnedBytes(string path, byte[] content)
	{
		lock (_fileLock)
		{
			try
			{
				WriteBytesOrThrow(path, content, keepBackup: false);
			}
			finally
			{
				_failedBytes.Remove(path);
			}
		}
	}

	public static void RegisterExitPreparation(string name, Action prepare)
	{
		lock (_exitPreparations)
		{
			_exitPreparations[name] = prepare;
		}
	}

	public static void PrepareExit()
	{
		Action[] array;
		lock (_exitPreparations)
		{
			array = _exitPreparations.Values.ToArray();
		}
		Action[] array2 = array;
		foreach (Action action in array2)
		{
			action();
		}
	}

	public static void RegisterExitFlush(string name, Action flush)
	{
		lock (_exitFlushes)
		{
			_exitFlushes[name] = flush;
		}
	}

	public static bool FlushForExit()
	{
		Action[] array;
		lock (_exitFlushes)
		{
			array = _exitFlushes.Values.ToArray();
		}
		bool flag = true;
		Action[] array2 = array;
		foreach (Action action in array2)
		{
			try
			{
				action();
			}
			catch (TimeoutException ex)
			{
				flag = false;
				TransactionLogger.Warn("Save still pending at flush: " + ex.Message);
			}
			catch (Exception error)
			{
				flag = false;
				ReportSaveFailure("settings", error);
			}
		}
		bool result = RetryFailedWrites();
		if (flag)
		{
			return result;
		}
		return false;
	}

	public static void AttachUserNotifications(Application app)
	{
		int notified = 0;
		Action<string, string> handler = delegate
		{
			if (!SessionEnding && Interlocked.Exchange(ref notified, 1) == 0)
			{
				app.Dispatcher.BeginInvoke((Action)delegate
				{
					MessageService.Instance.ShowNotice("Couldn't save some data — retrying", TimeSpan.FromSeconds(5.0));
				});
			}
		};
		SaveFailed += handler;
		app.SessionEnding += delegate(object s, SessionEndingCancelEventArgs e)
		{
			e.Cancel = false;
			OnSessionEnding(e.ReasonSessionEnding.ToString());
		};
		app.Exit += delegate
		{
			SaveFailed -= handler;
		};
	}

	public static void OnSessionEnding(string reason)
	{
		SessionEnding = true;
		PackageUpdateLifecycle.ArmExitDeadline(reason);
		CrashDiagnostics.MarkEnding("session");
		CrashGuard.PrepareForSessionEnd();
		QuitWatchdog.Trace("Session ending (" + reason + "): marked as a normal exit; saving");
		CapsuleWindow instance = CapsuleWindow.Instance;
		QuitWatchdog.Trace((instance == null || instance.SaveForSessionEnd(1800)) ? "Session ending: saved; letting Windows continue" : ("Session ending: save did not finish in " + 1800 + " ms; letting Windows continue"));
		try
		{
			LogWriter.Flush(200);
		}
		catch
		{
		}
	}

	public static void ReportSaveFailure(string path, Exception error)
	{
		if (path != "settings")
		{
			lock (_fileLock)
			{
				if (_writesSealed && !_retryingRecorded)
				{
					RecordSealViolation(path, "failure-after-seal Win32=" + (error.HResult & 0xFFFF));
					return;
				}
				_saveErrors.Add(path);
			}
		}
		NotifySaveFailure(path, error);
	}

	private static void NotifySaveFailure(string path, Exception error)
	{
		string text = ((error is Win32Exception { NativeErrorCode: var nativeErrorCode }) ? nativeErrorCode.ToString() : (((error.HResult & -65536) == -2147024896) ? (error.HResult & 0xFFFF).ToString() : "unavailable"));
		TransactionLogger.Error("Save failed: category=" + PathCategory(path) + " Win32=" + text + " HRESULT=0x" + error.HResult.ToString("X8"));
		CrashDiagnostics.NoteExitReason("save-pending");
		Action<string, string> handler = PersistenceHelper.SaveFailed;
		if (handler == null)
		{
			return;
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			Delegate[] invocationList = handler.GetInvocationList();
			for (int i = 0; i < invocationList.Length; i++)
			{
				Action<string, string> action = (Action<string, string>)invocationList[i];
				try
				{
					action(path, error.Message);
				}
				catch (Exception ex2)
				{
					TransactionLogger.Warn("Save failure notification: " + ex2.Message);
				}
			}
		});
	}

	public static bool RetryFailedWrites()
	{
		lock (_fileLock)
		{
			_retryingRecorded = true;
			try
			{
				KeyValuePair<string, string>[] array = _failedWrites.ToArray();
				for (int i = 0; i < array.Length; i++)
				{
					KeyValuePair<string, string> keyValuePair = array[i];
					SafeWriteAllText(keyValuePair.Key, keyValuePair.Value);
				}
				KeyValuePair<string, PendingBytes>[] array2 = _failedBytes.ToArray();
				for (int j = 0; j < array2.Length; j++)
				{
					KeyValuePair<string, PendingBytes> keyValuePair2 = array2[j];
					try
					{
						WriteBytesOrThrow(keyValuePair2.Key, keyValuePair2.Value.Content, keyValuePair2.Value.KeepBackup);
					}
					catch
					{
					}
				}
			}
			finally
			{
				_retryingRecorded = false;
			}
			return _failedWrites.Count == 0 && _failedBytes.Count == 0 && _saveErrors.Count == 0;
		}
	}

	public static void WriteAllTextOrThrow(string path, string content, bool keepBackup = true)
	{
		if (!SafeWriteAllText(path, content, keepBackup))
		{
			throw new IOException("Could not save " + path + ". Your existing file has been preserved.");
		}
	}

	public static T ReadWithBackup<T>(string path, Func<string, T> parse, Action ownerRecovery = null) where T : class
	{
		lock (_fileLock)
		{
			bool flag = false;
			foreach (string item in RecoveryTemps(path).Concat(new string[1] { path }).Concat(RecoveryTemps(path + ".bak")).Concat(new string[1] { path + ".bak" }))
			{
				try
				{
					try
					{
						if ((File.GetAttributes(item) & FileAttributes.Directory) != 0)
						{
							flag = true;
							throw new InvalidDataException("Expected a data file.");
						}
					}
					catch (FileNotFoundException)
					{
						goto end_IL_0075;
					}
					catch (DirectoryNotFoundException)
					{
						goto end_IL_0075;
					}
					flag = true;
					string text = ReadShared(item);
					T val = parse(text);
					if (val == null)
					{
						throw new InvalidDataException("Settings file is empty or null.");
					}
					if (item != path)
					{
						if (ownerRecovery != null)
						{
							ownerRecovery();
							return val;
						}
						try
						{
							if (File.Exists(path))
							{
								File.Copy(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
							}
						}
						catch
						{
						}
						SafeWriteAllText(path, text, keepBackup: false);
						TransactionLogger.Info("Recovered data: category=" + PathCategory(path));
					}
					return val;
					end_IL_0075:;
				}
				catch (Exception ex3)
				{
					flag = true;
					TransactionLogger.Warn("Data recovery: category=" + PathCategory(path) + " Win32=" + (ex3.HResult & 0xFFFF));
					if (item == path && IsSharingFailure(ex3))
					{
						throw;
					}
				}
			}
			if (flag)
			{
				throw new InvalidDataException("Neither the settings file nor its backup could be recovered: " + path + ". The files were preserved.");
			}
			return null;
		}
	}

	public static bool SafeWriteAllText(string destinationPath, string content, bool keepBackup = true)
	{
		if (string.IsNullOrEmpty(destinationPath))
		{
			throw new ArgumentException("A destination is required.", "destinationPath");
		}
		lock (_fileLock)
		{
			if (RejectSealedWrite(destinationPath))
			{
				return false;
			}
			string directoryName = Path.GetDirectoryName(destinationPath);
			string text = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");
			string text2 = destinationPath + ".bak";
			try
			{
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				if (AppPaths.IsSandboxed && BeforeWriteForQa != null)
				{
					BeforeWriteForQa(destinationPath);
				}
				using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough))
				{
					using StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
					streamWriter.Write(content ?? string.Empty);
					streamWriter.Flush();
					fileStream.Flush(flushToDisk: true);
				}
				RetireTemps(destinationPath, text);
				if (File.Exists(destinationPath))
				{
					ReplaceWithBoundedRetry(text, destinationPath, keepBackup ? text2 : null, ignoreMetadataErrors: true);
				}
				else
				{
					File.Move(text, destinationPath);
				}
				_failedCacheJson.Remove(destinationPath);
				_failedWrites.Remove(destinationPath);
				_saveErrors.Remove(destinationPath);
				RefreshSaveActivity();
				return true;
			}
			catch (Exception error)
			{
				_failedWrites[destinationPath] = content;
				ReportSaveFailure(destinationPath, error);
				return false;
			}
			finally
			{
				try
				{
					if (!_failedWrites.ContainsKey(destinationPath) && File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
			}
		}
	}

	private static void ReplaceWithBoundedRetry(string source, string destination, string backup, bool ignoreMetadataErrors)
	{
		int num = 0;
		while (true)
		{
			try
			{
				File.Replace(source, destination, backup, ignoreMetadataErrors);
				if (backup != null)
				{
					RetireTemps(backup, null);
				}
				return;
			}
			catch (Exception ex)
			{
				int num2 = ex.HResult & 0xFFFF;
				if (!(ex is IOException) && !(ex is UnauthorizedAccessException))
				{
					throw;
				}
				if (num2 != 5 && num2 != 32 && num2 != 33 && num2 != 1175 && num2 != 1176 && num2 != 1177)
				{
					throw;
				}
				TransactionLogger.Warn("Replace retry: category=" + PathCategory(destination) + " Win32=" + num2);
				if (num >= ReplaceBackoffMs.Length)
				{
					break;
				}
				try
				{
					ClearReadOnly(destination);
					if (backup != null)
					{
						ClearReadOnly(backup);
					}
				}
				catch
				{
				}
				ReplaceBackoff(ReplaceBackoffMs[num]);
				goto IL_00cf;
			}
			IL_00cf:
			num++;
		}
		if (backup != null)
		{
			try
			{
				CopyShared(destination, backup);
			}
			catch
			{
				if (!File.Exists(backup))
				{
					throw;
				}
			}
		}
		if (MoveFileEx(source, destination, 9))
		{
			return;
		}
		TransactionLogger.Warn("Move fallback: category=" + PathCategory(destination) + " Win32=" + Marshal.GetLastWin32Error());
		byte[] array;
		using (FileStream fileStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			using MemoryStream memoryStream = new MemoryStream();
			fileStream.CopyTo(memoryStream);
			array = memoryStream.ToArray();
		}
		using (FileStream fileStream2 = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
		{
			fileStream2.Write(array, 0, array.Length);
			fileStream2.SetLength(array.Length);
			fileStream2.Flush(flushToDisk: true);
		}
		File.Delete(source);
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool MoveFileEx(string source, string destination, int flags);

	private static void ClearReadOnly(string path)
	{
		if (File.Exists(path))
		{
			FileAttributes attributes = File.GetAttributes(path);
			if ((attributes & FileAttributes.ReadOnly) != 0)
			{
				File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
			}
		}
	}

	public static string ReadShared(string path)
	{
		int num = 0;
		while (true)
		{
			try
			{
				lock (_fileLock)
				{
					using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
					using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
					return streamReader.ReadToEnd();
				}
			}
			catch (Exception error)
			{
				if (!IsSharingFailure(error) || num >= ReplaceBackoffMs.Length)
				{
					throw;
				}
				Thread.Sleep(ReplaceBackoffMs[num]);
			}
			num++;
		}
	}

	private static bool IsSharingFailure(Exception error)
	{
		int num = error.HResult & 0xFFFF;
		if (error is IOException || error is UnauthorizedAccessException)
		{
			if (num != 5 && num != 32)
			{
				return num == 33;
			}
			return true;
		}
		return false;
	}

	private static void CopyShared(string source, string destination)
	{
		ClearReadOnly(destination);
		string text = destination + ".tmp." + Guid.NewGuid().ToString("N");
		bool flag = false;
		bool flag2 = false;
		try
		{
			using (FileStream fileStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			{
				using FileStream fileStream2 = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
				fileStream.CopyTo(fileStream2);
				fileStream2.Flush(flushToDisk: true);
			}
			flag = true;
			if (!MoveFileEx(text, destination, 9))
			{
				TransactionLogger.Warn("Backup fallback: category=" + PathCategory(source) + " Win32=" + Marshal.GetLastWin32Error());
				using FileStream fileStream3 = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using FileStream fileStream4 = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
				fileStream3.CopyTo(fileStream4);
				fileStream4.SetLength(fileStream3.Length);
				fileStream4.Flush(flushToDisk: true);
			}
			flag2 = true;
			RetireTemps(destination, text);
		}
		finally
		{
			if (flag2 || !flag)
			{
				try
				{
					File.Delete(text);
				}
				catch
				{
				}
			}
		}
	}

	private static IEnumerable<string> RecoveryTemps(string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!Directory.Exists(directoryName))
		{
			return new string[0];
		}
		return Directory.GetFiles(directoryName, Path.GetFileName(path) + ".tmp.*").OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
	}

	private static void RetireTemps(string path, string current)
	{
		foreach (string item in RecoveryTemps(path))
		{
			if (!string.Equals(item, current, StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					File.Delete(item);
				}
				catch
				{
				}
			}
		}
	}

	private static string PathCategory(string path)
	{
		string text = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
		if (!new string[5] { "history", "settings", "notes", "clipboard", "pet" }.Contains(text))
		{
			return "app-data";
		}
		return text;
	}

	public static void WriteBytesOrThrow(string destination, byte[] content, bool keepBackup = true)
	{
		lock (_fileLock)
		{
			if (RejectSealedWrite(destination))
			{
				throw new IOException("Persistence is sealed after shutdown commit: " + destination);
			}
			string text = destination + ".tmp." + Guid.NewGuid().ToString("N");
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough))
				{
					fileStream.Write(content, 0, content.Length);
					fileStream.Flush(flushToDisk: true);
				}
				RetireTemps(destination, text);
				if (File.Exists(destination))
				{
					ReplaceWithBoundedRetry(text, destination, keepBackup ? (destination + ".bak") : null, ignoreMetadataErrors: false);
				}
				else
				{
					File.Move(text, destination);
				}
				_failedCacheJson.Remove(destination);
				_failedBytes.Remove(destination);
				_saveErrors.Remove(destination);
				RefreshSaveActivity();
			}
			catch (Exception error)
			{
				_failedBytes[destination] = new PendingBytes
				{
					Content = (byte[])content.Clone(),
					KeepBackup = keepBackup
				};
				ReportSaveFailure(destination, error);
				throw;
			}
			finally
			{
				try
				{
					if (!_failedBytes.ContainsKey(destination) && File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
			}
		}
	}

	public static void RunAdmittedWrite(string label, Action write)
	{
		lock (_fileLock)
		{
			if (RejectSealedWrite(label))
			{
				throw new IOException("Persistence is sealed after shutdown commit: " + label);
			}
			write();
		}
	}

	public static void WriteProtectedBytes(string destination, byte[] content)
	{
		RunAdmittedWrite(destination, delegate
		{
			string text = destination + ".tmp." + Guid.NewGuid().ToString("N");
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough))
				{
					fileStream.Write(content, 0, content.Length);
					fileStream.Flush(flushToDisk: true);
				}
				if (File.Exists(destination))
				{
					ReplaceWithBoundedRetry(text, destination, null, ignoreMetadataErrors: false);
				}
				else
				{
					File.Move(text, destination);
				}
			}
			finally
			{
				try
				{
					if (!_failedBytes.ContainsKey(destination) && File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
			}
		});
	}

	public static string SafeReadAllText(string filePath)
	{
		if (string.IsNullOrEmpty(filePath))
		{
			return null;
		}
		if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
		{
			return ReadWithBackup(filePath, delegate(string text)
			{
				object obj2 = new JavaScriptSerializer().DeserializeObject(text);
				if (!(obj2 is Dictionary<string, object>) && !(obj2 is object[]))
				{
					throw new InvalidDataException("Expected a JSON object or array.");
				}
				return text;
			});
		}
		lock (_fileLock)
		{
			if (File.Exists(filePath))
			{
				try
				{
					FileInfo fileInfo = new FileInfo(filePath);
					if (fileInfo.Length > 0)
					{
						return ReadShared(filePath);
					}
					TransactionLogger.Warn("Encountered 0-byte file: " + filePath + ", checking backup.");
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Failed to read " + filePath + ", checking backup: " + ex.Message);
				}
			}
			string path = filePath + ".bak";
			if (File.Exists(path))
			{
				try
				{
					return ReadShared(path);
				}
				catch
				{
				}
			}
			return null;
		}
	}

	public static void SafeWriteJson<T>(string destinationPath, T data)
	{
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			string content = javaScriptSerializer.Serialize(data);
			SafeWriteAllText(destinationPath, content);
		}
		catch (Exception error)
		{
			ReportSaveFailure(destinationPath, error);
		}
	}

	public static T SafeReadJson<T>(string filePath)
	{
		if (string.IsNullOrEmpty(filePath))
		{
			return default(T);
		}
		try
		{
			string text2 = ReadWithBackup(filePath, delegate(string text)
			{
				if (new JavaScriptSerializer().Deserialize<T>(text) == null)
				{
					throw new InvalidDataException("Null data file.");
				}
				return text;
			});
			return (text2 == null) ? default(T) : new JavaScriptSerializer().Deserialize<T>(text2);
		}
		catch
		{
			return default(T);
		}
	}
}
