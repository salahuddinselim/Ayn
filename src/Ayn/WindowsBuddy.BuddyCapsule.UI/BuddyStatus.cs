using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.BuddyCapsule.UI;

public class BuddyStatus : Border
{
	public BuddyStatus(BuddyStatusState state = BuddyStatusState.Normal, double size = 6.0)
	{
		base.Width = size;
		base.Height = size;
		base.CornerRadius = new CornerRadius(size / 2.0);
		UpdateState(state);
	}

	public void UpdateState(BuddyStatusState state)
	{
		switch (state)
		{
		case BuddyStatusState.Charging:
			base.Background = BuddyTokens.SuccessGreenBrush;
			break;
		case BuddyStatusState.Warning:
			base.Background = BuddyTokens.WarningAmberBrush;
			break;
		case BuddyStatusState.Error:
			base.Background = BuddyTokens.DangerRedBrush;
			break;
		case BuddyStatusState.ActiveTimer:
			base.Background = BuddyTokens.AccentCoralBrush;
			break;
		default:
			base.Background = BuddyTokens.TextMutedBrush;
			break;
		}
	}
}
