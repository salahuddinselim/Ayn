using System;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class EdgeLightLook
{
	public const double CoreWidth = 1.25;

	public const double GlowRadius = 6.0;

	public const double GlowOpacity = 0.45;

	public const double CometTail = 0.18;

	public const double TwinTail = 0.15;

	public const double ChargingFillSeconds = 1.1;

	public const double ChargingHoldSeconds = 0.6;

	public const double FadeInSeconds = 0.18;

	public const double FadeOutSeconds = 0.09;

	public const double EventFadeOutSeconds = 0.6;

	public EdgeLightStyle Style;

	public Color Color = Colors.White;

	public double Intensity = 0.8;

	public string Speed = "Normal";

	public bool Always;

	public static readonly Color ChargingColor = Color.FromRgb(52, 199, 89);

	public static readonly double[][] CometLayers = BuildCometLayers();

	public double CycleSeconds
	{
		get
		{
			int num = ((!(Speed == "Slow")) ? ((!(Speed == "Fast")) ? 1 : 2) : 0);
			return Style switch
			{
				EdgeLightStyle.TwinComets => (new double[3] { 5.2, 3.8, 2.6 })[num], 
				EdgeLightStyle.Breathe => (new double[3] { 5.0, 3.8, 2.8 })[num], 
				_ => (new double[3] { 4.4, 3.2, 2.2 })[num], 
			};
		}
	}

	public double EventRunSeconds => Math.Max(4.0, CycleSeconds * 1.2);

	public static double ChargingTotalSeconds => 1.7000000000000002;

	public string Key => string.Join("|", Style, Color, Intensity.ToString("0.00"), Speed, Always);

	private static double[][] BuildCometLayers()
	{
		double[][] array = new double[11][]
		{
			new double[2] { 0.012, 1.0 },
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		for (int i = 1; i <= 10; i++)
		{
			array[i] = new double[2]
			{
				0.18 * (double)i / 10.0,
				0.2
			};
		}
		return array;
	}

	public static EdgeLightLook FromConfig(NotchConfig cfg)
	{
		EdgeLightLook edgeLightLook = new EdgeLightLook();
		edgeLightLook.Style = ParseStyle(cfg.EdgeLightStyle);
		edgeLightLook.Color = cfg.GetEdgeLightColor();
		edgeLightLook.Intensity = Math.Max(0.0, Math.Min(1.0, cfg.EdgeLightIntensity));
		edgeLightLook.Speed = cfg.EdgeLightSpeed;
		edgeLightLook.Always = cfg.EdgeLightWhen == "Always";
		return edgeLightLook;
	}

	public static EdgeLightStyle ParseStyle(string s)
	{
		if (string.Equals(s, "Twin comets", StringComparison.OrdinalIgnoreCase))
		{
			return EdgeLightStyle.TwinComets;
		}
		if (string.Equals(s, "Breathe", StringComparison.OrdinalIgnoreCase))
		{
			return EdgeLightStyle.Breathe;
		}
		return EdgeLightStyle.Comet;
	}

	public EdgeLightLook With(EdgeLightStyle style)
	{
		EdgeLightLook edgeLightLook = (EdgeLightLook)MemberwiseClone();
		edgeLightLook.Style = style;
		return edgeLightLook;
	}

	public static Color Tint(Color c, double towardsWhite)
	{
		return Color.FromRgb((byte)((double)(int)c.R + (double)(255 - c.R) * towardsWhite), (byte)((double)(int)c.G + (double)(255 - c.G) * towardsWhite), (byte)((double)(int)c.B + (double)(255 - c.B) * towardsWhite));
	}
}
