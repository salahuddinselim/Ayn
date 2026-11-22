namespace WindowsBuddy.Services.Pet;

public sealed class PetActivity
{
	public string Name;

	public string Prop;

	public double Sit;

	public double Left = -15.0;

	public double Right = 15.0;

	public double Tilt;

	public double LookY;

	public string Requires;

	public PetActivity(string name, string prop, double sit, double left, double right, double tilt, string requires = null)
	{
		Name = name;
		Prop = prop;
		Sit = sit;
		Left = left;
		Right = right;
		Tilt = tilt;
		Requires = requires;
	}
}
