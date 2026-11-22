namespace WindowsBuddy.Services;

public sealed class PendingSeatRelease
{
	public string LicenseKey { get; set; }

	public string InstanceId { get; set; }

	public int Attempts { get; set; }

	public long LastAttemptUtcTicks { get; set; }

	public int LastStatusCode { get; set; }

	public bool NeedsAttention { get; set; }
}
