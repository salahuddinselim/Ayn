using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.ScreenTime;

public static class AppIconExtractor
{
	private const int Px = 64;

	private static readonly Dictionary<string, ImageSource> _cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, DateTime> _failed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, List<Action<ImageSource>>> _pending = new Dictionary<string, List<Action<ImageSource>>>(StringComparer.OrdinalIgnoreCase);

	private static readonly object _lock = new object();

	private static ImageSource _generic;

	private static string _dir;

	public static ImageSource Generic
	{
		get
		{
			if (_generic != null)
			{
				return _generic;
			}
			Pen pen = new Pen(BuddyTheme.Text50, 1.25);
			pen.StartLineCap = PenLineCap.Round;
			pen.EndLineCap = PenLineCap.Round;
			Pen pen2 = pen;
			DrawingGroup drawingGroup = new DrawingGroup();
			using (DrawingContext drawingContext = drawingGroup.Open())
			{
				drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, 26.0, 26.0));
				drawingContext.DrawRoundedRectangle(null, pen2, new Rect(4.5, 5.5, 17.0, 15.0), 3.5, 3.5);
				drawingContext.DrawLine(pen2, new Point(4.5, 9.5), new Point(21.5, 9.5));
				drawingContext.DrawEllipse(BuddyTheme.Text50, null, new Point(7.0, 7.5), 0.7, 0.7);
				drawingContext.DrawEllipse(BuddyTheme.Text50, null, new Point(9.2, 7.5), 0.7, 0.7);
			}
			DrawingImage generic = new DrawingImage(drawingGroup);
			_generic = generic;
			return _generic;
		}
	}

	public static ImageSource Get(string app, Action<ImageSource> onLoaded)
	{
		if (string.IsNullOrEmpty(app))
		{
			return Generic;
		}
		lock (_lock)
		{
			if (_cache.TryGetValue(app, out var value))
			{
				return value;
			}
			if (_failed.TryGetValue(app, out var value2) && DateTime.UtcNow - value2 < TimeSpan.FromMinutes(1.0))
			{
				return Generic;
			}
			if (_pending.TryGetValue(app, out var value3))
			{
				if (onLoaded != null)
				{
					value3.Add(onLoaded);
				}
				return Generic;
			}
			_pending[app] = new List<Action<ImageSource>>();
			if (onLoaded != null)
			{
				_pending[app].Add(onLoaded);
			}
		}
		Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
		Task.Run(delegate
		{
			ImageSource img = null;
			try
			{
				img = Load(app);
			}
			catch
			{
			}
			List<Action<ImageSource>> callbacks;
			lock (_lock)
			{
				_pending.TryGetValue(app, out callbacks);
				_pending.Remove(app);
				if (img != null)
				{
					_cache[app] = img;
					_failed.Remove(app);
				}
				else
				{
					_failed[app] = DateTime.UtcNow;
				}
			}
			if (img != null && callbacks != null && callbacks.Count > 0)
			{
				dispatcher.BeginInvoke((Action)delegate
				{
					foreach (Action<ImageSource> item in callbacks)
					{
						try
						{
							item(img);
						}
						catch
						{
						}
					}
				});
			}
		});
		return Generic;
	}

	public static ImageSource GetIcon(string app)
	{
		if (string.IsNullOrEmpty(app))
		{
			return Generic;
		}
		lock (_lock)
		{
			if (_cache.TryGetValue(app, out var value))
			{
				return value;
			}
		}
		ImageSource imageSource = null;
		try
		{
			imageSource = Load(app);
		}
		catch
		{
		}
		if (imageSource == null)
		{
			return Generic;
		}
		lock (_lock)
		{
			_cache[app] = imageSource;
			return imageSource;
		}
	}

	private static ImageSource Load(string app)
	{
		string exePath = ScreenTimeService.Instance.GetExePath(app);
		string text = PngPath(app);
		bool flag = !string.IsNullOrEmpty(exePath) && File.Exists(exePath);
		if (text != null && File.Exists(text) && (!flag || File.GetLastWriteTimeUtc(text) >= File.GetLastWriteTimeUtc(exePath)))
		{
			ImageSource imageSource = ReadPng(text);
			if (imageSource != null)
			{
				return imageSource;
			}
		}
		if (!flag)
		{
			return null;
		}
		ImageSource imageSource2 = ExtractLarge(exePath);
		if (imageSource2 != null && text != null)
		{
			WritePng(imageSource2, text);
		}
		return imageSource2;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHDefExtractIcon(string iconFile, int index, uint flags, out IntPtr large, out IntPtr small, uint sizes);

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr icon);

	private static ImageSource ExtractLarge(string exe)
	{
		IntPtr large = IntPtr.Zero;
		IntPtr small = IntPtr.Zero;
		try
		{
			if (SHDefExtractIcon(exe, 0, 0u, out large, out small, 1048640u) != 0 || large == IntPtr.Zero)
			{
				return null;
			}
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(large, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bitmapSource.Freeze();
			return bitmapSource;
		}
		catch
		{
			return null;
		}
		finally
		{
			if (large != IntPtr.Zero)
			{
				DestroyIcon(large);
			}
			if (small != IntPtr.Zero)
			{
				DestroyIcon(small);
			}
		}
	}

	private static string PngPath(string app)
	{
		try
		{
			if (_dir == null)
			{
				_dir = Path.Combine(AppPaths.LocalAppData, AppPaths.FolderName, "screentime", "icons-v2");
				Directory.CreateDirectory(_dir);
			}
			using SHA1 sHA = SHA1.Create();
			byte[] array = sHA.ComputeHash(Encoding.UTF8.GetBytes(app.ToLowerInvariant()));
			return Path.Combine(_dir, BitConverter.ToString(array, 0, 10).Replace("-", "") + ".png");
		}
		catch
		{
			return null;
		}
	}

	private static ImageSource ReadPng(string path)
	{
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
			bitmapImage.UriSource = new Uri(path);
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static void WritePng(ImageSource img, string path)
	{
		try
		{
			if (!(img is BitmapSource source))
			{
				return;
			}
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
			pngBitmapEncoder.Frames.Add(BitmapFrame.Create(source));
			using MemoryStream memoryStream = new MemoryStream();
			pngBitmapEncoder.Save(memoryStream);
			PersistenceHelper.WriteCacheBytes(path, memoryStream.ToArray());
		}
		catch
		{
		}
	}
}
