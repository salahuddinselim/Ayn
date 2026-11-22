using System;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services;

public static class BackupService
{
	private static readonly string BackupDir;

	private static readonly string SnapshotPath;

	static BackupService()
	{
		string localAppData = AppPaths.LocalAppData;
		BackupDir = Path.Combine(localAppData, AppPaths.FolderName, "backups");
		Directory.CreateDirectory(BackupDir);
		SnapshotPath = Path.Combine(BackupDir, "snapshot.json");
	}

	public static bool HasSnapshot()
	{
		return File.Exists(SnapshotPath);
	}

	public static ConfigurationSnapshot CreateSnapshot(string currentThemeId)
	{
		if (File.Exists(SnapshotPath))
		{
			TransactionLogger.Info("Preserving existing Ayn configuration snapshot as baseline.");
			return LoadSnapshot();
		}
		ConfigurationSnapshot configurationSnapshot = new ConfigurationSnapshot();
		configurationSnapshot.OriginalWallpaperPath = WallpaperService.GetCurrentWallpaper();
		configurationSnapshot.OriginalWallpaperStyle = WallpaperService.GetCurrentWallpaperStyle();
		configurationSnapshot.OriginalTileWallpaper = WallpaperService.GetCurrentTileWallpaper();
		configurationSnapshot.OriginalCursorSchemeName = CursorService.GetCurrentSchemeName();
		configurationSnapshot.OriginalCursorSchemeSource = CursorService.GetCurrentSchemeSource();
		configurationSnapshot.OriginalCursorScheme = CursorService.GetCurrentCursorScheme();
		configurationSnapshot.OriginalNativeTaskbarState = TaskbarService.GetNativeTaskbarState();
		configurationSnapshot.AppliedThemeId = currentThemeId;
		configurationSnapshot.CreatedAt = DateTime.Now;
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		string content = javaScriptSerializer.Serialize(configurationSnapshot);
		PersistenceHelper.WriteAllTextOrThrow(SnapshotPath, content);
		TransactionLogger.Info($"Created atomic Ayn configuration snapshot at: {SnapshotPath}");
		return configurationSnapshot;
	}

	public static ConfigurationSnapshot LoadSnapshot()
	{
		if (!File.Exists(SnapshotPath))
		{
			return null;
		}
		try
		{
			string input = PersistenceHelper.ReadShared(SnapshotPath);
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			return javaScriptSerializer.Deserialize<ConfigurationSnapshot>(input);
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Failed to deserialize snapshot: " + ex.Message, ex);
			return null;
		}
	}

	public static bool RestoreSnapshot()
	{
		ConfigurationSnapshot configurationSnapshot = LoadSnapshot();
		if (configurationSnapshot == null)
		{
			TransactionLogger.Warn("No configuration snapshot found to restore.");
			return false;
		}
		TransactionLogger.Info("Initiating restoration of previous configuration...");
		try
		{
			BuddyCapsuleService.StopCapsule();
			if (configurationSnapshot.OriginalNativeTaskbarState > 0)
			{
				TaskbarService.RestoreNativeTaskbarState(configurationSnapshot.OriginalNativeTaskbarState);
			}
			if (!string.IsNullOrEmpty(configurationSnapshot.OriginalWallpaperPath) && File.Exists(configurationSnapshot.OriginalWallpaperPath))
			{
				WallpaperService.RestoreWallpaper(configurationSnapshot.OriginalWallpaperPath, configurationSnapshot.OriginalWallpaperStyle, configurationSnapshot.OriginalTileWallpaper);
			}
			if (configurationSnapshot.OriginalCursorScheme != null && configurationSnapshot.OriginalCursorScheme.Count > 0)
			{
				CursorService.RestoreCursorScheme(configurationSnapshot.OriginalCursorScheme, configurationSnapshot.OriginalCursorSchemeName, configurationSnapshot.OriginalCursorSchemeSource);
			}
			if (!string.IsNullOrEmpty(configurationSnapshot.OriginalWallpaperPath) && File.Exists(configurationSnapshot.OriginalWallpaperPath) && !WallpaperService.VerifyWallpaper(configurationSnapshot.OriginalWallpaperPath))
			{
				TransactionLogger.Warn("Read-back verification: Wallpaper did not match snapshot during restore.");
			}
			string localAppData = AppPaths.LocalAppData;
			string path = Path.Combine(localAppData, AppPaths.FolderName, "config", "active_theme.json");
			if (File.Exists(path))
			{
				try
				{
					File.Delete(path);
				}
				catch
				{
				}
			}
			try
			{
				File.Delete(SnapshotPath);
			}
			catch
			{
			}
			TransactionLogger.Success("Reset completed: User pre-theme configuration successfully restored.");
			return true;
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Error during restore: " + ex.Message, ex);
			Rollback(configurationSnapshot);
			return false;
		}
	}

	public static void Rollback(ConfigurationSnapshot snapshot)
	{
		if (snapshot == null)
		{
			return;
		}
		TransactionLogger.Warn("Executing emergency rollback of partially applied theme...");
		try
		{
			BuddyCapsuleService.StopCapsule();
			if (snapshot.OriginalNativeTaskbarState > 0)
			{
				TaskbarService.RestoreNativeTaskbarState(snapshot.OriginalNativeTaskbarState);
			}
			if (!string.IsNullOrEmpty(snapshot.OriginalWallpaperPath) && File.Exists(snapshot.OriginalWallpaperPath))
			{
				WallpaperService.RestoreWallpaper(snapshot.OriginalWallpaperPath, snapshot.OriginalWallpaperStyle, snapshot.OriginalTileWallpaper);
			}
			if (snapshot.OriginalCursorScheme != null)
			{
				CursorService.RestoreCursorScheme(snapshot.OriginalCursorScheme, snapshot.OriginalCursorSchemeName, snapshot.OriginalCursorSchemeSource);
			}
			TransactionLogger.Info("Emergency rollback completed.");
		}
		catch (Exception ex)
		{
			TransactionLogger.Error("Critical error during rollback: " + ex.Message, ex);
		}
	}
}
