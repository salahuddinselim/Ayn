using System.Text.RegularExpressions;

namespace WindowsBuddy.Core;

public static class LogScrubber
{
	private static readonly Regex Url = new Regex("\\b[a-zA-Z][a-zA-Z0-9+.\\-]*://[^\\s'\"<>]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex WinPath = new Regex("(?:\\b[a-zA-Z]:\\\\|\\\\\\\\)[^\\s'\"<>|]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex Quoted = new Regex("'[^'\\r\\n]{1,300}'|\"[^\"\\r\\n]{1,300}\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex Email = new Regex("\\b[\\w.%+\\-]+@[\\w.\\-]+\\.[a-zA-Z]{2,}\\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex FileName = new Regex("\\b[\\w\\-.]{1,120}\\.(?:png|jpe?g|gif|bmp|webp|heic|tiff?|ico|svg|txt|md|rtf|pdf|docx?|xlsx?|pptx?|csv|mp3|m4a|flac|wav|mp4|mkv|mov|avi|zip|rar|7z|json|xml|cs|js|ts|py|exe|dll|lnk|url|html?)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static string Scrub(string line)
	{
		if (string.IsNullOrEmpty(line))
		{
			return line;
		}
		try
		{
			string input = Url.Replace(line, "<url>");
			input = WinPath.Replace(input, "<path>");
			input = Email.Replace(input, "<email>");
			input = Quoted.Replace(input, (Match m) => m.Value[0] + "…" + m.Value[0]);
			return FileName.Replace(input, "<file>");
		}
		catch
		{
			return "<unloggable line>";
		}
	}
}
