using System;
using System.IO;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.State;

public static class FirstRunManager
{
	private static readonly object _syncLock = new object();

	private static FirstRunState _currentState = FirstRunState.NotStarted;

	private static bool _initialized = false;

	private static string SettingsDir => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "settings");

	private static string StateFilePath => Path.Combine(SettingsDir, "first_run_state.json");

	private static string LegacyDoneFilePath => Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "firstrun.done");

	public static FirstRunState CurrentState
	{
		get
		{
			EnsureInitialized();
			return _currentState;
		}
	}

	public static bool NeedsFirstRunArrival
	{
		get
		{
			EnsureInitialized();
			return _currentState == FirstRunState.NotStarted;
		}
	}

	private static void EnsureInitialized()
	{
		if (_initialized)
		{
			return;
		}
		lock (_syncLock)
		{
			if (_initialized)
			{
				return;
			}
			try
			{
				if (File.Exists(StateFilePath))
				{
					string text = PersistenceHelper.ReadShared(StateFilePath).Trim();
					if (text.Contains("\"Completed\""))
					{
						_currentState = FirstRunState.Completed;
					}
					else if (text.Contains("\"Dismissed\""))
					{
						_currentState = FirstRunState.Dismissed;
					}
					else if (text.Contains("\"Started\""))
					{
						_currentState = FirstRunState.Dismissed;
						PersistStateSafe(FirstRunState.Dismissed);
					}
					else
					{
						_currentState = FirstRunState.NotStarted;
					}
				}
				else if (File.Exists(LegacyDoneFilePath))
				{
					_currentState = FirstRunState.Completed;
					PersistStateSafe(FirstRunState.Completed);
				}
				else
				{
					_currentState = FirstRunState.NotStarted;
				}
			}
			catch
			{
				_currentState = FirstRunState.Dismissed;
			}
			finally
			{
				_initialized = true;
			}
		}
	}

	public static void MarkStarted()
	{
		lock (_syncLock)
		{
			_currentState = FirstRunState.Started;
			PersistStateSafe(FirstRunState.Started);
		}
	}

	public static void MarkCompleted()
	{
		lock (_syncLock)
		{
			_currentState = FirstRunState.Completed;
			PersistStateSafe(FirstRunState.Completed);
			TouchLegacyDone();
		}
	}

	public static void MarkDismissed()
	{
		lock (_syncLock)
		{
			_currentState = FirstRunState.Dismissed;
			PersistStateSafe(FirstRunState.Dismissed);
			TouchLegacyDone();
		}
	}

	public static void ResetForTesting()
	{
		lock (_syncLock)
		{
			try
			{
				if (File.Exists(StateFilePath))
				{
					File.Delete(StateFilePath);
				}
				if (File.Exists(LegacyDoneFilePath))
				{
					File.Delete(LegacyDoneFilePath);
				}
			}
			catch
			{
			}
			_currentState = FirstRunState.NotStarted;
			_initialized = true;
		}
	}

	private static void TouchLegacyDone()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(LegacyDoneFilePath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllText(LegacyDoneFilePath, DateTime.UtcNow.ToString("o"));
		}
		catch
		{
		}
	}

	private static void PersistStateSafe(FirstRunState state)
	{
		try
		{
			if (!Directory.Exists(SettingsDir))
			{
				Directory.CreateDirectory(SettingsDir);
			}
			string content = $"{{\n  \"state\": \"{state.ToString()}\",\n  \"updatedAt\": \"{DateTime.UtcNow:O}\",\n  \"version\": \"2.0\"\n}}";
			PersistenceHelper.SafeWriteAllText(StateFilePath, content);
		}
		catch (Exception error)
		{
			PersistenceHelper.ReportSaveFailure(StateFilePath, error);
		}
	}
}
