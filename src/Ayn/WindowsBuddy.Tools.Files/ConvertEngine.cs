using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Lifecycle;

namespace WindowsBuddy.Tools.Files;

public static class ConvertEngine
{
	internal sealed class Decoded
	{
		public int W;

		public int H;

		public byte[] Bgra;

		public bool HasAlpha;

		public ReadOnlyCollection<ColorContext> Color;

		public int LongEdge => Math.Max(W, H);

		public BitmapSource Opaque()
		{
			byte[] array = new byte[checked(W * H * 3)];
			int num = 0;
			int num2 = 0;
			while (num < Bgra.Length)
			{
				int num3 = Bgra[num + 3];
				if (num3 == 255)
				{
					array[num2] = Bgra[num];
					array[num2 + 1] = Bgra[num + 1];
					array[num2 + 2] = Bgra[num + 2];
				}
				else
				{
					int num4 = 255 - num3;
					array[num2] = (byte)((Bgra[num] * num3 + 255 * num4 + 127) / 255);
					array[num2 + 1] = (byte)((Bgra[num + 1] * num3 + 255 * num4 + 127) / 255);
					array[num2 + 2] = (byte)((Bgra[num + 2] * num3 + 255 * num4 + 127) / 255);
				}
				num += 4;
				num2 += 3;
			}
			BitmapSource bitmapSource = BitmapSource.Create(W, H, 96.0, 96.0, PixelFormats.Bgr24, null, array, W * 3);
			bitmapSource.Freeze();
			return bitmapSource;
		}

		public BitmapSource Lossless()
		{
			if (!HasAlpha)
			{
				return Opaque();
			}
			BitmapSource bitmapSource = BitmapSource.Create(W, H, 96.0, 96.0, PixelFormats.Bgra32, null, Bgra, W * 4);
			bitmapSource.Freeze();
			return bitmapSource;
		}

		public ImageSource Thumb()
		{
			BitmapSource bitmapSource = BitmapSource.Create(W, H, 96.0, 96.0, PixelFormats.Bgra32, null, Bgra, W * 4);
			bitmapSource.Freeze();
			return bitmapSource;
		}
	}

	internal sealed class Encoded
	{
		public byte[] Bytes;

		public int W;

		public int H;

		public bool Missed;
	}

	public sealed class Estimate
	{
		public long Total;

		public long InputTotal;

		public bool Capped;
	}

	public sealed class SaveFolderException : IOException
	{
		public SaveFolderException(string message, Exception inner)
			: base(message, inner)
		{
		}
	}

	public const int MaxFiles = 50;

	internal const int MaxInputBytes = 209715200;

	internal const long MaxPixels = 120000000L;

	public const int PdfInputDpi = 150;

	private const int QualityMin = 5;

	private const int QualityMax = 95;

	private const int MaxSearchEncodes = 7;

	private const int MaxDownscaleSteps = 6;

	private const double DownscaleStep = 0.85;

	public const string DialogFilter = "Images and PDFs|*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic;*.heif;*.ico;*.pdf|All files|*.*";

	private static readonly SemaphoreSlim ProbeSlots = new SemaphoreSlim(2);

	public static readonly int[] IcoSizes = new int[4] { 16, 32, 48, 256 };

	public static readonly string[] InputExtensions = new string[13]
	{
		".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic",
		".heif", ".ico", ".pdf"
	};

	private static readonly Dictionary<string, ImageSource> ThumbCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

	private static readonly object NameLock = new object();

	public static readonly ProducerFence Outputs = new ProducerFence();

	internal static void CheckPixels(int width, int height)
	{
		if (width <= 0 || height <= 0 || (long)width * (long)height > 120000000)
		{
			throw new InvalidDataException("Image exceeds the 120 megapixel limit");
		}
	}

	internal static void CheckInputSize(string path)
	{
		if (new FileInfo(path).Length > 209715200)
		{
			throw new InvalidDataException("File exceeds the 200 MB limit");
		}
	}

