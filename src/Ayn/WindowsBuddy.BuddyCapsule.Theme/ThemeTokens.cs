using System;
using WindowsBuddy.Liquid;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Theme;

public static class ThemeTokens
{
	private sealed class Listener
	{
		public WeakReference Owner;

		public WeakReference Callback;
	}

	private sealed class OwnerCallbacks
	{
		public readonly List<Action<object>> Items = new List<Action<object>>();
	}

	public const string PureBlackId = "PureBlack";

	public const string EmberDarkId = "EmberDark";

	public const string VividId = "Vivid";

	public const string SilverId = "Silver";

	public static readonly string[] Ids;

	public static readonly Duration Transition;

	public static readonly Color NotchBlack;

	public static readonly SolidColorBrush Bg;

	public static readonly SolidColorBrush Surface;

	public static readonly SolidColorBrush Surface2;

	public static readonly SolidColorBrush Surface3;

	public static readonly SolidColorBrush Overlay;

	public static readonly SolidColorBrush Border;

	public static readonly SolidColorBrush BorderStrong;

	public static readonly SolidColorBrush Text;

	public static readonly SolidColorBrush Text2;

	public static readonly SolidColorBrush Text3;

	public static readonly SolidColorBrush TextDisabled;

	public static readonly SolidColorBrush Icon;

	public static readonly SolidColorBrush IconMuted;

	public static readonly SolidColorBrush Accent;

	public static readonly SolidColorBrush AccentFg;

	public static readonly SolidColorBrush AccentSoft;

	public static readonly SolidColorBrush Track;

	public static readonly SolidColorBrush Bar;

	public static readonly SolidColorBrush Chart1;

	public static readonly SolidColorBrush Chart2;

	public static readonly SolidColorBrush Chart3;

	public static readonly SolidColorBrush Chart4;

	public static readonly SolidColorBrush CScreen;

	public static readonly SolidColorBrush CSystem;

	public static readonly SolidColorBrush CWeather;

	public static readonly SolidColorBrush CFocus;

	public static readonly SolidColorBrush CClipboard;

	public static readonly SolidColorBrush CNotes;

	public static readonly SolidColorBrush Success;

	public static readonly SolidColorBrush Warning;

	public static readonly SolidColorBrush Danger;

	public static readonly SolidColorBrush Info;

	public static readonly SolidColorBrush SuccessSoft;

	public static readonly SolidColorBrush WarningSoft;

	public static readonly SolidColorBrush DangerSoft;

	public static readonly SolidColorBrush InfoSoft;

	public static readonly SolidColorBrush Focus;

	public static readonly SolidColorBrush CardBorder;

	public static readonly SolidColorBrush GlassFill;

	public static readonly SolidColorBrush GlassEdge;

	public static readonly SolidColorBrush GlassSheen;

	public static readonly SolidColorBrush AccentStrong;

	public static readonly SolidColorBrush Inverse;

	public static readonly SolidColorBrush Rule;

	public static readonly SolidColorBrush PanelRule;

	public static readonly SolidColorBrush Selection;

	public static readonly SolidColorBrush ControlFill;

	public static readonly SolidColorBrush ControlFillHover;

	public static readonly SolidColorBrush SegmentThumb;

	public static readonly SolidColorBrush Ring1;

	public static readonly SolidColorBrush Ring2;

	public static readonly SolidColorBrush Ring3;

	public static readonly SolidColorBrush PlayInk;

	public static readonly SolidColorBrush SunInk;

	public static readonly SolidColorBrush TileGlyph;

	public static readonly SolidColorBrush TileGlyphOnLight;

	public static readonly SolidColorBrush DockPill;

	public static readonly SolidColorBrush NavSelectedFill;

	public static readonly SolidColorBrush NavHoverFill;

	public static readonly SolidColorBrush CardHoverFill;

	public static readonly SolidColorBrush CardPressedFill;

	private static ThemePalette _current;

	private static readonly Dictionary<string, ThemePalette> _palettes;

	private static readonly List<KeyValuePair<FieldInfo, SolidColorBrush>> _map;

	private static readonly List<Listener> _listeners;

	private static bool _configHooked;

