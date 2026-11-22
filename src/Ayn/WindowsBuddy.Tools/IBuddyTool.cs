using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.Tools;

public interface IBuddyTool
{
	string Id { get; }

	string DisplayName { get; }

	ToolCategory Category { get; }

	bool IsEnabled { get; set; }

	int OrderIndex { get; set; }

	bool HasCollapsedBadge { get; }

	bool IsInitialized { get; }

	bool RequiresNetwork { get; }

	ToolState State { get; }

	double DesiredWidth { get; }

	double DesiredHeight { get; }

	CollapsedBadgeData GetCollapsedBadge();

	UIElement CreateIcon(double size, Brush stroke);

	UIElement CreateView();

	void OnActivated();

	void OnDeactivated();

	void Initialize();

	void Shutdown();

	UIElement GetHeaderActions();
}
