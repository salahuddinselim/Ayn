using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Services;

public static class CredentialStore
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct CREDENTIAL
	{
		public uint Flags;

		public uint Type;

		public string TargetName;

		public string Comment;

		public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;

		public uint CredentialBlobSize;

		public IntPtr CredentialBlob;

		public uint Persist;

		public uint AttributeCount;

		public IntPtr Attributes;

		public string TargetAlias;

		public string UserName;
	}

	private sealed class WindowsCredentialVault : ICredentialVault
	{
		public bool Enabled => true;

		public bool TryRead(string target, out byte[] blob)
		{
			blob = null;
			if (!CredRead(target, 1u, 0, out var credentialPtr))
			{
				int lastWin32Error = Marshal.GetLastWin32Error();
				if (lastWin32Error == 1168)
				{
					return false;
				}
				throw new Win32Exception(lastWin32Error);
			}
			try
			{
				CREDENTIAL cREDENTIAL = (CREDENTIAL)Marshal.PtrToStructure(credentialPtr, typeof(CREDENTIAL));
				blob = new byte[cREDENTIAL.CredentialBlobSize];
				if (cREDENTIAL.CredentialBlobSize != 0 && cREDENTIAL.CredentialBlob != IntPtr.Zero)
				{
					Marshal.Copy(cREDENTIAL.CredentialBlob, blob, 0, (int)cREDENTIAL.CredentialBlobSize);
				}
				return true;
			}
			finally
			{
				CredFree(credentialPtr);
			}
		}

		public void Write(string target, byte[] blob)
		{
			IntPtr intPtr = Marshal.AllocHGlobal(blob.Length);
			try
			{
				Marshal.Copy(blob, 0, intPtr, blob.Length);
				CREDENTIAL cREDENTIAL = default(CREDENTIAL);
				cREDENTIAL.Type = 1u;
				cREDENTIAL.TargetName = target;
				cREDENTIAL.UserName = Environment.UserName;
				cREDENTIAL.CredentialBlob = intPtr;
				cREDENTIAL.CredentialBlobSize = (uint)blob.Length;
				cREDENTIAL.Persist = 2u;
				cREDENTIAL.Comment = "Ayn Entitlement Token";
				CREDENTIAL credential = cREDENTIAL;
				if (!CredWrite(ref credential, 0u))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}

		public void Delete(string target)
		{
			if (!CredDelete(target, 1u, 0))
			{
				int lastWin32Error = Marshal.GetLastWin32Error();
				if (lastWin32Error != 1168)
				{
					throw new Win32Exception(lastWin32Error);
				}
			}
		}
	}

	private sealed class DisabledVault : ICredentialVault
	{
		public bool Enabled => false;

		public bool TryRead(string target, out byte[] blob)
		{
			blob = null;
			return false;
		}

		public void Write(string target, byte[] blob)
		{
		}

		public void Delete(string target)
		{
		}
	}

	private sealed class DpapiProtector : ICredentialProtector
	{
		public byte[] Protect(byte[] plain)
		{
			return ProtectedData.Protect(plain, EntropySalt, DataProtectionScope.CurrentUser);
		}

		public byte[] Unprotect(byte[] blob)
		{
			return ProtectedData.Unprotect(blob, EntropySalt, DataProtectionScope.CurrentUser);
		}
	}

	private sealed class Slot
	{
		public bool Present;

		public bool Unreadable;

		public bool Corrupt;

		public bool Tombstone;

		public long Revision;

		public string Payload;

		public string Problem;

		public bool Valid
		{
			get
			{
				if (Present && !Unreadable)
				{
					return !Corrupt;
				}
				return false;
			}
		}
	}

	private sealed class Snapshot
	{
		public Slot Vault;

		public Slot Mirror;

		public Slot Winner;

		public bool Conflict;

		public long MaxRevision;
	}

	private const int CRED_TYPE_GENERIC = 1;

	private const int CRED_PERSIST_CURRENT_USER = 2;

	private const int ERROR_NOT_FOUND = 1168;

	private const string Magic = "\u0001VCR1\n";

	private static readonly byte[] EntropySalt = Encoding.UTF8.GetBytes(
		"Ayn:" + (WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName));


	private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static ICredentialVault _vault;

	private static ICredentialProtector _protector;

	public static readonly ProducerFence Writes = new ProducerFence();

	private static readonly object Sync = new object();

	internal static ICredentialVault Vault
	{
		get
		{
			object obj = _vault;
			if (obj == null)
			{
				if (!AppPaths.IsSandboxed)
				{
					ICredentialVault credentialVault = new WindowsCredentialVault();
					obj = credentialVault;
				}
				else
				{
					obj = new DisabledVault();
				}
				_vault = (ICredentialVault)obj;
			}
			return (ICredentialVault)obj;
		}
		set
		{
			RequireSandbox();
			_vault = value;
		}
	}

	internal static ICredentialProtector Protector
	{
		get
		{
			return _protector ?? (_protector = new DpapiProtector());
		}
		set
		{
			RequireSandbox();
			_protector = value;
		}
	}

	[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)]
	private static extern bool CredWrite([In] ref CREDENTIAL credential, [In] uint flags);

	[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)]
	private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

	[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)]
	private static extern bool CredDelete(string target, uint type, int flags);

	[DllImport("advapi32.dll", SetLastError = true)]
	private static extern void CredFree([In] IntPtr buffer);

	private static void RequireSandbox()
	{
		if (!AppPaths.IsSandboxed)
		{
			throw new InvalidOperationException("Credential adapters can only be replaced in a sandboxed QA profile.");
		}
	}

	public static Action PauseForShutdown()
	{
		Writes.Close();
		return Writes.Reopen;
	}

	public static string MirrorPath(string target)
	{
		string text;
		switch (target)
		{
		default:
			if (!(target == "Ayn.Dev.PendingRelease"))
			{
				text = AppPaths.DataDir;
				break;
			}
			goto case "Ayn_LicenseRecord";
		case "Ayn_LicenseRecord":
		case "Ayn_LicenseRecord.PendingRelease":
		case "Ayn.Dev":
			text = Path.Combine(AppPaths.IsSandboxed ? Path.Combine(AppPaths.LocalAppData, "user-profile") : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Ayn");
			break;
		}
		string path = text;
		return Path.Combine(path, ".auth", target + ".dat");
	}

	internal static void MigrateLicenseMirror(string target)
	{
		if (!Writes.TryEnter(out var _))
		{
			throw new IOException("License migration is waiting for storage.");
		}
		try
		{
			lock (Sync)
			{
				string old = Path.Combine(AppPaths.DataDir, ".auth", target + ".dat");
				string text = MirrorPath(target);
				if (old == text || !File.Exists(old))
				{
					return;
				}
				byte[] array = File.ReadAllBytes(old);
				Slot slot = Decode(array, "legacy mirror");
				if (!slot.Valid)
				{
					throw new IOException("Legacy license mirror cannot be decrypted; it has been preserved.");
				}
				Slot slot2 = (File.Exists(text) ? Decode(File.ReadAllBytes(text), "durable mirror") : new Slot());
				if (!slot2.Valid || slot.Revision > slot2.Revision)
				{
					PersistenceHelper.WriteProtectedBytes(text, array);
				}
				PersistenceHelper.RunAdmittedWrite(old, delegate
				{
					File.Delete(old);
					if (File.Exists(old + ".bak"))
					{
						File.Delete(old + ".bak");
					}
				});
			}
		}
		finally
		{
			Writes.Exit();
		}
	}

	public static bool SaveToken(string targetName, string token)
	{
		return Save(targetName, token).Committed;
	}

	public static string LoadToken(string targetName)
	{
		return Read(targetName).Value;
	}

	public static bool DeleteToken(string targetName)
	{
		return Delete(targetName).Committed;
	}

	private static byte[] Encode(long revision, bool tombstone, string payload)
	{
		return Utf8.GetBytes("\u0001VCR1\n" + revision.ToString(CultureInfo.InvariantCulture) + "\n" + (tombstone ? "T" : "L") + "\n" + (tombstone ? "" : (payload ?? "")));
	}

	private static Slot Decode(byte[] blob, string store)
	{
		Slot slot = new Slot();
		slot.Present = true;
		Slot slot2 = slot;
		if (blob == null || blob.Length == 0)
		{
			slot2.Corrupt = true;
			slot2.Problem = store + " record is empty";
			return slot2;
		}
		string @string;
		try
		{
			@string = Utf8.GetString(Protector.Unprotect(blob));
		}
		catch (Exception ex)
		{
			slot2.Corrupt = true;
			slot2.Problem = store + " record cannot be decrypted (" + Describe(ex) + ")";
			return slot2;
		}
		if (!@string.StartsWith("\u0001VCR1\n", StringComparison.Ordinal))
		{
			slot2.Payload = @string;
			return slot2;
		}
		int num = @string.IndexOf('\n', "\u0001VCR1\n".Length);
		if (num < 0 || @string.Length < num + 3 || !long.TryParse(@string.Substring("\u0001VCR1\n".Length, num - "\u0001VCR1\n".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var result) || (@string[num + 1] != 'L' && @string[num + 1] != 'T') || @string[num + 2] != '\n')
		{
			slot2.Corrupt = true;
			slot2.Problem = store + " record is malformed";
			return slot2;
		}
		slot2.Revision = result;
		slot2.Tombstone = @string[num + 1] == 'T';
		slot2.Payload = @string.Substring(num + 3);
		if (slot2.Tombstone && slot2.Payload.Length != 0)
		{
			slot2.Corrupt = true;
			slot2.Problem = store + " tombstone is malformed";
		}
		return slot2;
	}

	private static Slot ReadVault(string target)
	{
		if (!Vault.Enabled)
		{
			return new Slot();
		}
		byte[] blob;
		try
		{
			if (!Vault.TryRead(target, out blob))
			{
				return new Slot();
			}
		}
		catch (Exception ex)
		{
			Slot slot = new Slot();
			slot.Present = true;
			slot.Unreadable = true;
			slot.Problem = "vault cannot be read (" + Describe(ex) + ")";
			return slot;
		}
		return Decode(blob, "vault");
	}

	private static Slot ReadMirror(string target)
	{
		string path = MirrorPath(target);
		byte[] blob;
		try
		{
			if (!File.Exists(path))
			{
				return new Slot();
			}
			blob = File.ReadAllBytes(path);
		}
		catch (Exception ex)
		{
			Slot slot = new Slot();
			slot.Present = true;
			slot.Unreadable = true;
			slot.Problem = "mirror cannot be read (" + Describe(ex) + ")";
			return slot;
		}
		return Decode(blob, "mirror");
	}

	private static bool Same(Slot a, Slot b)
	{
		if (a.Tombstone == b.Tombstone)
		{
			return string.Equals(a.Payload, b.Payload, StringComparison.Ordinal);
		}
		return false;
	}

	private static Snapshot Inspect(string target)
	{
		Snapshot snapshot = new Snapshot();
		snapshot.Vault = ReadVault(target);
		snapshot.Mirror = ReadMirror(target);
		Snapshot snapshot2 = snapshot;
		bool valid = snapshot2.Vault.Valid;
		bool valid2 = snapshot2.Mirror.Valid;
		if (valid && valid2)
		{
			if (snapshot2.Vault.Revision >= snapshot2.Mirror.Revision)
			{
				snapshot2.Winner = snapshot2.Vault;
				snapshot2.Conflict = snapshot2.Vault.Revision == snapshot2.Mirror.Revision && !Same(snapshot2.Vault, snapshot2.Mirror);
			}
			else
			{
				snapshot2.Winner = snapshot2.Mirror;
			}
		}
		else if (valid)
		{
			snapshot2.Winner = snapshot2.Vault;
		}
		else if (valid2)
		{
			snapshot2.Winner = snapshot2.Mirror;
		}
		snapshot2.MaxRevision = Math.Max(valid ? snapshot2.Vault.Revision : 0, valid2 ? snapshot2.Mirror.Revision : 0);
		return snapshot2;
	}

	private static bool IsCurrent(Snapshot s, Slot store, bool enabled)
	{
		if (!enabled)
		{
			return true;
		}
		if (s.Winner == null)
		{
			return !store.Present;
		}
		if (s.Winner.Tombstone)
		{
			if (store.Present)
			{
				if (store.Valid)
				{
					return store.Tombstone;
				}
				return false;
			}
			return true;
		}
		if (store.Valid && !s.Conflict && store.Revision == s.Winner.Revision)
		{
			return Same(store, s.Winner);
		}
		return false;
	}

	private static bool Pending(Snapshot s)
	{
		if (IsCurrent(s, s.Vault, Vault.Enabled))
		{
			return !IsCurrent(s, s.Mirror, enabled: true);
		}
		return true;
	}

	private static long NextRevision(Snapshot s)
	{
		return Math.Max(s.MaxRevision + 1, DateTime.UtcNow.Ticks);
	}

	private static string Problems(Snapshot s, List<string> notes)
	{
		List<string> list = new List<string>();
		if (s.Vault.Problem != null)
		{
			list.Add(s.Vault.Problem);
		}
		if (s.Mirror.Problem != null)
		{
			list.Add(s.Mirror.Problem);
		}
		if (s.Conflict)
		{
			list.Add("stores disagree at the same revision");
		}
		if (notes != null)
		{
			list.AddRange(notes);
		}
		return string.Join("; ", list.ToArray());
	}

	private static CredentialResult Describe(Snapshot s, List<string> notes, bool committed)
	{
		bool flag = Pending(s);
		CredentialState state = ((s.Winner == null) ? ((s.Vault.Unreadable || s.Mirror.Unreadable) ? CredentialState.Unavailable : ((s.Vault.Corrupt || s.Mirror.Corrupt) ? CredentialState.Corrupt : CredentialState.Absent)) : ((!s.Winner.Tombstone) ? ((!flag) ? CredentialState.Complete : CredentialState.Degraded) : CredentialState.Absent));
		string value = ((s.Winner != null && !s.Winner.Tombstone) ? s.Winner.Payload : null);
		return new CredentialResult(state, committed, flag, (s.Winner != null) ? s.Winner.Revision : 0, Problems(s, notes), value);
	}

	private static string Describe(Exception ex)
	{
		return ex.GetType().Name + " 0x" + ex.HResult.ToString("X8");
	}

	private static bool TryWriteVault(string target, byte[] blob, List<string> notes)
	{
		try
		{
			PersistenceHelper.RunAdmittedWrite("credential-vault:" + target, delegate
			{
				Vault.Write(target, blob);
			});
			return true;
		}
		catch (Exception ex)
		{
			notes.Add("vault write failed (" + Describe(ex) + ")");
			return false;
		}
	}

	private static bool TryWriteMirror(string target, byte[] blob, List<string> notes)
	{
		try
		{
			PersistenceHelper.WriteProtectedBytes(MirrorPath(target), blob);
		}
		catch (Exception ex)
		{
			notes.Add("mirror write failed (" + Describe(ex) + ")");
			return false;
		}
		RemoveResidues(target, includeMirror: false, notes);
		return true;
	}

	private static void RemoveResidues(string target, bool includeMirror, List<string> notes)
	{
		string path = MirrorPath(target);
		try
		{
			PersistenceHelper.RunAdmittedWrite(path, delegate
			{
				string directoryName = Path.GetDirectoryName(path);
				if (Directory.Exists(directoryName))
				{
					if (includeMirror && File.Exists(path))
					{
						File.Delete(path);
					}
					if (File.Exists(path + ".bak"))
					{
						File.Delete(path + ".bak");
					}
					string[] files = Directory.GetFiles(directoryName, Path.GetFileName(path) + ".tmp.*");
					foreach (string path2 in files)
					{
						File.Delete(path2);
					}
					PersistenceHelper.ForgetDeletedStore(path);
				}
			});
		}
		catch (Exception ex)
		{
			notes.Add("mirror residue cleanup failed (" + Describe(ex) + ")");
		}
	}

	private static bool ResiduesExist(string target)
	{
		string text = MirrorPath(target);
		try
		{
			string directoryName = Path.GetDirectoryName(text);
			return Directory.Exists(directoryName) && (File.Exists(text + ".bak") || Directory.GetFiles(directoryName, Path.GetFileName(text) + ".tmp.*").Length > 0);
		}
		catch
		{
			return true;
		}
	}

	private static void Purge(string target, List<string> notes)
	{
		RemoveResidues(target, includeMirror: true, notes);
		if (!Vault.Enabled)
		{
			return;
		}
		try
		{
			PersistenceHelper.RunAdmittedWrite("credential-vault:" + target, delegate
			{
				Vault.Delete(target);
			});
		}
		catch (Exception ex)
		{
			notes.Add("vault tombstone removal failed (" + Describe(ex) + ")");
		}
	}

	private static void Repair(string target, Snapshot s, List<string> notes)
	{
		if (s.Winner == null || !Pending(s))
		{
			if (s.Winner != null && s.Winner.Tombstone)
			{
				Purge(target, notes);
			}
			return;
		}
		bool flag = Vault.Enabled && !IsCurrent(s, s.Vault, enabled: true) && !s.Vault.Unreadable;
		bool flag2 = !IsCurrent(s, s.Mirror, enabled: true) && !s.Mirror.Unreadable;
		long revision = (s.Conflict ? NextRevision(s) : s.Winner.Revision);
		byte[] blob;
		try
		{
			blob = Protector.Protect(Encode(revision, s.Winner.Tombstone, s.Winner.Payload));
		}
		catch (Exception ex)
		{
			notes.Add("reconciliation could not protect the record (" + Describe(ex) + ")");
			return;
		}
		bool flag3 = !flag || TryWriteVault(target, blob, notes);
		bool flag4 = !flag2 || TryWriteMirror(target, blob, notes);
		if (s.Winner.Tombstone && flag3 && flag4 && !s.Vault.Unreadable && !s.Mirror.Unreadable)
		{
			Purge(target, notes);
		}
	}

	private static CredentialResult Refuse(string target, string operation)
	{
		if (PersistenceHelper.WritesSealed)
		{
			PersistenceHelper.RecordSealViolation("credential:" + target, "credential-" + operation + "-after-seal");
			return new CredentialResult(CredentialState.Failed, committed: false, pending: false, 0L, operation + " refused: persistence is sealed after shutdown commit", null);
		}
		return new CredentialResult(CredentialState.Failed, committed: false, pending: false, 0L, operation + " refused: shutdown in progress", null);
	}

	private static void Log(string target, string operation, CredentialResult result)
	{
		string message = "Credential " + operation + " [" + target + "]: " + result;
		if (result.State == CredentialState.Failed)
		{
			TransactionLogger.Error(message);
		}
		else if (result.ReconciliationPending || result.State == CredentialState.Corrupt || result.State == CredentialState.Unavailable)
		{
			TransactionLogger.Warn(message);
		}
		else
		{
			TransactionLogger.Info(message);
		}
	}

	public static CredentialResult Save(string target, string value)
	{
		if (string.IsNullOrEmpty(target))
		{
			return new CredentialResult(CredentialState.Failed, committed: false, pending: false, 0L, "no target", null);
		}
		if (PersistenceHelper.WritesSealed || !Writes.TryEnter(out var _))
		{
			CredentialResult result = Refuse(target, "save");
			Log(target, "save", result);
			return result;
		}
		try
		{
			CredentialResult result2;
			lock (Sync)
			{
				List<string> list = new List<string>();
				Snapshot snapshot = Inspect(target);
				if (snapshot.Winner != null && !snapshot.Winner.Tombstone && string.Equals(snapshot.Winner.Payload, value ?? "", StringComparison.Ordinal))
				{
					Repair(target, snapshot, list);
					Snapshot snapshot2 = Inspect(target);
					result2 = Describe(snapshot2, list, snapshot2.Winner != null && !snapshot2.Winner.Tombstone && string.Equals(snapshot2.Winner.Payload, value ?? "", StringComparison.Ordinal));
				}
				else
				{
					long revision = NextRevision(snapshot);
					byte[] array = null;
					try
					{
						array = Protector.Protect(Encode(revision, tombstone: false, value));
					}
					catch (Exception ex)
					{
						list.Add("record could not be protected (" + Describe(ex) + ")");
					}
					bool flag = array != null && Vault.Enabled && TryWriteVault(target, array, list);
					bool flag2 = array != null && TryWriteMirror(target, array, list);
					bool flag3 = flag2 && (flag || !Vault.Enabled);
					string text = string.Join("; ", list.ToArray());
					result2 = ((flag || flag2) ? new CredentialResult(flag3 ? CredentialState.Complete : CredentialState.Degraded, committed: true, !flag3, revision, text, null) : new CredentialResult(CredentialState.Failed, committed: false, Pending(snapshot), (snapshot.Winner != null) ? snapshot.Winner.Revision : 0, "previous record kept; " + text, null));
				}
			}
			Log(target, "save", result2);
			return result2;
		}
		finally
		{
			Writes.Exit();
		}
	}

	public static CredentialResult Delete(string target)
	{
		if (string.IsNullOrEmpty(target))
		{
			return new CredentialResult(CredentialState.Failed, committed: false, pending: false, 0L, "no target", null);
		}
		if (PersistenceHelper.WritesSealed || !Writes.TryEnter(out var _))
		{
			CredentialResult result = Refuse(target, "delete");
			Log(target, "delete", result);
			return result;
		}
		try
		{
			CredentialResult result2;
			lock (Sync)
			{
				List<string> list = new List<string>();
				Snapshot snapshot = Inspect(target);
				if (!snapshot.Vault.Present && !snapshot.Mirror.Present)
				{
					if (ResiduesExist(target))
					{
						RemoveResidues(target, includeMirror: false, list);
					}
					bool flag = ResiduesExist(target);
					result2 = new CredentialResult(flag ? CredentialState.Degraded : CredentialState.Absent, !flag, flag, 0L, string.Join("; ", list.ToArray()), null);
				}
				else
				{
					long revision = NextRevision(snapshot);
					byte[] array = null;
					try
					{
						array = Protector.Protect(Encode(revision, tombstone: true, null));
					}
					catch (Exception ex)
					{
						list.Add("tombstone could not be protected (" + Describe(ex) + ")");
					}
					bool flag2 = array != null && Vault.Enabled && TryWriteVault(target, array, list);
					bool flag3 = array != null && TryWriteMirror(target, array, list);
					bool flag4 = !Vault.Enabled || flag2 || !snapshot.Vault.Present;
					bool flag5 = flag3 || !snapshot.Mirror.Present;
					if (flag2 || flag3)
					{
						if (flag4 && flag5)
						{
							Purge(target, list);
						}
						else
						{
							RemoveResidues(target, includeMirror: false, list);
						}
						bool flag6 = !flag4 || !flag5;
						result2 = new CredentialResult(flag6 ? CredentialState.Degraded : CredentialState.Absent, committed: true, flag6, revision, flag6 ? ("deleted; the secret remains in the " + (flag4 ? "mirror" : "vault") + " until reconciliation; " + string.Join("; ", list.ToArray())) : string.Join("; ", list.ToArray()), null);
					}
					else
					{
						result2 = new CredentialResult(CredentialState.Failed, committed: false, Pending(snapshot), (snapshot.Winner != null) ? snapshot.Winner.Revision : 0, "record kept; " + string.Join("; ", list.ToArray()), null);
					}
				}
			}
			Log(target, "delete", result2);
			return result2;
		}
		finally
		{
			Writes.Exit();
		}
	}

	public static CredentialResult Read(string target)
	{
		if (string.IsNullOrEmpty(target))
		{
			return new CredentialResult(CredentialState.Absent, committed: false, pending: false, 0L, "no target", null);
		}
		CredentialResult credentialResult;
		lock (Sync)
		{
			List<string> list = new List<string>();
			Snapshot snapshot = Inspect(target);
			if (Pending(snapshot) || (snapshot.Winner != null && snapshot.Winner.Tombstone))
			{
				if (!PersistenceHelper.WritesSealed && Writes.TryEnter(out var _))
				{
					try
					{
						Repair(target, snapshot, list);
						snapshot = Inspect(target);
					}
					finally
					{
						Writes.Exit();
					}
				}
				else
				{
					list.Add("reconciliation deferred: shutdown in progress");
				}
			}
			credentialResult = Describe(snapshot, list, committed: false);
		}
		if ((credentialResult.State != CredentialState.Complete && credentialResult.State != 0) || credentialResult.ReconciliationPending)
		{
			Log(target, "read", credentialResult);
		}
		return credentialResult;
	}

	public static CredentialResult Reconcile(string target)
	{
		if (string.IsNullOrEmpty(target))
		{
			return new CredentialResult(CredentialState.Failed, committed: false, pending: false, 0L, "no target", null);
		}
		if (PersistenceHelper.WritesSealed || !Writes.TryEnter(out var _))
		{
			return Refuse(target, "reconcile");
		}
		try
		{
			CredentialResult result;
			lock (Sync)
			{
				List<string> notes = new List<string>();
				Snapshot s = Inspect(target);
				Repair(target, s, notes);
				s = Inspect(target);
				result = Describe(s, notes, !Pending(s));
			}
			Log(target, "reconcile", result);
			return result;
		}
		finally
		{
			Writes.Exit();
		}
	}
}
