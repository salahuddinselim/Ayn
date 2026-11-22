namespace WindowsBuddy.Services;

public class CursorApplyResult
{
	public bool Success { get; set; }

	public string PackId { get; set; }

	public string PackName { get; set; }

	public string SnapshotPath { get; set; }

	public int StatesApplied { get; set; }

	public string ErrorMessage { get; set; }
}
