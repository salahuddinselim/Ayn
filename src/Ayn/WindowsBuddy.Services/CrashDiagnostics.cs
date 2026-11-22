using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;

namespace WindowsBuddy.Services;

public static class CrashDiagnostics
{
	private delegate int NativeExceptionFilter(IntPtr info);

	public sealed class Report
	{
		public string TimeUtc { get; set; }

		public string Version { get; set; }

		public string Build { get; set; }

		public string BuildModule { get; set; }

		public string Windows { get; set; }

		public int Boundary { get; set; }

		public string Tool { get; set; }

		public string[] Types { get; set; }

		public string[] Frames { get; set; }
	}

	public enum PreviousExit
	{
		Clean,
		Quit,
		SessionEnd,
		Update,
		WindowsSessionEnded,
		Unexpected
	}

	public const int MaxLogs = 10;

	public const int MaxBytes = 16384;

	private static readonly object Sync = new object();

	private static readonly string[] Tools = new string[20]
	{
		"none", "home", "clock", "units", "links", "message", "timer", "system", "clipboard", "notes",
		"calendar", "weather", "screentime", "shelf", "files", "analytics", "revenue", "aicoding", "claude", "notch_customization"
	};

	private static bool _uncleanSession;

	private static bool _sessionBegun;

	private static string _previousActivity = "unavailable";

	private static string _activityReason = "running";

	private static long _lastUiActivityTicks = DateTime.UtcNow.Ticks;

	private static long _lastMarkerWrite;

	private static uint _nativeCode;

	private static DispatcherTimer _activityTimer;

	private static readonly NativeExceptionFilter NativeFilter = delegate(IntPtr info)
	{
		try
		{
			_nativeCode = (uint)Marshal.ReadInt32(Marshal.ReadIntPtr(info));
		}
		catch
		{
		}
		NoteExitReason("native-crash");
		return 0;
	};

	private static IntPtr _previousNativeFilter;

	private static int _nativeFilterInstalled;

	private static string _reasonBeforeHang;

	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(5.0);

	internal static Func<DateTime?> ShellStartUtcProvider = delegate
	{
		DateTime? dateTime = StartupService.LogonTimeProvider();
		return (!dateTime.HasValue) ? null : new DateTime?(dateTime.Value.ToUniversalTime());
	};

