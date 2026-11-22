namespace WindowsBuddy.Services;

public class ActivationResult
{
	public bool Success { get; set; }

	public ActivationStatus Status { get; set; }

	public string Message { get; set; }

	public bool PersistencePending { get; set; }
}