	private static readonly ConditionalWeakTable<object, OwnerCallbacks> _ownerCallbacks;

	private static HashSet<Brush> _tokenSet;

	public static readonly DropShadowEffect CardElevation;

	public static ThemePalette Current => _current;

	public static bool IsLight => _current.IsLight;

	public static bool UseIconTiles => _current.UseIconTiles;

	public static int ListenerCount
	{
		get
		{
			lock (_listeners)
			{
				_listeners.RemoveAll((Listener l) => !l.Owner.IsAlive);
				return _listeners.Count;
			}
		}
	}

	public static int FrozenTokenCount
	{
		get
		{
			int num = 0;
			foreach (KeyValuePair<FieldInfo, SolidColorBrush> item in _map)
			{
				if (item.Value.IsFrozen)
				{
					num++;
				}
			}
			return num;
		}
	}

	public static string FrozenTokenNames
	{
		get
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<FieldInfo, SolidColorBrush> item in _map)
			{
				if (item.Value.IsFrozen)
				{
					list.Add(item.Key.Name);
				}
			}
			return string.Join(", ", list);
		}
	}

	public static event EventHandler ThemeChanged;

	static ThemeTokens()
	{
		Ids = new string[5] { "PureBlack", "EmberDark", "Vivid", "Silver", "Glass" };
		Transition = new Duration(TimeSpan.FromMilliseconds(150.0));
		NotchBlack = Color.FromRgb(0, 0, 0);
		Bg = B();
		Surface = B();
		Surface2 = B();
		Surface3 = B();
		Overlay = B();
		Border = B();
		BorderStrong = B();
		Text = B();
		Text2 = B();
		Text3 = B();
		TextDisabled = B();
		Icon = B();
		IconMuted = B();
		Accent = B();
		AccentFg = B();
		AccentSoft = B();
		Track = B();
		Bar = B();
		Chart1 = B();
		Chart2 = B();
		Chart3 = B();
		Chart4 = B();
		CScreen = B();
		CSystem = B();
		CWeather = B();
		CFocus = B();
		CClipboard = B();
		CNotes = B();
		Success = B();
		Warning = B();
		Danger = B();
		Info = B();
		SuccessSoft = B();
		WarningSoft = B();
		DangerSoft = B();
		InfoSoft = B();
		Focus = B();
		CardBorder = B();
		GlassFill = B();
		GlassEdge = B();
		GlassSheen = B();
		AccentStrong = B();
		Inverse = B();
		Rule = B();
		PanelRule = B();
		Selection = B();
		ControlFill = B();
		ControlFillHover = B();
		SegmentThumb = B();
		Ring1 = B();
		Ring2 = B();
		Ring3 = B();
		PlayInk = B();
		SunInk = B();
		TileGlyph = B();
		TileGlyphOnLight = B();
		DockPill = B();
		NavSelectedFill = B();
		NavHoverFill = B();
		CardHoverFill = B();
		CardPressedFill = B();
		_palettes = BuildPalettes();
		_map = BuildMap();
		_listeners = new List<Listener>();
		_ownerCallbacks = new ConditionalWeakTable<object, OwnerCallbacks>();
		CardElevation = CreateElevation();
		Paint(_palettes["PureBlack"], animate: false);
		OverlayScroll.Install();
	}

	public static ThemePalette Palette(string id)
	{
		return _palettes[NormalizeId(id)];
	}

	public static string NormalizeId(string id)
	{
		if (!string.IsNullOrEmpty(id))
		{
			string[] ids = Ids;
			foreach (string text in ids)
			{
				if (string.Equals(text, id.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
				{
					return text;
				}
			}
		}
		return "PureBlack";
	}

	public static void Initialize()
	{
		NotchConfig instance = NotchConfig.Instance;
		Apply(instance.AppTheme, animate: false);
		if (_configHooked)
		{
			return;
		}
		_configHooked = true;
		instance.ConfigChanged += delegate
		{
			string id = NotchConfig.Instance.AppTheme;
			if (!(id == _current.Id))
			{
				Dispatcher dispatcher = Bg.Dispatcher;
				if (dispatcher == null || dispatcher.CheckAccess())
				{
					Apply(id, animate: true);
				}
				else
				{
					dispatcher.BeginInvoke((Action)delegate
					{
						Apply(id, animate: true);
					});
				}
			}
		};
	}

	public static void Apply(string id, bool animate)
	{
		OverlayScroll.Install();
		ThemePalette themePalette = _palettes[NormalizeId(id)];
		if (_current == themePalette)
		{
			return;
		}
		Paint(themePalette, animate && CapsuleMotion.AnimationsEnabled);
		EventHandler themeChanged = ThemeTokens.ThemeChanged;
		if (themeChanged != null)
		{
			try
			{
				themeChanged(null, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("ThemeChanged handler failed: " + ex.Message);
			}
		}
		NotifyListeners();
	}

	private static void Paint(ThemePalette p, bool animate)
	{
		_current = p;
		foreach (KeyValuePair<FieldInfo, SolidColorBrush> item in _map)
		{
			SolidColorBrush value = item.Value;
			Color color = (Color)item.Key.GetValue(p);
			if (value.IsFrozen)
			{
				TransactionLogger.Error("Theme token " + item.Key.Name + " was frozen by a caller; it can no longer change colour.");
				continue;
			}
			Color color2 = value.Color;
			value.BeginAnimation(SolidColorBrush.ColorProperty, null);
			value.Color = color;
			if (animate && color2 != color)
			{
				value.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color2, color, Transition)
				{
					FillBehavior = FillBehavior.Stop
				});
			}
		}
	}

	public static void Subscribe<T>(T owner, Action<T> onChange) where T : class
	{
		if (owner == null || onChange == null)
		{
			return;
		}
		lock (_listeners)
		{
			Action<object> action = delegate(object o)
			{
				onChange((T)o);
			};
			_ownerCallbacks.GetValue(owner, (object key) => new OwnerCallbacks()).Items.Add(action);
			_listeners.Add(new Listener
			{
				Owner = new WeakReference(owner),
				Callback = new WeakReference(action)
			});
			if (_listeners.Count % 64 == 0)
			{
				_listeners.RemoveAll((Listener l) => !l.Owner.IsAlive);
			}
		}
	}

	private static void NotifyListeners()
	{
		Listener[] array;
		lock (_listeners)
		{
			_listeners.RemoveAll((Listener l) => !l.Owner.IsAlive);
			array = _listeners.ToArray();
		}
		Listener[] array2 = array;
		foreach (Listener listener in array2)
		{
			object target = listener.Owner.Target;
			if (target != null && listener.Callback.Target is Action<object> action)
			{
				try
				{
					action(target);
				}
				catch (Exception ex)
				{
					TransactionLogger.Warn("Theme listener failed: " + ex.Message);
				}
			}
		}
	}

	public static Color Target(Brush token)
	{
		foreach (KeyValuePair<FieldInfo, SolidColorBrush> item in _map)
		{
			if (object.ReferenceEquals(item.Value, token))
			{
				return (Color)item.Key.GetValue(_current);
			}
		}
		if (!(token is SolidColorBrush solidColorBrush))
		{
			return Colors.Transparent;
		}
		return solidColorBrush.Color;
	}

	public static bool IsToken(Brush b)
	{
		if (b == null)
		{
			return false;
		}
		if (_tokenSet == null)
		{
			HashSet<Brush> hashSet = new HashSet<Brush>();
			foreach (KeyValuePair<FieldInfo, SolidColorBrush> item in _map)
			{
				hashSet.Add(item.Value);
			}
			_tokenSet = hashSet;
		}
		return _tokenSet.Contains(b);
	}

	public static Binding Ref(Brush b)
	{
		Binding binding = new Binding();
		binding.Source = b;
		binding.Mode = BindingMode.OneWay;
		return binding;
	}

	public static T FreezeSafe<T>(T f) where T : Freezable
	{
		if (f == null || f.IsFrozen)
		{
			return f;
		}
		Pen pen = f as Pen;
		if (IsToken(f as Brush) || (pen != null && IsToken(pen.Brush)))
		{
			return f;
		}
		if (f.CanFreeze)
		{
			f.Freeze();
		}
		return f;
	}

	public static SolidColorBrush Copy(SolidColorBrush token)
	{
		return new SolidColorBrush(token.Color);
	}

	private static DropShadowEffect CreateElevation()
	{
		DropShadowEffect dropShadowEffect = new DropShadowEffect();
		dropShadowEffect.Color = Colors.Black;
		dropShadowEffect.Direction = 270.0;
		dropShadowEffect.ShadowDepth = 2.0;
		dropShadowEffect.BlurRadius = 12.0;
		dropShadowEffect.Opacity = 0.07;
		dropShadowEffect.RenderingBias = RenderingBias.Performance;
		DropShadowEffect dropShadowEffect2 = dropShadowEffect;
		dropShadowEffect2.Freeze();
		return dropShadowEffect2;
	}

	public static T Card<T>(T card) where T : Border
	{
		if (card == null)
		{
			return null;
		}
		card.BorderBrush = CardBorder;
		if (card.BorderThickness.Left <= 0.0)
		{
			card.BorderThickness = new Thickness(1.0);
		}
		ApplyElevation(card);
		Subscribe(card, ApplyElevation);
		return card;
	}

	private static void ApplyElevation(Border card)
	{
		card.Effect = (_current.CardShadow ? CardElevation : null);
	}

	public static T Elevate<T>(T surface) where T : UIElement
	{
		if (surface == null)
		{
			return null;
		}
		ApplyElevationTo(surface);
		Subscribe(surface, ApplyElevationTo);
		return surface;
	}

	private static void ApplyElevationTo(UIElement e)
	{
		e.Effect = (_current.CardShadow ? CardElevation : null);
	}

	public static SolidColorBrush Widget(string id)
	{
		switch ((id ?? "").ToLowerInvariant())
		{
		case "screen":
		case "screentime":
			return CScreen;
		case "system":
			return CSystem;
		case "weather":
			return CWeather;
		case "focus":
		case "timer":
			return CFocus;
		case "clipboard":
			return CClipboard;
		case "notes":
			return CNotes;
		default:
			return Accent;
		}
	}

	private static SolidColorBrush B()
	{
		return new SolidColorBrush(Colors.Transparent);
	}

	private static List<KeyValuePair<FieldInfo, SolidColorBrush>> BuildMap()
	{
		List<KeyValuePair<FieldInfo, SolidColorBrush>> list = new List<KeyValuePair<FieldInfo, SolidColorBrush>>();
		FieldInfo[] fields = typeof(ThemePalette).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!(fieldInfo.FieldType != typeof(Color)))
			{
				FieldInfo field = typeof(ThemeTokens).GetField(fieldInfo.Name, BindingFlags.Static | BindingFlags.Public);
				if (!(field == null))
				{
					list.Add(new KeyValuePair<FieldInfo, SolidColorBrush>(fieldInfo, (SolidColorBrush)field.GetValue(null)));
				}
			}
		}
		return list;
	}

	private static Color H(uint argbOrRgb)
	{
		if (argbOrRgb <= 16777215)
		{
			argbOrRgb |= 0xFF000000u;
		}
		return Color.FromArgb((byte)(argbOrRgb >> 24), (byte)(argbOrRgb >> 16), (byte)(argbOrRgb >> 8), (byte)argbOrRgb);
	}

	private static Color Soft(Color c, byte alpha)
	{
		return Color.FromArgb(alpha, c.R, c.G, c.B);
	}

	private static Dictionary<string, ThemePalette> BuildPalettes()
	{
		ThemePalette themePalette = new ThemePalette();
		themePalette.Id = "PureBlack";
		themePalette.DisplayName = "Pure Black";
		themePalette.Bg = H(0u);
		themePalette.Surface = Color.FromArgb(214, 14, 14, 15);
		themePalette.Surface2 = Color.FromArgb(190, 22, 22, 24);
		themePalette.Surface3 = Color.FromArgb(176, 31, 31, 34);
		themePalette.Overlay = H(2566914048u);
		themePalette.Border = H(268435455u);
		themePalette.BorderStrong = H(536870911u);
		themePalette.Text = H(16777215u);
		themePalette.Text2 = H(10592678u);
		themePalette.Text3 = H(9079439u);
		themePalette.TextDisabled = H(4868685u);
		themePalette.Icon = H(15066599u);
		themePalette.IconMuted = H(9342611u);
		themePalette.Accent = H(16777215u);
		themePalette.AccentFg = H(0u);
		themePalette.AccentSoft = H(452984831u);
		themePalette.Track = H(520093695u);
		themePalette.Bar = H(620756991u);
		themePalette.Chart1 = H(16777215u);
		themePalette.Chart2 = H(10592678u);
		themePalette.Chart3 = H(7237235u);
		themePalette.Chart4 = H(3815996u);
		themePalette.Focus = H(16777215u);
		themePalette.Success = H(3187820u);
		themePalette.Warning = H(15239436u);
		themePalette.Danger = H(15026253u);
		themePalette.Info = H(3112182u);
		ThemePalette themePalette2 = themePalette;
		PointWidgetsAtAccent(themePalette2);
		SoftFunctional(themePalette2);
		ThemePalette themePalette3 = themePalette2.Clone();
		themePalette3.Id = "EmberDark";
		themePalette3.DisplayName = "Ember Dark";
		themePalette3.Accent = H(16734751u);
		themePalette3.AccentFg = H(16777215u);
		themePalette3.AccentSoft = H(620714527u);
		themePalette3.Chart1 = H(16734751u);
		themePalette3.Focus = H(16734751u);
		PointWidgetsAtAccent(themePalette3);
		ThemePalette themePalette4 = themePalette2.Clone();
		themePalette4.Id = "Vivid";
		themePalette4.DisplayName = "Vivid";
		themePalette4.UseIconTiles = false;
		themePalette4.Surface = Color.FromArgb(214, 13, 13, 15);
		themePalette4.Accent = H(3112182u);
		themePalette4.AccentFg = H(16777215u);
		themePalette4.AccentSoft = H(607091958u);
		themePalette4.Focus = H(3112182u);
		PointWidgetsAtAccent(themePalette4);
		themePalette4.Chart1 = H(3112182u);
		themePalette4.Chart2 = H(3055210u);
		themePalette4.Chart3 = H(15239436u);
		themePalette4.Chart4 = H(5987286u);
		ThemePalette themePalette5 = new ThemePalette();
		themePalette5.Id = "Silver";
		themePalette5.DisplayName = "Silver";
		themePalette5.IsLight = true;
		themePalette5.Bg = H(15921908u);
		themePalette5.Surface = Color.FromArgb(214, 255, 255, 255);
		themePalette5.Surface2 = Color.FromArgb(190, 246, 246, 248);
		themePalette5.Surface3 = Color.FromArgb(180, 236, 236, 239);
		themePalette5.Overlay = H(1073741824u);
		themePalette5.Border = H(301989888u);
		themePalette5.BorderStrong = H(603979776u);
		themePalette5.Text = H(1907999u);
		themePalette5.Text2 = H(6184547u);
		themePalette5.Text3 = H(6842477u);
		themePalette5.TextDisabled = H(11579573u);
		themePalette5.Icon = H(1907999u);
		themePalette5.IconMuted = H(7566200u);
		themePalette5.Accent = H(1907999u);
		themePalette5.AccentFg = H(16777215u);
		themePalette5.AccentSoft = H(251658240u);
		themePalette5.Track = H(436207616u);
		themePalette5.Bar = H(385875968u);
		themePalette5.Chart1 = H(1907999u);
		themePalette5.Chart2 = H(6184547u);
		themePalette5.Chart3 = H(10592678u);
		themePalette5.Chart4 = H(13750742u);
		themePalette5.Focus = H(1907999u);
		themePalette5.Success = H(2067029u);
		themePalette5.Warning = H(12083712u);
		themePalette5.Danger = H(13710392u);
		themePalette5.Info = H(681686u);
		ThemePalette themePalette6 = themePalette5;
		themePalette6.Rule = H(301989888u);
		themePalette6.Border = Color.FromArgb(0, 0, 0, 0);
		PointWidgetsAtAccent(themePalette6);
		SoftFunctional(themePalette6);
		ThemePalette glass = new ThemePalette();
		glass.Id = "Glass";
		glass.DisplayName = "Glass";
		glass.IsLight = true;
		glass.Bg = Color.FromRgb(238, 232, 222);
		glass.Surface = Color.FromArgb(150, 255, 252, 246);
		glass.Surface2 = Color.FromArgb(105, 255, 250, 242);
		glass.Surface3 = Color.FromArgb(135, 232, 220, 204);
		glass.Overlay = Color.FromArgb(120, 60, 52, 44);
		glass.Border = Color.FromArgb(120, 255, 255, 255);
		glass.BorderStrong = Color.FromArgb(170, 255, 255, 255);
		glass.Text = Color.FromRgb(46, 42, 38);
		glass.Text2 = Color.FromRgb(104, 95, 86);
		glass.Text3 = Color.FromRgb(134, 124, 112);
		glass.TextDisabled = Color.FromRgb(170, 162, 150);
		glass.Icon = Color.FromRgb(64, 56, 48);
		glass.IconMuted = Color.FromRgb(132, 122, 110);
		glass.Accent = Color.FromRgb(176, 126, 82);
		glass.AccentFg = Colors.White;
		glass.AccentSoft = Color.FromArgb(60, 176, 126, 82);
		glass.Track = Color.FromArgb(80, 120, 108, 94);
		glass.Bar = Color.FromArgb(190, 176, 126, 82);
		glass.Chart1 = Color.FromRgb(176, 126, 82);
		glass.Chart2 = Color.FromRgb(120, 150, 130);
		glass.Chart3 = Color.FromRgb(198, 152, 118);
		glass.Chart4 = Color.FromRgb(140, 130, 165);
		glass.Focus = Color.FromRgb(176, 126, 82);
		glass.Success = Color.FromRgb(70, 140, 100);
		glass.Warning = Color.FromRgb(190, 140, 60);
		glass.Danger = Color.FromRgb(190, 80, 75);
		glass.Info = Color.FromRgb(80, 130, 180);
		glass.Rule = Color.FromArgb(70, 120, 108, 94);
		glass.CardShadow = true;
		PointWidgetsAtAccent(glass);
		SoftFunctional(glass);

		Dictionary<string, ThemePalette> dictionary = new Dictionary<string, ThemePalette>(StringComparer.OrdinalIgnoreCase);
		ThemePalette[] array = new ThemePalette[5] { themePalette2, themePalette3, themePalette4, themePalette6, glass };
		foreach (ThemePalette themePalette7 in array)
		{
			dictionary[themePalette7.Id] = themePalette7.Finish();
		}
		return dictionary;
	}

	private static void PointWidgetsAtAccent(ThemePalette p)
	{
		p.CScreen = (p.CSystem = (p.CWeather = (p.CFocus = (p.CClipboard = (p.CNotes = p.Accent)))));
	}

	private static void SoftFunctional(ThemePalette p)
	{
		p.SuccessSoft = Soft(p.Success, 36);
		p.WarningSoft = Soft(p.Warning, 36);
		p.DangerSoft = Soft(p.Danger, 36);
		p.InfoSoft = Soft(p.Info, 36);
	}

	public static double Luminance(Color c)
	{
		Func<byte, double> func = delegate(byte v)
		{
			double num = (double)(int)v / 255.0;
			return (!(num <= 0.03928)) ? Math.Pow((num + 0.055) / 1.055, 2.4) : (num / 12.92);
		};
		return 0.2126 * func(c.R) + 0.7152 * func(c.G) + 0.0722 * func(c.B);
	}

	public static Color Over(Color fg, Color bg)
	{
		double num = (double)(int)fg.A / 255.0;
		return Color.FromRgb((byte)Math.Round((double)(int)fg.R * num + (double)(int)bg.R * (1.0 - num)), (byte)Math.Round((double)(int)fg.G * num + (double)(int)bg.G * (1.0 - num)), (byte)Math.Round((double)(int)fg.B * num + (double)(int)bg.B * (1.0 - num)));
	}

	public static double Contrast(Color fg, Color bg)
	{
		Color c = Over(fg, bg);
		double num = Luminance(c);
		double num2 = Luminance(bg);
		if (num < num2)
		{
			double num3 = num;
			num = num2;
			num2 = num3;
		}
		return (num + 0.05) / (num2 + 0.05);
	}

	public static int RunContrastQa(Action<string> log)
	{
		int num = 0;
		string[] ids = Ids;
		foreach (string key in ids)
		{
			ThemePalette themePalette = _palettes[key];
			log("== " + themePalette.DisplayName);
			Tuple<string, Color>[] array = new Tuple<string, Color>[4]
			{
				Tuple.Create("bg", themePalette.Bg),
				Tuple.Create("surface", themePalette.Surface),
				Tuple.Create("surface2", themePalette.Surface2),
				Tuple.Create("surface3", themePalette.Surface3)
			};
			Tuple<string, Color>[] array2 = array;
			foreach (Tuple<string, Color> tuple in array2)
			{
				num += Check(log, "text", themePalette.Text, tuple.Item1, tuple.Item2, 4.5);
				num += Check(log, "text2", themePalette.Text2, tuple.Item1, tuple.Item2, 4.5);
				num += Check(log, "text3", themePalette.Text3, tuple.Item1, tuple.Item2, 4.5);
				num += Check(log, "icon", themePalette.Icon, tuple.Item1, tuple.Item2, 3.0);
				num += Check(log, "iconMuted", themePalette.IconMuted, tuple.Item1, tuple.Item2, 3.0);
				num += Check(log, "accent", themePalette.Accent, tuple.Item1, tuple.Item2, 3.0);
				num += Check(log, "focus", themePalette.Focus, tuple.Item1, tuple.Item2, 3.0);
				Tuple<string, Color>[] array3 = new Tuple<string, Color>[4]
				{
					Tuple.Create("success", themePalette.Success),
					Tuple.Create("warning", themePalette.Warning),
					Tuple.Create("danger", themePalette.Danger),
					Tuple.Create("info", themePalette.Info)
				};
				foreach (Tuple<string, Color> tuple2 in array3)
				{
					num += Check(log, tuple2.Item1, tuple2.Item2, tuple.Item1, tuple.Item2, 3.0);
				}
			}
			num += Check(log, "accentFg glyph", themePalette.AccentFg, "accent", themePalette.Accent, 3.0);
			num += Check(log, "accentFg text", themePalette.AccentFg, "accentStrong", themePalette.AccentStrong, 4.5);
			num += Check(log, "text", themePalette.Text, "accentSoft/surface", Over(themePalette.AccentSoft, themePalette.Surface), 4.5);
			num += Check(log, "accent", themePalette.Accent, "accentSoft/surface", Over(themePalette.AccentSoft, themePalette.Surface), 3.0);
			num += Check(log, "ring1", themePalette.Ring1, "track/surface", Over(themePalette.Track, themePalette.Surface), 3.0);
			num += Check(log, "playInk", themePalette.PlayInk, "surface", themePalette.Surface, 3.0);
			if (themePalette.UseIconTiles)
			{
				Tuple<string, Color>[] array4 = new Tuple<string, Color>[5]
				{
					Tuple.Create("cScreen", themePalette.CScreen),
					Tuple.Create("cSystem", themePalette.CSystem),
					Tuple.Create("cWeather", themePalette.CWeather),
					Tuple.Create("cFocus", themePalette.CFocus),
					Tuple.Create("cClipboard", themePalette.CClipboard)
				};
				foreach (Tuple<string, Color> tuple3 in array4)
				{
					num += Check(log, "tileGlyph", themePalette.TileGlyph, tuple3.Item1, tuple3.Item2, 3.0);
				}
				num += Check(log, "tileGlyphOnLight", themePalette.TileGlyphOnLight, "cNotes", themePalette.CNotes, 3.0);
			}
		}
		log((num == 0) ? "THEME-CONTRAST-QA PASSED" : ("THEME-CONTRAST-QA FAILED: " + num));
		return num;
	}

	private static int Check(Action<string> log, string fgName, Color fg, string bgName, Color bg, double min)
	{
		double num = Contrast(fg, bg);
		bool flag = num + 1E-09 >= min;
		log(string.Format("  {0,-4} {1,-16} on {2,-20} {3,5:F2} (min {4})", flag ? "ok" : "FAIL", fgName, bgName, num, min));
		if (!flag)
		{
			return 1;
		}
		return 0;
	}
}
