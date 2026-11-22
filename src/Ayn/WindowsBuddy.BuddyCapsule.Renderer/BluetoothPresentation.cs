using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services.Bluetooth;
using WindowsBuddy.Services.Pet;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal static class BluetoothPresentation
{
	internal sealed class DeviceVisual
	{
		public string Id;

		public string Name;

		public string Category;

		public string Source;

		public BitmapSource Image;

		public int? Battery;
	}

	private sealed class Entry
	{
		public DateTime Expires;

		public Task<DeviceVisual> Task;
	}

	private static readonly Dictionary<string, Entry> Names = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, DeviceVisual> Devices = new Dictionary<string, DeviceVisual>(StringComparer.OrdinalIgnoreCase);

	internal static Func<string, Task<DeviceVisual>> LoaderOverride;

	internal static string LastWindowsReadError;

	private static readonly string[] Properties = new string[8] { "System.Devices.BatteryLife", "System.Devices.Category", "System.Devices.ClassGuid", "System.Devices.ModelName", "System.Devices.Aep.Category", "System.Devices.AepContainer.Categories", "System.Devices.Aep.Bluetooth.Cod.Major", "System.Devices.Aep.Bluetooth.Cod.Minor" };

	private static Type WinRt(string name, string contract)
	{
		return Type.GetType(name + ", " + contract + ", ContentType=WindowsRuntime");
	}

	private static object Get(object value, string name)
	{
		return value.GetType().GetProperty(name).GetValue(value, null);
	}

	private static async Task<object> Await(object operation, Type result)
	{
		Assembly assembly = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
		MethodInfo method = assembly.GetType("System.WindowsRuntimeSystemExtensions").GetMethods().First((MethodInfo m) => m.Name == "AsTask" && m.IsGenericMethod && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.Name.StartsWith("IAsyncOperation`"));
		Task task = (Task)method.MakeGenericMethod(result).Invoke(null, new object[1] { operation });
		if (await Task.WhenAny(task, Task.Delay(4000)).ConfigureAwait(continueOnCapturedContext: false) != task)
		{
			try
			{
				Type type = WinRt("Windows.Foundation.IAsyncInfo", "Windows.Foundation");
				type.GetMethod("Cancel").Invoke(operation, null);
			}
			catch
			{
			}
			task.ContinueWith(delegate(Task t)
			{
				_ = t.Exception;
			}, TaskContinuationOptions.OnlyOnFaulted);
			throw new TimeoutException("Device metadata lookup timed out");
		}
		await task.ConfigureAwait(continueOnCapturedContext: false);
		return task.GetType().GetProperty("Result").GetValue(task, null);
	}

	internal static int? ReadBattery(IDictionary<string, object> properties)
	{
		if (properties == null || !properties.TryGetValue("System.Devices.BatteryLife", out var value) || value == null || value is bool || !int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var result) || result < 0 || result > 100)
		{
			return null;
		}
		return result;
	}

	internal static string Fallback(string name, string metadata = null)
	{
		string text = (name + " " + metadata).ToLowerInvariant();
		if (text.Contains("phone") && !text.Contains("headphone") && !text.Contains("earphone"))
		{
			return "phone";
		}
		if (text.Contains("watch") || text.Contains("wearable"))
		{
			return "watch";
		}
		switch (BluetoothService.ClassifyDevice(text))
		{
		case DeviceCategory.Earbuds:
		case DeviceCategory.Headphones:
			return "headphones";
		case DeviceCategory.Speaker:
			return "speaker";
		case DeviceCategory.Mouse:
			return "mouse";
		case DeviceCategory.Keyboard:
			return "keyboard";
		case DeviceCategory.Controller:
			return "controller";
		default:
			return "bluetooth";
		}
	}

	internal static string DeviceClassFallback(string name, IDictionary<string, object> properties, string metadata = null)
	{
		if (properties != null && properties.TryGetValue("System.Devices.Aep.Bluetooth.Cod.Major", out var value) && properties.TryGetValue("System.Devices.Aep.Bluetooth.Cod.Minor", out var value2) && int.TryParse(Convert.ToString(value), out var result) && int.TryParse(Convert.ToString(value2), out var result2))
		{
			switch (result)
			{
			case 2:
				return "phone";
			case 4:
				switch (result2)
				{
				case 1:
				case 2:
				case 6:
					return "headphones";
				case 5:
				case 7:
				case 8:
				case 10:
					return "speaker";
				}
				break;
			}
			if (result == 5)
			{
				if (((uint)result2 & 0x20u) != 0)
				{
					return "mouse";
				}
				if (((uint)result2 & 0x10u) != 0)
				{
					return "keyboard";
				}
				if ((result2 & 0xF) == 1 || (result2 & 0xF) == 2)
				{
					return "controller";
				}
			}
			if ((result == 7 && result2 == 1) || (result == 1 && result2 == 6))
			{
				return "watch";
			}
		}
		return Fallback(name, metadata);
	}

	private static bool Match(string requested, string actual)
	{
		string text = BluetoothService.SanitizeDeviceName(requested).TrimEnd('…').Trim();
		string text2 = BluetoothService.SanitizeDeviceName(actual).TrimEnd('…').Trim();
		if (text.Length >= 3 && text2.Length >= 3)
		{
			if (!text.Equals(text2, StringComparison.OrdinalIgnoreCase) && !text.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
			{
				return text2.StartsWith(text, StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	internal static async Task<List<DeviceVisual>> ReadWindowsDevices(string requested = null)
	{
		List<DeviceVisual> found = new List<DeviceVisual>();
		Type information = WinRt("Windows.Devices.Enumeration.DeviceInformation", "Windows.Devices");
		Type collection = WinRt("Windows.Devices.Enumeration.DeviceInformationCollection", "Windows.Devices");
		Type kind = WinRt("Windows.Devices.Enumeration.DeviceInformationKind", "Windows.Devices");
		LastWindowsReadError = null;
		if (information == null || collection == null || kind == null)
		{
			LastWindowsReadError = "Windows device enumeration is unavailable";
			return found;
		}
		MethodInfo find = information.GetMethods().First((MethodInfo m) => m.Name == "FindAllAsync" && m.GetParameters().Length == 3 && m.GetParameters()[2].ParameterType == kind);
		int num = default(int);
		string[] array = default(string[]);
		int num2 = default(int);
		string provider = default(string);
		switch (num)
		{
		default:
			array = new string[2] { "BluetoothDevice", "BluetoothLEDevice" };
			num2 = 0;
			goto IL_0852;
		case 0:
		case 1:
			{
				try
				{
					Type bluetooth = WinRt("Windows.Devices.Bluetooth." + provider, "Windows.Devices");
					string selector = (string)bluetooth.GetMethod("GetDeviceSelector", Type.EmptyTypes).Invoke(null, null);
					IEnumerable enumerable = (IEnumerable)(await Await(find.Invoke(null, new object[3]
					{
						selector,
						Properties,
						Enum.Parse(kind, "AssociationEndpoint")
					}), collection).ConfigureAwait(continueOnCapturedContext: false));
					IEnumerable devices = enumerable;
					string[] array2 = default(string[]);
					int num3 = default(int);
					string thumbnailMethod = default(string);
					foreach (object device in devices)
					{
						string id = (string)Get(device, "Id");
						string name = ((string)Get(device, "Name")) ?? "Bluetooth device";
						if (found.Any((DeviceVisual d) => d.Id == id) || (requested != null && !Match(requested, name)))
						{
							continue;
						}
						object propertyBag = Get(device, "Properties");
						IDictionary<string, object> properties = propertyBag as IDictionary<string, object>;
						IReadOnlyDictionary<string, object> readOnly = propertyBag as IReadOnlyDictionary<string, object>;
						if (properties == null && readOnly != null)
						{
							properties = readOnly.ToDictionary((KeyValuePair<string, object> p) => p.Key, (KeyValuePair<string, object> p) => p.Value);
						}
						string metadata = ((properties == null) ? "" : string.Join(" ", from p in properties
							where p.Key != "System.Devices.BatteryLife"
							select (!(p.Value is string)) ? ((!(p.Value is IEnumerable)) ? Convert.ToString(p.Value) : string.Join(" ", ((IEnumerable)p.Value).Cast<object>().Select(Convert.ToString))) : Convert.ToString(p.Value)));
						DeviceVisual visual = new DeviceVisual
						{
							Id = id,
							Name = name,
							Category = DeviceClassFallback(name, properties, metadata),
							Battery = ReadBattery(properties),
							Source = "device class fallback"
						};
						bool airpods = (name + " " + metadata).IndexOf("airpod", StringComparison.OrdinalIgnoreCase) >= 0;
						if (!airpods)
						{
							lock (Devices)
							{
								if (Devices.TryGetValue(id, out var value))
								{
									visual.Image = value.Image;
									visual.Source = value.Source;
								}
							}
						}
						if (visual.Image == null && !airpods)
						{
							if (num == 1)
							{
								goto IL_05d3;
							}
							array2 = new string[2] { "GetThumbnailAsync", "GetGlyphThumbnailAsync" };
							num3 = 0;
							goto IL_07b0;
						}
						goto IL_07c6;
						IL_05d3:
						try
						{
							object thumbnail = await Await(result: WinRt("Windows.Devices.Enumeration.DeviceThumbnail", "Windows.Devices"), operation: information.GetMethod(thumbnailMethod).Invoke(device, null)).ConfigureAwait(continueOnCapturedContext: false);
							try
							{
								Type type = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089").GetType("System.IO.WindowsRuntimeStreamExtensions");
								MethodInfo methodInfo = type.GetMethods().First((MethodInfo m) => m.Name == "AsStream" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.Name == "IRandomAccessStream");
								using Stream stream = (Stream)methodInfo.Invoke(null, new object[1] { thumbnail });
								if (stream.Length != 0)
								{
									BitmapImage bitmapImage = new BitmapImage();
									bitmapImage.BeginInit();
									bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
									bitmapImage.DecodePixelWidth = 96;
									bitmapImage.StreamSource = stream;
									bitmapImage.EndInit();
									bitmapImage.Freeze();
									visual.Image = bitmapImage;
									visual.Source = "Windows DeviceInformation." + thumbnailMethod;
									goto IL_078e;
								}
							}
							finally
							{
								if (thumbnail is IDisposable disposable)
								{
									disposable.Dispose();
								}
							}
							goto end_IL_05e0;
							IL_078e:
							if (visual.Image != null)
							{
								goto IL_07c6;
							}
							end_IL_05e0:;
						}
						catch
						{
						}
						num3++;
						goto IL_07b0;
						IL_07b0:
						if (num3 < array2.Length)
						{
							thumbnailMethod = array2[num3];
							goto IL_05d3;
						}
						goto IL_07c6;
						IL_07c6:
						found.Add(visual);
					}
				}
				catch (Exception ex)
				{
					LastWindowsReadError = ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message;
				}
				num2++;
				goto IL_0852;
			}
			IL_0852:
			if (num2 < array.Length)
			{
				provider = array[num2];
				goto case 0;
			}
			return found;
		}
	}

	internal static Task<DeviceVisual> Load(string name)
	{
		name = name ?? "Bluetooth device";
		lock (Names)
		{
			if (Names.TryGetValue(name, out var value) && value.Expires > DateTime.UtcNow)
			{
				return value.Task;
			}
			if (Names.Count >= 64)
			{
				Names.Clear();
			}
			Task<DeviceVisual> task = ((LoaderOverride != null) ? LoaderOverride(name) : Task.Run(async delegate
			{
				DeviceVisual result = (await ReadWindowsDevices(name).ConfigureAwait(continueOnCapturedContext: false)).FirstOrDefault((DeviceVisual d) => Match(name, d.Name));
				if (result != null)
				{
					lock (Devices)
					{
						if (Devices.Count >= 64)
						{
							Devices.Clear();
						}
						if (Devices.TryGetValue(result.Id, out var value2) && result.Image == null)
						{
							result.Image = value2.Image;
						}
						Devices[result.Id] = result;
					}
				}
				return result ?? new DeviceVisual
				{
					Name = name,
					Category = Fallback(name),
					Source = "device class fallback"
				};
			}));
			Names[name] = new Entry
			{
				Task = task,
				Expires = DateTime.UtcNow.AddMinutes(2.0)
			};
			return task;
		}
	}

	internal static double BatteryRoom(string name, double scale)
	{
		name = name ?? "Bluetooth device";
		lock (Names)
		{
			Entry value;
			return (Names.TryGetValue(name, out value) && value.Task.Status == TaskStatus.RanToCompletion && value.Task.Result.Battery.HasValue) ? (52.0 * scale) : 0.0;
		}
	}

	internal static FrameworkElement Create(string name, string status, double scale, double height, double inset, Action updated)
	{
		double size = Math.Floor(Math.Min(22.0 * scale, height - 6.0 * scale));
		Grid host = new Grid
		{
			Margin = new Thickness(inset, 0.0, inset, 0.0),
			Height = height,
			VerticalAlignment = VerticalAlignment.Center
		};
		host.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		host.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		host.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		host.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		bool pet = PetPreferences.Instance.Enabled;
		ContentControl icon = new ContentControl
		{
			Width = size,
			Height = size,
			Content = (pet ? new AynRobotMark(size) : LiveMessageVisuals.Glyph(Fallback(name), size, LiveMessageVisuals.Blue)),
			Focusable = false
		};
		icon.Margin = new Thickness(0.0, 0.0, 10.0 * scale, 0.0);
		host.Children.Add(icon);
		TextBlock textBlock = new TextBlock();
		textBlock.Text = name;
		textBlock.FontSize = 15.0 * scale;
		textBlock.FontWeight = AynType.Semibold;
		textBlock.FontFamily = BuddyFonts.Geist;
		textBlock.Foreground = Brushes.White;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock textBlock2 = textBlock;
		LiveMessageVisuals.Crisp(textBlock2);
		Grid.SetColumn(textBlock2, 1);
		host.Children.Add(textBlock2);
		TextBlock textBlock3 = new TextBlock();
		textBlock3.Text = status;
		textBlock3.FontSize = 13.5 * scale;
		textBlock3.FontFamily = BuddyFonts.Geist;
		textBlock3.Foreground = LiveMessageVisuals.Brush(Colors.White, 179);
		textBlock3.VerticalAlignment = VerticalAlignment.Center;
		textBlock3.TextTrimming = TextTrimming.CharacterEllipsis;
		textBlock3.Margin = new Thickness(8.0 * scale, 0.0, 0.0, 0.0);
		TextBlock textBlock4 = textBlock3;
		LiveMessageVisuals.Crisp(textBlock4);
		Grid.SetColumn(textBlock4, 2);
		host.Children.Add(textBlock4);
		StackPanel battery = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(8.0 * scale, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		Grid.SetColumn(battery, 3);
		host.Children.Add(battery);
		Action<DeviceVisual> apply = delegate(DeviceVisual visual)
		{
			if (!pet)
			{
				if (visual.Image != null)
				{
					icon.Content = new Image
					{
						Source = visual.Image,
						Width = size,
						Height = size,
						Stretch = Stretch.Uniform,
						SnapsToDevicePixels = true
					};
				}
				else
				{
					icon.Content = LiveMessageVisuals.Glyph(visual.Category ?? Fallback(name), size, LiveMessageVisuals.Blue);
				}
			}
			AutomationProperties.SetName(icon, visual.Source ?? "Device class fallback");
			battery.Children.Clear();
			battery.Visibility = ((!visual.Battery.HasValue) ? Visibility.Collapsed : Visibility.Visible);
			if (visual.Battery.HasValue)
			{
				int value = visual.Battery.Value;
				Grid grid = new Grid
				{
					Width = 12.0 * scale,
					Height = 12.0 * scale,
					Margin = new Thickness(0.0, 0.0, 4.0 * scale, 0.0)
				};
				grid.Children.Add(new Ellipse
				{
					Stroke = LiveMessageVisuals.Brush(Colors.White, 50),
					StrokeThickness = 2.0 * scale
				});
				if (value > 0)
				{
					double num = Math.Min(359.9, (double)value * 3.6) * Math.PI / 180.0;
					PathFigure pathFigure = new PathFigure
					{
						StartPoint = new Point(6.0, 1.0)
					};
					pathFigure.Segments.Add(new ArcSegment(new Point(6.0 + 5.0 * Math.Sin(num), 6.0 - 5.0 * Math.Cos(num)), new Size(5.0, 5.0), 0.0, value > 50, SweepDirection.Clockwise, isStroked: true));
					PathGeometry pathGeometry = new PathGeometry(new PathFigure[1] { pathFigure });
					pathGeometry.Freeze();
					grid.Children.Add(new System.Windows.Shapes.Path
					{
						RenderTransform = new ScaleTransform(scale, scale),
						Data = pathGeometry,
						Stroke = LiveMessageVisuals.Brush((value <= 20) ? LiveMessageVisuals.Red : LiveMessageVisuals.Green),
						StrokeThickness = 2.0,
						StrokeStartLineCap = PenLineCap.Round,
						StrokeEndLineCap = PenLineCap.Round
					});
				}
				battery.Children.Add(grid);
				TextBlock textBlock5 = new TextBlock
				{
					Text = value + "%",
					FontSize = 11.0 * scale,
					FontFamily = BuddyFonts.Geist,
					Foreground = LiveMessageVisuals.Brush(Colors.White, 179),
					VerticalAlignment = VerticalAlignment.Center
				};
				LiveMessageVisuals.Crisp(textBlock5);
				battery.Children.Add(textBlock5);
			}
			if (updated != null)
			{
				updated();
			}
		};
		int generation = 0;
		host.Unloaded += delegate
		{
			generation++;
		};
		host.Loaded += async delegate
		{
			int current = ++generation;
			try
			{
				DeviceVisual visual2 = await Load(name);
				if (host.IsLoaded && current == generation)
				{
					apply(visual2);
				}
			}
			catch
			{
			}
		};
		return host;
	}
}
