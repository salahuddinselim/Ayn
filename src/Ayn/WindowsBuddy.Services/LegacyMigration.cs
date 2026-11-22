using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class LegacyMigration
{
	public class Result
	{
		public bool LocalMigrated;

		public bool RoamingMigrated;

		public bool Failed;

		public int FilesCopied;

		public int FilesSkipped;

		public int JsonRewritten;

		public long BytesCopied;

		public string Error;
	}

	public const string MarkerFileName = ".migrated-from-windowsbuddy";

	public static Result Run()
	{
		Result result = new Result();
		try
		{
			result.LocalMigrated = MigrateRoot(AppPaths.LocalAppData, result);
			result.RoamingMigrated = MigrateRoot(AppPaths.RoamingAppData, result);
		}
		catch (Exception ex)
		{
			result.Failed = true;
			result.Error = ex.Message;
		}
		if (result.Failed)
		{
			AppPaths.UseLegacyFolderForThisSession();
			TransactionLogger.Error("Data migration WindowsBuddy -> Ayn failed; using the legacy folder this session and retrying next launch: " + result.Error);
		}
		else if (result.LocalMigrated || result.RoamingMigrated)
		{
			TransactionLogger.Info($"Migrated data from WindowsBuddy to Ayn: {result.FilesCopied} files ({result.BytesCopied:N0} bytes) copied, {result.FilesSkipped} kept, {result.JsonRewritten} JSON files re-pointed. The old folder was left untouched.");
		}
		return result;
	}

	private static bool MigrateRoot(string baseDir, Result r)
	{
		string text = Path.Combine(baseDir, "WindowsBuddy");
		string text2 = Path.Combine(baseDir, "Ayn");
		string text3 = Path.Combine(text2, ".migrated-from-windowsbuddy");
		if (!Directory.Exists(text))
		{
			return false;
		}
		if (File.Exists(text3))
		{
			string text4 = File.ReadAllText(text3);
			if (text4.StartsWith("VAYLI-MIGRATION-COMPLETE-2\n", StringComparison.Ordinal) && text4.EndsWith("\nCOMPLETE\n", StringComparison.Ordinal))
			{
				return false;
			}
			File.Copy(text3, text3 + ".incomplete-" + Guid.NewGuid().ToString("N"));
		}
		Directory.CreateDirectory(text2);
		Dictionary<string, long> dictionary = new Dictionary<string, long>();
		string[] files = Directory.GetFiles(text, "*", SearchOption.AllDirectories);
		foreach (string text5 in files)
		{
			string text6 = text5.Substring(text.Length).TrimStart(Path.DirectorySeparatorChar);
			string text7 = Path.Combine(text2, text6);
			FileInfo fileInfo = new FileInfo(text5);
			FileInfo fileInfo2 = new FileInfo(text7);
			if (fileInfo2.Exists && fileInfo2.LastWriteTimeUtc >= fileInfo.LastWriteTimeUtc)
			{
				r.FilesSkipped++;
				continue;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(text7));
			string text8 = text7 + ".ayn-migrating";
			try
			{
				using (FileStream fileStream = new FileStream(text5, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
				{
					using FileStream fileStream2 = new FileStream(text8, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
					fileStream.CopyTo(fileStream2);
					fileStream2.Flush(flushToDisk: true);
				}
				File.SetLastWriteTimeUtc(text8, fileInfo.LastWriteTimeUtc);
				if (File.Exists(text7))
				{
					File.Replace(text8, text7, text7 + ".pre-migration.bak");
				}
				else
				{
					File.Move(text8, text7);
				}
			}
			finally
			{
				try
				{
					if (File.Exists(text8))
					{
						File.Delete(text8);
					}
				}
				catch
				{
				}
			}
			dictionary[text6] = new FileInfo(text7).Length;
			r.FilesCopied++;
			r.BytesCopied += fileInfo.Length;
		}
		string[] directories = Directory.GetDirectories(text, "*", SearchOption.AllDirectories);
		foreach (string text9 in directories)
		{
			Directory.CreateDirectory(Path.Combine(text2, text9.Substring(text.Length).TrimStart(Path.DirectorySeparatorChar)));
		}
		foreach (KeyValuePair<string, long> item in dictionary)
		{
			FileInfo fileInfo3 = new FileInfo(Path.Combine(text, item.Key));
			FileInfo fileInfo4 = new FileInfo(Path.Combine(text2, item.Key));
			if (!fileInfo4.Exists || !fileInfo3.Exists || fileInfo4.Length != fileInfo3.Length || fileInfo4.Length != item.Value)
			{
				throw new IOException("verification failed for " + item.Key);
			}
			using SHA256 sHA = SHA256.Create();
			using FileStream inputStream = File.OpenRead(fileInfo3.FullName);
			using FileStream inputStream2 = File.OpenRead(fileInfo4.FullName);
			if (!sHA.ComputeHash(inputStream).SequenceEqual(sHA.ComputeHash(inputStream2)))
			{
				throw new IOException("content verification failed for " + item.Key);
			}
		}
		r.JsonRewritten += RewriteLegacyPaths(text2, text);
		PersistenceHelper.WriteAllTextOrThrow(text3, "VAYLI-MIGRATION-COMPLETE-2\n" + text + "\nCOMPLETE\n");
		return true;
	}

	private static int RewriteLegacyPaths(string current, string legacy)
	{
		int num = 0;
		string root = legacy.Replace("\\", "\\\\");
		string replacement = current.Replace("\\", "\\\\");
		string[] files = Directory.GetFiles(current, "*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			if (text.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase))
			{
				byte[] array = File.ReadAllBytes(text);
				bool flag = array.Length >= 3 && array[0] == 239 && array[1] == 187 && array[2] == 191;
				UTF8Encoding uTF8Encoding = new UTF8Encoding(flag);
				string @string = uTF8Encoding.GetString(array, flag ? 3 : 0, array.Length - (flag ? 3 : 0));
				string text2 = ReplaceRoot(ReplaceRoot(@string, root, replacement, "\\\\"), legacy, current, "\\");
				if (!(text2 == @string))
				{
					string text3 = text + ".ayn-migrating";
					File.WriteAllText(text3, text2, uTF8Encoding);
					File.Replace(text3, text, null);
					num++;
				}
			}
		}
		return num;
	}

	private static string ReplaceRoot(string text, string root, string replacement, string sep)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		int num = 0;
		while (true)
		{
			int num2 = text.IndexOf(root, num, StringComparison.OrdinalIgnoreCase);
			if (num2 < 0)
			{
				break;
			}
			int num3 = num2 + root.Length;
			bool flag = num3 == text.Length || string.CompareOrdinal(text, num3, sep, 0, sep.Length) == 0 || text[num3] == '"';
			stringBuilder.Append(text, num, num2 - num).Append(flag ? replacement : text.Substring(num2, root.Length));
			num = num3;
		}
		stringBuilder.Append(text, num, text.Length - num);
		return stringBuilder.ToString();
	}
}
