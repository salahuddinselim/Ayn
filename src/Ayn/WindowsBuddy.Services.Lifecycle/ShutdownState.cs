namespace WindowsBuddy.Services.Lifecycle;

public enum ShutdownState
{
	Running,
	Preparing,
	Quiescing,
	Verifying,
	Committed,
	Terminated,
	RecoveryRequired
}
