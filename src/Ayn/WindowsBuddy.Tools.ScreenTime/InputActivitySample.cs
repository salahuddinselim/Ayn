using System;

namespace WindowsBuddy.Tools.ScreenTime;

public sealed class InputActivitySample : EventArgs
{
	public double IdleSeconds { get; private set; }

	public double ElapsedSeconds { get; private set; }

	public InputActivitySample(double idle, double elapsed)
	{
		IdleSeconds = idle;
		ElapsedSeconds = elapsed;
	}
}
