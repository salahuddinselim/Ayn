namespace WindowsBuddy.Services;

public static class NotchStabilityCounters
{
	public static long RenderCalls;

	public static long OwnershipTransitions;

	public static long SlotAnimationsStarted;

	public static long SlotAnimationsCancelled;

	public static long VisualInstancesCreated;

	public static long VisualInstancesReused;

	public static long TimerUpdates;

	public static long MediaUpdates;

	public static long VolumeUpdates;

	public static long ClockUpdates;

	public static void Reset()
	{
		RenderCalls = 0L;
		OwnershipTransitions = 0L;
		SlotAnimationsStarted = 0L;
		SlotAnimationsCancelled = 0L;
		VisualInstancesCreated = 0L;
		VisualInstancesReused = 0L;
		TimerUpdates = 0L;
		MediaUpdates = 0L;
		VolumeUpdates = 0L;
		ClockUpdates = 0L;
	}

	public static string GetSummary()
	{
		return $"renderCalls={RenderCalls}, ownershipTransitions={OwnershipTransitions}, slotAnimationsStarted={SlotAnimationsStarted}, slotAnimationsCancelled={SlotAnimationsCancelled}, visualInstancesCreated={VisualInstancesCreated}, visualInstancesReused={VisualInstancesReused}, timerUpdates={TimerUpdates}, mediaUpdates={MediaUpdates}, volumeUpdates={VolumeUpdates}, clockUpdates={ClockUpdates}";
	}
}
