namespace WindowsBuddy.Services;

public enum CrashBoundary
{
	AppDomain,
	Dispatcher,
	UnobservedTask,
	ExceptionStorm,
	Startup
}
