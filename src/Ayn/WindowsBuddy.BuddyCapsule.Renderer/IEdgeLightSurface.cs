using System;
using System.Windows;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public interface IEdgeLightSurface : IDisposable
{
	int RunningAnimations { get; }

	void SetOutline(EdgeLightOutline outline, Point cardOffset, double scale);

	void SetLook(EdgeLightLook look);

	void SetMode(EdgeLightMode mode);

	void SyncWindow();
}
