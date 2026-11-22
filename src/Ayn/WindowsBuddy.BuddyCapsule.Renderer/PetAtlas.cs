using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class PetAtlas
{
	public Dictionary<string, Dictionary<string, PetEyeArea>> poses { get; set; }
}
