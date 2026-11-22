namespace WindowsBuddy.Services;

public class StoredLicenseRecord
{
	public string LicenseKey { get; set; }

	public string InstanceId { get; set; }

	public long LastValidatedUtcTicks { get; set; }
}
