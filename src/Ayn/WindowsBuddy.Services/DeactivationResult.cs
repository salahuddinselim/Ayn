namespace WindowsBuddy.Services;

public sealed class DeactivationResult
{
	public bool LocalCleared { get; set; }

	public RemoteRelease Remote { get; set; }

	public bool ReleasePending { get; set; }

	public string Message { get; set; }

	public bool Success
	{
		get
		{
			if (LocalCleared)
			{
				return Remote != RemoteRelease.Failed;
			}
			return false;
		}
	}
}
