using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Theme;

public static class NotchInk
{
	public static readonly Brush Black = F(4278190080u);

	public static readonly Brush Primary = F(uint.MaxValue);

	public static readonly Brush Secondary = F(4288782758u);

	public static readonly Brush Tertiary = F(4287269519u);

	public static readonly Brush Hairline = F(536870911u);

	public static readonly Brush HairlineStrong = F(704643071u);

	public static readonly Brush Surface = F(4279111182u);

	public static readonly Brush Surface2 = F(4279637526u);

	public static readonly Brush Danger = F(4293216333u);

	public static readonly Brush Success = F(4281377900u);

	private static Brush F(uint argb)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}
}
