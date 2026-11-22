using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal static class PetAssets
{
	private static readonly Dictionary<string, PetFrame> frames = new Dictionary<string, PetFrame>();

	private static PetAtlas atlas;

	internal static long DecodedBytes { get; private set; }

	internal static int FrameCount => frames.Count;

	internal static void Release()
	{
		frames.Clear();
		atlas = null;
		DecodedBytes = 0L;
	}

	private static Stream Resource(string name)
	{
		Stream manifestResourceStream = typeof(PetAssets).Assembly.GetManifestResourceStream("Ayn.Pet." + name);
		if (manifestResourceStream == null)
		{
			throw new InvalidDataException("Missing embedded pet asset: " + name);
		}
		return manifestResourceStream;
	}

	private static BitmapSource Decode(string pose, string layer, out Int32Rect bounds)
	{
		BitmapImage bitmapImage;
		using (Stream streamSource = Resource(layer + "." + pose + ".png"))
		{
			bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = 128;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
		}
		BitmapSource bitmapSource = bitmapImage;
		if (bitmapSource.Format != PixelFormats.Bgra32 && bitmapSource.Format != PixelFormats.Pbgra32)
		{
			bitmapSource = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0.0);
			bitmapSource.Freeze();
		}
		int pixelWidth = bitmapSource.PixelWidth;
		int pixelHeight = bitmapSource.PixelHeight;
		int num = pixelWidth * 4;
		byte[] array = new byte[num * pixelHeight];
		bitmapSource.CopyPixels(array, num, 0);
		int num2 = pixelWidth;
		int num3 = -1;
		int num4 = pixelHeight;
		int num5 = -1;
		bool flag = false;
		for (int i = 0; i < pixelHeight; i++)
		{
			for (int j = 0; j < pixelWidth; j++)
			{
				int num6 = i * num + j * 4;
				if (array[num6 + 3] > 2)
				{
					flag = true;
				}
				if (array[num6 + 3] >= 32 && Math.Max(array[num6], Math.Max(array[num6 + 1], array[num6 + 2])) >= 12)
				{
					num2 = Math.Min(num2, j);
					num3 = Math.Max(num3, j);
					num4 = Math.Min(num4, i);
					num5 = Math.Max(num5, i);
				}
			}
		}
		bounds = ((num3 < num2) ? Int32Rect.Empty : new Int32Rect(num2, num4, num3 - num2 + 1, num5 - num4 + 1));
		if (!flag)
		{
			return null;
		}
		DecodedBytes += num * pixelHeight;
		return bitmapSource;
	}

	internal static PetFrame Get(string pose)
	{
		if (frames.TryGetValue(pose, out var value))
		{
			return value;
		}
		if (atlas == null)
		{
			using Stream stream = Resource("eye-areas.json");
			using StreamReader streamReader = new StreamReader(stream);
			atlas = new JavaScriptSerializer().Deserialize<PetAtlas>(streamReader.ReadToEnd());
		}
		Int32Rect bounds;
		BitmapSource source = Decode(pose, "body", out bounds);
		Int32Rect bounds2;
		BitmapSource bitmapSource = Decode(pose, "props", out bounds2);
		Rect rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
		if (!bounds2.IsEmpty)
		{
			rect.Union(new Rect(bounds2.X, bounds2.Y, bounds2.Width, bounds2.Height));
		}
		Int32Rect int32Rect = new Int32Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
		double[][] eye_safe_quad = atlas.poses[pose]["128"].eye_safe_quad;
		Point[] array = new Point[4];
		for (int i = 0; i < 4; i++)
		{
			ref Point reference = ref array[i];
			reference = new Point(eye_safe_quad[i][0] - (double)int32Rect.X, eye_safe_quad[i][1] - (double)int32Rect.Y);
		}
		CroppedBitmap croppedBitmap = new CroppedBitmap(source, int32Rect);
		croppedBitmap.Freeze();
		BitmapSource bitmapSource2 = null;
		if (bitmapSource != null)
		{
			bitmapSource2 = new CroppedBitmap(bitmapSource, int32Rect);
			bitmapSource2.Freeze();
		}
		PetFrame petFrame = new PetFrame();
		petFrame.Body = croppedBitmap;
		petFrame.Props = bitmapSource2;
		petFrame.Crop = int32Rect;
		petFrame.Eyes = array;
		value = petFrame;
		frames.Add(pose, value);
		return value;
	}
}
