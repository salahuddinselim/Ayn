namespace WindowsBuddy.Services;

public enum ActivationStatus
{
	Success,
	InvalidKey,
	DeviceLimitReached,
	NetworkError,
	Busy,
	KeyDisabled,
	WrongProduct,
	PersistenceFailed
}
