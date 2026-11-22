using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;

namespace WindowsBuddy.UI.Settings;

public static class SettingsNavQa
{
	private sealed class Shot
	{
		public int W;

		public int H;

		public int Stride;

		public byte[] Px;
	}

	[DllImport("gdi32.dll")]
	private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);

	[DllImport("user32.dll")]
	private static extern IntPtr GetDC(IntPtr h);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(IntPtr h, IntPtr dc);

	private static Shot Grab(int x, int y, int w, int h)
	{
		using Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			IntPtr hdc = graphics.GetHdc();
			IntPtr dC = GetDC(IntPtr.Zero);
			BitBlt(hdc, 0, 0, w, h, dC, x, y, 1087111200);
			ReleaseDC(IntPtr.Zero, dC);
			graphics.ReleaseHdc(hdc);
		}
		BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		Shot shot = new Shot();
		shot.W = w;
		shot.H = h;
		shot.Stride = bitmapData.Stride;
		shot.Px = new byte[bitmapData.Stride * h];
		Shot shot2 = shot;
		Marshal.Copy(bitmapData.Scan0, shot2.Px, 0, shot2.Px.Length);
		bitmap.UnlockBits(bitmapData);
		return shot2;
	}

	private static double Diff(Shot a, Shot b, int x0, int y0)
	{
		long num = 0L;
		long num2 = 0L;
		for (int i = y0; i < a.H; i += 2)
		{
			for (int j = x0; j < a.W; j += 2)
			{
				int num3 = i * a.Stride + j * 4;
				num2++;
				if (Math.Abs(a.Px[num3] - b.Px[num3]) > 24 || Math.Abs(a.Px[num3 + 1] - b.Px[num3 + 1]) > 24 || Math.Abs(a.Px[num3 + 2] - b.Px[num3 + 2]) > 24)
				{
					num++;
				}
			}
		}
		if (num2 != 0)
		{
			return (double)num / (double)num2;
		}
		return 0.0;
	}

	private static void Save(Shot s, string path)
	{
		using Bitmap bitmap = new Bitmap(s.W, s.H, PixelFormat.Format32bppArgb);
		BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, s.W, s.H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
		Marshal.Copy(s.Px, 0, bitmapData.Scan0, s.Px.Length);
		bitmap.UnlockBits(bitmapData);
		bitmap.Save(path, ImageFormat.Png);
	}

	private static void PumpOnce()
	{
		DispatcherFrame f = new DispatcherFrame();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			f.Continue = false;
		});
		Dispatcher.PushFrame(f);
		Render();
	}

	private static void Render()
	{
		Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, (Action)delegate
		{
		});
	}

	private static void Pump(int ms)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ms);
		do
		{
			DispatcherFrame f = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
			{
				f.Continue = false;
			});
			Dispatcher.PushFrame(f);
			Thread.Sleep(4);
		}
		while (DateTime.UtcNow < dateTime);
	}

	public static int Run(string[] args)
	{
		AppPaths.UseTempFolder("settings-nav-qa");
		int cycles = 50;
		if (args.Length > 1)
		{
			int.TryParse(args[1], out cycles);
		}
		string outDir = ((args.Length > 2) ? args[2] : Path.Combine(Path.GetTempPath(), "WindowsBuddyQA", "settings-nav-qa"));
		try
		{
			Directory.CreateDirectory(outDir);
		}
		catch
		{
		}
		string environmentVariable = Environment.GetEnvironmentVariable("VAYLI_QA_THEME");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			NotchConfig.Instance.AppTheme = environmentVariable;
		}
		int failures = 0;
		int flashes = 0;
		int slow = 0;
		int switches = 0;
		double worst = 0.0;
		double sum = 0.0;
		string worstPage = "";
		Dictionary<string, double> perPage = new Dictionary<string, double>();
		int saved = 0;
		Application app = new Application
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown
		};
		app.Startup += delegate
		{
			try
			{
				CapsuleWindow.RegisterDockTools(ToolRegistry.Instance);
				SettingsLauncher.Open("general");
				SettingsWindow current = SettingsWindow.Current;
				current.Topmost = true;
				current.Activate();
				Pump(6000);
				List<string> list = PageInfo.All.Select((PageInfo p) => p.Key).ToList();
				double m = PresentationSource.FromVisual(current).CompositionTarget.TransformToDevice.M11;
				System.Windows.Point point = current.PointToScreen(new System.Windows.Point(0.0, 0.0));
				int num = (int)point.X;
				int num2 = (int)point.Y;
				int num3 = (int)(current.ActualWidth * m);
				int num4 = (int)(current.ActualHeight * m);
				int x = (int)(248.0 * m);
				int y = (int)(44.0 * m);
				Console.WriteLine("SETTINGS-NAV-QA: " + list.Count + " pages x " + cycles + " cycles, window " + num3 + "x" + num4 + " px at " + num + "," + num2);
				string text = "general";
				for (int i = 0; i < cycles; i++)
				{
					foreach (string item in list)
					{
						if (!(item == text))
						{
							Render();
							Shot b = Grab(num, num2, num3, num4);
							Stopwatch stopwatch = Stopwatch.StartNew();
							current.Navigate(item);
							Render();
							double totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
							List<Shot> list2 = new List<Shot> { Grab(num, num2, num3, num4) };
							while (stopwatch.ElapsedMilliseconds < 160)
							{
								Render();
								list2.Add(Grab(num, num2, num3, num4));
								Thread.Sleep(2);
							}
							Pump(60);
							Shot b2 = Grab(num, num2, num3, num4);
							text = item;
							switches++;
							sum += totalMilliseconds;
							if (totalMilliseconds > worst)
							{
								worst = totalMilliseconds;
								worstPage = item;
							}
							perPage.TryGetValue(item, out var value);
							if (totalMilliseconds > value)
							{
								perPage[item] = totalMilliseconds;
							}
							if (totalMilliseconds >= 50.0)
							{
								slow++;
							}
							int num5 = 0;
							double num6 = 0.0;
							Shot shot = null;
							foreach (Shot item2 in list2)
							{
								double num7 = Diff(item2, b, x, y);
								double num8 = Diff(item2, b2, x, y);
								if (num7 > 0.03 && num8 > 0.03)
								{
									num5++;
									double num9 = Math.Min(num7, num8);
									if (num9 > num6)
									{
										num6 = num9;
										shot = item2;
									}
								}
							}
							if (num5 > 0)
							{
								flashes++;
								if (saved < 6 && shot != null)
								{
									saved++;
									try
									{
										Save(shot, Path.Combine(outDir, "flash_" + saved + "_" + item + ".png"));
									}
									catch
									{
									}
								}
								Console.WriteLine("  FLASH on switch to " + item + ": " + num5 + " of " + list2.Count + " frames match neither page (worst " + (int)(num6 * 100.0) + "% different)");
							}
						}
					}
				}
				Console.WriteLine($"  {switches} switches, average {sum / (double)Math.Max(1, switches):0.0} ms, worst {worst:0.0} ms ({worstPage})");
				foreach (KeyValuePair<string, double> item3 in perPage.OrderByDescending((KeyValuePair<string, double> k) => k.Value))
				{
					Console.WriteLine($"    worst {item3.Key,-12} {item3.Value,6:0.0} ms");
				}
				Console.WriteLine(((flashes == 0) ? "  PASS " : "  FAIL ") + "no flash: " + flashes + " switches showed a frame that matched neither the old nor the new page");
				Console.WriteLine(((slow == 0) ? "  PASS " : "  FAIL ") + "every switch under 50 ms: " + slow + " of " + switches + " took 50 ms or more");
				if (flashes > 0)
				{
					failures++;
				}
				if (slow > 0)
				{
					failures++;
				}
				current.Topmost = false;
				current.Close();
			}
			catch (Exception ex)
			{
				Console.WriteLine("SETTINGS-NAV-QA exception: " + ex);
				failures++;
			}
			finally
			{
				app.Shutdown((failures != 0) ? 1 : 0);
			}
		};
		app.Run();
		Console.WriteLine((failures == 0) ? "SETTINGS-NAV-QA PASSED" : "SETTINGS-NAV-QA FAILED");
		if (failures != 0)
		{
			return 1;
		}
		return 0;
	}

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x, int y);

	private static double Luma(Shot s, int x0, int y0, int x1, int y1)
	{
		long num = 0L;
		long num2 = 0L;
		for (int i = y0; i < y1; i += 2)
		{
			for (int j = x0; j < x1; j += 2)
			{
				int num3 = i * s.Stride + j * 4;
				num += (s.Px[num3] + s.Px[num3 + 1] + s.Px[num3 + 2]) / 3;
				num2++;
			}
		}
		if (num2 != 0)
		{
			return (double)num / (double)num2;
		}
		return 0.0;
	}

	public static int RunHover(string[] args)
	{
		AppPaths.UseTempFolder("settings-hover-qa");
		string outDir = ((args.Length > 1) ? args[1] : Path.Combine(Path.GetTempPath(), "WindowsBuddyQA", "settings-hover-qa"));
		try
		{
			Directory.CreateDirectory(outDir);
		}
		catch
		{
		}
		string environmentVariable = Environment.GetEnvironmentVariable("VAYLI_QA_THEME");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			NotchConfig.Instance.AppTheme = environmentVariable;
		}
		int failures = 0;
		Application app = new Application
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown
		};
		app.Startup += delegate
		{
			try
			{
				CapsuleWindow.RegisterDockTools(ToolRegistry.Instance);
				SettingsLauncher.Open("live");
				SettingsWindow current = SettingsWindow.Current;
				current.Topmost = true;
				current.Activate();
				SetCursorPos(0, 0);
				Pump(5000);
				double m = PresentationSource.FromVisual(current).CompositionTarget.TransformToDevice.M11;
				System.Windows.Point point = current.PointToScreen(new System.Windows.Point(0.0, 0.0));
				int num = (int)point.X;
				int num2 = (int)point.Y;
				int num3 = (int)(current.ActualWidth * m);
				int num4 = (int)(current.ActualHeight * m);
				List<FrameworkElement> navRowElements = current.NavRowElements;
				List<int[]> list = new List<int[]>();
				foreach (FrameworkElement item in navRowElements)
				{
					System.Windows.Point point2 = item.TransformToAncestor(current).Transform(new System.Windows.Point(0.0, 0.0));
					list.Add(new int[4]
					{
						(int)(point2.X * m) + 4,
						(int)(point2.Y * m) + 4,
						(int)((point2.X + item.ActualWidth) * m) - 4,
						(int)((point2.Y + item.ActualHeight) * m) - 4
					});
				}
				List<int[]> list2 = new List<int[]>();
				foreach (int[] item2 in list)
				{
					int num5 = (item2[1] + item2[3]) / 2;
					int num6 = (item2[3] - item2[1]) / 4;
					list2.Add(new int[4]
					{
						item2[2] - 70,
						num5 - num6,
						item2[2] - 12,
						num5 + num6
					});
				}
				int x = (int)(248.0 * m);
				Render();
				Pump(300);
				Shot shot = Grab(num, num2, num3, num4);
				Console.WriteLine("SETTINGS-HOVER-QA: " + navRowElements.Count + " sidebar items, cursor path across each item and back, no clicks (window " + num3 + "x" + num4 + " px)");
				List<int[]> list3 = new List<int[]>();
				for (int i = 0; i < list.Count; i++)
				{
					int num7 = (list[i][1] + list[i][3]) / 2;
					int num8 = 0;
					for (int j = list[i][0] + 6; j < list[i][2] - 6; j += 7)
					{
						list3.Add(new int[4]
						{
							num + j,
							num2 + num7,
							i,
							0
						});
						num8 = j;
					}
					for (int k = 0; k < 12; k++)
					{
						list3.Add(new int[4]
						{
							num + num8,
							num2 + num7,
							i,
							0
						});
					}
				}
				for (int num9 = list.Count - 1; num9 >= 0; num9--)
				{
					int num10 = (list[num9][1] + list[num9][3]) / 2;
					for (int num11 = list[num9][2] - 6; num11 > list[num9][0] + 6; num11 -= 7)
					{
						list3.Add(new int[4]
						{
							num + num11,
							num2 + num10,
							num9,
							1
						});
					}
				}
				int num12 = 0;
				for (int l = 0; l < list.Count; l++)
				{
					num12 += (int)Math.Ceiling((double)(list[l][2] - list[l][0] - 12) / 7.0);
				}
				List<double>[] array = new List<double>[navRowElements.Count];
				double[] array2 = new double[navRowElements.Count];
				double[] array3 = new double[navRowElements.Count];
				for (int n = 0; n < navRowElements.Count; n++)
				{
					array[n] = new List<double>();
					array2[n] = 0.0;
					array3[n] = 255.0;
				}
				int num13 = 0;
				int num14 = 0;
				int num15 = 0;
				int num16 = 0;
				double num17 = 0.0;
				string text = "";
				double[] array4 = new double[navRowElements.Count];
				bool flag = false;
				Stopwatch stopwatch = Stopwatch.StartNew();
				long num18 = 0L;
				foreach (int[] item3 in list3)
				{
					SetCursorPos(item3[0], item3[1]);
					while (stopwatch.ElapsedMilliseconds < num18)
					{
						Thread.Sleep(1);
					}
					num18 += 16;
					PumpOnce();
					Shot shot2 = Grab(num, num2, num3, num4);
					num13++;
					int num19 = item3[2];
					double num20 = Diff(shot2, shot, x, (int)(44.0 * m));
					if (num20 > 0.002)
					{
						num15++;
						if (!flag)
						{
							flag = true;
							try
							{
								Save(shot2, Path.Combine(outDir, "hover_content_changed.png"));
							}
							catch
							{
							}
						}
					}
					for (int num21 = 0; num21 < navRowElements.Count; num21++)
					{
						double num22 = Luma(shot2, list2[num21][0], list2[num21][1], list2[num21][2], list2[num21][3]);
						double num23 = Luma(shot, list2[num21][0], list2[num21][1], list2[num21][2], list2[num21][3]);
						if (Math.Abs(num21 - num19) > 1 && Math.Abs(num22 - num23) > 1.5)
						{
							num14++;
							double num24 = Math.Abs(num22 - num23);
							if (num24 > num17)
							{
								num17 = num24;
								text = "row " + num21 + " while on " + num19;
							}
						}
						if (num21 == num19 && item3[3] == 0)
						{
							array[num21].Add(num22);
						}
						if (num21 == num19)
						{
							if (num22 > array2[num21])
							{
								array2[num21] = num22;
							}
							if (num22 < array3[num21])
							{
								array3[num21] = num22;
							}
						}
						array4[num21] = num22;
					}
				}
				double[] array5 = new double[navRowElements.Count];
				for (int num25 = 0; num25 < navRowElements.Count; num25++)
				{
					array5[num25] = Luma(shot, list2[num25][0], list2[num25][1], list2[num25][2], list2[num25][3]);
				}
				int num26 = 0;
				string text2 = "";
				double num27 = 0.0;
				for (int num28 = 0; num28 < navRowElements.Count; num28++)
				{
					List<double> list4 = array[num28];
					if (list4.Count >= 4)
					{
						double val = list4[list4.Count - 1];
						double num29 = Math.Max(array5[num28], val) + 6.0;
						double num30 = Math.Min(array5[num28], val) - 6.0;
						foreach (double item4 in list4)
						{
							double num31 = item4;
							if (num31 > num29 || num31 < num30)
							{
								num26++;
								double num32 = Math.Max(num31 - num29, num30 - num31) + 6.0;
								if (num32 > num27)
								{
									num27 = num32;
									text2 = "item " + num28 + " (rest " + array5[num28].ToString("0") + ", steady " + val.ToString("0") + ")";
								}
							}
						}
						int num33 = 0;
						int num34 = 0;
						for (int num35 = 1; num35 < list4.Count; num35++)
						{
							double num36 = list4[num35] - list4[num35 - 1];
							if (!(Math.Abs(num36) < 1.5))
							{
								int num37 = ((num36 > 0.0) ? 1 : (-1));
								if (num34 != 0 && num37 != num34)
								{
									num33++;
								}
								num34 = num37;
							}
						}
						if (num33 > 1)
						{
							num16++;
						}
					}
				}
				int num38 = PageInfo.All.Select((PageInfo p) => p.Key).ToList().IndexOf("live");
				SetCursorPos(0, 0);
				Render();
				Pump(400);
				Shot s2 = Grab(num, num2, num3, num4);
				double num39 = -1.0;
				double num40 = -1.0;
				if (num38 >= 0 && num38 < list.Count)
				{
					num39 = Luma(s2, list2[num38][0], list2[num38][1], list2[num38][2], list2[num38][3]);
					int num41 = (list[num38][1] + list[num38][3]) / 2;
					SetCursorPos(num + list[num38][2] - 10, num2 + num41);
					Render();
					Pump(400);
					Shot s3 = Grab(num, num2, num3, num4);
					num40 = Luma(s3, list2[num38][0], list2[num38][1], list2[num38][2], list2[num38][3]);
					try
					{
						Save(s3, Path.Combine(outDir, "hover_selected.png"));
						Save(s2, Path.Combine(outDir, "rest_selected.png"));
					}
					catch
					{
					}
				}
				SetCursorPos(0, 0);
				Console.WriteLine("  " + num13 + " frames at about 60 fps; selected row brightness " + num39.ToString("0.0") + " at rest, " + num40.ToString("0.0") + " hovered");
				Console.WriteLine(((num15 == 0) ? "  PASS " : "  FAIL ") + "content column untouched by sidebar hover: " + num15 + " frames changed");
				Console.WriteLine(((num14 == 0) ? "  PASS " : "  FAIL ") + "non-hovered rows untouched: " + num14 + " row-frames changed" + ((num14 > 0) ? (" (worst " + num17.ToString("0.0") + " levels, " + text + ")") : ""));
				Console.WriteLine(((num16 == 0) ? "  PASS " : "  FAIL ") + "hover fill fades one way, no enter/leave flicker: " + num16 + " items with brightness reversals");
				Console.WriteLine(((num26 == 0) ? "  PASS " : "  FAIL ") + "hover fade never passes through another colour: " + num26 + " frames outside rest..steady" + ((num26 > 0) ? (" (worst +" + num27.ToString("0") + " levels at " + text2 + ")") : ""));
				bool flag2 = num39 < 0.0 || Math.Abs(num40 - num39) <= 20.0;
				Console.WriteLine((flag2 ? "  PASS " : "  FAIL ") + "selected + hover stay soft (the selected fill is #FFFFFF14, hover does not stack on it)");
				if (num15 > 0 || num14 > 0 || num16 > 0 || num26 > 0 || !flag2)
				{
					failures++;
				}
				current.Topmost = false;
				current.Close();
			}
			catch (Exception ex)
			{
				Console.WriteLine("SETTINGS-HOVER-QA exception: " + ex);
				failures++;
			}
			finally
			{
				app.Shutdown((failures != 0) ? 1 : 0);
			}
		};
		app.Run();
		Console.WriteLine((failures == 0) ? "SETTINGS-HOVER-QA PASSED" : "SETTINGS-HOVER-QA FAILED");
		if (failures != 0)
		{
			return 1;
		}
		return 0;
	}
}
