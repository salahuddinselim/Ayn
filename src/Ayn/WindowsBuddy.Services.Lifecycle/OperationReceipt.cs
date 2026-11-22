namespace WindowsBuddy.Services.Lifecycle;

public sealed class OperationReceipt
{
	private readonly OperationGate owner;

	public long Revision { get; private set; }

	internal OperationReceipt(OperationGate owner, long revision)
	{
		this.owner = owner;
		Revision = revision;
	}

	public void AssertWriteAllowed()
	{
		owner.AssertWrite(this);
	}

	public void CompleteDurably()
	{
		owner.Finish(this, success: true);
	}

	public void Fail()
	{
		owner.Finish(this, success: false);
	}
}
