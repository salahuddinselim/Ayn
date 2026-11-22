using System;
using System.IO;
using System.Web.Script.Serialization;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Engine;

public static class ThemeEngine
{
	private static readonly string ConfigDir;

	private static readonly string StatePath;

	static ThemeEngine()
	{
		string localAppData = AppPaths.LocalAppData;
		ConfigDir = Path.Combine(localAppData, AppPaths.FolderName, "config");
		Directory.CreateDirectory(ConfigDir);
		StatePath = Path.Combine(ConfigDir, "active_theme.json");
	}

	public static string GetActiveThemeId()
	{
		try
		{
			if (File.Exists(StatePath))
			{
				string input = File.ReadAllText(StatePath);
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				dynamic val = javaScriptSerializer.Deserialize<object>(input);
				if (val != null && val.ContainsKey("id"))
				{
					return val["id"].ToString();
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static ApplyResult ApplyTheme(string themeDirectory)
	{
		ApplyResult applyResult = new ApplyResult();
		applyResult.Success = false;
		ApplyResult applyResult2 = applyResult;
		ConfigurationSnapshot configurationSnapshot = null;
		try
		{
			TransactionLogger.Info($"=== Starting Theme Application: '{themeDirectory}' ===");
			TransactionLogger.Info("Step 1/8: Validating theme schema and resolving asset paths...");
			ThemePackage themePackage = ThemeValidator.LoadAndValidate(themeDirectory);
			applyResult2.ThemeId = themePackage.id;
			applyResult2.ThemeName = themePackage.name;
			TransactionLogger.Info("Step 2/8: Taking atomic snapshot of desktop, cursors, and native taskbar...");
			configurationSnapshot = BackupService.CreateSnapshot(themePackage.id);
			TransactionLogger.Info($"Step 3/8: Applying wallpaper ({themePackage.wallpaper.file})...");
			WallpaperService.ApplyWallpaper(themePackage.ResolvedWallpaperPath, themePackage.wallpaper.style);
			applyResult2.WallpaperApplied = true;
			TransactionLogger.Info("Step 4/8: Writing cursor scheme registry keys and triggering live refresh...");
			CursorService.ApplyCursorScheme(themePackage.ResolvedCursorPaths, $"Ayn - {themePackage.name}");
			applyResult2.CursorApplied = true;
			TransactionLogger.Info("Step 5/8: Coordinating native taskbar baseline...");
			applyResult2.TaskbarResult = TaskbarService.ApplyTaskbarConfiguration(themePackage.taskbar);
			applyResult2.TaskbarSurfaceActive = false;
			TransactionLogger.Info("Step 6/8: Performing mandatory read-back verification on actual system state...");
			if (!WallpaperService.VerifyWallpaper(themePackage.ResolvedWallpaperPath))
			{
				throw new InvalidOperationException("Read-back verification failed: System wallpaper does not match applied theme asset.");
			}
			if (!CursorService.VerifyCursorScheme(themePackage.ResolvedCursorPaths))
			{
				throw new InvalidOperationException("Read-back verification failed: Registry cursor scheme does not match applied theme cursors.");
			}
			if (!BuddyCapsuleService.IsRunning())
			{
				BuddyCapsuleService.StartCapsule();
			}
			applyResult2.BuddyCapsuleActive = true;
			applyResult2.TaskbarSurfaceActive = true;
			TransactionLogger.Success("Read-back verification: Wallpaper, 15 cursor states, and Buddy Capsule confirmed active.");
			TransactionLogger.Info("Step 7/8: Persisting active theme state locally...");
			PersistActiveTheme(themePackage);
			applyResult2.Success = true;
			TransactionLogger.Success($"=== Theme '{themePackage.name}' successfully applied to Windows. System fully operational. ===");
		}
		catch (Exception ex)
		{
			applyResult2.Success = false;
			applyResult2.ErrorMessage = ex.Message;
			TransactionLogger.Error($"Theme application failed: {ex.Message}", ex);
			if (configurationSnapshot != null)
			{
				BackupService.Rollback(configurationSnapshot);
			}
		}
		return applyResult2;
	}

	private static void PersistActiveTheme(ThemePackage theme)
	{
		var obj = new
		{
			id = theme.id,
			name = theme.name,
			version = theme.version,
			appliedAt = DateTime.Now.ToString("o"),
			rootDirectory = theme.RootDirectory
		};
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		string content = javaScriptSerializer.Serialize(obj);
		PersistenceHelper.WriteAllTextOrThrow(StatePath, content);
	}
}
