using System;
using System.Diagnostics;
using System.Windows;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.UI;

public static class CrashNotice
{
	public const string SupportAddress = "contact@modistudio.online";

	private const int MailBodyLimit = 1500;

	internal static Func<string, bool> OpenMailForQa;

	public static string SupportUri => "mailto:contact@modistudio.online";

	public static bool CopyDiagnostics()
	{
		try
		{
			Clipboard.SetText(CrashDiagnostics.CopyText());
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static string MailUri(string diagnostics, bool fullReportCopied = false)
	{
		string text = diagnostics ?? "";
		if (text.Length > 1500)
		{
			text = text.Substring(0, 1500) + (fullReportCopied ? "\n(The full report is on your clipboard.)" : "\n(Report shortened for email. Use About > Copy diagnostics for the full report.)");
		}
		return SupportUri + "?subject=" + Uri.EscapeDataString("Ayn diagnostics") + "&body=" + Uri.EscapeDataString(text);
	}

	public static string Send(string report)
	{
		bool flag = false;
		try
		{
			Clipboard.SetText(report ?? "");
			flag = true;
		}
		catch
		{
		}
		string text = MailUri(report, flag);
		bool flag2 = false;
		try
		{
			if (OpenMailForQa != null)
			{
				flag2 = OpenMailForQa(text);
			}
			else
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo(text);
				processStartInfo.UseShellExecute = true;
				Process.Start(processStartInfo);
				flag2 = true;
			}
		}
		catch
		{
		}
		if (flag2 || flag)
		{
			CrashDiagnostics.AcknowledgeNotice();
		}
		TransactionLogger.Info(flag2 ? "Diagnostics: email app opened with the reviewed report." : (flag ? "Diagnostics: report copied; the email app did not open." : "Diagnostics: send failed."));
		if (!flag2)
		{
			if (!flag)
			{
				return "Try again";
			}
			return "Copied — email contact@modistudio.online";
		}
		return "Email opened";
	}
}
