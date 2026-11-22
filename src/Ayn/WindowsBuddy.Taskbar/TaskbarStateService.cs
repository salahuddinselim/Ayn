using System;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Taskbar;

public static class TaskbarStateService
{
	private static readonly string TaskbarDir;

	private static readonly string SnapshotFile;

	private static readonly string ConfigFile;

	static TaskbarStateService()
	{
		string localAppData = AppPaths.LocalAppData;
		TaskbarDir = Path.Combine(localAppData, AppPaths.FolderName, "taskbar");
		Directory.CreateDirectory(TaskbarDir);
		SnapshotFile = Path.Combine(TaskbarDir, "taskbar_snapshot.json");
		ConfigFile = Path.Combine(TaskbarDir, "active_taskbar_config.json");
	}

	public static bool HasSnapshot()
	{
		return File.Exists(SnapshotFile);
	}

	public static void SaveSnapshot(TaskbarSnapshot snapshot)
	{
		try
		{
			PersistenceHelper.WriteAllTextOrThrow(SnapshotFile, snapshot.ToJson());
			TransactionLogger.Info("Taskbar snapshot saved to: " + SnapshotFile);
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Failed to save taskbar snapshot: " + ex.Message);
		}
	}

	public static TaskbarSnapshot LoadSnapshot()
	{
		try
		{
			if (File.Exists(SnapshotFile))
			{
				string json = File.ReadAllText(SnapshotFile);
				return TaskbarSnapshot.FromJson(json);
			}
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Failed to load taskbar snapshot: " + ex.Message);
		}
		return null;
	}

	public static void ClearSnapshot()
	{
		try
		{
			if (File.Exists(SnapshotFile))
			{
				File.Delete(SnapshotFile);
			}
		}
		catch
		{
		}
	}

	public static void SaveConfiguration(TaskbarConfiguration config)
	{
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			PersistenceHelper.WriteAllTextOrThrow(ConfigFile, javaScriptSerializer.Serialize(config));
		}
		catch (Exception ex)
		{
			TransactionLogger.Warn("Failed to save taskbar config: " + ex.Message);
		}
	}

	public static TaskbarConfiguration LoadConfiguration()
	{
		try
		{
			if (File.Exists(ConfigFile))
			{
				string input = File.ReadAllText(ConfigFile);
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				return javaScriptSerializer.Deserialize<TaskbarConfiguration>(input);
			}
		}
		catch
		{
		}
		return new TaskbarConfiguration();
	}
}
