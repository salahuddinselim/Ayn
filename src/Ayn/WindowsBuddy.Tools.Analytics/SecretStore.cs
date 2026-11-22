using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Analytics;

public static class SecretStore
{
	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WindowsBuddy.Analytics.Secrets.v2");

	private static string Dir => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "analytics", "secrets");

	private static string FileFor(string key)
	{
		string text = new string((key ?? "").Where(char.IsLetterOrDigit).ToArray());
		return Path.Combine(Dir, ((text.Length == 0) ? "default" : text) + ".bin");
	}

	public static bool Save(string key, string secret)
	{
		try
		{
			Directory.CreateDirectory(Dir);
			byte[] content = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret ?? ""), Entropy, DataProtectionScope.CurrentUser);
			PersistenceHelper.WriteProtectedBytes(FileFor(key), content);
			RemoveResidues(key);
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Analytics: could not store a credential (" + ex.GetType().Name + ")");
			return false;
		}
	}

	public static string Load(string key)
	{
		try
		{
			string path = FileFor(key);
			if (!File.Exists(path))
			{
				return null;
			}
			return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser));
		}
		catch
		{
			return null;
		}
	}

	public static void Delete(string key)
	{
		string f = FileFor(key);
		PersistenceHelper.RunAdmittedWrite(f, delegate
		{
			if (File.Exists(f))
			{
				File.Delete(f);
			}
		});
		RemoveResidues(key);
		PersistenceHelper.ForgetDeletedStore(f);
	}

	private static void RemoveResidues(string key)
	{
		string f = FileFor(key);
		PersistenceHelper.RunAdmittedWrite(f, delegate
		{
			if (File.Exists(f + ".bak"))
			{
				File.Delete(f + ".bak");
			}
			if (Directory.Exists(Dir))
			{
				string[] files = Directory.GetFiles(Dir, Path.GetFileName(f) + ".tmp.*");
				foreach (string path in files)
				{
					File.Delete(path);
				}
			}
		});
	}

	public static string RawPathForQa(string key)
	{
		return FileFor(key);
	}
}
