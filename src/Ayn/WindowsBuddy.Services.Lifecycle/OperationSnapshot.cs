namespace WindowsBuddy.Services.Lifecycle;

public sealed class OperationSnapshot
{
	public readonly long Accepted;

	public readonly long Durable;

	public readonly int Pending;

	public readonly int Failed;

	public readonly int Violations;

	public OperationSnapshot(long accepted, long durable, int pending, int failed, int violations)
	{
		Accepted = accepted;
		Durable = durable;
		Pending = pending;
		Failed = failed;
		Violations = violations;
	}
}