	private static byte[] ReadInput(string path)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		if (fileStream.Length > 209715200)
		{
			throw new InvalidDataException("File exceeds the 200 MB limit");
		}
		byte[] array = new byte[(int)fileStream.Length];
		int num;
		for (int i = 0; i < array.Length; i += num)
		{
			num = fileStream.Read(array, i, array.Length - i);
			if (num == 0)
			{
				throw new EndOfStreamException();
			}
		}
		if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !EndsWithIend(array))
		{
			throw new InvalidDataException("File is incomplete or damaged");
		}
		return array;
	}

	private static bool EndsWithIend(byte[] b)
	{
		int num = b.Length - 4;
		while (num >= 0 && num >= b.Length - 256)
		{
			if (b[num] == 73 && b[num + 1] == 69 && b[num + 2] == 78 && b[num + 3] == 68)
			{
				return true;
			}
			num--;
		}
		return false;
	}

	public static bool IsSupported(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return false;
		}
		string value = Path.GetExtension(path).ToLowerInvariant();
		return Array.IndexOf(InputExtensions, value) >= 0;
	}

	public static void Probe(FileItem item)
	{
		ProbeSlots.Wait();
		try
		{
			CheckInputSize(item.Path);
			FileInfo fileInfo = new FileInfo(item.Path);
			item.SizeBytes = fileInfo.Length;
			string key = item.Path + "|" + fileInfo.LastWriteTimeUtc.Ticks;
			ImageSource value;
			lock (ThumbCache)
			{
				ThumbCache.TryGetValue(key, out value);
			}
			if (item.IsPdf)
			{
				if (!PdfInput.Available)
				{
					item.InputError = "PDF input not supported";
					return;
				}
				using PdfInput pdfInput = PdfInput.Open(item.Path);
				item.PageCount = pdfInput.PageCount;
				pdfInput.PageSizeAt(0, 0, out var w, out var h);
				item.Width = w;
				item.Height = h;
				if (value == null)
				{
					value = Decode(pdfInput.Render(0, 64), 0).Thumb();
				}
			}
			else
			{
				byte[] data = ReadInput(item.Path);
				ReadInfo(data, out var w2, out var h2);
				item.Width = w2;
				item.Height = h2;
				if (value == null)
				{
					value = Decode(data, 64).Thumb();
				}
			}
			if (value != null)
			{
				lock (ThumbCache)
				{
					ThumbCache[key] = value;
				}
			}
			item.Thumb = value;
		}
		catch (Exception ex)
		{
			item.InputError = Reason(ex, item.Ext);
			TransactionLogger.Warn("Converter: probe failed for " + item.Name + ": " + ex.Message);
		}
		finally
		{
			item.Probed = true;
			ProbeSlots.Release();
		}
	}

	private static BitmapFrame ReadInfo(byte[] data, out int w, out int h)
	{
		BitmapDecoder bitmapDecoder = BitmapDecoder.Create(new MemoryStream(data), BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
		BitmapFrame bitmapFrame = bitmapDecoder.Frames[0];
		bool flag = ReadOrientation(bitmapFrame) >= 5;
		w = (flag ? bitmapFrame.PixelHeight : bitmapFrame.PixelWidth);
		h = (flag ? bitmapFrame.PixelWidth : bitmapFrame.PixelHeight);
		CheckPixels(w, h);
		return bitmapFrame;
	}

	internal static Decoded Decode(byte[] data, int longEdge)
	{
		int w;
		int h;
		BitmapFrame bitmapFrame = ReadInfo(data, out w, out h);
		int num = ReadOrientation(bitmapFrame);
		ReadOnlyCollection<ColorContext> color = null;
		try
		{
			color = bitmapFrame.ColorContexts;
		}
		catch
		{
		}
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.StreamSource = new MemoryStream(data);
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
		if (longEdge > 0 && Math.Max(w, h) > longEdge)
		{
			double num2 = (double)longEdge / (double)Math.Max(w, h);
			int num3 = ((num >= 5) ? h : w);
			bitmapImage.DecodePixelWidth = Math.Max(1, (int)Math.Round((double)num3 * num2));
		}
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		BitmapSource bitmapSource = bitmapImage;
		Transform transform = OrientationTransform(num);
		if (transform != null)
		{
			bitmapSource = new TransformedBitmap(bitmapSource, transform);
			bitmapSource.Freeze();
		}
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0.0);
		formatConvertedBitmap.Freeze();
		Decoded decoded = new Decoded();
		decoded.W = formatConvertedBitmap.PixelWidth;
		decoded.H = formatConvertedBitmap.PixelHeight;
		decoded.Color = color;
		Decoded decoded2 = decoded;
		CheckPixels(decoded2.W, decoded2.H);
		decoded2.Bgra = new byte[checked(decoded2.W * decoded2.H * 4)];
		formatConvertedBitmap.CopyPixels(decoded2.Bgra, decoded2.W * 4, 0);
		for (int i = 3; i < decoded2.Bgra.Length; i += 4)
		{
			if (decoded2.Bgra[i] != byte.MaxValue)
			{
				decoded2.HasAlpha = true;
				break;
			}
		}
		return decoded2;
	}

	private static int ReadOrientation(BitmapFrame f)
	{
		try
		{
			if (!(f.Metadata is BitmapMetadata bitmapMetadata))
			{
				return 1;
			}
			object obj = null;
			string[] array = new string[3] { "System.Photo.Orientation", "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" };
			foreach (string query in array)
			{
				try
				{
					obj = bitmapMetadata.GetQuery(query);
				}
				catch
				{
					obj = null;
				}
				if (obj != null)
				{
					break;
				}
			}
			if (obj == null)
			{
				return 1;
			}
			int num = Convert.ToInt32(obj);
			return (num < 1 || num > 8) ? 1 : num;
		}
		catch
		{
			return 1;
		}
	}

	private static Transform OrientationTransform(int o)
	{
		ScaleTransform scaleTransform = new ScaleTransform(-1.0, 1.0);
		return o switch
		{
			2 => scaleTransform, 
			3 => new RotateTransform(180.0), 
			4 => new ScaleTransform(1.0, -1.0), 
			5 => Group(new RotateTransform(90.0), scaleTransform), 
			6 => new RotateTransform(90.0), 
			7 => Group(new RotateTransform(270.0), scaleTransform), 
			8 => new RotateTransform(270.0), 
			_ => null, 
		};
	}

	private static Transform Group(Transform a, Transform b)
	{
		TransformGroup transformGroup = new TransformGroup();
		transformGroup.Children.Add(a);
		transformGroup.Children.Add(b);
		return transformGroup;
	}

	internal static byte[] Jpeg(BitmapSource opaque, int quality, ReadOnlyCollection<ColorContext> color)
	{
		try
		{
			return JpegCore(opaque, quality, color);
		}
		catch
		{
			if (color == null)
			{
				throw;
			}
			return JpegCore(opaque, quality, null);
		}
	}

	private static byte[] JpegCore(BitmapSource opaque, int quality, ReadOnlyCollection<ColorContext> color)
	{
		JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder();
		jpegBitmapEncoder.QualityLevel = Math.Max(1, Math.Min(100, quality));
		JpegBitmapEncoder jpegBitmapEncoder2 = jpegBitmapEncoder;
		jpegBitmapEncoder2.Frames.Add(BitmapFrame.Create(opaque, null, null, color));
		using MemoryStream memoryStream = new MemoryStream();
		jpegBitmapEncoder2.Save(memoryStream);
		return memoryStream.ToArray();
	}

	internal static byte[] Png(BitmapSource src, ReadOnlyCollection<ColorContext> color)
	{
		try
		{
			return PngCore(src, color);
		}
		catch
		{
			if (color == null)
			{
				throw;
			}
			return PngCore(src, null);
		}
	}

	private static byte[] PngCore(BitmapSource src, ReadOnlyCollection<ColorContext> color)
	{
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Interlace = PngInterlaceOption.Off;
		PngBitmapEncoder pngBitmapEncoder2 = pngBitmapEncoder;
		pngBitmapEncoder2.Frames.Add(BitmapFrame.Create(src, null, null, color));
		using MemoryStream memoryStream = new MemoryStream();
		pngBitmapEncoder2.Save(memoryStream);
		return memoryStream.ToArray();
	}

	private static byte[] EncodeWith(BitmapEncoder encoder, BitmapSource source)
	{
		encoder.Frames.Add(BitmapFrame.Create(source));
		using MemoryStream stream = new MemoryStream();
		encoder.Save(stream);
		return stream.ToArray();
	}

	internal static byte[] Ico(Func<int, Decoded> decodeAt, CancellationToken ct)
	{
		List<byte[]> list = new List<byte[]>();
		int[] icoSizes = IcoSizes;
		foreach (int num in icoSizes)
		{
			ct.ThrowIfCancellationRequested();
			Decoded decoded = decodeAt(num);
			if (decoded.LongEdge != num)
			{
				decoded = Rescale(decoded, num);
			}
			byte[] array = new byte[num * num * 4];
			int num2 = (num - decoded.W) / 2;
			int num3 = (num - decoded.H) / 2;
			for (int j = 0; j < decoded.H; j++)
			{
				Buffer.BlockCopy(decoded.Bgra, j * decoded.W * 4, array, ((j + num3) * num + num2) * 4, decoded.W * 4);
			}
			BitmapSource bitmapSource = BitmapSource.Create(num, num, 96.0, 96.0, PixelFormats.Bgra32, null, array, num * 4);
			bitmapSource.Freeze();
			list.Add(PngCore(bitmapSource, null));
		}
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((ushort)0);
		binaryWriter.Write((ushort)1);
		binaryWriter.Write((ushort)list.Count);
		int num4 = 6 + 16 * list.Count;
		for (int k = 0; k < list.Count; k++)
		{
			int num5 = IcoSizes[k];
			binaryWriter.Write((byte)((num5 < 256) ? ((uint)num5) : 0u));
			binaryWriter.Write((byte)((num5 < 256) ? ((uint)num5) : 0u));
			binaryWriter.Write((byte)0);
			binaryWriter.Write((byte)0);
			binaryWriter.Write((ushort)1);
			binaryWriter.Write((ushort)32);
			binaryWriter.Write(list[k].Length);
			binaryWriter.Write(num4);
			num4 += list[k].Length;
		}
		foreach (byte[] item in list)
		{
			binaryWriter.Write(item);
		}
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}

	private static Decoded Rescale(Decoded d, int longEdge)
	{
		double num = (double)longEdge / (double)d.LongEdge;
		BitmapSource source = BitmapSource.Create(d.W, d.H, 96.0, 96.0, PixelFormats.Bgra32, null, d.Bgra, d.W * 4);
		TransformedBitmap transformedBitmap = new TransformedBitmap(source, new ScaleTransform(num, num));
		transformedBitmap.Freeze();
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(transformedBitmap, PixelFormats.Bgra32, null, 0.0);
		Decoded decoded = new Decoded();
		decoded.W = formatConvertedBitmap.PixelWidth;
		decoded.H = formatConvertedBitmap.PixelHeight;
		decoded.Color = d.Color;
		decoded.HasAlpha = d.HasAlpha;
		Decoded decoded2 = decoded;
		decoded2.Bgra = new byte[checked(decoded2.W * decoded2.H * 4)];
		formatConvertedBitmap.CopyPixels(decoded2.Bgra, decoded2.W * 4, 0);
		return decoded2;
	}

	internal static Encoded JpegToTarget(Decoded d, Func<int, Decoded> decodeAt, long target, CancellationToken ct)
	{
		Encoded encoded = null;
		for (int i = 0; i <= 6; i++)
		{
			ct.ThrowIfCancellationRequested();
			if (i > 0)
			{
				d = decodeAt(Math.Max(16, (int)((double)d.LongEdge * 0.85)));
			}
			BitmapSource opaque = d.Opaque();
			int num = 5;
			int num2 = 95;
			int num3 = 0;
			Encoded encoded2 = null;
			if (i > 0)
			{
				Encoded encoded3 = new Encoded();
				encoded3.Bytes = Jpeg(opaque, 5, d.Color);
				encoded3.W = d.W;
				encoded3.H = d.H;
				Encoded encoded4 = encoded3;
				num3++;
				if (encoded == null || encoded4.Bytes.Length < encoded.Bytes.Length)
				{
					encoded = encoded4;
				}
				if (encoded4.Bytes.Length > target)
				{
					continue;
				}
				encoded2 = encoded4;
				num = 6;
			}
			while (num <= num2 && num3 < 7)
			{
				ct.ThrowIfCancellationRequested();
				int num4 = (num + num2) / 2;
				byte[] array = Jpeg(opaque, num4, d.Color);
				num3++;
				Encoded encoded5 = new Encoded();
				encoded5.Bytes = array;
				encoded5.W = d.W;
				encoded5.H = d.H;
				Encoded encoded6 = encoded5;
				if (encoded == null || array.Length < encoded.Bytes.Length)
				{
					encoded = encoded6;
				}
				if (array.Length <= target)
				{
					encoded2 = encoded6;
					num = num4 + 1;
				}
				else
				{
					num2 = num4 - 1;
				}
			}
			if (encoded2 != null)
			{
				return encoded2;
			}
		}
		encoded.Missed = true;
		return encoded;
	}

	public static void ConvertOne(FileItem item, ConvertSettings s, Action<double> progress, CancellationToken ct)
	{
		List<string> list = new List<string>();
		long num = 0L;
		string missed = null;
		try
		{
			string dir = OutputFolder(item, s);
			string ext = ((s.Format == OutFormat.Png) ? ".png" : ((s.Format == OutFormat.Ico) ? ".ico" : ".jpg"));
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
			if (item.IsPdf)
			{
				PdfInput doc = PdfInput.Open(item.Path);
				try
				{
					int i = doc.PageCount;
					for (int j = 0; j < i; j++)
					{
						ct.ThrowIfCancellationRequested();
						int page = j;
						Func<int, Decoded> decodeAt = (int le) => Decode(doc.Render(page, le), 0);
						Action<double> action = delegate(double f)
						{
							progress(((double)page + f) / (double)i);
						};
						string missed2;
						byte[] array = EncodeImage(decodeAt, s, action, ct, out missed2);
						if (missed2 != null)
						{
							missed = missed2;
						}
						list.Add(WriteUnique(dir, fileNameWithoutExtension + "-p" + (j + 1), ext, array));
						num += array.Length;
						action(1.0);
					}
				}
				finally
				{
					if (doc != null)
					{
						((IDisposable)doc).Dispose();
					}
				}
			}
			else
			{
				byte[] data = ReadInput(item.Path);
				Func<int, Decoded> decodeAt2 = (int le) => Decode(data, le);
				byte[] array2 = EncodeImage(decodeAt2, s, progress, ct, out missed);
				ct.ThrowIfCancellationRequested();
				list.Add(WriteUnique(dir, fileNameWithoutExtension, ext, array2));
				num = array2.Length;
				progress(1.0);
			}
			item.Outputs.Clear();
			item.Outputs.AddRange(list);
			item.OutputBytes = num;
			item.Note = missed;
		}
		catch
		{
			foreach (string item2 in list)
			{
				TryDelete(item2);
			}
			throw;
		}
	}

	private static byte[] EncodeImage(Func<int, Decoded> decodeAt, ConvertSettings s, Action<double> progress, CancellationToken ct, out string missed)
	{
		missed = null;
		if (s.Format == OutFormat.Ico)
		{
			progress(0.3);
			byte[] result = Ico(decodeAt, ct);
			progress(0.9);
			return result;
		}
		Decoded decoded = decodeAt(s.ResizeLongEdge);
		ct.ThrowIfCancellationRequested();
		progress(0.3);
		byte[] result2;
		if (s.Format == OutFormat.Png)
		{
			result2 = Png(decoded.Lossless(), decoded.Color);
		}
		else if (s.Format == OutFormat.Bmp)
		{
			result2 = EncodeWith(new BmpBitmapEncoder(), decoded.Lossless());
		}
		else if (s.Format == OutFormat.Gif)
		{
			result2 = EncodeWith(new GifBitmapEncoder(), decoded.Lossless());
		}
		else if (s.Format == OutFormat.Tiff)
		{
			result2 = EncodeWith(new TiffBitmapEncoder(), decoded.Lossless());
		}
		else if (s.UsesTarget)
		{
			Encoded encoded = JpegToTarget(decoded, decodeAt, s.TargetBytes, ct);
			if (encoded.Missed)
			{
				missed = "Couldn't reach " + FileItem.FormatBytes(s.TargetBytes) + " · " + FileItem.FormatBytes(encoded.Bytes.Length);
			}
			result2 = encoded.Bytes;
		}
		else
		{
			result2 = Jpeg(decoded.Opaque(), s.Quality, decoded.Color);
		}
		progress(0.9);
		return result2;
	}

	public static string ConvertToPdf(IList<FileItem> items, ConvertSettings s, Action<FileItem, double> progress, CancellationToken ct, out string missedNote)
	{
		missedNote = null;
		List<Encoded>[] pages = new List<Encoded>[items.Count];
		double totalArea = ((IEnumerable<FileItem>)items).Sum((Func<FileItem, double>)((FileItem i) => OutputArea(i, s.ResizeLongEdge)));
		long budgetTotal = Math.Max(1024L, s.TargetBytes - 1024 - 400L * (long)items.Sum((FileItem i) => i.PageCount));
		bool missed = false;
		long encodedBytes = 0L;
		SemaphoreSlim gate = new SemaphoreSlim(2);
		try
		{
			List<Task> list = new List<Task>();
			for (int j = 0; j < items.Count; j++)
			{
				int idx = j;
				FileItem item = items[idx];
				list.Add(Task.Run(delegate
				{
					gate.Wait(ct);
					try
					{
						ct.ThrowIfCancellationRequested();
						List<Encoded> list2 = new List<Encoded>();
						Action<int, Func<int, Decoded>, int> action = delegate(int page, Func<int, Decoded> at, int n)
						{
							Decoded decoded = at(s.ResizeLongEdge);
							progress(item, ((double)page + 0.3) / (double)n);
							Encoded encoded;
							if (s.UsesTarget)
							{
								long val = (long)((double)budgetTotal * ((double)decoded.W * (double)decoded.H / Math.Max(1.0, totalArea)));
								encoded = JpegToTarget(decoded, at, Math.Max(2048L, val), ct);
								if (encoded.Missed)
								{
									missed = true;
								}
							}
							else
							{
								encoded = new Encoded
								{
									Bytes = Jpeg(decoded.Opaque(), s.Quality, null),
									W = decoded.W,
									H = decoded.H
								};
							}
							if (Interlocked.Add(ref encodedBytes, encoded.Bytes.Length) > 134217728)
							{
								throw new InvalidDataException("Combined PDF exceeds the 128 MB limit");
							}
							list2.Add(encoded);
							progress(item, ((double)page + 0.9) / (double)n);
						};
						if (item.IsPdf)
						{
							PdfInput doc = PdfInput.Open(item.Path);
							try
							{
								for (int k = 0; k < doc.PageCount; k++)
								{
									ct.ThrowIfCancellationRequested();
									int page2 = k;
									action(page2, (int le) => Decode(doc.Render(page2, le), 0), doc.PageCount);
								}
							}
							finally
							{
								if (doc != null)
								{
									((IDisposable)doc).Dispose();
								}
							}
						}
						else
						{
							byte[] data = ReadInput(item.Path);
							action(0, (int le) => Decode(data, le), 1);
						}
						pages[idx] = list2;
						item.OutputBytes = ((IEnumerable<Encoded>)list2).Sum((Func<Encoded, long>)((Encoded e) => e.Bytes.Length));
					}
					finally
					{
						gate.Release();
					}
				}, ct));
			}
			Task.WaitAll(list.ToArray());
		}
		finally
		{
			if (gate != null)
			{
				((IDisposable)gate).Dispose();
			}
		}
		ct.ThrowIfCancellationRequested();
		FileItem fileItem = items[0];
		string baseName = Path.GetFileNameWithoutExtension(fileItem.Path) + ((items.Count > 1) ? (" + " + (items.Count - 1)) : "");
		byte[] array = PdfWriter.Build(pages.SelectMany((List<Encoded> p) => p).ToList());
		string result = WriteUnique(OutputFolder(fileItem, s), baseName, ".pdf", array);
		if (missed)
		{
			missedNote = "Couldn't reach " + FileItem.FormatBytes(s.TargetBytes) + " · " + FileItem.FormatBytes(array.Length);
		}
		return result;
	}

	public static Estimate EstimateBatch(IList<FileItem> items, ConvertSettings s, CancellationToken ct)
	{
		List<FileItem> list = items.Where((FileItem i) => i.Probed && i.InputError == null && i.Width > 0).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		FileItem fileItem = list[0];
		Func<int, Decoded> func;
		if (fileItem.IsPdf)
		{
			PdfInput doc = PdfInput.Open(fileItem.Path);
			func = (int le) => Decode(doc.Render(0, le), 0);
		}
		else
		{
			byte[] data = ReadInput(fileItem.Path);
			func = (int le) => Decode(data, le);
		}
		ct.ThrowIfCancellationRequested();
		long num;
		if (s.Format == OutFormat.Ico)
		{
			num = Ico(func, ct).Length;
			Estimate estimate = new Estimate();
			estimate.InputTotal = list.Sum((FileItem i) => i.SizeBytes);
			estimate.Total = num * ((IEnumerable<FileItem>)list).Sum((Func<FileItem, long>)((FileItem i) => i.PageCount));
			return estimate;
		}
		Decoded decoded = func(s.ResizeLongEdge);
		ct.ThrowIfCancellationRequested();
		bool usesTarget = s.UsesTarget;
		long num2 = ((s.Format == OutFormat.Pdf) ? long.MaxValue : s.TargetBytes);
		int w;
		int h;
		if (s.Format == OutFormat.Png)
		{
			num = Png(decoded.Lossless(), decoded.Color).Length;
			w = decoded.W;
			h = decoded.H;
		}
		else if (usesTarget && s.Format == OutFormat.Jpg)
		{
			Encoded encoded = JpegToTarget(decoded, func, s.TargetBytes, ct);
			num = encoded.Bytes.Length;
			w = decoded.W;
			h = decoded.H;
		}
		else
		{
			num = Jpeg(decoded.Opaque(), s.Quality, null).Length;
			w = decoded.W;
			h = decoded.H;
		}
		double num3 = (double)num / Math.Max(1.0, (double)w * (double)h);
		Estimate estimate2 = new Estimate();
		estimate2.InputTotal = list.Sum((FileItem i) => i.SizeBytes);
		Estimate estimate3 = estimate2;
		long num4 = 0L;
		for (int j = 0; j < list.Count; j++)
		{
			FileItem fileItem2 = list[j];
			long num5 = ((j == 0 && !fileItem2.IsPdf) ? num : ((long)(num3 * (double)OutputArea(fileItem2, s.ResizeLongEdge))));
			if (usesTarget && num5 > num2)
			{
				num5 = num2;
				estimate3.Capped = true;
			}
			num4 += num5;
		}
		if (s.Format == OutFormat.Pdf)
		{
			num4 += 1024 + 400 * ((IEnumerable<FileItem>)list).Sum((Func<FileItem, long>)((FileItem i) => i.PageCount));
			if (usesTarget && num4 > s.TargetBytes)
			{
				num4 = s.TargetBytes;
				estimate3.Capped = true;
			}
		}
		estimate3.Total = num4;
		return estimate3;
	}

	public static long OutputArea(FileItem i, int longEdge)
	{
		double num = i.Width;
		double num2 = i.Height;
		double num3 = Math.Max(num, num2);
		if (longEdge > 0 && num3 > (double)longEdge)
		{
			double num4 = (double)longEdge / num3;
			num *= num4;
			num2 *= num4;
		}
		return (long)(num * num2) * Math.Max(1, i.PageCount);
	}

	public static Action PauseOutputs()
	{
		Outputs.Close();
		return Outputs.Reopen;
	}

	public static string OutputFolder(FileItem item, ConvertSettings s)
	{
		switch (s.SaveTo)
		{
		case SaveTarget.Downloads:
			return DownloadsFolder();
		case SaveTarget.Custom:
			if (!string.IsNullOrEmpty(s.CustomFolder) && Directory.Exists(s.CustomFolder))
			{
				return s.CustomFolder;
			}
			throw new SaveFolderException("Save folder is missing", null);
		default:
			return Path.GetDirectoryName(item.Path);
		}
	}

	private static string WriteUnique(string dir, string baseName, string ext, byte[] bytes)
	{
		if (!Outputs.TryEnter(out var _))
		{
			throw new OperationCanceledException("Ayn is closing; the output was not written.");
		}
		try
		{
			return WriteUniqueAdmitted(dir, baseName, ext, bytes);
		}
		catch (UnauthorizedAccessException inner)
		{
			throw new SaveFolderException("Can't write to the save folder", inner);
		}
		catch (DirectoryNotFoundException inner2)
		{
			throw new SaveFolderException("Save folder is missing", inner2);
		}
		catch (PathTooLongException inner3)
		{
			throw new SaveFolderException("Save folder path is too long", inner3);
		}
		finally
		{
			Outputs.Exit();
		}
	}

	public static string CheckSaveFolder(string dir)
	{
		try
		{
			if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
			{
				return "Save folder is missing";
			}
			string path = Path.Combine(dir, ".ayn-write-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
			using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
			{
			}
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return "Can't write to the save folder";
		}
		catch (DirectoryNotFoundException)
		{
			return "Save folder is missing";
		}
		catch (PathTooLongException)
		{
			return "Save folder path is too long";
		}
		catch (IOException ex4)
		{
			return (ex4.HResult == -2147024784) ? "Disk full" : "Can't write to the save folder";
		}
		catch (Exception)
		{
			return "Can't write to the save folder";
		}
	}

	private static string WriteUniqueAdmitted(string dir, string baseName, string ext, byte[] bytes)
	{
		FileStream fileStream = null;
		string text = null;
		lock (NameLock)
		{
			for (int i = 0; i < 10000; i++)
			{
				if (fileStream != null)
				{
					break;
				}
				text = Path.Combine(dir, (i == 0) ? (baseName + ext) : (baseName + " (" + i + ")" + ext));
				if (File.Exists(text))
				{
					continue;
				}
				try
				{
					fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None);
				}
				catch (IOException)
				{
					if (!File.Exists(text))
					{
						throw;
					}
				}
			}
		}
		if (fileStream == null)
		{
			throw new IOException("No free file name");
		}
		try
		{
			using (fileStream)
			{
				fileStream.Write(bytes, 0, bytes.Length);
				return text;
			}
		}
		catch
		{
			TryDelete(text);
			throw;
		}
	}

	public static void TryDelete(string path)
	{
		try
		{
			if (!string.IsNullOrEmpty(path) && File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	[DllImport("shell32.dll")]
	private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);

	public static string DownloadsFolder()
	{
		try
		{
			if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0u, IntPtr.Zero, out var path) == 0)
			{
				string text = Marshal.PtrToStringUni(path);
				Marshal.FreeCoTaskMem(path);
				if (!string.IsNullOrEmpty(text))
				{
					return text;
				}
			}
		}
		catch
		{
		}
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
	}

	public static string Reason(Exception ex, string ext)
	{
		while (ex is AggregateException && ex.InnerException != null)
		{
			ex = ex.InnerException;
		}
		while (ex is TargetInvocationException && ex.InnerException != null)
		{
			ex = ex.InnerException;
		}
		ext = (ext ?? "").ToLowerInvariant();
		if (ex is SaveFolderException)
		{
			return ex.Message;
		}
		uint hResult = (uint)ex.HResult;
		bool flag = ex is NotSupportedException || hResult == 2291674960u || hResult == 2291674887u || hResult == 2291675009u;
		if (ex is FileFormatException || flag)
		{
			switch (ext)
			{
			case ".heic":
			case ".heif":
				return "Install HEIF Image Extensions";
			case ".webp":
				return "Install WebP Image Extensions";
			default:
				if (!flag)
				{
					return "File is damaged";
				}
				return "Unsupported image";
			}
		}
		if (hResult == 2291674976u || hResult == 2291674977u || hResult == 2291674978u)
		{
			return "File is damaged";
		}
		if (ex is UnauthorizedAccessException)
		{
			return "Can't read it (access denied)";
		}
		if (ex is DirectoryNotFoundException)
		{
			return "Folder not found";
		}
		if (ex is FileNotFoundException)
		{
			return "File not found";
		}
		if (ex is OutOfMemoryException || ex is InsufficientMemoryException)
		{
			return "Image too large";
		}
		if (ex is InvalidDataException)
		{
			return ex.Message;
		}
		if (ex is IOException)
		{
			if (hResult != 2147942512u)
			{
				return "File is open in another app";
			}
			return "Disk full";
		}
		if (ex is PdfInput.PdfException)
		{
			return ex.Message;
		}
		TransactionLogger.Warn("Converter: " + ex.GetType().Name + ": " + ex.Message);
		switch (ext)
		{
		case ".heic":
		case ".heif":
			return "Install HEIF Image Extensions";
		case ".webp":
			return "Install WebP Image Extensions";
		default:
			return "Couldn't read it (damaged?)";
		}
	}
}
