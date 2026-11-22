using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.Core;

public static class ContrastScan
{
	public const double MinRatio = 3.0;

	private static Dictionary<Brush, string> _tokenNames;

	private static string TokenName(Brush b)
	{
		if (_tokenNames == null)
		{
			_tokenNames = new Dictionary<Brush, string>();
			FieldInfo[] fields = typeof(ThemeTokens).GetFields(BindingFlags.Static | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (fieldInfo.FieldType == typeof(SolidColorBrush) && fieldInfo.GetValue(null) is Brush key && !_tokenNames.ContainsKey(key))
				{
					_tokenNames[key] = fieldInfo.Name;
				}
			}
		}
		if (b == null || !_tokenNames.TryGetValue(b, out var value))
		{
			if (b != null)
			{
				return (b.IsFrozen ? "frozen " : "") + b.GetType().Name;
			}
			return "null";
		}
		return value;
	}

	public static List<string> Scan(Visual root, string where)
	{
		List<string> list = new List<string>();
		if (root == null)
		{
			return list;
		}
		Walk(root, where, list);
		return list;
	}

	private static void Walk(DependencyObject d, string where, List<string> fails)
	{
		if (!(d is UIElement uIElement) || (uIElement.IsVisible && !(uIElement.Opacity <= 0.01)))
		{
			if (d is TextBlock textBlock)
			{
				Check(textBlock, textBlock.Foreground, TextOf(textBlock), where, fails);
			}
			if (d is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
			{
				Check(textBox, textBox.Foreground, textBox.Text, where, fails);
			}
			int childrenCount = VisualTreeHelper.GetChildrenCount(d);
			for (int i = 0; i < childrenCount; i++)
			{
				Walk(VisualTreeHelper.GetChild(d, i), where, fails);
			}
		}
	}

	private static string TextOf(TextBlock tb)
	{
		if (!string.IsNullOrEmpty(tb.Text))
		{
			return tb.Text;
		}
		return string.Concat(from r in tb.Inlines.OfType<Run>()
			select r.Text);
	}

	private static void Check(FrameworkElement el, Brush ink, string text, string where, List<string> fails)
	{
		if (string.IsNullOrWhiteSpace(text) || el.ActualWidth < 1.0 || el.ActualHeight < 1.0 || !el.IsEnabled)
		{
			return;
		}
		double num = 1.0;
		for (DependencyObject dependencyObject = el; dependencyObject != null; dependencyObject = VisualTreeHelper.GetParent(dependencyObject))
		{
			if (dependencyObject is UIElement uIElement)
			{
				num *= uIElement.Opacity;
			}
		}
		Color top = ColorOf(ink);
		double num2 = (double)(int)top.A / 255.0 * num;
		if (!(num2 < 0.05))
		{
			Color color = BackgroundBehind(el);
			Color color2 = Blend(top, color, num2);
			double num3 = Ratio(color2, color);
			if (!(num3 >= 3.0))
			{
				string text2 = ((text.Length > 18) ? (text.Substring(0, 18) + "…") : text);
				fails.Add(string.Format("{0}: {1:F2}:1 \"{2}\" ink {3} ({4}) on {5} [{6}{7}]", where, num3, text2.Replace("\n", " "), Hex(color2), TokenName(ink), Hex(color), el.GetType().Name, string.IsNullOrEmpty(el.Name) ? "" : (" #" + el.Name)));
			}
		}
	}

	private static Color BackgroundBehind(FrameworkElement el)
	{
		List<Color> list = new List<Color>();
		Point point = new Point(el.ActualWidth / 2.0, el.ActualHeight / 2.0);
		DependencyObject dependencyObject = el;
		for (DependencyObject parent = VisualTreeHelper.GetParent(el); parent != null; parent = VisualTreeHelper.GetParent(parent))
		{
			if (parent is Panel panel)
			{
				int num = panel.Children.IndexOf(dependencyObject as UIElement);
				bool flag = false;
				for (int num2 = num - 1; num2 >= 0 && !flag; num2--)
				{
					FrameworkElement frameworkElement = panel.Children[num2] as FrameworkElement;
					if (frameworkElement == null || !frameworkElement.IsVisible || frameworkElement.Opacity < 0.05)
					{
						continue;
					}
					Brush brush = ((frameworkElement is Shape) ? ((Shape)frameworkElement).Fill : ((frameworkElement is Border) ? ((Border)frameworkElement).Background : null));
					Panel panel2 = frameworkElement as Panel;
					if (brush == null && panel2 != null && panel2.Background == null)
					{
						foreach (UIElement child in panel2.Children)
						{
							if (!(child is FrameworkElement { IsVisible: not false, Opacity: 0 } frameworkElement2))
							{
								continue;
							}
							Brush brush2 = ((frameworkElement2 is Shape) ? ((Shape)frameworkElement2).Fill : ((frameworkElement2 is Border) ? ((Border)frameworkElement2).Background : null));
							if (brush2 == null)
							{
								continue;
							}
							try
							{
								Point point2 = el.TransformToVisual(frameworkElement2).Transform(point);
								if (point2.X < 0.0 || point2.Y < 0.0 || point2.X > frameworkElement2.ActualWidth || point2.Y > frameworkElement2.ActualHeight)
								{
									continue;
								}
								goto IL_01df;
							}
							catch (InvalidOperationException)
							{
							}
							continue;
							IL_01df:
							frameworkElement = frameworkElement2;
							brush = brush2;
							break;
						}
					}
					if (brush == null)
					{
						continue;
					}
					try
					{
						Point point3 = el.TransformToVisual(frameworkElement).Transform(point);
						if (point3.X < 0.0 || point3.Y < 0.0 || point3.X > frameworkElement.ActualWidth || point3.Y > frameworkElement.ActualHeight)
						{
							continue;
						}
						goto IL_026e;
					}
					catch (InvalidOperationException)
					{
					}
					continue;
					IL_026e:
					Color item = ColorOf(brush);
					item.A = (byte)Math.Round((double)(int)item.A * brush.Opacity * frameworkElement.Opacity);
					if (item.A != 0)
					{
						list.Add(item);
						flag = item.A >= 250;
					}
				}
				if (flag)
				{
					break;
				}
			}
			Brush brush3 = null;
			if (parent is Panel)
			{
				brush3 = ((Panel)parent).Background;
			}
			else if (parent is Border)
			{
				brush3 = ((Border)parent).Background;
			}
			else if (parent is Control)
			{
				brush3 = ((Control)parent).Background;
			}
			if (brush3 != null)
			{
				Color item2 = ColorOf(brush3);
				item2.A = (byte)Math.Round((double)(int)item2.A * brush3.Opacity);
				if (item2.A != 0)
				{
					list.Add(item2);
					if (item2.A >= 250)
					{
						break;
					}
				}
			}
			dependencyObject = parent;
		}
		Color color = ThemeTokens.Target(ThemeTokens.Bg);
		for (int num3 = list.Count - 1; num3 >= 0; num3--)
		{
			color = Blend(list[num3], color, (double)(int)list[num3].A / 255.0);
		}
		return color;
	}

	private static Color ColorOf(Brush b)
	{
		if (b is SolidColorBrush solidColorBrush)
		{
			return solidColorBrush.Color;
		}
		if (b is GradientBrush gradientBrush && gradientBrush.GradientStops.Count > 0)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			foreach (GradientStop gradientStop in gradientBrush.GradientStops)
			{
				num += (double)(int)gradientStop.Color.R;
				num2 += (double)(int)gradientStop.Color.G;
				num3 += (double)(int)gradientStop.Color.B;
				num4 += (double)(int)gradientStop.Color.A;
			}
			int count = gradientBrush.GradientStops.Count;
			return Color.FromArgb((byte)(num4 / (double)count), (byte)(num / (double)count), (byte)(num2 / (double)count), (byte)(num3 / (double)count));
		}
		return Colors.Transparent;
	}

	private static Color Blend(Color top, Color under, double a)
	{
		Func<byte, byte, byte> func = (byte t, byte u) => (byte)Math.Round((double)(int)t * a + (double)(int)u * (1.0 - a));
		return Color.FromRgb(func(top.R, under.R), func(top.G, under.G), func(top.B, under.B));
	}

	private static double Lum(Color c)
	{
		Func<byte, double> func = delegate(byte v)
		{
			double num = (double)(int)v / 255.0;
			return (!(num <= 0.03928)) ? Math.Pow((num + 0.055) / 1.055, 2.4) : (num / 12.92);
		};
		return 0.2126 * func(c.R) + 0.7152 * func(c.G) + 0.0722 * func(c.B);
	}

	public static double Ratio(Color a, Color b)
	{
		double val = Lum(a);
		double val2 = Lum(b);
		return (Math.Max(val, val2) + 0.05) / (Math.Min(val, val2) + 0.05);
	}

	private static string Hex(Color c)
	{
		return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
	}
}