	private static string Folder => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "logs", "crashes");

	private static string Pending => Path.Combine(Folder, "pending.marker");

	private static string Session => Path.Combine(Folder, "session.marker");

	private static string ActivityPath => Path.Combine(Folder, "activity.marker");

	public static PreviousExit LastPreviousExit { get; private set; }

	internal static string CurrentVersion => typeof(CrashDiagnostics).Assembly.GetName().Version.ToString();

	[DllImport("kernel32.dll")]
	private static extern IntPtr SetUnhandledExceptionFilter(NativeExceptionFilter filter);

	[DllImport("kernel32.dll", EntryPoint = "SetUnhandledExceptionFilter")]
	private static extern IntPtr RestoreUnhandledExceptionFilter(IntPtr previous);

	internal static void ReleaseNativeFilter()
	{
		if (Interlocked.Exchange(ref _nativeFilterInstalled, 0) == 1)
		{
			try
			{
				RestoreUnhandledExceptionFilter(_previousNativeFilter);
			}
			catch
			{
			}
		}
	}

	public static void NoteExitReason(string reason)
	{
		if (!_sessionBegun || !new string[7] { "running", "save-pending", "native-crash", "managed-crash", "hang", "normal-quit-storage-budget", "normal-exit" }.Contains(reason))
		{
			return;
		}
		long ticks = DateTime.UtcNow.Ticks;
		if (reason == _activityReason && ticks - Interlocked.Read(ref _lastMarkerWrite) < HeartbeatInterval.Ticks - TimeSpan.FromSeconds(10.0).Ticks)
		{
			return;
		}
		if (reason == "hang" && _activityReason != "hang")
		{
			_reasonBeforeHang = _activityReason;
		}
		_activityReason = reason;
		Interlocked.Exchange(ref _lastMarkerWrite, ticks);
		try
		{
			Directory.CreateDirectory(Folder);
			using FileStream fileStream = new FileStream(ActivityPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
			byte[] bytes = Encoding.ASCII.GetBytes(reason + "|" + ticks + "|" + Interlocked.Read(ref _lastUiActivityTicks) + "|" + _nativeCode);
			fileStream.Write(bytes, 0, bytes.Length);
			fileStream.Flush(flushToDisk: true);
		}
		catch
		{
		}
	}

	internal static void StorageRecovered()
	{
		if (_activityReason == "save-pending")
		{
			NoteExitReason("running");
		}
	}

	internal static void HangEnded()
	{
		if (_activityReason == "hang")
		{
			NoteExitReason(_reasonBeforeHang ?? "running");
		}
	}

	private static string NumericVersion(string value)
	{
		if (value == null || !Regex.IsMatch(value, "\\A[0-9]{1,10}(\\.[0-9]{1,10}){0,3}\\z"))
		{
			return "unknown";
		}
		return value;
	}

	private static string SafeType(string value)
	{
		if (value == null || value.Length > 160)
		{
			return "System.Exception";
		}
		Assembly[] array = new Assembly[5]
		{
			typeof(Exception).Assembly,
			typeof(Win32Exception).Assembly,
			typeof(Dispatcher).Assembly,
			typeof(Application).Assembly,
			typeof(CrashDiagnostics).Assembly
		};
		foreach (Assembly assembly in array)
		{
			try
			{
				Type type = assembly.GetType(value, throwOnError: false);
				if (type != null && !type.IsGenericType && typeof(Exception).IsAssignableFrom(type))
				{
					return type.FullName;
				}
			}
			catch
			{
			}
		}
		return "System.Exception";
	}

	private static string BuildNumber()
	{
		object[] customAttributes = typeof(CrashDiagnostics).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), inherit: false);
		for (int i = 0; i < customAttributes.Length; i++)
		{
			AssemblyMetadataAttribute assemblyMetadataAttribute = (AssemblyMetadataAttribute)customAttributes[i];
			if (assemblyMetadataAttribute.Key == "BuildNumber")
			{
				return NumericVersion(assemblyMetadataAttribute.Value);
			}
		}
		return "unknown";
	}

	private static int ActiveTool()
	{
		try
		{
			return Math.Max(0, Array.IndexOf(Tools, ToolRouter.Instance.ActiveToolId));
		}
		catch
		{
			return 0;
		}
	}

	private static Report Capture(CrashBoundary boundary, Exception error)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		int num = 0;
		while (error != null && num < 4)
		{
			list.Add(SafeType(error.GetType().FullName));
			StackTrace stackTrace = new StackTrace(error, fNeedFileInfo: false);
			for (int i = 0; i < stackTrace.FrameCount; i++)
			{
				if (list2.Count >= 32)
				{
					break;
				}
				try
				{
					StackFrame frame = stackTrace.GetFrame(i);
					MethodBase method = frame.GetMethod();
					if (!(method == null))
					{
						list2.Add(method.Module.ModuleVersionId.ToString("D") + " / 0x" + method.MetadataToken.ToString("X8", CultureInfo.InvariantCulture) + " / IL " + frame.GetILOffset().ToString(CultureInfo.InvariantCulture));
					}
				}
				catch
				{
				}
			}
			num++;
			error = error.InnerException;
		}
		Report report = new Report();
		report.TimeUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
		report.Version = typeof(CrashDiagnostics).Assembly.GetName().Version.ToString();
		report.Build = BuildNumber();
		report.BuildModule = typeof(CrashDiagnostics).Module.ModuleVersionId.ToString("D");
		report.Windows = Environment.OSVersion.Version.ToString();
		report.Boundary = (int)boundary;
		report.Tool = Tools[ActiveTool()];
		report.Types = list.ToArray();
		report.Frames = list2.ToArray();
		return report;
	}

	public static void Record(CrashBoundary boundary, Exception error, bool terminating)
	{
		if (terminating)
		{
			NoteExitReason("managed-crash");
		}
		try
		{
			lock (Sync)
			{
				Directory.CreateDirectory(Folder);
				if (terminating)
				{
					File.WriteAllText(Pending, "1", Encoding.ASCII);
				}
				string s = new JavaScriptSerializer().Serialize(Capture(boundary, error));
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				if (bytes.Length > 16384)
				{
					return;
				}
				FileInfo[] source = new DirectoryInfo(Folder).GetFiles("crash-*.json").OrderByDescending((FileInfo f) => f.Name, StringComparer.Ordinal).ToArray();
				foreach (FileInfo item in source.Skip(9))
				{
					item.Delete();
				}
				string path = Path.Combine(Folder, "crash-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + ".json");
				using FileStream fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough);
				fileStream.Write(bytes, 0, bytes.Length);
				fileStream.Flush(flushToDisk: true);
			}
		}
		catch
		{
		}
	}

	public static bool HasPendingNotice()
	{
		try
		{
			return File.Exists(Pending);
		}
		catch
		{
			return false;
		}
	}

	public static void AcknowledgeNotice()
	{
		try
		{
			File.Delete(Pending);
		}
		catch
		{
		}
	}

	internal static PreviousExit Classify(string marker, DateTime markerWriteUtc, string currentVersion, DateTime? shellStartUtc)
	{
		if (marker == null)
		{
			return PreviousExit.Clean;
		}
		string[] array = marker.Trim().Split('|');
		if (array[0] == "ending")
		{
			if (array.Length <= 1 || !(array[1] == "quit"))
			{
				return PreviousExit.SessionEnd;
			}
			return PreviousExit.Quit;
		}
		if (array[0] == "running" && array.Length > 1 && array[1] != currentVersion)
		{
			return PreviousExit.Update;
		}
		if (shellStartUtc.HasValue && shellStartUtc.Value > markerWriteUtc)
		{
			return PreviousExit.WindowsSessionEnded;
		}
		return PreviousExit.Unexpected;
	}

	public static void BeginSession(bool restored = false)
	{
		if (_activityTimer != null)
		{
			_activityTimer.Stop();
		}
		_previousActivity = "unavailable";
		try
		{
			Directory.CreateDirectory(Folder);
			string marker = null;
			DateTime markerWriteUtc = DateTime.MinValue;
			try
			{
				if (new FileInfo(ActivityPath).Length > 256)
				{
					throw new InvalidDataException();
				}
				string[] array = PersistenceHelper.ReadShared(ActivityPath).Split('|');
				if ((array.Length == 2 || array.Length == 4) && new string[7] { "running", "save-pending", "native-crash", "managed-crash", "hang", "normal-quit-storage-budget", "normal-exit" }.Contains(array[0]) && long.TryParse(array[1], out var result) && result >= DateTime.MinValue.Ticks && result <= DateTime.MaxValue.Ticks)
				{
					_previousActivity = array[0] + "; marker UTC " + new DateTime(result, DateTimeKind.Utc).ToString("O");
					if (array.Length == 4 && long.TryParse(array[2], out var result2) && result2 >= 0 && result2 <= DateTime.MaxValue.Ticks && uint.TryParse(array[3], out var result3))
					{
						string previousActivity = _previousActivity;
						_previousActivity = previousActivity + "; last UI activity UTC " + new DateTime(result2, DateTimeKind.Utc).ToString("O") + "; native code 0x" + result3.ToString("X8");
					}
				}
			}
			catch
			{
			}
			if (File.Exists(Session))
			{
				marker = PersistenceHelper.ReadShared(Session);
				markerWriteUtc = File.GetLastWriteTimeUtc(Session);
			}
			DateTime? shellStartUtc = null;
			try
			{
				shellStartUtc = ShellStartUtcProvider();
			}
			catch
			{
			}
			LastPreviousExit = Classify(marker, markerWriteUtc, CurrentVersion, shellStartUtc);
			if (restored && LastPreviousExit == PreviousExit.Unexpected)
			{
				LastPreviousExit = PreviousExit.WindowsSessionEnded;
			}
			_uncleanSession = LastPreviousExit == PreviousExit.Unexpected;
			if (_uncleanSession)
			{
				File.WriteAllText(Pending, "1", Encoding.ASCII);
			}
			WriteMarker("running|" + CurrentVersion + "|" + DateTime.UtcNow.Ticks);
			_sessionBegun = true;
			_nativeCode = 0u;
			_activityReason = "running";
			_reasonBeforeHang = null;
			Interlocked.Exchange(ref _lastMarkerWrite, 0L);
			Interlocked.Exchange(ref _lastUiActivityTicks, DateTime.UtcNow.Ticks);
			NoteExitReason("running");
			if (Interlocked.Exchange(ref _nativeFilterInstalled, 1) == 0)
			{
				_previousNativeFilter = SetUnhandledExceptionFilter(NativeFilter);
			}
			DispatcherTimer dispatcherTimer = new DispatcherTimer();
			dispatcherTimer.Interval = HeartbeatInterval;
			_activityTimer = dispatcherTimer;
			_activityTimer.Tick += delegate
			{
				Interlocked.Exchange(ref _lastUiActivityTicks, DateTime.UtcNow.Ticks);
				NoteExitReason(_activityReason);
			};
			_activityTimer.Start();
		}
		catch
		{
		}
	}

	public static void MarkEnding(string reason)
	{
		try
		{
			Directory.CreateDirectory(Folder);
			WriteMarker("ending|" + reason + "|" + DateTime.UtcNow.Ticks);
		}
		catch
		{
		}
	}

	private static void WriteMarker(string text)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(text);
		using FileStream fileStream = new FileStream(Session, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 256, FileOptions.WriteThrough);
		fileStream.Write(bytes, 0, bytes.Length);
		fileStream.Flush(flushToDisk: true);
	}

	public static void EndSession()
	{
		if (_activityTimer != null)
		{
			_activityTimer.Stop();
		}
		NoteExitReason("normal-exit");
		_sessionBegun = false;
		try
		{
			File.Delete(Session);
		}
		catch
		{
		}
	}

	private static string Format(Report report)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("App version: " + NumericVersion(report.Version));
		stringBuilder.AppendLine("Build: " + NumericVersion(report.Build));
		if (Guid.TryParseExact(report.BuildModule, "D", out var result))
		{
			stringBuilder.AppendLine("Build module: " + result.ToString("D"));
		}
		stringBuilder.AppendLine("Windows: " + NumericVersion(report.Windows));
		if (DateTime.TryParseExact(report.TimeUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result2))
		{
			stringBuilder.AppendLine("Time UTC: " + result2.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
		}
		stringBuilder.AppendLine("Boundary: " + (Enum.IsDefined(typeof(CrashBoundary), report.Boundary) ? ((CrashBoundary)report.Boundary).ToString() : "unknown"));
		stringBuilder.AppendLine("Active tool: " + ((Array.IndexOf(Tools, report.Tool) >= 0) ? report.Tool : "none"));
		foreach (string item in (report.Types ?? new string[0]).Take(4))
		{
			stringBuilder.AppendLine("Exception type: " + SafeType(item));
		}
		stringBuilder.AppendLine("Stack trace (module / method token / IL offset; names and paths omitted):");
		foreach (string item2 in (report.Frames ?? new string[0]).Take(32))
		{
			if (item2 != null && Regex.IsMatch(item2, "\\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12} / 0x[0-9A-F]{8} / IL -?[0-9]{1,10}\\z"))
			{
				stringBuilder.AppendLine("  at " + item2);
			}
		}
		return stringBuilder.ToString();
	}

	public static string CopyText()
	{
		StringBuilder stringBuilder = new StringBuilder("Ayn diagnostics\n");
		stringBuilder.AppendLine("App version: " + typeof(CrashDiagnostics).Assembly.GetName().Version);
		stringBuilder.AppendLine("Build: " + BuildNumber());
		stringBuilder.AppendLine("Windows: " + Environment.OSVersion.Version);
		int build = Environment.OSVersion.Version.Build;
		stringBuilder.AppendLine("Windows family: " + ((build >= 22000) ? "Windows 11" : ((build >= 10240) ? "Windows 10" : "other Windows")));
		stringBuilder.AppendLine("Build module: " + typeof(CrashDiagnostics).Module.ModuleVersionId.ToString("D"));
		try
		{
			stringBuilder.AppendLine("Rendering: " + (RenderProfile.LightMode ? "light" : "full") + "; tier " + RenderProfile.Tier + "; software " + RenderProfile.SoftwareRendering);
			stringBuilder.AppendLine("Notch surface: " + (CapsuleWindow.DwmComposed ? "DWM composed" : "layered"));
			stringBuilder.AppendLine("Remote session: " + SystemParameters.IsRemoteSession + "; primary display: " + SystemParameters.PrimaryScreenWidth.ToString("0", CultureInfo.InvariantCulture) + "x" + SystemParameters.PrimaryScreenHeight.ToString("0", CultureInfo.InvariantCulture) + " DIP");
		}
		catch
		{
			stringBuilder.AppendLine("Rendering: unavailable");
		}
		if (_uncleanSession)
		{
			stringBuilder.AppendLine("Previous session ended without a normal exit. Native failures or forced termination may have no managed exception report.");
		}
		stringBuilder.AppendLine("Previous exit/activity: " + _previousActivity);
		stringBuilder.AppendLine("Previous session classification: " + LastPreviousExit);
		if (_uncleanSession && (_previousActivity.StartsWith("running;") || _previousActivity.StartsWith("save-pending;")))
		{
			stringBuilder.AppendLine("Exit reason: forced termination or unobserved native failure (no definitive OS exit code available).");
		}
		try
		{
			lock (Sync)
			{
				if (Directory.Exists(Folder))
				{
					foreach (FileInfo item in new DirectoryInfo(Folder).GetFiles("crash-*.json").OrderByDescending((FileInfo f) => f.Name, StringComparer.Ordinal).Take(10))
					{
						try
						{
							if (item.Length <= 16384)
							{
								JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
								javaScriptSerializer.MaxJsonLength = 16384;
								javaScriptSerializer.RecursionLimit = 8;
								JavaScriptSerializer javaScriptSerializer2 = javaScriptSerializer;
								Report report = javaScriptSerializer2.Deserialize<Report>(PersistenceHelper.ReadShared(item.FullName));
								if (report != null)
								{
									stringBuilder.AppendLine("Recent exception:");
									stringBuilder.Append(Format(report));
									return stringBuilder.ToString();
								}
							}
						}
						catch
						{
						}
					}
				}
			}
		}
		catch
		{
		}
		stringBuilder.AppendLine("Recent exception: none available.");
		return stringBuilder.ToString();
	}
}
