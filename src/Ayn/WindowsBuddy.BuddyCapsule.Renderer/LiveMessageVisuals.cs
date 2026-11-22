using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Animation;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal static class LiveMessageVisuals
{
	public static readonly Color Blue = Color.FromRgb(10, 132, byte.MaxValue);

	public static readonly Color Green = Color.FromRgb(48, 209, 88);

	public static readonly Color Red = Color.FromRgb(byte.MaxValue, 69, 58);

	public static readonly Color Grey = Color.FromRgb(142, 142, 147);

	public static readonly Color Orange = Color.FromRgb(byte.MaxValue, 159, 10);

	public static readonly Color Purple = Color.FromRgb(191, 90, 242);

	public static readonly Color Teal = Color.FromRgb(64, 200, 185);

	public static readonly Color Cyan = Color.FromRgb(100, 210, byte.MaxValue);

	public static readonly Color Yellow = Color.FromRgb(byte.MaxValue, 214, 10);

	public static readonly Color Indigo = Color.FromRgb(94, 92, 230);

	public static readonly Color Amber = Color.FromRgb(byte.MaxValue, 184, 64);

	private static readonly Dictionary<string, Geometry> Filled = MakeFilled();

	private static readonly Dictionary<string, BitmapSource> EmojiImages = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);

	public static bool Motion
	{
		get
		{
			if (CapsuleMotion.AnimationsEnabled)
			{
				return !PowerSaver.IsBatterySaverOn;
			}
			return false;
		}
	}

	public static SolidColorBrush Brush(Color c, byte alpha = byte.MaxValue)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public static FrameworkElement Glyph(string name, double size, Color tint)
	{
		string text = EmojiName(name);
		if (text != null)
		{
			return Emoji(text, size);
		}
		string text2;
		switch (name)
		{
		case "clipboard.check":
			text2 = "clipboard_checkmark";
			break;
		case "speaker.slash":
			text2 = "speaker_off";
			break;
		case "speaker":
		case "speaker.wave.1":
		case "speaker.wave.2":
		case "speaker.wave.3":
			text2 = "speaker_2";
			break;
		case "bolt":
		case "battery.charge":
			text2 = "battery_charge";
			break;
		case "battery.warning":
			text2 = "battery_warning";
			break;
		case "wifi":
			text2 = "wifi_1";
			break;
		case "wifi.slash":
			text2 = "wifi_off";
			break;
		case "headphones":
		case "bluetooth":
			text2 = "bluetooth";
			break;
		case "timer":
		case "alarm":
			text2 = "clock_alarm";
			break;
		case "focus":
			text2 = "target";
			break;
		case "coffee":
			text2 = "drink_coffee";
			break;
		case "drop.fill":
		case "drop":
			text2 = "drop";
			break;
		case "bell":
			text2 = "alert";
			break;
		case "calendar":
			text2 = "calendar_ltr";
			break;
		case "exclamation.triangle":
		case "sparkle":
			text2 = "sparkle";
			break;
		case "download":
			text2 = "arrow_download";
			break;
		case "backup":
			text2 = "box_arrow_up";
			break;
		default:
			text2 = name;
			break;
		}
		if (text2 != null && Filled.TryGetValue(text2, out var value))
		{
			Canvas canvas = new Canvas();
			canvas.Width = size;
			canvas.Height = size;
			canvas.IsHitTestVisible = false;
			canvas.UseLayoutRounding = true;
			Canvas canvas2 = canvas;
			canvas2.Children.Add(new System.Windows.Shapes.Path
			{
				Data = value,
				Fill = Brush(tint),
				RenderTransform = new ScaleTransform(size / 20.0, size / 20.0)
			});
			return canvas2;
		}
		return FluentIcons.Create(text2, size, Brush(tint), filled: true);
	}

	public static ImageSource GlyphImage(string name, Color tint)
	{
		string text = EmojiName(name);
		if (text != null)
		{
			Image image = (Image)Emoji(text, 20.0);
			return image.Source;
		}
		object obj;
		switch (name)
		{
		default:
			obj = name;
			break;
		case "drop.fill":
		case "drop":
			obj = "drop";
			break;
		case "backup":
			obj = "box_arrow_up";
			break;
		case "download":
			obj = "arrow_download";
			break;
		case "bluetooth":
		case "headphones":
			obj = "bluetooth";
			break;
		case "battery.warning":
			obj = "battery_warning";
			break;
		case "battery.charge":
		case "bolt":
			obj = "battery_charge";
			break;
		case "clipboard.check":
			obj = "clipboard_checkmark";
			break;
		}
		string text2 = (string)obj;
		if (text2 == null || !Filled.TryGetValue(text2, out var value))
		{
			return null;
		}
		GeometryDrawing value2 = new GeometryDrawing(Brush(tint), null, value);
		DrawingGroup drawingGroup = new DrawingGroup();
		drawingGroup.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0.0, 0.0, 20.0, 20.0))));
		drawingGroup.Children.Add(value2);
		drawingGroup.Freeze();
		DrawingImage drawingImage = new DrawingImage(drawingGroup);
		drawingImage.Freeze();
		return drawingImage;
	}

	public static Border Icon(string glyph, double circle, Color tint, FrameworkElement custom = null)
	{
		bool flag = glyph == "battery.charge";
		double num = ((circle >= 35.0) ? (0.7 * circle) : (11.0 / 15.0 * circle));
		FrameworkElement icon = custom ?? Glyph(glyph, (circle == 0.0) ? 22.0 : (circle * ((circle >= 35.0) ? 0.7 : (11.0 / 15.0))), tint);
		icon.HorizontalAlignment = HorizontalAlignment.Center;
		icon.VerticalAlignment = VerticalAlignment.Center;
		Border border = new Border();
		border.Width = (flag ? circle : num);
		border.Height = (flag ? circle : num);
		border.CornerRadius = new CornerRadius(flag ? (circle / 2.0) : 0.0);
		border.Background = (flag ? Brush(tint, 46) : Brushes.Transparent);
		border.Child = icon;
		border.VerticalAlignment = VerticalAlignment.Center;
		Border border2 = border;
		border2.Loaded += delegate
		{
			if (Motion)
			{
				ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
				icon.RenderTransformOrigin = new Point(0.5, 0.5);
				icon.RenderTransform = scaleTransform;
				DoubleAnimation animation = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(220.0))
				{
					EasingFunction = CapsuleMotion.EaseOutCubic,
					FillBehavior = FillBehavior.Stop
				};
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			}
		};
		return border2;
	}

	public static FrameworkElement Charging(double size)
	{
		Grid grid = new Grid
		{
			Width = size,
			Height = size
		};
		grid.Children.Add(Glyph("battery.charge", size, Green));
		FrameworkElement frameworkElement = Glyph("battery.charge", size, Green);
		RectangleGeometry rectangleGeometry = new RectangleGeometry(new Rect(0.0, 0.0, size, size));
		TranslateTransform offset = new TranslateTransform(0.0, 0.0);
		rectangleGeometry.Transform = offset;
		frameworkElement.Clip = rectangleGeometry;
		grid.Children.Add(frameworkElement);
		frameworkElement.Loaded += delegate
		{
			if (Motion)
			{
				offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(size, 0.0, TimeSpan.FromMilliseconds(650.0))
				{
					EasingFunction = CapsuleMotion.EaseOutCubic,
					FillBehavior = FillBehavior.Stop
				});
				grid.Children[0].Opacity = 0.28;
			}
		};
		return grid;
	}

	public static FrameworkElement VisitorsDot(double size)
	{
		FrameworkElement dot = Emoji("green_circle", size);
		dot.Loaded += delegate
		{
			if (Motion)
			{
				dot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.4, TimeSpan.FromMilliseconds(1300.0))
				{
					AutoReverse = true,
					RepeatBehavior = RepeatBehavior.Forever
				});
			}
		};
		dot.Unloaded += delegate
		{
			dot.BeginAnimation(UIElement.OpacityProperty, null);
		};
		return dot;
	}

	private static Dictionary<string, Geometry> MakeFilled()
	{
		Dictionary<string, Geometry> dictionary = new Dictionary<string, Geometry>();
		dictionary.Add("alert", Geometry.Parse("F1 M12.4496 16.0016C12.2174 17.1419 11.2089 18 9.99995 18C8.79102 18 7.78251 17.1419 7.55027 16.0016H12.4496ZM9.99759 2C13.1465 2 15.7415 4.33488 15.9821 7.3554L15.9954 7.57762L15.9999 7.80214L15.9989 11.408L16.9527 13.9951L16.9783 14.0796L16.9938 14.1664L16.999 14.2546C16.999 14.5697 16.8033 14.845 16.477 14.9618L16.3635 14.9947L16.249 15.0046H3.7513C3.6628 15.0046 3.575 14.9889 3.49197 14.9583C3.20507 14.8526 3.0163 14.5865 3.00952 14.2422L3.01412 14.1247L3.04756 13.9952L3.99895 11.411L4.00003 7.79281L4.00433 7.56824C4.12695 4.45115 6.77097 2 9.99759 2Z"));
		dictionary.Add("arrow_download", Geometry.Parse("F1 M15.2446 16.4982C15.6589 16.4982 15.9946 16.834 15.9946 17.2482C15.9946 17.6279 15.7125 17.9417 15.3464 17.9914L15.2446 17.9982H4.75C4.33579 17.9982 4 17.6624 4 17.2482C4 16.8685 4.28215 16.5547 4.64823 16.505L4.75 16.4982H15.2446ZM10.0036 2C10.3833 2 10.6971 2.28215 10.7468 2.64823L10.7536 2.75L10.753 12.943L13.7192 9.97242C13.9852 9.70591 14.4019 9.68132 14.6957 9.8989L14.7799 9.97144C15.0464 10.2375 15.071 10.6541 14.8534 10.9479L14.7809 11.0321L10.538 15.2827L10.4674 15.3453L10.3764 15.4039L10.34 15.425L10.2488 15.4629L10.1283 15.493L10.0591 15.501L9.999 15.5029C9.94888 15.5029 9.89801 15.4974 9.84828 15.4868L9.76741 15.4637C9.6714 15.4327 9.58429 15.3824 9.51014 15.3177L5.22056 11.033C4.92746 10.7403 4.92712 10.2654 5.21981 9.97231C5.48588 9.70585 5.90253 9.68135 6.1963 9.89899L6.28047 9.97155L9.253 12.939L9.25362 2.75C9.25362 2.33579 9.58941 2 10.0036 2Z"));
		dictionary.Add("battery_charge", Geometry.Parse("F1 M6.2334 6.17578C5.81383 7.00817 6.41657 7.99587 7.35352 7.9961H8V9.66602C8.00001 11.0629 9.88418 11.5187 10.5176 10.2666L12.7656 5.82031C12.902 5.54975 12.9288 5.2637 12.873 5H16C17.6569 5 19 6.34315 19 8C19.5523 8 20 8.44772 20 9V11C20 11.5523 19.5523 12 19 12L18.9961 12.1543C18.9158 13.7394 17.6051 15 16 15H3C1.34315 15 7.02247e-06 13.6569 0 12V8C6.44266e-08 6.34315 1.34315 5 3 5H6.82812L6.2334 6.17578ZM9.37305 2.18164C9.52978 1.87172 9.99775 1.98294 9.99805 2.33008V5.00098H11.6445C11.8346 5.00098 11.9578 5.20151 11.8721 5.3711L9.62402 9.81641C9.46715 10.1262 9.00001 10.0143 9 9.66699V6.99707H7.35254C7.16285 6.99684 7.03973 6.79635 7.125 6.62696L9.37305 2.18164Z"));
		dictionary.Add("battery_warning", Geometry.Parse("F1 M3.60352 9.55371C3.97207 8.81595 5.02441 8.81544 5.39355 9.55273L8.89355 16.5518C9.22614 17.2171 8.74201 18 7.99805 18H1.00293C0.259073 18 -0.224982 17.2171 0.107422 16.5518L3.60352 9.55371ZM4.49805 15.999C4.22174 15.9991 3.99805 16.2227 3.99805 16.499C3.99809 16.7753 4.22176 16.9989 4.49805 16.999C4.7744 16.999 4.99898 16.7753 4.99902 16.499C4.99902 16.2227 4.77443 15.999 4.49805 15.999ZM16 5C17.6569 5 19 6.34315 19 8C19.5523 8 20 8.44772 20 9V11C20 11.5523 19.5523 12 19 12L18.9961 12.1543C18.9158 13.7394 17.6051 15 16 15H9.23828L6.29199 9.10645C5.55439 7.63121 3.44888 7.63182 2.71191 9.10742L0.466797 13.6025C0.172742 13.1386 2.2938e-08 12.5899 0 12V8C6.44228e-08 6.34324 1.34328 5.00016 3 5H16ZM4.49805 10.9951C4.22174 10.9952 3.99805 11.2188 3.99805 11.4951V14.4971C3.99805 14.7734 4.22174 14.998 4.49805 14.998C4.77443 14.998 4.99902 14.7734 4.99902 14.4971V11.4951C4.99902 11.2188 4.77443 10.9951 4.49805 10.9951Z"));
		dictionary.Add("bluetooth", Geometry.Parse("F1 M9.46301 2.0571C9.74327 1.94101 10.0659 2.00518 10.2804 2.21968L14.2804 6.21968C14.4305 6.36983 14.51 6.57658 14.499 6.78865C14.4881 7.00071 14.3878 7.19819 14.223 7.33209L10.9394 10L14.223 12.6679C14.3878 12.8018 14.4881 12.9993 14.499 13.2114C14.51 13.4234 14.4305 13.6302 14.2804 13.7803L10.2804 17.7803C10.0659 17.9948 9.74327 18.059 9.46301 17.9429C9.18276 17.8268 9.00002 17.5534 9.00002 17.25V11.5757L6.22297 13.8321C5.90149 14.0933 5.42914 14.0444 5.16794 13.723C4.90674 13.4015 4.9556 12.9291 5.27708 12.6679L8.56067 10L5.27708 7.33209C4.9556 7.07089 4.90674 6.59854 5.16794 6.27706C5.42914 5.95559 5.90149 5.90672 6.22297 6.16792L9.00002 8.42428V2.75001C9.00002 2.44666 9.18276 2.17318 9.46301 2.0571ZM10.5 11.5757V15.4393L12.6317 13.3077L10.5 11.5757ZM10.5 8.42428L12.6317 6.69232L10.5 4.56067V8.42428Z"));
		dictionary.Add("box_arrow_up", Geometry.Parse("F1 M17.4212 4.99247L14.3751 6.21088L6.87504 3.21084L8.70013 2.4808C9.53457 2.14702 10.4654 2.14702 11.2999 2.4808L17.0571 4.78369C17.1898 4.83679 17.312 4.90749 17.4212 4.99247ZM10.0001 7.96088L13.0289 6.74939L5.52875 3.74935L2.94291 4.78369C2.81019 4.83678 2.68812 4.90745 2.57894 4.99239L10.0001 7.96088ZM2 6.1764C2 6.06575 2.01215 5.95711 2.03542 5.85202L9.50015 8.83791V12.2047C9.17905 12.9031 9 13.6804 9 14.4995C9 15.3186 9.17905 16.0959 9.50015 16.7943V17.7324C9.22802 17.6931 8.95942 17.6217 8.70013 17.5179L2.94291 15.2151C2.37343 14.9873 2 14.4357 2 13.8223V6.1764ZM18 6.1764V10.2567C17.0491 9.47133 15.8296 8.99951 14.5 8.99951C12.924 8.99951 11.5029 9.66234 10.5001 10.7244V8.83791L17.9646 5.85213C17.9879 5.95719 18 6.06579 18 6.1764ZM14.5 18.9995C16.9853 18.9995 19 16.9848 19 14.4995C19 12.0142 16.9853 9.99951 14.5 9.99951C12.0147 9.99951 10 12.0142 10 14.4995C10 16.9848 12.0147 18.9995 14.5 18.9995ZM14.8514 12.1439L14.8539 12.1464L16.8535 14.146C17.0488 14.3413 17.0488 14.6579 16.8535 14.8531C16.6583 15.0484 16.3417 15.0484 16.1464 14.8531L15 13.7067L15 16.4996C15 16.7757 14.7761 16.9996 14.5 16.9996C14.2238 16.9996 14 16.7757 14 16.4996V13.7067L12.8535 14.8531C12.6583 15.0484 12.3417 15.0484 12.1464 14.8531C11.9512 14.6579 11.9512 14.3413 12.1464 14.146L14.1464 12.146C14.1944 12.0981 14.2496 12.0619 14.3086 12.0375C14.3667 12.0134 14.4303 12 14.497 11.9996L14.5 11.9996L14.503 11.9996C14.5697 12 14.6333 12.0134 14.6914 12.0375C14.7495 12.0615 14.804 12.097 14.8514 12.1439Z"));
		dictionary.Add("calendar_ltr", Geometry.Parse("F1 M5.5 3C4.11929 3 3 4.11929 3 5.5V6H17V5.5C17 4.11929 15.8807 3 14.5 3H5.5ZM17 7H3V14.5C3 15.8807 4.11929 17 5.5 17H14.5C15.8807 17 17 15.8807 17 14.5V7ZM8 10C8 10.5523 7.55228 11 7 11C6.44772 11 6 10.5523 6 10C6 9.44771 6.44772 9 7 9C7.55228 9 8 9.44771 8 10ZM7 14C6.44772 14 6 13.5523 6 13C6 12.4477 6.44772 12 7 12C7.55228 12 8 12.4477 8 13C8 13.5523 7.55228 14 7 14ZM11 10C11 10.5523 10.5523 11 10 11C9.44771 11 9 10.5523 9 10C9 9.44771 9.44771 9 10 9C10.5523 9 11 9.44771 11 10ZM10 14C9.44771 14 9 13.5523 9 13C9 12.4477 9.44771 12 10 12C10.5523 12 11 12.4477 11 13C11 13.5523 10.5523 14 10 14ZM14 10C14 10.5523 13.5523 11 13 11C12.4477 11 12 10.5523 12 10C12 9.44771 12.4477 9 13 9C13.5523 9 14 9.44771 14 10Z"));
		dictionary.Add("clipboard_checkmark", Geometry.Parse("F1 M7.08535 3C7.29127 2.4174 7.84689 2 8.5 2H11.5C12.1531 2 12.7087 2.4174 12.9146 3H14.5C15.3284 3 16 3.67157 16 4.5V9.20703C15.5232 9.07217 15.02 9 14.5 9C11.4624 9 9 11.4624 9 14.5C9 15.8296 9.47182 17.0491 10.2572 18H5.5C4.67157 18 4 17.3284 4 16.5V4.5C4 3.67157 4.67157 3 5.5 3H7.08535ZM8.5 3C8.22386 3 8 3.22386 8 3.5C8 3.77614 8.22386 4 8.5 4H11.5C11.7761 4 12 3.77614 12 3.5C12 3.22386 11.7761 3 11.5 3H8.5ZM19 14.5C19 16.9853 16.9853 19 14.5 19C12.0147 19 10 16.9853 10 14.5C10 12.0147 12.0147 10 14.5 10C16.9853 10 19 12.0147 19 14.5ZM16.8536 12.6464C16.6583 12.4512 16.3417 12.4512 16.1464 12.6464L13.5 15.2929L12.8536 14.6464C12.6583 14.4512 12.3417 14.4512 12.1464 14.6464C11.9512 14.8417 11.9512 15.1583 12.1464 15.3536L13.1464 16.3536C13.3417 16.5488 13.6583 16.5488 13.8536 16.3536L16.8536 13.3536C17.0488 13.1583 17.0488 12.8417 16.8536 12.6464Z"));
		dictionary.Add("clock_alarm", Geometry.Parse("F1 M7.76314 3.36501C7.19218 2.54151 6.24274 2 5.16562 2C3.41353 2 2 3.43067 2 5.18705C2 6.26782 2.53481 7.22381 3.35271 7.79998C3.12386 8.49181 3 9.23144 3 10C3 11.753 3.64441 13.3556 4.70927 14.5836L3.14644 16.1465C2.95118 16.3418 2.95119 16.6584 3.14645 16.8536C3.34172 17.0489 3.6583 17.0489 3.85356 16.8536L5.41638 15.2907C6.64444 16.3556 8.24696 17 10 17C11.7531 17 13.3556 16.3556 14.5837 15.2907L16.1464 16.8534C16.3417 17.0487 16.6583 17.0487 16.8536 16.8534C17.0488 16.6582 17.0488 16.3416 16.8536 16.1463L15.2908 14.5836C16.3556 13.3555 17 11.753 17 10C17 9.22463 16.8739 8.4787 16.6412 7.78159C17.462 7.20829 17.9999 6.25472 17.9999 5.17516C17.9999 3.42268 16.5823 2 14.8311 2C13.7526 2 12.8009 2.53998 12.2292 3.36243C11.529 3.12737 10.7794 3 10 3C9.21778 3 8.46553 3.1283 7.76314 3.36501ZM5.16562 3C5.82326 3 6.41409 3.29611 6.81222 3.76632C5.49483 4.44136 4.41946 5.52174 3.75071 6.84286C3.29098 6.44186 3 5.84915 3 5.18705C3 3.97539 3.97335 3 5.16562 3ZM13.1813 3.76302C13.5797 3.2953 14.1714 3 14.8311 3C16.0278 3 16.9999 3.97273 16.9999 5.17516C16.9999 5.83616 16.7062 6.42841 16.2416 6.82764C15.5712 5.51129 14.4966 4.43524 13.1813 3.76302ZM9.5 6.00012C9.77614 6.00012 10 6.22398 10 6.50012V10.0001H12.5C12.7761 10.0001 13 10.224 13 10.5001C13 10.7763 12.7761 11.0001 12.5 11.0001H9.5C9.22386 11.0001 9 10.7763 9 10.5001V6.50012C9 6.22398 9.22386 6.00012 9.5 6.00012Z"));
		dictionary.Add("drink_coffee", Geometry.Parse("F1 M5 2.5C5 2.22386 4.77614 2 4.5 2C4.22386 2 4 2.22386 4 2.5C4 3.50207 4.68608 4.01543 5.16023 4.37022L5.2 4.4C5.72551 4.79413 6 5.03046 6 5.5C6 5.77614 6.22386 6 6.5 6C6.77614 6 7 5.77614 7 5.5C7 4.49793 6.31392 3.98457 5.83977 3.62978L5.8 3.6C5.27449 3.20587 5 2.96954 5 2.5ZM3.93478 7C3.41852 7 3 7.41852 3 7.93478V12C3 15.3137 5.68629 18 9 18C11.2208 18 13.1599 16.7934 14.1973 15H16C17.6569 15 19 13.6569 19 12C19 10.3431 17.6569 9 16 9H15V7.93478C15 7.41852 14.5815 7 14.0652 7H3.93478ZM16 14H14.6586C14.8797 13.3744 15 12.7013 15 12V10H16C17.1046 10 18 10.8954 18 12C18 13.1046 17.1046 14 16 14ZM7.5 2C7.77614 2 8 2.22386 8 2.5C8 2.96954 8.27449 3.20587 8.8 3.6L8.83977 3.62978C9.31392 3.98457 10 4.49793 10 5.5C10 5.77614 9.77614 6 9.5 6C9.22386 6 9 5.77614 9 5.5C9 5.03046 8.72551 4.79413 8.2 4.4L8.16023 4.37022C7.68608 4.01543 7 3.50207 7 2.5C7 2.22386 7.22386 2 7.5 2ZM11 2.5C11 2.22386 10.7761 2 10.5 2C10.2239 2 10 2.22386 10 2.5C10 3.50207 10.6861 4.01543 11.1602 4.37022L11.2 4.4C11.7255 4.79413 12 5.03046 12 5.5C12 5.77614 12.2239 6 12.5 6C12.7761 6 13 5.77614 13 5.5C13 4.49793 12.3139 3.98457 11.8398 3.62978L11.8 3.6C11.2745 3.20587 11 2.96954 11 2.5Z"));
		dictionary.Add("drop", Geometry.Parse("F1 M9.64645 2.14645C9.84171 1.95118 10.1583 1.95118 10.3536 2.14645C10.8853 2.67821 12.1584 4.20981 13.2998 6.04882C14.4297 7.86917 15.5 10.0985 15.5 12C15.5 13.8438 14.972 15.3521 13.9905 16.4037C13.0058 17.4587 11.6148 18 10 18C8.38524 18 6.99415 17.4587 6.00947 16.4037C5.02801 15.3521 4.5 13.8438 4.5 12C4.5 10.0985 5.5703 7.86917 6.70018 6.04882C7.84163 4.20981 9.11469 2.67821 9.64645 2.14645Z"));
		dictionary.Add("hourglass", Geometry.Parse("F1 M13.69 5.4C13.8891 4.95982 13.9947 4.48309 14 4H6C6.00608 4.48409 6.11516 4.96134 6.32 5.4C6.52759 5.85025 6.83544 6.24705 7.22 6.56L9.04 8.08C9.32073 8.31449 9.54611 8.60817 9.7 8.94C9.85933 9.27057 9.9414 9.63304 9.94 10C9.9414 10.367 9.85933 10.7294 9.7 11.06C9.54611 11.3918 9.32073 11.6855 9.04 11.92L7.22 13.44C6.83544 13.753 6.52759 14.1497 6.32 14.6C6.11516 15.0387 6.00608 15.5159 6 16H14C13.9947 15.5169 13.8891 15.0402 13.69 14.6C13.476 14.1509 13.1652 13.7548 12.78 13.44L11 11.91C10.7234 11.678 10.5014 11.3878 10.35 11.06C10.1733 10.7336 10.0741 10.3709 10.06 10C10.0741 9.6291 10.1733 9.26642 10.35 8.94C10.5014 8.61223 10.7234 8.32198 11 8.09L12.78 6.56C13.1652 6.24524 13.476 5.84909 13.69 5.4Z"));
		dictionary.Add("sparkle", Geometry.Parse("F1 M7.39804 12.8088C7.57428 12.9331 7.78476 12.9997 8.00043 12.9993C8.21633 12.9995 8.42686 12.932 8.60243 12.8063C8.77993 12.6758 8.91464 12.4955 8.98943 12.2883L9.43643 10.9153C9.55086 10.571 9.74391 10.2581 10.0003 10.0014C10.2566 9.74467 10.5693 9.5512 10.9134 9.4363L12.3044 8.9853C12.4564 8.93095 12.5936 8.84214 12.7055 8.72586C12.8174 8.60957 12.9008 8.46896 12.9492 8.31503C12.9977 8.16111 13.0098 7.99806 12.9847 7.83867C12.9596 7.67927 12.8979 7.52786 12.8044 7.3963C12.6703 7.21038 12.4794 7.07314 12.2604 7.0053L10.8854 6.5583C10.5409 6.44407 10.2278 6.25111 9.97087 5.99472C9.71396 5.73833 9.52035 5.42558 9.40543 5.0813L8.95343 3.6933C8.88113 3.491 8.74761 3.31624 8.57143 3.1933C8.43877 3.09957 8.28607 3.03809 8.12548 3.01375C7.96489 2.9894 7.80083 3.00286 7.64636 3.05306C7.49188 3.10326 7.35125 3.18881 7.23564 3.3029C7.12004 3.417 7.03265 3.5565 6.98043 3.7103L6.52343 5.1103C6.40884 5.44513 6.21967 5.74954 5.97022 6.00055C5.72076 6.25157 5.41753 6.44263 5.08343 6.5593L3.69243 7.0073C3.54065 7.0617 3.40352 7.15048 3.29177 7.26669C3.18001 7.3829 3.09666 7.5234 3.04824 7.67719C2.99982 7.83097 2.98764 7.99388 3.01265 8.15315C3.03767 8.31243 3.0992 8.46376 3.19243 8.5953C3.32027 8.77475 3.50105 8.90972 3.70943 8.9813L5.08343 9.4263C5.52354 9.57278 5.90999 9.84713 6.19343 10.2143C6.35585 10.4249 6.4813 10.6616 6.56443 10.9143L7.01643 12.3053C7.08846 12.5086 7.22179 12.6845 7.39804 12.8088ZM13.5353 16.8513C13.6713 16.9475 13.8337 16.9992 14.0003 16.9993C14.1654 16.9994 14.3264 16.9484 14.4613 16.8533C14.6008 16.7548 14.7058 16.6149 14.7613 16.4533L15.0093 15.6913C15.0625 15.5329 15.1515 15.3888 15.2693 15.2703C15.3867 15.1518 15.5307 15.063 15.6893 15.0113L16.4613 14.7593C16.619 14.7048 16.7557 14.6024 16.8523 14.4663C16.9257 14.3633 16.9736 14.2444 16.9921 14.1193C17.0106 13.9942 16.9992 13.8665 16.9588 13.7467C16.9184 13.6268 16.8501 13.5183 16.7597 13.4299C16.6692 13.3415 16.5591 13.2759 16.4383 13.2383L15.6743 12.9893C15.5162 12.9368 15.3724 12.8481 15.2544 12.7305C15.1364 12.6128 15.0473 12.4693 14.9943 12.3113L14.7423 11.5383C14.6886 11.3805 14.586 11.2439 14.4493 11.1483C14.3473 11.0754 14.2295 11.0274 14.1056 11.0084C13.9816 10.9893 13.8549 10.9997 13.7357 11.0386C13.6164 11.0775 13.508 11.1439 13.4192 11.2325C13.3304 11.321 13.2636 11.4292 13.2243 11.5483L12.9773 12.3103C12.925 12.468 12.8375 12.6116 12.7213 12.7303C12.6066 12.8468 12.4667 12.9354 12.3123 12.9893L11.5393 13.2413C11.3803 13.2952 11.2422 13.3978 11.1447 13.5346C11.0472 13.6713 10.9952 13.8353 10.9961 14.0032C10.997 14.1712 11.0507 14.3346 11.1496 14.4703C11.2486 14.606 11.3877 14.7071 11.5473 14.7593L12.3103 15.0063C12.4692 15.0597 12.6136 15.149 12.7323 15.2673C12.8505 15.3856 12.939 15.5302 12.9903 15.6893L13.2433 16.4633C13.2981 16.6198 13.4001 16.7554 13.5353 16.8513Z"));
		dictionary.Add("speaker_2", Geometry.Parse("F1 M12 3.00588C12 2.13286 10.9597 1.67903 10.3198 2.27288L6.4476 5.86626C6.35513 5.95208 6.23364 5.99976 6.10749 5.99976H3.5C2.67157 5.99976 2 6.67134 2 7.49976V12.4998C2 13.3282 2.67157 13.9998 3.5 13.9998H6.10749C6.23364 13.9998 6.35513 14.0474 6.4476 14.1333L10.3198 17.7266C10.9597 18.3205 12 17.8667 12 16.9936V3.00588ZM15.2537 4.6274C15.4594 4.44313 15.7755 4.46047 15.9598 4.66614C18.676 7.69772 18.676 12.3018 15.9598 15.3334C15.7756 15.5391 15.4594 15.5565 15.2538 15.3722C15.0481 15.1879 15.0308 14.8718 15.215 14.6661C17.591 12.0143 17.591 7.98523 15.215 5.33344C15.0307 5.12778 15.0481 4.81167 15.2537 4.6274ZM14.0785 12.933C13.8393 12.7952 13.757 12.4895 13.8948 12.2502C14.671 10.9026 14.7265 9.19139 13.8942 7.7497C13.7561 7.51056 13.838 7.20476 14.0772 7.06669C14.3163 6.92862 14.6221 7.01056 14.7602 7.2497C15.7782 9.01297 15.7084 11.1052 14.7614 12.7493C14.6235 12.9886 14.3178 13.0708 14.0785 12.933Z"));
		dictionary.Add("speaker_off", Geometry.Parse("F1 M2.85355 2.14645C2.65829 1.95118 2.34171 1.95118 2.14645 2.14645C1.95118 2.34171 1.95118 2.65829 2.14645 2.85355L5.29289 5.99999H3.5C2.67157 5.99999 2 6.67157 2 7.49999V12.5C2 13.3284 2.67157 14 3.5 14H6.10749C6.23364 14 6.35513 14.0477 6.4476 14.1335L10.3198 17.7269C10.9597 18.3207 12 17.8669 12 16.9939V12.7071L17.1464 17.8536C17.3417 18.0488 17.6583 18.0488 17.8536 17.8536C18.0488 17.6583 18.0488 17.3417 17.8536 17.1464L15.2893 14.5822C15.2893 14.5822 15.2893 14.5821 15.2893 14.5822L2.85355 2.14645ZM15.9598 4.66637C18.4359 7.42999 18.6548 11.5004 16.6164 14.4951L15.8947 13.7734C17.5639 11.17 17.3373 7.70238 15.215 5.33367C15.0307 5.12801 15.0481 4.8119 15.2537 4.62763C15.4594 4.44336 15.7755 4.4607 15.9598 4.66637ZM14.7602 7.24993C15.7638 8.9883 15.7101 11.0464 14.801 12.6796L14.059 11.9377C14.6743 10.6447 14.6633 9.08222 13.8942 7.74993C13.7561 7.51079 13.838 7.20499 14.0772 7.06692C14.3163 6.92885 14.6221 7.01079 14.7602 7.24993ZM12 3.00611V9.87865L7.24648 5.12513L10.3198 2.27311C10.9597 1.67926 12 2.13309 12 3.00611Z"));
		dictionary.Add("target", Geometry.Parse("F1 M9.99817 11.5001C10.8266 11.5001 11.4982 10.8285 11.4982 10.0001C11.4982 9.17169 10.8266 8.50012 9.99817 8.50012C9.16974 8.50012 8.49817 9.17169 8.49817 10.0001C8.49817 10.8285 9.16974 11.5001 9.99817 11.5001ZM4.99817 10.0001C4.99817 7.23867 7.23675 5.00009 9.99817 5.00009C12.7596 5.00009 14.9982 7.23867 14.9982 10.0001C14.9982 12.7615 12.7596 15.0001 9.99817 15.0001C7.23675 15.0001 4.99817 12.7615 4.99817 10.0001ZM9.99817 6.50009C8.06517 6.50009 6.49817 8.06709 6.49817 10.0001C6.49817 11.9331 8.06517 13.5001 9.99817 13.5001C11.9312 13.5001 13.4982 11.9331 13.4982 10.0001C13.4982 8.06709 11.9312 6.50009 9.99817 6.50009ZM1.99609 9.9971C1.99609 5.57934 5.57739 1.99805 9.99514 1.99805C14.4129 1.99805 17.9942 5.57934 17.9942 9.9971C17.9942 14.4148 14.4129 17.9961 9.99514 17.9961C5.57739 17.9961 1.99609 14.4148 1.99609 9.9971ZM9.99514 3.49805C6.40582 3.49805 3.49609 6.40777 3.49609 9.9971C3.49609 13.5864 6.40582 16.4961 9.99514 16.4961C13.5845 16.4961 16.4942 13.5864 16.4942 9.9971C16.4942 6.40777 13.5845 3.49805 9.99514 3.49805Z"));
		foreach (Geometry value in dictionary.Values)
		{
			value.Freeze();
		}
		return dictionary;
	}

	internal static string EmojiName(string name)
	{
		switch (name)
		{
		case "clipboard.check":
			return "clipboard";
		case "drop":
		case "drop.fill":
			return "water_glass";
		case "bell":
			return "bell";
		case "calendar":
			return "calendar";
		case "timer":
		case "alarm":
			return "alarm_clock";
		case "focus":
			return "bullseye";
		case "coffee":
			return "hot_beverage";
		case "hourglass":
			return "hourglass_not_done";
		case "sparkle":
		case "exclamation.triangle":
			return "sparkles";
		case "download":
			return "package";
		case "backup":
			return "card_file_box";
		case "speaker.slash":
			return "muted_speaker";
		case "speaker.wave.1":
			return "speaker_low_volume";
		case "speaker":
		case "speaker.wave.2":
		case "speaker.wave.3":
			return "speaker_high_volume";
		case "wifi":
			return "antenna_bars_floating";
		case "wifi.slash":
			return "no_signal";
		case "battery.warning":
			return "low_battery";
		case "headphones":
			return "headphone";
		case "mouse":
			return "computer_mouse";
		case "keyboard":
			return "keyboard";
		case "phone":
			return "mobile_phone";
		case "controller":
			return "video_game";
		case "watch":
			return "watch";
		case "visitors":
			return "green_circle";
		default:
			return null;
		}
	}

	internal static FrameworkElement Emoji(string name, double size)
	{
		BitmapSource value;
		lock (EmojiImages)
		{
			if (!EmojiImages.TryGetValue(name, out value))
			{
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ayn.Emoji." + name + "_color.png"))
				{
					if (stream == null)
					{
						throw new InvalidOperationException("Missing Fluent Emoji Color asset: " + name);
					}
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.StreamSource = stream;
					bitmapImage.EndInit();
					bitmapImage.Freeze();
					value = bitmapImage;
				}
				EmojiImages[name] = value;
			}
		}
		Image image = new Image();
		image.Source = value;
		image.Width = size;
		image.Height = size;
		image.Stretch = Stretch.Uniform;
		image.SnapsToDevicePixels = true;
		image.UseLayoutRounding = true;
		image.IsHitTestVisible = false;
		Image image2 = image;
		RenderOptions.SetBitmapScalingMode(image2, BitmapScalingMode.HighQuality);
		AutomationProperties.SetName(image2, "Microsoft Fluent Emoji Color: " + name);
		return image2;
	}

	internal static void Crisp(FrameworkElement text)
	{
		text.UseLayoutRounding = true;
		text.SnapsToDevicePixels = true;
		text.CacheMode = null;
		TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
		TextOptions.SetTextHintingMode(text, TextHintingMode.Fixed);
		TextOptions.SetTextRenderingMode(text, TextRenderingMode.Grayscale);
		Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
	}
}
