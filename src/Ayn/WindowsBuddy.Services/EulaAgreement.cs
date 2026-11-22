using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using WindowsBuddy.Core;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.Services;

public static class EulaAgreement
{
	public const string CurrentVersion = "1.0";

	public const string OnlineUrl = "https://github.com/salahuddinselim/Ayn";

	private static string _acceptedThisSession;

	public static string RequiredVersion => "1.0";

	public static bool IsAccepted => !NeedsAgreement(ReadAcceptedVersion(), RequiredVersion);

	internal static string PreferencesPath => Path.Combine(AppPaths.DataDir, "settings", "app_preferences.json");

	public static bool NeedsAgreement(string acceptedVersion, string required)
	{
		if (!string.IsNullOrEmpty(acceptedVersion))
		{
			return !string.Equals(acceptedVersion.Trim(), required, StringComparison.Ordinal);
		}
		return true;
	}

	public static string ReadAcceptedVersion()
	{
		if (_acceptedThisSession != null)
		{
			return _acceptedThisSession;
		}
		string preferencesPath = PreferencesPath;
		string[] array = new string[2]
		{
			preferencesPath,
			preferencesPath + ".bak"
		};
		foreach (string file in array)
		{
			string text = ReadShared(file);
			if (text == null)
			{
				continue;
			}
			try
			{
				if (!(new JavaScriptSerializer().DeserializeObject(text) is Dictionary<string, object> dictionary))
				{
					continue;
				}
				object value;
				return dictionary.TryGetValue("EulaAcceptedVersion", out value) ? (value as string) : null;
			}
			catch
			{
			}
		}
		return null;
	}

	private static string ReadShared(string file)
	{
		try
		{
			if (!File.Exists(file))
			{
				return null;
			}
			using FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
			return streamReader.ReadToEnd();
		}
		catch
		{
			return null;
		}
	}

	public static bool RecordAcceptance()
	{
		string requiredVersion = RequiredVersion;
		string text = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		string text2 = AppVersion();
		_acceptedThisSession = requiredVersion;
		try
		{
			AppPreferences instance = AppPreferences.Instance;
			instance.RecordEulaAcceptance(requiredVersion, text, text2);
			instance.VerifyAsync(CancellationToken.None).Wait(5000);
			bool flag = string.Equals(ReadFromDisk(), requiredVersion, StringComparison.Ordinal);
			TransactionLogger.Info("End User License Agreement v" + requiredVersion + " accepted (" + text + ", Ayn " + text2 + ")" + (flag ? "." : "; NOT stored, will ask again."));
			if (flag)
			{
				_acceptedThisSession = null;
			}
			return flag;
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("End User License Agreement accepted but not stored (" + ex.GetType().Name + "); will ask again next launch.");
			return false;
		}
	}

	private static string ReadFromDisk()
	{
		string acceptedThisSession = _acceptedThisSession;
		_acceptedThisSession = null;
		try
		{
			return ReadAcceptedVersion();
		}
		finally
		{
			_acceptedThisSession = acceptedThisSession;
		}
	}

	private static string AppVersion()
	{
		try
		{
			return SettingsPages.ShortVersion();
		}
		catch
		{
			return typeof(EulaAgreement).Assembly.GetName().Version.ToString();
		}
	}

	public static string PlainText()
	{
		StringBuilder stringBuilder = new StringBuilder();
		EulaBlock[] blocks = EulaDocument.Blocks;
		foreach (EulaBlock eulaBlock in blocks)
		{
			if (eulaBlock.Kind == EulaBlockKind.LI)
			{
				stringBuilder.Append("• ");
			}
			stringBuilder.AppendLine(eulaBlock.Text);
			if (eulaBlock.Kind != EulaBlockKind.LI)
			{
				stringBuilder.AppendLine();
			}
		}
		return stringBuilder.ToString();
	}
}
