namespace WindowsBuddy.Services;

public class TaskbarCompatibilityResult
{
	public TaskbarCapabilityStatus Status { get; set; }

	public string WindowsBuild { get; set; }

	public string MethodUsed { get; set; }

	public string DiagnosticMessage { get; set; }
}
