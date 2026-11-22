namespace WindowsBuddy.Tools.FileShelf;

public sealed class ShelfCopyRecovery
{
	public string Id { get; set; }

	public long OriginalRevision { get; set; }

	public string ShelfId { get; set; }

	public string Name { get; set; }

	public string Path { get; set; }

	public string Prefix { get; set; }

	public string EvidenceFrom { get; set; }

	public string State { get; set; }

	public string Error { get; set; }

	public bool Suppressed { get; set; }

	public ShelfCopyRecovery Clone()
	{
		return (ShelfCopyRecovery)MemberwiseClone();
	}
}
