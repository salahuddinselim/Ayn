using System;
using System.Windows;
using WindowsBuddy.Tools;

namespace WindowsBuddy.BuddyCapsule.Core;

public class ToolNavigationEventArgs : EventArgs
{
	public IBuddyTool Tool { get; private set; }

	public UIElement View { get; private set; }

	public double TargetWidth { get; private set; }

	public double TargetHeight { get; private set; }

	public bool IsSwap { get; set; }

	public ToolNavigationEventArgs(IBuddyTool tool, UIElement view, double targetWidth, double targetHeight)
	{
		Tool = tool;
		View = view;
		TargetWidth = targetWidth;
		TargetHeight = targetHeight;
	}
}
