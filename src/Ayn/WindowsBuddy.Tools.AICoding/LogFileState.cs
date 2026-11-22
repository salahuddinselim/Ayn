namespace WindowsBuddy.Tools.AICoding;

public class LogFileState
{
	public long Offset { get; set; }

	public long Length { get; set; }

	public long WriteTicks { get; set; }

	public string CxSession { get; set; }

	public string CxModel { get; set; }

	public string CxCwd { get; set; }

	public long CxTs { get; set; }

	public long CxIn { get; set; }

	public long CxCached { get; set; }

	public long CxOut { get; set; }
}
