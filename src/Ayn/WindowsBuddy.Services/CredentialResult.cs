namespace WindowsBuddy.Services;

public sealed class CredentialResult
{
	public CredentialState State { get; private set; }

	public bool Committed { get; private set; }

	public bool ReconciliationPending { get; private set; }

	public long Revision { get; private set; }

	public string Detail { get; private set; }

	public string Value { get; private set; }

	internal CredentialResult(CredentialState state, bool committed, bool pending, long revision, string detail, string value)
	{
		State = state;
		Committed = committed;
		ReconciliationPending = pending;
		Revision = revision;
		Detail = detail ?? "";
		Value = value;
	}

	public override string ToString()
	{
		return string.Concat(State, Committed ? " committed" : "", ReconciliationPending ? " pending" : "", " rev=", Revision, (Detail.Length > 0) ? (" (" + Detail + ")") : "");
	}
}
