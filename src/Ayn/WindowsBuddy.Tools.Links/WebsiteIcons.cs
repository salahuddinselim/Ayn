using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Links;

public static class WebsiteIcons
{
	private static readonly ConcurrentDictionary<string, Task<ImageSource>> Pending = new ConcurrentDictionary<string, Task<ImageSource>>();

	private static readonly Regex Links = new Regex("<link\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex Attributes = new Regex("([\\w:-]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.Compiled);

	public static string CacheFolder => Path.Combine(AppPaths.DataDir, "links", "icons");

	public static ImageSource GetCached(string url)
	{
		if (!AppPreferences.Instance.ShowWebsiteIconsInLinks || !LinkUrl.TryNormalize(url, out var uri))
		{
			return null;
		}
		try
		{
			string text;
			using (SHA256 sHA = SHA256.Create())
			{
				text = BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(uri.GetLeftPart(UriPartial.Authority)))).Replace("-", "");
			}
			string path = Path.Combine(CacheFolder, text + ".icon");
			return File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null;
		}
		catch
		{
			return null;
		}
	}

	public static Task<ImageSource> GetAsync(string url)
	{
		if (!AppPreferences.Instance.ShowWebsiteIconsInLinks || !LinkUrl.TryNormalize(url, out var site))
		{
			return Task.FromResult<ImageSource>(null);
		}
		string key = site.GetLeftPart(UriPartial.Authority);
		if (Pending.TryGetValue(key, out var value) && value.IsCompleted)
		{
			Pending.TryRemove(key, out value);
		}
		Task<ImageSource> orAdd = Pending.GetOrAdd(key, (string k) => Task.Run(() => Load(site)));
		orAdd.ContinueWith(delegate
		{
			Pending.TryRemove(key, out var _);
		}, TaskContinuationOptions.ExecuteSynchronously);
		return orAdd;
	}

	private static ImageSource Load(Uri site)
	{
		try
		{
			Directory.CreateDirectory(CacheFolder);
			string text;
			using (SHA256 sHA = SHA256.Create())
			{
				text = BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(site.GetLeftPart(UriPartial.Authority)))).Replace("-", "");
			}
			string path = Path.Combine(CacheFolder, text + ".icon");
			string path2 = Path.Combine(CacheFolder, text + ".checked");
			if (File.Exists(path2) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path2) < TimeSpan.FromDays(30.0))
			{
				return File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null;
			}
			if (!AppPreferences.Instance.ShowWebsiteIconsInLinks)
			{
				return null;
			}
			File.WriteAllText(path2, DateTime.UtcNow.ToString("O"));
			Stopwatch stopwatch = Stopwatch.StartNew();
			List<KeyValuePair<Uri, int>> list = new List<KeyValuePair<Uri, int>>();
			try
			{
				string @string = Encoding.UTF8.GetString(Fetch(site, site, stopwatch, 524288));
				foreach (Match item in Links.Matches(@string))
				{
					Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (Match item2 in Attributes.Matches(item.Value))
					{
						dictionary[item2.Groups[1].Value] = WebUtility.HtmlDecode(item2.Groups[2].Success ? item2.Groups[2].Value : (item2.Groups[3].Success ? item2.Groups[3].Value : item2.Groups[4].Value));
					}
					if (dictionary.TryGetValue("rel", out var value) && dictionary.TryGetValue("href", out var value2) && value.Split(' ').Any((string r) => r.Equals("icon", StringComparison.OrdinalIgnoreCase) || r.Equals("apple-touch-icon", StringComparison.OrdinalIgnoreCase)) && Uri.TryCreate(site, value2, out var result) && SameSite(site, result))
					{
						int result2 = 0;
						if (dictionary.TryGetValue("sizes", out var value3))
						{
							int.TryParse(value3.Split('x')[0], out result2);
						}
						int value4 = (result.AbsolutePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? 200 : (result.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? 190 : 0)) + ((result2 >= 64 && result2 <= 180) ? 100 : 0) - Math.Abs(result2 - 128);
						list.Add(new KeyValuePair<Uri, int>(result, value4));
					}
				}
			}
			catch (WebException)
			{
			}
			list.Add(new KeyValuePair<Uri, int>(new Uri(site.GetLeftPart(UriPartial.Authority) + "/favicon.ico"), int.MinValue));
			foreach (KeyValuePair<Uri, int> item3 in list.OrderByDescending((KeyValuePair<Uri, int> c) => c.Value).Take(8))
			{
				if (stopwatch.ElapsedMilliseconds >= 5000 || !AppPreferences.Instance.ShowWebsiteIconsInLinks)
				{
					break;
				}
				try
				{
					byte[] array = Fetch(site, item3.Key, stopwatch, 1048576);
					ImageSource imageSource = Decode(array);
					if (imageSource == null)
					{
						continue;
					}
					PersistenceHelper.WriteCacheBytes(path, array);
					return imageSource;
				}
				catch
				{
				}
			}
			return File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null;
		}
		catch
		{
			return null;
		}
	}

	private static bool SameSite(Uri site, Uri icon)
	{
		if ((icon.Scheme == "https" || icon.Scheme == "http") && Host(icon).Equals(Host(site), StringComparison.OrdinalIgnoreCase))
		{
			if (icon.Port != site.Port)
			{
				if (icon.IsDefaultPort)
				{
					return site.IsDefaultPort;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static string Host(Uri uri)
	{
		if (!uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
		{
			return uri.Host;
		}
		return uri.Host.Substring(4);
	}

	private static byte[] Fetch(Uri site, Uri uri, Stopwatch clock, int limit)
	{
		for (int i = 0; i < 4; i++)
		{
			if (!SameSite(site, uri) || !AppPreferences.Instance.ShowWebsiteIconsInLinks)
			{
				throw new WebException("Website icons disabled or foreign host");
			}
			int num = 5000 - (int)clock.ElapsedMilliseconds;
			if (num <= 0)
			{
				throw new WebException("Icon timeout");
			}
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
			request.Timeout = num;
			request.ReadWriteTimeout = num;
			request.AllowAutoRedirect = false;
			request.UserAgent = "Ayn";
			request.CookieContainer = null;
			request.UseDefaultCredentials = false;
			using (new System.Threading.Timer(delegate
			{
				request.Abort();
			}, null, num, -1))
			{
				using HttpWebResponse httpWebResponse = (HttpWebResponse)request.GetResponse();
				if (httpWebResponse.StatusCode >= HttpStatusCode.MultipleChoices && httpWebResponse.StatusCode < HttpStatusCode.BadRequest)
				{
					uri = new Uri(uri, httpWebResponse.Headers["Location"]);
					continue;
				}
				using Stream stream = httpWebResponse.GetResponseStream();
				using MemoryStream memoryStream = new MemoryStream();
				byte[] array = new byte[8192];
				int num2;
				while ((num2 = stream.Read(array, 0, array.Length)) > 0)
				{
					if (clock.ElapsedMilliseconds >= 5000 || memoryStream.Length + num2 > limit)
					{
						throw new WebException("Icon size/timeout limit");
					}
					memoryStream.Write(array, 0, num2);
				}
				return memoryStream.ToArray();
			}
		}
		throw new WebException("Too many icon redirects");
	}

	public static ImageSource Decode(byte[] bytes)
	{
		try
		{
			using MemoryStream bitmapStream = new MemoryStream(bytes);
			BitmapDecoder bitmapDecoder = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			BitmapFrame bitmapFrame = bitmapDecoder.Frames.OrderBy((BitmapFrame f) => Math.Abs(f.PixelWidth - 96)).First();
			bitmapFrame.Freeze();
			return bitmapFrame;
		}
		catch
		{
			return Svg(bytes);
		}
	}

	private static ImageSource Svg(byte[] bytes)
	{
		try
		{
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.XmlResolver = null;
			XmlDocument xmlDocument2 = xmlDocument;
			using (XmlReader reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null
			}))
			{
				xmlDocument2.Load(reader);
			}
			if (xmlDocument2.DocumentElement.LocalName != "svg")
			{
				return null;
			}
			DrawingGroup drawingGroup = new DrawingGroup();
			DrawSvg(xmlDocument2.DocumentElement, drawingGroup, Brushes.Black);
			if (drawingGroup.Children.Count == 0)
			{
				return null;
			}
			DrawingImage drawingImage = new DrawingImage(drawingGroup);
			drawingImage.Freeze();
			return drawingImage;
		}
		catch
		{
			return null;
		}
	}

	private static void DrawSvg(XmlElement node, DrawingGroup group, Brush inherited)
	{
		Brush brush = inherited;
		string attribute = node.GetAttribute("fill");
		if (attribute == "none")
		{
			brush = null;
		}
		else if (attribute.Length > 0 && attribute != "currentColor")
		{
			try
			{
				brush = (Brush)new BrushConverter().ConvertFromInvariantString(attribute);
			}
			catch
			{
			}
		}
		DrawingGroup drawingGroup = new DrawingGroup();
		string attribute2 = node.GetAttribute("transform");
		if (attribute2.Length > 0)
		{
			try
			{
				drawingGroup.Transform = Transform.Parse(attribute2);
			}
			catch
			{
			}
		}
		Geometry geometry = null;
		if (node.LocalName == "path")
		{
			geometry = Geometry.Parse(node.GetAttribute("d"));
		}
		else if (node.LocalName == "circle")
		{
			geometry = new EllipseGeometry(new Point(Number(node, "cx"), Number(node, "cy")), Number(node, "r"), Number(node, "r"));
		}
		else if (node.LocalName == "rect")
		{
			geometry = new RectangleGeometry(new Rect(Number(node, "x"), Number(node, "y"), Number(node, "width"), Number(node, "height")), Number(node, "rx"), Number(node, "rx"));
		}
		if (geometry != null)
		{
			drawingGroup.Children.Add(new GeometryDrawing(brush, null, geometry));
		}
		foreach (XmlNode childNode in node.ChildNodes)
		{
			if (childNode is XmlElement)
			{
				DrawSvg((XmlElement)childNode, drawingGroup, brush);
			}
		}
		if (drawingGroup.Children.Count > 0)
		{
			group.Children.Add(drawingGroup);
		}
	}

	private static double Number(XmlElement e, string name)
	{
		if (!double.TryParse(e.GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return 0.0;
		}
		return result;
	}
}
