using System.Windows;
using System.Windows.Media.Imaging;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class PetFrame
{
	public BitmapSource Body;

	public BitmapSource Props;

	public Int32Rect Crop;

	public Point[] Eyes;

	public double Aspect => (double)Crop.Width / (double)Crop.Height;
}
