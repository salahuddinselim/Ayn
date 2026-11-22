namespace WindowsBuddy.Services;

public sealed class SignedAynAnchor
{
	public int Version { get; set; }

	public string DeviceHash { get; set; }

	public string Mg { get; set; }

	public string Nonce { get; set; }

	public long AynStartMs { get; set; }

	public long ServerTimeMs { get; set; }

	public string Signature { get; set; }
}
