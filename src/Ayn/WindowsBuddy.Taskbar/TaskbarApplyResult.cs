namespace WindowsBuddy.Taskbar;

public class TaskbarApplyResult
{
	public bool Success { get; set; }

	public string Message { get; set; }

	public bool RolledBack { get; set; }

	public string ErrorDetails { get; set; }
}
