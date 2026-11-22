using System.Collections.Generic;

namespace WindowsBuddy.Services;

public sealed class PendingSeatReleaseList
{
	public List<PendingSeatRelease> Entries { get; set; }
}
