using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class PetEyeArea
{
	public double[][] eye_safe_quad { get; set; }

	public Dictionary<string, double> notch_rect { get; set; }
}
