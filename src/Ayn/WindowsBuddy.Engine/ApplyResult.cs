using WindowsBuddy.Services;

namespace WindowsBuddy.Engine;

public class ApplyResult
{
	public bool Success { get; set; }

	public string ThemeId { get; set; }

	public string ThemeName { get; set; }

	public bool WallpaperApplied { get; set; }

	public bool CursorApplied { get; set; }

	public bool TaskbarSurfaceActive { get; set; }

	public bool BuddyCapsuleActive { get; set; }

	public TaskbarCompatibilityResult TaskbarResult { get; set; }

	public string ErrorMessage { get; set; }
}
