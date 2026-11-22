namespace WindowsBuddy.BuddyCapsule.Core;

public interface INotchSurface
{
	int AnimationBudgetMs { get; }

	void AnimateOpen(int generation, bool fromRest);

	void AnimateClose(int generation);

	void ShowPeek(bool on);

	void ApplyPresence(NotchPresence presence, bool animate);

	bool IsPointerInside();

	bool IsTextInputActive();

	string CheckConsistency(NotchPhase phase, NotchPresence presence);

	void ResetVisuals(NotchPresence presence);

	bool IsContentReady();
}
